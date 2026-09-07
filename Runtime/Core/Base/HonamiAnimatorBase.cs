using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Riggings;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Defines when the animator should build and start its playable graph automatically.
    /// </summary>
    public enum HonamiAnimationStartup
    {
        Start,
        Enable
    }

    /// <summary>
    /// Defines which Unity update loop drives the Honami playable graph.
    /// </summary>
    public enum HonamiUpdateMode
    {
        Normal,
        AnimatePhysics,
        UnscaledTime,
        LateUpdate,
        Manual
    }

    public enum HonamiGlobalWeightMode
    {
        Init,
        Bind
    }

    [RequireComponent(typeof(Animator))]
    public abstract partial class HonamiAnimatorBase : MonoBehaviour
    {
        /// <summary>
        /// Converts a parameter, state, or event name into Honami's stable runtime hash.
        /// </summary>
        public static int StringToHash(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }

            unchecked
            {
                int hash = 5381;
                for (int i = 0; i < name.Length; i++)
                {
                    hash = ((hash << 5) + hash) + name[i];
                }

                return hash;
            }
        }

        [SerializeField] protected HonamiAnimationStartup startup = HonamiAnimationStartup.Enable;

        [Header("Initial Pose")]
        [SerializeField, Tooltip("Captures the hierarchy pose before Honami starts evaluating. Used as a safe fallback when nothing is playing.")]
        protected bool captureInitialPoseOnAwake = true;

        [SerializeField, Tooltip("When nothing is playing, restore the captured initial pose instead of keeping the last sampled animation frame.")]
        protected bool restoreInitialPoseWhenIdle = true;

        [SerializeField, Tooltip("Include this GameObject's transform in the initial pose snapshot. Disabled by default to avoid moving character roots.")]
        protected bool includeRootTransformInInitialPose = false;

        [Header("Time Settings")]
        [SerializeField] protected HonamiUpdateMode updateMode = HonamiUpdateMode.Normal;

        [SerializeField, Range(0, 10f)] protected float timeScale = 1f;

        [SerializeField] protected bool fpsCap = false;

        [SerializeField, Range(1, 120)] protected int targetFPS = 30;

        [SerializeField] protected bool fpsCapInterpolate = true;

        private double _fpsAccumulator = 0.0;

        [Header("Animator Synchronization")]
        [SerializeField] protected bool applyRootMotion = false;

        [SerializeField] protected AnimatorCullingMode cullingMode = AnimatorCullingMode.AlwaysAnimate;

        [Header("Linked System")]
        public bool preventLinking = false;

        public HonamiTagID linkingTag;

        [SerializeField] protected HonamiGlobalWeightMode globalWeightMode = HonamiGlobalWeightMode.Init;

        internal HonamiLinkedAnimator _linkedBrain;

        internal Animator _animator;
        internal PlayableGraph _playableGraph;
        internal AnimationPlayableOutput _playableOutput;
        internal HonamiRiggingProcessor _riggingProcessor;

        internal double _cachedDeltaTime;
        protected bool _isPaused;
        private bool _needsCatchUpTick;
        protected bool _keepPoseOnDisable;

        private float _globalWeight = 1f;

        private struct PendingAction
        {
            public HonamiActionID actionId;
            public float transitionDuration;
            public float remainingDelay;
        }

        private readonly List<PendingAction> _pendingActions = new();
        private readonly List<PendingAction> _dueActions = new();

        public Animator UnityAnimator => _animator;
        public bool HasPlayableGraph => _playableGraph.IsValid();
        public bool IsPaused => _isPaused;

        public float TimeScale
        {
            get => timeScale;
            set => timeScale = Mathf.Max(0f, value);
        }

        public bool FpsCap
        {
            get => fpsCap;
            set
            {
                fpsCap = value;
                ResetFpsCapState();
            }
        }

        public int TargetFPS
        {
            get => targetFPS;
            set => targetFPS = Mathf.Clamp(value, 1, 120);
        }

        public HonamiGlobalWeightMode GlobalWeightMode
        {
            get => globalWeightMode;
            set
            {
                if (globalWeightMode == value) return;
                globalWeightMode = value;
                if (_playableOutput.IsOutputValid())
                    _playableOutput.SetWeight(globalWeightMode == HonamiGlobalWeightMode.Bind ? _globalWeight : 1f);
            }
        }

        public float GlobalWeight
        {
            get => _globalWeight;
            set
            {
                _globalWeight = Mathf.Clamp01(value);
                if (globalWeightMode == HonamiGlobalWeightMode.Bind && _playableOutput.IsOutputValid())
                    _playableOutput.SetWeight(_globalWeight);
            }
        }

        protected virtual void Awake()
        {
            TryGetComponent<Animator>(out _animator);
            _animator.keepAnimatorStateOnDisable = true;
            _animator.applyRootMotion = applyRootMotion;
            _animator.cullingMode = cullingMode;
            if (captureInitialPoseOnAwake) CaptureInitialPose();
        }

        protected virtual void OnEnable()
        {
            if (_animator != null) _animator.enabled = true;
            _needsCatchUpTick = true;
        }

        protected virtual void OnDisable()
        {
            _needsCatchUpTick = false;
            ResetFpsCapState();
            CancelPendingActions();
            HonamiLinkedAction.UnregisterAll(this);
        }

        protected virtual void OnDestroy()
        {
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
        }

        protected virtual void Update()
        {
            if (updateMode == HonamiUpdateMode.Normal)
            {
                _needsCatchUpTick = false;
                TickWithFpsCap(Time.deltaTime);
            }
            else if (updateMode == HonamiUpdateMode.UnscaledTime)
            {
                _needsCatchUpTick = false;
                TickWithFpsCap(Time.unscaledDeltaTime);
            }
        }

        protected virtual void LateUpdate()
        {
            if (updateMode == HonamiUpdateMode.LateUpdate)
            {
                _needsCatchUpTick = false;
                TickWithFpsCap(Time.deltaTime);
                return;
            }

            RunCatchUpTick();
        }

        protected virtual void FixedUpdate()
        {
            if (updateMode == HonamiUpdateMode.AnimatePhysics)
            {
                _needsCatchUpTick = false;
                TickWithFpsCap(Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Queues a zero-delta evaluation for the current frame's LateUpdate.
        /// Used after the graph is rebuilt mid-frame so the animator does not hold a stale pose until the next Update.
        /// </summary>
        public void RequestCatchUpTick()
        {
            if (updateMode == HonamiUpdateMode.Manual) return;
            _needsCatchUpTick = true;
        }

        private void RunCatchUpTick()
        {
            if (!_needsCatchUpTick || updateMode == HonamiUpdateMode.Manual) return;

            _needsCatchUpTick = false;
            Tick(0.0);
            InvalidateFpsCapInterpolation();
        }

        public abstract void Tick(double deltaTime);

        protected abstract bool IsGraphIdle();

        public void Play(string name) => Play(name, 0f);

        public abstract void Play(string name, float transitionDuration);

        public abstract void StopAll();

        public void Stop() => StopAll();

        public abstract bool IsPlaying(string name);

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;
        public void StopUpdates() => Pause();
        public void ResumeUpdates() => Resume();

        public void StopAndKeepPose()
        {
            _keepPoseOnDisable = true;
            if (_animator != null) _animator.enabled = false;
            enabled = false;
        }

        public void ReactToAction(HonamiActionID actionId) => ReactToAction(actionId, 0.25f);

        public abstract void ReactToAction(HonamiActionID actionId, float transitionDuration);

        public void ReactToAction(HonamiActionID actionId, float transitionDuration, float delay)
        {
            if (actionId == null) return;

            if (delay <= 0f)
            {
                ReactToAction(actionId, transitionDuration);
                return;
            }

            _pendingActions.Add(new PendingAction
            {
                actionId = actionId,
                transitionDuration = transitionDuration,
                remainingDelay = delay
            });
        }

        public void CancelPendingActions()
        {
            _pendingActions.Clear();
            _dueActions.Clear();
        }

        public int PendingActionCount => _pendingActions.Count;

        protected void UpdatePendingActions(float deltaTime)
        {
            if (_pendingActions.Count == 0) return;

            _dueActions.Clear();

            for (int i = _pendingActions.Count - 1; i >= 0; i--)
            {
                var pending = _pendingActions[i];
                pending.remainingDelay -= deltaTime;

                if (pending.remainingDelay > 0f)
                {
                    _pendingActions[i] = pending;
                    continue;
                }

                _dueActions.Add(pending);
                _pendingActions.RemoveAt(i);
            }

            // Filled back-to-front, and fired outside the loop so a reaction can queue another.
            for (int i = _dueActions.Count - 1; i >= 0; i--)
            {
                ReactToAction(_dueActions[i].actionId, _dueActions[i].transitionDuration);
            }
        }

        protected void PrepareRigs()
        {
            if (_riggingProcessor != null)
                _riggingProcessor.PrepareAllRigs((float)_cachedDeltaTime);
        }

        protected void ProcessLegacyRigs()
        {
            if (_riggingProcessor != null)
                _riggingProcessor.ProcessLegacyRigs((float)_cachedDeltaTime);
        }

        internal void InsertRigChain()
        {
            if (!TryGetComponent<HonamiRiggingProcessor>(out var rigProcessor))
            {
                return;
            }

            var output = _playableGraph.GetOutput(0);
            if (!output.IsPlayableOutputOfType<AnimationPlayableOutput>())
            {
                return;
            }

            Playable currentSource = output.GetSourcePlayable();
            Playable rigChainEnd = rigProcessor.InsertIntoGraph(_animator, _playableGraph, currentSource);

            if (rigChainEnd.IsValid() && !rigChainEnd.Equals(currentSource))
            {
                output.SetSourcePlayable(rigChainEnd);
            }

            _riggingProcessor = rigProcessor;
        }
    }
}
