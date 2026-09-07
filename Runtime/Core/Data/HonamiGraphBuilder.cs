using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Events;
using HonamiAnimationSystem.Runtime.Riggings;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Builds and refreshes the runtime playable graph used by <see cref="HonamiAnimator"/>.
    /// </summary>
    public static class HonamiGraphBuilder
    {
        public static void InitializeGraph(HonamiAnimator anim, bool keepGraph = false, bool forceRebuildStates = false)
        {
            if (!keepGraph)
            {
                DestroyExistingGraph(anim);
            }

            if (anim.controller == null)
            {
                return;
            }

            bool isSameController = !forceRebuildStates && anim._lastBuiltController == anim.controller;
            anim._lastBuiltController = anim.controller;

            CacheRuntimeStates(anim);
            ClearRuntimeCaches(anim);

            int layerCount = Mathf.Max(1, anim.controller.ActiveLayers.Count);
            int portCount = anim._activeStatesCount + 1;

            AllocateLayerState(anim, layerCount, portCount, isSameController);
            CreateRootGraph(anim, layerCount);
            InitializeProcessors(anim);
            BuildStateMetadata(anim);
            AllocateBlendCaches(anim, layerCount, portCount);
            BuildLayerPlayables(anim, layerCount, portCount);
            anim.InsertRigChain();
            BakePortals(anim);
            BakeAnyStateLookup(anim, layerCount);
            BakeDefaultStateLookup(anim, layerCount);
        }

        public static void BuildMetadataForState(HonamiAnimator anim, HonamiState state, int index)
        {
            EnsureStateGuid(state, index);

            int guidHash = state.guid.GetHashCode();
            int nameHash = HonamiAnimator.StringToHash(state.stateName);

#if UNITY_EDITOR
            if (anim._stateGuidToIndex.ContainsKey(guidHash))
            {
                Debug.LogError($"[Honami] GUID hash collision: state '{state.stateName}' (GUID: {state.guid}) shares a hash with another state. Lookup by GUID may silently fail.");
            }

            if (anim._stateNameToIndex.ContainsKey(nameHash))
            {
                Debug.LogError($"[Honami] Name hash collision: state '{state.stateName}' shares a hash. PlayState(\"{state.stateName}\") may resolve to the wrong state.");
            }
#endif

            anim._stateGuidToIndex[guidHash] = index;
            anim._stateNameToIndex[nameHash] = index;

            var activeNode = anim.controller.GetActiveNode(state);
            string blendParameterName = activeNode != null ? activeNode.GetBlendParameterName() : null;
            if (!string.IsNullOrEmpty(blendParameterName))
            {
                anim._blendTreeParamHashes[index] = HonamiAnimator.StringToHash(blendParameterName);
            }

            BakeTransitionMetadata(anim, state);
            BakeStateAssignmentMetadata(anim, state);

            if (anim._nodeRuntimes != null && index < anim._nodeRuntimes.Length)
            {
                anim._nodeRuntimes[index] = activeNode != null ? activeNode.CreateRuntime() : null;
            }

            activeNode?.OnBuildMetadata(index, state, anim._anyStateIndices);
        }

        public static void BakePortals(HonamiAnimator anim)
        {
            for (int i = 0; i < anim._activeStatesCount; i++)
            {
                var state = anim._runtimeStates[i];
                var transitions = anim.controller.GetTransitions(state);
                if (transitions == null)
                {
                    continue;
                }

                for (int transitionIndex = 0; transitionIndex < transitions.Count; transitionIndex++)
                {
                    var transition = transitions[transitionIndex];
                    if (!TryGetPortalEntrance(anim, transition, out HonamiPortalEntranceNode entrance))
                    {
                        continue;
                    }

                    HonamiState exit = FindPortalExit(anim, entrance.portalName);
                    if (exit != null)
                    {
                        anim._bakedPortalExits[transition] = exit;
                    }
                }
            }
        }

        public static void PlayDefaultStates(HonamiAnimator anim)
        {
            if (anim.controller == null)
            {
                return;
            }

            int layerCount = anim._layerStates != null ? anim._layerStates.Length : anim.controller.ActiveLayers.Count;
            if (layerCount == 0)
            {
                return;
            }

            for (int layer = 0; layer < layerCount; layer++)
            {
                if (anim._layerStates != null &&
                    layer < anim._layerStates.Length &&
                    anim._layerStates[layer].CurrentStateIndex != -1)
                {
                    continue;
                }

                int defaultIndex = anim._defaultStateIndex != null && layer < anim._defaultStateIndex.Length
                    ? anim._defaultStateIndex[layer]
                    : -1;

                if (defaultIndex >= 0 && defaultIndex < anim._activeStatesCount)
                {
                    anim.PlayStateInternal(defaultIndex, 0f, layer, true, null, 0f);
                }
            }
        }

        private static void DestroyExistingGraph(HonamiAnimator anim)
        {
            if (anim._playableGraph.IsValid())
            {
                anim._playableGraph.Destroy();
            }

            anim._avatarProcessor?.Dispose();
            anim._constraints?.Dispose();
        }

        private static void CacheRuntimeStates(HonamiAnimator anim)
        {
            anim._activeStatesCount = anim.controller.ActiveStates.Count;
            anim._runtimeStates = new HonamiState[anim._activeStatesCount];

            for (int i = 0; i < anim._activeStatesCount; i++)
            {
                anim._runtimeStates[i] = anim.controller.ActiveStates[i];
            }

            anim._blendTreeParamHashes = new int[anim._activeStatesCount];
            for (int i = 0; i < anim._activeStatesCount; i++)
            {
                anim._blendTreeParamHashes[i] = -1;
            }

            anim._nodeRuntimes = new HonamiNodeRuntime[anim._activeStatesCount];
        }

        private static void ClearRuntimeCaches(HonamiAnimator anim)
        {
            anim._stateGuidToIndex.Clear();
            anim._stateNameToIndex.Clear();
            anim._conditionParamHashes.Clear();
            anim._assignmentParamHashes.Clear();
            anim._anyStateIndices.Clear();
            anim._bakedPortalExits.Clear();
            anim._bakedPortalTargets.Clear();
            anim._layerMixers.Clear();
        }

        private static void AllocateLayerState(HonamiAnimator anim, int layerCount, int portCount, bool isSameController)
        {
            if (CanReuseLayerState(anim, layerCount, portCount, isSameController))
            {
                return;
            }

            anim._layerStates = new HonamiLayerState[layerCount];
            anim._portStates = new HonamiPortState[layerCount][];
            anim._activeTransitionCurve = new AnimationCurve[layerCount];
            anim._activeVictimCurve = new AnimationCurve[layerCount];
            anim._firedEventsPerPort = new HashSet<HonamiEventMarker>[layerCount][];
            anim._pausedStateIndices = new HashSet<int>[layerCount];

            for (int layer = 0; layer < layerCount; layer++)
            {
                anim._layerStates[layer] = CreateDefaultLayerState();
                anim._portStates[layer] = new HonamiPortState[portCount];
                anim._firedEventsPerPort[layer] = new HashSet<HonamiEventMarker>[portCount];

                for (int port = 0; port < portCount; port++)
                {
                    anim._firedEventsPerPort[layer][port] = new HashSet<HonamiEventMarker>();
                    anim._portStates[layer][port] = CreateDefaultPortState();
                }

                anim._pausedStateIndices[layer] = new HashSet<int>();
            }
        }

        private static bool CanReuseLayerState(HonamiAnimator anim, int layerCount, int portCount, bool isSameController)
        {
            return isSameController &&
                   anim._layerStates != null &&
                   anim._layerStates.Length == layerCount &&
                   anim._portStates != null &&
                   anim._portStates.Length == layerCount &&
                   anim._portStates[0] != null &&
                   anim._portStates[0].Length == portCount;
        }

        private static HonamiLayerState CreateDefaultLayerState()
        {
            return new HonamiLayerState
            {
                CurrentStateIndex = -1,
                PreviousStateIndex = -1,
                TransientStateIndex = -1,
                TransitionStartPrevWeight = 0f,
                TransitionStartLayerWeight = 0f,
                IsLayerPaused = false
            };
        }

        private static HonamiPortState CreateDefaultPortState()
        {
            return new HonamiPortState
            {
                LastFrameRawTime = -1.0,
                CurrentLoopCount = 0,
                StateFinishedFired = false,
                StateExitedFired = false,
                WeightSnapshot = 0f
            };
        }

        private static void CreateRootGraph(HonamiAnimator anim, int layerCount)
        {
            if (!anim._playableGraph.IsValid())
            {
                anim._playableGraph = PlayableGraph.Create(anim.gameObject.name + "_HonamiGraph");
                anim._playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                anim._playableOutput = AnimationPlayableOutput.Create(anim._playableGraph, "AnimationOutput", anim._animator);
                anim._playableOutput.SetWeight(anim.GlobalWeightMode == HonamiGlobalWeightMode.Bind ? anim.GlobalWeight : 1f);
            }

            anim._layerMixer = AnimationLayerMixerPlayable.Create(anim._playableGraph, layerCount);
            anim._playableOutput.SetSourcePlayable(anim._layerMixer);
        }

        private static void InitializeProcessors(HonamiAnimator anim)
        {
            InitializeConstraintProcessor(anim);
            InitializeAvatarProcessor(anim);
        }

        private static void InitializeConstraintProcessor(HonamiAnimator anim)
        {
            anim._constraintsEnabled = false;

            for (int i = 0; i < anim._activeStatesCount; i++)
            {
                var state = anim._runtimeStates[i];
                if (state != null && state.constraints.AnyLocked())
                {
                    anim._constraintsEnabled = true;
                    break;
                }
            }

            if (anim._constraintsEnabled)
            {
                anim._constraints.Initialize(anim._animator, anim._playableGraph, anim._layerMixer, anim.avatar);
            }
        }

        private static void InitializeAvatarProcessor(HonamiAnimator anim)
        {
            anim._avatarEnabled = anim.avatar != null;
            if (!anim._avatarEnabled)
            {
                return;
            }

            bool dummiesCreated = anim._avatarProcessor.EnsureDummyBones(anim._animator, anim.avatar);
            RebindAnimatorIfNeeded(anim, dummiesCreated);
            anim._avatarProcessor.PreInitialize(anim._animator, anim.avatar, anim._playableGraph, anim.controller);
        }

        private static void RebindAnimatorIfNeeded(HonamiAnimator anim, bool shouldRebind)
        {
            var runtimeController = anim._animator.runtimeAnimatorController;
            var avatar = anim._animator.avatar;

            anim._animator.runtimeAnimatorController = null;
            anim._animator.avatar = null;

            if (shouldRebind)
            {
                anim._animator.Rebind();
            }

            anim._animator.runtimeAnimatorController = runtimeController;
            anim._animator.avatar = avatar;
        }

        private static void BuildStateMetadata(HonamiAnimator anim)
        {
            for (int i = 0; i < anim._activeStatesCount; i++)
            {
                var state = anim._runtimeStates[i];
                if (state != null)
                {
                    BuildMetadataForState(anim, state, i);
                }
            }
        }

        private static void AllocateBlendCaches(HonamiAnimator anim, int layerCount, int portCount)
        {
            anim._pCountTotal = portCount;

            if (anim._blendStateValues.IsCreated)
            {
                anim._blendStateValues.Dispose();
            }

            anim._blendStateValues = new NativeArray<float>(layerCount * portCount * 2, Allocator.Persistent);
            anim._blendParamIndices = new int[layerCount * portCount];

            for (int i = 0; i < anim._blendParamIndices.Length; i++)
            {
                anim._blendParamIndices[i] = -1;
            }
        }

        private static void BuildLayerPlayables(HonamiAnimator anim, int layerCount, int portCount)
        {
            var perLayerPlayables = new List<Playable>(layerCount);

            for (int layer = 0; layer < layerCount; layer++)
            {
                var layerMixer = AnimationMixerPlayable.Create(anim._playableGraph, portCount);
                anim._layerMixers.Add(layerMixer);

                RestoreLayerMixerWeights(anim, layerMixer, layer, portCount);
                anim._layerMixer.SetInputWeight(layer, GetInitialLayerWeight(anim, layer));

                BuildStatePlayablesForLayer(anim, layerMixer, layer, portCount);

                Playable layerOutput = WrapLayerMirrorIfNeeded(anim, layerMixer, layer);
                perLayerPlayables.Add(layerOutput);

                if (!anim._avatarEnabled || !anim._avatarProcessor.HasBones)
                {
                    anim._playableGraph.Connect(layerOutput, 0, anim._layerMixer, layer);
                }
            }

            if (anim._avatarEnabled && anim._avatarProcessor.HasBones)
            {
                SetupAvatarCascade(anim, perLayerPlayables);
            }
        }

        private static void RestoreLayerMixerWeights(HonamiAnimator anim, AnimationMixerPlayable layerMixer, int layer, int portCount)
        {
            if (anim._layerStates == null || layer >= anim._layerStates.Length)
            {
                return;
            }

            SetInputWeightIfValid(layerMixer, anim._layerStates[layer].CurrentStateIndex, portCount);
            SetInputWeightIfValid(layerMixer, anim._layerStates[layer].PreviousStateIndex, portCount);
            SetInputWeightIfValid(layerMixer, anim._layerStates[layer].TransientStateIndex, portCount);
        }

        private static void SetInputWeightIfValid(AnimationMixerPlayable mixer, int inputIndex, int inputCount)
        {
            if (inputIndex != -1 && inputIndex < inputCount)
            {
                mixer.SetInputWeight(inputIndex, 1f);
            }
        }

        private static float GetInitialLayerWeight(HonamiAnimator anim, int layer)
        {
            if (layer != 0)
            {
                return 0f;
            }

            return layer < anim.controller.ActiveLayers.Count ? anim.controller.ActiveLayers[layer].weight : 1f;
        }

        private static void BuildStatePlayablesForLayer(
            HonamiAnimator anim,
            AnimationMixerPlayable layerMixer,
            int layer,
            int portCount)
        {
            for (int i = 0; i < anim._activeStatesCount; i++)
            {
                var state = anim._runtimeStates[i];
                if (state == null || state.layerIndex != layer)
                {
                    continue;
                }

                CacheBlendParameterIndex(anim, state, layer, i, portCount);

                Func<PlayableGraph, Playable> mirrorFactory = anim._avatarEnabled && anim._avatarProcessor.HasBones
                    ? anim._avatarProcessor.CreateMirrorPlayable
                    : null;

                var playable = HonamiPlayableFactory.CreateAndConnect(anim._playableGraph, anim.controller, state, layerMixer, i, mirrorFactory);
                WrapStateMirrorIfNeeded(anim, state, layerMixer, playable, i);
            }
        }

        private static void CacheBlendParameterIndex(HonamiAnimator anim, HonamiState state, int layer, int stateIndex, int portCount)
        {
            if (state.node is not HonamiBlendTreeNode blendTreeNode)
            {
                return;
            }

            int cacheIndex = layer * portCount + stateIndex;
            int parameterHash = HonamiAnimator.StringToHash(blendTreeNode.blendParameter);

            if (anim._params.TryGetFloatIndex(parameterHash, out int parameterIndex))
            {
                anim._blendParamIndices[cacheIndex] = parameterIndex;
            }
        }

        private static void WrapStateMirrorIfNeeded(
            HonamiAnimator anim,
            HonamiState state,
            AnimationMixerPlayable layerMixer,
            Playable playable,
            int inputIndex)
        {
            if (!ShouldMirrorState(anim, state, playable))
            {
                return;
            }

            var mirrorPlayable = anim._avatarProcessor.CreateMirrorPlayable(anim._playableGraph);
            if (!mirrorPlayable.IsValid())
            {
                return;
            }

            anim._playableGraph.Disconnect(layerMixer, inputIndex);
            mirrorPlayable.SetInputCount(1);
            anim._playableGraph.Connect(playable, 0, mirrorPlayable, 0);
            mirrorPlayable.SetInputWeight(0, 1f);
            anim._playableGraph.Connect(mirrorPlayable, 0, layerMixer, inputIndex);
        }

        private static bool ShouldMirrorState(HonamiAnimator anim, HonamiState state, Playable playable)
        {
            if (!anim._avatarEnabled || !anim._avatarProcessor.HasBones || !playable.IsValid())
            {
                return false;
            }

            return state.mirror || HasDynamicMirrorSubNode(state);
        }

        private static bool HasDynamicMirrorSubNode(HonamiState state)
        {
            if (state.subNodes == null)
            {
                return false;
            }

            for (int i = 0; i < state.subNodes.Count; i++)
            {
                if (state.subNodes[i] != null && state.subNodes[i].RequiresMirroring)
                {
                    return true;
                }
            }

            return false;
        }

        private static Playable WrapLayerMirrorIfNeeded(HonamiAnimator anim, AnimationMixerPlayable layerMixer, int layer)
        {
            if (layer >= anim.controller.ActiveLayers.Count ||
                !anim.controller.ActiveLayers[layer].mirror ||
                !anim._avatarEnabled ||
                !anim._avatarProcessor.HasBones)
            {
                return layerMixer;
            }

            var mirrorPlayable = anim._avatarProcessor.CreateMirrorPlayable(anim._playableGraph);
            if (!mirrorPlayable.IsValid())
            {
                return layerMixer;
            }

            mirrorPlayable.SetInputCount(1);
            mirrorPlayable.ConnectInput(0, layerMixer, 0, 1f);
            return mirrorPlayable;
        }

        private static void SetupAvatarCascade(HonamiAnimator anim, List<Playable> perLayerPlayables)
        {
            anim._avatarProcessor.SetupCascade(anim._playableGraph, perLayerPlayables, anim._layerMixer);
            anim._globalMirrorPlayable = (AnimationScriptPlayable)anim._avatarProcessor.CreateMirrorPlayable(anim._playableGraph);

            if (!anim._globalMirrorPlayable.IsValid())
            {
                return;
            }

            anim._currentGlobalMirrorWeight = anim.MirrorAvatar ? 1f : 0f;
            var mirrorJob = anim._globalMirrorPlayable.GetJobData<HonamiMirrorJob>();
            mirrorJob.weight = anim._currentGlobalMirrorWeight;
            anim._globalMirrorPlayable.SetJobData(mirrorJob);

            var output = anim._playableGraph.GetOutput(0);
            if (output.IsPlayableOutputOfType<AnimationPlayableOutput>())
            {
                Playable currentSource = output.GetSourcePlayable();
                anim._globalMirrorPlayable.AddInput(currentSource, 0, 1f);
                output.SetSourcePlayable(anim._globalMirrorPlayable);
            }
        }

        private static void BakeAnyStateLookup(HonamiAnimator anim, int layerCount)
        {
            anim._anyStateIndicesByLayer = new List<int>[layerCount];

            for (int i = 0; i < layerCount; i++)
            {
                anim._anyStateIndicesByLayer[i] = new List<int>();
            }

            for (int i = 0; i < anim._anyStateIndices.Count; i++)
            {
                int stateIndex = anim._anyStateIndices[i];
                int layerIndex = anim._runtimeStates[stateIndex].layerIndex;

                if (layerIndex >= 0 && layerIndex < layerCount)
                {
                    anim._anyStateIndicesByLayer[layerIndex].Add(stateIndex);
                }
            }
        }

        private static void BakeDefaultStateLookup(HonamiAnimator anim, int layerCount)
        {
            anim._defaultStateIndex = new int[layerCount];

            for (int i = 0; i < layerCount; i++)
            {
                anim._defaultStateIndex[i] = -1;
            }

            for (int i = 0; i < anim._activeStatesCount; i++)
            {
                var state = anim._runtimeStates[i];
                if (state == null)
                {
                    continue;
                }

                int layerIndex = state.layerIndex;
                if (layerIndex >= 0 &&
                    layerIndex < layerCount &&
                    layerIndex < anim.controller.ActiveLayers.Count &&
                    state.isDefaultState &&
                    anim._defaultStateIndex[layerIndex] == -1)
                {
                    anim._defaultStateIndex[layerIndex] = i;
                }
            }
        }

        private static void EnsureStateGuid(HonamiState state, int index)
        {
            if (!string.IsNullOrEmpty(state.guid))
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                state.guid = Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(state);
            }
#endif

            if (string.IsNullOrEmpty(state.guid))
            {
                state.guid = $"temp_{index}_{state.stateName}";
            }
        }

        private static void BakeTransitionMetadata(HonamiAnimator anim, HonamiState state)
        {
            var transitions = anim.controller.GetTransitions(state);
            if (transitions == null)
            {
                return;
            }

            for (int i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                transition.BakeHashes();

                for (int conditionIndex = 0; conditionIndex < transition.conditions.Count; conditionIndex++)
                {
                    var condition = transition.conditions[conditionIndex];
                    anim._conditionParamHashes[condition] = HonamiAnimator.StringToHash(condition.parameter);
                }

                for (int assignmentIndex = 0; assignmentIndex < transition.parameterAssignments.Count; assignmentIndex++)
                {
                    var assignment = transition.parameterAssignments[assignmentIndex];
                    anim._assignmentParamHashes[assignment] = HonamiAnimator.StringToHash(assignment.parameterName);
                }
            }
        }

        private static void BakeStateAssignmentMetadata(HonamiAnimator anim, HonamiState state)
        {
            for (int i = 0; i < state.parameterAssignments.Count; i++)
            {
                var assignment = state.parameterAssignments[i];
                anim._assignmentParamHashes[assignment] = HonamiAnimator.StringToHash(assignment.parameterName);
            }
        }

        private static bool TryGetPortalEntrance(
            HonamiAnimator anim,
            HonamiTransition transition,
            out HonamiPortalEntranceNode entrance)
        {
            entrance = null;

            if (!transition.hasTargetGuid)
            {
                return false;
            }

            if (!anim._stateGuidToIndex.TryGetValue(transition.targetStateGuidHash, out int targetIndex))
            {
                return false;
            }

            var targetState = anim._runtimeStates[targetIndex];
            if (targetState.node is not HonamiPortalEntranceNode portalEntrance)
            {
                return false;
            }

            entrance = portalEntrance;
            return true;
        }

        private static HonamiState FindPortalExit(HonamiAnimator anim, string portalName)
        {
            for (int i = 0; i < anim._activeStatesCount; i++)
            {
                var state = anim._runtimeStates[i];
                if (state != null &&
                    state.node is HonamiPortalExitNode exitNode &&
                    exitNode.portalName == portalName)
                {
                    return state;
                }
            }

            return null;
        }
    }
}
