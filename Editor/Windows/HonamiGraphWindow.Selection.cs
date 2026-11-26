using UnityEditor;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed partial class HonamiGraphWindow
    {
        public void ClearSelection()
        {
            SelectedNode = null;
            SelectedNodeGuid = null;
            SelectedTransition = null;
            SelectedTransitionGuid = null;
            SelectedTransitionIndex = -1;
            SelectedTransitionOwner = null;
            SelectedSubNode = null;
            SerializedSubNode = null;
            SerializedState = null;
            SelectedLinkedNode = null;
            SelectedLinkedEvent = null;
            SelectedProfileState = null;
        }

        public void SelectNode(HonamiNode node)
        {
            ClearSelection();
            SelectedNode = node;
            SelectedNodeGuid = node?.StateGuid;
            SerializedState = node?.State != null && !node.State.isVirtualInheritedState
                ? new SerializedObject(node.State)
                : null;
        }

        public void SelectSubNode(HonamiNode node, HonamiSubNodeBase subNode)
        {
            ClearSelection();
            SelectedNode = node;
            SelectedNodeGuid = node?.StateGuid;
            SelectedSubNode = subNode;
            SerializedSubNode = subNode != null ? new SerializedObject(subNode) : null;
            SerializedState = node?.State != null ? new SerializedObject(node.State) : null;
        }

        public void ClearSelectedSubNode()
        {
            SelectedSubNode = null;
            SerializedSubNode = null;
        }

        public void SelectTransition(HonamiTransition transition)
        {
            ClearSelection();
            SelectedTransition = transition;
            SelectedTransitionGuid = transition?.targetStateGuid;
        }

        public void SetTransitionOwner(HonamiState owner, int index, HonamiTransition resolved)
        {
            SelectedTransitionOwner = owner;
            SelectedTransitionIndex = index;
            SelectedTransition = resolved;
            SerializedState = owner != null && !owner.isVirtualInheritedState ? new SerializedObject(owner) : null;
        }

        public void SelectLinkedNode(HonamiLinkedAnimatorNodeBase node)
        {
            SelectedLinkedNode = node;
            SelectedLinkedEvent = null;
        }

        public void SelectLinkedEvent(HonamiLinkedAnimatorEvent linkedEvent)
        {
            SelectedLinkedNode = null;
            SelectedLinkedEvent = linkedEvent;
        }

        public void SelectProfileState(HonamiProfileState state)
        {
            SelectedProfileState = state;
            SerializedState = state != null ? new SerializedObject(state) : null;
        }

        public void SaveSelectionToTab(HonamiGraphTab tab)
        {
            if (tab == null) return;

            tab.SelectedNodeGuid = SelectedNodeGuid;
            tab.SelectedTransitionGuid = SelectedTransitionGuid;
            tab.SelectedTransitionIndex = SelectedTransitionIndex;
            tab.SelectedTransitionOwner = SelectedTransitionOwner;
            tab.SelectedSubNode = SelectedSubNode;
            tab.SelectedLinkedNode = SelectedLinkedNode;
            tab.SelectedLinkedEvent = SelectedLinkedEvent;
            tab.SelectedProfileState = SelectedProfileState;
            tab.CurrentLayerIndex = CurrentLayerIndex;
            tab.IsLocked = IsLocked;
        }

        public void RestoreFromTab(HonamiGraphTab tab)
        {
            if (tab == null) return;

            SelectedNodeGuid = tab.SelectedNodeGuid;
            SelectedTransitionGuid = tab.SelectedTransitionGuid;
            SelectedTransitionIndex = tab.SelectedTransitionIndex;
            SelectedTransitionOwner = tab.SelectedTransitionOwner;
            SelectedSubNode = tab.SelectedSubNode;
            SelectedLinkedNode = tab.SelectedLinkedNode;
            SelectedLinkedEvent = tab.SelectedLinkedEvent;
            SelectedProfileState = tab.SelectedProfileState;
            CurrentLayerIndex = tab.CurrentLayerIndex;
            IsLocked = tab.IsLocked;

            RuntimeController = tab.RuntimeController;
            Controller = RuntimeController != null ? RuntimeController.BaseController : null;
            SerializedController = Controller != null ? new SerializedObject(Controller) : null;

            LinkedGraph = tab.LinkedGraph;
            RuntimeLinkedAnimator = tab.RuntimeLinkedAnimator;
            ProfileGraph = tab.ProfileGraph;

            SwitchMode(tab.ModeId);
        }
    }
}
