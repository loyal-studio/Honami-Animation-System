using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiFootIKRig))]
    public sealed class HonamiFootIKRigEditor : UnityEditor.Editor
    {
        private SerializedProperty _legs;
        private SerializedProperty _rootBone;
        private SerializedProperty _rootBoneWeight;
        private SerializedProperty _rootBoneAdjustSpeed;
        private SerializedProperty _groundLayers;
        private SerializedProperty _raycastRadius;
        private SerializedProperty _raycastUpOffset;
        private SerializedProperty _raycastDistance;
        private SerializedProperty _footSurfaceOffset;
        private SerializedProperty _ikBlendSpeed;
        private SerializedProperty _ankleRotationSpeed;
        private SerializedProperty _horizontalSticking;
        private SerializedProperty _footHeightThreshold;
        private SerializedProperty _maxStickDistance;
        private SerializedProperty _stickReleaseSpeed;
        private SerializedProperty _weight;

        private bool _legsFoldout = true;
        private bool _rootFoldout = true;
        private bool _raycastFoldout = true;
        private bool _blendFoldout = true;
        private bool _stickingFoldout = true;

        private static readonly Color _accentColor = new Color(0.18f, 0.76f, 0.9f);
        private static readonly Color _warningColor = new Color(1f, 0.7f, 0.2f);
        private static readonly Color _okColor = new Color(0.3f, 0.9f, 0.4f);

        private static GUIStyle _headerStyle;
        private static GUIStyle _subStyle;
        private static GUIStyle _foldoutStyle;
        private static GUIStyle _legLabelStyle;
        private static GUIStyle _removeStyle;
        private static GUIStyle _debugHeaderStyle;
        private string[] _legLabelCache = System.Array.Empty<string>();

        private void OnEnable()
        {
            _legs = serializedObject.FindProperty("legs");
            _rootBone = serializedObject.FindProperty("rootBone");
            _rootBoneWeight = serializedObject.FindProperty("rootBoneWeight");
            _rootBoneAdjustSpeed = serializedObject.FindProperty("rootBoneAdjustSpeed");
            _groundLayers = serializedObject.FindProperty("groundLayers");
            _raycastRadius = serializedObject.FindProperty("raycastRadius");
            _raycastUpOffset = serializedObject.FindProperty("raycastUpOffset");
            _raycastDistance = serializedObject.FindProperty("raycastDistance");
            _footSurfaceOffset = serializedObject.FindProperty("footSurfaceOffset");
            _ikBlendSpeed = serializedObject.FindProperty("ikBlendSpeed");
            _ankleRotationSpeed = serializedObject.FindProperty("ankleRotationSpeed");
            _horizontalSticking = serializedObject.FindProperty("horizontalSticking");
            _footHeightThreshold = serializedObject.FindProperty("footHeightThreshold");
            _maxStickDistance = serializedObject.FindProperty("maxStickDistance");
            _stickReleaseSpeed = serializedObject.FindProperty("stickReleaseSpeed");
            _weight = serializedObject.FindProperty("weight");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(4);

            DrawGlobalWeight();
            EditorGUILayout.Space(4);

            DrawLegsSection();
            EditorGUILayout.Space(4);

            DrawRootBoneSection();
            EditorGUILayout.Space(4);

            DrawRaycastSection();
            EditorGUILayout.Space(4);

            DrawBlendSection();
            EditorGUILayout.Space(4);

            DrawStickingSection();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                DrawRuntimeStatus();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private new void DrawHeader()
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
            GUILayout.Label("HONAMI FOOT IK", _headerStyle);
            GUILayout.Label("Procedural Ground Adaptation - Any Skeleton", _subStyle);

            Rect line = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(line, new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.5f));
        }

        private void DrawGlobalWeight()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Slider(_weight, 0f, 1f, new GUIContent("Global Weight", "Master blend weight for the entire rig."));
            EditorGUILayout.EndVertical();
        }

        private void DrawLegsSection()
        {
            _legsFoldout = DrawFoldoutHeader("  Legs", _legsFoldout, _accentColor);
            if (!_legsFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_legs.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add at least one leg to enable Foot IK.", MessageType.Warning);
            }

            for (int i = 0; i < _legs.arraySize; i++)
            {
                SerializedProperty legProp = _legs.GetArrayElementAtIndex(i);
                DrawLegEntry(legProp, i);
                if (i < _legs.arraySize - 1) EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("+ Add Leg", GUILayout.Height(22)))
            {
                _legs.InsertArrayElementAtIndex(_legs.arraySize);
                var newLeg = _legs.GetArrayElementAtIndex(_legs.arraySize - 1);
                newLeg.FindPropertyRelative("weightMultiplier").floatValue = 1f;
                newLeg.FindPropertyRelative("kneeLocalAxis").vector3Value = Vector3.forward;
            }

            EditorGUI.BeginDisabledGroup(_legs.arraySize == 0);
            if (GUILayout.Button("- Remove Last", GUILayout.Height(22)))
                _legs.DeleteArrayElementAtIndex(_legs.arraySize - 1);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawLegEntry(SerializedProperty legProp, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _legLabelStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.8f, 0.95f, 1f) } };
            GUILayout.Label($"Leg {index}", _legLabelStyle);
            _removeStyle ??= new GUIStyle(GUI.skin.button) { fixedWidth = 22, fixedHeight = 18, normal = { textColor = _warningColor } };
            if (GUILayout.Button(HonamiEditorSymbols.Remove, _removeStyle)) { _legs.DeleteArrayElementAtIndex(index); return; }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(legProp.FindPropertyRelative("thigh"), new GUIContent("Thigh"));
            EditorGUILayout.PropertyField(legProp.FindPropertyRelative("calf"), new GUIContent("Calf / Shin"));
            EditorGUILayout.PropertyField(legProp.FindPropertyRelative("foot"), new GUIContent("Foot / Ankle"));
            EditorGUILayout.PropertyField(legProp.FindPropertyRelative("toes"), new GUIContent("Toes (Optional)"));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(legProp.FindPropertyRelative("kneeLocalAxis"), new GUIContent("Knee Local Axis", "Local axis of the thigh bone that points toward the knee bend direction."));
            EditorGUILayout.PropertyField(legProp.FindPropertyRelative("poleTarget"), new GUIContent("Pole Target", "Optional transform to define knee bend direction."));
            if (legProp.FindPropertyRelative("poleTarget").objectReferenceValue != null)
            {
                EditorGUILayout.Slider(legProp.FindPropertyRelative("poleWeight"), 0f, 1f, new GUIContent("Pole Weight"));
            }
            EditorGUILayout.Slider(legProp.FindPropertyRelative("weightMultiplier"), 0f, 1f, new GUIContent("Weight Multiplier"));

            bool hasThigh = legProp.FindPropertyRelative("thigh").objectReferenceValue != null;
            bool hasCalf = legProp.FindPropertyRelative("calf").objectReferenceValue != null;
            bool hasFoot = legProp.FindPropertyRelative("foot").objectReferenceValue != null;

            if (!hasThigh || !hasCalf || !hasFoot)
                EditorGUILayout.HelpBox("Assign Thigh, Calf and Foot transforms.", MessageType.Warning);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawRootBoneSection()
        {
            _rootFoldout = DrawFoldoutHeader("  Root Bone (Pelvis)", _rootFoldout, new Color(0.9f, 0.75f, 0.3f));
            if (!_rootFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_rootBone, new GUIContent("Root Bone", "Optional. Pelvis or hip bone adjusted vertically to keep legs in reach."));

            if (_rootBone.objectReferenceValue != null)
            {
                EditorGUILayout.Slider(_rootBoneWeight, 0f, 1f, new GUIContent("Blend Weight"));
                EditorGUILayout.PropertyField(_rootBoneAdjustSpeed, new GUIContent("Adjust Speed", "Higher = faster response to ground height changes."));
            }
            else
            {
                EditorGUILayout.HelpBox("No root bone assigned. Pelvis will not be adjusted - legs may over-stretch on uneven terrain.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRaycastSection()
        {
            _raycastFoldout = DrawFoldoutHeader("  Raycast", _raycastFoldout, new Color(0.5f, 0.9f, 0.5f));
            if (!_raycastFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_groundLayers, new GUIContent("Ground Layers"));
            EditorGUILayout.PropertyField(_raycastRadius, new GUIContent("Raycast Radius", "Radius of the spherecast to avoid dipping into cracks."));
            EditorGUILayout.PropertyField(_raycastUpOffset, new GUIContent("Ray Up Offset", "Distance above the foot where the ray starts."));
            EditorGUILayout.PropertyField(_raycastDistance, new GUIContent("Ray Down Distance", "Total downward cast length from the origin."));
            EditorGUILayout.PropertyField(_footSurfaceOffset, new GUIContent("Foot Surface Offset", "Small gap between foot and ground surface."));
            EditorGUILayout.EndVertical();
        }

        private void DrawBlendSection()
        {
            _blendFoldout = DrawFoldoutHeader("  Blending", _blendFoldout, new Color(0.8f, 0.5f, 1f));
            if (!_blendFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_ikBlendSpeed, new GUIContent("IK Blend Speed", "How quickly IK fades in/out when ground contact changes. 0 = instant."));
            EditorGUILayout.PropertyField(_ankleRotationSpeed, new GUIContent("Ankle Rotation Speed", "How quickly the ankle aligns to ground normal. 0 = instant."));
            EditorGUILayout.EndVertical();
        }

        private void DrawStickingSection()
        {
            _stickingFoldout = DrawFoldoutHeader("  Sticking (Anti-Sliding)", _stickingFoldout, new Color(1f, 0.6f, 0.4f));
            if (!_stickingFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Slider(_horizontalSticking, 0f, 1f, new GUIContent("Horizontal Sticking", "How much the foot 'sticks' to its landing spot horizontally to prevent sliding."));
            EditorGUILayout.PropertyField(_footHeightThreshold, new GUIContent("Height Threshold", "Maximum height of the animated foot above the ground to allow sticking."));
            EditorGUILayout.PropertyField(_maxStickDistance, new GUIContent("Max Stick Distance", "Max distance the foot can move before un-sticking."));
            EditorGUILayout.PropertyField(_stickReleaseSpeed, new GUIContent("Release Speed", "Speed at which the foot 'unlocks' and moves to a new position if it moves too far."));
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeStatus()
        {
            var rig = (HonamiFootIKRig)target;
            if (rig.legs == null || rig.legs.Length == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _debugHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = _warningColor } };
            GUILayout.Label("  Runtime Status", _debugHeaderStyle);
            EditorGUILayout.Space(2);

            if (_legLabelCache.Length != rig.legs.Length)
            {
                _legLabelCache = new string[rig.legs.Length];
                for (int i = 0; i < _legLabelCache.Length; i++)
                    _legLabelCache[i] = $"Leg {i}";
            }

            for (int legIndex = 0; legIndex < rig.legs.Length; legIndex++)
            {
                var leg = rig.legs[legIndex];
                if (leg == null) continue;
                string lbl = _legLabelCache[legIndex];

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(lbl, GUILayout.Width(90));

                Color prev = GUI.color;
                GUI.color = leg._hasContact ? _okColor : _warningColor;
                GUILayout.Label(leg._hasContact ? "On Heel" : "Off Heel", EditorStyles.miniLabel);

                if (leg.toes != null)
                {
                    GUI.color = leg._hasToesContact ? _okColor : _warningColor;
                    GUILayout.Label(leg._hasToesContact ? "On Toes" : "Off Toes", EditorStyles.miniLabel);
                }

                GUI.color = prev;

                GUILayout.FlexibleSpace();
                GUILayout.Label($"IK: {leg._currentIKWeight:F2}", EditorStyles.miniLabel, GUILayout.Width(55));
                EditorGUILayout.EndHorizontal();

                Rect bar = EditorGUILayout.GetControlRect(false, 4);
                EditorGUI.DrawRect(bar, new Color(0.2f, 0.2f, 0.2f));
                Rect fill = new Rect(bar.x, bar.y, bar.width * leg._currentIKWeight, bar.height);
                EditorGUI.DrawRect(fill, leg._hasContact ? _okColor : _warningColor);

                EditorGUILayout.Space(2);
            }

            Repaint();
            EditorGUILayout.EndVertical();
        }

        private static bool DrawFoldoutHeader(string title, bool state, Color color)
        {
            _foldoutStyle ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            _foldoutStyle.normal.textColor = color;
            _foldoutStyle.onNormal.textColor = color;
            return EditorGUILayout.Foldout(state, title, true, _foldoutStyle);
        }
    }
}
