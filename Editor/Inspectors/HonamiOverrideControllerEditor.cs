using UnityEngine;
using UnityEditor;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Core;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiOverrideController))]
    public sealed class HonamiOverrideControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _parentControllerProp;
        private SerializedProperty _additionalLayersProp;
        private SerializedProperty _additionalParametersProp;

        private void OnEnable()
        {
            _parentControllerProp = serializedObject.FindProperty("parentController");
            _additionalLayersProp = serializedObject.FindProperty("additionalLayers");
            _additionalParametersProp = serializedObject.FindProperty("additionalParameters");

            if (target is HonamiOverrideController overrideController && overrideController.parentController != null)
            {
                HonamiOverrideAuthoring.ResyncFromParent(overrideController);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var overrideController = target as HonamiOverrideController;

            if (GUILayout.Button("Open Animator Graph", GUILayout.Height(30)))
            {
                HonamiGraphWindow.OpenWindow();
                HonamiGraphWindow.LoadController(overrideController);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_parentControllerProp);

            if (overrideController.parentController == null)
            {
                EditorGUILayout.HelpBox("Please assign a parent Honami Controller to override it.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var parent = overrideController.parentController as HonamiController;
            if (parent == null)
            {
                EditorGUILayout.HelpBox("Parent must be an actual HonamiController.", MessageType.Warning);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_additionalLayersProp, true);
            EditorGUILayout.PropertyField(_additionalParametersProp, true);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Field Overrides", EditorStyles.boldLabel);
                if (GUILayout.Button("Resync from Parent", GUILayout.Width(140)))
                {
                    HonamiOverrideAuthoring.ResyncFromParent(overrideController);
                }
            }

            EditorGUILayout.HelpBox(
                "Edit inherited states in the Animator Graph. Each field you change is overridden individually (prefab-style); untouched fields keep inheriting from the parent.",
                MessageType.None);

            DrawOverrideList(overrideController);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawOverrideList(HonamiOverrideController overrideController)
        {
            if (overrideController.overrides == null || overrideController.overrides.Count == 0)
            {
                EditorGUILayout.LabelField("No states overridden.", EditorStyles.miniLabel);
                return;
            }

            for (int i = overrideController.overrides.Count - 1; i >= 0; i--)
            {
                var entry = overrideController.overrides[i];
                if (entry == null) continue;

                var parentState = HonamiOverrideAuthoring.GetParentState(overrideController, entry.parentStateGuid);
                string stateName = parentState != null ? parentState.stateName : "(missing state)";
                int count = entry.modifiedStatePaths.Count + entry.modifiedNodePaths.Count + (entry.nodeTypeOverridden ? 1 : 0)
                    + entry.modifiedTransitionIds.Count + entry.removedParentTransitionIds.Count + entry.addedTransitionIds.Count
                    + entry.removedParentSubNodeIds.Count + entry.addedSubNodeIds.Count + entry.subNodeFieldOverrides.Count;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{stateName}", EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUILayout.LabelField($"{count} field(s)", GUILayout.Width(80));

                    if (GUILayout.Button("Revert", GUILayout.Width(70)))
                    {
                        HonamiOverrideAuthoring.RevertAll(overrideController, entry);
                        serializedObject.Update();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }
    }
}
