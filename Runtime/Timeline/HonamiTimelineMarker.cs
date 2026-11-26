using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public abstract class HonamiTimelineMarker
    {
        public string id = "Marker";
        [Min(0f)] public float time;

        // Editing-time link group. Markers sharing a group are dragged together on the ruler,
        // each keeping its own offset. Empty when the marker is not linked.
        [HideInInspector] public string linkGroup;

        public string LinkGroup => linkGroup;
        public bool IsLinked => !string.IsNullOrEmpty(linkGroup);

        public void SetLinkGroup(string group) => linkGroup = string.IsNullOrEmpty(group) ? null : group;
        public void ClearLinkGroup() => linkGroup = null;
    }

    [Preserve]
    [Serializable]
    public sealed class HonamiTimelineCueMarker : HonamiTimelineMarker
    {
        public Color color = new(0.98f, 0.76f, 0.28f);
        [TextArea(2, 6)] public string note;

        public HonamiTimelineCueMarker()
        {
            id = "Marker";
        }
    }

    [Preserve]
    [Serializable]
    public sealed class HonamiTimelineLoopMarker : HonamiTimelineMarker
    {
        public float endTime = 5f;
        [Tooltip("Preview Only keeps this region in the Timeline window only. Runtime Loop repeats it forever. Runtime Loop Until Signal repeats it until HonamiDirector.Continue(signal) is called.")]
        public HonamiTimelineLoopMode mode;
        [Tooltip("The signal that releases a Runtime Loop Until Signal. Leave empty to accept any Continue call.")]
        public string signal = "Continue";

        public HonamiTimelineLoopMarker()
        {
            id = "Loop";
        }
    }

    public enum HonamiTimelineLoopMode
    {
        PreviewOnly,
        RuntimeLoop,
        RuntimeLoopUntilSignal
    }

    [Preserve]
    [Serializable]
    public sealed class HonamiTimelineWaitMarker : HonamiTimelineMarker
    {
        [Tooltip("The signal that releases this wait. Leave empty to accept any Continue call.")]
        public string signal = "Continue";

        public HonamiTimelineWaitMarker()
        {
            id = "Wait";
        }
    }
}
