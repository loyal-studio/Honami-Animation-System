using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiInspectorSectionRegistry
    {
        private static List<HonamiStateInspectorSection> _stateSections;
        private static List<HonamiTransitionInspectorSection> _transitionSections;

        public static IReadOnlyList<HonamiStateInspectorSection> StateSections
            => _stateSections ??= Collect<HonamiStateInspectorSection>();

        public static IReadOnlyList<HonamiTransitionInspectorSection> TransitionSections
            => _transitionSections ??= Collect<HonamiTransitionInspectorSection>();

        [InitializeOnLoadMethod]
        private static void Rebuild()
        {
            _stateSections = null;
            _transitionSections = null;
        }

        private static List<T> Collect<T>() where T : class
        {
            var ordered = new List<(int order, T section)>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"[Honami] Inspector section '{type.FullName}' needs a public parameterless constructor.");
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(type) is not T section) continue;
                    var attribute = (HonamiInspectorSectionAttribute)Attribute.GetCustomAttribute(type, typeof(HonamiInspectorSectionAttribute));
                    ordered.Add((attribute?.Order ?? 100, section));
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Honami] Inspector section '{type.FullName}' failed to construct: {exception.Message}");
                }
            }

            ordered.Sort((a, b) => a.order.CompareTo(b.order));

            var result = new List<T>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++) result.Add(ordered[i].section);
            return result;
        }

        public static void AppendStateSections(VisualElement root, HonamiStateSectionContext context)
        {
            IReadOnlyList<HonamiStateInspectorSection> sections = StateSections;
            for (int i = 0; i < sections.Count; i++)
            {
                HonamiStateInspectorSection section = sections[i];
                VisualElement content;
                try
                {
                    if (!section.IsVisible(context)) continue;
                    content = section.Build(context);
                }
                catch (Exception exception)
                {
                    content = LogAndReport(section, exception);
                }

                if (content != null) root.Add(Wrap(content, section.Title, section.DefaultExpanded));
            }
        }

        public static void AppendTransitionSections(VisualElement root, HonamiTransitionSectionContext context)
        {
            IReadOnlyList<HonamiTransitionInspectorSection> sections = TransitionSections;
            for (int i = 0; i < sections.Count; i++)
            {
                HonamiTransitionInspectorSection section = sections[i];
                VisualElement content;
                try
                {
                    if (!section.IsVisible(context)) continue;
                    content = section.Build(context);
                }
                catch (Exception exception)
                {
                    content = LogAndReport(section, exception);
                }

                if (content != null) root.Add(Wrap(content, section.Title, section.DefaultExpanded));
            }
        }

        private static VisualElement LogAndReport(object section, Exception exception)
        {
            Debug.LogError($"[Honami] Inspector section '{section.GetType().FullName}' threw while building: {exception}");
            return new HelpBox($"{section.GetType().Name} failed to draw. See the Console.", HelpBoxMessageType.Error);
        }

        private static VisualElement Wrap(VisualElement content, string title, bool expanded)
        {
            if (string.IsNullOrEmpty(title)) return content;

            Foldout foldout = HonamiGraphStyles.Foldout(title, expanded);
            foldout.Add(content);
            return foldout;
        }
    }
}
