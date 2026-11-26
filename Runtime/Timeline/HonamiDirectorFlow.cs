using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    // Keeps track of the Wait marker playback rests on and the Loop region it currently sits in,
    // so HonamiDirector only coordinates playback instead of owning marker bookkeeping.
    internal sealed class HonamiDirectorFlow
    {
        private HonamiTimelineWaitMarker _wait;
        private HonamiTimelineLoopMarker _loop;
        private HonamiTimelineLoopMarker _releasedLoop;
        private bool _releaseLoop;

        public HonamiTimelineWaitMarker Waiting => _wait;
        public HonamiTimelineLoopMarker Loop => _loop;

        public string WaitingFor =>
            _wait != null ? _wait.signal
            : _loop != null && _loop.mode == HonamiTimelineLoopMode.RuntimeLoopUntilSignal && !_releaseLoop ? _loop.signal
            : null;

        public void Clear()
        {
            _wait = null;
            _loop = null;
            _releasedLoop = null;
            _releaseLoop = false;
        }

        public void EnterWait(HonamiTimelineWaitMarker wait) => _wait = wait;

        public bool TryRelease(string signal)
        {
            if (_wait != null)
            {
                if (!SignalMatches(_wait.signal, signal)) return false;
                _wait = null;
                return true;
            }

            if (_loop == null || _loop.mode != HonamiTimelineLoopMode.RuntimeLoopUntilSignal) return false;
            if (!SignalMatches(_loop.signal, signal)) return false;
            _releaseLoop = true;
            return true;
        }

        // Picks up the region the playhead has entered since the previous step.
        public void EnsureLoop(List<HonamiTimelineMarker> markers, float time)
        {
            if (_loop == null)
                _loop = FindLoopAt(markers, time, _releasedLoop);
        }

        // Applies a release requested by Continue(). True means the caller may step again without advancing time.
        public bool TryApplyLoopRelease()
        {
            if (_loop == null || !_releaseLoop) return false;

            _releasedLoop = _loop;
            _loop = null;
            _releaseLoop = false;
            return true;
        }

        public static HonamiTimelineWaitMarker FindWaitAt(List<HonamiTimelineMarker> markers, float time)
        {
            if (markers == null) return null;

            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] is HonamiTimelineWaitMarker wait && Mathf.Approximately(wait.time, time))
                    return wait;
            }
            return null;
        }

        public static HonamiTimelineWaitMarker FindWaitCrossed(List<HonamiTimelineMarker> markers, float previous, float time)
        {
            if (markers == null) return null;

            HonamiTimelineWaitMarker found = null;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] is not HonamiTimelineWaitMarker wait || !(previous < wait.time && wait.time <= time)) continue;
                if (found == null || wait.time < found.time) found = wait;
            }
            return found;
        }

        private static HonamiTimelineLoopMarker FindLoopAt(List<HonamiTimelineMarker> markers, float time, HonamiTimelineLoopMarker skip)
        {
            if (markers == null) return null;

            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] is not HonamiTimelineLoopMarker loop || loop == skip || loop.mode == HonamiTimelineLoopMode.PreviewOnly) continue;
                if (loop.endTime > loop.time && time >= loop.time && time < loop.endTime) return loop;
            }
            return null;
        }

        private static bool SignalMatches(string expected, string received) =>
            string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(received) || expected == received;
    }
}
