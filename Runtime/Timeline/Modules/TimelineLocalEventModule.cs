using System;
using HonamiAnimationSystem.Runtime.Events;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class TimelineLocalEventItem : HonamiTimelineItem
    {
        [Tooltip("The name of the event in the HonamiLocalEventReceiver")]
        public string eventName = "NewEvent";

        public override bool IsInstant => true;
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineLocalEventModule : HonamiTimelineModule<TimelineLocalEventItem>
    {
        public override Type BindingType => typeof(HonamiLocalEventReceiver);

        public override bool RequiresBinding(HonamiTimelineModuleHost host) => host == HonamiTimelineModuleHost.Timeline;

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context)
        {
            var receiver = context.Binding as HonamiLocalEventReceiver;
            if (receiver == null && context.Animator != null)
                receiver = context.Animator._localEventReceiver;
            return receiver != null ? new Evaluator(this, receiver) : null;
        }

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineLocalEventModule _module;
            private readonly HonamiLocalEventReceiver _receiver;

            public Evaluator(TimelineLocalEventModule module, HonamiLocalEventReceiver receiver)
            {
                _module = module;
                _receiver = receiver;
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                if (_receiver == null) return;

                var events = _module.items;
                for (int i = 0; i < events.Count; i++)
                {
                    var item = events[i];
                    if (item != null && frame.Crossed(item.time))
                        _receiver.TriggerEvent(item.eventName);
                }
            }
        }
    }
}
