using System.Collections.Generic;
using HonamiAnimationSystem.Editor.Timeline;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiDirector))]
    public sealed class HonamiDirectorEditor : UnityEditor.Editor
    {
        private SerializedProperty _timeline;
        private SerializedProperty _playTrigger;
        private SerializedProperty _pauseAtFirstFrameOnAutoPlay;
        private SerializedProperty _wrapMode;
        private SerializedProperty _updateMode;
        private SerializedProperty _speed;
        private SerializedProperty _tags;

        private void OnEnable()
        {
            _timeline = serializedObject.FindProperty("timeline");
            _playTrigger = serializedObject.FindProperty("playTrigger");
            _pauseAtFirstFrameOnAutoPlay = serializedObject.FindProperty("pauseAtFirstFrameOnAutoPlay");
            _wrapMode = serializedObject.FindProperty("wrapMode");
            _updateMode = serializedObject.FindProperty("updateMode");
            _speed = serializedObject.FindProperty("speed");
            _tags = serializedObject.FindProperty("tags");
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_timeline);
            EditorGUILayout.PropertyField(_playTrigger);
            using (new EditorGUI.DisabledScope(_playTrigger.enumValueIndex == (int)HonamiDirectorPlayTrigger.Manual))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_pauseAtFirstFrameOnAutoPlay, new GUIContent("Pause At First Frame", "If enabled, automatic playback (Awake, Start, OnEnable) only evaluates the first frame and pauses."));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_wrapMode);
            EditorGUILayout.PropertyField(_updateMode);
            EditorGUILayout.PropertyField(_speed);
            EditorGUILayout.PropertyField(_tags);
            serializedObject.ApplyModifiedProperties();

            var director = (HonamiDirector)target;
            if (director.Timeline == null)
            {
                EditorGUILayout.HelpBox("Assign a Honami Timeline to bind its modules and play it.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Timeline Window", GUILayout.Height(26)))
            {
                Selection.activeGameObject = director.gameObject;
                HonamiTimelineWindow.InspectTimeline(director.Timeline);
            }

            EditorGUILayout.Space(8);
            DrawBindings(director);
            EditorGUILayout.Space(8);
            DrawReferences(director);
            EditorGUILayout.Space(8);
            DrawPlayback(director);
        }

        private static void DrawBindings(HonamiDirector director)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            HonamiInspectorGUI.SubHeader(" Module Bindings", HonamiInspectorGUI.Accent);
            EditorGUILayout.Space(3);

            bool any = false;
            foreach (var module in director.Timeline.Modules)
            {
                if (module?.BindingType == null) continue;
                any = true;

                var current = director.GetBinding(module);
                string label = string.IsNullOrEmpty(module.moduleName) ? module.GetType().Name : module.moduleName;
                EditorGUI.BeginChangeCheck();
                var assigned = EditorGUILayout.ObjectField(label, current, module.BindingType, true);
                if (EditorGUI.EndChangeCheck())
                    HonamiEditorController.SetTimelineBinding(director, module, assigned);
            }

            if (!any)
                EditorGUILayout.LabelField("No module in this timeline needs a scene binding.", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private static void DrawReferences(HonamiDirector director)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            HonamiInspectorGUI.SubHeader(" Scene References", HonamiInspectorGUI.Accent);
            EditorGUILayout.Space(3);

            var used = CollectReferenceKeys(director.Timeline);
            int unused = 0;
            int shown = 0;
            foreach (var entry in director.References)
            {
                if (entry == null) continue;
                bool isUsed = used.Contains(entry.key);
                if (!isUsed)
                {
                    unused++;
                    continue;
                }
                shown++;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(entry.target != null ? entry.target.name : "(missing)", entry.target, typeof(Object), true);
            }

            if (shown == 0)
                EditorGUILayout.LabelField("Camera shot anchors and other scene objects items point at appear here.", EditorStyles.wordWrappedMiniLabel);

            if (unused > 0 && GUILayout.Button($"Remove {unused} Unused Reference{(unused == 1 ? string.Empty : "s")}"))
            {
                Undo.RecordObject(director, "Remove Unused References");
                var keys = new List<string>();
                foreach (var entry in director.References)
                {
                    if (entry != null && !used.Contains(entry.key)) keys.Add(entry.key);
                }
                foreach (var key in keys)
                    director.SetReference(key, null);
                EditorUtility.SetDirty(director);
            }

            EditorGUILayout.EndVertical();
        }

        private static HashSet<string> CollectReferenceKeys(HonamiTimeline timeline)
        {
            var keys = new HashSet<string>();
            if (timeline == null) return keys;

            // Modules are separate ScriptableObject sub-assets, so the timeline's serialized property tree
            // stops at the modules list and never reaches the items that own the scene references.
            foreach (var module in timeline.Modules)
            {
                if (module == null) continue;
                using var serialized = new SerializedObject(module);
                var iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.Generic || iterator.type != nameof(HonamiSceneReference)) continue;
                    var key = iterator.FindPropertyRelative("key");
                    if (key != null && !string.IsNullOrEmpty(key.stringValue)) keys.Add(key.stringValue);
                }
            }
            return keys;
        }

        private static void DrawPlayback(HonamiDirector director)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            HonamiInspectorGUI.SubHeader(" Playback", HonamiInspectorGUI.Accent);
            EditorGUILayout.Space(3);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Preview and scrub in the Timeline window. These controls drive the director in Play Mode.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            float duration = director.Duration;
            EditorGUI.BeginChangeCheck();
            float time = EditorGUILayout.Slider("Time", director.CurrentTime, 0f, duration);
            if (EditorGUI.EndChangeCheck())
                director.Seek(time);

            string status = director.IsPlaying ? "Playing" : director.IsPaused ? "Paused" : "Stopped";
            EditorGUILayout.LabelField("State", $"{status}  ·  {TimelineTimecode.FormatTimecode(director.CurrentTime, director.Timeline.FrameRate)}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play", GUILayout.Height(24))) director.Play();
            if (GUILayout.Button(director.IsPlaying ? "Pause" : "Resume", GUILayout.Height(24)))
            {
                if (director.IsPlaying) director.Pause();
                else director.Resume();
            }
            if (GUILayout.Button("Skip", GUILayout.Height(24))) director.Skip();
            if (GUILayout.Button("Stop", GUILayout.Height(24))) director.Stop();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
