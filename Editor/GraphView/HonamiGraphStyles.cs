using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiGraphStyles
    {
        public static Color PanelBg => HonamiEditorTheme.PanelBg;
        public static Color BoxBg => HonamiEditorTheme.BoxBg;
        public static readonly Color BoxBorder = new(0.045f, 0.048f, 0.054f);
        public static readonly Color ListBoxBg = new(0.115f, 0.124f, 0.138f);
        public static readonly Color ListBoxBorder = new(0.055f, 0.06f, 0.068f);
        public static readonly Color TitleClr = new(0.95f, 0.95f, 0.96f);
        public static Color SubTitleClr => HonamiEditorTheme.Accent;
        public static readonly Color GreyText = new(0.65f, 0.65f, 0.65f);
        public static Color Accent => HonamiEditorTheme.Accent;
        public static Color AccentDim => HonamiEditorTheme.AccentDim;
        public static Color AccentHeader => HonamiEditorTheme.Accent;
        public static readonly Color AxisRed = new(1.00f, 0.40f, 0.40f);
        public static readonly Color AxisGreen = new(0.40f, 1.00f, 0.40f);
        public static readonly Color AxisBlue = new(0.40f, 0.60f, 1.00f);

        public static readonly Color Green = new(0.35f, 0.85f, 0.55f);
        public static readonly Color Red = new(0.85f, 0.35f, 0.35f);
        public static readonly Color Orange = new(0.90f, 0.75f, 0.20f);
        public static Color WindowBg => HonamiEditorTheme.WindowBg;

        public static Label Title(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 16;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.color = TitleClr;
            l.style.marginTop = l.style.marginBottom = 12;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        public static Label SubTitle(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 13;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = SubTitleClr;
            l.style.marginTop = 4;
            l.style.marginBottom = 10;
            return l;
        }

        public static Label MiniLabel(string text, Color? color = null)
        {
            var l = new Label(text);
            l.style.fontSize = 10;
            l.style.color = color ?? GreyText;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        public static VisualElement Box()
        {
            var e = new VisualElement();
            ApplyBox(e);
            return e;
        }

        public static void ApplyBox(VisualElement e)
        {
            e.style.backgroundColor = BoxBg;
            e.style.borderTopColor = e.style.borderBottomColor =
            e.style.borderLeftColor = e.style.borderRightColor = BoxBorder;
            e.style.borderTopWidth = e.style.borderBottomWidth =
            e.style.borderLeftWidth = e.style.borderRightWidth = 1;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius =
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
            e.style.paddingTop = e.style.paddingBottom = 12;
            e.style.paddingLeft = e.style.paddingRight = 12;
            e.style.marginTop = e.style.marginBottom = 6;
            e.style.marginLeft = e.style.marginRight = 0;
        }

        public static VisualElement ListBox()
        {
            var e = new VisualElement();
            ApplyListBox(e);
            return e;
        }

        public static void ApplyListBox(VisualElement e)
        {
            e.style.backgroundColor = ListBoxBg;
            e.style.borderTopColor = e.style.borderBottomColor =
            e.style.borderLeftColor = e.style.borderRightColor = ListBoxBorder;
            e.style.borderTopWidth = e.style.borderBottomWidth =
            e.style.borderLeftWidth = e.style.borderRightWidth = 1;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius =
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
            e.style.paddingTop = e.style.paddingBottom = 8;
            e.style.paddingLeft = e.style.paddingRight = 10;
            e.style.marginTop = e.style.marginBottom = 4;
            e.style.marginLeft = e.style.marginRight = 0;
        }

        public static VisualElement Row()
        {
            var e = new VisualElement();
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            e.style.marginBottom = 2;
            return e;
        }

        public static VisualElement Spacer()
        {
            var e = new VisualElement();
            e.style.flexGrow = 1;
            return e;
        }

        public static VisualElement Separator()
        {
            var e = new VisualElement();
            e.style.height = 1;
            e.style.backgroundColor = new Color(0.32f, 0.34f, 0.36f, 0.32f);
            e.style.marginTop = e.style.marginBottom = 8;
            return e;
        }

        public static Button SmallButton(string text, System.Action onClick = null)
        {
            var b = new Button(onClick) { text = text };
            b.style.width = 25;
            b.style.height = 20;
            b.style.paddingLeft = b.style.paddingRight = 2;
            b.style.paddingTop = b.style.paddingBottom = 0;
            b.style.fontSize = 11;
            return b;
        }

        public static Button TallButton(string text, System.Action onClick = null)
        {
            var b = new Button(onClick) { text = text };
            b.style.height = 35;
            b.style.marginTop = b.style.marginBottom = 4;
            b.style.fontSize = 12;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            return b;
        }

        public static Button TallButton(string text, Texture2D icon, System.Action onClick = null)
        {
            Button b = new(onClick);
            b.style.height = 35;
            b.style.marginTop = b.style.marginBottom = 4;
            b.style.flexDirection = FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            b.style.paddingLeft = b.style.paddingRight = 12;

            if (icon != null)
            {
                Image img = new() { image = icon };
                img.style.width = 22;
                img.style.height = 22;
                img.style.marginRight = 10;
                img.style.flexShrink = 0;
                b.Add(img);
            }

            Label lbl = new(text);
            lbl.style.fontSize = 12;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = Color.white;
            lbl.style.paddingLeft = lbl.style.paddingRight = 0;
            lbl.style.marginTop = 0;
            lbl.style.marginBottom = 0;
            b.Add(lbl);

            return b;
        }

        public static Foldout Foldout(string title, bool expanded = true)
        {
            var f = new Foldout { text = title, value = expanded };
            f.style.marginTop = 10;
            f.style.marginBottom = 4;
            f.style.unityFontStyleAndWeight = FontStyle.Bold;
            var toggle = f.Q<Toggle>();
            if (toggle != null)
            {
                var label = toggle.Q<Label>();
                if (label != null)
                {
                    label.style.color = SubTitleClr;
                    label.style.fontSize = 13;
                    label.style.marginLeft = 4;
                }
                toggle.style.backgroundColor = BoxBg;
                toggle.style.borderTopColor = toggle.style.borderBottomColor =
                toggle.style.borderLeftColor = toggle.style.borderRightColor = BoxBorder;
                toggle.style.borderTopWidth = toggle.style.borderBottomWidth =
                toggle.style.borderLeftWidth = toggle.style.borderRightWidth = 1;
                toggle.style.borderTopLeftRadius = toggle.style.borderTopRightRadius =
                toggle.style.borderBottomLeftRadius = toggle.style.borderBottomRightRadius = 6;
                toggle.style.paddingTop = toggle.style.paddingBottom = 6;
                toggle.style.paddingLeft = 6;
                toggle.style.marginBottom = 4;
            }
            return f;
        }

        public static HelpBox Warning(string text) => new HelpBox(text, HelpBoxMessageType.Warning);
    }
}
