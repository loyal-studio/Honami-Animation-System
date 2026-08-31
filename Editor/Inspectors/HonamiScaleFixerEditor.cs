using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiScaleFixer))]
    public sealed class HonamiScaleFixerEditor : UnityEditor.Editor
    {
        private SerializedProperty _bone;
        private SerializedProperty _useCustomScale;
        private SerializedProperty _customTargetScale;
        private SerializedProperty _fixX;
        private SerializedProperty _fixY;
        private SerializedProperty _fixZ;
        private SerializedProperty _fixMode;
        private SerializedProperty _smoothSpeed;
        private SerializedProperty _weight;

        private bool _targetFoldout = true;
        private bool _axesFoldout = true;
        private bool _modeFoldout = true;

        private static readonly Color _accentColor = new Color(0.2f, 1f, 0.6f);
        private static readonly Color _warningColor = new Color(1f, 0.7f, 0.2f);

        private static GUIStyle _headerStyle;
        private static GUIStyle _subStyle;
        private static GUIStyle _foldoutStyle;
        private static GUIStyle _infoStyle;
        private static GUIStyle _dimStyle;
        private static GUIStyle _debugHeaderStyle;
        private static GUIStyle _valStyle;

        private void OnEnable()
        {
            _bone = serializedObject.FindProperty("bone");
            _useCustomScale = serializedObject.FindProperty("useCustomScale");
            _customTargetScale = serializedObject.FindProperty("customTargetScale");
            _fixX = serializedObject.FindProperty("fixX");
            _fixY = serializedObject.FindProperty("fixY");
            _fixZ = serializedObject.FindProperty("fixZ");
            _fixMode = serializedObject.FindProperty("fixMode");
            _smoothSpeed = serializedObject.FindProperty("smoothSpeed");
            _weight = serializedObject.FindProperty("weight");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(4);

            DrawGlobalWeight();
            EditorGUILayout.Space(4);

            DrawTargetSection();
            EditorGUILayout.Space(4);

            DrawAxesSection();
            EditorGUILayout.Space(4);

            DrawModeSection();

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
            GUILayout.Label("HONAMI SCALE FIXER", _headerStyle);
            GUILayout.Label("Eliminates Scale Jitter from FBX / Retargeted Animations", _subStyle);

            Rect line = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(line, new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.5f));
        }

        private void DrawGlobalWeight()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Slider(_weight, 0f, 1f, new GUIContent("Global Weight", "Master blend weight for the Scale Fixer."));
            EditorGUILayout.EndVertical();
        }

        private void DrawTargetSection()
        {
            _targetFoldout = DrawFoldoutHeader("  Target Bone", _targetFoldout, _accentColor);
            if (!_targetFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(_bone, new GUIContent("Bone", "The bone whose localScale will be corrected."));

            if (_bone.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a Bone transform to fix its localScale.", MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_useCustomScale, new GUIContent("Use Custom Scale", "If enabled, override with a custom target scale instead of the rest-pose scale captured at Awake."));

            if (_useCustomScale.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_customTargetScale, new GUIContent("Target Scale", "Desired localScale to enforce on the bone."));
                EditorGUI.indentLevel--;
            }
            else
            {
                var rig = (HonamiScaleFixer)target;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Captured Rest Scale", GUILayout.Width(140));

                if (Application.isPlaying)
                {
                    _infoStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = _accentColor } };
                    GUILayout.Label(rig.bone != null ? rig.bone.localScale.ToString("F4") : "—", _infoStyle);
                }
                else
                {
                    _dimStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.grey } };
                    GUILayout.Label("(captured at Awake / Enable)", _dimStyle);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Recapture Now", GUILayout.Width(120), GUILayout.Height(20)))
                    rig.CaptureRestScale();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAxesSection()
        {
            _axesFoldout = DrawFoldoutHeader("  Axes Mask", _axesFoldout, new Color(0.8f, 0.5f, 1f));
            if (!_axesFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            DrawToggleButton(_fixX, "X");
            DrawToggleButton(_fixY, "Y");
            DrawToggleButton(_fixZ, "Z");
            EditorGUILayout.EndHorizontal();

            if (!_fixX.boolValue && !_fixY.boolValue && !_fixZ.boolValue)
                EditorGUILayout.HelpBox("All axes are disabled. The rig will have no effect.", MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        private void DrawModeSection()
        {
            _modeFoldout = DrawFoldoutHeader("  Fix Mode", _modeFoldout, new Color(1f, 0.75f, 0.3f));
            if (!_modeFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_fixMode, new GUIContent("Mode",
                "SnapToTarget: instantly sets scale each frame (eliminates jitter completely).\n" +
                "Smooth: gently interpolates toward target scale (softer correction, some lag)."));

            if (_fixMode.enumValueIndex == (int)HonamiScaleFixMode.Smooth)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_smoothSpeed, new GUIContent("Smooth Speed", "Lerp speed toward the target scale per second."));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeStatus()
        {
            var rig = (HonamiScaleFixer)target;
            if (rig.bone == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _debugHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = _warningColor } };
            GUILayout.Label("  Runtime Scale", _debugHeaderStyle);
            EditorGUILayout.Space(2);

            Vector3 current = rig.bone.localScale;
            _valStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = _accentColor } };
            EditorGUILayout.LabelField("Current localScale", current.ToString("F4"), _valStyle);

            Repaint();
            EditorGUILayout.EndVertical();
        }

        private static void DrawToggleButton(SerializedProperty prop, string label)
        {
            bool active = prop.boolValue;
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = active ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.4f, 0.4f, 0.4f);
            if (GUILayout.Button(label, GUILayout.Height(22)))
                prop.boolValue = !prop.boolValue;
            GUI.backgroundColor = prev;
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
