using UnityEditor;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiStateSectionContext
    {
        public HonamiState State { get; }
        public SerializedObject SerializedState { get; }
        public HonamiController Controller { get; }
        public HonamiRuntimeController RuntimeController { get; }
        public int LayerIndex { get; }
        public bool IsInherited { get; }

        public HonamiStateSectionContext(HonamiState state, SerializedObject serializedState,
            HonamiController controller, HonamiRuntimeController runtimeController, int layerIndex, bool isInherited)
        {
            State = state;
            SerializedState = serializedState;
            Controller = controller;
            RuntimeController = runtimeController;
            LayerIndex = layerIndex;
            IsInherited = isInherited;
        }
    }

    public abstract class HonamiStateInspectorSection
    {
        public virtual string Title => null;
        public virtual bool DefaultExpanded => false;

        public virtual bool IsVisible(HonamiStateSectionContext context) => true;

        public abstract VisualElement Build(HonamiStateSectionContext context);
    }
}
