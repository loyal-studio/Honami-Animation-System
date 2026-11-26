using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CustomGraphModesPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Custom Graph Modes", "Власні режими графа");
        public string Category => HonamiDocLocalization.Get("09. Developer API", "09. API для розробників");
        public string SearchKeywords => "graph mode handler HonamiGraphModeHandlerBase IHonamiGraphModeHandler window tab lifecycle switchmode режим графа хендлер вкладка життєвий цикл вікно";
        public int Order => 709;
        public int EstimatedReadTime => 7;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiGraphWindow holds several IHonamiGraphModeHandler implementations and shows one at a time. Animator, Linked Animator and Profile are three of them and get no special treatment; a handler you write is found, hosted and switched to the same way. HonamiGraphModeHandlerBase implements the interface with a working default for every member, so you override only what your mode uses.",
                "HonamiGraphWindow тримає кілька реалізацій IHonamiGraphModeHandler і показує одну за раз. Animator, Linked Animator і Profile - три з них, і жодного особливого ставлення не мають; ваш хендлер знаходиться, тримається і перемикається так само. HonamiGraphModeHandlerBase реалізує інтерфейс із робочим дефолтом на кожен член, тож ви перевизначаєте лише те, що використовує ваш режим."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "ModeId is the key the window stores your mode under. It is written into the tab and read back after a domain reload, so it must be unique and must not change between versions. Prefix it to avoid collisions.",
                "ModeId - ключ, під яким вікно зберігає ваш режим. Він пишеться у вкладку і читається назад після перезавантаження домену, тож має бути унікальним і не змінюватись між версіями. Додайте префікс, щоб уникнути збігів."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Editor;
using HonamiAnimationSystem.Editor.Handlers;
using UnityEngine.UIElements;

public sealed class AuditModeHandler : HonamiGraphModeHandlerBase
{
    private VisualElement _view;
    private Label _summary;

    public override string ModeId => ""MyStudio.Audit"";

    protected override void OnInitialize()
    {
        _view = HonamiGraphStyles.Box();
        _view.Add(HonamiGraphStyles.SubTitle(""Controller Audit""));

        _summary = new Label();
        _view.Add(_summary);
    }

    public override VisualElement GetMainView() => _view;

    public override bool HasContent() => Window.RuntimeController != null;

    public override void SwitchToMode()
    {
        Window.SupportLeftPanel(false);
        Window.SupportRightPanel(false);
        Refresh();
    }

    public override void OnUndoRedo() => Refresh();

    private void Refresh()
    {
        var controller = Window.Controller;
        _summary.text = controller == null
            ? ""No controller.""
            : $""{controller.states.Count} states across {controller.layers.Count} layers."";
    }
}");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Initialization", "Ініціалізація"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "In its OnEnable, HonamiGraphWindow constructs every handler it found, calls Initialize on each, then parents the elements returned by GetMainView and GetLeftPanelView into the centre area and the left panel with display set to None. From then on it calls only the handler whose ModeId is active.",
                "У своєму OnEnable HonamiGraphWindow створює кожен знайдений хендлер, викликає в кожного Initialize, потім вкладає елементи, повернуті GetMainView і GetLeftPanelView, у центральну область і ліву панель зі значенням display None. Далі воно викликає лише той хендлер, чий ModeId активний."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Initialize is not virtual. It assigns the Window property and then calls OnInitialize, which is the method you override. This means Window is never null inside your setup, and a subclass cannot break it by forgetting to call the base implementation.",
                "Initialize не є віртуальним. Він призначає властивість Window і потім викликає OnInitialize - саме його ви перевизначаєте. Тому Window ніколи не є null усередині вашої ініціалізації, і підклас не може це зламати, забувши викликати базову реалізацію."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "GetMainView and GetLeftPanelView must return the same instance every call. The window parents the element once and then only toggles its display; returning a new element each time leaves the parented one on screen. Return null from GetLeftPanelView when your mode has no left panel.",
                "GetMainView і GetLeftPanelView мають повертати той самий екземпляр на кожен виклик. Вікно вкладає елемент один раз і далі лише перемикає display; повернення нового елемента щоразу лишить на екрані вкладений раніше. Повертайте null з GetLeftPanelView, коли у вашого режиму немає лівої панелі."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("When each member is called", "Коли викликається кожен член"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Член"), 230),
                (HonamiDocLocalization.Get("Called", "Викликається"), 0),
                ("", 0),
                ("OnInitialize()", HonamiDocLocalization.Get("Once, from Initialize, during the window's OnEnable.", "Один раз, з Initialize, під час OnEnable вікна."), ""),
                ("GetMainView()", HonamiDocLocalization.Get("After Initialize, on every SwitchMode, and when the empty state is recalculated.", "Після Initialize, при кожному SwitchMode і при перерахунку порожнього стану."), ""),
                ("GetLeftPanelView()", HonamiDocLocalization.Get("After Initialize and on every SwitchMode.", "Після Initialize і при кожному SwitchMode."), ""),
                ("SwitchToMode()", HonamiDocLocalization.Get("From SwitchMode, after your views are shown.", "З SwitchMode, після того як ваші панелі показано."), ""),
                ("ApplySettings()", HonamiDocLocalization.Get("Immediately after SwitchToMode, and on every HonamiGraphSettings.Changed.", "Одразу після SwitchToMode і на кожну подію HonamiGraphSettings.Changed."), ""),
                ("HasContent()", HonamiDocLocalization.Get("From UpdateEmptyState. Returning false hides your view and shows the window's empty-state panel.", "З UpdateEmptyState. Повернення false ховає вашу панель і показує порожній стан вікна."), ""),
                ("BuildRightPanel(content)", HonamiDocLocalization.Get("From HonamiGraphWindow.BuildRightPanel. The element is shared between modes, so clear it before filling it.", "З HonamiGraphWindow.BuildRightPanel. Елемент спільний для режимів, тож очистіть його перед наповненням."), ""),
                ("RebuildLeftPanel()", HonamiDocLocalization.Get("From the window and from layer operations that change what the left panel lists.", "З вікна та з операцій над шарами, які змінюють вміст лівої панелі."), ""),
                ("OnSelectionChange()", HonamiDocLocalization.Get("On Selection.selectionChanged, and once through delayCall after the window opens.", "На Selection.selectionChanged і один раз через delayCall після відкриття вікна."), ""),
                ("OnUndoRedo()", HonamiDocLocalization.Get("On Undo.undoRedoPerformed.", "На Undo.undoRedoPerformed."), ""),
                ("Update()", HonamiDocLocalization.Get("From the window's Update, only while your mode is active.", "З Update вікна, лише поки ваш режим активний."), ""),
                ("FrameAll()", HonamiDocLocalization.Get("From the Frame All button in the window header.", "З кнопки Frame All у шапці вікна."), ""),
                ("OnEnable() / OnDisable()", HonamiDocLocalization.Get("From the window's OnEnable and OnDisable, and only for the active handler.", "З OnEnable і OnDisable вікна, і лише для активного хендлера."), "")
            );

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "OnEnable and OnDisable reach only the active handler, so a mode that was never switched to never receives them. Subscribe to editor events in OnInitialize if you need them regardless of which mode is showing, and unsubscribe from AssemblyReloadEvents.beforeAssemblyReload.",
                "OnEnable і OnDisable доходять лише до активного хендлера, тож режим, на який жодного разу не перемикались, їх не отримає. Підписуйтесь на редакторські події в OnInitialize, якщо вони потрібні незалежно від активного режиму, і відписуйтесь з AssemblyReloadEvents.beforeAssemblyReload."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Switching to your mode", "Перемикання на ваш режим"), HonamiEditorIcons.Graph);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiGraphWindow.SwitchMode is public and takes a ModeId. A menu item that opens the window and switches it is the usual entry point.",
                "HonamiGraphWindow.SwitchMode є публічним і приймає ModeId. Звичайна точка входу - пункт меню, що відкриває вікно й перемикає його."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"[MenuItem(""Window/Honami/Controller Audit"")]
private static void Open()
{
    var window = EditorWindow.GetWindow<HonamiGraphWindow>();
    window.SwitchMode(""MyStudio.Audit"");
}");

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "SwitchMode changes the mode of the current tab. To give your mode a tab of its own, add a HonamiGraphTab with its ModeId set to yours to HonamiGraphWindow.Tabs and switch to it. A tab keeps its ModeId across tab switches and domain reloads.",
                "SwitchMode змінює режим поточної вкладки. Щоб дати режиму власну вкладку, додайте HonamiGraphTab із вашим ModeId до HonamiGraphWindow.Tabs і перемкніться на нього. Вкладка зберігає свій ModeId між перемиканнями вкладок і перезавантаженнями домену."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What Window gives you", "Що дає Window"), HonamiEditorIcons.Profile);
            HonamiDocumentationBuilder.AddParameterTable(root,
                ("Controller", "HonamiController", HonamiDocLocalization.Get("The base controller of the open asset, or null.", "Базовий контролер відкритого ассета, або null.")),
                ("RuntimeController", "HonamiRuntimeController", HonamiDocLocalization.Get("The controller as opened, which may be a HonamiOverrideController.", "Контролер у тому вигляді, як його відкрито; може бути HonamiOverrideController.")),
                ("CurrentLayerIndex", "int", HonamiDocLocalization.Get("The layer shown in the graph.", "Шар, показаний у графі.")),
                ("LinkedGraph / ProfileGraph", "asset", HonamiDocLocalization.Get("What the other built-in modes have open, or null.", "Те, що відкрито в інших вбудованих режимах, або null.")),
                ("SupportLeftPanel(bool)", "void", HonamiDocLocalization.Get("Declares whether your mode has a left panel. Passing false hides its toggle instead of leaving a dead button.", "Оголошує, чи має ваш режим ліву панель. false ховає її тогл замість мертвої кнопки.")),
                ("SupportRightPanel(bool)", "void", HonamiDocLocalization.Get("The same for the right panel.", "Те саме для правої панелі.")),
                ("BuildRightPanel()", "void", HonamiDocLocalization.Get("Asks the window to rebuild the inspector, which calls back into your BuildRightPanel.", "Просить вікно перебудувати інспектор, що повертається у ваш BuildRightPanel.")),
                ("UpdateEmptyState()", "void", HonamiDocLocalization.Get("Asks the window to call HasContent again after your data changed.", "Просить вікно викликати HasContent заново після зміни ваших даних.")),
                ("ShowNotification(...)", "static void", HonamiDocLocalization.Get("Posts a notification into the window without holding a reference to it.", "Показує сповіщення у вікні, не тримаючи на нього посилання."))
            );

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "If GetMainView returns a GraphView, HonamiGraphWindow.OnAppearanceChanged passes it to HonamiGraphAccent.RefreshGraphView when the user changes the theme. FrameAll and ApplySettings always route to your overrides, whatever type your view is.",
                "Якщо GetMainView повертає GraphView, HonamiGraphWindow.OnAppearanceChanged передає його в HonamiGraphAccent.RefreshGraphView при зміні теми користувачем. FrameAll і ApplySettings завжди йдуть у ваші перевизначення, хай якого типу ваша панель."
            ));
        }
    }
}
