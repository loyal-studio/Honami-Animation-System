using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public abstract class HonamiTimelineItem
    {
        public const float MinDuration = 0.01f;

        [SerializeField, HideInInspector] private string id;
        [Min(0f)] public float time;

        // Editing-time link group. Items sharing a group are dragged together on the timeline,
        // each keeping its own offset from the others. Empty when the item is not linked.
        [SerializeField, HideInInspector] private string linkGroup;

        public string Id => id;
        public string LinkGroup => linkGroup;
        public bool IsLinked => !string.IsNullOrEmpty(linkGroup);
        public virtual float Duration => 0f;
        public virtual bool IsInstant => false;
        public virtual bool CanResize => false;
        public float EndTime => time + Duration;

        // Ids are only needed once a module is nested under the item, so they are assigned on demand.
        public string EnsureId()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
            return id;
        }

        public void ClearId() => id = null;

        public void SetLinkGroup(string group) => linkGroup = string.IsNullOrEmpty(group) ? null : group;
        public void ClearLinkGroup() => linkGroup = null;

        public virtual void SetDuration(float duration) { }

        public virtual void TrimStart(float newStartTime)
        {
            float end = EndTime;
            time = Mathf.Clamp(newStartTime, 0f, end - MinDuration);
            SetDuration(end - time);
        }
    }
}
