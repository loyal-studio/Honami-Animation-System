using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class TimelineAnimationClipItem : HonamiTimelineItem, IHonamiTimelineBlendCurveItem
    {
        public AnimationClip clip;
        [Min(0f)] public float clipStartOffset;
        [Tooltip("0 = play the clip to its end")]
        [Min(0f)] public float durationOverride;
        public float speed = 1f;
        public bool reversed;
        [Tooltip("Repeat the clip when the item is longer than the clip")]
        public bool loop;
        [Tooltip("Seconds to ease in from the pose underneath (the character's own animation or an earlier clip)")]
        [Min(0f)] public float blendIn;
        [Tooltip("Shape of the ease in: a preset ease or a custom curve.")]
        public HonamiBlendCurve blendInCurve = HonamiBlendCurve.SmoothStep;
        [Tooltip("Seconds to ease back out to the pose underneath")]
        [Min(0f)] public float blendOut;
        [Tooltip("Shape of the ease out: a preset ease or a custom curve.")]
        public HonamiBlendCurve blendOutCurve = HonamiBlendCurve.SmoothStep;

        public float EffectiveSpeed => speed != 0f ? Mathf.Abs(speed) : 1f;

        public float BlendIn
        {
            get => blendIn;
            set => blendIn = Mathf.Max(0f, value);
        }

        public float BlendOut
        {
            get => blendOut;
            set => blendOut = Mathf.Max(0f, value);
        }

        public HonamiBlendCurve BlendInCurve => blendInCurve;

        public HonamiBlendCurve BlendOutCurve => blendOutCurve;

        public override float Duration
        {
            get
            {
                if (durationOverride > 0f) return durationOverride;
                if (clip == null) return 0f;
                return Mathf.Max(0f, clip.length - clipStartOffset) / EffectiveSpeed;
            }
        }

        public override bool CanResize => clip != null;

        public override void SetDuration(float duration) => durationOverride = Mathf.Max(MinDuration, duration);

        public override void TrimStart(float newStartTime)
        {
            float end = EndTime;
            float earliest = loop ? float.NegativeInfinity : time - clipStartOffset / EffectiveSpeed;
            float start = Mathf.Clamp(newStartTime, Mathf.Max(0f, earliest), end - MinDuration);
            float offset = clipStartOffset + (start - time) * EffectiveSpeed;
            clipStartOffset = loop && clip != null && clip.length > 0f ? Mathf.Repeat(offset, clip.length) : Mathf.Max(0f, offset);
            time = start;
            durationOverride = end - start;
        }

        public float GetSampleTime(float localTime)
        {
            float sampleTime = clipStartOffset + localTime * EffectiveSpeed;
            if (clip == null) return sampleTime;

            float length = clip.length;
            if (loop && length > 0f)
                sampleTime = Mathf.Repeat(sampleTime, length);
            else
                sampleTime = Mathf.Min(sampleTime, length);
            return reversed ? length - sampleTime : sampleTime;
        }
    }
}
