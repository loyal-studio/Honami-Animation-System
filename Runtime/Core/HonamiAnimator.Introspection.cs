using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public partial class HonamiAnimator
    {
        /// <summary>
        /// Number of states the current controller contributes to the runtime graph.
        /// </summary>
        public int StateCount => _activeStatesCount;

        /// <summary>
        /// Number of layers in the running graph.
        /// </summary>
        public int LayerCount => _layerMixers?.Count ?? 0;

        /// <summary>
        /// Resolves a Honami name hash to a runtime state index.
        /// </summary>
        public bool TryResolveState(int stateHash, out int stateIndex)
        {
            if (_stateNameToIndex.TryGetValue(stateHash, out stateIndex) && stateIndex >= 0 && stateIndex < _activeStatesCount)
            {
                return true;
            }

            stateIndex = -1;
            return false;
        }

        /// <summary>
        /// Resolves a state name to a runtime state index.
        /// </summary>
        public bool TryResolveState(string stateName, out int stateIndex)
            => TryResolveState(StringToHash(stateName), out stateIndex);

        /// <summary>
        /// Resolves an authoring GUID to a runtime state index.
        /// </summary>
        public bool TryResolveStateByGuid(string guid, out int stateIndex)
        {
            if (string.IsNullOrEmpty(guid))
            {
                stateIndex = -1;
                return false;
            }

            return TryResolveStateByGuidHash(guid.GetHashCode(), out stateIndex);
        }

        /// <summary>
        /// Resolves a pre-hashed authoring GUID, as baked into <see cref="HonamiTransition.targetStateGuidHash"/>.
        /// </summary>
        public bool TryResolveStateByGuidHash(int guidHash, out int stateIndex)
        {
            if (_stateGuidToIndex.TryGetValue(guidHash, out stateIndex) && stateIndex >= 0 && stateIndex < _activeStatesCount)
            {
                return true;
            }

            stateIndex = -1;
            return false;
        }

        /// <summary>
        /// Gets the runtime state at an index, or null when the index is out of range.
        /// An int elsewhere on this component is a name hash; index accessors carry the At suffix.
        /// </summary>
        public HonamiState GetStateAt(int stateIndex)
            => _runtimeStates != null && stateIndex >= 0 && stateIndex < _activeStatesCount ? _runtimeStates[stateIndex] : null;

        public HonamiState GetState(string stateName)
            => TryResolveState(stateName, out int index) ? _runtimeStates[index] : null;

        public HonamiState GetStateByGuid(string guid)
            => TryResolveStateByGuid(guid, out int index) ? _runtimeStates[index] : null;

        /// <summary>
        /// Copies every runtime state into <paramref name="buffer"/> and returns how many were written.
        /// </summary>
        public int GetStatesNonAlloc(Span<HonamiState> buffer)
        {
            if (_runtimeStates == null) return 0;

            int count = Mathf.Min(buffer.Length, _activeStatesCount);
            for (int i = 0; i < count; i++) buffer[i] = _runtimeStates[i];
            return count;
        }

        /// <summary>
        /// The state currently owning a layer, or null when the layer is idle.
        /// </summary>
        public HonamiState GetCurrentState(int layer = 0) => GetStateAt(GetActiveStateIndex(layer));

        /// <summary>
        /// Name of the state currently owning a layer, or null when the layer is idle.
        /// </summary>
        public string GetCurrentStateName(int layer = 0)
        {
            HonamiState state = GetCurrentState(layer);
            if (state != null) return state.stateName;
            int idx = GetActiveStateIndex(layer);
            if (IsExternalPort(idx))
            {
                var clip = GetCurrentExternalClip(layer);
                return clip != null ? clip.name : "[External Clip]";
            }
            return null;
        }

        /// <summary>
        /// The state a layer is transitioning away from, or null when no transition is running.
        /// </summary>
        public HonamiState GetPreviousState(int layer = 0) => GetStateAt(GetPreviousStateIndex(layer));

        /// <summary>
        /// Snapshot of a layer's transition bookkeeping. Returns default for an unknown layer.
        /// </summary>
        public HonamiLayerState GetLayerState(int layer)
            => _layerStates != null && layer >= 0 && layer < _layerStates.Length ? _layerStates[layer] : default;

        /// <summary>
        /// Snapshot of one layer mixer port's runtime bookkeeping. Returns default for an unknown port.
        /// </summary>
        public HonamiPortState GetPortState(int layer, int portIndex)
        {
            if (_portStates == null || layer < 0 || layer >= _portStates.Length) return default;

            HonamiPortState[] ports = _portStates[layer];
            return ports != null && portIndex >= 0 && portIndex < ports.Length ? ports[portIndex] : default;
        }

        /// <summary>
        /// Unscaled length of a state as the graph currently evaluates it, including blend-tree resolution.
        /// Returns 0 when the state cannot be measured.
        /// </summary>
        public float GetStateDurationAt(int stateIndex)
        {
            HonamiState state = GetStateAt(stateIndex);
            if (state == null) return 0f;

            return HonamiStateEvaluator.GetUnscaledStateDuration(
                controller, state, stateIndex, GetNodeRuntime(stateIndex), GetStateBlendParam(stateIndex));
        }

        public float GetStateDuration(string stateName)
            => TryResolveState(stateName, out int index) ? GetStateDurationAt(index) : 0f;

        /// <summary>
        /// Playback progress of the state currently owning a layer, looped into 0..1 for looping states.
        /// </summary>
        public float GetCurrentStateProgress(int layer = 0) => GetStateProgress(layer, GetActiveStateIndex(layer));

        /// <summary>
        /// The playable driving a layer mixer port, or <see cref="Playable.Null"/> when there is none.
        /// </summary>
        public Playable GetStatePlayable(int layer, int portIndex)
        {
            if (!TryGetLayerMixer(layer, out AnimationMixerPlayable mixer)) return Playable.Null;
            if (portIndex < 0 || portIndex >= mixer.GetInputCount()) return Playable.Null;

            return mixer.GetInput(portIndex);
        }

        /// <summary>
        /// Current blend weight of a layer mixer port.
        /// </summary>
        public float GetStateWeight(int layer, int portIndex)
        {
            if (!TryGetLayerMixer(layer, out AnimationMixerPlayable mixer)) return 0f;
            if (portIndex < 0 || portIndex >= mixer.GetInputCount()) return 0f;

            return mixer.GetInputWeight(portIndex);
        }

        /// <summary>
        /// True while the given state index is explicitly paused on a layer.
        /// </summary>
        public bool IsStatePausedAt(int stateIndex, int layer = 0)
            => IsLayerInRange(layer) && _pausedStateIndices[layer].Contains(stateIndex);

        /// <summary>
        /// The per-animator runtime object a custom node allocated for a state, or null when the node is stateless.
        /// </summary>
        public HonamiNodeRuntime GetStateRuntimeAt(int stateIndex) => GetNodeRuntime(stateIndex);

        /// <summary>
        /// The node the controller currently evaluates for a state, after override resolution.
        /// </summary>
        public HonamiNodeBase GetStateNodeAt(int stateIndex)
        {
            HonamiState state = GetStateAt(stateIndex);
            return state != null && controller != null ? controller.GetActiveNode(state) : null;
        }

        /// <summary>
        /// True while a timed controller swap started by <see cref="SetController"/> is still blending.
        /// </summary>
        public bool IsTransitioningController => _isTransitioningController;
    }
}
