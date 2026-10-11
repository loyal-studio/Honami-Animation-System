using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Immutable, preprocessed Polar Gradient Bands Interpolation solver for directional trees.
    /// This is a deterministic candidate for Unity Freeform Directional, not a claim of native
    /// intermediate-weight parity.
    /// </summary>
    public sealed class HonamiDirectionalBlendSolver
    {
        public const int MaximumMotionCount = 17;

        private const double Alpha = 2.0;
        private const double DuplicateDistanceSquared = 1e-12;
        private const double AngularCoverageTolerance = 1e-8;
        private const double WeightSumEpsilon = 1e-12;
        private const double RayIntersectionTolerance = 1e-10;
        private const double TwoPi = Math.PI * 2.0;

        private readonly Vector2[] positions;
        private readonly double[] radii;
        private readonly double[] angles;
        private readonly float[] clipLengths;
        private readonly float[] childSpeeds;
        private readonly float[] cycleOffsets;
        private readonly PairCoefficients[] pairs;
        private readonly Vector2[] hull;
        private readonly int originIndex;

        private readonly struct PairCoefficients
        {
            internal readonly double inverseMeanRadius;
            internal readonly double radialDelta;
            internal readonly double angularDelta;
            internal readonly double inverseLengthSquared;
            internal readonly bool usesAngle;

            internal PairCoefficients(double inverseMeanRadius, double radialDelta,
                double angularDelta, double inverseLengthSquared, bool usesAngle)
            {
                this.inverseMeanRadius = inverseMeanRadius;
                this.radialDelta = radialDelta;
                this.angularDelta = angularDelta;
                this.inverseLengthSquared = inverseLengthSquared;
                this.usesAngle = usesAngle;
            }
        }

        public int MotionCount => positions.Length;

        /// <summary>Prepares one immutable authored topology with all cycle offsets at zero. Input arrays are copied.</summary>
        public HonamiDirectionalBlendSolver(Vector2[] authoredPositions, float[] authoredClipLengths,
            float[] authoredChildSpeeds) : this(authoredPositions, authoredClipLengths, authoredChildSpeeds, null)
        {
        }

        /// <summary>Prepares one immutable authored topology. Input arrays are copied.</summary>
        /// <param name="authoredCycleOffsets">Optional per-motion normalized cycle offsets in [0, 1),
        /// like Unity's ChildMotion.cycleOffset: a child samples its clip at frac(phase + offset).
        /// They change only sampled child time, never weights, period, or phase. Null means all zero.</param>
        public HonamiDirectionalBlendSolver(Vector2[] authoredPositions, float[] authoredClipLengths,
            float[] authoredChildSpeeds, float[] authoredCycleOffsets)
        {
            ValidateInputs(authoredPositions, authoredClipLengths, authoredChildSpeeds, out int foundOrigin);
            ValidateCycleOffsets(authoredCycleOffsets, authoredPositions.Length);

            positions = (Vector2[])authoredPositions.Clone();
            clipLengths = (float[])authoredClipLengths.Clone();
            childSpeeds = (float[])authoredChildSpeeds.Clone();
            cycleOffsets = authoredCycleOffsets != null
                ? (float[])authoredCycleOffsets.Clone()
                : new float[authoredPositions.Length];
            originIndex = foundOrigin;
            radii = new double[positions.Length];
            angles = new double[positions.Length];
            pairs = new PairCoefficients[checked(positions.Length * positions.Length)];

            for (int i = 0; i < positions.Length; i++)
            {
                double x = positions[i].x;
                double y = positions[i].y;
                radii[i] = Math.Sqrt(x * x + y * y);
                angles[i] = Math.Atan2(y, x);
            }

            BuildPairCoefficients();
            hull = BuildDirectionalHull(positions, originIndex);
        }

        public Vector2 GetPosition(int motionIndex)
        {
            ValidateIndex(motionIndex);
            return positions[motionIndex];
        }

        public float GetClipLength(int motionIndex)
        {
            ValidateIndex(motionIndex);
            return clipLengths[motionIndex];
        }

        /// <summary>Returns the motion's normalized cycle offset in [0, 1).</summary>
        public float GetCycleOffset(int motionIndex)
        {
            ValidateIndex(motionIndex);
            return cycleOffsets[motionIndex];
        }

        /// <summary>Read-only view of all per-motion cycle offsets (all zero when none were authored).</summary>
        public ReadOnlySpan<float> CycleOffsets => cycleOffsets;

        /// <summary>True when the value is a valid normalized cycle offset: finite and within [0, 1).</summary>
        public static bool IsValidCycleOffset(float offset)
        {
            return !float.IsNaN(offset) && !float.IsInfinity(offset) && offset >= 0f && offset < 1f;
        }

        private static void ValidateCycleOffsets(float[] offsets, int motionCount)
        {
            if (offsets == null)
                return;
            if (offsets.Length != motionCount)
                throw new ArgumentException("Directional cycle offsets must have one entry per motion.", nameof(offsets));
            for (int i = 0; i < offsets.Length; i++)
            {
                if (!IsValidCycleOffset(offsets[i]))
                    throw new ArgumentException($"Directional motion {i} cycle offset must be finite and within [0, 1).", nameof(offsets));
            }
        }

        public float GetChildSpeed(int motionIndex)
        {
            ValidateIndex(motionIndex);
            return childSpeeds[motionIndex];
        }

        /// <summary>
        /// Writes a normalized nonnegative weight vector and its effective period into caller-owned
        /// storage. On failure, destinationWeights is left unchanged.
        /// </summary>
        public bool TryEvaluate(Vector2 query, float[] destinationWeights, out float period, out string error)
        {
            period = 0f;
            error = null;
            if (destinationWeights == null || destinationWeights.Length < MotionCount)
            {
                error = "The caller-owned weight buffer is smaller than the solver motion count.";
                return false;
            }
            if (!IsFinite(query.x) || !IsFinite(query.y))
            {
                error = "Directional input must contain finite X and Y values.";
                return false;
            }

            Span<float> candidateWeights = stackalloc float[MaximumMotionCount];
            candidateWeights.Clear();
            if (MotionCount == 1 || (query.x == 0f && query.y == 0f))
            {
                int oneHotIndex = MotionCount == 1 ? 0 : originIndex;
                candidateWeights[oneHotIndex] = 1f;
            }
            else
            {
                bool exactSample = false;
                for (int i = 0; i < MotionCount; i++)
                {
                    if (query.x == positions[i].x && query.y == positions[i].y)
                    {
                        candidateWeights[i] = 1f;
                        exactSample = true;
                        break;
                    }
                }

                if (!exactSample)
                {
                    double queryAngle = Math.Atan2(query.y, query.x);
                    EvaluateBands(query, queryAngle, candidateWeights, out string evaluationError);
                    if (evaluationError != null)
                    {
                        error = evaluationError;
                        return false;
                    }
                }
            }

            double weightSum = 0d;
            int largestIndex = 0;
            for (int i = 0; i < MotionCount; i++)
            {
                float value = candidateWeights[i];
                if (!IsFinite(value) || value < 0f)
                {
                    error = "The directional solver produced a non-finite or negative candidate weight.";
                    return false;
                }
                weightSum += value;
                if (value > candidateWeights[largestIndex]) largestIndex = i;
            }

            if (weightSum <= WeightSumEpsilon)
            {
                error = "The directional solver could not produce a positive finite weight sum.";
                return false;
            }
            else
            {
                for (int i = 0; i < MotionCount; i++)
                    candidateWeights[i] = (float)(candidateWeights[i] / weightSum);

                float normalizedSum = 0f;
                for (int i = 0; i < MotionCount; i++) normalizedSum += candidateWeights[i];
                candidateWeights[largestIndex] += 1f - normalizedSum;
            }

            double effectivePeriod = 0d;
            for (int i = 0; i < MotionCount; i++)
                effectivePeriod += candidateWeights[i] * (double)clipLengths[i] / childSpeeds[i];
            if (!IsFinite(effectivePeriod) || effectivePeriod <= 0d || effectivePeriod > float.MaxValue)
            {
                error = "The directional solver produced a non-finite or non-positive cycle period.";
                return false;
            }
            float effectivePeriodFloat = (float)effectivePeriod;
            if (!IsFinite(effectivePeriodFloat) || effectivePeriodFloat <= 0f)
            {
                error = "The directional cycle period is outside the positive finite float range.";
                return false;
            }

            for (int i = 0; i < MotionCount; i++)
                destinationWeights[i] = candidateWeights[i];
            period = effectivePeriodFloat;
            return true;
        }

        private void EvaluateBands(Vector2 query, double queryAngle, Span<float> candidateWeights, out string error)
        {
            error = null;
            double x = query.x;
            double y = query.y;
            double queryRadius = Math.Sqrt(x * x + y * y);
            if (!TryClampToDirectionalHull(x, y, queryRadius, out double clampedRadius))
            {
                error = "The query ray does not intersect the preprocessed directional hull.";
                return;
            }

            double weightSum = 0d;
            for (int i = 0; i < MotionCount; i++)
            {
                double minimumBand = double.PositiveInfinity;
                double queryRadialBase = clampedRadius - radii[i];
                int pairBase = i * MotionCount;
                for (int j = 0; j < MotionCount; j++)
                {
                    if (i == j) continue;
                    PairCoefficients pair = pairs[pairBase + j];
                    double queryRadial = queryRadialBase * pair.inverseMeanRadius;
                    double queryAngular = pair.usesAngle
                        ? Alpha * WrapSignedAngle(angles[i] - queryAngle)
                        : 0d;
                    double projection = (queryRadial * pair.radialDelta +
                                         queryAngular * pair.angularDelta) * pair.inverseLengthSquared;
                    double band = 1d - projection;
                    if (!IsFinite(band))
                    {
                        error = "A directional gradient band was non-finite.";
                        return;
                    }
                    if (band < minimumBand) minimumBand = band;
                }

                double nonnegative = Math.Max(0d, minimumBand);
                if (!IsFinite(nonnegative) || nonnegative > float.MaxValue)
                {
                    error = "A directional motion weight was non-finite or out of range.";
                    return;
                }
                candidateWeights[i] = (float)nonnegative;
                weightSum += nonnegative;
            }

            if (!IsFinite(weightSum))
            {
                error = "The directional weight sum was non-finite.";
                return;
            }

            if (weightSum <= WeightSumEpsilon)
            {
                int nearest = FindNearest(clampedRadius, queryAngle);
                for (int i = 0; i < MotionCount; i++) candidateWeights[i] = i == nearest ? 1f : 0f;
                return;
            }

            for (int i = 0; i < MotionCount; i++)
                candidateWeights[i] = (float)(candidateWeights[i] / weightSum);
        }

        private bool TryClampToDirectionalHull(double queryX, double queryY, double queryRadius,
            out double resultRadius)
        {
            resultRadius = queryRadius;
            if (hull.Length == 0) return MotionCount == 1;
            if (queryRadius == 0d) return true;

            double directionX = queryX / queryRadius;
            double directionY = queryY / queryRadius;
            double boundaryRadius = double.PositiveInfinity;
            for (int i = 0; i < hull.Length; i++)
            {
                Vector2 a = hull[i];
                Vector2 b = hull[(i + 1) % hull.Length];
                double edgeX = (double)b.x - a.x;
                double edgeY = (double)b.y - a.y;
                double denominator = Cross(directionX, directionY, edgeX, edgeY);
                if (Math.Abs(denominator) <= RayIntersectionTolerance) continue;

                double t = Cross(a.x, a.y, edgeX, edgeY) / denominator;
                double u = Cross(a.x, a.y, directionX, directionY) / denominator;
                if (t < -RayIntersectionTolerance || u < -RayIntersectionTolerance ||
                    u > 1d + RayIntersectionTolerance)
                    continue;
                if (t >= 0d && t < boundaryRadius) boundaryRadius = t;
            }

            if (!IsFinite(boundaryRadius) || boundaryRadius <= 0d) return false;
            if (queryRadius > boundaryRadius)
                resultRadius = boundaryRadius;
            return true;
        }

        private int FindNearest(double queryRadius, double queryAngle)
        {
            double queryX = queryRadius * Math.Cos(queryAngle);
            double queryY = queryRadius * Math.Sin(queryAngle);
            int nearest = 0;
            double bestDistanceSquared = double.PositiveInfinity;
            for (int i = 0; i < MotionCount; i++)
            {
                double dx = queryX - positions[i].x;
                double dy = queryY - positions[i].y;
                double distanceSquared = dx * dx + dy * dy;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    nearest = i;
                }
            }
            return nearest;
        }

        private void BuildPairCoefficients()
        {
            for (int i = 0; i < MotionCount; i++)
            {
                for (int j = 0; j < MotionCount; j++)
                {
                    if (i == j) continue;
                    double meanRadius = (radii[i] + radii[j]) * 0.5d;
                    if (!IsFinite(meanRadius) || meanRadius <= 0d)
                        throw new ArgumentException("Distinct directional samples must not both be at the origin.");

                    double radialDelta = (radii[j] - radii[i]) / meanRadius;
                    bool usesAngle = i != originIndex && j != originIndex;
                    double angularDelta = usesAngle
                        ? Alpha * WrapSignedAngle(angles[i] - angles[j])
                        : 0d;
                    double lengthSquared = radialDelta * radialDelta + angularDelta * angularDelta;
                    if (!IsFinite(lengthSquared) || lengthSquared <= 0d)
                        throw new ArgumentException("Directional sample pairs must have distinct polar coordinates.");

                    pairs[i * MotionCount + j] = new PairCoefficients(
                        1d / meanRadius, radialDelta, angularDelta, 1d / lengthSquared, usesAngle);
                }
            }
        }

        private static void ValidateInputs(Vector2[] authoredPositions, float[] authoredClipLengths,
            float[] authoredChildSpeeds, out int origin)
        {
            origin = -1;
            if (authoredPositions == null || authoredClipLengths == null || authoredChildSpeeds == null)
                throw new ArgumentNullException("Directional sample arrays cannot be null.");
            int count = authoredPositions.Length;
            if (count < 1 || count > MaximumMotionCount || authoredClipLengths.Length != count ||
                authoredChildSpeeds.Length != count)
                throw new ArgumentException($"Directional trees require 1..{MaximumMotionCount} aligned position, clip-length, and child-speed entries.");

            for (int i = 0; i < count; i++)
            {
                Vector2 point = authoredPositions[i];
                if (!IsFinite(point.x) || !IsFinite(point.y))
                    throw new ArgumentException($"Directional position {i} must be finite.");
                if (!IsFinite(authoredClipLengths[i]) || authoredClipLengths[i] <= 0f)
                    throw new ArgumentException($"Directional clip length {i} must be finite and positive.");
                if (!IsFinite(authoredChildSpeeds[i]) || authoredChildSpeeds[i] <= 0f)
                    throw new ArgumentException($"Directional child speed {i} must be finite and positive.");
                if (point.x == 0f && point.y == 0f)
                {
                    if (origin >= 0) throw new ArgumentException("Directional topology must have exactly one origin sample.");
                    origin = i;
                }

                for (int j = 0; j < i; j++)
                {
                    double dx = (double)point.x - authoredPositions[j].x;
                    double dy = (double)point.y - authoredPositions[j].y;
                    if (dx * dx + dy * dy <= DuplicateDistanceSquared)
                        throw new ArgumentException($"Directional positions {j} and {i} are duplicate or numerically indistinguishable.");
                }
            }

            if (origin < 0)
                throw new ArgumentException("Directional topology must include one exact (0,0) origin sample.");
            if (count == 1) return;

            var uniqueAngles = new List<double>(count - 1);
            for (int i = 0; i < count; i++)
            {
                if (i == origin) continue;
                double angle = Math.Atan2(authoredPositions[i].y, authoredPositions[i].x);
                if (angle < 0d) angle += TwoPi;
                uniqueAngles.Add(angle);
            }
            uniqueAngles.Sort();
            double maxGap = 0d;
            double previous = uniqueAngles[0];
            for (int i = 1; i < uniqueAngles.Count; i++)
            {
                double current = uniqueAngles[i];
                if (current - previous > AngularCoverageTolerance)
                    maxGap = Math.Max(maxGap, current - previous);
                previous = current;
            }
            maxGap = Math.Max(maxGap, uniqueAngles[0] + TwoPi - uniqueAngles[uniqueAngles.Count - 1]);
            if (maxGap >= Math.PI - AngularCoverageTolerance)
                throw new ArgumentException("Directional samples must surround the origin with angular gaps strictly below 180 degrees.");
        }

        private static Vector2[] BuildDirectionalHull(Vector2[] source, int origin)
        {
            if (source.Length == 1) return Array.Empty<Vector2>();
            var points = new List<Vector2>(source.Length - 1);
            for (int i = 0; i < source.Length; i++)
                if (i != origin) points.Add(source[i]);
            points.Sort(ComparePoints);

            var lower = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 0d)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(point);
            }

            var upper = new List<Vector2>(points.Count);
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 point = points[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 0d)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(point);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            if (lower.Count < 3)
                throw new ArgumentException("Directional samples must form a two-dimensional hull around the origin.");
            return lower.ToArray();
        }

        private static int ComparePoints(Vector2 left, Vector2 right)
        {
            int xOrder = left.x.CompareTo(right.x);
            return xOrder != 0 ? xOrder : left.y.CompareTo(right.y);
        }

        private static double Cross(Vector2 a, Vector2 b, Vector2 c)
            => Cross((double)b.x - a.x, (double)b.y - a.y,
                (double)c.x - a.x, (double)c.y - a.y);

        private static double Cross(double ax, double ay, double bx, double by)
            => ax * by - ay * bx;

        private static double WrapSignedAngle(double angle)
        {
            if (angle <= -Math.PI) angle += TwoPi;
            else if (angle > Math.PI) angle -= TwoPi;
            return angle;
        }

        private void ValidateIndex(int motionIndex)
        {
            if ((uint)motionIndex >= (uint)MotionCount)
                throw new ArgumentOutOfRangeException(nameof(motionIndex));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
