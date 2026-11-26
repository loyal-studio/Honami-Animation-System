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
    [HonamiNodeEditor(typeof(HonamiSequencerNode), "Complex/Sequencer", 60)]
    public sealed class HonamiSequencerNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-sequencer";
        public override string TopBarCssClass => "honami-node-top-sequencer";
        public override string AvatarCssClass => "honami-node-avatar-sequencer";
        public override string IconCssClass => "honami-node-icon-sequencer";
        public override string Tooltip => "Plays a sequence of clips with defined start times.";

        public override string GetSubtitle(HonamiState state)
        {
            int count = (state.node as HonamiSequencerNode)?.sequencedClips?.Count ?? 0;
            return $"SEQUENCER  {count} clips";
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
            box.Add(HonamiGraphStyles.SubTitle("Sequencer Node"));
            AddStateField(box, state, "loop", "Loop Sequence");
            AddStateField(box, state, "holdLastFrame", "Freeze Last Frame");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");
            AddNodeListField(box, nodeSO, "sequencedClips", "Sequenced Clips", prop =>
            {
                var clip = prop.FindPropertyRelative("clip");
                var speed = prop.FindPropertyRelative("speed");
                var startTime = prop.FindPropertyRelative("startTime");
                prop.FindPropertyRelative("id").stringValue = HonamiNodeBase.NewMotionId();
                if (clip.objectReferenceValue == null && speed.floatValue == 0f && startTime.floatValue == 0f)
                {
                    speed.floatValue = 1f;
                }
            });
            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiSequencerNode;
            if (node == null || node.sequencedClips == null) return 5f;
            float max = 1f;
            foreach (var sc in node.sequencedClips)
            {
                if (sc.clip == null) continue;
                float eff = sc.speed != 0 ? Mathf.Abs(sc.speed) : 1f;
                float d = sc.startTime + sc.clip.length / eff;
                if (d > max) max = d;
            }
            return max;
        }

        public override void CollectTimelineRows(TimelineState state, List<HonamiTimelineRow> rows)
        {
            if (state.node is not HonamiSequencerNode node || node.sequencedClips == null) return;
            for (int i = 0; i < node.sequencedClips.Count; i++)
            {
                var clip = node.sequencedClips[i]?.clip;
                rows.Add(new HonamiTimelineRow(clip != null ? clip.name : $"Empty Sequence {i}", HonamiTimelineRow.ClipIcon, TimelineTheme.SequencerTrack, i));
            }
        }

        public override void DrawTimelineRow(TimelineState state, ITimelineTrackDrawer drawer, int row, float y)
        {
            var node = state.node as HonamiSequencerNode;
            if (node == null || node.sequencedClips == null || row >= node.sequencedClips.Count) return;

            var clip = node.sequencedClips[row];
            if (clip?.clip == null) return;

            float speed = clip.speed != 0 ? Mathf.Abs(clip.speed) : 1f;
            drawer.AddClipElement(clip, clip.clip.name, clip.startTime, clip.clip.length / speed, y, state.SelectedSeqClips.Contains(clip), TimelineTheme.SequencerTrack);
        }

        public override bool SupportsTimelinePreview => true;

        public override void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT)
        {
            var node = state.node as HonamiSequencerNode;
            if (node == null || node.sequencedClips == null) return;
            foreach (var sc in node.sequencedClips)
            {
                if (sc.clip == null) continue;
                float eff = sc.speed != 0 ? Mathf.Abs(sc.speed) : 1f;
                float start = sc.startTime;
                float end = start + sc.clip.length / eff;
                if (internalT >= start && internalT < end)
                {
                    GameObject matchedTarget = TimelinePreview.FindBestTargetForClip(state, rootTarget, sc.clip);
                    float sTime = (internalT - start) * eff;
                    TimelinePreview.SafeSample(state, matchedTarget, sc.clip, sTime);
                }
            }
        }
    }
}
