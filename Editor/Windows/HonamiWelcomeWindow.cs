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
    public sealed class HonamiWelcomeWindow : EditorWindow
    {
        private const string SessionShownKey = "Honami.Welcome.ShownThisSession";
        private const string IntroPlayedKey = "Honami.Welcome.IntroPlayed";
        private const int Steps = 7;
        private const double FirstRunSettleSeconds = 0.75;

        private static double _idleSince = -1;

        private static readonly Vector2 WindowSize = new(880, 720);
        private static readonly Color CardBg = HonamiGraphStyles.BoxBg;
        private static readonly Color CardBorder = HonamiGraphStyles.BoxBorder;
        private static readonly Color Hairline = new(1f, 1f, 1f, 0.06f);

        private int _step;
        private VisualElement _stage;
        private VisualElement _dotsRow;
        private VisualElement _backHolder;
        private Button _backButton;
        private Button _nextButton;

        [MenuItem("Window/Honami/Welcome")]
        public static void ShowWindow()
        {
            SessionState.SetBool(IntroPlayedKey, false);
            var w = GetWindow<HonamiWelcomeWindow>(true, "Welcome to Honami", true);
            w.titleContent = HonamiEditorIcons.IconContent("HonamiGraphWhite", "Welcome to Honami");
            w.minSize = w.maxSize = WindowSize;
            CenterOnEditor(w);
        }

        [InitializeOnLoadMethod]
        private static void BootstrapFirstRun()
        {
            if (Application.isBatchMode) return;
            if (SessionState.GetBool(SessionShownKey, false)) return;
            if (HasSeenWelcome()) return;

            _idleSince = -1;
            EditorApplication.update -= ShowWhenEditorReady;
            EditorApplication.update += ShowWhenEditorReady;
        }

        private static void ShowWhenEditorReady()
        {
            if (SessionState.GetBool(SessionShownKey, false) || HasSeenWelcome())
            {
                EditorApplication.update -= ShowWhenEditorReady;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _idleSince = -1;
                return;
            }

            if (_idleSince < 0) _idleSince = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - _idleSince < FirstRunSettleSeconds) return;

            EditorApplication.update -= ShowWhenEditorReady;
            SessionState.SetBool(SessionShownKey, true);
            ShowWindow();
        }

        private static string SeenKey => $"Honami.Welcome.Seen.{HonamiPackageInfo.Version}";

        private static bool HasSeenWelcome() => !string.IsNullOrEmpty(EditorUserSettings.GetConfigValue(SeenKey));

        private static void MarkWelcomeSeen() => EditorUserSettings.SetConfigValue(SeenKey, "1");

        private static void CenterOnEditor(EditorWindow window)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var pos = window.position;
            pos.x = main.x + (main.width - WindowSize.x) * 0.5f;
            pos.y = main.y + (main.height - WindowSize.y) * 0.5f;
            window.position = pos;
        }

        private void OnEnable()
        {
            BuildShell();
            GoTo(0);
            MarkSeenWhenPresented();

            if (SessionState.GetBool(IntroPlayedKey, false)) return;

            EditorApplication.update -= TryPlayIntro;
            EditorApplication.update += TryPlayIntro;
        }

        private void MarkSeenWhenPresented()
        {
            rootVisualElement.schedule.Execute(() =>
            {
                if (this == null) return;
                MarkWelcomeSeen();
            }).StartingIn(400);
        }

        private void OnDisable()
        {
            EditorApplication.update -= TryPlayIntro;
        }

        private void TryPlayIntro()
        {
            if (this == null)
            {
                EditorApplication.update -= TryPlayIntro;
                return;
            }

            if (EditorApplication.isCompiling) return;
            if (EditorApplication.isUpdating) return;

            EditorApplication.update -= TryPlayIntro;
            PlayIntroClip();
        }

        private void BuildShell()
        {
            var root = rootVisualElement;
            HonamiEditorLayout.PrepareRoot(root);

            var accentStrip = new VisualElement();
            accentStrip.style.height = 3;
            accentStrip.style.backgroundColor = HonamiEditorTheme.Accent;
            accentStrip.style.flexShrink = 0;
            root.Add(accentStrip);

            var topBar = BuildTopBar();
            root.Add(topBar);

            _stage = new VisualElement();
            _stage.style.flexGrow = 1;
            _stage.style.overflow = Overflow.Hidden;
            root.Add(_stage);

            var transport = BuildTransport();
            root.Add(transport);
        }

        private void PlayIntroClip()
        {
            var root = rootVisualElement;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = 0;
            overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0.115f, 0.12f, 0.13f, 1f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            root.Add(overlay);

            var logoWrap = new VisualElement();
            logoWrap.style.alignItems = Align.Center;
            logoWrap.style.justifyContent = Justify.Center;
            logoWrap.style.width = logoWrap.style.height = 200;

            var ring1 = MakeRippleRing(200);
            var ring2 = MakeRippleRing(140);
            logoWrap.Add(ring1);
            logoWrap.Add(ring2);

            var logo = new Image { image = HonamiEditorIcons.GraphWhite };
            logo.style.width = logo.style.height = 76;
            logo.style.position = Position.Absolute;
            logo.style.left = Length.Percent(50);
            logo.style.top = Length.Percent(50);
            logo.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50), 0);
            logo.tintColor = HonamiEditorTheme.Accent;
            logoWrap.Add(logo);
            overlay.Add(logoWrap);

            var title = new Label("HONAMI");
            title.style.fontSize = 36;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.letterSpacing = 8f;
            title.style.marginTop = 22;
            overlay.Add(title);

            var subtitle = new Label("ANIMATION SYSTEM");
            subtitle.style.fontSize = 11;
            subtitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            subtitle.style.letterSpacing = 3.5f;
            subtitle.style.color = HonamiEditorTheme.Accent;
            subtitle.style.marginTop = 8;
            overlay.Add(subtitle);

            var line = new VisualElement();
            line.style.height = 2;
            line.style.width = 0;
            line.style.backgroundColor = HonamiEditorTheme.Accent;
            line.style.marginTop = 22;
            overlay.Add(line);

            PulseRing(ring1, 80);
            PulseRing(ring2, 260);
            SpringPopIn(logo, 120);
            FadeSlideIn(title, 380);
            FadeSlideIn(subtitle, 520);

            line.schedule.Execute(() =>
            {
                line.style.transitionProperty = new List<StylePropertyName> { "width" };
                line.style.transitionDuration = new List<TimeValue> { new(500, TimeUnit.Millisecond) };
                line.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };
                line.style.width = 64;
            }).StartingIn(580);

            overlay.schedule.Execute(() => DismissIntroOverlay(overlay)).StartingIn(2200);
        }

        private static VisualElement MakeRippleRing(float size)
        {
            var ring = new VisualElement();
            ring.style.position = Position.Absolute;
            ring.style.width = ring.style.height = size;
            ring.style.left = Length.Percent(50);
            ring.style.top = Length.Percent(50);
            ring.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50), 0);
            ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius =
            ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = size * 0.5f;
            ring.style.borderTopWidth = ring.style.borderBottomWidth =
            ring.style.borderLeftWidth = ring.style.borderRightWidth = 1.5f;
            ring.style.borderTopColor = ring.style.borderBottomColor =
            ring.style.borderLeftColor = ring.style.borderRightColor = HonamiEditorTheme.Accent;
            ring.style.backgroundColor = Color.clear;
            return ring;
        }

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

        private VisualElement BuildTopBar()
        {
            var bar = new VisualElement();
            bar.style.height = 44;
            bar.style.flexShrink = 0;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 18;
            bar.style.paddingRight = 12;
            bar.style.backgroundColor = HonamiEditorLayout.HeaderBg;
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = HonamiEditorLayout.HeaderBorder;

            var mark = new Image { image = HonamiEditorIcons.GraphWhite };
            mark.style.width = mark.style.height = 18;
            mark.style.marginRight = 8;
            mark.tintColor = HonamiEditorTheme.Accent;
            bar.Add(mark);

            var wordmark = new Label("HONAMI");
            wordmark.style.fontSize = 12;
            wordmark.style.unityFontStyleAndWeight = FontStyle.Bold;
            wordmark.style.letterSpacing = 3f;
            wordmark.style.color = HonamiEditorTheme.Accent;
            bar.Add(wordmark);

            var version = new Label(HonamiPackageInfo.ShortVersion);
            version.style.fontSize = 10;
            version.style.marginLeft = 8;
            version.style.color = HonamiEditorTheme.MutedText;
            bar.Add(version);

            bar.Add(HonamiGraphStyles.Spacer());

            var skip = TextLink("Skip intro", Close);
            bar.Add(skip);

            return bar;
        }

        private VisualElement BuildTransport()
        {
            var bar = new VisualElement();
            bar.style.height = 68;
            bar.style.flexShrink = 0;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 24;
            bar.style.paddingRight = 24;
            bar.style.backgroundColor = HonamiEditorLayout.HeaderBg;
            bar.style.borderTopWidth = 1;
            bar.style.borderTopColor = HonamiEditorLayout.HeaderBorder;

            _backHolder = new VisualElement();
            _backHolder.style.width = 150;
            _backHolder.style.flexDirection = FlexDirection.Row;
            _backButton = GhostButton(Localized("Back", "Назад"), () => GoTo(_step - 1));
            _backHolder.Add(_backButton);
            bar.Add(_backHolder);

            _dotsRow = new VisualElement();
            _dotsRow.style.flexGrow = 1;
            _dotsRow.style.flexDirection = FlexDirection.Row;
            _dotsRow.style.alignItems = Align.Center;
            _dotsRow.style.justifyContent = Justify.Center;
            bar.Add(_dotsRow);

            var nextHolder = new VisualElement();
            nextHolder.style.width = 150;
            nextHolder.style.flexDirection = FlexDirection.Row;
            nextHolder.style.justifyContent = Justify.FlexEnd;
            _nextButton = PrimaryButton(Localized("Next", "Далі"), OnNext);
            nextHolder.Add(_nextButton);
            bar.Add(nextHolder);

            return bar;
        }

        private void OnNext()
        {
            if (_step >= Steps - 1) PlayOutroClip();
            else GoTo(_step + 1);
        }

        private void PlayOutroClip()
        {
            var root = rootVisualElement;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = 0;
            overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0.115f, 0.12f, 0.13f, 0f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            overlay.style.transitionDuration = new List<TimeValue> { new(500, TimeUnit.Millisecond) };
            overlay.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseInCubic) };
            root.Add(overlay);

            overlay.schedule.Execute(() =>
            {
                overlay.style.backgroundColor = new Color(0.115f, 0.12f, 0.13f, 1f);
            }).StartingIn(16);

            var logoWrap = new VisualElement();
            logoWrap.style.alignItems = Align.Center;
            logoWrap.style.justifyContent = Justify.Center;
            logoWrap.style.width = logoWrap.style.height = 180;
            logoWrap.style.opacity = 0f;

            var ring1 = MakeRippleRing(180);
            var ring2 = MakeRippleRing(120);
            logoWrap.Add(ring1);
            logoWrap.Add(ring2);

            var logo = new Image { image = HonamiEditorIcons.GraphWhite };
            logo.style.width = logo.style.height = 64;
            logo.style.position = Position.Absolute;
            logo.style.left = Length.Percent(50);
            logo.style.top = Length.Percent(50);
            logo.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50), 0);
            logo.tintColor = HonamiEditorTheme.Accent;
            logoWrap.Add(logo);
            overlay.Add(logoWrap);

            var heading = new Label(Localized("Happy Animating!", "Щасливого анімування!"));
            heading.style.fontSize = 32;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.color = Color.white;
            heading.style.letterSpacing = -0.5f;
            heading.style.marginTop = 18;
            heading.style.opacity = 0f;
            overlay.Add(heading);

            var wish = new Label(Localized(
                "Good luck with your project — may every frame be smooth!",
                "Удачі з вашим проєктом — нехай кожен кадр буде плавним!"));
            wish.style.fontSize = 14;
            wish.style.color = new Color(0.72f, 0.74f, 0.77f);
            wish.style.whiteSpace = WhiteSpace.Normal;
            wish.style.unityTextAlign = TextAnchor.MiddleCenter;
            wish.style.maxWidth = 420;
            wish.style.marginTop = 10;
            wish.style.opacity = 0f;
            overlay.Add(wish);

            var line = new VisualElement();
            line.style.height = 2;
            line.style.width = 0;
            line.style.backgroundColor = HonamiEditorTheme.Accent;
            line.style.marginTop = 22;
            overlay.Add(line);

            var footnote = new Label(Localized(
                "Window ▸ Honami ▸ Welcome to revisit anytime.",
                "Window ▸ Honami ▸ Welcome — відкрити знову будь-коли."));
            footnote.style.fontSize = 11;
            footnote.style.color = new Color(0.5f, 0.52f, 0.55f);
            footnote.style.marginTop = 16;
            footnote.style.opacity = 0f;
            overlay.Add(footnote);

            long baseDelay = 550;

            overlay.schedule.Execute(() =>
            {
                logoWrap.style.opacity = 1f;
            }).StartingIn(baseDelay);

            SpringPopIn(logo, baseDelay + 50);
            PulseRing(ring1, baseDelay + 80);
            PulseRing(ring2, baseDelay + 260);

            FadeSlideIn(heading, baseDelay + 300);
            FadeSlideIn(wish, baseDelay + 450);
            FadeSlideIn(footnote, baseDelay + 700);

            overlay.schedule.Execute(() =>
            {
                line.style.transitionProperty = new List<StylePropertyName> { "width" };
                line.style.transitionDuration = new List<TimeValue> { new(600, TimeUnit.Millisecond) };
                line.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };
                line.style.width = 80;
            }).StartingIn(baseDelay + 550);

            overlay.schedule.Execute(() =>
            {
                if (this != null) Close();
            }).StartingIn(3500);
        }

        private void GoTo(int step)
        {
            _step = Mathf.Clamp(step, 0, Steps - 1);
            _stage.Clear();

            var page = new VisualElement();
            page.style.flexGrow = 1;

            switch (_step)
            {
                case 0: BuildWelcomeStep(page); break;
                case 1: BuildFeaturesStep(page); break;
                case 2: BuildEaseOfUseStep(page); break;
                case 3: BuildMigrationStep(page); break;
                case 4: BuildToolsStep(page); break;
                case 5: BuildQuickSetupStep(page); break;
                default: BuildGetStartedStep(page); break;
            }

            _stage.Add(page);
            AnimateIn(page);
            RefreshTransport();
        }

        private void RefreshTransport()
        {
            _backHolder.style.visibility = _step == 0 ? Visibility.Hidden : Visibility.Visible;

            var nextText = _step switch
            {
                0 => Localized("Get Started", "Почнімо"),
                Steps - 1 => Localized("Start Animating", "Почати анімувати"),
                _ => Localized("Next", "Далі")
            };
            SetButtonLabel(_nextButton, nextText);
            SetButtonLabel(_backButton, Localized("Back", "Назад"));

            _dotsRow.Clear();
            for (int i = 0; i < Steps; i++)
            {
                bool active = i == _step;
                var dot = new VisualElement();
                dot.style.height = 7;
                dot.style.width = active ? 22 : 7;
                dot.style.marginLeft = dot.style.marginRight = 4;
                dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius =
                dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 3.5f;
                dot.style.backgroundColor = active ? HonamiEditorTheme.Accent : new Color(1f, 1f, 1f, 0.16f);
                dot.style.transitionProperty = new List<StylePropertyName> { "width", "background-color" };
                dot.style.transitionDuration = new List<TimeValue> { new(220, TimeUnit.Millisecond) };
                dot.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };
                _dotsRow.Add(dot);
            }
        }

        private void BuildWelcomeStep(VisualElement page)
        {
            page.style.alignItems = Align.Center;
            page.style.justifyContent = Justify.Center;
            page.style.paddingLeft = page.style.paddingRight = 40;

            var logoWrap = new VisualElement();
            logoWrap.style.width = logoWrap.style.height = 168;
            logoWrap.style.alignItems = Align.Center;
            logoWrap.style.justifyContent = Justify.Center;
            logoWrap.style.marginBottom = 6;
            logoWrap.Add(Halo(168, 0.10f));
            logoWrap.Add(Halo(118, 0.16f));

            var logo = new Image { image = HonamiEditorIcons.GraphWhite };
            logo.style.width = logo.style.height = 66;
            logo.tintColor = HonamiEditorTheme.Accent;
            logoWrap.Add(logo);
            page.Add(logoWrap);

            var title = new Label("Honami");
            title.style.fontSize = 48;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.letterSpacing = -1f;
            page.Add(title);

            var subtitle = new Label(Localized("ANIMATION SYSTEM", "СИСТЕМА АНІМАЦІЇ"));
            subtitle.style.fontSize = 12;
            subtitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            subtitle.style.letterSpacing = 4f;
            subtitle.style.color = HonamiEditorTheme.Accent;
            subtitle.style.marginTop = 2;
            page.Add(subtitle);

            var badges = new VisualElement();
            badges.style.flexDirection = FlexDirection.Row;
            badges.style.alignItems = Align.Center;
            badges.style.marginTop = 14;
            badges.Add(Pill("BETA", HonamiEditorTheme.Accent, Color.white));
            var ver = new Label(HonamiPackageInfo.ShortVersion);
            ver.style.fontSize = 11;
            ver.style.marginLeft = 8;
            ver.style.color = HonamiEditorTheme.MutedText;
            badges.Add(ver);
            page.Add(badges);

            var tagline = new Label(Localized(
                "A complete, high-performance replacement for Unity's Animator — with a live graph, procedural rigging and a zero-allocation runtime.",
                "Повноцінна, високопродуктивна заміна Unity Animator — з живим графом, процедурним рігінгом та рантаймом без алокацій."));
            tagline.style.fontSize = 14;
            tagline.style.color = new Color(0.78f, 0.80f, 0.83f);
            tagline.style.whiteSpace = WhiteSpace.Normal;
            tagline.style.unityTextAlign = TextAnchor.MiddleCenter;
            tagline.style.maxWidth = 480;
            tagline.style.marginTop = 18;
            page.Add(tagline);

            var langHint = new Label(Localized("Choose your language", "Оберіть мову"));
            langHint.style.fontSize = 10;
            langHint.style.unityFontStyleAndWeight = FontStyle.Bold;
            langHint.style.letterSpacing = 1.5f;
            langHint.style.color = new Color(0.5f, 0.52f, 0.55f);
            langHint.style.marginTop = 30;
            langHint.style.marginBottom = 8;
            page.Add(langHint);

            page.Add(BuildLanguageToggle());
        }

        private VisualElement BuildLanguageToggle()
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.backgroundColor = new Color(0f, 0f, 0f, 0.28f);
            group.style.paddingTop = group.style.paddingBottom = 3;
            group.style.paddingLeft = group.style.paddingRight = 3;
            group.style.borderTopLeftRadius = group.style.borderTopRightRadius =
            group.style.borderBottomLeftRadius = group.style.borderBottomRightRadius = 9;
            group.style.borderTopWidth = group.style.borderBottomWidth =
            group.style.borderLeftWidth = group.style.borderRightWidth = 1;
            group.style.borderTopColor = group.style.borderBottomColor =
            group.style.borderLeftColor = group.style.borderRightColor = Hairline;

            group.Add(LanguageSegment("English", HonamiDocLanguage.English));
            group.Add(LanguageSegment("Українська", HonamiDocLanguage.Ukrainian));
            return group;
        }

        private Button LanguageSegment(string label, HonamiDocLanguage language)
        {
            bool active = HonamiDocLocalization.CurrentLanguage == language;

            var btn = new Button(() =>
            {
                if (HonamiDocLocalization.CurrentLanguage == language) return;
                HonamiDocLocalization.CurrentLanguage = language;
                GoTo(_step);
            })
            { text = label };

            btn.style.height = 30;
            btn.style.width = 128;
            btn.style.marginTop = btn.style.marginBottom = 0;
            btn.style.marginLeft = btn.style.marginRight = 0;
            btn.style.paddingLeft = btn.style.paddingRight = 0;
            btn.style.fontSize = 12;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = active ? HonamiEditorTheme.Accent : Color.clear;
            btn.style.color = active ? Color.white : HonamiEditorTheme.MutedText;
            btn.style.borderTopWidth = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth = 0;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 7;
            return btn;
        }

        private void BuildFeaturesStep(VisualElement page)
        {
            var scroll = BuildStepScroll(page,
                Localized("WHY HONAMI", "ЧОМУ HONAMI"),
                Localized("Built for animators, engineered for developers", "Зроблено для аніматорів, оптимізовано для розробників"),
                Localized(
                    "Honami replaces the built-in Animator with a reactive simulation you can actually see and debug.",
                    "Honami замінює вбудований Animator реактивною симуляцією, яку справді видно та можна дебажити."));

            var grid = NewGrid();
            grid.Add(FeatureCard(
                Localized("Custom Runtime", "Власний рантайм"),
                Localized("A from-scratch Animator replacement built on the Playables API.", "Заміна Animator, написана з нуля на Playables API."),
                HonamiEditorIcons.GraphWhite));
            grid.Add(FeatureCard(
                Localized("Zero Allocation", "Без алокацій"),
                Localized("Evaluation produces no garbage, so your framerate stays smooth.", "Обчислення не створює garbage — FPS лишається стабільним."),
                HonamiEditorIcons.TimelineWhite));
            grid.Add(FeatureCard(
                Localized("Live Debugging", "Живий дебаг"),
                Localized("Watch active nodes, weights and parameters update in real time.", "Дивіться активні ноди, ваги та параметри в реальному часі."),
                HonamiEditorIcons.Graph));
            grid.Add(FeatureCard(
                Localized("Rigging & IK", "Рігінг та IK"),
                Localized("Built-in Foot IK, LookAt, constraints and pseudo-physics.", "Вбудовані Foot IK, LookAt, констрейнти та псевдофізика."),
                HonamiEditorIcons.BlendTreeWhite));
            grid.Add(FeatureCard(
                Localized("Sub-Nodes", "Саб-ноди"),
                Localized("Encapsulate sounds, effects and IK inside states to keep graphs clean.", "Інкапсулюйте звуки, ефекти та IK у стейти — граф лишається чистим."),
                HonamiEditorIcons.Controller));
            grid.Add(FeatureCard(
                Localized("Linked Brain", "Linked Brain"),
                Localized("Orchestrate many characters with synced timing and logic.", "Керуйте кількома персонажами із синхронізованим таймінгом та логікою."),
                HonamiEditorIcons.GetIcon("HonamiAvatarWhite")));
            scroll.Add(grid);
        }

        private void BuildToolsStep(VisualElement page)
        {
            var scroll = BuildStepScroll(page,
                Localized("THE TOOLKIT", "ІНСТРУМЕНТАРІЙ"),
                Localized("Everything ships in one package", "Усе постачається в одному пакеті"),
                Localized(
                    "A full editor suite — click any card to open it now, or reach it later from Window ▸ Honami.",
                    "Повний набір редакторів — клікніть будь-яку картку, щоб відкрити зараз, або пізніше через Window ▸ Honami."));

            var grid = NewGrid();
            grid.Add(ToolCard(
                Localized("Animator Graph", "Граф аніматора"),
                Localized("Design states, transitions and layers visually.", "Візуальне проєктування стейтів, транзішнів та шарів."),
                HonamiEditorIcons.GraphWhite, HonamiGraphWindow.OpenWindow));
            grid.Add(ToolCard(
                Localized("Timeline", "Таймлайн"),
                Localized("Sequence clips, events and markers on a track editor.", "Секвенсинг кліпів, івентів та маркерів на треках."),
                HonamiEditorIcons.TimelineWhite, HonamiTimelineWindow.OpenWindow));
            grid.Add(ToolCard(
                Localized("Blend Tree", "Блендтрі"),
                Localized("Blend motion smoothly along a 1D parameter axis.", "Плавне змішування руху вздовж 1D-осі параметра."),
                HonamiEditorIcons.BlendTreeWhite, HonamiBlendTreeWindow.OpenWindow));
            grid.Add(ToolCard(
                Localized("Avatar Editor", "Редактор аватара"),
                Localized("Define skeletons and swap them at runtime.", "Визначайте скелети та міняйте їх у рантаймі."),
                HonamiEditorIcons.GetIcon("HonamiAvatarWhite"), HonamiAvatarEditorWindow.Open));
            grid.Add(ToolCard(
                Localized("Mask Editor", "Редактор масок"),
                Localized("Paint avatar masks for per-bone layer control.", "Малюйте маски аватара для контролю шарів по кістках."),
                HonamiEditorIcons.GetIcon("HonamiMaskWhite"), HonamiAvatarMaskEditorWindow.Open));
            grid.Add(ToolCard(
                Localized("Project Validator", "Валідатор проєкту"),
                Localized("Find and fix missing references and broken transitions.", "Знайдіть та виправте втрачені посилання та зламані транзішни."),
                HonamiEditorIcons.GraphWhite, HonamiValidatorWindow.ShowWindow));
            grid.Add(ToolCard(
                Localized("Animation Fixer", "Фіксер кліпів"),
                Localized("Batch process AnimationClips to fix generic/humanoid mismatches.", "Масова обробка кліпів для виправлення проблем generic/humanoid."),
                HonamiEditorIcons.TimelineWhite, HonamiAnimationClipFixerWindow.ShowWindow));
            scroll.Add(grid);
        }

        private void BuildEaseOfUseStep(VisualElement page)
        {
            var scroll = BuildStepScroll(page,
                Localized("SIMPLICITY", "ПРОСТОТА"),
                Localized("No tangled webs, just logic", "Жодних заплутаних павутин, лише логіка"),
                Localized(
                    "Say goodbye to spider-web state machines. Honami is built to keep animation logic clean, predictable, and easy to maintain.",
                    "Попрощайтеся з павутинами стейт-машин. Honami створено, щоб тримати логіку анімації чистою, передбачуваною і легкою в підтримці."));

            var grid = NewGrid();
            grid.Add(FeatureCard(
                Localized("Clean Graphs", "Чисті графи"),
                Localized("Direct transitions and Any State logic without spaghetti connections.", "Прямі транзішни та Any State логіка без спагеті-зв'язків."),
                HonamiEditorIcons.GraphWhite));
            grid.Add(FeatureCard(
                Localized("Code Driven", "Керованість кодом"),
                Localized("Set parameters via strings or hashed IDs directly from C#.", "Встановлюйте параметри через рядки або хеші прямо з C#."),
                HonamiEditorIcons.TimelineWhite));
            grid.Add(FeatureCard(
                Localized("Predictable", "Передбачуваність"),
                Localized("What you see in the editor is exactly what plays in the game.", "Те, що ви бачите в редакторі, — це саме те, що грає в грі."),
                HonamiEditorIcons.Graph));
            grid.Add(FeatureCard(
                Localized("Event System", "Система івентів"),
                Localized("Trigger sounds and logic without Animation Events headache.", "Запускайте звуки та логіку без головного болю з Animation Events."),
                HonamiEditorIcons.Controller));
            scroll.Add(grid);
        }

        private void BuildMigrationStep(VisualElement page)
        {
            var scroll = BuildStepScroll(page,
                Localized("MIGRATION", "МІГРАЦІЯ"),
                Localized("Migrate at your own pace", "Міграція у своєму темпі"),
                Localized(
                    "Moving to Honami is incremental. It works alongside your existing Animator components, plays standard AnimationClips, and converters help port controllers and code.",
                    "Перехід на Honami поступовий. Він працює поруч з наявними Animator-компонентами, грає стандартні AnimationClips, а конвертери допомагають портувати контролери й код."));

            var grid = NewGrid();
            grid.Add(FeatureCard(
                Localized("Keeps Your Clips", "Зберігає ваші кліпи"),
                Localized("Honami plays standard .anim files. No need to re-export assets.", "Honami грає стандартні .anim файли. Не треба переекспортовувати ассети."),
                HonamiEditorIcons.TimelineWhite));
            grid.Add(FeatureCard(
                Localized("Parallel Run", "Паралельна робота"),
                Localized("Transition characters gradually. Honami and Unity Animator can safely coexist in the same scene.", "Переводьте персонажів поступово. Honami та Unity Animator можуть безпечно працювати в одній сцені."),
                HonamiEditorIcons.BlendTreeWhite));
            grid.Add(ToolCard(
                Localized("Controller Converter", "Конвертер контролера"),
                Localized("Automatically convert Unity Animator to Honami Controller.", "Автоматично конвертуйте Unity Animator в Honami Controller."),
                HonamiEditorIcons.Controller, AnimatorToHonamiConverterWindow.ShowWindow));
            grid.Add(ToolCard(
                Localized("Script API Converter", "Конвертер скриптів"),
                Localized("Scan and update Animator API calls in your C# codebase.", "Скануйте та оновлюйте виклики Animator API у вашому C# коді."),
                HonamiEditorIcons.GraphWhite, HonamiScriptConverterWindow.ShowWindow));
            scroll.Add(grid);
        }

        private void BuildQuickSetupStep(VisualElement page)
        {
            var scroll = BuildStepScroll(page,
                Localized("QUICK SETUP", "ШВИДКИЙ СТАРТ"),
                Localized("From zero to animating in five steps", "Від нуля до анімації за п'ять кроків"),
                Localized(
                    "Follow these steps to wire Honami into your project. Each step takes under a minute.",
                    "Виконайте ці кроки, щоб підключити Honami до проєкту. Кожен займає менше хвилини."));

            scroll.Add(SetupStep("1",
                Localized("Create a Controller asset", "Створіть Controller-ассет"),
                Localized(
                    "Right-click in the Project window → Create → Honami → Controller. The template includes Idle and Walk states with a Speed parameter.",
                    "ПКМ у Project → Create → Honami → Controller. Шаблон містить стейти Idle та Walk з параметром Speed."),
                HonamiEditorIcons.Controller,
                Localized("Create now", "Створити зараз"),
                CreateControllerWithExampleGraph));

            scroll.Add(SetupStep("2",
                Localized("Add HonamiAnimator to your character", "Додайте HonamiAnimator до персонажа"),
                Localized(
                    "Select your character GameObject, click Add Component → HonamiAnimator, then assign the Controller asset you just created.",
                    "Виберіть GameObject персонажа, натисніть Add Component → HonamiAnimator та призначте щойно створений Controller."),
                HonamiEditorIcons.GetIcon("HonamiAvatarWhite"),
                null, null));

            scroll.Add(SetupStep("3",
                Localized("Design your state machine", "Налаштуйте стейт-машину"),
                Localized(
                    "Open the Animator Graph, drag in clips, connect transitions and set up parameters. The graph updates live in Play Mode.",
                    "Відкрийте Animator Graph, додайте кліпи, з'єднайте транзішни та налаштуйте параметри. Граф оновлюється в реальному часі."),
                HonamiEditorIcons.GraphWhite,
                Localized("Open Animator", "Відкрити Animator"),
                HonamiGraphWindow.OpenWindow));

            scroll.Add(SetupStep("4",
                Localized("Add a control script", "Додайте скрипт керування"),
                Localized(
                    "Create a MonoBehaviour that drives Honami parameters from code. The generated template animates the Speed parameter automatically so you can see transitions working.",
                    "Створіть MonoBehaviour, що керує параметрами Honami з коду. Згенерований шаблон автоматично анімує параметр Speed, щоб ви побачили транзішни в дії."),
                HonamiEditorIcons.Controller,
                Localized("Create script", "Створити скрипт"),
                CreateSampleScript));

            scroll.Add(SetupStep("5",
                Localized("Press Play", "Натисніть Play"),
                Localized(
                    "Honami's zero-allocation runtime takes over. Watch parameters, weights and active states update live in the graph.",
                    "Zero-allocation рантайм Honami починає роботу. Параметри, ваги та активні стейти оновлюються у графі в реальному часі."),
                HonamiEditorIcons.TimelineWhite,
                null, null));
        }

        private void BuildGetStartedStep(VisualElement page)
        {
            var scroll = BuildStepScroll(page,
                Localized("YOU'RE READY", "ВСЕ ГОТОВО"),
                Localized("Let's make something move", "Зробімо щось живим"),
                Localized(
                    "Create your first controller, then open the Animator to start building. The documentation is a full course on game animation logic.",
                    "Створіть свій перший контролер і відкрийте Animator, щоб почати. Документація — це повний курс із логіки ігрової анімації."));

            HonamiDocumentationBuilder.AddActionButton(scroll,
                Localized("Create a Honami Controller", "Створити Honami контролер"),
                HonamiEditorIcons.Controller,
                HonamiDocumentationBuilder.CreateController);
            HonamiDocumentationBuilder.AddActionButton(scroll,
                Localized("Open the Animator", "Відкрити Animator"),
                HonamiEditorIcons.GraphWhite,
                HonamiGraphWindow.OpenWindow);
            HonamiDocumentationBuilder.AddActionButton(scroll,
                Localized("Read the Documentation", "Читати документацію"),
                HonamiEditorIcons.TimelineWhite,
                HonamiDocumentationWindow.ShowWindow);

            var linksRow = new VisualElement();
            linksRow.style.flexDirection = FlexDirection.Row;
            linksRow.style.alignItems = Align.Center;
            linksRow.style.marginTop = 22;
            linksRow.Add(TextLink(Localized("View on GitHub", "Дивитись на GitHub"), () => Application.OpenURL(HonamiPackageInfo.RepositoryUrl)));
            var dot = new Label("•");
            dot.style.marginLeft = dot.style.marginRight = 10;
            dot.style.color = new Color(0.35f, 0.36f, 0.38f);
            linksRow.Add(dot);
            linksRow.Add(TextLink(Localized("Report an issue", "Повідомити про баг"), () => Application.OpenURL($"{HonamiPackageInfo.RepositoryUrl}/issues")));
            scroll.Add(linksRow);

            var footnote = new Label(Localized(
                "You can reopen this anytime from Window ▸ Honami ▸ Welcome.",
                "Відкрити знову можна будь-коли через Window ▸ Honami ▸ Welcome."));
            footnote.style.fontSize = 11;
            footnote.style.color = new Color(0.5f, 0.52f, 0.55f);
            footnote.style.marginTop = 18;
            footnote.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(footnote);
        }

        private ScrollView BuildStepScroll(VisualElement page, string kicker, string heading, string lead)
        {
            var scroll = new ScrollView { mode = ScrollViewMode.Vertical };
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = scroll.style.paddingRight = 48;
            scroll.style.paddingTop = 40;
            scroll.style.paddingBottom = 32;
            page.Add(scroll);

            var kickerLabel = new Label(kicker);
            kickerLabel.style.fontSize = 11;
            kickerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            kickerLabel.style.letterSpacing = 2.5f;
            kickerLabel.style.color = HonamiEditorTheme.Accent;
            kickerLabel.style.marginBottom = 8;
            scroll.Add(kickerLabel);

            var headingLabel = new Label(heading);
            headingLabel.style.fontSize = 30;
            headingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headingLabel.style.color = Color.white;
            headingLabel.style.letterSpacing = -0.5f;
            headingLabel.style.whiteSpace = WhiteSpace.Normal;
            headingLabel.style.marginBottom = 12;
            scroll.Add(headingLabel);

            var leadLabel = new Label(lead);
            leadLabel.style.fontSize = 14;
            leadLabel.style.color = new Color(0.72f, 0.74f, 0.77f);
            leadLabel.style.whiteSpace = WhiteSpace.Normal;
            leadLabel.style.marginBottom = 26;
            scroll.Add(leadLabel);

            return scroll;
        }

        private static VisualElement NewGrid()
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.SpaceBetween;
            return grid;
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
            card.style.paddingTop = card.style.paddingBottom = 16;
            card.style.paddingLeft = card.style.paddingRight = 16;
            card.style.backgroundColor = CardBg;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 10;
            card.style.borderTopWidth = card.style.borderBottomWidth =
            card.style.borderLeftWidth = card.style.borderRightWidth = 1;
            card.style.borderTopColor = card.style.borderBottomColor =
            card.style.borderLeftColor = card.style.borderRightColor = CardBorder;
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
                chevron.style.color = new Color(0.5f, 0.52f, 0.55f);
                iconRow.Add(chevron);
            }
            card.Add(iconRow);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            titleLabel.style.marginBottom = 6;
            card.Add(titleLabel);

            var descLabel = new Label(desc);
            descLabel.style.fontSize = 12;
            descLabel.style.color = new Color(0.7f, 0.7f, 0.72f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(descLabel);

            var idleBorder = CardBorder;
            var hoverBorder = onClick != null ? HonamiEditorTheme.Accent : new Color(1f, 1f, 1f, 0.16f);

            card.RegisterCallback<MouseOverEvent>(_ =>
            {
                card.style.borderTopColor = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor = hoverBorder;
                card.style.backgroundColor = new Color(0.16f, 0.17f, 0.185f);
                if (onClick != null) card.style.translate = new Translate(0, -2, 0);
            });
            card.RegisterCallback<MouseOutEvent>(_ =>
            {
                card.style.borderTopColor = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor = idleBorder;
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
            step.style.paddingTop = step.style.paddingBottom = 16;
            step.style.paddingLeft = step.style.paddingRight = 20;
            step.style.backgroundColor = CardBg;
            step.style.borderTopLeftRadius = step.style.borderTopRightRadius =
            step.style.borderBottomLeftRadius = step.style.borderBottomRightRadius = 10;
            step.style.borderTopWidth = step.style.borderBottomWidth =
            step.style.borderLeftWidth = step.style.borderRightWidth = 1;
            step.style.borderTopColor = step.style.borderBottomColor =
            step.style.borderLeftColor = step.style.borderRightColor = CardBorder;

            var numBox = new VisualElement();
            numBox.style.width = numBox.style.height = 36;
            numBox.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
            numBox.style.borderTopLeftRadius = numBox.style.borderTopRightRadius =
            numBox.style.borderBottomLeftRadius = numBox.style.borderBottomRightRadius = 18;
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
            titleLabel.style.color = Color.white;
            header.Add(titleLabel);
            content.Add(header);

            var descLabel = new Label(desc);
            descLabel.style.fontSize = 12;
            descLabel.style.color = new Color(0.7f, 0.7f, 0.72f);
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
                btn.style.color = Color.white;
                btn.style.borderTopWidth = btn.style.borderBottomWidth =
                btn.style.borderLeftWidth = btn.style.borderRightWidth = 0;
                btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
                btn.style.paddingTop = btn.style.paddingBottom = 6;
                btn.style.paddingLeft = btn.style.paddingRight = 12;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                
                btn.RegisterCallback<MouseOverEvent>(_ => btn.style.backgroundColor = HonamiEditorTheme.Accent);
                btn.RegisterCallback<MouseOutEvent>(_ => btn.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f));

                btnWrapper.Add(btn);
                step.Add(btnWrapper);
            }

            return step;
        }

        private static void CreateControllerWithExampleGraph()
        {
            var folder = "Assets";
            if (Selection.activeObject != null)
            {
                folder = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!Directory.Exists(folder)) folder = Path.GetDirectoryName(folder);
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/New Honami Controller.asset");
            var controller = ScriptableObject.CreateInstance<HonamiController>();

            controller.parameters = new List<HonamiParameter>
            {
                new HonamiParameter
                {
                    name = "Speed",
                    type = HonamiParameterType.Float,
                    defaultFloat = 0f
                }
            };

            var idleNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
            idleNode.name = "IdleAnimationNode";

            var walkNode = ScriptableObject.CreateInstance<HonamiAnimationNode>();
            walkNode.name = "WalkAnimationNode";

            var idleState = ScriptableObject.CreateInstance<HonamiState>();
            idleState.stateName = "Idle";
            idleState.name = "Idle";
            idleState.layerIndex = 0;
            idleState.isDefaultState = true;
            idleState.loop = true;
            idleState.speed = 1f;
            idleState.node = idleNode;
            idleState.editorPosition = new Vector2(250, 120);

            var walkState = ScriptableObject.CreateInstance<HonamiState>();
            walkState.stateName = "Walk";
            walkState.name = "Walk";
            walkState.layerIndex = 0;
            walkState.isDefaultState = false;
            walkState.loop = true;
            walkState.speed = 1f;
            walkState.node = walkNode;
            walkState.editorPosition = new Vector2(550, 120);

            idleState.transitions = new List<HonamiTransition>
            {
                new HonamiTransition
                {
                    targetStateGuid = walkState.guid,
                    duration = 0.2f,
                    conditions = new List<HonamiCondition>
                    {
                        new HonamiCondition
                        {
                            parameter = "Speed",
                            mode = HonamiConditionMode.Greater,
                            threshold = 0.1f
                        }
                    }
                }
            };

            walkState.transitions = new List<HonamiTransition>
            {
                new HonamiTransition
                {
                    targetStateGuid = idleState.guid,
                    duration = 0.2f,
                    conditions = new List<HonamiCondition>
                    {
                        new HonamiCondition
                        {
                            parameter = "Speed",
                            mode = HonamiConditionMode.Less,
                            threshold = 0.1f
                        }
                    }
                }
            };

            controller.states = new List<HonamiState> { idleState, walkState };

            AssetDatabase.CreateAsset(controller, assetPath);
            AssetDatabase.AddObjectToAsset(idleNode, controller);
            AssetDatabase.AddObjectToAsset(walkNode, controller);
            AssetDatabase.AddObjectToAsset(idleState, controller);
            AssetDatabase.AddObjectToAsset(walkState, controller);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = controller;
        }

        private static void CreateSampleScript()
        {
            var folder = "Assets";
            if (Selection.activeObject != null)
            {
                folder = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!Directory.Exists(folder)) folder = Path.GetDirectoryName(folder);
            }

            var scriptPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/HonamiCharacterController.cs");

            const string template =
@"using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

[RequireComponent(typeof(HonamiAnimator))]
public sealed class HonamiCharacterController : MonoBehaviour
{
    private HonamiAnimator _animator;

    private static readonly int SpeedHash = HonamiAnimator.StringToHash(""Speed"");

    private void Awake()
    {
        TryGetComponent(out _animator);
    }

    private void Update()
    {
        float speed = Mathf.PingPong(Time.time, 1f);
        _animator.SetFloat(SpeedHash, speed);
    }
}
";
            File.WriteAllText(scriptPath, template);
            AssetDatabase.ImportAsset(scriptPath);
            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = scriptAsset;
        }

        private static VisualElement Halo(float size, float alpha)
        {
            var halo = new VisualElement();
            halo.style.position = Position.Absolute;
            halo.style.width = halo.style.height = size;
            halo.style.left = Length.Percent(50);
            halo.style.top = Length.Percent(50);
            halo.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50), 0);
            halo.style.borderTopLeftRadius = halo.style.borderTopRightRadius =
            halo.style.borderBottomLeftRadius = halo.style.borderBottomRightRadius = size * 0.5f;
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
            pill.style.paddingLeft = pill.style.paddingRight = 8;
            pill.style.paddingTop = pill.style.paddingBottom = 2;
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius =
            pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 4;
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
            btn.style.borderTopWidth = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth = 0;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 8;
            btn.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            btn.style.transitionDuration = new List<TimeValue> { new(120, TimeUnit.Millisecond) };

            btn.RegisterCallback<MouseOverEvent>(_ => btn.style.backgroundColor = new Color(1f, 0.42f, 0.62f));
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
            btn.style.color = new Color(0.8f, 0.82f, 0.85f);
            btn.style.backgroundColor = Color.clear;
            btn.style.borderTopWidth = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth = 1;
            btn.style.borderTopColor = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 8;
            btn.style.transitionProperty = new List<StylePropertyName> { "background-color", "color" };
            btn.style.transitionDuration = new List<TimeValue> { new(120, TimeUnit.Millisecond) };

            btn.RegisterCallback<MouseOverEvent>(_ =>
            {
                btn.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
                btn.style.color = Color.white;
            });
            btn.RegisterCallback<MouseOutEvent>(_ =>
            {
                btn.style.backgroundColor = Color.clear;
                btn.style.color = new Color(0.8f, 0.82f, 0.85f);
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

        private static string Localized(string en, string ua) => HonamiDocLocalization.Get(en, ua);
    }
}
