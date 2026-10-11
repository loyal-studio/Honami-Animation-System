using System.Collections;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace HonamiAnimationSystem.Tests.BlendTree2D
{
    /// <summary>State events on a plain looping clip: each event fires exactly once per completed cycle.</summary>
    public class LoopingEventTests : HonamiPlayModeTestBase
    {
        private const float ClipLength = 1f;

        private Rig StartLoopingClip(float eventTime, System.Action onBeat)
        {
            var rig = NewRig(r =>
            {
                var ue = new UnityEvent();
                ue.AddListener(() => onBeat());
                r.localEvents.Add(new LocalEventEntry { eventName = "Beat", onEventTriggered = ue });
            });
            var c = NewController();
            var s = AddState(c, "Loop", ClipNode(MakeClip("Loop1s", ClipLength, 0f, 0f)), 0, true);
            s.events.Add(new HonamiEventMarker { time = eventTime, eventType = HonamiEventType.Local, eventName = "Beat" });
            rig.Begin(c);
            return rig;
        }

        [UnityTest]
        public IEnumerator MidCycleEvent_FiresOncePerCycle()
        {
            int count = 0;
            StartLoopingClip(0.5f, () => count++);
            yield return Sim(2.95f);
            Assert.AreEqual(3, count, "event at 0.5 s of a 1 s loop over ~2.95 s");
        }

        [UnityTest]
        public IEnumerator EventAtTimeZero_FiresAtEachCycleStart()
        {
            int count = 0;
            StartLoopingClip(0f, () => count++);
            yield return Sim(2.95f);
            Assert.AreEqual(3, count, "event at 0 s fires at the start of cycles 0, 1 and 2");
        }

        [UnityTest]
        public IEnumerator EventAtCycleEnd_FiresOncePerCompletedCycle()
        {
            int count = 0;
            StartLoopingClip(ClipLength, () => count++);
            yield return Sim(2.95f);
            Assert.AreEqual(2, count, "event at the cycle end fires when cycles 0 and 1 complete");
        }

        [UnityTest]
        public IEnumerator LargeTimeStep_SkippedCycleStillFiresOnce()
        {
            int count = 0;
            StartLoopingClip(0.5f, () => count++);
            yield return Sim(0.6f);
            Assert.AreEqual(1, count, "cycle 0 fired before the jump");

            float savedMax = Time.maximumDeltaTime;
            try
            {
                // One 2.25 s frame: ends cycle 0 (already fired), skips cycle 1 entirely, lands mid cycle 2.
                Time.maximumDeltaTime = 5f;
                Time.captureDeltaTime = 2.25f;
                yield return null;
                Time.captureDeltaTime = Dt;
                yield return Frames(3);
            }
            finally
            {
                Time.captureDeltaTime = Dt;
                Time.maximumDeltaTime = savedMax;
            }
            Assert.AreEqual(3, count, "cycles 0, 1 and 2 each fire exactly once across the jump");
        }
    }
}
