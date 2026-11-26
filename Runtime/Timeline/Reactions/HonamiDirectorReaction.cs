using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    // Listens from Awake to OnDestroy rather than OnEnable/OnDisable, so a reaction can switch off its own GameObject and still hear the cutscene end.
    public abstract class HonamiDirectorReaction : MonoBehaviour
    {
        [Tooltip("The Director to react to. Leave empty to react while any Director in the loaded scenes is running.")]
        [SerializeField] private HonamiDirector director;
        [Tooltip("Seconds to wait before OnDirectorStarted fires. If the director stops during this window, neither callback runs.")]
        [SerializeField] private float startDelay;

        private readonly List<HonamiDirector> _running = new();
        private HonamiDirector _lastRunning;
        private bool _anyDirector;
        private int _startTicket;
        private bool _startedFired;

        public bool IsReacting { get; private set; }
        public bool ReactsToAnyDirector => _anyDirector;
        public bool IsDirectorRunning => _running.Count > 0;
        public IReadOnlyList<HonamiDirector> RunningDirectors => _running;

        public HonamiDirector Director
        {
            get => director;
            set
            {
                director = value;
                _anyDirector = value == null;
                RefreshDirectors();
            }
        }

        protected virtual void Awake()
        {
            _anyDirector = director == null;
            HonamiDirector.AnyStarted += HandleDirectorStarted;
            HonamiDirector.AnyStopped += HandleDirectorStopped;
        }

        protected virtual void OnEnable() => RefreshDirectors();

        protected virtual void OnValidate() => _anyDirector = director == null;

        protected virtual void OnDestroy()
        {
            CancelPendingStart();
            HonamiDirector.AnyStarted -= HandleDirectorStarted;
            HonamiDirector.AnyStopped -= HandleDirectorStopped;
        }

        protected virtual bool Accepts(HonamiDirector candidate) => _anyDirector || candidate == director;

        protected abstract void OnDirectorStarted(HonamiDirector director);
        protected abstract void OnDirectorStopped(HonamiDirector director);

        // Call when whatever Accepts depends on has changed, so the running set is rebuilt against it.
        public void RefreshDirectors()
        {
            _running.Clear();
            var active = HonamiDirector.ActiveDirectors;
            for (int i = 0; i < active.Count; i++)
            {
                var candidate = active[i];
                if (candidate != null && Accepts(candidate))
                    _running.Add(candidate);
            }

            UpdateReaction(_running.Count > 0 ? _running[0] : _lastRunning);
        }

        private void HandleDirectorStarted(HonamiDirector started)
        {
            if (_running.Contains(started) || !Accepts(started)) return;
            _running.Add(started);
            UpdateReaction(started);
        }

        private void HandleDirectorStopped(HonamiDirector stopped)
        {
            if (!_running.Remove(stopped)) return;
            UpdateReaction(stopped);
        }

        // Unticking the component stops new reactions, but one already running still gets its OnDirectorStopped.
        private void UpdateReaction(HonamiDirector cause)
        {
            bool running = _running.Count > 0;
            if (running)
                _lastRunning = _running[_running.Count - 1];
            if (running == IsReacting) return;

            if (running)
            {
                if (!enabled) return;
                IsReacting = true;

                if (startDelay > 0f)
                {
                    _startedFired = false;
                    DelayedStart(cause, ++_startTicket);
                }
                else
                {
                    _startedFired = true;
                    OnDirectorStarted(cause);
                }
                return;
            }

            CancelPendingStart();
            if (_startedFired)
            {
                _startedFired = false;
                OnDirectorStopped(cause);
            }
            IsReacting = false;
            _lastRunning = null;
        }

        // Keeps counting while the GameObject is inactive, so a reaction that hides itself still gets its start.
        private async void DelayedStart(HonamiDirector cause, int ticket)
        {
            float remaining = startDelay;
            while (remaining > 0f)
            {
                await Awaitable.NextFrameAsync();
                if (ticket != _startTicket || !this) return;
                remaining -= Time.deltaTime;
            }

            _startedFired = true;
            OnDirectorStarted(cause);
        }

        private void CancelPendingStart() => _startTicket++;
    }
}
