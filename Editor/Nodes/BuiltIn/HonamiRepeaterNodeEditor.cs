using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiRepeaterNode), "Logic/Repeater", 70)]
    public sealed class HonamiRepeaterNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-repeater";
        public override string TopBarCssClass => "honami-node-top-repeater";
        public override string AvatarCssClass => "honami-node-avatar-repeater";
        public override string IconCssClass => "honami-node-icon-repeater";
        public override string Tooltip => "Fires transitions periodically from any active state.";
        public override bool HasInput => false;

        public override string GetSubtitle(HonamiState state)
        {
            HonamiRepeaterNode n = state.node as HonamiRepeaterNode;
            if (n == null) return "REPEATER";
            return n.comboMode ? $"COMBO  {n.repeatCooldown:F1}s" : $"REPEATER  {n.repeatCooldown:F1}s";
        }

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Repeater Node"));
            AddNodeField(box, nodeSO, "repeatCooldown", "Cooldown (s)");
            AddNodeField(box, nodeSO, "maxRepeats", "Max Repeats");
            box.Add(new HelpBox("0 max repeats = unlimited.", HelpBoxMessageType.Info));

            AddNodeField(box, nodeSO, "comboMode", "Combo Mode");

            VisualElement comboGroup = new VisualElement();
            AddNodeField(comboGroup, nodeSO, "cancelWindow", "Cancel Window");
            AddNodeField(comboGroup, nodeSO, "comboResetTime", "Combo Reset (s)");
            AddNodeField(comboGroup, nodeSO, "loopCombo", "Loop Combo");
            comboGroup.Add(new HelpBox("Combo mode fires outgoing transitions in order, one per trigger. A trigger pressed before the current attack reaches Cancel Window is buffered and fires when the window opens.", HelpBoxMessageType.Info));
            box.Add(comboGroup);

            SerializedProperty comboModeProp = nodeSO.FindProperty("comboMode");
            comboGroup.style.display = comboModeProp != null && comboModeProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (comboModeProp != null)
            {
                comboGroup.TrackPropertyValue(comboModeProp, p =>
                    comboGroup.style.display = p.boolValue ? DisplayStyle.Flex : DisplayStyle.None);
            }

            return box;
        }
    }
}
