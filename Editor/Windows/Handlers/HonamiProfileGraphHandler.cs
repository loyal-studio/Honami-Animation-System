using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Handlers
{
    public sealed class HonamiProfileGraphHandler : HonamiGraphModeHandlerBase
    {
        private HonamiProfileGraphView _graphView;

        public override string ModeId => "Profile";

        protected override void OnInitialize()
        {

            _graphView = new HonamiProfileGraphView { name = "Honami Profile Graph" };
            _graphView.style.flexGrow = 1;
            _graphView.Init(Window);
            WireProfileGraphViewEvents();
        }

        public override VisualElement GetMainView() => _graphView;

        public override void OnEnable()
        {
            if (Window.ProfileGraph == null)
            {
                string last = EditorPrefs.GetString("Honami_LastOpenedProfileGraphPath", "");
                if (!string.IsNullOrEmpty(last))
                    Window.SetProfileGraph(AssetDatabase.LoadAssetAtPath<HonamiControllerProfileGraph>(last));
            }
            else
            {
                Window.SetProfileGraph(Window.ProfileGraph);
            }
            ApplySettings();
        }


        public override void SwitchToMode()
        {
            Window.SupportLeftPanel(false);
            Window.SupportRightPanel(true);

            if (Window.TitleLabel != null)
                Window.TitleLabel.text = Window.ProfileGraph != null ? Window.ProfileGraph.name : "No Profile Graph";

            if (Window.OverrideBadge != null) Window.OverrideBadge.style.display = DisplayStyle.None;
            if (Window.LinkedBadge != null) Window.LinkedBadge.style.display = DisplayStyle.None;
            if (Window.Toolbar != null) Window.Toolbar.style.display = DisplayStyle.None;

            if (Window.ProfileGraph != null)
            {
                string path = AssetDatabase.GetAssetPath(Window.ProfileGraph);
                if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString("Honami_LastOpenedProfileGraphPath", path);
            }

            Window.SaveSelection();
            if (Window.ProfileGraph != null) _graphView.PopulateView(Window.ProfileGraph);

            Window.RestoreSelection();
            Window.BuildRightPanel();
            Window.UpdateEmptyState();
        }

        public override void BuildRightPanel(VisualElement rightContent)
        {
            rightContent.Clear();
            rightContent.Unbind();

            if (Window.SelectedProfileState != null && Window.SerializedState?.targetObject != null)
            {
                var pad = HonamiEditorLayout.PaddedInspectorRoot();
                pad.Add(HonamiGraphStyles.Title("Profile State"));
                pad.Add(HonamiGraphStyles.MiniLabel("State Configuration", HonamiGraphStyles.SubTitleClr));

                var box = HonamiGraphStyles.Box();
                var inspector = new InspectorElement(Window.SerializedState);
                inspector.style.paddingTop = 4;

                inspector.TrackPropertyValue(Window.SerializedState.FindProperty("stateName"), _ =>
                {
                    _graphView.PopulateView(Window.ProfileGraph);
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


        public override void OnSelectionChange()
        {
            if (Window.IsLocked) return;

            if (Selection.activeObject is HonamiControllerProfileGraph pg)
            {
                if (Window.ProfileGraph != pg) Window.SetProfileGraph(pg);
            }
        }

        public override void OnUndoRedo()
        {
            Window.SaveSelection();
            _graphView?.PopulateView(Window.ProfileGraph);
            Window.RestoreSelection();
            Window.BuildRightPanel();
        }

        public override void FrameAll() => _graphView?.FrameAll();
        public override bool HasContent() => Window.ProfileGraph != null;

        public override void ApplySettings()
        {
            if (_graphView != null)
            {
                _graphView.SetGridVisible(HonamiGraphSettings.ShowGrid);
            }
        }

        private void WireProfileGraphViewEvents()
        {
            _graphView.OnStateSelected = state =>
            {
                Window.SelectProfileState(state);
                Window.BuildRightPanel();
            };
            _graphView.OnGraphChanged = () => Window.BuildRightPanel();
        }
    }
}
