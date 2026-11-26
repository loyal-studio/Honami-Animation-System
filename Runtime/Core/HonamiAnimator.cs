
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
    /// Defines how the outgoing controller is evaluated while a controller transition is active.
    /// </summary>
    public enum HonamiControllerTransitionMode
    {
        ContinueEvaluating,
        Freeze
    }

    /// <summary>
    /// Runtime component that replaces Unity Animator Controller playback with Honami's graph, parameter, event, avatar, and rigging pipeline.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Honami Animation/Honami Animator")]
    public partial class HonamiAnimator : HonamiAnimatorBase
    {
        [Header("Configuration")]
        [SerializeField] internal HonamiRuntimeController controller;
        internal HonamiRuntimeController _lastBuiltController;

        [Header("Initial Pose")]
        [SerializeField, Tooltip("If true, captures the initial pose from the final frame of the default state instead of the Awake pose.")]
        private bool captureFromDefaultStateEnd = false;

        [SerializeField, Tooltip("When a non-loop state finishes on a layer without a default state, release it so Initial Pose becomes the fallback instead of freezing on the final frame.")]
        private bool releaseFinishedStatesWithoutDefault = true;

        [Header("Avatar")]
        [SerializeField] internal HonamiAvatar avatar;
        public HonamiAvatar Avatar => avatar;
        
        [SerializeField] private bool _mirrorAvatar = false;

        [SerializeField] private float _mirrorBlendSpeed = 0f;

        public bool MirrorAvatar => _mirrorAvatar;
        public float MirrorBlendSpeed => _mirrorBlendSpeed;

        /// <summary>
        /// Enables or disables global avatar mirroring for all evaluated states.
        /// </summary>
        public void SetGlobalMirror(bool active) => _mirrorAvatar = active;

        /// <summary>
        /// Sets the blend speed used when global avatar mirroring changes.
        /// </summary>
        public void SetGlobalMirrorSpeed(float speed) => _mirrorBlendSpeed = Mathf.Max(0f, speed);

        internal float _currentGlobalMirrorWeight = 0f;

        internal AnimationLayerMixerPlayable _layerMixer;
        internal AnimationScriptPlayable _globalMirrorPlayable;

        internal readonly List<AnimationMixerPlayable> _layerMixers = new();

        internal HonamiLayerState[] _layerStates;
        internal HonamiPortState[][] _portStates;
        internal AnimationCurve[] _activeTransitionCurve;
        internal AnimationCurve[] _activeVictimCurve;
        internal Timeline.HonamiTimelineModuleSet[] _stateModuleSets;
        internal int[] _activeModuleState;
        internal HonamiState[] _moduleStorageStates;

        internal HashSet<int>[] _pausedStateIndices;

        internal readonly List<int> _anyStateIndices = new();
        internal HonamiNodeRuntime[] _nodeRuntimes;

        internal HonamiNodeRuntime GetNodeRuntime(int stateIndex)
            => _nodeRuntimes != null && stateIndex >= 0 && stateIndex < _nodeRuntimes.Length ? _nodeRuntimes[stateIndex] : null;
        private int[] _lastActiveStateIndex;
        internal List<int>[] _anyStateIndicesByLayer;

        internal readonly List<int> _tentativeBuffer = new();
        internal readonly List<int> _exitTentativeBuffer = new();
        internal NativeArray<float> _blendStateValues;
        internal int[] _blendParamIndices;
        internal int _pCountTotal;

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

        internal bool _avatarEnabled;

        public event System.Action<string> OnStateEntered;
        public event System.Action<string> OnStateFinished;
        public event System.Action<HonamiStateExitInfo> OnStateExited;

        public HonamiRuntimeController CurrentController => controller;
        public HonamiParameterStore Parameters => _params;

        protected override void OnBeforeGraphBuild()
        {
            TryGetComponent<HonamiLocalEventReceiver>(out _localEventReceiver);
            _params.Initialize(controller);
        }

        protected override void BuildGraph()
        {
            if (controller == null) return;
            InitializeGraph();
        }

        protected override void ResetPlaybackState() => ResetAnimatorState();

        protected override void ResetPlaybackForShutdown() => ResetToDefault();

        protected override void PlayStartupContent() => PlayDefaultStates();

        protected override void OnBeforeStartupEvaluate()
        {
            ApplyConstraints();
            if (captureFromDefaultStateEnd) CaptureInitialPoseFromDefaultState();
        }

        protected override void OnDisable()
        {
            HonamiEventEngine.StopAllStateModules(this);
            base.OnDisable();
            AbandonControllerTransition();
        }

        protected override void OnDestroy()
        {
            DisposeOutgoingControllerResources();
            base.OnDestroy();
            _avatarProcessor?.Dispose();
            _constraints?.Dispose();
            _params?.Dispose();
            if (_blendStateValues.IsCreated) _blendStateValues.Dispose();
        }

        public override void Tick(double deltaTime)
        {
            if (_isPaused || !HasPlayableGraph) return;
            _cachedDeltaTime = deltaTime * EffectiveTimeScale;
            _params.UpdateRandomParameters();

            HonamiTransitionEngine.UpdateTransitions(this);
            ReleaseSupersededHolds();
            UpdateStatesRuntimeProperties();
            ApplyConstraints();

            UpdateControllerTransition((float)deltaTime);

            _params.ConsumePendingTriggers();
            _params.ClearTriggers();
            ApplyAvatarProcessors();

            UpdatePendingActions((float)deltaTime);

            EvaluateGraph((float)_cachedDeltaTime);
        }

        protected override bool SupportsBatchedEvaluation => true;

        protected override bool SupportsClipTrackLayering => false;

        protected override void OnGraphEvaluated() => EvaluateStateModules();

        public void RefreshModuleBindings() => HonamiEventEngine.InvalidateStateModules(this);

        public void SetFloat(string name, float value) { EnsureGraphBuilt(); _params.SetFloat(name, value); }
        public void SetFloat(int id, float value) { EnsureGraphBuilt(); _params.SetFloat(id, value); }
        public float GetFloat(string name) { EnsureGraphBuilt(); return _params.GetFloat(name); }
        public float GetFloat(int id) { EnsureGraphBuilt(); return _params.GetFloat(id); }

        public void SetInteger(string name, int value) { EnsureGraphBuilt(); _params.SetInteger(name, value); }
        public void SetInteger(int id, int value) { EnsureGraphBuilt(); _params.SetInteger(id, value); }
        public int GetInteger(string name) { EnsureGraphBuilt(); return _params.GetInteger(name); }
        public int GetInteger(int id) { EnsureGraphBuilt(); return _params.GetInteger(id); }

        public void SetBool(string name, bool value) { EnsureGraphBuilt(); _params.SetBool(name, value); }
        public void SetBool(int id, bool value) { EnsureGraphBuilt(); _params.SetBool(id, value); }
        public bool GetBool(string name) { EnsureGraphBuilt(); return _params.GetBool(name); }
        public bool GetBool(int id) { EnsureGraphBuilt(); return _params.GetBool(id); }

        public void SetTrigger(string name) { EnsureGraphBuilt(); _params.SetTrigger(name); }
        public void SetTrigger(int id) { EnsureGraphBuilt(); _params.SetTrigger(id); }
        public void ResetTrigger(string name) { EnsureGraphBuilt(); _params.ResetTrigger(name); }
        public void ResetTrigger(int id) { EnsureGraphBuilt(); _params.ResetTrigger(id); }

        public bool HasTag(string tag, int layer = 0)
        {
            if (_runtimeStates == null || _layerStates == null || layer < 0 || layer >= _layerStates.Length) return false;
            int currentIdx = _layerStates[layer].CurrentStateIndex;
            if (currentIdx < 0 || currentIdx >= _activeStatesCount) return false;
            var state = _runtimeStates[currentIdx];
            return state != null && state.tags != null && state.tags.Contains(tag);
        }

        private readonly List<int> _carriedTriggers = new();

        // A controller swap rebuilds the parameter store; without this a trigger raised earlier in the frame is dropped.
        private void ReinitializeParameters(HonamiRuntimeController newController)
        {
            _params.CaptureActiveTriggers(_carriedTriggers);
            _params.Initialize(newController);
            _params.RestoreTriggers(_carriedTriggers);
        }

        /// <summary>
        /// Swaps the running controller, optionally cross-fading from the outgoing graph over
        /// <paramref name="transitionDuration"/> seconds.
        /// </summary>
        public void SetController(
            HonamiRuntimeController newController,
            float transitionDuration = 0f,
            AnimationCurve transitionCurve = null,
            HonamiControllerTransitionMode mode = HonamiControllerTransitionMode.ContinueEvaluating)
        {
            RequestCatchUpTick();

            // A null controller builds no incoming root, so a cross-fade would blend the outgoing
            // graph against itself. Fall through to the plain swap and let the initial pose take over.
            bool crossFade = transitionDuration > 0f && _playableGraph.IsValid() && newController != null;

            AbortControllerTransition(destroyOutgoingGraph: crossFade);
            NotifyActiveStatesExited(HonamiStateExitReason.ControllerChanged);
            CancelPendingActions();
            HonamiLinkedAction.UnregisterAll(this);

            Playable outgoingRoot = Playable.Null;

            if (crossFade)
            {
                outgoingRoot = _playableOutput.GetSourcePlayable();

                // The outgoing subgraph keeps evaluating during the blend, so it needs its own processors.
                _oldAvatarProcessor = _avatarProcessor;
                _oldConstraints = _constraints;
                _oldBlendStateValues = _blendStateValues;

                _avatarProcessor = new HonamiAvatarProcessor();
                _constraints = new HonamiConstraintProcessor();
                _blendStateValues = default;
            }
            else if (_playableGraph.IsValid())
            {
                _playableGraph.Destroy();
            }

            controller = newController;
            ReinitializeParameters(controller);
            InitializeGraph(keepGraph: crossFade, forceRebuildStates: true);

            if (TryGetComponent<HonamiRiggingProcessor>(out var riggingProcessor))
            {
                riggingProcessor.RefreshRigs();
            }

            if (!HasPlayableGraph) return;

            RegisterLinkedActions();
            if (!crossFade) _playableGraph.Play();

            PlayDefaultStates();
            if (captureFromDefaultStateEnd) CaptureInitialPoseFromDefaultState();

            if (crossFade)
            {
                BeginControllerCrossFade(outgoingRoot, transitionDuration, transitionCurve, mode);
            }

            _playableGraph.Evaluate(0f);
            RestoreInitialPoseIfIdle();
            ApplyGlobalWeightBlend();

            if (crossFade) _playableGraph.Play();
        }

        private void UpdateControllerTransition(float deltaTime)
        {
            if (!_isTransitioningController) return;

            _controllerTransitionTime += deltaTime;
            float progress = Mathf.Clamp01(_controllerTransitionTime / _controllerTransitionDuration);
            float weight = _controllerTransitionCurve != null ? _controllerTransitionCurve.Evaluate(progress) : progress;

            if (_controllerMixer.IsValid())
            {
                _controllerMixer.SetInputWeight(0, 1f - weight);
                _controllerMixer.SetInputWeight(1, weight);
            }

            if (progress >= 1f) AbortControllerTransition(destroyOutgoingGraph: true);
        }

        private void BeginControllerCrossFade(
            Playable outgoingRoot,
            float transitionDuration,
            AnimationCurve transitionCurve,
            HonamiControllerTransitionMode mode)
        {
            Playable incomingRoot = _playableOutput.GetSourcePlayable();

            _controllerMixer = AnimationMixerPlayable.Create(_playableGraph, 2);
            _playableGraph.Connect(outgoingRoot, 0, _controllerMixer, 0);
            _playableGraph.Connect(incomingRoot, 0, _controllerMixer, 1);
            _controllerMixer.SetInputWeight(0, 1f);
            _controllerMixer.SetInputWeight(1, 0f);

            if (mode == HonamiControllerTransitionMode.Freeze) outgoingRoot.SetSpeed(0f);

            _playableOutput.SetSourcePlayable(_controllerMixer);

            _controllerTransitionDuration = transitionDuration;
            _controllerTransitionTime = 0f;
            _controllerTransitionCurve = transitionCurve;
            _oldControllerPlayable = outgoingRoot;
            _isTransitioningController = true;
        }

        // Collapses an in-flight controller blend so the newer swap starts from a single-rooted graph.
        private void AbortControllerTransition(bool destroyOutgoingGraph)
        {
            if (!_isTransitioningController) return;
            _isTransitioningController = false;

            if (destroyOutgoingGraph)
            {
                if (_oldControllerPlayable.IsValid()) _playableGraph.DestroySubgraph(_oldControllerPlayable);

                if (_controllerMixer.IsValid())
                {
                    Playable survivingRoot = _controllerMixer.GetInput(1);
                    if (_playableOutput.IsOutputValid() && survivingRoot.IsValid())
                        _playableOutput.SetSourcePlayable(survivingRoot);

                    _playableGraph.Disconnect(_controllerMixer, 1);
                    _controllerMixer.Destroy();
                }
            }

            DisposeOutgoingControllerResources();
        }

        // OnDisable destroys the graph, so an in-flight blend can never be finished. Without this the
        // animator keeps the flag and ticks a dead mixer for the whole duration after the next enable.
        private void AbandonControllerTransition()
        {
            if (!_isTransitioningController) return;

            _isTransitioningController = false;
            _controllerMixer = default;
            _oldControllerPlayable = Playable.Null;
            _controllerTransitionCurve = null;
            _controllerTransitionTime = 0f;

            DisposeOutgoingControllerResources();
        }

        private void DisposeOutgoingControllerResources()
        {
            _oldAvatarProcessor?.Dispose();
            _oldConstraints?.Dispose();
            if (_oldBlendStateValues.IsCreated) _oldBlendStateValues.Dispose();

            _oldAvatarProcessor = null;
            _oldConstraints = null;
            _oldBlendStateValues = default;
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

        public void PauseLayer(int layer, bool paused)
        {
            if (_layerStates != null && layer >= 0 && layer < _layerStates.Length) _layerStates[layer].IsLayerPaused = paused;
        }

        public bool IsLayerPaused(int layer) => _layerStates != null && layer >= 0 && layer < _layerStates.Length && _layerStates[layer].IsLayerPaused;

        public void PauseState(string stateName, bool paused, int layer = 0)
        {
            if (TryResolveState(StringToHash(stateName), out int idx))
                SetStatePause(idx, layer, paused);
        }

        public void PauseStateByGuid(string guid, bool paused, int layer = 0)
        {
            if (TryResolveStateByGuid(guid, out int idx))
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

        public override void StopAll() => ResetAnimatorState();

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
                else if (IsExternalPort(i)) ClearExternalPort(layer, i);
                else ResetTime(i, layer);
            }
        }

        private void ResetLayerRuntimeState(int layer)
        {
            _layerStates[layer].HoldsLastFrame = false;
            _layerStates[layer].HoldSuperseded = false;
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
            if (_portStates == null || layer >= _portStates.Length || _portStates[layer] == null) return;

            for (int port = 0; port < _portStates[layer].Length; port++)
            {
                _portStates[layer][port].LastFrameRawTime = double.NaN;
                _portStates[layer][port].CurrentLoopCount = 0;
                _portStates[layer][port].WeightSnapshot = 0f;
            }
        }

        public void StopState(string stateName, int layer = 0)
        {
            if (TryResolveState(StringToHash(stateName), out int idx))
                StopStateInternal(idx, layer);
        }

        public void StopStateByGuid(string guid, int layer = 0)
        {
            if (TryResolveStateByGuid(guid, out int idx))
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
                if (stateIndex == _layerStates[layer].CurrentStateIndex)
                {
                    _layerStates[layer].CurrentStateIndex = -1;
                    _layerStates[layer].HoldsLastFrame = false;
                    _layerStates[layer].HoldSuperseded = false;
                }
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
            EnsureGraphBuilt();
            if (TryResolveState(stateHash, out int targetIndex))
                PlayStateInternal(targetIndex, transitionDuration, layer, forceRestart, curve, destinationStartTime);
        }

        public void PlayState(string stateName, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
            => PlayState(StringToHash(stateName), transitionDuration, layer, forceRestart, curve, destinationStartTime);

        public void PlayStateByGuid(string guid, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            EnsureGraphBuilt();
            if (TryResolveStateByGuid(guid, out int targetIndex))
                PlayStateInternal(targetIndex, transitionDuration, layer, forceRestart, curve, destinationStartTime);
        }

        public override void Play(string name, float transitionDuration) => PlayState(name, transitionDuration);

        public override bool IsPlaying(string name) => IsStateActive(name);

        public bool IsStateActive(int stateHash, int layer = 0)
            => TryResolveState(stateHash, out int targetIndex) && GetActiveStateIndex(layer) == targetIndex;

        public bool IsStateActive(string stateName, int layer = 0)
            => IsStateActive(StringToHash(stateName), layer);

        public bool IsStateActiveByGuid(string guid, int layer = 0)
            => TryResolveStateByGuid(guid, out int targetIndex) && GetActiveStateIndex(layer) == targetIndex;

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
            => TryResolveState(stateHashToSkip, out int index)
               && IsStateActive(stateHashToSkip, layer)
               && AutoSkipStateAt(index, layer, ignoreExitTime, cancelEvents);

        public bool TryAutoSkipState(string stateToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
            => TryAutoSkipState(StringToHash(stateToSkip), layer, ignoreExitTime, cancelEvents);

        public bool TryAutoSkipStateByGuid(string guidToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
            => TryResolveStateByGuid(guidToSkip, out int index)
               && IsStateActiveByGuid(guidToSkip, layer)
               && AutoSkipStateAt(index, layer, ignoreExitTime, cancelEvents);

        /// <summary>
        /// Takes the state's first transition immediately, honoring its exit time unless <paramref name="ignoreExitTime"/> is set.
        /// </summary>
        public bool AutoSkipStateAt(int stateIndex, int layer, bool ignoreExitTime = false, bool cancelEvents = false)
        {
            if (stateIndex < 0 || stateIndex >= _activeStatesCount) return false;

            HonamiState state = _runtimeStates[stateIndex];
            if (state == null || state.transitions == null || state.transitions.Count == 0) return false;

            HonamiTransition transition = state.transitions[0];
            if (string.IsNullOrEmpty(transition.targetStateGuid)) return false;
            if (!ignoreExitTime && !HasReachedExitTime(state, stateIndex, layer, transition)) return false;

            if (cancelEvents) FreezeStateForSkip(stateIndex, layer);

            PlayStateByGuidWithPriority(
                transition.targetStateGuid,
                transition.duration,
                layer,
                false,
                transition.ResolveBlendCurve(),
                transition.destinationStartTime,
                transition.priority,
                transition.victimMode,
                transition.sacrificeSpeedMultiplier,
                transition.acceleratedWeightDrop,
                transition.useCustomVictimCurve ? transition.victimWeightCurve : null);

            HonamiPlaybackEngine.ApplyTransitionFreeze(this, layer, transition.freezeMode);
            return true;
        }

        private bool HasReachedExitTime(HonamiState state, int stateIndex, int layer, HonamiTransition transition)
        {
            if (!transition.hasExitTime) return true;
            if (!TryGetLayerMixer(layer, out AnimationMixerPlayable mixer)) return true;

            Playable playable = mixer.GetInput(stateIndex);
            if (!playable.IsValid()) return true;

            float unscaledDuration = HonamiStateEvaluator.GetUnscaledStateDuration(
                controller, state, stateIndex, GetNodeRuntime(stateIndex), GetStateBlendParam(stateIndex));
            if (unscaledDuration <= 0f) return true;

            double normalized = playable.GetTime() / unscaledDuration;
            float progress = Mathf.Max(0f, state.isReversed ? 1f - (float)normalized : (float)normalized);
            return progress >= transition.exitTime;
        }

        private void FreezeStateForSkip(int stateIndex, int layer)
        {
            if (!IsLayerInRange(layer) || !_pausedStateIndices[layer].Add(stateIndex)) return;
            if (!TryGetLayerMixer(layer, out AnimationMixerPlayable mixer)) return;

            Playable playable = mixer.GetInput(stateIndex);
            if (playable.IsValid()) playable.SetSpeed(0f);
        }


        public override void ReactToAction(HonamiActionID actionId, float transitionDuration)
        {
            if (controller == null || actionId == null || _runtimeStates == null) return;

            for (int i = 0; i < _activeStatesCount; i++)
            {
                var state = _runtimeStates[i];
                if (state.linkedActionId == actionId)
                    PlayStateInternal(i, transitionDuration, state.layerIndex, true, null, 0f);
            }
        }

        protected override void RegisterLinkedActions()
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
            if (_nodeRuntimes != null)
            {
                for (int i = 0; i < _nodeRuntimes.Length; i++)
                {
                    _nodeRuntimes[i]?.Reset();
                }
            }

            ClearAllExternalPorts();
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

            float dur = HonamiStateEvaluator.GetUnscaledStateDuration(controller, _runtimeStates[realIdx], realIdx, GetNodeRuntime(realIdx), GetStateBlendParam(realIdx));
            if (dur <= 0) return 0f;

            double rawTime = playable.GetTime();
            if (_runtimeStates[realIdx].loop)
                return (float)((rawTime / dur) % 1.0);
            return Mathf.Clamp01((float)(rawTime / dur));
        }

    }
}
