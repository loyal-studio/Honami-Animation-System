using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CustomTimelineModulesPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Custom Timeline Modules", "Власні модулі Timeline");
        public string Category => HonamiDocLocalization.Get("09. Developer API", "09. API для розробників");
        public string SearchKeywords => "timeline module track custom item director state binding runtime evaluate moduleeditor add module scene reference blend crossfade curve easing ease custom split drop preview таймлайн модуль трек власний директор стан біндинг крива плавність";
        public int Order => 711;
        public int EstimatedReadTime => 10;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Everything that runs on a timeline is a module: one row in the Timeline window, holding items at points or ranges of time. The same module class can live in a Honami Timeline asset, played by a Honami Director, and in a state of a Honami Controller, played while that state is the current state of its layer. Camera Shots, Animation, Audio, Screen Fade, Activation, Parameter Curve and the Local, Global and Log events are ordinary modules written against the API on this page, so yours gets the same rows, Add Module entry, inspector and preview.",
                "Усе, що виконується на таймлайні, це модуль: один рядок у вікні Timeline, що тримає item-и в точках або проміжках часу. Той самий клас модуля може жити в ассеті Honami Timeline, який грає Honami Director, і в стані Honami Controller, де він виконується, поки цей стан поточний на своєму шарі. Camera Shots, Animation, Audio, Screen Fade, Activation, Parameter Curve та Local, Global і Log події це звичайні модулі, написані на API з цієї сторінки, тож ваш модуль отримує ті самі рядки, пункт Add Module, інспектор і прев'ю."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The runtime half", "Runtime-частина"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Derive the module from HonamiTimelineModule<TItem>, where TItem is your HonamiTimelineItem. The module stores its items and says what object it needs; CreateRuntime returns the object that is evaluated every frame, once per Director or per animator.",
                "Успадкуйте модуль від HonamiTimelineModule<TItem>, де TItem це ваш HonamiTimelineItem. Модуль зберігає свої item-и й каже, який об'єкт йому потрібен; CreateRuntime повертає об'єкт, що оцінюється щокадру, окремо для кожного директора чи аніматора."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using System;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEngine;

[Serializable]
public sealed class LightColorItem : HonamiTimelineItem
{
    [Min(0f)] public float length = 1f;
    public Color color = Color.white;
    public override float Duration => length;
}

[Serializable]
public sealed class LightColorModule : HonamiTimelineModule<LightColorItem>
{
    public override Type BindingType => typeof(Light);

    public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context) =>
        context.Binding is Light light ? new Evaluator(this, light) : null;

    private sealed class Evaluator : HonamiTimelineModuleRuntime
    {
        private readonly LightColorModule _module;
        private readonly Light _light;

        public Evaluator(LightColorModule module, Light light)
        {
            _module = module;
            _light = light;
        }

        public override void Evaluate(in HonamiTimelineFrame frame)
        {
            var items = _module.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (frame.Time >= items[i].time && frame.Time <= items[i].EndTime)
                    _light.color = items[i].color;
            }
        }
    }
}");

            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Член"), 230),
                (HonamiDocLocalization.Get("Meaning", "Значення"), 0),
                ("", 0),
                ("HonamiTimelineItem.time", HonamiDocLocalization.Get("Start of the item, in seconds. In a state that is seconds on the state's own timeline, the same unit event markers use.", "Початок item-а, у секундах. У стані це секунди на власному таймлайні стану, та сама одиниця, що й у маркерів подій."), ""),
                ("CanResize / SetDuration / TrimStart", HonamiDocLocalization.Get("Return true from CanResize and implement SetDuration to get trim handles on both edges of the item. Override TrimStart when moving the start must also shift something else, as the Animation clip does with its clip offset.", "Поверніть true з CanResize і реалізуйте SetDuration, щоб отримати ручки обрізки на обох краях item-а. Перевизначте TrimStart, коли зсув початку має зсунути ще щось, як кліп Animation зсуває свій offset."), ""),
                ("Duration / IsInstant", HonamiDocLocalization.Get("A ranged item is drawn as a block; an instant item (IsInstant = true) is drawn as a marker, like an event.", "Item з тривалістю малюється блоком; миттєвий item (IsInstant = true) малюється маркером, як подія."), ""),
                ("BindingType", HonamiDocLocalization.Get("The object type the module needs, or null. A GameObject or any Component type.", "Тип об'єкта, потрібного модулю, або null. GameObject чи будь-який тип Component."), ""),
                ("SupportedHosts", HonamiDocLocalization.Get("Timeline, State or All (the default). A module that cannot run somewhere is left out of that Add Module menu; Animation, for example, is Timeline only because a state's motion comes from its node.", "Timeline, State або All (за замовчуванням). Модуль, що не може працювати в якомусь місці, не з'являється в тамтешньому меню Add Module; наприклад, Animation лише для Timeline, бо рух стану задає його нода."), ""),
                ("CreateRuntime(in context)", HonamiDocLocalization.Get("Called when playback builds the module. The context carries Host, Owner (the Director's or animator's GameObject), the resolved Binding, and the Director, or the Animator and State. Return null to skip the module.", "Викликається, коли відтворення будує модуль. Контекст несе Host, Owner (GameObject директора чи аніматора), знайдений Binding, а також Director або Animator і State. Поверніть null, щоб пропустити модуль."), ""),
                ("Evaluate(in frame)", HonamiDocLocalization.Get("Runs every frame for unmuted modules. frame.Crossed(t) is true once when playback passes t in either direction, never during a Seek.", "Виконується щокадру для незаглушених модулів. frame.Crossed(t) стає true один раз, коли відтворення проходить t у будь-якому напрямку, і ніколи під час Seek."), ""),
                ("OnPlay / OnStop", HonamiDocLocalization.Get("A state's modules start when the state becomes the current state of its layer and stop when it stops being current: a transition out begins, the state is skipped, or the animator is disabled. A Director's modules start with Play and stop with Stop or at the end (wrap mode None). Restore whatever you changed in OnStop.", "Модулі стану стартують, коли стан стає поточним на своєму шарі, і зупиняються, коли перестає бути поточним: почався перехід, стан пропущено або аніматор вимкнено. Модулі Director стартують із Play і зупиняються зі Stop або в кінці (wrap mode None). Відновлюйте в OnStop усе, що змінили."), ""),
                ("OnPause / OnResume", HonamiDocLocalization.Get("The Director was paused or resumed while the module is running. Audio parks its sources here.", "Director поставили на паузу чи відновили, поки модуль працює. Audio тут зупиняє свої джерела."), ""),
                ("Dispose", HonamiDocLocalization.Get("The runtime is thrown away: the timeline changed, the Director was destroyed or the state's modules were rebuilt. Destroy objects you created.", "Runtime-об'єкт викидається: таймлайн змінився, Director знищено або модулі стану перебудовано. Знищуйте створені вами об'єкти."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Scene references in items", "Посилання на сцену в item-ах"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A binding gives a whole module one target. When each item needs its own scene object, as every camera shot needs its anchor, give the item a HonamiSceneReference field. The item stores only a key; the object lives in the Director's reference table, or in a state is looked up by that key on the animator's binding providers. The field draws as an ordinary object field in the properties panel, and HonamiSceneReferenceType limits what it accepts.",
                "Біндинг дає всьому модулю одну ціль. Коли кожному item-у потрібен свій об'єкт сцени, як кожному камерному шоту потрібен якір, дайте item-у поле HonamiSceneReference. Item зберігає лише ключ; сам об'єкт живе в таблиці посилань Director, а в стані шукається за цим ключем у binding provider-ах аніматора. На панелі властивостей поле малюється як звичайне поле об'єкта, а HonamiSceneReferenceType обмежує, що воно приймає."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"[Serializable]
public sealed class LookAtItem : HonamiTimelineItem
{
    [HonamiSceneReferenceType(typeof(Transform))]
    public HonamiSceneReference target;
    public float length = 2f;
    public override float Duration => length;
}

// In CreateRuntime keep the context, it resolves references for the right Director or animator:
var target = context.Resolve<Transform>(item.target);");
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "In the editor HonamiTimelineEditContext.Resolve and HonamiTimelinePreviewContext.Resolve do the same, and AssignReference stores an object for an item, creating the key the first time. The Director inspector lists the references and removes the ones no item uses any more.",
                "У редакторі HonamiTimelineEditContext.Resolve та HonamiTimelinePreviewContext.Resolve роблять те саме, а AssignReference зберігає об'єкт для item-а, створюючи ключ першого разу. Інспектор Director показує посилання та прибирає ті, якими вже не користується жоден item."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Blending and crossfades", "Блендинг і crossfade"), HonamiEditorIcons.Timeline);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Implement IHonamiTimelineBlendItem (BlendIn and BlendOut in seconds) on an item to get ease handles in its top corners and shaded ramps in the window. HonamiTimelineBlend.Weight(module, item, time, fadeOutUnderNext) returns the eased 0..1 weight at a time, and treats an overlap with another item of the same module as a crossfade: the later item fades in across the overlap. Pass fadeOutUnderNext = true when your items mix additively, as sound does, so the earlier item also fades out; pass false when the later item is layered on top, as poses and camera shots are. SortByTime orders items for layering without allocating.",
                "Реалізуйте IHonamiTimelineBlendItem (BlendIn і BlendOut у секундах) на item-і, щоб отримати ручки плавності у верхніх кутах і затінені рампи у вікні. HonamiTimelineBlend.Weight(module, item, time, fadeOutUnderNext) повертає згладжену вагу 0..1 у момент часу і сприймає перекриття з іншим item-ом того ж модуля як crossfade: пізніший item з'являється впродовж перекриття. Передайте fadeOutUnderNext = true, коли item-и змішуються додаванням, як звук, щоб ранній item теж згасав; false, коли пізніший item лягає зверху, як пози й камерні шоти. SortByTime впорядковує item-и для нашарування без алокацій."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The shape of a fade is configurable. Implement IHonamiTimelineBlendCurveItem as well and return a HonamiBlendCurve from BlendInCurve and BlendOutCurve. A HonamiBlendCurve holds a HonamiTransitionEase preset, Smooth Step by default plus the Sine, Quad, Cubic, Quart, Quint, Expo, Circ, Back, Elastic and Bounce families, or a custom AnimationCurve when Use Custom Curve is on. The curve is read as fade amount from 0 to 1 in both directions: 0 means nothing has faded and 1 means the fade is complete, so the same shape stays meaningful on the way in and on the way out. Values outside 0..1 are allowed, which lets Back and Elastic add a little overshoot. The Timeline window draws the actual curve in the item's ramps, and animation tracks hand it to the animator crossfade, so preview and runtime match. The built-in camera, animation, audio and shake items implement the interface and expose both curves in the properties panel; items that only implement IHonamiTimelineBlendItem keep the classic smooth step.",
                "Форму фейду можна налаштувати. Додатково реалізуйте IHonamiTimelineBlendCurveItem і повертайте HonamiBlendCurve з BlendInCurve та BlendOutCurve. HonamiBlendCurve містить пресет HonamiTransitionEase, за замовчуванням Smooth Step, а також родини Sine, Quad, Cubic, Quart, Quint, Expo, Circ, Back, Elastic і Bounce, або власну AnimationCurve, коли увімкнено Use Custom Curve. Крива читається як величина фейду від 0 до 1 в обидва боки: 0 означає, що нічого не згасло, 1 що фейд завершено, тож та сама форма лишається зрозумілою і на вході, і на виході. Значення за межами 0..1 дозволені, завдяки чому Back та Elastic дають легкий overshoot. Вікно Timeline малює справжню криву в рампах item-а, а треки Animation передають її в crossfade аніматора, тож прев'ю і runtime збігаються. Вбудовані item-и Camera, Animation, Audio і Shake реалізують цей інтерфейс і показують обидві криві в панелі властивостей; item-и, що реалізують лише IHonamiTimelineBlendItem, зберігають класичний smooth step."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiTransformPose captures a hierarchy and blends back towards the captured pose, which is how Animation eases a clip over whatever pose was there. IHonamiClipSampler lets the same evaluation code sample clips at runtime and through animation mode in the editor preview.",
                "HonamiTransformPose запам'ятовує ієрархію і змішує назад із запам'ятованою позою, саме так Animation плавно накладає кліп на позу, яка там була. IHonamiClipSampler дає тому самому коду оцінки семплювати кліпи в runtime і через animation mode у прев'ю редактора."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Bindings", "Біндинги"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Timelines and states are assets, so neither stores scene references. In a timeline the binding lives on the Honami Director, keyed by the module's Id. In a state the module stores a Binding Key, and each character answers it: add a Honami Timeline Binding Receiver next to the Honami Animator and map the key (for example Weapon) to an object. Characters that share one controller can bind the same key to different objects, and renaming or moving the object in the hierarchy breaks nothing. An empty key means the animator's own GameObject.",
                "Таймлайни та стани це ассети, тож жоден не зберігає посилань на сцену. У таймлайні біндинг живе на Honami Director за Id модуля. У стані модуль зберігає Binding Key, а відповідає на нього кожен персонаж: додайте Honami Timeline Binding Receiver поруч із Honami Animator і зіставте ключ (наприклад, Weapon) з об'єктом. Персонажі зі спільним контролером можуть прив'язати той самий ключ до різних об'єктів, а перейменування чи переміщення об'єкта в ієрархії нічого не ламає. Порожній ключ означає власний GameObject аніматора."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The receiver is one implementation of IHonamiTimelineBindingProvider. Put your own provider component on the animator's GameObject to resolve keys another way, such as the weapon currently in a slot or an object spawned at runtime. When the answer changes during play, call RefreshModuleBindings on the Honami Animator; modules are rebuilt without re-firing events that already fired.",
                "Ресівер це одна з реалізацій IHonamiTimelineBindingProvider. Покладіть власний компонент-провайдер на GameObject аніматора, щоб розв'язувати ключі інакше, наприклад зброєю в поточному слоті чи об'єктом, заспавненим у runtime. Коли відповідь змінюється під час гри, викличте RefreshModuleBindings на Honami Animator; модулі перебудуються без повторного спрацювання вже запущених подій."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"public sealed class WeaponSlotBindings : MonoBehaviour, IHonamiTimelineBindingProvider
{
    [SerializeField] private WeaponInventory inventory;

    public bool TryGetBinding(string key, Type bindingType, out UnityEngine.Object binding)
    {
        binding = key == ""Weapon"" && inventory.Current != null
            ? HonamiTimelineModuleBinding.Convert(inventory.Current.gameObject, bindingType)
            : null;
        return binding != null;
    }

    public void CollectBindingKeys(List<string> keys) => keys.Add(""Weapon"");
}");
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "A module that cannot work without a target should return true from RequiresBinding. When its binding is missing, the Timeline window marks its row with a warning and playback logs one warning naming the module, the state and the missing key. Override GetBindingProblem to add your own checks, as Activation does to refuse the animator's own GameObject.",
                "Модуль, що не може працювати без цілі, має повертати true з RequiresBinding. Коли його біндинг відсутній, вікно Timeline позначає рядок попередженням, а відтворення пише одне попередження з модулем, станом і ключем, якого бракує. Перевизначте GetBindingProblem, щоб додати власні перевірки, як Activation відмовляється від власного GameObject аніматора."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Evaluate is a hot path that runs for every animator using the state. Iterate with for, cache what you need in the runtime's constructor, and keep per-animator state in the runtime object, never on the module: the module belongs to the shared asset.",
                "Evaluate це гарячий шлях, що виконується для кожного аніматора, який використовує стан. Ітеруйте через for, кешуйте потрібне в конструкторі runtime-об'єкта й тримайте стан аніматора в runtime-об'єкті, а не в модулі: модуль належить спільному ассету."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The editor half", "Editor-частина"), HonamiEditorIcons.Timeline);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The runtime class alone is enough: every serializable module type appears under Add Module with a name taken from its class, and its serialized fields are drawn in the properties panel. Add a HonamiTimelineModuleEditor for your own look, menu entries or edit-mode preview.",
                "Самого runtime-класу достатньо: кожен серіалізовний тип модуля з'являється в Add Module з назвою від класу, а його серіалізовані поля малюються в панелі властивостей. Додайте HonamiTimelineModuleEditor для власного вигляду, пунктів меню або прев'ю в Edit Mode."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Editor.Timeline;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

[HonamiTimelineModuleEditor(typeof(LightColorModule), ""Lighting/Light Color"", 50)]
public sealed class LightColorModuleEditor : HonamiTimelineModuleEditor
{
    public override Color ModuleColor => new(0.9f, 0.8f, 0.3f);
    public override Texture Icon => EditorGUIUtility.IconContent(""Light Icon"").image;
    public override Color GetItemColor(HonamiTimelineItem item) => ((LightColorItem)item).color;

    public override void Preview(HonamiTimelinePreviewContext context)
    {
        if (context.Binding is not Light light) return;
        foreach (var item in ((LightColorModule)context.Module).Items)
            if (context.Time >= item.time && context.Time <= item.EndTime)
                light.color = item.color;
    }
}");

            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Override", "Перевизначення"), 250),
                (HonamiDocLocalization.Get("Used for", "Для чого"), 0),
                ("", 0),
                ("DisplayName, ModuleColor, Icon", HonamiDocLocalization.Get("The module's row header and its Add Module entry.", "Заголовок рядка модуля та його пункт в Add Module."), ""),
                ("ItemDisplayName, CreateItem", HonamiDocLocalization.Get("The \"Add ...\" entry of the row's right-click menu and the item it creates.", "Пункт \"Add ...\" у меню правого кліку рядка та item, який він створює."), ""),
                ("GetItemLabel, GetItemColor, GetItemIcon", HonamiDocLocalization.Get("The text, colour and icon of each item. An instant item is drawn as a marker showing its icon (the module's Icon by default, the first letter of ItemDisplayName when there is none); Log picks the console icon of its log type.", "Текст, колір та іконка кожного item-а. Миттєвий item малюється маркером зі своєю іконкою (за замовчуванням Icon модуля, а без неї перша літера ItemDisplayName); Log бере консольну іконку свого типу."), ""),
                ("GetItemLabel(context, item)", HonamiDocLocalization.Get("A label that depends on the scene, such as the name of a shot's anchor. Falls back to GetItemLabel(item).", "Підпис, що залежить від сцени, як-от ім'я якоря шота. За замовчуванням бере GetItemLabel(item)."), ""),
                ("DecorateItem(view)", HonamiDocLocalization.Get("Everything drawn on top of an item and every handle on it. The base implementation adds the trim handles and, for blend items, the ease handles; call it unless you want to replace them.", "Усе, що малюється поверх item-а, і всі ручки на ньому. Базова реалізація додає ручки обрізки, а для blend item-ів ще й ручки плавності; викликайте її, якщо не хочете їх замінити."), ""),
                ("FadesOutUnderOverlap", HonamiDocLocalization.Get("True when overlapping items mix additively, so the ramps drawn match fadeOutUnderNext in your runtime.", "True, коли item-и, що перекриваються, змішуються додаванням, щоб намальовані рампи збігалися з fadeOutUnderNext у вашому runtime."), ""),
                ("CanSplit, SplitItem", HonamiDocLocalization.Get("The Blade tool, S and Split menu entries. The default clones the item, keeps the head and trims the tail with TrimStart; Parameter Curve overrides it to cut its curve in two.", "Інструмент Blade, клавіша S і пункти Split. За замовчуванням клонує item, лишає голову й обрізає хвіст через TrimStart; Parameter Curve перевизначає це, щоб розрізати свою криву надвоє."), ""),
                ("AcceptsDrop, CreateItemFromDrop", HonamiDocLocalization.Get("Assets dragged from the Project onto the row, or onto empty space, which creates a module of the first type that accepts them.", "Ассети, перетягнуті з Project на рядок або на порожнє місце, що створює модуль першого типу, який їх приймає."), ""),
                ("StopPreview", HonamiDocLocalization.Get("Preview turned off or the window closed: stop sounds and destroy helper objects.", "Прев'ю вимкнено або вікно закрито: зупиніть звуки й знищіть допоміжні об'єкти."), ""),
                ("OnItemDoubleClicked(context, item, time)", HonamiDocLocalization.Get("Double-click on an item, with the timeline time under the pointer. Animation opens the clip editor, Parameter Curve adds a key there.", "Подвійний клік по item-у з часом таймлайну під курсором. Animation відкриває редактор кліпа, Parameter Curve додає там ключ."), ""),
                ("AddContextMenuItems", HonamiDocLocalization.Get("Extra right-click entries for your row.", "Додаткові пункти правого кліку для вашого рядка."), ""),
                ("BuildModuleInspector, BuildItemInspector", HonamiDocLocalization.Get("The properties panel. BuildModuleInspector receives a SerializedObject for the whole module; BuildItemInspector receives a SerializedProperty for the selected item. Bound fields get Undo and override tracking for free.", "Панель властивостей. BuildModuleInspector отримує SerializedObject цілого модуля; BuildItemInspector отримує SerializedProperty вибраного item-а. Прив'язані поля автоматично отримують Undo і відстеження override."), ""),
                ("Preview", HonamiDocLocalization.Get("Edit-mode scrubbing in the Timeline window. SampleClip samples an AnimationClip through the same path as the built-in preview. Before changing any other property call DriveProperty, DriveTransform or DriveActive, every sample: animation mode then restores the original value when preview ends. IsPlaying, IsScrubbing and PlaybackSpeed tell playback from scrubbing.", "Скрабінг у вікні Timeline в Edit Mode. SampleClip семплює AnimationClip тим самим шляхом, що й вбудоване прев'ю. Перед зміною будь-якої іншої властивості викликайте DriveProperty, DriveTransform або DriveActive на кожному семплі: тоді animation mode відновить початкове значення, коли прев'ю закінчиться. IsPlaying, IsScrubbing і PlaybackSpeed відрізняють відтворення від скрабу."), "")
            );

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiTimelineEditContext carries the Container (the timeline or the state), the Module, the Director or the preview root, the playhead and the selection. Call RecordUndo before you change data and MarkChanged after, so playback rebuilds its runtimes and the window redraws.",
                "HonamiTimelineEditContext несе Container (таймлайн або стан), Module, Director чи корінь прев'ю, плейхед і виділення. Викликайте RecordUndo до зміни даних і MarkChanged після, щоб відтворення перебудувало runtime-об'єкти, а вікно перемалювалось."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Nested modules", "Вкладені модулі"), HonamiEditorIcons.Timeline);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A module can live inside an item of another module (an event inside a curve, an activation inside an animation clip) or inside a clip of the state's node, to any depth. It stores its parent's id in parentId; the Parent field in the properties panel and Parent in the row's right-click menu change it, and right-clicking an item offers Add Module Inside. The nested module runs only while the playhead is inside its parent's window and the parent itself runs, a muted parent silences everything below it, and removing a module removes the modules nested in it.",
                "Модуль може жити всередині item-а іншого модуля (подія всередині кривої, активація всередині анімаційного кліпа) або всередині кліпа ноди стану, на будь-якій глибині. Він зберігає id батька в parentId; поле Parent на панелі властивостей і Parent у меню правого кліку рядка змінюють його, а правий клік по item-у пропонує Add Module Inside. Вкладений модуль працює лише поки плейхед усередині вікна батька і сам батько працює, заглушений батько глушить усе під собою, а видалення модуля видаляє вкладені в нього модулі."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Your module decides what can be nested in its items by overriding CanHostModule(item, moduleType). The default accepts any module type in any item with a duration and refuses instant items such as event markers. Every time your editor and runtime see is local to the parent: item.time, PlayheadTime, the time passed to CreateItem, AddContextMenuItems and OnItemDoubleClicked, HonamiTimelineFrame at runtime and the preview Time. OnPlay and OnStop follow the parent's window. TimeOffset on the edit and preview contexts is where the parent starts on the window's timeline, in case you draw something yourself. HonamiTimelineModuleParent.Resolve tells you what a module's parentId points at.",
                "Ваш модуль вирішує, що можна вкладати в його item-и, перевизначаючи CanHostModule(item, moduleType). За замовчуванням він приймає будь-який тип модуля в будь-який item із тривалістю і відмовляє миттєвим item-ам, як-от маркерам подій. Весь час, який бачать ваш редактор і runtime, локальний для батька: item.time, PlayheadTime, час у CreateItem, AddContextMenuItems та OnItemDoubleClicked, HonamiTimelineFrame у runtime і Time у прев'ю. OnPlay та OnStop ідуть за вікном батька. TimeOffset у контекстах редагування та прев'ю показує, де батько починається на таймлайні вікна, якщо ви малюєте щось самі. HonamiTimelineModuleParent.Resolve повідомляє, на що вказує parentId модуля."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Only events may be nested in a fade, and only in fades longer than half a second.
public override bool CanHostModule(HonamiTimelineItem item, Type moduleType) =>
    item is FadeItem fade && fade.length > 0.5f
    && (moduleType == typeof(TimelineLocalEventModule) || moduleType == typeof(TimelineGlobalEventModule));");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Handles on items", "Ручки на item-ах"), HonamiEditorIcons.Timeline);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiTimelineItemView is the item as drawn: its Element, the Item, the edit Context and the conversion between time and pixels. AddHandle turns any VisualElement you add to the item into something the user can drag. You receive the pointer position inside the item and the timeline time under it; change your data, and the view records one undo step for the whole drag, relays the item out, repaints it and rebuilds the window on release. The trim handles and the draggable keys of Parameter Curve are written this way.",
                "HonamiTimelineItemView це item у тому вигляді, як його намальовано: його Element, Item, Context редагування та перетворення між часом і пікселями. AddHandle робить будь-який VisualElement, доданий до item-а, тим, що користувач може тягнути. Ви отримуєте позицію курсора всередині item-а і час таймлайну під ним; змінюєте свої дані, а вигляд записує один крок undo на все перетягування, перекладає item, перемальовує його та перебудовує вікно після відпускання. Ручки обрізки і ключі Parameter Curve, які можна тягнути, написані саме так."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"public override void DecorateItem(HonamiTimelineItemView view)
{
    base.DecorateItem(view);

    var item = (FadeItem)view.Item;
    var pin = new VisualElement { style = { position = Position.Absolute, top = 2, width = 6, height = 6, backgroundColor = Color.white } };
    pin.style.left = view.TimeToLocalX(item.time + item.fadeIn) - 3f;
    view.Element.Add(pin);

    view.AddHandle(pin, ""Move Fade In"", drag =>
    {
        float t = view.SnapTime(drag.PointerTime, drag.ShiftHeld);
        item.fadeIn = Mathf.Clamp(t - item.time, 0f, item.length);
        pin.style.left = view.TimeToLocalX(item.time + item.fadeIn) - 3f;
    });
}");

            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Член"), 250),
                (HonamiDocLocalization.Get("Meaning", "Значення"), 0),
                ("", 0),
                ("AddHandle(handle, undoName, onDrag, onEnd)", HonamiDocLocalization.Get("Makes handle draggable with the left button. onDrag gets LocalPosition, StartLocalPosition, Delta, PointerTime and ShiftHeld.", "Робить handle таким, що тягнеться лівою кнопкою. onDrag отримує LocalPosition, StartLocalPosition, Delta, PointerTime і ShiftHeld."), ""),
                ("SnapTime(time, shiftHeld)", HonamiDocLocalization.Get("Snaps to the same points as dragging an item and shows the snap line. Holding Shift inverts the Snap toggle.", "Прилипає до тих самих точок, що й перетягування item-а, і показує лінію прилипання. Shift інвертує перемикач Snap."), ""),
                ("TimeToLocalX, LocalXToTime, PixelsPerSecond", HonamiDocLocalization.Get("Conversion between timeline time and pixels inside the item.", "Перетворення між часом таймлайну і пікселями всередині item-а."), ""),
                ("Modify(undoName, change)", HonamiDocLocalization.Get("A one-off edit, such as deleting a key on right-click: records undo, runs the change and rebuilds the window.", "Одноразова зміна, як-от видалення ключа правим кліком: записує undo, виконує зміну й перебудовує вікно."), ""),
                ("AddTrimHandles, Refresh", HonamiDocLocalization.Get("The built-in trim handles, and a manual relayout and repaint of the item.", "Вбудовані ручки обрізки та ручне перекладання й перемальовування item-а."), ""),
                ("AddBlendHandles, AddCycleMarkers", HonamiDocLocalization.Get("Ease ramps and handles for blend items, and separators where a looping clip starts over.", "Рампи й ручки плавності для blend item-ів і роздільники там, де зациклений кліп починається знову."), ""),
                ("IsLocked", HonamiDocLocalization.Get("The module or one of its parents is locked. Handles added with AddHandle ignore the pointer then.", "Модуль або один із його батьків заблоковано. Ручки, додані через AddHandle, тоді ігнорують курсор."), "")
            );

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Modules are saved with [SerializeReference], which records the class name. Renaming or moving a module or item class orphans it in existing assets; add UnityEngine.Scripting.APIUpdating.MovedFrom with the old name when you do.",
                "Модулі зберігаються через [SerializeReference], який записує ім'я класу. Перейменування чи переміщення класу модуля або item-а робить його сиротою в наявних ассетах; у такому разі додайте UnityEngine.Scripting.APIUpdating.MovedFrom зі старим ім'ям."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Projects made before modules stored state events in a list on the state. They convert to modules automatically on load and are written back the next time the asset is saved.",
                "Проєкти, створені до модулів, зберігали події стану списком на стані. Вони автоматично конвертуються в модулі при завантаженні й записуються назад під час наступного збереження ассета."
            ));
        }
    }
}
