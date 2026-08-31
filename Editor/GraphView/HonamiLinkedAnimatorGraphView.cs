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
    public partial class HonamiLinkedAnimatorGraphView : GraphView
    {
        private HonamiLinkedAnimatorGraph _brainGraph;
        private HonamiLinkedAnimatorSearchProvider _brainSearchProvider;
        private List<HonamiLinkedAnimatorNode> _cachedBrainNodes = new();
        private List<HonamiLinkedAnimatorEventNode> _cachedBrainEventNodes = new();
        private readonly Dictionary<HonamiLinkedAnimatorNodeBase, HonamiLinkedAnimatorNode> _brainNodeByAsset = new();
        private readonly Dictionary<HonamiLinkedAnimatorEvent, HonamiLinkedAnimatorEventNode> _brainEventNodeByAsset = new();
        private readonly HashSet<HonamiLinkedAnimatorNodeBase> _activeBrainNodeSet = new();

        private static List<Type> _cachedBrainNodeTypes;
        private static List<Type> CachedBrainNodeTypes => _cachedBrainNodeTypes ??= TypeCache.GetTypesDerivedFrom<HonamiLinkedAnimatorNodeBase>()
            .Where(t => !t.IsAbstract && !t.IsGenericType)
            .OrderBy(t => t.Name).ToList();
        private Label _brainWatermark;
        private EditorWindow _window;

        public HonamiLinkedAnimatorGraph BrainGraph => _brainGraph;

        public System.Action<HonamiLinkedAnimatorNodeBase> OnBrainNodeSelected;
        public System.Action<HonamiLinkedAnimatorEvent> OnBrainEventSelected;
        public System.Action OnBrainStructureChanged;

        private HonamiLinkedAnimatorEdgeConnectorListener _edgeConnectorListener;
        public IEdgeConnectorListener EdgeConnectorListener
        {
            get
            {
                if (_edgeConnectorListener == null) _edgeConnectorListener = new HonamiLinkedAnimatorEdgeConnectorListener(this);
                return _edgeConnectorListener;
            }
        }

        public static void DeferredSave() => HonamiGraphView.DeferredSave();

        public HonamiLinkedAnimatorGraphView()
        {
            Insert(0, new HonamiGridBackground());

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

            _brainWatermark = new Label("LINKED ANIMATOR GRAPH");
            _brainWatermark.style.position = Position.Absolute;
            _brainWatermark.style.top = 20;
            _brainWatermark.style.right = 20;
            _brainWatermark.style.fontSize = 16;
            _brainWatermark.style.color = new Color(0.3f, 0.7f, 1f, 0.8f);
            _brainWatermark.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 0.7f);
            _brainWatermark.style.paddingTop = 6;
            _brainWatermark.style.paddingBottom = 6;
            _brainWatermark.style.paddingLeft = 14;
            _brainWatermark.style.paddingRight = 14;
            _brainWatermark.style.borderTopLeftRadius = 8;
            _brainWatermark.style.borderTopRightRadius = 8;
            _brainWatermark.style.borderBottomLeftRadius = 8;
            _brainWatermark.style.borderBottomRightRadius = 8;
            _brainWatermark.style.borderTopWidth = 1;
            _brainWatermark.style.borderBottomWidth = 1;
            _brainWatermark.style.borderLeftWidth = 1;
            _brainWatermark.style.borderRightWidth = 1;
            _brainWatermark.style.borderTopColor = new Color(0.3f, 0.7f, 1f, 0.3f);
            _brainWatermark.style.borderBottomColor = new Color(0.3f, 0.7f, 1f, 0.3f);
            _brainWatermark.style.borderLeftColor = new Color(0.3f, 0.7f, 1f, 0.3f);
            _brainWatermark.style.borderRightColor = new Color(0.3f, 0.7f, 1f, 0.3f);
            _brainWatermark.style.unityFontStyleAndWeight = FontStyle.Bold;
            _brainWatermark.pickingMode = PickingMode.Ignore;
            Add(_brainWatermark);

            graphViewChanged = (changes) =>
            {
                if (changes.edgesToCreate != null)
                {
                    foreach (var edge in changes.edgesToCreate)
                        ConnectBrainFlow(edge.output, edge.input);
                }

                if (changes.elementsToRemove != null)
                {
                    foreach (var elem in changes.elementsToRemove)
                    {
                        if (elem is Edge edge)
                            DisconnectBrainFlow(edge.output, edge.input);
                        else if (elem is HonamiLinkedAnimatorNode bn)
                            DeleteBrainNode(bn);
                        else if (elem is HonamiLinkedAnimatorEventNode en)
                            DeleteBrainEvent(en);
                    }
                }

                return changes;
            };
        }

        public void InitBrainMode(EditorWindow window)
        {
            if (_brainSearchProvider == null)
            {
                _brainSearchProvider = ScriptableObject.CreateInstance<HonamiLinkedAnimatorSearchProvider>();
                _brainSearchProvider.Init(this, window);
            }
            _window = window;

            nodeCreationRequest = (context) =>
            {
                if (_brainSearchProvider != null)
                    SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _brainSearchProvider);
            };
        }

        public void OpenLinkedAnimatorSearchWindow(Vector2 position, Port sourcePort)
        {
            if (_brainSearchProvider == null || _window == null) return;

            _brainSearchProvider.SourcePort = sourcePort;
            SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(position)), _brainSearchProvider);
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

        public void UpdateLinkedAnimatorNodeUI(HonamiLinkedAnimatorNodeBase nodeBase)
        {
            if (nodeBase != null && _brainNodeByAsset.TryGetValue(nodeBase, out var node))
                node.UpdateUI();
        }

        public void UpdateLinkedAnimatorEventUI(HonamiLinkedAnimatorEvent eventBase)
        {
            if (eventBase != null && _brainEventNodeByAsset.TryGetValue(eventBase, out var evtNode))
                evtNode.UpdateTitle();
        }

        public void PopulateBrainView(HonamiLinkedAnimatorGraph graph)
        {
            _brainGraph = graph;
            _cachedBrainNodes.Clear();
            _cachedBrainEventNodes.Clear();
            _brainNodeByAsset.Clear();
            _brainEventNodeByAsset.Clear();

            DeleteElements(graphElements.ToList());

            if (graph == null)
            {
                return;
            }

            foreach (var evt in graph.events)
            {
                if (evt == null) continue;

                var eventNode = new HonamiLinkedAnimatorEventNode(evt, this);
                eventNode.SetPosition(new Rect(evt.editorPosition, Vector2.zero));
                AddElement(eventNode);
                _cachedBrainEventNodes.Add(eventNode);
                _brainEventNodeByAsset.TryAdd(evt, eventNode);
            }

            foreach (var node in graph.nodes)
            {
                if (node == null) continue;

                var brainNode = new HonamiLinkedAnimatorNode(node, this);
                brainNode.SetPosition(new Rect(node.editorPosition, Vector2.zero));
                AddElement(brainNode);
                _cachedBrainNodes.Add(brainNode);
                _brainNodeByAsset.TryAdd(node, brainNode);
            }

            ConnectBrainFlows(graph);

            schedule.Execute(() => FrameAll()).ExecuteLater(50);
        }

        private void ConnectBrainFlows(HonamiLinkedAnimatorGraph graph)
        {
            foreach (var evt in graph.events)
            {
                if (evt?.rootNodes == null) continue;

                if (!_brainEventNodeByAsset.TryGetValue(evt, out var eventNode)) continue;

                foreach (var rootNode in evt.rootNodes)
                {
                    if (rootNode == null) continue;
                    if (!_brainNodeByAsset.TryGetValue(rootNode, out var targetNode)) continue;

                    var edge = eventNode.FlowOut.ConnectTo(targetNode.FlowIn);
                    AddElement(edge);
                }
            }

            foreach (var node in graph.nodes)
            {
                if (node is LinkedAnimatorSequenceNode seq && seq.children != null)
                {
                    if (!_brainNodeByAsset.TryGetValue(node, out var sourceNode)) continue;

                    foreach (var child in seq.children)
                    {
                        if (child == null) continue;
                        if (!_brainNodeByAsset.TryGetValue(child, out var targetNode)) continue;

                        var edge = sourceNode.FlowOut.ConnectTo(targetNode.FlowIn);
                        AddElement(edge);
                    }
                }

                if (node is LinkedAnimatorConditionNode cond)
                {
                    if (!_brainNodeByAsset.TryGetValue(node, out var sourceNode)) continue;

                    if (cond.onTrue != null)
                    {
                        if (_brainNodeByAsset.TryGetValue(cond.onTrue, out var trueNode))
                        {
                            var edge = sourceNode.FlowOut.ConnectTo(trueNode.FlowIn);
                            AddElement(edge);
                        }
                    }
                    if (cond.onFalse != null)
                    {
                        if (_brainNodeByAsset.TryGetValue(cond.onFalse, out var falseNode))
                        {
                            var edge = sourceNode.FlowOut.ConnectTo(falseNode.FlowIn);
                            AddElement(edge);
                        }
                    }
                }
            }
        }

        public Node CreateBrainEvent(Vector2 position)
        {
            if (_brainGraph == null) return null;

            Undo.RecordObject(_brainGraph, "Create Brain Event");

            var evt = ScriptableObject.CreateInstance<HonamiLinkedAnimatorEvent>();
            evt.name = "New Event";
            evt.eventName = "New Event";
            evt.editorPosition = position;
            evt.guid = Guid.NewGuid().ToString();

            AssetDatabase.AddObjectToAsset(evt, _brainGraph);
            _brainGraph.events.Add(evt);
            EditorUtility.SetDirty(_brainGraph);
            DeferredSave();

            var eventNode = new HonamiLinkedAnimatorEventNode(evt, this);
            eventNode.SetPosition(new Rect(position, Vector2.zero));
            AddElement(eventNode);
            _cachedBrainEventNodes.Add(eventNode);
            _brainEventNodeByAsset[evt] = eventNode;

            ClearSelection();
            AddToSelection(eventNode);

            OnBrainStructureChanged?.Invoke();
            return eventNode;
        }

        public Node CreateBrainNode(Vector2 position, Type nodeType)
        {
            if (_brainGraph == null || nodeType == null) return null;
            if (!typeof(HonamiLinkedAnimatorNodeBase).IsAssignableFrom(nodeType)) return null;

            Undo.RecordObject(_brainGraph, "Create Brain Node");

            var nodeInstance = ScriptableObject.CreateInstance(nodeType) as HonamiLinkedAnimatorNodeBase;
            if (nodeInstance == null) return null;

            nodeInstance.name = nodeType.Name;
            nodeInstance.editorPosition = position;
            nodeInstance.guid = Guid.NewGuid().ToString();

            AssetDatabase.AddObjectToAsset(nodeInstance, _brainGraph);
            _brainGraph.nodes.Add(nodeInstance);
            EditorUtility.SetDirty(_brainGraph);
            DeferredSave();

            var brainNode = new HonamiLinkedAnimatorNode(nodeInstance, this);
            brainNode.SetPosition(new Rect(position, Vector2.zero));
            AddElement(brainNode);
            _cachedBrainNodes.Add(brainNode);
            _brainNodeByAsset[nodeInstance] = brainNode;

            ClearSelection();
            AddToSelection(brainNode);

            OnBrainStructureChanged?.Invoke();
            return brainNode;
        }

        public void DeleteBrainNode(HonamiLinkedAnimatorNode node)
        {
            if (_brainGraph == null || node?.BrainNode == null) return;

            Undo.RecordObject(_brainGraph, "Delete Brain Node");

            foreach (var evt in _brainGraph.events)
            {
                if (evt?.rootNodes != null)
                    evt.rootNodes.Remove(node.BrainNode);
            }

            foreach (var n in _brainGraph.nodes)
            {
                if (n is LinkedAnimatorSequenceNode seq && seq.children != null)
                    seq.children.Remove(node.BrainNode);
                if (n is LinkedAnimatorConditionNode cond)
                {
                    if (cond.onTrue == node.BrainNode) cond.onTrue = null;
                    if (cond.onFalse == node.BrainNode) cond.onFalse = null;
                }
            }

            _brainGraph.nodes.Remove(node.BrainNode);
            Undo.DestroyObjectImmediate(node.BrainNode);

            _cachedBrainNodes.Remove(node);
            _brainNodeByAsset.Remove(node.BrainNode);
            RemoveElement(node);

            EditorUtility.SetDirty(_brainGraph);
            DeferredSave();
        }

        public void DeleteBrainEvent(HonamiLinkedAnimatorEventNode eventNode)
        {
            if (_brainGraph == null || eventNode?.BrainEvent == null) return;

            Undo.RecordObject(_brainGraph, "Delete Brain Event");
            _brainGraph.events.Remove(eventNode.BrainEvent);
            Undo.DestroyObjectImmediate(eventNode.BrainEvent);

            _cachedBrainEventNodes.Remove(eventNode);
            _brainEventNodeByAsset.Remove(eventNode.BrainEvent);
            RemoveElement(eventNode);

            EditorUtility.SetDirty(_brainGraph);
            DeferredSave();
            OnBrainStructureChanged?.Invoke();
        }

        public void ConnectBrainFlow(Port outputPort, Port inputPort)
        {
            if (_brainGraph == null) return;

            var sourceEventNode = outputPort.node as HonamiLinkedAnimatorEventNode;
            var sourceNode = outputPort.node as HonamiLinkedAnimatorNode;
            var targetNode = inputPort.node as HonamiLinkedAnimatorNode;

            if (targetNode == null) return;

            Undo.RecordObject(_brainGraph, "Connect Brain Flow");

            if (sourceEventNode != null && sourceEventNode.BrainEvent != null)
            {
                if (!sourceEventNode.BrainEvent.rootNodes.Contains(targetNode.BrainNode))
                {
                    Undo.RecordObject(sourceEventNode.BrainEvent, "Connect Event Flow");
                    sourceEventNode.BrainEvent.rootNodes.Add(targetNode.BrainNode);
                    EditorUtility.SetDirty(sourceEventNode.BrainEvent);
                }
            }
            else if (sourceNode != null && sourceNode.BrainNode != null)
            {
                if (sourceNode.BrainNode is LinkedAnimatorSequenceNode seq)
                {
                    Undo.RecordObject(sourceNode.BrainNode, "Connect Sequence Flow");
                    if (!seq.children.Contains(targetNode.BrainNode))
                        seq.children.Add(targetNode.BrainNode);
                    EditorUtility.SetDirty(sourceNode.BrainNode);
                }
                else if (sourceNode.BrainNode is LinkedAnimatorConditionNode cond)
                {
                    Undo.RecordObject(cond, "Connect Condition Flow");
                    if (outputPort.portName == "True") cond.onTrue = targetNode.BrainNode;
                    else cond.onFalse = targetNode.BrainNode;
                    EditorUtility.SetDirty(cond);
                }
                else
                {
                    Undo.RecordObject(sourceNode.BrainNode, "Connect Node Flow");
                    sourceNode.BrainNode.next = targetNode.BrainNode;
                    EditorUtility.SetDirty(sourceNode.BrainNode);
                }
            }

            EditorUtility.SetDirty(_brainGraph);
            DeferredSave();
        }

        public void DisconnectBrainFlow(Port outputPort, Port inputPort)
        {
            if (_brainGraph == null) return;

            var sourceEventNode = outputPort.node as HonamiLinkedAnimatorEventNode;
            var sourceNode = outputPort.node as HonamiLinkedAnimatorNode;
            var targetNode = inputPort.node as HonamiLinkedAnimatorNode;

            if (targetNode == null) return;

            Undo.RecordObject(_brainGraph, "Disconnect Brain Flow");

            if (sourceEventNode != null && sourceEventNode.BrainEvent != null)
            {
                Undo.RecordObject(sourceEventNode.BrainEvent, "Disconnect Event Flow");
                sourceEventNode.BrainEvent.rootNodes.Remove(targetNode.BrainNode);
                EditorUtility.SetDirty(sourceEventNode.BrainEvent);
            }
            else if (sourceNode != null && sourceNode.BrainNode != null)
            {
                if (sourceNode.BrainNode is LinkedAnimatorSequenceNode seq)
                {
                    Undo.RecordObject(sourceNode.BrainNode, "Disconnect Sequence Flow");
                    seq.children.Remove(targetNode.BrainNode);
                    EditorUtility.SetDirty(sourceNode.BrainNode);
                }
                else if (sourceNode.BrainNode is LinkedAnimatorConditionNode cond)
                {
                    Undo.RecordObject(cond, "Disconnect Condition Flow");
                    if (outputPort.portName == "True") cond.onTrue = null;
                    else cond.onFalse = null;
                    EditorUtility.SetDirty(cond);
                }
                else
                {
                    Undo.RecordObject(sourceNode.BrainNode, "Disconnect Node Flow");
                    sourceNode.BrainNode.next = null;
                    EditorUtility.SetDirty(sourceNode.BrainNode);
                }
            }

            EditorUtility.SetDirty(_brainGraph);
            DeferredSave();
        }

        public void AutoConnectPorts(Port sourcePort, Node targetNode)
        {
            if (sourcePort == null || targetNode == null) return;

            Port targetPort = targetNode.Query<Port>().ToList()
                .FirstOrDefault(p => p.direction != sourcePort.direction);

            if (targetPort != null)
            {
                var edge = sourcePort.direction == Direction.Output
                    ? sourcePort.ConnectTo(targetPort)
                    : targetPort.ConnectTo(sourcePort);

                AddElement(edge);
                ConnectBrainFlow(edge.output, edge.input);
            }
        }
        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            NotifySelectionChanged();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            NotifySelectionChanged();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            if (selection == null || selection.Count == 0)
            {
                OnBrainNodeSelected?.Invoke(null);
                OnBrainEventSelected?.Invoke(null);
                return;
            }

            var first = selection[0];
            if (first is HonamiLinkedAnimatorNode linkedNode)
            {
                OnBrainNodeSelected?.Invoke(linkedNode.BrainNode);
            }
            else if (first is HonamiLinkedAnimatorEventNode eventNode)
            {
                OnBrainEventSelected?.Invoke(eventNode.BrainEvent);
            }
            else
            {
                OnBrainNodeSelected?.Invoke(null);
                OnBrainEventSelected?.Invoke(null);
            }
        }

        public void UpdateBrainDebugVisuals(IReadOnlyList<HonamiLinkedAnimatorNodeBase> activeNodes)
        {
            if (activeNodes == null) return;

            _activeBrainNodeSet.Clear();
            for (int i = 0; i < activeNodes.Count; i++)
                _activeBrainNodeSet.Add(activeNodes[i]);

            foreach (var node in _cachedBrainNodes)
            {
                bool isActive = _activeBrainNodeSet.Contains(node.BrainNode);
                node.SetActive(isActive);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var menuPosition = evt.mousePosition;
            var graphPosition = contentViewContainer.WorldToLocal(menuPosition);

            evt.menu.AppendAction("Create Event Entry Point", _ => CreateBrainEvent(graphPosition));
            evt.menu.AppendSeparator();

            var brainNodeTypes = CachedBrainNodeTypes;

            foreach (var type in brainNodeTypes)
            {
                string typeName = type.Name.Replace("LinkedAnimator", "").Replace("Node", "");
                evt.menu.AppendAction($"Create Node/{typeName}", _ => CreateBrainNode(graphPosition, type));
            }

            var targetBrainNode = evt.target as HonamiLinkedAnimatorNode ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<HonamiLinkedAnimatorNode>();
            if (targetBrainNode != null)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete Node", _ => DeleteBrainNode(targetBrainNode));
            }

            var targetEventNode = evt.target as HonamiLinkedAnimatorEventNode ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<HonamiLinkedAnimatorEventNode>();
            if (targetEventNode != null)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete Event Node", _ => DeleteBrainEvent(targetEventNode));
            }
        }
    }

    public sealed class HonamiLinkedAnimatorEdgeConnectorListener : IEdgeConnectorListener
    {
        private HonamiLinkedAnimatorGraphView _graphView;
        public HonamiLinkedAnimatorEdgeConnectorListener(HonamiLinkedAnimatorGraphView gv) => _graphView = gv;

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            _graphView.OpenLinkedAnimatorSearchWindow(position, edge.output ?? edge.input);
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            _graphView.ConnectBrainFlow(edge.output, edge.input);
        }
    }
}
