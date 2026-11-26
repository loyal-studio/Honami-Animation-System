using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using UBlendTree = UnityEditor.Animations.BlendTree;

namespace HonamiAnimationSystem.Editor
{
    public sealed class AnimatorToHonamiConverterWindow : EditorWindow
    {
        private AnimatorController sourceAnimator;
        private bool convertParameters = true;
        private bool convertLayers = true;
        private bool convertStates = true;
        private bool convertTransitions = true;

        [MenuItem("Window/Honami/Tools/Honami Animator Converter")]
        public static void ShowWindow()
        {
            GetWindow<AnimatorToHonamiConverterWindow>("Honami Converter");
        }

        [MenuItem("Assets/Convert to Honami Controller", true)]
        private static bool ValidateContextConvert()
        {
            return Selection.activeObject is AnimatorController;
        }

        [MenuItem("Assets/Convert to Honami Controller")]
        private static void ContextConvert()
        {
            var controller = Selection.activeObject as AnimatorController;
            if (controller == null) return;

            var window = GetWindow<AnimatorToHonamiConverterWindow>("Honami Converter");
            window.sourceAnimator = controller;
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Animator to Honami Converter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            sourceAnimator = (AnimatorController)EditorGUILayout.ObjectField("Source Animator", sourceAnimator, typeof(AnimatorController), false);

            EditorGUILayout.Space();
            GUILayout.Label("Conversion Settings", EditorStyles.boldLabel);
            convertParameters = EditorGUILayout.Toggle("Convert Parameters", convertParameters);
            convertLayers = EditorGUILayout.Toggle("Convert Layers", convertLayers);
            convertStates = EditorGUILayout.Toggle("Convert States", convertStates);
            convertTransitions = EditorGUILayout.Toggle("Convert Transitions", convertTransitions);

            EditorGUILayout.Space();

            GUI.enabled = sourceAnimator != null;
            if (GUILayout.Button("Convert to Honami Controller", GUILayout.Height(30)))
            {
                ConvertAnimator(sourceAnimator);
            }
            GUI.enabled = true;
        }

        private void ConvertAnimator(AnimatorController source)
        {
            string defaultDir = "Assets";
            if (source != null)
            {
                string sourcePath = AssetDatabase.GetAssetPath(source);
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    defaultDir = System.IO.Path.GetDirectoryName(sourcePath);
                }
            }

            string absPath = EditorUtility.SaveFilePanel("Save Honami Controller", defaultDir, source.name + "_Honami", "asset");
            if (string.IsNullOrEmpty(absPath)) return;

            string path = FileUtil.GetProjectRelativePath(absPath);
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Error", "Please save the asset inside the project's Assets folder.", "OK");
                return;
            }


            HonamiController target = ScriptableObject.CreateInstance<HonamiController>();
            AssetDatabase.CreateAsset(target, path);

            if (convertParameters) ConvertParameters(source, target);

            // Dictionaries for mapping Animator states to Honami elements
            Dictionary<AnimatorState, HonamiState> stateMap = new();
            Dictionary<AnimatorStateMachine, HonamiState> anyStateMap = new();
            Dictionary<AnimatorStateMachine, HonamiState> exitStateMap = new();
            Dictionary<AnimatorStateMachine, HonamiState> stateMachineDefaultStates = new();

            if (convertLayers || convertStates)
            {
                ConvertLayersAndStates(source, target, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);
            }

            if (convertTransitions && convertStates)
            {
                ConvertTransitions(source, target, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", "Conversion to Honami Controller complete!\nNote: Some elements (e.g. 2D BlendTrees, Behaviours) may require manual adjustments.", "OK");
        }

        private void ConvertParameters(AnimatorController source, HonamiController target)
        {
            target.parameters.Clear();
            foreach (var p in source.parameters)
            {
                HonamiParameter newParam = new() { name = p.name };
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Float:
                        newParam.type = HonamiParameterType.Float;
                        newParam.defaultFloat = p.defaultFloat;
                        break;
                    case AnimatorControllerParameterType.Int:
                        newParam.type = HonamiParameterType.Int;
                        newParam.defaultInt = p.defaultInt;
                        break;
                    case AnimatorControllerParameterType.Bool:
                        newParam.type = HonamiParameterType.Bool;
                        newParam.defaultBool = p.defaultBool;
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        newParam.type = HonamiParameterType.Trigger;
                        break;
                }
                target.parameters.Add(newParam);
            }
        }

        private void ConvertLayersAndStates(AnimatorController source, HonamiController target,
            Dictionary<AnimatorState, HonamiState> stateMap,
            Dictionary<AnimatorStateMachine, HonamiState> anyStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> exitStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> stateMachineDefaultStates)
        {
            target.layers.Clear();
            for (int i = 0; i < source.layers.Length; i++)
            {
                var l = source.layers[i];
                if (convertLayers)
                {
                    HonamiLayer newLayer = new() { name = l.name, weight = i == 0 ? 1f : l.defaultWeight };
                    target.layers.Add(newLayer);
                }

                if (convertStates)
                {
                    ConvertStateMachine(l.stateMachine, target, i, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);
                }
            }

            if (target.layers.Count == 0 && convertStates)
            {
                target.layers.Add(new() { name = "Base Layer" });
            }
        }

        private void ConvertStateMachine(AnimatorStateMachine sm, HonamiController target, int layerIndex,
            Dictionary<AnimatorState, HonamiState> stateMap,
            Dictionary<AnimatorStateMachine, HonamiState> anyStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> exitStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> stateMachineDefaultStates)
        {
            // Create Any State
            HonamiState hAnyState = ScriptableObject.CreateInstance<HonamiState>();
            hAnyState.name = "Any State (" + sm.name + ")";
            hAnyState.stateName = hAnyState.name;
            hAnyState.layerIndex = layerIndex;
            hAnyState.editorPosition = new Vector2(sm.anyStatePosition.x, sm.anyStatePosition.y);

            HonamiAnyStateNode anyNode = ScriptableObject.CreateInstance<HonamiAnyStateNode>();
            anyNode.name = hAnyState.name + "_Node";
            AssetDatabase.AddObjectToAsset(anyNode, target);
            hAnyState.node = anyNode;
            AssetDatabase.AddObjectToAsset(hAnyState, target);
            target.states.Add(hAnyState);
            anyStateMap[sm] = hAnyState;

            // Create Exit State
            HonamiState hExitState = ScriptableObject.CreateInstance<HonamiState>();
            hExitState.name = "Exit (" + sm.name + ")";
            hExitState.stateName = hExitState.name;
            hExitState.layerIndex = layerIndex;
            hExitState.editorPosition = new Vector2(sm.exitPosition.x, sm.exitPosition.y);

            HonamiExitNode exitNode = ScriptableObject.CreateInstance<HonamiExitNode>();
            exitNode.name = hExitState.name + "_Node";
            AssetDatabase.AddObjectToAsset(exitNode, target);
            hExitState.node = exitNode;
            AssetDatabase.AddObjectToAsset(hExitState, target);
            target.states.Add(hExitState);
            exitStateMap[sm] = hExitState;

            foreach (var childState in sm.states)
            {
                AnimatorState aState = childState.state;
                HonamiState hState = ScriptableObject.CreateInstance<HonamiState>();
                hState.name = aState.name;
                hState.stateName = aState.name;
                hState.layerIndex = layerIndex;
                hState.speed = aState.speed;
                hState.editorPosition = new Vector2(childState.position.x, childState.position.y);
                hState.isDefaultState = (sm.defaultState == aState);

                if (hState.isDefaultState)
                    stateMachineDefaultStates[sm] = hState;

                AssetDatabase.AddObjectToAsset(hState, target);

                if (aState.motion is AnimationClip clip)
                {
                    HonamiAnimationNode animNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
                    animNode.name = aState.name + "_Node";
                    animNode.clip = clip;
                    AssetDatabase.AddObjectToAsset(animNode, target);
                    hState.node = animNode;
                    hState.loop = clip.isLooping;
                }
                else if (aState.motion is UBlendTree blendTree)
                {
                    hState.node = IsTwoDimensional(blendTree.blendType)
                        ? ConvertToBlendSpace(blendTree, aState.name, target)
                        : ConvertToBlendTree(blendTree, aState.name, target);
                    hState.loop = true;
                }
                else if (aState.motion == null)
                {
                    HonamiAnimationNode animNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
                    animNode.name = aState.name + "_EmptyNode";
                    AssetDatabase.AddObjectToAsset(animNode, target);
                    hState.node = animNode;
                    hState.loop = false;
                }

                target.states.Add(hState);
                stateMap[aState] = hState;
            }

            foreach (var childSm in sm.stateMachines)
            {
                ConvertStateMachine(childSm.stateMachine, target, layerIndex, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);

                // Inherit default state for the top-level parent if necessary
                if (sm.defaultState == null && stateMachineDefaultStates.TryGetValue(childSm.stateMachine, out HonamiState childDefault))
                {
                    stateMachineDefaultStates[sm] = childDefault;
                }
            }
        }

        private static bool IsTwoDimensional(BlendTreeType type)
            => type == BlendTreeType.SimpleDirectional2D
               || type == BlendTreeType.FreeformDirectional2D
               || type == BlendTreeType.FreeformCartesian2D;

        private HonamiNodeBase ConvertToBlendTree(UBlendTree tree, string stateName, HonamiController target)
        {
            if (tree.blendType == BlendTreeType.Direct)
            {
                Debug.LogWarning($"[Honami] Blend tree on state '{stateName}' uses Direct blending, which Honami does not support. Converted as a 1D blend tree.");
            }

            HonamiBlendTreeNode node = ScriptableObject.CreateInstance<HonamiBlendTreeNode>();
            node.name = stateName + "_Node";
            node.blendParameter = tree.blendParameter;

            ExtractBlendTreeMotionsFlat(tree, node.blendMotions);

            AssetDatabase.AddObjectToAsset(node, target);
            return node;
        }

        private HonamiNodeBase ConvertToBlendSpace(UBlendTree tree, string stateName, HonamiController target)
        {
            HonamiBlendSpaceNode node = ScriptableObject.CreateInstance<HonamiBlendSpaceNode>();
            node.name = stateName + "_Node";
            node.spaceType = tree.blendType switch
            {
                BlendTreeType.SimpleDirectional2D => HonamiBlendSpaceType.SimpleDirectional,
                BlendTreeType.FreeformCartesian2D => HonamiBlendSpaceType.FreeformCartesian,
                _ => HonamiBlendSpaceType.FreeformDirectional
            };

            node.blendParameters.Add(tree.blendParameter);
            node.blendParameters.Add(tree.blendParameterY);

            ExtractBlendSpaceMotionsFlat(tree, node.blendMotions);
            node.OnValidateNode();

            AssetDatabase.AddObjectToAsset(node, target);
            return node;
        }

        private void ExtractBlendTreeMotionsFlat(UBlendTree tree, List<HonamiBlendTreeMotion> motions)
        {
            foreach (var child in tree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    HonamiBlendTreeMotion btm = new();
                    btm.clip = clip;
                    btm.threshold = child.threshold;
                    btm.speed = child.timeScale;
                    btm.mirror = child.mirror;
                    motions.Add(btm);
                }
                else if (child.motion is UBlendTree nestedTree)
                {
                    ExtractBlendTreeMotionsFlat(nestedTree, motions);
                }
            }
        }

        private void ExtractBlendSpaceMotionsFlat(UBlendTree tree, List<HonamiBlendSpaceMotion> motions)
        {
            foreach (var child in tree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    motions.Add(new HonamiBlendSpaceMotion
                    {
                        clip = clip,
                        position = new[] { child.position.x, child.position.y },
                        speed = child.timeScale,
                        mirror = child.mirror
                    });
                }
                else if (child.motion is UBlendTree nestedTree)
                {
                    ExtractBlendSpaceMotionsFlat(nestedTree, motions);
                }
            }
        }

        private void ConvertTransitions(AnimatorController source, HonamiController target,
            Dictionary<AnimatorState, HonamiState> stateMap,
            Dictionary<AnimatorStateMachine, HonamiState> anyStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> exitStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> stateMachineDefaultStates)
        {
            // Convert state transitions
            foreach (var smEntry in stateMap)
            {
                AnimatorState aState = smEntry.Key;
                HonamiState hState = smEntry.Value;

                foreach (var trans in aState.transitions)
                {
                    ProcessTransition(trans, aState, hState, stateMap, exitStateMap, stateMachineDefaultStates);
                }
            }

            // Convert AnyState transitions
            foreach (var layer in source.layers)
            {
                ProcessStateMachineTransitions(layer.stateMachine, sourceAnimator, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);
            }
        }

        private void ProcessStateMachineTransitions(AnimatorStateMachine sm, AnimatorController ac,
            Dictionary<AnimatorState, HonamiState> stateMap,
            Dictionary<AnimatorStateMachine, HonamiState> anyStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> exitStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> stateMachineDefaultStates)
        {
            if (anyStateMap.TryGetValue(sm, out HonamiState hAnyState))
            {
                foreach (var trans in sm.anyStateTransitions)
                {
                    ProcessTransition(trans, null, hAnyState, stateMap, exitStateMap, stateMachineDefaultStates);
                }
            }

            foreach (var childSm in sm.stateMachines)
            {
                ProcessStateMachineTransitions(childSm.stateMachine, ac, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);
            }
        }

        private void ProcessTransition(AnimatorStateTransition trans, AnimatorState sourceState, HonamiState sourceHState,
            Dictionary<AnimatorState, HonamiState> stateMap,
            Dictionary<AnimatorStateMachine, HonamiState> exitStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> stateMachineDefaultStates)
        {
            HonamiState destHState = null;

            if (trans.isExit)
            {
                AnimatorStateMachine parentSm = FindParentStateMachine(sourceAnimator, sourceState);
                if (parentSm != null && exitStateMap.TryGetValue(parentSm, out HonamiState exitState))
                {
                    destHState = exitState;
                }
            }
            else if (trans.destinationState != null)
            {
                stateMap.TryGetValue(trans.destinationState, out destHState);
            }
            else if (trans.destinationStateMachine != null)
            {
                stateMachineDefaultStates.TryGetValue(trans.destinationStateMachine, out destHState);
            }

            if (destHState == null) return;

            HonamiTransition hTrans = new();
            hTrans.targetStateGuid = destHState.guid;
            hTrans.duration = trans.duration;
            hTrans.hasExitTime = trans.hasExitTime;
            hTrans.exitTime = trans.exitTime;
            hTrans.destinationStartTime = trans.offset;
            hTrans.canInterruptTransition = (trans.interruptionSource != TransitionInterruptionSource.None);
            hTrans.canTransitionToSelf = trans.canTransitionToSelf;

            foreach (var cond in trans.conditions)
            {
                HonamiCondition hCond = new();
                hCond.parameter = cond.parameter;
                hCond.threshold = cond.threshold;
                switch (cond.mode)
                {
                    case AnimatorConditionMode.If: hCond.mode = HonamiConditionMode.If; break;
                    case AnimatorConditionMode.IfNot: hCond.mode = HonamiConditionMode.IfNot; break;
                    case AnimatorConditionMode.Greater: hCond.mode = HonamiConditionMode.Greater; break;
                    case AnimatorConditionMode.Less: hCond.mode = HonamiConditionMode.Less; break;
                    case AnimatorConditionMode.Equals: hCond.mode = HonamiConditionMode.Equals; break;
                    case AnimatorConditionMode.NotEqual: hCond.mode = HonamiConditionMode.NotEqual; break;
                }
                hTrans.conditions.Add(hCond);
            }

            sourceHState.transitions.Add(hTrans);
        }

        private AnimatorStateMachine FindParentStateMachine(AnimatorController ac, AnimatorState state)
        {
            if (state == null) return null;
            foreach (var layer in ac.layers)
            {
                var sm = FindParentStateMachineRecursive(layer.stateMachine, state);
                if (sm != null) return sm;
            }
            return null;
        }

        private AnimatorStateMachine FindParentStateMachineRecursive(AnimatorStateMachine sm, AnimatorState state)
        {
            foreach (var child in sm.states)
            {
                if (child.state == state) return sm;
            }
            foreach (var childSm in sm.stateMachines)
            {
                var found = FindParentStateMachineRecursive(childSm.stateMachine, state);
                if (found != null) return found;
            }
            return null;
        }
    }
}
