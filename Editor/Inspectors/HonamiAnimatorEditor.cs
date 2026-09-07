using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiAnimator))]
    public sealed class HonamiAnimatorEditor : UnityEditor.Editor
    {
        private SerializedProperty _controller;
        private SerializedProperty _avatar;
        private SerializedProperty _mirrorAvatar;
        private SerializedProperty _mirrorBlendSpeed;
        private SerializedProperty _captureFromDefaultStateEnd;
        private SerializedProperty _releaseFinishedStatesWithoutDefault;

        private readonly HonamiSharedAnimatorInspector _shared = new HonamiSharedAnimatorInspector();

        private static GUIStyle _headerStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _debugHeaderStyle;

        private static readonly GUIContent _controllerContent = new GUIContent("Controller", "The HonamiController asset that defines states and transitions.");
        private static readonly GUIContent _avatarContent = new GUIContent("Avatar", "Optional skeleton definition. Required to use Avatar Masks on states.");
        private static readonly GUIContent _mirrorAvatarContent = new GUIContent("Global Mirror", "Dynamically mirror all animations applied to the avatar's bones.");
        private static readonly GUIContent _mirrorBlendSpeedContent = new GUIContent("Mirror Blend Speed", "Speed of the smooth transition when toggling mirror. 0 = instant.");
        private static readonly GUIContent _captureFromDefaultStateEndContent = new GUIContent("Capture From Default State End", "If true, captures the initial pose from the final frame of the default state instead of the Awake pose.");
        private static readonly GUIContent _releaseFinishedWithoutDefaultContent = new GUIContent("Release Finished Without Default", "For non-loop states on layers without a default state, release the state after it finishes instead of holding its final frame.");

        private string[] _layerLabelCache = System.Array.Empty<string>();
        private HonamiRuntimeController _layerLabelCacheController;

        private void OnEnable()
        {
            _controller = serializedObject.FindProperty("controller");
            _avatar = serializedObject.FindProperty("avatar");
            _mirrorAvatar = serializedObject.FindProperty("_mirrorAvatar");
            _mirrorBlendSpeed = serializedObject.FindProperty("_mirrorBlendSpeed");
            _captureFromDefaultStateEnd = serializedObject.FindProperty("captureFromDefaultStateEnd");
            _releaseFinishedStatesWithoutDefault = serializedObject.FindProperty("releaseFinishedStatesWithoutDefault");
            _shared.Bind(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            HonamiAnimator animator = (HonamiAnimator)target;

            DrawCustomHeader();
            EditorGUILayout.Space(5);
            DrawRequiredSection();
            EditorGUILayout.Space(5);
            _shared.DrawPlaybackSettings();
            EditorGUILayout.Space(5);
            DrawInitialPoseSection(animator);
            EditorGUILayout.Space(5);
            _shared.DrawAnimatorSync();
            EditorGUILayout.Space(5);
            _shared.DrawLinkedSystem();
            EditorGUILayout.Space(10);
            DrawActionsSection();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);
                DrawRuntimeDebugInfo(animator);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCustomHeader()
        {
            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.18f, 0.76f, 0.9f) }
            };

            EditorGUILayout.Space(10);
            GUILayout.Label("HONAMI ANIMATOR", _headerStyle);

            _subtitleStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Advanced Playable Animation System", _subtitleStyle);

            Rect rect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.76f, 0.9f, 0.5f));
            EditorGUILayout.Space(5);
        }

        private void DrawRequiredSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Core Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(_controller, _controllerContent);
            EditorGUILayout.PropertyField(_avatar, _avatarContent);

            if (_avatar.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(_mirrorAvatar, _mirrorAvatarContent);
                EditorGUILayout.PropertyField(_mirrorBlendSpeed, _mirrorBlendSpeedContent);
            }

            if (_controller.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("A Honami Controller is required to run animations.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawInitialPoseSection(HonamiAnimator animator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Initial Pose", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(_shared.CaptureInitialPoseOnAwake, HonamiSharedAnimatorInspector.CaptureOnAwakeContent);
            EditorGUILayout.PropertyField(_captureFromDefaultStateEnd, _captureFromDefaultStateEndContent);
            EditorGUILayout.PropertyField(_shared.RestoreInitialPoseWhenIdle, HonamiSharedAnimatorInspector.RestoreWhenIdleContent);
            EditorGUILayout.PropertyField(_releaseFinishedStatesWithoutDefault, _releaseFinishedWithoutDefaultContent);
            EditorGUILayout.PropertyField(_shared.IncludeRootTransformInInitialPose, HonamiSharedAnimatorInspector.IncludeRootTransformContent);

            EditorGUILayout.Space(4);
            _shared.DrawGlobalWeightMode(_shared.CaptureInitialPoseOnAwake.boolValue || _captureFromDefaultStateEnd.boolValue);
            _shared.DrawPoseButtons(animator);

            EditorGUILayout.EndVertical();
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fixedHeight = 35
            };

            if (GUILayout.Button("Open Graph Editor", _buttonStyle))
            {
                if (_controller.objectReferenceValue != null)
                {
                    HonamiGraphWindow.OpenWindow();
                    HonamiGraphWindow.LoadController((HonamiRuntimeController)_controller.objectReferenceValue);
                }
                else
                {
                    Debug.LogWarning("[Honami] Please assign a HonamiController first.");
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeDebugInfo(HonamiAnimator animator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _debugHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.9f, 0.6f, 0.2f) } };
            GUILayout.Label(" Runtime Debug", _debugHeaderStyle);
            EditorGUILayout.Space(3);

            var controller = animator.CurrentController;
            if (controller != null && controller.ActiveLayers.Count > 0)
            {
                if (_layerLabelCache.Length != controller.ActiveLayers.Count || _layerLabelCacheController != controller)
                {
                    _layerLabelCache = new string[controller.ActiveLayers.Count];
                    _layerLabelCacheController = controller;
                }

                for (int i = 0; i < controller.ActiveLayers.Count; i++)
                {
                    int currentStateIdx = animator.GetActiveStateIndex(i);
                    string stateName = "None";

                    if (currentStateIdx >= 0 && currentStateIdx < controller.ActiveStates.Count)
                    {
                        var state = controller.ActiveStates[currentStateIdx];
                        stateName = state != null ? state.stateName : "Invalid";
                    }

                    string layerLabel = _layerLabelCache[i] ??= $"Layer {i} [{controller.ActiveLayers[i].name}]:";
                    EditorGUILayout.LabelField(layerLabel, stateName, EditorStyles.boldLabel);

                    float progress = animator.GetStateProgress(i, currentStateIdx);
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 15), progress, $"Progress: {progress * 100:F1}%");
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Reset State to Default", GUILayout.Height(25)))
            {
                animator.ResetToDefault();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
