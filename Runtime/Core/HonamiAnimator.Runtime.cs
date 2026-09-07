using UnityEngine;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public partial class HonamiAnimator
    {
        private void ApplyConstraints()
        {
            if (!_constraintsEnabled) return;

            if (!_constraints.AnyLockActive || HasLayerWithoutActiveTransition())
                _constraints.UpdateBaselines();

            _constraints.Apply(
                _runtimeStates, _activeStatesCount, _layerMixer, _layerMixers,
                _layerStates, TransientPortIndex);
        }

        private bool HasLayerWithoutActiveTransition()
        {
            if (_layerStates == null) return false;

            for (int i = 0; i < _layerStates.Length; i++)
            {
                if (_layerStates[i].TransitionTime <= 0.0)
                    return true;
            }

            return false;
        }

        private void ApplyAvatarProcessors()
        {
            if (!_avatarEnabled) return;

            _avatarProcessor.Apply(
                _runtimeStates, _activeStatesCount, controller, _layerMixer, _layerMixers,
                _layerStates, TransientPortIndex);

            UpdateGlobalMirrorWeight();
        }

        private void UpdateGlobalMirrorWeight()
        {
            if (!_globalMirrorPlayable.IsValid()) return;

            float targetWeight = _mirrorAvatar ? 1f : 0f;
            _currentGlobalMirrorWeight = _mirrorBlendSpeed <= 0.001f
                ? targetWeight
                : Mathf.MoveTowards(_currentGlobalMirrorWeight, targetWeight, (float)(_cachedDeltaTime * _mirrorBlendSpeed));

            HonamiMirrorJob mirrorJob = _globalMirrorPlayable.GetJobData<HonamiMirrorJob>();
            if (Mathf.Abs(mirrorJob.weight - _currentGlobalMirrorWeight) <= 0.001f) return;

            mirrorJob.weight = _currentGlobalMirrorWeight;
            _globalMirrorPlayable.SetJobData(mirrorJob);
        }
    }
}
