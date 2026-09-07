using HonamiAnimationSystem.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    public sealed class HonamiSharedAnimatorInspector
    {
        public SerializedProperty Startup;
        public SerializedProperty UpdateMode;
        public SerializedProperty TimeScale;
        public SerializedProperty FpsCap;
        public SerializedProperty TargetFPS;
        public SerializedProperty FpsCapInterpolate;
        public SerializedProperty ApplyRootMotion;
        public SerializedProperty CullingMode;
        public SerializedProperty CaptureInitialPoseOnAwake;
        public SerializedProperty RestoreInitialPoseWhenIdle;
        public SerializedProperty IncludeRootTransformInInitialPose;
        public SerializedProperty GlobalWeightMode;
        public SerializedProperty PreventLinking;
        public SerializedProperty LinkingTag;

        public static readonly GUIContent StartupContent = new GUIContent("Startup Action", "When should the animation start playing?");
        public static readonly GUIContent UpdateModeContent = new GUIContent("Update Mode", "How time drives the animation graph.");
        public static readonly GUIContent TimeScaleContent = new GUIContent("Time Scale", "Multiplier for animation speed.");
        public static readonly GUIContent FpsCapContent = new GUIContent("FPS Cap", "Limit the animation update rate to a fixed number of frames per second.");
        public static readonly GUIContent TargetFpsContent = new GUIContent("Target FPS", "Animation graph ticks at this rate. Lower values reduce CPU cost but increase motion choppiness.");
        public static readonly GUIContent FpsCapInterpolateContent = new GUIContent("Interpolate", "Lerp bone transforms between capped ticks each game frame for smooth motion at low target FPS.");
        public static readonly GUIContent ApplyRootMotionContent = new GUIContent("Apply Root Motion", "Should root motion be applied from the animation?");
        public static readonly GUIContent CullingModeContent = new GUIContent("Culling Mode", "Controls how the Unity Animator updates when off-screen.");
        public static readonly GUIContent CaptureOnAwakeContent = new GUIContent("Capture On Awake", "Capture the hierarchy pose before Honami starts evaluating.");
        public static readonly GUIContent RestoreWhenIdleContent = new GUIContent("Restore When Idle", "Restore the captured initial pose whenever nothing is playing.");
        public static readonly GUIContent IncludeRootTransformContent = new GUIContent("Include Root Transform", "Include this GameObject's transform in the captured pose. Leave disabled for character roots.");
        public static readonly GUIContent GlobalWeightModeContent = new GUIContent("Global Weight Mode", "How GlobalWeight attenuates the animator. Init: blend the pose toward the captured initial pose (clean neutral). Bind: scale the animation output weight against the Animator bind pose.");
        public static readonly GUIContent PreventLinkingContent = new GUIContent("Prevent Linking", "Keep this animator out of every Linked Animator brain.");
        public static readonly GUIContent LinkingTagContent = new GUIContent("Linking Tag", "Tag used by Linked Animator broadcasts that target a subset of animators.");

        public void Bind(SerializedObject serializedObject)
        {
            Startup = serializedObject.FindProperty("startup");
            UpdateMode = serializedObject.FindProperty("updateMode");
            TimeScale = serializedObject.FindProperty("timeScale");
            FpsCap = serializedObject.FindProperty("fpsCap");
            TargetFPS = serializedObject.FindProperty("targetFPS");
            FpsCapInterpolate = serializedObject.FindProperty("fpsCapInterpolate");
            ApplyRootMotion = serializedObject.FindProperty("applyRootMotion");
            CullingMode = serializedObject.FindProperty("cullingMode");
            CaptureInitialPoseOnAwake = serializedObject.FindProperty("captureInitialPoseOnAwake");
            RestoreInitialPoseWhenIdle = serializedObject.FindProperty("restoreInitialPoseWhenIdle");
            IncludeRootTransformInInitialPose = serializedObject.FindProperty("includeRootTransformInInitialPose");
            GlobalWeightMode = serializedObject.FindProperty("globalWeightMode");
            PreventLinking = serializedObject.FindProperty("preventLinking");
            LinkingTag = serializedObject.FindProperty("linkingTag");
        }

        public void DrawPlaybackSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Playback Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(Startup, StartupContent);
            EditorGUILayout.PropertyField(UpdateMode, UpdateModeContent);

            EditorGUILayout.Space(3);
            EditorGUILayout.Slider(TimeScale, 0f, 10f, TimeScaleContent);

            if (TimeScale.floatValue == 0f)
            {
                EditorGUILayout.HelpBox("Time Scale is 0. Animation is paused.", MessageType.Info);
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(FpsCap, FpsCapContent);

            if (FpsCap.boolValue)
            {
                EditorGUILayout.IntSlider(TargetFPS, 1, 120, TargetFpsContent);
                EditorGUILayout.PropertyField(FpsCapInterpolate, FpsCapInterpolateContent);
                EditorGUILayout.HelpBox($"Animation updates at {TargetFPS.intValue} FPS instead of the game's frame rate.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        public void DrawAnimatorSync()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Animator Synchronization", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(ApplyRootMotion, ApplyRootMotionContent);
            EditorGUILayout.PropertyField(CullingMode, CullingModeContent);

            EditorGUILayout.EndVertical();
        }

        public void DrawLinkedSystem()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Linked System", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(PreventLinking, PreventLinkingContent);
            EditorGUILayout.PropertyField(LinkingTag, LinkingTagContent);

            EditorGUILayout.EndVertical();
        }

        public void DrawGlobalWeightMode(bool capturesPose)
        {
            EditorGUILayout.PropertyField(GlobalWeightMode, GlobalWeightModeContent);

            if (GlobalWeightMode.enumValueIndex == (int)HonamiGlobalWeightMode.Init && !capturesPose)
            {
                EditorGUILayout.HelpBox("Global Weight Mode is Init but no initial pose is captured. GlobalWeight < 1 will have no effect. Enable Capture On Awake, or switch to Bind.", MessageType.Warning);
            }
        }

        public void DrawPoseButtons(HonamiAnimatorBase animator)
        {
            if (!Application.isPlaying) return;

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
    }
}
