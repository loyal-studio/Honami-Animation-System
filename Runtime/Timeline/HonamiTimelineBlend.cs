using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    public interface IHonamiTimelineBlendItem
    {
        float BlendIn { get; set; }
        float BlendOut { get; set; }
    }

    // Optional companion to IHonamiTimelineBlendItem. An item that does not
    // implement it keeps the classic smooth step crossfade.
    public interface IHonamiTimelineBlendCurveItem : IHonamiTimelineBlendItem
    {
        HonamiBlendCurve BlendInCurve { get; }
        HonamiBlendCurve BlendOutCurve { get; }
    }

    public static class HonamiTimelineBlend
    {
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float Ease(in HonamiBlendCurve curve, float t) => curve.Evaluate(t);

        public static HonamiBlendCurve GetBlendInCurve(HonamiTimelineItem item) =>
            item is IHonamiTimelineBlendCurveItem blend ? blend.BlendInCurve : HonamiBlendCurve.SmoothStep;

        public static HonamiBlendCurve GetBlendOutCurve(HonamiTimelineItem item) =>
            item is IHonamiTimelineBlendCurveItem blend ? blend.BlendOutCurve : HonamiBlendCurve.SmoothStep;

        // normalizedTime runs from 0 at the start of the fade in to 1 at its end.
        public static float BlendInWeight(in HonamiBlendCurve curve, float normalizedTime) =>
            curve.Evaluate(normalizedTime);

        public static float BlendInWeight(HonamiTimelineItem item, float normalizedTime) =>
            BlendInWeight(GetBlendInCurve(item), normalizedTime);

        // normalizedRemaining runs from 1 at the start of the fade out to 0 at its end.
        public static float BlendOutWeight(in HonamiBlendCurve curve, float normalizedRemaining) =>
            1f - curve.Evaluate(1f - Mathf.Clamp01(normalizedRemaining));

        public static float BlendOutWeight(HonamiTimelineItem item, float normalizedRemaining) =>
            BlendOutWeight(GetBlendOutCurve(item), normalizedRemaining);

        public static bool IsActive(HonamiTimelineItem item, float time) =>
            item != null && time >= item.time && time <= item.EndTime;

        // The later of two overlapping items fades in across the overlap, so overlaps read as crossfades.
        public static float GetOverlapIn(HonamiTimelineModule module, HonamiTimelineItem item)
        {
            float overlap = 0f;
            int count = module.ItemCount;
            int index = module.IndexOf(item);
            for (int i = 0; i < count; i++)
            {
                var other = module.GetItem(i);
                if (other == null || other == item || other.IsInstant) continue;
                bool earlier = other.time < item.time || (Mathf.Approximately(other.time, item.time) && i < index);
                if (!earlier || other.EndTime <= item.time) continue;
                overlap = Mathf.Max(overlap, Mathf.Min(other.EndTime, item.EndTime) - item.time);
            }
            return overlap;
        }

        public static float GetOverlapOut(HonamiTimelineModule module, HonamiTimelineItem item)
        {
            float overlap = 0f;
            int count = module.ItemCount;
            int index = module.IndexOf(item);
            for (int i = 0; i < count; i++)
            {
                var other = module.GetItem(i);
                if (other == null || other == item || other.IsInstant) continue;
                bool later = other.time > item.time || (Mathf.Approximately(other.time, item.time) && i > index);
                if (!later || other.time >= item.EndTime) continue;
                overlap = Mathf.Max(overlap, item.EndTime - other.time);
            }
            return overlap;
        }

        public static float GetBlendIn(HonamiTimelineModule module, HonamiTimelineItem item)
        {
            float authored = item is IHonamiTimelineBlendItem blend ? blend.BlendIn : 0f;
            return Mathf.Min(Mathf.Max(authored, GetOverlapIn(module, item)), item.Duration);
        }

        // In Hold nothing fades out into a gap: the item stays until the next one covers it.
        public static float GetBlendOut(HonamiTimelineModule module, HonamiTimelineItem item, bool fadeOutUnderNext, bool hold = false)
        {
            float overlap = GetOverlapOut(module, item);
            if (overlap > 0f) return fadeOutUnderNext ? Mathf.Min(overlap, item.Duration) : 0f;
            if (hold) return 0f;
            float authored = item is IHonamiTimelineBlendItem blend ? blend.BlendOut : 0f;
            return Mathf.Min(authored, item.Duration);
        }

        // fadeOutUnderNext: true for sources that mix additively (audio), false for layered poses where the next item covers this one.
        public static float Weight(HonamiTimelineModule module, HonamiTimelineItem item, float time, bool fadeOutUnderNext, bool hold = false)
        {
            if (!IsActive(item, time)) return 0f;

            float weight = 1f;
            float blendIn = GetBlendIn(module, item);
            if (blendIn > 0f)
                weight *= BlendInWeight(item, (time - item.time) / blendIn);

            float blendOut = GetBlendOut(module, item, fadeOutUnderNext, hold);
            if (blendOut > 0f)
                weight *= BlendOutWeight(item, (item.EndTime - time) / blendOut);

            return weight;
        }

        // In Hold, the item that ended last before time keeps its final frame until another item covers it.
        public static HonamiTimelineItem FindHeldItem(HonamiTimelineModule module, float time)
        {
            HonamiTimelineItem held = null;
            int count = module.ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = module.GetItem(i);
                if (item == null || item.IsInstant || item.EndTime >= time) continue;
                if (held == null || item.EndTime > held.EndTime) held = item;
            }
            return held;
        }

        // Insertion sort into a reusable buffer, so evaluators can layer items by start time without allocating.
        public static int SortByTime(HonamiTimelineModule module, ref int[] order)
        {
            int count = module.ItemCount;
            if (order == null || order.Length < count)
                order = new int[Mathf.Max(4, count * 2)];

            for (int i = 0; i < count; i++)
            {
                float time = module.GetItem(i)?.time ?? float.MaxValue;
                int j = i - 1;
                while (j >= 0 && (module.GetItem(order[j])?.time ?? float.MaxValue) > time)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = i;
            }
            return count;
        }
    }
}
