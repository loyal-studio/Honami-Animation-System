using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Riggings
{
    [CustomEditor(typeof(HonamiPoseConstraint))]
    [CanEditMultipleObjects]
    public sealed class HonamiPoseConstraintEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetSource;
        private SerializedProperty _poseType;
        private SerializedProperty _poseSpace;
        private SerializedProperty _targets;
        private SerializedProperty _honamiAnimator;
        private SerializedProperty _avatar;
        private SerializedProperty _avatarTargets;
        private SerializedProperty _weight;

        private HashSet<int> _selectedTargetIndices = new HashSet<int>();
        private int _lastClickedIndex = -1;
        private bool _isAvatarMode;
        private bool _isDragging;
        private int _primaryDragIndex = -1;

        private List<int> _multiDragIndices = new List<int>();
        private List<Vector3> _multiDragStartPosOffsets = new List<Vector3>();
        private List<Vector3> _multiDragStartRotOffsets = new List<Vector3>();

        private List<Vector3> _multiDragStartBasePos = new List<Vector3>();
        private List<Quaternion> _multiDragStartBaseRot = new List<Quaternion>();
        private List<Vector3> _multiDragStartBaseLocalPos = new List<Vector3>();
        private List<Quaternion> _multiDragStartBaseLocalRot = new List<Quaternion>();

        private List<Vector3> _multiDragStartTargetPos = new List<Vector3>();
        private List<Quaternion> _multiDragStartTargetRot = new List<Quaternion>();
        private List<Vector3> _multiDragStartTargetLocalPos = new List<Vector3>();
        private List<Quaternion> _multiDragStartTargetLocalRot = new List<Quaternion>();

        private List<bool> _multiDragStartHasTarget = new List<bool>();
        private List<bool> _multiDragStartHasParent = new List<bool>();
        private List<Matrix4x4> _multiDragStartParentMatrix = new List<Matrix4x4>();
        private List<Quaternion> _multiDragStartParentRot = new List<Quaternion>();
        private List<Vector3> _multiDragStartParentPos = new List<Vector3>();

        private List<Vector3> _multiDragStartPoseWorldPos = new List<Vector3>();
        private List<Quaternion> _multiDragStartPoseWorldRot = new List<Quaternion>();
        private List<Transform> _multiDragStartBones = new List<Transform>();
        private readonly HashSet<Transform> _multiDragStartBoneSet = new HashSet<Transform>();

        private void OnEnable()
        {
            _targetSource = serializedObject.FindProperty("targetSource");
            _poseType = serializedObject.FindProperty("poseType");
            _poseSpace = serializedObject.FindProperty("poseSpace");
            _targets = serializedObject.FindProperty("targets");
            _honamiAnimator = serializedObject.FindProperty("honamiAnimator");
            _avatar = serializedObject.FindProperty("avatar");
            _avatarTargets = serializedObject.FindProperty("avatarTargets");
            _weight = serializedObject.FindProperty("weight");

            Undo.undoRedoPerformed += Repaint;
            Tools.hidden = false;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            Tools.hidden = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader("SETTINGS");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_weight);
                EditorGUILayout.PropertyField(_targetSource);
                EditorGUILayout.PropertyField(_poseType);
                EditorGUILayout.PropertyField(_poseSpace);
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

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawManualTargets()
        {
            DrawHeader("CONSTRAINED OBJECTS");

            int newSize = EditorGUILayout.IntField("Count", _targets.arraySize);
            if (newSize != _targets.arraySize) _targets.arraySize = newSize;

            for (int i = 0; i < _targets.arraySize; i++)
            {
                SerializedProperty element = _targets.GetArrayElementAtIndex(i);
                SerializedProperty boneProp = element.FindPropertyRelative("bone");
                string label = boneProp.objectReferenceValue != null ? boneProp.objectReferenceValue.name : $"Object {i}";

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
                newElement.FindPropertyRelative("positionWeight").floatValue = 1f;
                newElement.FindPropertyRelative("rotationWeight").floatValue = 1f;
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
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("target"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("positionOffset"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("rotationOffset"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Pose Weights", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("positionWeight"), new GUIContent("Position Weight"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("rotationWeight"), new GUIContent("Rotation Weight"));
            }
        }

        private void PopulateFromAvatar()
        {
            HonamiAvatar sourceAvatar = _avatar.objectReferenceValue as HonamiAvatar;
            if (sourceAvatar == null && _honamiAnimator.objectReferenceValue is HonamiAnimator animator)
                sourceAvatar = animator.Avatar;

            if (sourceAvatar == null)
            {
                Debug.LogWarning("HonamiPoseConstraint: assign a Honami Avatar or a Honami Animator with Avatar first.");
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
                Debug.LogWarning("HonamiPoseConstraint: assign Honami Animator to validate avatar bone paths.");
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
                        Debug.LogWarning($"HonamiPoseConstraint: '{boneName}' is disabled by HonamiBoneReplacer because it has no replacement.", animator);
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
                    Debug.LogWarning($"HonamiPoseConstraint: missing avatar bone '{boneName}' at path '{bonePath}'.", animator);
                }
            }

            Debug.Log($"HonamiPoseConstraint: avatar validation finished. Resolved: {resolved}, Missing: {missing}.", animator);
        }

        private static void SetDefaultAvatarTarget(SerializedProperty prop, string boneName, string bonePath)
        {
            prop.FindPropertyRelative("enabled").boolValue = true;
            prop.FindPropertyRelative("boneName").stringValue = boneName;
            prop.FindPropertyRelative("bonePath").stringValue = bonePath;
            prop.FindPropertyRelative("target").objectReferenceValue = null;
            prop.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
            prop.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
            prop.FindPropertyRelative("positionWeight").floatValue = 1f;
            prop.FindPropertyRelative("rotationWeight").floatValue = 1f;
        }

        private void DrawTargetSettings(SerializedProperty prop, string label, bool showBox)
        {
            if (showBox) EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(prop.FindPropertyRelative("bone"), label != null ? new GUIContent(label) : new GUIContent("Bone"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("target"), new GUIContent("Target"));

            EditorGUILayout.Space(2);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("positionOffset"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("rotationOffset"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Pose Weights", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("positionWeight"), new GUIContent("Position Weight"));
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("rotationWeight"), new GUIContent("Rotation Weight"));
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

        private void OnSceneGUI()
        {
            if ((Event.current.type == EventType.ValidateCommand || Event.current.type == EventType.ExecuteCommand) && Event.current.commandName == "UndoRedoPerformed") return;

            HonamiPoseConstraint constraint = (HonamiPoseConstraint)target;
            if (constraint == null) return;

            _isAvatarMode = constraint.targetSource == HonamiLookAtTargetSource.Avatar;
            int count = _isAvatarMode ? (constraint.avatarTargets != null ? constraint.avatarTargets.Length : 0) : (constraint.targets != null ? constraint.targets.Length : 0);

            _selectedTargetIndices.RemoveWhere(idx => idx >= count);
            if (_selectedTargetIndices.Count == 0) _lastClickedIndex = -1;
            else if (!_selectedTargetIndices.Contains(_lastClickedIndex)) _lastClickedIndex = _selectedTargetIndices.First();

            if (GUIUtility.hotControl != 0)
            {
                if (!_isDragging && _selectedTargetIndices.Count > 0)
                {
                    _multiDragIndices.Clear();
                    _multiDragStartPosOffsets.Clear();
                    _multiDragStartRotOffsets.Clear();
                    _multiDragStartBasePos.Clear();
                    _multiDragStartBaseRot.Clear();
                    _multiDragStartBaseLocalPos.Clear();
                    _multiDragStartBaseLocalRot.Clear();
                    _multiDragStartTargetPos.Clear();
                    _multiDragStartTargetRot.Clear();
                    _multiDragStartTargetLocalPos.Clear();
                    _multiDragStartTargetLocalRot.Clear();
                    _multiDragStartHasTarget.Clear();
                    _multiDragStartHasParent.Clear();
                    _multiDragStartParentMatrix.Clear();
                    _multiDragStartParentRot.Clear();
                    _multiDragStartParentPos.Clear();
                    _multiDragStartPoseWorldPos.Clear();
                    _multiDragStartPoseWorldRot.Clear();
                    _multiDragStartBones.Clear();
                    _multiDragStartBoneSet.Clear();
                    foreach (int idx in _selectedTargetIndices)
                    {
                        if (idx >= 0 && idx < count)
                        {
                            _multiDragIndices.Add(idx);
                        }
                    }
                    if (_multiDragIndices.Count > 0)
                    {
                        var constraintToRecord = target as HonamiPoseConstraint;
                        if (constraintToRecord != null)
                        {
                            Undo.RecordObject(constraintToRecord, "Edit Pose Constraint Handle");
                        }

                        _isDragging = true;
                        _primaryDragIndex = _lastClickedIndex;

                        foreach (int draggedIdx in _multiDragIndices)
                        {
                            Transform bone = null;
                            Transform targetTrans = null;
                            float pWeight = 1f;
                            float rWeight = 1f;
                            Vector3 posOffset = Vector3.zero;
                            Vector3 rotOffset = Vector3.zero;

                            if (_isAvatarMode)
                            {
                                var a = constraint.avatarTargets[draggedIdx];
                                bone = constraint.ResolveAvatarBone(a);
                                targetTrans = a.target;
                                posOffset = a.positionOffset;
                                rotOffset = a.rotationOffset;
                                pWeight = a.positionWeight * constraint.weight;
                                rWeight = a.rotationWeight * constraint.weight;
                            }
                            else
                            {
                                var t = constraint.targets[draggedIdx];
                                bone = t.bone;
                                targetTrans = t.target;
                                posOffset = t.positionOffset;
                                rotOffset = t.rotationOffset;
                                pWeight = t.positionWeight * constraint.weight;
                                rWeight = t.rotationWeight * constraint.weight;
                            }

                            _multiDragStartPosOffsets.Add(posOffset);
                            _multiDragStartRotOffsets.Add(rotOffset);
                            _multiDragStartBones.Add(bone);
                            _multiDragStartBoneSet.Add(bone);

                            if (bone != null)
                            {
                                Vector3 dragStartBonePosition = bone.position;
                                Quaternion dragStartBoneRotation = bone.rotation;
                                Vector3 dragStartBoneLocalPosition = bone.localPosition;
                                Quaternion dragStartBoneLocalRotation = bone.localRotation;

                                Transform boneParent = bone.parent;
                                if (boneParent != null)
                                {
                                    _multiDragStartParentPos.Add(boneParent.position);
                                    _multiDragStartParentMatrix.Add(boneParent.localToWorldMatrix);
                                    _multiDragStartParentRot.Add(boneParent.rotation);
                                    _multiDragStartHasParent.Add(true);
                                }
                                else
                                {
                                    _multiDragStartParentPos.Add(Vector3.zero);
                                    _multiDragStartParentMatrix.Add(Matrix4x4.identity);
                                    _multiDragStartParentRot.Add(Quaternion.identity);
                                    _multiDragStartHasParent.Add(false);
                                }

                                if (constraint.poseType == HonamiPoseType.Additive && Application.isPlaying)
                                {
                                    if (constraint.poseSpace == HonamiPoseSpace.World)
                                    {
                                        Vector3 oldTotalPosOffset = targetTrans != null ? targetTrans.position + posOffset : posOffset;
                                        Quaternion oldTotalRotOffset = targetTrans != null ? targetTrans.rotation * Quaternion.Euler(rotOffset) : Quaternion.Euler(rotOffset);
                                        _multiDragStartBasePos.Add(dragStartBonePosition - oldTotalPosOffset * pWeight);
                                        _multiDragStartBaseRot.Add(dragStartBoneRotation * Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, oldTotalRotOffset, rWeight)));
                                        _multiDragStartBaseLocalPos.Add(dragStartBoneLocalPosition);
                                        _multiDragStartBaseLocalRot.Add(dragStartBoneLocalRotation);
                                    }
                                    else
                                    {
                                        Vector3 oldTotalLocalPosOffset = targetTrans != null ? targetTrans.localPosition + posOffset : posOffset;
                                        Quaternion oldTotalLocalRotOffset = targetTrans != null ? targetTrans.localRotation * Quaternion.Euler(rotOffset) : Quaternion.Euler(rotOffset);
                                        _multiDragStartBaseLocalPos.Add(dragStartBoneLocalPosition - oldTotalLocalPosOffset * pWeight);
                                        _multiDragStartBaseLocalRot.Add(dragStartBoneLocalRotation * Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, oldTotalLocalRotOffset, rWeight)));
                                        _multiDragStartBasePos.Add(dragStartBonePosition);
                                        _multiDragStartBaseRot.Add(dragStartBoneRotation);
                                    }
                                }
                                else
                                {
                                    _multiDragStartBasePos.Add(dragStartBonePosition);
                                    _multiDragStartBaseRot.Add(dragStartBoneRotation);
                                    _multiDragStartBaseLocalPos.Add(dragStartBoneLocalPosition);
                                    _multiDragStartBaseLocalRot.Add(dragStartBoneLocalRotation);
                                }
                            }
                            else
                            {
                                _multiDragStartParentPos.Add(Vector3.zero);
                                _multiDragStartParentMatrix.Add(Matrix4x4.identity);
                                _multiDragStartParentRot.Add(Quaternion.identity);
                                _multiDragStartHasParent.Add(false);
                                _multiDragStartBasePos.Add(Vector3.zero);
                                _multiDragStartBaseRot.Add(Quaternion.identity);
                                _multiDragStartBaseLocalPos.Add(Vector3.zero);
                                _multiDragStartBaseLocalRot.Add(Quaternion.identity);
                            }

                            if (targetTrans != null)
                            {
                                _multiDragStartTargetPos.Add(targetTrans.position);
                                _multiDragStartTargetRot.Add(targetTrans.rotation);
                                _multiDragStartTargetLocalPos.Add(targetTrans.localPosition);
                                _multiDragStartTargetLocalRot.Add(targetTrans.localRotation);
                                _multiDragStartHasTarget.Add(true);
                            }
                            else
                            {
                                _multiDragStartTargetPos.Add(Vector3.zero);
                                _multiDragStartTargetRot.Add(Quaternion.identity);
                                _multiDragStartTargetLocalPos.Add(Vector3.zero);
                                _multiDragStartTargetLocalRot.Add(Quaternion.identity);
                                _multiDragStartHasTarget.Add(false);
                            }

                            Vector3 m_poseWorldPos = Vector3.zero;
                            Quaternion m_poseWorldRot = Quaternion.identity;

                            if (bone != null)
                            {
                                bool m_isAdditive = constraint.poseType == HonamiPoseType.Additive;
                                Vector3 m_basePos = _multiDragStartBasePos[_multiDragStartBasePos.Count - 1];
                                Quaternion m_baseRot = _multiDragStartBaseRot[_multiDragStartBaseRot.Count - 1];
                                Vector3 m_baseLocalPos = _multiDragStartBaseLocalPos[_multiDragStartBaseLocalPos.Count - 1];
                                Quaternion m_baseLocalRot = _multiDragStartBaseLocalRot[_multiDragStartBaseLocalRot.Count - 1];

                                if (constraint.poseSpace == HonamiPoseSpace.World)
                                {
                                    if (m_isAdditive)
                                    {
                                        m_poseWorldPos = targetTrans != null ? m_basePos + targetTrans.position + posOffset : m_basePos + posOffset;
                                        m_poseWorldRot = targetTrans != null ? m_baseRot * targetTrans.rotation * Quaternion.Euler(rotOffset) : m_baseRot * Quaternion.Euler(rotOffset);
                                    }
                                    else
                                    {
                                        m_poseWorldPos = targetTrans != null ? targetTrans.position + posOffset : posOffset;
                                        m_poseWorldRot = targetTrans != null ? targetTrans.rotation * Quaternion.Euler(rotOffset) : Quaternion.Euler(rotOffset);
                                    }
                                }
                                else
                                {
                                    Vector3 m_targetLocalPos;
                                    Quaternion m_targetLocalRot;
                                    if (m_isAdditive)
                                    {
                                        m_targetLocalPos = targetTrans != null ? m_baseLocalPos + targetTrans.localPosition + posOffset : m_baseLocalPos + posOffset;
                                        m_targetLocalRot = targetTrans != null ? m_baseLocalRot * targetTrans.localRotation * Quaternion.Euler(rotOffset) : m_baseLocalRot * Quaternion.Euler(rotOffset);
                                    }
                                    else
                                    {
                                        m_targetLocalPos = targetTrans != null ? targetTrans.localPosition + posOffset : posOffset;
                                        m_targetLocalRot = targetTrans != null ? targetTrans.localRotation * Quaternion.Euler(rotOffset) : Quaternion.Euler(rotOffset);
                                    }

                                    bool m_hasParent = _multiDragStartHasParent[_multiDragStartHasParent.Count - 1];
                                    if (m_hasParent)
                                    {
                                        Matrix4x4 pMat = _multiDragStartParentMatrix[_multiDragStartParentMatrix.Count - 1];
                                        Quaternion pRot = _multiDragStartParentRot[_multiDragStartParentRot.Count - 1];
                                        m_poseWorldPos = pMat.MultiplyPoint3x4(m_targetLocalPos);
                                        m_poseWorldRot = pRot * m_targetLocalRot;
                                    }
                                    else
                                    {
                                        m_poseWorldPos = m_targetLocalPos;
                                        m_poseWorldRot = m_targetLocalRot;
                                    }
                                }
                            }
                            _multiDragStartPoseWorldPos.Add(m_poseWorldPos);
                            _multiDragStartPoseWorldRot.Add(m_poseWorldRot);
                        }
                    }
                }
            }
            else
            {
                if (_isDragging)
                {
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(constraint);
                    _isDragging = false;
                    _multiDragIndices.Clear();
                    _multiDragStartPosOffsets.Clear();
                    _multiDragStartRotOffsets.Clear();
                    _multiDragStartBasePos.Clear();
                    _multiDragStartBaseRot.Clear();
                    _multiDragStartBaseLocalPos.Clear();
                    _multiDragStartBaseLocalRot.Clear();
                    _multiDragStartTargetPos.Clear();
                    _multiDragStartTargetRot.Clear();
                    _multiDragStartTargetLocalPos.Clear();
                    _multiDragStartTargetLocalRot.Clear();
                    _multiDragStartHasTarget.Clear();
                    _multiDragStartHasParent.Clear();
                    _multiDragStartParentMatrix.Clear();
                    _multiDragStartParentRot.Clear();
                    _multiDragStartParentPos.Clear();
                    _multiDragStartPoseWorldPos.Clear();
                    _multiDragStartPoseWorldRot.Clear();
                    _multiDragStartBones.Clear();
                    _multiDragStartBoneSet.Clear();
                }
                _isDragging = false;
            }

            bool isAnyBoneSelected = _selectedTargetIndices.Count > 0;
            Tools.hidden = isAnyBoneSelected;

            for (int i = 0; i < count; i++)
            {
                Transform bone = null;
                Transform targetTrans = null;
                Vector3 posOffset = Vector3.zero;
                Vector3 rotOffset = Vector3.zero;
                float pWeight = 1f;
                float rWeight = 1f;
                bool enabled = true;

                if (_isAvatarMode)
                {
                    var a = constraint.avatarTargets[i];
                    enabled = a.enabled;
                    bone = constraint.ResolveAvatarBone(a);
                    targetTrans = a.target;
                    posOffset = a.positionOffset;
                    rotOffset = a.rotationOffset;
                    pWeight = a.positionWeight * constraint.weight;
                    rWeight = a.rotationWeight * constraint.weight;
                }
                else
                {
                    var t = constraint.targets[i];
                    bone = t.bone;
                    targetTrans = t.target;
                    posOffset = t.positionOffset;
                    rotOffset = t.rotationOffset;
                    pWeight = t.positionWeight * constraint.weight;
                    rWeight = t.rotationWeight * constraint.weight;
                }

                if (!enabled || bone == null) continue;

                Vector3 poseWorldPos = bone.position;
                Quaternion poseWorldRot = bone.rotation;
                bool isAdditive = constraint.poseType == HonamiPoseType.Additive;

                Vector3 basePos = bone.position;
                Quaternion baseRot = bone.rotation;
                Vector3 baseLocalPos = bone.localPosition;
                Quaternion baseLocalRot = bone.localRotation;

                if (_isDragging && _multiDragIndices.Contains(i))
                {
                    int m = _multiDragIndices.IndexOf(i);
                    basePos = _multiDragStartBasePos[m];
                    baseRot = _multiDragStartBaseRot[m];
                    baseLocalPos = _multiDragStartBaseLocalPos[m];
                    baseLocalRot = _multiDragStartBaseLocalRot[m];
                }
                else if (isAdditive && Application.isPlaying)
                {
                    if (constraint.poseSpace == HonamiPoseSpace.World)
                    {
                        Vector3 totalPosOffset = targetTrans != null ? targetTrans.position + posOffset : posOffset;
                        Quaternion totalRotOffset = targetTrans != null ? targetTrans.rotation * Quaternion.Euler(rotOffset) : Quaternion.Euler(rotOffset);
                        basePos = bone.position - totalPosOffset * pWeight;
                        baseRot = bone.rotation * Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, totalRotOffset, rWeight));
                    }
                    else
                    {
                        Vector3 totalLocalPosOffset = targetTrans != null ? targetTrans.localPosition + posOffset : posOffset;
                        Quaternion totalLocalRotOffset = targetTrans != null ? targetTrans.localRotation * Quaternion.Euler(rotOffset) : Quaternion.Euler(rotOffset);
                        baseLocalPos = bone.localPosition - totalLocalPosOffset * pWeight;
                        baseLocalRot = bone.localRotation * Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, totalLocalRotOffset, rWeight));
                    }
                }

                if (constraint.poseSpace == HonamiPoseSpace.World)
                {
                    if (isAdditive)
                    {
                        poseWorldPos = targetTrans != null ? basePos + targetTrans.position + posOffset : basePos + posOffset;
                        poseWorldRot = targetTrans != null ? baseRot * targetTrans.rotation * Quaternion.Euler(rotOffset) : baseRot * Quaternion.Euler(rotOffset);
                    }
                    else
                    {
                        poseWorldPos = targetTrans != null ? targetTrans.position + posOffset : posOffset;
                        poseWorldRot = targetTrans != null ? targetTrans.rotation * Quaternion.Euler(rotOffset) : Quaternion.Euler(rotOffset);
                    }
                }
                else
                {
                    Transform targetLocalPosTrans = targetTrans;
                    Vector3 targetLocalPos;
                    Quaternion targetLocalRot;
                    if (isAdditive)
                    {
                        targetLocalPos = targetTrans != null ? baseLocalPos + targetTrans.localPosition + posOffset : baseLocalPos + posOffset;
                        targetLocalRot = targetTrans != null ? baseLocalRot * targetTrans.localRotation * Quaternion.Euler(rotOffset) : baseLocalRot * Quaternion.Euler(rotOffset);
                    }
                    else
                    {
                        targetLocalPos = targetTrans != null ? targetTrans.localPosition + posOffset : posOffset;
                        targetLocalRot = targetTrans != null ? targetTrans.localRotation * Quaternion.Euler(rotOffset) : Quaternion.Euler(rotOffset);
                    }

                    Transform parent = bone.parent;
                    poseWorldPos = parent != null ? parent.TransformPoint(targetLocalPos) : targetLocalPos;
                    poseWorldRot = parent != null ? parent.rotation * targetLocalRot : targetLocalRot;
                }

                float handleSize = HandleUtility.GetHandleSize(poseWorldPos) * 0.15f;

                if (bone.parent != null)
                {
                    Handles.color = new Color(0.5f, 1f, 0.5f, 0.3f);
                    Vector3 parentPos = bone.parent.position;
                    if (_isDragging && _multiDragIndices.Contains(i))
                    {
                        int m = _multiDragIndices.IndexOf(i);
                        if (_multiDragStartHasParent[m]) parentPos = _multiDragStartParentPos[m];
                    }
                    Handles.DrawLine(parentPos, poseWorldPos, 3f);
                }

                bool isSelected = _selectedTargetIndices.Contains(i);
                Handles.color = isSelected ? Color.yellow : Color.cyan;
                if (Handles.Button(poseWorldPos, poseWorldRot, handleSize, handleSize * 1.5f, Handles.SphereHandleCap))
                {
                    if (Event.current.shift)
                    {
                        if (!_selectedTargetIndices.Remove(i))
                            _selectedTargetIndices.Add(i);
                        _lastClickedIndex = i;
                    }
                    else
                    {
                        _selectedTargetIndices.Clear();
                        _selectedTargetIndices.Add(i);
                        _lastClickedIndex = i;
                    }
                    Repaint();
                }

                if (isSelected && _lastClickedIndex == i)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newWorldPos = poseWorldPos;
                    Quaternion newWorldRot = poseWorldRot;

                    if (Tools.current == Tool.Move)
                    {
                        newWorldPos = Handles.PositionHandle(poseWorldPos, poseWorldRot);
                    }
                    else if (Tools.current == Tool.Rotate)
                    {
                        newWorldRot = Handles.RotationHandle(poseWorldRot, poseWorldPos);
                    }
                    bool movingPos = Tools.current == Tool.Move;
                    bool movingRot = Tools.current == Tool.Rotate;

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (!_isDragging || i != _primaryDragIndex) continue;

                        int primaryListIdx = _multiDragIndices.IndexOf(i);

                        Vector3 dragStartWorldPos = _multiDragStartPoseWorldPos[primaryListIdx];
                        Quaternion dragStartWorldRot = _multiDragStartPoseWorldRot[primaryListIdx];

                        Vector3 worldPosDelta = newWorldPos - dragStartWorldPos;
                        Quaternion worldRotDelta = newWorldRot * Quaternion.Inverse(dragStartWorldRot);

                        for (int m = 0; m < _multiDragIndices.Count; m++)
                        {
                            int targetIdx = _multiDragIndices[m];
                            Transform m_bone = _multiDragStartBones[m];

                            bool isDescendant = false;
                            if (m_bone != null)
                            {
                                Transform p = m_bone.parent;
                                while (p != null)
                                {
                                    if (_multiDragStartBoneSet.Contains(p))
                                    {
                                        isDescendant = true;
                                        break;
                                    }
                                    p = p.parent;
                                }
                            }
                            if (isDescendant) continue;

                            Vector3 m_dragStartWorldPos = _multiDragStartPoseWorldPos[m];
                            Quaternion m_dragStartWorldRot = _multiDragStartPoseWorldRot[m];

                            Vector3 m_desiredWorldPos = m_dragStartWorldPos + worldPosDelta;
                            Quaternion m_desiredWorldRot = worldRotDelta * m_dragStartWorldRot;

                            Vector3 finalPosOffset = _multiDragStartPosOffsets[m];
                            Vector3 finalRotOffset = _multiDragStartRotOffsets[m];

                            if (constraint.poseSpace == HonamiPoseSpace.World)
                            {
                                if (isAdditive)
                                {
                                    if (movingPos) finalPosOffset = _multiDragStartHasTarget[m] ? m_desiredWorldPos - _multiDragStartBasePos[m] - _multiDragStartTargetPos[m] : m_desiredWorldPos - _multiDragStartBasePos[m];
                                    if (movingRot) { Quaternion deltaQuat = _multiDragStartHasTarget[m] ? Quaternion.Inverse(_multiDragStartBaseRot[m] * _multiDragStartTargetRot[m]) * m_desiredWorldRot : Quaternion.Inverse(_multiDragStartBaseRot[m]) * m_desiredWorldRot; finalRotOffset = GetContinuousEuler(_multiDragStartRotOffsets[m], deltaQuat); }
                                }
                                else
                                {
                                    if (movingPos) finalPosOffset = _multiDragStartHasTarget[m] ? m_desiredWorldPos - _multiDragStartTargetPos[m] : m_desiredWorldPos;
                                    if (movingRot) { Quaternion deltaQuat = _multiDragStartHasTarget[m] ? Quaternion.Inverse(_multiDragStartTargetRot[m]) * m_desiredWorldRot : m_desiredWorldRot; finalRotOffset = GetContinuousEuler(_multiDragStartRotOffsets[m], deltaQuat); }
                                }
                            }
                            else
                            {
                                Vector3 localPos = _multiDragStartHasParent[m] ? _multiDragStartParentMatrix[m].inverse.MultiplyPoint3x4(m_desiredWorldPos) : m_desiredWorldPos;
                                Quaternion localRot = _multiDragStartHasParent[m] ? Quaternion.Inverse(_multiDragStartParentRot[m]) * m_desiredWorldRot : m_desiredWorldRot;

                                if (isAdditive)
                                {
                                    if (movingPos) finalPosOffset = _multiDragStartHasTarget[m] ? localPos - _multiDragStartBaseLocalPos[m] - _multiDragStartTargetLocalPos[m] : localPos - _multiDragStartBaseLocalPos[m];
                                    if (movingRot) { Quaternion deltaQuat = _multiDragStartHasTarget[m] ? Quaternion.Inverse(_multiDragStartBaseLocalRot[m] * _multiDragStartTargetLocalRot[m]) * localRot : Quaternion.Inverse(_multiDragStartBaseLocalRot[m]) * localRot; finalRotOffset = GetContinuousEuler(_multiDragStartRotOffsets[m], deltaQuat); }
                                }
                                else
                                {
                                    if (movingPos) finalPosOffset = _multiDragStartHasTarget[m] ? localPos - _multiDragStartTargetLocalPos[m] : localPos;
                                    if (movingRot) { Quaternion deltaQuat = _multiDragStartHasTarget[m] ? Quaternion.Inverse(_multiDragStartTargetLocalRot[m]) * localRot : localRot; finalRotOffset = GetContinuousEuler(_multiDragStartRotOffsets[m], deltaQuat); }
                                }
                            }

                            if (_isAvatarMode)
                            {
                                constraint.avatarTargets[targetIdx].positionOffset = finalPosOffset;
                                constraint.avatarTargets[targetIdx].rotationOffset = finalRotOffset;
                            }
                            else
                            {
                                constraint.targets[targetIdx].positionOffset = finalPosOffset;
                                constraint.targets[targetIdx].rotationOffset = finalRotOffset;
                            }
                        }
                        EditorUtility.SetDirty(constraint);
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(constraint);
                    }
                }
            }
        }

        private Vector3 GetContinuousEuler(Vector3 oldEuler, Quaternion newRotation)
        {
            Vector3 newEuler = newRotation.eulerAngles;

            // Generate the mathematically equivalent alternative Euler representation for Unity (Z-X-Y order)
            Vector3 altEuler = new Vector3(
                180f - newEuler.x,
                newEuler.y + 180f,
                newEuler.z + 180f
            );

            // Normalize both to be closest to oldEuler in terms of 360 degree wrapping
            newEuler.x = newEuler.x + Mathf.Round((oldEuler.x - newEuler.x) / 360f) * 360f;
            newEuler.y = newEuler.y + Mathf.Round((oldEuler.y - newEuler.y) / 360f) * 360f;
            newEuler.z = newEuler.z + Mathf.Round((oldEuler.z - newEuler.z) / 360f) * 360f;

            altEuler.x = altEuler.x + Mathf.Round((oldEuler.x - altEuler.x) / 360f) * 360f;
            altEuler.y = altEuler.y + Mathf.Round((oldEuler.y - altEuler.y) / 360f) * 360f;
            altEuler.z = altEuler.z + Mathf.Round((oldEuler.z - altEuler.z) / 360f) * 360f;

            // Choose the one that minimizes the distance to oldEuler
            if (Vector3.Distance(oldEuler, altEuler) < Vector3.Distance(oldEuler, newEuler))
            {
                return altEuler;
            }

            return newEuler;
        }
    }
}
