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
            if (!CanReleaseFinishedState(layer, stateIdx)) return;
            if (portIdx != stateIdx) return;
            if (_layerStates[layer].CurrentStateIndex != stateIdx) return;
            if (_layerStates[layer].PreviousStateIndex != -1) return;
            if (_layerStates[layer].TransitionDuration > 0.0) return;

            if (StateHoldsLastFrame(stateIdx))
            {
                _layerStates[layer].HoldsLastFrame = true;
                return;
            }

            Stop(layer);
            RestoreInitialPoseIfIdle();
        }

        /// <summary>
        /// Drops every Freeze Last Frame hold on another layer once this layer starts a state, including a
        /// Freeze Last Frame state still playing toward its hold, so a held pose never outlives the state that replaced it.
        /// </summary>
        internal void ReleaseHoldsSupersededBy(int layer)
        {
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return;

            ClearLastFrameHold(layer);

            for (int i = 0; i < _layerStates.Length; i++)
            {
                if (i == layer || !HoldsOrAwaitsLastFrameHold(i)) continue;

                // A layer above the held one covers it, so the incoming blend still needs the held pose to fade out of.
                if (layer > i && _layerStates[layer].TransitionDuration > 0.0)
                {
                    _layerStates[i].HoldSuperseded = true;
                    _layerStates[i].HoldSupersededByLayer = layer;
                    continue;
                }

                ReleaseHeldLayer(i);
            }
        }

        internal void ReleaseSupersededHolds()
        {
            if (_layerStates == null) return;

            for (int i = 0; i < _layerStates.Length; i++)
            {
                if (!_layerStates[i].HoldSuperseded) continue;

                int supersedingLayer = _layerStates[i].HoldSupersededByLayer;
                bool stillBlending = supersedingLayer >= 0
                                     && supersedingLayer < _layerStates.Length
                                     && _layerStates[supersedingLayer].CurrentStateIndex != -1
                                     && _layerStates[supersedingLayer].TransitionDuration > 0.0;

                if (stillBlending) continue;

                ReleaseHeldLayer(i);
            }
        }

        private void ReleaseHeldLayer(int layer)
        {
            Stop(layer);
            RestoreInitialPoseIfIdle();
        }

        private void ClearLastFrameHold(int layer)
        {
            _layerStates[layer].HoldsLastFrame = false;
            _layerStates[layer].HoldSuperseded = false;
        }

        private bool CanReleaseFinishedState(int layer, int stateIdx)
        {
            if (!releaseFinishedStatesWithoutDefault || !restoreInitialPoseWhenIdle) return false;
            if (_defaultStateIndex != null && layer >= 0 && layer < _defaultStateIndex.Length && _defaultStateIndex[layer] != -1) return false;
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return false;
            return !StateHasOutgoingTransitions(stateIdx);
        }

        private bool HoldsOrAwaitsLastFrameHold(int layer)
        {
            if (_layerStates[layer].HoldsLastFrame) return true;

            int stateIdx = _layerStates[layer].CurrentStateIndex;
            if (stateIdx == -1 || !StateHoldsLastFrame(stateIdx) || _runtimeStates[stateIdx].loop) return false;

            return CanReleaseFinishedState(layer, stateIdx);
        }

        private bool StateHoldsLastFrame(int stateIdx)
        {
            if (_runtimeStates == null || stateIdx < 0 || stateIdx >= _runtimeStates.Length) return false;

            HonamiState state = _runtimeStates[stateIdx];
            return state != null && state.holdLastFrame;
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
