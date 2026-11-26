using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Builds the shared "nothing selected" placeholder card (icon, title, CTA button and hint)
    /// shown by editor windows when no asset is loaded.
    /// </summary>
    public static class HonamiEmptyStatePanel
    {
        public static VisualElement Box(Texture2D icon, string title, string subtitle)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.06f, 0.065f, 0.075f, 0.98f);
            box.BorderWidth(1);
            box.style.borderTopColor = new Color(0.2f, 0.22f, 0.25f, 0.6f);
            box.style.borderBottomColor = new Color(0.02f, 0.02f, 0.02f, 0.8f);
            box.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            box.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            box.Radius(14);
            box.Padding(40);
            box.style.alignItems = Align.Center;
            box.style.maxWidth = Length.Percent(90);
            box.style.minWidth = 300;

            var iconElement = new VisualElement();
            iconElement.style.width = 72;
            iconElement.style.height = 72;
            iconElement.style.marginBottom = 20;
            if (icon != null)
            {
                iconElement.style.backgroundImage = icon;
                iconElement.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            else
            {
                iconElement.style.backgroundColor = HonamiEditorTheme.AccentSoft;
                iconElement.Radius(14);
            }
            box.Add(iconElement);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 22;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = HonamiEditorTheme.Text;
            titleLabel.style.marginBottom = 8;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(titleLabel);

            var subtitleLabel = new Label(subtitle);
            subtitleLabel.style.fontSize = 15;
            subtitleLabel.style.color = HonamiEditorTheme.MutedText;
            subtitleLabel.style.marginBottom = 30;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(subtitleLabel);

            return box;
        }

        public static Button CTAButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 36;
            btn.style.paddingLeft = btn.style.paddingRight = 30;
            btn.style.backgroundColor = HonamiEditorTheme.Accent;
            btn.style.color = Color.white;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.fontSize = 13;
            btn.Radius(8);
            btn.Border(1, new Color(1, 1, 1, 0.1f));
            btn.style.marginTop = 10;

            btn.RegisterCallback<PointerEnterEvent>(_ =>
            {
                btn.style.backgroundColor = Color.Lerp(HonamiEditorTheme.Accent, Color.white, 0.2f);
                btn.BorderColor(new Color(1, 1, 1, 0.4f));
            });
            btn.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = HonamiEditorTheme.Accent;
                btn.BorderColor(new Color(1, 1, 1, 0.1f));
            });
            btn.RegisterCallback<PointerDownEvent>(_ =>
            {
                btn.style.backgroundColor = Color.Lerp(HonamiEditorTheme.Accent, Color.black, 0.1f);
                btn.BorderColor(new Color(1, 1, 1, 0.2f));
            });
            btn.RegisterCallback<PointerUpEvent>(_ =>
            {
                btn.style.backgroundColor = Color.Lerp(HonamiEditorTheme.Accent, Color.white, 0.2f);
                btn.BorderColor(new Color(1, 1, 1, 0.4f));
            });

            return btn;
        }

        public static Label HintLabel(string text)
        {
            var hint = new Label(text);
            hint.style.fontSize = 12;
            hint.style.color = HonamiEditorTheme.MutedText;
            hint.style.marginTop = 15;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            return hint;
        }
    }
}
