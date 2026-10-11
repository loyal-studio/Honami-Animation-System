using System;
using System.Collections;
using HonamiAnimationSystem.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HonamiAnimationSystem.Tests.BlendTree2D
{
    /// <summary>
    /// Per-motion cycle offsets on FreeformDirectional2D trees: a child samples its clip at
    /// frac(phase + offset) * clipLength, while weights, period and the shared phase are unchanged.
    /// </summary>
    public class CycleOffsetTests : HonamiPlayModeTestBase
    {
        private const string RangeMessage = "finite and within [0, 1)";

        private static float[] Lengths() => (float[])Dir.Len.Clone();
        private static float[] Speeds() => new[] { 1f, 1f, 1f, 1f, 1f };

        private static double Frac(double v) => v - Math.Floor(v);

        /// <summary>Shortest distance between two values on the unit circle [0, 1).</summary>
        private static float CircularDistance(float a, float b)
        {
            float d = Mathf.Repeat(a - b, 1f);
            return Mathf.Min(d, 1f - d);
        }

        // ---------------------------------------------------------------- solver validation
        [Test]
        public void Solver_DefaultConstructor_HasZeroOffsets()
        {
            var solver = new HonamiDirectionalBlendSolver(Dir.Pos, Lengths(), Speeds());
            Assert.AreEqual(5, solver.CycleOffsets.Length);
            for (int i = 0; i < solver.MotionCount; i++)
                Assert.AreEqual(0f, solver.GetCycleOffset(i));

            var nullOffsets = new HonamiDirectionalBlendSolver(Dir.Pos, Lengths(), Speeds(), null);
            for (int i = 0; i < nullOffsets.MotionCount; i++)
                Assert.AreEqual(0f, nullOffsets.GetCycleOffset(i));
        }

        [Test]
        public void Solver_CopiesOffsets()
        {
            var offsets = new[] { 0f, 0.25f, 0.5f, 0.75f, 0.9999f };
            var solver = new HonamiDirectionalBlendSolver(Dir.Pos, Lengths(), Speeds(), offsets);
            offsets[1] = 0.1f;
            Assert.AreEqual(0.25f, solver.GetCycleOffset(1), "the solver must copy its input");
            Assert.AreEqual(0.9999f, solver.GetCycleOffset(4));
        }

        [TestCase(1f)]
        [TestCase(-0.25f)]
        [TestCase(1.5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Solver_RejectsInvalidOffset(float invalid)
        {
            var offsets = new[] { 0f, 0f, invalid, 0f, 0f };
            var exception = Assert.Throws<ArgumentException>(
                () => new HonamiDirectionalBlendSolver(Dir.Pos, Lengths(), Speeds(), offsets));
            StringAssert.Contains(RangeMessage, exception.Message);
            StringAssert.Contains("motion 2", exception.Message);
            Assert.IsFalse(HonamiDirectionalBlendSolver.IsValidCycleOffset(invalid));
        }

        [Test]
        public void Solver_RejectsWrongOffsetCount()
        {
            Assert.Throws<ArgumentException>(
                () => new HonamiDirectionalBlendSolver(Dir.Pos, Lengths(), Speeds(), new[] { 0f, 0f }));
        }

        [TestCase(1.25f, 0.25f)]
        [TestCase(-0.25f, 0.75f)]
        [TestCase(1f, 0f)]
        [TestCase(3f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        public void Node_OutOfRangeOffset_WrapsCyclically(float authored, float expected)
        {
            // The solver API stays strict; the node maps authored values onto the cycle like Unity.
            Assert.AreEqual(expected, HonamiBlendTreeNode.WrapCycleOffset(authored), 0f);

            var node = Tree2D("X", "Y", 0f, 0f);
            node.blendMotions[Dir.East].cycleOffset = authored;
            Assert.IsTrue(node.TryGetDirectionalSolver(out var solver, out string error), error);
            Assert.AreEqual(expected, solver.GetCycleOffset(Dir.East), 0f);
            Assert.IsTrue(node.TryGetDirectionalSolver(out var cached, out error), error);
            Assert.AreSame(solver, cached, "a wrapped offset must not defeat the solver cache");
        }

        [Test]
        public void Node_EditingOffset_RebuildsSolver()
        {
            var node = Tree2D("X", "Y", 0f, 0f);
            Assert.IsTrue(node.TryGetDirectionalSolver(out var first, out string error), error);
            Assert.AreEqual(0f, first.GetCycleOffset(Dir.North));
            Assert.IsTrue(node.TryGetDirectionalSolver(out var cached, out error), error);
            Assert.AreSame(first, cached, "an unchanged topology reuses the cached solver");

            node.blendMotions[Dir.North].cycleOffset = 0.4f;
            Assert.IsTrue(node.TryGetDirectionalSolver(out var rebuilt, out error), error);
            Assert.AreNotSame(first, rebuilt, "a changed cycle offset must rebuild the solver");
            Assert.AreEqual(0.4f, rebuilt.GetCycleOffset(Dir.North));
        }

        // ---------------------------------------------------------------- port child time
        private static HonamiBlend2DPortRuntime StartedPort(float[] offsets, Vector2 input, double startSeconds, int steps)
        {
            var solver = new HonamiDirectionalBlendSolver(Dir.Pos, Lengths(), Speeds(), offsets);
            var port = new HonamiBlend2DPortRuntime(solver);
            Assert.IsTrue(port.Initialize(input, startSeconds, out string error), error);
            for (int i = 0; i < steps; i++)
                Assert.IsTrue(port.Advance(input, 1d / 60d, 1f, 0f, 0f, out error), error);
            return port;
        }

        [Test]
        public void Port_ZeroOffset_ChildTimeIsExactlyPhaseTimesLength()
        {
            var input = new Vector2(0.6f, 0.3f);
            var implicitZero = StartedPort(null, input, 0.37, 47);
            var explicitZero = StartedPort(new float[5], input, 0.37, 47);

            Assert.AreEqual(implicitZero.Phase, explicitZero.Phase);
            for (int i = 0; i < 5; i++)
            {
                double expected = implicitZero.Phase * Dir.Len[i];
                Assert.AreEqual(expected, implicitZero.GetChildTime(i), 0d, $"motion {i} without offsets");
                Assert.AreEqual(expected, explicitZero.GetChildTime(i), 0d, $"motion {i} with explicit zero offsets");
                Assert.AreEqual(implicitZero.GetWeight(i), explicitZero.GetWeight(i), 0f);
            }
        }

        [Test]
        public void Port_Offset_ShiftsChildTimeOnly()
        {
            var offsets = new[] { 0.8f, 0.25f, 0.5f, 0.0f, 0.9999f };
            var input = new Vector2(0.6f, -0.3f);
            var basePort = StartedPort(null, input, 0.37, 61);
            var offsetPort = StartedPort(offsets, input, 0.37, 61);

            Assert.AreEqual(basePort.Phase, offsetPort.Phase, 0d, "offsets must not change the shared phase");
            Assert.AreEqual(basePort.CyclePosition, offsetPort.CyclePosition, 0d);
            Assert.AreEqual(basePort.Period, offsetPort.Period, 0f, "offsets must not change the period");
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(basePort.GetWeight(i), offsetPort.GetWeight(i), 0f, "offsets must not change weights");
                double expected = Frac(offsetPort.Phase + offsets[i]) * Dir.Len[i];
                double actual = offsetPort.GetChildTime(i);
                Assert.AreEqual(expected, actual, 1e-9, $"motion {i}: frac(phase + offset) * length");
                Assert.That(actual, Is.GreaterThanOrEqualTo(0d).And.LessThan(Dir.Len[i]));
            }
        }

        // ---------------------------------------------------------------- Playables evaluation
        /// <summary>
        /// Creates the rig before its clips, node and controller so TearDown destroys the animator
        /// (and lets it reset its states) before the assets it references.
        /// </summary>
        private Rig StartRig(float[] offsets)
        {
            var rig = NewRig();
            var tree = Tree2D("X", "Y", 0f, 0f);
            if (offsets != null)
                for (int i = 0; i < offsets.Length; i++) tree.blendMotions[i].cycleOffset = offsets[i];
            var c = NewController(("X", 0f), ("Y", 0f));
            AddState(c, "Move", tree, 0, true);
            rig.Begin(c);
            return rig;
        }

        [UnityTest]
        public IEnumerator Playables_ZeroOffsets_MatchDefaultPose()
        {
            // Hip.z is a 0..1 sawtooth over each clip, so it reads back the sampled child phase.
            var baseline = StartRig(null);
            var zeros = StartRig(new float[5]);
            yield return Sim(0.35f);

            for (int d = 0; d < 5; d++)
            {
                foreach (var rig in new[] { baseline, zeros })
                {
                    rig.Anim.SetFloat("X", Dir.Pos[d].x + 0.3f);
                    rig.Anim.SetFloat("Y", Dir.Pos[d].y - 0.2f);
                }
                yield return Sim(0.3f);
                Assert.AreEqual(baseline.Hip.localPosition, zeros.Hip.localPosition, $"pose near {Dir.Names[d]}");
                Assert.AreEqual(Internals.Port(baseline.Anim, "Move").Phase, Internals.Port(zeros.Anim, "Move").Phase, 0d);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Playables_Offset_ShiftsSampledChildPhase()
        {
            var offsets = new[] { 0.25f, 0.6f, 0f, 0.9f, 0.5f };
            var baseline = StartRig(null);
            var shifted = StartRig(offsets);
            yield return Sim(0.2f);

            var basePort = Internals.Port(baseline.Anim, "Move");
            var shiftedPort = Internals.Port(shifted.Anim, "Move");
            Assert.NotNull(basePort);
            Assert.NotNull(shiftedPort);

            for (int d = 0; d < 5; d++)
            {
                foreach (var rig in new[] { baseline, shifted })
                {
                    rig.Anim.SetFloat("X", Dir.Pos[d].x);
                    rig.Anim.SetFloat("Y", Dir.Pos[d].y);
                }
                // Sample several frames so the check covers different phases, including a wrap.
                for (int frame = 0; frame < 4; frame++)
                {
                    yield return Sim(0.27f);
                    Assert.Greater(shiftedPort.GetWeight(d), 0.99f, $"{Dir.Names[d]} should dominate");
                    Assert.AreEqual(basePort.Phase, shiftedPort.Phase, 0d, "offsets must not change the shared phase");
                    Assert.AreEqual(basePort.Period, shiftedPort.Period, 0f, "offsets must not change the period");

                    float basePhase = baseline.Hip.localPosition.z;
                    float shiftedPhase = shifted.Hip.localPosition.z;
                    float expected = Mathf.Repeat(basePhase + offsets[d], 1f);
                    Assert.Less(CircularDistance(expected, shiftedPhase), 0.02f,
                        $"{Dir.Names[d]}: sampled phase {shiftedPhase:F4}, expected frac({basePhase:F4} + {offsets[d]}) = {expected:F4}");
                    Assert.AreEqual(baseline.Hip.localPosition.x, shifted.Hip.localPosition.x, 1e-4f, "static channels are unchanged");
                    Assert.AreEqual(baseline.Hip.localPosition.y, shifted.Hip.localPosition.y, 1e-4f, "static channels are unchanged");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
