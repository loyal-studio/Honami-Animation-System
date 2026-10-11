using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace HonamiAnimationSystem.Tests.BlendTree2D
{
    public class Directional2DTests : HonamiPlayModeTestBase
    {
        private static float Frac(double v) => (float)(v - System.Math.Floor(v));

        // ---------------------------------------------------------------- harness sanity
        [UnityTest]
        public IEnumerator Z_Harness_FixedDeltaTime()
        {
            yield return null;
            yield return null;
            Assert.AreEqual(Dt, Time.deltaTime, 1e-4f, "Time.captureDeltaTime did not take effect; time based tests are unreliable.");
        }

        // ---------------------------------------------------------------- a. plays
        [UnityTest]
        public IEnumerator A_Plays_DirectionsDominateAndPoseFollows()
        {
            var rig = NewRig();
            var c = NewController(("X", 0f), ("Y", 0f));
            AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            rig.Begin(c);
            yield return Sim(0.2f);

            var port = Internals.Port(rig.Anim, "Move");
            Assert.NotNull(port, "no directional port was built for the 2D state");
            Assert.IsTrue(port.IsInitialized);
            Assert.AreEqual(5, port.Solver.MotionCount);
            Assert.Greater(port.GetWeight(Dir.Origin), 0.99f, "origin should dominate at (0,0)");
            Assert.AreEqual(0f, rig.Hip.localPosition.x, 0.05f);
            Assert.AreEqual(0f, rig.Hip.localPosition.y, 0.05f);

            for (int d = 1; d < 5; d++)
            {
                rig.Anim.SetFloat("X", Dir.Pos[d].x);
                rig.Anim.SetFloat("Y", Dir.Pos[d].y);
                yield return Sim(0.25f);
                Assert.Greater(port.GetWeight(d), 0.99f, $"{Dir.Names[d]} should dominate");
                for (int o = 0; o < 5; o++) if (o != d) Assert.Less(port.GetWeight(o), 0.01f, $"{Dir.Names[o]} leaks into {Dir.Names[d]}");
                Assert.AreEqual(Dir.Hip[d].x, rig.Hip.localPosition.x, 0.05f, $"hip.x for {Dir.Names[d]}");
                Assert.AreEqual(Dir.Hip[d].y, rig.Hip.localPosition.y, 0.05f, $"hip.y for {Dir.Names[d]}");
                Assert.AreEqual(d * 20f, Mathf.Repeat(rig.Arm.localEulerAngles.z, 360f), 1.5f, $"arm rotation for {Dir.Names[d]}");
            }

            // Between East and North both contribute and nothing opposite does.
            rig.Anim.SetFloat("X", 0.7f); rig.Anim.SetFloat("Y", 0.7f);
            yield return Sim(0.25f);
            Assert.Greater(port.GetWeight(Dir.East), 0.1f);
            Assert.Greater(port.GetWeight(Dir.North), 0.1f);
            Assert.Less(port.GetWeight(Dir.West) + port.GetWeight(Dir.South), 0.02f);
            float sum = 0; for (int i = 0; i < 5; i++) sum += port.GetWeight(i);
            Assert.AreEqual(1f, sum, 1e-3f, "weights are normalized");
            Assert.Greater(rig.Hip.localPosition.x, 0.2f);
            Assert.Greater(rig.Hip.localPosition.y, 0.2f);
        }

        // ---------------------------------------------------------------- b. damping
        [UnityTest]
        public IEnumerator B_Damping_InputApproachesTargetGradually()
        {
            var rig = NewRig();
            var c = NewController(("X", 0f), ("Y", 0f));
            AddState(c, "Move", Tree2D("X", "Y", 0f, 0.4f), 0, true); // X instant, Y damped
            rig.Begin(c);
            yield return Sim(0.1f);
            var port = Internals.Port(rig.Anim, "Move");
            Assert.NotNull(port);

            rig.Anim.SetFloat("X", 1f);
            rig.Anim.SetFloat("Y", 1f);
            yield return Frames(2);
            Assert.AreEqual(1f, port.Input.x, 1e-4f, "undamped axis reaches the target immediately");
            float first = port.Input.y;
            Assert.Greater(first, 0f, "damped axis has started to move");
            Assert.Less(first, 0.5f, "damped axis must not jump to the target");

            float prev = first; int increases = 0; bool mono = true;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                float y = port.Input.y;
                if (y < prev - 1e-5f) mono = false;
                if (y > prev + 1e-6f) increases++;
                prev = y;
            }
            Assert.IsTrue(mono, "Y approaches monotonically (no overshoot)");
            Assert.Greater(increases, 20);
            Assert.Less(prev, 0.99f, "still approaching after ~0.5s with 0.4s damp time");
            yield return Sim(3f);
            Assert.Greater(port.Input.y, 0.99f, "eventually reaches the target");
        }

        [UnityTest]
        public IEnumerator B_Damping_ZeroDampTimeIsInstant()
        {
            var rig = NewRig();
            var c = NewController(("X", 0f), ("Y", 0f));
            AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            rig.Begin(c);
            yield return Sim(0.1f);
            var port = Internals.Port(rig.Anim, "Move");
            rig.Anim.SetFloat("Y", -1f);
            yield return Frames(2);
            Assert.AreEqual(-1f, port.Input.y, 1e-4f);
        }

        // ---------------------------------------------------------------- c. phase / progress
        [UnityTest]
        public IEnumerator C_Progress_IncreasesWrapsAndPeriodFollowsInput()
        {
            var rig = NewRig();
            var c = NewController(("X", 0f), ("Y", 0f));
            AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            rig.Begin(c);
            yield return Frames(2);
            int idx = rig.Anim.GetActiveStateIndex(0);
            Assert.GreaterOrEqual(idx, 0);
            var port = Internals.Port(rig.Anim, "Move");
            Assert.AreEqual(1.0f, port.Period, 1e-3f, "origin period");

            float prev = rig.Anim.GetStateProgress(0, idx);
            int wraps = 0; float end = Time.time + 3.3f;
            while (Time.time < end)
            {
                yield return null;
                float p = rig.Anim.GetStateProgress(0, idx);
                Assert.That(p, Is.InRange(0f, 1f));
                if (p < prev - 0.5f) { wraps++; Assert.Less(p, 0.1f, "progress restarts near 0 after wrapping"); }
                else Assert.GreaterOrEqual(p, prev - 1e-5f, "progress must not go backwards");
                prev = p;
            }
            Assert.AreEqual(3, wraps, "3.3s at a 1.0s period = 3 wraps");

            foreach (int d in new[] { Dir.East, Dir.West, Dir.South })
            {
                rig.Anim.SetFloat("X", Dir.Pos[d].x); rig.Anim.SetFloat("Y", Dir.Pos[d].y);
                yield return Frames(3);
                Assert.AreEqual(Dir.Len[d], port.Period, 1e-3f, $"period at {Dir.Names[d]}");
                double c0 = port.CyclePosition; float t0 = Time.time;
                yield return Sim(0.6f);
                double c1 = port.CyclePosition; float t1 = Time.time;
                float rate = (float)((c1 - c0) / (t1 - t0));
                Assert.AreEqual(1f / Dir.Len[d], rate, 0.03f / Dir.Len[d], $"normalized rate at {Dir.Names[d]} follows 1/clip length");
                Assert.AreEqual(Frac(port.CyclePosition), rig.Anim.GetStateProgress(0, idx), 1e-3f);
            }

            // A blended input yields an in-between period.
            rig.Anim.SetFloat("X", 0.5f); rig.Anim.SetFloat("Y", 0f);
            yield return Frames(3);
            Assert.That(port.Period, Is.GreaterThan(1.0f).And.LessThan(1.2f), "half way between origin and east");
        }

        // ---------------------------------------------------------------- d. exit time
        private IEnumerator ExitTimeCase(float startX, float exitTime, float expectedSeconds, float switchToXAtCycle = -1f, float switchX = 0f)
        {
            var rig = NewRig();
            var c = NewController(("X", startX), ("Y", 0f));
            var clipState = AddState(c, "Clip", ClipNode(MakeClip("c", 1f, 5f, 0f)));
            var move = AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            move.transitions.Add(ExitTimeTransition(clipState, exitTime, 0.1f));
            rig.Begin(c);
            float t0 = rig.TimeEntered("Move");
            Assert.IsFalse(float.IsNaN(t0), "Move should have been entered as the default state");
            var port = Internals.Port(rig.Anim, "Move");

            bool switched = switchToXAtCycle < 0f;
            float tSwitch = 0f; double cycAtSwitch = 0;
            int guard = 0;
            while (rig.EnteredCount("Clip") == 0 && guard++ < 1000)
            {
                yield return null;
                if (!switched && port.CyclePosition >= switchToXAtCycle)
                {
                    rig.Anim.SetFloat("X", switchX); switched = true; tSwitch = Time.time; cycAtSwitch = port.CyclePosition;
                }
            }
            Assert.AreEqual(1, rig.EnteredCount("Clip"), "exit-time transition did not fire");
            float measured = rig.TimeEntered("Clip") - t0;
            if (switchToXAtCycle >= 0f)
            {
                float newPeriod = Dir.Len[Dir.West];
                expectedSeconds = (tSwitch - t0) + (exitTime - (float)cycAtSwitch) * newPeriod;
            }
            Assert.That(measured, Is.GreaterThanOrEqualTo(expectedSeconds - 0.03f).And.LessThanOrEqualTo(expectedSeconds + 0.07f),
                $"exit time {exitTime}: transition fired after {measured:F3}s, expected ~{expectedSeconds:F3}s");
            yield return Sim(0.3f);
            Assert.IsTrue(rig.Anim.IsStateActive("Clip"));
            Assert.AreEqual(5f, rig.Hip.localPosition.x, 0.05f);
        }

        [UnityTest] public IEnumerator D_ExitTime_1_0_Origin() { yield return ExitTimeCase(0f, 1.0f, 1.0f); }
        [UnityTest] public IEnumerator D_ExitTime_1_0_West() { yield return ExitTimeCase(-1f, 1.0f, 1.6f); }
        [UnityTest] public IEnumerator D_ExitTime_0_5_Origin() { yield return ExitTimeCase(0f, 0.5f, 0.5f); }
        [UnityTest] public IEnumerator D_ExitTime_0_5_West() { yield return ExitTimeCase(-1f, 0.5f, 0.8f); }
        [UnityTest] public IEnumerator D_ExitTime_1_0_InputChangesMidCycle() { yield return ExitTimeCase(0f, 1.0f, 0f, 0.5f, -1f); }

        // ---------------------------------------------------------------- e. events
        private Rig NewEventRig(System.Action onBeat)
        {
            return NewRig(r =>
            {
                var ue = new UnityEvent(); ue.AddListener(() => onBeat());
                r.localEvents.Add(new LocalEventEntry { eventName = "Beat", onEventTriggered = ue });
            });
        }

        [UnityTest]
        public IEnumerator E_Events_2D_FireOncePerCycleOver3Cycles_FixedInput()
        {
            int count = 0;
            var rig = NewEventRig(() => count++);
            var c = NewController(("X", 0f), ("Y", 0f));
            var s = AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            s.events.Add(new HonamiEventMarker { time = 0.5f, eventType = HonamiEventType.Local, eventName = "Beat" });
            rig.Begin(c);
            yield return Frames(1);
            var port = Internals.Port(rig.Anim, "Move");
            int guard = 0;
            while (port.CyclePosition < 3.0 && guard++ < 2000) yield return null;
            Assert.AreEqual(3, count, $"event at 0.5s should fire once in each of 3 cycles (cycle position {port.CyclePosition:F3})");
        }

        [UnityTest]
        public IEnumerator E_Events_2D_FireOncePerCycle_InputChangesMidCycle()
        {
            int count = 0;
            var rig = NewEventRig(() => count++);
            var c = NewController(("X", 0f), ("Y", 0f));
            var s = AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            s.events.Add(new HonamiEventMarker { time = 0.5f, eventType = HonamiEventType.Local, eventName = "Beat" });
            rig.Begin(c);
            yield return Frames(1);
            var port = Internals.Port(rig.Anim, "Move");
            bool s1 = false, s2 = false, s3 = false; int guard = 0;
            while (port.CyclePosition < 3.0 && guard++ < 3000)
            {
                yield return null;
                double cp = port.CyclePosition;
                if (!s1 && cp >= 0.45) { s1 = true; rig.Anim.SetFloat("X", -1f); }                       // origin(1.0s) -> west(1.6s)
                if (!s2 && cp >= 1.3) { s2 = true; rig.Anim.SetFloat("X", 0f); rig.Anim.SetFloat("Y", 1f); } // west -> north(1.4s)
                if (!s3 && cp >= 2.2) { s3 = true; rig.Anim.SetFloat("Y", 0f); }                         // north -> origin
            }
            Assert.IsTrue(s1 && s2 && s3);
            Assert.AreEqual(3, count, $"exactly one event per cycle even when the period changes mid-cycle (cycle position {port.CyclePosition:F3})");
        }

        [UnityTest]
        public IEnumerator E_Events_2D_PeriodChangeAcrossEventTime_StillOnce()
        {
            // Switch exactly around the event point (cycle ~0.45 -> ~0.55 of the cycle), west -> origin.
            int count = 0;
            var rig = NewEventRig(() => count++);
            var c = NewController(("X", -1f), ("Y", 0f)); // start west, 1.6s
            var s = AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            s.events.Add(new HonamiEventMarker { time = 0.5f, eventType = HonamiEventType.Local, eventName = "Beat" });
            rig.Begin(c);
            yield return Frames(1);
            var port = Internals.Port(rig.Anim, "Move");
            int guard = 0; bool sw = false;
            // Cycle 0: event due at 0.5s = cycle 0.3125. Switch to origin (1.0s) at cycle 0.25 (loopedTime 0.4 -> 0.25).
            while (port.CyclePosition < 1.0 && guard++ < 2000)
            {
                yield return null;
                if (!sw && port.CyclePosition >= 0.25) { sw = true; rig.Anim.SetFloat("X", 0f); }
            }
            Assert.AreEqual(1, count, "cycle 0 produced exactly one event");
            while (port.CyclePosition < 2.0 && guard++ < 4000) yield return null;
            Assert.AreEqual(2, count, "cycle 1 produced exactly one event");
        }

        [UnityTest]
        public IEnumerator E_Events_2D_MidCycleFiringsExactlyOncePerCycle_InputChanges()
        {
            // Firings that happen away from the loop boundary must still be exactly one per cycle when the period changes.
            var fires = new List<double>();
            HonamiBlend2DPortRuntime port = null;
            var rig = NewEventRig(() => fires.Add(port != null ? port.CyclePosition : -1));
            var c = NewController(("X", 0f), ("Y", 0f));
            var s = AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            s.events.Add(new HonamiEventMarker { time = 0.5f, eventType = HonamiEventType.Local, eventName = "Beat" });
            rig.Begin(c);
            yield return Frames(1);
            port = Internals.Port(rig.Anim, "Move");
            bool s1 = false, s2 = false, s3 = false; int guard = 0;
            while (port.CyclePosition < 3.0 && guard++ < 3000)
            {
                yield return null;
                double cp = port.CyclePosition;
                if (!s1 && cp >= 0.45) { s1 = true; rig.Anim.SetFloat("X", -1f); }
                if (!s2 && cp >= 1.3) { s2 = true; rig.Anim.SetFloat("X", 0f); rig.Anim.SetFloat("Y", 1f); }
                if (!s3 && cp >= 2.2) { s3 = true; rig.Anim.SetFloat("Y", 0f); }
            }
            var mid = new int[3];
            var wrap = 0;
            foreach (double f in fires)
            {
                double frac = f - System.Math.Floor(f);
                if (frac > 0.05 && frac < 0.95) { int cycle = (int)System.Math.Floor(f); if (cycle >= 0 && cycle < 3) mid[cycle]++; }
                else wrap++;
            }
            Assert.AreEqual(new[] { 1, 1, 1 }, mid, "one mid-cycle firing per cycle");
            Assert.AreEqual(0, wrap, "no extra firing at the loop boundary");
        }

        [UnityTest]
        public IEnumerator E_Events_1D_FireOncePerCycle()
        {
            // Same event on a 1D tree: the shared looping evaluator must fire once per cycle.
            int count = 0;
            var rig = NewEventRig(() => count++);
            var c = NewController(("X", 0f));
            var s = AddState(c, "Move", Tree1D("X"), 0, true);
            s.events.Add(new HonamiEventMarker { time = 0.5f, eventType = HonamiEventType.Local, eventName = "Beat" });
            rig.Begin(c);
            yield return Frames(1);
            float t0 = Time.time;
            yield return Sim(3.05f);
            Assert.AreEqual(3, count, $"1D: event at 0.5s of a 1.0s loop, {Time.time - t0:F2}s elapsed");
        }

        // ---------------------------------------------------------------- f. crossfades
        [UnityTest]
        public IEnumerator F_Crossfade_ClipTo2D_And2DToClip_WithInterrupt()
        {
            var rig = NewRig();
            var c = NewController(("X", 1f), ("Y", 0f)); // 2D sits at East: hip.x = 2
            AddState(c, "Clip", ClipNode(MakeClip("c", 1f, 5f, 0f)), 0, true); // hip.x = 5
            AddState(c, "Move", Tree2D("X", "Y", 0f, 0f));
            rig.Begin(c);
            yield return Sim(0.2f);
            Assert.AreEqual(5f, rig.Hip.localPosition.x, 0.05f);

            // clip -> 2D
            rig.Anim.PlayState("Move", 0.5f);
            yield return Sim(0.25f);
            float mid = rig.Hip.localPosition.x;
            Assert.That(mid, Is.GreaterThan(2.3f).And.LessThan(4.7f), "mid fade clip->2D is a blend");
            // interrupt back to the source mid-fade
            rig.Anim.PlayState("Clip", 0.5f);
            yield return Sim(0.8f);
            Assert.AreEqual(5f, rig.Hip.localPosition.x, 0.05f, "interrupted fade settles on the source clip");
            Assert.IsTrue(rig.Anim.IsStateActive("Clip"));

            // clip -> 2D to completion
            rig.Anim.PlayState("Move", 0.3f);
            yield return Sim(0.6f);
            Assert.AreEqual(2f, rig.Hip.localPosition.x, 0.05f, "fully faded to the 2D state");
            var port = Internals.Port(rig.Anim, "Move");
            Assert.IsTrue(port.IsInitialized);
            Assert.AreEqual(1.2f, port.Period, 1e-3f);
            Assert.Greater(port.GetWeight(Dir.East), 0.99f);

            // 2D -> clip, interrupted by a return to 2D
            rig.Anim.PlayState("Clip", 0.4f);
            yield return Sim(0.2f);
            float mid2 = rig.Hip.localPosition.x;
            Assert.That(mid2, Is.GreaterThan(2.3f).And.LessThan(4.7f), "mid fade 2D->clip is a blend");
            rig.Anim.PlayState("Move", 0.3f);
            yield return Sim(0.6f);
            Assert.AreEqual(2f, rig.Hip.localPosition.x, 0.05f, "interrupt back to the 2D source settles on it");
            Assert.IsTrue(rig.Anim.IsStateActive("Move"));
            Assert.Greater(port.GetWeight(Dir.East), 0.99f);

            // plain 2D -> clip
            rig.Anim.PlayState("Clip", 0.3f);
            yield return Sim(0.6f);
            Assert.AreEqual(5f, rig.Hip.localPosition.x, 0.05f);
            Assert.IsTrue(rig.Anim.IsStateActive("Clip"));
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- g. invalid content
        private IEnumerator InvalidCase(HonamiController c, Rig rig)
        {
            rig.Begin(c);
            yield return Sim(0.5f);

            // Another layer keeps playing: Tail.z is a sawtooth driven by layer 1.
            int moved = 0; float prev = rig.Tail.localPosition.z;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                float z = rig.Tail.localPosition.z;
                if (Mathf.Abs(z - prev) > 1e-5f) moved++;
                prev = z;
            }
            Assert.Greater(moved, 25, "the other layer should keep animating Tail");

            // Another state on the same layer still plays.
            rig.Anim.PlayState("Good", 0.1f, 0);
            yield return Sim(0.4f);
            Assert.AreEqual(5f, rig.Hip.localPosition.x, 0.05f, "a valid state on the faulty layer still plays");
            // Faulty state is selectable again without extra logs or exceptions.
            rig.Anim.PlayState("Bad", 0.1f, 0);
            yield return Sim(0.3f);
            rig.Anim.PlayState("Good", 0.1f, 0);
            yield return Sim(0.3f);
            Assert.AreEqual(5f, rig.Hip.localPosition.x, 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        private HonamiController InvalidController(HonamiBlendTreeNode bad, bool loop, out HonamiState badState)
        {
            var c = NewController(("X", 1f), ("Y", 0f));
            AddLayer(c, "Aux");
            badState = AddState(c, "Bad", bad, 0, true);
            badState.loop = loop;
            AddState(c, "Good", ClipNode(MakeClip("good", 1f, 5f, 0f)), 0, false);
            AddState(c, "Aux", ClipNode(MakeClip("aux", 1f, 0f, 0f, true, 0f, "Tail")), 1, true);
            return c;
        }

        [UnityTest]
        public IEnumerator G_Invalid_NoOriginMotion_LogsOnceAndKeepsRunning()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Honami\] Directional blend tree state 'Bad' is disabled: .*origin", RegexOptions.Singleline));
            var rig = NewRig();
            var c = InvalidController(Tree2D("X", "Y", 0f, 0f, includeOrigin: false), true, out _);
            yield return InvalidCase(c, rig);
        }

        [UnityTest]
        public IEnumerator G_Invalid_LoopOff_LogsOnceAndKeepsRunning()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Honami\] Directional blend tree state 'Bad' is disabled: .*(non-looping|loop)", RegexOptions.Singleline));
            var rig = NewRig();
            var c = InvalidController(Tree2D("X", "Y", 0f, 0f), false, out _);
            yield return InvalidCase(c, rig);
        }

        [UnityTest]
        public IEnumerator G_Invalid_ReplacerSetsLoopOffAtRuntime_LogsOnceAndKeepsRunning()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Honami\] Directional blend tree state 'Bad' is disabled: .*loop", RegexOptions.Singleline));
            var rig = NewRig();
            var c = InvalidController(Tree2D("X", "Y", 0f, 0f), true, out var bad);
            AddBool(c, "NoLoop");
            var replacer = Make<HonamiReplacerSubNode>();
            var rule = new HonamiReplacerRule { overrideLoop = true, loop = false };
            rule.conditions.Add(new HonamiCondition { parameter = "NoLoop", mode = HonamiConditionMode.If });
            replacer.rules.Add(rule);
            bad.subNodes.Add(replacer);

            rig.Begin(c);
            yield return Sim(0.4f);
            Assert.AreEqual(2f, rig.Hip.localPosition.x, 0.05f, "2D state plays before the replacer fires");
            Assert.IsTrue(bad.loop);

            rig.Anim.SetBool("NoLoop", true);
            yield return Sim(0.5f);
            Assert.IsFalse(bad.loop, "replacer applied loop=false");
            var port = Internals.Port(rig.Anim, "Bad");
            Assert.IsFalse(port.IsInitialized, "faulted port is reset");

            // Other layer keeps running; no further logs.
            int moved = 0; float prev = rig.Tail.localPosition.z;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                float z = rig.Tail.localPosition.z;
                if (Mathf.Abs(z - prev) > 1e-5f) moved++;
                prev = z;
            }
            Assert.Greater(moved, 25);
            rig.Anim.PlayState("Good", 0.1f, 0);
            yield return Sim(0.4f);
            Assert.AreEqual(5f, rig.Hip.localPosition.x, 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- h. timed controller swap
        private IEnumerator SwapCase(HonamiControllerTransitionMode mode, bool expectAdvance)
        {
            var rig = NewRig();
            var a = NewController(("X", 1f), ("Y", 0f)); // East, period 1.2
            AddState(a, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            var b = NewController();
            AddState(b, "B", ClipNode(MakeClip("b", 1f, 5f, 0f, false)), 0, true); // z constant 0
            rig.Begin(a);
            yield return Sim(0.1f);

            const float fade = 3f;
            float swapStart = Time.time;
            rig.Anim.SetController(b, fade, null, mode);

            float ZEst() => rig.Hip.localPosition.z / Mathf.Max(0.05f, 1f - (Time.time - swapStart) / fade);

            yield return Sim(0.2f);
            float zA = ZEst();
            var series = new List<float>();
            float end = Time.time + 0.6f;
            while (Time.time < end) { yield return null; series.Add(ZEst()); }
            float zB = ZEst();
            float x = rig.Hip.localPosition.x;
            Assert.That(x, Is.GreaterThan(2f).And.LessThan(5f), "pose is mid-blend between old (x=2) and new (x=5) controllers");

            if (expectAdvance)
            {
                Assert.Greater(zB - zA, 0.3f, $"outgoing 2D animation must keep advancing during the fade (z_est {zA:F3} -> {zB:F3})");
                int inc = 0;
                for (int i = 1; i < series.Count; i++) if (series[i] > series[i - 1] + 1e-5f) inc++;
                Assert.Greater(inc, series.Count * 0.8f, "outgoing pose advances nearly every frame (no freeze)");
            }
            else
            {
                Assert.Less(Mathf.Abs(zB - zA), 0.05f, $"Freeze mode: outgoing pose must hold (z_est {zA:F3} -> {zB:F3})");
            }

            yield return Sim(fade);
            Assert.AreEqual(5f, rig.Hip.localPosition.x, 0.05f, "fade completes on the new controller");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator H_TimedControllerSwap_ContinueEvaluating_OutgoingKeepsAdvancing()
        { yield return SwapCase(HonamiControllerTransitionMode.ContinueEvaluating, true); }

        [UnityTest] public IEnumerator H_TimedControllerSwap_Freeze_OutgoingHolds()
        { yield return SwapCase(HonamiControllerTransitionMode.Freeze, false); }

        [UnityTest]
        public IEnumerator H_TimedControllerSwap_BackToA_NewPortWorks()
        {
            var rig = NewRig();
            var a = NewController(("X", 1f), ("Y", 0f));
            AddState(a, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            var b = NewController();
            AddState(b, "B", ClipNode(MakeClip("b", 1f, 5f, 0f, false)), 0, true);
            rig.Begin(a);
            yield return Sim(0.3f);
            rig.Anim.SetController(b, 0.3f);
            yield return Sim(0.2f);
            rig.Anim.SetController(a, 0.3f); // swap again while the first fade is still running
            yield return Sim(0.8f);
            Assert.AreEqual(2f, rig.Hip.localPosition.x, 0.05f);
            var port = Internals.Port(rig.Anim, "Move");
            Assert.NotNull(port);
            Assert.IsTrue(port.IsInitialized);
            double c0 = port.CyclePosition;
            yield return Sim(0.3f);
            Assert.Greater(port.CyclePosition, c0 + 0.1, "new 2D port is advancing");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- i. disable / enable
        [UnityTest]
        public IEnumerator I_DisableEnable_DuringPlayback_ResumesWithoutErrors()
        {
            var rig = NewRig();
            var c = NewController(("X", 1f), ("Y", 0f));
            AddState(c, "Move", Tree2D("X", "Y", 0f, 0f), 0, true);
            rig.Begin(c);
            yield return Sim(0.5f);

            for (int round = 0; round < 3; round++)
            {
                rig.Go.SetActive(false);
                yield return Frames(5);
                rig.Go.SetActive(true);
                yield return Frames(2);

                var port = Internals.Port(rig.Anim, "Move");
                Assert.NotNull(port, $"round {round}: port exists after re-enable");
                Assert.IsTrue(port.IsInitialized, $"round {round}: port initialized after re-enable");
                int moved = 0; float prev = rig.Hip.localPosition.z;
                double c0 = port.CyclePosition;
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                    float z = rig.Hip.localPosition.z;
                    if (Mathf.Abs(z - prev) > 1e-5f) moved++;
                    prev = z;
                }
                Assert.Greater(moved, 33, $"round {round}: pose keeps moving after re-enable");
                Assert.Greater(port.CyclePosition, c0 + 0.3, $"round {round}: port advances after re-enable");
                Assert.AreEqual(2f, rig.Hip.localPosition.x, 0.05f);
                Assert.Greater(port.GetWeight(Dir.East), 0.99f);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- j. 1D regression
        [UnityTest]
        public IEnumerator J_OneD_StillBlendsByParameter()
        {
            var rig = NewRig();
            var c = NewController(("X", 0f));
            AddState(c, "Move", Tree1D("X"), 0, true);
            rig.Begin(c);
            yield return Sim(0.3f);
            Assert.AreEqual(0f, rig.Hip.localPosition.x, 0.05f);
            foreach (var (v, expected) in new[] { (0.5f, 1f), (1f, 2f), (0.25f, 0.5f), (0f, 0f) })
            {
                rig.Anim.SetFloat("X", v);
                yield return Sim(0.3f);
                Assert.AreEqual(expected, rig.Hip.localPosition.x, 0.05f, $"1D X={v}");
            }
            // 1D progress still advances and wraps.
            int idx = rig.Anim.GetActiveStateIndex(0);
            float prev = rig.Anim.GetStateProgress(0, idx); int wraps = 0; float end = Time.time + 2.2f;
            while (Time.time < end)
            {
                yield return null;
                float p = rig.Anim.GetStateProgress(0, idx);
                if (p < prev - 0.5f) wraps++;
                prev = p;
            }
            Assert.That(wraps, Is.InRange(2, 3), "1D state at X=0 loops every ~1.0s");
            Assert.IsNull(Internals.Port(rig.Anim, "Move"), "1D state must not get a directional port");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator J_OneD_ExitTimeStillWorks()
        {
            var rig = NewRig();
            var c = NewController(("X", 0f));
            var clipState = AddState(c, "Clip", ClipNode(MakeClip("c", 1f, 5f, 0f)));
            var move = AddState(c, "Move", Tree1D("X"), 0, true);
            move.transitions.Add(ExitTimeTransition(clipState, 1.0f, 0.1f));
            rig.Begin(c);
            float t0 = rig.TimeEntered("Move");
            int guard = 0;
            while (rig.EnteredCount("Clip") == 0 && guard++ < 600) yield return null;
            float measured = rig.TimeEntered("Clip") - t0;
            Assert.That(measured, Is.InRange(1.0f - 0.03f, 1.0f + 0.07f), "1D exit time 1.0 at a 1.0s period");
        }
    }
}
