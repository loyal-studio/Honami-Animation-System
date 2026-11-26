using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiEventNode), "Logic/Event", 25)]
    public sealed class HonamiEventNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-event";
        public override string TopBarCssClass => "honami-node-top-event";
        public override string AvatarCssClass => "honami-node-avatar-event";
        public override string IconCssClass => "honami-node-icon-event";
        public override string Tooltip => "Plays no animation - a pure logic state that fires events and Sub-Nodes for a set duration.";

        public override string GetSubtitle(HonamiState state)
        {
            HonamiEventNode n = state.node as HonamiEventNode;
            return n != null ? $"EVENT  {n.duration:F1}s" : "EVENT";
        }

        public override void OnDoubleClick(HonamiState state, HonamiController ctrl)
        {
            HonamiTimelineWindow.OpenWindow();
            HonamiTimelineWindow.InspectState(ctrl, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController ctrl)
            => root.Add(TimelineButton(state, ctrl));

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Event Node"));
            AddNodeField(box, nodeSO, "duration", "Duration (s)");
            AddStateField(box, state, "loop", "Loop");
            AddStateField(box, state, "holdLastFrame", "Freeze Last Frame");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");
            box.Add(new HelpBox("This state plays no animation. Place Local/Global event markers on its timeline, stack Sub-Nodes and assign parameters on enter/exit to run logic without a clip.", HelpBoxMessageType.Info));
            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiEventNode;
            return node != null && node.duration > 0f ? node.duration : 1f;
        }

        public override void CollectTimelineRows(TimelineState state, List<HonamiTimelineRow> rows) =>
            rows.Add(new HonamiTimelineRow("Logic Window", EditorGUIUtility.IconContent("AnimationWindowEvent Icon").image, TimelineTheme.EventTrack));

        public override void DrawTimelineRow(TimelineState state, ITimelineTrackDrawer drawer, int row, float y)
        {
            var node = state.node as HonamiEventNode;
            if (node == null) return;

            drawer.AddClipWithHandles(
                0,
                "Logic Window",
                0f,
                Mathf.Max(0.01f, node.duration),
                1f,
                y,
                TimelineTheme.EventTrack,
                null,
                newEnd =>
                {
                    Undo.RecordObject(node, "Resize Event Duration");
                    node.duration = Mathf.Max(0.01f, newEnd);
                    EditorUtility.SetDirty(node);
                },
                null,
                null);
        }
    }
}
