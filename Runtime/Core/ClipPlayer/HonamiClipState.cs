using UnityEngine;
using UnityEngine.Animations;

namespace HonamiAnimationSystem.Runtime.Core
{
    public sealed class HonamiClipState
    {
        internal AnimationClipPlayable Playable;
        internal float RawWeight;
        internal float TargetWeight;
        internal float FadeRate;
        internal float Time;
        internal bool Forward = true;
        internal bool Finished;

        public int Index { get; internal set; }
        public string Name { get; internal set; }
        public AnimationClip Clip { get; internal set; }
        public int Layer { get; internal set; }
        public HonamiActionID LinkedActionId { get; internal set; }
        public bool Enabled { get; internal set; }

        public float Speed { get; set; } = 1f;
        public HonamiClipWrapMode WrapMode { get; set; } = HonamiClipWrapMode.Once;

        public float Length => Clip != null ? Mathf.Max(0.0001f, Clip.length) : 0.0001f;

        public float NormalizedTime
        {
            get => Time / Length;
            set => Time = Mathf.Clamp01(value) * Length;
        }

        public float CurrentTime
        {
            get => Time;
            set
            {
                Time = Mathf.Clamp(value, 0f, Length);
                Finished = false;
            }
        }

        public float Weight
        {
            get => RawWeight;
            set
            {
                RawWeight = Mathf.Clamp01(value);
                TargetWeight = RawWeight;
                FadeRate = 0f;
                Enabled = RawWeight > 0f;
            }
        }

        public bool IsPlaying => Enabled && (RawWeight > 0f || TargetWeight > 0f);

        internal void FadeTo(float target, float duration)
        {
            TargetWeight = Mathf.Clamp01(target);

            if (duration <= 0f)
            {
                RawWeight = TargetWeight;
                FadeRate = 0f;
            }
            else
            {
                FadeRate = 1f / duration;
            }

            if (TargetWeight > 0f) Enabled = true;
        }

        internal void Rewind()
        {
            Time = Speed < 0f ? Length : 0f;
            Forward = Speed >= 0f;
            Finished = false;
        }

        internal void Reset()
        {
            RawWeight = 0f;
            TargetWeight = 0f;
            FadeRate = 0f;
            Enabled = false;
            Rewind();
        }
    }
}
