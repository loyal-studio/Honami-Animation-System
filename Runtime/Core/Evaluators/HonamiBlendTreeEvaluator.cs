using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiBlendTreeEvaluator
    {
        /// <summary>Evaluates directional weights using the node's shared immutable solver.</summary>
        public static bool TryEvaluateDirectional(HonamiBlendTreeNode node, Vector2 input,
            float[] destinationWeights, out float period, out string error)
        {
            period = 0f;
            if (node == null)
            {
                error = "A directional blend tree node is required.";
                return false;
            }
            if (!node.TryGetDirectionalSolver(out HonamiDirectionalBlendSolver solver, out error))
                return false;
            return solver.TryEvaluate(input, destinationWeights, out period, out error);
        }

        public static bool TryEvaluateDirectional(HonamiDirectionalBlendSolver solver, Vector2 input,
            float[] destinationWeights, out float period, out string error)
        {
            if (solver == null)
            {
                period = 0f;
                error = "A prepared directional solver is required.";
                return false;
            }
            return solver.TryEvaluate(input, destinationWeights, out period, out error);
        }

        /// <summary>
        /// Initializes an actor-owned directional port and applies its exact start weights/times
        /// without advancing its phase. The port must have been created from this node's cached solver.
        /// </summary>
        public static bool InitializeDirectional(HonamiBlendTreeNode node, HonamiState state,
            AnimationMixerPlayable mixer, HonamiBlend2DPortRuntime port, Vector2 initialInput,
            double startSeconds, out string error)
        {
            error = null;
            if (!TryValidateDirectionalState(node, state, out HonamiDirectionalBlendSolver solver, out error))
                return false;
            if (port == null || !object.ReferenceEquals(port.Solver, solver))
            {
                error = "The directional port must use the solver prepared for this authored tree.";
                return false;
            }
            if (!mixer.IsValid() || mixer.GetInputCount() != node.blendMotions.Count)
            {
                error = "The directional playable mixer must have one input per authored motion.";
                return false;
            }
            if (!port.Initialize(initialInput, startSeconds, out error))
                return false;

            ApplyDirectionalState(mixer, port);
            return true;
        }

        /// <summary>Restarts a self-transition at the current damped input, preserving its velocity.</summary>
        public static bool RestartDirectionalAtCurrentInput(HonamiBlendTreeNode node, HonamiState state,
            AnimationMixerPlayable mixer, HonamiBlend2DPortRuntime port, double startSeconds, out string error)
        {
            error = null;
            if (!TryValidateDirectionalState(node, state, out HonamiDirectionalBlendSolver solver, out error))
                return false;
            if (port == null || !object.ReferenceEquals(port.Solver, solver))
            {
                error = "The directional port must use the solver prepared for this authored tree.";
                return false;
            }
            if (!mixer.IsValid() || mixer.GetInputCount() != node.blendMotions.Count)
            {
                error = "The directional playable mixer must have one input per authored motion.";
                return false;
            }
            if (!port.RestartAtCurrentInput(startSeconds, out error))
                return false;

            ApplyDirectionalState(mixer, port);
            return true;
        }

        /// <summary>
        /// Smooths both parameters and advances the shared canonical phase once, then applies each
        /// child weight/time with child speeds stopped to prevent independent Playable clocks drifting.
        /// </summary>
        public static bool UpdateDirectional(HonamiBlendTreeNode node, HonamiState state,
            AnimationMixerPlayable mixer, HonamiBlend2DPortRuntime port, Vector2 targetInput,
            double deltaTime, float dampingTimeX, float dampingTimeY, out string error)
        {
            error = null;
            if (node == null || state == null || node.blendType != HonamiBlendTreeType.FreeformDirectional2D)
            {
                error = "A FreeformDirectional2D node and owning state are required.";
                return false;
            }
            if (!state.loop)
            {
                error = "Directional trees require a looping state; non-looping directional clips are unsupported.";
                return false;
            }
            if (state.isReversed)
            {
                error = "Reversed playback is unsupported for directional blend trees.";
                return false;
            }
            if (state.mirror)
            {
                error = "State mirroring is unsupported for directional blend trees.";
                return false;
            }
            if (!IsFinite(dampingTimeX) || dampingTimeX < 0f || !IsFinite(dampingTimeY) || dampingTimeY < 0f)
            {
                error = "Directional X/Y damping times must be finite and nonnegative.";
                return false;
            }
            if (port == null || !port.IsInitialized || node.blendMotions == null ||
                port.Solver.MotionCount != node.blendMotions.Count)
            {
                error = "Initialize an actor-owned directional port for this tree before updating it.";
                return false;
            }
            if (!mixer.IsValid() || mixer.GetInputCount() != node.blendMotions.Count)
            {
                error = "The directional playable mixer must have one input per authored motion.";
                return false;
            }

            float stateSpeed = state.speed;
            if (!IsFinite(stateSpeed) || stateSpeed < 0f)
            {
                error = "Directional tree state speed must be finite and nonnegative.";
                return false;
            }
            if (!port.Advance(targetInput, deltaTime, stateSpeed, dampingTimeX, dampingTimeY, out error))
                return false;

            ApplyDirectionalState(mixer, port);
            return true;
        }

        /// <summary>Reads a prepared actor port's canonical cycle period for 2D duration callers.</summary>
        public static bool TryGetDirectionalPeriod(HonamiBlend2DPortRuntime port, out float period)
        {
            period = 0f;
            if (port == null || !port.IsInitialized || !IsFinite(port.Period) || port.Period <= 0f)
                return false;
            period = port.Period;
            return true;
        }

        /// <summary>Applies an already-initialized actor port without advancing or smoothing it.</summary>
        internal static bool ApplyDirectionalPort(AnimationMixerPlayable mixer,
            HonamiBlend2DPortRuntime port, out string error)
        {
            error = null;
            if (port == null || !port.IsInitialized)
            {
                error = "Initialize the directional port before applying it to a mixer.";
                return false;
            }
            if (!mixer.IsValid() || mixer.GetInputCount() != port.Solver.MotionCount)
            {
                error = "The directional playable mixer must have one input per authored motion.";
                return false;
            }

            ApplyDirectionalState(mixer, port);
            return true;
        }

        private static bool TryValidateDirectionalState(HonamiBlendTreeNode node, HonamiState state,
            out HonamiDirectionalBlendSolver solver, out string error)
        {
            solver = null;
            error = null;
            if (node == null || state == null || node.blendType != HonamiBlendTreeType.FreeformDirectional2D)
            {
                error = "A FreeformDirectional2D node and owning state are required.";
                return false;
            }
            if (!state.loop)
            {
                error = "Directional trees require a looping state; non-looping directional clips are unsupported.";
                return false;
            }
            if (state.isReversed)
            {
                error = "Reversed playback is unsupported for directional blend trees.";
                return false;
            }
            if (state.mirror)
            {
                error = "State mirroring is unsupported for directional blend trees.";
                return false;
            }
            if (!IsFinite(state.speed) || state.speed < 0f)
            {
                error = "Directional tree state speed must be finite and nonnegative.";
                return false;
            }
            if (!IsFinite(node.blendParameterDampTime) || node.blendParameterDampTime < 0f ||
                !IsFinite(node.blendParameterDampTimeY) || node.blendParameterDampTimeY < 0f)
            {
                error = "Directional X/Y damping times must be finite and nonnegative.";
                return false;
            }
            if (!node.TryGetDirectionalSolver(out solver, out error))
                return false;
            return true;
        }

        private static void ApplyDirectionalState(AnimationMixerPlayable mixer, HonamiBlend2DPortRuntime port)
        {
            for (int i = 0; i < port.Solver.MotionCount; i++)
            {
                Playable child = mixer.GetInput(i);
                mixer.SetInputWeight(i, port.GetWeight(i));
                if (!child.IsValid()) continue;

                Playable clipPlayable = GetClipPlayable(child);
                if (clipPlayable.IsValid())
                    clipPlayable.SetTime(port.GetChildTime(i));
                child.SetSpeed(0d);
            }
        }

        private static Playable GetClipPlayable(Playable child)
        {
            while (child.IsValid() && child.GetPlayableType() == typeof(AnimationScriptPlayable) &&
                   child.GetInputCount() > 0)
                child = child.GetInput(0);
            return child;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static void Update(HonamiRuntimeController controller, HonamiState state, AnimationMixerPlayable mixer, float paramValue)
        {
            if (controller.GetActiveNode(state) is not HonamiBlendTreeNode btNode) return;
            Update(btNode, state, mixer, paramValue);
        }

        public static void Update(HonamiBlendTreeNode btNode, HonamiState state, AnimationMixerPlayable mixer, float paramValue)
        {
            var motions = btNode.blendMotions;
            if (motions == null || motions.Count == 0) return;
            int count = motions.Count;
            if (count == 1)
            {
                mixer.SetInputWeight(0, 1f);
                UpdateChildSpeeds(motions, state, mixer, GetMotionDuration(motions[0]), btNode.blendType);
                return;
            }

            FindBlendIndices(motions, paramValue, out int minIdx, out int maxIdx, out float weight);
            for (int i = 0; i < count; i++) mixer.SetInputWeight(i, 0f);

            if (minIdx == -1)
            {
                int idx = paramValue <= motions[0].threshold ? 0 : count - 1;
                mixer.SetInputWeight(idx, 1f);
                UpdateChildSpeeds(motions, state, mixer, GetMotionDuration(motions[idx]), btNode.blendType);
            }
            else
            {
                mixer.SetInputWeight(minIdx, 1f - weight);
                mixer.SetInputWeight(maxIdx, weight);
                float dur = GetMotionDuration(motions[minIdx]) + (GetMotionDuration(motions[maxIdx]) - GetMotionDuration(motions[minIdx])) * weight;
                UpdateChildSpeeds(motions, state, mixer, dur, btNode.blendType);
            }
        }

        public static void UpdateWeights(HonamiRuntimeController controller, HonamiState state, AnimationMixerPlayable mixer, float paramValue)
        {
            if (controller.GetActiveNode(state) is not HonamiBlendTreeNode btNode) return;
            UpdateWeightsFromMotions(btNode.blendMotions, mixer, paramValue);
        }

        public static void UpdateWeightsFromMotions(List<HonamiBlendTreeMotion> motions, AnimationMixerPlayable mixer, float paramValue)
        {
            if (motions == null || motions.Count == 0) return;
            int count = motions.Count;
            if (count == 1) { mixer.SetInputWeight(0, 1f); return; }

            FindBlendIndices(motions, paramValue, out int minIdx, out int maxIdx, out float weight);
            for (int i = 0; i < count; i++) mixer.SetInputWeight(i, 0f);

            if (minIdx == -1) mixer.SetInputWeight(paramValue <= motions[0].threshold ? 0 : count - 1, 1f);
            else { mixer.SetInputWeight(minIdx, 1f - weight); mixer.SetInputWeight(maxIdx, weight); }
        }

        public static void UpdateChildSpeedsFromParam(HonamiRuntimeController controller, HonamiState state, AnimationMixerPlayable mixer, float paramValue)
        {
            if (controller.GetActiveNode(state) is not HonamiBlendTreeNode btNode) return;
            float dur = GetDurationFromMotions(btNode.blendMotions, paramValue);
            UpdateChildSpeeds(btNode.blendMotions, state, mixer, dur, btNode.blendType);
        }

        public static void UpdateChildSpeedsFromMotions(HonamiState state, List<HonamiBlendTreeMotion> motions, AnimationMixerPlayable mixer, float paramValue, HonamiBlendTreeType blendType = HonamiBlendTreeType.Standard1D)
        {
            float dur   = GetDurationFromMotions(motions, paramValue);
            UpdateChildSpeeds(motions, state, mixer, dur, blendType);
        }

        private static void UpdateChildSpeeds(List<HonamiBlendTreeMotion> motions, HonamiState state, AnimationMixerPlayable mixer, float avgDur, HonamiBlendTreeType blendType = HonamiBlendTreeType.Standard1D)
        {
            int count = motions.Count;
            for (int i = 0; i < count; i++)
            {
                Playable child = mixer.GetInput(i);
                if (!child.IsValid()) continue;

                HonamiBlendTreeMotion motion = motions[i];
                float motionSpeed;
                if (blendType == HonamiBlendTreeType.Standard1D && avgDur > 0.0001f && motion.clip != null)
                    motionSpeed = motion.clip.length / avgDur;
                else
                    motionSpeed = motion.speed;

                if (state.loop && motion.clip != null && motion.clip.length > 0f)
                {
                    Playable actualCp = child;
                    if (actualCp.GetPlayableType() == typeof(AnimationScriptPlayable) && actualCp.GetInputCount() > 0)
                        actualCp = actualCp.GetInput(0);

                    double t = actualCp.GetTime();
                    float len = motion.clip.length;
                    if (t >= len || t < 0.0)
                    {
                        t %= len;
                        if (t < 0.0) t += len;
                        actualCp.SetTime(t);
                    }
                }

                child.SetSpeed(motionSpeed);
            }
        }

        public static float GetDuration(HonamiRuntimeController controller, HonamiState state, float paramValue)
        {
            if (controller.GetActiveNode(state) is not HonamiBlendTreeNode btNode) return 1f;
            return GetDurationFromMotions(btNode.blendMotions, paramValue);
        }

        public static float GetDurationFromMotions(List<HonamiBlendTreeMotion> motions, float paramValue)
        {
            if (motions == null || motions.Count == 0) return 1f;
            int count = motions.Count;
            if (count == 1) return GetMotionDuration(motions[0]);

            FindBlendIndices(motions, paramValue, out int minIdx, out int maxIdx, out float weight);
            if (minIdx == -1) return GetMotionDuration(motions[paramValue <= motions[0].threshold ? 0 : count - 1]);

            float d1 = GetMotionDuration(motions[minIdx]);
            float d2 = GetMotionDuration(motions[maxIdx]);
            return d1 + (d2 - d1) * weight;
        }

        private static void FindBlendIndices(List<HonamiBlendTreeMotion> motions, float paramValue,
            out int minIdx, out int maxIdx, out float weight)
        {
            minIdx = -1; maxIdx = -1; weight = 0f;
            int count = motions.Count;

            if (paramValue <= motions[0].threshold || paramValue >= motions[count - 1].threshold) return;

            for (int i = 0; i < count - 1; i++)
            {
                float t1 = motions[i].threshold;
                float t2 = motions[i + 1].threshold;
                if (paramValue >= t1 && paramValue <= t2)
                {
                    minIdx = i; maxIdx = i + 1;
                    weight = t1 == t2 ? 0f : (paramValue - t1) / (t2 - t1);
                    return;
                }
            }
        }

        private static float GetMotionDuration(HonamiBlendTreeMotion m)
        {
            if (m.clip == null) return 0f;
            float s = Mathf.Abs(m.speed);
            return m.clip.length / (s > 0f ? s : 1f);
        }
    }
}
