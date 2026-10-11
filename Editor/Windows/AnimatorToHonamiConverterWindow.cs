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

        // Messages collected during the last conversion (blend tree fallbacks, dropped features).
        internal readonly List<string> conversionWarnings = new();
        // Informational notes (not counted as problems in the result dialog).
        internal readonly List<string> conversionInfo = new();

        // "layer 'X', sub-state machine 'A/B'" for the state machine currently being converted.
        private string currentContext = "";

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

            RunConversion(source, path);

            string message = "Conversion to Honami Controller complete!\nNote: Some elements (e.g. Cartesian/Direct BlendTrees, Behaviours) may require manual adjustments.";
            if (conversionWarnings.Count > 0)
            {
                message += "\n\n" + conversionWarnings.Count + " blend tree warning(s) were written to the Console.";
            }
            EditorUtility.DisplayDialog("Success", message, "OK");
        }

        /// <summary>
        /// Converts <paramref name="source"/> into a new HonamiController asset at <paramref name="path"/>.
        /// Blend tree notes are collected in <see cref="conversionWarnings"/> and logged as warnings.
        /// </summary>
        internal HonamiController RunConversion(AnimatorController source, string path)
        {
            conversionWarnings.Clear();
            conversionInfo.Clear();

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
            return target;
        }

        private void AddInfo(string message)
        {
            conversionInfo.Add(message);
            Debug.Log("[Honami Converter] " + message);
        }

        private void AddWarning(string message)
        {
            conversionWarnings.Add(message);
            Debug.LogWarning("[Honami Converter] " + message);
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
                    currentContext = $"layer '{l.name}'";
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
                    HonamiBlendTreeNode btNode = ScriptableObject.CreateInstance<HonamiBlendTreeNode>();
                    btNode.name = aState.name + "_Node";
                    ConvertBlendTree(blendTree, aState, btNode);

                    AssetDatabase.AddObjectToAsset(btNode, target);
                    hState.node = btNode;
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

            string stateMachineContext = currentContext;
            foreach (var childSm in sm.stateMachines)
            {
                currentContext = stateMachineContext + $", sub-state machine '{childSm.stateMachine.name}'";
                ConvertStateMachine(childSm.stateMachine, target, layerIndex, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);

                // Inherit default state for the top-level parent if necessary
                if (sm.defaultState == null && stateMachineDefaultStates.TryGetValue(childSm.stateMachine, out HonamiState childDefault))
                {
                    stateMachineDefaultStates[sm] = childDefault;
                }
            }
        }

        private void ConvertBlendTree(UBlendTree tree, AnimatorState state, HonamiBlendTreeNode node)
        {
            node.blendParameter = tree.blendParameter;
            string label = $"Blend tree '{tree.name}' in state '{state.name}' ({currentContext})";

            switch (tree.blendType)
            {
                case BlendTreeType.FreeformDirectional2D:
                case BlendTreeType.SimpleDirectional2D:
                    if (TryConvertDirectional2D(tree, state, label, node)) return;
                    break;
                case BlendTreeType.FreeformCartesian2D:
                case BlendTreeType.Direct:
                    AddWarning($"{label} is {tree.blendType}, which Honami does not support. "
                        + "It was flattened into a 1D motion list; re-author it as 1D trees, states or layers.");
                    break;
            }

            ExtractBlendTreeMotionsFlat(tree, node.blendMotions, label);
        }

        // Returns false when the tree cannot be represented as a flat Honami 2D tree (caller falls back to flattening).
        private bool TryConvertDirectional2D(UBlendTree tree, AnimatorState state, string label, HonamiBlendTreeNode node)
        {
            var children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion is UBlendTree)
                {
                    AddWarning($"{label} is {tree.blendType} with a nested blend tree, which Honami's flat 2D tree cannot hold. "
                        + "It was flattened into a 1D motion list; re-author it manually.");
                    return false;
                }
            }

            node.blendType = HonamiBlendTreeType.FreeformDirectional2D;
            node.blendParameterY = tree.blendParameterY;
            // Unity blend trees do not damp their parameters; match that instead of Honami's 0.1 default.
            node.blendParameterDampTime = 0f;
            node.blendParameterDampTimeY = 0f;

            if (float.IsNaN(state.speed) || float.IsInfinity(state.speed) || state.speed < 0f)
            {
                AddWarning($"{label} is in a state with speed {state.speed}; Honami rejects negative or non-finite state speed for 2D trees, so it will play as an empty mixer until fixed.");
            }
            if (state.mirror || state.mirrorParameterActive)
            {
                AddWarning($"{label} is in a mirrored state (mirror / mirror parameter), which Honami's 2D tree does not support; mirroring was dropped.");
            }
            if (state.speedParameterActive)
            {
                AddWarning($"{label} is in a state with a speed multiplier parameter ('{state.speedParameter}'), which was dropped.");
            }
            if (state.cycleOffset != 0f || state.cycleOffsetParameterActive)
            {
                AddWarning($"{label} is in a state with a state-level cycle offset, which was dropped.");
            }

            bool droppedMirror = false;
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (!(child.motion is AnimationClip clip))
                {
                    AddWarning($"{label} has an empty motion slot at index {i}; it was skipped. "
                        + "Unity still weights a null child, so skipping it changes the blend layout (a null child at the origin leaves no origin motion).");
                    continue;
                }

                if (float.IsNaN(child.timeScale) || float.IsInfinity(child.timeScale) || child.timeScale <= 0f)
                {
                    AddWarning($"{label}: child {i} ('{clip.name}') has timeScale {child.timeScale}. Unity reverse playback (timeScale <= 0) has no Honami equivalent; the tree will not evaluate until it is positive.");
                }

                node.blendMotions.Add(new HonamiBlendTreeMotion
                {
                    clip = clip,
                    position = child.position,
                    threshold = child.threshold,
                    speed = child.timeScale,
                    cycleOffset = HonamiBlendTreeNode.WrapCycleOffset(child.cycleOffset),
                    // Honami's flat 2D profile rejects mirrored motions, so the flag is not carried over.
                    mirror = false
                });
                droppedMirror |= child.mirror;
            }

            if (droppedMirror)
            {
                AddWarning($"{label} uses mirrored motions, which Honami's 2D tree does not support; mirroring was dropped.");
            }

            if (tree.blendType == BlendTreeType.SimpleDirectional2D)
            {
                AddInfo($"{label} is SimpleDirectional2D; it was converted to FreeformDirectional2D. Honami uses its own directional interpolation, so intermediate blends may differ slightly from Unity.");
            }

            if (!node.TryGetDirectionalSolver(out _, out string error))
            {
                AddWarning($"{label} was converted but will not evaluate until fixed: {error} "
                    + "(Honami needs one motion at (0,0), distinct positions surrounding the origin, positive speeds, positive clip lengths and at most "
                    + HonamiDirectionalBlendSolver.MaximumMotionCount + " motions).");
            }

            return true;
        }

        private void ExtractBlendTreeMotionsFlat(UBlendTree tree, List<HonamiBlendTreeMotion> motions, string label)
        {
            foreach (var child in tree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    HonamiBlendTreeMotion btm = new();
                    btm.clip = clip;
                    btm.threshold = child.threshold;
                    btm.speed = child.timeScale;
                    motions.Add(btm);
                }
                else if (child.motion is UBlendTree nestedTree)
                {
                    bool is2D = nestedTree.blendType != BlendTreeType.Simple1D;
                    AddWarning($"{label} contains a nested {nestedTree.blendType} blend tree '{nestedTree.name}' that was flattened into the parent's motion list; Honami has no nested blend trees."
                        + (is2D ? " Thresholds of the 2D tree's motions are meaningless in a 1D list." : ""));
                    ExtractBlendTreeMotionsFlat(nestedTree, motions, label);
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
                    ProcessTransition(source, trans, aState, hState, stateMap, exitStateMap, stateMachineDefaultStates);
                }
            }

            // Convert AnyState transitions
            foreach (var layer in source.layers)
            {
                ProcessStateMachineTransitions(layer.stateMachine, source, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);
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
                    ProcessTransition(ac, trans, null, hAnyState, stateMap, exitStateMap, stateMachineDefaultStates);
                }
            }

            foreach (var childSm in sm.stateMachines)
            {
                ProcessStateMachineTransitions(childSm.stateMachine, ac, stateMap, anyStateMap, exitStateMap, stateMachineDefaultStates);
            }
        }

        private void ProcessTransition(AnimatorController ac, AnimatorStateTransition trans, AnimatorState sourceState, HonamiState sourceHState,
            Dictionary<AnimatorState, HonamiState> stateMap,
            Dictionary<AnimatorStateMachine, HonamiState> exitStateMap,
            Dictionary<AnimatorStateMachine, HonamiState> stateMachineDefaultStates)
        {
            HonamiState destHState = null;

            if (trans.isExit)
            {
                AnimatorStateMachine parentSm = FindParentStateMachine(ac, sourceState);
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
