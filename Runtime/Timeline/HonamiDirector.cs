using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [AddComponentMenu("Honami Animation/Honami Director")]
    public sealed class HonamiDirector : MonoBehaviour, IHonamiSceneReferenceTable
    {
        // A single Advance may cross several waits and loops; the cap also protects malformed overlapping regions.
        private const int MaxStepsPerAdvance = 128;
        // A looping region restarts just before its start, so only events at the start fire again, not every earlier one.
        private const float RegionRestartMargin = 0.0001f;

        [SerializeField] private HonamiTimeline timeline;
        [Tooltip("When the director starts playing automatically: Manual (wait for Play()), Awake, Start, or OnEnable.")]
        [SerializeField] private HonamiDirectorPlayTrigger playTrigger = HonamiDirectorPlayTrigger.Manual;
        [Tooltip("If enabled, automatic playback (Awake, Start, OnEnable) only evaluates the first frame and pauses.")]
        [SerializeField] private bool pauseAtFirstFrameOnAutoPlay;
        [Tooltip("None: stop and release everything at the end. Hold: keep the last frame until Stop. Loop: start over.")]
        [SerializeField] private HonamiDirectorWrapMode wrapMode = HonamiDirectorWrapMode.None;
        [Tooltip("Game Time follows Time.timeScale, Unscaled ignores it, Manual only advances through Advance().")]
        [SerializeField] private HonamiDirectorUpdateMode updateMode = HonamiDirectorUpdateMode.GameTime;
        [SerializeField, Min(0f)] private float speed = 1f;
        [Tooltip("Free-form labels other systems can query while this director runs, e.g. Cutscene to block the pause menu.")]
        [SerializeField] private List<string> tags = new();
        [SerializeField] private List<HonamiTimelineBinding> bindings = new();
        [SerializeField] private List<HonamiTimelineBindingEntry> references = new();

        private static readonly List<HonamiDirector> _activeDirectors = new();

        private readonly HonamiDirectorFlow _flow = new();
        private HonamiTimelineModuleSet _modules;
        private HonamiTimeline _builtTimeline;
        private int _builtRevision = -1;
        private bool _runtimesDirty = true;
        private float _time;
        private bool _playing;
        private bool _active;

        public static event Action<HonamiDirector> AnyStarted;
        public static event Action<HonamiDirector> AnyStopped;

        public event Action<HonamiDirector> Started;
        public event Action<HonamiDirector> Played;
        public event Action<HonamiDirector> Paused;
        public event Action<HonamiDirector> Stopped;
        public event Action<HonamiDirector> Finished;
        public event Action<HonamiDirector> Looped;
        public event Action<HonamiDirector, HonamiTimelineWaitMarker> Waiting;
        public event Action<HonamiDirector, HonamiTimelineLoopMarker> RegionLooped;

        public HonamiTimeline Timeline
        {
            get => timeline;
            set
            {
                if (timeline == value) return;
                Stop();
                timeline = value;
                _runtimesDirty = true;
            }
        }

        public HonamiDirectorPlayTrigger PlayTrigger
        {
            get => playTrigger;
            set => playTrigger = value;
        }

        public bool PauseAtFirstFrameOnAutoPlay
        {
            get => pauseAtFirstFrameOnAutoPlay;
            set => pauseAtFirstFrameOnAutoPlay = value;
        }

        public HonamiDirectorWrapMode WrapMode
        {
            get => wrapMode;
            set => wrapMode = value;
        }

        public HonamiDirectorUpdateMode UpdateMode
        {
            get => updateMode;
            set => updateMode = value;
        }

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        public float CurrentTime => _time;
        public bool IsPlaying => _playing;
        public bool IsPaused => _active && !_playing;
        public bool IsActive => _active;
        public bool IsWaiting => _flow.Waiting != null;
        public string WaitingFor => _flow.WaitingFor;
        public float Duration => timeline != null ? timeline.GetDuration() : 0f;
        public IReadOnlyList<string> Tags => tags;
        public IReadOnlyList<HonamiTimelineBinding> Bindings => bindings;
        public IReadOnlyList<HonamiTimelineBindingEntry> References => references;
        public static IReadOnlyList<HonamiDirector> ActiveDirectors => _activeDirectors;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag) return true;
            }
            return false;
        }

        // Active covers playing, paused, waiting and holding the last frame.
        public static bool IsAnyActiveWithTag(string tag)
        {
            for (int i = 0; i < _activeDirectors.Count; i++)
            {
                var director = _activeDirectors[i];
                if (director != null && director.HasTag(tag)) return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _activeDirectors.Clear();
            AnyStarted = null;
            AnyStopped = null;
        }

        private void Awake()
        {
            if (playTrigger == HonamiDirectorPlayTrigger.Awake)
                TriggerAutoPlay();
        }

        private void OnEnable()
        {
            if (playTrigger == HonamiDirectorPlayTrigger.OnEnable)
                TriggerAutoPlay();
        }

        private void Start()
        {
            if (playTrigger == HonamiDirectorPlayTrigger.Start)
                TriggerAutoPlay();
        }

        private void TriggerAutoPlay()
        {
            Play();
            if (pauseAtFirstFrameOnAutoPlay)
                Pause();
        }

        private void OnValidate() => _runtimesDirty = true;

        private void OnDisable() => Stop();

        private void OnDestroy() => ReleaseRuntimes();

        // LateUpdate runs after Animators and Honami animators evaluate, so sampled clips and camera shots win the frame.
        private void LateUpdate()
        {
            if (!_active || timeline == null || updateMode == HonamiDirectorUpdateMode.Manual) return;

            if (_playing)
            {
                float delta = updateMode == HonamiDirectorUpdateMode.UnscaledGameTime ? Time.unscaledDeltaTime : Time.deltaTime;
                Step(delta * speed);
            }
            else if (wrapMode == HonamiDirectorWrapMode.Hold && _time >= Duration)
            {
                Evaluate(_time, _time, false);
            }
        }

        public void Play() => StartPlayback(0f, false);

        public void Play(float startTime) => StartPlayback(Mathf.Clamp(startTime, 0f, Duration), true);

        // Playing from the start evaluates from -infinity so events on the very first frame still fire;
        // seeking starts exactly at the requested time without replaying earlier events.
        private void StartPlayback(float time, bool seek)
        {
            if (timeline == null) return;

            if (_active)
                NotifyStop();

            EnsureRuntimes();
            _time = time;
            _flow.Clear();
            _playing = true;
            Begin();

            Evaluate(seek ? time : float.NegativeInfinity, time, seek);
            EnterWaitAtCurrentTime();
            Played?.Invoke(this);
        }

        public void Pause()
        {
            if (!_playing) return;
            SuspendPlayback();
            Paused?.Invoke(this);
        }

        public void Resume()
        {
            if (timeline == null || _playing) return;
            if (_flow.Waiting != null) return;
            if (!_active)
            {
                Play(_time);
                return;
            }

            ResumePlayback();
        }

        public void Stop()
        {
            bool wasActive = Deactivate();
            // A Seek on a stopped director evaluates without OnPlay, so OnStop would never clear what it left, like a fade overlay.
            if (!wasActive) ReleaseRuntimes();
            _playing = false;
            _time = 0f;
            _flow.Clear();
            if (wasActive)
                RaiseStopped();
        }

        public void Seek(float time)
        {
            if (timeline == null) return;
            _time = Mathf.Clamp(time, 0f, Duration);
            Evaluate();
        }

        public bool SeekToMarker(string markerId)
        {
            if (timeline == null || !timeline.TryGetMarkerTime(markerId, out float time)) return false;
            Seek(time);
            return true;
        }

        // Releases the current Wait marker or a Runtime Loop Until Signal region.
        public bool Continue(string signal = null)
        {
            bool wasWaiting = _flow.Waiting != null;
            if (!_flow.TryRelease(signal)) return false;

            // A loop release is picked up by the next Step, a wait resumes immediately.
            if (wasWaiting)
                ResumePlayback();
            return true;
        }

        // Jumps to the end firing every event on the way, for cutscene skipping; the wrap mode then applies as if the end was reached.
        public void Skip()
        {
            if (timeline == null) return;
            if (!_active) Play();

            MoveTo(Duration);
            Finish(wrapMode == HonamiDirectorWrapMode.Loop ? HonamiDirectorWrapMode.None : wrapMode);
        }

        // Manual stepping still honours Wait markers: time does not move until Continue releases the wait.
        public void Advance(float deltaTime)
        {
            if (timeline == null) return;
            if (!_active) StartPlayback(_time, _time > 0f);
            if (_flow.Waiting != null) return;
            Step(deltaTime);
        }

        public void Evaluate()
        {
            if (timeline == null) return;
            EnsureRuntimes();
            Evaluate(_time, _time, true);
        }

        public void RebuildRuntimes() => _runtimesDirty = true;

        public UnityEngine.Object GetBinding(HonamiTimelineModule module)
        {
            if (module == null) return null;

            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].ModuleId == module.Id)
                    return bindings[i].Target;
            }
            return null;
        }

        public void SetBinding(HonamiTimelineModule module, UnityEngine.Object target)
        {
            if (module == null) return;

            for (int i = bindings.Count - 1; i >= 0; i--)
            {
                if (bindings[i].ModuleId == module.Id)
                    bindings.RemoveAt(i);
            }

            if (target != null)
                bindings.Add(new HonamiTimelineBinding(module.Id, target));

            _runtimesDirty = true;
        }

        public UnityEngine.Object GetReference(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < references.Count; i++)
            {
                var entry = references[i];
                if (entry != null && entry.key == key)
                    return entry.target;
            }
            return null;
        }

        public void SetReference(string key, UnityEngine.Object value)
        {
            if (string.IsNullOrEmpty(key)) return;
            for (int i = references.Count - 1; i >= 0; i--)
            {
                if (references[i] == null || references[i].key == key)
                    references.RemoveAt(i);
            }
            if (value != null)
                references.Add(new HonamiTimelineBindingEntry { key = key, target = value });
        }

        public UnityEngine.Object Resolve(HonamiSceneReference reference, Type type) =>
            reference.IsAssigned ? HonamiTimelineModuleBinding.Convert(GetReference(reference.Key), type) : null;

        private void Step(float delta)
        {
            EnsureRuntimes();

            if (delta <= 0f)
            {
                MoveTo(Mathf.Max(0f, _time + delta));
                return;
            }

            float duration = Duration;
            float remaining = delta;
            for (int guard = 0; remaining > 0f && guard < MaxStepsPerAdvance; guard++)
            {
                _flow.EnsureLoop(timeline.Markers, _time);
                if (_flow.TryApplyLoopRelease()) continue;

                float target = _time + remaining;
                var wait = HonamiDirectorFlow.FindWaitCrossed(timeline.Markers, _time, target);
                if (wait != null) target = Mathf.Min(target, wait.time);

                target = Mathf.Min(target, _flow.Loop != null ? _flow.Loop.endTime : duration);

                remaining -= Mathf.Max(0f, target - _time);
                MoveTo(target);

                if (wait != null && Mathf.Approximately(_time, wait.time))
                {
                    EnterWait(wait);
                    return;
                }

                var loop = _flow.Loop;
                if (loop != null && Mathf.Approximately(_time, loop.endTime))
                {
                    RestartAt(loop.time, loop.time > RegionRestartMargin ? loop.time - RegionRestartMargin : float.NegativeInfinity);
                    RegionLooped?.Invoke(this, loop);
                    continue;
                }

                if (_time >= duration)
                {
                    if (wrapMode == HonamiDirectorWrapMode.Loop && duration > 0f)
                    {
                        RestartAt(Mathf.Repeat(_time, duration));
                        Looped?.Invoke(this);
                        continue;
                    }
                    Finish(wrapMode);
                    return;
                }

                // No boundary was reached, therefore all requested time was consumed.
                return;
            }
        }

        private void Finish(HonamiDirectorWrapMode mode)
        {
            _playing = false;
            _flow.Clear();
            if (mode == HonamiDirectorWrapMode.Hold)
            {
                Finished?.Invoke(this);
                return;
            }

            Deactivate();
            Finished?.Invoke(this);
            RaiseStopped();
        }

        private void MoveTo(float time)
        {
            float previous = _time;
            _time = time;
            Evaluate(previous, time, false);
        }

        // Re-enters from -infinity so events on the first frame of the loop fire again.
        private void RestartAt(float time, float previous = float.NegativeInfinity)
        {
            _time = time;
            Evaluate(previous, time, false);
        }

        private void SuspendPlayback()
        {
            _playing = false;
            _modules?.PauseAll();
        }

        private void ResumePlayback()
        {
            _playing = true;
            _modules?.ResumeAll();
            Played?.Invoke(this);
        }

        private bool Deactivate()
        {
            if (!_active) return false;
            NotifyStop();
            SetActive(false);
            return true;
        }

        // Raised before the first frame is evaluated, so reactions can prepare the scene the cutscene opens on.
        private void Begin()
        {
            if (_active) return;
            SetActive(true);
            Started?.Invoke(this);
            AnyStarted?.Invoke(this);
        }

        private void SetActive(bool active)
        {
            if (_active == active) return;
            _active = active;
            if (active)
                _activeDirectors.Add(this);
            else
                _activeDirectors.Remove(this);
        }

        private void RaiseStopped()
        {
            Stopped?.Invoke(this);
            AnyStopped?.Invoke(this);
        }

        private void ReleaseRuntimes()
        {
            _modules?.Dispose();
            _modules = null;
            _runtimesDirty = true;
        }

        private void EnsureRuntimes()
        {
            if (!_runtimesDirty && _builtTimeline == timeline && timeline != null && _builtRevision == timeline.ModuleRevision)
                return;

            _builtTimeline = timeline;
            _builtRevision = timeline != null ? timeline.ModuleRevision : -1;
            _runtimesDirty = false;

            _modules?.Dispose();
            if (timeline == null)
            {
                _modules = null;
                return;
            }

            var modules = timeline.Modules;
            _modules = new HonamiTimelineModuleSet(modules, null, timeline.ModuleRevision);
            for (int i = 0; i < modules.Count; i++)
                CreateModuleRuntime(i, modules[i]);
        }

        private void CreateModuleRuntime(int index, HonamiTimelineModule module)
        {
            if (module == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[Honami] Timeline module at index {index} in '{timeline.name}' on '{name}' is an empty slot: the list references a module sub-asset that no longer exists. Remove the null entry from the timeline's modules list.", this);
#else
                Debug.LogError($"[Honami] Timeline module at index {index} in '{timeline.name}' on '{name}' could not be deserialized (null). Either the slot is empty in the asset, or the module class was stripped by Unity Linker; ensure it has [UnityEngine.Scripting.Preserve].", this);
#endif
                return;
            }

            if (!module.SupportsHost(HonamiTimelineModuleHost.Timeline)) return;

            string where = $"timeline '{timeline.name}' on '{name}'";
            if (_modules.IsMissingParent(index))
            {
                HonamiTimelineModuleBinding.ReportProblem(module, "its parent item is gone", where, this);
                return;
            }

            var binding = GetBinding(module);
            HonamiTimelineModuleBinding.ReportProblem(module,
                module.GetBindingProblem(HonamiTimelineModuleHost.Timeline, binding, gameObject), where, this);
            var context = new HonamiTimelineModuleContext(HonamiTimelineModuleHost.Timeline, gameObject, binding, this, null, null);
            _modules.Runtimes[index] = module.CreateRuntime(in context);
        }

        private void Evaluate(float previousTime, float time, bool isSeek)
        {
            bool hold = wrapMode == HonamiDirectorWrapMode.Hold;
            var frame = new HonamiTimelineFrame(previousTime, time, isSeek, hold);
            _modules?.Advance(in frame, null, null, _active);
        }

        private void NotifyStop() => _modules?.StopAll();

        private void EnterWait(HonamiTimelineWaitMarker wait)
        {
            _flow.EnterWait(wait);
            SuspendPlayback();
            Waiting?.Invoke(this, wait);
        }

        private void EnterWaitAtCurrentTime()
        {
            if (timeline == null) return;
            var wait = HonamiDirectorFlow.FindWaitAt(timeline.Markers, _time);
            if (wait != null) EnterWait(wait);
        }
    }
}
