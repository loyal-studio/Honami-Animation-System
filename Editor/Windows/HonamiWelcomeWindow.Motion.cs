using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Documentation;
using HonamiAnimationSystem.Editor.Timeline;
using HonamiAnimationSystem.Editor.Windows;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed partial class HonamiWelcomeWindow
    {
        private static void PulseRing(VisualElement ring, long delayMs)
        {
            ring.style.opacity = 0f;
            ring.style.scale = new Scale(new Vector3(0.25f, 0.25f, 1f));
            ring.style.transitionProperty = new List<StylePropertyName> { "scale", "opacity" };
            ring.style.transitionDuration = new List<TimeValue>
            {
                new(1600, TimeUnit.Millisecond),
                new(700, TimeUnit.Millisecond)
            };
            ring.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseInOut)
            };

            ring.schedule.Execute(() =>
            {
                ring.style.opacity = 0.55f;
                ring.style.scale = new Scale(new Vector3(2.4f, 2.4f, 1f));
            }).StartingIn(delayMs);

            ring.schedule.Execute(() =>
            {
                ring.style.opacity = 0f;
            }).StartingIn(delayMs + 900);
        }

        private static void DismissIntroOverlay(VisualElement overlay)
        {
            overlay.style.transitionProperty = new List<StylePropertyName> { "opacity", "scale" };
            overlay.style.transitionDuration = new List<TimeValue>
            {
                new(500, TimeUnit.Millisecond),
                new(500, TimeUnit.Millisecond)
            };
            overlay.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseInCubic) };

            overlay.schedule.Execute(() =>
            {
                overlay.style.opacity = 0f;
                overlay.style.scale = new Scale(new Vector3(1.055f, 1.055f, 1f));
            }).StartingIn(16);

            overlay.schedule.Execute(() =>
            {
                overlay.RemoveFromHierarchy();
                SessionState.SetBool(IntroPlayedKey, true);
            }).StartingIn(530);
        }

        private static void AnimateIn(VisualElement element)
        {
            element.style.opacity = 0f;
            element.style.translate = new Translate(0, 14, 0);
            element.style.transitionProperty = new List<StylePropertyName> { "opacity", "translate" };
            element.style.transitionDuration = new List<TimeValue>
            {
                new(260, TimeUnit.Millisecond),
                new(340, TimeUnit.Millisecond)
            };
            element.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };

            element.schedule.Execute(() =>
            {
                element.style.opacity = 1f;
                element.style.translate = new Translate(0, 0, 0);
            }).StartingIn(16);
        }

        private static void SpringPopIn(VisualElement element, long delayMs = 0)
        {
            element.style.opacity = 0f;
            element.style.scale = new Scale(new Vector3(0.55f, 0.55f, 1f));
            element.style.transitionProperty = new List<StylePropertyName> { "opacity", "scale" };
            element.style.transitionDuration = new List<TimeValue>
            {
                new(220, TimeUnit.Millisecond),
                new(480, TimeUnit.Millisecond)
            };
            element.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOutCubic),
                new(EasingMode.EaseOutBack)
            };

            element.schedule.Execute(() =>
            {
                element.style.opacity = 1f;
                element.style.scale = new Scale(Vector3.one);
            }).StartingIn(delayMs > 0 ? delayMs : 16);
        }

        private static void FadeSlideIn(VisualElement element, long delayMs)
        {
            element.style.opacity = 0f;
            element.style.translate = new Translate(0, 12, 0);
            element.style.transitionProperty = new List<StylePropertyName> { "opacity", "translate" };
            element.style.transitionDuration = new List<TimeValue>
            {
                new(280, TimeUnit.Millisecond),
                new(320, TimeUnit.Millisecond)
            };
            element.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };

            element.schedule.Execute(() =>
            {
                element.style.opacity = 1f;
                element.style.translate = new Translate(0, 0, 0);
            }).StartingIn(delayMs);
        }
    }
}
