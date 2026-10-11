using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Per-layer/port mutable state for a directional 2D blend. The immutable solver may be shared;
    /// this input, damping state, weights, period, and canonical phase are actor-local.
    /// </summary>
    public sealed class HonamiBlend2DPortRuntime
    {
        private readonly HonamiDirectionalBlendSolver solver;
        private readonly float[] weights;
        private readonly float[] candidateWeights;

        private Vector2 input;
        private Vector2 dampingVelocity;
        private double phase;
        private double cyclePosition;
        private float period;
        private bool initialized;

        public HonamiDirectionalBlendSolver Solver => solver;
        public Vector2 Input => input;
        public Vector2 DampingVelocity => dampingVelocity;
        public double Phase => phase;
        /// <summary>
        /// Unwrapped normalized time since the state started (start offset plus every advanced
        /// cycle). Its fractional part equals <see cref="Phase"/>. Exit time and state events use
        /// it as the directional equivalent of rawTime / duration.
        /// </summary>
        public double CyclePosition => cyclePosition;
        public float Period => period;
        public bool IsInitialized => initialized;
        public ReadOnlySpan<float> Weights => initialized ? new ReadOnlySpan<float>(weights) : ReadOnlySpan<float>.Empty;

        public HonamiBlend2DPortRuntime(HonamiDirectionalBlendSolver solver)
        {
            this.solver = solver ?? throw new ArgumentNullException(nameof(solver));
            weights = new float[solver.MotionCount];
            candidateWeights = new float[solver.MotionCount];
        }

        /// <summary>Sets the initial blended value and loop offset without advancing the phase.</summary>
        public bool Initialize(Vector2 initialInput, double startSeconds, out string error)
        {
            error = null;
            if (!IsFinite(startSeconds))
            {
                error = "The directional start offset must be finite.";
                return false;
            }
            if (!solver.TryEvaluate(initialInput, candidateWeights, out float initialPeriod, out error))
                return false;

            double initialPhase = startSeconds / initialPeriod;
            if (!IsFinite(initialPhase))
            {
                error = "The directional start offset cannot be represented as a finite phase.";
                return false;
            }

            input = initialInput;
            dampingVelocity = Vector2.zero;
            phase = Wrap01(initialPhase);
            cyclePosition = initialPhase;
            period = initialPeriod;
            Array.Copy(candidateWeights, weights, weights.Length);
            initialized = true;
            return true;
        }

        /// <summary>
        /// Restarts the canonical cycle at the current damped input without discarding its input
        /// velocity. This is used for a self-restart so the visible blend does not jump to a new
        /// raw parameter target before damping has reached it.
        /// </summary>
        public bool RestartAtCurrentInput(double startSeconds, out string error)
        {
            error = null;
            if (!initialized)
            {
                error = "Initialize the directional port before restarting it.";
                return false;
            }
            if (!IsFinite(startSeconds))
            {
                error = "The directional start offset must be finite.";
                return false;
            }
            if (!solver.TryEvaluate(input, candidateWeights, out float currentPeriod, out error))
                return false;

            double restartedPhase = startSeconds / currentPeriod;
            if (!IsFinite(restartedPhase))
            {
                error = "The directional start offset cannot be represented as a finite phase.";
                return false;
            }

            phase = Wrap01(restartedPhase);
            cyclePosition = restartedPhase;
            period = currentPeriod;
            Array.Copy(candidateWeights, weights, weights.Length);
            return true;
        }

        /// <summary>
        /// Smooths X/Y once, recomputes weights only when the smoothed input changes, then advances
        /// the normalized loop phase using stateSpeed * deltaTime / current period.
        /// </summary>
        public bool Advance(Vector2 targetInput, double deltaTime, float stateSpeed,
            float dampingTimeX, float dampingTimeY, out string error)
        {
            error = null;
            if (!initialized)
            {
                error = "Initialize the directional port before advancing it.";
                return false;
            }
            if (!IsFinite(targetInput.x) || !IsFinite(targetInput.y))
            {
                error = "Directional input must contain finite X and Y values.";
                return false;
            }
            if (!IsFinite(deltaTime) || deltaTime < 0d || deltaTime > float.MaxValue ||
                !IsFinite(stateSpeed) || stateSpeed < 0f ||
                !IsFinite(dampingTimeX) || dampingTimeX < 0f ||
                !IsFinite(dampingTimeY) || dampingTimeY < 0f)
            {
                error = "Directional delta time, state speed, and damping times must be finite and nonnegative.";
                return false;
            }

            float dt = (float)deltaTime;
            float nextVelocityX = dampingVelocity.x;
            float nextVelocityY = dampingVelocity.y;
            bool settledInput = targetInput.x == input.x && targetInput.y == input.y &&
                dampingVelocity.x == 0f && dampingVelocity.y == 0f;
            float nextX;
            float nextY;
            if (settledInput)
            {
                // Preserve zero-damping behavior while skipping work for settled input.
                nextX = dampingTimeX <= 0f ? targetInput.x : input.x;
                nextY = dampingTimeY <= 0f ? targetInput.y : input.y;
                if (dampingTimeX <= 0f) nextVelocityX = 0f;
                if (dampingTimeY <= 0f) nextVelocityY = 0f;
            }
            else
            {
                nextX = SmoothDamp(input.x, targetInput.x, ref nextVelocityX, dampingTimeX, dt);
                nextY = SmoothDamp(input.y, targetInput.y, ref nextVelocityY, dampingTimeY, dt);
            }
            if (!IsFinite(nextX) || !IsFinite(nextY) || !IsFinite(nextVelocityX) || !IsFinite(nextVelocityY))
            {
                error = "Directional damping produced non-finite input or velocity state.";
                return false;
            }

            Vector2 nextInput = new Vector2(nextX, nextY);
            bool inputChanged = nextX != input.x || nextY != input.y;
            float nextPeriod = period;
            if (inputChanged && !solver.TryEvaluate(nextInput, candidateWeights, out nextPeriod, out error))
                return false;

            double nextPhase = phase;
            double nextCyclePosition = cyclePosition;
            if (deltaTime > 0d && stateSpeed > 0f)
            {
                double phaseDelta = (double)stateSpeed * deltaTime / nextPeriod;
                if (!IsFinite(phaseDelta))
                {
                    error = "Directional phase advance was non-finite.";
                    return false;
                }
                nextPhase = Wrap01(phase + phaseDelta);
                nextCyclePosition = cyclePosition + phaseDelta;
            }

            input = nextInput;
            dampingVelocity = new Vector2(nextVelocityX, nextVelocityY);
            period = nextPeriod;
            phase = nextPhase;
            cyclePosition = nextCyclePosition;
            if (inputChanged) Array.Copy(candidateWeights, weights, weights.Length);
            return true;
        }

        /// <summary>
        /// Returns the child clip time implied by the shared canonical phase and the motion's authored
        /// cycle offset: frac(phase + offset) * clipLength. Offset 0 returns exactly phase * clipLength.
        /// </summary>
        public double GetChildTime(int motionIndex)
        {
            if (!initialized) throw new InvalidOperationException("Initialize the directional port before reading child time.");
            float cycleOffset = solver.GetCycleOffset(motionIndex);
            double samplePhase = cycleOffset == 0f ? phase : Wrap01(phase + cycleOffset);
            return samplePhase * solver.GetClipLength(motionIndex);
        }

        public float GetWeight(int motionIndex)
        {
            if (!initialized) throw new InvalidOperationException("Initialize the directional port before reading weights.");
            if ((uint)motionIndex >= (uint)weights.Length) throw new ArgumentOutOfRangeException(nameof(motionIndex));
            return weights[motionIndex];
        }

        /// <summary>Copies all mutable values into this port without sharing either weight array.</summary>
        public void CopyFrom(HonamiBlend2DPortRuntime source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!ReferenceEquals(solver, source.solver))
                throw new ArgumentException("Directional port states can only copy between ports using the same immutable solver.", nameof(source));

            input = source.input;
            dampingVelocity = source.dampingVelocity;
            phase = source.phase;
            cyclePosition = source.cyclePosition;
            period = source.period;
            initialized = source.initialized;
            Array.Copy(source.weights, weights, weights.Length);
            Array.Copy(source.candidateWeights, candidateWeights, candidateWeights.Length);
        }

        /// <summary>Clears this port so it can be initialized from a fresh state start.</summary>
        public void Reset()
        {
            input = Vector2.zero;
            dampingVelocity = Vector2.zero;
            phase = 0d;
            cyclePosition = 0d;
            period = 0f;
            initialized = false;
            Array.Clear(weights, 0, weights.Length);
            Array.Clear(candidateWeights, 0, candidateWeights.Length);
        }

        private static float SmoothDamp(float current, float target, ref float velocity,
            float smoothTime, float deltaTime)
        {
            if (smoothTime <= 0f)
            {
                velocity = 0f;
                return target;
            }
            if (deltaTime == 0f)
                return current;
            return Mathf.SmoothDamp(current, target, ref velocity, smoothTime,
                float.PositiveInfinity, deltaTime);
        }

        private static double Wrap01(double value)
        {
            double wrapped = value - Math.Floor(value);
            return wrapped >= 1d ? 0d : wrapped;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
