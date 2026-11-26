using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiAnyStateNode), "Logic/Any State", 20)]
    public sealed class HonamiAnyStateNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-any";
        public override string TopBarCssClass => "honami-node-top-any";
        public override string AvatarCssClass => "honami-node-avatar-any";
        public override string IconCssClass => "honami-node-icon-any";
        public override string Tooltip => "Global transition source - fires from any active state.";
        public override bool HasInput => false;
        public override string GetSubtitle(HonamiState _) => "ANY STATE";

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Any State Node"));
            box.Add(new HelpBox("Transitions from this node can fire regardless of which state is active.", HelpBoxMessageType.Info));
            return box;
        }
    }
}
