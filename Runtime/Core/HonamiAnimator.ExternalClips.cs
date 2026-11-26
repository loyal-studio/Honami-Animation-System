using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public partial class HonamiAnimator
    {
        public const int ExternalPortCount = 2;

        public int ExternalPort0Index => _activeStatesCount + 1;
        public int ExternalPort1Index => _activeStatesCount + 2;
        public int TotalPortCount => _activeStatesCount + 1 + ExternalPortCount;

        public bool IsExternalPort(int portIndex) =>
            portIndex >= ExternalPort0Index && portIndex < ExternalPort0Index + ExternalPortCount;

        internal struct HonamiExternalSlot
        {
            public AnimationClip Clip;
            public AnimationClipPlayable Playable;
            public float Weight;
            public float Speed;
            public bool Loop;
            public double StartTime;
            public bool IsActive;
        }

        internal HonamiExternalSlot[][] _externalSlots;

        internal void EnsureExternalSlotsAllocated(int layerCount)
        {
            if (_externalSlots != null && _externalSlots.Length == layerCount) return;

            _externalSlots = new HonamiExternalSlot[layerCount][];
            for (int layer = 0; layer < layerCount; layer++)
            {
                _externalSlots[layer] = new HonamiExternalSlot[ExternalPortCount];
            }
        }

        internal int GetExternalSlotIndex(int portIndex) => portIndex - ExternalPort0Index;
        internal int GetExternalPortIndex(int slotIndex) => ExternalPort0Index + slotIndex;

        public float GetPortTargetWeight(int layer, int portIndex)
        {
            if (IsExternalPort(portIndex))
            {
                int slot = GetExternalSlotIndex(portIndex);
                return (_externalSlots != null && layer < _externalSlots.Length && slot >= 0 && slot < _externalSlots[layer].Length)
                    ? _externalSlots[layer][slot].Weight
                    : 1f;
            }
            if (portIndex >= 0 && portIndex < _activeStatesCount && _runtimeStates != null)
            {
                return _runtimeStates[portIndex].weight;
            }
            return 1f;
        }

        public float GetPortSpeed(int layer, int portIndex)
        {
            if (IsExternalPort(portIndex))
            {
                int slot = GetExternalSlotIndex(portIndex);
                return (_externalSlots != null && layer < _externalSlots.Length && slot >= 0 && slot < _externalSlots[layer].Length)
                    ? _externalSlots[layer][slot].Speed
                    : 1f;
            }
            if (portIndex >= 0 && portIndex < _activeStatesCount && _runtimeStates != null)
            {
                float speed = _runtimeStates[portIndex].speed;
                return _runtimeStates[portIndex].isReversed ? -speed : speed;
            }
            return 1f;
        }

        public bool IsExitState(int stateIndex) =>
            stateIndex >= 0 && stateIndex < _activeStatesCount && _runtimeStates != null && _runtimeStates[stateIndex] != null && _runtimeStates[stateIndex].node is { IsExit: true };

        /// <summary>
        /// Plays an external animation clip on the specified layer, immediately or with an optional cross-fade duration.
        /// </summary>
        public void PlayExternalClip(
            AnimationClip clip,
            float transitionDuration = 0f,
            int layer = 0,
            float startTime = 0f,
            float speed = 1f,
            bool loop = false,
            AnimationCurve curve = null)
        {
            CrossFadeExternalClip(clip, transitionDuration, layer, startTime, speed, loop, curve);
        }

        /// <summary>
        /// Transitions to an external animation clip inside the controller's layer mixer using the native Honami transition engine.
        /// </summary>
        public void CrossFadeExternalClip(
            AnimationClip clip,
            float transitionDuration,
            int layer = 0,
            float startTime = 0f,
            float speed = 1f,
            bool loop = false,
            AnimationCurve curve = null)
        {
            if (clip == null)
            {
                StopExternalClip(layer, transitionDuration);
                return;
            }

            EnsureGraphBuilt();

            if (_layerMixers == null || layer < 0 || layer >= _layerMixers.Count || !_playableGraph.IsValid())
            {
                return;
            }

            EnsureExternalSlotsAllocated(_layerMixers.Count);

            int currentIdx = _layerStates[layer].CurrentStateIndex;

            // Choose destination port: alternate between ExternalPort0 and ExternalPort1
            int targetPort = (currentIdx == ExternalPort0Index) ? ExternalPort1Index : ExternalPort0Index;

            SetupExternalPort(layer, targetPort, clip, startTime, speed, loop);

            if (curve == null && transitionDuration > 0f)
            {
                curve = HonamiTransitionEasing.GetCurve(HonamiTransitionEase.InOutSine);
            }

            HonamiPlaybackEngine.ExecuteCrossFade(
                this,
                targetPort,
                currentIdx,
                layer,
                transitionDuration,
                curve,
                startTime);

            ReleaseHoldsSupersededBy(layer);
        }

        /// <summary>
        /// Stops the active external clip on the layer, transitioning back to the layer's default state or fading out.
        /// </summary>
        public void StopExternalClip(int layer = 0, float transitionDuration = 0f, AnimationCurve curve = null)
        {
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return;
            int curr = _layerStates[layer].CurrentStateIndex;
            if (!IsExternalPort(curr)) return;

            int defaultIdx = (_defaultStateIndex != null && layer < _defaultStateIndex.Length)
                ? _defaultStateIndex[layer]
                : -1;

            if (defaultIdx >= 0 && defaultIdx < _activeStatesCount)
            {
                PlayStateInternal(defaultIdx, transitionDuration, layer, false, curve, 0f);
            }
            else
            {
                ClearExternalPort(layer, curr);
                _layerStates[layer].CurrentStateIndex = -1;
                _layerStates[layer].PreviousStateIndex = -1;
                _layerStates[layer].TransitionDuration = 0.0;
                _layerStates[layer].TransitionTime = 0.0;
                if (_layerMixers != null && layer < _layerMixers.Count && _layerMixers[layer].IsValid())
                {
                    _layerMixers[layer].SetInputWeight(curr, 0f);
                }
            }
        }

        /// <summary>
        /// Sets playback time on active external clip playables for the layer.
        /// </summary>
        public void SetExternalClipTime(int layer, double time, int portIndex = -1)
        {
            if (_externalSlots == null || layer < 0 || layer >= _externalSlots.Length) return;

            if (portIndex >= 0)
            {
                if (IsExternalPort(portIndex))
                {
                    int slot = GetExternalSlotIndex(portIndex);
                    if (slot >= 0 && slot < _externalSlots[layer].Length && _externalSlots[layer][slot].Playable.IsValid())
                    {
                        _externalSlots[layer][slot].Playable.SetTime(time);
                    }
                }
                return;
            }

            int curr = _layerStates[layer].CurrentStateIndex;
            if (IsExternalPort(curr))
            {
                int slot = GetExternalSlotIndex(curr);
                if (slot >= 0 && slot < _externalSlots[layer].Length && _externalSlots[layer][slot].Playable.IsValid())
                {
                    _externalSlots[layer][slot].Playable.SetTime(time);
                }
            }

            int prev = _layerStates[layer].PreviousStateIndex;
            if (IsExternalPort(prev))
            {
                int slot = GetExternalSlotIndex(prev);
                if (slot >= 0 && slot < _externalSlots[layer].Length && _externalSlots[layer][slot].Playable.IsValid())
                {
                    _externalSlots[layer][slot].Playable.SetTime(time);
                }
            }
        }

        /// <summary>
        /// Directly updates the transition progress between 0 and 1, used for manual scrubbing and seeking.
        /// </summary>
        public void SetTransitionProgress(int layer, float progress)
        {
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return;
            ref var layerState = ref _layerStates[layer];
            if (layerState.TransitionDuration > 0.0)
            {
                layerState.TransitionTime = Mathf.Clamp01(progress) * layerState.TransitionDuration;
                HonamiTransitionEngine.UpdateTransitions(this);
            }
        }

        public bool IsExternalClipPlaying(int layer = 0)
        {
            if (_layerStates == null || layer < 0 || layer >= _layerStates.Length) return false;
            return IsExternalPort(_layerStates[layer].CurrentStateIndex);
        }

        public AnimationClip GetCurrentExternalClip(int layer = 0)
        {
            if (_layerStates == null || _externalSlots == null || layer < 0 || layer >= _layerStates.Length || layer >= _externalSlots.Length) return null;
            int curr = _layerStates[layer].CurrentStateIndex;
            if (!IsExternalPort(curr)) return null;
            int slot = GetExternalSlotIndex(curr);
            return (slot >= 0 && slot < _externalSlots[layer].Length) ? _externalSlots[layer][slot].Clip : null;
        }

        /// <summary>
        /// Immediately evaluates transitions, state properties, constraints, avatar retargeting, and graph.
        /// </summary>
        public void EvaluateImmediate(float deltaTime = 0f)
        {
            if (!HasPlayableGraph || _isPaused) return;
            _cachedDeltaTime = deltaTime;
            HonamiTransitionEngine.UpdateTransitions(this);
            UpdateStatesRuntimeProperties();
            ApplyConstraints();
            ApplyAvatarProcessors();
            EvaluateGraph(deltaTime);
        }

        internal void SetupExternalPort(int layer, int portIndex, AnimationClip clip, float startTime, float speed, bool loop)
        {
            if (_externalSlots == null || layer < 0 || layer >= _externalSlots.Length) return;
            int slot = GetExternalSlotIndex(portIndex);
            if (slot < 0 || slot >= _externalSlots[layer].Length) return;

            ref var ext = ref _externalSlots[layer][slot];

            if (ext.Playable.IsValid())
            {
                if (_layerMixers != null && layer < _layerMixers.Count && _layerMixers[layer].IsValid())
                {
                    var currentInput = _layerMixers[layer].GetInput(portIndex);
                    if (currentInput.IsValid() && currentInput.Equals(ext.Playable))
                    {
                        _playableGraph.Disconnect(_layerMixers[layer], portIndex);
                    }
                }
                if (_playableGraph.IsValid())
                {
                    _playableGraph.DestroyPlayable(ext.Playable);
                }
                ext.Playable = default;
            }

            if (clip != null && _playableGraph.IsValid())
            {
                var clipPlayable = AnimationClipPlayable.Create(_playableGraph, clip);
                clipPlayable.SetApplyFootIK(false);
                clipPlayable.SetSpeed(speed);
                if (loop)
                {
                    clipPlayable.SetDuration(double.MaxValue);
                }
                clipPlayable.SetTime(startTime);

                if (_layerMixers != null && layer < _layerMixers.Count && _layerMixers[layer].IsValid())
                {
                    _playableGraph.Connect(clipPlayable, 0, _layerMixers[layer], portIndex);
                    _layerMixers[layer].SetInputWeight(portIndex, 0f);
                }

                ext.Clip = clip;
                ext.Playable = clipPlayable;
                ext.Speed = speed;
                ext.Loop = loop;
                ext.Weight = 1f;
                ext.StartTime = startTime;
                ext.IsActive = true;
            }
            else
            {
                ext = default;
            }

            if (_portStates != null && layer < _portStates.Length && portIndex < _portStates[layer].Length)
            {
                _portStates[layer][portIndex].LastFrameRawTime = double.NaN;
                _portStates[layer][portIndex].CurrentLoopCount = 0;
                _portStates[layer][portIndex].StateFinishedFired = false;
                _portStates[layer][portIndex].StateExitedFired = false;
            }
        }

        internal void ClearExternalPort(int layer, int portIndex)
        {
            if (_externalSlots == null || layer < 0 || layer >= _externalSlots.Length) return;
            int slot = GetExternalSlotIndex(portIndex);
            if (slot < 0 || slot >= _externalSlots[layer].Length) return;

            ref var ext = ref _externalSlots[layer][slot];
            if (ext.Playable.IsValid())
            {
                if (_layerMixers != null && layer < _layerMixers.Count && _layerMixers[layer].IsValid())
                {
                    var currentInput = _layerMixers[layer].GetInput(portIndex);
                    if (currentInput.IsValid() && currentInput.Equals(ext.Playable))
                    {
                        _playableGraph.Disconnect(_layerMixers[layer], portIndex);
                    }
                }
                if (_playableGraph.IsValid())
                {
                    _playableGraph.DestroyPlayable(ext.Playable);
                }
            }
            ext = default;

            if (_layerMixers != null && layer < _layerMixers.Count && _layerMixers[layer].IsValid())
            {
                _layerMixers[layer].SetInputWeight(portIndex, 0f);
            }
        }

        internal void ClearAllExternalPorts()
        {
            if (_externalSlots == null) return;
            for (int l = 0; l < _externalSlots.Length; l++)
            {
                if (_externalSlots[l] == null) continue;
                for (int s = 0; s < _externalSlots[l].Length; s++)
                {
                    ClearExternalPort(l, GetExternalPortIndex(s));
                }
            }
        }
    }
}

