using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    internal sealed class HonamiTimelineModuleSet
    {
        private const byte Unresolved = 0;
        private const byte Resolved = 1;

        public readonly HonamiTimelineModuleRuntime[] Runtimes;
        public readonly int Revision;
        public bool IsStale;

        private readonly List<HonamiTimelineModule> _modules;
        private readonly int[] _parentModule;
        private readonly HonamiTimelineItem[] _parentItem;
        private readonly int[] _motion;
        private readonly bool[] _missing;
        private readonly bool[] _playing;
        private readonly byte[] _pass;
        private readonly bool[] _active;
        private readonly float[] _previous;
        private readonly float[] _time;

        public HonamiTimelineModuleSet(List<HonamiTimelineModule> modules, HonamiNodeBase node, int revision)
        {
            int count = modules.Count;
            _modules = modules;
            Revision = revision;
            Runtimes = new HonamiTimelineModuleRuntime[count];
            _parentModule = new int[count];
            _parentItem = new HonamiTimelineItem[count];
            _motion = new int[count];
            _missing = new bool[count];
            _playing = new bool[count];
            _pass = new byte[count];
            _active = new bool[count];
            _previous = new float[count];
            _time = new float[count];

            for (int i = 0; i < count; i++)
            {
                var parent = HonamiTimelineModuleParent.Resolve(modules, node, modules[i]);
                _parentModule[i] = parent.ModuleIndex;
                _parentItem[i] = parent.Item;
                _motion[i] = parent.MotionIndex;
                _missing[i] = parent.IsMissing || HonamiTimelineModuleParent.HasCycle(modules, node, modules[i]);
            }
        }

        public bool IsMissingParent(int index) => _missing[index];

        public void PauseAll()
        {
            for (int i = 0; i < Runtimes.Length; i++)
            {
                if (_playing[i]) Runtimes[i]?.OnPause();
            }
        }

        public void ResumeAll()
        {
            for (int i = 0; i < Runtimes.Length; i++)
            {
                if (_playing[i]) Runtimes[i]?.OnResume();
            }
        }

        public void Dispose()
        {
            StopAll();
            for (int i = 0; i < Runtimes.Length; i++)
            {
                var runtime = Runtimes[i];
                if (runtime != null)
                {
                    runtime.Dispose();
                    Runtimes[i] = null;
                }
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < Runtimes.Length; i++)
            {
                if (!_playing[i]) continue;
                _playing[i] = false;
                Runtimes[i]?.OnStop();
            }
        }

        // notify = false evaluates without OnPlay/OnStop, for seeking a timeline that is not playing.
        public void Advance(in HonamiTimelineFrame frame, HonamiNodeBase node, HonamiNodeRuntime nodeRuntime, bool notify = true)
        {
            int count = Mathf.Min(Runtimes.Length, _modules.Count);
            for (int i = 0; i < count; i++)
                _pass[i] = Unresolved;
            for (int i = 0; i < count; i++)
                Resolve(i, in frame, node, nodeRuntime);

            if (notify)
            {
                for (int i = 0; i < count; i++)
                {
                    var runtime = Runtimes[i];
                    if (runtime == null || _active[i] == _playing[i]) continue;

                    _playing[i] = _active[i];
                    if (_active[i]) runtime.OnPlay();
                    else runtime.OnStop();
                }
            }

            for (int i = 0; i < count; i++)
            {
                var runtime = Runtimes[i];
                if (runtime == null || !_active[i] || IsMuted(i)) continue;
                var local = new HonamiTimelineFrame(_previous[i], _time[i], frame.IsSeek, frame.Hold);
                runtime.Evaluate(in local);
            }
        }

        private void Resolve(int index, in HonamiTimelineFrame frame, HonamiNodeBase node, HonamiNodeRuntime nodeRuntime)
        {
            if (_pass[index] == Resolved) return;
            _pass[index] = Resolved;
            _active[index] = false;

            if (_missing[index] || _modules[index] == null) return;

            int parentModule = _parentModule[index];
            if (parentModule >= 0)
            {
                Resolve(parentModule, in frame, node, nodeRuntime);
                if (!_active[parentModule]) return;

                // Only modules under the parent's last item keep holding past its end; earlier items have been left behind.
                var item = _parentItem[index];
                bool hold = frame.Hold && item.EndTime >= _modules[parentModule].GetDuration() - 0.001f;
                _active[index] = TryEnterWindow(_previous[parentModule], _time[parentModule], frame.IsSeek, hold, item.time, item.Duration, out _previous[index], out _time[index]);
                return;
            }

            if (_motion[index] >= 0)
            {
                int motion = ResolveMotion(index, node);
                if (motion < 0 || !node.IsMotionPlaying(motion, nodeRuntime)) return;

                _active[index] = TryEnterWindow(frame.PreviousTime, frame.Time, frame.IsSeek, frame.Hold, node.GetMotionStart(motion), node.GetMotionDuration(motion), out _previous[index], out _time[index]);
                return;
            }

            _active[index] = true;
            _previous[index] = frame.PreviousTime;
            _time[index] = frame.Time;
        }

        // Node motions can be edited while the state plays, so a cached index is re-resolved when its id no longer matches.
        private int ResolveMotion(int index, HonamiNodeBase node)
        {
            if (node == null) return -1;

            int motion = _motion[index];
            string parentId = _modules[index].parentId;
            if (motion < node.MotionCount && node.GetMotionId(motion) == parentId) return motion;

            motion = node.FindMotion(parentId);
            if (motion < 0) _missing[index] = true;
            else _motion[index] = motion;
            return motion;
        }

        // Only the current time is clamped: an unclamped previous time keeps events at the very start of the window reachable.
        private static bool TryEnterWindow(float previous, float time, bool isSeek, bool hold, float start, float duration, out float localPrevious, out float localTime)
        {
            float end = start + duration;
            localPrevious = previous - start;
            localTime = Mathf.Clamp(time - start, 0f, duration);

            if (isSeek)
            {
                localPrevious = localTime;
                return (time >= start && time <= end) || (hold && time >= end);
            }

            if (hold && time >= end && Mathf.Max(previous, time) >= start)
            {
                return true;
            }

            return Mathf.Max(previous, time) >= start && Mathf.Min(previous, time) <= end;
        }

        private bool IsMuted(int index)
        {
            for (int step = 0; index >= 0 && step <= _modules.Count; step++)
            {
                var module = _modules[index];
                if (module == null || module.muted) return true;
                index = _parentModule[index];
            }
            return false;
        }
    }
}
