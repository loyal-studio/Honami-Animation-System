using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Core
{
    public static partial class HonamiOverrideAuthoring
    {
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

                if (!FieldEquals(pSO, eSO, field))
                {
                    if (!record.modifiedPaths.Contains(field)) record.modifiedPaths.Add(field);
                }
                else
                {
                    record.modifiedPaths.Remove(field);
                }
            }

            if (record.modifiedPaths.Count == 0) entry.subNodeFieldOverrides.Remove(record);

            EditorUtility.SetDirty(ov);
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
    }
}
