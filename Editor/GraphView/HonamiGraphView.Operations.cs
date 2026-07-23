using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Core;

namespace HonamiAnimationSystem.Editor
{
    public partial class HonamiGraphView
    {
        private List<HonamiTransition> GetWritableTransitionsForOverride(HonamiState displayedSource, out HonamiState effectiveSource)
        {
            effectiveSource = displayedSource;

            if (_runtimeController is not HonamiOverrideController ov || ov.IsOwnedState(displayedSource))
            {
                displayedSource.transitions ??= new List<HonamiTransition>();
                return displayedSource.transitions;
            }

            effectiveSource = HonamiOverrideAuthoring.EnsureEffectiveState(ov, displayedSource);
            effectiveSource.transitions ??= new List<HonamiTransition>();
            return effectiveSource.transitions;
        }

        private void RegisterOverrideTransitionAdded(HonamiState effectiveSource, string transitionId)
        {
            if (_runtimeController is not HonamiOverrideController ov) return;
            HonamiOverrideAuthoring.ResolveState(ov, effectiveSource, out var entry, out _);
            if (entry != null) HonamiOverrideAuthoring.RegisterAddedTransition(ov, entry, transitionId);
        }

        private HonamiState FindTransitionOwnerState(HonamiTransition transition)
        {
            if (_runtimeController == null || transition == null) return null;

            var states = _runtimeController.ActiveStates;
            for (int si = 0; si < states.Count; si++)
            {
                var s = states[si];
                if (s == null) continue;

                var trans = _runtimeController.GetTransitions(s);
                if (trans == null) continue;

                for (int i = 0; i < trans.Count; i++)
                {
                    if (trans[i] == transition) return s;
                    if (trans[i] != null && !string.IsNullOrEmpty(transition.id) && trans[i].id == transition.id) return s;
                }
            }

            return null;
        }

        private void BuildOverrideEdgeMenu(ContextualMenuPopulateEvent evt, HonamiTransitionEdge edge)
        {
            var transition = edge.userData as HonamiTransition;
            if (transition != null && _runtimeController is HonamiOverrideController ov)
            {
                var ownerState = FindTransitionOwnerState(transition);
                HonamiOverrideAuthoring.ResolveState(ov, ownerState, out var entry, out _);
                bool overridden = entry != null && !string.IsNullOrEmpty(transition.id) && entry.IsTransitionModified(transition.id);

                if (overridden)
                {
                    string id = transition.id;
                    evt.menu.AppendAction("Revert Transition", _ =>
                    {
                        HonamiOverrideAuthoring.RevertTransition(ov, entry, id);
                        PopulateView(_runtimeController, currentLayerIndex);
                    });
                    evt.menu.AppendAction("Apply to Parent", _ =>
                    {
                        HonamiOverrideAuthoring.ApplyTransitionToParent(ov, entry, id);
                        PopulateView(_runtimeController, currentLayerIndex);
                    });
                    evt.menu.AppendSeparator();
                }
            }

            evt.menu.AppendAction("Delete Transition", _ => DeleteElements(new List<GraphElement> { edge }));
        }

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

            if (newNode is HonamiEventNode)
            {
                newState.loop = false;
            }

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

            HonamiState state = node.State;

            if (_runtimeController is HonamiOverrideController ov)
            {
                ChangeNodeTypeInOverride(ov, state, newType);
                return;
            }

            Undo.SetCurrentGroupName("Change Node Type");
            int groupIdx = Undo.GetCurrentGroup();

            string stateGuid = state.guid;
            HonamiNodeBase oldNode = state.node;

            HonamiNodeBase newNode = (HonamiNodeBase)ScriptableObject.CreateInstance(newType);
            newNode.name = $"{state.stateName}_{newType.Name}";
            CopyPrimaryClip(oldNode, newNode);

            AssetDatabase.AddObjectToAsset(newNode, _controller);
            Undo.RegisterCreatedObjectUndo(newNode, "Change Node Type");

            Undo.RecordObject(state, "Change Node Type");
            state.node = newNode;

            if (oldNode != null)
            {
                Undo.DestroyObjectImmediate(oldNode);
            }

            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(_controller);

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

        private void ChangeNodeTypeInOverride(HonamiOverrideController ov, HonamiState state, Type newType)
        {
            if (!ov.IsOwnedState(state))
            {
                state = HonamiOverrideAuthoring.EnsureEffectiveState(ov, state);
            }

            string stateGuid = state.guid;
            HonamiNodeBase oldNode = state.node;

            var newNode = (HonamiNodeBase)ScriptableObject.CreateInstance(newType);
            newNode.name = $"{state.stateName}_{newType.Name}";
            CopyPrimaryClip(oldNode, newNode);

            HonamiOverrideAuthoring.ResolveState(ov, state, out var entry, out _);
            if (entry != null)
            {
                HonamiOverrideAuthoring.MarkNodeTypeOverride(ov, entry, newNode);
            }
            else
            {
                if (oldNode != null && AssetDatabase.GetAssetPath(oldNode) == AssetDatabase.GetAssetPath(ov))
                {
                    UnityEngine.Object.DestroyImmediate(oldNode, true);
                }

                AssetDatabase.AddObjectToAsset(newNode, ov);
                state.node = newNode;
                EditorUtility.SetDirty(state);
                EditorUtility.SetDirty(ov);
                DeferredSave();
            }

            PopulateView(_runtimeController, currentLayerIndex);

            var newNodeView = _cachedNodes.FirstOrDefault(n => n.StateGuid == stateGuid);
            if (newNodeView != null)
            {
                AddToSelection(newNodeView);
            }

            HonamiNotificationPanel.ShowGlobal("Node Type Changed", $"Changed to {newType.Name.Replace("Honami", "").Replace("Node", "")}", HonamiNotificationType.Success);
        }

        private static void CopyPrimaryClip(HonamiNodeBase oldNode, HonamiNodeBase newNode)
        {
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

            if (clip == null)
            {
                return;
            }

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

        public void SplitRandomIntoAnimationStates(HonamiNode node)
        {
            if (node?.State == null || _controller == null) return;

            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                HonamiGraphWindow.ShowNotification("Override Controller", "Splitting nodes is not supported in Override Controllers. Edit the base controller instead.", HonamiNotificationType.Info);
                return;
            }

            HonamiState sourceState = node.State;
            if (sourceState.node is not HonamiRandomAnimationNode randomNode) return;

            var clips = randomNode.randomClips?.Where(c => c != null && c.clip != null && !c.muted).ToList();
            if (clips == null || clips.Count == 0)
            {
                HonamiGraphWindow.ShowNotification("Nothing to Split", "The Random node has no active clips.", HonamiNotificationType.Warning);
                return;
            }

            Undo.SetCurrentGroupName("Split Random Into Animations");
            int groupIdx = Undo.GetCurrentGroup();
            Undo.RecordObject(_controller, "Split Random Into Animations");

            var newStates = new List<HonamiState>();
            for (int i = 1; i < clips.Count; i++)
            {
                newStates.Add(CreateSplitAnimationState(sourceState, clips[i], i));
            }

            foreach (var st in _controller.states)
            {
                if (st == null || st == sourceState || st.transitions == null || newStates.Count == 0) continue;

                bool recorded = false;
                for (int t = 0; t < st.transitions.Count; t++)
                {
                    var tr = st.transitions[t];
                    if (tr == null || tr.targetStateGuid != sourceState.guid) continue;

                    if (!recorded)
                    {
                        Undo.RecordObject(st, "Split Random Into Animations");
                        recorded = true;
                    }

                    int insertAt = t + 1;
                    foreach (var newState in newStates)
                    {
                        var clone = JsonUtility.FromJson<HonamiTransition>(JsonUtility.ToJson(tr));
                        clone.targetStateGuid = newState.guid;
                        st.transitions.Insert(insertAt++, clone);
                    }
                    t = insertAt - 1;
                }

                if (recorded) EditorUtility.SetDirty(st);
            }

            var firstClip = clips[0];
            var animNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
            animNode.name = $"{sourceState.stateName}_{nameof(HonamiAnimationNode)}";
            animNode.clip = firstClip.clip;
            animNode.startTime = firstClip.startTime;
            animNode.endTime = firstClip.endTime;
            AssetDatabase.AddObjectToAsset(animNode, _controller);
            Undo.RegisterCreatedObjectUndo(animNode, "Split Random Into Animations");

            Undo.RecordObject(sourceState, "Split Random Into Animations");
            sourceState.node = animNode;
            sourceState.speed *= firstClip.speed != 0f ? firstClip.speed : 1f;
            sourceState.mirror |= firstClip.mirror;
            Undo.DestroyObjectImmediate(randomNode);

            _controller.ClearEffectiveStateCache();
            EditorUtility.SetDirty(sourceState);
            EditorUtility.SetDirty(_controller);

            Undo.CollapseUndoOperations(groupIdx);
            DeferredSave();
            PopulateView(_runtimeController, currentLayerIndex);

            HonamiNotificationPanel.ShowGlobal("Random Node Split", $"'{sourceState.stateName}' split into {clips.Count} Animation states, transitions ported.", HonamiNotificationType.Success);
        }

        private HonamiState CreateSplitAnimationState(HonamiState sourceState, HonamiRandomAnimationClip clip, int index)
        {
            var newState = UnityEngine.Object.Instantiate(sourceState);
            newState.guid = Guid.NewGuid().ToString();
            newState.stateName = $"{sourceState.stateName} {index + 1}";
            newState.name = newState.stateName;
            newState.isDefaultState = false;
            newState.inheritedFromStateGuid = null;
            newState.isVirtualInheritedState = false;
            newState.inheritedSourceState = null;
            newState.editorPosition = sourceState.editorPosition + new Vector2(0f, 170f * index);
            newState.speed *= clip.speed != 0f ? clip.speed : 1f;
            newState.mirror |= clip.mirror;

            var animNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
            animNode.clip = clip.clip;
            animNode.startTime = clip.startTime;
            animNode.endTime = clip.endTime;
            newState.node = animNode;

            if (newState.subNodes != null)
            {
                var copiedSubNodes = new List<HonamiSubNodeBase>();
                foreach (var subNode in newState.subNodes)
                {
                    if (subNode == null) continue;
                    var copied = UnityEngine.Object.Instantiate(subNode);
                    copied.name = subNode.name;
                    AssetDatabase.AddObjectToAsset(copied, _controller);
                    Undo.RegisterCreatedObjectUndo(copied, "Split Random Into Animations");
                    copiedSubNodes.Add(copied);
                }
                newState.subNodes = copiedSubNodes;
            }

            HonamiAnimationSystem.Editor.Core.HonamiEditorController.EnsureUniqueStateName(_runtimeController, newState);
            animNode.name = $"{newState.stateName}_{nameof(HonamiAnimationNode)}";

            AssetDatabase.AddObjectToAsset(newState, _controller);
            AssetDatabase.AddObjectToAsset(animNode, _controller);
            Undo.RegisterCreatedObjectUndo(newState, "Split Random Into Animations");
            Undo.RegisterCreatedObjectUndo(animNode, "Split Random Into Animations");

            _controller.states.Add(newState);
            EditorUtility.SetDirty(newState);
            return newState;
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
            exitTransition.ease = transition.ease;
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

            Vector2 position = state.editorPosition;
            if (_runtimeController is HonamiOverrideController ov && !ov.IsOwnedState(state) &&
                ov.TryGetNodePosition(state.guid, out var storedPosition))
            {
                position = storedPosition;
            }

            node.SetPosition(new Rect(position, Vector2.zero));
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

        private void RevertNodeOverrides(HonamiNode node)
        {
            if (_runtimeController is not HonamiOverrideController overrideController || node?.State == null) return;

            HonamiOverrideAuthoring.ResolveState(overrideController, node.State, out var entry, out _);
            if (entry == null) return;

            HonamiOverrideAuthoring.RevertAll(overrideController, entry);
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

                    if (_runtimeController is HonamiOverrideController ov)
                    {
                        var list = GetWritableTransitionsForOverride(_transitionSourceNode.State, out var effectiveSource);
                        list.Add(transition);
                        RegisterOverrideTransitionAdded(effectiveSource, transition.id);
                        EditorUtility.SetDirty(ov);
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
                evt.StopPropagation();
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

        private HonamiTransitionEdge _retargetEdge;
        private HonamiTransitionEdge _retargetPreviewEdge;
        private bool _retargetActive;
        private bool _retargetToEnd;
        private Vector2 _retargetMouseStart;

        private void OnEdgeRetargetMouseDown(MouseDownEvent evt)
        {
            if (_isMakingTransition || _retargetEdge != null) return;
            if (evt.button != 0 || evt.altKey) return;
            if (_controller == null) return;

            var edge = evt.target as HonamiTransitionEdge ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<HonamiTransitionEdge>();
            if (edge == null || edge.userData is not HonamiTransition) return;
            if (edge.output?.node == null || edge.input?.node == null) return;

            _retargetEdge = edge;
            _retargetActive = false;
            _retargetMouseStart = evt.mousePosition;

            if (!edge.selected)
            {
                if (!evt.actionKey) ClearSelection();
                AddToSelection(edge);
            }
            else if (evt.actionKey)
            {
                RemoveFromSelection(edge);
            }

            this.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnEdgeRetargetMouseMove(MouseMoveEvent evt)
        {
            if (_retargetEdge == null) return;

            if (!_retargetActive)
            {
                if ((evt.mousePosition - _retargetMouseStart).sqrMagnitude < 64f) return;
                BeginRetargetDrag(contentViewContainer.WorldToLocal(_retargetMouseStart));
            }

            if (_retargetPreviewEdge != null)
            {
                _retargetPreviewEdge.candidatePosition = contentViewContainer.WorldToLocal(evt.mousePosition);
                _retargetPreviewEdge.UpdateEdgeControl();
            }

            evt.StopPropagation();
        }

        private void BeginRetargetDrag(Vector2 grabPosition)
        {
            _retargetActive = true;

            bool isLoop = _retargetEdge.output.node == _retargetEdge.input.node;
            Vector2 segment = _retargetEdge.EndPoint - _retargetEdge.StartPoint;
            float segmentLengthSqr = segment.sqrMagnitude;
            float grabT = segmentLengthSqr > 0.0001f
                ? Vector2.Dot(grabPosition - _retargetEdge.StartPoint, segment) / segmentLengthSqr
                : 1f;
            _retargetToEnd = isLoop || grabT > 0.3f;

            _retargetPreviewEdge = new HonamiTransitionEdge();
            _retargetPreviewEdge.pickingMode = PickingMode.Ignore;
            _retargetPreviewEdge.candidatePosition = grabPosition;

            if (_retargetToEnd)
                _retargetPreviewEdge.output = _retargetEdge.output;
            else
                _retargetPreviewEdge.input = _retargetEdge.input;

            var badge = _retargetPreviewEdge.Q(className: "honami-transition-badge");
            if (badge != null)
            {
                badge.AddToClassList("honami-transition-badge-preview");
                HonamiGraphAccent.SetBorderColor(badge, HonamiGraphStyles.Accent);
            }

            AddElement(_retargetPreviewEdge);
            _retargetEdge.style.opacity = 0.3f;
        }

        private void OnEdgeRetargetMouseUp(MouseUpEvent evt)
        {
            if (_retargetEdge == null) return;

            var edge = _retargetEdge;
            bool wasDragging = _retargetActive;
            bool toEnd = _retargetToEnd;

            CleanupRetargetDrag();

            if (wasDragging && evt.button == 0)
            {
                var dropNode = GetNodeAtWorldPosition(evt.mousePosition);
                if (dropNode != null)
                    ApplyRetarget(edge, dropNode, toEnd);
            }

            if (this.HasMouseCapture()) this.ReleaseMouse();
            evt.StopPropagation();
        }

        private void CancelRetargetDrag()
        {
            if (_retargetEdge == null) return;
            CleanupRetargetDrag();
            if (this.HasMouseCapture()) this.ReleaseMouse();
        }

        private void CleanupRetargetDrag()
        {
            if (_retargetPreviewEdge != null)
            {
                RemoveElement(_retargetPreviewEdge);
                _retargetPreviewEdge = null;
            }

            if (_retargetEdge != null)
                _retargetEdge.style.opacity = StyleKeyword.Null;

            _retargetEdge = null;
            _retargetActive = false;
        }

        private HonamiNode GetNodeAtWorldPosition(Vector2 worldPosition)
        {
            for (int i = _cachedNodes.Count - 1; i >= 0; i--)
            {
                var node = _cachedNodes[i];
                if (node != null && node.worldBound.Contains(worldPosition)) return node;
            }
            return null;
        }

        private void ApplyRetarget(HonamiTransitionEdge edge, HonamiNode dropNode, bool toEnd)
        {
            var trans = edge.userData as HonamiTransition;
            var sourceNode = edge.output?.node as HonamiNode;
            var targetNode = edge.input?.node as HonamiNode;
            if (trans == null || sourceNode?.State == null || targetNode?.State == null || dropNode?.State == null) return;

            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                HonamiGraphWindow.ShowNotification("Override Controller", "Retargeting transitions by drag is not supported in Override Controllers. Edit the base controller instead.", HonamiNotificationType.Info);
                return;
            }

            if (sourceNode.State.isVirtualInheritedState)
            {
                HonamiGraphWindow.ShowNotification("Override Required", "Create an override before changing inherited transitions.", HonamiNotificationType.Info);
                return;
            }

            if (toEnd)
            {
                if (dropNode == targetNode) return;

                if (dropNode == sourceNode)
                {
                    HonamiGraphWindow.ShowNotification("Invalid Transition", "Self-transitions are controlled by the 'Can Transition To Itself' property in the Inspector.", HonamiNotificationType.Info);
                    return;
                }

                if (dropNode.InputPort == null)
                {
                    HonamiGraphWindow.ShowNotification("Invalid Target", $"Node of type '{(dropNode.State.node != null ? dropNode.State.node.GetType().Name : "Unknown")}' cannot be a transition target.", HonamiNotificationType.Warning);
                    return;
                }

                if (sourceNode.State.transitions != null && sourceNode.State.transitions.Any(t => t != trans && t != null && t.targetStateGuid == dropNode.StateGuid))
                {
                    HonamiGraphWindow.ShowNotification("Duplicate Transition", $"Transition from '{sourceNode.State.stateName}' to '{dropNode.State.stateName}' already exists.", HonamiNotificationType.Warning);
                    return;
                }

                Undo.RecordObject(sourceNode.State, "Retarget Transition");
                trans.targetStateGuid = dropNode.StateGuid;
                EditorUtility.SetDirty(sourceNode.State);

                edge.input.Disconnect(edge);
                edge.input = dropNode.InputPort;
                edge.input.Connect(edge);
            }
            else
            {
                if (dropNode == sourceNode) return;

                if (dropNode == targetNode)
                {
                    HonamiGraphWindow.ShowNotification("Invalid Transition", "Self-transitions are controlled by the 'Can Transition To Itself' property in the Inspector.", HonamiNotificationType.Info);
                    return;
                }

                if (dropNode.State.isVirtualInheritedState)
                {
                    HonamiGraphWindow.ShowNotification("Override Required", "Create an override before changing inherited transitions.", HonamiNotificationType.Info);
                    return;
                }

                if (dropNode.OutputPort == null)
                {
                    HonamiGraphWindow.ShowNotification("Invalid Source", "This node type cannot be a source of transitions.", HonamiNotificationType.Warning);
                    return;
                }

                if (dropNode.State.transitions != null && dropNode.State.transitions.Any(t => t != null && t.targetStateGuid == targetNode.StateGuid))
                {
                    HonamiGraphWindow.ShowNotification("Duplicate Transition", $"Transition from '{dropNode.State.stateName}' to '{targetNode.State.stateName}' already exists.", HonamiNotificationType.Warning);
                    return;
                }

                Undo.RecordObjects(new UnityEngine.Object[] { sourceNode.State, dropNode.State }, "Move Transition Source");
                sourceNode.State.transitions.Remove(trans);
                dropNode.State.transitions ??= new List<HonamiTransition>();
                dropNode.State.transitions.Add(trans);
                EditorUtility.SetDirty(sourceNode.State);
                EditorUtility.SetDirty(dropNode.State);

                edge.output.Disconnect(edge);
                edge.output = dropNode.OutputPort;
                edge.output.Connect(edge);
            }

            EditorUtility.SetDirty(_controller);
            DeferredSave();

            edge.UpdateEdgeControl();
            edge.MarkDirtyRepaint();
        }
    }
}
