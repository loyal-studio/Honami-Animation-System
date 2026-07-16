using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Supported parameter storage types for Honami controllers.
    /// </summary>
    public enum HonamiParameterType
    {
        Float,
        Int,
        Bool,
        Trigger,
        Random
    }

    /// <summary>
    /// Serialized parameter definition stored on a Honami controller.
    /// </summary>
    [Serializable]
    public sealed class HonamiParameter
    {
        public string name;
        public HonamiParameterType type;
        public float defaultFloat;
        public int defaultInt;
        public bool defaultBool;
    }

    /// <summary>
    /// Axis locks applied by a state when constraint processing is enabled.
    /// </summary>
    [Serializable]
    public struct HonamiConstraints
    {
        public bool lockPositionX;
        public bool lockPositionY;
        public bool lockPositionZ;
        public bool lockRotationX;
        public bool lockRotationY;
        public bool lockRotationZ;
        public bool lockScaleX;
        public bool lockScaleY;
        public bool lockScaleZ;

        public bool AnyLocked()
        {
            return lockPositionX || lockPositionY || lockPositionZ ||
                   lockRotationX || lockRotationY || lockRotationZ ||
                   lockScaleX || lockScaleY || lockScaleZ;
        }
    }

    /// <summary>
    /// Serialized layer definition used by a Honami controller.
    /// </summary>
    [Serializable]
    public sealed class HonamiLayer
    {
        public string name = "Base Layer";

        [Range(0f, 1f)]
        public float weight = 1f;

        [Tooltip("Global mask applied to all states in this layer.")]
        public HonamiAvatarMask avatarMask;

        [Tooltip("If true, the layer's output is mirrored based on the Avatar's mirror bones configuration.")]
        public bool mirror = false;

        [Tooltip("When >= 0, this layer inherits states and transitions from an earlier layer. Override individual states to customize them.")]
        public int parentLayerIndex = -1;
    }

    /// <summary>
    /// Primary Honami controller asset containing layers, parameters, graph states, and editor graph metadata.
    /// </summary>
    [CreateAssetMenu(fileName = "NewHonamiController", menuName = "Honami Animation/Controller")]
    public sealed class HonamiController : HonamiRuntimeController
    {
        public List<HonamiLayer> layers = new List<HonamiLayer>() { new HonamiLayer() };
        public List<HonamiParameter> parameters = new List<HonamiParameter>();

        [Tooltip("List of animation states driven by this controller.")]
        public List<HonamiState> states = new List<HonamiState>();

        [SerializeField, HideInInspector]
        private int serializedLayerInheritanceVersion = 0;

        private EffectiveStatesView _effectiveStatesView;

        [HideInInspector]
        public string editorGraphDataJson;

        [HideInInspector]
        public List<HonamiGroupData> groups = new List<HonamiGroupData>();

        [HideInInspector]
        public List<HonamiStickyNoteData> stickyNotes = new List<HonamiStickyNoteData>();

        public override IReadOnlyList<HonamiLayer> ActiveLayers => layers;
        public override IReadOnlyList<HonamiParameter> ActiveParameters => parameters;
        public override IReadOnlyList<HonamiState> ActiveStates => _effectiveStatesView ??= new EffectiveStatesView(this);

        public override HonamiNodeBase GetActiveNode(HonamiState state)
        {
            return state.node;
        }

        public override HonamiNodeBase GetActiveNodeByGuid(string guid)
        {
            var activeStates = ActiveStates;
            for (int i = 0; i < activeStates.Count; i++)
            {
                var activeState = activeStates[i];
                if (activeState != null && activeState.guid == guid)
                {
                    return activeState.node;
                }
            }

            var directState = states.Find(x => x.guid == guid);
            return directState != null ? directState.node : null;
        }

        public override IReadOnlyList<HonamiTransition> GetTransitions(HonamiState state)
        {
            if (state == null)
            {
                return null;
            }

            return _effectiveStatesView != null && state.isVirtualInheritedState
                ? _effectiveStatesView.GetVirtualTransitions(state)
                : state.transitions;
        }

        /// <summary>
        /// Returns true when the layer inherits states and transitions from an earlier layer.
        /// </summary>
        public bool IsLayerInherited(int layerIndex)
        {
            return layerIndex >= 0 &&
                   layerIndex < layers.Count &&
                   layers[layerIndex] != null &&
                   layers[layerIndex].parentLayerIndex >= 0 &&
                   layers[layerIndex].parentLayerIndex < layers.Count &&
                   layers[layerIndex].parentLayerIndex < layerIndex;
        }

        public const string InheritedLayerGuidMarker = "@HonamiInheritedLayer:";

        /// <summary>
        /// Builds the stable GUID used by a virtual or overridden inherited state.
        /// </summary>
        public static string GetInheritedStateGuid(string sourceStateGuid, int layerIndex)
        {
            return string.IsNullOrEmpty(sourceStateGuid) ? null : $"{sourceStateGuid}{InheritedLayerGuidMarker}{layerIndex}";
        }

        /// <summary>
        /// Clears cached inherited-layer states so they will be rebuilt on the next runtime query.
        /// </summary>
        public void ClearEffectiveStateCache()
        {
            _effectiveStatesView?.DestroyVirtualStates();
            _effectiveStatesView = null;
        }

#if UNITY_EDITOR
        public override HonamiController BaseController => this;
        public override bool IsOverride => false;

        /// <summary>
        /// Removes invalid editor references and migrates older layer inheritance data.
        /// </summary>
        public bool SanitizeEditorData()
        {
            bool changed = false;

            if (states != null)
            {
                changed |= states.RemoveAll(s => s == null) > 0;
            }

            if (layers != null)
            {
                changed |= MigrateLayerInheritance();
                changed |= SanitizeLayerInheritance();
            }

            return changed;
        }
#endif

        private void OnValidate()
        {
            ClearEffectiveStateCache();
#if UNITY_EDITOR
            if (HonamiAssetImportGuard.IsImportingAssets)
            {
                return;
            }

            SanitizeEditorData();
#endif
        }

#if UNITY_EDITOR
        private bool MigrateLayerInheritance()
        {
            if (serializedLayerInheritanceVersion != 0)
            {
                return false;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null)
                {
                    layers[i].parentLayerIndex = -1;
                }
            }

            serializedLayerInheritanceVersion = 1;
            return true;
        }

        private bool SanitizeLayerInheritance()
        {
            bool changed = false;

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] == null)
                {
                    continue;
                }

                if (layers[i].parentLayerIndex >= i || layers[i].parentLayerIndex >= layers.Count)
                {
                    layers[i].parentLayerIndex = -1;
                    changed = true;
                }
            }

            return changed;
        }
#endif

        private sealed class EffectiveStatesView : IReadOnlyList<HonamiState>
        {
            private readonly HonamiController _controller;
            private readonly Dictionary<string, HonamiState> _guidToState = new();
            private readonly Dictionary<HonamiState, List<HonamiTransition>> _virtualTransitions = new();
            private HonamiState[] _cache;

            public EffectiveStatesView(HonamiController controller)
            {
                _controller = controller;
            }

            public HonamiState this[int index]
            {
                get
                {
                    EnsureCache();
                    return _cache[index];
                }
            }

            public int Count
            {
                get
                {
                    EnsureCache();
                    return _cache.Length;
                }
            }

            public IReadOnlyList<HonamiTransition> GetVirtualTransitions(HonamiState state)
            {
                EnsureCache();

                if (state == null)
                {
                    return null;
                }

                if (_virtualTransitions.TryGetValue(state, out var transitions))
                {
                    return transitions;
                }

                transitions = BuildVirtualTransitions(state);
                _virtualTransitions[state] = transitions;
                return transitions;
            }

            public void DestroyVirtualStates()
            {
                if (_cache == null)
                {
                    return;
                }

                for (int i = 0; i < _cache.Length; i++)
                {
                    var state = _cache[i];
                    if (state == null || !state.isVirtualInheritedState)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(state);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(state);
                    }
                }

                _cache = null;
                _guidToState.Clear();
                _virtualTransitions.Clear();
            }

            public IEnumerator<HonamiState> GetEnumerator()
            {
                EnsureCache();

                for (int i = 0; i < _cache.Length; i++)
                {
                    yield return _cache[i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            private void EnsureCache()
            {
                if (_cache != null)
                {
                    return;
                }

                var result = new List<HonamiState>();
                _guidToState.Clear();
                _virtualTransitions.Clear();

                AddDirectStates(result);
                AddInheritedLayerStates(result);
                AddStoredInheritedOverrides(result);

                _cache = result.ToArray();
            }

            private void AddDirectStates(List<HonamiState> result)
            {
                if (_controller.states == null)
                {
                    return;
                }

                for (int i = 0; i < _controller.states.Count; i++)
                {
                    var state = _controller.states[i];
                    if (state == null || !string.IsNullOrEmpty(state.inheritedFromStateGuid))
                    {
                        continue;
                    }

                    AddState(result, state);
                }
            }

            private void AddInheritedLayerStates(List<HonamiState> result)
            {
                int layerCount = _controller.layers?.Count ?? 0;
                for (int layer = 0; layer < layerCount; layer++)
                {
                    if (!_controller.IsLayerInherited(layer))
                    {
                        continue;
                    }

                    int parentLayer = _controller.layers[layer].parentLayerIndex;
                    var sourceStates = result
                        .Where(s => s != null && s.layerIndex == parentLayer)
                        .ToList();

                    for (int i = 0; i < sourceStates.Count; i++)
                    {
                        var sourceState = sourceStates[i];
                        if (sourceState == null)
                        {
                            continue;
                        }

                        var overrideState = FindOverrideState(layer, sourceState.guid);
                        if (overrideState != null)
                        {
                            overrideState.layerIndex = layer;
                            overrideState.guid = GetInheritedStateGuid(sourceState.guid, layer);
                            overrideState.inheritedFromStateGuid = sourceState.guid;
                            AddState(result, overrideState);
                            continue;
                        }

                        AddState(result, CreateVirtualState(sourceState, layer));
                    }
                }
            }

            private void AddStoredInheritedOverrides(List<HonamiState> result)
            {
                if (_controller.states == null)
                {
                    return;
                }

                for (int i = 0; i < _controller.states.Count; i++)
                {
                    var state = _controller.states[i];
                    if (state == null || string.IsNullOrEmpty(state.inheritedFromStateGuid))
                    {
                        continue;
                    }

                    if (_guidToState.ContainsKey(state.guid))
                    {
                        continue;
                    }

                    AddState(result, state);
                }
            }

            private void AddState(List<HonamiState> result, HonamiState state)
            {
                if (state == null || string.IsNullOrEmpty(state.guid))
                {
                    return;
                }

                result.Add(state);
                _guidToState[state.guid] = state;
            }

            private HonamiState FindOverrideState(int layerIndex, string sourceGuid)
            {
                if (_controller.states == null || string.IsNullOrEmpty(sourceGuid))
                {
                    return null;
                }

                string inheritedGuid = GetInheritedStateGuid(sourceGuid, layerIndex);

                for (int i = 0; i < _controller.states.Count; i++)
                {
                    var state = _controller.states[i];
                    if (state == null || state.layerIndex != layerIndex)
                    {
                        continue;
                    }

                    if (state.inheritedFromStateGuid == sourceGuid || state.guid == inheritedGuid)
                    {
                        return state;
                    }
                }

                return null;
            }

            private HonamiState CreateVirtualState(HonamiState sourceState, int layerIndex)
            {
                var virtualState = ScriptableObject.CreateInstance<HonamiState>();
                virtualState.hideFlags = HideFlags.HideAndDontSave;
                virtualState.name = $"{sourceState.name}_Inherited_Layer_{layerIndex}";
                virtualState.stateName = sourceState.stateName;
                virtualState.layerIndex = layerIndex;
                virtualState.isDefaultState = sourceState.isDefaultState;
                virtualState.node = sourceState.node;
                virtualState.loop = sourceState.loop;
                virtualState.speed = sourceState.speed;
                virtualState.isReversed = sourceState.isReversed;
                virtualState.weight = sourceState.weight;
                virtualState.events = sourceState.events;
                virtualState.parameterAssignments = sourceState.parameterAssignments;
                virtualState.subNodes = sourceState.subNodes;
                virtualState.constraints = sourceState.constraints;
                virtualState.avatarMask = sourceState.avatarMask;
                virtualState.mirror = sourceState.mirror;
                virtualState.editorPosition = sourceState.editorPosition + new Vector2(36f, 36f);
                virtualState.guid = GetInheritedStateGuid(sourceState.guid, layerIndex);
                virtualState.inheritedFromStateGuid = sourceState.guid;
                virtualState.isVirtualInheritedState = true;
                virtualState.inheritedSourceState = sourceState;
                virtualState.linkedActionId = sourceState.linkedActionId;
                virtualState.tags = sourceState.tags;
                return virtualState;
            }

            private List<HonamiTransition> BuildVirtualTransitions(HonamiState state)
            {
                var transitions = new List<HonamiTransition>();
                var sourceState = state.inheritedSourceState;
                if (sourceState == null)
                {
                    return transitions;
                }

                var sourceTransitions = _controller.GetTransitions(sourceState);
                if (sourceTransitions == null)
                {
                    return transitions;
                }

                for (int i = 0; i < sourceTransitions.Count; i++)
                {
                    var sourceTransition = sourceTransitions[i];
                    if (sourceTransition == null)
                    {
                        continue;
                    }

                    var clone = JsonUtility.FromJson<HonamiTransition>(JsonUtility.ToJson(sourceTransition));
                    clone.id = $"{sourceTransition.id}@L{state.layerIndex}";

                    if (!string.IsNullOrEmpty(clone.targetStateGuid) &&
                        _guidToState.TryGetValue(clone.targetStateGuid, out var targetState) &&
                        targetState.layerIndex == sourceState.layerIndex)
                    {
                        clone.targetStateGuid = GetInheritedStateGuid(clone.targetStateGuid, state.layerIndex);
                    }

                    transitions.Add(clone);
                }

                return transitions;
            }
        }
    }
}
