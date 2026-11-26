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
            title.style.color = TextColor;
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
                "A complete, high-performance replacement for Unity's Animator - with a live graph, procedural rigging and a zero-allocation runtime.",
                "Повноцінна, високопродуктивна заміна Unity Animator - з живим графом, процедурним рігінгом та рантаймом без алокацій."));
            tagline.style.fontSize = 14;
            tagline.style.color = SoftText;
            tagline.style.whiteSpace = WhiteSpace.Normal;
            tagline.style.unityTextAlign = TextAnchor.MiddleCenter;
            tagline.style.maxWidth = 480;
            tagline.style.marginTop = 18;
            page.Add(tagline);

            var langHint = new Label(Localized("Choose your language", "Оберіть мову"));
            langHint.style.fontSize = 10;
            langHint.style.unityFontStyleAndWeight = FontStyle.Bold;
            langHint.style.letterSpacing = 1.5f;
            langHint.style.color = FaintText;
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
            group.Padding(3);
            group.Radius(9);
            group.Border(1, Hairline);

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
            btn.Margin(0);
            btn.style.paddingLeft = btn.style.paddingRight = 0;
            btn.style.fontSize = 12;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = active ? HonamiEditorTheme.Accent : Color.clear;
            btn.style.color = active ? Color.white : HonamiEditorTheme.MutedText;
            btn.BorderWidth(0);
            btn.Radius(7);
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
                Localized("Evaluation produces no garbage, so your framerate stays smooth.", "Обчислення не створює garbage - FPS лишається стабільним."),
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
                Localized("Encapsulate sounds, effects and IK inside states to keep graphs clean.", "Інкапсулюйте звуки, ефекти та IK у стейти - граф лишається чистим."),
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
                    "A full editor suite - click any card to open it now, or reach it later from Window ▸ Honami.",
                    "Повний набір редакторів - клікніть будь-яку картку, щоб відкрити зараз, або пізніше через Window ▸ Honami."));

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
            grid.Add(ToolCard(
                Localized("Humanoid Baker", "Бейкер Humanoid"),
                Localized("Retarget marketplace Humanoid clips onto your skeleton and bake them to Generic.", "Ретаргет Humanoid-кліпів з маркетплейсу на ваш скелет і бейк у Generic."),
                HonamiEditorIcons.BlendTreeWhite, HonamiHumanoidBakerWindow.ShowWindow));
            scroll.Add(grid);
        }

        private void BuildAppearanceStep(VisualElement page)
        {
            var scroll = BuildStepScroll(page,
                Localized("APPEARANCE", "ВИГЛЯД"),
                Localized("Make it yours", "Зробіть його своїм"),
                Localized(
                    "Pick a theme for every Honami window - it applies instantly and you can change it anytime.",
                    "Оберіть тему для всіх вікон Honami - вона застосовується миттєво, і її можна змінити будь-коли."));

            var presets = HonamiThemePresets.All;
            int activePreset = HonamiAppearanceSettings.FindMatchingPreset();

            var grid = NewGrid();
            for (int i = 0; i < presets.Length; i++)
            {
                grid.Add(ThemeCard(presets[i], i == activePreset));
            }
            scroll.Add(grid);

            if (activePreset < 0)
            {
                var customNote = new Label(Localized(
                    "You are using a custom theme right now - it stays untouched until you pick a preset.",
                    "Зараз у вас кастомна тема - вона залишиться, поки ви не оберете пресет."));
                customNote.style.fontSize = 11;
                customNote.style.color = FaintText;
                customNote.style.whiteSpace = WhiteSpace.Normal;
                customNote.style.marginTop = 4;
                scroll.Add(customNote);
            }

            var linksRow = new VisualElement();
            linksRow.style.flexDirection = FlexDirection.Row;
            linksRow.style.marginTop = 14;
            linksRow.Add(TextLink(
                Localized("Fine-tune every color in Preferences", "Тонко налаштувати кожен колір у Preferences"),
                () => HonamiPreferences.Open("Appearance")));
            scroll.Add(linksRow);
        }

        private VisualElement ThemeCard(HonamiThemePreset preset, bool isActive)
        {
            var accent = preset.Colors[0];
            var windowBg = preset.Colors[2];
            var panelBg = preset.Colors[3];
            var toolbarBg = preset.Colors[4];
            var boxBg = preset.Colors[5];
            var text = preset.Colors[6];
            var mutedText = preset.Colors[7];
            var canvasBg = preset.Colors[8];

            var card = new VisualElement();
            card.style.width = Length.Percent(31.5f);
            card.style.marginBottom = 14;
            card.Padding(10);
            card.style.backgroundColor = CardBg;
            card.Radius(10);
            card.BorderWidth(1);
            var idleBorder = isActive ? accent : CardBorder;
            card.BorderColor(idleBorder);
            card.style.transitionProperty = new List<StylePropertyName> { "border-color", "background-color", "translate" };
            card.style.transitionDuration = new List<TimeValue> { new(120, TimeUnit.Millisecond) };
            card.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };

            var preview = new VisualElement();
            preview.style.height = 92;
            preview.style.flexShrink = 0;
            preview.style.backgroundColor = windowBg;
            preview.style.overflow = Overflow.Hidden;
            preview.Radius(6);

            var toolbar = new VisualElement();
            toolbar.style.height = 14;
            toolbar.style.flexShrink = 0;
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 7;
            toolbar.style.backgroundColor = toolbarBg;
            toolbar.Add(Bar(26, 3, text));
            var toolbarChip = Bar(12, 3, accent);
            toolbarChip.style.marginLeft = 5;
            toolbar.Add(toolbarChip);
            preview.Add(toolbar);

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;

            var canvas = new VisualElement();
            canvas.style.flexGrow = 1;
            canvas.style.backgroundColor = canvasBg;
            canvas.style.alignItems = Align.Center;
            canvas.style.justifyContent = Justify.Center;

            var node = new VisualElement();
            node.Padding(6, 8);
            node.style.backgroundColor = boxBg;
            node.Radius(3);
            node.Border(1, accent);
            node.Add(Bar(34, 3, text));
            var nodeSubBar = Bar(22, 3, mutedText);
            nodeSubBar.style.marginTop = 4;
            node.Add(nodeSubBar);
            canvas.Add(node);
            body.Add(canvas);

            var panel = new VisualElement();
            panel.style.width = 30;
            panel.style.flexShrink = 0;
            panel.style.backgroundColor = panelBg;
            panel.style.alignItems = Align.Center;
            panel.style.paddingTop = 8;
            panel.Add(Bar(16, 12, boxBg));
            var panelButton = Bar(16, 6, accent);
            panelButton.style.marginTop = 5;
            panel.Add(panelButton);
            body.Add(panel);

            preview.Add(body);
            card.Add(preview);

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.justifyContent = Justify.SpaceBetween;
            nameRow.style.marginTop = 9;

            var nameLabel = new Label(preset.Name);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = TextColor;
            nameRow.Add(nameLabel);

            var check = new Label("✓");
            check.style.fontSize = 12;
            check.style.unityFontStyleAndWeight = FontStyle.Bold;
            check.style.color = accent;
            check.style.visibility = isActive ? Visibility.Visible : Visibility.Hidden;
            nameRow.Add(check);
            card.Add(nameRow);

            card.RegisterCallback<MouseOverEvent>(_ =>
            {
                card.BorderColor(accent);
                card.style.backgroundColor = CardHoverBg;
                card.style.translate = new Translate(0, -2, 0);
            });
            card.RegisterCallback<MouseOutEvent>(_ =>
            {
                card.BorderColor(idleBorder);
                card.style.backgroundColor = CardBg;
                card.style.translate = new Translate(0, 0, 0);
            });
            card.RegisterCallback<ClickEvent>(_ => ApplyThemePreset(preset));

            return card;
        }

        private void ApplyThemePreset(HonamiThemePreset preset)
        {
            HonamiAppearanceSettings.ApplyPreset(preset);
            _appearanceRebuild.Request(0);
        }

        private static VisualElement Bar(float width, float height, Color color)
        {
            var bar = new VisualElement();
            bar.style.width = width;
            bar.style.height = height;
            bar.style.flexShrink = 0;
            bar.style.backgroundColor = color;
            bar.Radius(Mathf.Min(height * 0.5f, 3f));
            return bar;
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
                Localized("What you see in the editor is exactly what plays in the game.", "Те, що ви бачите в редакторі, - це саме те, що грає в грі."),
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
                    "Створіть свій перший контролер і відкрийте Animator, щоб почати. Документація - це повний курс із логіки ігрової анімації."));

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
            dot.style.color = FaintText;
            linksRow.Add(dot);
            linksRow.Add(TextLink(Localized("Report an issue", "Повідомити про баг"), () => Application.OpenURL($"{HonamiPackageInfo.RepositoryUrl}/issues")));
            scroll.Add(linksRow);

            var footnote = new Label(Localized(
                "You can reopen this anytime from Window ▸ Honami ▸ Welcome.",
                "Відкрити знову можна будь-коли через Window ▸ Honami ▸ Welcome."));
            footnote.style.fontSize = 11;
            footnote.style.color = FaintText;
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
            headingLabel.style.color = TextColor;
            headingLabel.style.letterSpacing = -0.5f;
            headingLabel.style.whiteSpace = WhiteSpace.Normal;
            headingLabel.style.marginBottom = 12;
            scroll.Add(headingLabel);

            var leadLabel = new Label(lead);
            leadLabel.style.fontSize = 14;
            leadLabel.style.color = SoftText;
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
    }
}
