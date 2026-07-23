using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Core
{
    /// <summary>
    /// Authoring helpers for prefab-style overrides. An overridden inherited state is materialized as a baked
    /// effective copy (sub-asset of the override) that deep-copies its node and sub-nodes. Scalar/struct state
    /// fields and node fields are overridden at top-level granularity; transitions and sub-nodes are tracked
    /// per-element. Everything not locally overridden is re-synced from the parent so inheritance stays live.
    /// </summary>
    public static class HonamiOverrideAuthoring
    {
        private static readonly HashSet<string> AlwaysSyncedStateFields = new HashSet<string>
        {
            "guid", "layerIndex", "inheritedFromStateGuid"
        };

        // Handled by dedicated per-element logic, never by the scalar field system.
        private static readonly HashSet<string> DedicatedStateFields = new HashSet<string>
        {
            "node", "transitions", "subNodes"
        };

        private static readonly HashSet<string> IgnoredStateFields = new HashSet<string>
        {
            "m_Script", "m_Name", "editorPosition"
        };

        private static readonly HashSet<string> IgnoredNodeFields = new HashSet<string>
        {
            "m_Script", "m_Name"
        };

        public static string TopLevelField(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            int dot = propertyPath.IndexOf('.');
            return dot < 0 ? propertyPath : propertyPath.Substring(0, dot);
        }

        public static HonamiState GetParentState(HonamiOverrideController ov, string guid)
        {
            if (ov == null || ov.parentController == null || string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var parentStates = ov.parentController.ActiveStates;
            for (int i = 0; i < parentStates.Count; i++)
            {
                if (parentStates[i] != null && parentStates[i].guid == guid)
                {
                    return parentStates[i];
                }
            }

            return null;
        }

        public static bool ResolveState(HonamiOverrideController ov, HonamiState displayed,
            out HonamiOverrideEntry entry, out HonamiState parentState)
        {
            entry = null;
            parentState = null;
            if (ov == null || displayed == null)
            {
                return false;
            }

            if (ov.overrides != null)
            {
                for (int i = 0; i < ov.overrides.Count; i++)
                {
                    if (ov.overrides[i] != null && ov.overrides[i].effectiveState == displayed)
                    {
                        entry = ov.overrides[i];
                        parentState = GetParentState(ov, entry.parentStateGuid);
                        return true;
                    }
                }
            }

            entry = ov.FindEntry(displayed.guid);
            parentState = displayed;
            return true;
        }

        // ── Materialization ───────────────────────────────────────────────────

        public static HonamiState CreateTransientCopy(HonamiState parentState)
        {
            if (parentState == null)
            {
                return null;
            }

            var copy = Object.Instantiate(parentState);
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.name = parentState.name;

            if (parentState.node != null)
            {
                var nodeCopy = Object.Instantiate(parentState.node);
                nodeCopy.hideFlags = HideFlags.HideAndDontSave;
                nodeCopy.name = parentState.node.name;
                copy.node = nodeCopy;
            }

            copy.subNodes = DeepCopySubNodes(parentState.subNodes, HideFlags.HideAndDontSave);
            return copy;
        }

        public static HonamiOverrideEntry PromoteTransient(HonamiOverrideController ov, string parentGuid, HonamiState transient)
        {
            if (ov == null || transient == null)
            {
                return null;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Override Field");

            transient.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(transient, ov);
            Undo.RegisterCreatedObjectUndo(transient, "Override Field");

            if (transient.node != null)
            {
                transient.node.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(transient.node, ov);
                Undo.RegisterCreatedObjectUndo(transient.node, "Override Field");
            }

            if (transient.subNodes != null)
            {
                foreach (var subNode in transient.subNodes)
                {
                    if (subNode == null) continue;
                    subNode.hideFlags = HideFlags.None;
                    AssetDatabase.AddObjectToAsset(subNode, ov);
                    Undo.RegisterCreatedObjectUndo(subNode, "Override Field");
                }
            }

            var entry = ov.FindEntry(parentGuid);
            if (entry == null)
            {
                entry = new HonamiOverrideEntry { parentStateGuid = parentGuid };
                ov.overrides.Add(entry);
            }

            entry.effectiveState = transient;
            ov.ClearCaches();
            EditorUtility.SetDirty(ov);
            HonamiAnimationSystem.Editor.HonamiGraphView.DeferredSave();
            return entry;
        }

        public static HonamiState EnsureEffectiveState(HonamiOverrideController ov, HonamiState parentState)
        {
            if (ov == null || parentState == null)
            {
                return null;
            }

            var entry = ov.FindEntry(parentState.guid);
            if (entry != null && entry.effectiveState != null)
            {
                return entry.effectiveState;
            }

            var transient = CreateTransientCopy(parentState);
            transient.transitions = CloneTransitions(ov.parentController.GetTransitions(parentState));
            PromoteTransient(ov, parentState.guid, transient);
            return transient;
        }

        public static HonamiState CreateTransientForTransitions(HonamiOverrideController ov, HonamiState parentState)
        {
            if (ov == null || parentState == null)
            {
                return null;
            }

            var transient = CreateTransientCopy(parentState);
            transient.transitions = CloneTransitions(ov.parentController.GetTransitions(parentState));
            return transient;
        }

        public static bool TransitionsDifferFromParent(HonamiOverrideController ov, HonamiState effective, HonamiState parent)
        {
            if (ov == null || effective == null || parent == null)
            {
                return false;
            }

            var parentById = new Dictionary<string, HonamiTransition>();
            var parentTransitions = ov.parentController.GetTransitions(parent);
            int parentCount = 0;
            if (parentTransitions != null)
            {
                parentCount = parentTransitions.Count;
                for (int i = 0; i < parentTransitions.Count; i++)
                {
                    var t = parentTransitions[i];
                    if (t != null && !string.IsNullOrEmpty(t.id)) parentById[t.id] = t;
                }
            }

            int effCount = effective.transitions?.Count ?? 0;
            if (effCount != parentCount)
            {
                return true;
            }

            if (effective.transitions != null)
            {
                for (int i = 0; i < effective.transitions.Count; i++)
                {
                    var et = effective.transitions[i];
                    if (et == null) continue;
                    if (!parentById.TryGetValue(et.id, out var pt) || !TransitionEquals(et, pt))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void DestroyTransient(HonamiState transient)
        {
            if (transient == null || AssetDatabase.Contains(transient))
            {
                return;
            }

            if (transient.subNodes != null)
            {
                foreach (var subNode in transient.subNodes)
                {
                    if (subNode != null && !AssetDatabase.Contains(subNode)) Object.DestroyImmediate(subNode);
                }
            }

            if (transient.node != null && !AssetDatabase.Contains(transient.node)) Object.DestroyImmediate(transient.node);
            Object.DestroyImmediate(transient);
        }

        // ── Scalar state / node fields ────────────────────────────────────────

        public static bool DiffersFromParent(HonamiState effective, HonamiState parent)
        {
            if (effective == null || parent == null)
            {
                return false;
            }

            var pSO = new SerializedObject(parent);
            var eSO = new SerializedObject(effective);
            foreach (var field in TopLevelFields(eSO))
            {
                if (DedicatedStateFields.Contains(field) || IgnoredStateFields.Contains(field) || AlwaysSyncedStateFields.Contains(field))
                {
                    continue;
                }

                if (!FieldEquals(pSO, eSO, field))
                {
                    return true;
                }
            }

            bool sameType = (parent.node == null) == (effective.node == null) &&
                (parent.node == null || parent.node.GetType() == effective.node.GetType());
            if (!sameType)
            {
                return true;
            }

            if (parent.node != null && effective.node != null)
            {
                var pnSO = new SerializedObject(parent.node);
                var enSO = new SerializedObject(effective.node);
                foreach (var field in TopLevelFields(enSO))
                {
                    if (IgnoredNodeFields.Contains(field))
                    {
                        continue;
                    }

                    if (!FieldEquals(pnSO, enSO, field))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void RefreshStateModified(HonamiOverrideController ov, HonamiOverrideEntry entry)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || entry.effectiveState == null)
            {
                return;
            }

            var pSO = new SerializedObject(parent);
            var eSO = new SerializedObject(entry.effectiveState);

            foreach (var field in TopLevelFields(eSO))
            {
                if (DedicatedStateFields.Contains(field) || IgnoredStateFields.Contains(field) || AlwaysSyncedStateFields.Contains(field))
                {
                    continue;
                }

                if (!FieldEquals(pSO, eSO, field) && !entry.modifiedStatePaths.Contains(field))
                {
                    entry.modifiedStatePaths.Add(field);
                }
            }

            RefreshNodeModified(entry, parent);
            RefreshTransitionModified(ov, entry, parent);
            EditorUtility.SetDirty(ov);
        }

        private static void RefreshNodeModified(HonamiOverrideEntry entry, HonamiState parent)
        {
            if (entry.nodeTypeOverridden || parent.node == null || entry.effectiveState.node == null)
            {
                return;
            }

            if (parent.node.GetType() != entry.effectiveState.node.GetType())
            {
                return;
            }

            var pnSO = new SerializedObject(parent.node);
            var enSO = new SerializedObject(entry.effectiveState.node);

            foreach (var field in TopLevelFields(enSO))
            {
                if (IgnoredNodeFields.Contains(field))
                {
                    continue;
                }

                if (!FieldEquals(pnSO, enSO, field) && !entry.modifiedNodePaths.Contains(field))
                {
                    entry.modifiedNodePaths.Add(field);
                }
            }
        }

        public static void MarkNodeTypeOverride(HonamiOverrideController ov, HonamiOverrideEntry entry, HonamiNodeBase newNode)
        {
            if (ov == null || entry == null || entry.effectiveState == null || newNode == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Change Node Type");

            var oldNode = entry.effectiveState.node;
            if (oldNode != null && IsSubAssetOf(oldNode, ov))
            {
                Undo.DestroyObjectImmediate(oldNode);
            }

            newNode.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(newNode, ov);
            Undo.RegisterCreatedObjectUndo(newNode, "Change Node Type");
            entry.effectiveState.node = newNode;
            entry.nodeTypeOverridden = true;
            entry.modifiedNodePaths.Clear();

            Finish(ov, entry);
        }

        public static void RevertStateField(HonamiOverrideController ov, HonamiOverrideEntry entry, string field)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || entry.effectiveState == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Revert Field");
            Undo.RecordObject(entry.effectiveState, "Revert Field");
            var pSO = new SerializedObject(parent);
            var eSO = new SerializedObject(entry.effectiveState);
            CopyField(pSO, eSO, field);
            eSO.ApplyModifiedProperties();
            entry.modifiedStatePaths.Remove(field);
            Finish(ov, entry);
        }

        public static void ApplyStateFieldToParent(HonamiOverrideController ov, HonamiOverrideEntry entry, string field)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || entry.effectiveState == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Apply Field To Parent");
            Undo.RecordObject(parent, "Apply Field To Parent");
            var pSO = new SerializedObject(parent);
            var eSO = new SerializedObject(entry.effectiveState);
            CopyField(eSO, pSO, field);
            pSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(parent);
            entry.modifiedStatePaths.Remove(field);
            ov.parentController?.BaseController?.ClearEffectiveStateCache();
            Finish(ov, entry);
        }

        public static void RevertNodeField(HonamiOverrideController ov, HonamiOverrideEntry entry, string field)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || parent.node == null || entry.effectiveState?.node == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Revert Field");
            Undo.RecordObject(entry.effectiveState.node, "Revert Field");
            var pnSO = new SerializedObject(parent.node);
            var enSO = new SerializedObject(entry.effectiveState.node);
            CopyField(pnSO, enSO, field);
            enSO.ApplyModifiedProperties();
            entry.modifiedNodePaths.Remove(field);
            Finish(ov, entry);
        }

        public static void ApplyNodeFieldToParent(HonamiOverrideController ov, HonamiOverrideEntry entry, string field)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || parent.node == null || entry.effectiveState?.node == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Apply Field To Parent");
            Undo.RecordObject(parent.node, "Apply Field To Parent");
            var pnSO = new SerializedObject(parent.node);
            var enSO = new SerializedObject(entry.effectiveState.node);
            CopyField(enSO, pnSO, field);
            pnSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(parent.node);
            entry.modifiedNodePaths.Remove(field);
            ov.parentController?.BaseController?.ClearEffectiveStateCache();
            Finish(ov, entry);
        }

        // ── Transitions (per element, matched by id) ──────────────────────────

        public static void RefreshTransitionModified(HonamiOverrideController ov, HonamiOverrideEntry entry, HonamiState parent)
        {
            if (entry?.effectiveState == null || parent == null)
            {
                return;
            }

            var parentById = new Dictionary<string, HonamiTransition>();
            var parentTransitions = ov.parentController.GetTransitions(parent);
            if (parentTransitions != null)
            {
                for (int i = 0; i < parentTransitions.Count; i++)
                {
                    var t = parentTransitions[i];
                    if (t != null && !string.IsNullOrEmpty(t.id)) parentById[t.id] = t;
                }
            }

            var effTransitions = entry.effectiveState.transitions;
            if (effTransitions == null)
            {
                return;
            }

            for (int i = 0; i < effTransitions.Count; i++)
            {
                var et = effTransitions[i];
                if (et == null || string.IsNullOrEmpty(et.id)) continue;
                if (!parentById.TryGetValue(et.id, out var pt)) continue;

                if (!TransitionEquals(et, pt) && !entry.modifiedTransitionIds.Contains(et.id))
                {
                    entry.modifiedTransitionIds.Add(et.id);
                }
            }
        }

        public static void RemoveInheritedTransition(HonamiOverrideController ov, HonamiOverrideEntry entry, string transitionId)
        {
            if (ov == null || entry?.effectiveState == null || string.IsNullOrEmpty(transitionId))
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Remove Transition");
            var parent = GetParentState(ov, entry.parentStateGuid);
            bool isParentTransition = false;
            var parentTransitions = ov.parentController.GetTransitions(parent);
            if (parentTransitions != null)
            {
                for (int i = 0; i < parentTransitions.Count; i++)
                {
                    if (parentTransitions[i] != null && parentTransitions[i].id == transitionId) { isParentTransition = true; break; }
                }
            }

            entry.effectiveState.transitions?.RemoveAll(t => t != null && t.id == transitionId);
            entry.modifiedTransitionIds.Remove(transitionId);
            if (isParentTransition && !entry.removedParentTransitionIds.Contains(transitionId))
            {
                entry.removedParentTransitionIds.Add(transitionId);
            }

            Finish(ov, entry);
        }

        public static void RevertTransition(HonamiOverrideController ov, HonamiOverrideEntry entry, string transitionId)
        {
            if (ov == null || entry == null || string.IsNullOrEmpty(transitionId))
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Revert Transition");
            entry.modifiedTransitionIds.Remove(transitionId);
            entry.removedParentTransitionIds.Remove(transitionId);
            var parent = GetParentState(ov, entry.parentStateGuid);
            if (parent != null)
            {
                ResyncTransitions(ov, entry, parent);
            }

            Finish(ov, entry);
        }

        // ── Sub-nodes (deep copied, per element, matched by OverrideId) ───────

        public static bool FindSubNodeOwner(HonamiOverrideController ov, HonamiSubNodeBase subNode,
            out HonamiOverrideEntry entry)
        {
            entry = null;
            if (ov == null || subNode == null || ov.overrides == null)
            {
                return false;
            }

            for (int i = 0; i < ov.overrides.Count; i++)
            {
                var e = ov.overrides[i];
                var subs = e?.effectiveState?.subNodes;
                if (subs == null) continue;
                for (int j = 0; j < subs.Count; j++)
                {
                    if (subs[j] == subNode)
                    {
                        entry = e;
                        return true;
                    }
                }
            }

            return false;
        }

        public static void AddSubNodeToOverride(HonamiOverrideController ov, HonamiState state, HonamiSubNodeBase instance)
        {
            if (ov == null || state == null || instance == null)
            {
                return;
            }

            HonamiState target = ov.IsOwnedState(state) ? state : EnsureEffectiveState(ov, state);
            ResolveState(ov, target, out var entry, out _);

            Undo.RegisterCompleteObjectUndo(ov, "Add Sub-Node");
            instance.SetOverrideId(System.Guid.NewGuid().ToString());
            instance.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(instance, ov);
            Undo.RegisterCreatedObjectUndo(instance, "Add Sub-Node");

            target.subNodes ??= new List<HonamiSubNodeBase>();
            target.subNodes.Add(instance);
            EditorUtility.SetDirty(target);

            if (entry != null && !entry.addedSubNodeIds.Contains(instance.OverrideId))
            {
                entry.addedSubNodeIds.Add(instance.OverrideId);
            }

            Finish(ov, entry);
        }

        public static void RemoveSubNodeFromOverride(HonamiOverrideController ov, HonamiOverrideEntry entry, string subNodeId)
        {
            if (ov == null || entry?.effectiveState?.subNodes == null || string.IsNullOrEmpty(subNodeId))
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Remove Sub-Node");

            HonamiSubNodeBase removed = null;
            var subs = entry.effectiveState.subNodes;
            for (int i = subs.Count - 1; i >= 0; i--)
            {
                if (subs[i] != null && subs[i].OverrideId == subNodeId)
                {
                    removed = subs[i];
                    subs.RemoveAt(i);
                }
            }

            if (removed != null && IsSubAssetOf(removed, ov)) Undo.DestroyObjectImmediate(removed);

            bool isParentSubNode = false;
            var parent = GetParentState(ov, entry.parentStateGuid);
            if (parent?.subNodes != null)
            {
                foreach (var ps in parent.subNodes)
                {
                    if (ps != null && ps.OverrideId == subNodeId) { isParentSubNode = true; break; }
                }
            }

            var record = entry.GetSubNodeFieldOverride(subNodeId, false);
            if (record != null) entry.subNodeFieldOverrides.Remove(record);
            entry.addedSubNodeIds.Remove(subNodeId);
            if (isParentSubNode && !entry.removedParentSubNodeIds.Contains(subNodeId))
            {
                entry.removedParentSubNodeIds.Add(subNodeId);
            }

            Finish(ov, entry);
        }

        public static void RevertSubNode(HonamiOverrideController ov, HonamiOverrideEntry entry, string subNodeId)
        {
            if (ov == null || entry == null || string.IsNullOrEmpty(subNodeId))
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(ov, "Revert Sub-Node");
            var record = entry.GetSubNodeFieldOverride(subNodeId, false);
            if (record != null)
            {
                record.modifiedPaths.Clear();
                entry.subNodeFieldOverrides.Remove(record);
            }

            entry.removedParentSubNodeIds.Remove(subNodeId);
            var parent = GetParentState(ov, entry.parentStateGuid);
            if (parent != null)
            {
                ResyncSubNodes(ov, entry, parent);
            }

            Finish(ov, entry);
        }

        public static void RefreshSubNodeModified(HonamiOverrideController ov, HonamiOverrideEntry entry, HonamiSubNodeBase effSub)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || effSub == null || parent.subNodes == null) return;

            string id = effSub.OverrideId;
            if (string.IsNullOrEmpty(id)) return;

            HonamiSubNodeBase parentSub = null;
            foreach (var ps in parent.subNodes)
            {
                if (ps != null && ps.OverrideId == id) { parentSub = ps; break; }
            }
            if (parentSub == null || parentSub.GetType() != effSub.GetType()) return;

            var record = entry.GetSubNodeFieldOverride(id, true);
            var pSO = new SerializedObject(parentSub);
            var eSO = new SerializedObject(effSub);
            foreach (var field in TopLevelFields(eSO))
            {
                if (IgnoredNodeFields.Contains(field) || field == "overrideId") continue;
                if (!FieldEquals(pSO, eSO, field) && !record.modifiedPaths.Contains(field))
                {
                    record.modifiedPaths.Add(field);
                }
            }

            EditorUtility.SetDirty(ov);
        }

        // ── Revert-all / removal ──────────────────────────────────────────────

        public static void RevertAll(HonamiOverrideController ov, HonamiOverrideEntry entry)
        {
            RemoveEntry(ov, entry);
        }

        public static void RemoveEntry(HonamiOverrideController ov, HonamiOverrideEntry entry)
        {
            RemoveEntryInternal(ov, entry, true);
        }

        private static void RemoveEntryInternal(HonamiOverrideController ov, HonamiOverrideEntry entry, bool withUndo)
        {
            if (ov == null || entry == null)
            {
                return;
            }

            if (withUndo) Undo.RegisterCompleteObjectUndo(ov, "Revert Overrides");

            if (entry.effectiveState != null)
            {
                if (entry.effectiveState.subNodes != null)
                {
                    foreach (var subNode in entry.effectiveState.subNodes)
                    {
                        if (subNode != null && IsSubAssetOf(subNode, ov)) DestroySubAsset(subNode, withUndo);
                    }
                }

                var node = entry.effectiveState.node;
                if (node != null && IsSubAssetOf(node, ov)) DestroySubAsset(node, withUndo);
                if (IsSubAssetOf(entry.effectiveState, ov)) DestroySubAsset(entry.effectiveState, withUndo);
            }

            ov.overrides.Remove(entry);
            ov.ClearCaches();
            EditorUtility.SetDirty(ov);
            HonamiAnimationSystem.Editor.HonamiGraphView.DeferredSave();
        }

        // ── Legacy migration (schema v0 → v1) ─────────────────────────────────

        /// <summary>
        /// Converts pre-existing whole-node/whole-transition overrides (legacy <c>nodeOverrides</c> /
        /// <c>transitionOverrides</c>) into the current per-field model, then clears the legacy data. Runs once,
        /// gated by the asset's schema version. Idempotent and safe to call from any resync trigger.
        /// </summary>
        public static void MigrateLegacy(HonamiOverrideController ov)
        {
            if (ov == null || !ov.NeedsMigration || ov.parentController == null)
            {
                return;
            }

            bool hadData = (ov.nodeOverrides != null && ov.nodeOverrides.Count > 0) ||
                           (ov.transitionOverrides != null && ov.transitionOverrides.Count > 0);

            if (hadData)
            {
                Undo.RegisterCompleteObjectUndo(ov, "Migrate Honami Override");

                var guids = new HashSet<string>();
                if (ov.nodeOverrides != null)
                {
                    foreach (var no in ov.nodeOverrides)
                    {
                        if (!string.IsNullOrEmpty(no.stateGuid)) guids.Add(no.stateGuid);
                    }
                }
                if (ov.transitionOverrides != null)
                {
                    foreach (var to in ov.transitionOverrides)
                    {
                        if (!string.IsNullOrEmpty(to.stateGuid)) guids.Add(to.stateGuid);
                    }
                }

                foreach (var guid in guids)
                {
                    var parent = GetParentState(ov, guid);
                    if (parent == null) continue;

                    var eff = EnsureEffectiveState(ov, parent);
                    ResolveState(ov, eff, out var entry, out _);
                    if (entry == null) continue;

                    HonamiNodeBase legacyNode = null;
                    if (ov.nodeOverrides != null)
                    {
                        foreach (var no in ov.nodeOverrides)
                        {
                            if (no.stateGuid == guid && no.overrideNode != null) { legacyNode = no.overrideNode; break; }
                        }
                    }

                    if (legacyNode != null)
                    {
                        if (eff.node != null && eff.node != legacyNode && IsSubAssetOf(eff.node, ov))
                        {
                            Object.DestroyImmediate(eff.node, true);
                        }
                        eff.node = legacyNode;
                        entry.nodeTypeOverridden = true;
                        entry.modifiedNodePaths.Clear();
                    }

                    if (ov.transitionOverrides != null)
                    {
                        foreach (var to in ov.transitionOverrides)
                        {
                            if (to.stateGuid == guid) { ApplyLegacyTransitions(ov, entry, parent, to.transitions); break; }
                        }
                    }

                    EditorUtility.SetDirty(eff);
                }

                ov.nodeOverrides.Clear();
                ov.transitionOverrides.Clear();
            }

            ov.OverrideSchemaVersion = HonamiOverrideController.CurrentOverrideSchemaVersion;
            ov.ClearCaches();
            EditorUtility.SetDirty(ov);
            HonamiAnimationSystem.Editor.HonamiGraphView.DeferredSave();
        }

        private static void ApplyLegacyTransitions(HonamiOverrideController ov, HonamiOverrideEntry entry,
            HonamiState parent, List<HonamiTransition> legacyTransitions)
        {
            var eff = entry.effectiveState;
            eff.transitions = CloneTransitions(legacyTransitions);

            var parentIds = new HashSet<string>();
            var parentTransitions = ov.parentController.GetTransitions(parent);
            if (parentTransitions != null)
            {
                foreach (var pt in parentTransitions)
                {
                    if (pt != null && !string.IsNullOrEmpty(pt.id)) parentIds.Add(pt.id);
                }
            }

            var listIds = new HashSet<string>();
            if (eff.transitions != null)
            {
                foreach (var t in eff.transitions)
                {
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;
                    listIds.Add(t.id);

                    if (parentIds.Contains(t.id))
                    {
                        if (!entry.modifiedTransitionIds.Contains(t.id)) entry.modifiedTransitionIds.Add(t.id);
                    }
                    else if (!entry.addedTransitionIds.Contains(t.id))
                    {
                        entry.addedTransitionIds.Add(t.id);
                    }
                }
            }

            foreach (var pid in parentIds)
            {
                if (!listIds.Contains(pid) && !entry.removedParentTransitionIds.Contains(pid))
                {
                    entry.removedParentTransitionIds.Add(pid);
                }
            }
        }

        // ── Resync ────────────────────────────────────────────────────────────

        public static void ResyncFromParent(HonamiOverrideController ov)
        {
            if (ov == null || ov.parentController == null)
            {
                return;
            }

            MigrateLegacy(ov);

            if (ov.overrides == null)
            {
                return;
            }

            bool changed = false;

            for (int i = ov.overrides.Count - 1; i >= 0; i--)
            {
                var entry = ov.overrides[i];
                if (entry == null)
                {
                    ov.overrides.RemoveAt(i);
                    changed = true;
                    continue;
                }

                var parent = GetParentState(ov, entry.parentStateGuid);
                if (parent == null || entry.effectiveState == null)
                {
                    RemoveEntryInternal(ov, entry, false);
                    changed = true;
                    continue;
                }

                if (!entry.HasAnyOverride)
                {
                    RemoveEntryInternal(ov, entry, false);
                    changed = true;
                    continue;
                }

                changed |= ResyncEntry(ov, entry, parent);
            }

            if (changed)
            {
                ov.ClearCaches();
                EditorUtility.SetDirty(ov);
            }
        }

        private static bool ResyncEntry(HonamiOverrideController ov, HonamiOverrideEntry entry, HonamiState parent)
        {
            var eff = entry.effectiveState;

            HandleNodeType(ov, entry, parent);

            var pSO = new SerializedObject(parent);
            var eSO = new SerializedObject(eff);
            foreach (var field in TopLevelFields(pSO))
            {
                if (DedicatedStateFields.Contains(field) || IgnoredStateFields.Contains(field))
                {
                    continue;
                }

                if (!AlwaysSyncedStateFields.Contains(field) && entry.modifiedStatePaths.Contains(field))
                {
                    continue;
                }

                CopyField(pSO, eSO, field);
            }
            eSO.ApplyModifiedPropertiesWithoutUndo();

            if (!entry.nodeTypeOverridden && parent.node != null && eff.node != null &&
                parent.node.GetType() == eff.node.GetType())
            {
                var pnSO = new SerializedObject(parent.node);
                var enSO = new SerializedObject(eff.node);
                foreach (var field in TopLevelFields(pnSO))
                {
                    if (IgnoredNodeFields.Contains(field) || entry.modifiedNodePaths.Contains(field))
                    {
                        continue;
                    }

                    CopyField(pnSO, enSO, field);
                }
                enSO.ApplyModifiedPropertiesWithoutUndo();
            }

            ResyncTransitions(ov, entry, parent);
            ResyncSubNodes(ov, entry, parent);

            EditorUtility.SetDirty(eff);
            if (eff.node != null)
            {
                EditorUtility.SetDirty(eff.node);
            }

            return true;
        }

        private static void ResyncTransitions(HonamiOverrideController ov, HonamiOverrideEntry entry, HonamiState parent)
        {
            var eff = entry.effectiveState;
            var parentTransitions = ov.parentController.GetTransitions(parent);

            var effById = new Dictionary<string, HonamiTransition>();
            if (eff.transitions != null)
            {
                for (int i = 0; i < eff.transitions.Count; i++)
                {
                    var t = eff.transitions[i];
                    if (t != null && !string.IsNullOrEmpty(t.id)) effById[t.id] = t;
                }
            }

            var parentIds = new HashSet<string>();
            var merged = new List<HonamiTransition>();

            if (parentTransitions != null)
            {
                for (int i = 0; i < parentTransitions.Count; i++)
                {
                    var pt = parentTransitions[i];
                    if (pt == null || string.IsNullOrEmpty(pt.id)) continue;
                    parentIds.Add(pt.id);

                    if (entry.removedParentTransitionIds.Contains(pt.id))
                    {
                        continue;
                    }

                    if (entry.modifiedTransitionIds.Contains(pt.id) && effById.TryGetValue(pt.id, out var localT))
                    {
                        merged.Add(localT);
                    }
                    else
                    {
                        merged.Add(CloneTransition(pt));
                    }
                }
            }

            if (eff.transitions != null)
            {
                for (int i = 0; i < eff.transitions.Count; i++)
                {
                    var et = eff.transitions[i];
                    if (et != null && !string.IsNullOrEmpty(et.id) && !parentIds.Contains(et.id) &&
                        entry.addedTransitionIds.Contains(et.id))
                    {
                        merged.Add(et);
                    }
                }
            }

            var mergedIds = new HashSet<string>();
            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i] != null && !string.IsNullOrEmpty(merged[i].id)) mergedIds.Add(merged[i].id);
            }

            entry.modifiedTransitionIds.RemoveAll(id => !parentIds.Contains(id));
            entry.removedParentTransitionIds.RemoveAll(id => !parentIds.Contains(id));
            entry.addedTransitionIds.RemoveAll(id => !mergedIds.Contains(id));
            eff.transitions = merged;
        }

        public static void RegisterAddedTransition(HonamiOverrideController ov, HonamiOverrideEntry entry, string transitionId)
        {
            if (entry == null || string.IsNullOrEmpty(transitionId)) return;
            if (!entry.addedTransitionIds.Contains(transitionId)) entry.addedTransitionIds.Add(transitionId);
            if (ov != null) EditorUtility.SetDirty(ov);
        }

        private static void ResyncSubNodes(HonamiOverrideController ov, HonamiOverrideEntry entry, HonamiState parent)
        {
            var eff = entry.effectiveState;
            var parentSubs = parent.subNodes;

            if (parentSubs != null)
            {
                foreach (var ps in parentSubs) ps?.EnsureOverrideId();
            }

            var effById = new Dictionary<string, HonamiSubNodeBase>();
            if (eff.subNodes != null)
            {
                foreach (var es in eff.subNodes)
                {
                    if (es != null && !string.IsNullOrEmpty(es.OverrideId)) effById[es.OverrideId] = es;
                }
            }

            var parentIds = new HashSet<string>();
            var merged = new List<HonamiSubNodeBase>();

            if (parentSubs != null)
            {
                foreach (var ps in parentSubs)
                {
                    if (ps == null) continue;
                    string id = ps.OverrideId;
                    parentIds.Add(id);

                    if (entry.removedParentSubNodeIds.Contains(id))
                    {
                        continue;
                    }

                    if (effById.TryGetValue(id, out var es) && es.GetType() == ps.GetType())
                    {
                        SyncSubNodeFields(entry, ps, es, id);
                        merged.Add(es);
                    }
                    else
                    {
                        var copy = Object.Instantiate(ps);
                        copy.hideFlags = HideFlags.None;
                        copy.name = ps.name;
                        AssetDatabase.AddObjectToAsset(copy, ov);
                        merged.Add(copy);
                    }
                }
            }

            if (eff.subNodes != null)
            {
                foreach (var es in eff.subNodes)
                {
                    if (es != null && !string.IsNullOrEmpty(es.OverrideId) && !parentIds.Contains(es.OverrideId) &&
                        entry.addedSubNodeIds.Contains(es.OverrideId))
                    {
                        merged.Add(es);
                    }
                }
            }

            var mergedSet = new HashSet<HonamiSubNodeBase>(merged);
            if (eff.subNodes != null)
            {
                foreach (var es in eff.subNodes)
                {
                    if (es != null && !mergedSet.Contains(es) && IsSubAssetOf(es, ov)) Object.DestroyImmediate(es, true);
                }
            }

            var mergedIds = new HashSet<string>();
            foreach (var m in merged)
            {
                if (m != null && !string.IsNullOrEmpty(m.OverrideId)) mergedIds.Add(m.OverrideId);
            }

            entry.removedParentSubNodeIds.RemoveAll(id => !parentIds.Contains(id));
            entry.addedSubNodeIds.RemoveAll(id => !mergedIds.Contains(id));
            entry.subNodeFieldOverrides.RemoveAll(r => r == null || !mergedIds.Contains(r.subNodeId));

            eff.subNodes = merged;
        }

        private static void SyncSubNodeFields(HonamiOverrideEntry entry, HonamiSubNodeBase parentSub, HonamiSubNodeBase effSub, string id)
        {
            var record = entry.GetSubNodeFieldOverride(id, false);
            var pSO = new SerializedObject(parentSub);
            var eSO = new SerializedObject(effSub);
            foreach (var field in TopLevelFields(pSO))
            {
                if (IgnoredNodeFields.Contains(field) || field == "overrideId") continue;
                if (record != null && record.modifiedPaths.Contains(field)) continue;
                CopyField(pSO, eSO, field);
            }
            eSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effSub);
        }

        // ── Shared helpers ────────────────────────────────────────────────────

        private static void HandleNodeType(HonamiOverrideController ov, HonamiOverrideEntry entry, HonamiState parent)
        {
            if (entry.nodeTypeOverridden)
            {
                return;
            }

            var eff = entry.effectiveState;
            bool needsRecreate = parent.node != null &&
                (eff.node == null || eff.node.GetType() != parent.node.GetType());

            if (!needsRecreate)
            {
                return;
            }

            if (eff.node != null && IsSubAssetOf(eff.node, ov))
            {
                Object.DestroyImmediate(eff.node, true);
            }

            var nodeCopy = Object.Instantiate(parent.node);
            nodeCopy.hideFlags = HideFlags.None;
            nodeCopy.name = parent.node.name;
            AssetDatabase.AddObjectToAsset(nodeCopy, ov);
            eff.node = nodeCopy;
            entry.modifiedNodePaths.Clear();
        }

        private static void Finish(HonamiOverrideController ov, HonamiOverrideEntry entry)
        {
            ov.ClearCaches();
            if (entry != null && entry.effectiveState != null)
            {
                EditorUtility.SetDirty(entry.effectiveState);
            }
            EditorUtility.SetDirty(ov);
            HonamiAnimationSystem.Editor.HonamiGraphView.DeferredSave();
        }

        private static List<HonamiSubNodeBase> DeepCopySubNodes(List<HonamiSubNodeBase> source, HideFlags flags)
        {
            if (source == null)
            {
                return new List<HonamiSubNodeBase>();
            }

            var result = new List<HonamiSubNodeBase>(source.Count);
            foreach (var subNode in source)
            {
                if (subNode == null)
                {
                    result.Add(null);
                    continue;
                }

                subNode.EnsureOverrideId();
                var copy = Object.Instantiate(subNode);
                copy.hideFlags = flags;
                copy.name = subNode.name;
                result.Add(copy);
            }

            return result;
        }

        private static HonamiTransition CloneTransition(HonamiTransition transition)
        {
            return transition == null ? null : JsonUtility.FromJson<HonamiTransition>(JsonUtility.ToJson(transition));
        }

        private static List<HonamiTransition> CloneTransitions(IReadOnlyList<HonamiTransition> source)
        {
            var result = new List<HonamiTransition>();
            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Count; i++)
            {
                var clone = CloneTransition(source[i]);
                if (clone != null) result.Add(clone);
            }

            return result;
        }

        private static bool TransitionEquals(HonamiTransition a, HonamiTransition b)
        {
            if (a == null || b == null)
            {
                return (a == null) == (b == null);
            }

            return JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
        }

        private static void DestroySubAsset(Object obj, bool withUndo)
        {
            if (obj == null) return;
            if (withUndo) Undo.DestroyObjectImmediate(obj);
            else Object.DestroyImmediate(obj, true);
        }

        private static bool IsSubAssetOf(Object obj, HonamiOverrideController ov)
        {
            return obj != null && AssetDatabase.GetAssetPath(obj) == AssetDatabase.GetAssetPath(ov);
        }

        private static IEnumerable<string> TopLevelFields(SerializedObject so)
        {
            var it = so.GetIterator();
            if (it.NextVisible(true))
            {
                do
                {
                    yield return it.name;
                }
                while (it.NextVisible(false));
            }
        }

        private static bool FieldEquals(SerializedObject a, SerializedObject b, string field)
        {
            var pa = a.FindProperty(field);
            var pb = b.FindProperty(field);
            if (pa == null || pb == null)
            {
                return (pa == null) == (pb == null);
            }

            return SerializedProperty.DataEquals(pa, pb);
        }

        private static void CopyField(SerializedObject src, SerializedObject dst, string field)
        {
            var prop = src.FindProperty(field);
            if (prop != null)
            {
                dst.CopyFromSerializedProperty(prop);
            }
        }
    }
}
