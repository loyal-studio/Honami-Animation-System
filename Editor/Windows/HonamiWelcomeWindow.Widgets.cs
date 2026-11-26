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
        private static VisualElement MakeRippleRing(float size)
        {
            var ring = new VisualElement();
            ring.style.position = Position.Absolute;
            ring.style.width = ring.style.height = size;
            ring.style.left = Length.Percent(50);
            ring.style.top = Length.Percent(50);
            ring.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50), 0);
            ring.Radius(size * 0.5f);
            ring.Border(1.5f, HonamiEditorTheme.Accent);
            ring.style.backgroundColor = Color.clear;
            return ring;
        }

        private VisualElement FeatureCard(string title, string desc, Texture2D icon)
            => Card(title, desc, icon, null);

        private VisualElement ToolCard(string title, string desc, Texture2D icon, Action onClick)
            => Card(title, desc, icon, onClick);

        private VisualElement Card(string title, string desc, Texture2D icon, Action onClick)
        {
            var card = new VisualElement();
            card.style.width = Length.Percent(48.5f);
            card.style.marginBottom = 14;
            card.Padding(16);
            card.style.backgroundColor = CardBg;
            card.Radius(10);
            card.Border(1, CardBorder);
            card.style.transitionProperty = new List<StylePropertyName> { "border-color", "background-color", "translate" };
            card.style.transitionDuration = new List<TimeValue> { new(120, TimeUnit.Millisecond) };
            card.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };

            var iconRow = new VisualElement();
            iconRow.style.flexDirection = FlexDirection.Row;
            iconRow.style.alignItems = Align.Center;
            iconRow.style.justifyContent = Justify.SpaceBetween;
            iconRow.style.marginBottom = 10;

            if (icon != null)
            {
                var img = new Image { image = icon };
                img.style.width = img.style.height = 24;
                img.tintColor = HonamiEditorTheme.Accent;
                iconRow.Add(img);
            }

            if (onClick != null)
            {
                var chevron = new Label("→");
                chevron.style.fontSize = 15;
                chevron.style.unityFontStyleAndWeight = FontStyle.Bold;
                chevron.style.color = FaintText;
                iconRow.Add(chevron);
            }
            card.Add(iconRow);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = TextColor;
            titleLabel.style.marginBottom = 6;
            card.Add(titleLabel);

            var descLabel = new Label(desc);
            descLabel.style.fontSize = 12;
            descLabel.style.color = SoftText;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(descLabel);

            var idleBorder = CardBorder;
            var hoverBorder = onClick != null ? HonamiEditorTheme.Accent : new Color(1f, 1f, 1f, 0.16f);

            card.RegisterCallback<MouseOverEvent>(_ =>
            {
                card.BorderColor(hoverBorder);
                card.style.backgroundColor = CardHoverBg;
                if (onClick != null) card.style.translate = new Translate(0, -2, 0);
            });
            card.RegisterCallback<MouseOutEvent>(_ =>
            {
                card.BorderColor(idleBorder);
                card.style.backgroundColor = CardBg;
                card.style.translate = new Translate(0, 0, 0);
            });

            if (onClick != null)
            {
                card.RegisterCallback<ClickEvent>(_ => onClick());
            }

            return card;
        }

        private VisualElement SetupStep(string number, string title, string desc, Texture2D icon, string btnText, Action onBtn)
        {
            var step = new VisualElement();
            step.style.flexDirection = FlexDirection.Row;
            step.style.marginBottom = 16;
            step.Padding(16, 20);
            step.style.backgroundColor = CardBg;
            step.Radius(10);
            step.Border(1, CardBorder);

            var numBox = new VisualElement();
            numBox.style.width = numBox.style.height = 36;
            numBox.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
            numBox.Radius(18);
            numBox.style.alignItems = Align.Center;
            numBox.style.justifyContent = Justify.Center;
            numBox.style.marginRight = 16;
            numBox.style.flexShrink = 0;

            var numLabel = new Label(number);
            numLabel.style.fontSize = 16;
            numLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            numLabel.style.color = HonamiEditorTheme.Accent;
            numBox.Add(numLabel);
            step.Add(numBox);

            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.justifyContent = Justify.Center;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;

            if (icon != null)
            {
                var img = new Image { image = icon };
                img.style.width = img.style.height = 16;
                img.tintColor = HonamiEditorTheme.Accent;
                img.style.marginRight = 8;
                header.Add(img);
            }

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = TextColor;
            header.Add(titleLabel);
            content.Add(header);

            var descLabel = new Label(desc);
            descLabel.style.fontSize = 12;
            descLabel.style.color = SoftText;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            content.Add(descLabel);

            step.Add(content);

            if (onBtn != null)
            {
                var btnWrapper = new VisualElement();
                btnWrapper.style.justifyContent = Justify.Center;
                btnWrapper.style.marginLeft = 16;
                btnWrapper.style.flexShrink = 0;

                var btn = new Button(onBtn) { text = btnText };
                btn.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                btn.style.color = TextColor;
                btn.BorderWidth(0);
                btn.Radius(4);
                btn.Padding(6, 12);
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                
                btn.RegisterCallback<MouseOverEvent>(_ => btn.style.backgroundColor = HonamiEditorTheme.Accent);
                btn.RegisterCallback<MouseOutEvent>(_ => btn.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f));

                btnWrapper.Add(btn);
                step.Add(btnWrapper);
            }

            return step;
        }

        private static VisualElement Halo(float size, float alpha)
        {
            var halo = new VisualElement();
            halo.style.position = Position.Absolute;
            halo.style.width = halo.style.height = size;
            halo.style.left = Length.Percent(50);
            halo.style.top = Length.Percent(50);
            halo.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50), 0);
            halo.Radius(size * 0.5f);
            var color = HonamiEditorTheme.Accent;
            color.a = alpha;
            halo.style.backgroundColor = color;
            return halo;
        }

        private static VisualElement Pill(string text, Color bg, Color fg)
        {
            var pill = new Label(text);
            pill.style.backgroundColor = bg;
            pill.style.color = fg;
            pill.style.fontSize = 10;
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.style.letterSpacing = 1.5f;
            pill.Padding(2, 8);
            pill.Radius(4);
            return pill;
        }

        private static Button PrimaryButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 40;
            btn.style.minWidth = 132;
            btn.style.paddingLeft = btn.style.paddingRight = 22;
            btn.style.marginTop = btn.style.marginBottom = 0;
            btn.style.fontSize = 13;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.color = Color.white;
            btn.style.backgroundColor = HonamiEditorTheme.Accent;
            btn.BorderWidth(0);
            btn.Radius(8);
            btn.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            btn.style.transitionDuration = new List<TimeValue> { new(120, TimeUnit.Millisecond) };

            btn.RegisterCallback<MouseOverEvent>(_ => btn.style.backgroundColor = Color.Lerp(HonamiEditorTheme.Accent, Color.white, 0.2f));
            btn.RegisterCallback<MouseOutEvent>(_ => btn.style.backgroundColor = HonamiEditorTheme.Accent);
            return btn;
        }

        private static Button GhostButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 40;
            btn.style.paddingLeft = btn.style.paddingRight = 18;
            btn.style.marginTop = btn.style.marginBottom = 0;
            btn.style.marginLeft = 0;
            btn.style.fontSize = 13;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.color = SoftText;
            btn.style.backgroundColor = Color.clear;
            btn.Border(1, new Color(1f, 1f, 1f, 0.14f));
            btn.Radius(8);
            btn.style.transitionProperty = new List<StylePropertyName> { "background-color", "color" };
            btn.style.transitionDuration = new List<TimeValue> { new(120, TimeUnit.Millisecond) };

            btn.RegisterCallback<MouseOverEvent>(_ =>
            {
                btn.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
                btn.style.color = TextColor;
            });
            btn.RegisterCallback<MouseOutEvent>(_ =>
            {
                btn.style.backgroundColor = Color.clear;
                btn.style.color = SoftText;
            });
            return btn;
        }

        private static Label TextLink(string text, Action onClick)
        {
            var link = new Label(text);
            link.style.fontSize = 12;
            link.style.unityFontStyleAndWeight = FontStyle.Bold;
            link.style.color = HonamiEditorTheme.MutedText;
            link.RegisterCallback<MouseOverEvent>(_ => link.style.color = HonamiEditorTheme.Accent);
            link.RegisterCallback<MouseOutEvent>(_ => link.style.color = HonamiEditorTheme.MutedText);
            link.RegisterCallback<MouseDownEvent>(_ => onClick());
            return link;
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button != null) button.text = text;
        }
    }
}
