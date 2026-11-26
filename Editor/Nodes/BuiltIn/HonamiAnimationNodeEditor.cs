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
    [HonamiNodeEditor(typeof(HonamiAnimationNode), "Basic/Animation", 10)]
    public sealed class HonamiAnimationNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-animation";
        public override string TopBarCssClass => "honami-node-top-animation";
        public override string AvatarCssClass => "honami-node-avatar-animation";
        public override string IconCssClass => "honami-node-icon-animation";
        public override string Tooltip => "Plays a single animation clip.";
        public override string GetSubtitle(HonamiState _) => "ANIMATION";

        public override void OnDoubleClick(HonamiState state, HonamiController ctrl)
        {
            HonamiTimelineWindow.OpenWindow();
            HonamiTimelineWindow.InspectState(ctrl, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController ctrl)
            => root.Add(TimelineButton(state, ctrl));

        private AnimationClip _lastClip;

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Animation Node"));
            AddNodeField(box, nodeSO, "clip", "Motion");
            AddNodeField(box, nodeSO, "startTime", "Clip Start");
            AddNodeField(box, nodeSO, "endTime", "Clip End");
            AddStateField(box, state, "loop", "Loop");
            AddStateField(box, state, "holdLastFrame", "Freeze Last Frame");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");

            var node = state.node as HonamiAnimationNode;
            if (node != null) _lastClip = node.clip;

            box.schedule.Execute(() =>
            {
                if (node == null) return;
                if (node.clip != _lastClip)
                {
                    if (node.clip != null && _lastClip != node.clip)
                    {
                        var endTimeProp = nodeSO.FindProperty("endTime");
                        if (endTimeProp != null) endTimeProp.floatValue = 0f;
                        var startTimeProp = nodeSO.FindProperty("startTime");
                        if (startTimeProp != null) startTimeProp.floatValue = 0f;
                        nodeSO.ApplyModifiedProperties();
                    }
                    _lastClip = node.clip;
                }
            }).Every(100);

            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiAnimationNode;
            if (node != null && node.clip != null) return node.clip.length;
            return 5f;
        }

        public override void CollectTimelineRows(TimelineState state, List<HonamiTimelineRow> rows) =>
            rows.Add(new HonamiTimelineRow("Animation Clip", HonamiTimelineRow.ClipIcon, TimelineTheme.AnimationTrack));

        public override void DrawTimelineRow(TimelineState state, ITimelineTrackDrawer drawer, int row, float y)
        {
            var node = state.node as HonamiAnimationNode;
            if (node != null && node.clip != null)
            {
                float stop = node.endTime > 0 ? node.endTime : node.clip.length;
                drawer.AddClipWithHandles(
                    0,
                    node.clip.name,
                    node.startTime,
                    Mathf.Max(0, stop - node.startTime),
                    1f,
                    y,
                    TimelineTheme.AnimationTrack,
                    newStart =>
                    {
                        Undo.RecordObject(node, "Trim Anim Start");
                        node.startTime = newStart;
                        EditorUtility.SetDirty(node);
                    },
                    newEnd =>
                    {
                        Undo.RecordObject(node, "Trim Anim End");
                        node.endTime = newEnd >= node.clip.length - 0.01f ? 0f : newEnd;
                        EditorUtility.SetDirty(node);
                    },
                    () =>
                    {
                        state.Mode = TimelineMode.HonamiClipEdit;
                        state.ActiveClip = node.clip;
                    },
                    null,
                    node.clip.length
                );
            }
        }

        public override bool SupportsTimelinePreview => true;

        public override void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT)
        {
            var node = state.node as HonamiAnimationNode;
            if (node != null && node.clip != null)
            {
                GameObject matchedTarget = TimelinePreview.FindBestTargetForClip(state, rootTarget, node.clip);
                float sTime = state.PlayheadTime;
                if (state.SelectedState.isReversed) sTime = node.clip.length - sTime;
                TimelinePreview.SafeSample(state, matchedTarget, node.clip, sTime);
            }
        }
    }
}
