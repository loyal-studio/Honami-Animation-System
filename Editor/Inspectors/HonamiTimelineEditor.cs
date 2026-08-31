using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor.Sequence
{
    [CustomEditor(typeof(HonamiTimeline))]
    public sealed class HonamiTimelineEditor : UnityEditor.Editor
    {
        private SerializedProperty _timelineName;
        private SerializedProperty _durationOverride;
        private SerializedProperty _tracks;

        private static readonly GUIContent _durationOverrideContent = new GUIContent("Duration Override", "0 = auto from tracks");
        private static readonly GUIContent _muteContent = new GUIContent("M", "Mute track");
        private static readonly GUIContent _targetContent = new GUIContent("Target GameObject");
        private static readonly GUIContent _clipsContent = new GUIContent("Clips");
        private static readonly GUIContent _eventsContent = new GUIContent("Events");

        private void OnEnable()
        {
            _timelineName = serializedObject.FindProperty("timelineName");
            _durationOverride = serializedObject.FindProperty("durationOverride");
            _tracks = serializedObject.FindProperty("tracks");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var tl = (HonamiTimeline)target;

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Timeline Window", GUILayout.Height(30)))
                HonamiAnimationSystem.Editor.Timeline.HonamiTimelineWindow.InspectTimeline(tl);

            EditorGUILayout.Space(8);
            EditorGUILayout.PropertyField(_timelineName);
            EditorGUILayout.PropertyField(_durationOverride, _durationOverrideContent);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tracks", EditorStyles.boldLabel);

            for (int i = 0; i < _tracks.arraySize; i++)
            {
                var tp = _tracks.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var trackTypeProp = tp.FindPropertyRelative("trackType");
                    var mutedProp = tp.FindPropertyRelative("muted");

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(tp.FindPropertyRelative("trackName"), GUIContent.none, GUILayout.ExpandWidth(true));
                    EditorGUILayout.PropertyField(trackTypeProp, GUIContent.none, GUILayout.Width(100));
                    mutedProp.boolValue = GUILayout.Toggle(mutedProp.boolValue, _muteContent, GUILayout.Width(20));
                    if (GUILayout.Button(HonamiEditorSymbols.Remove, GUILayout.Width(22)))
                    {
                        _tracks.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        return;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (trackTypeProp.enumValueIndex == (int)HonamiTimelineTrackType.Animation)
                    {
                        EditorGUILayout.PropertyField(tp.FindPropertyRelative("target"), _targetContent);
                        EditorGUILayout.PropertyField(tp.FindPropertyRelative("clips"), _clipsContent, true);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(tp.FindPropertyRelative("events"), _eventsContent, true);
                    }
                }
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Animation Track"))
            {
                _tracks.InsertArrayElementAtIndex(_tracks.arraySize);
                var np = _tracks.GetArrayElementAtIndex(_tracks.arraySize - 1);
                np.FindPropertyRelative("trackName").stringValue = "Animation Track";
                np.FindPropertyRelative("trackType").enumValueIndex = (int)HonamiTimelineTrackType.Animation;
            }
            if (GUILayout.Button("+ Event Track"))
            {
                _tracks.InsertArrayElementAtIndex(_tracks.arraySize);
                var np = _tracks.GetArrayElementAtIndex(_tracks.arraySize - 1);
                np.FindPropertyRelative("trackName").stringValue = "Event Track";
                np.FindPropertyRelative("trackType").enumValueIndex = (int)HonamiTimelineTrackType.Event;
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
