using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiPortalExitNode), "Logic/Portal Exit", 90)]
    public sealed class HonamiPortalExitNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-portal-exit";
        public override string TopBarCssClass => "honami-node-top-portal-exit";
        public override string AvatarCssClass => "honami-node-avatar-portal-exit";
        public override string IconCssClass => "honami-node-icon-portal-exit";
        public override string Tooltip => "Exit of a named portal pair - transitions FROM here.";
        public override bool HasInput => false;

        public override string GetTitle(HonamiState state)
        {
            HonamiPortalExitNode n = state.node as HonamiPortalExitNode;
            return string.IsNullOrEmpty(n?.portalName) ? "UNNAMED" : n.portalName;
        }

        public override string GetSubtitle(HonamiState _) => "PORTAL OUT";

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Portal Exit"));
            AddNodeField(box, nodeSO, "portalName", "Portal Name");
            box.Add(new HelpBox("Transitions FROM this node determine where the paired Entrance leads.", HelpBoxMessageType.Info));
            return box;
        }
    }
}
