using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace HonamiAnimationSystem.Editor.Documentation
{
    public static class HonamiDocumentationBuilder
    {
        public static void AddHeader(VisualElement root, string text, Texture2D icon = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 32;
            row.style.marginBottom = 16;

            if (icon != null)
            {
                var img = new Image { image = icon };
                img.style.width = 24;
                img.style.height = 24;
                img.style.marginRight = 12;
                img.tintColor = HonamiGraphStyles.Accent;
                row.Add(img);
            }

            var header = new Label(text);
            header.style.fontSize = 22;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = HonamiGraphStyles.TitleClr;
            header.style.letterSpacing = 0.5f;
            row.Add(header);

            root.Add(row);

            var line = new VisualElement();
            line.style.height = 3;
            line.style.backgroundColor = HonamiGraphStyles.Accent;
            line.style.width = 50;
            line.style.marginBottom = 16;
            line.style.borderTopLeftRadius = line.style.borderTopRightRadius =
            line.style.borderBottomLeftRadius = line.style.borderBottomRightRadius = 1.5f;
            root.Add(line);
        }

        public static void AddSeparator(VisualElement root)
        {
            var sep = new VisualElement();
            sep.style.height = 1;
            sep.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
            sep.style.marginTop = 20;
            sep.style.marginBottom = 20;
            root.Add(sep);
        }

        public static void AddParagraph(VisualElement root, string text)
        {
            var p = new Label(text);
            p.style.fontSize = 14;
            p.style.color = new Color(0.90f, 0.92f, 0.94f);
            p.style.whiteSpace = WhiteSpace.Normal;
            p.style.marginBottom = 16;
            p.style.unityTextAlign = TextAnchor.MiddleLeft;
            p.style.unityFontStyleAndWeight = FontStyle.Normal;

            p.style.paddingLeft = 2;
            p.style.paddingRight = 2;

            root.Add(p);
        }

        public static void AddSpace(VisualElement root, float pixels = 20)
        {
            var s = new VisualElement();
            s.style.height = pixels;
            root.Add(s);
        }

        public static void AddBulletPoint(VisualElement root, string text)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 10;
            row.style.marginLeft = 16;

            var bullet = new VisualElement();
            bullet.style.width = 6;
            bullet.style.height = 6;
            bullet.style.backgroundColor = HonamiGraphStyles.Accent;
            bullet.style.marginRight = 12;
            bullet.style.marginTop = 7;
            bullet.style.borderTopLeftRadius = bullet.style.borderTopRightRadius =
            bullet.style.borderBottomLeftRadius = bullet.style.borderBottomRightRadius = 3;

            var p = new Label(text);
            p.style.fontSize = 13.5f;
            p.style.color = new Color(0.88f, 0.88f, 0.88f);
            p.style.whiteSpace = WhiteSpace.Normal;
            p.style.flexShrink = 1;

            row.Add(bullet);
            row.Add(p);
            root.Add(row);
        }

        public static void AddCallout(VisualElement root, string text, CalloutType type = CalloutType.Info)
        {
            var box = new VisualElement();
            box.style.paddingTop = box.style.paddingBottom = 12;
            box.style.paddingLeft = box.style.paddingRight = 16;
            box.style.marginTop = 12;
            box.style.marginBottom = 12;
            box.style.borderLeftWidth = 4;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius =
            box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 4;

            Color accentColor = type switch
            {
                CalloutType.Info => new Color(0.2f, 0.6f, 1.0f),
                CalloutType.Warning => new Color(1.0f, 0.7f, 0.1f),
                CalloutType.Tip => HonamiGraphStyles.Green,
                CalloutType.Danger => HonamiGraphStyles.Red,
                _ => HonamiGraphStyles.Accent
            };

            box.style.backgroundColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.1f);
            box.style.borderLeftColor = accentColor;

            var typeName = type switch
            {
                CalloutType.Info => HonamiDocLocalization.Get("INFO", "ІНФО"),
                CalloutType.Warning => HonamiDocLocalization.Get("WARNING", "УВАГА"),
                CalloutType.Tip => HonamiDocLocalization.Get("TIP", "ПОРАДА"),
                CalloutType.Danger => HonamiDocLocalization.Get("DANGER", "НЕБЕЗПЕКА"),
                _ => type.ToString().ToUpperInvariant()
            };
            var title = new Label(typeName);
            title.style.fontSize = 10;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = accentColor;
            title.style.marginBottom = 4;
            box.Add(title);

            var p = new Label(text);
            p.style.fontSize = 13;
            p.style.color = Color.white;
            p.style.whiteSpace = WhiteSpace.Normal;
            box.Add(p);

            root.Add(box);
        }

        public static void AddTip(VisualElement root, string text) => AddCallout(root, text, CalloutType.Tip);
        public static void AddWarning(VisualElement root, string text) => AddCallout(root, text, CalloutType.Warning);
        public static void AddDanger(VisualElement root, string text) => AddCallout(root, text, CalloutType.Danger);

        public static void AddInfoBox(VisualElement root, string text, HelpBoxMessageType type = HelpBoxMessageType.Info)
        {
            var calloutType = type switch
            {
                HelpBoxMessageType.Warning => CalloutType.Warning,
                HelpBoxMessageType.Error => CalloutType.Danger,
                _ => CalloutType.Info
            };
            AddCallout(root, text, calloutType);
        }

        public static void AddPageIntro(VisualElement root, string[] youWillLearn, string[] prerequisites = null)
        {
            var box = new VisualElement();
            box.style.paddingTop = box.style.paddingBottom = 14;
            box.style.paddingLeft = box.style.paddingRight = 16;
            box.style.marginBottom = 24;
            box.style.backgroundColor = HonamiGraphStyles.BoxBg;
            box.style.borderTopColor = box.style.borderBottomColor =
            box.style.borderRightColor = HonamiGraphStyles.BoxBorder;
            box.style.borderTopWidth = box.style.borderBottomWidth =
            box.style.borderRightWidth = 1;
            box.style.borderLeftWidth = 4;
            box.style.borderLeftColor = HonamiGraphStyles.Accent;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius =
            box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 6;

            AddIntroSection(box, HonamiDocLocalization.Get("YOU WILL LEARN", "ЩО ВИ ДІЗНАЄТЕСЬ"), HonamiGraphStyles.Accent, youWillLearn, 0);

            if (prerequisites != null && prerequisites.Length > 0)
            {
                AddIntroSection(box, HonamiDocLocalization.Get("BEFORE YOU START", "ПЕРЕДУМОВИ"), new Color(0.55f, 0.58f, 0.62f), prerequisites, 12);
            }

            root.Add(box);
        }

        private static void AddIntroSection(VisualElement box, string title, Color titleColor, string[] lines, float topMargin)
        {
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 10;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = titleColor;
            titleLabel.style.letterSpacing = 1.2f;
            titleLabel.style.marginTop = topMargin;
            titleLabel.style.marginBottom = 6;
            box.Add(titleLabel);

            foreach (var line in lines)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 4;

                var bullet = new VisualElement();
                bullet.style.width = 5;
                bullet.style.height = 5;
                bullet.style.backgroundColor = titleColor;
                bullet.style.marginRight = 10;
                bullet.style.marginTop = 7;
                bullet.style.borderTopLeftRadius = bullet.style.borderTopRightRadius =
                bullet.style.borderBottomLeftRadius = bullet.style.borderBottomRightRadius = 2.5f;

                var p = new Label(line);
                p.style.fontSize = 13;
                p.style.color = new Color(0.88f, 0.88f, 0.88f);
                p.style.whiteSpace = WhiteSpace.Normal;
                p.style.flexShrink = 1;

                row.Add(bullet);
                row.Add(p);
                box.Add(row);
            }
        }

        public static void AddCodeBlock(VisualElement root, string code)
        {
            var container = new VisualElement();
            container.style.marginTop = 12;
            container.style.marginBottom = 12;
            container.style.backgroundColor = new Color(0.08f, 0.08f, 0.09f);
            container.style.overflow = Overflow.Hidden;
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius =
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderTopColor = container.style.borderBottomColor =
            container.style.borderLeftColor = container.style.borderRightColor = new Color(1, 1, 1, 0.05f);
            container.style.borderTopWidth = container.style.borderBottomWidth =
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;

            var header = new VisualElement();
            header.style.height = 28;
            header.style.backgroundColor = new Color(0.12f, 0.13f, 0.14f);
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 6;

            var langLabel = new Label("C#");
            langLabel.style.fontSize = 10;
            langLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            langLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(langLabel);

            var copyBtn = new Button { text = HonamiDocLocalization.Get("COPY", "КОПІЮВАТИ") };
            copyBtn.style.fontSize = 9;
            copyBtn.style.height = 18;
            copyBtn.style.backgroundColor = new Color(1, 1, 1, 0.05f);
            copyBtn.style.borderTopWidth = copyBtn.style.borderBottomWidth = copyBtn.style.borderLeftWidth = copyBtn.style.borderRightWidth = 1;
            copyBtn.style.borderTopColor = copyBtn.style.borderBottomColor = copyBtn.style.borderLeftColor = copyBtn.style.borderRightColor = Color.clear;
            copyBtn.style.borderTopLeftRadius = copyBtn.style.borderTopRightRadius = copyBtn.style.borderBottomLeftRadius = copyBtn.style.borderBottomRightRadius = 3;
            copyBtn.style.color = new Color(0.8f, 0.8f, 0.8f);
            copyBtn.style.paddingLeft = copyBtn.style.paddingRight = 8;
            copyBtn.style.transitionProperty = new List<StylePropertyName> { "background-color", "border-color", "color" };
            copyBtn.style.transitionDuration = new List<TimeValue> { new TimeValue(100, TimeUnit.Millisecond) };

            copyBtn.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = code;
                var originalText = copyBtn.text;
                copyBtn.text = HonamiDocLocalization.Get("COPIED!", "СКОПІЙОВАНО!");
                copyBtn.style.color = HonamiGraphStyles.Green;
                copyBtn.schedule.Execute(() =>
                {
                    copyBtn.text = originalText;
                    copyBtn.style.color = new Color(0.8f, 0.8f, 0.8f);
                }).StartingIn(1500);
            };

            copyBtn.RegisterCallback<MouseOverEvent>(evt =>
            {
                copyBtn.style.backgroundColor = new Color(1, 1, 1, 0.1f);
                copyBtn.style.borderTopColor = copyBtn.style.borderBottomColor = copyBtn.style.borderLeftColor = copyBtn.style.borderRightColor = new Color(1, 1, 1, 0.15f);
            });
            copyBtn.RegisterCallback<MouseOutEvent>(evt =>
            {
                copyBtn.style.backgroundColor = new Color(1, 1, 1, 0.05f);
                copyBtn.style.borderTopColor = copyBtn.style.borderBottomColor = copyBtn.style.borderLeftColor = copyBtn.style.borderRightColor = Color.clear;
            });

            header.Add(copyBtn);

            container.Add(header);

            var codeContent = new VisualElement();
            codeContent.style.paddingTop = codeContent.style.paddingBottom = 12;
            codeContent.style.paddingLeft = codeContent.style.paddingRight = 12;

            var highlighted = HighlightCSharp(code);
            var style = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                fontSize = 12,
                wordWrap = false
            };

            var imgui = new IMGUIContainer(() =>
            {
                var rect = EditorGUILayout.GetControlRect(false, style.CalcHeight(new GUIContent(highlighted), 1000));
                EditorGUI.SelectableLabel(rect, highlighted, style);
            });

            codeContent.Add(imgui);

            container.Add(codeContent);
            root.Add(container);
        }

        private static string HighlightCSharp(string code)
        {
            code = System.Text.RegularExpressions.Regex.Replace(code, @"\b(public|private|protected|internal|static|class|struct|interface|enum|void|override|virtual|new|using|namespace|if|else|while|for|foreach|return|true|false|null|in|out|ref|params|var)\b", "<color=#569cd6>$1</color>");
            code = System.Text.RegularExpressions.Regex.Replace(code, @"\b(string|int|float|bool|VisualElement|HonamiAnimator|HonamiController|HonamiExecutionContext|honami|ctx)\b", "<color=#4ec9b0>$1</color>");
            code = System.Text.RegularExpressions.Regex.Replace(code, @"""(.*?)""", "<color=#d69d85>\"$1\"</color>");
            code = System.Text.RegularExpressions.Regex.Replace(code, @"//(.*)", "<color=#6a9955>//$1</color>");
            return code;
        }

        public static void AddParameterTable(VisualElement root, params (string name, string type, string desc)[] parameters)
        {
            AddTable(root,
                (HonamiDocLocalization.Get("PARAMETER", "ПАРАМЕТР"), 150f),
                (HonamiDocLocalization.Get("TYPE", "ТИП"), 100f),
                (HonamiDocLocalization.Get("DESCRIPTION", "ОПИС"), 0f),
                parameters);
        }

        public static void AddTable(VisualElement root, (string title, float width) h1, (string title, float width) h2, (string title, float width) h3, params (string c1, string c2, string c3)[] rows)
        {
            var container = new VisualElement();
            container.style.marginTop = 12;
            container.style.marginBottom = 12;
            container.style.backgroundColor = new Color(0.12f, 0.13f, 0.14f, 0.3f);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius =
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 6;
            container.style.overflow = Overflow.Hidden;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingTop = header.style.paddingBottom = 8;
            header.style.backgroundColor = new Color(0.15f, 0.16f, 0.17f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);

            header.Add(CreateHeaderLabel(h1.title, h1.width, h1.width <= 0 ? 1 : 0));
            header.Add(CreateHeaderLabel(h2.title, h2.width, h2.width <= 0 ? 1 : 0));
            header.Add(CreateHeaderLabel(h3.title, h3.width, h3.width <= 0 ? 1 : 0));
            container.Add(header);

            for (int i = 0; i < rows.Length; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.paddingTop = row.style.paddingBottom = 10;
                row.style.paddingLeft = 12;
                if (i < rows.Length - 1)
                {
                    row.style.borderBottomWidth = 1;
                    row.style.borderBottomColor = new Color(1, 1, 1, 0.05f);
                }

                row.Add(CreateCellLabel(rows[i].c1, h1.width, true));
                row.Add(CreateCellLabel(rows[i].c2, h2.width, false));
                row.Add(CreateCellLabel(rows[i].c3, h3.width, false));

                container.Add(row);
            }

            root.Add(container);
        }

        private static Label CreateCellLabel(string text, float width, bool bold)
        {
            var l = new Label(text);
            if (width > 0)
            {
                l.style.width = width;
                l.style.minWidth = width;
            }
            else
            {
                l.style.flexGrow = 1;
                l.style.flexShrink = 1;
            }

            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.fontSize = 12;
            l.style.color = new Color(0.85f, 0.85f, 0.85f);
            if (bold)
            {
                l.style.unityFontStyleAndWeight = FontStyle.Bold;
                l.style.color = HonamiGraphStyles.Accent;
            }
            return l;
        }

        public static void AddPropertyBox(VisualElement root, params (string name, string desc)[] properties)
        {
            var container = new VisualElement();
            container.style.marginTop = 12;
            container.style.marginBottom = 12;
            container.style.backgroundColor = HonamiGraphStyles.BoxBg;
            container.style.borderTopColor = container.style.borderBottomColor =
            container.style.borderLeftColor = container.style.borderRightColor = HonamiGraphStyles.BoxBorder;
            container.style.borderTopWidth = container.style.borderBottomWidth =
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius =
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 6;
            container.style.paddingTop = container.style.paddingBottom = 12;
            container.style.paddingLeft = container.style.paddingRight = 16;

            ReadOnlySpan<(string name, string desc)> span = properties;
            for (int i = 0; i < span.Length; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = i < span.Length - 1 ? 8 : 0;

                var label = new Label(span[i].name);
                label.style.width = 160;
                label.style.minWidth = 160;
                label.style.marginRight = 12;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = HonamiGraphStyles.Accent;
                label.style.whiteSpace = WhiteSpace.Normal;

                var val = new Label(span[i].desc);
                val.style.flexShrink = 1;
                val.style.whiteSpace = WhiteSpace.Normal;
                val.style.color = new Color(0.85f, 0.85f, 0.85f);

                row.Add(label);
                row.Add(val);
                container.Add(row);
            }
            root.Add(container);
        }

        public static void AddNodeVisual(VisualElement root, string title, string subtitle, Color accent, Texture2D icon = null)
        {
            var node = new VisualElement();
            node.style.width = 220;
            node.style.backgroundColor = new Color(0.12f, 0.13f, 0.14f);
            node.style.borderTopColor = node.style.borderBottomColor = node.style.borderLeftColor = node.style.borderRightColor = new Color(0.06f, 0.07f, 0.08f);
            node.style.borderTopWidth = node.style.borderBottomWidth = node.style.borderLeftWidth = node.style.borderRightWidth = 1;
            node.style.borderTopLeftRadius = node.style.borderTopRightRadius = node.style.borderBottomLeftRadius = node.style.borderBottomRightRadius = 10;
            node.style.marginTop = 16;
            node.style.marginBottom = 16;
            node.style.alignSelf = Align.Center;
            node.style.overflow = Overflow.Hidden;

            var accentBar = new VisualElement();
            accentBar.style.height = 6;
            accentBar.style.backgroundColor = accent;
            node.Add(accentBar);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = header.style.paddingBottom = 12;
            header.style.paddingLeft = 14;

            if (icon != null)
            {
                var img = new Image { image = icon };
                img.style.width = 24;
                img.style.height = 24;
                img.style.marginRight = 12;
                img.tintColor = accent;
                header.Add(img);
            }

            var textContainer = new VisualElement();
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            titleLabel.style.fontSize = 14;
            textContainer.Add(titleLabel);

            var subtitleLabel = new Label(subtitle.ToUpperInvariant());
            subtitleLabel.style.fontSize = 9;
            subtitleLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            subtitleLabel.style.marginTop = -2;
            textContainer.Add(subtitleLabel);

            header.Add(textContainer);
            node.Add(header);
            root.Add(node);
        }

        public static void AddActionButton(VisualElement root, string text, Texture2D icon, Action onClick)
        {
            var btn = new Button(onClick);
            btn.style.height = 42;
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.paddingLeft = 16;
            btn.style.paddingRight = 16;
            btn.style.marginTop = 8;
            btn.style.marginBottom = 8;
            btn.style.backgroundColor = new Color(0.15f, 0.16f, 0.17f);
            btn.style.borderTopColor = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor = new Color(0.25f, 0.26f, 0.27f, 0.5f);
            btn.style.borderTopWidth = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth = 1;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 6;
            btn.style.transitionProperty = new List<StylePropertyName> { "background-color", "border-color" };
            btn.style.transitionDuration = new List<TimeValue> { new TimeValue(100, TimeUnit.Millisecond) };

            btn.RegisterCallback<MouseOverEvent>(evt =>
            {
                btn.style.backgroundColor = new Color(0.20f, 0.21f, 0.22f);
                btn.style.borderTopColor = btn.style.borderBottomColor =
                btn.style.borderLeftColor = btn.style.borderRightColor = HonamiGraphStyles.Accent;
            });
            btn.RegisterCallback<MouseOutEvent>(evt =>
            {
                btn.style.backgroundColor = new Color(0.15f, 0.16f, 0.17f);
                btn.style.borderTopColor = btn.style.borderBottomColor =
                btn.style.borderLeftColor = btn.style.borderRightColor = new Color(0.25f, 0.26f, 0.27f, 0.5f);
            });

            if (icon != null)
            {
                var img = new Image { image = icon };
                img.style.width = 18;
                img.style.height = 18;
                img.style.marginRight = 10;
                img.tintColor = HonamiGraphStyles.Accent;
                btn.Add(img);
            }

            var label = new Label(text);
            label.style.fontSize = 13;
            label.style.color = Color.white;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.Add(label);

            root.Add(btn);
        }

        public static void AddActionGroup(VisualElement root, params (string text, Texture2D icon, Action onClick)[] actions)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;
            container.style.marginTop = 12;
            container.style.marginBottom = 12;

            ReadOnlySpan<(string text, Texture2D icon, Action onClick)> span = actions;
            for (int i = 0; i < span.Length; i++)
            {
                var btn = new Button(span[i].onClick);
                btn.style.height = 36;
                btn.style.flexDirection = FlexDirection.Row;
                btn.style.alignItems = Align.Center;
                btn.style.paddingLeft = 14;
                btn.style.paddingRight = 14;
                btn.style.marginRight = 10;
                btn.style.marginBottom = 10;
                btn.style.backgroundColor = new Color(0.18f, 0.19f, 0.20f);
                btn.style.borderTopColor = btn.style.borderBottomColor =
                btn.style.borderLeftColor = btn.style.borderRightColor = new Color(1, 1, 1, 0.05f);
                btn.style.borderTopWidth = btn.style.borderBottomWidth =
                btn.style.borderLeftWidth = btn.style.borderRightWidth = 1;
                btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
                btn.style.transitionProperty = new List<StylePropertyName> { "background-color" };
                btn.style.transitionDuration = new List<TimeValue> { new TimeValue(100, TimeUnit.Millisecond) };

                btn.RegisterCallback<MouseOverEvent>(evt => btn.style.backgroundColor = new Color(0.25f, 0.26f, 0.27f));
                btn.RegisterCallback<MouseOutEvent>(evt => btn.style.backgroundColor = new Color(0.18f, 0.19f, 0.20f));

                if (span[i].icon != null)
                {
                    var img = new Image { image = span[i].icon };
                    img.style.width = 16;
                    img.style.height = 16;
                    img.style.marginRight = 8;
                    img.tintColor = HonamiGraphStyles.Accent;
                    btn.Add(img);
                }

                var label = new Label(span[i].text);
                label.style.fontSize = 12;
                label.style.color = Color.white;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.Add(label);

                container.Add(btn);
            }

            root.Add(container);
        }

        public static void AddFeatureGrid(VisualElement root, params (string title, string desc, Texture2D icon)[] features)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;
            container.style.marginTop = 12;
            container.style.justifyContent = Justify.SpaceBetween;

            ReadOnlySpan<(string title, string desc, Texture2D icon)> span = features;
            for (int i = 0; i < span.Length; i++)
            {
                var card = new VisualElement();
                card.style.width = Length.Percent(48.5f);
                card.style.marginBottom = 12;
                card.style.paddingTop = card.style.paddingBottom = 16;
                card.style.paddingLeft = card.style.paddingRight = 16;
                card.style.backgroundColor = HonamiGraphStyles.BoxBg;
                card.style.borderTopColor = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor = HonamiGraphStyles.BoxBorder;
                card.style.borderTopWidth = card.style.borderBottomWidth =
                card.style.borderLeftWidth = card.style.borderRightWidth = 1;
                card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;

                if (span[i].icon != null)
                {
                    var icon = new Image { image = span[i].icon };
                    icon.style.width = 24;
                    icon.style.height = 24;
                    icon.style.marginBottom = 8;
                    icon.tintColor = HonamiGraphStyles.Accent;
                    card.Add(icon);
                }

                var title = new Label(span[i].title);
                title.style.fontSize = 14;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = Color.white;
                title.style.marginBottom = 6;
                card.Add(title);

                var desc = new Label(span[i].desc);
                desc.style.fontSize = 12;
                desc.style.color = new Color(0.7f, 0.7f, 0.7f);
                desc.style.whiteSpace = WhiteSpace.Normal;
                card.Add(desc);

                container.Add(card);
            }

            root.Add(container);
        }

        public static void CreateController()
        {
            var path = "Assets";
            if (Selection.activeObject != null)
            {
                path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!System.IO.Directory.Exists(path)) path = System.IO.Path.GetDirectoryName(path);
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "/New Honami Controller.asset");
            var asset = ScriptableObject.CreateInstance<Runtime.Core.HonamiController>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        private static Label CreateHeaderLabel(string text, float width, float flexGrow = 0)
        {
            var l = new Label(text);
            l.style.fontSize = 10;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = new Color(0.5f, 0.5f, 0.5f);
            l.style.paddingLeft = 12;
            if (width > 0) l.style.width = width;
            l.style.flexGrow = flexGrow;
            return l;
        }

        public enum CalloutType
        {
            Info,
            Warning,
            Tip,
            Danger
        }
    }
}
