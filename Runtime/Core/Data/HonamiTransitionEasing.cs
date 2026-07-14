using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Penner easing functions for <see cref="HonamiTransitionEase"/>, exposed both as direct
    /// math evaluation and as shared cached <see cref="AnimationCurve"/> presets.
    /// </summary>
    public static class HonamiTransitionEasing
    {
        private static readonly AnimationCurve[] _curves = new AnimationCurve[31];

        // Presets are baked into curves so they travel through the existing
        // AnimationCurve plumbing (runtime, preview, script API) unchanged.
        public static AnimationCurve GetCurve(HonamiTransitionEase ease)
        {
            int index = (int)ease;
            if (index <= 0 || index >= _curves.Length) return null;
            return _curves[index] ??= Bake(ease);
        }

        private static AnimationCurve Bake(HonamiTransitionEase ease)
        {
            int segments = ease >= HonamiTransitionEase.InElastic ? 96 : 24;
            var keys = new Keyframe[segments + 1];
            const float h = 1e-3f;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float t0 = Mathf.Max(0f, t - h);
                float t1 = Mathf.Min(1f, t + h);
                float tangent = (Evaluate(ease, t1) - Evaluate(ease, t0)) / (t1 - t0);
                keys[i] = new Keyframe(t, Evaluate(ease, t), tangent, tangent);
            }

            return new AnimationCurve(keys);
        }

        public static float Evaluate(HonamiTransitionEase ease, float t)
        {
            t = Mathf.Clamp01(t);

            const float backC1 = 1.70158f;
            const float backC3 = backC1 + 1f;
            const float backC2 = backC1 * 1.525f;
            const float elasticC4 = 2f * Mathf.PI / 3f;
            const float elasticC5 = 2f * Mathf.PI / 4.5f;

            switch (ease)
            {
                case HonamiTransitionEase.InSine:
                    return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                case HonamiTransitionEase.OutSine:
                    return Mathf.Sin(t * Mathf.PI * 0.5f);
                case HonamiTransitionEase.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

                case HonamiTransitionEase.InQuad:
                    return t * t;
                case HonamiTransitionEase.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case HonamiTransitionEase.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

                case HonamiTransitionEase.InCubic:
                    return t * t * t;
                case HonamiTransitionEase.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case HonamiTransitionEase.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

                case HonamiTransitionEase.InQuart:
                    return t * t * t * t;
                case HonamiTransitionEase.OutQuart:
                    return 1f - Mathf.Pow(1f - t, 4f);
                case HonamiTransitionEase.InOutQuart:
                    return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f;

                case HonamiTransitionEase.InQuint:
                    return t * t * t * t * t;
                case HonamiTransitionEase.OutQuint:
                    return 1f - Mathf.Pow(1f - t, 5f);
                case HonamiTransitionEase.InOutQuint:
                    return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) * 0.5f;

                case HonamiTransitionEase.InExpo:
                    return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case HonamiTransitionEase.OutExpo:
                    return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case HonamiTransitionEase.InOutExpo:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f
                        ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
                        : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;

                case HonamiTransitionEase.InCirc:
                    return 1f - Mathf.Sqrt(1f - t * t);
                case HonamiTransitionEase.OutCirc:
                    return Mathf.Sqrt(1f - (t - 1f) * (t - 1f));
                case HonamiTransitionEase.InOutCirc:
                    return t < 0.5f
                        ? (1f - Mathf.Sqrt(1f - 4f * t * t)) * 0.5f
                        : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) * 0.5f;

                case HonamiTransitionEase.InBack:
                    return backC3 * t * t * t - backC1 * t * t;
                case HonamiTransitionEase.OutBack:
                {
                    float u = t - 1f;
                    return 1f + backC3 * u * u * u + backC1 * u * u;
                }
                case HonamiTransitionEase.InOutBack:
                    return t < 0.5f
                        ? Mathf.Pow(2f * t, 2f) * ((backC2 + 1f) * 2f * t - backC2) * 0.5f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((backC2 + 1f) * (2f * t - 2f) + backC2) + 2f) * 0.5f;

                case HonamiTransitionEase.InElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((10f * t - 10.75f) * elasticC4);
                case HonamiTransitionEase.OutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((10f * t - 0.75f) * elasticC4) + 1f;
                case HonamiTransitionEase.InOutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * elasticC5)) * 0.5f
                        : Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * elasticC5) * 0.5f + 1f;

                case HonamiTransitionEase.InBounce:
                    return 1f - OutBounce(1f - t);
                case HonamiTransitionEase.OutBounce:
                    return OutBounce(t);
                case HonamiTransitionEase.InOutBounce:
                    return t < 0.5f
                        ? (1f - OutBounce(1f - 2f * t)) * 0.5f
                        : (1f + OutBounce(2f * t - 1f)) * 0.5f;

                default:
                    return t;
            }
        }

        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
