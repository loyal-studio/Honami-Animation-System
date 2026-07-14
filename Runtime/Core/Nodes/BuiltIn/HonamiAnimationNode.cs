using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Basic Honami state node that plays a single animation clip.
    /// </summary>
    public sealed class HonamiAnimationNode : HonamiNodeBase
    {
        public AnimationClip clip;
        public float startTime = 0f;
        public float endTime = 0f;

        public override void OnValidateNode()
        {
            if (clip == null) return;

            if (endTime <= 0)
            {
                endTime = clip.length;
            }

            startTime = Mathf.Clamp(startTime, 0f, clip.length);
            endTime = Mathf.Clamp(endTime, startTime, clip.length);
        }

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state)
        {
            if (clip == null)
            {
                return Playable.Null;
            }

            AnimationMixerPlayable mixer = AnimationMixerPlayable.Create(graph, 1);
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
            graph.Connect(clipPlayable, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);

            clipPlayable.SetSpeed(state.isReversed ? -state.speed : state.speed);
            clipPlayable.SetApplyFootIK(false);

            if (state.loop)
            {
                clipPlayable.SetDuration(Mathf.Infinity);
            }

            float stopTime = GetStopTime();
            clipPlayable.SetTime(state.isReversed ? stopTime : startTime);

            return mixer;
        }

        public override float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            if (clip == null)
            {
                return 1f;
            }

            float stateSpeed = state.speed != 0f ? Mathf.Abs(state.speed) : 1f;
            return GetClipRangeDuration() / stateSpeed;
        }

        public override float GetUnscaledDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            return clip == null ? 1f : GetClipRangeDuration();
        }

        public override void UpdateRuntime(in HonamiExecutionContext ctx)
        {
            if (clip == null || ctx.Playable.GetPlayableType() != typeof(AnimationMixerPlayable))
            {
                return;
            }

            AnimationMixerPlayable mixer = (AnimationMixerPlayable)ctx.Playable;
            Playable child = mixer.GetInput(0);
            if (!child.IsValid())
            {
                return;
            }

            float stopTime = GetStopTime();
            float duration = Mathf.Max(0.001f, stopTime - startTime);
            double rawTime = NormalizeRawTime(ctx.Playable.GetTime(), duration, ctx.State.loop);
            double sampleTime = ctx.State.isReversed ? stopTime - rawTime : startTime + rawTime;

            if (sampleTime >= clip.length)
            {
                sampleTime = clip.length - 0.001;
            }

            child.SetTime(sampleTime);
            child.SetSpeed(0f);
        }

        private float GetStopTime()
        {
            return endTime > 0 ? endTime : clip.length;
        }

        private float GetClipRangeDuration()
        {
            return Mathf.Max(0f, GetStopTime() - startTime);
        }

        private static double NormalizeRawTime(double rawTime, float duration, bool loop)
        {
            if (loop && duration > 0)
            {
                double previousTime = rawTime;
                rawTime %= duration;
                return previousTime > 0.0 && rawTime < 1e-4 ? duration : rawTime;
            }

            return Math.Max(0.0, Math.Min(rawTime, duration));
        }
    }
}
