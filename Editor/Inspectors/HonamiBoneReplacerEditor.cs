using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiBoneReplacer))]
    public sealed class HonamiBoneReplacerEditor : UnityEditor.Editor
    {
        private SerializedProperty _disableUnreplacedBones;
        private SerializedProperty _replacements;
        private HonamiAnimator _animator;

        private void OnEnable()
        {
            _disableUnreplacedBones = serializedObject.FindProperty("disableUnreplacedBones");
            _replacements = serializedObject.FindProperty("replacements");

            var component = (HonamiBoneReplacer)target;
            component.TryGetComponent(out _animator);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_disableUnreplacedBones);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_replacements, new GUIContent("Replacements"), true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            if (_animator != null && _animator.Avatar != null)
            {
                if (GUILayout.Button("Populate from Avatar", GUILayout.Height(30)))
                {
                    PopulateFromAvatar();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Add this component to an object with a Honami Animator and an Avatar to enable population.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void PopulateFromAvatar()
        {
            if (_animator == null || _animator.Avatar == null) return;

            var component = (HonamiBoneReplacer)target;
            Undo.RecordObject(component, "Populate Bone Replacements");

            foreach (var bone in _animator.Avatar.bones)
            {
                if (!bone.enabled) continue;

                bool exists = false;
                foreach (var r in component.replacements)
                {
                    if (r.boneName == bone.boneName)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    Transform t = string.IsNullOrEmpty(bone.bonePath) ? _animator.transform : _animator.transform.Find(bone.bonePath);
                    component.AddReplacement(bone.boneName, t);
                }
            }

            EditorUtility.SetDirty(component);
        }
    }

    [CustomPropertyDrawer(typeof(HonamiBoneReplacer.BoneReplacement))]
    public sealed class BoneReplacementDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var boneNameProp = property.FindPropertyRelative("boneName");
            var targetTransformProp = property.FindPropertyRelative("targetTransform");

            label.text = string.IsNullOrEmpty(boneNameProp.stringValue) ? "New Replacement" : boneNameProp.stringValue;

            EditorGUI.BeginProperty(position, label, property);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                Rect r1 = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
                Rect r2 = new Rect(position.x, r1.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);

                EditorGUI.PropertyField(r1, boneNameProp);
                EditorGUI.PropertyField(r2, targetTransformProp);

                EditorGUI.indentLevel--;
            }

            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded)
                return EditorGUIUtility.singleLineHeight * 3 + 6;
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
