using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Supported blend tree evaluation modes.
    /// </summary>
    public enum HonamiBlendTreeType
    {
        Standard1D,
        Simple1D
    }

    /// <summary>
    /// Honami state node that blends multiple animation clips from a float parameter.
    /// </summary>
    public sealed class HonamiBlendTreeNode : HonamiNodeBase
    {
        public HonamiBlendTreeType blendType = HonamiBlendTreeType.Standard1D;
        public string blendParameter = "";
        public float blendParameterDampTime = 0.1f;
        public List<HonamiBlendTreeMotion> blendMotions = new();

        public override string GetBlendParameterName() => blendParameter;

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state)
            => CreatePlayable(graph, state, null);

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state, Func<PlayableGraph, Playable> mirrorFactory)
        {
            if (blendMotions == null)
            {
                return Playable.Null;
            }

            AnimationMixerPlayable mixer = AnimationMixerPlayable.Create(graph, blendMotions.Count);

            for (int i = 0; i < blendMotions.Count; i++)
            {
                HonamiBlendTreeMotion motion = blendMotions[i];
                if (motion.clip == null)
                {
                    continue;
                }

                Playable clipOutput = CreateClipOutput(graph, state, motion, mirrorFactory);
                graph.Connect(clipOutput, 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }

            mixer.Play();
            return mixer;
        }

        public override float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            if (blendMotions == null || blendMotions.Count == 0)
            {
                return 1f;
            }

            float stateSpeed = state.speed != 0f ? Mathf.Abs(state.speed) : 1f;
            return HonamiBlendTreeEvaluator.GetDurationFromMotions(blendMotions, blendParam) / stateSpeed;
        }

        public override float GetUnscaledDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            if (blendMotions == null || blendMotions.Count == 0)
            {
                return 1f;
            }

            return HonamiBlendTreeEvaluator.GetDurationFromMotions(blendMotions, blendParam);
        }

        // HonamiAnimator.UpdateStateOnPort normally handles blend tree runtime updates directly
        // so blend parameter damping can use the animator-owned SmoothDamp cache.
        public override void UpdateRuntime(in HonamiExecutionContext ctx)
        {
            if (ctx.Playable.GetPlayableType() != typeof(AnimationMixerPlayable))
            {
                return;
            }

            AnimationMixerPlayable mixer = (AnimationMixerPlayable)ctx.Playable;
            float parameterValue = GetBlendParameterValue(ctx);

            HonamiBlendTreeEvaluator.UpdateWeightsFromMotions(blendMotions, mixer, parameterValue);
            HonamiBlendTreeEvaluator.UpdateChildSpeedsFromMotions(ctx.State, blendMotions, mixer, parameterValue, blendType);
        }

        private static Playable CreateClipOutput(
            PlayableGraph graph,
            HonamiState state,
            HonamiBlendTreeMotion motion,
            Func<PlayableGraph, Playable> mirrorFactory)
        {
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, motion.clip);
            clipPlayable.SetSpeed(1f);
            clipPlayable.SetTime(state.isReversed ? motion.clip.length : 0f);
            clipPlayable.Play();

            if (state.loop)
            {
                clipPlayable.SetDuration(Mathf.Infinity);
            }

            if (!motion.mirror || mirrorFactory == null)
            {
                return clipPlayable;
            }

            Playable mirrorPlayable = mirrorFactory(graph);
            if (!mirrorPlayable.IsValid())
            {
                return clipPlayable;
            }

            mirrorPlayable.SetInputCount(1);
            graph.Connect(clipPlayable, 0, mirrorPlayable, 0);
            mirrorPlayable.SetInputWeight(0, 1f);
            return mirrorPlayable;
        }

        private float GetBlendParameterValue(in HonamiExecutionContext ctx)
        {
            if (string.IsNullOrEmpty(blendParameter))
            {
                return 0f;
            }

            int hash = HonamiAnimator.StringToHash(blendParameter);
            return ctx.Params.GetFloat(hash);
        }
    }
}
