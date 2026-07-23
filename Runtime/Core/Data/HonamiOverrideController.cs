using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Per-sub-node field override, matched to the parent sub-node by its stable OverrideId.
    /// </summary>
    [Serializable]
    public sealed class HonamiSubNodeFieldOverride
    {
        public string subNodeId;
        public List<string> modifiedPaths = new List<string>();
    }

    /// <summary>
    /// Prefab-style override record for a single inherited state. Unmodified fields keep inheriting from the
    /// parent; only the serialized fields listed in the modified path sets are owned locally. State scalar/struct
    /// fields and node fields are tracked at top-level granularity; transitions and sub-nodes are tracked
    /// per-element.
    /// </summary>
    [Serializable]
    public sealed class HonamiOverrideEntry
    {
        public string parentStateGuid;

        [Tooltip("Baked copy of the inherited state that carries the locally overridden field values. Its node is the baked node copy.")]
        public HonamiState effectiveState;

        [Tooltip("Top-level serialized field names on the state that are overridden locally (e.g. weight, avatarMask). Excludes transitions and subNodes.")]
        public List<string> modifiedStatePaths = new List<string>();

        [Tooltip("Top-level serialized field names on the node that are overridden locally.")]
        public List<string> modifiedNodePaths = new List<string>();

        [Tooltip("True when the node type itself was replaced relative to the parent.")]
        public bool nodeTypeOverridden;

        [Tooltip("Parent transition ids whose fields are overridden locally (matched by transition id).")]
        public List<string> modifiedTransitionIds = new List<string>();

        [Tooltip("Parent transition ids deleted locally.")]
        public List<string> removedParentTransitionIds = new List<string>();

        [Tooltip("Transition ids added locally (not present on the parent).")]
        public List<string> addedTransitionIds = new List<string>();

        [Tooltip("Parent sub-node ids deleted locally (matched by OverrideId).")]
        public List<string> removedParentSubNodeIds = new List<string>();

        [Tooltip("Sub-node ids added locally (not present on the parent).")]
        public List<string> addedSubNodeIds = new List<string>();

        [Tooltip("Per-sub-node field overrides, keyed by the sub-node's OverrideId.")]
        public List<HonamiSubNodeFieldOverride> subNodeFieldOverrides = new List<HonamiSubNodeFieldOverride>();

        public bool HasAnyOverride =>
            nodeTypeOverridden ||
            (modifiedStatePaths != null && modifiedStatePaths.Count > 0) ||
            (modifiedNodePaths != null && modifiedNodePaths.Count > 0) ||
            (modifiedTransitionIds != null && modifiedTransitionIds.Count > 0) ||
            (removedParentTransitionIds != null && removedParentTransitionIds.Count > 0) ||
            (addedTransitionIds != null && addedTransitionIds.Count > 0) ||
            (removedParentSubNodeIds != null && removedParentSubNodeIds.Count > 0) ||
            (addedSubNodeIds != null && addedSubNodeIds.Count > 0) ||
            HasSubNodeFieldOverride();

        private bool HasSubNodeFieldOverride()
        {
            if (subNodeFieldOverrides == null)
            {
                return false;
            }

            for (int i = 0; i < subNodeFieldOverrides.Count; i++)
            {
                if (subNodeFieldOverrides[i] != null && subNodeFieldOverrides[i].modifiedPaths.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsStateFieldModified(string field)
            => modifiedStatePaths != null && field != null && modifiedStatePaths.Contains(field);

        public bool IsNodeFieldModified(string field)
            => modifiedNodePaths != null && field != null && modifiedNodePaths.Contains(field);

        public bool IsTransitionModified(string transitionId)
            => transitionId != null &&
               ((modifiedTransitionIds != null && modifiedTransitionIds.Contains(transitionId)) ||
                (removedParentTransitionIds != null && removedParentTransitionIds.Contains(transitionId)));

        public HonamiSubNodeFieldOverride GetSubNodeFieldOverride(string subNodeId, bool createIfMissing)
        {
            if (string.IsNullOrEmpty(subNodeId))
            {
                return null;
            }

            subNodeFieldOverrides ??= new List<HonamiSubNodeFieldOverride>();
            for (int i = 0; i < subNodeFieldOverrides.Count; i++)
            {
                if (subNodeFieldOverrides[i] != null && subNodeFieldOverrides[i].subNodeId == subNodeId)
                {
                    return subNodeFieldOverrides[i];
                }
            }

            if (!createIfMissing)
            {
                return null;
            }

            var created = new HonamiSubNodeFieldOverride { subNodeId = subNodeId };
            subNodeFieldOverrides.Add(created);
            return created;
        }
    }

    /// <summary>
    /// Per-override editor layout position, so moving an inherited node in an override graph does not leak into the parent.
    /// </summary>
    [Serializable]
    public struct HonamiOverrideNodePosition
    {
        public string stateGuid;
        public Vector2 position;
    }

    /// <summary>
    /// Legacy (schema v0) whole-node override record. Kept only so pre-existing assets can be migrated; never
    /// written by current code.
    /// </summary>
    [Serializable]
    public struct HonamiNodeOverride
    {
        public string stateGuid;
        public HonamiNodeBase overrideNode;
    }

    /// <summary>
    /// Legacy (schema v0) whole-list transition override record. Kept only for migration.
    /// </summary>
    [Serializable]
    public struct HonamiStateTransitionsOverride
    {
        public string stateGuid;
        public List<HonamiTransition> transitions;
    }

    /// <summary>
    /// Runtime controller that layers prefab-style per-field overrides on top of a parent controller.
    /// </summary>
    public sealed class HonamiOverrideController : HonamiRuntimeController
    {
        public const int CurrentOverrideSchemaVersion = 1;

        [Tooltip("The base controller to inherit layers, parameters, and states from.")]
        public HonamiRuntimeController parentController;

        [Tooltip("Per-state prefab-style overrides applied on top of the parent controller.")]
        public List<HonamiOverrideEntry> overrides = new List<HonamiOverrideEntry>();

        [SerializeField, HideInInspector]
        private int overrideSchemaVersion;

        [HideInInspector]
        public List<HonamiNodeOverride> nodeOverrides = new List<HonamiNodeOverride>();

        [HideInInspector]
        public List<HonamiStateTransitionsOverride> transitionOverrides = new List<HonamiStateTransitionsOverride>();

        public int OverrideSchemaVersion
        {
            get => overrideSchemaVersion;
            set => overrideSchemaVersion = value;
        }

        public bool NeedsMigration => overrideSchemaVersion < CurrentOverrideSchemaVersion;

        [Tooltip("Additional layers exclusively for this override controller.")]
        public List<HonamiLayer> additionalLayers = new List<HonamiLayer>();

        [Tooltip("Additional parameters exclusively for this override controller.")]
        public List<HonamiParameter> additionalParameters = new List<HonamiParameter>();

        [HideInInspector]
        public List<HonamiState> additionalStates = new List<HonamiState>();

        [HideInInspector]
        public List<HonamiGroupData> additionalGroups = new List<HonamiGroupData>();

        [HideInInspector]
        public List<HonamiStickyNoteData> additionalStickyNotes = new List<HonamiStickyNoteData>();

        [HideInInspector]
        public List<HonamiOverrideNodePosition> nodePositions = new List<HonamiOverrideNodePosition>();

        private CompositeListView<HonamiLayer> _layersView;
        private CompositeListView<HonamiParameter> _parametersView;
        private CompositeListView<HonamiState> _statesView;

        public override IReadOnlyList<HonamiLayer> ActiveLayers
            => _layersView ??= new CompositeListView<HonamiLayer>(
                GetParentLayers,
                () => additionalLayers);

        public override IReadOnlyList<HonamiParameter> ActiveParameters
            => _parametersView ??= new CompositeListView<HonamiParameter>(
                GetParentParameters,
                () => additionalParameters);

        public override IReadOnlyList<HonamiState> ActiveStates
            => _statesView ??= new CompositeListView<HonamiState>(
                GetEffectiveInheritedStates,
                () => additionalStates);

        public override HonamiNodeBase GetActiveNode(HonamiState state)
        {
            return state != null ? state.node : null;
        }

        public override IReadOnlyList<HonamiTransition> GetTransitions(HonamiState state)
        {
            if (state == null)
            {
                return null;
            }

            if (IsOwnedState(state))
            {
                return state.transitions;
            }

            return parentController != null && parentController != this
                ? parentController.GetTransitions(state)
                : state.transitions;
        }

        public override HonamiNodeBase GetActiveNodeByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var activeStates = ActiveStates;
            for (int i = 0; i < activeStates.Count; i++)
            {
                var state = activeStates[i];
                if (state != null && state.guid == guid)
                {
                    return state.node;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the override entry that targets the given inherited parent state, if any.
        /// </summary>
        public HonamiOverrideEntry FindEntry(string parentStateGuid)
        {
            if (string.IsNullOrEmpty(parentStateGuid) || overrides == null)
            {
                return null;
            }

            for (int i = 0; i < overrides.Count; i++)
            {
                if (overrides[i] != null && overrides[i].parentStateGuid == parentStateGuid)
                {
                    return overrides[i];
                }
            }

            return null;
        }

        /// <summary>
        /// True when the state is locally owned by this override (an added state or a baked effective copy).
        /// </summary>
        public bool IsOwnedState(HonamiState state)
        {
            if (state == null)
            {
                return false;
            }

            if (additionalStates != null && additionalStates.Contains(state))
            {
                return true;
            }

            if (overrides != null)
            {
                for (int i = 0; i < overrides.Count; i++)
                {
                    if (overrides[i] != null && overrides[i].effectiveState == state)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryGetNodePosition(string stateGuid, out Vector2 position)
        {
            if (!string.IsNullOrEmpty(stateGuid) && nodePositions != null)
            {
                for (int i = 0; i < nodePositions.Count; i++)
                {
                    if (nodePositions[i].stateGuid == stateGuid)
                    {
                        position = nodePositions[i].position;
                        return true;
                    }
                }
            }

            position = default;
            return false;
        }

        public void SetNodePosition(string stateGuid, Vector2 position)
        {
            if (string.IsNullOrEmpty(stateGuid))
            {
                return;
            }

            nodePositions ??= new List<HonamiOverrideNodePosition>();
            for (int i = 0; i < nodePositions.Count; i++)
            {
                if (nodePositions[i].stateGuid == stateGuid)
                {
                    nodePositions[i] = new HonamiOverrideNodePosition { stateGuid = stateGuid, position = position };
                    return;
                }
            }

            nodePositions.Add(new HonamiOverrideNodePosition { stateGuid = stateGuid, position = position });
        }

        /// <summary>
        /// Clears all runtime views so the next query rebuilds the effective state set.
        /// </summary>
        public void ClearCaches()
        {
            _layersView?.Invalidate();
            _parametersView?.Invalidate();
            _statesView?.Invalidate();
        }

        private void OnEnable()
        {
            ClearCaches();
        }

        private IReadOnlyList<HonamiLayer> GetParentLayers()
        {
            return parentController != null && parentController != this ? parentController.ActiveLayers : null;
        }

        private IReadOnlyList<HonamiParameter> GetParentParameters()
        {
            return parentController != null && parentController != this ? parentController.ActiveParameters : null;
        }

        private IReadOnlyList<HonamiState> GetEffectiveInheritedStates()
        {
            if (parentController == null || parentController == this)
            {
                return null;
            }

            var parentStates = parentController.ActiveStates;
            if (parentStates == null)
            {
                return null;
            }

            var result = new List<HonamiState>(parentStates.Count);
            for (int i = 0; i < parentStates.Count; i++)
            {
                var parentState = parentStates[i];
                if (parentState == null)
                {
                    continue;
                }

                var entry = FindEntry(parentState.guid);
                result.Add(entry != null && entry.effectiveState != null ? entry.effectiveState : parentState);
            }

            return result;
        }

#if UNITY_EDITOR
        public override HonamiController BaseController => parentController != null ? parentController.BaseController : null;
        public override bool IsOverride => true;

        private void OnValidate()
        {
            ClearCaches();

            if (HonamiAssetImportGuard.IsImportingAssets)
            {
                return;
            }

            RemoveCircularParentReference();
        }

        private void RemoveCircularParentReference()
        {
            if (parentController == null)
            {
                return;
            }

            var current = parentController;
            int depth = 0;

            while (current != null && current is HonamiOverrideController overrideController)
            {
                if (overrideController == this)
                {
                    Debug.LogError($"[Honami] Circular dependency detected in HonamiOverrideController '{name}'. Removing parent controller.");
                    parentController = null;
                    break;
                }

                if (depth > 20)
                {
                    Debug.LogError("[Honami] Too many nested override controllers. Removing parent controller.");
                    parentController = null;
                    break;
                }

                current = overrideController.parentController;
                depth++;
            }
        }
#endif

        private sealed class CompositeListView<T> : IReadOnlyList<T>
        {
            private readonly Func<IReadOnlyList<T>> _getParentItems;
            private readonly Func<IReadOnlyList<T>> _getLocalItems;
            private T[] _cache;

            public CompositeListView(Func<IReadOnlyList<T>> getParentItems, Func<IReadOnlyList<T>> getLocalItems)
            {
                _getParentItems = getParentItems;
                _getLocalItems = getLocalItems;
            }

            public T this[int index]
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

            public void Invalidate() => _cache = null;

            public IEnumerator<T> GetEnumerator()
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

                var list = new List<T>();
                AddRangeIfPresent(list, _getParentItems?.Invoke());
                AddRangeIfPresent(list, _getLocalItems?.Invoke());
                _cache = list.ToArray();
            }

            private static void AddRangeIfPresent(List<T> destination, IReadOnlyList<T> source)
            {
                if (source == null)
                {
                    return;
                }

                for (int i = 0; i < source.Count; i++)
                {
                    destination.Add(source[i]);
                }
            }
        }
    }
}
