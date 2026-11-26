using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiExitNode), "Basic/Exit", 30)]
    public sealed class HonamiExitNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-exit";
        public override string TopBarCssClass => "honami-node-top-exit";
        public override string AvatarCssClass => "honami-node-avatar-exit";
        public override string IconCssClass => "honami-node-icon-exit";
        public override string Tooltip => "Exits the current layer - fades it out.";
        public override bool HasOutput => false;
        public override string GetSubtitle(HonamiState _) => "EXIT";

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Exit Node"));
            box.Add(new HelpBox("Transition to this node to smoothly deactivate the layer.", HelpBoxMessageType.Info));
            return box;
        }
    }
}
