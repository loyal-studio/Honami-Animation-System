using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Immutable runtime context passed to Honami nodes and sub-nodes during evaluation.
    /// </summary>
    public readonly struct HonamiExecutionContext
    {
        public readonly HonamiAnimator Animator;
        public readonly HonamiState State;
        public readonly int StateIndex;
        public readonly int Layer;
        public readonly int PortIndex;
        public readonly Playable Playable;
        public readonly AnimationMixerPlayable LayerMixer;
        public readonly HonamiParameterStore Params;
        public readonly HonamiNodeRuntime Runtime;
        public readonly int[] BlendTreeParamHashes;
        public readonly float DeltaTime;

        public HonamiExecutionContext(
            HonamiAnimator animator,
            HonamiState state,
            int stateIndex,
            int layer,
            int portIndex,
            Playable playable,
            AnimationMixerPlayable layerMixer,
            HonamiParameterStore parms,
            HonamiNodeRuntime runtime,
            int[] blendTreeParamHashes,
            float deltaTime)
        {
            Animator = animator;
            State = state;
            StateIndex = stateIndex;
            Layer = layer;
            PortIndex = portIndex;
            Playable = playable;
            LayerMixer = layerMixer;
            Params = parms;
            Runtime = runtime;
            BlendTreeParamHashes = blendTreeParamHashes;
            DeltaTime = deltaTime;
        }

        /// <summary>
        /// Attempts to read an extension component from the owning animator GameObject.
        /// </summary>
        public bool TryGetExtension<T>(out T extension) where T : class
        {
            if (Animator != null && Animator.TryGetComponent(out extension))
            {
                return true;
            }

            extension = null;
            return false;
        }
    }
}
