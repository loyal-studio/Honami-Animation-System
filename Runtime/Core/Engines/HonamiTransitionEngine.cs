using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiTransitionEngine
    {
        public static void UpdateTransitions(HonamiAnimator anim)
        {
            EvaluateTransitions(anim);
            HandleTransitions(anim);
        }

        private static void EvaluateTransitions(HonamiAnimator anim)
        {
            if (anim.controller == null) return;

            for (int layer = 0; layer < anim._layerStates.Length; layer++)
            {
                if (anim._layerStates[layer].IsLayerPaused) continue;
                int currentIdx = anim._layerStates[layer].CurrentStateIndex;
                bool isTransitioning = anim._layerStates[layer].PreviousStateIndex != -1;
                bool hasCurrent = currentIdx != -1;
                bool isCurrentExit = hasCurrent && anim._runtimeStates[currentIdx].node is { IsExit: true };

                if (EvaluateAnyStateTransitions(anim, layer, currentIdx, isTransitioning, isCurrentExit)) continue;

                bool interrupted = false;
                if (isTransitioning)
                {
                    int prevIdx = anim._layerStates[layer].PreviousStateIndex;
                    if (prevIdx != -1 && prevIdx != anim.TransientPortIndex)
                    {
                        bool isPrevExit = anim._runtimeStates[prevIdx].node is { IsExit: true };
                        if (!isPrevExit)
                        {
                            if (EvaluateCurrentStateTransitions(anim, layer, prevIdx, true, false))
                                interrupted = true;
                        }
                    }
                }

                if (!interrupted && hasCurrent && !isCurrentExit)
                    EvaluateCurrentStateTransitions(anim, layer, currentIdx, isTransitioning, isCurrentExit);
            }
        }

        private static bool EvaluateAnyStateTransitions(HonamiAnimator anim, int layer, int currentIdx, bool isTransitioning, bool isCurrentExit)
        {
            List<int> layerAnyStates = (anim._anyStateIndicesByLayer != null && layer < anim._anyStateIndicesByLayer.Length)
                ? anim._anyStateIndicesByLayer[layer]
                : null;

            int anyCount = layerAnyStates?.Count ?? 0;
            for (int i = 0; i < anyCount; i++)
            {
                int anyStateIdx = layerAnyStates[i];
                HonamiState anyState = anim._runtimeStates[anyStateIdx];

                HonamiNodeBase globalNode = anim.controller.GetActiveNode(anyState);
                if (globalNode == null) continue;

                HonamiNodeRuntime globalRuntime = anim.GetNodeRuntime(anyStateIdx);
                if (!globalNode.CanFireGlobal(globalRuntime)) continue;

                IReadOnlyList<HonamiTransition> anyTransitions = anim.controller.GetTransitions(anyState);
                if (anyTransitions == null || anyTransitions.Count == 0) continue;

                if (!globalNode.SelectGlobalTransition(anim, layer, currentIdx, globalRuntime, anyTransitions, out int onlyTransitionIdx, out bool forceConditionsMet))
                    continue;

                bool forceRestart = globalNode.ForcesRestartOnGlobalFire;

                if (!CheckTransitions(anim, anyTransitions, layer, currentIdx, isTransitioning, forceRestart, isCurrentExit, anyStateIdx, onlyTransitionIdx, forceConditionsMet)) continue;

                globalNode.OnGlobalFired(globalRuntime, anyTransitions.Count);

                return true;
            }

            return false;
        }

        private static bool EvaluateCurrentStateTransitions(HonamiAnimator anim, int layer, int currentIdx, bool isTransitioning, bool isCurrentExit)
        {
            HonamiState currentState = anim._runtimeStates[currentIdx];
            IReadOnlyList<HonamiTransition> currentTransitions = anim.controller.GetTransitions(currentState);
            if (currentTransitions?.Count > 0)
                return CheckTransitions(anim, currentTransitions, layer, currentIdx, isTransitioning, false, isCurrentExit);
            return false;
        }

        private static bool CheckTransitions(HonamiAnimator anim, IReadOnlyList<HonamiTransition> transitions, int layer, int currentIdx, bool isTransitioning, bool forceRestart, bool isCurrentExit = false, int pulseStateIdx = -1, int onlyTransitionIdx = -1, bool forceConditionsMet = false)
        {
            if (transitions == null || transitions.Count == 0) return false;

            int count = transitions.Count;
            int start = onlyTransitionIdx >= 0 ? Mathf.Min(onlyTransitionIdx, count - 1) : 0;
            if (onlyTransitionIdx >= 0) count = start + 1;
            for (int i = start; i < count; i++)
            {
                var tr = transitions[i];
                if (tr == null) continue;

                bool conditionsMet = true;
                anim._tentativeBuffer.Clear();

                if (!forceConditionsMet)
                {
                    if (tr.conditions != null && tr.conditions.Count > 0)
                    {
                        int cCount = tr.conditions.Count;
                        for (int ci = 0; ci < cCount; ci++)
                        {
                            if (!anim.EvaluateConditionTentative(tr.conditions[ci], anim._tentativeBuffer))
                            {
                                conditionsMet = false;
                                break;
                            }
                        }
                    }
                    else if (!tr.hasExitTime)
                    {
                        continue;
                    }
                }

                if (!conditionsMet) continue;

                if (tr.hasExitTime && currentIdx >= 0)
                {
                    var playable = anim._layerMixers[layer].GetInput(currentIdx);
                    if (playable.IsValid())
                    {
                        float unscaledDuration = HonamiStateEvaluator.GetUnscaledStateDuration(
                            anim.controller, anim._runtimeStates[currentIdx], currentIdx, 
                            anim.GetNodeRuntime(currentIdx), anim.GetStateBlendParam(anim._runtimeStates[currentIdx]));
                        if (unscaledDuration > 0)
                        {
                            double rawTime = playable.GetTime();
                            float normalizedTime = (float)(rawTime / unscaledDuration);
                            var st = anim._runtimeStates[currentIdx];
                            // Not clamped to 1: exit time above 1 acts as a cooldown measured in state durations.
                            float progress = Mathf.Max(0f, st.isReversed ? (1f - normalizedTime) : normalizedTime);
                            if (progress < tr.exitTime) continue;
                        }
                    }
                }

                if (!tr.hasTargetGuid) continue;
                if (!anim._stateGuidToIndex.TryGetValue(tr.targetStateGuidHash, out int targetIdxResolved)) continue;

                float finalDuration = tr.duration;
                if (tr.adaptiveDuration && currentIdx >= 0)
                {
                    var playable = anim._layerMixers[layer].GetInput(currentIdx);
                    if (playable.IsValid())
                    {
                        float unscaledDuration = HonamiStateEvaluator.GetUnscaledStateDuration(
                            anim.controller, anim._runtimeStates[currentIdx], currentIdx, 
                            anim.GetNodeRuntime(currentIdx), anim.GetStateBlendParam(anim._runtimeStates[currentIdx]));
                        if (unscaledDuration > 0)
                        {
                            float normalizedTime = (float)(playable.GetTime() / unscaledDuration);
                            float remaining = Mathf.Clamp01(1f - normalizedTime);
                            finalDuration = tr.duration * remaining * tr.smartScaleFactor;
                        }
                    }
                }

                if (currentIdx == -1 && !tr.hasExitTime && (tr.conditions == null || tr.conditions.Count == 0))
                    continue;

                if (anim._bakedPortalExits.TryGetValue(tr, out var portalExit))
                {
                    if (anim.EvaluatePortalRecursive(portalExit, layer, currentIdx, forceRestart, tr, anim._tentativeBuffer))
                    {
                        if (pulseStateIdx >= 0)
                            anim.FireGlobalStatePulse(pulseStateIdx, layer, targetIdxResolved);
                        return true;
                    }
                    continue;
                }

                var targetState = anim._runtimeStates[targetIdxResolved];
                if (targetState?.node == null || targetState.node.IsVirtual) continue;
 
                bool isSelfTransition = (targetIdxResolved == currentIdx);
                bool canInterrupt = forceRestart || isSelfTransition || isCurrentExit || tr.canInterruptTransition || tr.priority > anim._layerStates[layer].CurrentTransitionPriority;

                if (isTransitioning)
                {
                    if (!canInterrupt) continue;
                    if (targetIdxResolved == anim._layerStates[layer].CurrentStateIndex && !isSelfTransition) continue;
                }

                if (isSelfTransition)
                {
                    if (!tr.canTransitionToSelf && !forceRestart) continue;

                    if (pulseStateIdx >= 0)
                        anim.FireGlobalStatePulse(pulseStateIdx, layer, targetIdxResolved);

                    anim._params.CommitTriggers(anim._tentativeBuffer);
                    anim.ApplyTransitionAssignments(tr);

                    if (tr.sacrificeExisting) anim.Stop(layer);

                    anim.PlayStateByGuidWithPriority(targetState.guid, finalDuration, layer, true,
                        tr.useCurve ? tr.curve : null,
                        tr.destinationStartTime,
                        tr.type == HonamiTransitionType.Victim ? tr.priority : 0,
                        tr.type == HonamiTransitionType.Victim ? tr.victimMode : HonamiVictimMode.None,
                        tr.type == HonamiTransitionType.Victim ? tr.sacrificeSpeedMultiplier : 1f,
                        tr.type == HonamiTransitionType.Victim ? tr.acceleratedWeightDrop : false,
                        (tr.type == HonamiTransitionType.Victim && tr.useCustomVictimCurve) ? tr.victimWeightCurve : null);

                    HonamiPlaybackEngine.ApplyTransitionFreeze(anim, layer, tr.freezeMode);
                    return true;
                }

                if (pulseStateIdx >= 0)
                    anim.FireGlobalStatePulse(pulseStateIdx, layer, targetIdxResolved);

                anim._params.CommitTriggers(anim._tentativeBuffer);
                anim.ApplyTransitionAssignments(tr);

                if (tr.sacrificeExisting) anim.Stop(layer);

                anim.PlayStateByGuidWithPriority(targetState.guid, finalDuration, layer, forceRestart,
                    tr.useCurve ? tr.curve : null,
                    tr.destinationStartTime,
                    tr.type == HonamiTransitionType.Victim ? tr.priority : 0,
                    tr.type == HonamiTransitionType.Victim ? tr.victimMode : HonamiVictimMode.None,
                    tr.type == HonamiTransitionType.Victim ? tr.sacrificeSpeedMultiplier : 1f,
                    tr.type == HonamiTransitionType.Victim ? tr.acceleratedWeightDrop : false,
                    (tr.type == HonamiTransitionType.Victim && tr.useCustomVictimCurve) ? tr.victimWeightCurve : null);

                HonamiPlaybackEngine.ApplyTransitionFreeze(anim, layer, tr.freezeMode);
                return true;
            }
            return false;
        }

        private static void HandleTransitions(HonamiAnimator anim)
        {
            for (int layer = 0; layer < anim._layerMixers.Count; layer++)
            {
                if (anim._layerStates[layer].IsLayerPaused || anim._layerStates[layer].TransitionDuration <= 0.0) continue;

                anim._layerStates[layer].TransitionTime += anim._cachedDeltaTime;
                float t = Mathf.Clamp01((float)(anim._layerStates[layer].TransitionTime / anim._layerStates[layer].TransitionDuration));

                float weightT = anim._activeTransitionCurve[layer] != null
                    ? anim._activeTransitionCurve[layer].Evaluate(t)
                    : t;

                int currIdx = anim._layerStates[layer].CurrentStateIndex;
                var layerMixer = anim._layerMixers[layer];
                if (currIdx < 0 || currIdx >= anim._activeStatesCount) continue;

                float currTargetWeight = anim._runtimeStates[currIdx].weight;
                if (currTargetWeight < 0.01f) continue;

                int portCount = layerMixer.GetInputCount();
                int prevIdx = anim._layerStates[layer].PreviousStateIndex;
                bool isPrevExit = prevIdx == -1 || (prevIdx != anim.TransientPortIndex && anim._runtimeStates[prevIdx].node is { IsExit: true });
                bool isCurrExit = anim._runtimeStates[currIdx].node is { IsExit: true };

                float startLW = anim._layerStates[layer].TransitionStartLayerWeight;
                if (prevIdx != -1 && isPrevExit && !isCurrExit && startLW > 0.01f)
                {
                    float totalLayerSnapshotWeight = 0f;
                    for (int i = 0; i < portCount; i++) totalLayerSnapshotWeight += anim._portStates[layer][i].WeightSnapshot;
                    if (totalLayerSnapshotWeight > 0.01f) isPrevExit = false;
                }

                bool isLayerTransition = isPrevExit || isCurrExit || startLW < 0.01f;

                anim._layerStates[layer].TransitionWeight = weightT;

                if (anim._layerMixer.IsValid())
                {
                    float configWeight = (layer < anim.controller.ActiveLayers.Count) ? anim.controller.ActiveLayers[layer].weight : (layer == 0 ? 1f : 0f);
                    float targetLW = isCurrExit ? 0f : configWeight;
                    anim._layerMixer.SetInputWeight(layer, Mathf.Lerp(startLW, targetLW, weightT));
                }

                float totalW = 0f;
                var victimMode = anim._layerStates[layer].VictimMode;
                float victimSpeed = anim._layerStates[layer].VictimSpeedMultiplier;
                bool accelWeight = anim._layerStates[layer].AcceleratedWeightDrop;
                var activeVCurve = anim._activeVictimCurve[layer];

                for (int i = 0; i < portCount; i++)
                {
                    if (isLayerTransition)
                    {
                        if (isPrevExit)
                            layerMixer.SetInputWeight(i, (i == currIdx) ? currTargetWeight : 0f);
                        else
                            layerMixer.SetInputWeight(i, anim._portStates[layer][i].WeightSnapshot);
                    }
                    else
                    {
                        float startW = anim._portStates[layer][i].WeightSnapshot;
                        float targetW = (i == currIdx) ? currTargetWeight : 0f;
                        float finalWeight;

                        if (i == prevIdx && victimMode == HonamiVictimMode.Source)
                        {
                            if (victimSpeed != 1f)
                            {
                                var vPlayable = layerMixer.GetInput(i);
                                if (vPlayable.IsValid())
                                {
                                    int realVIdx = (i == anim.TransientPortIndex) ? anim._layerStates[layer].TransientStateIndex : i;
                                    float bSpd = anim._runtimeStates[realVIdx].speed;
                                    if (anim._runtimeStates[realVIdx].isReversed) bSpd = -bSpd;
                                    vPlayable.SetSpeed(bSpd * victimSpeed);
                                }
                            }

                            float factor;
                            if (activeVCurve != null) factor = 1f - activeVCurve.Evaluate(t);
                            else if (accelWeight)    factor = weightT * weightT;
                            else                     factor = weightT;

                            finalWeight = Mathf.Lerp(startW, targetW, factor);
                        }
                        else
                        {
                            finalWeight = Mathf.Lerp(startW, targetW, weightT);
                        }

                        layerMixer.SetInputWeight(i, finalWeight);
                        totalW += finalWeight;
                    }
                }

                // Normalize only for non-layer transitions to fix the scale issue
                if (!isLayerTransition && totalW > 0.001f && Mathf.Abs(totalW - 1f) > 0.001f)
                {
                    for (int i = 0; i < portCount; i++)
                        layerMixer.SetInputWeight(i, layerMixer.GetInputWeight(i) / totalW);
                }

                if (t >= 1f)
                {
                    if (anim._layerStates[layer].DestinationFrozen)
                    {
                        anim._layerStates[layer].DestinationFrozen = false;
                        var frozenPlayable = layerMixer.GetInput(currIdx);
                        if (frozenPlayable.IsValid())
                        {
                            var frozenState = anim._runtimeStates[currIdx];
                            frozenPlayable.SetSpeed(frozenState.isReversed ? -frozenState.speed : frozenState.speed);
                        }
                    }

                    anim._layerStates[layer].SourceFrozen = false;

                    for (int i = 0; i < portCount; i++)
                    {
                        if (i == currIdx && !isCurrExit) continue;
                        if (layerMixer.GetInputWeight(i) > 0.0001f || (prevIdx != -1 && i == prevIdx) || i == anim.TransientPortIndex)
                        {
                            layerMixer.SetInputWeight(i, 0f);
                            if (i == anim.TransientPortIndex) anim.ClearTransientPort(layer);
                            else if (i != currIdx) anim.ResetTime(i, layer);
                        }
                    }
                    if (isCurrExit)
                    {
                        anim.CompleteExitState(currIdx, layer);
                        anim._layerStates[layer].CurrentStateIndex = -1;
                    }
                    anim._layerStates[layer].PreviousStateIndex = -1;
                    anim._layerStates[layer].TransitionDuration = 0.0;
                    anim._layerStates[layer].TransitionTime = 0.0;
                    anim._layerStates[layer].TransitionWeight = 1.0f;
                    anim._layerStates[layer].CurrentTransitionPriority = 0;
                    anim._layerStates[layer].VictimMode = HonamiVictimMode.None;
                    anim._activeTransitionCurve[layer] = null;
                    anim._activeVictimCurve[layer] = null;
                }
            }
        }
    }
}
