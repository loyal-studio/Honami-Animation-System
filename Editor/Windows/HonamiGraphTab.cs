using System;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    [Serializable]
    public sealed class HonamiGraphTab
    {
        public string ModeId;
        public HonamiRuntimeController RuntimeController;
        public HonamiLinkedAnimatorGraph LinkedGraph;
        public HonamiControllerProfileGraph ProfileGraph;
        public HonamiLinkedAnimator RuntimeLinkedAnimator;

        // Selection State
        public string SelectedNodeGuid;
        public string SelectedTransitionGuid;
        public int SelectedTransitionIndex = -1;
        public HonamiState SelectedTransitionOwner;
        public HonamiSubNodeBase SelectedSubNode;
        public HonamiLinkedAnimatorNodeBase SelectedLinkedNode;
        public HonamiLinkedAnimatorEvent SelectedLinkedEvent;
        public HonamiProfileState SelectedProfileState;

        // View State
        public int CurrentLayerIndex;
        public bool IsLocked;

        public HonamiGraphTab()
        {
            ModeId = "Empty";
        }

        public HonamiGraphTab(HonamiRuntimeController controller)
        {
            ModeId = "Animator";
            RuntimeController = controller;
        }

        public HonamiGraphTab(HonamiLinkedAnimatorGraph linkedGraph, HonamiLinkedAnimator runtimeLinkedAnimator)
        {
            ModeId = "LinkedAnimator";
            LinkedGraph = linkedGraph;
            RuntimeLinkedAnimator = runtimeLinkedAnimator;
        }

        public HonamiGraphTab(HonamiControllerProfileGraph profileGraph)
        {
            ModeId = "Profile";
            ProfileGraph = profileGraph;
        }

        public string GetTabName()
        {
            if (ModeId == "Animator" && RuntimeController != null)
                return RuntimeController.name;
            if (ModeId == "LinkedAnimator" && LinkedGraph != null)
                return LinkedGraph.name;
            if (ModeId == "Profile" && ProfileGraph != null)
                return ProfileGraph.name;
            return "New Tab";
        }

        public Texture2D GetTabIcon()
        {
            if (ModeId == "Animator")
                return HonamiEditorIcons.Controller;
            if (ModeId == "LinkedAnimator")
                return HonamiEditorIcons.Graph;
            if (ModeId == "Profile")
                return HonamiEditorIcons.Profile;
            return HonamiEditorIcons.Graph;
        }

        public bool IsEmpty()
        {
            return ModeId == "Empty" ||
                   (ModeId == "Animator" && RuntimeController == null) ||
                   (ModeId == "LinkedAnimator" && LinkedGraph == null) ||
                  (ModeId == "Profile" && ProfileGraph == null);
        }
    }
}
