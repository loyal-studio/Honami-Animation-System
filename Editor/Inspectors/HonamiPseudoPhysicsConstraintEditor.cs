using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Riggings
{
    [CustomEditor(typeof(HonamiPseudoPhysicsConstraint))]
    [CanEditMultipleObjects]
    public sealed class HonamiPseudoPhysicsConstraintEditor : UnityEditor.Editor
    {
        private SerializedProperty _weight;
        private SerializedProperty _mode;
        private SerializedProperty _bones;

        private SerializedProperty _simulationGravity;
        private SerializedProperty _simulationDamping;
        private SerializedProperty _simulationStiffness;
        private SerializedProperty _simulationInertia;

        private SerializedProperty _simulationSpace;
        private SerializedProperty _positionAxisMask;
        private SerializedProperty _rotationAxisMask;
        private SerializedProperty _positionDrag;
        private SerializedProperty _positionInertia;
        private SerializedProperty _positionStiffness;
        private SerializedProperty _positionDamping;
        private SerializedProperty _maxPositionOffset;
        private SerializedProperty _positionGravity;
        private SerializedProperty _rotationDrag;
        private SerializedProperty _rotationInertia;
        private SerializedProperty _rotationStiffness;
        private SerializedProperty _rotationDamping;
        private SerializedProperty _maxRotationOffset;
        private SerializedProperty _rotationGravity;
        private SerializedProperty _rotationHangAxis;
        private SerializedProperty _rotationGravityDirection;
        private SerializedProperty _movementToRotation;

        private SerializedProperty _teleportDistanceThreshold;
        private SerializedProperty _teleportAngleThreshold;
        private SerializedProperty _simulateInEditMode;

        private HonamiPhysicsMode _modeOnEnterPlay;

        private void OnEnable()
        {
            _weight = serializedObject.FindProperty("weight");
            _mode = serializedObject.FindProperty("mode");
            _bones = serializedObject.FindProperty("bones");

            _simulationGravity = serializedObject.FindProperty("simulationGravity");
            _simulationDamping = serializedObject.FindProperty("simulationDamping");
            _simulationStiffness = serializedObject.FindProperty("simulationStiffness");
            _simulationInertia = serializedObject.FindProperty("simulationInertia");

            _simulationSpace = serializedObject.FindProperty("simulationSpace");
            _positionAxisMask = serializedObject.FindProperty("positionAxisMask");
            _rotationAxisMask = serializedObject.FindProperty("rotationAxisMask");
            _positionDrag = serializedObject.FindProperty("positionDrag");
            _positionInertia = serializedObject.FindProperty("positionInertia");
            _positionStiffness = serializedObject.FindProperty("positionStiffness");
            _positionDamping = serializedObject.FindProperty("positionDamping");
            _maxPositionOffset = serializedObject.FindProperty("maxPositionOffset");
            _positionGravity = serializedObject.FindProperty("positionGravity");
            _rotationDrag = serializedObject.FindProperty("rotationDrag");
            _rotationInertia = serializedObject.FindProperty("rotationInertia");
            _rotationStiffness = serializedObject.FindProperty("rotationStiffness");
            _rotationDamping = serializedObject.FindProperty("rotationDamping");
            _maxRotationOffset = serializedObject.FindProperty("maxRotationOffset");
            _rotationGravity = serializedObject.FindProperty("rotationGravity");
            _rotationHangAxis = serializedObject.FindProperty("rotationHangAxis");
            _rotationGravityDirection = serializedObject.FindProperty("rotationGravityDirection");
            _movementToRotation = serializedObject.FindProperty("movementToRotation");

            _teleportDistanceThreshold = serializedObject.FindProperty("teleportDistanceThreshold");
            _teleportAngleThreshold = serializedObject.FindProperty("teleportAngleThreshold");
            _simulateInEditMode = serializedObject.FindProperty("simulateInEditMode");

            _modeOnEnterPlay = (HonamiPhysicsMode)_mode.enumValueIndex;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var mode = (HonamiPhysicsMode)_mode.enumValueIndex;

            DrawHeader("SETUP");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_weight);
                EditorGUILayout.PropertyField(_mode);
            }

            EditorGUILayout.Space(5);
            DrawBones(mode);

            if (Application.isPlaying && !_mode.hasMultipleDifferentValues && mode != _modeOnEnterPlay)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("The mode is read when the animator builds its graph. Exit and re-enter Play mode to apply this change.", MessageType.Warning);
            }
            else if (!Application.isPlaying)
            {
                _modeOnEnterPlay = mode;
            }

            EditorGUILayout.Space(5);
            if (mode == HonamiPhysicsMode.Simulation) DrawSimulation();
            else DrawPseudoPhysics();

            EditorGUILayout.Space(5);
            DrawHeader("STABILITY");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_teleportDistanceThreshold);
                EditorGUILayout.PropertyField(_teleportAngleThreshold);
                EditorGUILayout.PropertyField(_simulateInEditMode);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBones(HonamiPhysicsMode mode)
        {
            DrawHeader("BONES");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int newSize = EditorGUILayout.DelayedIntField("Count", _bones.arraySize);
                if (newSize != _bones.arraySize) _bones.arraySize = Mathf.Max(0, newSize);

                for (int i = 0; i < _bones.arraySize; i++)
                {
                    SerializedProperty element = _bones.GetArrayElementAtIndex(i);
                    SerializedProperty boneProp = element.FindPropertyRelative("bone");
                    string label = boneProp.objectReferenceValue != null ? boneProp.objectReferenceValue.name : $"Bone {i}";

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, label, true);
                            if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(70)))
                            {
                                _bones.DeleteArrayElementAtIndex(i);
                                break;
                            }
                        }

                        if (!element.isExpanded) continue;

                        EditorGUILayout.PropertyField(boneProp);
                        EditorGUILayout.PropertyField(element.FindPropertyRelative("weightMultiplier"));
                        if (mode == HonamiPhysicsMode.Simulation) DrawBoneSimulation(element);
                    }
                }

                if (GUILayout.Button("Add Bone", GUILayout.Height(25)))
                {
                    _bones.InsertArrayElementAtIndex(_bones.arraySize);
                    SerializedProperty added = _bones.GetArrayElementAtIndex(_bones.arraySize - 1);
                    added.FindPropertyRelative("bone").objectReferenceValue = null;
                    added.FindPropertyRelative("weightMultiplier").floatValue = 1f;
                    added.FindPropertyRelative("joint").enumValueIndex = (int)HonamiPhysicsJoint.Free;
                    added.FindPropertyRelative("hingeAxis").vector3Value = Vector3.forward;
                    added.FindPropertyRelative("angleLimit").floatValue = 0f;
                    added.FindPropertyRelative("limitCenter").vector3Value = Vector3.zero;
                    added.FindPropertyRelative("upAxis").vector3Value = Vector3.zero;
                    added.FindPropertyRelative("tipDirection").vector3Value = Vector3.zero;
                    added.FindPropertyRelative("tipLength").floatValue = 0f;
                    added.FindPropertyRelative("gravityScale").floatValue = 1f;
                    added.FindPropertyRelative("dampingScale").floatValue = 1f;
                    added.FindPropertyRelative("stiffnessScale").floatValue = 1f;
                    added.isExpanded = true;
                }

                if (mode == HonamiPhysicsMode.Simulation && _bones.arraySize > 0)
                    EditorGUILayout.HelpBox("Bones that are parent and child of each other link into one chain. Order does not matter — it is read from the hierarchy.", MessageType.None);
            }
        }

        private void DrawBoneSimulation(SerializedProperty element)
        {
            SerializedProperty joint = element.FindPropertyRelative("joint");

            EditorGUILayout.Space(2);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Joint", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(joint);

                var jointValue = (HonamiPhysicsJoint)joint.enumValueIndex;
                if (jointValue == HonamiPhysicsJoint.Hinge)
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("hingeAxis"));

                if (jointValue != HonamiPhysicsJoint.Fixed)
                {
                    SerializedProperty angleLimit = element.FindPropertyRelative("angleLimit");
                    EditorGUILayout.PropertyField(angleLimit);
                    if (angleLimit.floatValue > 0f)
                        EditorGUILayout.PropertyField(element.FindPropertyRelative("limitCenter"));
                }

                if (jointValue != HonamiPhysicsJoint.Fixed)
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("upAxis"));

                if (jointValue == HonamiPhysicsJoint.Hinge)
                    EditorGUILayout.HelpBox("A hinge axis along the bone spins it in place like a ring on its peg: the spin follows the parent's twist, and Up Axis marks the ring's heavy side for gravity (zero = balanced ring).", MessageType.None);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Mass", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("gravityScale"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("dampingScale"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("stiffnessScale"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Tip (last bone of a chain only)", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("tipDirection"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("tipLength"));
            }
        }

        private void DrawSimulation()
        {
            DrawHeader("CHAIN BASELINE");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_simulationGravity, new GUIContent("Gravity"));
                EditorGUILayout.PropertyField(_simulationDamping, new GUIContent("Damping"));
                EditorGUILayout.PropertyField(_simulationStiffness, new GUIContent("Stiffness"));
                EditorGUILayout.PropertyField(_simulationInertia, new GUIContent("Inertia"));
                EditorGUILayout.HelpBox("Damping is the air drag on the free swing, Stiffness is the pull back to the animated pose — the two are independent, so tuning one never forces you to retune the other. Each bone scales Gravity, Damping and Stiffness for its own mass, and carries its own joint and angle limit.", MessageType.None);
            }

            if (_simulationStiffness.floatValue <= 0f)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Stiffness is 0, so the chain ignores its bind pose entirely: it starts already hanging along Gravity and stays wherever physics puts it. This is what a keychain modelled sticking straight up needs.", MessageType.None);
            }
        }

        private void DrawPseudoPhysics()
        {
            DrawHeader("SIMULATION SPACE");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_simulationSpace);
                EditorGUILayout.PropertyField(_positionAxisMask);
                EditorGUILayout.PropertyField(_rotationAxisMask);
            }

            EditorGUILayout.Space(5);
            DrawHeader("POSITION PHYSICS");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_positionDrag);
                EditorGUILayout.PropertyField(_positionInertia);
                EditorGUILayout.PropertyField(_positionStiffness);
                EditorGUILayout.PropertyField(_positionDamping);
                EditorGUILayout.PropertyField(_maxPositionOffset);
                EditorGUILayout.PropertyField(_positionGravity);
            }

            EditorGUILayout.Space(5);
            DrawHeader("ROTATION PHYSICS");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_rotationDrag);
                EditorGUILayout.PropertyField(_rotationInertia);
                EditorGUILayout.PropertyField(_rotationStiffness);
                EditorGUILayout.PropertyField(_rotationDamping);
                EditorGUILayout.PropertyField(_maxRotationOffset);
                EditorGUILayout.PropertyField(_movementToRotation);
            }

            EditorGUILayout.Space(5);
            DrawHeader("ROTATION GRAVITY (PENDULUM)");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_rotationGravity);
                if (_rotationGravity.floatValue != 0f)
                {
                    EditorGUILayout.PropertyField(_rotationHangAxis);
                    EditorGUILayout.PropertyField(_rotationGravityDirection);
                    EditorGUILayout.HelpBox("Switch to Simulation mode for anything that should truly hang and swing — this torque only bends the bone away from its animated pose.", MessageType.None);
                }
            }
        }

        private void DrawHeader(string label)
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : Color.black;

            Rect rect = GUILayoutUtility.GetRect(18, 18, headerStyle);
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            EditorGUI.LabelField(new Rect(rect.x + 4, rect.y, rect.width, rect.height), label, headerStyle);
        }
    }
}
