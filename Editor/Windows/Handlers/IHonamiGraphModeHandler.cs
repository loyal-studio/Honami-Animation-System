using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Handlers
{
    public interface IHonamiGraphModeHandler
    {
        string ModeId { get; }
        void Initialize(HonamiGraphWindow window);
        void OnEnable();
        void OnDisable();
        void Update();
        void BuildRightPanel(VisualElement rightContent);
        void RebuildLeftPanel();
        void OnSelectionChange();
        void OnUndoRedo();
        void FrameAll();
        bool HasContent();
        void ApplySettings();
        void SwitchToMode();
        VisualElement GetMainView();
        VisualElement GetLeftPanelView();
    }
}
