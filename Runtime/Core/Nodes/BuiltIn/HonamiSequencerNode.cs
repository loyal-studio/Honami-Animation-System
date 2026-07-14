using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Honami state node that plays a timeline-like sequence of animation clips.
    /// </summary>
    public sealed class HonamiSequencerNode : HonamiNodeBase
    {
        public List<HonamiSequencedAnimationClip> sequencedClips = new();

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state)
        {
            if (sequencedClips == null)
            {
                return Playable.Null;
            }

            AnimationMixerPlayable mixer = AnimationMixerPlayable.Create(graph, sequencedClips.Count);

            for (int i = 0; i < sequencedClips.Count; i++)
            {
                HonamiSequencedAnimationClip sequencedClip = sequencedClips[i];
                if (sequencedClip.clip == null)
                {
                    continue;
                }

                AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, sequencedClip.clip);
                clipPlayable.SetSpeed(sequencedClip.speed * (state.isReversed ? -state.speed : state.speed));

                if (state.loop)
                {
                    clipPlayable.SetDuration(Mathf.Infinity);
                }

                graph.Connect(clipPlayable, 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }

            return mixer;
        }

        public override float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            if (sequencedClips == null || sequencedClips.Count == 0)
            {
                return 1f;
            }

            float stateSpeed = state.speed != 0f ? Mathf.Abs(state.speed) : 1f;
            return GetSequenceDuration() / stateSpeed;
        }

        public override float GetUnscaledDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            if (sequencedClips == null || sequencedClips.Count == 0)
            {
                return 1f;
            }

            return GetSequenceDuration();
        }

        public override void UpdateRuntime(in HonamiExecutionContext ctx)
        {
            if (sequencedClips == null || ctx.Playable.GetPlayableType() != typeof(AnimationMixerPlayable))
            {
                return;
            }

            AnimationMixerPlayable mixer = (AnimationMixerPlayable)ctx.Playable;
            float sequenceDuration = GetUnscaledDuration(ctx.State, ctx.StateIndex, ctx.Runtime, 0f);
            double normalizedTime = NormalizeTime(ctx.Playable.GetTime(), sequenceDuration, ctx.State.loop);
            float finalTime = ctx.State.isReversed ? sequenceDuration - (float)normalizedTime : (float)normalizedTime;

            for (int i = 0; i < sequencedClips.Count; i++)
            {
                HonamiSequencedAnimationClip sequencedClip = sequencedClips[i];
                Playable child = mixer.GetInput(i);
                if (!child.IsValid())
                {
                    continue;
                }

                child.SetSpeed(sequencedClip.speed);

                float clipDuration = GetClipDuration(sequencedClip);
                float weight = finalTime >= sequencedClip.startTime && finalTime <= sequencedClip.startTime + clipDuration ? 1f : 0f;
                mixer.SetInputWeight(i, weight);

                if (weight <= 0f)
                {
                    continue;
                }

                double sampleTime = (finalTime - sequencedClip.startTime) * GetClipSpeed(sequencedClip);
                if (sequencedClip.clip != null && sampleTime >= sequencedClip.clip.length)
                {
                    sampleTime = sequencedClip.clip.length - 0.001;
                }

                child.SetTime(sampleTime);
            }
        }

        private float GetSequenceDuration()
        {
            float maxSequenceTime = 0f;

            foreach (HonamiSequencedAnimationClip sequencedClip in sequencedClips)
            {
                if (sequencedClip.clip == null)
                {
                    continue;
                }

                float endTime = sequencedClip.startTime + GetClipDuration(sequencedClip);
                if (endTime > maxSequenceTime)
                {
                    maxSequenceTime = endTime;
                }
            }

            return maxSequenceTime > 0f ? maxSequenceTime : 1f;
        }

        private static float GetClipDuration(HonamiSequencedAnimationClip sequencedClip)
        {
            return sequencedClip.clip != null ? sequencedClip.clip.length / GetClipSpeed(sequencedClip) : 0f;
        }

        private static float GetClipSpeed(HonamiSequencedAnimationClip sequencedClip)
        {
            return Mathf.Abs(sequencedClip.speed != 0 ? sequencedClip.speed : 1f);
        }

        private static double NormalizeTime(double time, float duration, bool loop)
        {
            if (loop && duration > 0)
            {
                double previousTime = time;
                time %= duration;
                return previousTime > 0.0 && time < 1e-4 ? duration : time;
            }

            return Math.Max(0.0, Math.Min(time, duration));
        }
    }
}
