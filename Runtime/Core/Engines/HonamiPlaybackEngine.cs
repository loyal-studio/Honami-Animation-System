using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Common;
using HonamiAnimationSystem.Runtime.Events;
using Unity.Collections;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Handles runtime state playback, transition setup, playable timing, and per-frame node updates for <see cref="HonamiAnimator"/>.
    /// </summary>
    public static class HonamiPlaybackEngine
    {
        private struct SyncedRandomPick
        {
            public int frame;
            public int pickedIndex;
        }

        private static readonly Dictionary<int, SyncedRandomPick> _syncedRandomPicks = new();
        private static int _lastSyncedRandomCleanupFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _syncedRandomPicks.Clear();
            _lastSyncedRandomCleanupFrame = -1;
        }

        public static void PlayStateInternal(
            HonamiAnimator anim,
            int targetIndex,
            float transitionDuration,
            int layer,
            bool forceRestart,
            AnimationCurve curve,
            float destinationStartTime,
            int priority = 0,
            HonamiVictimMode victimMode = HonamiVictimMode.None,
            float victimSpeedMultiplier = 1f,
            bool acceleratedWeightDrop = false,
            AnimationCurve victimCurve = null)
        {
            int intendedLayer = anim._runtimeStates[targetIndex].layerIndex;

            if (layer != intendedLayer)
            {
                layer = intendedLayer;
            }

            if (layer < 0 || layer >= anim._layerMixers.Count)
            {
                return;
            }

            if (anim._runtimeStates[targetIndex].node == null || anim._runtimeStates[targetIndex].node.IsVirtual)
            {
                return;
            }

            int prevIdx = anim._layerStates[layer].CurrentStateIndex;

            if (targetIndex == prevIdx)
            {
                if (!forceRestart)
                {
                    return;
                }

                HandleSelfTransition(
                    anim,
                    targetIndex,
                    layer,
                    transitionDuration,
                    curve,
                    destinationStartTime,
                    priority,
                    victimMode,
                    victimSpeedMultiplier,
                    acceleratedWeightDrop,
                    victimCurve);
            }
            else
            {
                HandleCrossStateTransition(
                    anim,
                    targetIndex,
                    prevIdx,
                    layer,
                    transitionDuration,
                    curve,
                    destinationStartTime,
                    priority,
                    victimMode,
                    victimSpeedMultiplier,
                    acceleratedWeightDrop,
                    victimCurve);
            }
        }

        private static void HandleSelfTransition(
            HonamiAnimator anim,
            int targetIndex,
            int layer,
            float transitionDuration,
            AnimationCurve curve,
            float destinationStartTime,
            int priority,
            HonamiVictimMode victimMode,
            float victimSpeedMultiplier,
            bool acceleratedWeightDrop,
            AnimationCurve victimCurve)
        {
            anim.TryTriggerStateExited(layer, targetIndex, HonamiStateExitReason.Restarted, targetIndex);

            if (transitionDuration > 0f)
            {
                SetupTransientPort(anim, layer, targetIndex);
                TakeWeightSnapshot(anim, layer);

                anim._portStates[layer][anim.TransientPortIndex].WeightSnapshot = anim._portStates[layer][targetIndex].WeightSnapshot;
                anim._portStates[layer][targetIndex].WeightSnapshot = 0f;

                if (anim._layerMixer.IsValid())
                    anim._layerStates[layer].TransitionStartLayerWeight = anim._layerMixer.GetInputWeight(layer);
                else
                    anim._layerStates[layer].TransitionStartLayerWeight = 0f;

                anim._layerStates[layer].TransitionDuration = transitionDuration;
                anim._layerStates[layer].TransitionTime = 0.0;
                anim._activeTransitionCurve[layer] = curve;
                anim._layerStates[layer].PreviousStateIndex = anim.TransientPortIndex;
                anim._layerStates[layer].CurrentStateIndex = targetIndex;
                anim._layerStates[layer].CurrentTransitionPriority = priority;
                anim._layerStates[layer].VictimMode = victimMode;
                anim._layerStates[layer].VictimSpeedMultiplier = victimSpeedMultiplier;
                anim._layerStates[layer].AcceleratedWeightDrop = acceleratedWeightDrop;
                anim._layerStates[layer].DestinationFrozen = false;
                anim._layerStates[layer].SourceFrozen = false;
                anim._activeVictimCurve[layer] = victimCurve;

                PlayClip(anim, targetIndex, layer, destinationStartTime, true);
            }
            else
            {
                int oldPrev = anim._layerStates[layer].PreviousStateIndex;
                if (oldPrev != -1)
                {
                    if (oldPrev == anim.TransientPortIndex)
                    {
                        ClearTransientPort(anim, layer);
                    }
                    else
                    {
                        anim.ResetTime(oldPrev, layer);
                    }
                }

                anim._layerStates[layer].PreviousStateIndex = -1;
                anim._layerStates[layer].TransitionDuration = 0.0;
                anim._layerStates[layer].TransitionWeight = 1.0f;
                anim._activeTransitionCurve[layer] = null;
                PlayClip(anim, targetIndex, layer, destinationStartTime, true);
                anim._layerMixers[layer].SetInputWeight(targetIndex, anim._runtimeStates[targetIndex].weight);
            }
        }

        private static void HandleCrossStateTransition(
            HonamiAnimator anim,
            int targetIndex,
            int prevIdx,
            int layer,
            float transitionDuration,
            AnimationCurve curve,
            float destinationStartTime,
            int priority,
            HonamiVictimMode victimMode,
            float victimSpeedMultiplier,
            bool acceleratedWeightDrop,
            AnimationCurve victimCurve)
        {
            if (prevIdx != -1)
            {
                HonamiStateExitReason reason = anim._layerStates[layer].PreviousStateIndex != -1
                    ? HonamiStateExitReason.Interrupted
                    : HonamiStateExitReason.Transition;
                anim.TryTriggerStateExited(layer, prevIdx, reason, targetIndex);
            }

            if (transitionDuration > 0f)
            {
                float currentLW = anim._layerMixer.IsValid() ? anim._layerMixer.GetInputWeight(layer) : 1f;

                if (prevIdx == -1 || currentLW < 0.01f)
                {
                    ResetWeightSnapshots(anim, layer);
                }
                else
                {
                    TakeWeightSnapshot(anim, layer);
                }

                if (anim._layerMixer.IsValid())
                    anim._layerStates[layer].TransitionStartLayerWeight = anim._layerMixer.GetInputWeight(layer);
                else
                    anim._layerStates[layer].TransitionStartLayerWeight = 1f;

                anim._layerStates[layer].PreviousStateIndex = prevIdx;
                anim._layerStates[layer].CurrentStateIndex = targetIndex;
                anim._layerStates[layer].TransitionDuration = transitionDuration;
                anim._layerStates[layer].TransitionTime = 0.0;
                anim._activeTransitionCurve[layer] = curve;
                anim._layerStates[layer].CurrentTransitionPriority = priority;
                anim._layerStates[layer].VictimMode = victimMode;
                anim._layerStates[layer].VictimSpeedMultiplier = victimSpeedMultiplier;
                anim._layerStates[layer].AcceleratedWeightDrop = acceleratedWeightDrop;
                anim._layerStates[layer].DestinationFrozen = false;
                anim._layerStates[layer].SourceFrozen = false;
                anim._activeVictimCurve[layer] = victimCurve;

                if (anim._constraintsEnabled)
                {
                    anim._constraints.UpdateBaselines();
                }

                PlayClip(anim, targetIndex, layer, destinationStartTime, true);
                anim._layerMixers[layer].SetInputWeight(targetIndex, 0f);
            }
            else
            {
                var mixer = anim._layerMixers[layer];
                int portCount = mixer.GetInputCount();
                for (int i = 0; i < portCount; i++)
                {
                    if (i == targetIndex) continue;
                    if (mixer.GetInputWeight(i) > 0f)
                    {
                        mixer.SetInputWeight(i, 0f);

                        if (i == anim.TransientPortIndex)
                        {
                            ClearTransientPort(anim, layer);
                        }
                        else
                        {
                            anim.ResetTime(i, layer);
                        }
                    }
                }

                anim._layerStates[layer].PreviousStateIndex = -1;
                anim._layerStates[layer].CurrentStateIndex = targetIndex;
                anim._layerStates[layer].TransitionDuration = 0.0;
                anim._layerStates[layer].TransitionWeight = 1.0f;
                anim._layerStates[layer].DestinationFrozen = false;
                anim._layerStates[layer].SourceFrozen = false;
                anim._activeTransitionCurve[layer] = null;

                PlayClip(anim, targetIndex, layer, destinationStartTime, true);
                mixer.SetInputWeight(targetIndex, anim._runtimeStates[targetIndex].weight);

                bool isCurrExit2 = anim._runtimeStates[targetIndex].node is { IsExit: true };
                float configWeight = GetConfiguredLayerWeight(anim, layer);

                if (anim._layerMixer.IsValid())
                {
                    anim._layerMixer.SetInputWeight(layer, isCurrExit2 ? 0f : configWeight);
                }

                if (isCurrExit2)
                {
                    anim.CompleteExitState(targetIndex, layer);
                }
            }
        }

        public static void ApplyTransitionFreeze(HonamiAnimator anim, int layer, HonamiTransitionFreezeMode mode)
        {
            if (mode == HonamiTransitionFreezeMode.None) return;
            if (anim._layerStates[layer].TransitionDuration <= 0.0) return;

            int portIdx;
            if (mode == HonamiTransitionFreezeMode.Destination)
            {
                anim._layerStates[layer].DestinationFrozen = true;
                portIdx = anim._layerStates[layer].CurrentStateIndex;
            }
            else
            {
                anim._layerStates[layer].SourceFrozen = true;
                portIdx = anim._layerStates[layer].PreviousStateIndex;
            }

            if (portIdx < 0) return;

            var playable = anim._layerMixers[layer].GetInput(portIdx);
            if (playable.IsValid()) playable.SetSpeed(0f);
        }

        private static void TakeWeightSnapshot(HonamiAnimator anim, int layer)
        {
            var mixer = anim._layerMixers[layer];
            int count = mixer.GetInputCount();

            for (int i = 0; i < count; i++)
            {
                anim._portStates[layer][i].WeightSnapshot = mixer.GetInputWeight(i);
            }
        }

        private static void ResetWeightSnapshots(HonamiAnimator anim, int layer)
        {
            if (anim._portStates == null || layer < 0 || layer >= anim._portStates.Length || anim._portStates[layer] == null)
            {
                return;
            }

            for (int port = 0; port < anim._portStates[layer].Length; port++)
            {
                anim._portStates[layer][port].WeightSnapshot = 0f;
            }
        }

        private static float GetConfiguredLayerWeight(HonamiAnimator anim, int layer)
        {
            if (layer < anim.controller.ActiveLayers.Count)
            {
                return anim.controller.ActiveLayers[layer].weight;
            }

            return layer == 0 ? 1f : 0f;
        }

        public static void ClearTransientPort(HonamiAnimator anim, int layer)
        {
            var layerMixer = anim._layerMixers[layer];
            var existing = layerMixer.GetInput(anim.TransientPortIndex);
            if (existing.IsValid())
            {
                anim._playableGraph.Disconnect(layerMixer, anim.TransientPortIndex);
                anim.DestroyPlayableTree(existing);
            }

            if (layer < anim._layerStates.Length)
            {
                anim._layerStates[layer].TransientStateIndex = -1;
            }
        }

        private static void SetupTransientPort(HonamiAnimator anim, int layer, int sourceIndex)
        {
            var layerMixer = anim._layerMixers[layer];
            int transPort = anim.TransientPortIndex;

            ClearTransientPort(anim, layer);
            anim._layerStates[layer].TransientStateIndex = sourceIndex;

            anim._blendParamIndices[layer * anim._pCountTotal + transPort] = anim._blendParamIndices[layer * anim._pCountTotal + sourceIndex];

            var state = anim._runtimeStates[sourceIndex];
            var srcPlayable = layerMixer.GetInput(sourceIndex);

            System.Func<PlayableGraph, Playable> mirrorFactory = anim._avatarEnabled && anim._avatarProcessor.HasBones ? anim._avatarProcessor.CreateMirrorPlayable : null;
            Playable newPlayable = HonamiPlayableFactory.CreateAndConnect(anim._playableGraph, anim.controller, state, layerMixer, transPort, mirrorFactory);

            if (!newPlayable.IsValid()) return;

            if (srcPlayable.IsValid())
            {
                newPlayable.SetTime(srcPlayable.GetTime());
                if (srcPlayable.GetPlayableType() == typeof(AnimationMixerPlayable) &&
                    newPlayable.GetPlayableType() == typeof(AnimationMixerPlayable))
                {
                    var sMixer = (AnimationMixerPlayable)srcPlayable;
                    var nMixer = (AnimationMixerPlayable)newPlayable;
                    for (int i = 0; i < sMixer.GetInputCount(); i++)
                    {
                        nMixer.SetInputWeight(i, sMixer.GetInputWeight(i));
                        var sChild = sMixer.GetInput(i);
                        var nChild = nMixer.GetInput(i);
                        if (sChild.IsValid() && nChild.IsValid())
                        {
                            nChild.SetTime(sChild.GetTime());
                            nChild.Play();
                        }
                    }
                }
            }

            layerMixer.SetInputWeight(transPort, layerMixer.GetInputWeight(sourceIndex));

            if (anim._firedEventsPerPort != null && layer < anim._firedEventsPerPort.Length)
            {
                anim._firedEventsPerPort[layer][transPort].Clear();
                anim._portStates[layer][transPort].CurrentLoopCount = 0;
                anim._portStates[layer][transPort].StateFinishedFired = true;
                anim._portStates[layer][transPort].StateExitedFired = true;
            }

            newPlayable.Play();
        }

        public static void UpdateStatesRuntimeProperties(HonamiAnimator anim)
        {
            if (anim.controller == null)
            {
                return;
            }

            for (int layer = 0; layer < anim._layerMixers.Count; layer++)
            {
                if (anim._layerStates[layer].IsLayerPaused)
                {
                    continue;
                }

                var mixer = anim._layerMixers[layer];
                int portCount = mixer.GetInputCount();

                for (int i = 0; i < portCount; i++)
                {
                    if (mixer.GetInputWeight(i) <= 0.0001f)
                    {
                        continue;
                    }

                    if (anim._pausedStateIndices[layer].Contains(i))
                    {
                        mixer.GetInput(i).SetSpeed(0f);
                        continue;
                    }

                    int stateIdx = i == anim.TransientPortIndex
                        ? anim._layerStates[layer].TransientStateIndex
                        : i;

                    if (stateIdx < 0 || stateIdx >= anim._activeStatesCount)
                    {
                        continue;
                    }

                    var state = anim._runtimeStates[stateIdx];
                    var playable = mixer.GetInput(i);

                    if (playable.IsValid())
                    {
                        bool isTransitioning = anim._layerStates[layer].PreviousStateIndex != -1
                            || anim._layerStates[layer].TransitionDuration > 0.0;
                        bool frozen = isTransitioning
                            && ((anim._layerStates[layer].DestinationFrozen && i == anim._layerStates[layer].CurrentStateIndex)
                                || (anim._layerStates[layer].SourceFrozen && i == anim._layerStates[layer].PreviousStateIndex));
                        float speed = frozen ? 0f : (state.isReversed ? -state.speed : state.speed);
                        playable.SetSpeed(speed);
                    }

                    UpdateStateOnPort(anim, layer, i, stateIdx);
                }
            }
        }

        private static void UpdateStateOnPort(HonamiAnimator anim, int layer, int portIdx, int stateIdx)
        {
            if (portIdx < 0 || stateIdx < 0 || stateIdx >= anim._activeStatesCount)
            {
                return;
            }

            var layerMixer = anim._layerMixers[layer];
            var state = anim._runtimeStates[stateIdx];
            var playable = layerMixer.GetInput(portIdx);

            if (!playable.IsValid())
            {
                return;
            }

            Playable actualPlayable = playable;
            if (actualPlayable.GetPlayableType() == typeof(AnimationScriptPlayable) && actualPlayable.GetInputCount() > 0)
            {
                actualPlayable = actualPlayable.GetInput(0);
            }

            bool isTransitioning = anim._layerStates[layer].PreviousStateIndex != -1 || anim._layerStates[layer].TransitionDuration > 0f;
            if (!isTransitioning && portIdx == anim._layerStates[layer].CurrentStateIndex)
            {
                int inputCount = layerMixer.GetInputCount();
                for (int i = 0; i < inputCount; i++)
                {
                    layerMixer.SetInputWeight(i, i == portIdx ? state.weight : 0f);
                }

                if (anim._layerMixer.IsValid())
                {
                    anim._layerMixer.SetInputWeight(layer, GetConfiguredLayerWeight(anim, layer));
                }
            }

            var activeNode = anim.controller.GetActiveNode(state);
            if (activeNode != null && !activeNode.IsVirtual)
            {
                // HonamiExecutionContext must be created passing `anim` context
                HonamiExecutionContext ctx = new HonamiExecutionContext(
                    anim, state, stateIdx, layer, portIdx, actualPlayable,
                    layerMixer, anim._params, anim.GetNodeRuntime(stateIdx),
                    anim._blendTreeParamHashes, (float)anim._cachedDeltaTime);

                if (activeNode is HonamiBlendTreeNode btNode)
                {
                    int cacheBase = (layer * anim._pCountTotal + portIdx) * 2;
                    int pIdx = anim._blendParamIndices[layer * anim._pCountTotal + portIdx];
                    float targetValue = pIdx >= 0 ? anim._params.GetFloatByIndex(pIdx) : 0f;

                    var span = anim._blendStateValues.AsSpan().Slice(cacheBase, 2);
                    float lastVal = span[0];
                    float dampRef = span[1];

                    float paramValue;
                    if (btNode.blendParameterDampTime > 0f)
                    {
                        paramValue = Mathf.SmoothDamp(lastVal, targetValue, ref dampRef,
                            btNode.blendParameterDampTime, float.PositiveInfinity, (float)anim._cachedDeltaTime);
                    }
                    else
                    {
                        paramValue = targetValue;
                        dampRef = 0f;
                    }

                    span[0] = paramValue;
                    span[1] = dampRef;

                    if (actualPlayable.GetPlayableType() == typeof(AnimationMixerPlayable))
                    {
                        var btMixer = (AnimationMixerPlayable)actualPlayable;
                        HonamiBlendTreeEvaluator.UpdateWeightsFromMotions(btNode.blendMotions, btMixer, paramValue);
                        HonamiBlendTreeEvaluator.UpdateChildSpeedsFromMotions(state, btNode.blendMotions, btMixer, paramValue, btNode.blendType);
                    }
                }
                else
                {
                    activeNode.UpdateRuntime(in ctx);
                }

                if (state.subNodes != null && state.subNodes.Count > 0)
                {
                    int snCount = state.subNodes.Count;
                    for (int s = 0; s < snCount; s++)
                    {
                        var sn = state.subNodes[s];

                        if (sn != null)
                        {
                            sn.UpdateRuntime(in ctx);
                        }
                    }
                }
            }
        }

        private static void PlayClip(HonamiAnimator anim, int stateIndex, int layer, float startTime = 0f, bool resetTime = true)
        {
            if (stateIndex < 0 || stateIndex >= anim._activeStatesCount)
            {
                return;
            }

            var state = anim._runtimeStates[stateIndex];
            anim.ApplyParameterAssignments(state, false);

            var playable = anim._layerMixers[layer].GetInput(stateIndex);
            bool hasPlayable = playable.IsValid();

            Playable actualPlayable = playable;
            if (hasPlayable && actualPlayable.GetPlayableType() == typeof(AnimationScriptPlayable) && actualPlayable.GetInputCount() > 0)
            {
                actualPlayable = actualPlayable.GetInput(0);
            }

            var activeNode = anim.controller.GetActiveNode(state);
            if (activeNode != null)
            {
                HonamiExecutionContext ctx = new(
                    anim,
                    state,
                    stateIndex,
                    layer,
                    stateIndex,
                    actualPlayable,
                    anim._layerMixers[layer],
                    anim._params,
                    anim.GetNodeRuntime(stateIndex),
                    anim._blendTreeParamHashes,
                    0f);

                activeNode.OnEnter(in ctx);

                if (state.subNodes != null)
                {
                    int snCount = state.subNodes.Count;
                    for (int s = 0; s < snCount; s++)
                    {
                        var sn = state.subNodes[s];

                        if (sn != null)
                        {
                            sn.OnEnter(in ctx);
                        }
                    }
                }
            }

            anim.TriggerStateEntered(state.stateName);

            // Playable-less states (e.g. Exit) still get the full enter lifecycle above.
            if (!hasPlayable)
            {
                ResetPortEventFlags(anim, layer, stateIndex, state);
                return;
            }

            float duration = HonamiStateEvaluator.GetUnscaledStateDuration(
                anim.controller,
                state,
                stateIndex,
                anim.GetNodeRuntime(stateIndex),
                anim.GetStateBlendParam(state));

            if (resetTime)
            {
                playable.SetTime(state.isReversed ? duration - startTime : startTime);
            }

            playable.Play();

            float initialSpeed = state.isReversed ? -state.speed : state.speed;
            playable.SetSpeed(initialSpeed);

            if (actualPlayable.IsValid() && actualPlayable.GetHandle() != playable.GetHandle())
            {
                actualPlayable.Play();
                actualPlayable.SetSpeed(initialSpeed);
            }

            if (actualPlayable.GetPlayableType() == typeof(AnimationMixerPlayable))
            {
                var mixer = (AnimationMixerPlayable)actualPlayable;
                bool isRandom = activeNode is HonamiRandomAnimationNode;
                bool isSeq = activeNode is HonamiSequencerNode;
                int pickedIdx = -1;

                if (isRandom && activeNode is HonamiRandomAnimationNode randomNode && randomNode.randomClips?.Count > 0
                    && anim.GetNodeRuntime(stateIndex) is HonamiRandomAnimationNode.Runtime randomRuntime)
                {
                    pickedIdx = PickRandomClip(anim, randomNode, randomRuntime, stateIndex);
                }

                for (int i = 0; i < mixer.GetInputCount(); i++)
                {
                    var inputPlayable = mixer.GetInput(i);

                    if (!inputPlayable.IsValid())
                    {
                        continue;
                    }

                    if (isRandom)
                    {
                        mixer.SetInputWeight(i, i == pickedIdx ? 1f : 0f);
                    }

                    if (resetTime)
                    {
                        if (isSeq && activeNode is HonamiSequencerNode seqNode)
                        {
                            var sc = seqNode.sequencedClips[i];
                            float clipDur = sc.clip != null ? (sc.clip.length / Mathf.Abs(sc.speed != 0 ? sc.speed : 1f)) : 0f;
                            float seqTime = state.isReversed ? (duration - startTime) : startTime;
                            inputPlayable.SetTime(seqTime - sc.startTime);
                            mixer.SetInputWeight(i, (seqTime >= sc.startTime && seqTime <= sc.startTime + clipDur) ? 1f : 0f);
                        }
                        else if (inputPlayable.GetPlayableType() == typeof(AnimationClipPlayable)
                                 && activeNode is HonamiAnimationNode animNode2)
                        {
                            var cp = (AnimationClipPlayable)inputPlayable;
                            float stop = animNode2.endTime > 0 ? animNode2.endTime : (cp.GetAnimationClip()?.length ?? 0f);
                            float dur = Mathf.Max(0.001f, stop - animNode2.startTime);
                            float local = state.isReversed ? (dur - startTime) : startTime;
                            inputPlayable.SetTime(animNode2.startTime + local);
                        }
                        else
                        {
                            inputPlayable.SetTime(state.isReversed ? (duration - startTime) : startTime);
                        }
                    }
                    inputPlayable.Play();
                }

                if (activeNode is HonamiBlendTreeNode btNode2)
                {
                    int ph = anim._blendTreeParamHashes[stateIndex];
                    float paramValue = ph != -1 ? anim._params.GetFloat(ph) : 0f;
                    HonamiBlendTreeEvaluator.UpdateWeightsFromMotions(btNode2.blendMotions, mixer, paramValue);
                    HonamiBlendTreeEvaluator.UpdateChildSpeedsFromMotions(state, btNode2.blendMotions, mixer, paramValue, btNode2.blendType);

                    int cacheBase = (layer * anim._pCountTotal + stateIndex) * 2;
                    var span = anim._blendStateValues.AsSpan().Slice(cacheBase, 2);
                    span[0] = paramValue;
                    span[1] = 0f;
                }
            }

            ResetPortEventFlags(anim, layer, stateIndex, state);
        }

        private static void ResetPortEventFlags(HonamiAnimator anim, int layer, int stateIndex, HonamiState state)
        {
            if (state.events != null && anim._firedEventsPerPort != null && layer < anim._firedEventsPerPort.Length)
            {
                if (stateIndex >= 0 && stateIndex < anim._firedEventsPerPort[layer].Length)
                {
                    anim._firedEventsPerPort[layer][stateIndex].Clear();
                    anim._portStates[layer][stateIndex].LastFrameRawTime = -1.0;
                    anim._portStates[layer][stateIndex].CurrentLoopCount = 0;
                }
            }

            if (anim._portStates != null && layer < anim._portStates.Length
                && stateIndex >= 0 && stateIndex < anim._portStates[layer].Length)
            {
                anim._portStates[layer][stateIndex].StateFinishedFired = false;
                anim._portStates[layer][stateIndex].StateExitedFired = false;
            }
        }

        private static int PickRandomClip(HonamiAnimator anim, HonamiRandomAnimationNode randomNode, HonamiRandomAnimationNode.Runtime runtime, int stateIndex)
        {
            int count = randomNode.randomClips.Count;
            HashSet<int> playedClips = GetNoRepeatPlayedClips(randomNode, runtime, count);
            float totalWeight = GetSelectableWeight(randomNode, playedClips);

            if (playedClips != null && totalWeight <= 0f)
            {
                RestartNoRepeatBag(runtime, playedClips);
                totalWeight = GetSelectableWeight(randomNode, playedClips);

                if (totalWeight <= 0f)
                {
                    playedClips.Clear();
                    totalWeight = GetSelectableWeight(randomNode, playedClips);
                }
            }

            int pickedIdx = -1;
            int syncKey = 0;
            bool useLinkedSync = randomNode.syncWhenLinked && TryGetLinkedRandomSyncKey(anim, randomNode, stateIndex, count, out syncKey);

            if (useLinkedSync && TryGetSyncedRandomPick(syncKey, count, out pickedIdx))
            {
                MarkNoRepeatPlayed(playedClips, pickedIdx);
                runtime.PickedIndex = pickedIdx;
                return pickedIdx;
            }

            if (totalWeight > 0f)
            {
                float r;
                if (useLinkedSync)
                {
                    Random.State oldState = Random.state;
                    Random.InitState(syncKey ^ Time.frameCount);
                    r = Random.Range(0f, totalWeight);
                    Random.state = oldState;
                }
                else
                {
                    r = Random.Range(0f, totalWeight);
                }

                float sum = 0f;
                for (int i = 0; i < randomNode.randomClips.Count; i++)
                {
                    HonamiRandomAnimationClip c = randomNode.randomClips[i];

                    if (!IsSelectable(c, i, playedClips))
                    {
                        continue;
                    }

                    sum += c.weight;
                    if (r <= sum)
                    {
                        pickedIdx = i;
                        break;
                    }
                }
            }

            MarkNoRepeatPlayed(playedClips, pickedIdx);

            if (useLinkedSync)
            {
                _syncedRandomPicks[syncKey] = new SyncedRandomPick { frame = Time.frameCount, pickedIndex = pickedIdx };
            }

            runtime.PickedIndex = pickedIdx;
            return pickedIdx;
        }

        private static HashSet<int> GetNoRepeatPlayedClips(HonamiRandomAnimationNode randomNode, HonamiRandomAnimationNode.Runtime runtime, int clipCount)
        {
            if (!randomNode.noRepeat || clipCount <= 1)
            {
                return null;
            }

            runtime.PlayedClips ??= new HashSet<int>();
            return runtime.PlayedClips;
        }

        private static void RestartNoRepeatBag(HonamiRandomAnimationNode.Runtime runtime, HashSet<int> playedClips)
        {
            playedClips.Clear();

            if (runtime.PickedIndex >= 0)
            {
                playedClips.Add(runtime.PickedIndex);
            }
        }

        private static float GetSelectableWeight(HonamiRandomAnimationNode randomNode, HashSet<int> playedClips)
        {
            float totalWeight = 0f;
            for (int i = 0; i < randomNode.randomClips.Count; i++)
            {
                HonamiRandomAnimationClip c = randomNode.randomClips[i];
                if (IsSelectable(c, i, playedClips))
                {
                    totalWeight += c.weight;
                }
            }

            return totalWeight;
        }

        private static bool IsSelectable(HonamiRandomAnimationClip c, int index, HashSet<int> playedClips)
        {
            if (c.clip == null || c.muted)
            {
                return false;
            }

            return playedClips == null || (c.weight > 0f && !playedClips.Contains(index));
        }

        private static void MarkNoRepeatPlayed(HashSet<int> playedClips, int pickedIdx)
        {
            if (playedClips != null && pickedIdx >= 0)
            {
                playedClips.Add(pickedIdx);
            }
        }

        private static bool TryGetSyncedRandomPick(int syncKey, int clipCount, out int pickedIdx)
        {
            CleanupSyncedRandomPicks();

            if (_syncedRandomPicks.TryGetValue(syncKey, out SyncedRandomPick pick) &&
                pick.frame == Time.frameCount &&
                pick.pickedIndex >= -1 &&
                pick.pickedIndex < clipCount)
            {
                pickedIdx = pick.pickedIndex;
                return true;
            }

            pickedIdx = -1;
            return false;
        }

        private static void CleanupSyncedRandomPicks()
        {
            int frame = Time.frameCount;

            if (_lastSyncedRandomCleanupFrame == frame)
            {
                return;
            }

            _lastSyncedRandomCleanupFrame = frame;

            List<int> staleKeys = null;
            foreach (var kv in _syncedRandomPicks)
            {
                if (kv.Value.frame >= frame - 1)
                {
                    continue;
                }

                staleKeys ??= new List<int>();
                staleKeys.Add(kv.Key);
            }

            if (staleKeys == null)
            {
                return;
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                _syncedRandomPicks.Remove(staleKeys[i]);
            }
        }

        private static bool TryGetLinkedRandomSyncKey(HonamiAnimator anim, HonamiRandomAnimationNode randomNode, int stateIndex, int clipCount, out int syncKey)
        {
            syncKey = 0;

            if (anim == null || randomNode == null || stateIndex < 0 || stateIndex >= anim._activeStatesCount)
            {
                return false;
            }

            HonamiState state = anim._runtimeStates[stateIndex];

            if (state == null)
            {
                return false;
            }

            HonamiLinkedAnimator brain = anim._linkedBrain ?? anim.GetComponentInParent<HonamiLinkedAnimator>();

            int groupHash = brain != null
                ? HonamiObjectHash.Of(brain)
                : HonamiObjectHash.Of(anim.transform.root);

            int stateHash = state.linkedActionId != null
                ? HonamiObjectHash.Of(state.linkedActionId)
                : HonamiAnimator.StringToHash(!string.IsNullOrEmpty(state.stateName) ? state.stateName : randomNode.name);

            unchecked
            {
                syncKey = 17;
                syncKey = syncKey * 31 + groupHash;
                syncKey = syncKey * 31 + stateHash;
                syncKey = syncKey * 31 + clipCount;
            }

            return syncKey != 0;
        }

        public static void ResetTime(HonamiAnimator anim, int portIndex, int layer)
        {
            int stateIdx = (portIndex == anim.TransientPortIndex) ? anim._layerStates[layer].TransientStateIndex : portIndex;

            if (stateIdx < 0 || stateIdx >= anim._activeStatesCount)
            {
                return;
            }

            var state = anim._runtimeStates[stateIdx];
            anim.ApplyParameterAssignments(state, true);

            var playable = anim._layerMixers[layer].GetInput(portIndex);
            bool hasPlayable = playable.IsValid();

            Playable actualPlayable = playable;
            if (hasPlayable && actualPlayable.GetPlayableType() == typeof(AnimationScriptPlayable) && actualPlayable.GetInputCount() > 0)
            {
                actualPlayable = actualPlayable.GetInput(0);
            }

            var activeNode = anim.controller.GetActiveNode(state);
            if (activeNode != null)
            {
                HonamiExecutionContext ctx = new(
                    anim,
                    state,
                    stateIdx,
                    layer,
                    portIndex,
                    actualPlayable,
                    anim._layerMixers[layer],
                    anim._params,
                    anim.GetNodeRuntime(stateIdx),
                    anim._blendTreeParamHashes,
                    0f);

                activeNode.OnExit(in ctx);

                if (state.subNodes != null)
                {
                    int snCount = state.subNodes.Count;
                    for (int s = 0; s < snCount; s++)
                    {
                        var sn = state.subNodes[s];

                        if (sn != null)
                        {
                            sn.OnExit(in ctx);
                        }
                    }
                }
            }

            if (!hasPlayable)
            {
                return;
            }

            playable.Pause();

            if (actualPlayable.GetPlayableType() == typeof(AnimationMixerPlayable))
            {
                anim.ResetPlayableTreeTime(actualPlayable);
            }

            float duration = HonamiStateEvaluator.GetUnscaledStateDuration(
                anim.controller,
                state,
                stateIdx,
                anim.GetNodeRuntime(stateIdx),
                anim.GetStateBlendParam(state));

            playable.SetTime(state.isReversed ? duration : 0f);
        }
    }
}
