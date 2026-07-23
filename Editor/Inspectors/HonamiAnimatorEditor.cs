using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiAnimator))]
    public sealed class HonamiAnimatorEditor : UnityEditor.Editor
    {
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
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.18f, 0.76f, 0.9f) }
            };

            EditorGUILayout.Space(10);
            GUILayout.Label("HONAMI ANIMATOR", headerStyle);

            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Advanced Playable Animation System", subtitleStyle);

            Rect rect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.76f, 0.9f, 0.5f));
            EditorGUILayout.Space(5);
        }

        private void DrawRequiredSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Core Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            var controllerProp = serializedObject.FindProperty("controller");
            var avatarProp = serializedObject.FindProperty("avatar");

            EditorGUILayout.PropertyField(controllerProp, new GUIContent("Controller", "The HonamiController asset that defines states and transitions."));
            EditorGUILayout.PropertyField(avatarProp, new GUIContent("Avatar", "Optional skeleton definition. Required to use Avatar Masks on states."));

            if (avatarProp.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_mirrorAvatar"), new GUIContent("Global Mirror", "Dynamically mirror all animations applied to the avatar's bones."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_mirrorBlendSpeed"), new GUIContent("Mirror Blend Speed", "Speed of the smooth transition when toggling mirror. 0 = instant."));
            }

            if (controllerProp.objectReferenceValue == null)
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

            EditorGUILayout.PropertyField(serializedObject.FindProperty("startup"), new GUIContent("Startup Action", "When should the animation start playing?"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("updateMode"), new GUIContent("Update Mode", "How time drives the animation graph."));

            EditorGUILayout.Space(3);
            var timeScaleProp = serializedObject.FindProperty("timeScale");
            EditorGUILayout.Slider(timeScaleProp, 0f, 10f, new GUIContent("Time Scale", "Multiplier for animation speed."));

            if (timeScaleProp.floatValue == 0f)
            {
                EditorGUILayout.HelpBox("Time Scale is 0. Animation is paused.", MessageType.Info);
            }

            EditorGUILayout.Space(3);
            var fpsCapProp = serializedObject.FindProperty("fpsCap");
            EditorGUILayout.PropertyField(fpsCapProp, new GUIContent("FPS Cap", "Limit the animation update rate to a fixed number of frames per second."));

            if (fpsCapProp.boolValue)
            {
                var targetFpsProp = serializedObject.FindProperty("targetFPS");
                EditorGUILayout.IntSlider(targetFpsProp, 1, 120, new GUIContent("Target FPS", "Animation graph ticks at this rate. Lower values reduce CPU cost but increase motion choppiness."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fpsCapInterpolate"), new GUIContent("Interpolate", "Lerp bone transforms between capped ticks each game frame for smooth motion at low target FPS."));
                EditorGUILayout.HelpBox($"Animation updates at {targetFpsProp.intValue} FPS instead of the game's frame rate.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAnimatorSyncSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Animator Synchronization", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyRootMotion"), new GUIContent("Apply Root Motion", "Should root motion be applied from the animation?"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cullingMode"), new GUIContent("Culling Mode", "Controls how the Unity Animator updates when off-screen."));

            EditorGUILayout.EndVertical();
        }

        private void DrawInitialPoseSection(HonamiAnimator animator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Initial Pose", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("captureInitialPoseOnAwake"), new GUIContent("Capture On Awake", "Capture the hierarchy pose before Honami starts evaluating."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("captureFromDefaultStateEnd"), new GUIContent("Capture From Default State End", "If true, captures the initial pose from the final frame of the default state instead of the Awake pose."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("restoreInitialPoseWhenIdle"), new GUIContent("Restore When Idle", "Restore the captured initial pose whenever no Honami state is active."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("releaseFinishedStatesWithoutDefault"), new GUIContent("Release Finished Without Default", "For non-loop states on layers without a default state, release the state after it finishes instead of holding its final frame."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("includeRootTransformInInitialPose"), new GUIContent("Include Root Transform", "Include this GameObject's transform in the captured pose. Leave disabled for character roots."));

            EditorGUILayout.Space(4);
            var globalWeightModeProp = serializedObject.FindProperty("globalWeightMode");
            EditorGUILayout.PropertyField(globalWeightModeProp, new GUIContent("Global Weight Mode", "How GlobalWeight attenuates the animator. Init: blend the pose toward the captured initial pose (clean neutral). Bind: scale the animation output weight against the Animator bind pose."));

            bool capturesPose = serializedObject.FindProperty("captureInitialPoseOnAwake").boolValue || serializedObject.FindProperty("captureFromDefaultStateEnd").boolValue;
            if (globalWeightModeProp.enumValueIndex == (int)HonamiGlobalWeightMode.Init && !capturesPose)
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

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fixedHeight = 35
            };

            if (GUILayout.Button("Open Graph Editor", buttonStyle))
            {
                var controllerProp = serializedObject.FindProperty("controller");
                if (controllerProp.objectReferenceValue != null)
                {
                    HonamiGraphWindow.OpenWindow();
                    HonamiGraphWindow.LoadController((HonamiRuntimeController)controllerProp.objectReferenceValue);
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
            GUIStyle debugHeader = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.9f, 0.6f, 0.2f) } };
            GUILayout.Label(" Runtime Debug", debugHeader);
            EditorGUILayout.Space(3);

            var controller = animator.CurrentController;
            if (controller != null && controller.ActiveLayers.Count > 0)
            {
                for (int i = 0; i < controller.ActiveLayers.Count; i++)
                {
                    int currentStateIdx = animator.GetActiveStateIndex(i);
                    string stateName = "None";

                    if (currentStateIdx >= 0 && currentStateIdx < controller.ActiveStates.Count)
                    {
                        var state = controller.ActiveStates[currentStateIdx];
                        stateName = state != null ? state.stateName : "Invalid";
                    }

                    EditorGUILayout.LabelField($"Layer {i} [{controller.ActiveLayers[i].name}]:", stateName, EditorStyles.boldLabel);

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
