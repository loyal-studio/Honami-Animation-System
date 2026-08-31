using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public partial class HonamiAnimator
    {
        private struct HonamiTransformPose
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        private HonamiTransformPose[] _initialPose;
        private bool _hasInitialPose;

        private HonamiTransformPose[] _globalWeightPoses;
        private bool _globalWeightPosesDirty = true;
        private int _globalWeightDrivenVersion = -1;
        private HonamiAvatarProcessor _globalWeightPosesProcessor;

        /// <summary>
        /// True after Honami captured a runtime initial pose snapshot for this hierarchy.
        /// </summary>
        public bool HasInitialPose => _hasInitialPose;

        /// <summary>
        /// Captures the current local transform pose of this Honami hierarchy.
        /// Call this manually if another setup script changes the rig before Honami should define its rest pose.
        /// </summary>
        public void CaptureInitialPose()
        {
            var transforms = new System.Collections.Generic.List<Transform>();
            GetComponentsInChildren<Transform>(true, transforms);
            int skipRoot = includeRootTransformInInitialPose ? 0 : 1;
            int poseCount = Mathf.Max(0, transforms.Count - skipRoot);

            _initialPose = new HonamiTransformPose[poseCount];

            for (int i = 0; i < poseCount; i++)
            {
                Transform target = transforms[i + skipRoot];
                _initialPose[i] = new HonamiTransformPose
                {
                    Transform = target,
                    LocalPosition = target.localPosition,
                    LocalRotation = target.localRotation,
                    LocalScale = target.localScale
                };
            }

            _hasInitialPose = true;
            _globalWeightPosesDirty = true;
        }

        /// <summary>
        /// Captures the initial pose from the final frame of the default state.
        /// Useful when the Awake pose is an Equip animation first frame and you want the bind pose to be the Idle state.
        /// </summary>
        public void CaptureInitialPoseFromDefaultState()
        {
            if (controller == null || !_playableGraph.IsValid()) return;
            
            HonamiState defaultState = null;
            int defaultIndex = -1;
            int defaultLayer = -1;
            
            for (int i = 0; i < _activeStatesCount; i++)
            {
                if (_runtimeStates[i] != null && _runtimeStates[i].isDefaultState)
                {
                    defaultState = _runtimeStates[i];
                    defaultIndex = i;
                    defaultLayer = defaultState.layerIndex;
                    break;
                }
            }

            if (defaultState != null && defaultLayer >= 0)
            {
                // Play default state
                PlayStateInternal(defaultIndex, 0f, defaultLayer, true, null, 0f);
                
                // Fast forward to end
                float dur = HonamiStateEvaluator.GetUnscaledStateDuration(controller, defaultState, defaultIndex, GetNodeRuntime(defaultIndex), GetStateBlendParam(defaultIndex));
                if (dur <= 0f) dur = 1f; // Fallback
                _playableGraph.Evaluate(dur);
                
                // Force animator to flush playable outputs to transforms
                if (_animator != null) _animator.Update(0f);
                
                // Capture pose
                CaptureInitialPose();
                
                // Reset state back to start
                PlayStateInternal(defaultIndex, 0f, defaultLayer, true, null, 0f);
                _playableGraph.Evaluate(0f);
                if (_animator != null) _animator.Update(0f);
            }
        }

        /// <summary>
        /// Restores the last captured initial pose snapshot.
        /// </summary>
        public void RestoreInitialPose()
        {
            if (!_hasInitialPose || _initialPose == null) return;

            for (int i = 0; i < _initialPose.Length; i++)
            {
                HonamiTransformPose pose = _initialPose[i];
                if (pose.Transform == null) continue;

                pose.Transform.localPosition = pose.LocalPosition;
                pose.Transform.localRotation = pose.LocalRotation;
                pose.Transform.localScale = pose.LocalScale;
            }
        }

        private void RestoreInitialPoseIfIdle()
        {
            if (!restoreInitialPoseWhenIdle || !_hasInitialPose || !IsGraphIdle()) return;
            RestoreInitialPose();
        }

        private void ApplyGlobalWeightBlend()
        {
            if (globalWeightMode != HonamiGlobalWeightMode.Init) return;
            if (_globalWeight >= 0.9999f || !_hasInitialPose || _initialPose == null) return;

            HonamiTransformPose[] poses = ResolveGlobalWeightPoses();
            if (poses == null) return;

            float toRest = 1f - _globalWeight;
            for (int i = 0; i < poses.Length; i++)
            {
                HonamiTransformPose pose = poses[i];
                if (pose.Transform == null) continue;

                pose.Transform.localPosition = Vector3.Lerp(pose.Transform.localPosition, pose.LocalPosition, toRest);
                pose.Transform.localRotation = Quaternion.Slerp(pose.Transform.localRotation, pose.LocalRotation, toRest);
                pose.Transform.localScale = Vector3.Lerp(pose.Transform.localScale, pose.LocalScale, toRest);
            }
        }

        // Scope the weight blend to replacer-resolved driven bones, else it drags bones owned by another animator (e.g. FPS arms under the camera) toward rest.
        private HonamiTransformPose[] ResolveGlobalWeightPoses()
        {
            var processor = _avatarEnabled ? _avatarProcessor : null;
            var driven = processor != null ? processor.DrivenTransforms : null;
            int drivenVersion = processor != null ? processor.DrivenVersion : -1;

            if (!_globalWeightPosesDirty && _globalWeightDrivenVersion == drivenVersion && ReferenceEquals(_globalWeightPosesProcessor, processor))
                return _globalWeightPoses;

            _globalWeightPosesDirty = false;
            _globalWeightDrivenVersion = drivenVersion;
            _globalWeightPosesProcessor = processor;

            int drivenCount = driven != null ? driven.Count : -1;

            // No avatar system: keep legacy whole-hierarchy blend. With an avatar, scope strictly to its driven bones (empty while rebinding => relax nothing rather than the arms).
            if (driven == null)
            {
                _globalWeightPoses = _initialPose;
                return _globalWeightPoses;
            }

            if (drivenCount == 0)
            {
                _globalWeightPoses = System.Array.Empty<HonamiTransformPose>();
                return _globalWeightPoses;
            }

            var drivenSet = new HashSet<Transform>(driven);
            var scoped = new List<HonamiTransformPose>(drivenCount);
            for (int i = 0; i < _initialPose.Length; i++)
            {
                if (drivenSet.Contains(_initialPose[i].Transform))
                    scoped.Add(_initialPose[i]);
            }

            _globalWeightPoses = scoped.ToArray();
            return _globalWeightPoses;
        }

        internal void ReleaseFinishedStateIfNeeded(int layer, int portIdx, int stateIdx)
        {
            if (!releaseFinishedStatesWithoutDefault || !restoreInitialPoseWhenIdle) return;
            if (_defaultStateIndex != null && layer >= 0 && layer < _defaultStateIndex.Length && _defaultStateIndex[layer] != -1) return;
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return;
            if (portIdx != stateIdx) return;
            if (_layerStates[layer].CurrentStateIndex != stateIdx) return;
            if (_layerStates[layer].PreviousStateIndex != -1) return;
            if (_layerStates[layer].TransitionDuration > 0.0) return;
            if (StateHasOutgoingTransitions(stateIdx)) return;

            Stop(layer);
            RestoreInitialPoseIfIdle();
        }

        private bool StateHasOutgoingTransitions(int stateIdx)
        {
            if (controller == null || _runtimeStates == null || stateIdx < 0 || stateIdx >= _runtimeStates.Length) return false;

            var transitions = controller.GetTransitions(_runtimeStates[stateIdx]);
            return transitions != null && transitions.Count > 0;
        }

        private bool IsGraphIdle()
        {
            if (_layerStates == null || _layerStates.Length == 0) return true;

            for (int i = 0; i < _layerStates.Length; i++)
            {
                if (_layerStates[i].CurrentStateIndex != -1) return false;
                if (_layerStates[i].PreviousStateIndex != -1) return false;
                if (_layerStates[i].TransitionDuration > 0.0) return false;
            }

            return true;
        }
    }
}
