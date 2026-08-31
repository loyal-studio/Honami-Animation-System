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
        private SerializedProperty _startup;
        private SerializedProperty _updateMode;
        private SerializedProperty _timeScale;
        private SerializedProperty _fpsCap;
        private SerializedProperty _targetFPS;
        private SerializedProperty _fpsCapInterpolate;
        private SerializedProperty _applyRootMotion;
        private SerializedProperty _cullingMode;
        private SerializedProperty _captureInitialPoseOnAwake;
        private SerializedProperty _captureFromDefaultStateEnd;
        private SerializedProperty _restoreInitialPoseWhenIdle;
        private SerializedProperty _releaseFinishedStatesWithoutDefault;
        private SerializedProperty _includeRootTransformInInitialPose;
        private SerializedProperty _globalWeightMode;

        private static GUIStyle _headerStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _debugHeaderStyle;

        private static readonly GUIContent _controllerContent = new GUIContent("Controller", "The HonamiController asset that defines states and transitions.");
        private static readonly GUIContent _avatarContent = new GUIContent("Avatar", "Optional skeleton definition. Required to use Avatar Masks on states.");
        private static readonly GUIContent _mirrorAvatarContent = new GUIContent("Global Mirror", "Dynamically mirror all animations applied to the avatar's bones.");
        private static readonly GUIContent _mirrorBlendSpeedContent = new GUIContent("Mirror Blend Speed", "Speed of the smooth transition when toggling mirror. 0 = instant.");
        private static readonly GUIContent _startupContent = new GUIContent("Startup Action", "When should the animation start playing?");
        private static readonly GUIContent _updateModeContent = new GUIContent("Update Mode", "How time drives the animation graph.");
        private static readonly GUIContent _timeScaleContent = new GUIContent("Time Scale", "Multiplier for animation speed.");
        private static readonly GUIContent _fpsCapContent = new GUIContent("FPS Cap", "Limit the animation update rate to a fixed number of frames per second.");
        private static readonly GUIContent _targetFpsContent = new GUIContent("Target FPS", "Animation graph ticks at this rate. Lower values reduce CPU cost but increase motion choppiness.");
        private static readonly GUIContent _fpsCapInterpolateContent = new GUIContent("Interpolate", "Lerp bone transforms between capped ticks each game frame for smooth motion at low target FPS.");
        private static readonly GUIContent _applyRootMotionContent = new GUIContent("Apply Root Motion", "Should root motion be applied from the animation?");
        private static readonly GUIContent _cullingModeContent = new GUIContent("Culling Mode", "Controls how the Unity Animator updates when off-screen.");
        private static readonly GUIContent _captureOnAwakeContent = new GUIContent("Capture On Awake", "Capture the hierarchy pose before Honami starts evaluating.");
        private static readonly GUIContent _captureFromDefaultStateEndContent = new GUIContent("Capture From Default State End", "If true, captures the initial pose from the final frame of the default state instead of the Awake pose.");
        private static readonly GUIContent _restoreWhenIdleContent = new GUIContent("Restore When Idle", "Restore the captured initial pose whenever no Honami state is active.");
        private static readonly GUIContent _releaseFinishedWithoutDefaultContent = new GUIContent("Release Finished Without Default", "For non-loop states on layers without a default state, release the state after it finishes instead of holding its final frame.");
        private static readonly GUIContent _includeRootTransformContent = new GUIContent("Include Root Transform", "Include this GameObject's transform in the captured pose. Leave disabled for character roots.");
        private static readonly GUIContent _globalWeightModeContent = new GUIContent("Global Weight Mode", "How GlobalWeight attenuates the animator. Init: blend the pose toward the captured initial pose (clean neutral). Bind: scale the animation output weight against the Animator bind pose.");

        private string[] _layerLabelCache = System.Array.Empty<string>();
        private HonamiRuntimeController _layerLabelCacheController;

        private void OnEnable()
        {
            _controller = serializedObject.FindProperty("controller");
            _avatar = serializedObject.FindProperty("avatar");
            _mirrorAvatar = serializedObject.FindProperty("_mirrorAvatar");
            _mirrorBlendSpeed = serializedObject.FindProperty("_mirrorBlendSpeed");
            _startup = serializedObject.FindProperty("startup");
            _updateMode = serializedObject.FindProperty("updateMode");
            _timeScale = serializedObject.FindProperty("timeScale");
            _fpsCap = serializedObject.FindProperty("fpsCap");
            _targetFPS = serializedObject.FindProperty("targetFPS");
            _fpsCapInterpolate = serializedObject.FindProperty("fpsCapInterpolate");
            _applyRootMotion = serializedObject.FindProperty("applyRootMotion");
            _cullingMode = serializedObject.FindProperty("cullingMode");
            _captureInitialPoseOnAwake = serializedObject.FindProperty("captureInitialPoseOnAwake");
            _captureFromDefaultStateEnd = serializedObject.FindProperty("captureFromDefaultStateEnd");
            _restoreInitialPoseWhenIdle = serializedObject.FindProperty("restoreInitialPoseWhenIdle");
            _releaseFinishedStatesWithoutDefault = serializedObject.FindProperty("releaseFinishedStatesWithoutDefault");
            _includeRootTransformInInitialPose = serializedObject.FindProperty("includeRootTransformInInitialPose");
            _globalWeightMode = serializedObject.FindProperty("globalWeightMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            HonamiAnimator animator = (HonamiAnimator)target;

            DrawCustomHeader();
            EditorGUILayout.Space(5);
            DrawRequiredSection();
            EditorGUILayout.Space(5);
            DrawSettingsSection();
            EditorGUILayout.Space(5);
            DrawInitialPoseSection(animator);
            EditorGUILayout.Space(5);
            DrawAnimatorSyncSection();
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

        private void DrawSettingsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Playback Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(_startup, _startupContent);
            EditorGUILayout.PropertyField(_updateMode, _updateModeContent);

            EditorGUILayout.Space(3);
            EditorGUILayout.Slider(_timeScale, 0f, 10f, _timeScaleContent);

            if (_timeScale.floatValue == 0f)
            {
                EditorGUILayout.HelpBox("Time Scale is 0. Animation is paused.", MessageType.Info);
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(_fpsCap, _fpsCapContent);

            if (_fpsCap.boolValue)
            {
                EditorGUILayout.IntSlider(_targetFPS, 1, 120, _targetFpsContent);
                EditorGUILayout.PropertyField(_fpsCapInterpolate, _fpsCapInterpolateContent);
                EditorGUILayout.HelpBox($"Animation updates at {_targetFPS.intValue} FPS instead of the game's frame rate.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAnimatorSyncSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Animator Synchronization", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(_applyRootMotion, _applyRootMotionContent);
            EditorGUILayout.PropertyField(_cullingMode, _cullingModeContent);

            EditorGUILayout.EndVertical();
        }

        private void DrawInitialPoseSection(HonamiAnimator animator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Initial Pose", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(_captureInitialPoseOnAwake, _captureOnAwakeContent);
            EditorGUILayout.PropertyField(_captureFromDefaultStateEnd, _captureFromDefaultStateEndContent);
            EditorGUILayout.PropertyField(_restoreInitialPoseWhenIdle, _restoreWhenIdleContent);
            EditorGUILayout.PropertyField(_releaseFinishedStatesWithoutDefault, _releaseFinishedWithoutDefaultContent);
            EditorGUILayout.PropertyField(_includeRootTransformInInitialPose, _includeRootTransformContent);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_globalWeightMode, _globalWeightModeContent);

            bool capturesPose = _captureInitialPoseOnAwake.boolValue || _captureFromDefaultStateEnd.boolValue;
            if (_globalWeightMode.enumValueIndex == (int)HonamiGlobalWeightMode.Init && !capturesPose)
            {
                EditorGUILayout.HelpBox("Global Weight Mode is Init but no initial pose is captured. GlobalWeight < 1 will have no effect. Enable Capture On Awake, or switch to Bind.", MessageType.Warning);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Capture Current Pose"))
                    animator.CaptureInitialPose();
                using (new EditorGUI.DisabledScope(!animator.HasInitialPose))
                {
                    if (GUILayout.Button("Restore Initial Pose"))
                        animator.RestoreInitialPose();
                }
                EditorGUILayout.EndHorizontal();
            }

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
