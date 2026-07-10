using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class GettingStartedPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("First Steps", "Перші кроки");
        public string Category => HonamiDocLocalization.Get("01. Start Here", "01. Старт");
        public string SearchKeywords => "setup components animator runtime controller create attach startup updatemode initial pose fps cap налаштування компоненти перші кроки";
        public int Order => 20;
        public int EstimatedReadTime => 7;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "Honami requires a Unity Animator component on the same GameObject. The Animator's Controller slot must be left EMPTY — Honami completely bypasses the built-in Animator logic, building and driving the PlayableGraph directly at runtime.",
                    "Для роботи Honami необхідний стандартний компонент Unity Animator на тому ж GameObject. Проте слот Controller в Animator має обов'язково залишатися ПОРОЖНІМ — Honami повністю обходить вбудовану логіку Animator, генеруючи та керуючи PlayableGraph безпосередньо в рантаймі."
                ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Core Components", "Фундаментальні компоненти"), HonamiEditorIcons.Controller);

            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("Unity Animator", "Unity Animator"), HonamiDocLocalization.Get("The underlying engine required for processing skeletal animations. Keep its Controller slot empty.", "Базовий рушій для обробки скелетних анімацій. Тримайте слот Controller порожнім."), HonamiEditorIcons.Controller),
                (HonamiDocLocalization.Get("Honami Animator", "Honami Animator"), HonamiDocLocalization.Get("The bridge connecting your gameplay code to the animation system.", "Міст, що з'єднує ваш ігровий код із системою анімації."), HonamiEditorIcons.GraphWhite),
                (HonamiDocLocalization.Get("Honami Controller", "Honami Controller"), HonamiDocLocalization.Get("The logic brain of your character. Stores states, transitions, and nodes.", "Логічний мозок вашого персонажа. Зберігає стани, переходи та вузли."), HonamiEditorIcons.TimelineWhite)
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Setup in five steps", "Налаштування за п'ять кроків"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "1. Add an Animator component to your character (or use the one from the model import) and clear its Controller slot.",
                "1. Додайте компонент Animator на персонажа (або використайте той, що прийшов з імпортом моделі) та очистіть його слот Controller."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "2. Add the Honami Animator component (Add Component → Honami Animation → Honami Animator).",
                "2. Додайте компонент Honami Animator (Add Component → Honami Animation → Honami Animator)."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "3. Create a controller asset: right-click in the Project window → Create → Honami Animation → Controller, or use the button below.",
                "3. Створіть ассет контролера: правий клік у вікні Project → Create → Honami Animation → Controller, або кнопкою нижче."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "4. Assign the controller to the Honami Animator, open the Graph Editor and add your first Animation Node. Mark it as the default state.",
                "4. Призначте контролер компоненту Honami Animator, відкрийте Graph Editor і додайте перший Animation Node. Позначте його станом за замовчуванням."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "5. Press Play. The default state starts automatically according to the Startup setting.",
                "5. Натисніть Play. Стан за замовчуванням запуститься автоматично відповідно до налаштування Startup."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Startup", "Startup"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Startup property picks the moment the playable graph is built: Enable (default) builds it in OnEnable, Start postpones it to Start. Use Start when other components must configure the character first.",
                "Властивість Startup визначає момент побудови playable-графа: Enable (типово) будує його в OnEnable, Start — відкладає до Start. Використовуйте Start, коли інші компоненти мають спершу налаштувати персонажа."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Update Modes", "Режими оновлення"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The 'Update Mode' property determines when the animation advances:",
                "Властивість «Update Mode» визначає, коли саме оновлюється анімація:"
            ));

            HonamiDocumentationBuilder.AddParameterTable(root,
                ("Normal", "Update", HonamiDocLocalization.Get("Standard execution loop. Uses Time.deltaTime.", "Стандартний цикл оновлення. Використовує Time.deltaTime.")),
                ("UnscaledTime", "Update", HonamiDocLocalization.Get("Ignores Time.timeScale. Ideal for UI or pause menus.", "Ігнорує Time.timeScale. Ідеально для UI або меню паузи.")),
                ("AnimatePhysics", "FixedUpdate", HonamiDocLocalization.Get("Synchronized with Unity physics.", "Синхронізовано з фізикою Unity.")),
                ("LateUpdate", "LateUpdate", HonamiDocLocalization.Get("Advances after all Update logic — useful when gameplay code must finish moving things first.", "Оновлюється після всієї Update-логіки — корисно, коли ігровий код має спершу завершити рухи.")),
                ("Manual", "Code", HonamiDocLocalization.Get("You drive the Tick via code: honami.Tick(dt).", "Ви самі викликаєте оновлення через код: honami.Tick(dt)."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Other inspector settings", "Інші налаштування інспектора"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Initial Pose", "Initial Pose"), HonamiDocLocalization.Get("Captures the hierarchy pose before Honami starts and restores it whenever no state is active — the character never freezes in a random frame. Optionally captures from the default state's last frame instead.", "Знімає позу ієрархії до старту Honami та відновлює її, коли жоден стан не активний — персонаж ніколи не застигає у випадковому кадрі. Опційно знімає з останнього кадру стану за замовчуванням.")),
                (HonamiDocLocalization.Get("Time Scale", "Time Scale"), HonamiDocLocalization.Get("Per-animator playback speed multiplier (0-10).", "Множник швидкості відтворення для цього аніматора (0-10).")),
                (HonamiDocLocalization.Get("FPS Cap", "FPS Cap"), HonamiDocLocalization.Get("Evaluates the graph at a fixed rate (1-120 FPS) with optional interpolation — a cheap LOD for distant characters.", "Оцінює граф із фіксованою частотою (1-120 FPS) з опційною інтерполяцією — дешевий LOD для віддалених персонажів.")),
                (HonamiDocLocalization.Get("Apply Root Motion", "Apply Root Motion"), HonamiDocLocalization.Get("Forwards root motion from clips to the Animator.", "Передає root motion із кліпів до Animator.")),
                (HonamiDocLocalization.Get("Culling Mode", "Culling Mode"), HonamiDocLocalization.Get("Standard Unity AnimatorCullingMode passed to the Animator.", "Стандартний AnimatorCullingMode Unity, що передається в Animator.")),
                (HonamiDocLocalization.Get("Linked System", "Linked System"), HonamiDocLocalization.Get("Prevent Linking excludes this animator from Linked Brains; Linking Tag identifies it for tag-targeted broadcasts.", "Prevent Linking виключає аніматор із Linked Brain; Linking Tag ідентифікує його для розсилок за тегом.")),
                (HonamiDocLocalization.Get("Avatar", "Avatar"), HonamiDocLocalization.Get("Optional Honami Avatar for mirroring and skeleton remapping — see Avatars & Masks.", "Опційний Honami Avatar для віддзеркалення та ремапінгу скелета — див. Avatars & Masks."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("First script", "Перший скрипт"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;

public sealed class SimpleCharacter : MonoBehaviour
{
    private HonamiAnimator _honami;

    private void Awake()
    {
        TryGetComponent(out _honami);
    }

    private void Update()
    {
        _honami.SetFloat(""Speed"", Input.GetAxis(""Vertical""));

        if (Input.GetKeyDown(KeyCode.Space))
            _honami.PlayState(""Jump"", transitionDuration: 0.1f);
    }
}");

            HonamiDocumentationBuilder.AddSeparator(root);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Setup done. Continue with the tutorial series '02. Tutorial: First Character' — it walks you from a single idle state to a moving, jumping character with sound events, one concept per page.",
                "Налаштування завершено. Продовжуйте серією «02. Туторіал: Перший персонаж» — вона проведе вас від одного idle-стейту до персонажа, що рухається, стрибає та звучить, по одному концепту на сторінку."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Quick Actions", "Швидкі дії"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddActionGroup(root,
                (HonamiDocLocalization.Get("Create New Controller", "Створити новий контролер"), HonamiEditorIcons.TimelineWhite, () => HonamiDocumentationBuilder.CreateController()),
                (HonamiDocLocalization.Get("Open Graph Editor", "Відкрити редактор графа"), HonamiEditorIcons.GraphWhite, () => HonamiGraphWindow.OpenWindow())
            );
        }
    }
}
