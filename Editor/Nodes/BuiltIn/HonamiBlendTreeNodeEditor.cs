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
    [HonamiNodeEditor(typeof(HonamiBlendTreeNode), "Complex/Blend Tree (1D)", 40)]
    public sealed class HonamiBlendTreeNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-blend";
        public override string TopBarCssClass => "honami-node-top-blend";
        public override string AvatarCssClass => "honami-node-avatar-blend";
        public override string IconCssClass => "honami-node-icon-blend";
        public override string Tooltip => "Blends multiple clips based on a float parameter.";

        public override string GetSubtitle(HonamiState state)
        {
            HonamiBlendTreeNode n = state.node as HonamiBlendTreeNode;
            return "BLEND  " + (string.IsNullOrEmpty(n?.blendParameter) ? "-" : n.blendParameter);
        }

        public override void OnDoubleClick(HonamiState state, HonamiController ctrl)
        {
            HonamiBlendTreeWindow.OpenWindow();
            HonamiBlendTreeWindow.InspectState(ctrl, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController ctrl)
        {
            root.Add(BlendTreeButton(state, ctrl));
            root.Add(TimelineButton(state, ctrl));
        }

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Blend Tree Node"));
            AddNodeField(box, nodeSO, "blendType", "Blend Mode");
            AddParameterPopup(box, nodeSO, controller, "blendParameter", "Blend Parameter");
            AddNodeField(box, nodeSO, "blendParameterDampTime", "Damp Time");
            AddStateField(box, state, "loop", "Loop");
            AddStateField(box, state, "holdLastFrame", "Freeze Last Frame");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");
            AddNodeListField(box, nodeSO, "blendMotions", "Blend Motions", prop =>
            {
                var clip = prop.FindPropertyRelative("clip");
                var speed = prop.FindPropertyRelative("speed");
                if (clip.objectReferenceValue == null && speed.floatValue == 0f)
                    speed.floatValue = 1f;
            });

            box.Add(HonamiGraphStyles.MiniLabel(
                "Need strafing, backpedalling or diagonals? Right-click the node and pick Change Type / BlendSpace - every motion carries over."));

            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiBlendTreeNode;
            if (node == null || node.blendMotions == null) return 5f;
            return HonamiBlendTreeEvaluator.GetDurationFromMotions(node.blendMotions, state.BlendPreviewValue);
        }

        public override void CollectTimelineRows(TimelineState state, List<HonamiTimelineRow> rows)
        {
            if (state.node is not HonamiBlendTreeNode node || node.blendMotions == null) return;
            for (int i = 0; i < node.blendMotions.Count; i++)
            {
                var motion = node.blendMotions[i];
                string name = motion.clip != null ? motion.clip.name : $"Empty Motion {i}";
                rows.Add(new HonamiTimelineRow($"{name}  ({motion.threshold:F2})", HonamiTimelineRow.ClipIcon, TimelineTheme.PreviewTrack));
            }
        }

        public override void DrawTimelineRow(TimelineState state, ITimelineTrackDrawer drawer, int row, float y)
        {
            var node = state.node as HonamiBlendTreeNode;
            if (node == null || node.blendMotions == null || row >= node.blendMotions.Count) return;

            var motion = node.blendMotions[row];
            if (motion.clip == null) return;

            int mCount = node.blendMotions.Count;
            Span<float> weights = mCount <= 32 ? stackalloc float[mCount] : new float[mCount];
            Span<(float threshold, int index)> sorted = mCount <= 32 ? stackalloc (float, int)[mCount] : new (float, int)[mCount];
            HonamiBlendWeightUtility.CalculateWeights(node.blendMotions, state.BlendPreviewValue, weights, sorted);

            float speed = motion.speed != 0 ? Mathf.Abs(motion.speed) : 1f;
            string label = $"{motion.clip.name}  {weights[row] * 100f:F0}%";
            drawer.AddReadonlyClip(motion.clip, label, 0f, motion.clip.length / speed, y, weights[row] > 0.001f ? TimelineTheme.PreviewTrack : TimelineTheme.RandomTrack);
        }

        public override bool SupportsTimelinePreview => true;

        public override void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT)
        {
            TimelinePreview.SampleBlendTree(state, rootTarget, internalT);
        }
    }
}
