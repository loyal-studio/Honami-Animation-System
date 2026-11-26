using UnityEditor;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiTransitionSectionContext
    {
        public HonamiTransition Transition { get; }
        public HonamiState Owner { get; }
        public int TransitionIndex { get; }
        public SerializedObject SerializedOwner { get; }
        public HonamiController Controller { get; }
        public HonamiRuntimeController RuntimeController { get; }
        public bool IsInherited { get; }

        public HonamiTransitionSectionContext(HonamiTransition transition, HonamiState owner, int transitionIndex,
            SerializedObject serializedOwner, HonamiController controller, HonamiRuntimeController runtimeController, bool isInherited)
        {
            Transition = transition;
            Owner = owner;
            TransitionIndex = transitionIndex;
            SerializedOwner = serializedOwner;
            Controller = controller;
            RuntimeController = runtimeController;
            IsInherited = isInherited;
        }
    }

    public abstract class HonamiTransitionInspectorSection
    {
        public virtual string Title => null;
        public virtual bool DefaultExpanded => false;

        public virtual bool IsVisible(HonamiTransitionSectionContext context) => true;

        public abstract VisualElement Build(HonamiTransitionSectionContext context);
    }
}
