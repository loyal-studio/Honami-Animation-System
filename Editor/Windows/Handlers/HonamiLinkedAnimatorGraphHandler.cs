using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace HonamiAnimationSystem.Editor.Handlers
{
    public sealed class HonamiLinkedAnimatorGraphHandler : IHonamiGraphModeHandler
    {
        private HonamiGraphWindow _window;
        private HonamiLinkedAnimatorGraphView _graphView;
        private HonamiLinkedAnimatorLeftPanel _leftPanel;

        public string ModeId => "LinkedAnimator";

        public void Initialize(HonamiGraphWindow window)
        {
            _window = window;

            _graphView = new HonamiLinkedAnimatorGraphView { name = "Honami Linked Animator Graph" };
            _graphView.style.flexGrow = 1;
            _graphView.InitBrainMode(_window);
            WireBrainGraphViewEvents();

            _leftPanel = new HonamiLinkedAnimatorLeftPanel(_window);
            _leftPanel.Root.style.flexGrow = 1;
        }

        public VisualElement GetMainView() => _graphView;
        public VisualElement GetLeftPanelView() => _leftPanel.Root;

        public void OnEnable()
        {
            if (_window.LinkedGraph == null)
            {
                string last = EditorPrefs.GetString("Honami_LastOpenedBrainGraphPath", "");
                if (!string.IsNullOrEmpty(last))
                    _window.SetLinkedGraph(AssetDatabase.LoadAssetAtPath<Runtime.Core.HonamiLinkedAnimatorGraph>(last), null);
            }
            else
            {
                _window.SetLinkedGraph(_window.LinkedGraph, _window.RuntimeLinkedAnimator);
            }
        }

        public void OnDisable() { }

        public void ApplySettings() { }

        public void Update()
        {
            if (!UnityEngine.Application.isPlaying) return;

            if (_graphView != null && _window.RuntimeLinkedAnimator != null)
            {
                _graphView.UpdateBrainDebugVisuals(_window.RuntimeLinkedAnimator.ActiveNodes);
            }
        }

        public void SwitchToMode()
        {
            _window.SupportLeftPanel(true);
            _window.SupportRightPanel(true);

            if (_window.TitleLabel != null)
                _window.TitleLabel.text = _window.LinkedGraph != null ? _window.LinkedGraph.name : "No Linked Graph";

            if (_window.OverrideBadge != null) _window.OverrideBadge.style.display = DisplayStyle.None;
            if (_window.LinkedBadge != null) _window.LinkedBadge.style.display = DisplayStyle.Flex;
            if (_window.Toolbar != null) _window.Toolbar.style.display = DisplayStyle.None;

            if (_window.LinkedGraph != null)
            {
                string path = AssetDatabase.GetAssetPath(_window.LinkedGraph);
                if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString("Honami_LastOpenedBrainGraphPath", path);
            }

            _window.SaveSelection();
            if (_window.LinkedGraph != null) _graphView.PopulateBrainView(_window.LinkedGraph);

            _leftPanel?.Rebuild();
            _window.RestoreSelection();
            _window.BuildRightPanel();
            _window.UpdateEmptyState();
        }

        public void BuildRightPanel(VisualElement rightContent)
        {
            rightContent.Clear();
            rightContent.Unbind();

            if (_window.SelectedLinkedNode != null)
            {
                var so = new SerializedObject(_window.SelectedLinkedNode);
                var inspector = HonamiLinkedAnimatorInspector.BuildNodeInspector(
                    _window.SelectedLinkedNode,
                    so,
                    _window.LinkedGraph,
                    _graphView);

                rightContent.Add(inspector);
            }
            else if (_window.SelectedLinkedEvent != null)
            {
                var so = new SerializedObject(_window.SelectedLinkedEvent);
                var inspector = HonamiLinkedAnimatorInspector.BuildEventInspector(
                    _window.SelectedLinkedEvent,
                    so,
                    _window.LinkedGraph,
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

        public void RebuildLeftPanel()
        {
            _leftPanel?.Rebuild();
        }

        public void OnSelectionChange()
        {
            if (_window.IsLocked) return;

            if (Selection.activeGameObject != null)
            {
                var brain = Selection.activeGameObject.GetComponentInParent<Runtime.Core.HonamiLinkedAnimator>(true);
                if (brain != null && brain.Graph != null)
                {
                    _window.SetLinkedGraph(brain.Graph, brain);
                    return;
                }
            }

            if (Selection.activeObject is Runtime.Core.HonamiLinkedAnimatorGraph bg)
            {
                _window.SetLinkedGraph(bg, null);
            }
        }

        public void OnUndoRedo()
        {
            _window.SaveSelection();
            _graphView?.PopulateBrainView(_window.LinkedGraph);
            _leftPanel?.Rebuild();
            _window.RestoreSelection();
            _window.BuildRightPanel();
        }

        public void FrameAll() => _graphView?.FrameAll();
        public bool HasContent() => _window.LinkedGraph != null;

        private void WireBrainGraphViewEvents()
        {
            _graphView.OnBrainNodeSelected = (node) =>
            {
                _window.SelectedLinkedNode = node;
                _window.SelectedLinkedEvent = null;
                _window.BuildRightPanel();
            };
            _graphView.OnBrainEventSelected = (evt) =>
            {
                _window.SelectedLinkedEvent = evt;
                _window.SelectedLinkedNode = null;
                _window.BuildRightPanel();
            };
            _graphView.OnBrainStructureChanged = () => _leftPanel?.Rebuild();
        }
    }
}
