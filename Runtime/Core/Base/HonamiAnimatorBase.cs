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

        [SerializeField, Tooltip("Let Unity evaluate this animator together with all others in its multithreaded animation pass. Applies in Normal and UnscaledTime without FPS Cap, for animators that support it.")]
        protected bool batchedEvaluation = true;

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

        // protected internal so animators written outside this assembly can build and drive their own graph.
        protected internal Animator _animator;
        protected internal PlayableGraph _playableGraph;
        protected internal AnimationPlayableOutput _playableOutput;
        protected HonamiRiggingProcessor _riggingProcessor;

        protected internal double _cachedDeltaTime;
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
            set
            {
                timeScale = Mathf.Max(0f, value);
                SyncGraphTimeMode();
            }
        }

        private float _speedMultiplier = 1f;

        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set
            {
                _speedMultiplier = Mathf.Max(0f, value);
                SyncGraphTimeMode();
            }
        }

        protected float EffectiveTimeScale => timeScale * _speedMultiplier;

        public bool FpsCap
        {
            get => fpsCap;
            set
            {
                fpsCap = value;
                ResetFpsCapState();
                SyncGraphTimeMode();
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

            OnBeforeGraphBuild();
            BuildGraph();
            RegisterLinkedActions();
        }

        protected virtual void OnEnable()
        {
            if (_animator != null) _animator.enabled = true;
            _needsCatchUpTick = true;

            EnsureGraphBuilt();
            if (!HasPlayableGraph) return;

            RegisterLinkedActions();

            if (startup == HonamiAnimationStartup.Enable && !_keepPoseOnDisable)
            {
                RunStartupSequence(resetPlaybackFirst: true);
            }

            _keepPoseOnDisable = false;
            SyncGraphTimeMode();
            _playableGraph.Play();
        }

        protected virtual void Start()
        {
            if (startup != HonamiAnimationStartup.Start) return;

            EnsureGraphBuilt();
            if (!HasPlayableGraph) return;

            RegisterLinkedActions();
            RunStartupSequence(resetPlaybackFirst: false);
            SyncGraphTimeMode();
            _playableGraph.Play();
        }

        protected virtual void OnDisable()
        {
            CancelDeferredEvaluation();

            if (!_keepPoseOnDisable && HasPlayableGraph)
            {
                ResetPlaybackForShutdown();
                _playableGraph.Evaluate(0f);
                RestoreInitialPoseIfIdle();
                _playableGraph.Stop();
            }

            if (_playableGraph.IsValid()) _playableGraph.Destroy();

            _needsCatchUpTick = false;
            ResetFpsCapState();
            CancelPendingActions();
            HonamiLinkedAction.UnregisterAll(this);
        }

        protected virtual void OnDestroy()
        {
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
        }

        /// <summary>
        /// Builds this animator's playable graph. Start from <see cref="EnsureGraphCreated"/>, create the
        /// root playable on the graph it returns, hand it to the output, then call <see cref="InsertRigChain"/>.
        /// Implementations must leave <see cref="HasPlayableGraph"/> false when there is nothing to play.
        /// </summary>
        protected abstract void BuildGraph();

        /// <summary>
        /// Name given to this animator's playable graph, as it appears in the Playable Graph Visualizer.
        /// </summary>
        protected virtual string GraphName => gameObject.name + "_HonamiGraph";

        /// <summary>
        /// Creates the playable graph and its animation output if they do not exist yet, and seeds the
        /// output weight from the current Global Weight Mode. Idempotent, so a rebuild that keeps the
        /// existing graph can call it unconditionally. Returns an invalid graph when there is no Animator
        /// to drive - check <see cref="PlayableGraph.IsValid"/> before creating playables on it.
        /// </summary>
        protected internal PlayableGraph EnsureGraphCreated()
        {
            if (_playableGraph.IsValid()) return _playableGraph;
            if (_animator == null && !TryGetComponent(out _animator)) return default;

            _playableGraph = PlayableGraph.Create(GraphName);
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _graphTimeMode = DirectorUpdateMode.Manual;

            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "AnimationOutput", _animator);
            // Seeding this here is what makes a serialized GlobalWeight take effect in Bind mode
            // without waiting for the property to be assigned again.
            _playableOutput.SetWeight(globalWeightMode == HonamiGlobalWeightMode.Bind ? GlobalWeight : 1f);

            return _playableGraph;
        }

        /// <summary>
        /// Clears all playback so the next startup sequence begins from a known-empty state.
        /// </summary>
        protected abstract void ResetPlaybackState();

        /// <summary>
        /// Starts whatever this animator plays by default (default states, default clip, ...).
        /// </summary>
        protected abstract void PlayStartupContent();

        /// <summary>
        /// Re-registers this animator against every <see cref="HonamiActionID"/> it can react to.
        /// </summary>
        protected abstract void RegisterLinkedActions();

        /// <summary>
        /// Runs once in Awake before the first graph build, for subclass state the builder depends on.
        /// </summary>
        protected virtual void OnBeforeGraphBuild()
        {
        }

        /// <summary>
        /// Runs after startup content is selected but before the graph is evaluated for the first frame.
        /// </summary>
        protected virtual void OnBeforeStartupEvaluate()
        {
        }

        /// <summary>
        /// Teardown counterpart of <see cref="ResetPlaybackState"/>. Defaults to the same reset.
        /// </summary>
        protected virtual void ResetPlaybackForShutdown() => ResetPlaybackState();

        public void EnsureGraphBuilt()
        {
            if (!HasPlayableGraph) BuildGraph();
        }

        private void RunStartupSequence(bool resetPlaybackFirst)
        {
            if (resetPlaybackFirst) ResetPlaybackState();

            PlayStartupContent();
            OnBeforeStartupEvaluate();

            _playableGraph.Evaluate(0f);
            RestoreInitialPoseIfIdle();
            ApplyGlobalWeightBlend();
        }

        /// <summary>
        /// Shared tail of every <see cref="Tick"/>: rig preparation, graph evaluation, idle pose fallback,
        /// non-playable rigs, the global weight blend, then <see cref="OnGraphEvaluated"/>. When Unity
        /// batch-evaluates the graph, everything after rig preparation runs right after Unity's animation pass.
        /// </summary>
        protected void EvaluateGraph(float deltaTime)
        {
            if (!HasPlayableGraph) return;

            if (IsClipTrackAttached)
            {
                DeferToClipTracks(deltaTime);
                return;
            }

            PrepareRigs();

            if (_deferEvaluation)
            {
                if (_evaluationDeferred) return;
                _evaluationDeferred = true;
                HonamiDirectorEvaluation.Defer(this);
                return;
            }

            _playableGraph.Evaluate(deltaTime);
            CompleteEvaluation();
        }

        private void CompleteEvaluation()
        {
            RestoreInitialPoseIfIdle();
            ProcessPostEvaluationRigs();
            ApplyGlobalWeightBlend();
            OnGraphEvaluated();
        }

        protected virtual void OnGraphEvaluated()
        {
        }

        protected virtual void Update()
        {
            SyncGraphTimeMode();

            if (updateMode == HonamiUpdateMode.Normal)
            {
                _needsCatchUpTick = false;
                TickFromUpdate(Time.deltaTime);
            }
            else if (updateMode == HonamiUpdateMode.UnscaledTime)
            {
                _needsCatchUpTick = false;
                TickFromUpdate(Time.unscaledDeltaTime);
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

        public void Pause()
        {
            _isPaused = true;
            SyncGraphTimeMode();
        }

        public void Resume()
        {
            _isPaused = false;
            SyncGraphTimeMode();
        }

        public void StopAndKeepPose()
        {
            _keepPoseOnDisable = true;
            if (_animator != null) _animator.enabled = false;
            enabled = false;
        }

        /// <summary>
        /// The rigging processor driving this animator, or null when the GameObject has none.
        /// </summary>
        public HonamiRiggingProcessor RiggingProcessor => _riggingProcessor;

        /// <summary>
        /// The Linked Animator brain currently owning this animator, or null when it is unlinked.
        /// </summary>
        public HonamiLinkedAnimator LinkedBrain => _linkedBrain;

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

        protected void ProcessPostEvaluationRigs()
        {
            if (_riggingProcessor != null)
                _riggingProcessor.ProcessPostEvaluationRigs((float)_cachedDeltaTime);
        }

        /// <summary>
        /// Splices this GameObject's <see cref="HonamiRiggingProcessor"/> into the built graph.
        /// Call this at the end of <see cref="BuildGraph"/>, once the output has a source playable.
        /// </summary>
        protected internal void InsertRigChain()
        {
            AttachClipTrackMixer();

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
