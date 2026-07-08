using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace HonamiAnimationSystem.Editor.Handlers
{
    public sealed class HonamiProfileGraphHandler : IHonamiGraphModeHandler
    {
        private HonamiGraphWindow _window;
        private HonamiProfileGraphView _graphView;

        public string ModeId => "Profile";

        public void Initialize(HonamiGraphWindow window)
        {
            _window = window;

            _graphView = new HonamiProfileGraphView { name = "Honami Profile Graph" };
            _graphView.style.flexGrow = 1;
            _graphView.Init(_window);
            WireProfileGraphViewEvents();
        }

        public VisualElement GetMainView() => _graphView;
        public VisualElement GetLeftPanelView() => null; // Profile doesn't have a left panel

        public void OnEnable()
        {
            if (_window.ProfileGraph == null)
            {
                string last = EditorPrefs.GetString("Honami_LastOpenedProfileGraphPath", "");
                if (!string.IsNullOrEmpty(last))
                    _window.SetProfileGraph(AssetDatabase.LoadAssetAtPath<Runtime.Core.HonamiControllerProfileGraph>(last));
            }
            else
            {
                _window.SetProfileGraph(_window.ProfileGraph);
            }
            ApplySettings();
        }

        public void OnDisable() { }
        public void Update() { }

        public void SwitchToMode()
        {
            _window.SupportLeftPanel(false);
            _window.SupportRightPanel(true);

            if (_window.TitleLabel != null)
                _window.TitleLabel.text = _window.ProfileGraph != null ? _window.ProfileGraph.name : "No Profile Graph";

            if (_window.OverrideBadge != null) _window.OverrideBadge.style.display = DisplayStyle.None;
            if (_window.LinkedBadge != null) _window.LinkedBadge.style.display = DisplayStyle.None;
            if (_window.Toolbar != null) _window.Toolbar.style.display = DisplayStyle.None;

            if (_window.ProfileGraph != null)
            {
                string path = AssetDatabase.GetAssetPath(_window.ProfileGraph);
                if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString("Honami_LastOpenedProfileGraphPath", path);
            }

            _window.SaveSelection();
            if (_window.ProfileGraph != null) _graphView.PopulateView(_window.ProfileGraph);

            _window.RestoreSelection();
            _window.BuildRightPanel();
            _window.UpdateEmptyState();
        }

        public void BuildRightPanel(VisualElement rightContent)
        {
            rightContent.Clear();
            rightContent.Unbind();

            if (_window.SelectedProfileState != null && _window.SerializedState?.targetObject != null)
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();
                pad.Add(HonamiGraphStyles.Title("Profile State"));
                pad.Add(HonamiGraphStyles.MiniLabel("State Configuration", HonamiGraphStyles.SubTitleClr));

                var box = HonamiGraphStyles.Box();
                var inspector = new InspectorElement(_window.SerializedState);
                inspector.style.paddingTop = 4;

                inspector.TrackPropertyValue(_window.SerializedState.FindProperty("stateName"), _ =>
                {
                    _graphView.PopulateView(_window.ProfileGraph);
                });

                box.Add(inspector);
                pad.Add(box);

                rightContent.Add(pad);
            }
            else
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();
                pad.Add(HonamiGraphStyles.Title("Profile Inspector"));
                var box = HonamiGraphStyles.Box();
                box.Add(new HelpBox("Select a Profile State to view its settings.", HelpBoxMessageType.Info));
                pad.Add(box);
                rightContent.Add(pad);
            }
        }

        public void RebuildLeftPanel() { }

        public void OnSelectionChange()
        {
            if (_window.IsLocked) return;

            if (Selection.activeObject is Runtime.Core.HonamiControllerProfileGraph pg)
            {
                if (_window.ProfileGraph != pg) _window.SetProfileGraph(pg);
            }
        }

        public void OnUndoRedo()
        {
            _window.SaveSelection();
            _graphView?.PopulateView(_window.ProfileGraph);
            _window.RestoreSelection();
            _window.BuildRightPanel();
        }

        public void FrameAll() => _graphView?.FrameAll();
        public bool HasContent() => _window.ProfileGraph != null;

        public void ApplySettings()
        {
            if (_graphView != null)
            {
                _graphView.SetGridVisible(HonamiGraphSettings.ShowGrid);
            }
        }

        private void WireProfileGraphViewEvents()
        {
            _graphView.OnStateSelected = (state) =>
            {
                _window.SelectedProfileState = state;
                _window.SerializedState = state != null ? new SerializedObject(state) : null;
                _window.BuildRightPanel();
            };
            _graphView.OnGraphChanged = () => _window.BuildRightPanel();
        }
    }
}
