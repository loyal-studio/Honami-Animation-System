using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Pure logic state that plays no animation - it runs event markers, parameter assignments and Sub-Nodes for a set duration.
    /// </summary>
    public sealed class HonamiEventNode : HonamiNodeBase
    {
        [Tooltip("How long the state lasts, in seconds. Event markers, exit time and OnStateFinished all use this duration.")]
        public float duration = 1f;

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state)
        {
            // Must be a real playable: the engine skips states with invalid playables, so Playable.Null would make the state dead.
            return AnimationMixerPlayable.Create(graph, 0);
        }

        public override float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            float stateSpeed = state.speed != 0f ? Mathf.Abs(state.speed) : 1f;
            return GetClampedDuration() / stateSpeed;
        }

        public override float GetUnscaledDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            return GetClampedDuration();
        }

        public override void UpdateRuntime(in HonamiExecutionContext ctx)
        {
        }

        private float GetClampedDuration()
        {
            return Mathf.Max(0.001f, duration);
        }
    }
}
