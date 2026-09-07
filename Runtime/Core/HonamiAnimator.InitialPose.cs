using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public partial class HonamiAnimator
    {
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

        protected override IReadOnlyList<Transform> GetDrivenTransforms(out int version)
        {
            var processor = _avatarEnabled ? _avatarProcessor : null;
            version = processor != null ? processor.DrivenVersion : -1;
            return processor != null ? processor.DrivenTransforms : null;
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

        protected override bool IsGraphIdle()
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
