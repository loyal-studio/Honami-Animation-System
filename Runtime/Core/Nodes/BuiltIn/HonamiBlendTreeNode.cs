using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Common;
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
        Standard1D = 0,
        Simple1D = 1,
        FreeformDirectional2D = 2
    }

    /// <summary>
    /// Honami state node that blends multiple animation clips from a float parameter.
    /// </summary>
    public sealed class HonamiBlendTreeNode : HonamiNodeBase
    {
        public HonamiBlendTreeType blendType = HonamiBlendTreeType.Standard1D;
        public string blendParameter = "";
        public float blendParameterDampTime = 0.1f;
        public string blendParameterY = "";
        public float blendParameterDampTimeY = 0.1f;
        public List<HonamiBlendTreeMotion> blendMotions = new();

        [NonSerialized] private HonamiDirectionalBlendSolver directionalSolver;
        [NonSerialized] private int directionalTopologySignature;
        [NonSerialized] private AnimationClip[] directionalClipSnapshot;
        [NonSerialized] private Vector2[] directionalPositionSnapshot;
        [NonSerialized] private float[] directionalLengthSnapshot;
        [NonSerialized] private float[] directionalSpeedSnapshot;
        [NonSerialized] private float[] directionalCycleOffsetSnapshot;

        public override string GetBlendParameterName() => blendParameter;

        /// <summary>
        /// Returns the immutable preprocessed 2D solver for this authored motion table. The cached
        /// solver is shared safely; per-actor ports and weights must live outside this ScriptableObject.
        /// The exact snapshot check prevents a hash collision from reusing stale topology.
        /// </summary>
        public bool TryGetDirectionalSolver(out HonamiDirectionalBlendSolver solver, out string error)
        {
            solver = null;
            error = null;
            if (blendType != HonamiBlendTreeType.FreeformDirectional2D)
            {
                error = "The blend tree is not configured for FreeformDirectional2D.";
                return false;
            }

            if (blendMotions == null || blendMotions.Count < 1 ||
                blendMotions.Count > HonamiDirectionalBlendSolver.MaximumMotionCount)
            {
                error = $"A flat directional tree requires 1..{HonamiDirectionalBlendSolver.MaximumMotionCount} motions.";
                return false;
            }

            int signature = GetDirectionalTopologySignature();
            if (directionalSolver != null && signature == directionalTopologySignature &&
                MatchesDirectionalSnapshot())
            {
                solver = directionalSolver;
                return true;
            }

            int count = blendMotions.Count;
            var clips = new AnimationClip[count];
            var positions = new Vector2[count];
            var lengths = new float[count];
            var speeds = new float[count];
            var cycleOffsets = new float[count];
            for (int i = 0; i < count; i++)
            {
                HonamiBlendTreeMotion motion = blendMotions[i];
                if (motion == null || motion.clip == null)
                {
                    error = $"Directional motion {i} must have a clip.";
                    return false;
                }
                if (motion.mirror)
                {
                    error = $"Directional motion {i} uses mirroring, which is outside the supported flat 2D profile.";
                    return false;
                }

                clips[i] = motion.clip;
                positions[i] = motion.position;
                lengths[i] = motion.clip.length;
                speeds[i] = motion.speed;
                cycleOffsets[i] = WrapCycleOffset(motion.cycleOffset);
            }

            try
            {
                var prepared = new HonamiDirectionalBlendSolver(positions, lengths, speeds, cycleOffsets);
                directionalSolver = prepared;
                directionalTopologySignature = signature;
                directionalClipSnapshot = clips;
                directionalPositionSnapshot = positions;
                directionalLengthSnapshot = lengths;
                directionalSpeedSnapshot = speeds;
                directionalCycleOffsetSnapshot = cycleOffsets;
                solver = prepared;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Evaluates the directional period for one static input. Intended for authoring tools and
        /// one-off queries; the runtime reads the live period from the actor-owned port instead.
        /// </summary>
        public bool TryGetDirectionalPeriod(Vector2 input, out float period, out string error)
        {
            period = 0f;
            if (!TryGetDirectionalSolver(out HonamiDirectionalBlendSolver solver, out error)) return false;
            return solver.TryEvaluate(input, new float[solver.MotionCount], out period, out error);
        }

        public int GetDirectionalTopologySignature()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (blendMotions != null ? blendMotions.Count : -1);
                if (blendMotions == null) return hash;
                for (int i = 0; i < blendMotions.Count; i++)
                {
                    HonamiBlendTreeMotion motion = blendMotions[i];
                    if (motion == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + HonamiObjectHash.Of(motion.clip);
                    hash = hash * 31 + motion.position.GetHashCode();
                    hash = hash * 31 + motion.speed.GetHashCode();
                    hash = hash * 31 + (motion.clip != null ? motion.clip.length.GetHashCode() : 0);
                    hash = hash * 31 + (motion.mirror ? 1 : 0);
                    hash = hash * 31 + WrapCycleOffset(motion.cycleOffset).GetHashCode();
                }
                return hash;
            }
        }

        private bool MatchesDirectionalSnapshot()
        {
            if (blendMotions == null || directionalClipSnapshot == null ||
                directionalPositionSnapshot == null || directionalLengthSnapshot == null ||
                directionalSpeedSnapshot == null || directionalCycleOffsetSnapshot == null ||
                blendMotions.Count != directionalClipSnapshot.Length)
                return false;

            for (int i = 0; i < blendMotions.Count; i++)
            {
                HonamiBlendTreeMotion motion = blendMotions[i];
                if (motion == null || motion.mirror || motion.clip != directionalClipSnapshot[i] ||
                    motion.position.x != directionalPositionSnapshot[i].x ||
                    motion.position.y != directionalPositionSnapshot[i].y ||
                    motion.speed != directionalSpeedSnapshot[i] ||
                    WrapCycleOffset(motion.cycleOffset) != directionalCycleOffsetSnapshot[i] ||
                    motion.clip == null || motion.clip.length != directionalLengthSnapshot[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Maps an authored cycle offset onto the cycle like Unity's ChildMotion.cycleOffset:
        /// frac(value) in [0, 1), with non-finite values treated as 0.
        /// </summary>
        public static float WrapCycleOffset(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            float wrapped = value - Mathf.Floor(value);
            return wrapped >= 1f || wrapped < 0f ? 0f : wrapped;
        }

        private void OnValidate()
        {
            directionalSolver = null;
            directionalTopologySignature = 0;
            directionalClipSnapshot = null;
            directionalPositionSnapshot = null;
            directionalLengthSnapshot = null;
            directionalSpeedSnapshot = null;
            directionalCycleOffsetSnapshot = null;
        }

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state)
            => CreatePlayable(graph, state, null);

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state, Func<PlayableGraph, Playable> mirrorFactory)
        {
            if (blendMotions == null)
            {
                return Playable.Null;
            }

            AnimationMixerPlayable mixer = AnimationMixerPlayable.Create(graph, blendMotions.Count);
            bool disableFootIK = blendType == HonamiBlendTreeType.FreeformDirectional2D;

            for (int i = 0; i < blendMotions.Count; i++)
            {
                HonamiBlendTreeMotion motion = blendMotions[i];
                if (motion.clip == null)
                {
                    continue;
                }

                Playable clipOutput = CreateClipOutput(graph, state, motion, mirrorFactory, disableFootIK);
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
            // Two-dimensional trees are advanced through the actor-owned port by the explicit
            // evaluator overload. Do not run the legacy scalar threshold path for this enum value.
            if (blendType == HonamiBlendTreeType.FreeformDirectional2D)
            {
                return;
            }

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
            Func<PlayableGraph, Playable> mirrorFactory,
            bool disableFootIK)
        {
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, motion.clip);
            if (disableFootIK)
            {
                clipPlayable.SetApplyFootIK(false);
            }
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
