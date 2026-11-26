using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Core
{
    public static partial class HonamiOverrideAuthoring
    {
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

        public static void ResyncFromParent(HonamiOverrideController ov)
        {
            if (ov == null || ov.parentController == null)
            {
                return;
            }

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

            var nodeCopy = InstantiateNode(parent.node);
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
    }
}
