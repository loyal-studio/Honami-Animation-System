using System.Collections.Generic;
using HonamiAnimationSystem.Editor.Timeline;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [CustomEditor(typeof(HonamiTimeline))]
    public sealed class HonamiTimelineEditor : UnityEditor.Editor
    {
        private static readonly HashSet<string> HeaderFields = new() { nameof(HonamiTimelineModule.moduleName), nameof(HonamiTimelineModule.muted) };
        private static readonly GUIContent MuteContent = new("M", "Mute module");

        private SerializedProperty _timelineName;
        private SerializedProperty _durationOverride;
        private SerializedProperty _modules;

        private void OnEnable()
        {
            _timelineName = serializedObject.FindProperty("timelineName");
            _durationOverride = serializedObject.FindProperty("durationOverride");
            _modules = serializedObject.FindProperty("modules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var timeline = (HonamiTimeline)target;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Timeline Window", GUILayout.Height(30)))
                HonamiTimelineWindow.InspectTimeline(timeline);

            EditorGUILayout.Space(8);
            EditorGUILayout.PropertyField(_timelineName);
            EditorGUILayout.PropertyField(_durationOverride);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);

            for (int i = 0; i < _modules.arraySize; i++)
            {
                if (DrawModule(timeline, _modules.GetArrayElementAtIndex(i), i))
                    break;
                EditorGUILayout.Space(2);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ Add Module"))
                ShowAddModuleMenu(timeline);
        }

        private bool DrawModule(HonamiTimeline timeline, SerializedProperty moduleProperty, int index)
        {
            var module = index < timeline.Modules.Count ? timeline.Modules[index] : null;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (module == null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox("This module's type no longer exists (renamed or deleted class).", MessageType.Warning);
                    bool removeMissing = GUILayout.Button(HonamiEditorSymbols.Remove, GUILayout.Width(22));
                    EditorGUILayout.EndHorizontal();
                    if (!removeMissing) return false;

                    _modules.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    return true;
                }

                var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
                var moduleSerializedObject = new SerializedObject(module);
                moduleSerializedObject.Update();

                var muted = moduleSerializedObject.FindProperty(nameof(HonamiTimelineModule.muted));
                var moduleName = moduleSerializedObject.FindProperty(nameof(HonamiTimelineModule.moduleName));

                EditorGUILayout.BeginHorizontal();
                if (moduleName != null)
                    EditorGUILayout.PropertyField(moduleName, GUIContent.none, GUILayout.ExpandWidth(true));
                GUILayout.Label(editor.DisplayName, EditorStyles.miniLabel, GUILayout.Width(80));
                if (muted != null)
                    muted.boolValue = GUILayout.Toggle(muted.boolValue, MuteContent, GUILayout.Width(20));
                bool remove = GUILayout.Button(HonamiEditorSymbols.Remove, GUILayout.Width(22));
                EditorGUILayout.EndHorizontal();

                if (remove)
                {
                    moduleSerializedObject.ApplyModifiedProperties();
                    serializedObject.ApplyModifiedProperties();
                    HonamiEditorController.DeleteModule(timeline, module);
                    serializedObject.Update();
                    return true;
                }

                // Root iterator: NextVisible(false) is the correct termination condition.
                // GetEndProperty() would throw "Invalid iteration" until the iterator has advanced
                // past the initial state, so it is not used here.
                var iterator = moduleSerializedObject.GetIterator();
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (!HeaderFields.Contains(iterator.name) && iterator.name != "m_Script")
                            EditorGUILayout.PropertyField(iterator, true);
                    }
                    while (iterator.NextVisible(false));
                }
                moduleSerializedObject.ApplyModifiedProperties();
            }
            return false;
        }

        private static void ShowAddModuleMenu(HonamiTimeline timeline)
        {
            var menu = new GenericMenu();
            foreach (var moduleType in HonamiTimelineModuleEditorRegistry.ModuleTypesFor(HonamiTimelineModuleHost.Timeline))
            {
                var editor = HonamiTimelineModuleEditorRegistry.GetEditor(moduleType);
                menu.AddItem(new GUIContent(editor.MenuPath), false, () => HonamiEditorController.AddModule(timeline, moduleType));
            }
            menu.ShowAsContext();
        }
    }
}
