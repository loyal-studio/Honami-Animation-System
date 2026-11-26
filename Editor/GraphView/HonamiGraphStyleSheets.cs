using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiGraphStyleSheets
    {
        public const string BaseStyleSheetName = "HonamiGraphStyle";

        public static StyleSheet BaseStyleSheet => Resources.Load<StyleSheet>(BaseStyleSheetName);

        public static void Apply(VisualElement target)
        {
            if (target == null) return;

            Add(target, BaseStyleSheet);

            foreach (HonamiNodeEditorBase editor in HonamiNodeEditorRegistry.EditorByNodeType.Values)
            {
                StyleSheet sheet;
                try
                {
                    sheet = editor.StyleSheet;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Honami] Node editor '{editor.GetType().FullName}' threw while resolving its StyleSheet: {exception}");
                    continue;
                }

                Add(target, sheet);
            }
        }

        private static void Add(VisualElement target, StyleSheet sheet)
        {
            if (sheet == null || target.styleSheets.Contains(sheet)) return;
            target.styleSheets.Add(sheet);
        }
    }
}
