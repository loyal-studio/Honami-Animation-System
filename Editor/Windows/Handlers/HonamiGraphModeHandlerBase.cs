using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Handlers
{
    public abstract class HonamiGraphModeHandlerBase : IHonamiGraphModeHandler
    {
        protected HonamiGraphWindow Window { get; private set; }

        public abstract string ModeId { get; }

        public void Initialize(HonamiGraphWindow window)
        {
            Window = window;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }

        public virtual void OnEnable() { }
        public virtual void OnDisable() { }
        public virtual void Update() { }
        public virtual void BuildRightPanel(VisualElement rightContent) { }
        public virtual void RebuildLeftPanel() { }
        public virtual void OnSelectionChange() { }
        public virtual void OnUndoRedo() { }
        public virtual void FrameAll() { }
        public virtual bool HasContent() => false;
        public virtual void ApplySettings() { }
        public virtual void SwitchToMode() { }
        public virtual VisualElement GetMainView() => null;
        public virtual VisualElement GetLeftPanelView() => null;
    }
}
