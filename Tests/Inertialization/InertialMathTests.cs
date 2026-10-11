using HonamiAnimationSystem.Runtime.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace HonamiAnimationSystem.Tests.Inertialization
{
    /// <summary>Pure math: constant-velocity extrapolation and residual endpoints.</summary>
    public class InertialMathTests
    {
        private static HonamiInertialPose Pose(float3 p, float angleRad, float3 s)
            => new HonamiInertialPose(p, quaternion.AxisAngle(math.up(), angleRad), s);

        // atan2 form: acos loses all precision near identity, so it cannot resolve small angles.
        private static float Angle(quaternion a, quaternion b)
        {
            quaternion rel = math.mul(a, math.conjugate(b));
            return math.degrees(2f * math.atan2(math.length(rel.value.xyz), math.abs(rel.value.w)));
        }

        [Test]
        public void Extrapolate_LinearPositionAndConstantAngularVelocity_IsExact()
        {
            const float prevDt = 1f / 60f;
            float dt = 1f / 30f;
            var prev = Pose(new float3(1, 2, 3), 0.2f, new float3(1, 1, 1));
            var cur = Pose(new float3(1.5f, 2, 2.5f), 0.3f, new float3(1.1f, 1, 1));

            var result = HonamiInertialPoseMath.Extrapolate(cur, prev, prevDt, dt);

            float k = dt / prevDt;
            Assert.AreEqual(1.5f + 0.5f * k, result.Position.x, 1e-5f);
            Assert.AreEqual(2f, result.Position.y, 1e-5f);
            Assert.AreEqual(2.5f - 0.5f * k, result.Position.z, 1e-5f);
            Assert.AreEqual(1.1f + 0.1f * k, result.Scale.x, 1e-5f);
            var expected = quaternion.AxisAngle(math.up(), 0.3f + 0.1f * k);
            Assert.Less(Angle(expected, result.Rotation), 1e-3f);
        }

        [Test]
        public void Extrapolate_UnusableDelta_ReturnsCurrent()
        {
            var prev = Pose(new float3(0, 0, 0), 0f, new float3(1, 1, 1));
            var cur = Pose(new float3(1, 0, 0), 0.5f, new float3(1, 1, 1));

            foreach (var (prevDt, dt) in new[] { (0f, 0.016f), (0.016f, 0f), (float.NaN, 0.016f), (0.016f, float.PositiveInfinity), (-1f, 0.016f) })
            {
                var r = HonamiInertialPoseMath.Extrapolate(cur, prev, prevDt, dt);
                Assert.AreEqual(cur.Position.x, r.Position.x, 0f, $"prevDt={prevDt} dt={dt}");
                Assert.AreEqual(cur.Rotation.value.w, r.Rotation.value.w, 0f);
            }
        }

        [Test]
        public void Extrapolate_HitchIsClamped()
        {
            var prev = Pose(new float3(0, 0, 0), 0f, new float3(1, 1, 1));
            var cur = Pose(new float3(1, 0, 0), 0f, new float3(1, 1, 1));
            var r = HonamiInertialPoseMath.Extrapolate(cur, prev, 0.01f, 10f);
            Assert.AreEqual(3f, r.Position.x, 1e-5f, "step ratio is clamped to 2");
        }

        [Test]
        public void ApplyResidual_StartAddsOffset_EndIsExactlyTheTarget()
        {
            var source = Pose(new float3(3, 0, 0), 0.4f, new float3(1, 1, 1));
            var prevSource = Pose(new float3(2.9f, 0, 0), 0.39f, new float3(1, 1, 1));
            var target = Pose(new float3(5, 0, 0), 0.9f, new float3(1, 1, 1));
            var targetNext = Pose(new float3(5, 0, 0), 0.9f, new float3(1, 1, 1));
            var residual = HonamiInertialPoseMath.CaptureResidual(source, prevSource, 1f / 60f, target, targetNext, 1f / 60f);

            var start = HonamiInertialPoseMath.ApplyResidual(target, residual, 0f, 0.3f);
            Assert.AreEqual(source.Position.x, start.Position.x, 1e-5f);
            Assert.Less(Angle(source.Rotation, start.Rotation), 1e-3f);

            var end = HonamiInertialPoseMath.ApplyResidual(target, residual, 0.3f, 0.3f);
            Assert.AreEqual(target.Position.x, end.Position.x, 0f);
            Assert.Less(Angle(target.Rotation, end.Rotation), 1e-5f);

            var later = HonamiInertialPoseMath.ApplyResidual(target, residual, 5f, 0.3f);
            Assert.AreEqual(target.Position.x, later.Position.x, 0f);
        }
    }
}
