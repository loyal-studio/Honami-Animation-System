using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Handlers
{
    public sealed class HonamiLinkedAnimatorGraphHandler : HonamiGraphModeHandlerBase
    {
        private HonamiLinkedAnimatorGraphView _graphView;
        private HonamiLinkedAnimatorLeftPanel _leftPanel;

        public override string ModeId => "LinkedAnimator";

        protected override void OnInitialize()
        {

            _graphView = new HonamiLinkedAnimatorGraphView { name = "Honami Linked Animator Graph" };
            _graphView.style.flexGrow = 1;
            _graphView.InitBrainMode(Window);
            WireBrainGraphViewEvents();

            _leftPanel = new HonamiLinkedAnimatorLeftPanel(Window);
            _leftPanel.Root.style.flexGrow = 1;
        }

        public override VisualElement GetMainView() => _graphView;
        public override VisualElement GetLeftPanelView() => _leftPanel.Root;

        public override void OnEnable()
        {
            if (Window.LinkedGraph == null)
            {
                string last = EditorPrefs.GetString("Honami_LastOpenedBrainGraphPath", "");
                if (!string.IsNullOrEmpty(last))
                    Window.SetLinkedGraph(AssetDatabase.LoadAssetAtPath<HonamiLinkedAnimatorGraph>(last), null);
            }
            else
            {
                Window.SetLinkedGraph(Window.LinkedGraph, Window.RuntimeLinkedAnimator);
            }
        }


        public override void Update()
        {
            if (!UnityEngine.Application.isPlaying) return;

            if (_graphView != null && Window.RuntimeLinkedAnimator != null)
            {
                _graphView.UpdateBrainDebugVisuals(Window.RuntimeLinkedAnimator.ActiveNodes);
            }
        }

        public override void SwitchToMode()
        {
            Window.SupportLeftPanel(true);
            Window.SupportRightPanel(true);

            if (Window.TitleLabel != null)
                Window.TitleLabel.text = Window.LinkedGraph != null ? Window.LinkedGraph.name : "No Linked Graph";

            if (Window.OverrideBadge != null) Window.OverrideBadge.style.display = DisplayStyle.None;
            if (Window.LinkedBadge != null) Window.LinkedBadge.style.display = DisplayStyle.Flex;
            if (Window.Toolbar != null) Window.Toolbar.style.display = DisplayStyle.None;

            if (Window.LinkedGraph != null)
            {
                string path = AssetDatabase.GetAssetPath(Window.LinkedGraph);
                if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString("Honami_LastOpenedBrainGraphPath", path);
            }

            Window.SaveSelection();
            if (Window.LinkedGraph != null) _graphView.PopulateBrainView(Window.LinkedGraph);

            _leftPanel?.Rebuild();
            Window.RestoreSelection();
            Window.BuildRightPanel();
            Window.UpdateEmptyState();
        }

        public override void BuildRightPanel(VisualElement rightContent)
        {
            rightContent.Clear();
            rightContent.Unbind();

            if (Window.SelectedLinkedNode != null)
            {
                var so = new SerializedObject(Window.SelectedLinkedNode);
                var inspector = HonamiLinkedAnimatorInspector.BuildNodeInspector(
                    Window.SelectedLinkedNode,
                    so,
                    Window.LinkedGraph,
                    _graphView);

                rightContent.Add(inspector);
            }
            else if (Window.SelectedLinkedEvent != null)
            {
                var so = new SerializedObject(Window.SelectedLinkedEvent);
                var inspector = HonamiLinkedAnimatorInspector.BuildEventInspector(
                    Window.SelectedLinkedEvent,
                    so,
                    Window.LinkedGraph,
                    _graphView,
                    onNameChanged: () => _leftPanel?.Rebuild());

                rightContent.Add(inspector);
            }
            else
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();
                pad.Add(HonamiGraphStyles.Title("Linked Animator Inspector"));
                var box = HonamiGraphStyles.Box();
                box.Add(new HelpBox("Select an Event or Node to view its properties.", HelpBoxMessageType.Info));
                pad.Add(box);
                rightContent.Add(pad);
            }
        }

        public override void RebuildLeftPanel()
        {
            _leftPanel?.Rebuild();
        }

        public override void OnSelectionChange()
        {
            if (Window.IsLocked) return;

            if (Selection.activeGameObject != null)
            {
                var brain = Selection.activeGameObject.GetComponentInParent<HonamiLinkedAnimator>(true);
                if (brain != null && brain.Graph != null)
                {
                    Window.SetLinkedGraph(brain.Graph, brain);
                    return;
                }
            }

            if (Selection.activeObject is HonamiLinkedAnimatorGraph bg)
            {
                Window.SetLinkedGraph(bg, null);
            }
        }

        public override void OnUndoRedo()
        {
            Window.SaveSelection();
            _graphView?.PopulateBrainView(Window.LinkedGraph);
            _leftPanel?.Rebuild();
            Window.RestoreSelection();
            Window.BuildRightPanel();
        }

        public override void FrameAll() => _graphView?.FrameAll();
        public override bool HasContent() => Window.LinkedGraph != null;

        private void WireBrainGraphViewEvents()
        {
            _graphView.OnBrainNodeSelected = node =>
            {
                Window.SelectLinkedNode(node);
                Window.BuildRightPanel();
            };
            _graphView.OnBrainEventSelected = evt =>
            {
                Window.SelectLinkedEvent(evt);
                Window.BuildRightPanel();
            };
            _graphView.OnBrainStructureChanged = () => _leftPanel?.Rebuild();
        }
    }
}
