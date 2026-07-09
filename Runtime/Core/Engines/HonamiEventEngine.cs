using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Events;

namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiEventEngine
    {
        public static void EvaluateEvents(HonamiAnimator anim)
        {
            if (anim.controller == null) return;

            for (int layer = 0; layer < anim._layerMixers.Count; layer++)
            {
                if (anim._layerStates[layer].IsLayerPaused) continue;

                var layerMixer = anim._layerMixers[layer];
                int portCount = layerMixer.GetInputCount();

                for (int portIdx = 0; portIdx < portCount; portIdx++)
                {
                    if (layerMixer.GetInputWeight(portIdx) > 0.0001f)
                    {
                        if (anim._pausedStateIndices[layer].Contains(portIdx)) continue;
                        if (anim._portStates[layer][portIdx].StateExitedFired) continue;

                        if (portIdx != anim._layerStates[layer].CurrentStateIndex) continue;

                        int stateIdx = (portIdx == anim.TransientPortIndex) ? anim._layerStates[layer].TransientStateIndex : portIdx;
                        if (stateIdx < 0 || stateIdx >= anim._activeStatesCount) continue;

                        var state = anim._runtimeStates[stateIdx];
                        bool hasEvents = state.events != null && state.events.Count > 0;
                        bool needsFinishCheck = !state.loop;
                        if (!hasEvents && !needsFinishCheck) continue;

                        var playable = layerMixer.GetInput(portIdx);
                        if (!playable.IsValid()) continue;

                        EvaluateEventsForState(anim, layer, portIdx, state, stateIdx, playable);
                    }
                }
            }
        }

        private static void EvaluateEventsForState(HonamiAnimator anim, int layer, int portIdx, HonamiState state, int stateIdx, Playable playable)
        {
            float unscaledDuration = HonamiStateEvaluator.GetUnscaledStateDuration(anim.controller, state, stateIdx, anim._pickedRandomIdx, GetStateBlendParam(anim, state));
            if (unscaledDuration <= 0f) return;

            double rawTime = playable.GetTime();
            double lastRawTime = anim._portStates[layer][portIdx].LastFrameRawTime;
            anim._portStates[layer][portIdx].LastFrameRawTime = rawTime;

            if (lastRawTime < 0) lastRawTime = rawTime;

            if (state.loop)
            {
                EvaluateLoopingStateEvents(anim, layer, portIdx, state, unscaledDuration, rawTime, lastRawTime);
            }
            else
            {
                EvaluateSingleRunStateEvents(anim, layer, portIdx, state, stateIdx, unscaledDuration, rawTime);
            }
        }

        private static void EvaluateLoopingStateEvents(HonamiAnimator anim, int layer, int portIdx, HonamiState state, float unscaledDuration, double rawTime, double lastRawTime)
        {
            int eventCount = state.events?.Count ?? 0;
            if (eventCount == 0) return;

            int lastLoop = (int)System.Math.Floor(lastRawTime / unscaledDuration);
            int currentLoop = (int)System.Math.Floor(rawTime / unscaledDuration);

            if (currentLoop != lastLoop)
            {
                int missed = System.Math.Min(System.Math.Abs(currentLoop - lastLoop), 3);
                for (int i = 0; i < missed; i++)
                {
                    for (int ei = 0; ei < eventCount; ei++)
                        FireEvent(anim, state.events[ei]);
                }
                anim._firedEventsPerPort[layer][portIdx].Clear();
            }

            float loopedTime = (float)(rawTime % unscaledDuration);
            if (loopedTime < 0) loopedTime += unscaledDuration;

            FireDueEvents(anim, layer, portIdx, state, loopedTime, eventCount);
        }

        private static void EvaluateSingleRunStateEvents(HonamiAnimator anim, int layer, int portIdx, HonamiState state, int stateIdx, float unscaledDuration, double rawTime)
        {
            float clampedTime = Mathf.Clamp((float)rawTime, 0f, unscaledDuration);
            int eventCount = state.events?.Count ?? 0;

            FireDueEvents(anim, layer, portIdx, state, clampedTime, eventCount);

            bool isFinished = state.isReversed ? (rawTime <= 0.0) : (rawTime >= (double)unscaledDuration);

            if (!anim._portStates[layer][portIdx].StateFinishedFired && isFinished)
            {
                anim._portStates[layer][portIdx].StateFinishedFired = true;
                anim.TryTriggerStateExited(layer, portIdx, HonamiStateExitReason.Completed);
                anim.TriggerStateFinished(state.stateName);
                anim.ReleaseFinishedStateIfNeeded(layer, portIdx, stateIdx);
            }
        }

        private static void FireDueEvents(HonamiAnimator anim, int layer, int portIdx, HonamiState state, float sampleTime, int eventCount)
        {
            for (int ei = 0; ei < eventCount; ei++)
            {
                var evt = state.events[ei];
                bool shouldFire = state.isReversed ? (sampleTime <= evt.time) : (sampleTime >= evt.time);
                if (!shouldFire || anim._firedEventsPerPort[layer][portIdx].Contains(evt)) continue;

                FireEvent(anim, evt);
                anim._firedEventsPerPort[layer][portIdx].Add(evt);
            }
        }

        private static void FireEvent(HonamiAnimator anim, HonamiEventMarker evt)
        {
            HonamiEventEvaluator.FireEvent(evt, anim._localEventReceiver);
        }

        public static bool EvaluateConditionTentative(HonamiAnimator anim, HonamiCondition cond, List<int> triggersTentative)
        {
            return anim._conditionParamHashes.TryGetValue(cond, out int hash) 
                   && HonamiConditionEvaluator.EvaluateConditionTentative(cond, anim._params, hash, triggersTentative);
        }

        public static void ApplyParameterAssignments(HonamiAnimator anim, HonamiState state, bool onExit)
        {
            HonamiConditionEvaluator.ApplyParameterAssignments(state, onExit, anim._params, anim._assignmentParamHashes);
        }

        public static void ApplyTransitionAssignments(HonamiAnimator anim, HonamiTransition transition)
        {
            HonamiConditionEvaluator.ApplyTransitionAssignments(transition, anim._params, anim._assignmentParamHashes);
        }

        public static bool EvaluatePortalRecursive(HonamiAnimator anim, HonamiState portalExit, int layer, int currentIdx, bool isFromRepeater, HonamiTransition originalTrans, List<int> originalTentative, int depth = 0)
        {
            if (depth > 32)
            {
                Debug.LogError("[Honami] Portal chain depth exceeded 32. Check for circular portal references in the controller.");
                return false;
            }

            var exitTransitions = anim.controller.GetTransitions(portalExit);
            if (exitTransitions == null) return false;

            int etCount = exitTransitions.Count;
            for (int ei = 0; ei < etCount; ei++)
            {
                var exitTrans = exitTransitions[ei];
                bool exitMet = true;
                anim._exitTentativeBuffer.Clear();

                if (!string.IsNullOrEmpty(exitTrans.portalSourceFilterGuid))
                {
                    var srcState = currentIdx >= 0 ? anim._runtimeStates[currentIdx] : null;
                    if (srcState == null || exitTrans.portalSourceFilterGuid != srcState.guid)
                        exitMet = false;
                }

                if (exitMet && exitTrans.conditions != null)
                {
                    int cCount = exitTrans.conditions.Count;
                    for (int ci = 0; ci < cCount; ci++)
                    {
                        if (!EvaluateConditionTentative(anim, exitTrans.conditions[ci], anim._exitTentativeBuffer))
                        { exitMet = false; break; }
                    }
                }

                if (exitMet && anim._stateGuidToIndex.TryGetValue(exitTrans.targetStateGuidHash, out int nextTargetIdx))
                {
                    var nextState = anim._runtimeStates[nextTargetIdx];
                    if (nextState.node is HonamiPortalEntranceNode)
                    {
                        var nextEntranceNode = (HonamiPortalEntranceNode)nextState.node;
                        HonamiState nextExit = null;
                        int statesCount = anim._activeStatesCount;
                        for (int sIdx = 0; sIdx < statesCount; sIdx++)
                        {
                            var tmpState = anim._runtimeStates[sIdx];
                            if (tmpState != null && tmpState.node is HonamiPortalExitNode en && en.portalName == nextEntranceNode.portalName)
                            {
                                nextExit = tmpState;
                                break;
                            }
                        }

                        if (nextExit != null)
                        {
                            anim._params.CommitTriggers(anim._exitTentativeBuffer);
                            ApplyTransitionAssignments(anim, exitTrans);
                            return EvaluatePortalRecursive(anim, nextExit, layer, currentIdx, isFromRepeater, originalTrans, originalTentative, depth + 1);
                        }
                    }
                    else if (!nextState.node.IsVirtual)
                    {
                        anim._params.CommitTriggers(originalTentative);
                        anim._params.CommitTriggers(anim._exitTentativeBuffer);
                        ApplyTransitionAssignments(anim, originalTrans);
                        ApplyTransitionAssignments(anim, exitTrans);

                        anim.PlayStateByGuid(nextState.guid, originalTrans.duration, layer,
                            isFromRepeater,
                            originalTrans.useCurve ? originalTrans.curve : null,
                            exitTrans.destinationStartTime);
                        return true;
                    }
                }
            }
            return false;
        }

        public static void DestroyPlayableTree(HonamiAnimator anim, Playable p)
        {
            if (!p.IsValid()) return;
            int count = p.GetInputCount();
            for (int i = 0; i < count; i++)
            {
                var child = p.GetInput(i);
                if (child.IsValid()) DestroyPlayableTree(anim, child);
            }
            p.Destroy();
        }

        public static void ResetPlayableTreeTime(HonamiAnimator anim, Playable p)
        {
            if (!p.IsValid()) return;
            p.Pause();
            p.SetTime(0f);
            int count = p.GetInputCount();
            for (int i = 0; i < count; i++)
                ResetPlayableTreeTime(anim, p.GetInput(i));
        }

        public static float GetStateBlendParam(HonamiAnimator anim, HonamiState s)
        {
            if (s?.node is not HonamiBlendTreeNode btNode || string.IsNullOrEmpty(btNode.blendParameter)) return 0f;
            int hash = HonamiAnimator.StringToHash(btNode.blendParameter);
            return anim._params.GetFloat(hash);
        }
    }
}
