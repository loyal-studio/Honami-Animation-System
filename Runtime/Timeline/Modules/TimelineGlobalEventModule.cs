using System;
using HonamiAnimationSystem.Runtime.Events;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class TimelineGlobalEventItem : HonamiTimelineItem
    {
        [Tooltip("Id of the HonamiGlobalEvent components to execute")]
        public string globalEventId = "NewEvent";

        public override bool IsInstant => true;
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineGlobalEventModule : HonamiTimelineModule<TimelineGlobalEventItem>
    {
        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context) => new Evaluator(this);

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineGlobalEventModule _module;

            public Evaluator(TimelineGlobalEventModule module) => _module = module;

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                var events = _module.items;
                for (int i = 0; i < events.Count; i++)
                {
                    var item = events[i];
                    if (item != null && frame.Crossed(item.time))
                        HonamiGlobalEvent.Execute(item.globalEventId);
                }
            }
        }
    }
}
