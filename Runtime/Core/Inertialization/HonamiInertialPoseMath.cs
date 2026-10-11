using Unity.Mathematics;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>A local TRS value used by the shared inertialization math.</summary>
    public readonly struct HonamiInertialPose
    {
        public float3 Position { get; }
        public quaternion Rotation { get; }
        public float3 Scale { get; }

        public HonamiInertialPose(float3 position, quaternion rotation, float3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }

    /// <summary>Captured pose offset and relative velocity, expressed in local TRS coordinates.</summary>
    public readonly struct HonamiInertialPoseResidual
    {
        public float3 PositionOffset { get; }
        public float3 PositionVelocity { get; }
        public float3 RotationLogOffset { get; }
        public float3 RotationLogVelocity { get; }
        public float3 ScaleOffset { get; }
        public float3 ScaleVelocity { get; }

        public HonamiInertialPoseResidual(float3 positionOffset, float3 positionVelocity,
            float3 rotationLogOffset, float3 rotationLogVelocity,
            float3 scaleOffset, float3 scaleVelocity)
        {
            PositionOffset = positionOffset;
            PositionVelocity = positionVelocity;
            RotationLogOffset = rotationLogOffset;
            RotationLogVelocity = rotationLogVelocity;
            ScaleOffset = scaleOffset;
            ScaleVelocity = scaleVelocity;
        }
    }

    /// <summary>
    /// Pure pose residual capture/application shared by the Playables inertializer and the deterministic Editor
    /// preview. Residuals follow a cubic Hermite curve whose offset and velocity are zero at the
    /// requested duration.
    /// </summary>
    public static class HonamiInertialPoseMath
    {
        /// <summary>
        /// Deltas at or below this are unusable for a finite-difference velocity. Prepare also uses it to
        /// decide that a frame does not advance the destination (so capture must wait for one that does).
        /// </summary>
        public const float MinimumUsableDeltaSeconds = 1e-7f;

        // Rotation conventions used below (why the normalization work is deliberately minimal):
        //  * Every rotation input is a finite quaternion of approximately unit length with an
        //    arbitrary sign: Buffered blend outputs are normalizesafe'd, sample-table rotations are
        //    baked unit quaternions, ApplyResidual outputs are normalized, and Editor preview reads
        //    Transform rotations. SanitizeRotation only re-normalizes inputs that violate that
        //    contract (zero, NaN, wildly scaled), so those still degrade to identity as before.
        //  * Quaternion logarithms are invariant to positive scaling (angle = 2 * atan2(|v|, w),
        //    axis = v / |v|), so products of near-unit quaternions are logged without renormalizing.
        //  * Only the sign of relativeNow is canonicalized; relativeBefore is sign-aligned to it,
        //    so the signs of every other intermediate cancel out.
        /// <summary>
        /// Captures the residual between the visible source pose and the destination pose at transition
        /// start. The source and target velocities are measured over independent intervals: the source
        /// over <paramref name="sourceDeltaSeconds"/> (previous visible outputs), the target over
        /// <paramref name="targetDeltaSeconds"/> (capture-current row to the row one frame delta later).
        /// A delta that is non-finite or &lt;= 1e-7 disables only its own velocity (no velocity of that
        /// side is assumed); the other side stays valid. With equal deltas the result equals the former
        /// single-interval capture.
        /// </summary>
        public static HonamiInertialPoseResidual CaptureResidual(
            HonamiInertialPose sourceCurrent,
            HonamiInertialPose sourcePrevious,
            float sourceDeltaSeconds,
            HonamiInertialPose targetCurrent,
            HonamiInertialPose targetNext,
            float targetDeltaSeconds)
        {
            float3 positionOffset = sourceCurrent.Position - targetCurrent.Position;
            float3 scaleOffset = sourceCurrent.Scale - targetCurrent.Scale;

            quaternion sourceNow = SanitizeRotation(sourceCurrent.Rotation);
            quaternion targetNow = SanitizeRotation(targetCurrent.Rotation);
            quaternion relativeNow = CanonicalizeSign(math.mul(sourceNow, math.conjugate(targetNow)));
            // One logarithm serves both the residual offset and the velocity's "current" end.
            float3 currentLog = LogInHemisphere(relativeNow);

            bool sourceUsable = IsUsableDelta(sourceDeltaSeconds);
            bool targetUsable = IsUsableDelta(targetDeltaSeconds);

            // One decision for position/scale and rotation: intervals equal within float rounding share
            // the former single-interval expressions (bit-identical to the old capture when exactly equal).
            bool equalIntervals = sourceUsable && targetUsable &&
                math.abs(sourceDeltaSeconds - targetDeltaSeconds) <= 1e-6f * targetDeltaSeconds;

            float3 positionVelocity = float3.zero;
            float3 scaleVelocity = float3.zero;
            if (equalIntervals)
            {
                // Equal intervals: keep the exact former expression (bit-identical to the old capture).
                float inverseDelta = 1f / sourceDeltaSeconds;
                positionVelocity = ((sourceCurrent.Position - sourcePrevious.Position) -
                    (targetNext.Position - targetCurrent.Position)) * inverseDelta;
                scaleVelocity = ((sourceCurrent.Scale - sourcePrevious.Scale) -
                    (targetNext.Scale - targetCurrent.Scale)) * inverseDelta;
            }
            else
            {
                if (sourceUsable)
                {
                    float inverseSource = 1f / sourceDeltaSeconds;
                    positionVelocity = (sourceCurrent.Position - sourcePrevious.Position) * inverseSource;
                    scaleVelocity = (sourceCurrent.Scale - sourcePrevious.Scale) * inverseSource;
                }
                if (targetUsable)
                {
                    float inverseTarget = 1f / targetDeltaSeconds;
                    positionVelocity -= (targetNext.Position - targetCurrent.Position) * inverseTarget;
                    scaleVelocity -= (targetNext.Scale - targetCurrent.Scale) * inverseTarget;
                }
            }

            // Rotation velocity is the finite difference of the relative-rotation log over one interval
            // "rotationDelta": the source goes back by its own history, the target is extrapolated back
            // by the same time using its own rate. Without a usable source interval the source is
            // assumed still (sourceBefore = sourceNow) and the target's interval is used.
            float3 rotationVelocity = float3.zero;
            float rotationDelta = sourceUsable ? sourceDeltaSeconds : (targetUsable ? targetDeltaSeconds : 0f);
            if (rotationDelta > 0f)
            {
                quaternion sourceBefore = sourceUsable ? SanitizeRotation(sourcePrevious.Rotation) : sourceNow;
                quaternion targetBefore = targetNow;
                if (targetUsable)
                {
                    quaternion targetAfter = SanitizeRotation(targetNext.Rotation);
                    quaternion targetDelta = math.mul(targetAfter, math.conjugate(targetNow));
                    if (equalIntervals || !sourceUsable)
                    {
                        // Same interval (or the target interval is the only one): undo the target step exactly.
                        targetBefore = math.mul(math.conjugate(targetDelta), targetNow);
                    }
                    else
                    {
                        // Different intervals: scale the target's rotation step to rotationDelta in log
                        // space (constant target angular velocity), then step back.
                        float3 targetStepLog = LogInHemisphere(CanonicalizeSign(targetDelta));
                        targetBefore = math.mul(Exp(targetStepLog * (-rotationDelta / targetDeltaSeconds)),
                            targetNow);
                    }
                }
                quaternion relativeBefore = math.mul(sourceBefore, math.conjugate(targetBefore));
                // Keep the previous relative rotation on the same hemisphere branch as the current
                // one so the finite difference stays continuous through pi. w < 0 afterwards is the
                // intended continuation beyond pi, not an error.
                if (math.dot(relativeNow.value, relativeBefore.value) < 0f)
                    relativeBefore.value = -relativeBefore.value;
                // A single log of relativeNow * conj(relativeBefore) was evaluated and rejected: it
                // is the body-frame angular displacement, which differs from the difference of the
                // two log vectors (what ApplyResidual's Hermite curve interpolates) by roughly
                // |log| / 2 relative error, i.e. far beyond tolerance for large relative angles.
                float3 previousLog = LogInHemisphere(relativeBefore);
                rotationVelocity = (currentLog - previousLog) * (1f / rotationDelta);
            }

            return new HonamiInertialPoseResidual(
                FiniteOrZero(positionOffset), FiniteOrZero(positionVelocity),
                currentLog, FiniteOrZero(rotationVelocity),
                FiniteOrZero(scaleOffset), FiniteOrZero(scaleVelocity));
        }

        /// <summary>
        /// Constant-velocity extrapolation of <paramref name="current"/> by <paramref name="deltaSeconds"/>, with the
        /// velocity measured over <paramref name="previousDeltaSeconds"/> (the interval between previous and current).
        /// Unusable deltas return <paramref name="current"/>. The step ratio is clamped to [0, 2] so one long frame
        /// cannot throw the extrapolated pose far from the source. Rotation is stepped in log space and
        /// left-multiplied, the same convention <see cref="CaptureResidual"/> uses.
        /// </summary>
        public static HonamiInertialPose Extrapolate(HonamiInertialPose current, HonamiInertialPose previous,
            float previousDeltaSeconds, float deltaSeconds)
        {
            if (!IsUsableDelta(previousDeltaSeconds) || !IsUsableDelta(deltaSeconds))
                return current;

            float k = math.clamp(deltaSeconds / previousDeltaSeconds, 0f, 2f);
            float3 position = current.Position + (current.Position - previous.Position) * k;
            float3 scale = current.Scale + (current.Scale - previous.Scale) * k;

            quaternion now = SanitizeRotation(current.Rotation);
            quaternion before = SanitizeRotation(previous.Rotation);
            quaternion step = CanonicalizeSign(math.mul(now, math.conjugate(before)));
            quaternion rotation = NormalizeShortest(math.mul(Exp(LogInHemisphere(step) * k), now));

            if (!IsFinite(position) || !IsFinite(scale))
                return current;
            return new HonamiInertialPose(position, rotation, scale);
        }

        private static bool IsUsableDelta(float deltaSeconds) => IsFinite(deltaSeconds) && deltaSeconds > MinimumUsableDeltaSeconds;

        public static HonamiInertialPose ApplyResidual(HonamiInertialPose targetAtElapsed,
            HonamiInertialPoseResidual residual, float elapsedSeconds, float durationSeconds)
        {
            if (!IsFinite(elapsedSeconds) || !IsFinite(durationSeconds) || durationSeconds <= 0f ||
                elapsedSeconds >= durationSeconds)
                return TargetOnly(targetAtElapsed);

            float elapsed = math.max(0f, elapsedSeconds);
            float u = math.saturate(elapsed / durationSeconds);
            float u2 = u * u;
            float u3 = u2 * u;
            float h00 = (2f * u3) - (3f * u2) + 1f;
            float h10 = u3 - (2f * u2) + u;
            float velocityTimeWeight = durationSeconds * h10;

            float3 positionResidual = residual.PositionOffset * h00 +
                residual.PositionVelocity * velocityTimeWeight;
            float3 scaleResidual = residual.ScaleOffset * h00 +
                residual.ScaleVelocity * velocityTimeWeight;
            float3 rotationLogResidual = residual.RotationLogOffset * h00 +
                residual.RotationLogVelocity * velocityTimeWeight;
            if (!IsFinite(positionResidual) || !IsFinite(scaleResidual) ||
                !IsFinite(rotationLogResidual))
                return TargetOnly(targetAtElapsed);

            // The final NormalizeShortest below fixes both length and sign of the product, so the
            // target only needs to be a usable (near-unit) quaternion here and Exp's result does
            // not need its own normalization.
            quaternion rotationResidual = Exp(rotationLogResidual);
            quaternion resultRotation = NormalizeShortest(
                math.mul(rotationResidual, SanitizeRotation(targetAtElapsed.Rotation)));
            float3 resultPosition = targetAtElapsed.Position + positionResidual;
            float3 resultScale = targetAtElapsed.Scale + scaleResidual;
            if (!IsFinite(resultPosition) || !IsFinite(resultScale))
                return TargetOnly(targetAtElapsed);
            return new HonamiInertialPose(resultPosition, resultRotation, resultScale);
        }

        private static HonamiInertialPose TargetOnly(HonamiInertialPose target) =>
            new HonamiInertialPose(target.Position, NormalizeShortest(target.Rotation), target.Scale);

        // Returns an approximately unit-length (not sign-canonical) rotation. Rotations that already
        // satisfy the documented near-unit contract are passed through untouched; anything else
        // (zero, NaN, infinite, badly scaled) is renormalized, and degenerates to identity when it
        // cannot be (length squared overflows/underflows float, where normalizesafe alone would
        // return a zero quaternion and poison every product built from it).
        private static quaternion SanitizeRotation(quaternion value)
        {
            float lengthSquared = math.dot(value.value, value.value);
            if (lengthSquared > 0.25f && lengthSquared < 4f)
                return value;
            quaternion normalized = math.normalizesafe(value, quaternion.identity);
            return math.dot(normalized.value, normalized.value) > 0.5f ? normalized : quaternion.identity;
        }

        // Unit result up to rounding; the sign is left to the caller (NormalizeShortest).
        private static quaternion Exp(float3 rotationVector)
        {
            float angleSquared = math.lengthsq(rotationVector);
            // A non-finite residual angle has no meaningful rotation; identity keeps the target (the
            // former normalizesafe of an overflowed (huge, 1) quaternion produced a zero quaternion).
            if (!IsFinite(angleSquared))
                return quaternion.identity;
            if (angleSquared < 1e-12f)
                // Here (v/2, 1) has |q|^2 - 1 = angleSquared / 4 < 2.5e-13, below float resolution,
                // so no renormalization is needed.
                return new quaternion(rotationVector.x * 0.5f, rotationVector.y * 0.5f,
                    rotationVector.z * 0.5f, 1f);

            float angle = math.sqrt(angleSquared);
            math.sincos(angle * 0.5f, out float sine, out float cosine);
            float scale = sine / angle;
            return new quaternion(rotationVector.x * scale, rotationVector.y * scale,
                rotationVector.z * scale, cosine);
        }

        // q need not be unit length: angle and axis only depend on atan2(|v|, w) and v / |v|.
        private static float3 LogInHemisphere(quaternion q)
        {
            float3 vector = new float3(q.value.x, q.value.y, q.value.z);
            float vectorLengthSquared = math.lengthsq(vector);
            float w = q.value.w;
            if (!IsFinite(vectorLengthSquared) || !IsFinite(w))
                return float3.zero;
            // Threshold is relative to w so it matches the former absolute 1e-14 at unit length.
            if (vectorLengthSquared < 1e-14f * (w * w))
                return vector * (2f / math.abs(w));

            float vectorLength = math.sqrt(vectorLengthSquared);
            // Do not canonicalize q.w here. A previous relative rotation is hemisphere-aligned
            // to the current one before logging, so w < 0 intentionally represents the continuous
            // branch just beyond pi instead of jumping to the opposite shortest axis.
            float angle = 2f * math.atan2(vectorLength, w);
            return FiniteOrZero(vector * (angle / vectorLength));
        }

        private static quaternion NormalizeShortest(quaternion value) =>
            CanonicalizeSign(math.normalizesafe(value, quaternion.identity));

        private static quaternion CanonicalizeSign(quaternion normalized)
        {
            float w = normalized.value.w;
            bool negate = w < 0f || (w == 0f && (normalized.value.x < 0f ||
                (normalized.value.x == 0f && (normalized.value.y < 0f ||
                 (normalized.value.y == 0f && normalized.value.z < 0f)))));
            if (negate)
                normalized.value = -normalized.value;
            return normalized;
        }

        private static float3 FiniteOrZero(float3 value) => IsFinite(value) ? value : float3.zero;
        private static bool IsFinite(float value) => !math.isnan(value) && !math.isinf(value);
        private static bool IsFinite(float3 value) => math.all(math.isfinite(value));
    }
}
