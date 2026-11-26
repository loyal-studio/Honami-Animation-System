using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiEventEngine
    {
        private const int MaxCatchUpLoops = 3;

        public static void EvaluateStateModules(HonamiAnimator anim)
        {
            if (anim.controller == null || anim._layerStates == null) return;

            EnsureModuleStorage(anim);

            int layerCount = Mathf.Min(anim._layerMixers.Count, anim._layerStates.Length);
            for (int layer = 0; layer < layerCount; layer++)
            {
                if (anim._layerStates[layer].IsLayerPaused) continue;

                var layerMixer = anim._layerMixers[layer];
                int portIdx = anim._layerStates[layer].CurrentStateIndex;
                int stateIdx = -1;
                bool skipFrame = false;

                if (portIdx >= 0 && portIdx < layerMixer.GetInputCount() && !anim._portStates[layer][portIdx].StateExitedFired)
                {
                    int candidate = portIdx == anim.TransientPortIndex ? anim._layerStates[layer].TransientStateIndex : portIdx;
                    if (candidate >= 0 && candidate < anim._activeStatesCount)
                    {
                        stateIdx = candidate;
                        skipFrame = anim._pausedStateIndices[layer].Contains(portIdx) || layerMixer.GetInputWeight(portIdx) <= 0.0001f;
                    }
                }

                if (anim._activeModuleState[layer] >= 0 && anim._activeModuleState[layer] != stateIdx)
                    StopLayerModules(anim, layer);

                if (stateIdx < 0 || skipFrame) continue;

                var state = anim._runtimeStates[stateIdx];
                bool hasModules = state.Modules != null && state.Modules.Count > 0;
                if (!hasModules && state.loop) continue;

                var playable = layerMixer.GetInput(portIdx);
                if (!playable.IsValid()) continue;

                EvaluateStateTimeline(anim, layer, portIdx, state, stateIdx, playable, hasModules);
            }
        }

        // Forces every state's modules to be rebuilt on its next evaluation without re-entering the state,
        // so events that already fired are not fired again.
        public static void InvalidateStateModules(HonamiAnimator anim)
        {
            var sets = anim._stateModuleSets;
            if (sets == null) return;
            for (int i = 0; i < sets.Length; i++)
            {
                if (sets[i] != null)
                    sets[i].IsStale = true;
            }
        }

        public static void StopAllStateModules(HonamiAnimator anim)
        {
            if (anim._activeModuleState == null) return;
            for (int layer = 0; layer < anim._activeModuleState.Length; layer++)
                StopLayerModules(anim, layer);
        }

        private static void EnsureModuleStorage(HonamiAnimator anim)
        {
            int layerCount = anim._layerStates.Length;
            if (anim._moduleStorageStates == anim._runtimeStates && anim._stateModuleSets != null
                && anim._activeModuleState != null && anim._activeModuleState.Length == layerCount)
                return;

            StopAllStateModules(anim);
            if (anim._stateModuleSets != null)
            {
                for (int i = 0; i < anim._stateModuleSets.Length; i++)
                    anim._stateModuleSets[i]?.Dispose();
            }

            int stateCount = anim._activeStatesCount;
            anim._moduleStorageStates = anim._runtimeStates;
            anim._stateModuleSets = new HonamiTimelineModuleSet[stateCount];
            anim._activeModuleState = new int[layerCount];
            for (int layer = 0; layer < layerCount; layer++)
                anim._activeModuleState[layer] = -1;
        }

        private static void StopLayerModules(HonamiAnimator anim, int layer)
        {
            int stateIdx = anim._activeModuleState[layer];
            anim._activeModuleState[layer] = -1;
            if (stateIdx < 0 || stateIdx >= anim._stateModuleSets.Length) return;

            anim._stateModuleSets[stateIdx]?.StopAll();
        }

        private static HonamiTimelineModuleSet GetStateModules(HonamiAnimator anim, HonamiState state, int stateIdx)
        {
            var modules = state.Modules;
            var set = anim._stateModuleSets[stateIdx];
            if (set != null && !set.IsStale && set.Runtimes.Length == modules.Count && set.Revision == state.ModuleRevision)
                return set;

            set = BuildStateModules(anim, state);
            anim._stateModuleSets[stateIdx] = set;
            return set;
        }

        private static HonamiTimelineModuleSet BuildStateModules(HonamiAnimator anim, HonamiState state)
        {
            var modules = state.Modules;
            var set = new HonamiTimelineModuleSet(modules, state.node, state.ModuleRevision);
            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module == null)
                {
                    Debug.LogError($"[Honami] Timeline module at index {i} in state '{state.stateName}' on '{anim.name}' could not be deserialized (null). This usually indicates the module class was stripped by Unity Linker during player build. Ensure the module class has [UnityEngine.Scripting.Preserve].", anim);
                    continue;
                }

                if (!module.SupportsHost(HonamiTimelineModuleHost.State)) continue;

                string where = $"state '{state.stateName}' on '{anim.name}'";
                if (set.IsMissingParent(i))
                {
                    HonamiTimelineModuleBinding.ReportProblem(module, "its parent clip or item is gone", where, anim);
                    continue;
                }

                var binding = HonamiTimelineModuleBinding.Resolve(anim.gameObject, module.bindingKey, module.BindingType);
                HonamiTimelineModuleBinding.ReportProblem(module,
                    module.GetBindingProblem(HonamiTimelineModuleHost.State, binding, anim.gameObject), where, anim);
                var context = new HonamiTimelineModuleContext(HonamiTimelineModuleHost.State, anim.gameObject, binding, null, anim, state);
                set.Runtimes[i] = module.CreateRuntime(in context);
            }
            return set;
        }

        private static void EvaluateStateTimeline(HonamiAnimator anim, int layer, int portIdx, HonamiState state, int stateIdx, Playable playable, bool hasModules)
        {
            float unscaledDuration = HonamiStateEvaluator.GetUnscaledStateDuration(anim.controller, state, stateIdx, anim.GetNodeRuntime(stateIdx), GetStateBlendParam(anim, stateIdx));
            if (unscaledDuration <= 0f) return;

            double rawTime = playable.GetTime();
            double lastRawTime = anim._portStates[layer][portIdx].LastFrameRawTime;
            anim._portStates[layer][portIdx].LastFrameRawTime = rawTime;

            if (hasModules)
            {
                bool entering = double.IsNaN(lastRawTime) || anim._activeModuleState[layer] != stateIdx;
                var previous = anim._stateModuleSets[stateIdx];
                var set = GetStateModules(anim, state, stateIdx);

                if (entering || set != previous)
                {
                    if (anim._activeModuleState[layer] == stateIdx)
                        previous?.StopAll();
                    if (set != previous)
                        previous?.Dispose();
                    anim._activeModuleState[layer] = stateIdx;
                }

                var nodeRuntime = anim.GetNodeRuntime(stateIdx);
                if (state.loop)
                    EvaluateLoopingModules(state, set, nodeRuntime, unscaledDuration, rawTime, entering ? double.NaN : lastRawTime);
                else
                    EvaluateSingleRunModules(state, set, nodeRuntime, unscaledDuration, rawTime, entering ? double.NaN : lastRawTime);
            }

            if (!state.loop)
                CheckStateFinished(anim, layer, portIdx, state, stateIdx, unscaledDuration, rawTime);
        }

        private static void EvaluateLoopingModules(HonamiState state, HonamiTimelineModuleSet set, HonamiNodeRuntime nodeRuntime, float duration, double rawTime, double lastRawTime)
        {
            float looped = Repeat(rawTime, duration);
            bool reversed = state.isReversed;
            float restart = reversed ? float.PositiveInfinity : float.NegativeInfinity;
            float end = reversed ? 0f : duration;

            if (double.IsNaN(lastRawTime))
            {
                RunModules(state, set, nodeRuntime, restart, looped);
                return;
            }

            long lastLoop = (long)System.Math.Floor(lastRawTime / duration);
            long currentLoop = (long)System.Math.Floor(rawTime / duration);
            float lastLooped = Repeat(lastRawTime, duration);

            if (lastLoop == currentLoop)
            {
                RunModules(state, set, nodeRuntime, lastLooped, looped);
                return;
            }

            long wraps = System.Math.Min(System.Math.Abs(currentLoop - lastLoop), MaxCatchUpLoops);
            RunModules(state, set, nodeRuntime, lastLooped, end);
            for (long i = 1; i < wraps; i++)
                RunModules(state, set, nodeRuntime, restart, end);
            RunModules(state, set, nodeRuntime, restart, looped);
        }

        private static void EvaluateSingleRunModules(HonamiState state, HonamiTimelineModuleSet set, HonamiNodeRuntime nodeRuntime, float duration, double rawTime, double lastRawTime)
        {
            float time = Mathf.Clamp((float)rawTime, 0f, duration);
            float previous = double.IsNaN(lastRawTime)
                ? (state.isReversed ? float.PositiveInfinity : float.NegativeInfinity)
                : Mathf.Clamp((float)lastRawTime, 0f, duration);
            RunModules(state, set, nodeRuntime, previous, time);
        }

        private static void RunModules(HonamiState state, HonamiTimelineModuleSet set, HonamiNodeRuntime nodeRuntime, float previousTime, float time)
        {
            var frame = new HonamiTimelineFrame(previousTime, time, false);
            set.Advance(in frame, state.node, nodeRuntime);
        }

        private static float Repeat(double time, float duration)
        {
            double looped = time % duration;
            if (looped < 0) looped += duration;
            return (float)looped;
        }

        private static void CheckStateFinished(HonamiAnimator anim, int layer, int portIdx, HonamiState state, int stateIdx, float unscaledDuration, double rawTime)
        {
            bool isFinished = state.isReversed ? (rawTime <= 0.0) : (rawTime >= (double)unscaledDuration);

            if (!anim._portStates[layer][portIdx].StateFinishedFired && isFinished)
            {
                anim._portStates[layer][portIdx].StateFinishedFired = true;
                anim.TryTriggerStateExited(layer, portIdx, HonamiStateExitReason.Completed);
                anim.TriggerStateFinished(state.stateName);
                anim.ReleaseFinishedStateIfNeeded(layer, portIdx, stateIdx);
            }
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

        public static bool EvaluatePortalRecursive(HonamiAnimator anim, HonamiState portalExit, int layer, int currentIdx, bool forceRestart, HonamiTransition originalTrans, List<int> originalTentative, int depth = 0)
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
                            return EvaluatePortalRecursive(anim, nextExit, layer, currentIdx, forceRestart, originalTrans, originalTentative, depth + 1);
                        }
                    }
                    else if (!nextState.node.IsVirtual)
                    {
                        anim._params.CommitTriggers(originalTentative);
                        anim._params.CommitTriggers(anim._exitTentativeBuffer);
                        ApplyTransitionAssignments(anim, originalTrans);
                        ApplyTransitionAssignments(anim, exitTrans);

                        anim.PlayStateByGuid(nextState.guid, originalTrans.duration, layer,
                            forceRestart,
                            originalTrans.ResolveBlendCurve(),
                            exitTrans.destinationStartTime);

                        HonamiPlaybackEngine.ApplyTransitionFreeze(anim, layer, originalTrans.freezeMode);
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

        public static float GetStateBlendParam(HonamiAnimator anim, int stateIndex)
        {
            if (anim._blendTreeParamHashes == null || stateIndex < 0 || stateIndex >= anim._blendTreeParamHashes.Length) return 0f;
            int hash = anim._blendTreeParamHashes[stateIndex];
            return hash != -1 ? anim._params.GetFloat(hash) : 0f;
        }
    }
}
