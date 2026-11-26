using System;
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    // Read as fade amount over normalized blend time (0 = nothing faded, 1 = done) for fades in and out alike.
    [Preserve]
    [Serializable]
    public struct HonamiBlendCurve
    {
        [Tooltip("Easing preset for the fade. Smooth Step matches the classic Honami crossfade.")]
        public HonamiTransitionEase ease;

        [Tooltip("Use the custom curve below instead of the preset.")]
        public bool useCustomCurve;

        [Tooltip("Fade amount over normalized blend time: 0 = nothing faded, 1 = fully faded. Values outside 0..1 are allowed so overshooting eases can add punch.")]
        public AnimationCurve customCurve;

        private static AnimationCurve _linearCurve;

        public static HonamiBlendCurve SmoothStep => new() { ease = HonamiTransitionEase.SmoothStep };
        public static HonamiBlendCurve Linear => new() { ease = HonamiTransitionEase.Linear };

        public bool HasCustomCurve => useCustomCurve && customCurve != null && customCurve.length > 0;

        public float Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            if (HasCustomCurve)
                return customCurve.Evaluate(t);
            return HonamiTransitionEasing.Evaluate(ease, t);
        }

        // The Animator transition pipeline takes an AnimationCurve, so presets are
        // passed as their cached baked form to keep animation tracks in sync with
        // the ramps drawn in the Timeline window.
        public AnimationCurve ToAnimationCurve()
        {
            if (HasCustomCurve) return customCurve;
            if (ease == HonamiTransitionEase.Linear) return _linearCurve ??= AnimationCurve.Linear(0f, 0f, 1f, 1f);
            return HonamiTransitionEasing.GetCurve(ease);
        }
    }
}
