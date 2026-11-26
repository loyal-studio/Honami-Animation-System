using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Honami state node that blends clips across a blend space of any dimension. Each axis is driven
    /// by its own float parameter and each motion carries one coordinate per axis. For a single
    /// parameter prefer <see cref="HonamiBlendTreeNode"/>, which resolves a sorted 1D blend directly.
    /// </summary>
    public sealed class HonamiBlendSpaceNode : HonamiNodeBase
    {
        private const int StackLimit = 64;

        /// <summary>
        /// Per-animator damped axis values. Blend space nodes are shared assets, so the smoothing
        /// state of each playing instance lives here rather than on the node.
        /// </summary>
        public sealed class Runtime : HonamiNodeRuntime
        {
            public float[] Values = Array.Empty<float>();
            public float[] Velocities = Array.Empty<float>();

            public void EnsureAxisCount(int axisCount)
            {
                if (Values.Length == axisCount) return;
                Values = new float[axisCount];
                Velocities = new float[axisCount];
            }

            public override void Reset()
            {
                Array.Clear(Values, 0, Values.Length);
                Array.Clear(Velocities, 0, Velocities.Length);
            }
        }

        public HonamiBlendSpaceType spaceType = HonamiBlendSpaceType.FreeformDirectional;

        [Tooltip("One float parameter per blend space axis. Two axes give the classic directional blend space; more are allowed.")]
        public List<string> blendParameters = new();

        public float blendParameterDampTime = 0.1f;

        [Tooltip("Derive each clip's speed from the blended duration so mixed-length clips stay in phase.")]
        public bool synchronizeSpeed = true;

        public List<HonamiBlendSpaceMotion> blendMotions = new();

        private int[] _parameterHashes;

        public int AxisCount => blendParameters?.Count ?? 0;

        public bool HasValidAxisCount => HonamiBlendSpaceSolver.SupportsAxisCount(spaceType, AxisCount);

        public override HonamiNodeRuntime CreateRuntime() => new Runtime();

        // Reported so state introspection and the graph builder still see a driving parameter; the
        // node reads every axis itself rather than through the animator's single-parameter cache.
        public override string GetBlendParameterName()
            => blendParameters != null && blendParameters.Count > 0 ? blendParameters[0] : null;

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
                HonamiBlendSpaceMotion motion = blendMotions[i];
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

        public override void OnEnter(in HonamiExecutionContext ctx)
        {
            if (!TryGetMixer(ctx, out AnimationMixerPlayable mixer)) return;

            int axisCount = AxisCount;
            Span<float> sample = axisCount <= StackLimit ? stackalloc float[axisCount] : new float[axisCount];
            ReadParameters(ctx, sample);

            if (ctx.Runtime is Runtime runtime)
            {
                runtime.EnsureAxisCount(axisCount);
                for (int axis = 0; axis < axisCount; axis++)
                {
                    runtime.Values[axis] = sample[axis];
                    runtime.Velocities[axis] = 0f;
                }
            }

            Apply(ctx.State, mixer, sample);
        }

        public override void UpdateRuntime(in HonamiExecutionContext ctx)
        {
            if (!TryGetMixer(ctx, out AnimationMixerPlayable mixer)) return;

            int axisCount = AxisCount;
            Span<float> sample = axisCount <= StackLimit ? stackalloc float[axisCount] : new float[axisCount];
            ReadParameters(ctx, sample);

            if (ctx.Runtime is Runtime runtime)
            {
                runtime.EnsureAxisCount(axisCount);
                Damp(runtime, sample, ctx.DeltaTime);
            }

            Apply(ctx.State, mixer, sample);
        }

        public override float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            float stateSpeed = state.speed != 0f ? Mathf.Abs(state.speed) : 1f;
            return GetUnscaledDuration(state, stateIndex, runtime, blendParam) / stateSpeed;
        }

        public override float GetUnscaledDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
        {
            int count = blendMotions?.Count ?? 0;
            if (count == 0) return 1f;

            Span<float> weights = count <= StackLimit ? stackalloc float[count] : new float[count];
            SolveWeights(runtime as Runtime, weights);
            return HonamiBlendSpaceSolver.GetBlendedDuration(blendMotions, weights);
        }

        public override void OnValidateNode()
        {
            _parameterHashes = null;

            int axisCount = AxisCount;
            if (blendMotions == null) return;

            for (int i = 0; i < blendMotions.Count; i++)
            {
                var motion = blendMotions[i];
                if (motion == null) continue;

                if (motion.position == null || motion.position.Length != axisCount)
                {
                    var resized = new float[axisCount];
                    int copy = Mathf.Min(axisCount, motion.position?.Length ?? 0);
                    for (int axis = 0; axis < copy; axis++) resized[axis] = motion.position[axis];
                    motion.position = resized;
                }
            }
        }

        private void SolveWeights(Runtime runtime, Span<float> weights)
        {
            int axisCount = AxisCount;
            var values = runtime != null && runtime.Values.Length == axisCount
                ? runtime.Values.AsSpan()
                : ReadOnlySpan<float>.Empty;

            HonamiBlendSpaceSolver.CalculateWeights(blendMotions, axisCount, spaceType, values, weights);
        }

        private void Apply(HonamiState state, AnimationMixerPlayable mixer, ReadOnlySpan<float> sample)
        {
            int count = blendMotions?.Count ?? 0;
            if (count == 0) return;

            Span<float> weights = count <= StackLimit ? stackalloc float[count] : new float[count];
            HonamiBlendSpaceSolver.CalculateWeights(blendMotions, AxisCount, spaceType, sample, weights);

            float duration = 0f;
            for (int i = 0; i < count; i++)
            {
                mixer.SetInputWeight(i, weights[i]);
                duration += weights[i] * HonamiBlendSpaceSolver.GetMotionDuration(blendMotions[i]);
            }

            ApplyChildSpeeds(state, mixer, count, duration);
        }

        private void ApplyChildSpeeds(HonamiState state, AnimationMixerPlayable mixer, int count, float blendedDuration)
        {
            for (int i = 0; i < count; i++)
            {
                Playable child = mixer.GetInput(i);
                if (!child.IsValid()) continue;

                HonamiBlendSpaceMotion motion = blendMotions[i];
                float speed = synchronizeSpeed && blendedDuration > 0.0001f && motion.clip != null
                    ? motion.clip.length / blendedDuration
                    : motion.speed;

                if (state.loop && motion.clip != null && motion.clip.length > 0f)
                {
                    Playable clipPlayable = child;
                    if (clipPlayable.GetPlayableType() == typeof(AnimationScriptPlayable) && clipPlayable.GetInputCount() > 0)
                    {
                        clipPlayable = clipPlayable.GetInput(0);
                    }

                    double time = clipPlayable.GetTime();
                    float length = motion.clip.length;
                    if (time >= length || time < 0.0)
                    {
                        time %= length;
                        if (time < 0.0) time += length;
                        clipPlayable.SetTime(time);
                    }
                }

                child.SetSpeed(speed);
            }
        }

        private void Damp(Runtime runtime, Span<float> target, float deltaTime)
        {
            if (blendParameterDampTime <= 0f)
            {
                for (int axis = 0; axis < target.Length; axis++)
                {
                    runtime.Values[axis] = target[axis];
                    runtime.Velocities[axis] = 0f;
                }
                return;
            }

            for (int axis = 0; axis < target.Length; axis++)
            {
                float velocity = runtime.Velocities[axis];
                float value = Mathf.SmoothDamp(
                    runtime.Values[axis], target[axis], ref velocity,
                    blendParameterDampTime, float.PositiveInfinity, deltaTime);

                runtime.Values[axis] = value;
                runtime.Velocities[axis] = velocity;
                target[axis] = value;
            }
        }

        private void ReadParameters(in HonamiExecutionContext ctx, Span<float> destination)
        {
            EnsureParameterHashes();

            for (int axis = 0; axis < destination.Length; axis++)
            {
                int hash = _parameterHashes[axis];
                destination[axis] = hash != 0 ? ctx.Params.GetFloat(hash) : 0f;
            }
        }

        private void EnsureParameterHashes()
        {
            int axisCount = AxisCount;
            if (_parameterHashes != null && _parameterHashes.Length == axisCount) return;

            _parameterHashes = new int[axisCount];
            for (int axis = 0; axis < axisCount; axis++)
            {
                string name = blendParameters[axis];
                _parameterHashes[axis] = string.IsNullOrEmpty(name) ? 0 : HonamiAnimator.StringToHash(name);
            }
        }

        private static bool TryGetMixer(in HonamiExecutionContext ctx, out AnimationMixerPlayable mixer)
        {
            if (ctx.Playable.GetPlayableType() != typeof(AnimationMixerPlayable))
            {
                mixer = default;
                return false;
            }

            mixer = (AnimationMixerPlayable)ctx.Playable;
            return true;
        }

        private static Playable CreateClipOutput(
            PlayableGraph graph,
            HonamiState state,
            HonamiBlendSpaceMotion motion,
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
    }
}
