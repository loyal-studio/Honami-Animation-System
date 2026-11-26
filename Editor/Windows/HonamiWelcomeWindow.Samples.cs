using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Documentation;
using HonamiAnimationSystem.Editor.Timeline;
using HonamiAnimationSystem.Editor.Windows;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed partial class HonamiWelcomeWindow
    {
        private static void CreateControllerWithExampleGraph()
        {
            var folder = "Assets";
            if (Selection.activeObject != null)
            {
                folder = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!Directory.Exists(folder)) folder = Path.GetDirectoryName(folder);
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/New Honami Controller.asset");
            var controller = ScriptableObject.CreateInstance<HonamiController>();

            controller.parameters = new List<HonamiParameter>
            {
                new HonamiParameter
                {
                    name = "Speed",
                    type = HonamiParameterType.Float,
                    defaultFloat = 0f
                }
            };

            var idleNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
            idleNode.name = "IdleAnimationNode";

            var walkNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
            walkNode.name = "WalkAnimationNode";

            var idleState = ScriptableObject.CreateInstance<HonamiState>();
            idleState.stateName = "Idle";
            idleState.name = "Idle";
            idleState.layerIndex = 0;
            idleState.isDefaultState = true;
            idleState.loop = true;
            idleState.speed = 1f;
            idleState.node = idleNode;
            idleState.editorPosition = new Vector2(250, 120);

            var walkState = ScriptableObject.CreateInstance<HonamiState>();
            walkState.stateName = "Walk";
            walkState.name = "Walk";
            walkState.layerIndex = 0;
            walkState.isDefaultState = false;
            walkState.loop = true;
            walkState.speed = 1f;
            walkState.node = walkNode;
            walkState.editorPosition = new Vector2(550, 120);

            idleState.transitions = new List<HonamiTransition>
            {
                new HonamiTransition
                {
                    targetStateGuid = walkState.guid,
                    duration = 0.2f,
                    conditions = new List<HonamiCondition>
                    {
                        new HonamiCondition
                        {
                            parameter = "Speed",
                            mode = HonamiConditionMode.Greater,
                            threshold = 0.1f
                        }
                    }
                }
            };

            walkState.transitions = new List<HonamiTransition>
            {
                new HonamiTransition
                {
                    targetStateGuid = idleState.guid,
                    duration = 0.2f,
                    conditions = new List<HonamiCondition>
                    {
                        new HonamiCondition
                        {
                            parameter = "Speed",
                            mode = HonamiConditionMode.Less,
                            threshold = 0.1f
                        }
                    }
                }
            };

            controller.states = new List<HonamiState> { idleState, walkState };

            AssetDatabase.CreateAsset(controller, assetPath);
            AssetDatabase.AddObjectToAsset(idleNode, controller);
            AssetDatabase.AddObjectToAsset(walkNode, controller);
            AssetDatabase.AddObjectToAsset(idleState, controller);
            AssetDatabase.AddObjectToAsset(walkState, controller);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = controller;
        }

        private static void CreateSampleScript()
        {
            var folder = "Assets";
            if (Selection.activeObject != null)
            {
                folder = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!Directory.Exists(folder)) folder = Path.GetDirectoryName(folder);
            }

            var scriptPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/HonamiCharacterController.cs");

            const string template =
@"using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

[RequireComponent(typeof(HonamiAnimator))]
public sealed class HonamiCharacterController : MonoBehaviour
{
    private HonamiAnimator _animator;

    private static readonly int SpeedHash = HonamiAnimator.StringToHash(""Speed"");

    private void Awake()
    {
        TryGetComponent(out _animator);
    }

    private void Update()
    {
        float speed = Mathf.PingPong(Time.time, 1f);
        _animator.SetFloat(SpeedHash, speed);
    }
}
";
            File.WriteAllText(scriptPath, template);
            AssetDatabase.ImportAsset(scriptPath);
            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = scriptAsset;
        }
    }
}
