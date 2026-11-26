using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class TimelineScreenFadeItem : HonamiTimelineItem
    {
        [Min(0f)] public float delay;
        [Min(0.001f)] public float length = 1f;
        public Color color = Color.black;
        [Range(0f, 1f)] public float fromOpacity;
        [Range(0f, 1f)] public float toOpacity = 1f;
        [Tooltip("Ease in and out instead of a linear ramp")]
        public bool smooth = true;
        [Tooltip("Shape of the opacity ramp when Smooth is on: a preset ease or a custom curve.")]
        public HonamiBlendCurve opacityCurve = HonamiBlendCurve.SmoothStep;

        public override float Duration => delay + length;
        public override bool CanResize => true;

        public override void SetDuration(float duration)
        {
            float total = Mathf.Max(MinDuration, duration);
            if (total <= delay)
            {
                delay = total - MinDuration;
                length = MinDuration;
            }
            else
            {
                length = total - delay;
            }
        }

        public override void TrimStart(float newStartTime)
        {
            float end = EndTime;
            time = Mathf.Clamp(newStartTime, 0f, end - MinDuration);
            float remaining = end - time;
            if (remaining <= delay)
            {
                delay = remaining - MinDuration;
                length = MinDuration;
            }
            else
            {
                length = remaining - delay;
            }
        }

        public float OpacityAt(float localTime)
        {
            if (localTime <= delay) return fromOpacity;
            float t = Mathf.Clamp01((localTime - delay) / length);
            if (smooth) t = opacityCurve.Evaluate(t);
            return Mathf.Lerp(fromOpacity, toOpacity, t);
        }
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineScreenFadeModule : HonamiTimelineModule<TimelineScreenFadeItem>
    {
        [Tooltip("Sorting order of the fade overlay canvas; raise it to cover other overlay UI")]
        public int sortingOrder = 32000;

        public override HonamiTimelineModuleHosts SupportedHosts => HonamiTimelineModuleHosts.Timeline;

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context) => new Evaluator(this);

        // After an item ends its final opacity holds until the next item, so a fade to black stays black through the cut.
        public Color Evaluate(float time)
        {
            TimelineScreenFadeItem inside = null;
            TimelineScreenFadeItem lastEnded = null;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                if (time >= item.time && time <= item.EndTime)
                {
                    if (inside == null || item.time >= inside.time) inside = item;
                }
                else if (item.EndTime < time && (lastEnded == null || item.EndTime > lastEnded.EndTime))
                {
                    lastEnded = item;
                }
            }

            if (inside != null)
            {
                var color = inside.color;
                color.a *= inside.OpacityAt(time - inside.time);
                return color;
            }

            if (lastEnded != null)
            {
                var color = lastEnded.color;
                color.a *= lastEnded.toOpacity;
                return color;
            }

            return Color.clear;
        }

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineScreenFadeModule _module;
            private HonamiScreenOverlay _overlay;

            public Evaluator(TimelineScreenFadeModule module) => _module = module;

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                var color = _module.Evaluate(frame.Time);
                if (_overlay == null || !_overlay.IsAlive)
                {
                    if (color.a <= 0f) return;
                    _overlay = new HonamiScreenOverlay("Honami Screen Fade", HideFlags.DontSave, _module.sortingOrder);
                }
                _overlay.SetColor(color);
            }

            public override void OnStop() => _overlay?.SetColor(Color.clear);

            public override void Dispose()
            {
                _overlay?.Dispose();
                _overlay = null;
            }
        }
    }
}
