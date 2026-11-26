using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineShakeModule), "Shake", 20)]
    public sealed class TimelineShakeModuleEditor : HonamiTimelineModuleEditor
    {
        private static readonly Color ShakeColor = new(0.8f, 0.4f, 0.1f, 0.96f);

        public override Color ModuleColor => ShakeColor;
        public override Texture Icon => EditorGUIUtility.IconContent("Transform Icon").image;
        public override string ItemDisplayName => "Shake";

        public override string GetItemLabel(HonamiTimelineItem item)
        {
            var shake = (TimelineShakeItem)item;
            return $"Shake ({shake.length:0.##}s)";
        }

        public override HonamiTimelineItem CreateItem(HonamiTimelineEditContext context, float time) =>
            new TimelineShakeItem { time = time };

        public override void Preview(HonamiTimelinePreviewContext context)
        {
            var transform = HonamiTimelineModuleBinding.Convert(context.Binding, typeof(Transform)) as Transform;
            if (transform == null && context.Director != null)
                transform = context.Director.transform;
            if (transform == null) return;

            var module = (TimelineShakeModule)context.Module;
            if (!module.EvaluateShake(context.Time, out var posOffset, out var rotOffset)) return;

            HonamiTimelinePreviewContext.DriveTransform(transform);
            transform.localPosition += posOffset;
            transform.localRotation *= rotOffset;
        }
    }
}
