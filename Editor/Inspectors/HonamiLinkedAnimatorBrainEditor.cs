using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiLinkedAnimator))]
    public sealed class HonamiLinkedAnimatorBrainEditor : UnityEditor.Editor
    {
        private static GUIStyle _headerStyle;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var brain = (HonamiLinkedAnimator)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.9f, 0.6f, 0.2f) } };
            GUILayout.Label(" Brain Testing & Debug", _headerStyle);
            EditorGUILayout.Space(3);

            if (GUILayout.Button("Refresh Linked Animators", GUILayout.Height(25)))
            {
                brain.RefreshLinkedAnimators();
                Debug.Log($"[Honami Linked Animator] Refreshed. Found {brain.LinkedAnimators.Count} linked animators.");
            }

            if (brain.graph != null && brain.graph.events.Count > 0)
            {
                EditorGUILayout.Space(5);
                GUILayout.Label(" Graph Events", EditorStyles.boldLabel);

                foreach (var evt in brain.graph.events)
                {
                    if (GUILayout.Button($"Trigger: {evt.eventName}", GUILayout.Height(25)))
                    {
                        if (Application.isPlaying)
                        {
                            brain.TriggerEvent(evt.eventName);
                        }
                        else
                        {
                            Debug.LogWarning("[Honami Linked Animator] Please enter Play Mode to test Brain Events. Animator component requires runtime execution to change states properly.");
                        }
                    }
                }

                if (Application.isPlaying)
                {
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Stop All Active Events", GUILayout.Height(25)))
                    {
                        brain.StopBrainEvents();
                    }
                }
            }
            else if (brain.graph == null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Assign a Blueprint Graph to see and test available events.", MessageType.Info);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                GUILayout.Label(" Runtime Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Linked Animators", brain.LinkedAnimators.Count.ToString());
                EditorGUILayout.LabelField("Active Nodes", brain.ActiveNodes.Count.ToString());
            }

            EditorGUILayout.EndVertical();
        }
    }
}
