using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public interface ITimelineTrackDrawer
    {
        void AddReadonlyClip(AnimationClip clipAsset, string label, float start, float duration, float y, Color color);
        void AddClipElement(object target, string label, float start, float duration, float y, bool selected, Color color);

        void AddClipWithHandles(
            int dragId,
            string label,
            float start,
            float duration,
            float speed,
            float y,
            Color themeColor,
            Action<float> onTrimStart,
            Action<float> onTrimEnd,
            Action onDoubleClick,
            Action onDragEnd,
            float maxDuration = 1000f);
    }
}
