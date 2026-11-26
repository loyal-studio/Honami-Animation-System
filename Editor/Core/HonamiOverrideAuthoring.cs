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
    public static partial class HonamiOverrideAuthoring
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
            copy.hideFlags = HideFlags.DontSave;
            copy.name = parentState.name;

            if (parentState.node != null)
            {
                var nodeCopy = InstantiateNode(parentState.node);
                nodeCopy.hideFlags = HideFlags.DontSave;
                nodeCopy.name = parentState.node.name;
                copy.node = nodeCopy;
            }

            copy.subNodes = DeepCopySubNodes(parentState.subNodes, HideFlags.DontSave);
            return copy;
        }

        // Modules bound to a clip store the clip's id, so the parent must own the ids before the override copies them.
        private static HonamiNodeBase InstantiateNode(HonamiNodeBase parentNode)
        {
            if (parentNode.EnsureMotionIds())
                EditorUtility.SetDirty(parentNode);
            return Object.Instantiate(parentNode);
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

                if (!FieldEquals(pSO, eSO, field))
                {
                    if (!entry.modifiedStatePaths.Contains(field)) entry.modifiedStatePaths.Add(field);
                }
                else
                {
                    entry.modifiedStatePaths.Remove(field);
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

                if (!FieldEquals(pnSO, enSO, field))
                {
                    if (!entry.modifiedNodePaths.Contains(field)) entry.modifiedNodePaths.Add(field);
                }
                else
                {
                    entry.modifiedNodePaths.Remove(field);
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

        // ── Sub-nodes (deep copied, per element, matched by OverrideId) ───────

        // ── Revert-all / removal ──────────────────────────────────────────────

        // ── Resync ────────────────────────────────────────────────────────────

        // ── Shared helpers ────────────────────────────────────────────────────

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
