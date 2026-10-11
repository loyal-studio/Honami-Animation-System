using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Animation clip entry used by one-dimensional Honami blend tree nodes.
    /// </summary>
    [Serializable]
    public sealed class HonamiBlendTreeMotion
    {
        public AnimationClip clip;
        public float threshold;
        public Vector2 position;
        public float speed = 1f;
        public bool mirror = false;
        [Tooltip("FreeformDirectional2D only: normalized cycle offset in [0, 1) added to this child's sample phase, like Unity's ChildMotion.cycleOffset (values set from script outside the range wrap cyclically). Weights, period, and the shared phase are unchanged.")]
        [Range(0f, 0.9999f)]
        public float cycleOffset = 0f;
    }

    /// <summary>
    /// Weighted animation clip entry used by random animation nodes.
    /// </summary>
    [Serializable]
    public sealed class HonamiRandomAnimationClip
    {
        public AnimationClip clip;
        public float weight = 1f;
        public float speed = 1f;
        public bool mirror = false;
        public bool muted = false;
        public float startTime = 0f;
        public float endTime = 0f;
    }

    /// <summary>
    /// Timeline segment entry used by sequencer animation nodes.
    /// </summary>
    [Serializable]
    public sealed class HonamiSequencedAnimationClip
    {
        public AnimationClip clip;
        public float startTime = 0f;
        public float speed = 1f;
    }
}
