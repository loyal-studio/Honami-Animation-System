using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Factory for the shared toolbar look: buttons, icon buttons and toggles with the
    /// common Honami hover/press styling, reused across the editor windows.
    /// </summary>
    public static class HonamiToolbarControls
    {
        public static Button ToolbarButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            StyleToolbarElement(button);
            return button;
        }

        public static Button IconButton(string iconName, string fallback, Action onClick)
        {
            var button = ToolbarButton("", onClick);
            button.tooltip = fallback;
            button.style.paddingLeft = button.style.paddingRight = 0;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;

            var icon = EditorGUIUtility.IconContent(iconName).image;
            if (icon != null)
            {
                button.Add(new Image { image = icon, style = { width = 16, height = 16, flexShrink = 0 } });
                button.style.width = 32;
            }
            else
            {
                button.text = fallback;
                button.style.minWidth = 45;
                button.style.paddingLeft = button.style.paddingRight = 7;
            }
            return button;
        }

        public static Toggle ToolbarToggle(string text, bool value, Action<bool> changed)
        {
            var toggle = new Toggle { text = text, value = value };
            StyleToolbarElement(toggle);
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return toggle;
        }

       public static VisualElement AccentStrip()
        {
            return new VisualElement
            {
                style =
                {
                    width = 3,
                    height = Length.Percent(100),
                    backgroundColor = HonamiEditorTheme.Accent,
                    marginRight = 8,
                    flexShrink = 0
                }
            };
        }

        public static VisualElement Separator()
        {
            return new VisualElement
            {
                style =
                {
                    width = 1,
                    height = 18,
                    backgroundColor = HonamiEditorTheme.SubtleLine,
                    marginLeft = 6,
                    marginRight = 6,
                    flexShrink = 0
                }
            };
        }

        public static VisualElement FlexibleSpace() => new() { style = { flexGrow = 1 } };

        public static void StyleToolbarElement(VisualElement element)
        {
            element.style.height = 24;
            element.style.marginLeft = 2;
            element.style.marginRight = 2;
            element.style.paddingLeft = 7;
            element.style.paddingRight = 7;
           element.style.backgroundColor = HonamiEditorTheme.ToolbarButton;
            element.Border(1, HonamiEditorTheme.SubtleLine).Radius(4);

            element.RegisterCallback<PointerEnterEvent>(_ => { if (element.enabledSelf) SetToolbarElementState(element, HonamiEditorTheme.ToolbarButtonHot, HonamiEditorTheme.AccentDim); });
            element.RegisterCallback<PointerLeaveEvent>(_ => SetToolbarElementState(element, HonamiEditorTheme.ToolbarButton, HonamiEditorTheme.SubtleLine));
            element.RegisterCallback<PointerDownEvent>(_ => { if (element.enabledSelf) SetToolbarElementState(element, HonamiEditorTheme.ToolbarButtonPressed, HonamiEditorTheme.Accent); });
            element.RegisterCallback<PointerUpEvent>(_ => { if (element.enabledSelf) SetToolbarElementState(element, HonamiEditorTheme.ToolbarButtonHot, HonamiEditorTheme.AccentDim); });
        }

        private static void SetToolbarElementState(VisualElement element, Color background, Color border)
        {
            element.style.backgroundColor = background;
            element.BorderColor(border);
        }
    }
}
