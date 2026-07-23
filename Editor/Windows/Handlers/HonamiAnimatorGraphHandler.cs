using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace HonamiAnimationSystem.Editor.Handlers
{
    public sealed class HonamiAnimatorGraphHandler : IHonamiGraphModeHandler
    {
        private HonamiGraphWindow _window;
        private Runtime.Core.HonamiAnimator _lastRuntimeAnimator;
        private HonamiGraphView _graphView;
        private HonamiLeftPanel _leftPanel;
        private readonly System.Collections.Generic.List<HonamiTransitionMultiInspector.Entry> _multiTransitions = new();

        public string ModeId => "Animator";

        public void Initialize(HonamiGraphWindow window)
        {
            _window = window;

            _graphView = new HonamiGraphView { name = "Honami Animator Graph" };
            _graphView.style.flexGrow = 1;
            _graphView.Init(_window);
            WireGraphViewEvents();

            _leftPanel = new HonamiLeftPanel(_window);
            _leftPanel.Root.style.flexGrow = 1;
        }

        public VisualElement GetMainView() => _graphView;
        public VisualElement GetLeftPanelView() => _leftPanel.Root;

        public void OnEnable()
        {
            if (_window.RuntimeController == null)
            {
                string last = EditorPrefs.GetString("Honami_LastOpenedControllerPath", "");
                if (!string.IsNullOrEmpty(last))
                    _window.SetController(AssetDatabase.LoadAssetAtPath<Runtime.Core.HonamiRuntimeController>(last));
            }
            else
            {
                _window.SetController(_window.RuntimeController);
            }
            ApplySettings();
        }

        public void OnDisable() { }

        public void Update()
        {
            if (!UnityEngine.Application.isPlaying) return;

            if (_window.Controller == null) return;

            if (!_window.IsLocked)
            {
                var found = ResolveSelectedAnimator();
                if (IsCompatible(found)) _window.RuntimeAnimator = found;
            }

            if (!IsCompatible(_window.RuntimeAnimator) || !_window.RuntimeAnimator.gameObject.activeInHierarchy)
            {
                _window.RuntimeAnimator = null;
                if (Time.realtimeSinceStartup > _window.NextAnimatorSearchTime)
                {
                    _window.NextAnimatorSearchTime = Time.realtimeSinceStartup + 1.2f;
                    _window.RuntimeAnimator = Object.FindObjectsByType<Runtime.Core.HonamiAnimator>(FindObjectsSortMode.None)
                        .FirstOrDefault(a => a.gameObject.activeInHierarchy && IsCompatible(a));
                }
            }

            if (_lastRuntimeAnimator != _window.RuntimeAnimator)
            {
                _lastRuntimeAnimator = _window.RuntimeAnimator;
                RebuildLeftPanel();
            }

            if (_graphView != null)
            {
                if (_window.RuntimeAnimator != null)
                {
                    _graphView.UpdateRuntimeVisualization(_window.RuntimeAnimator);
                    if (EditorWindow.focusedWindow == _window || EditorWindow.mouseOverWindow == _window) _window.Repaint();
                }
                else
                {
                    _graphView.ClearRuntimeVisualization();
                }
            }
        }

        public void SwitchToMode()
        {
            _window.SupportLeftPanel(true);
            _window.SupportRightPanel(true);

            if (_window.TitleLabel != null)
            {
                _window.TitleLabel.text = _window.RuntimeController != null ? _window.RuntimeController.name : "No Controller Selected";

                if (_window.OverrideBadge != null)
                    _window.OverrideBadge.style.display = (_window.RuntimeController != null && _window.RuntimeController.IsOverride) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_window.LinkedBadge != null) _window.LinkedBadge.style.display = DisplayStyle.None;
            if (_window.Toolbar != null) _window.Toolbar.style.display = DisplayStyle.Flex;

            if (_window.RuntimeController != null)
            {
                string path = AssetDatabase.GetAssetPath(_window.RuntimeController);
                if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString("Honami_LastOpenedControllerPath", path);
            }

            if (_window.Toolbar != null)
            {
                var cf = _window.Toolbar.Q<ObjectField>();
                cf?.SetValueWithoutNotify(_window.RuntimeController);
            }

            _window.SaveSelection();
            if (_window.RuntimeController is Runtime.Core.HonamiOverrideController overrideToResync && overrideToResync.parentController != null)
            {
                HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.ResyncFromParent(overrideToResync);
            }
            if (_window.RuntimeController != null) _graphView.PopulateView(_window.RuntimeController, _window.CurrentLayerIndex);

            _leftPanel?.Rebuild();
            _window.RestoreSelection();
            _window.BuildRightPanel();
            _window.UpdateEmptyState();
        }

        public void BuildRightPanel(VisualElement rightContent)
        {
            rightContent.Clear();
            rightContent.Unbind();

            if (_window.SelectedSubNode != null && _window.SerializedSubNode?.targetObject != null)
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();

                pad.Add(HonamiGraphStyles.Title(_window.SelectedSubNode.GetType().Name.Replace("Honami", "")));
                pad.Add(HonamiGraphStyles.MiniLabel("Sub-Node Configuration", HonamiGraphStyles.SubTitleClr));

                var subOverride = _window.RuntimeController as Runtime.Core.HonamiOverrideController;
                Runtime.Core.HonamiOverrideEntry subEntry = null;
                bool subIsOverride = subOverride != null &&
                    HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.FindSubNodeOwner(subOverride, _window.SelectedSubNode, out subEntry);
                var capturedSubNode = _window.SelectedSubNode;
                string capturedSubId = capturedSubNode != null ? capturedSubNode.OverrideId : null;

                if (subIsOverride)
                {
                    var ovRow = HonamiGraphStyles.Row();
                    ovRow.Add(new Label("OVERRIDE") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, color = new Color(0.9f, 0.49f, 0.13f) } });
                    ovRow.Add(HonamiGraphStyles.Spacer());
                    var revertSub = HonamiGraphStyles.SmallButton("Revert");
                    revertSub.style.width = 70;
                    revertSub.clicked += () =>
                    {
                        HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.RevertSubNode(subOverride, subEntry, capturedSubId);
                        _graphView.PopulateView(_window.RuntimeController, _window.CurrentLayerIndex);
                        _window.SelectedSubNode = null;
                        _window.BuildRightPanel();
                    };
                    ovRow.Add(revertSub);
                    pad.Add(ovRow);
                }

                var box = HonamiGraphStyles.Box();
                var inspector = new InspectorElement(_window.SerializedSubNode);
                inspector.style.paddingTop = 4;
                box.Add(inspector);
                pad.Add(box);

                if (subIsOverride)
                {
                    pad.TrackSerializedObjectValue(_window.SerializedSubNode, _ =>
                        HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.RefreshSubNodeModified(subOverride, subEntry, capturedSubNode));
                }

                Button removeBtn = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog("Remove Sub-Node", $"Are you sure you want to remove '{_window.SelectedSubNode.name}'?", "Remove", "Cancel"))
                    {
                        if (subIsOverride)
                        {
                            HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.RemoveSubNodeFromOverride(subOverride, subEntry, capturedSubId);
                            _graphView.PopulateView(_window.RuntimeController, _window.CurrentLayerIndex);
                            _window.SelectedSubNode = null;
                            _window.BuildRightPanel();
                            return;
                        }

                        Undo.RecordObject(_window.SelectedNode.State, "Remove Sub-Node");
                        var list = _window.SelectedNode.State.subNodes;
                        if (list.Contains(_window.SelectedSubNode))
                        {
                            list.Remove(_window.SelectedSubNode);
                            Undo.DestroyObjectImmediate(_window.SelectedSubNode);
                            EditorUtility.SetDirty(_window.SelectedNode.State);
                            _window.SelectedSubNode = null;
                            _window.SelectedNode.RefreshView(false);
                            _window.BuildRightPanel();
                        }
                    }
                })
                { text = "Remove Sub-Node" };
                removeBtn.style.marginTop = 8;
                removeBtn.style.height = 24;
                removeBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.15f);
                removeBtn.style.borderLeftWidth = removeBtn.style.borderRightWidth =
                removeBtn.style.borderTopWidth = removeBtn.style.borderBottomWidth = 0;
                removeBtn.style.color = new Color(1f, 0.4f, 0.4f);
                pad.Add(removeBtn);

                rightContent.Add(pad);
            }
            else if (_window.SelectedNode?.State != null && _window.SerializedState?.targetObject != null)
            {
                var inspector = HonamiStateInspector.Build(
                    _window.SelectedNode.State,
                    _window.SerializedState,
                    _window.Controller,
                    _window.CurrentLayerIndex,
                    _window.SelectedNode,
                    _graphView,
                    onRebuildRequired: () => _window.BuildRightPanel());

                rightContent.Add(inspector);
            }
            else if (_window.SelectedNode?.State != null && _window.SelectedNode.State.isVirtualInheritedState)
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();
                pad.Add(HonamiGraphStyles.Title(_window.SelectedNode.State.stateName));
                pad.Add(HonamiGraphStyles.MiniLabel("Inherited State", new Color(0.7f, 0.7f, 0.7f)));

                var box = HonamiGraphStyles.Box();
                var source = _window.SelectedNode.State.inheritedSourceState;
                box.Add(new HelpBox(
                    source != null
                        ? $"This state is inherited from '{source.stateName}'. Create an override to edit this layer's state or transitions."
                        : "This state is inherited. Create an override to edit it.",
                    HelpBoxMessageType.Info));
                pad.Add(box);

                Button overrideBtn = new Button(() =>
                {
                    _graphView.CreateInheritedStateOverride(_window.SelectedNode);
                    _window.BuildRightPanel();
                })
                { text = "Create Override" };
                overrideBtn.style.height = 26;
                overrideBtn.style.marginTop = 8;
                pad.Add(overrideBtn);

                rightContent.Add(pad);
            }
            else if (_multiTransitions.Count >= 2 && IsMultiSelectionValid())
            {
                rightContent.Add(HonamiTransitionMultiInspector.Build(_multiTransitions));
            }
            else if (_window.SelectedTransition != null && _window.SelectedTransitionOwner != null &&
                     _window.SerializedState?.targetObject != null)
            {
                var inspector = HonamiTransitionInspector.Build(
                    _window.SelectedTransition,
                    _window.SelectedTransitionIndex,
                    _window.SelectedTransitionOwner,
                    _window.SerializedState,
                    _window.Controller,
                    _graphView);

                rightContent.Add(inspector);
            }
            else
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();

                pad.Add(HonamiGraphStyles.Title("Graph Inspector"));
                var box = HonamiGraphStyles.Box();
                box.Add(new HelpBox("Select a State Node, Sub-Node badge, or Transition Edge to view its settings.",
                    HelpBoxMessageType.Info));
                pad.Add(box);
                rightContent.Add(pad);
            }
        }

        public void RebuildLeftPanel()
        {
            _leftPanel?.Rebuild();
        }

        private bool IsMultiSelectionValid()
        {
            foreach (var entry in _multiTransitions)
            {
                if (entry.Owner == null) return false;

                var transitions = _window.RuntimeController != null
                    ? _window.RuntimeController.GetTransitions(entry.Owner)
                    : entry.Owner.transitions;
                if (transitions == null) return false;

                bool found = false;
                for (int i = 0; i < transitions.Count; i++)
                {
                    if (transitions[i] == entry.Transition)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        public void OnSelectionChange()
        {
            if (_window.IsLocked) return;

            var anim = ResolveSelectedAnimator();
            if (anim != null)
            {
                if (_window.RuntimeController != anim.CurrentController) _window.SetController(anim.CurrentController);
                return;
            }

            if (Selection.activeObject is Runtime.Core.HonamiRuntimeController ca)
            {
                if (_window.RuntimeController != ca) _window.SetController(ca);
            }
        }

        private static Runtime.Core.HonamiAnimator ResolveSelectedAnimator()
        {
            var go = Selection.activeGameObject;
            return go != null ? go.GetComponentInParent<Runtime.Core.HonamiAnimator>(true) : null;
        }

        private bool IsCompatible(Runtime.Core.HonamiAnimator anim)
        {
            if (anim == null) return false;

            var current = anim.CurrentController;
            if (current == null) return false;
            if (current == _window.RuntimeController) return true;

            return _window.Controller != null && current.BaseController == _window.Controller;
        }

        public void OnUndoRedo()
        {
            _window.SaveSelection();
            _window.SerializedController?.Update();
            _window.SerializedState?.Update();
            _graphView?.PopulateView(_window.RuntimeController, _window.CurrentLayerIndex);
            _leftPanel?.Rebuild();
            _window.RestoreSelection();
            _window.BuildRightPanel();
        }

        public void FrameAll() => _graphView?.FrameAll();
        public bool HasContent() => _window.RuntimeController != null;

        public void ApplySettings()
        {
            if (_lastRuntimeAnimator != _window.RuntimeAnimator)
            {
                _lastRuntimeAnimator = _window.RuntimeAnimator;
                RebuildLeftPanel();
            }

            if (_graphView != null)
            {
                _graphView.IsMiniMapVisible = HonamiGraphSettings.ShowMinimap;
                _graphView.SetAnimationsEnabled(HonamiGraphSettings.EnableAnimations);
                _graphView.SetTactile3DEnabled(HonamiGraphSettings.EnableTactile3D);
                _graphView.SetGridVisible(HonamiGraphSettings.ShowGrid);
            }
        }

        private void WireGraphViewEvents()
        {
            _graphView.OnNodeSelected += node =>
            {
                _multiTransitions.Clear();
                _window.SelectedNode = node;
                _window.SelectedNodeGuid = node?.StateGuid;
                _window.SelectedTransition = null;
                _window.SelectedTransitionGuid = null;
                _window.SelectedTransitionOwner = null;
                _window.SelectedSubNode = null;
                _window.SerializedSubNode = null;
                _window.SerializedState = node?.State != null && !node.State.isVirtualInheritedState ? new SerializedObject(node.State) : null;

                if (_window.Controller != null && node?.State != null &&
                    (node.State.node is Runtime.Core.HonamiAnimationNode ||
                     node.State.node is Runtime.Core.HonamiBlendTreeNode ||
                     node.State.node is Runtime.Core.HonamiRandomAnimationNode ||
                     node.State.node is Runtime.Core.HonamiSequencerNode) &&
                    EditorWindow.HasOpenInstances<HonamiAnimationSystem.Editor.Timeline.HonamiTimelineWindow>())
                {
                    HonamiAnimationSystem.Editor.Timeline.HonamiTimelineWindow.InspectState(_window.Controller, node.State.guid);
                }
                _window.BuildRightPanel();
            };

            _graphView.OnSubNodeSelected += (node, subNode) =>
            {
                _multiTransitions.Clear();
                _window.SelectedNode = node;
                _window.SelectedNodeGuid = node?.StateGuid;
                _window.SelectedTransition = null;
                _window.SelectedTransitionGuid = null;

                if (_window.RuntimeController is Runtime.Core.HonamiOverrideController subOverride &&
                    node?.State != null && !subOverride.IsOwnedState(node.State) && subNode != null)
                {
                    int idx = node.State.subNodes != null ? node.State.subNodes.IndexOf(subNode) : -1;
                    var eff = HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.EnsureEffectiveState(subOverride, node.State);
                    if (idx >= 0 && eff.subNodes != null && idx < eff.subNodes.Count && eff.subNodes[idx] != null)
                    {
                        subNode = eff.subNodes[idx];
                    }
                }

                _window.SelectedSubNode = subNode;
                _window.SerializedSubNode = subNode != null ? new SerializedObject(subNode) : null;
                _window.SerializedState = node?.State != null ? new SerializedObject(node.State) : null;
                _window.BuildRightPanel();
            };

            _graphView.OnEdgeSelected += (edge, transition) =>
            {
                _multiTransitions.Clear();
                _window.SelectedNode = null;
                _window.SelectedNodeGuid = null;
                _window.SelectedTransition = transition;
                _window.SelectedTransitionGuid = transition?.targetStateGuid;
                _window.SelectedTransitionIndex = -1;
                _window.SelectedTransitionOwner = null;

                if (_window.Controller != null)
                {
                    System.Collections.Generic.IReadOnlyList<Runtime.Core.HonamiState> activeStates =
                        _window.RuntimeController != null ? _window.RuntimeController.ActiveStates : _window.Controller.states;
                    foreach (var s in activeStates)
                    {
                        if (s == null) continue;

                        var transitions = _window.RuntimeController != null ? _window.RuntimeController.GetTransitions(s) : s.transitions;
                        if (transitions == null) continue;

                        int idx = -1;
                        for (int i = 0; i < transitions.Count; i++)
                        {
                            if (transitions[i] == transition)
                            {
                                idx = i;
                                break;
                            }
                        }
                        if (idx < 0) continue;
                        _window.SelectedTransitionOwner = s;
                        _window.SelectedTransitionIndex = idx;
                        _window.SerializedState = !s.isVirtualInheritedState ? new SerializedObject(s) : null;
                        break;
                    }
                }
                _window.BuildRightPanel();
            };

            _graphView.OnEdgesSelected += transitions =>
            {
                _window.SelectedNode = null;
                _window.SelectedNodeGuid = null;
                _window.SelectedTransition = null;
                _window.SelectedTransitionGuid = null;
                _window.SelectedTransitionIndex = -1;
                _window.SelectedTransitionOwner = null;
                _window.SelectedSubNode = null;
                _window.SerializedSubNode = null;
                _window.SerializedState = null;

                _multiTransitions.Clear();
                if (_window.Controller != null)
                {
                    System.Collections.Generic.IReadOnlyList<Runtime.Core.HonamiState> activeStates =
                        _window.RuntimeController != null ? _window.RuntimeController.ActiveStates : _window.Controller.states;

                    foreach (var transition in transitions)
                    {
                        Runtime.Core.HonamiState owner = null;
                        foreach (var s in activeStates)
                        {
                            if (s == null || s.isVirtualInheritedState) continue;

                            var stateTransitions = _window.RuntimeController != null ? _window.RuntimeController.GetTransitions(s) : s.transitions;
                            if (stateTransitions == null) continue;

                            for (int i = 0; i < stateTransitions.Count; i++)
                            {
                                if (stateTransitions[i] == transition)
                                {
                                    owner = s;
                                    break;
                                }
                            }
                            if (owner != null) break;
                        }
                        if (owner == null) continue;

                        Runtime.Core.HonamiState target = null;
                        if (!string.IsNullOrEmpty(transition.targetStateGuid))
                        {
                            foreach (var s in activeStates)
                            {
                                if (s != null && s.guid == transition.targetStateGuid)
                                {
                                    target = s;
                                    break;
                                }
                            }
                        }

                        _multiTransitions.Add(new HonamiTransitionMultiInspector.Entry(transition, owner, target));
                    }
                }
                _window.BuildRightPanel();
            };

            _graphView.OnDeselected += () =>
            {
                _multiTransitions.Clear();
                _window.SelectedNode = null;
                _window.SelectedNodeGuid = null;
                _window.SelectedTransition = null;
                _window.SelectedTransitionGuid = null;
                _window.SelectedTransitionOwner = null;
                _window.SelectedSubNode = null;
                _window.SerializedSubNode = null;
                _window.SerializedState = null;
                _window.BuildRightPanel();
            };
        }
    }
}

