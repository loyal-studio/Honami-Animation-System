using System;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private void RegisterRecording()
        {
            Undo.postprocessModifications += OnPostprocessModifications;
        }

        private void UnregisterRecording()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
        }

        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (!_state.IsRecording || _state.Mode != TimelineMode.HonamiClipEdit || _state.ActiveClip == null || _state.RecordingContext == null)
                return modifications;

            if (_state.IsClipReadOnly) return modifications;

            bool added = false;
            foreach (var mod in modifications)
            {
                var targetObj = mod.currentValue.target;
                if (targetObj == null) continue;

                GameObject go = targetObj switch
                {
                    GameObject g => g,
                    Component c => c.gameObject,
                    _ => null
                };

                if (go != null && (go == _state.RecordingContext || go.transform.IsChildOf(_state.RecordingContext.transform)))
                {
                    if (AddKeyframeFromModification(mod, go)) added = true;
                }
            }

            if (added)
            {
                EditorUtility.SetDirty(_state.ActiveClip);
                _curveCache.Clear();
                _rebuild();
            }
            return modifications;
        }

        private bool AddKeyframeFromModification(UndoPropertyModification mod, GameObject go)
        {
            string path = AnimationUtility.CalculateTransformPath(go.transform, _state.RecordingContext.transform);
            Type type = mod.currentValue.target.GetType();
            string prop = mod.currentValue.propertyPath;

            var binding = new EditorCurveBinding { path = path, type = type, propertyName = prop };

            if (!float.TryParse(mod.currentValue.value, out float value))
                return false;

            var curve = AnimationUtility.GetEditorCurve(_state.ActiveClip, binding) ?? new AnimationCurve();
            curve.AddKey(_state.PlayheadTime, value);
            AnimationUtility.SetEditorCurve(_state.ActiveClip, binding, curve);
            return true;
        }
    }
}
