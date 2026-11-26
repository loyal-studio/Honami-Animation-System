using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Handlers
{
    public sealed class HonamiAnimatorGraphHandler : HonamiGraphModeHandlerBase
    {
        private HonamiAnimator _lastRuntimeAnimator;
        private HonamiGraphView _graphView;
        private HonamiLeftPanel _leftPanel;
        private readonly System.Collections.Generic.List<HonamiTransitionMultiInspector.Entry> _multiTransitions = new();

        public override string ModeId => "Animator";

        protected override void OnInitialize()
        {

            _graphView = new HonamiGraphView { name = "Honami Animator Graph" };
            _graphView.style.flexGrow = 1;
            _graphView.Init(Window);
            WireGraphViewEvents();

            _leftPanel = new HonamiLeftPanel(Window);
            _leftPanel.Root.style.flexGrow = 1;
        }

        public override VisualElement GetMainView() => _graphView;
        public override VisualElement GetLeftPanelView() => _leftPanel.Root;

        public override void OnEnable()
        {
            if (Window.RuntimeController == null)
            {
                string last = EditorPrefs.GetString("Honami_LastOpenedControllerPath", "");
                if (!string.IsNullOrEmpty(last))
                    Window.SetController(AssetDatabase.LoadAssetAtPath<HonamiRuntimeController>(last));
            }
            else
            {
                Window.SetController(Window.RuntimeController);
            }
            ApplySettings();
        }


        public override void Update()
        {
            if (!UnityEngine.Application.isPlaying) return;

            if (Window.Controller == null) return;

            if (!Window.IsLocked)
            {
                var found = ResolveSelectedAnimator();
                if (IsCompatible(found)) Window.RuntimeAnimator = found;
            }

            if (!IsCompatible(Window.RuntimeAnimator) || !Window.RuntimeAnimator.gameObject.activeInHierarchy)
            {
                Window.RuntimeAnimator = null;
                if (Time.realtimeSinceStartup > Window.NextAnimatorSearchTime)
                {
                    Window.NextAnimatorSearchTime = Time.realtimeSinceStartup + 1.2f;
                    Window.RuntimeAnimator = Object.FindObjectsByType<HonamiAnimator>(FindObjectsSortMode.None)
                        .FirstOrDefault(a => a.gameObject.activeInHierarchy && IsCompatible(a));
                }
            }

            if (_lastRuntimeAnimator != Window.RuntimeAnimator)
            {
                _lastRuntimeAnimator = Window.RuntimeAnimator;
                RebuildLeftPanel();
            }

            if (_graphView != null)
            {
                if (Window.RuntimeAnimator != null)
                {
                    _graphView.UpdateRuntimeVisualization(Window.RuntimeAnimator);
                    if (EditorWindow.focusedWindow == Window || EditorWindow.mouseOverWindow == Window) Window.Repaint();
                }
                else
                {
                    _graphView.ClearRuntimeVisualization();
                }
            }
        }

        public override void SwitchToMode()
        {
            Window.SupportLeftPanel(true);
            Window.SupportRightPanel(true);

            if (Window.TitleLabel != null)
            {
                Window.TitleLabel.text = Window.RuntimeController != null ? Window.RuntimeController.name : "No Controller Selected";

                if (Window.OverrideBadge != null)
                    Window.OverrideBadge.style.display = (Window.RuntimeController != null && Window.RuntimeController.IsOverride) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (Window.LinkedBadge != null) Window.LinkedBadge.style.display = DisplayStyle.None;
            if (Window.Toolbar != null) Window.Toolbar.style.display = DisplayStyle.Flex;

            if (Window.RuntimeController != null)
            {
                string path = AssetDatabase.GetAssetPath(Window.RuntimeController);
                if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString("Honami_LastOpenedControllerPath", path);
            }

            if (Window.Toolbar != null)
            {
                var cf = Window.Toolbar.Q<ObjectField>();
                cf?.SetValueWithoutNotify(Window.RuntimeController);
            }

            Window.SaveSelection();
            if (Window.RuntimeController is HonamiOverrideController overrideToResync && overrideToResync.parentController != null)
            {
                HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.ResyncFromParent(overrideToResync);
            }
            if (Window.RuntimeController != null) _graphView.PopulateView(Window.RuntimeController, Window.CurrentLayerIndex);

            _leftPanel?.Rebuild();
            Window.RestoreSelection();
            Window.BuildRightPanel();
            Window.UpdateEmptyState();
        }

        public override void BuildRightPanel(VisualElement rightContent)
        {
            rightContent.Clear();
            rightContent.Unbind();

            if (Window.SelectedSubNode != null && Window.SerializedSubNode?.targetObject != null)
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();

                pad.Add(HonamiGraphStyles.Title(Window.SelectedSubNode.GetType().Name.Replace("Honami", "")));
                pad.Add(HonamiGraphStyles.MiniLabel("Sub-Node Configuration", HonamiGraphStyles.SubTitleClr));

                var subOverride = Window.RuntimeController as HonamiOverrideController;
                HonamiOverrideEntry subEntry = null;
                bool subIsOverride = subOverride != null &&
                    HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.FindSubNodeOwner(subOverride, Window.SelectedSubNode, out subEntry);
                var capturedSubNode = Window.SelectedSubNode;
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
                        _graphView.PopulateView(Window.RuntimeController, Window.CurrentLayerIndex);
                        Window.ClearSelectedSubNode();
                        Window.BuildRightPanel();
                    };
                    ovRow.Add(revertSub);
                    pad.Add(ovRow);
                }

                var box = HonamiGraphStyles.Box();
                var inspector = new InspectorElement(Window.SerializedSubNode);
                inspector.style.paddingTop = 4;
                box.Add(inspector);
                pad.Add(box);

                if (subIsOverride)
                {
                    pad.TrackSerializedObjectValue(Window.SerializedSubNode, _ =>
                        HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.RefreshSubNodeModified(subOverride, subEntry, capturedSubNode));
                }

                Button removeBtn = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog("Remove Sub-Node", $"Are you sure you want to remove '{Window.SelectedSubNode.name}'?", "Remove", "Cancel"))
                    {
                        if (subIsOverride)
                        {
                            HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.RemoveSubNodeFromOverride(subOverride, subEntry, capturedSubId);
                            _graphView.PopulateView(Window.RuntimeController, Window.CurrentLayerIndex);
                            Window.ClearSelectedSubNode();
                            Window.BuildRightPanel();
                            return;
                        }

                        Undo.RecordObject(Window.SelectedNode.State, "Remove Sub-Node");
                        var list = Window.SelectedNode.State.subNodes;
                        if (list.Contains(Window.SelectedSubNode))
                        {
                            list.Remove(Window.SelectedSubNode);
                            Undo.DestroyObjectImmediate(Window.SelectedSubNode);
                            EditorUtility.SetDirty(Window.SelectedNode.State);
                            Window.ClearSelectedSubNode();
                            Window.SelectedNode.RefreshView(false);
                            Window.BuildRightPanel();
                        }
                    }
                })
                { text = "Remove Sub-Node" };
                removeBtn.style.marginTop = 8;
                removeBtn.style.height = 24;
                removeBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.15f);
                removeBtn.BorderWidth(0);
                removeBtn.style.color = new Color(1f, 0.4f, 0.4f);
                pad.Add(removeBtn);

                rightContent.Add(pad);
            }
            else if (Window.SelectedNode?.State != null && Window.SerializedState?.targetObject != null)
            {
                var inspector = HonamiStateInspector.Build(
                    Window.SelectedNode.State,
                    Window.SerializedState,
                    Window.Controller,
                    Window.CurrentLayerIndex,
                    Window.SelectedNode,
                    _graphView,
                    onRebuildRequired: () => Window.BuildRightPanel());

                rightContent.Add(inspector);
            }
            else if (Window.SelectedNode?.State != null && Window.SelectedNode.State.isVirtualInheritedState)
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();
                pad.Add(HonamiGraphStyles.Title(Window.SelectedNode.State.stateName));
                pad.Add(HonamiGraphStyles.MiniLabel("Inherited State", new Color(0.7f, 0.7f, 0.7f)));

                var box = HonamiGraphStyles.Box();
                var source = Window.SelectedNode.State.inheritedSourceState;
                box.Add(new HelpBox(
                    source != null
                        ? $"This state is inherited from '{source.stateName}'. Create an override to edit this layer's state or transitions."
                        : "This state is inherited. Create an override to edit it.",
                    HelpBoxMessageType.Info));
                pad.Add(box);

                Button overrideBtn = new Button(() =>
                {
                    _graphView.CreateInheritedStateOverride(Window.SelectedNode);
                    Window.BuildRightPanel();
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
            else if (Window.SelectedTransition != null && Window.SelectedTransitionOwner != null &&
                    Window.SerializedState?.targetObject != null)
            {
                var inspector = HonamiTransitionInspector.Build(
                    Window.SelectedTransition,
                    Window.SelectedTransitionIndex,
                    Window.SelectedTransitionOwner,
                    Window.SerializedState,
                    Window.Controller,
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

        public override void RebuildLeftPanel()
        {
            _leftPanel?.Rebuild();
        }

        private static int FindTransitionIndex(System.Collections.Generic.IReadOnlyList<HonamiTransition> transitions, HonamiTransition target)
        {
            if (transitions == null || target == null) return -1;

            for (int i = 0; i < transitions.Count; i++)
            {
                if (transitions[i] == target) return i;
            }

            if (!string.IsNullOrEmpty(target.id))
            {
                for (int i = 0; i < transitions.Count; i++)
                {
                    if (transitions[i] != null && transitions[i].id == target.id) return i;
                }
            }

            return -1;
        }

        private bool IsMultiSelectionValid()
        {
            foreach (var entry in _multiTransitions)
            {
                if (entry.Owner == null) return false;

                var transitions = Window.RuntimeController != null
                    ? Window.RuntimeController.GetTransitions(entry.Owner)
                    : entry.Owner.transitions;
                if (transitions == null) return false;

                if (FindTransitionIndex(transitions, entry.Transition) < 0) return false;
            }
            return true;
        }

        public override void OnSelectionChange()
        {
            if (Window.IsLocked) return;

            var anim = ResolveSelectedAnimator();
            if (anim != null)
            {
                if (Window.RuntimeController != anim.CurrentController) Window.SetController(anim.CurrentController);
                return;
            }

            if (Selection.activeObject is HonamiRuntimeController ca)
            {
                if (Window.RuntimeController != ca) Window.SetController(ca);
            }
        }

        private static HonamiAnimator ResolveSelectedAnimator()
        {
            var go = Selection.activeGameObject;
            return go != null ? go.GetComponentInParent<HonamiAnimator>(true) : null;
        }

        private bool IsCompatible(HonamiAnimator anim)
        {
            if (anim == null) return false;

            var current = anim.CurrentController;
            if (current == null) return false;
            if (current == Window.RuntimeController) return true;

            return Window.Controller != null && current.BaseController == Window.Controller;
        }

        public override void OnUndoRedo()
        {
            Window.SaveSelection();
            if (Window.SerializedController != null && Window.SerializedController.targetObject != null)
                Window.SerializedController.Update();
            if (Window.SerializedState != null && Window.SerializedState.targetObject != null)
                Window.SerializedState.Update();
            _graphView?.PopulateView(Window.RuntimeController, Window.CurrentLayerIndex);
            _leftPanel?.Rebuild();
            Window.RestoreSelection();
            Window.BuildRightPanel();
        }

        public override void FrameAll() => _graphView?.FrameAll();
        public override bool HasContent() => Window.RuntimeController != null;

        public override void ApplySettings()
        {
            if (_lastRuntimeAnimator != Window.RuntimeAnimator)
            {
                _lastRuntimeAnimator = Window.RuntimeAnimator;
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
                Window.SelectNode(node);

                if (Window.Controller != null && node?.State != null &&
                    (node.State.node is HonamiAnimationNode ||
                    node.State.node is HonamiBlendTreeNode ||
                    node.State.node is HonamiRandomAnimationNode ||
                    node.State.node is HonamiSequencerNode) &&
                    EditorWindow.HasOpenInstances<HonamiAnimationSystem.Editor.Timeline.HonamiTimelineWindow>())
                {
                    HonamiAnimationSystem.Editor.Timeline.HonamiTimelineWindow.InspectState(Window.RuntimeController, node.State.guid);
                }
                Window.BuildRightPanel();
            };

            _graphView.OnSubNodeSelected += (node, subNode) =>
            {
                _multiTransitions.Clear();

                if (Window.RuntimeController is HonamiOverrideController subOverride &&
                    node?.State != null && !subOverride.IsOwnedState(node.State) && subNode != null)
                {
                    int idx = node.State.subNodes != null ? node.State.subNodes.IndexOf(subNode) : -1;
                    var eff = HonamiAnimationSystem.Editor.Core.HonamiOverrideAuthoring.EnsureEffectiveState(subOverride, node.State);
                    if (idx >= 0 && eff.subNodes != null && idx < eff.subNodes.Count && eff.subNodes[idx] != null)
                    {
                        subNode = eff.subNodes[idx];
                    }
                }

                Window.SelectSubNode(node, subNode);
                Window.BuildRightPanel();
            };

            _graphView.OnEdgeSelected += (edge, transition) =>
            {
                _multiTransitions.Clear();
                Window.SelectTransition(transition);

                if (Window.Controller != null)
                {
                    System.Collections.Generic.IReadOnlyList<HonamiState> activeStates =
                        Window.RuntimeController != null ? Window.RuntimeController.ActiveStates : Window.Controller.states;
                    foreach (var s in activeStates)
                    {
                        if (s == null) continue;

                        var transitions = Window.RuntimeController != null ? Window.RuntimeController.GetTransitions(s) : s.transitions;
                        if (transitions == null) continue;

                        int idx = FindTransitionIndex(transitions, transition);
                        if (idx < 0) continue;
                        Window.SetTransitionOwner(s, idx, transitions[idx]);
                        break;
                    }
                }
                Window.BuildRightPanel();
            };

            _graphView.OnEdgesSelected += transitions =>
            {
                Window.ClearSelection();
                _multiTransitions.Clear();
                if (Window.Controller != null)
                {
                    System.Collections.Generic.IReadOnlyList<HonamiState> activeStates =
                        Window.RuntimeController != null ? Window.RuntimeController.ActiveStates : Window.Controller.states;

                    foreach (var transition in transitions)
                    {
                        HonamiState owner = null;
                        HonamiTransition resolvedTransition = transition;
                        foreach (var s in activeStates)
                        {
                            if (s == null || s.isVirtualInheritedState) continue;

                            var stateTransitions = Window.RuntimeController != null ? Window.RuntimeController.GetTransitions(s) : s.transitions;
                            if (stateTransitions == null) continue;

                            int mi = FindTransitionIndex(stateTransitions, transition);
                            if (mi >= 0)
                            {
                                owner = s;
                                resolvedTransition = stateTransitions[mi];
                                break;
                            }
                        }
                        if (owner == null) continue;

                        HonamiState target = null;
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

                        _multiTransitions.Add(new HonamiTransitionMultiInspector.Entry(resolvedTransition, owner, target));
                    }
                }
                Window.BuildRightPanel();
            };

            _graphView.OnDeselected += () =>
            {
                _multiTransitions.Clear();
                Window.ClearSelection();
                Window.BuildRightPanel();
            };
        }
    }
}

