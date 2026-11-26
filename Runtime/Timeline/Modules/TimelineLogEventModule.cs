using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class TimelineLogEventItem : HonamiTimelineItem
    {
        public string message = "Timeline event";
        public LogType logType = LogType.Log;

        public override bool IsInstant => true;
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineLogEventModule : HonamiTimelineModule<TimelineLogEventItem>
    {
        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context) =>
            new Evaluator(this, context.Owner);

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineLogEventModule _module;
            private readonly GameObject _owner;

            public Evaluator(TimelineLogEventModule module, GameObject owner)
            {
                _module = module;
                _owner = owner;
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                var events = _module.items;
                for (int i = 0; i < events.Count; i++)
                {
                    var item = events[i];
                    if (item != null && frame.Crossed(item.time))
                        Debug.LogFormat(item.logType, LogOption.None, _owner, "{0}: {1}", _owner != null ? _owner.name : "Timeline", item.message);
                }
            }
        }
    }
}
