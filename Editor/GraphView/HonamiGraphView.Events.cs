using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Core;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public partial class HonamiGraphView
    {
        private bool IsEditingText()
        {
            var focused = panel?.focusController?.focusedElement as VisualElement;
            if (focused == null) return false;
            return focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (IsEditingText()) return;

            if (evt.actionKey && evt.keyCode == KeyCode.G)
            {
                if (evt.shiftKey) UngroupSelection();
                else GroupSelection();
                evt.StopPropagation();
            }
            else if (evt.actionKey && evt.keyCode == KeyCode.C)
            {
                CopySelection();
                evt.StopPropagation();
            }
            else if (evt.actionKey && evt.keyCode == KeyCode.V)
            {
                PasteSelection(contentViewContainer.WorldToLocal(_lastMousePos));
                evt.StopPropagation();
            }
        }

        private void GroupSelection()
        {
            var nodes = selection.OfType<HonamiNode>().ToList();
            if (nodes.Count == 0) return;

            Undo.RecordObject(_controller, "Group Selection");

            var groupData = new HonamiGroupData();
            groupData.title = "New Group";
            groupData.layerIndex = currentLayerIndex;

            Rect rect = nodes[0].GetPosition();
            foreach (var node in nodes.Skip(1))
            {
                Rect nodeRect = node.GetPosition();
                rect.xMin = Mathf.Min(rect.xMin, nodeRect.xMin);
                rect.yMin = Mathf.Min(rect.yMin, nodeRect.yMin);
                rect.xMax = Mathf.Max(rect.xMax, nodeRect.xMax);
                rect.yMax = Mathf.Max(rect.yMax, nodeRect.yMax);
            }
            rect.xMin -= 30;
            rect.yMin -= 50;
            rect.xMax += 30;
            rect.yMax += 30;

            groupData.position = rect.position;
            groupData.size = rect.size;

            foreach (var node in nodes)
            {
                if (!groupData.containedNodes.Contains(node.StateGuid))
                    groupData.containedNodes.Add(node.StateGuid);
            }

            if (_controller.groups == null) _controller.groups = new List<HonamiGroupData>();
            _controller.groups.Add(groupData);

            _suppressGroupCallbacks = true;
            try
            {
                var group = CreateGroupView(groupData);
                group.AddElements(nodes.Cast<GraphElement>());
            }
            finally
            {
                _suppressGroupCallbacks = false;
            }

            EditorUtility.SetDirty(_controller);
        }

        private void UngroupSelection()
        {
            var selectedGroups = selection.OfType<Group>().ToList();
            if (selectedGroups.Count == 0) return;

            Undo.RecordObject(_controller, "Ungroup Selection");

            foreach (var group in selectedGroups)
            {
                DeleteGroupOnly(group);
            }

            EditorUtility.SetDirty(_controller);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            var draggedObjects = DragAndDrop.objectReferences;
            if (draggedObjects.Any(obj => obj is AnimationClip))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (_controller == null) return;

            var draggedObjects = DragAndDrop.objectReferences;
            var clips = draggedObjects.OfType<AnimationClip>().ToList();
            if (clips.Count == 0) return;

            Undo.RecordObject(_controller, "Drag Animation Clips to Graph");
            Vector2 localMousePos = contentViewContainer.WorldToLocal(evt.mousePosition);
            Group targetGroup = null;
            var picked = panel.Pick(evt.mousePosition);
            targetGroup = picked?.GetFirstAncestorOfType<Group>();
            var newNodes = new List<HonamiNode>();

            foreach (var clip in clips)
            {
                var animNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
                animNode.name = clip.name + "_AnimNode";
                animNode.clip = clip;

                var newState = ScriptableObject.CreateInstance<HonamiState>();
                newState.name = clip.name;
                newState.stateName = clip.name;
                newState.node = animNode;
                newState.editorPosition = localMousePos;
                newState.layerIndex = currentLayerIndex;

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

                Object dragTargetCtrl = _runtimeController != null && _runtimeController.IsOverride
                    ? (Object)_runtimeController
                    : _controller;
                AssetDatabase.AddObjectToAsset(animNode, dragTargetCtrl);
                AssetDatabase.AddObjectToAsset(newState, dragTargetCtrl);
                Undo.RegisterCreatedObjectUndo(animNode, "Drag Animation Clips to Graph");
                Undo.RegisterCreatedObjectUndo(newState, "Drag Animation Clips to Graph");

                // State is already added above

                var node = CreateNodeView(newState, false);
                newNodes.Add(node);
                localMousePos += new Vector2(30, 30);
            }

            if (targetGroup != null)
            {
                targetGroup.AddElements(newNodes.Cast<GraphElement>());
            }

            if (_runtimeController != null && _runtimeController.IsOverride)
                EditorUtility.SetDirty(_runtimeController);
            else
                EditorUtility.SetDirty(_controller);

            DeferredSave();
            HonamiNotificationPanel.ShowGlobal("Animations Added", $"Successfully added {clips.Count} animation states.", HonamiNotificationType.Success);

            DragAndDrop.AcceptDrag();
            evt.StopPropagation();
        }

        private void DeleteGroupOnly(Group group)
        {
            if (group.userData is not HonamiGroupData gData) return;

            _suppressGroupCallbacks = true;
            try
            {
                var children = group.containedElements.ToList();
                if (children.Count > 0)
                    group.RemoveElements(children);
            }
            finally
            {
                _suppressGroupCallbacks = false;
            }

            if (_controller != null && _controller.groups != null)
                _controller.groups.Remove(gData);

            RemoveElement(group);
            EditorUtility.SetDirty(_controller);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            if (_controller == null) return graphViewChange;

            if (graphViewChange.elementsToRemove != null)
            {
                var extraEdges = new List<Edge>();
                foreach (var elem in graphViewChange.elementsToRemove)
                {
                    if (elem is HonamiNode node)
                    {
                        if (node.InputPort != null) extraEdges.AddRange(node.InputPort.connections);
                        if (node.OutputPort != null) extraEdges.AddRange(node.OutputPort.connections);
                    }
                }
                foreach (var edge in extraEdges)
                {
                    if (edge != null && !graphViewChange.elementsToRemove.Contains(edge))
                        graphViewChange.elementsToRemove.Add(edge);
                }

                int inheritedRemoveCount = graphViewChange.elementsToRemove.RemoveAll(elem =>
                    elem is HonamiNode node && node.State != null && node.State.isVirtualInheritedState);
                if (inheritedRemoveCount > 0)
                    HonamiGraphWindow.ShowNotification("Inherited State", "Create an override before deleting or changing inherited states.", HonamiNotificationType.Info);

                var groupsToRemove = graphViewChange.elementsToRemove.OfType<Group>().ToList();
                foreach (var grp in groupsToRemove)
                {
                    if (grp == null) continue;
                    _suppressGroupCallbacks = true;
                    try
                    {
                        var children = grp.containedElements.ToList();
                        if (children.Count > 0)
                            grp.RemoveElements(children);
                    }
                    finally
                    {
                        _suppressGroupCallbacks = false;
                    }
                }

                var nodesToRemove = graphViewChange.elementsToRemove.OfType<HonamiNode>().ToList();
                var removedNodeGuids = new HashSet<string>(nodesToRemove.Select(n => n.StateGuid));

                if (removedNodeGuids.Count > 0)
                {
                    foreach (var os in _controller.states)
                    {
                        if (os == null || removedNodeGuids.Contains(os.guid)) continue;
                        if (os.transitions == null) continue;
                        if (!os.transitions.Exists(t => t != null && removedNodeGuids.Contains(t.targetStateGuid))) continue;

                        Undo.RecordObject(os, "Remove Incoming Transitions");
                        os.transitions.RemoveAll(t => t != null && removedNodeGuids.Contains(t.targetStateGuid));
                        EditorUtility.SetDirty(os);
                    }

                    if (_controller.groups != null)
                    {
                        foreach (var grp in _controller.groups)
                        {
                            if (grp == null || grp.containedNodes == null) continue;
                            if (!grp.containedNodes.Exists(guid => removedNodeGuids.Contains(guid))) continue;

                            Undo.RecordObject(_controller, "Remove Nodes From Group");
                            grp.containedNodes.RemoveAll(guid => removedNodeGuids.Contains(guid));
                            EditorUtility.SetDirty(_controller);
                        }
                    }
                }

                foreach (var elem in graphViewChange.elementsToRemove)
                {
                    if (elem == null) continue;

                    switch (elem)
                    {
                        case HonamiNode node:
                            _cachedNodes.Remove(node);
                            if (_stateGuidMap.TryGetValue(node.StateGuid, out var state))
                            {
                                _stateGuidMap.Remove(node.StateGuid);
                                HonamiEditorController.DeleteStateNode(
                                    _runtimeController != null ? _runtimeController : _controller, state);
                            }
                            break;
                        case HonamiTransitionEdge hEdge:
                            _cachedEdges.Remove(hEdge);
                            if (hEdge.userData is HonamiTransition trans)
                                RemoveTransitionFromSourceState(trans, hEdge.output?.node as HonamiNode);
                            break;
                        case Edge edge:
                            if (edge.userData is HonamiTransition transEdge)
                                RemoveTransitionFromSourceState(transEdge, edge.output?.node as HonamiNode);
                            break;
                        case Group grp when grp.userData is HonamiGroupData gData:
                            Undo.RecordObject(_controller, "Remove Group");
                            _controller.groups.Remove(gData);
                            break;
                        case HonamiStickyNote note when note.userData is HonamiStickyNoteData nData:
                            Undo.RecordObject(_controller, "Remove Sticky Note");
                            _controller.stickyNotes.Remove(nData);
                            break;
                    }
                }

                EditorUtility.SetDirty(_controller);
            }

            if (graphViewChange.edgesToCreate != null)
            {
                int inheritedEdgeCount = graphViewChange.edgesToCreate.RemoveAll(edge =>
                {
                    var startNode = edge.output?.node as HonamiNode;
                    return startNode?.State != null && startNode.State.isVirtualInheritedState;
                });
                if (inheritedEdgeCount > 0)
                    HonamiGraphWindow.ShowNotification("Override Required", "Create an override before changing inherited transitions.", HonamiNotificationType.Info);

                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    var startNode = edge.output?.node as HonamiNode;
                    var endNode = edge.input?.node as HonamiNode;
                    if (startNode == null || endNode == null) continue;

                    _stateGuidMap.TryGetValue(startNode.StateGuid, out var sourceState);
                    sourceState ??= _controller.states.FirstOrDefault(s => s != null && s.guid == startNode.StateGuid);
                    if (sourceState == null) continue;

                    var transition = new HonamiTransition { targetStateGuid = endNode.StateGuid };
                    if (_runtimeController is HonamiOverrideController ov)
                    {
                        var list = GetWritableTransitionsForOverride(sourceState, out var effectiveSource);
                        list.Add(transition);
                        RegisterOverrideTransitionAdded(effectiveSource, transition.id);
                        EditorUtility.SetDirty(ov);
                    }
                    else
                    {
                        Undo.RecordObject(sourceState, "Add Transition");
                        sourceState.transitions ??= new List<HonamiTransition>();
                        sourceState.transitions.Add(transition);
                        EditorUtility.SetDirty(sourceState);
                    }

                    edge.userData = transition;
                }
                if (_runtimeController != null && _runtimeController.IsOverride)
                    EditorUtility.SetDirty(_runtimeController);
                else
                    EditorUtility.SetDirty(_controller);
            }

            if (graphViewChange.movedElements != null)
            {
                bool anythingDirty = false;
                foreach (var elem in graphViewChange.movedElements)
                {
                    switch (elem)
                    {
                        case HonamiNode node:
                            var state = _stateGuidMap.TryGetValue(node.StateGuid, out var s) ? s : null;
                            if (state != null && !state.isVirtualInheritedState)
                            {
                                if (_runtimeController is HonamiOverrideController posOverride && !posOverride.IsOwnedState(state))
                                {
                                    posOverride.SetNodePosition(node.StateGuid, node.GetPosition().position);
                                    EditorUtility.SetDirty(posOverride);
                                }
                                else
                                {
                                    HonamiEditorController.MoveStateNode(state, node.GetPosition().position);
                                }
                                anythingDirty = true;
                            }
                            break;
                        case Group group when group.userData is HonamiGroupData:
                            anythingDirty = true;
                            break;
                    }
                }
                if (anythingDirty)
                {
                    EditorUtility.SetDirty(_controller);
                    DeferredSave();
                }
            }

            return graphViewChange;
        }

        private void RemoveTransitionFromSourceState(HonamiTransition trans, HonamiNode startNode)
        {
            if (trans == null) return;

            HonamiState sourceState = null;
            if (startNode != null)
            {
                _stateGuidMap.TryGetValue(startNode.StateGuid, out sourceState);
                sourceState ??= _controller.states.FirstOrDefault(st => st != null && st.guid == startNode.StateGuid);
            }
            else
            {
                sourceState = _controller.states.FirstOrDefault(st => st != null && st.transitions != null && st.transitions.Contains(trans));
            }

            if (sourceState != null)
            {
                if (sourceState.isVirtualInheritedState)
                {
                    HonamiGraphWindow.ShowNotification("Override Required", "Create an override before removing inherited transitions.", HonamiNotificationType.Info);
                    return;
                }

                if (_runtimeController is HonamiOverrideController ov && trans.id != null)
                {
                    if (ov.IsOwnedState(sourceState))
                    {
                        HonamiOverrideAuthoring.ResolveState(ov, sourceState, out var ownedEntry, out _);
                        if (ownedEntry != null)
                        {
                            HonamiOverrideAuthoring.RemoveInheritedTransition(ov, ownedEntry, trans.id);
                            return;
                        }
                    }
                    else
                    {
                        var effectiveSource = HonamiOverrideAuthoring.EnsureEffectiveState(ov, sourceState);
                        HonamiOverrideAuthoring.ResolveState(ov, effectiveSource, out var entry, out _);
                        HonamiOverrideAuthoring.RemoveInheritedTransition(ov, entry, trans.id);
                        return;
                    }
                }

                HonamiEditorController.RemoveTransition(sourceState, trans);
            }
        }
    }
}
