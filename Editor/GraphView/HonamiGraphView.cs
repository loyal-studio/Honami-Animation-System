using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    [UxmlElement]
    public partial class HonamiGraphView : GraphView
    {
        public System.Action<HonamiNode> OnNodeSelected;
        public System.Action<HonamiNode, HonamiSubNodeBase> OnSubNodeSelected;
        public System.Action<Edge, HonamiTransition> OnEdgeSelected;
        public System.Action<List<HonamiTransition>> OnEdgesSelected;
        public System.Action OnDeselected;

        private void ClearEvents()
        {
            OnNodeSelected = null;
            OnSubNodeSelected = null;
            OnEdgeSelected = null;
            OnEdgesSelected = null;
            OnDeselected = null;
        }

        private HonamiRuntimeController _runtimeController;
        private HonamiController _controller;
        public int currentLayerIndex = 0;

        private MiniMap _miniMap;
        private bool _isMiniMapVisible = true;
        private bool _animationsEnabled = true;
        private bool _tactile3DEnabled = true;
        private bool _gridVisible = true;

        public HonamiController Controller => _controller;
        public HonamiRuntimeController RuntimeController => _runtimeController;

        public bool IsMiniMapVisible
        {
            get => _isMiniMapVisible;
            set
            {
                _isMiniMapVisible = value;
                if (_miniMap != null)
                    _miniMap.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetAnimationsEnabled(bool enabled)
        {
            _animationsEnabled = enabled;
            if (enabled) AddToClassList("honami-animated");
            else RemoveFromClassList("honami-animated");
        }

        public void SetTactile3DEnabled(bool enabled)
        {
            _tactile3DEnabled = enabled;
        }

        public void SetGridVisible(bool visible)
        {
            _gridVisible = visible;
            var grid = this.Q<HonamiGridBackground>();
            if (grid != null)
                grid.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private HonamiNode _transitionSourceNode;
        private HonamiTransitionEdge _transitionPreviewEdge;
        private bool _isMakingTransition = false;

        private bool _suppressGroupCallbacks = false;
        private bool _isLoadingView = false;

        private string _cachedControllerGuid;
        private List<HonamiNode> _cachedNodes = new();
        private List<HonamiTransitionEdge> _cachedEdges = new();
        private Dictionary<string, HonamiState> _stateGuidMap = new();

        private Label _overrideWatermark;
        private Label _layerWatermark;

        public HonamiGraphView()
        {
            Insert(0, new HonamiGridBackground());

            _overrideWatermark = new Label("OVERRIDE CONTROLLER");
            _overrideWatermark.style.position = Position.Absolute;
            _overrideWatermark.style.top = 20;
            _overrideWatermark.style.right = 20;
            _overrideWatermark.style.fontSize = 16;
            _overrideWatermark.style.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            _overrideWatermark.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 0.7f);
            _overrideWatermark.style.paddingTop = 6;
            _overrideWatermark.style.paddingBottom = 6;
            _overrideWatermark.style.paddingLeft = 14;
            _overrideWatermark.style.paddingRight = 14;
            _overrideWatermark.style.borderTopLeftRadius = 8;
            _overrideWatermark.style.borderTopRightRadius = 8;
            _overrideWatermark.style.borderBottomLeftRadius = 8;
            _overrideWatermark.style.borderBottomRightRadius = 8;
            _overrideWatermark.style.borderTopWidth = 1;
            _overrideWatermark.style.borderBottomWidth = 1;
            _overrideWatermark.style.borderLeftWidth = 1;
            _overrideWatermark.style.borderRightWidth = 1;
            _overrideWatermark.style.borderTopColor = new Color(1f, 0.6f, 0.2f, 0.3f);
            _overrideWatermark.style.borderBottomColor = new Color(1f, 0.6f, 0.2f, 0.3f);
            _overrideWatermark.style.borderLeftColor = new Color(1f, 0.6f, 0.2f, 0.3f);
            _overrideWatermark.style.borderRightColor = new Color(1f, 0.6f, 0.2f, 0.3f);
            _overrideWatermark.style.unityFontStyleAndWeight = FontStyle.Bold;
            _overrideWatermark.style.display = DisplayStyle.None;
            _overrideWatermark.pickingMode = PickingMode.Ignore;
            Add(_overrideWatermark);

            _layerWatermark = new Label(string.Empty);
            _layerWatermark.style.position = Position.Absolute;
            _layerWatermark.style.top = 12;
            _layerWatermark.style.left = 12;
            _layerWatermark.style.fontSize = 12;
            _layerWatermark.style.color = new Color(0.75f, 0.78f, 0.85f, 0.85f);
            _layerWatermark.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 0.6f);
            _layerWatermark.style.paddingTop = 4;
            _layerWatermark.style.paddingBottom = 4;
            _layerWatermark.style.paddingLeft = 10;
            _layerWatermark.style.paddingRight = 10;
            _layerWatermark.style.borderTopLeftRadius = 6;
            _layerWatermark.style.borderTopRightRadius = 6;
            _layerWatermark.style.borderBottomLeftRadius = 6;
            _layerWatermark.style.borderBottomRightRadius = 6;
            _layerWatermark.style.borderTopWidth = 1;
            _layerWatermark.style.borderBottomWidth = 1;
            _layerWatermark.style.borderLeftWidth = 1;
            _layerWatermark.style.borderRightWidth = 1;
            _layerWatermark.style.borderTopColor = new Color(1f, 1f, 1f, 0.08f);
            _layerWatermark.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);
            _layerWatermark.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);
            _layerWatermark.style.borderRightColor = new Color(1f, 1f, 1f, 0.08f);
            _layerWatermark.style.unityFontStyleAndWeight = FontStyle.Bold;
            _layerWatermark.style.display = DisplayStyle.None;
            _layerWatermark.pickingMode = PickingMode.Ignore;
            Add(_layerWatermark);

            this.AddManipulator(new ContentZoomer
            {
                minScale = 0.01f,
                maxScale = 50f,
                scaleStep = 0.15f,
                referenceScale = 1f,
            });
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var styleSheet = Resources.Load<StyleSheet>("HonamiGraphStyle");
            if (styleSheet != null)
                styleSheets.Add(styleSheet);

            _miniMap = new MiniMap { anchored = false };
            _miniMap.AddToClassList("honami-minimap");
            _miniMap.SetPosition(new Rect(10, 30, 200, 140));
            _miniMap.RegisterCallback<AttachToPanelEvent>(_ => HonamiGraphAccent.ApplyMinimapViewport(_miniMap));
            Add(_miniMap);

            IsMiniMapVisible = HonamiGraphSettings.ShowMinimap;
            SetAnimationsEnabled(HonamiGraphSettings.EnableAnimations);
            SetTactile3DEnabled(HonamiGraphSettings.EnableTactile3D);
            SetGridVisible(HonamiGraphSettings.ShowGrid);

            viewTransformChanged = (gv) => SaveCameraPosition();

            RegisterCallback<MouseMoveEvent>(evt => _lastMousePos = evt.mousePosition);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);

            RegisterCallback<MouseDownEvent>(OnEdgeRetargetMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(OnEdgeRetargetMouseMove);
            RegisterCallback<MouseUpEvent>(OnEdgeRetargetMouseUp);
            RegisterCallback<MouseCaptureOutEvent>(_ => CancelRetargetDrag());

            elementsAddedToGroup = (group, elements) =>
            {
                if (_suppressGroupCallbacks) return;
                if (group is Group g && g.userData is HonamiGroupData data)
                {
                    var toAdd = new System.Collections.Generic.List<string>();
                    foreach (var elem in elements)
                        if (elem is HonamiNode node) toAdd.Add(node.StateGuid);

                    HonamiAnimationSystem.Editor.Core.HonamiEditorController.EditGroupNodes(_controller, data, toAdd, null);
                }
            };

            elementsRemovedFromGroup = (group, elements) =>
            {
                if (_suppressGroupCallbacks) return;
                if (group is Group g && g.userData is HonamiGroupData data)
                {
                    var toRemove = new System.Collections.Generic.List<string>();
                    foreach (var elem in elements)
                        if (elem is HonamiNode node) toRemove.Add(node.StateGuid);

                    HonamiAnimationSystem.Editor.Core.HonamiEditorController.EditGroupNodes(_controller, data, null, toRemove);
                }
            };

            groupTitleChanged = (group, newTitle) =>
            {
                if (_suppressGroupCallbacks) return;
                if (group is Group g && g.userData is HonamiGroupData data)
                {
                    HonamiAnimationSystem.Editor.Core.HonamiEditorController.RenameGroup(_controller, data, newTitle);
                }
            };

            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private HonamiSearchProvider _searchProvider;
        private EditorWindow _window;

        public void Init(EditorWindow window)
        {
            _window = window;
            _searchProvider = ScriptableObject.CreateInstance<HonamiSearchProvider>();
            _searchProvider.Init(this, window);

            nodeCreationRequest = (context) =>
            {
                SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchProvider);
            };
        }

        private Vector2 _lastMousePos;

        #region Runtime Visualization
        private Dictionary<HonamiState, int> _stateIndexMap = new Dictionary<HonamiState, int>();

        public void UpdateRuntimeVisualization(Runtime.Core.HonamiAnimator animator)
        {
            if (animator == null || _runtimeController == null) return;

            int activeIdx = animator.GetActiveStateIndex(currentLayerIndex);
            int prevIdx = animator.GetPreviousStateIndex(currentLayerIndex);
            float weight = animator.GetTransitionWeight(currentLayerIndex);

            var activeStates = _runtimeController.ActiveStates;
            if (_stateIndexMap.Count != activeStates.Count)
            {
                _stateIndexMap.Clear();
                for (int i = 0; i < activeStates.Count; i++)
                {
                    if (activeStates[i] != null)
                        _stateIndexMap[activeStates[i]] = i;
                }
            }

            foreach (HonamiNode hNode in _cachedNodes)
            {
                if (hNode.State == null || !_stateIndexMap.TryGetValue(hNode.State, out int stateIdx))
                    continue;

                bool isActive = (stateIdx == activeIdx);
                bool isPrev = (stateIdx == prevIdx);

                float progress = 0f;
                if (isActive) progress = animator.GetStateProgress(currentLayerIndex, activeIdx);
                else if (isPrev) progress = animator.GetStateProgress(currentLayerIndex, prevIdx);

                hNode.UpdateRuntimeState(isActive || (isPrev && weight < 0.99f), progress);
            }

            foreach (HonamiTransitionEdge hEdge in _cachedEdges)
            {
                var trans = hEdge.userData as HonamiTransition;
                bool isActive = false;

                if (trans != null && prevIdx != -1)
                {
                    var sourceNode = hEdge.output?.node as HonamiNode;
                    if (sourceNode != null && sourceNode.State != null && _stateIndexMap.TryGetValue(sourceNode.State, out int sourceIdx))
                    {
                        if (sourceIdx == prevIdx && activeIdx != -1 && activeIdx < activeStates.Count && trans.targetStateGuid == activeStates[activeIdx].guid)
                        {
                            isActive = weight > 0f && weight < 1f;
                        }
                    }
                }

                hEdge.UpdateRuntimeState(isActive);
            }
        }

        public void ClearRuntimeVisualization()
        {
            foreach (HonamiNode node in _cachedNodes)
                node.UpdateRuntimeState(false, 0f);
            foreach (HonamiTransitionEdge edge in _cachedEdges)
                edge.UpdateRuntimeState(false);
        }
        #endregion

        public void ClearSubNodeSelection()
        {
            foreach (var node in _cachedNodes)
            {
                node.SetSubNodeSelected(null);
            }
        }

        public void PopulateView(HonamiRuntimeController runtimeController, int layerIdx)
        {
            FlushCameraSave();
            _runtimeController = runtimeController;
            _controller = runtimeController != null ? runtimeController.BaseController : null;
            if (_controller != null && _controller.SanitizeEditorData())
            {
                _controller.ClearEffectiveStateCache();
                EditorUtility.SetDirty(_controller);
                DeferredSave();
            }
            currentLayerIndex = layerIdx;
            _cachedControllerGuid = null;
            _stateGuidMap.Clear();
            _stateIndexMap.Clear();

            if (_overrideWatermark != null)
            {
                _overrideWatermark.style.display = (_runtimeController != null && _runtimeController.IsOverride)
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }

            UpdateLayerWatermark();

            ClearSelection();

            graphViewChanged -= OnGraphViewChanged;

            var toDelete = graphElements.ToList();
            DeleteElements(toDelete);

            _cachedNodes.Clear();
            _cachedEdges.Clear();

            graphViewChanged += OnGraphViewChanged;

            LoadCameraPosition();

            if (_isMakingTransition)
            {
                CancelMakeTransition();
            }

            CancelRetargetDrag();

            if (_runtimeController == null || _runtimeController.ActiveStates == null) return;

            var nodeDict = new Dictionary<string, HonamiNode>();
            if (currentLayerIndex < 0 || currentLayerIndex >= _runtimeController.ActiveLayers.Count) return;

            var activeStates = _runtimeController.ActiveStates;
            int statesCount = activeStates.Count;

            for (int i = 0; i < statesCount; i++)
            {
                var state = activeStates[i];
                if (state == null || state.layerIndex != currentLayerIndex) continue;

                _stateGuidMap[state.guid] = state;
                var node = CreateNodeView(state, state.isDefaultState);
                nodeDict[state.guid] = node;
            }

            for (int i = 0; i < statesCount; i++)
            {
                var state = activeStates[i];
                if (state == null || state.layerIndex != currentLayerIndex) continue;

                var transitions = _runtimeController.GetTransitions(state);
                if (transitions == null || transitions.Count == 0) continue;
                if (!nodeDict.TryGetValue(state.guid, out var startNode)) continue;

                int transCount = transitions.Count;
                for (int j = 0; j < transCount; j++)
                {
                    var trans = transitions[j];
                    if (trans == null || string.IsNullOrEmpty(trans.targetStateGuid)) continue;
                    if (!nodeDict.TryGetValue(trans.targetStateGuid, out var endNode)) continue;

                    if (startNode.OutputPort != null && endNode.InputPort != null)
                    {
                        var edge = new HonamiTransitionEdge();
                        edge.output = startNode.OutputPort;
                        edge.input = endNode.InputPort;
                        edge.output.Connect(edge);
                        edge.input.Connect(edge);
                        edge.userData = trans;
                        edge.tooltip = GetTransitionTooltip(trans);
                        AddElement(edge);
                        _cachedEdges.Add(edge);
                    }
                }
            }


            var groupsToLoad = new List<HonamiGroupData>();
            if (_controller != null && _controller.groups != null) groupsToLoad.AddRange(_controller.groups);
            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                var overCtrl = _runtimeController as HonamiOverrideController;
                if (overCtrl != null && overCtrl.additionalGroups != null) groupsToLoad.AddRange(overCtrl.additionalGroups);
            }

            if (groupsToLoad.Count > 0)
            {
                _suppressGroupCallbacks = true;
                try
                {
                    foreach (var groupData in groupsToLoad)
                    {
                        if (groupData == null || groupData.layerIndex != currentLayerIndex) continue;
                        var group = CreateGroupView(groupData);

                        if (groupData.containedNodes != null)
                        {
                            var elementsToAdd = new List<GraphElement>();
                            foreach (var guid in groupData.containedNodes)
                            {
                                if (nodeDict.TryGetValue(guid, out var node))
                                {
                                    if (node.parent == contentViewContainer)
                                        elementsToAdd.Add(node);
                                }
                            }
                            group.AddElements(elementsToAdd);
                        }
                    }
                }
                finally
                {
                    _suppressGroupCallbacks = false;
                }
            }

            var stickyNotesToLoad = new List<HonamiStickyNoteData>();
            if (_controller != null && _controller.stickyNotes != null) stickyNotesToLoad.AddRange(_controller.stickyNotes);
            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                var overCtrl = _runtimeController as HonamiOverrideController;
                if (overCtrl != null && overCtrl.additionalStickyNotes != null) stickyNotesToLoad.AddRange(overCtrl.additionalStickyNotes);
            }

            foreach (var noteData in stickyNotesToLoad)
            {
                if (noteData == null || noteData.layerIndex != currentLayerIndex) continue;
                CreateStickyNoteView(noteData);
            }
        }

        private void UpdateLayerWatermark()
        {
            if (_layerWatermark == null) return;

            var layers = _runtimeController != null ? _runtimeController.ActiveLayers : null;
            if (layers == null || currentLayerIndex < 0 || currentLayerIndex >= layers.Count || layers[currentLayerIndex] == null)
            {
                _layerWatermark.style.display = DisplayStyle.None;
                return;
            }

            var layer = layers[currentLayerIndex];
            string text = currentLayerIndex == 0
                ? $"{layer.name}  |  Base Layer"
                : $"{layer.name}  |  Layer {currentLayerIndex}";

            if (layer.parentLayerIndex >= 0 && layer.parentLayerIndex < layers.Count && layers[layer.parentLayerIndex] != null)
                text += $"  |  Child of {layers[layer.parentLayerIndex].name}";

            _layerWatermark.text = text;
            _layerWatermark.style.display = DisplayStyle.Flex;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach(port =>
            {
                if (startPort == port || startPort.node == port.node) return;
                if (startPort.direction == port.direction) return;
                if (startPort.portType != port.portType) return;
                compatiblePorts.Add(port);
            });
            return compatiblePorts;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                var targetNodeOverride = evt.target as HonamiNode ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<HonamiNode>();
                if (targetNodeOverride != null)
                {
                    if (targetNodeOverride.IsOverridden)
                    {
                        evt.menu.AppendAction("Clear Override", (a) => ClearNodeOverride(targetNodeOverride));
                    }

                    var nodeTypes = TypeCache.GetTypesDerivedFrom<HonamiNodeBase>()
                        .Where(t => !t.IsAbstract && !t.IsGenericType)
                        .OrderBy(t => t.Name).ToList();

                    foreach (var type in nodeTypes)
                    {
                        string typeName = type.Name.Replace("Honami", "").Replace("Node", "");
                        evt.menu.AppendAction($"Create Override/{typeName}", (a) => CreateNodeOverride(targetNodeOverride, type));
                    }
                }
                else
                {
                    var overrideGraphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);

                    foreach ((HonamiNodeEditorAttribute attr, _) in HonamiNodeEditorRegistry.AllEntries)
                    {
                        Type capturedType = attr.NodeType;
                        evt.menu.AppendAction($"Create Node/{attr.MenuPath}",
                            _ => CreateNewStateNodeOfType(overrideGraphPosition, capturedType));
                    }

                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Create Decoration Group", _ => CreateNewGroup(overrideGraphPosition));
                    evt.menu.AppendAction("Create Sticky Note", _ => CreateNewStickyNote(overrideGraphPosition));
                }
                return;
            }

            if (_isMakingTransition)
            {
                evt.menu.AppendAction("Cancel Transition", (a) => CancelMakeTransition());
                return;
            }

            if (_controller == null) return;

            var targetNode = evt.target as HonamiNode ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<HonamiNode>();
            var targetEdge = evt.target as HonamiTransitionEdge ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<HonamiTransitionEdge>();
            var targetGroup = targetNode == null
                ? (evt.target as Group ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<Group>())
                : null;

            if (targetNode != null)
            {
                if (targetNode.State != null && targetNode.State.isVirtualInheritedState)
                {
                    evt.menu.AppendAction("Create Override", (a) => CreateInheritedStateOverride(targetNode));
                    evt.menu.AppendAction("Inherited State", _ => { }, DropdownMenuAction.Status.Disabled);
                    return;
                }

                if (targetNode.OutputPort != null)
                {
                    evt.menu.AppendAction("Make Transition", (a) => StartMakeTransition(targetNode));
                }
                else
                {
                    evt.menu.AppendAction("Make Transition (Invalid)", (a) =>
                        HonamiGraphWindow.ShowNotification("Invalid Action", $"This node type cannot be a source of transitions.", HonamiNotificationType.Warning),
                        DropdownMenuAction.Status.Disabled);
                }

                if (targetNode.State != null && targetNode.State.node is not HonamiAnyStateNode && targetNode.State.node is not HonamiRepeaterNode)
                {
                    bool isCurrentlyDefault = targetNode.State.isDefaultState;
                    if (isCurrentlyDefault)
                    {
                        evt.menu.AppendAction("Unset as Default State", (a) =>
                        {
                            HonamiAnimationSystem.Editor.Core.HonamiEditorController.SetDefaultState(null, targetNode.State);
                            PopulateView(_runtimeController, currentLayerIndex);
                        });
                    }
                    else
                    {
                        evt.menu.AppendAction("Set as Default State", (a) =>
                        {
                            var oldDefault = _controller.states.FirstOrDefault(s => s != null && s.layerIndex == currentLayerIndex && s.isDefaultState);
                            HonamiAnimationSystem.Editor.Core.HonamiEditorController.SetDefaultState(targetNode.State, oldDefault);
                            PopulateView(_runtimeController, currentLayerIndex);
                        });
                    }
                }

                evt.menu.AppendSeparator();
                var nodeTypes = TypeCache.GetTypesDerivedFrom<HonamiNodeBase>()
                    .Where(t => !t.IsAbstract && !t.IsGenericType && (targetNode.State.node == null || t != targetNode.State.node.GetType()))
                    .OrderBy(t => t.Name).ToList();

                foreach (var type in nodeTypes)
                {
                    string typeName = type.Name.Replace("Honami", "").Replace("Node", "");
                    evt.menu.AppendAction($"Change Type/{typeName}", (a) => ChangeNodeType(targetNode, type));
                }

                if (targetNode.State.node is HonamiRandomAnimationNode randomToSplit)
                {
                    int splitClipCount = randomToSplit.randomClips?.Count(c => c != null && c.clip != null && !c.muted) ?? 0;
                    if (splitClipCount >= 2)
                    {
                        evt.menu.AppendAction($"Split Into {splitClipCount} Animation States", (a) => SplitRandomIntoAnimationStates(targetNode));
                    }
                }

                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Selection/Align Horizontal", (a) => AlignNodesHorizontal());
                evt.menu.AppendAction("Selection/Align Vertical", (a) => AlignNodesVertical());

                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete Node", (a) =>
                {
                    DeleteElements(new List<GraphElement> { targetNode });
                });
            }
            else if (targetEdge != null)
            {
                var sourceNode = targetEdge.output?.node as HonamiNode;
                if (sourceNode?.State != null && sourceNode.State.isVirtualInheritedState)
                {
                    evt.menu.AppendAction("Create Source Override", (a) => CreateInheritedStateOverride(sourceNode));
                    evt.menu.AppendAction("Edit Transition After Override", _ => { }, DropdownMenuAction.Status.Disabled);
                    return;
                }

                evt.menu.AppendAction("Split with Portals", (a) => SplitTransitionWithPortals(targetEdge, contentViewContainer.WorldToLocal(evt.mousePosition)));
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete Transition", (a) =>
                {
                    DeleteElements(new List<GraphElement> { targetEdge });
                });
            }
            else if (targetGroup != null && targetGroup.userData is HonamiGroupData)
            {
                evt.menu.AppendAction("Delete Group", (a) =>
                {
                    DeleteGroupOnly(targetGroup);
                });
            }
            else
            {
                var selectedNodes = selection.OfType<HonamiNode>().ToList();
                if (selectedNodes.Count > 0)
                {
                    evt.menu.AppendAction("Group Selection (Ctrl+G)", (a) => GroupSelection());
                }

                var selectedGroups = selection.OfType<Group>().ToList();
                if (selectedGroups.Count > 0)
                {
                    evt.menu.AppendAction("Ungroup (Ctrl+Shift+G)", (a) => UngroupSelection());
                }

                if (selectedNodes.Count > 0 || selectedGroups.Count > 0)
                    evt.menu.AppendSeparator();

                var menuPosition = evt.mousePosition;
                var graphPosition = contentViewContainer.WorldToLocal(menuPosition);

                foreach ((HonamiNodeEditorAttribute attr, _) in HonamiNodeEditorRegistry.AllEntries)
                {
                    Type capturedType = attr.NodeType;
                    evt.menu.AppendAction($"Create Node/{attr.MenuPath}",
                        _ => CreateNewStateNodeOfType(graphPosition, capturedType));
                }

                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Create Decoration Group", _ => CreateNewGroup(graphPosition));
                evt.menu.AppendAction("Create Sticky Note", _ => CreateNewStickyNote(graphPosition));
            }
        }

    }
}
