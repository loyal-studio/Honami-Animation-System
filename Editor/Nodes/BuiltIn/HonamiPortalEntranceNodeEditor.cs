using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiPortalEntranceNode), "Logic/Portal Entrance", 80)]
    public sealed class HonamiPortalEntranceNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-portal-entrance";
        public override string TopBarCssClass => "honami-node-top-portal-entrance";
        public override string AvatarCssClass => "honami-node-avatar-portal-entrance";
        public override string IconCssClass => "honami-node-icon-portal-entrance";
        public override string Tooltip => "Entrance of a named portal pair - routes to the matching Exit.";
        public override bool HasOutput => false;

        public override string GetTitle(HonamiState state)
        {
            HonamiPortalEntranceNode n = state.node as HonamiPortalEntranceNode;
            return string.IsNullOrEmpty(n?.portalName) ? "UNNAMED" : n.portalName;
        }

        public override string GetSubtitle(HonamiState _) => "PORTAL IN";

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Portal Entrance"));
            AddNodeField(box, nodeSO, "portalName", "Portal Name");
            box.Add(new HelpBox("Transitions INTO this node redirect to the matching Portal Exit.", HelpBoxMessageType.Info));
            return box;
        }
    }
}
