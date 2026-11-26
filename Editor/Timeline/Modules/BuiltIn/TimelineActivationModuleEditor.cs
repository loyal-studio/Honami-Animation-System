using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineActivationModule), "Activation", 20)]
    public sealed class TimelineActivationModuleEditor : HonamiTimelineModuleEditor
    {
        private static readonly Color ActivationColor = new(0.36f, 0.58f, 0.30f, 0.96f);

        public override Color ModuleColor => ActivationColor;
        public override Texture Icon => EditorGUIUtility.IconContent("GameObject Icon").image;
        public override string ItemDisplayName => "Active Range";
        public override string GetItemLabel(HonamiTimelineItem item) => "Active";

        public override void Preview(HonamiTimelinePreviewContext context)
        {
            if (context.Binding is not GameObject target || target == null) return;
            if (context.Host == HonamiTimelineModuleHost.State && target == context.PreviewRoot) return;

            var module = (TimelineActivationModule)context.Module;
            var ranges = module.Items;
            if (ranges == null || ranges.Count == 0) return;

            float minStartTime = float.PositiveInfinity;
            float maxEndTime = float.NegativeInfinity;
            bool insideActiveRange = false;

            for (int i = 0; i < ranges.Count; i++)
            {
                var item = ranges[i];
                if (item == null) continue;

                if (item.time < minStartTime) minStartTime = item.time;
                if (item.EndTime > maxEndTime) maxEndTime = item.EndTime;

                if (context.Time >= item.time && context.Time <= item.EndTime)
                {
                    insideActiveRange = true;
                }
            }

            if (float.IsPositiveInfinity(minStartTime)) return;

            bool active;
            if (insideActiveRange)
            {
                active = true;
            }
            else if (context.Time > maxEndTime)
            {
                if (context.Director != null && context.Director.WrapMode == HonamiDirectorWrapMode.Hold)
                {
                    active = true;
                }
                else
                {
                    switch (module.postPlayback)
                    {
                        case TimelineActivationPostPlayback.LeaveAsIs:
                        case TimelineActivationPostPlayback.Active:
                            active = true;
                            break;
                        case TimelineActivationPostPlayback.Inactive:
                            active = false;
                            break;
                        case TimelineActivationPostPlayback.Revert:
                        default:
                            // Leaving undriven allows AnimationMode to revert to original scene state
                            return;
                    }
                }
            }
            else
            {
                active = false;
            }

            HonamiTimelinePreviewContext.DriveActive(target);
            if (target.activeSelf != active) target.SetActive(active);
        }
    }
}
