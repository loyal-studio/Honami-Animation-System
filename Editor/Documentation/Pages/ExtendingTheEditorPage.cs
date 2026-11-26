using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class ExtendingTheEditorPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Extending the Editor", "Розширення редактора");
        public string Category => HonamiDocLocalization.Get("09. Developer API", "09. API для розробників");
        public string SearchKeywords => "editor extension extensibility typecache assembly definition asmdef shared ui розширення редактора асембл спільний інтерфейс";
        public int Order => 706;
        public int EstimatedReadTime => 5;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Each extension point is a base class or an interface in the HonamiAnimationSystem.Editor assembly. You derive from it in your own assembly and Honami collects your type with UnityEditor.TypeCache. There is no registration call and no file inside the package to edit. The pages after this one cover each point in detail; this page covers what applies to all of them.",
                "Кожна точка розширення - це базовий клас або інтерфейс в асемблі HonamiAnimationSystem.Editor. Ви успадковуєтесь від нього у власному асемблі, а Honami збирає ваш тип через UnityEditor.TypeCache. Немає ні виклику реєстрації, ні файлу всередині пакета, який треба правити. Наступні сторінки розбирають кожну точку детально; ця стосується всіх одразу."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The extension points", "Точки розширення"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Base type", "Базовий тип"), 280),
                (HonamiDocLocalization.Get("Collected by", "Хто збирає"), 250),
                (HonamiDocLocalization.Get("Appears in", "Де з'являється"), 0),
                ("HonamiNodeEditorBase", "HonamiNodeEditorRegistry", HonamiDocLocalization.Get("The graph, the State inspector and the Timeline window.", "Граф, інспектор стану та вікно Timeline.")),
                ("HonamiTimelineModuleEditor", "HonamiTimelineModuleEditorRegistry", HonamiDocLocalization.Get("The Timeline window, for both timelines and states: module rows, items, the Add Module menu, the properties panel and edit-mode preview.", "Вікно Timeline, і для таймлайнів, і для станів: рядки модулів, item-и, меню Add Module, панель властивостей та прев'ю в Edit Mode.")),
                ("HonamiStateInspectorSection", "HonamiInspectorSectionRegistry", HonamiDocLocalization.Get("The State inspector, below Sub-Nodes.", "Інспектор стану, під Sub-Nodes.")),
                ("HonamiTransitionInspectorSection", "HonamiInspectorSectionRegistry", HonamiDocLocalization.Get("The Transition inspector, below Parameter Assignments.", "Інспектор переходу, під Parameter Assignments.")),
                ("HonamiGraphModeHandlerBase", "HonamiGraphWindow", HonamiDocLocalization.Get("The Honami Animator window, as a mode next to Animator, Linked Animator and Profile.", "Вікно Honami Animator, як режим поряд з Animator, Linked Animator і Profile.")),
                ("IHonamiDocumentationPage", "HonamiDocumentationWindow", HonamiDocLocalization.Get("This window, placed by Category and Order.", "Це вікно, розміщується за Category та Order.")),
                (HonamiDocLocalization.Get("Unity [CustomEditor]", "Unity [CustomEditor]"), "UnityEditor", HonamiDocLocalization.Get("The inspector for your own HonamiSubNodeBase type, which is a ScriptableObject.", "Інспектор для вашого типу HonamiSubNodeBase, який є ScriptableObject."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Where your code lives", "Де живе ваш код"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Files in any folder named Editor compile into Assembly-CSharp-Editor, which already references Honami. For a package or a larger project, give your editor code its own assembly definition.",
                "Файли в будь-якій теці з назвою Editor збираються в Assembly-CSharp-Editor, який уже посилається на Honami. Для пакета або великого проєкту дайте редакторському коду власний assembly definition."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"{
    ""name"": ""MyStudio.HonamiTools.Editor"",
    ""references"": [
        ""HonamiAnimationSystem.Editor"",
        ""HonamiAnimationSystem.Runtime""
    ],
    ""includePlatforms"": [ ""Editor"" ]
}");

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Do not wrap files in #if UNITY_EDITOR when they are already in an Editor-only assembly. The directive is redundant there; the Honami editor assembly does not use it.",
                "Не загортайте файли в #if UNITY_EDITOR, якщо вони вже в Editor-only асемблі. Директива там зайва; редакторський асембл Honami її не використовує."
            ), HonamiDocumentationBuilder.CalloutType.Tip);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What the collectors require", "Що вимагають збирачі"), HonamiEditorIcons.Graph);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami constructs your type with Activator.CreateInstance, so it must be non-abstract and have a public parameterless constructor. A type without one is skipped and logged.",
                "Honami створює ваш тип через Activator.CreateInstance, тож він має бути неабстрактним і мати публічний конструктор без параметрів. Тип без такого конструктора пропускається із записом у лог."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "One instance is created and reused for every state, transition or node it handles. Do not store what you are currently inspecting in a field - it arrives as a parameter on each call.",
                "Створюється один екземпляр і перевикористовується для кожного стану, переходу чи ноди, які він обслуговує. Не зберігайте у полі те, що інспектуєте зараз - воно приходить параметром на кожен виклик."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "An exception thrown from a constructor or from a build method is caught, written to the Console and confined to that one extension. Inspector sections are replaced by an error HelpBox in their own slot; the rest of the window keeps working.",
                "Виняток із конструктора або з методу побудови перехоплюється, пишеться в Console і локалізується цим одним розширенням. Секції інспектора замінюються на HelpBox з помилкою у своєму місці; решта вікна продовжує працювати."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Collection runs again after every domain reload, so a newly compiled type appears without reopening the window.",
                "Збір виконується заново після кожного перезавантаження домену, тож щойно скомпільований тип з'являється без перевідкриття вікна."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Shared editor UI", "Спільний редакторський інтерфейс"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "These are the same types the built-in windows are built from. All colours come from HonamiAppearanceSettings, so UI built with them follows the accent the user picked in Preferences.",
                "Це ті самі типи, з яких зібрані вбудовані вікна. Усі кольори беруться з HonamiAppearanceSettings, тож інтерфейс, зібраний з них, слідує за акцентом, який користувач обрав у Preferences."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Type", "Тип"), 260),
                (HonamiDocLocalization.Get("Members", "Члени"), 0),
                ("", 0),
                ("HonamiGraphStyles", "Box, ListBox, Row, Spacer, Separator, Foldout, Title, SubTitle, MiniLabel, SmallButton, TallButton, Warning", ""),
                ("HonamiEditorTheme", "Accent, AccentDim, AccentSoft, WindowBg, PanelBg, BoxBg, ToolbarBg, Text, MutedText, Divider, SubtleLine, CanvasBg, GridLine, Scale", ""),
                ("HonamiStyleExtensions", "Border, BorderWidth, BorderColor, Radius, Padding, Margin, Size", ""),
                ("HonamiToolbarControls", "ToolbarButton, IconButton, ToolbarToggle, Separator, AccentStrip, FlexibleSpace, StyleToolbarElement", ""),
                ("HonamiInspectorGUI", HonamiDocLocalization.Get("IMGUI only:", "Лише IMGUI:") + " Title, SubHeader, SectionHeader, FoldoutHeader, Rule, Accent, Highlight, Warning, Debug, Ok", ""),
                ("HonamiEditorLayout", "Header, Breadcrumb, Badge, StyleSidePanel, PaddedInspectorRoot, HeaderButton, EmptyState, PrepareRoot", ""),
                ("HonamiEditorIcons", "Controller, Graph, GraphWhite, BlendTree, BlendTreeWhite, Timeline, TimelineWhite, Profile, GetIcon, IconContent", ""),
                ("HonamiEditorSymbols", "Remove, Enabled, Disabled, Expand, Collapse, DragHandle, Arrow, Bullet", ""),
                ("HonamiNotificationPanel", "ShowGlobal(title, message, HonamiNotificationType, duration)", ""),
                ("HonamiGraphStyleSheets", "Apply(VisualElement), BaseStyleSheet, BaseStyleSheetName", "")
            );

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Editor;
using UnityEngine.UIElements;

var box = HonamiGraphStyles.Box();
box.Border(1, HonamiEditorTheme.Accent).Radius(6).Padding(8, 10);
box.Add(HonamiGraphStyles.SubTitle(""My Tool""));
box.Add(HonamiToolbarControls.ToolbarButton(""Run"", () =>
    HonamiNotificationPanel.ShowGlobal(""Done"", ""Finished."", HonamiNotificationType.Success)));");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "The built-in node editors under Editor/Nodes/BuiltIn are one file per editor and use only the API described in these pages.",
                "Вбудовані редактори нод у Editor/Nodes/BuiltIn - один файл на редактор, і вони використовують лише API, описане в цих сторінках."
            ));
        }
    }
}
