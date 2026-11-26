using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class InspectorSectionsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Inspector Sections", "Секції інспектора");
        public string Category => HonamiDocLocalization.Get("09. Developer API", "09. API для розробників");
        public string SearchKeywords => "inspector section state transition HonamiInspectorSection context serialized object binding override секція інспектора стан перехід контекст привязка";
        public int Order => 708;
        public int EstimatedReadTime => 7;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A node editor draws the part of the State inspector that belongs to one node type. An inspector section is drawn for every state, or every transition, whatever node it holds. Derive from HonamiStateInspectorSection or HonamiTransitionInspectorSection and mark the class with [HonamiInspectorSection].",
                "Редактор ноди малює ту частину інспектора стану, що належить одному типу ноди. Секція інспектора малюється для кожного стану або кожного переходу, хай яку ноду він містить. Успадкуйтесь від HonamiStateInspectorSection або HonamiTransitionInspectorSection і позначте клас атрибутом [HonamiInspectorSection]."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[HonamiInspectorSection(50)]
public sealed class ReviewNotesSection : HonamiStateInspectorSection
{
    public override string Title => ""Review Notes"";
    public override bool DefaultExpanded => true;

    public override bool IsVisible(HonamiStateSectionContext context)
        => !context.IsInherited;

    public override VisualElement Build(HonamiStateSectionContext context)
    {
        var box = HonamiGraphStyles.Box();

        var note = new TextField(""Note"") { multiline = true };
        note.RegisterValueChangedCallback(evt =>
        {
            ReviewDatabase.SetNote(context.State.guid, evt.newValue);
            EditorUtility.SetDirty(context.Controller);
        });
        note.SetValueWithoutNotify(ReviewDatabase.GetNote(context.State.guid));

        box.Add(note);
        return box;
    }
}");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Placement and order", "Розміщення та порядок"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParameterTable(root,
                (HonamiDocLocalization.Get("State sections", "Секції стану"), "", HonamiDocLocalization.Get("Appended after the Sub-Nodes foldout and before the Node GUID field.", "Додаються після фолдаута Sub-Nodes і перед полем Node GUID.")),
                (HonamiDocLocalization.Get("Transition sections", "Секції переходу"), "", HonamiDocLocalization.Get("Appended after the Parameter Assignments foldout.", "Додаються після фолдаута Parameter Assignments.")),
                (HonamiDocLocalization.Get("Order", "Порядок"), "int", HonamiDocLocalization.Get("The number in the attribute, ascending. Defaults to 100 when the argument is omitted.", "Число з атрибута, за зростанням. За замовчуванням 100, якщо аргумент не вказано.")),
                ("Title", "string", HonamiDocLocalization.Get("When set, your element is wrapped in a HonamiGraphStyles.Foldout with this label. When null, the element is added as returned.", "Якщо задано, ваш елемент загортається у HonamiGraphStyles.Foldout з цим підписом. Якщо null, елемент додається як є.")),
                ("DefaultExpanded", "bool", HonamiDocLocalization.Get("The foldout's open state. It is applied on every rebuild - the foldout is created fresh each time and does not remember the user's last state.", "Стан фолдаута. Застосовується при кожній перебудові - фолдаут створюється заново щоразу і не памʼятає останній стан користувача.")),
                ("IsVisible(context)", "bool", HonamiDocLocalization.Get("Checked before Build. Returning false skips the section entirely.", "Перевіряється перед Build. false повністю пропускає секцію.")),
                ("Build(context)", "VisualElement", HonamiDocLocalization.Get("Returning null draws nothing, including no foldout.", "Повернення null не малює нічого, включно з фолдаутом."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("HonamiStateSectionContext", "HonamiStateSectionContext"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParameterTable(root,
                ("State", "HonamiState", HonamiDocLocalization.Get("The state being inspected. Under an override controller this is the effective state, already resolved.", "Стан, який інспектується. Під override-контролером це ефективний стан, уже розв'язаний.")),
                ("SerializedState", "SerializedObject", HonamiDocLocalization.Get("The SerializedObject wrapping State, the same one the built-in sections bind to.", "SerializedObject, що обгортає State - той самий, до якого прив'язуються вбудовані секції.")),
                ("Controller", "HonamiController", HonamiDocLocalization.Get("The base controller that owns the state.", "Базовий контролер, якому належить стан.")),
                ("RuntimeController", "HonamiRuntimeController", HonamiDocLocalization.Get("What the graph window has open. May be a HonamiOverrideController. Null when the inspector is built without a graph view.", "Те, що відкрито у вікні графа. Може бути HonamiOverrideController. Null, коли інспектор будується без в'ю графа.")),
                ("LayerIndex", "int", HonamiDocLocalization.Get("The layer shown in the graph.", "Шар, показаний у графі.")),
                ("IsInherited", "bool", HonamiDocLocalization.Get("True when the state resolved to an override parent's state.", "True, коли стан розв'язався у стан батька в override."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("HonamiTransitionSectionContext", "HonamiTransitionSectionContext"), HonamiEditorIcons.Graph);
            HonamiDocumentationBuilder.AddParameterTable(root,
                ("Transition", "HonamiTransition", HonamiDocLocalization.Get("The transition being inspected, remapped to the effective state under an override.", "Перехід, який інспектується, перенаправлений на ефективний стан під оверрайдом.")),
                ("Owner", "HonamiState", HonamiDocLocalization.Get("The state the transition leaves from.", "Стан, з якого виходить перехід.")),
                ("TransitionIndex", "int", HonamiDocLocalization.Get("Its index in Owner.transitions, valid against SerializedOwner.", "Його індекс в Owner.transitions, валідний для SerializedOwner.")),
                ("SerializedOwner", "SerializedObject", HonamiDocLocalization.Get("The SerializedObject of Owner. Transitions are not assets, so their properties are reached through the owning state's \"transitions\" array.", "SerializedObject для Owner. Переходи не є ассетами, тож їхні властивості доступні через масив \"transitions\" стану-власника.")),
                ("Controller", "HonamiController", HonamiDocLocalization.Get("The base controller.", "Базовий контролер.")),
                ("RuntimeController", "HonamiRuntimeController", HonamiDocLocalization.Get("What the graph window has open.", "Те, що відкрито у вікні графа.")),
                ("IsInherited", "bool", HonamiDocLocalization.Get("True when the transition resolved to an override parent's state.", "True, коли перехід розв'язався у стан батька в override."))
            );

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using UnityEditor.UIElements;

[HonamiInspectorSection(10)]
public sealed class TransitionCostSection : HonamiTransitionInspectorSection
{
    public override string Title => ""Cost"";

    public override VisualElement Build(HonamiTransitionSectionContext context)
    {
        var transitions = context.SerializedOwner.FindProperty(""transitions"");
        if (transitions == null || context.TransitionIndex >= transitions.arraySize) return null;

        var element = transitions.GetArrayElementAtIndex(context.TransitionIndex);
        var field = new PropertyField(element.FindPropertyRelative(""duration""), ""Duration"");
        field.Bind(context.SerializedOwner);

        var box = HonamiGraphStyles.Box();
        box.Add(field);
        return box;
    }
}");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Binding", "Прив'язка"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Bind to the SerializedObject in the context. Creating a second SerializedObject over the same target gives you an object that does not see the first one's unapplied changes, and the inspector's own change tracking is wired to the one you were given.",
                "Прив'язуйтесь до SerializedObject із контексту. Створення другого SerializedObject над тим самим цільовим об'єктом дає обʼєкт, який не бачить незастосованих змін першого, а відстеження змін самого інспектора підвʼязане до того, який вам дали."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Build runs on every inspector rebuild: a selection change, an undo, a graph reload, a layer switch. Construct elements there and nothing else.",
                "Build виконується при кожній перебудові інспектора: зміна виділення, undo, перезавантаження графа, перемикання шару. Створюйте там елементи і більше нічого."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Your section is constructed once and reused for every state the user selects, so a field on the section holding the inspected object will be stale on the next call. Everything you need is in the context.",
                "Ваша секція створюється один раз і перевикористовується для кожного стану, який обирає користувач, тож поле секції з інспектованим об'єктом на наступному виклику буде застарілим. Усе потрібне є в контексті."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Inherited states", "Успадковані стани"), HonamiEditorIcons.Profile);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "IsInherited is true when the user is looking at a state or transition through a child override controller and it resolved to the parent's. Honami has already picked the object to edit; what editing means for your data is your decision. Either disable your controls so the value visibly comes from the parent, or return false from IsVisible.",
                "IsInherited дорівнює true, коли користувач дивиться на стан або перехід через дочірній override-контролер, і він розв'язався у батьківський. Honami вже обрав об'єкт для редагування; що означає редагування для ваших даних - ваше рішення. Або вимкніть свої контроли, щоб було видно, що значення від батька, або поверніть false з IsVisible."
            ));
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Data stored outside the controller asset - EditorPrefs, a ScriptableObject of your own, a file - is not part of the override system. Key it by State.guid and decide yourself whether a child controller inherits it.",
                "Дані, збережені поза ассетом контролера - EditorPrefs, власний ScriptableObject, файл - не є частиною системи оверрайдів. Ключуйте їх за State.guid і самі вирішуйте, чи успадковує їх дочірній контролер."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "An exception thrown from IsVisible or Build is logged and replaced by an error HelpBox in that section's slot. Check the Console when a section stops appearing.",
                "Виняток з IsVisible або Build логується і замінюється на HelpBox з помилкою у місці цієї секції. Перевіряйте Console, коли секція перестала з'являтись."
            ));
        }
    }
}
