using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiDirector))]
    public sealed class HonamiDirectorEditor : UnityEditor.Editor
    {
        private float _previewTime = 0f;
        private static GUIStyle _headerStyle;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var director = (HonamiDirector)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.18f, 0.76f, 0.9f) } };
            GUILayout.Label(" Director Preview", _headerStyle);
            EditorGUILayout.Space(3);

            if (director.timeline != null)
            {
                float duration = director.timeline.GetDuration();

                EditorGUI.BeginChangeCheck();
                _previewTime = EditorGUILayout.Slider("Preview Time", _previewTime, 0f, duration);
                if (EditorGUI.EndChangeCheck())
                {
                    director.Seek(_previewTime);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("|<", GUILayout.Width(40), GUILayout.Height(25)))
                {
                    _previewTime = 0f;
                    director.Seek(_previewTime);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button(Application.isPlaying ? "Play Runtime" : "Play (PlayMode Only)", GUILayout.Height(25)))
                {
                    if (Application.isPlaying)
                    {
                        director.Play();
                    }
                    else
                    {
                        Debug.LogWarning("[Honami Director] Realtime playback is only supported in Play Mode. Use the slider to preview in Edit Mode.");
                    }
                }

                if (GUILayout.Button(">|", GUILayout.Width(40), GUILayout.Height(25)))
                {
                    _previewTime = duration;
                    director.Seek(_previewTime);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.EndHorizontal();

                if (Application.isPlaying)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Pause", GUILayout.Height(25))) director.Pause();
                    if (GUILayout.Button("Resume", GUILayout.Height(25))) director.Resume();
                    if (GUILayout.Button("Stop", GUILayout.Height(25))) director.Stop();
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a Honami Timeline to preview it.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
