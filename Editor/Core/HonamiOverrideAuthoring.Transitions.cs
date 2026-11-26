using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Core
{
    public static partial class HonamiOverrideAuthoring
    {
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

                if (!TransitionEquals(et, pt))
                {
                    if (!entry.modifiedTransitionIds.Contains(et.id)) entry.modifiedTransitionIds.Add(et.id);
                }
                else
                {
                    entry.modifiedTransitionIds.Remove(et.id);
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

        public static void ApplyTransitionToParent(HonamiOverrideController ov, HonamiOverrideEntry entry, string transitionId)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || entry?.effectiveState?.transitions == null || string.IsNullOrEmpty(transitionId))
            {
                return;
            }

            HonamiTransition effTrans = null;
            foreach (var t in entry.effectiveState.transitions)
            {
                if (t != null && t.id == transitionId) { effTrans = t; break; }
            }
            if (effTrans == null) return;

            Undo.RegisterCompleteObjectUndo(ov, "Apply Transition To Parent");
            Undo.RecordObject(parent, "Apply Transition To Parent");

            parent.transitions ??= new List<HonamiTransition>();
            var clone = CloneTransition(effTrans);
            int pIdx = -1;
            for (int i = 0; i < parent.transitions.Count; i++)
            {
                if (parent.transitions[i] != null && parent.transitions[i].id == transitionId) { pIdx = i; break; }
            }

            if (pIdx >= 0) parent.transitions[pIdx] = clone;
            else parent.transitions.Add(clone);

            EditorUtility.SetDirty(parent);
            entry.modifiedTransitionIds.Remove(transitionId);
            entry.addedTransitionIds.Remove(transitionId);
            ov.parentController?.BaseController?.ClearEffectiveStateCache();
            Finish(ov, entry);
        }

        private static SerializedProperty FindTransitionElement(SerializedObject so, string transitionId)
        {
            var trans = so.FindProperty("transitions");
            if (trans == null) return null;

            for (int i = 0; i < trans.arraySize; i++)
            {
                var el = trans.GetArrayElementAtIndex(i);
                var idProp = el.FindPropertyRelative("id");
                if (idProp != null && idProp.stringValue == transitionId) return el;
            }
            return null;
        }

        public static bool TransitionFieldDiffers(HonamiOverrideController ov, HonamiOverrideEntry entry, string transitionId, string field)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || entry?.effectiveState == null || string.IsNullOrEmpty(transitionId) || string.IsNullOrEmpty(field)) return false;

            var eField = FindTransitionElement(new SerializedObject(entry.effectiveState), transitionId)?.FindPropertyRelative(field);
            var pField = FindTransitionElement(new SerializedObject(parent), transitionId)?.FindPropertyRelative(field);
            if (eField == null) return false;
            if (pField == null) return true;
            return !SerializedProperty.DataEquals(eField, pField);
        }

        public static void RevertTransitionField(HonamiOverrideController ov, HonamiOverrideEntry entry, string transitionId, string field)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || entry?.effectiveState == null || string.IsNullOrEmpty(transitionId) || string.IsNullOrEmpty(field)) return;

            var pSO = new SerializedObject(parent);
            var eSO = new SerializedObject(entry.effectiveState);
            var pField = FindTransitionElement(pSO, transitionId)?.FindPropertyRelative(field);
            var eField = FindTransitionElement(eSO, transitionId)?.FindPropertyRelative(field);
            if (pField == null || eField == null) return;

            Undo.RegisterCompleteObjectUndo(ov, "Revert Transition Field");
            Undo.RecordObject(entry.effectiveState, "Revert Transition Field");
            eField.boxedValue = pField.boxedValue;
            eSO.ApplyModifiedProperties();

            RefreshTransitionModified(ov, entry, parent);
            Finish(ov, entry);
        }

        public static void ApplyTransitionFieldToParent(HonamiOverrideController ov, HonamiOverrideEntry entry, string transitionId, string field)
        {
            var parent = GetParentState(ov, entry?.parentStateGuid);
            if (parent == null || entry?.effectiveState == null || string.IsNullOrEmpty(transitionId) || string.IsNullOrEmpty(field)) return;

            var pSO = new SerializedObject(parent);
            var eSO = new SerializedObject(entry.effectiveState);
            var pField = FindTransitionElement(pSO, transitionId)?.FindPropertyRelative(field);
            var eField = FindTransitionElement(eSO, transitionId)?.FindPropertyRelative(field);
            if (pField == null || eField == null) return;

            Undo.RegisterCompleteObjectUndo(ov, "Apply Transition Field");
            Undo.RecordObject(parent, "Apply Transition Field");
            pField.boxedValue = eField.boxedValue;
            pSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(parent);
            ov.parentController?.BaseController?.ClearEffectiveStateCache();

            RefreshTransitionModified(ov, entry, parent);
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

        public static void RegisterAddedTransition(HonamiOverrideController ov, HonamiOverrideEntry entry, string transitionId)
        {
            if (entry == null || string.IsNullOrEmpty(transitionId)) return;
            if (!entry.addedTransitionIds.Contains(transitionId)) entry.addedTransitionIds.Add(transitionId);
            if (ov != null) EditorUtility.SetDirty(ov);
        }
    }
}
