using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    [AddComponentMenu("Honami Animation/Honami Clip Player")]
    public sealed class HonamiClipPlayer : HonamiAnimatorBase
    {
        [Header("Clips")]
        [SerializeField] private List<HonamiClipEntry> clips = new List<HonamiClipEntry>();

        [SerializeField, HideInInspector] private string defaultClip;

        [SerializeField, Tooltip("Play the default clip as soon as the player starts.")]
        private bool playAutomatically = true;

        private AnimationMixerPlayable _mixer;
        private HonamiClipState[] _states = Array.Empty<HonamiClipState>();
        private readonly Dictionary<int, int> _nameToIndex = new();
        private readonly List<QueuedClip> _queue = new();
        private float[] _resolvedWeights = Array.Empty<float>();
        private readonly List<int> _layerScratch = new();
        private int _defaultIndex = -1;
        private bool _anyWeightApplied;
        private bool _draining;

        private struct QueuedClip
        {
            public int Index;
            public float FadeLength;
        }

        public event Action<string> OnClipStarted;
        public event Action<string> OnClipFinished;

        public IReadOnlyList<HonamiClipState> States => _states;
        public int ClipCount => _states.Length;

        public HonamiClipState this[string name] => TryGetState(name, out var state) ? state : null;

        public HonamiClipState this[int index]
            => index >= 0 && index < _states.Length ? _states[index] : null;

        public string CurrentClip
        {
            get
            {
                HonamiClipState best = null;
                for (int i = 0; i < _states.Length; i++)
                {
                    var state = _states[i];
                    if (!state.IsPlaying) continue;
                    if (best == null || state.RawWeight > best.RawWeight) best = state;
                }
                return best?.Name;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            BuildGraph();
            RegisterLinkedActions();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!HasPlayableGraph) BuildGraph();
            if (!HasPlayableGraph) return;

            RegisterLinkedActions();

            if (startup == HonamiAnimationStartup.Enable && !_keepPoseOnDisable)
            {
                ResetAllStates();
                PlayDefaultClip();
                ApplyWeights();
                _playableGraph.Evaluate(0f);
                RestoreInitialPoseIfIdle();
                ApplyGlobalWeightBlend();
            }

            _keepPoseOnDisable = false;
            _playableGraph.Play();
        }

        private void Start()
        {
            if (startup != HonamiAnimationStartup.Start) return;

            if (!HasPlayableGraph) BuildGraph();
            if (!HasPlayableGraph) return;

            RegisterLinkedActions();
            PlayDefaultClip();
            ApplyWeights();
            _playableGraph.Evaluate(0f);
            RestoreInitialPoseIfIdle();
            ApplyGlobalWeightBlend();
            _playableGraph.Play();
        }

        protected override void OnDisable()
        {
            if (!_keepPoseOnDisable)
            {
                ResetAllStates();
                ApplyWeights();
                if (HasPlayableGraph)
                {
                    _playableGraph.Evaluate(0f);
                    RestoreInitialPoseIfIdle();
                    _playableGraph.Stop();
                }
            }

            if (_playableGraph.IsValid()) _playableGraph.Destroy();

            base.OnDisable();
        }

        public override void Tick(double deltaTime)
        {
            if (_isPaused || !HasPlayableGraph) return;

            _cachedDeltaTime = deltaTime * timeScale;
            float scaled = (float)_cachedDeltaTime;

            AdvanceStates(scaled);
            UpdateFades(scaled);
            UpdateQueue();
            ApplyWeights();

            UpdatePendingActions((float)deltaTime);

            PrepareRigs();
            _playableGraph.Evaluate(scaled);
            RestoreInitialPoseIfIdle();
            ProcessLegacyRigs();
            ApplyGlobalWeightBlend();
        }

        public bool PlayClip(string name, float fadeLength = 0f, bool forceRestart = false)
        {
            if (!TryGetState(name, out var state)) return false;

            if (forceRestart || !state.IsPlaying || state.Finished) state.Rewind();
            state.FadeTo(1f, fadeLength);

            for (int i = 0; i < _states.Length; i++)
            {
                var other = _states[i];
                if (ReferenceEquals(other, state) || other.Layer != state.Layer) continue;
                other.FadeTo(0f, fadeLength);
            }

            if (!_draining) _queue.Clear();
            OnClipStarted?.Invoke(state.Name);
            return true;
        }

        public bool CrossFade(string name, float fadeLength = 0.25f, bool forceRestart = false) => PlayClip(name, fadeLength, forceRestart);

        public bool PlayQueued(string name, HonamiQueueMode mode = HonamiQueueMode.CompleteOthers, float fadeLength = 0f)
        {
            if (!TryGetState(name, out var state)) return false;

            if (mode == HonamiQueueMode.PlayNow || !HasBlockingPlayback())
            {
                return PlayClip(name, fadeLength);
            }

            _queue.Add(new QueuedClip { Index = state.Index, FadeLength = fadeLength });
            return true;
        }

        public bool Blend(string name, float targetWeight = 1f, float fadeLength = 0.25f)
        {
            if (!TryGetState(name, out var state)) return false;

            if (targetWeight > 0f && (!state.IsPlaying || state.Finished)) state.Rewind();
            state.FadeTo(targetWeight, fadeLength);
            return true;
        }

        public bool Stop(string name)
        {
            if (!TryGetState(name, out var state)) return false;

            state.Reset();
            ApplyWeights();
            return true;
        }

        public override void StopAll()
        {
            ResetAllStates();
            ApplyWeights();
            RestoreInitialPoseIfIdle();
        }

        public void Rewind()
        {
            for (int i = 0; i < _states.Length; i++) _states[i].Rewind();
        }

        public bool Rewind(string name)
        {
            if (!TryGetState(name, out var state)) return false;
            state.Rewind();
            return true;
        }

        public bool Sample(string name, float normalizedTime)
        {
            if (!HasPlayableGraph || !TryGetState(name, out var state)) return false;

            for (int i = 0; i < _states.Length; i++)
            {
                _states[i].RawWeight = 0f;
                _states[i].TargetWeight = 0f;
                _states[i].FadeRate = 0f;
                _states[i].Enabled = false;
            }

            // Weight pins the pose; Enabled = false keeps AdvanceStates off it.
            state.Enabled = false;
            state.RawWeight = 1f;
            state.TargetWeight = 1f;
            state.NormalizedTime = normalizedTime;
            _queue.Clear();

            ApplyWeights();
            _playableGraph.Evaluate(0f);
            ApplyGlobalWeightBlend();
            return true;
        }

        public override bool IsPlaying(string name) => TryGetState(name, out var state) && state.IsPlaying;

        public bool IsPlayingAny
        {
            get
            {
                for (int i = 0; i < _states.Length; i++)
                {
                    if (_states[i].IsPlaying) return true;
                }
                return false;
            }
        }

        public override void Play(string name, float transitionDuration) => PlayClip(name, transitionDuration);

        public override void ReactToAction(HonamiActionID actionId, float transitionDuration)
        {
            if (actionId == null) return;

            for (int i = 0; i < _states.Length; i++)
            {
                if (_states[i].LinkedActionId == actionId)
                    PlayClip(_states[i].Name, transitionDuration);
            }
        }

        public void AddClip(AnimationClip clip, string name = null, HonamiClipWrapMode wrapMode = HonamiClipWrapMode.Once, int layer = 0)
        {
            if (clip == null) return;

            clips.Add(new HonamiClipEntry
            {
                clip = clip,
                name = string.IsNullOrEmpty(name) ? clip.name : name,
                wrapMode = wrapMode,
                layer = layer
            });

            Rebuild();
        }

        public bool RemoveClip(string name)
        {
            int hash = StringToHash(name);
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null && StringToHash(clips[i].ResolveName()) == hash)
                {
                    clips.RemoveAt(i);
                    Rebuild();
                    return true;
                }
            }
            return false;
        }

        public void Rebuild()
        {
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
            _queue.Clear();
            CancelPendingActions();
            BuildGraph();
            if (HasPlayableGraph && isActiveAndEnabled) _playableGraph.Play();
        }

        public bool TryGetState(string name, out HonamiClipState state)
        {
            state = null;
            if (string.IsNullOrEmpty(name)) return false;

            if (!_nameToIndex.TryGetValue(StringToHash(name), out int index)) return false;
            if (index < 0 || index >= _states.Length) return false;

            state = _states[index];
            return true;
        }

        protected override bool IsGraphIdle() => !_anyWeightApplied;

        private void BuildGraph()
        {
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
            _nameToIndex.Clear();

            int count = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null && clips[i].clip != null) count++;
            }

            _states = new HonamiClipState[count];
            _resolvedWeights = new float[count];
            _defaultIndex = -1;

            if (count == 0) return;

            if (_animator == null) TryGetComponent<Animator>(out _animator);
            if (_animator == null) return;

            _playableGraph = PlayableGraph.Create(gameObject.name + "_HonamiClipGraph");
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "AnimationOutput", _animator);
            _playableOutput.SetWeight(GlobalWeightMode == HonamiGlobalWeightMode.Bind ? GlobalWeight : 1f);

            _mixer = AnimationMixerPlayable.Create(_playableGraph, count);
            _playableOutput.SetSourcePlayable(_mixer);

            int slot = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                var entry = clips[i];
                if (entry == null || entry.clip == null) continue;

                var clipPlayable = AnimationClipPlayable.Create(_playableGraph, entry.clip);
                clipPlayable.SetApplyFootIK(false);
                clipPlayable.SetSpeed(0.0);
                clipPlayable.SetDuration(double.MaxValue);
                _playableGraph.Connect(clipPlayable, 0, _mixer, slot);
                _mixer.SetInputWeight(slot, 0f);

                string resolved = entry.ResolveName();
                var state = new HonamiClipState
                {
                    Index = slot,
                    Name = resolved,
                    Clip = entry.clip,
                    Layer = entry.layer,
                    Speed = entry.speed,
                    WrapMode = entry.wrapMode,
                    LinkedActionId = entry.linkedActionId,
                    Playable = clipPlayable
                };
                state.Reset();

                _states[slot] = state;
                if (entry.isDefault && _defaultIndex < 0) _defaultIndex = slot;

                _nameToIndex.TryAdd(StringToHash(resolved), slot);
                slot++;
            }

            InsertRigChain();
        }

        private void PlayDefaultClip()
        {
            if (!playAutomatically || _states.Length == 0) return;

            if (_defaultIndex >= 0)
            {
                PlayClip(_states[_defaultIndex].Name);
                return;
            }

            // Legacy scenes still carry the old name field; OnValidate only migrates them in the editor.
            if (!string.IsNullOrEmpty(defaultClip) && PlayClip(defaultClip)) return;

            PlayClip(_states[0].Name);
        }

        private void RegisterLinkedActions()
        {
            if (clips == null) return;

            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null && clips[i].linkedActionId != null)
                    HonamiLinkedAction.Register(this, clips[i].linkedActionId);
            }
        }

        private void ResetAllStates()
        {
            _queue.Clear();
            for (int i = 0; i < _states.Length; i++) _states[i].Reset();
        }

        private void AdvanceStates(float deltaTime)
        {
            for (int i = 0; i < _states.Length; i++)
            {
                var state = _states[i];
                if (!state.Enabled || state.Finished) continue;

                float step = deltaTime * state.Speed * (state.Forward ? 1f : -1f);
                if (step == 0f) continue;

                float length = state.Length;
                float time = state.Time + step;

                switch (state.WrapMode)
                {
                    case HonamiClipWrapMode.Loop:
                        time = Mathf.Repeat(time, length);
                        break;

                    case HonamiClipWrapMode.PingPong:
                        if (time > length)
                        {
                            time = length - (time - length);
                            state.Forward = !state.Forward;
                        }
                        else if (time < 0f)
                        {
                            time = -time;
                            state.Forward = !state.Forward;
                        }
                        time = Mathf.Clamp(time, 0f, length);
                        break;

                    case HonamiClipWrapMode.ClampForever:
                        if (time >= length || time <= 0f)
                        {
                            time = Mathf.Clamp(time, 0f, length);
                            FinishState(state, release: false);
                        }
                        break;

                    default:
                        if (time >= length || time <= 0f)
                        {
                            time = Mathf.Clamp(time, 0f, length);
                            FinishState(state, release: true);
                        }
                        break;
                }

                state.Time = time;
            }
        }

        private void FinishState(HonamiClipState state, bool release)
        {
            state.Finished = true;

            if (release)
            {
                state.TargetWeight = 0f;
                state.RawWeight = 0f;
                state.FadeRate = 0f;
                state.Enabled = false;
            }

            OnClipFinished?.Invoke(state.Name);
        }

        private void UpdateFades(float deltaTime)
        {
            for (int i = 0; i < _states.Length; i++)
            {
                var state = _states[i];
                if (state.FadeRate <= 0f)
                {
                    state.RawWeight = state.TargetWeight;
                }
                else
                {
                    state.RawWeight = Mathf.MoveTowards(state.RawWeight, state.TargetWeight, state.FadeRate * deltaTime);
                    if (Mathf.Approximately(state.RawWeight, state.TargetWeight)) state.FadeRate = 0f;
                }

                if (state.RawWeight <= 0f && state.TargetWeight <= 0f) state.Enabled = false;
            }
        }

        private void UpdateQueue()
        {
            if (_queue.Count == 0 || HasBlockingPlayback()) return;

            QueuedClip next = _queue[0];
            _queue.RemoveAt(0);

            _draining = true;
            PlayClip(_states[next.Index].Name, next.FadeLength);
            _draining = false;
        }

        // Loop and PingPong clips never finish, so only one-shot clips can hold the queue back.
        private bool HasBlockingPlayback()
        {
            for (int i = 0; i < _states.Length; i++)
            {
                var state = _states[i];
                if (!state.IsPlaying || state.Finished) continue;
                if (state.WrapMode == HonamiClipWrapMode.Once || state.WrapMode == HonamiClipWrapMode.ClampForever)
                    return true;
            }
            return false;
        }

        private void ApplyWeights()
        {
            if (!_mixer.IsValid()) return;

            for (int i = 0; i < _resolvedWeights.Length; i++) _resolvedWeights[i] = 0f;

            ResolveLayeredWeights();

            _anyWeightApplied = false;
            for (int i = 0; i < _states.Length; i++)
            {
                float weight = _resolvedWeights[i];
                _mixer.SetInputWeight(i, weight);

                if (weight > 0f)
                {
                    _anyWeightApplied = true;
                    _states[i].Playable.SetTime(_states[i].Time);
                }
            }
        }

        private void ResolveLayeredWeights()
        {
            _layerScratch.Clear();
            for (int i = 0; i < _states.Length; i++)
            {
                int layer = _states[i].Layer;
                if (_states[i].RawWeight > 0f && !_layerScratch.Contains(layer)) _layerScratch.Add(layer);
            }

            if (_layerScratch.Count == 0) return;

            _layerScratch.Sort();

            float remaining = 1f;
            for (int l = _layerScratch.Count - 1; l >= 0 && remaining > 0f; l--)
            {
                int layer = _layerScratch[l];

                float sum = 0f;
                for (int i = 0; i < _states.Length; i++)
                {
                    if (_states[i].Layer == layer) sum += _states[i].RawWeight;
                }

                if (sum <= 0f) continue;

                float layerShare = Mathf.Min(sum, 1f) * remaining;
                float scale = layerShare / sum;

                for (int i = 0; i < _states.Length; i++)
                {
                    if (_states[i].Layer == layer) _resolvedWeights[i] = _states[i].RawWeight * scale;
                }

                remaining -= layerShare;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (clips == null) return;

            for (int i = 0; i < clips.Count; i++)
            {
                var entry = clips[i];
                if (entry == null) continue;
                if (string.IsNullOrEmpty(entry.name) && entry.clip != null) entry.name = entry.clip.name;
                if (entry.layer < 0) entry.layer = 0;
            }

            MigrateDefaultClipName();
        }

        private void MigrateDefaultClipName()
        {
            if (string.IsNullOrEmpty(defaultClip)) return;

            int hash = StringToHash(defaultClip);
            for (int i = 0; i < clips.Count; i++)
            {
                var entry = clips[i];
                if (entry == null || StringToHash(entry.ResolveName()) != hash) continue;

                entry.isDefault = true;
                defaultClip = null;
                return;
            }
        }
#endif
    }
}
