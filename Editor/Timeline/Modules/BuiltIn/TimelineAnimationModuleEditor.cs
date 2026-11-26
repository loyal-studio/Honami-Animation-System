using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineAnimationModule), "Animation", 0)]
    public sealed class TimelineAnimationModuleEditor : HonamiTimelineModuleEditor
    {
        private static readonly Dictionary<GameObject, HonamiTransformPose> Poses = new();
        private static readonly PreviewSampler Sampler = new();
        private static int[] _order;

        public override Color ModuleColor => TimelineTheme.AnimationTrack;
        public override Texture Icon => EditorGUIUtility.IconContent("AnimationClip Icon").image;
        public override string ItemDisplayName => "Clip";
        public override bool FadesOutUnderOverlap => true;

        public override string GetItemLabel(HonamiTimelineItem item)
        {
            var clipItem = (TimelineAnimationClipItem)item;
            if (clipItem.clip == null) return "(No Clip)";
            float speed = clipItem.EffectiveSpeed;
            return Mathf.Approximately(speed, 1f) ? clipItem.clip.name : $"{clipItem.clip.name}  ×{speed:0.##}";
        }

        public override void DecorateItem(HonamiTimelineItemView view)
        {
            base.DecorateItem(view);

            var item = (TimelineAnimationClipItem)view.Item;
            if (!item.loop || item.clip == null || item.clip.length <= 0f) return;

            float period = item.clip.length / item.EffectiveSpeed;
            float first = (item.clip.length - Mathf.Repeat(item.clipStartOffset, item.clip.length)) / item.EffectiveSpeed;
            view.AddCycleMarkers(first, period);
        }

        public override void OnItemDoubleClicked(HonamiTimelineEditContext context, HonamiTimelineItem item, float time) =>
            context.OpenClip(((TimelineAnimationClipItem)item).clip);

        public override bool AcceptsDrop(Object dropped) => dropped is AnimationClip;

        public override HonamiTimelineItem CreateItemFromDrop(HonamiTimelineEditContext context, Object dropped, float time) =>
            dropped is AnimationClip clip ? new TimelineAnimationClipItem { clip = clip, time = time } : null;

        public override void Preview(HonamiTimelinePreviewContext context)
        {
            var target = context.Binding as GameObject;
            if (target == null) target = context.PreviewRoot;
            target = context.ResolveSceneInstance(target);
            if (target == null) return;

            if (!Poses.TryGetValue(target, out var pose) || pose.Root == null)
            {
                pose = new HonamiTransformPose(target.transform);
                Poses[target] = pose;
            }

            bool hold = (context.Director != null && context.Director.WrapMode == HonamiDirectorWrapMode.Hold) ||
                        (context.State?.SelectedState != null && context.State.SelectedState.holdLastFrame);
            Sampler.Context = context;
            ((TimelineAnimationModule)context.Module).Evaluate(target, context.Time, Sampler, pose, ref _order, hold);
            Sampler.Context = null;
        }

        public override void StopPreview() => Poses.Clear();

        private sealed class PreviewSampler : IHonamiClipSampler
        {
            public HonamiTimelinePreviewContext Context;

            public void Sample(GameObject target, AnimationClip clip, float time) => Context.SampleClip(target, clip, time);
        }
    }
}
