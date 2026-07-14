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
        public float speed = 1f;
        public bool mirror = false;
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
