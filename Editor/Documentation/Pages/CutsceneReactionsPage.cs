using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneReactionsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Director Reactions", "Реакції на Director");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "reactions reaction director started stopped accepts filter refreshdirectors runningdirectors any director disable player lock input toggle object behaviour swap replace level events unityevent custom реакції вимкнути гравця ввід заміна рівень події будь-який директор";
        public int Order => 567;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("Making scene objects react to cutscenes without touching the timeline", "Як змусити об'єкти сцени реагувати на катсцени, не чіпаючи таймлайн"),
                HonamiDocLocalization.Get("Listening to one Director or to all of them", "Як слухати один Director або всі одразу"),
                HonamiDocLocalization.Get("The built-in reactions, writing your own and filtering Directors", "Вбудовані реакції, власні реакції та фільтрація Director-ів")
            });

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Why reactions", "Навіщо реакції"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Some things have to happen around every cutscene rather than inside one: the player stops moving, a gameplay camera script lets go, a level prop is swapped for its cutscene version. Putting them on tracks means repeating them in every timeline. A reaction is a component you add to the object instead. It switches on when a Director starts and hands everything back when the Director stops.",
                "Деякі речі мають відбуватися навколо кожної катсцени, а не всередині однієї: гравець перестає рухатися, скрипт ігрової камери відпускає її, проп рівня підміняється своєю версією для катсцени. Якщо класти це на треки, доведеться повторювати в кожному таймлайні. Реакція натомість є компонентом, який ви додаєте на сам об'єкт. Вона вмикається, коли Director стартує, і повертає все назад, коли він зупиняється."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Which Director", "Який Director"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                ("Director", HonamiDocLocalization.Get("The Director to react to. Leave it empty to react to every Director in the loaded scenes, which is what a player living in a persistent scene needs, since it cannot reference Directors in level scenes.", "Director, на який реагувати. Лишіть порожнім, щоб реагувати на всі Director-и в завантажених сценах. Саме це потрібно гравцю з постійної сцени, бо він не може посилатися на Director-и в сценах рівнів."))
            );
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A Director counts as running from Started to Stopped: while it plays, while it is paused and while it holds its last frame in Wrap Mode Hold. With the field empty, a reaction starts with the first Director and stops only when the last one does, so two overlapping cutscenes do not hand control back in the middle.",
                "Director вважається працюючим від Started до Stopped: поки грає, поки на паузі й поки тримає останній кадр у Wrap Mode Hold. З порожнім полем реакція стартує з першим Director і зупиняється лише разом з останнім, тож дві катсцени, що накладаються, не повернуть керування посередині."
            ));
            HonamiDocumentationBuilder.AddWarning(root, HonamiDocLocalization.Get(
                "An empty Director field also counts Directors that are not cutscenes, such as a looping ambient timeline. In a scene that has one, assign the cutscene Director explicitly, or filter with Accepts as shown below.",
                "Порожнє поле Director враховує й Director-и, що не є катсценами, як-от зациклений амбієнтний таймлайн. У сцені з таким призначайте Director катсцени явно або фільтруйте через Accepts, як показано нижче."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Built-in reactions", "Вбудовані реакції"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Component", "Компонент"), 200),
                (HonamiDocLocalization.Get("What it does", "Що робить"), 0),
                ("", 0),
                ("Director Object Toggle", HonamiDocLocalization.Get("Deactivates the objects in Deactivate and activates the ones in Activate. Put the level prop in one list and its cutscene double in the other to swap them.", "Деактивує об'єкти з Deactivate й активує ті, що в Activate. Покладіть проп рівня в один список, а його двійника для катсцени в інший, щоб підмінити їх."), ""),
                ("Director Behaviour Toggle", HonamiDocLocalization.Get("Disables the components in Disable and enables the ones in Enable: input handlers, a follow camera, AI, a light.", "Вимикає компоненти з Disable й вмикає ті, що в Enable: обробники вводу, follow-камеру, AI, світло."), ""),
                ("Director Events", HonamiDocLocalization.Get("Invokes the Started and Stopped UnityEvents with the Director that caused them, for anything the toggles do not cover.", "Викликає UnityEvent-и Started і Stopped з Director-ом, що їх спричинив, для всього, що не покривають toggle-и."), "")
            );
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The toggles remember each object's state when the cutscene starts and put that state back when it stops, so an object that was already off stays off. They are in Add Component under Honami > Director Reactions.",
                "Toggle-и запам'ятовують стан кожного об'єкта на старті катсцени й повертають саме його на зупинці, тож об'єкт, що вже був вимкнений, лишиться вимкненим. Вони в Add Component, розділ Honami > Director Reactions."
            ));
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "A reaction listens from Awake to OnDestroy, so it may switch off its own GameObject and still hear the cutscene end. Untick the component to stop it reacting to new cutscenes. A reaction on an object that is inactive when the scene loads never wakes up; put it on an active object and reference the inactive one from there.",
                "Реакція слухає від Awake до OnDestroy, тож може вимкнути власний GameObject і все одно почути кінець катсцени. Зніміть галочку з компонента, щоб він не реагував на нові катсцени. Реакція на об'єкті, неактивному під час завантаження сцени, ніколи не прокинеться; кладіть її на активний об'єкт і посилайтеся звідти на неактивний."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Writing your own", "Власна реакція"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Derive from HonamiDirectorReaction and implement two methods. The base class handles the Director field, overlapping Directors and Directors that are destroyed mid-cutscene. OnDirectorStopped is called once for every OnDirectorStarted, even if the component was unticked in between. Both receive the Director that caused them: the first one to start and the last one to stop, so with overlapping cutscenes the two can differ. RunningDirectors lists every accepted Director that is running at the moment. If you override Awake, OnEnable, OnValidate or OnDestroy, call the base method.",
                "Успадкуйтеся від HonamiDirectorReaction і реалізуйте два методи. Базовий клас сам обробляє поле Director, Director-и, що накладаються, і Director-и, знищені посеред катсцени. OnDirectorStopped викликається рівно раз на кожен OnDirectorStarted, навіть якщо компонент між ними вимкнули. Обидва отримують Director, що їх спричинив: перший, що стартував, і останній, що зупинився, тож коли катсцени накладаються, це можуть бути різні Director-и. RunningDirectors містить усі прийняті Director-и, що працюють просто зараз. Якщо перевизначаєте Awake, OnEnable, OnValidate чи OnDestroy, викликайте базовий метод."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Timeline;

public sealed class PlayerCutsceneLock : HonamiDirectorReaction
{
    private PlayerController _player;
    private bool _locked;

    protected override void Awake()
    {
        _player = GetComponentInParent<PlayerController>();
        base.Awake();
    }

    protected override void OnDirectorStarted(HonamiDirector director)
    {
        if (_player == null || !_player.InputEnabled) return;
        _player.SetInputEnabled(false);
        _locked = true;
    }

    // Only give control back if this lock took it.
    protected override void OnDirectorStopped(HonamiDirector director)
    {
        if (!_locked) return;
        _locked = false;
        if (_player != null)
            _player.SetInputEnabled(true);
    }
}");
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Choosing which Directors count", "Які Director-и враховувати"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Every reaction decides through Accepts. By default it accepts the Director in the field, or any Director when the field is empty; the built-in reactions use exactly this. Override it to filter by anything: the timeline asset, a component on the Director's GameObject, its scene. Only accepted Directors start and stop the reaction, so a looping ambient timeline can simply be left out. When the data your filter reads changes at runtime, call RefreshDirectors to re-check the Directors that are already running.",
                "Кожна реакція вирішує через Accepts. За замовчуванням вона приймає Director з поля або будь-який, якщо поле порожнє; вбудовані реакції використовують саме це. Перевизначте метод, щоб фільтрувати за чим завгодно: ассетом таймлайну, компонентом на GameObject Director-а, його сценою. Реакцію запускають і зупиняють лише прийняті Director-и, тож зациклений амбієнтний таймлайн можна просто не приймати. Коли дані, які читає ваш фільтр, змінюються в runtime, викличте RefreshDirectors, щоб перевірити Director-и, які вже працюють."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"public sealed class StoryCutsceneEvents : HonamiDirectorReaction
{
    [SerializeField] private HonamiTimeline[] storyTimelines;
    [SerializeField] private UnityEvent<HonamiDirector> started;
    [SerializeField] private UnityEvent<HonamiDirector> stopped;

    protected override bool Accepts(HonamiDirector candidate) =>
        System.Array.IndexOf(storyTimelines, candidate.Timeline) >= 0;

    protected override void OnDirectorStarted(HonamiDirector director) => started.Invoke(director);
    protected override void OnDirectorStopped(HonamiDirector director) => stopped.Invoke(director);
}");

            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Член"), 200),
                (HonamiDocLocalization.Get("Meaning", "Значення"), 0),
                ("", 0),
                ("Director", HonamiDocLocalization.Get("The Director listened to; null means any. Settable from code, for Directors spawned at runtime.", "Director, який слухаємо; null означає будь-який. Можна задати з коду, для Director-ів, заспавнених у runtime."), ""),
                ("ReactsToAnyDirector", HonamiDocLocalization.Get("True when the Director field is empty.", "True, коли поле Director порожнє."), ""),
                ("Accepts(director)", HonamiDocLocalization.Get("Protected virtual. Whether a Director counts for this reaction.", "Protected virtual. Чи враховується Director для цієї реакції."), ""),
                ("RefreshDirectors()", HonamiDocLocalization.Get("Re-checks the running Directors against Accepts, starting or stopping the reaction if the answer changed.", "Заново перевіряє Director-и, що працюють, через Accepts і запускає чи зупиняє реакцію, якщо відповідь змінилась."), ""),
                ("RunningDirectors", HonamiDocLocalization.Get("Accepted Directors running right now, in the order they started.", "Прийняті Director-и, що працюють просто зараз, у порядку старту."), ""),
                ("IsDirectorRunning", HonamiDocLocalization.Get("Whether any accepted Director is running right now.", "Чи працює просто зараз хоч один прийнятий Director."), ""),
                ("IsReacting", HonamiDocLocalization.Get("Between OnDirectorStarted and OnDirectorStopped.", "Між OnDirectorStarted і OnDirectorStopped."), "")
            );
        }
    }
}
