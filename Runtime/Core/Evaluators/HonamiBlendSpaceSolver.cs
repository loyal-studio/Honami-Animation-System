using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Weight distribution strategies for <see cref="HonamiBlendSpaceNode"/>.
    /// </summary>
    public enum HonamiBlendSpaceType
    {
        FreeformDirectional,
        FreeformCartesian,
        SimpleDirectional
    }

    /// <summary>
    /// Allocation-free weight solver for blend spaces of any dimension. Weights are written into a
    /// caller-owned span and always normalized to sum to one.
    /// </summary>
    public static class HonamiBlendSpaceSolver
    {
        private const float DirectionalAngleScale = 2f;
        private const float Epsilon = 0.0001f;
        private const int StackLimit = 64;

        /// <summary>
        /// Axis count a space type is locked to, or 0 when it accepts any number of axes.
        /// Simple Directional brackets the sample between two motions by angular order around a
        /// circle, which only exists in the plane.
        /// </summary>
        public static int FixedAxisCount(HonamiBlendSpaceType spaceType)
            => spaceType == HonamiBlendSpaceType.SimpleDirectional ? 2 : 0;

        public static bool SupportsAxisCount(HonamiBlendSpaceType spaceType, int axisCount)
        {
            int fixedCount = FixedAxisCount(spaceType);
            return fixedCount == 0 ? axisCount >= 1 : axisCount == fixedCount;
        }

        public static void CalculateWeights(
            IReadOnlyList<HonamiBlendSpaceMotion> motions,
            int axisCount,
            HonamiBlendSpaceType spaceType,
            ReadOnlySpan<float> sample,
            Span<float> weights)
        {
            int count = motions?.Count ?? 0;
            if (count == 0 || axisCount <= 0)
            {
                return;
            }

            if (count > weights.Length)
            {
                count = weights.Length;
            }

            weights.Slice(0, count).Clear();

            if (count == 1)
            {
                weights[0] = 1f;
                return;
            }

            if (spaceType == HonamiBlendSpaceType.SimpleDirectional && axisCount == 2)
            {
                CalculateSimpleDirectional(motions, count, sample, weights);
            }
            else if (spaceType == HonamiBlendSpaceType.FreeformCartesian)
            {
                CalculateCartesian(motions, count, axisCount, sample, weights);
            }
            else
            {
                CalculateDirectional(motions, count, axisCount, sample, weights);
            }

            Normalize(weights, count);
        }

        public static float GetBlendedDuration(IReadOnlyList<HonamiBlendSpaceMotion> motions, ReadOnlySpan<float> weights)
        {
            int count = motions?.Count ?? 0;
            if (count == 0) return 1f;
            if (count > weights.Length) count = weights.Length;

            float duration = 0f;
            for (int i = 0; i < count; i++)
            {
                duration += weights[i] * GetMotionDuration(motions[i]);
            }

            return duration > Epsilon ? duration : 1f;
        }

        public static float GetMotionDuration(HonamiBlendSpaceMotion motion)
        {
            if (motion == null || motion.clip == null) return 0f;
            float speed = Mathf.Abs(motion.speed);
            return motion.clip.length / (speed > 0f ? speed : 1f);
        }

        public static float GetAxis(HonamiBlendSpaceMotion motion, int axis)
        {
            var position = motion?.position;
            return position != null && axis < position.Length ? position[axis] : 0f;
        }

        private static void CalculateCartesian(
            IReadOnlyList<HonamiBlendSpaceMotion> motions,
            int count,
            int axisCount,
            ReadOnlySpan<float> sample,
            Span<float> weights)
        {
            for (int i = 0; i < count; i++)
            {
                var motionI = motions[i];
                float value = 1f;

                for (int j = 0; j < count && value > 0f; j++)
                {
                    if (j == i) continue;

                    var motionJ = motions[j];
                    float squared = 0f;
                    float projection = 0f;

                    for (int axis = 0; axis < axisCount; axis++)
                    {
                        float origin = GetAxis(motionI, axis);
                        float toJ = GetAxis(motionJ, axis) - origin;
                        float toSample = Sample(sample, axis) - origin;
                        squared += toJ * toJ;
                        projection += toSample * toJ;
                    }

                    if (squared < Epsilon) continue;

                    float influence = 1f - projection / squared;
                    if (influence < value) value = influence;
                }

                weights[i] = value > 0f ? value : 0f;
            }
        }

        // Polar gradient band generalized to N axes: each pair is compared on a magnitude axis and an
        // angular axis, where the sample's angular offset is projected onto the i -> j direction. In
        // two axes this reduces exactly to the signed angle between them.
        private static void CalculateDirectional(
            IReadOnlyList<HonamiBlendSpaceMotion> motions,
            int count,
            int axisCount,
            ReadOnlySpan<float> sample,
            Span<float> weights)
        {
            Span<float> magnitudes = count <= StackLimit ? stackalloc float[count] : new float[count];
            for (int i = 0; i < count; i++)
            {
                magnitudes[i] = Magnitude(motions[i], axisCount);
            }

            float sampleSquared = DotSample(sample, sample, axisCount);
            float sampleMagnitude = Mathf.Sqrt(sampleSquared);

            for (int i = 0; i < count; i++)
            {
                var motionI = motions[i];
                float magnitudeI = magnitudes[i];
                float dotIS = DotMotionSample(motionI, sample, axisCount);
                float value = 1f;

                for (int j = 0; j < count && value > 0f; j++)
                {
                    if (j == i) continue;

                    float influence = DirectionalInfluence(
                        motionI, motions[j], sample, axisCount,
                        magnitudeI, magnitudes[j], sampleMagnitude, sampleSquared, dotIS);

                    if (influence < value) value = influence;
                }

                weights[i] = value > 0f ? value : 0f;
            }
        }

        private static float DirectionalInfluence(
            HonamiBlendSpaceMotion motionI,
            HonamiBlendSpaceMotion motionJ,
            ReadOnlySpan<float> sample,
            int axisCount,
            float magnitudeI,
            float magnitudeJ,
            float sampleMagnitude,
            float sampleSquared,
            float dotIS)
        {
            float magnitudeAverage = (magnitudeI + magnitudeJ) * 0.5f;
            if (magnitudeAverage < Epsilon)
            {
                return 1f;
            }

            float angleToJ;
            float angleToSample;

            if (magnitudeI < Epsilon || magnitudeJ < Epsilon || sampleMagnitude < Epsilon)
            {
                angleToJ = 0f;
                angleToSample = 0f;
            }
            else
            {
                float dotIJ = DotMotions(motionI, motionJ, axisCount);
                float dotJS = DotMotionSample(motionJ, sample, axisCount);

                angleToJ = AngleFromDot(dotIJ, magnitudeI, magnitudeJ);
                float rawAngleToSample = AngleFromDot(dotIS, magnitudeI, sampleMagnitude);
                angleToSample = rawAngleToSample * PerpendicularCosine(
                    dotIJ, dotIS, dotJS, magnitudeI, magnitudeJ, sampleSquared, ref angleToJ);
            }

            float magnitudeTerm = (magnitudeJ - magnitudeI) / magnitudeAverage;
            float angleTerm = angleToJ * DirectionalAngleScale;
            float squared = magnitudeTerm * magnitudeTerm + angleTerm * angleTerm;
            if (squared < Epsilon)
            {
                return 1f;
            }

            float sampleMagnitudeTerm = (sampleMagnitude - magnitudeI) / magnitudeAverage;
            float sampleAngleTerm = angleToSample * DirectionalAngleScale;
            return 1f - (sampleMagnitudeTerm * magnitudeTerm + sampleAngleTerm * angleTerm) / squared;
        }

        // Cosine between the components of the sample and of j perpendicular to i, computed straight
        // from dot products so no temporary vectors are needed. Returns the sign-and-scale that turns
        // the unsigned i -> sample angle into a displacement along the i -> j angular direction.
        private static float PerpendicularCosine(
            float dotIJ,
            float dotIS,
            float dotJS,
            float magnitudeI,
            float magnitudeJ,
            float sampleSquared,
            ref float angleToJ)
        {
            float inverseSquaredI = 1f / (magnitudeI * magnitudeI);
            float perpJSquared = magnitudeJ * magnitudeJ - dotIJ * dotIJ * inverseSquaredI;
            float perpSampleSquared = sampleSquared - dotIS * dotIS * inverseSquaredI;

            if (perpJSquared < Epsilon)
            {
                // i and j are colinear: the pair has no angular axis of its own.
                if (angleToJ <= Mathf.PI * 0.5f)
                {
                    angleToJ = 0f;
                    return 0f;
                }

                return 1f;
            }

            if (perpSampleSquared < Epsilon)
            {
                return 1f;
            }

            float perpDot = dotJS - dotIJ * dotIS * inverseSquaredI;
            float cosine = perpDot / Mathf.Sqrt(perpJSquared * perpSampleSquared);
            return Mathf.Clamp(cosine, -1f, 1f);
        }

        private static void CalculateSimpleDirectional(
            IReadOnlyList<HonamiBlendSpaceMotion> motions,
            int count,
            ReadOnlySpan<float> sample,
            Span<float> weights)
        {
            Vector2 sample2D = new(Sample(sample, 0), Sample(sample, 1));

            int centerIndex = -1;
            int directionalCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (IsCenter(Position2D(motions[i])))
                {
                    if (centerIndex < 0) centerIndex = i;
                }
                else
                {
                    directionalCount++;
                }
            }

            if (directionalCount == 0)
            {
                return;
            }

            if (sample2D.sqrMagnitude < Epsilon * Epsilon)
            {
                if (centerIndex >= 0)
                {
                    weights[centerIndex] = 1f;
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    if (!IsCenter(Position2D(motions[i]))) weights[i] = 1f;
                }
                return;
            }

            FindBracket(motions, count, sample2D, out int clockwiseIndex, out int counterClockwiseIndex);

            if (clockwiseIndex < 0 || counterClockwiseIndex < 0 || clockwiseIndex == counterClockwiseIndex)
            {
                ApplySingleDirection(motions, sample2D, counterClockwiseIndex >= 0 ? counterClockwiseIndex : clockwiseIndex, centerIndex, weights);
                return;
            }

            Vector2 clockwise = Position2D(motions[clockwiseIndex]);
            Vector2 counterClockwise = Position2D(motions[counterClockwiseIndex]);
            float determinant = clockwise.x * counterClockwise.y - clockwise.y * counterClockwise.x;

            if (Mathf.Abs(determinant) < Epsilon)
            {
                ApplySingleDirection(motions, sample2D, counterClockwiseIndex, centerIndex, weights);
                return;
            }

            float clockwiseWeight = Mathf.Max(0f, (sample2D.x * counterClockwise.y - sample2D.y * counterClockwise.x) / determinant);
            float counterClockwiseWeight = Mathf.Max(0f, (clockwise.x * sample2D.y - clockwise.y * sample2D.x) / determinant);

            float total = clockwiseWeight + counterClockwiseWeight;
            if (total > 1f)
            {
                clockwiseWeight /= total;
                counterClockwiseWeight /= total;
                total = 1f;
            }

            weights[clockwiseIndex] = clockwiseWeight;
            weights[counterClockwiseIndex] = counterClockwiseWeight;

            if (centerIndex >= 0)
            {
                weights[centerIndex] = 1f - total;
            }
        }

        private static void FindBracket(
            IReadOnlyList<HonamiBlendSpaceMotion> motions,
            int count,
            Vector2 sample,
            out int clockwiseIndex,
            out int counterClockwiseIndex)
        {
            clockwiseIndex = -1;
            counterClockwiseIndex = -1;
            float clockwiseBest = float.MaxValue;
            float counterClockwiseBest = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Vector2 position = Position2D(motions[i]);
                if (IsCenter(position)) continue;

                float angle = SignedAngle(sample, position);
                if (angle >= 0f)
                {
                    if (angle < counterClockwiseBest)
                    {
                        counterClockwiseBest = angle;
                        counterClockwiseIndex = i;
                    }
                }
                else if (-angle < clockwiseBest)
                {
                    clockwiseBest = -angle;
                    clockwiseIndex = i;
                }
            }
        }

        private static void ApplySingleDirection(
            IReadOnlyList<HonamiBlendSpaceMotion> motions,
            Vector2 sample,
            int index,
            int centerIndex,
            Span<float> weights)
        {
            if (index < 0) return;

            float magnitude = Position2D(motions[index]).magnitude;
            float weight = magnitude > Epsilon ? Mathf.Clamp01(sample.magnitude / magnitude) : 1f;

            weights[index] = weight;
            if (centerIndex >= 0)
            {
                weights[centerIndex] = 1f - weight;
            }
        }

        private static Vector2 Position2D(HonamiBlendSpaceMotion motion)
            => new(GetAxis(motion, 0), GetAxis(motion, 1));

        private static bool IsCenter(Vector2 position) => position.sqrMagnitude < Epsilon * Epsilon;

        private static float SignedAngle(Vector2 from, Vector2 to)
            => Mathf.Atan2(from.x * to.y - from.y * to.x, from.x * to.x + from.y * to.y);

        private static float AngleFromDot(float dot, float magnitudeA, float magnitudeB)
        {
            float denominator = magnitudeA * magnitudeB;
            if (denominator < Epsilon) return 0f;
            return Mathf.Acos(Mathf.Clamp(dot / denominator, -1f, 1f));
        }

        private static float Sample(ReadOnlySpan<float> sample, int axis)
            => axis < sample.Length ? sample[axis] : 0f;

        private static float Magnitude(HonamiBlendSpaceMotion motion, int axisCount)
        {
            float squared = 0f;
            for (int axis = 0; axis < axisCount; axis++)
            {
                float value = GetAxis(motion, axis);
                squared += value * value;
            }
            return Mathf.Sqrt(squared);
        }

        private static float DotMotions(HonamiBlendSpaceMotion a, HonamiBlendSpaceMotion b, int axisCount)
        {
            float sum = 0f;
            for (int axis = 0; axis < axisCount; axis++)
            {
                sum += GetAxis(a, axis) * GetAxis(b, axis);
            }
            return sum;
        }

        private static float DotMotionSample(HonamiBlendSpaceMotion motion, ReadOnlySpan<float> sample, int axisCount)
        {
            float sum = 0f;
            for (int axis = 0; axis < axisCount; axis++)
            {
                sum += GetAxis(motion, axis) * Sample(sample, axis);
            }
            return sum;
        }

        private static float DotSample(ReadOnlySpan<float> a, ReadOnlySpan<float> b, int axisCount)
        {
            float sum = 0f;
            for (int axis = 0; axis < axisCount; axis++)
            {
                sum += Sample(a, axis) * Sample(b, axis);
            }
            return sum;
        }

        private static void Normalize(Span<float> weights, int count)
        {
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                if (weights[i] < 0f) weights[i] = 0f;
                sum += weights[i];
            }

            if (sum <= Epsilon)
            {
                float share = 1f / count;
                for (int i = 0; i < count; i++) weights[i] = share;
                return;
            }

            float inverse = 1f / sum;
            for (int i = 0; i < count; i++) weights[i] *= inverse;
        }
    }
}
