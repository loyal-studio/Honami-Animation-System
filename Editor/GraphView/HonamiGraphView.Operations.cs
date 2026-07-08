using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public partial class HonamiGraphView
    {
        public void CreateNewStateNodeOfType(Vector2 position, Type nodeType)
        {
            if (_controller == null) return;

            if (nodeType == typeof(HonamiPortalEntranceNode) || nodeType == typeof(HonamiPortalExitNode))
            {
                var edgeAtPos = GetEdgeAtPosition(position);
                if (edgeAtPos != null)
                {
                    SplitTransitionWithPortals(edgeAtPos, position);
                    return;
                }
            }

            var newState = ScriptableObject.CreateInstance<HonamiState>();
            newState.name = $"State {_controller.states.Count}";
            newState.stateName = newState.name;
            newState.editorPosition = position;
            newState.layerIndex = currentLayerIndex;

            var newNode = (HonamiNodeBase)ScriptableObject.CreateInstance(nodeType);
            newNode.name = $"{newState.stateName}_{nodeType.Name}";
            newState.node = newNode;

            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                var ovCtrl = (HonamiOverrideController)_runtimeController;
                ovCtrl.additionalStates.Add(newState);
                ovCtrl.ClearCaches();
            }
            else
            {
                _controller.states.Add(newState);
                _controller.ClearEffectiveStateCache();
            }

            HonamiAnimationSystem.Editor.Core.HonamiEditorController.EnsureUniqueStateName(_runtimeController, newState);

            var targetCtrl = _runtimeController != null && _runtimeController.IsOverride ? (UnityEngine.Object)_runtimeController : (UnityEngine.Object)_controller;
            AssetDatabase.AddObjectToAsset(newState, targetCtrl);
            AssetDatabase.AddObjectToAsset(newNode, targetCtrl);
            Undo.RegisterCreatedObjectUndo(newState, "Create State");
            Undo.RegisterCreatedObjectUndo(newNode, "Create Node");

            EditorUtility.SetDirty(targetCtrl);
            DeferredSave();

            CreateNodeView(newState, false);
        }

        public void ChangeNodeType(HonamiNode node, Type newType)
        {
            if (node?.State == null || _controller == null) return;

            Undo.SetCurrentGroupName("Change Node Type");
            int groupIdx = Undo.GetCurrentGroup();

            HonamiState state = node.State;
            string stateGuid = state.guid;
            HonamiNodeBase oldNode = state.node;

            HonamiNodeBase newNode = (HonamiNodeBase)ScriptableObject.CreateInstance(newType);
            newNode.name = $"{state.stateName}_{newType.Name}";

            AnimationClip clip = null;
            if (oldNode != null)
            {
                switch (oldNode)
                {
                    case HonamiAnimationNode an:
                        clip = an.clip;
                        break;
                    case HonamiRandomAnimationNode rn when rn.randomClips.Count > 0:
                        clip = rn.randomClips[0].clip;
                        break;
                    case HonamiBlendTreeNode bn when bn.blendMotions.Count > 0:
                        clip = bn.blendMotions[0].clip;
                        break;
                    case HonamiSequencerNode sn when sn.sequencedClips.Count > 0:
                        clip = sn.sequencedClips[0].clip;
                        break;
                }
            }

            if (clip != null)
            {
                switch (newNode)
                {
                    case HonamiAnimationNode dan:
                        dan.clip = clip;
                        break;
                    case HonamiRandomAnimationNode drn:
                        drn.randomClips.Add(new HonamiRandomAnimationClip { clip = clip });
                        break;
                    case HonamiBlendTreeNode dbn:
                        dbn.blendMotions.Add(new HonamiBlendTreeMotion { clip = clip });
                        break;
                    case HonamiSequencerNode dsn:
                        dsn.sequencedClips.Add(new HonamiSequencedAnimationClip { clip = clip });
                        break;
                }
            }

            var targetCtrl = _runtimeController != null && _runtimeController.IsOverride ? (UnityEngine.Object)_runtimeController : (UnityEngine.Object)_controller;
            AssetDatabase.AddObjectToAsset(newNode, targetCtrl);
            Undo.RegisterCreatedObjectUndo(newNode, "Change Node Type");

            Undo.RecordObject(state, "Change Node Type");
            state.node = newNode;

            if (oldNode != null)
            {
                Undo.DestroyObjectImmediate(oldNode);
            }

            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(targetCtrl);

            Undo.CollapseUndoOperations(groupIdx);
            DeferredSave();

            PopulateView(_runtimeController, currentLayerIndex);

            var newNodeView = _cachedNodes.FirstOrDefault(n => n.StateGuid == stateGuid);
            if (newNodeView != null)
            {
                AddToSelection(newNodeView);
            }

            HonamiNotificationPanel.ShowGlobal("Node Type Changed", $"Changed to {newType.Name.Replace("Honami", "").Replace("Node", "")}", HonamiNotificationType.Success);
        }

        private HonamiTransitionEdge GetEdgeAtPosition(Vector2 localPos)
        {
            var worldPos = contentViewContainer.LocalToWorld(localPos);
            foreach (var edge in edges)
            {
                if (edge is HonamiTransitionEdge hEdge && hEdge.ContainsPoint(hEdge.WorldToLocal(worldPos)))
                    return hEdge;
            }
            return null;
        }

        private void SplitTransitionWithPortals(HonamiTransitionEdge edge, Vector2 position)
        {
            var sourceNode = edge.output?.node as HonamiNode;
            var targetNode = edge.input?.node as HonamiNode;
            var transition = edge.userData as HonamiTransition;

            if (sourceNode == null || targetNode == null || transition == null) return;

            Vector2 sPos = contentViewContainer.WorldToLocal(sourceNode.worldBound.center);
            Vector2 tPos = contentViewContainer.WorldToLocal(targetNode.worldBound.center);
            Vector2 spawnCenter = position;

            Vector2 direction = (tPos - sPos).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;

            Undo.SetCurrentGroupName("Split Transition with Portals");
            int groupIdx = Undo.GetCurrentGroup();

            Vector2 entrancePos = spawnCenter - direction * 80f;
            Vector2 exitPos = spawnCenter + direction * 80f;
            string pName = "Portal_" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper();

            var entranceState = ScriptableObject.CreateInstance<HonamiState>();
            entranceState.name = "Portal Entrance";
            entranceState.stateName = "Entrance";
            entranceState.layerIndex = currentLayerIndex;
            entranceState.editorPosition = entrancePos;
            var entranceNode = ScriptableObject.CreateInstance<HonamiPortalEntranceNode>();
            entranceNode.name = $"Entrance_{pName}";
            entranceNode.portalName = pName;
            entranceState.node = entranceNode;
            AssetDatabase.AddObjectToAsset(entranceState, _controller);
            AssetDatabase.AddObjectToAsset(entranceNode, _controller);
            Undo.RegisterCreatedObjectUndo(entranceState, "Create Portal Entrance");
            Undo.RegisterCreatedObjectUndo(entranceNode, "Create Portal Entrance Node");

            var exitState = ScriptableObject.CreateInstance<HonamiState>();
            exitState.name = "Portal Exit";
            exitState.stateName = "Exit";
            exitState.layerIndex = currentLayerIndex;
            exitState.editorPosition = exitPos;
            var exitNode = ScriptableObject.CreateInstance<HonamiPortalExitNode>();
            exitNode.name = $"Exit_{pName}";
            exitNode.portalName = pName;
            exitState.node = exitNode;
            AssetDatabase.AddObjectToAsset(exitState, _controller);
            AssetDatabase.AddObjectToAsset(exitNode, _controller);
            Undo.RegisterCreatedObjectUndo(exitState, "Create Portal Exit");
            Undo.RegisterCreatedObjectUndo(exitNode, "Create Portal Exit Node");

            Undo.RecordObject(_controller, "Add Portals to Controller");
            _controller.states.Add(entranceState);
            _controller.states.Add(exitState);

            var exitTransition = new HonamiTransition { targetStateGuid = targetNode.StateGuid };
            exitTransition.duration = transition.duration;
            exitTransition.hasExitTime = transition.hasExitTime;
            exitTransition.exitTime = transition.exitTime;
            exitTransition.useCurve = transition.useCurve;
            exitTransition.curve = transition.curve;
            exitState.transitions.Add(exitTransition);

            Undo.RecordObject(sourceNode.State, "Redirect Transition to Portal");
            transition.targetStateGuid = entranceState.guid;

            EditorUtility.SetDirty(sourceNode.State);
            EditorUtility.SetDirty(entranceState);
            EditorUtility.SetDirty(exitState);
            EditorUtility.SetDirty(_controller);

            Undo.CollapseUndoOperations(groupIdx);
            DeferredSave();

            PopulateView(_controller, currentLayerIndex);
            HonamiNotificationPanel.ShowGlobal("Portal Pair Created", "Transition split at midpoint.", HonamiNotificationType.Success);
        }

        private HonamiNode CreateNodeView(HonamiState state, bool isDefault = false)
        {
            var node = new HonamiNode(state, isDefault, _runtimeController);
            node.SetPosition(new Rect(state.editorPosition, Vector2.zero));
            AddElement(node);
            _cachedNodes.Add(node);
            return node;
        }

        public HonamiState CreateInheritedStateOverride(HonamiNode node)
        {
            if (_controller == null || node?.State == null || !node.State.isVirtualInheritedState) return node?.State;

            var virtualState = node.State;
            var sourceState = virtualState.inheritedSourceState;
            if (sourceState == null) return virtualState;

            var existing = _controller.states.FirstOrDefault(s =>
                s != null &&
                s.layerIndex == currentLayerIndex &&
                (s.guid == virtualState.guid || s.inheritedFromStateGuid == sourceState.guid));

            if (existing != null)
            {
                PopulateView(_controller, currentLayerIndex);
                return existing;
            }

            Undo.RecordObject(_controller, "Create Inherited State Override");

            var overrideState = UnityEngine.Object.Instantiate(virtualState);
            overrideState.name = sourceState.name + "_Override_Layer_" + currentLayerIndex;
            overrideState.guid = virtualState.guid;
            overrideState.layerIndex = currentLayerIndex;
            overrideState.inheritedFromStateGuid = sourceState.guid;
            overrideState.isVirtualInheritedState = false;
            overrideState.inheritedSourceState = null;

            if (overrideState.node != null)
            {
                overrideState.node = UnityEngine.Object.Instantiate(overrideState.node);
                overrideState.node.name = overrideState.node.GetType().Name;
                AssetDatabase.AddObjectToAsset(overrideState.node, _controller);
                Undo.RegisterCreatedObjectUndo(overrideState.node, "Create Inherited State Override Node");
            }

            if (overrideState.subNodes != null)
            {
                var copiedSubNodes = new List<HonamiSubNodeBase>();
                foreach (var subNode in overrideState.subNodes)
                {
                    if (subNode == null) continue;

                    var copied = UnityEngine.Object.Instantiate(subNode);
                    copied.name = subNode.name;
                    AssetDatabase.AddObjectToAsset(copied, _controller);
                    Undo.RegisterCreatedObjectUndo(copied, "Create Inherited State Override SubNode");
                    copiedSubNodes.Add(copied);
                }
                overrideState.subNodes = copiedSubNodes;
            }

            var inheritedTransitions = _runtimeController.GetTransitions(virtualState);
            overrideState.transitions = inheritedTransitions != null
                ? inheritedTransitions.Select(t => JsonUtility.FromJson<HonamiTransition>(JsonUtility.ToJson(t))).ToList()
                : new List<HonamiTransition>();

            AssetDatabase.AddObjectToAsset(overrideState, _controller);
            Undo.RegisterCreatedObjectUndo(overrideState, "Create Inherited State Override");
            _controller.states.Add(overrideState);
            _controller.ClearEffectiveStateCache();

            EditorUtility.SetDirty(overrideState);
            EditorUtility.SetDirty(_controller);
            DeferredSave();
            PopulateView(_controller, currentLayerIndex);
            HonamiNotificationPanel.ShowGlobal("State Override Created", $"'{sourceState.stateName}' can now be edited on this layer.", HonamiNotificationType.Success);
            return overrideState;
        }

        public void CreateNewGroup(Vector2 position)
        {
            if (_controller == null) return;

            var groupData = new HonamiGroupData { position = position, layerIndex = currentLayerIndex };
            var targetCtrl = _runtimeController != null && _runtimeController.IsOverride ? (UnityEngine.Object)_runtimeController : (UnityEngine.Object)_controller;
            Undo.RecordObject(targetCtrl, "Create Group");

            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                var ov = (HonamiOverrideController)_runtimeController;
                ov.additionalGroups ??= new List<HonamiGroupData>();
                ov.additionalGroups.Add(groupData);
            }
            else
            {
                _controller.groups ??= new List<HonamiGroupData>();
                _controller.groups.Add(groupData);
            }

            CreateGroupView(groupData);
            EditorUtility.SetDirty(targetCtrl);
        }

        private HonamiGroup CreateGroupView(HonamiGroupData data)
        {
            var group = new HonamiGroup(() => EditorUtility.SetDirty(_controller)) { title = data.title };
            group.userData = data;
            Vector2 savedSize = data.size.sqrMagnitude > 1f ? data.size : new Vector2(250f, 200f);
            group.SetPosition(new Rect(data.position, savedSize));
            AddElement(group);
            return group;
        }

        public void CreateNewStickyNote(Vector2 position)
        {
            if (_controller == null) return;

            var noteData = new HonamiStickyNoteData { position = position, layerIndex = currentLayerIndex };
            var targetCtrl = _runtimeController != null && _runtimeController.IsOverride ? (UnityEngine.Object)_runtimeController : (UnityEngine.Object)_controller;
            Undo.RecordObject(targetCtrl, "Create Sticky Note");

            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                var ov = (HonamiOverrideController)_runtimeController;
                ov.additionalStickyNotes ??= new List<HonamiStickyNoteData>();
                ov.additionalStickyNotes.Add(noteData);
            }
            else
            {
                _controller.stickyNotes ??= new List<HonamiStickyNoteData>();
                _controller.stickyNotes.Add(noteData);
            }

            CreateStickyNoteView(noteData);
            EditorUtility.SetDirty(targetCtrl);
        }

        private HonamiStickyNote CreateStickyNoteView(HonamiStickyNoteData data)
        {
            var note = new HonamiStickyNote(() => EditorUtility.SetDirty(_controller))
            {
                title = data.title,
                contents = data.contents,
            };
            note.userData = data;
            Vector2 savedSize = data.size.sqrMagnitude > 1f ? data.size : new Vector2(200f, 160f);
            note.SetPosition(new Rect(data.position, savedSize));

            note.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (note.userData is HonamiStickyNoteData d)
                {
                    Undo.RecordObject(_controller, "Update Sticky Note");
                    d.title = note.title;
                    d.contents = note.contents;
                    EditorUtility.SetDirty(_controller);
                }
            });

            AddElement(note);
            return note;
        }

        private void ClearNodeOverride(HonamiNode node)
        {
            if (_runtimeController is not HonamiOverrideController overrideController) return;

            for (int i = 0; i < overrideController.nodeOverrides.Count; i++)
            {
                if (overrideController.nodeOverrides[i].stateGuid == node.StateGuid)
                {
                    var oldNode = overrideController.nodeOverrides[i].overrideNode;
                    if (oldNode != null && AssetDatabase.GetAssetPath(oldNode) == AssetDatabase.GetAssetPath(overrideController))
                    {
                        AssetDatabase.RemoveObjectFromAsset(oldNode);
                        Undo.DestroyObjectImmediate(oldNode);
                    }

                    var ovr = overrideController.nodeOverrides[i];
                    ovr.overrideNode = null;
                    overrideController.nodeOverrides[i] = ovr;
                    EditorUtility.SetDirty(overrideController);
                    DeferredSave();
                    PopulateView(_runtimeController, currentLayerIndex);
                    break;
                }
            }
        }

        private void CreateNodeOverride(HonamiNode node, Type nodeType)
        {
            if (_runtimeController is not HonamiOverrideController overrideController) return;

            var newNode = ScriptableObject.CreateInstance(nodeType) as HonamiNodeBase;
            if (newNode == null) return;

            newNode.name = nodeType.Name;
            AssetDatabase.AddObjectToAsset(newNode, overrideController);
            Undo.RegisterCreatedObjectUndo(newNode, "Create Override Node");

            bool found = false;
            for (int i = 0; i < overrideController.nodeOverrides.Count; i++)
            {
                if (overrideController.nodeOverrides[i].stateGuid == node.StateGuid)
                {
                    var ovr = overrideController.nodeOverrides[i];
                    var oldNode = ovr.overrideNode;
                    if (oldNode != null && AssetDatabase.GetAssetPath(oldNode) == AssetDatabase.GetAssetPath(overrideController))
                    {
                        AssetDatabase.RemoveObjectFromAsset(oldNode);
                        Undo.DestroyObjectImmediate(oldNode);
                    }
                    ovr.overrideNode = newNode;
                    overrideController.nodeOverrides[i] = ovr;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                overrideController.nodeOverrides.Add(new HonamiNodeOverride { stateGuid = node.StateGuid, overrideNode = newNode });
            }

            EditorUtility.SetDirty(overrideController);
            DeferredSave();
            PopulateView(_runtimeController, currentLayerIndex);
        }

        private void StartMakeTransition(HonamiNode sourceNode)
        {
            if (_isMakingTransition) CancelMakeTransition();

            _transitionSourceNode = sourceNode;
            _isMakingTransition = true;

            _transitionPreviewEdge = new HonamiTransitionEdge();
            _transitionPreviewEdge.candidatePosition = sourceNode.GetPosition().center;
            _transitionPreviewEdge.output = sourceNode.OutputPort;
            _transitionPreviewEdge.input = null;
            _transitionPreviewEdge.pickingMode = PickingMode.Ignore;

            var badge = _transitionPreviewEdge.Q(className: "honami-transition-badge");
            if (badge != null)
            {
                badge.AddToClassList("honami-transition-badge-preview");
                HonamiGraphAccent.SetBorderColor(badge, HonamiGraphStyles.Accent);
            }

            AddElement(_transitionPreviewEdge);

            RegisterCallback<MouseMoveEvent>(OnMakeTransitionMouseMove, TrickleDown.TrickleDown);
            RegisterCallback<MouseDownEvent>(OnMakeTransitionMouseDown, TrickleDown.TrickleDown);
        }

        private void OnMakeTransitionMouseMove(MouseMoveEvent evt)
        {
            if (_transitionPreviewEdge != null && _isMakingTransition)
            {
                Vector2 candidatePos = contentViewContainer.WorldToLocal(evt.mousePosition);
                _transitionPreviewEdge.candidatePosition = candidatePos;
                _transitionPreviewEdge.UpdateEdgeControl();
            }
        }

        private void OnMakeTransitionMouseDown(MouseDownEvent evt)
        {
            if (!_isMakingTransition || _transitionPreviewEdge == null) return;

            if (evt.button == 1)
            {
                CancelMakeTransition();
                evt.StopPropagation();
                return;
            }
            if (evt.button != 0) return;

            var targetNode = evt.target as HonamiNode ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<HonamiNode>();

            if (targetNode != null && targetNode != _transitionSourceNode && targetNode.InputPort != null)
            {
                if (_transitionSourceNode.State != null && _transitionSourceNode.State.isVirtualInheritedState)
                {
                    HonamiGraphWindow.ShowNotification("Override Required", "Create an override before changing inherited transitions.", HonamiNotificationType.Info);
                    CancelMakeTransition();
                    evt.StopPropagation();
                    return;
                }

                bool alreadyExists = _transitionSourceNode.State.transitions != null &&
                    _transitionSourceNode.State.transitions.Any(t => t.targetStateGuid == targetNode.StateGuid);

                if (!alreadyExists)
                {
                    var transition = new HonamiTransition { targetStateGuid = targetNode.StateGuid };
                    bool isOverride = _runtimeController != null && _runtimeController.IsOverride;
                    var targetCtrl = isOverride ? (UnityEngine.Object)_runtimeController : (UnityEngine.Object)_controller;

                    if (isOverride)
                    {
                        var ov = (HonamiOverrideController)_runtimeController;
                        bool isAdditional = ov.additionalStates.Contains(_transitionSourceNode.State);
                        if (isAdditional)
                        {
                            _transitionSourceNode.State.transitions ??= new List<HonamiTransition>();
                            _transitionSourceNode.State.transitions.Add(transition);
                            EditorUtility.SetDirty(_transitionSourceNode.State);
                        }
                        else
                        {
                            int idx = ov.transitionOverrides.FindIndex(t => t.stateGuid == _transitionSourceNode.State.guid);
                            if (idx >= 0)
                            {
                                var o = ov.transitionOverrides[idx];
                                o.transitions.Add(transition);
                                ov.transitionOverrides[idx] = o;
                            }
                            else
                            {
                                var cloned = _transitionSourceNode.State.transitions?.Select(t => JsonUtility.FromJson<HonamiTransition>(JsonUtility.ToJson(t))).ToList() ?? new List<HonamiTransition>();
                                cloned.Add(transition);
                                ov.transitionOverrides.Add(new HonamiStateTransitionsOverride { stateGuid = _transitionSourceNode.State.guid, transitions = cloned });
                            }
                            EditorUtility.SetDirty(ov);
                        }
                    }
                    else
                    {
                        Undo.RecordObject(_transitionSourceNode.State, "Add Transition");
                        _transitionSourceNode.State.transitions ??= new List<HonamiTransition>();
                        _transitionSourceNode.State.transitions.Add(transition);
                        EditorUtility.SetDirty(_transitionSourceNode.State);
                    }

                    DeferredSave();

                    var edge = new HonamiTransitionEdge()
                    {
                        output = _transitionSourceNode.OutputPort,
                        input = targetNode.InputPort,
                        userData = transition
                    };
                    edge.output.Connect(edge);
                    edge.input.Connect(edge);
                    AddElement(edge);
                }
                else
                {
                    HonamiGraphWindow.ShowNotification("Duplicate Transition", $"Transition from '{_transitionSourceNode.State.stateName}' to '{targetNode.State.stateName}' already exists.", HonamiNotificationType.Warning);
                }

                CancelMakeTransition();
                evt.StopPropagation();
            }
            else if (targetNode != null)
            {
                if (targetNode == _transitionSourceNode)
                {
                    HonamiGraphWindow.ShowNotification("Invalid Transition", "Self-transitions are controlled by the 'Can Transition To Itself' property in the Inspector.", HonamiNotificationType.Info);
                }
                else if (targetNode.InputPort == null)
                {
                    HonamiGraphWindow.ShowNotification("Invalid Target", $"Node of type '{(targetNode.State.node != null ? targetNode.State.node.GetType().Name : "Unknown")}' cannot be a transition target.", HonamiNotificationType.Warning);
                }

                CancelMakeTransition();
                evt.StopPropagation();
            }
            else
            {
                CancelMakeTransition();
            }
        }

        private void CancelMakeTransition()
        {
            _isMakingTransition = false;

            if (_transitionPreviewEdge != null)
            {
                RemoveElement(_transitionPreviewEdge);
                _transitionPreviewEdge = null;
            }

            _transitionSourceNode = null;
            UnregisterCallback<MouseMoveEvent>(OnMakeTransitionMouseMove, TrickleDown.TrickleDown);
            UnregisterCallback<MouseDownEvent>(OnMakeTransitionMouseDown, TrickleDown.TrickleDown);
        }
    }
}
