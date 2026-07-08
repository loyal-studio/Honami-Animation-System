using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Linq;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiNode : Node
    {
        public string StateGuid;
        public HonamiState State => _state;
        private HonamiState _state;
        private bool _isDefault;
        private HonamiGraphView _graphView;
        private HonamiRuntimeController _runtimeController;

        public HonamiNodeBase ActiveNode { get; private set; }
        public bool IsOverridden { get; private set; }

        public Port InputPort;
        public Port OutputPort;

        private System.Collections.Generic.Dictionary<HonamiSubNodeBase, Label> _subNodeBadges = new();

        public HonamiNode(HonamiState state, bool isDefaultState = false, HonamiRuntimeController runtimeController = null)
        {
            _state = state;
            StateGuid = state.guid;
            _isDefault = isDefaultState;
            _runtimeController = runtimeController;
            title = state.stateName;

            capabilities &= ~Capabilities.Resizable;
            capabilities &= ~Capabilities.Collapsible;
            if (state.isVirtualInheritedState)
                capabilities &= ~Capabilities.Movable;

            titleContainer.style.display = DisplayStyle.None;
            inputContainer.style.display = DisplayStyle.None;
            outputContainer.style.display = DisplayStyle.None;
            extensionContainer.style.display = DisplayStyle.Flex;

            GenerateUI();

            var divider = this.Q("divider");
            if (divider != null) divider.style.display = DisplayStyle.None;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<AttachToPanelEvent>(_ => _graphView = this.GetFirstAncestorOfType<HonamiGraphView>());
            RegisterCallback<DetachFromPanelEvent>(_ => _graphView = null);

            this.AddManipulator(new ContextualMenuManipulator(PopulateContextMenu));

            RefreshExpandedState();
        }

        public void RefreshView(bool isDefaultState)
        {
            _isDefault = isDefaultState;
            title = _state.stateName;
            GenerateUI();
        }

        private void GenerateUI()
        {
            extensionContainer.Clear();

            foreach (string cls in GetNodeClasses()) RemoveFromClassList(cls);

            ActiveNode = _state.node;
            IsOverridden = false;
            if (_runtimeController != null && _runtimeController.IsOverride)
            {
                ActiveNode = _runtimeController.GetActiveNode(_state);
                if (ActiveNode != _state.node)
                {
                    IsOverridden = true;
                }
            }

            HonamiNodeEditorBase editor = HonamiNodeEditorRegistry.GetEditor(ActiveNode);

            string nodeCss = editor?.NodeCssClass ?? GetNodeCssFromState(_state, ActiveNode);
            string topCss = editor?.TopBarCssClass ?? (nodeCss.Replace("honami-node-", "honami-node-top-"));
            string avatarCss = editor?.AvatarCssClass ?? (nodeCss.Replace("honami-node-", "honami-node-avatar-"));
            string iconCss = editor?.IconCssClass ?? (nodeCss.Replace("honami-node-", "honami-node-icon-"));

            AddToClassList(nodeCss);
            if (_isDefault) AddToClassList("honami-node-default");
            if (IsOverridden) AddToClassList("honami-node-overridden");
            if (_state.isVirtualInheritedState) AddToClassList("honami-node-inherited");

            this.tooltip = editor?.Tooltip ?? "";

            VisualElement topBar = new VisualElement();
            topBar.AddToClassList("honami-node-top");
            topBar.AddToClassList(topCss);
            if (_isDefault) topBar.AddToClassList("honami-node-top-default");

            VisualElement customBody = new VisualElement();
            customBody.AddToClassList("honami-node-body");

            VisualElement avatar = new VisualElement();
            avatar.AddToClassList("honami-node-avatar");
            avatar.AddToClassList(avatarCss);
            if (_isDefault) avatar.AddToClassList("honami-node-avatar-default");

            VisualElement icon = new VisualElement();
            icon.AddToClassList("honami-node-icon");
            icon.AddToClassList(iconCss);
            if (_isDefault) icon.AddToClassList("honami-node-icon-default");

            avatar.Add(icon);

            VisualElement textContainer = new VisualElement();
            textContainer.AddToClassList("honami-node-text-container");

            string displayTitle = editor?.GetTitle(_state) ?? _state.stateName;
            string displaySubtitle = editor?.GetSubtitle(_state) ?? (ActiveNode != null ? ActiveNode.GetType().Name.Replace("Honami", "").Replace("Node", "") : _state.stateName);

            if (_isDefault)
                displaySubtitle = "DEFAULT " + displaySubtitle.ToUpper();

            Label titleLabel = new Label(displayTitle);
            titleLabel.name = "honami-title-label";
            titleLabel.AddToClassList("honami-node-label");

            Label subtitleLabel = new Label(displaySubtitle);
            subtitleLabel.AddToClassList("honami-node-subtitle");

            textContainer.Add(titleLabel);
            textContainer.Add(subtitleLabel);

            customBody.Add(avatar);
            customBody.Add(textContainer);

            extensionContainer.Add(topBar);
            extensionContainer.Add(customBody);

            _subNodeBadges.Clear();
            if (_state.subNodes != null && _state.subNodes.Count > 0)
            {
                VisualElement subNodesBar = new VisualElement();
                subNodesBar.AddToClassList("honami-node-subnodes-bar");
                subNodesBar.pickingMode = PickingMode.Position;
                foreach (var sn in _state.subNodes)
                {
                    if (sn == null) continue;
                    Label badge = new Label(sn.DisplayName);
                    badge.AddToClassList("honami-node-subnode-badge");
                    badge.pickingMode = PickingMode.Position;

                    badge.RegisterCallback<MouseDownEvent>(evt =>
                    {
                        evt.StopPropagation();
                        _graphView?.ClearSelection();
                        SetSubNodeSelected(sn);
                        _graphView?.OnSubNodeSelected?.Invoke(this, sn);
                    });

                    badge.RegisterCallback<MouseUpEvent>(evt => evt.StopPropagation());

                    badge.RegisterCallback<PointerOverEvent>(evt => badge.AddToClassList("honami-node-subnode-badge-hover"));
                    badge.RegisterCallback<PointerOutEvent>(evt => badge.RemoveFromClassList("honami-node-subnode-badge-hover"));
                    HonamiGraphAccent.AttachSubNodeBadge(badge);

                    subNodesBar.Add(badge);
                    _subNodeBadges[sn] = badge;
                }
                extensionContainer.Add(subNodesBar);
            }

            if (_progressContainer == null)
            {
                _progressBar = new VisualElement();
                _progressBar.AddToClassList("honami-node-progress-bar");

                _progressContainer = new VisualElement();
                _progressContainer.AddToClassList("honami-node-progress-container");
                _progressContainer.Add(_progressBar);
            }
            extensionContainer.Add(_progressContainer);

            UpdatePorts();
        }

        private static readonly string[] _nodeClasses =
        {
            "honami-node-any", "honami-node-repeater", "honami-node-exit",
            "honami-node-blend", "honami-node-random", "honami-node-animation",
            "honami-node-default", "honami-node-sequencer",
            "honami-node-portal-entrance", "honami-node-portal-exit",
            "honami-node-inherited", "honami-node-overridden"
        };
        private static string[] GetNodeClasses() => _nodeClasses;

        private static string GetNodeCssFromState(HonamiState state, HonamiNodeBase activeNode)
        {
            if (activeNode == null) return "honami-node-animation";
            return activeNode switch
            {
                HonamiAnyStateNode => "honami-node-any",
                HonamiRepeaterNode => "honami-node-repeater",
                HonamiExitNode => "honami-node-exit",
                HonamiBlendTreeNode => "honami-node-blend",
                HonamiRandomAnimationNode => "honami-node-random",
                HonamiSequencerNode => "honami-node-sequencer",
                HonamiPortalEntranceNode => "honami-node-portal-entrance",
                HonamiPortalExitNode => "honami-node-portal-exit",
                _ => "honami-node-animation"
            };
        }

        private void UpdatePorts()
        {
            HonamiNodeEditorBase editor = HonamiNodeEditorRegistry.GetEditor(ActiveNode);

            bool needsInput = editor?.HasInput ?? (ActiveNode is not HonamiAnyStateNode &&
                                                      ActiveNode is not HonamiRepeaterNode &&
                                                      ActiveNode is not HonamiPortalExitNode);
            bool needsOutput = editor?.HasOutput ?? (ActiveNode is not HonamiExitNode &&
                                                      ActiveNode is not HonamiPortalEntranceNode);

            if (needsInput)
            {
                if (InputPort == null)
                {
                    InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                    InputPort.portName = "";
                    InputPort.style.position = Position.Absolute;
                    InputPort.style.width = 0;
                    InputPort.style.height = 0;
                    InputPort.style.opacity = 0f;
                    InputPort.pickingMode = PickingMode.Ignore;
                }
                extensionContainer.Add(InputPort);
            }
            else if (InputPort != null)
            {
                var gv = this.GetFirstAncestorOfType<HonamiGraphView>();
                if (gv != null) gv.DeleteElements(InputPort.connections.Cast<GraphElement>().ToList());
                InputPort = null;
            }

            if (needsOutput)
            {
                if (OutputPort == null)
                {
                    OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                    OutputPort.portName = "";
                    OutputPort.style.position = Position.Absolute;
                    OutputPort.style.width = 0;
                    OutputPort.style.height = 0;
                    OutputPort.style.opacity = 0f;
                    OutputPort.pickingMode = PickingMode.Ignore;
                }
                extensionContainer.Add(OutputPort);
            }
            else if (OutputPort != null)
            {
                var gv = this.GetFirstAncestorOfType<HonamiGraphView>();
                if (gv != null) gv.DeleteElements(OutputPort.connections.Cast<GraphElement>().ToList());
                OutputPort = null;
            }
        }

        private VisualElement _progressContainer;
        private VisualElement _progressBar;

        private bool _lastActive = false;
        private float _lastProgress = -1f;

        public void UpdateRuntimeState(bool isActive, float progress)
        {
            if (isActive != _lastActive)
            {
                if (isActive) AddToClassList("honami-node-active");
                else RemoveFromClassList("honami-node-active");
                _lastActive = isActive;
            }

            if (_progressBar != null && Mathf.Abs(progress - _lastProgress) > 0.001f)
            {
                _progressBar.style.width = Length.Percent(progress * 100f);
                _lastProgress = progress;
            }
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.clickCount != 2) return;

            var graphView = this.GetFirstAncestorOfType<HonamiGraphView>();
            if (graphView == null) return;

            var controller = graphView.Controller;
            if (controller == null) return;

            HonamiNodeEditorBase editor = HonamiNodeEditorRegistry.GetEditor(_state.node);
            editor?.OnDoubleClick(_state, controller);
            if (editor != null) evt.StopPropagation();
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            if (_state != null)
            {
                if (_state.isVirtualInheritedState) return;
                if (_graphView == null && parent == null) return;

                Vector2 actualPos = newPos.position;
                var gv = _graphView;
                if (parent != null && gv != null && parent != gv.contentViewContainer)
                {
                    actualPos = parent.ChangeCoordinatesTo(gv.contentViewContainer, newPos.position);
                }

                if ((_state.editorPosition - actualPos).sqrMagnitude < 0.01f) return;
                _state.editorPosition = actualPos;
                EditorUtility.SetDirty(_state);
            }
        }

        private void PopulateContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (_state == null) return;
            if (_state.isVirtualInheritedState)
            {
                evt.menu.AppendAction("Inherited State", _ => { }, DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Create an override to add Sub-Nodes", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            var types = TypeCache.GetTypesDerivedFrom<HonamiSubNodeBase>()
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var t in types)
            {
                string name = t.Name.Replace("Honami", "").Replace("SubNode", "");
                name = ObjectNames.NicifyVariableName(name);
                evt.menu.AppendAction($"Add Sub-Node/{name}", _ => AddSubNode(t));
            }
        }

        private void AddSubNode(System.Type subNodeType)
        {
            if (_graphView == null || _state == null) return;
            var controller = _graphView.Controller;
            if (controller == null) return;

            HonamiSubNodeBase instance = ScriptableObject.CreateInstance(subNodeType) as HonamiSubNodeBase;
            instance.name = subNodeType.Name;

            HonamiAnimationSystem.Editor.Core.HonamiEditorController.AddSubNode(controller, _state, instance);

            HonamiGraphView.DeferredSave();


            RefreshView(_isDefault);

            _graphView.ClearSelection();
            _graphView.AddToSelection(this);
        }

        public void SetSubNodeSelected(HonamiSubNodeBase subNode)
        {
            foreach (var kvp in _subNodeBadges)
            {
                if (kvp.Key == subNode)
                {
                    kvp.Value.AddToClassList("honami-node-subnode-badge-selected");
                }
                else
                {
                    kvp.Value.RemoveFromClassList("honami-node-subnode-badge-selected");
                }
                HonamiGraphAccent.RefreshSubNodeBadge(kvp.Value);
            }
        }

        public override void OnSelected()
        {
            base.OnSelected();
            ApplySelectionAccent();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            ApplySelectionAccent();
        }

        private void ApplySelectionAccent()
        {
            var border = this.Q("node-border");
            if (border == null) return;
            bool accented = selected &&
                            !ClassListContains("honami-node-overridden") &&
                            !ClassListContains("honami-node-inherited");
            HonamiGraphAccent.SetSelectionBorder(border, accented);
        }

        public void RefreshAccent()
        {
            foreach (var badge in _subNodeBadges.Values) HonamiGraphAccent.RefreshSubNodeBadge(badge);
            ApplySelectionAccent();
        }
    }
}
