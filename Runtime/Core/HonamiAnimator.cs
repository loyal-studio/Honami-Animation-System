
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Events;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Collections;
using HonamiAnimationSystem.Runtime.Riggings;

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
    /// Defines how the outgoing controller is evaluated while a controller transition is active.
    /// </summary>
    public enum HonamiControllerTransitionMode
    {
        ContinueEvaluating,
        Freeze
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

    /// <summary>
    /// Runtime component that replaces Unity Animator Controller playback with Honami's graph, parameter, event, avatar, and rigging pipeline.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Honami Animation/Honami Animator")]
    public partial class HonamiAnimator : MonoBehaviour
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

        [Header("Configuration")]
        [SerializeField] internal HonamiRuntimeController controller;
        internal HonamiRuntimeController _lastBuiltController;
        
        [SerializeField] private HonamiAnimationStartup startup = HonamiAnimationStartup.Enable;

        [Header("Initial Pose")]
        [SerializeField, Tooltip("Captures the hierarchy pose before Honami starts evaluating. Used as a safe fallback when no state is active.")]
        private bool captureInitialPoseOnAwake = true;
        
        [SerializeField, Tooltip("If true, captures the initial pose from the final frame of the default state instead of the Awake pose.")]
        private bool captureFromDefaultStateEnd = false;

        [SerializeField, Tooltip("When no state is active, restore the captured initial pose instead of keeping the last sampled animation frame.")]
        private bool restoreInitialPoseWhenIdle = true;

        [SerializeField, Tooltip("When a non-loop state finishes on a layer without a default state, release it so Initial Pose becomes the fallback instead of freezing on the final frame.")]
        private bool releaseFinishedStatesWithoutDefault = true;

        [SerializeField, Tooltip("Include this GameObject's transform in the initial pose snapshot. Disabled by default to avoid moving character roots.")]
        private bool includeRootTransformInInitialPose = false;
        
        [Header("Time Settings")]
        [SerializeField] private HonamiUpdateMode updateMode = HonamiUpdateMode.Normal;
        
        [SerializeField, Range(0, 10f)] private float timeScale = 1f;

        [SerializeField] private bool fpsCap = false;

        [SerializeField, Range(1, 120)] private int targetFPS = 30;

        [SerializeField] private bool fpsCapInterpolate = true;

        private double _fpsAccumulator = 0.0;

        [Header("Animator Synchronization")]
        [SerializeField] private bool applyRootMotion = false;
        
        [SerializeField] private AnimatorCullingMode cullingMode = AnimatorCullingMode.AlwaysAnimate;

        [Header("Linked System")]
        public bool preventLinking = false;
        
        public HonamiTagID linkingTag;

        [Header("Avatar")]
        [SerializeField] internal HonamiAvatar avatar;
        public HonamiAvatar Avatar => avatar;
        
        [SerializeField] private bool _mirrorAvatar = false;

        [SerializeField] private float _mirrorBlendSpeed = 0f;

        public bool MirrorAvatar => _mirrorAvatar;
        public float MirrorBlendSpeed => _mirrorBlendSpeed;

        private float _globalWeight = 1f;
        public float GlobalWeight
        {
            get => _globalWeight;
            set
            {
                _globalWeight = Mathf.Clamp01(value);
                if (_playableOutput.IsOutputValid())
                {
                    _playableOutput.SetWeight(_globalWeight);
                }
            }
        }

        /// <summary>
        /// Enables or disables global avatar mirroring for all evaluated states.
        /// </summary>
        public void SetGlobalMirror(bool active) => _mirrorAvatar = active;

        /// <summary>
        /// Sets the blend speed used when global avatar mirroring changes.
        /// </summary>
        public void SetGlobalMirrorSpeed(float speed) => _mirrorBlendSpeed = Mathf.Max(0f, speed);

        internal float _currentGlobalMirrorWeight = 0f;

        internal HonamiLinkedAnimator _linkedBrain;

        internal Animator _animator;
        internal PlayableGraph _playableGraph;
        internal AnimationLayerMixerPlayable _layerMixer;
        internal AnimationPlayableOutput _playableOutput;
        internal AnimationScriptPlayable _globalMirrorPlayable;

        internal readonly List<AnimationMixerPlayable> _layerMixers = new();

        public HonamiLayerState[] _layerStates;
        public HonamiPortState[][] _portStates;
        internal AnimationCurve[] _activeTransitionCurve;
        internal AnimationCurve[] _activeVictimCurve;
        internal HashSet<HonamiEventMarker>[][] _firedEventsPerPort;

        internal readonly Dictionary<int, int> _pickedRandomIdx = new();
        internal HashSet<int>[] _pausedStateIndices;

        internal readonly List<int> _anyStateIndices = new();
        internal readonly Dictionary<int, double> _repeaterLastFireTime = new();
        internal readonly Dictionary<int, int> _repeaterFireCount = new();
        private int[] _lastActiveStateIndex;
        internal List<int>[] _anyStateIndicesByLayer;

        internal readonly List<int> _tentativeBuffer = new();
        internal readonly List<int> _exitTentativeBuffer = new();
        internal NativeArray<float> _blendStateValues;
        internal int[] _blendParamIndices;
        internal int _pCountTotal;

        private struct PendingAction
        {
            public HonamiActionID actionId;
            public float transitionDuration;
            public float remainingDelay;
        }
        private readonly List<PendingAction> _pendingActions = new();

        internal bool _constraintsEnabled;

        internal int[] _defaultStateIndex;

        internal bool _isTransitioningController;
        internal float _controllerTransitionDuration;
        internal float _controllerTransitionTime;
        internal AnimationCurve _controllerTransitionCurve;
        internal AnimationMixerPlayable _controllerMixer;
        internal Playable _oldControllerPlayable;
        internal HonamiAvatarProcessor _oldAvatarProcessor;
        internal HonamiConstraintProcessor _oldConstraints;
        internal NativeArray<float> _oldBlendStateValues;

        internal int TransientPortIndex => _activeStatesCount;

        internal readonly Dictionary<int, int> _stateNameToIndex = new();
        internal readonly Dictionary<int, int> _stateGuidToIndex = new();

        internal readonly Dictionary<HonamiTransition, int> _bakedPortalTargets = new();
        internal readonly Dictionary<HonamiTransition, HonamiState> _bakedPortalExits = new();

        internal readonly Dictionary<HonamiCondition, int> _conditionParamHashes = new();
        internal readonly Dictionary<HonamiParameterAssignment, int> _assignmentParamHashes = new();
        internal int[] _blendTreeParamHashes;
        internal HonamiState[] _runtimeStates;
        internal int _activeStatesCount;
        internal HonamiLocalEventReceiver _localEventReceiver;

        internal readonly HonamiParameterStore _params = new();
        internal HonamiConstraintProcessor _constraints = new();
        internal HonamiAvatarProcessor _avatarProcessor = new();
        internal HonamiRiggingProcessor _riggingProcessor;

        internal bool _avatarEnabled;

        internal double _cachedDeltaTime;
        private bool _isPaused;
        private bool _keepPoseOnDisable;

        public event System.Action<string> OnStateEntered;
        public event System.Action<string> OnStateFinished;
        public event System.Action<HonamiStateExitInfo> OnStateExited;

        public HonamiRuntimeController CurrentController => controller;
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
        public HonamiParameterStore Parameters => _params;

        private void EnsureGraph()
        {
            if (!HasPlayableGraph && HasController)
                InitializeGraph();
        }

        private void Awake()
        {
            TryGetComponent<Animator>(out _animator);
            _animator.keepAnimatorStateOnDisable = true;
            _animator.applyRootMotion = applyRootMotion;
            _animator.cullingMode = cullingMode;
            TryGetComponent<HonamiLocalEventReceiver>(out _localEventReceiver);
            if (captureInitialPoseOnAwake) CaptureInitialPose();
            _params.Initialize(controller);
            InitializeGraph();
            RegisterLinkedActions();

        }

        private void OnEnable()
        {
            if (_animator != null) _animator.enabled = true;
            
            EnsureGraph();

            if (!HasPlayableGraph) return;

            RegisterLinkedActions();

            if (startup == HonamiAnimationStartup.Enable && !_keepPoseOnDisable)
            {
                ResetAnimatorState();
                PlayDefaultStates();
                if (captureFromDefaultStateEnd) CaptureInitialPoseFromDefaultState();
                _playableGraph.Evaluate(0f);
                RestoreInitialPoseIfIdle();
            }
            
            _keepPoseOnDisable = false;
            _playableGraph.Play();
        }

        private void Start()
        {
            if (startup != HonamiAnimationStartup.Start) return;

            EnsureGraph();

            if (!HasPlayableGraph) return;

            RegisterLinkedActions();
            PlayDefaultStates();
            if (captureFromDefaultStateEnd) CaptureInitialPoseFromDefaultState();
            _playableGraph.Evaluate(0f);
            RestoreInitialPoseIfIdle();
            _playableGraph.Play();
        }

        private void OnDisable()
        {
            ResetFpsCapState();
            
            if (!_keepPoseOnDisable)
            {
                CleanAnimator();
            }

            if (_playableGraph.IsValid()) 
                _playableGraph.Destroy();

            HonamiLinkedAction.UnregisterAll(this);
        }

        private bool CleanAnimator() 
        {
            if (!HasPlayableGraph) return false;
            ResetToDefault();
            _playableGraph.Evaluate(0f);
            RestoreInitialPoseIfIdle();
            _playableGraph.Stop();
            return true;
        }

        private void OnDestroy()
        {
            if (_isTransitioningController)
            {
                _oldAvatarProcessor?.Dispose();
                _oldConstraints?.Dispose();
                if (_oldBlendStateValues.IsCreated) _oldBlendStateValues.Dispose();
            }
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
            _avatarProcessor?.Dispose();
            _constraints?.Dispose();
            _params?.Dispose();
            if (_blendStateValues.IsCreated) _blendStateValues.Dispose();
        }

        private void Update()
        {
            if (updateMode == HonamiUpdateMode.Normal) TickWithFpsCap(Time.deltaTime);
            else if (updateMode == HonamiUpdateMode.UnscaledTime) TickWithFpsCap(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            if (updateMode == HonamiUpdateMode.LateUpdate) TickWithFpsCap(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (updateMode == HonamiUpdateMode.AnimatePhysics) TickWithFpsCap(Time.fixedDeltaTime);
        }



        public void Tick(double deltaTime)
        {
            if (_isPaused || !HasPlayableGraph) return;
            _cachedDeltaTime = deltaTime * timeScale;
            _params.UpdateRandomParameters();

            HonamiTransitionEngine.UpdateTransitions(this);
            UpdateStatesRuntimeProperties();
            ApplyConstraints();

            if (_isTransitioningController)
            {
                _controllerTransitionTime += (float)deltaTime;
                float progress = Mathf.Clamp01(_controllerTransitionTime / _controllerTransitionDuration);
                float weight = _controllerTransitionCurve != null ? _controllerTransitionCurve.Evaluate(progress) : progress;
                
                if (_controllerMixer.IsValid())
                {
                    _controllerMixer.SetInputWeight(0, 1f - weight);
                    _controllerMixer.SetInputWeight(1, weight);
                }

                if (progress >= 1f)
                {
                    _isTransitioningController = false;
                    if (_oldControllerPlayable.IsValid()) _playableGraph.DestroySubgraph(_oldControllerPlayable);
                    if (_controllerMixer.IsValid())
                    {
                        var newRoot = _controllerMixer.GetInput(1);
                        if (_playableOutput.IsOutputValid()) _playableOutput.SetSourcePlayable(newRoot);
                        _playableGraph.Disconnect(_controllerMixer, 1);
                        _controllerMixer.Destroy();
                    }
                    
                    _oldAvatarProcessor?.Dispose();
                    _oldConstraints?.Dispose();
                    if (_oldBlendStateValues.IsCreated) _oldBlendStateValues.Dispose();
                }
            }

            _params.ConsumePendingTriggers();
            _params.ClearTriggers();
            ApplyAvatarProcessors();

            UpdatePendingActions((float)deltaTime);

            PrepareRigs();
            _playableGraph.Evaluate((float)_cachedDeltaTime);
            RestoreInitialPoseIfIdle();
            ProcessLegacyRigs();

            EvaluateEvents();
        }

        public void SetFloat(string name, float value) { EnsureGraph(); _params.SetFloat(name, value); }
        public void SetFloat(int id, float value) { EnsureGraph(); _params.SetFloat(id, value); }
        public float GetFloat(string name) { EnsureGraph(); return _params.GetFloat(name); }
        public float GetFloat(int id) { EnsureGraph(); return _params.GetFloat(id); }

        public void SetInteger(string name, int value) { EnsureGraph(); _params.SetInteger(name, value); }
        public void SetInteger(int id, int value) { EnsureGraph(); _params.SetInteger(id, value); }
        public int GetInteger(string name) { EnsureGraph(); return _params.GetInteger(name); }
        public int GetInteger(int id) { EnsureGraph(); return _params.GetInteger(id); }

        public void SetBool(string name, bool value) { EnsureGraph(); _params.SetBool(name, value); }
        public void SetBool(int id, bool value) { EnsureGraph(); _params.SetBool(id, value); }
        public bool GetBool(string name) { EnsureGraph(); return _params.GetBool(name); }
        public bool GetBool(int id) { EnsureGraph(); return _params.GetBool(id); }

        public void SetTrigger(string name) { EnsureGraph(); _params.SetTrigger(name); }
        public void SetTrigger(int id) { EnsureGraph(); _params.SetTrigger(id); }
        public void ResetTrigger(string name) { EnsureGraph(); _params.ResetTrigger(name); }
        public void ResetTrigger(int id) { EnsureGraph(); _params.ResetTrigger(id); }

        public bool HasTag(string tag, int layer = 0)
        {
            if (_runtimeStates == null || _layerStates == null || layer < 0 || layer >= _layerStates.Length) return false;
            int currentIdx = _layerStates[layer].CurrentStateIndex;
            if (currentIdx < 0 || currentIdx >= _activeStatesCount) return false;
            var state = _runtimeStates[currentIdx];
            return state != null && state.tags != null && state.tags.Contains(tag);
        }

        public void SetController(HonamiRuntimeController newController, float transitionDuration = 0f, AnimationCurve transitionCurve = null, HonamiControllerTransitionMode mode = HonamiControllerTransitionMode.ContinueEvaluating) 
        {
            if (transitionDuration > 0f && _playableGraph.IsValid()) 
            {
                if (_isTransitioningController)
                {
                    if (_oldControllerPlayable.IsValid()) _playableGraph.DestroySubgraph(_oldControllerPlayable);
                    if (_controllerMixer.IsValid())
                    {
                        var prevRoot = _controllerMixer.GetInput(1);
                        if (_playableOutput.IsOutputValid() && prevRoot.IsValid()) _playableOutput.SetSourcePlayable(prevRoot);
                        _playableGraph.Disconnect(_controllerMixer, 1);
                        _controllerMixer.Destroy();
                    }
                    _oldAvatarProcessor?.Dispose();
                    _oldConstraints?.Dispose();
                    if (_oldBlendStateValues.IsCreated) _oldBlendStateValues.Dispose();
                    _isTransitioningController = false;
                }

                NotifyActiveStatesExited(HonamiStateExitReason.ControllerChanged);
                HonamiLinkedAction.UnregisterAll(this);

                Playable oldRoot = _playableOutput.GetSourcePlayable();

                _oldAvatarProcessor = _avatarProcessor;
                _oldConstraints = _constraints;
                _oldBlendStateValues = _blendStateValues;

                _avatarProcessor = new HonamiAvatarProcessor();
                _constraints = new HonamiConstraintProcessor();
                _blendStateValues = default;

                controller = newController;
                _params.Initialize(controller);
                
                InitializeGraph(true, true);
                
                if (TryGetComponent<HonamiRiggingProcessor>(out var riggingProcessor))
                {
                    riggingProcessor.RefreshRigs();
                }

                RegisterLinkedActions();
                PlayDefaultStates();
                if (captureFromDefaultStateEnd) CaptureInitialPoseFromDefaultState();

                Playable newRoot = _playableOutput.GetSourcePlayable();

                _controllerMixer = AnimationMixerPlayable.Create(_playableGraph, 2);
                _playableGraph.Connect(oldRoot, 0, _controllerMixer, 0);
                _playableGraph.Connect(newRoot, 0, _controllerMixer, 1);
                
                _controllerMixer.SetInputWeight(0, 1f);
                _controllerMixer.SetInputWeight(1, 0f);

                if (mode == HonamiControllerTransitionMode.Freeze)
                {
                    oldRoot.SetSpeed(0f);
                }

                _playableOutput.SetSourcePlayable(_controllerMixer);

                _controllerTransitionDuration = transitionDuration;
                _controllerTransitionTime = 0f;
                _controllerTransitionCurve = transitionCurve;
                _oldControllerPlayable = oldRoot;
                _isTransitioningController = true;

                _playableGraph.Evaluate(0f);
                RestoreInitialPoseIfIdle();
                _playableGraph.Play();
            }
            else 
            {
                if (_isTransitioningController)
                {
                    _isTransitioningController = false;
                    _oldAvatarProcessor?.Dispose();
                    _oldConstraints?.Dispose();
                    if (_oldBlendStateValues.IsCreated) _oldBlendStateValues.Dispose();
                }

                NotifyActiveStatesExited(HonamiStateExitReason.ControllerChanged);
                HonamiLinkedAction.UnregisterAll(this);

                if (_playableGraph.IsValid())
                {
                    _playableGraph.Destroy();
                }

                controller = newController;
                _params.Initialize(controller);
                InitializeGraph(false, true);
                
                if (TryGetComponent<HonamiRiggingProcessor>(out var riggingProcessor))
                {
                    riggingProcessor.RefreshRigs();
                }

                if (HasPlayableGraph)
                {
                    RegisterLinkedActions();
                    _playableGraph.Play();
                    PlayDefaultStates();
                    if (captureFromDefaultStateEnd) CaptureInitialPoseFromDefaultState();
                    _playableGraph.Evaluate(0f);
                    RestoreInitialPoseIfIdle();
                }
            }
        }

        public void SetLayerWeight(int layer, float weight)
        {
            if (_layerMixer.IsValid() && layer >= 0 && layer < _layerMixer.GetInputCount())
                _layerMixer.SetInputWeight(layer, weight);
        }

        public bool RebakeAvatarMask(HonamiAvatarMask mask)
        {
            if (!_avatarEnabled) return false;
            return _avatarProcessor.RebakeMask(mask);
        }

        public void RebakeAllAvatarMasks()
        {
            if (!_avatarEnabled) return;
            _avatarProcessor.RebakeAllMasks();
        }

        public void RegisterAvatarMask(HonamiAvatarMask mask)
        {
            if (!_avatarEnabled) return;
            _avatarProcessor.RegisterMask(mask);
        }

        public void RefreshBoneReplacements()
        {
            if (_avatarEnabled)
            {
                if (_avatarProcessor != null) _avatarProcessor.RefreshReplacements(avatar);
                if (_oldAvatarProcessor != null) _oldAvatarProcessor.RefreshReplacements(avatar);
            }
        }

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

        public void PauseLayer(int layer, bool paused)
        {
            if (_layerStates != null && layer >= 0 && layer < _layerStates.Length) _layerStates[layer].IsLayerPaused = paused;
        }

        public bool IsLayerPaused(int layer) => _layerStates != null && layer >= 0 && layer < _layerStates.Length && _layerStates[layer].IsLayerPaused;

        public void PauseState(string stateName, bool paused, int layer = 0)
        {
            if (_stateNameToIndex.TryGetValue(stateName.GetHashCode(), out int idx))
                SetStatePause(idx, layer, paused);
        }

        public void PauseStateByGuid(string guid, bool paused, int layer = 0)
        {
            if (_stateGuidToIndex.TryGetValue(guid.GetHashCode(), out int idx))
                SetStatePause(idx, layer, paused);
        }

        private void SetStatePause(int stateIndex, int layer, bool paused)
        {
            if (!IsLayerInRange(layer)) return;
            if (paused) _pausedStateIndices[layer].Add(stateIndex);
            else        _pausedStateIndices[layer].Remove(stateIndex);

            if (!TryGetLayerMixer(layer, out AnimationMixerPlayable mixer)) return;
            if (stateIndex < 0 || stateIndex >= mixer.GetInputCount()) return;

            Playable playable = mixer.GetInput(stateIndex);
            if (!playable.IsValid()) return;

            playable.SetSpeed(paused ? 0f : GetConfiguredStateSpeed(stateIndex));
        }

        public void StopAll() => ResetAnimatorState();

        public void Stop(int layer)
        {
            if (!TryGetLayerMixer(layer, out AnimationMixerPlayable mixer)) return;

            ClearWeightedPorts(layer, mixer);

            _layerStates[layer].CurrentStateIndex = -1;
            _layerStates[layer].PreviousStateIndex = -1;

            _layerStates[layer].TransientStateIndex = -1;

            if (_layerMixer.IsValid() && layer < _layerMixer.GetInputCount())
                _layerMixer.SetInputWeight(layer, 0f);

            ResetLayerRuntimeState(layer);
        }

        private void ClearWeightedPorts(int layer, AnimationMixerPlayable mixer)
        {
            int portCount = mixer.GetInputCount();
            for (int i = 0; i < portCount; i++)
            {
                if (mixer.GetInputWeight(i) <= 0f) continue;

                mixer.SetInputWeight(i, 0f);
                TryTriggerStateExited(layer, i, HonamiStateExitReason.Stopped);
                if (i == TransientPortIndex) ClearTransientPort(layer);
                else ResetTime(i, layer);
            }
        }

        private void ResetLayerRuntimeState(int layer)
        {
            _layerStates[layer].TransitionDuration = 0.0;
            _layerStates[layer].TransitionTime = 0.0;
            _layerStates[layer].TransitionWeight = 0f;
            _layerStates[layer].TransitionStartPrevWeight = 0f;
            _layerStates[layer].TransitionStartLayerWeight = 0f;
            _layerStates[layer].IsLayerPaused = false;

            _activeTransitionCurve[layer] = null;
            _pausedStateIndices[layer].Clear();

            ResetPortRuntimeState(layer);
        }

        private void ResetPortRuntimeState(int layer)
        {
            if (_firedEventsPerPort != null && layer < _firedEventsPerPort.Length)
            {
                for (int port = 0; port < _firedEventsPerPort[layer].Length; port++)
                    _firedEventsPerPort[layer][port].Clear();
            }

            if (_portStates == null || layer >= _portStates.Length || _portStates[layer] == null) return;

            for (int port = 0; port < _portStates[layer].Length; port++)
            {
                _portStates[layer][port].CurrentLoopCount = 0;
                _portStates[layer][port].WeightSnapshot = 0f;
            }
        }

        public void StopState(string stateName, int layer = 0)
        {
            if (_stateNameToIndex.TryGetValue(stateName.GetHashCode(), out int idx))
                StopStateInternal(idx, layer);
        }

        public void StopStateByGuid(string guid, int layer = 0)
        {
            if (_stateGuidToIndex.TryGetValue(guid.GetHashCode(), out int idx))
                StopStateInternal(idx, layer);
        }

        private void StopStateInternal(int stateIndex, int layer)
        {
            if (!TryGetLayerMixer(layer, out AnimationMixerPlayable mixer)) return;
            if (stateIndex >= 0 && stateIndex < mixer.GetInputCount())
            {
                mixer.SetInputWeight(stateIndex, 0f);
                TryTriggerStateExited(layer, stateIndex, HonamiStateExitReason.Stopped);
                ResetTime(stateIndex, layer);
                if (stateIndex == _layerStates[layer].CurrentStateIndex) _layerStates[layer].CurrentStateIndex = -1;
                if (stateIndex == _layerStates[layer].PreviousStateIndex) _layerStates[layer].PreviousStateIndex = -1;
                _pausedStateIndices[layer].Remove(stateIndex);
            }
        }

        public float GetLayerWeight(int layer)
        {
            if (_layerMixer.IsValid() && layer >= 0 && layer < _layerMixer.GetInputCount())
                return _layerMixer.GetInputWeight(layer);
            return 0f;
        }

        public void PlayState(int stateHash, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            EnsureGraph();
            if (!_stateNameToIndex.TryGetValue(stateHash, out int targetIndex)) return;
            PlayStateInternal(targetIndex, transitionDuration, layer, forceRestart, curve, destinationStartTime);
        }

        public void PlayState(string stateName, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
            => PlayState(StringToHash(stateName), transitionDuration, layer, forceRestart, curve, destinationStartTime);

        public void PlayStateByGuid(string guid, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            EnsureGraph();
            if (!_stateGuidToIndex.TryGetValue(guid.GetHashCode(), out int targetIndex)) return;
            PlayStateInternal(targetIndex, transitionDuration, layer, forceRestart, curve, destinationStartTime);
        }

        public bool IsStateActive(int stateHash, int layer = 0)
        {
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return false;
            if (!_stateNameToIndex.TryGetValue(stateHash, out int targetIndex)) return false;
            return _layerStates[layer].CurrentStateIndex == targetIndex;
        }

        public bool IsStateActive(string stateName, int layer = 0)
            => IsStateActive(StringToHash(stateName), layer);

        public bool IsStateActiveByGuid(string guid, int layer = 0)
        {
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return false;
            if (!_stateGuidToIndex.TryGetValue(guid.GetHashCode(), out int targetIndex)) return false;
            return _layerStates[layer].CurrentStateIndex == targetIndex;
        }

        public bool TrySkipState(int stateHashToSkip, int targetStateHash, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            if (!IsStateActive(stateHashToSkip, layer)) return false;
            PlayState(targetStateHash, transitionDuration, layer, forceRestart, curve, destinationStartTime);
            return true;
        }

        public bool TrySkipState(string stateToSkip, string targetState, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
            => TrySkipState(StringToHash(stateToSkip), StringToHash(targetState), transitionDuration, layer, forceRestart, curve, destinationStartTime);

        public bool TrySkipStateByGuid(string guidToSkip, string targetGuid, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            if (!IsStateActiveByGuid(guidToSkip, layer)) return false;
            PlayStateByGuid(targetGuid, transitionDuration, layer, forceRestart, curve, destinationStartTime);
            return true;
        }

        public bool TryAutoSkipState(int stateHashToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
        {
            if (!IsStateActive(stateHashToSkip, layer)) return false;
            if (!_stateNameToIndex.TryGetValue(stateHashToSkip, out int stateIndex)) return false;
            if (stateIndex < 0 || stateIndex >= _activeStatesCount) return false;
            var state = _runtimeStates[stateIndex];
            if (state == null || state.transitions == null || state.transitions.Count == 0) return false;
            var t = state.transitions[0];

            if (!ignoreExitTime && t.hasExitTime)
            {
                var playable = _layerMixers[layer].GetInput(stateIndex);
                if (playable.IsValid())
                {
                    float unscaledDuration = HonamiStateEvaluator.GetUnscaledStateDuration(
                        controller, state, stateIndex, _pickedRandomIdx, GetStateBlendParam(state));
                    if (unscaledDuration > 0)
                    {
                        float progress = Mathf.Clamp01(state.isReversed ? (1f - (float)(playable.GetTime() / unscaledDuration)) : (float)(playable.GetTime() / unscaledDuration));
                        if (progress < t.exitTime) return false;
                    }
                }
            }

            if (string.IsNullOrEmpty(t.targetStateGuid)) return false;

            if (cancelEvents)
            {
                if (!_pausedStateIndices[layer].Contains(stateIndex))
                {
                    _pausedStateIndices[layer].Add(stateIndex);
                    var oldPlayable = _layerMixers[layer].GetInput(stateIndex);
                    if (oldPlayable.IsValid()) oldPlayable.SetSpeed(0f);
                }
            }

            PlayStateByGuidWithPriority(t.targetStateGuid, t.duration, layer, false, t.useCurve ? t.curve : null, t.destinationStartTime, t.priority, t.victimMode, t.sacrificeSpeedMultiplier, t.acceleratedWeightDrop, t.useCustomVictimCurve ? t.victimWeightCurve : null);
            return true;
        }

        public bool TryAutoSkipState(string stateToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
            => TryAutoSkipState(StringToHash(stateToSkip), layer, ignoreExitTime, cancelEvents);

        public bool TryAutoSkipStateByGuid(string guidToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
        {
            if (!IsStateActiveByGuid(guidToSkip, layer)) return false;
            if (!_stateGuidToIndex.TryGetValue(guidToSkip.GetHashCode(), out int stateIndex)) return false;
            if (stateIndex < 0 || stateIndex >= _activeStatesCount) return false;
            var state = _runtimeStates[stateIndex];
            if (state == null || state.transitions == null || state.transitions.Count == 0) return false;
            var t = state.transitions[0];

            if (!ignoreExitTime && t.hasExitTime)
            {
                var playable = _layerMixers[layer].GetInput(stateIndex);
                if (playable.IsValid())
                {
                    float unscaledDuration = HonamiStateEvaluator.GetUnscaledStateDuration(
                        controller, state, stateIndex, _pickedRandomIdx, GetStateBlendParam(state));
                    if (unscaledDuration > 0)
                    {
                        float progress = Mathf.Clamp01(state.isReversed ? (1f - (float)(playable.GetTime() / unscaledDuration)) : (float)(playable.GetTime() / unscaledDuration));
                        if (progress < t.exitTime) return false;
                    }
                }
            }

            if (string.IsNullOrEmpty(t.targetStateGuid)) return false;

            if (cancelEvents)
            {
                if (!_pausedStateIndices[layer].Contains(stateIndex))
                {
                    _pausedStateIndices[layer].Add(stateIndex);
                    var oldPlayable = _layerMixers[layer].GetInput(stateIndex);
                    if (oldPlayable.IsValid()) oldPlayable.SetSpeed(0f);
                }
            }

            PlayStateByGuidWithPriority(t.targetStateGuid, t.duration, layer, false, t.useCurve ? t.curve : null, t.destinationStartTime, t.priority, t.victimMode, t.sacrificeSpeedMultiplier, t.acceleratedWeightDrop, t.useCustomVictimCurve ? t.victimWeightCurve : null);
            return true;
        }


        public void ReactToAction(HonamiActionID actionId)
        {
            if (controller == null || actionId == null || _runtimeStates == null) return;

            for (int i = 0; i < _activeStatesCount; i++)
            {
                var state = _runtimeStates[i];
                if (state.linkedActionId == actionId)
                    PlayStateInternal(i, 0.25f, state.layerIndex, true, null, 0f);
            }
        }

        public void ReactToAction(HonamiActionID actionId, float transitionDuration)
        {
            if (controller == null || actionId == null || _runtimeStates == null) return;

            for (int i = 0; i < _activeStatesCount; i++)
            {
                var state = _runtimeStates[i];
                if (state.linkedActionId == actionId)
                    PlayStateInternal(i, transitionDuration, state.layerIndex, true, null, 0f);
            }
        }

        public void ReactToAction(HonamiActionID actionId, float transitionDuration, float delay)
        {
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

        private void UpdatePendingActions(float deltaTime)
        {
            if (_pendingActions.Count == 0) return;

            for (int i = _pendingActions.Count - 1; i >= 0; i--)
            {
                var pending = _pendingActions[i];
                pending.remainingDelay -= deltaTime;
                
                if (pending.remainingDelay <= 0f)
                {
                    ReactToAction(pending.actionId, pending.transitionDuration);
                    _pendingActions.RemoveAt(i);
                }
                else
                {
                    _pendingActions[i] = pending;
                }
            }
        }

        private void RegisterLinkedActions()
        {
            if (controller == null || _runtimeStates == null) return;

            for (int i = 0; i < _activeStatesCount; i++)
            {
                var state = _runtimeStates[i];
                if (state != null && state.linkedActionId != null)
                    HonamiLinkedAction.Register(this, state.linkedActionId);
            }
        }

        public void ResetToDefault()
        {
            _params.ClearAllTriggers();
            _params.Initialize(controller);
            ResetAnimatorState();
            if (HasPlayableGraph)
                PlayDefaultStates();
            RestoreInitialPoseIfIdle();
        }

        public void ResetAnimatorState()
        {
            _pickedRandomIdx.Clear();
            _repeaterLastFireTime.Clear();
            _repeaterFireCount.Clear();

            if (_layerMixers == null) return;

            for (int layer = 0; layer < _layerMixers.Count; layer++)
            {
                Stop(layer);

                if (_layerMixer.IsValid() && controller != null && layer < controller.ActiveLayers.Count)
                    _layerMixer.SetInputWeight(layer, layer == 0 ? controller.ActiveLayers[layer].weight : 0f);
            }

            RestoreInitialPoseIfIdle();
        }

        public int GetActiveStateIndex(int layer) => (_layerStates != null && layer >= 0 && layer < _layerStates.Length) ? _layerStates[layer].CurrentStateIndex : -1;
        public int GetPreviousStateIndex(int layer)
        {
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return -1;
            int idx = _layerStates[layer].PreviousStateIndex;
            return (idx == TransientPortIndex) ? _layerStates[layer].TransientStateIndex : idx;
        }
        public float GetTransitionWeight(int layer) => (_layerStates != null && layer >= 0 && layer < _layerStates.Length) ? _layerStates[layer].TransitionWeight : 0f;

        public float GetStateProgress(int layer, int statePortIdx)
        {
            if (!TryGetLayerMixer(layer, out AnimationMixerPlayable mixer) || statePortIdx < 0) return 0f;
            if (statePortIdx >= mixer.GetInputCount()) return 0f;

            var playable = mixer.GetInput(statePortIdx);
            if (!playable.IsValid()) return 0f;

            int realIdx = (statePortIdx == TransientPortIndex) ? _layerStates[layer].TransientStateIndex : statePortIdx;
            if (realIdx < 0 || realIdx >= _activeStatesCount) return 0f;

            float dur = HonamiStateEvaluator.GetUnscaledStateDuration(controller,_runtimeStates[realIdx], realIdx, _pickedRandomIdx, GetStateBlendParam(_runtimeStates[realIdx]));
            if (dur <= 0) return 0f;

            double rawTime = playable.GetTime();
            if (_runtimeStates[realIdx].loop)
                return (float)((rawTime / dur) % 1.0);
            return Mathf.Clamp01((float)(rawTime / dur));
        }

    }
}
