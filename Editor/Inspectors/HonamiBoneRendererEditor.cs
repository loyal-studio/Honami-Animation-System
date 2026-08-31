using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiBoneRenderer))]
    public sealed class HonamiBoneRendererEditor : UnityEditor.Editor
    {
        private SerializedProperty _shape;
        private SerializedProperty _boneSize;
        private SerializedProperty _drawJoints;
        private SerializedProperty _jointSize;
        private SerializedProperty _xRay;
        private SerializedProperty _boneColor;
        private SerializedProperty _hoverColor;
        private SerializedProperty _selectedColor;
        private SerializedProperty _bones;

        private bool _appearanceFoldout = true;
        private bool _bonesFoldout = true;

        private static readonly Color _accentColor = new Color(0.18f, 0.76f, 0.9f);
        private static GUIStyle _headerStyle;
        private static GUIStyle _subStyle;

        private void OnEnable()
        {
            _shape = serializedObject.FindProperty("shape");
            _boneSize = serializedObject.FindProperty("boneSize");
            _drawJoints = serializedObject.FindProperty("drawJoints");
            _jointSize = serializedObject.FindProperty("jointSize");
            _xRay = serializedObject.FindProperty("xRay");
            _boneColor = serializedObject.FindProperty("boneColor");
            _hoverColor = serializedObject.FindProperty("hoverColor");
            _selectedColor = serializedObject.FindProperty("selectedColor");
            _bones = serializedObject.FindProperty("bones");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTitle();
            EditorGUILayout.Space(4);

            DrawAppearanceSection();
            EditorGUILayout.Space(4);

            DrawBonesSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTitle()
        {
            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _accentColor }
            };
            _subStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.Space(8);
            GUILayout.Label("HONAMI BONE RENDERER", _headerStyle);
            GUILayout.Label("Scene View Skeleton Display & Picking", _subStyle);

            Rect line = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(line, new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.5f));
        }

        private void DrawAppearanceSection()
        {
            _appearanceFoldout = EditorGUILayout.Foldout(_appearanceFoldout, "Appearance", true, EditorStyles.foldoutHeader);
            if (!_appearanceFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_shape);
            EditorGUILayout.PropertyField(_boneSize);
            EditorGUILayout.PropertyField(_drawJoints);
            if (_drawJoints.boolValue) EditorGUILayout.PropertyField(_jointSize);
            EditorGUILayout.PropertyField(_xRay, new GUIContent("X-Ray", "Draw bones through geometry."));
            EditorGUILayout.PropertyField(_boneColor);
            EditorGUILayout.PropertyField(_hoverColor);
            EditorGUILayout.PropertyField(_selectedColor);
            EditorGUILayout.EndVertical();
        }

        private void DrawBonesSection()
        {
            _bonesFoldout = EditorGUILayout.Foldout(_bonesFoldout, $"Bones ({_bones.arraySize})", true, EditorStyles.foldoutHeader);
            if (!_bonesFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_bones.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No bones assigned. Fill the list to see the skeleton in the Scene View.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("From Avatar", GUILayout.Height(22))) FillFromAvatar();
            if (GUILayout.Button("From Skinned Mesh", GUILayout.Height(22))) FillFromSkinnedMesh();
            if (GUILayout.Button("From Children", GUILayout.Height(22))) FillFromChildren();
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(_bones.arraySize == 0);
            if (GUILayout.Button("Clear", GUILayout.Height(20))) ApplyBones(new List<Transform>());
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_bones, new GUIContent("Bone List"), true);
            EditorGUILayout.EndVertical();
        }

        private void FillFromAvatar()
        {
            var renderer = (HonamiBoneRenderer)target;
            var animator = renderer.GetComponentInParent<HonamiAnimator>();

            if (animator == null || animator.Avatar == null)
            {
                EditorUtility.DisplayDialog("Honami Bone Renderer",
                    "No HonamiAnimator with an assigned Honami Avatar found on this object or its parents.", "OK");
                return;
            }

            var root = animator.transform;
            var result = new List<Transform>();
            foreach (var entry in animator.Avatar.bones)
            {
                if (!entry.enabled) continue;
                var bone = string.IsNullOrEmpty(entry.bonePath) ? root : root.Find(entry.bonePath);
                if (bone != null) result.Add(bone);
            }

            ApplyBones(result);
        }

        private void FillFromSkinnedMesh()
        {
            var renderer = (HonamiBoneRenderer)target;
            var result = new List<Transform>();
            var seen = new HashSet<Transform>();

            foreach (var skinnedMesh in renderer.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var bone in skinnedMesh.bones)
                {
                    if (bone != null && seen.Add(bone)) result.Add(bone);
                }
            }

            if (result.Count == 0)
            {
                EditorUtility.DisplayDialog("Honami Bone Renderer",
                    "No SkinnedMeshRenderer bones found under this object.", "OK");
                return;
            }

            ApplyBones(result);
        }

        private void FillFromChildren()
        {
            var renderer = (HonamiBoneRenderer)target;
            var result = new List<Transform>();

            foreach (var child in renderer.GetComponentsInChildren<Transform>(true))
            {
                if (child == renderer.transform) continue;
                if (child.TryGetComponent<SkinnedMeshRenderer>(out _)) continue;
                if (child.TryGetComponent<MeshRenderer>(out _)) continue;
                result.Add(child);
            }

            ApplyBones(result);
        }

        private void ApplyBones(List<Transform> bones)
        {
            var renderer = (HonamiBoneRenderer)target;
            Undo.RecordObject(renderer, "Set Bone Renderer Bones");
            renderer.EditorSetBones(bones);
            EditorUtility.SetDirty(renderer);
            serializedObject.Update();
            SceneView.RepaintAll();
        }
    }
}
