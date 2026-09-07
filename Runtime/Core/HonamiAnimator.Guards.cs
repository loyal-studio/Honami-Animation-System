using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public partial class HonamiAnimator
    {
        private bool HasController => controller != null;

        private bool IsLayerInRange(int layer)
        {
            return _layerStates != null &&
                   _pausedStateIndices != null &&
                   layer >= 0 &&
                   layer < _layerStates.Length &&
                   layer < _pausedStateIndices.Length;
        }

        private bool TryGetLayerMixer(int layer, out AnimationMixerPlayable mixer)
        {
            mixer = default;
            if (_layerMixers == null || layer < 0 || layer >= _layerMixers.Count) return false;

            mixer = _layerMixers[layer];
            return mixer.IsValid();
        }

        private float GetConfiguredStateSpeed(int stateIndex)
        {
            if (!HasController || stateIndex < 0 || stateIndex >= _activeStatesCount) return 1f;

            HonamiState state = _runtimeStates[stateIndex];
            return state.isReversed ? -state.speed : state.speed;
        }
    }
}
