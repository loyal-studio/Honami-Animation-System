using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Riggings
{
    [CustomEditor(typeof(HonamiLookAtConstraint))]
    [CanEditMultipleObjects]
    public sealed class HonamiLookAtConstraintEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetSource;
        private SerializedProperty _targetMode;
        private SerializedProperty _lookAtType;
        private SerializedProperty _lookAtMode;
        private SerializedProperty _upMode;
        private SerializedProperty _target;
        private SerializedProperty _worldUpObject;
        private SerializedProperty _worldUpAxis;
        private SerializedProperty _boneUpLocalAxis;
        private SerializedProperty _targets;
        private SerializedProperty _honamiAnimator;
        private SerializedProperty _avatar;
        private SerializedProperty _avatarTargets;
        private SerializedProperty _lerpSpeed;
        private SerializedProperty _limitAngle;
        private SerializedProperty _maxAngle;
        private SerializedProperty _weight;
        private SerializedProperty _eyeTransform;
        private SerializedProperty _eyeCenterOffset;

        private void OnEnable()
        {
            _targetSource = serializedObject.FindProperty("targetSource");
            _targetMode = serializedObject.FindProperty("targetMode");
            _lookAtType = serializedObject.FindProperty("lookAtType");
            _lookAtMode = serializedObject.FindProperty("lookAtMode");
            _upMode = serializedObject.FindProperty("upMode");
            _target = serializedObject.FindProperty("target");
            _worldUpObject = serializedObject.FindProperty("worldUpObject");
            _worldUpAxis = serializedObject.FindProperty("worldUpAxis");
            _boneUpLocalAxis = serializedObject.FindProperty("boneUpLocalAxis");
            _targets = serializedObject.FindProperty("targets");
            _honamiAnimator = serializedObject.FindProperty("honamiAnimator");
            _avatar = serializedObject.FindProperty("avatar");
            _avatarTargets = serializedObject.FindProperty("avatarTargets");
            _lerpSpeed = serializedObject.FindProperty("lerpSpeed");
            _limitAngle = serializedObject.FindProperty("limitAngle");
            _maxAngle = serializedObject.FindProperty("maxAngle");
            _weight = serializedObject.FindProperty("weight");
            _eyeTransform = serializedObject.FindProperty("eyeTransform");
            _eyeCenterOffset = serializedObject.FindProperty("eyeCenterOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader("SETTINGS");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_weight);
                EditorGUILayout.PropertyField(_targetSource);
                if (_targetSource.enumValueIndex == (int)HonamiLookAtTargetSource.Manual)
                    EditorGUILayout.PropertyField(_targetMode);
                EditorGUILayout.PropertyField(_lookAtType);
                EditorGUILayout.PropertyField(_lookAtMode);
                if (_lookAtMode.enumValueIndex != (int)HonamiLookAtMode.LocalRotation)
                    EditorGUILayout.PropertyField(_upMode);
            }

            EditorGUILayout.Space(5);
            DrawHeader("GLOBAL TARGETS");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_target);
                if (_lookAtMode.enumValueIndex != (int)HonamiLookAtMode.LocalRotation &&
                    _upMode.enumValueIndex == (int)HonamiUpMode.Object)
                    EditorGUILayout.PropertyField(_worldUpObject);
                if (_lookAtMode.enumValueIndex != (int)HonamiLookAtMode.LocalRotation &&
                    _upMode.enumValueIndex == (int)HonamiUpMode.World)
                    EditorGUILayout.PropertyField(_worldUpAxis);
                if (_lookAtMode.enumValueIndex != (int)HonamiLookAtMode.LocalRotation &&
                    _upMode.enumValueIndex == (int)HonamiUpMode.BoneAxis)
                    EditorGUILayout.PropertyField(_boneUpLocalAxis, new GUIContent("Bone Local Up Axis"));
            }

            EditorGUILayout.Space(5);
            if (_targetSource.enumValueIndex == (int)HonamiLookAtTargetSource.Avatar)
            {
                DrawAvatarTargets();
            }
            else
            {
                DrawManualTargets();
            }

            EditorGUILayout.Space(5);
            DrawHeader("CONSTRAINTS & LIMITS");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_lerpSpeed);
                EditorGUILayout.PropertyField(_eyeTransform, new GUIContent("Eye Transform", "Optional transform marking the exact center of the eyes. If assigned, ignores Eye Center Offset."));
                EditorGUILayout.PropertyField(_eyeCenterOffset, new GUIContent("Eye Center Offset", "Local-space offset from bone to eye center. Shifts the look origin so the gaze appears to come from the eyes."));
                EditorGUILayout.PropertyField(_limitAngle);
                if (_limitAngle.boolValue)
                    EditorGUILayout.PropertyField(_maxAngle);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawManualTargets()
        {
            DrawHeader("CONSTRAINED OBJECTS");

            if (_targetMode.enumValueIndex == (int)HonamiTargetMode.Single)
            {
                if (_targets.arraySize == 0) _targets.arraySize = 1;
                DrawTargetSettings(_targets.GetArrayElementAtIndex(0), "Object", true);
                return;
            }

            int newSize = EditorGUILayout.IntField("Count", _targets.arraySize);
            if (newSize != _targets.arraySize) _targets.arraySize = newSize;

            for (int i = 0; i < _targets.arraySize; i++)
            {
                SerializedProperty element = _targets.GetArrayElementAtIndex(i);
                SerializedProperty transformProp = element.FindPropertyRelative("transform");
                string label = transformProp.objectReferenceValue != null ? transformProp.objectReferenceValue.name : $"Object {i}";

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, label, true);
                    if (element.isExpanded)
                    {
                        DrawTargetSettings(element, null, false);
                        if (GUILayout.Button("Remove", EditorStyles.miniButton))
                        {
                            _targets.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }
                }
            }

            if (GUILayout.Button("Add Object", GUILayout.Height(25)))
            {
                _targets.InsertArrayElementAtIndex(_targets.arraySize);
                SerializedProperty newElement = _targets.GetArrayElementAtIndex(_targets.arraySize - 1);
                newElement.FindPropertyRelative("aimAxis").vector3Value = Vector3.forward;
                newElement.FindPropertyRelative("upAxis").vector3Value = Vector3.up;
                newElement.FindPropertyRelative("weightX").floatValue = 1f;
                newElement.FindPropertyRelative("weightY").floatValue = 1f;
                newElement.FindPropertyRelative("weightZ").floatValue = 1f;
            }
        }

        private void DrawAvatarTargets()
        {
            DrawHeader("AVATAR OBJECTS");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_honamiAnimator);
                EditorGUILayout.PropertyField(_avatar);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Populate From Avatar", GUILayout.Height(24)))
                    {
                        PopulateFromAvatar();
                    }

                    if (GUILayout.Button("Validate Animator", GUILayout.Height(24)))
                    {
                        ValidateAnimator();
                    }

                    if (GUILayout.Button("Clear", GUILayout.Width(70), GUILayout.Height(24)))
                    {
                        _avatarTargets.ClearArray();
                    }
                }
            }

            EditorGUILayout.Space(5);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int newSize = EditorGUILayout.IntField("Count", _avatarTargets.arraySize);
                if (newSize != _avatarTargets.arraySize) _avatarTargets.arraySize = newSize;

                for (int i = 0; i < _avatarTargets.arraySize; i++)
                {
                    SerializedProperty element = _avatarTargets.GetArrayElementAtIndex(i);
                    SerializedProperty boneName = element.FindPropertyRelative("boneName");
                    SerializedProperty bonePath = element.FindPropertyRelative("bonePath");
                    string label = !string.IsNullOrEmpty(boneName.stringValue) ? boneName.stringValue : bonePath.stringValue;
                    if (string.IsNullOrEmpty(label)) label = $"Avatar Bone {i}";

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, label, true);
                            if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(70)))
                            {
                                _avatarTargets.DeleteArrayElementAtIndex(i);
                                break;
                            }
                        }

                        if (element.isExpanded)
                        {
                            EditorGUILayout.PropertyField(element.FindPropertyRelative("enabled"));
                            EditorGUILayout.PropertyField(boneName);
                            EditorGUILayout.PropertyField(bonePath);
                            DrawAvatarTargetSettings(element);
                        }
                    }
                }

                if (GUILayout.Button("Add Bone", GUILayout.Height(25)))
                {
                    _avatarTargets.InsertArrayElementAtIndex(_avatarTargets.arraySize);
                    SerializedProperty newElement = _avatarTargets.GetArrayElementAtIndex(_avatarTargets.arraySize - 1);
                    SetDefaultAvatarTarget(newElement, string.Empty, string.Empty);
                }
            }
        }

        private void DrawAvatarTargetSettings(SerializedProperty prop)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("aimAxis"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("upAxis"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("rotationOffset"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("positionOffset"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Axis Weights", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("weightX"), new GUIContent("X Weight"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("weightY"), new GUIContent("Y Weight"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("weightZ"), new GUIContent("Z Weight"));
            }
        }

        private void PopulateFromAvatar()
        {
            HonamiAvatar sourceAvatar = _avatar.objectReferenceValue as HonamiAvatar;
            if (sourceAvatar == null && _honamiAnimator.objectReferenceValue is HonamiAnimator animator)
                sourceAvatar = animator.Avatar;

            if (sourceAvatar == null)
            {
                Debug.LogWarning("HonamiLookAtConstraint: assign a Honami Avatar or a Honami Animator with Avatar first.");
                return;
            }

            _avatarTargets.ClearArray();
            for (int i = 0; i < sourceAvatar.bones.Count; i++)
            {
                var bone = sourceAvatar.bones[i];
                if (!bone.enabled) continue;

                _avatarTargets.InsertArrayElementAtIndex(_avatarTargets.arraySize);
                SerializedProperty element = _avatarTargets.GetArrayElementAtIndex(_avatarTargets.arraySize - 1);
                SetDefaultAvatarTarget(element, bone.boneName, bone.bonePath);
            }
        }

        private void ValidateAnimator()
        {
            HonamiAnimator animator = _honamiAnimator.objectReferenceValue as HonamiAnimator;
            if (animator == null)
            {
                Debug.LogWarning("HonamiLookAtConstraint: assign Honami Animator to validate avatar bone paths.");
                return;
            }

            Transform root = animator.transform;
            animator.TryGetComponent(out HonamiBoneReplacer replacer);

            int resolved = 0;
            int missing = 0;

            for (int i = 0; i < _avatarTargets.arraySize; i++)
            {
                SerializedProperty element = _avatarTargets.GetArrayElementAtIndex(i);
                if (!element.FindPropertyRelative("enabled").boolValue) continue;

                string boneName = element.FindPropertyRelative("boneName").stringValue;
                string bonePath = element.FindPropertyRelative("bonePath").stringValue;
                Transform bone = null;

                if (replacer != null)
                {
                    bone = replacer.GetReplacement(boneName);
                    if (bone == null && replacer.disableUnreplacedBones)
                    {
                        missing++;
                        Debug.LogWarning($"HonamiLookAtConstraint: '{boneName}' is disabled by HonamiBoneReplacer because it has no replacement.", animator);
                        continue;
                    }
                }

                if (bone == null)
                    bone = string.IsNullOrEmpty(bonePath) ? root : root.Find(bonePath);

                if (bone != null)
                {
                    resolved++;
                }
                else
                {
                    missing++;
                    Debug.LogWarning($"HonamiLookAtConstraint: missing avatar bone '{boneName}' at path '{bonePath}'.", animator);
                }
            }

            Debug.Log($"HonamiLookAtConstraint: avatar validation finished. Resolved: {resolved}, Missing: {missing}.", animator);
        }

        private static void SetDefaultAvatarTarget(SerializedProperty prop, string boneName, string bonePath)
        {
            prop.FindPropertyRelative("enabled").boolValue = true;
            prop.FindPropertyRelative("boneName").stringValue = boneName;
            prop.FindPropertyRelative("bonePath").stringValue = bonePath;
            prop.FindPropertyRelative("aimAxis").vector3Value = Vector3.forward;
            prop.FindPropertyRelative("upAxis").vector3Value = Vector3.up;
            prop.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
            prop.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
            prop.FindPropertyRelative("weightX").floatValue = 1f;
            prop.FindPropertyRelative("weightY").floatValue = 1f;
            prop.FindPropertyRelative("weightZ").floatValue = 1f;
        }

        private void DrawTargetSettings(SerializedProperty prop, string label, bool showBox)
        {
            if (showBox) EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(prop.FindPropertyRelative("transform"), label != null ? new GUIContent(label) : GUIContent.none);

            EditorGUILayout.Space(2);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("aimAxis"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("upAxis"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("rotationOffset"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("positionOffset"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Axis Weights", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("weightX"), new GUIContent("X Weight"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("weightY"), new GUIContent("Y Weight"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("weightZ"), new GUIContent("Z Weight"));
            }

            if (showBox) EditorGUILayout.EndVertical();
        }

        private static GUIStyle _headerStyle;

        private void DrawHeader(string label)
        {
            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : Color.black;

            Rect rect = GUILayoutUtility.GetRect(18, 18, _headerStyle);
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            EditorGUI.LabelField(new Rect(rect.x + 4, rect.y, rect.width, rect.height), label, _headerStyle);
        }
    }
}
