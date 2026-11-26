using System;
using HonamiAnimationSystem.Runtime.Riggings;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    // Scales a rig by a curve while the playhead is inside an item, on top of whatever drives the rig's own weight.
    [Preserve]
    [Serializable]
    public sealed class TimelineRigWeightModule : HonamiTimelineModule<TimelineParameterCurveItem>
    {
        public override Type BindingType => typeof(HonamiRig);

        public override bool RequiresBinding(HonamiTimelineModuleHost host) => true;

        public override HonamiTimelineItem CreateItem(float time) => new TimelineParameterCurveItem
        {
            time = time,
            curve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
        };

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context) =>
            context.Binding is HonamiRig rig ? new Evaluator(this, rig) : null;

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineRigWeightModule _module;
            private readonly HonamiRig _rig;

            public Evaluator(TimelineRigWeightModule module, HonamiRig rig)
            {
                _module = module;
                _rig = rig;
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                if (_rig == null) return;

                float scale = 1f;
                var items = _module.items;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null || frame.Time < item.time || frame.Time > item.EndTime) continue;

                    scale = Mathf.Clamp01(item.Evaluate(frame.Time));
                    break;
                }

                _rig.weightScale = scale;
            }

            public override void OnStop() => Restore();

            public override void Dispose() => Restore();

            private void Restore()
            {
                if (_rig != null) _rig.weightScale = 1f;
            }
        }
    }
}
