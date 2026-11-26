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
    public sealed partial class HonamiWelcomeWindow : EditorWindow
    {
        private const string SessionShownKey = "Honami.Welcome.ShownThisSession";
        private const string IntroPlayedKey = "Honami.Welcome.IntroPlayed";
        private const int Steps = 8;
        private const double FirstRunSettleSeconds = 0.75;

        private static double _idleSince = -1;

        private static readonly Vector2 WindowSize = new(880, 720);
        private static readonly Color Hairline = new(1f, 1f, 1f, 0.06f);

        private static Color CardBg => HonamiGraphStyles.BoxBg;
        private static Color CardBorder => HonamiGraphStyles.BoxBorder;
        private static Color CardHoverBg => Color.Lerp(HonamiGraphStyles.BoxBg, Color.white, 0.05f);
        private static Color BarBg => HonamiEditorTheme.ToolbarBg;
        private static Color TextColor => HonamiEditorTheme.Text;
        private static Color SoftText => Color.Lerp(HonamiEditorTheme.Text, HonamiEditorTheme.MutedText, 0.45f);
        private static Color FaintText => HonamiEditorTheme.MutedText;
        private static Color OverlayBg => HonamiEditorTheme.PanelBg;

        private static Color OverlayBgClear
        {
            get
            {
                var color = HonamiEditorTheme.PanelBg;
                color.a = 0f;
                return color;
            }
        }

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

        private HonamiAppearanceRebuildScheduler _appearanceRebuild;

        private void OnEnable()
        {
            _appearanceRebuild ??= new HonamiAppearanceRebuildScheduler(RebuildForAppearance);
            HonamiAppearanceSettings.Changed += RequestAppearanceRebuild;

            BuildShell();
            GoTo(0);
            MarkSeenWhenPresented();

            if (SessionState.GetBool(IntroPlayedKey, false)) return;

            EditorApplication.update -= TryPlayIntro;
            EditorApplication.update += TryPlayIntro;
        }

        private void RequestAppearanceRebuild() => _appearanceRebuild.Request();

        private void RebuildForAppearance()
        {
            if (this == null) return;
            BuildShell();
            GoTo(_step);
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
            HonamiAppearanceSettings.Changed -= RequestAppearanceRebuild;
            _appearanceRebuild?.Cancel();
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
            overlay.style.backgroundColor = OverlayBg;
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
            title.style.color = TextColor;
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

        private VisualElement BuildTopBar()
        {
            var bar = new VisualElement();
            bar.style.height = 44;
            bar.style.flexShrink = 0;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 18;
            bar.style.paddingRight = 12;
            bar.style.backgroundColor = BarBg;
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
            bar.style.backgroundColor = BarBg;
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
            overlay.style.backgroundColor = OverlayBgClear;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            overlay.style.transitionDuration = new List<TimeValue> { new(500, TimeUnit.Millisecond) };
            overlay.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseInCubic) };
            root.Add(overlay);

            overlay.schedule.Execute(() =>
            {
                overlay.style.backgroundColor = OverlayBg;
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
            heading.style.color = TextColor;
            heading.style.letterSpacing = -0.5f;
            heading.style.marginTop = 18;
            heading.style.opacity = 0f;
            overlay.Add(heading);

            var wish = new Label(Localized(
                "Good luck with your project - may every frame be smooth!",
                "Удачі з вашим проєктом - нехай кожен кадр буде плавним!"));
            wish.style.fontSize = 14;
            wish.style.color = SoftText;
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
                "Window ▸ Honami ▸ Welcome - відкрити знову будь-коли."));
            footnote.style.fontSize = 11;
            footnote.style.color = FaintText;
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
                case 1: BuildAppearanceStep(page); break;
                case 2: BuildFeaturesStep(page); break;
                case 3: BuildEaseOfUseStep(page); break;
                case 4: BuildMigrationStep(page); break;
                case 5: BuildToolsStep(page); break;
                case 6: BuildQuickSetupStep(page); break;
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
                dot.Radius(3.5f);
                dot.style.backgroundColor = active ? HonamiEditorTheme.Accent : new Color(1f, 1f, 1f, 0.16f);
                dot.style.transitionProperty = new List<StylePropertyName> { "width", "background-color" };
                dot.style.transitionDuration = new List<TimeValue> { new(220, TimeUnit.Millisecond) };
                dot.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };
                _dotsRow.Add(dot);
            }
        }

        private static string Localized(string en, string ua) => HonamiDocLocalization.Get(en, ua);
    }
}
