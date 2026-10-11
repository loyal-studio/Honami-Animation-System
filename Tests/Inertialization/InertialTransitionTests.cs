using System;
using System.Collections;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Tests.BlendTree2D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace HonamiAnimationSystem.Tests.Inertialization
{
    /// <summary>
    /// Inertial transitions in the Playables runtime. State A moves Hip.x at +6 units/s (0.1 per 60 Hz frame);
    /// B and C are held poses unless a test says otherwise.
    /// </summary>
    public class InertialTransitionTests : HonamiPlayModeTestBase
    {
        private const float Speed = 6f;
        private const float StepPerFrame = Speed * Dt;
        private const float ClipLength = 10f;

        private struct Sample
        {
            public float X, ArmDeg, TailX;
            public int State;
            public HonamiInertializerPhase Phase;
        }

        private sealed class Fixture
        {
            public Rig Rig;
            public HonamiController Controller;
            public HonamiState A, B, C;
        }

        // ---------------------------------------------------------------- authoring helpers

        /// <summary>Hip.x linear from x0 to x1 over len, Arm rotated about Z from armDeg0 to armDeg1, optional constant Tail.x.</summary>
        private AnimationClip LinearClip(string name, float len, float x0, float x1, float armDeg0 = 0f, float armDeg1 = 0f, float? tailX = null)
        {
            var clip = new AnimationClip { name = name, frameRate = 60f, wrapMode = WrapMode.Loop };
            clip.SetCurve("Hip", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, x0, len, x1));
            clip.SetCurve("Hip", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, 0, len, 0));
            clip.SetCurve("Hip", typeof(Transform), "localPosition.z", AnimationCurve.Linear(0, 0, len, 0));
            float h0 = armDeg0 * Mathf.Deg2Rad * 0.5f, h1 = armDeg1 * Mathf.Deg2Rad * 0.5f;
            clip.SetCurve("Hip/Arm", typeof(Transform), "localRotation.x", AnimationCurve.Linear(0, 0, len, 0));
            clip.SetCurve("Hip/Arm", typeof(Transform), "localRotation.y", AnimationCurve.Linear(0, 0, len, 0));
            clip.SetCurve("Hip/Arm", typeof(Transform), "localRotation.z", AnimationCurve.Linear(0, Mathf.Sin(h0), len, Mathf.Sin(h1)));
            clip.SetCurve("Hip/Arm", typeof(Transform), "localRotation.w", AnimationCurve.Linear(0, Mathf.Cos(h0), len, Mathf.Cos(h1)));
            if (tailX.HasValue)
                clip.SetCurve("Tail", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, tailX.Value, len, tailX.Value));
            Garbage.Add(clip);
            return clip;
        }

        private HonamiState AddClipState(HonamiController c, string name, AnimationClip clip, int layer = 0, bool isDefault = false)
            => AddState(c, name, ClipNode(clip), layer, isDefault);

        private static HonamiTransition Edge(HonamiState to, string param, HonamiTransitionType type, float duration,
            bool canInterrupt = false, bool canSelf = false)
        {
            return new HonamiTransition
            {
                targetStateGuid = to.guid,
                type = type,
                duration = duration,
                canInterruptTransition = canInterrupt,
                canTransitionToSelf = canSelf,
                conditions = new List<HonamiCondition> { new HonamiCondition { parameter = param, mode = HonamiConditionMode.If } }
            };
        }

        /// <summary>A (default, moving) -> B (held at bX) on "Go"; optional B -> C (held at 7) on "Back".</summary>
        private Fixture Build(HonamiTransitionType type, float duration, float bX = 5f, bool withBack = false,
            HonamiTransitionType backType = HonamiTransitionType.Inertial, float backDuration = 0.3f, bool backInterrupt = false,
            float armA0 = 0f, float armA1 = 0f, float armB = 0f, float bSlope = 0f)
        {
            var f = new Fixture { Rig = NewRig() };
            var c = NewController();
            AddBool(c, "Go");
            AddBool(c, "Back");
            f.A = AddClipState(c, "A", LinearClip("A", ClipLength, 0f, Speed * ClipLength, armA0, armA1), 0, true);
            f.B = AddClipState(c, "B", LinearClip("B", ClipLength, bX, bX + bSlope * ClipLength, armB, armB));
            f.A.transitions.Add(Edge(f.B, "Go", type, duration));
            if (withBack)
            {
                f.C = AddClipState(c, "C", LinearClip("C", ClipLength, 7f, 7f));
                f.B.transitions.Add(Edge(f.C, "Back", backType, backDuration, backInterrupt));
            }
            f.Controller = c;
            f.Rig.Begin(c);
            return f;
        }

        private static Sample Read(Rig rig)
        {
            var inert = rig.Anim._inertializer;
            return new Sample
            {
                X = rig.Hip.localPosition.x,
                ArmDeg = Quaternion.Angle(Quaternion.identity, rig.Arm.localRotation),
                TailX = rig.Tail.localPosition.x,
                State = rig.Anim._layerStates[0].CurrentStateIndex,
                Phase = inert != null && inert.IsCreated ? inert.State.Phase : HonamiInertializerPhase.Idle
            };
        }

        /// <summary>Samples once per rendered frame; <paramref name="before"/> runs ahead of the frame's Update.</summary>
        private static IEnumerator Record(Rig rig, int frames, List<Sample> list, Action<int> before = null)
        {
            for (int i = 0; i < frames; i++)
            {
                before?.Invoke(i);
                yield return null;
                list.Add(Read(rig));
            }
        }

        private static int FirstIndex(List<Sample> s, Func<Sample, bool> pred)
        {
            for (int i = 0; i < s.Count; i++) if (pred(s[i])) return i;
            return -1;
        }

        private static float MaxSecondDiff(List<Sample> s, int from, int to)
        {
            float m = 0f;
            for (int i = Mathf.Max(from, 1); i < Mathf.Min(to, s.Count - 1); i++)
                m = Mathf.Max(m, Mathf.Abs(s[i + 1].X - 2f * s[i].X + s[i - 1].X));
            return m;
        }

        private static float MaxStep(List<Sample> s, int from, int to)
        {
            float m = 0f;
            for (int i = Mathf.Max(from, 1); i < Mathf.Min(to, s.Count); i++)
                m = Mathf.Max(m, Mathf.Abs(s[i].X - s[i - 1].X));
            return m;
        }

        // ---------------------------------------------------------------- tests

        [UnityTest]
        public IEnumerator Inertial_NoVelocityPopAtTheSwitch()
        {
            var f = Build(HonamiTransitionType.Inertial, 0.3f);
            var s = new List<Sample>();
            yield return Record(f.Rig, 90, s, i => { if (i == 30) f.Rig.Anim.SetBool("Go", true); });

            int c = FirstIndex(s, x => x.Phase == HonamiInertializerPhase.CaptureVelocity);
            Assert.GreaterOrEqual(c, 3, "the inertializer must have run its capture frame");
            // Frame C continues the source velocity: it does not jump to the held target at x = 5.
            Assert.AreEqual(StepPerFrame, s[c].X - s[c - 1].X, 2e-3f, "first step after the switch");
            Assert.AreEqual(s[c - 1].X - s[c - 2].X, s[c].X - s[c - 1].X, 2e-3f, "velocity is continuous across the switch");
            Assert.Less(MaxSecondDiff(s, 5, s.Count), 0.07f, "no velocity pop anywhere in the decay");
        }

        [UnityTest]
        public IEnumerator Standard_PopsVelocity_ForContrast()
        {
            var f = Build(HonamiTransitionType.Standard, 0.3f);
            var inert = Build(HonamiTransitionType.Inertial, 0.3f);
            var a = new List<Sample>();
            var b = new List<Sample>();
            // Both rigs advance in lockstep so they see the same source motion.
            for (int i = 0; i < 60; i++)
            {
                if (i == 30)
                {
                    f.Rig.Anim.SetBool("Go", true);
                    inert.Rig.Anim.SetBool("Go", true);
                }
                yield return null;
                a.Add(Read(f.Rig));
                b.Add(Read(inert.Rig));
            }
            // A crossfade is C0 but its velocity jumps by (target - source) / duration at the start.
            int sa = FirstIndex(a, x => x.State == Internals.StateIndex(f.Rig.Anim, "B"));
            int sb = FirstIndex(b, x => x.State == Internals.StateIndex(inert.Rig.Anim, "B"));
            Assert.Greater(MaxSecondDiff(a, sa - 2, sa + 3), 0.07f, "crossfade velocity jumps at the switch");
            Assert.Less(MaxSecondDiff(b, sb - 2, sb + 3), 0.03f, "inertial velocity does not");
        }

        [UnityTest]
        public IEnumerator Inertial_RotationIsContinuous()
        {
            var f = Build(HonamiTransitionType.Inertial, 0.3f, bX: 5f, armA0: 0f, armA1: 80f, armB: 90f);
            var s = new List<Sample>();
            yield return Record(f.Rig, 90, s, i => { if (i == 30) f.Rig.Anim.SetBool("Go", true); });

            int c = FirstIndex(s, x => x.Phase == HonamiInertializerPhase.CaptureVelocity);
            Assert.GreaterOrEqual(c, 3);
            float prevStep = s[c - 1].ArmDeg - s[c - 2].ArmDeg;
            float step = s[c].ArmDeg - s[c - 1].ArmDeg;
            Assert.Greater(prevStep, 0.1f, "the source arm is moving");
            Assert.AreEqual(prevStep, step, 0.05f, "arm angular velocity carries over the switch");
        }

        [UnityTest]
        public IEnumerator Inertial_ConvergesExactlyToTheTarget()
        {
            var f = Build(HonamiTransitionType.Inertial, 0.3f, armB: 90f);
            var s = new List<Sample>();
            yield return Record(f.Rig, 20, s);
            f.Rig.Anim.SetBool("Go", true);
            yield return Record(f.Rig, Mathf.CeilToInt(0.3f / Dt) + 6, s);

            var last = s[s.Count - 1];
            Assert.AreEqual(5f, last.X, 1e-4f);
            Assert.AreEqual(90f, last.ArmDeg, 1e-2f);
            Assert.IsFalse(f.Rig.Anim._inertializer.IsActive);
            Assert.AreEqual(0.0, f.Rig.Anim._layerStates[0].InertialDuration);
            Assert.AreEqual(HonamiInertializerPhase.Idle, last.Phase);
        }

        [UnityTest]
        public IEnumerator Interrupt_AllowedEdgeRebasesContinuously_BlockedEdgeWaits()
        {
            // Interruptible: B -> C fires mid-decay and the pose stays continuous all the way to C.
            var f = Build(HonamiTransitionType.Inertial, 0.5f, withBack: true, backInterrupt: true, backDuration: 0.4f);
            var s = new List<Sample>();
            yield return Record(f.Rig, 90, s, i =>
            {
                if (i == 20) f.Rig.Anim.SetBool("Go", true);
                if (i == 32) f.Rig.Anim.SetBool("Back", true);
            });
            int cIdx = Internals.StateIndex(f.Rig.Anim, "C");
            Assert.AreEqual(cIdx, s[s.Count - 1].State, "interruptible edge fired");
            Assert.AreEqual(7f, s[s.Count - 1].X, 1e-4f);
            Assert.Less(MaxSecondDiff(s, 5, s.Count), 0.1f, "rebasing keeps the motion continuous");
            Assert.Less(MaxStep(s, 5, s.Count), StepPerFrame * 2.5f);
            int firstC = FirstIndex(s, x => x.State == cIdx);
            int firstB = FirstIndex(s, x => x.State == Internals.StateIndex(f.Rig.Anim, "B"));
            Assert.Less(firstC - firstB, Mathf.CeilToInt(0.5f / Dt), "fired before the first decay would have ended");

            // Not interruptible: B -> C waits for the decay to finish.
            var g = Build(HonamiTransitionType.Inertial, 0.5f, withBack: true, backInterrupt: false, backDuration: 0.4f);
            var t = new List<Sample>();
            yield return Record(g.Rig, 120, t, i =>
            {
                if (i == 20) g.Rig.Anim.SetBool("Go", true);
                if (i == 32) g.Rig.Anim.SetBool("Back", true);
            });
            int gB = Internals.StateIndex(g.Rig.Anim, "B");
            int gC = Internals.StateIndex(g.Rig.Anim, "C");
            int bStart = FirstIndex(t, x => x.State == gB);
            int cStart = FirstIndex(t, x => x.State == gC);
            Assert.GreaterOrEqual(cStart - bStart, Mathf.FloorToInt(0.5f / Dt) - 1, "blocked until the residual has decayed");
            Assert.AreEqual(7f, t[t.Count - 1].X, 1e-4f);
        }

        [UnityTest]
        public IEnumerator StandardAfterInertial_MidDecay_IsContinuous()
        {
            var f = Build(HonamiTransitionType.Inertial, 0.5f, withBack: true, backType: HonamiTransitionType.Standard,
                backDuration: 0.2f, backInterrupt: true);
            var s = new List<Sample>();
            yield return Record(f.Rig, 110, s, i =>
            {
                if (i == 20) f.Rig.Anim.SetBool("Go", true);
                if (i == 32) f.Rig.Anim.SetBool("Back", true);
            });
            Assert.AreEqual(Internals.StateIndex(f.Rig.Anim, "C"), s[s.Count - 1].State);
            Assert.AreEqual(7f, s[s.Count - 1].X, 1e-4f);
            // The crossfade alone moves 2 / 12 frames, the residual adds up to ~0.12 per frame; a pop would be far larger.
            Assert.Less(MaxStep(s, 5, s.Count), 0.4f, "no position pop when a crossfade starts on top of the residual");
        }

        [UnityTest]
        public IEnumerator ZeroDuration_CutsInstantlyAndLeavesNoResidual()
        {
            // The second Inertial edge keeps the inertializer built, so the zero-duration edge goes through it.
            var f = Build(HonamiTransitionType.Inertial, 0f, withBack: true);
            Assert.IsNotNull(f.Rig.Anim._inertializer, "controller has a positive-duration Inertial edge");
            var s = new List<Sample>();
            yield return Record(f.Rig, 60, s, i => { if (i == 30) f.Rig.Anim.SetBool("Go", true); });

            int b = FirstIndex(s, x => x.State == Internals.StateIndex(f.Rig.Anim, "B"));
            Assert.GreaterOrEqual(b, 0);
            Assert.AreEqual(HonamiInertializerPhase.Idle, s[b].Phase, "no residual starts on the switch frame");
            int atTarget = FirstIndex(s, x => Mathf.Abs(x.X - 5f) < 1e-4f);
            Assert.GreaterOrEqual(atTarget, 0);
            Assert.LessOrEqual(atTarget - b, 1, "cut straight to the target");
            Assert.AreEqual(HonamiInertializerPhase.Idle, s[s.Count - 1].Phase);
            Assert.IsFalse(f.Rig.Anim._inertializer.IsActive);
        }

        [UnityTest]
        public IEnumerator PausedTimeline_HoldsThePoseAndResumesContinuously()
        {
            var f = Build(HonamiTransitionType.Inertial, 0.3f);
            var s = new List<Sample>();
            yield return Record(f.Rig, 40, s);
            float held = s[s.Count - 1].X;

            f.Rig.Anim.TimeScale = 0f;
            f.Rig.Anim.SetBool("Go", true);
            var paused = new List<Sample>();
            yield return Record(f.Rig, 12, paused);
            foreach (var p in paused)
                Assert.AreEqual(held, p.X, 1e-4f, "pose is held while time does not advance");
            Assert.AreEqual(HonamiInertializerPhase.CaptureOffset, paused[paused.Count - 1].Phase);

            f.Rig.Anim.TimeScale = 1f;
            var resumed = new List<Sample>();
            yield return Record(f.Rig, 60, resumed);
            var all = new List<Sample>(s);
            all.AddRange(resumed);
            // The motion picks up with the source velocity instead of snapping to the target.
            Assert.Less(MaxStep(all, 5, all.Count), StepPerFrame * 2.5f);
            Assert.AreEqual(5f, all[all.Count - 1].X, 1e-4f);
        }

        /// <summary>Base layer moves Hip and Tail; a half-weight top layer switches only its Arm pose with an Inertial edge.</summary>
        [UnityTest]
        public IEnumerator MultiLayer_BonesOfOtherLayerAreLeftAlone()
        {
            var rig = NewRig();
            var c = NewController();
            AddLayer(c, "Top");
            c.layers[1].weight = 0.5f;
            AddBool(c, "Go");
            AddClipState(c, "Base", LinearClip("Base", ClipLength, 0f, 60f, 0f, 0f, tailX: 3f), 0, true);
            var a = AddClipState(c, "TopA", LinearClip("TopA", ClipLength, 1f, 1f, 0f, 0f, tailX: 3f), 1, true);
            var b = AddClipState(c, "TopB", LinearClip("TopB", ClipLength, 1f, 1f, 70f, 70f, tailX: 3f), 1);
            a.transitions.Add(Edge(b, "Go", HonamiTransitionType.Inertial, 0.3f));
            rig.Begin(c);

            var s = new List<Sample>();
            yield return Record(rig, 90, s, i => { if (i == 30) rig.Anim.SetBool("Go", true); });

            Assert.GreaterOrEqual(FirstIndex(s, x => x.Phase == HonamiInertializerPhase.CaptureVelocity), 0, "the inertial switch ran");
            // Hip moves with the base layer (and stays linear through the switch); Tail is identical in both top states.
            Assert.Less(MaxSecondDiff(s, 5, s.Count), 1e-3f, "Hip keeps its linear motion across the top layer's switch");
            float minTail = float.MaxValue, maxTail = float.MinValue;
            for (int i = 5; i < s.Count; i++)
            {
                minTail = Mathf.Min(minTail, s[i].TailX); maxTail = Mathf.Max(maxTail, s[i].TailX);
            }
            Assert.Less(maxTail - minTail, 1e-3f, "Tail does not move");
            Assert.Greater(s[s.Count - 1].ArmDeg, 10f, "Arm did blend towards the new top state");
        }

        [UnityTest]
        public IEnumerator CutOnAnotherLayer_DoesNotCancelTheResidual()
        {
            var rig = NewRig();
            var c = NewController();
            AddLayer(c, "Top");
            c.layers[1].weight = 0f;
            AddBool(c, "Go");
            AddBool(c, "Cut");
            var a = AddClipState(c, "A", LinearClip("A", ClipLength, 0f, Speed * ClipLength), 0, true);
            var b = AddClipState(c, "B", LinearClip("B", ClipLength, 5f, 5f));
            a.transitions.Add(Edge(b, "Go", HonamiTransitionType.Inertial, 0.5f));
            var ta = AddClipState(c, "TopA", LinearClip("TopA", ClipLength, 0f, 0f), 1, true);
            var tb = AddClipState(c, "TopB", LinearClip("TopB", ClipLength, 0f, 0f), 1);
            ta.transitions.Add(Edge(tb, "Cut", HonamiTransitionType.Standard, 0f));
            rig.Begin(c);

            var s = new List<Sample>();
            yield return Record(rig, 90, s, i =>
            {
                if (i == 20) rig.Anim.SetBool("Go", true);
                if (i == 30) rig.Anim.SetBool("Cut", true);
            });

            Assert.AreEqual(Internals.StateIndex(rig.Anim, "TopB"), rig.Anim._layerStates[1].CurrentStateIndex, "the cut happened");
            Assert.Less(MaxStep(s, 5, s.Count), StepPerFrame * 2.5f, "layer 0 stays continuous through the other layer's cut");
            Assert.AreEqual(5f, s[s.Count - 1].X, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Gate_ClearsByTimer_WhenTheJobIsNeverEvaluatedToTheEnd()
        {
            var f = Build(HonamiTransitionType.Inertial, 0.3f, withBack: true, backType: HonamiTransitionType.Standard, backInterrupt: false, backDuration: 0.2f);
            yield return Frames(20);
            f.Rig.Anim.SetBool("Go", true);
            yield return Frames(2);
            // Simulate a stalled job (culled / stopped animator): its own duration is far longer than the layer gate.
            f.Rig.Anim._inertializer.Begin(1000f);
            f.Rig.Anim.SetBool("Back", true);

            yield return Frames(Mathf.CeilToInt(0.3f / Dt) + 8);
            Assert.AreEqual(Internals.StateIndex(f.Rig.Anim, "C"), f.Rig.Anim._layerStates[0].CurrentStateIndex,
                "the non-interruptible edge fired once the timer released the gate");
            Assert.IsFalse(f.Rig.Anim._inertializer.IsActive, "the stalled residual was cancelled");
        }

        [UnityTest]
        public IEnumerator HeldSelfEdge_DoesNotDriftAway()
        {
            var rig = NewRig();
            var c = NewController();
            AddBool(c, "Go");
            var a = AddClipState(c, "A", LinearClip("A", ClipLength, 0f, Speed * ClipLength), 0, true);
            a.transitions.Add(Edge(a, "Go", HonamiTransitionType.Inertial, 0.3f, canSelf: true));
            rig.Begin(c);

            var s = new List<Sample>();
            yield return Record(rig, 20, s);
            rig.Anim.SetBool("Go", true); // stays true: the edge fires every tick
            yield return Record(rig, 90, s);

            float max = float.MinValue;
            for (int i = 20; i < s.Count; i++) max = Mathf.Max(max, s[i].X);
            // Unchecked, the extrapolation would run away at 6 units/s (9+ after 1.5 s).
            Assert.Less(max, 2.5f, "pose stays bounded while the edge keeps re-triggering");
            Assert.Less(Mathf.Abs(s[s.Count - 1].X - s[s.Count - 10].X), 0.5f);
        }

        [UnityTest]
        public IEnumerator SelfTransition_RestartsContinuously()
        {
            var rig = NewRig();
            var c = NewController();
            c.parameters.Add(new HonamiParameter { name = "Go", type = HonamiParameterType.Trigger });
            var a = AddClipState(c, "A", LinearClip("A", ClipLength, 0f, Speed * ClipLength), 0, true);
            a.transitions.Add(Edge(a, "Go", HonamiTransitionType.Inertial, 0.3f, canSelf: true));
            rig.Begin(c);

            var s = new List<Sample>();
            yield return Record(rig, 90, s, i => { if (i == 30) rig.Anim.SetTrigger("Go"); });

            Assert.GreaterOrEqual(FirstIndex(s, x => x.Phase == HonamiInertializerPhase.CaptureVelocity), 0, "the self transition ran");
            // The clip restarted at x = 0 while the pose was near x = 3; the residual hides that jump.
            Assert.Less(MaxStep(s, 5, s.Count), StepPerFrame * 2.5f);
            Assert.Less(s[s.Count - 1].X, 7.5f, "A really did restart (an unrestarted clip would be at 9)");
        }

        [UnityTest]
        public IEnumerator GraphWithoutInertialEdges_HasNoInertializer()
        {
            var plain = Build(HonamiTransitionType.Standard, 0.3f);
            yield return Frames(3);
            Assert.IsNull(plain.Rig.Anim._inertializer);
            Assert.AreEqual(typeof(AnimationLayerMixerPlayable), plain.Rig.Anim._playableOutput.GetSourcePlayable().GetPlayableType(),
                "the output chain is the unchanged layer mixer");

            var inert = Build(HonamiTransitionType.Inertial, 0.3f);
            yield return Frames(3);
            Assert.IsNotNull(inert.Rig.Anim._inertializer);
            Assert.AreEqual(typeof(AnimationScriptPlayable), inert.Rig.Anim._playableOutput.GetSourcePlayable().GetPlayableType());
            Assert.AreEqual(typeof(AnimationLayerMixerPlayable),
                inert.Rig.Anim._playableOutput.GetSourcePlayable().GetInput(0).GetPlayableType());
        }

        [UnityTest]
        public IEnumerator ExitTarget_FadesOutWithoutSnapping()
        {
            var rig = NewRig();
            var c = NewController();
            AddBool(c, "Go");
            var a = AddClipState(c, "A", LinearClip("A", ClipLength, 4f, 4f), 0, true);
            var exit = AddState(c, "Exit", Make<HonamiExitNode>());
            a.transitions.Add(Edge(exit, "Go", HonamiTransitionType.Inertial, 0.4f));
            rig.Begin(c);

            var s = new List<Sample>();
            yield return Record(rig, 120, s, i => { if (i == 30) rig.Anim.SetBool("Go", true); });

            float before = s[25].X;
            Assert.AreEqual(4f, before, 1e-3f);
            // No frame may jump straight to the initial pose: the offset decays over the 0.4 s.
            Assert.Less(MaxStep(s, 5, s.Count), 0.5f, "no snap to the initial pose");
            int switched = FirstIndex(s, x => x.Phase != HonamiInertializerPhase.Idle);
            Assert.GreaterOrEqual(switched, 0);
            Assert.Greater(s[switched + 3].X, 0.5f, "still decaying a few frames into the transition");
            Assert.AreEqual(0f, s[s.Count - 1].X, 1e-3f, "ends at the initial pose");
        }

        [UnityTest]
        public IEnumerator Lifecycle_ControllerSwapAndDestroyDuringDecay_DoNotThrowOrLeak()
        {
            // Controller swap with a transition duration while the residual is still running.
            var f = Build(HonamiTransitionType.Inertial, 0.6f);
            var s = new List<Sample>();
            yield return Record(f.Rig, 30, s, i => { if (i == 10) f.Rig.Anim.SetBool("Go", true); });
            Assert.IsTrue(f.Rig.Anim._inertializer.IsActive);
            var firstInertializer = f.Rig.Anim._inertializer;

            var second = NewController();
            AddBool(second, "Go");
            var sa = AddClipState(second, "SA", LinearClip("SA", ClipLength, 0f, 0f), 0, true);
            var sb = AddClipState(second, "SB", LinearClip("SB", ClipLength, 2f, 2f));
            sa.transitions.Add(Edge(sb, "Go", HonamiTransitionType.Inertial, 0.3f));
            f.Rig.Anim.SetController(second, 0.2f);
            Assert.IsNotNull(f.Rig.Anim._oldInertializer, "old inertializer lives on until the controller blend ends");
            yield return Frames(30);
            Assert.IsNull(f.Rig.Anim._oldInertializer);
            Assert.IsFalse(firstInertializer.IsCreated, "old native arrays are disposed");
            Assert.IsNotNull(f.Rig.Anim._inertializer);

            // Destroy while a residual is active.
            f.Rig.Anim.SetBool("Go", true);
            yield return Frames(6);
            Assert.IsTrue(f.Rig.Anim._inertializer.IsActive);
            var live = f.Rig.Anim._inertializer;
            UnityEngine.Object.DestroyImmediate(f.Rig.Go);
            Assert.IsFalse(live.IsCreated, "destroying the animator disposes its native arrays");
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator StreamDeltaTime_MatchesTheEvaluateDelta()
        {
            var f = Build(HonamiTransitionType.Inertial, 0.3f);
            yield return Frames(10);
            var st = f.Rig.Anim._inertializer.State;
            Assert.AreEqual(Dt, st.HistoryDelta, 1e-4f);
            Assert.AreEqual(2, st.HistoryDepth);
        }
    }
}
