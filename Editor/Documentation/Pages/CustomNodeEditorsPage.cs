using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CustomNodeEditorsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Custom Node Editors", "Власні редактори нод");
        public string Category => HonamiDocLocalization.Get("09. Developer API", "09. API для розробників");
        public string SearchKeywords => "custom node editor HonamiNodeEditorBase HonamiNodeEditor attribute uss class stylesheet timeline track inspector double click власний редактор ноди трек таймлайну атрибут клас стилі";
        public int Order => 707;
        public int EstimatedReadTime => 9;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A node editor is a class deriving from HonamiNodeEditorBase and marked with [HonamiNodeEditor]. The attribute names the HonamiNodeBase type it drives. HonamiNodeEditorRegistry finds it through TypeCache, constructs one instance and reuses it for every node of that type.",
                "Редактор ноди - це клас, успадкований від HonamiNodeEditorBase і позначений атрибутом [HonamiNodeEditor]. Атрибут називає тип HonamiNodeBase, яким редактор керує. HonamiNodeEditorRegistry знаходить його через TypeCache, створює один екземпляр і перевикористовує для кожної ноди цього типу."
            ));

            HonamiDocumentationBuilder.AddParameterTable(root,
                ("nodeType", "Type", HonamiDocLocalization.Get("The HonamiNodeBase subclass this editor draws. One editor per type; the last one found wins if two claim the same type.", "Підклас HonamiNodeBase, який малює цей редактор. Один редактор на тип; якщо два претендують на один тип, виграє знайдений останнім.")),
                ("menuPath", "string", HonamiDocLocalization.Get("The path in the node creation search window, for example \"Custom/Footstep\". The last segment is also the node subtitle unless GetSubtitle is overridden.", "Шлях у вікні пошуку створення ноди, наприклад \"Custom/Footstep\". Останній сегмент також стає підзаголовком ноди, якщо не перевизначено GetSubtitle.")),
                ("menuOrder", "int", HonamiDocLocalization.Get("Sort order in that menu, ascending. Defaults to 100. The built-in editors use 10 to 90.", "Порядок сортування в тому меню, за зростанням. За замовчуванням 100. Вбудовані редактори використовують від 10 до 90."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Required members", "Обов'язкові члени"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Five members are abstract. Four of them are USS class names, described in the next section; the fifth is the hover text.",
                "П'ять членів є абстрактними. Чотири з них - імена USS-класів, описані в наступному розділі; п'ятий - текст підказки при наведенні."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Editor;
using HonamiAnimationSystem.Runtime.Core;
using UnityEditor;
using UnityEngine.UIElements;

[HonamiNodeEditor(typeof(FootstepNode), ""Custom/Footstep"", 500)]
public sealed class FootstepNodeEditor : HonamiNodeEditorBase
{
    public override string NodeCssClass => ""honami-node-event"";
    public override string TopBarCssClass => ""honami-node-top-event"";
    public override string AvatarCssClass => ""honami-node-avatar-event"";
    public override string IconCssClass => ""honami-node-icon-event"";
    public override string Tooltip => ""Emits a footstep every N seconds."";

    public override VisualElement BuildInspectorUI(
        HonamiState state, SerializedObject nodeSO, HonamiController controller)
    {
        var box = HonamiGraphStyles.Box();
        box.Add(HonamiGraphStyles.SubTitle(""Footstep""));
        AddNodeField(box, nodeSO, ""interval"", ""Interval"");
        AddParameterPopup(box, nodeSO, controller, ""speedParameter"", ""Speed"");
        return box;
    }
}");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("USS classes", "USS-класи"), HonamiEditorIcons.Graph);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A node in the graph is four elements. HonamiNode puts a fixed structural class on each of them, then adds the class your editor returns for that element. The structural class carries the layout; yours carries the colour.",
                "Нода в графі - це чотири елементи. HonamiNode ставить на кожен фіксований структурний клас, а потім додає той клас, який ваш редактор повернув для цього елемента. Структурний клас несе розкладку; ваш - колір."
            ));

            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Член"), 190),
                (HonamiDocLocalization.Get("Element", "Елемент"), 220),
                (HonamiDocLocalization.Get("Always also has", "Завжди також має"), 0),
                ("NodeCssClass", HonamiDocLocalization.Get("The node itself", "Сама нода"), "-"),
                ("TopBarCssClass", HonamiDocLocalization.Get("The coloured strip along the top", "Кольорова смуга вгорі"), "honami-node-top"),
                ("AvatarCssClass", HonamiDocLocalization.Get("The shape left of the title", "Фігура ліворуч від заголовка"), "honami-node-avatar"),
                ("IconCssClass", HonamiDocLocalization.Get("The glyph inside that shape", "Гліф усередині тієї фігури"), "honami-node-icon")
            );

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiNode adds three more classes to the node on its own: honami-node-default when the state is the layer's default state, honami-node-overridden when an override controller has changed it, and honami-node-inherited when it is a virtual inherited state. You do not return these and you cannot suppress them.",
                "HonamiNode додає до ноди ще три класи самостійно: honami-node-default, коли стан є дефолтним для шару, honami-node-overridden, коли override-контролер його змінив, і honami-node-inherited, коли це віртуальний успадкований стан. Ви їх не повертаєте і не можете придушити."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The four members are independent - nothing checks that they agree. The built-in editors follow one convention: the same suffix on all four, so HonamiEventNodeEditor returns honami-node-event, honami-node-top-event, honami-node-avatar-event and honami-node-icon-event. Following it is not required, but mixing suffixes gives you a node with, say, a green strip and a pink icon.",
                "Ці чотири члени незалежні - ніщо не перевіряє, що вони узгоджені. Вбудовані редактори тримаються однієї угоди: однаковий суфікс на всіх чотирьох, тож HonamiEventNodeEditor повертає honami-node-event, honami-node-top-event, honami-node-avatar-event і honami-node-icon-event. Дотримуватись не обов'язково, але змішування суфіксів дасть ноду, скажімо, із зеленою смугою і рожевою іконкою."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Suffixes defined in the package", "Суфікси, визначені в пакеті"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiGraphStyle.uss defines these suffixes. Return them to get the same look as the built-in node they belong to.",
                "HonamiGraphStyle.uss визначає такі суфікси. Повертайте їх, щоб отримати той самий вигляд, що й у вбудованої ноди, якій вони належать."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Suffix", "Суфікс"), 170),
                (HonamiDocLocalization.Get("Strip colour", "Колір смуги"), 130),
                (HonamiDocLocalization.Get("Used by", "Використовує"), 0),
                ("animation", "#8dbb5b", "HonamiAnimationNodeEditor"),
                ("blend", "#888888", "HonamiBlendTreeNodeEditor"),
                ("random", "#ff4785", "HonamiRandomAnimationNodeEditor"),
                ("sequencer", "#6366f1", "HonamiSequencerNodeEditor"),
                ("event", "#ffc53d", "HonamiEventNodeEditor"),
                ("any", "#2bc4b6", "HonamiAnyStateNodeEditor"),
                ("repeater", "#a14df5", "HonamiRepeaterNodeEditor"),
                ("exit", "#f14c4c", "HonamiExitNodeEditor"),
                ("portal-entrance", "#3b82f6", "HonamiPortalEntranceNodeEditor"),
                ("portal-exit", "#ec4899", "HonamiPortalExitNodeEditor"),
                ("default", "#e57519", HonamiDocLocalization.Get("Added by HonamiNode, not by an editor.", "Додає HonamiNode, не редактор."))
            );

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Returning a suffix the package does not define is legal. The node renders with the structural rules only: a transparent top strip, and an avatar and icon with no accent colour.",
                "Повернути суфікс, якого пакет не визначає, дозволено. Нода відмалюється лише за структурними правилами: прозора верхня смуга, а також аватар та іконка без акцентного кольору."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Your own USS", "Власний USS"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Override StyleSheet to define your own suffix. HonamiGraphStyleSheets.Apply loads HonamiGraphStyle.uss first, then the StyleSheet of every registered node editor, skipping nulls and duplicates. Cache the asset in a static field: the property is read every time a graph view is built, and several of your editors can return the same file.",
                "Перевизначте StyleSheet, щоб визначити власний суфікс. HonamiGraphStyleSheets.Apply завантажує спершу HonamiGraphStyle.uss, потім StyleSheet кожного зареєстрованого редактора ноди, пропускаючи null і дублікати. Кешуйте ассет у статичному полі: властивість читається щоразу при побудові в'ю графа, і кілька ваших редакторів можуть повертати той самий файл."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"private static StyleSheet _sheet;

public override StyleSheet StyleSheet
    => _sheet ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(
        ""Assets/MyStudio/Editor/HonamiFootstepNodes.uss"");

public override string NodeCssClass => ""honami-node-footstep"";
public override string TopBarCssClass => ""honami-node-top-footstep"";
public override string AvatarCssClass => ""honami-node-avatar-footstep"";
public override string IconCssClass => ""honami-node-icon-footstep"";");

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Declare only the properties you want to change. The structural rules already set size, layout and transitions.",
                "Оголошуйте лише ті властивості, які хочете змінити. Структурні правила вже задають розмір, розкладку та переходи."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@".honami-node-top-footstep {
    background-color: #d97706;
}

.honami-node-avatar-footstep {
    border-color: rgba(217, 119, 6, 0.7);
    border-radius: 16px 4px 16px 4px;
}

.honami-node-icon-footstep {
    background-color: #d97706;
    border-radius: 2px;
    width: 10px;
    height: 10px;
    rotate: 45deg;
}

.honami-node-footstep #node-border {
    min-width: 140px;
}");

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The last rule targets the node frame, which is a child element named node-border rather than the node itself. HonamiGraphStyle.uss uses the same selector to give the portal nodes a narrower minimum width.",
                "Останнє правило цілиться в рамку ноди, яка є дочірнім елементом з іменем node-border, а не самою нодою. HonamiGraphStyle.uss використовує той самий селектор, щоб дати портальним нодам меншу мінімальну ширину."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Your stylesheet is added after the package one, so at equal specificity your rules win. Declaring .honami-node-top-animation in your file recolours every Animation node in the project, including Honami's own.",
                "Ваш стилевий файл додається після пакетного, тож за рівної специфічності перемагають ваші правила. Оголошення .honami-node-top-animation у вашому файлі перефарбує кожну ноду Animation у проєкті, включно з нодами самої Honami."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Optional members", "Необов'язкові члени"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParameterTable(root,
                ("HasInput / HasOutput", "bool", HonamiDocLocalization.Get("Whether the node shows an incoming and an outgoing port. Both true.", "Чи показує нода вхідний і вихідний порт. Обидва true.")),
                ("StyleSheet", "StyleSheet", HonamiDocLocalization.Get("A stylesheet loaded into the graph alongside the package one. Null.", "Стилевий файл, що завантажується в граф поряд із пакетним. Null.")),
                ("GetTitle(state)", "string", HonamiDocLocalization.Get("The large text on the node. Returns state.stateName.", "Великий текст на ноді. Повертає state.stateName.")),
                ("GetSubtitle(state)", "string", HonamiDocLocalization.Get("The small text under it. Returns the last segment of the attribute's menuPath.", "Малий текст під ним. Повертає останній сегмент menuPath з атрибута.")),
                ("BuildInspectorUI(state, nodeSO, controller)", "VisualElement", HonamiDocLocalization.Get("The node section of the State inspector. Returns null, which draws nothing.", "Секція ноди в інспекторі стану. Повертає null, тобто нічого не малює.")),
                ("AppendActionButtons(root, state, controller)", "void", HonamiDocLocalization.Get("Adds elements below the inspector section, outside its box. Does nothing.", "Додає елементи під секцією інспектора, поза її боксом. Нічого не робить.")),
                ("OnDoubleClick(state, controller)", "void", HonamiDocLocalization.Get("Runs on double-click in the graph. Does nothing.", "Виконується при подвійному кліку в графі. Нічого не робить.")),
                ("GetTimelineDuration(state)", "float", HonamiDocLocalization.Get("The length the Timeline window uses for this node. Returns 5.", "Довжина, яку вікно Timeline використовує для цієї ноди. Повертає 5.")),
                ("CollectTimelineRows(state, rows)", "void", HonamiDocLocalization.Get("Lists the node's rows in the Timeline window. Adds one row called Motion when DrawTimelineRow is overridden.", "Перелічує рядки ноди у вікні Timeline. Додає один рядок Motion, якщо DrawTimelineRow перевизначено.")),
                ("DrawTimelineRow(state, drawer, row, y)", "void", HonamiDocLocalization.Get("Draws one of those rows at y. Does nothing.", "Малює один із цих рядків на висоті y. Нічого не робить.")),
                ("IsMotionPreviewed(state, motionIndex)", "bool", HonamiDocLocalization.Get("Whether modules bound to that motion take part in the preview. Returns true; the random node returns true only for the clip picked in the toolbar.", "Чи беруть участь у прев'ю модулі, прив'язані до цього руху. Повертає true; random-нода повертає true лише для кліпа, вибраного в тулбарі.")),
                ("SampleTimelinePreview(state, rootTarget, internalT)", "void", HonamiDocLocalization.Get("Poses the preview character at normalized time internalT. Does nothing.", "Ставить позу превʼю-персонажа в нормалізованому часі internalT. Нічого не робить.")),
                ("SupportsTimelinePreview", "bool", HonamiDocLocalization.Get("Whether states using this node can be selected in the Timeline window at all. False, so override it to true alongside SampleTimelinePreview or the node will never be offered for preview.", "Чи можна взагалі обрати стани з цією нодою у вікні Timeline. False, тож перевизначайте на true разом із SampleTimelinePreview, інакше ноду ніколи не запропонують для превʼю.")),
                ("BuildTimelineControls(state, container, rebuild)", "void", HonamiDocLocalization.Get("Adds scrubbing controls for this node to the Timeline toolbar - sliders for a blend space's axes, for example. Per-node preview values can live in state.NodePreviewAxes. Call rebuild() after changing them. Does nothing.", "Додає контроли скрабу для цієї ноди в тулбар Timeline - наприклад, слайдери осей blend space. Превʼю-значення ноди можуть жити в state.NodePreviewAxes. Викликайте rebuild() після їх зміни. Нічого не робить."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Inspector helpers", "Хелпери інспектора"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "BuildInspectorUI receives a SerializedObject wrapping the node. Bind through it rather than writing to the node directly, or Undo and the override system will not see the change. These protected helpers on the base class do that binding.",
                "BuildInspectorUI отримує SerializedObject, що обгортає ноду. Прив'язуйтесь через нього, а не пишіть у ноду напряму, інакше Undo і система оверрайдів не побачать зміни. Ці protected-хелпери базового класу роблять прив'язку за вас."
            ));
            HonamiDocumentationBuilder.AddParameterTable(root,
                ("AddNodeField", "void", HonamiDocLocalization.Get("A bound PropertyField for one property of the node.", "Прив'язаний PropertyField для однієї властивості ноди.")),
                ("AddNodeListField", "void", HonamiDocLocalization.Get("The same for a list property, with a callback invoked on each newly added element.", "Те саме для властивості-списку, з колбеком на кожен новододаний елемент.")),
                ("AddParameterPopup", "void", HonamiDocLocalization.Get("A dropdown of controller parameters filtered by HonamiParameterType, writing the chosen name into a string property. Adds a \"- None -\" entry and keeps an existing value that is not in the list.", "Дропдаун параметрів контролера з фільтром за HonamiParameterType, що пише обране ім'я в рядкову властивість. Додає пункт \"- None -\" і зберігає наявне значення, якого немає в списку.")),
                ("AddStateField", "void", HonamiDocLocalization.Get("A bound field for a property of the owning HonamiState rather than the node.", "Прив'язане поле для властивості HonamiState-власника, а не ноди.")),
                ("SmallLabel", "Label", HonamiDocLocalization.Get("The small annotation label used by the built-in inspectors.", "Маленький підпис-анотація, який використовують вбудовані інспектори.")),
                ("TimelineButton", "Button", HonamiDocLocalization.Get("Opens the Timeline window on this state.", "Відкриває вікно Timeline на цьому стані.")),
                ("BlendTreeButton", "Button", HonamiDocLocalization.Get("Opens the Blend Tree window on this state.", "Відкриває вікно Blend Tree на цьому стані."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Timeline tracks", "Треки таймлайну"), HonamiEditorIcons.Timeline);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The motion rows at the top of a state's timeline belong to its node editor. CollectTimelineRows adds one HonamiTimelineRow (title, icon, colour and an optional motion index) per row, and the window uses that list for the row headers. An editor that overrides DrawTimelineRow without overriding CollectTimelineRows gets a single row called Motion.",
                "Рядки руху вгорі таймлайну стану належать його node editor-у. CollectTimelineRows додає один HonamiTimelineRow (назва, іконка, колір і необов'язковий індекс руху) на кожен рядок, і вікно бере цей список для заголовків рядків. Редактор, що перевизначає DrawTimelineRow без CollectTimelineRows, отримує один рядок з назвою Motion."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "DrawTimelineRow is called once per row with the row's index in that list and its y. Draw only at the y you receive: modules bound to a clip are listed right under that clip's row, so your rows are not necessarily adjacent.",
                "DrawTimelineRow викликається раз на рядок з індексом рядка в цьому списку та його y. Малюйте лише на отриманому y: модулі, прив'язані до кліпа, стоять одразу під рядком цього кліпа, тож ваші рядки не обов'язково йдуть поспіль."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"public override float GetTimelineDuration(TimelineState state)
{
    var node = state.SelectedState?.node as FootstepNode;
    return node != null ? node.interval * node.count : 5f;
}

public override void CollectTimelineRows(TimelineState state, List<HonamiTimelineRow> rows) =>
    rows.Add(new HonamiTimelineRow(""Steps"", HonamiTimelineRow.ClipIcon, TimelineTheme.EventTrack));

public override void DrawTimelineRow(
    TimelineState state, ITimelineTrackDrawer drawer, int row, float y)
{
    var node = state.SelectedState?.node as FootstepNode;
    if (node == null) return;

    for (int i = 0; i < node.count; i++)
    {
        drawer.AddReadonlyClip(
            null, $""step {i}"", i * node.interval, 0.1f, y, TimelineTheme.EventTrack);
    }
}");

            HonamiDocumentationBuilder.AddParameterTable(root,
                ("AddReadonlyClip", "void", HonamiDocLocalization.Get("A block the user cannot move. Passing a non-null AnimationClip makes double-clicking it open the clip editor.", "Блок, який користувач не може рухати. Ненульовий AnimationClip робить подвійний клік по ньому відкриттям редактора кліпу.")),
                ("AddClipElement", "void", HonamiDocLocalization.Get("A selectable block tied to an object of yours, passed back through the selection.", "Виділюваний блок, привʼязаний до вашого об'єкта, який повертається через виділення.")),
                ("AddClipWithHandles", "void", HonamiDocLocalization.Get("A block with trim handles and onTrimStart, onTrimEnd, onDoubleClick and onDragEnd callbacks.", "Блок із ручками обрізки та колбеками onTrimStart, onTrimEnd, onDoubleClick і onDragEnd."))
            );

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "TimelineState is the window's state object. Drawers usually read SelectedState, Controller, SelectedLayerIndex, PlayheadTime, BlendPreviewValue and RandomPreviewIdx. TimelineTheme carries RowHeight, ClipHeight and the track colours AnimationTrack, EventTrack, GlobalEventTrack, RandomTrack, SequencerTrack and PreviewTrack.",
                "TimelineState - обʼєкт стану вікна. Малювальники зазвичай читають SelectedState, Controller, SelectedLayerIndex, PlayheadTime, BlendPreviewValue і RandomPreviewIdx. TimelineTheme несе RowHeight, ClipHeight і кольори треків AnimationTrack, EventTrack, GlobalEventTrack, RandomTrack, SequencerTrack і PreviewTrack."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Modules nested in a clip", "Модулі, вкладені в кліп"), HonamiEditorIcons.Timeline);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A state module can run for the whole state or inside one of the node's clips: an event that belongs to one clip of a random node, an activation that follows one clip of a sequencer. The node describes its clips (motions) on HonamiNodeBase, and the module stores the motion's id in parentId, the same field used for nesting under a module's item (see Custom Timeline Modules). Nodes without motions, like the blend tree, only take whole-state modules.",
                "Модуль стану може працювати на весь стан або всередині одного з кліпів ноди: подія, що належить одному кліпу random-ноди, активація, що йде за одним кліпом sequencer-а. Нода описує свої кліпи (рухи) в HonamiNodeBase, а модуль зберігає id руху в parentId, тому самому полі, що й для вкладення під item модуля (див. Custom Timeline Modules). Ноди без рухів, як-от blend tree, приймають лише модулі на весь стан."
            ));
            HonamiDocumentationBuilder.AddParameterTable(root,
                ("MotionCount, GetMotionName(i)", "int, string", HonamiDocLocalization.Get("How many clips can host modules and what the window calls them. 0 and null.", "Скільки кліпів можуть мати модулі і як їх називає вікно. 0 і null.")),
                ("GetMotionId(i)", "string", HonamiDocLocalization.Get("A stable id. Modules keep it, so it must survive reordering and be unique within the node. Store it next to the clip entry.", "Стабільний id. Модулі зберігають його, тож він має переживати перестановку і бути унікальним у межах ноди. Зберігайте його поруч із записом кліпа.")),
                ("EnsureMotionIds()", "bool", HonamiDocLocalization.Get("Gives missing or duplicated ids a new one and returns true if anything changed. The editor calls it before binding a module and when a state validates. HonamiNodeBase.EnsureUniqueMotionIds does it for a list.", "Дає новий id відсутнім або дубльованим і повертає true, якщо щось змінилось. Редактор викликає його перед прив'язкою модуля та під час валідації стану. HonamiNodeBase.EnsureUniqueMotionIds робить це для списку.")),
                ("GetMotionStart(i), GetMotionDuration(i)", "float", HonamiDocLocalization.Get("The clip's window on the state's timeline. Nested items are timed from the start, and nested modules run only inside the window: the sequencer returns the clip's start and length, so its events move with the clip. 0 and infinity.", "Вікно кліпа на таймлайні стану. Вкладені item-и рахуються від старту, а вкладені модулі працюють лише всередині вікна: sequencer повертає старт і довжину кліпа, тож його події рухаються разом із кліпом. 0 і нескінченність.")),
                ("IsMotionPlaying(i, runtime)", "bool", HonamiDocLocalization.Get("Whether the clip is part of the current playthrough. The random node answers from the clip it picked. Nested modules get OnPlay when their clip starts playing and OnStop when it stops. True.", "Чи входить кліп у поточне відтворення. Random-нода відповідає за вибраним кліпом. Вкладені модулі отримують OnPlay, коли їхній кліп починає грати, і OnStop, коли перестає. True.")),
                ("CanHostModule(i, moduleType)", "bool", HonamiDocLocalization.Get("Whether a module of that type may be nested in the clip. The Timeline window offers only the types you accept. True.", "Чи можна вкласти в кліп модуль цього типу. Вікно Timeline пропонує лише ті типи, які ви приймаєте. True."))
            );
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "In the editor, pass the motion index as the last argument of HonamiTimelineRow. That row gets a + button and a right-click menu for nesting modules in the clip, and they are drawn under it. Override IsMotionPreviewed when only some clips play in the preview.",
                "У редакторі передайте індекс руху останнім аргументом HonamiTimelineRow. Такий рядок отримує кнопку + і меню правого кліку для вкладення модулів у кліп, і вони малюються під ним. Перевизначте IsMotionPreviewed, якщо в прев'ю грає лише частина кліпів."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"public override int MotionCount => steps.Count;
public override string GetMotionId(int index) => steps[index].id;
public override string GetMotionName(int index) => steps[index].clip != null ? steps[index].clip.name : $""Step {index}"";
public override float GetMotionStart(int index) => steps[index].startTime;
public override float GetMotionDuration(int index) => steps[index].length;
public override bool EnsureMotionIds() =>
    EnsureUniqueMotionIds(steps, step => step.id, (step, id) => step.id = id);

// Editor side
rows.Add(new HonamiTimelineRow(name, HonamiTimelineRow.ClipIcon, TimelineTheme.SequencerTrack, index));");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "SampleTimelinePreview runs on every scrub frame. Resolve clips and bindings outside it where you can.",
                "SampleTimelinePreview виконується на кожному кадрі скрабу. Розв'язуйте кліпи та привʼязки поза ним, де можливо."
            ));
        }
    }
}
