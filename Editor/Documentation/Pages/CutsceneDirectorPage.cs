using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneDirectorPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("The Director", "Director");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "director component play pause resume stop seek skip marker wrap mode hold loop update mode unscaled manual speed tags tag events finished bindings references api директор теги тег відтворення пауза пропуск маркер режим події біндинги посилання";
        public int Order => 566;
        public int EstimatedReadTime => 9;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("Every setting of the Honami Director component", "Кожне налаштування компонента Honami Director"),
                HonamiDocLocalization.Get("Bindings and scene references, and how one timeline serves several scenes", "Біндинги та посилання на сцену, і як один таймлайн служить кільком сценам"),
                HonamiDocLocalization.Get("Controlling playback from code and reacting to it", "Керування відтворенням з коду та реакція на нього"),
                HonamiDocLocalization.Get("What happens to the scene when a cutscene starts, pauses and stops", "Що відбувається зі сценою, коли катсцена стартує, стає на паузу й зупиняється")
            });

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Settings", "Налаштування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                ("Timeline", HonamiDocLocalization.Get("The Honami Timeline asset this Director plays. Changing it at runtime stops the current one.", "Ассет Honami Timeline, який грає цей Director. Зміна в runtime зупиняє поточний.")),
                ("Play Trigger", HonamiDocLocalization.Get("When playback begins automatically: Manual (only via Play()), Awake, Start, or OnEnable. Directors created from the Timeline window have it on Manual, since most cutscenes are started by the game.", "Коли відтворення починається автоматично: Manual (лише через Play()), Awake, Start або OnEnable. У директорів, створених з вікна Timeline, встановлено Manual, бо більшість катсцен запускає гра.")),
                ("Pause At First Frame", HonamiDocLocalization.Get("When automatic playback begins, evaluates only the first frame and pauses.", "При автоматичному старті відтворення оцінює лише перший кадр і стає на паузу.")),
                ("Wrap Mode", HonamiDocLocalization.Get("None: at the end stop and hand everything back. Hold: stay on the last frame, with the camera, poses and fade kept, until Stop. Along the way animation, camera and parameter curve items hold their final frame across gaps until the next item instead of fading out. Loop: start over, firing Looped each time.", "None: у кінці зупинитися й повернути все назад. Hold: лишитися на останньому кадрі з камерою, позами та фейдом, до Stop. По дорозі item-и анімації, камери й кривих параметрів тримають свій останній кадр у проміжках до наступного item-а замість затухання. Loop: почати знову, щоразу запускаючи Looped.")),
                ("Update Mode", HonamiDocLocalization.Get("Game Time follows Time.timeScale, so a paused game pauses the cutscene. Unscaled Game Time ignores it, for cutscenes that play while gameplay is frozen. Manual only moves when your code calls Advance.", "Game Time іде за Time.timeScale, тож пауза гри ставить на паузу й катсцену. Unscaled Game Time його ігнорує, для катсцен, що грають, поки геймплей заморожено. Manual рухається лише коли ваш код викликає Advance.")),
                ("Speed", HonamiDocLocalization.Get("Playback rate. 2 plays twice as fast.", "Швидкість відтворення. 2 грає вдвічі швидше.")),
                ("Tags", HonamiDocLocalization.Get("Free-form labels other systems check while the Director is active, for example Cutscene so the game can't be paused. Matching is exact and case-sensitive.", "Довільні мітки, які інші системи перевіряють, поки Director активний, наприклад Cutscene, щоб гру не можна було поставити на паузу. Порівняння точне, з урахуванням регістру."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Timeline control flow", "Керування потоком таймлайна"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A timeline does not have to run from beginning to end. Add a Wait marker to stop at a beat, then call director.Continue() from input, dialogue, or gameplay. Add Runtime Loop Region for a simple self-contained loop, such as an actor who walks in and then stays in an idle animation. Loop Region can also stay Preview Only or loop until Continue is called.",
                "Таймлайн не зобов'язаний іти від початку до кінця. Додайте маркер Wait, щоб зупинитись на біті, а потім викличте director.Continue() з вводу, діалогу чи геймплею. Додайте Runtime Loop Region для простого самостійного циклу - наприклад, актор заходить, а потім лишається в idle-анімації. Loop Region також може лишитися лише для прев'ю або крутитися до виклику Continue."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// The timeline's Loop Region uses mode Runtime Loop Until Signal
// and signal ""Continue"". This may be called from any input callback.
if (confirmPressed)
    director.Continue(""Continue"");");
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Continue is signal-aware: Continue(\"Dialogue\") releases only a Wait or Loop Region configured with Dialogue. Calling Continue with no argument releases the currently waiting control point regardless of its signal.",
                "Continue враховує сигнал: Continue(\"Dialogue\") відпустить лише Wait або Loop Region з налаштуванням Dialogue. Виклик Continue без аргументу відпускає поточну точку очікування незалежно від її сигналу."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Bindings and scene references", "Біндинги та посилання на сцену"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A binding connects a whole track to one scene object: the camera for Camera Shots, the character for an Animation track, the audio source for an Audio track. A scene reference connects a single item to its own object, like each shot to its anchor. Both are stored on the Director, never in the asset, and both are edited in the Timeline window: bindings in a module's properties, references in an item's properties, or by dropping objects from the Hierarchy. The Director's inspector lists them, and Remove Unused References cleans up objects no item points at any more.",
                "Біндинг з'єднує цілий трек з одним об'єктом сцени: камеру для Camera Shots, персонажа для треку Animation, джерело звуку для треку Audio. Посилання на сцену з'єднує окремий item з його власним об'єктом, як кожен шот із його якорем. Обидва зберігаються на Director, ніколи в ассеті, і обидва редагуються у вікні Timeline: біндинги у властивостях модуля, посилання у властивостях item-а або перетягуванням об'єктів з Hierarchy. Інспектор Director показує їх, а Remove Unused References прибирає об'єкти, на які вже не вказує жоден item."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Because the asset holds only keys, one timeline can be played by several directors with different casts. A duel cutscene can be one asset played by a Director in each arena, each bound to that arena's fighters and camera anchors. A track with nothing bound and a required binding logs one warning naming the track and the Director, and does nothing.",
                "Оскільки ассет тримає лише ключі, один таймлайн можуть грати кілька директорів з різним складом. Катсцена дуелі може бути одним ассетом, який грає Director у кожній арені, і кожен прив'язаний до бійців і якорів камери своєї арени. Трек без потрібного біндингу пише одне попередження з назвою треку й Director і нічого не робить."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Playback from code", "Відтворення з коду"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Член"), 200),
                (HonamiDocLocalization.Get("Meaning", "Значення"), 0),
                ("", 0),
                ("Play()", HonamiDocLocalization.Get("Starts from the beginning, restarting if already playing. Events at time 0 fire.", "Починає з початку, перезапускаючи, якщо вже грає. Події на 0 спрацьовують."), ""),
                ("Play(startTime)", HonamiDocLocalization.Get("Starts from a given time without firing the events before it.", "Починає з заданого моменту без подій, що були до нього."), ""),
                ("Pause() / Resume()", HonamiDocLocalization.Get("Freezes on the current frame with everything held, and continues. Audio pauses too.", "Завмирає на поточному кадрі, тримаючи все, і продовжує. Звук теж стає на паузу."), ""),
                ("Stop()", HonamiDocLocalization.Get("Stops and hands everything back: camera, activation, fades, sound.", "Зупиняє й повертає все назад: камеру, активацію, фейди, звук."), ""),
                ("Seek(time) / SeekToMarker(id)", HonamiDocLocalization.Get("Jumps without firing events. Works while playing or paused. SeekToMarker returns false when there is no marker with that name.", "Перестрибує без подій. Працює під час відтворення чи паузи. SeekToMarker повертає false, якщо маркера з такою назвою немає."), ""),
                ("Skip()", HonamiDocLocalization.Get("Jumps to the end firing every event on the way, then ends as the wrap mode says (Loop ends like None).", "Перестрибує в кінець, запускаючи всі події по дорозі, і закінчується за wrap mode (Loop закінчується як None)."), ""),
                ("Advance(deltaTime)", HonamiDocLocalization.Get("Moves time by hand, for Manual update mode, a replay system or a debug scrubber. A Wait marker still holds it until Continue.", "Рухає час вручну, для режиму Manual, системи повторів чи дебаг-скрабера. Маркер Wait так само тримає його до Continue."), ""),
                ("CurrentTime, Duration", HonamiDocLocalization.Get("Where the playhead is and how long the timeline is, in seconds.", "Де плейхед і скільки триває таймлайн, у секундах."), ""),
                ("IsPlaying, IsPaused, IsActive", HonamiDocLocalization.Get("Advancing; frozen; running at all (playing, paused or holding).", "Рухається; завмер; взагалі працює (грає, на паузі чи тримає кадр)."), ""),
                ("SetBinding, SetReference", HonamiDocLocalization.Get("Rebinds tracks and items from code, for actors spawned at runtime.", "Перепризначає треки й item-и з коду, для акторів, заспавнених у runtime."), ""),
                ("ActiveDirectors", HonamiDocLocalization.Get("Static. Every Director that is running right now, in the order they started.", "Статичне. Усі Director-и, що працюють просто зараз, у порядку старту."), ""),
                ("Tags, HasTag(tag)", HonamiDocLocalization.Get("The Director's tags, and whether it has a given one.", "Теги Director-а і чи є серед них заданий."), ""),
                ("IsAnyActiveWithTag(tag)", HonamiDocLocalization.Get("Static. True while any active Director (playing, paused, waiting or holding) has the tag.", "Статичне. True, поки будь-який активний Director (грає, на паузі, чекає чи тримає кадр) має цей тег."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Events", "Події"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Event", "Подія"), 120),
                (HonamiDocLocalization.Get("Raised when", "Коли"), 0),
                ("", 0),
                ("Started", HonamiDocLocalization.Get("The Director became active: Play, Play(startTime), Skip or Advance while stopped. Raised before the first frame is evaluated, and not again on restart or Resume", "Director став активним: Play, Play(startTime), Skip чи Advance із зупиненого стану. Приходить до оцінки першого кадру, і не повторюється на перезапуску чи Resume"), ""),
                ("Played", HonamiDocLocalization.Get("Play, Play(startTime) and Resume", "Play, Play(startTime) і Resume"), ""),
                ("Paused", HonamiDocLocalization.Get("Pause", "Pause"), ""),
                ("Finished", HonamiDocLocalization.Get("The end was reached or skipped to, in None and Hold", "Кінець досягнуто або перестрибнуто, у None і Hold"), ""),
                ("Stopped", HonamiDocLocalization.Get("Everything was handed back: Stop, or the end in None", "Усе повернуто назад: Stop або кінець у None"), ""),
                ("Looped", HonamiDocLocalization.Get("Playback wrapped around in Loop", "Відтворення пішло на нове коло в Loop"), ""),
                ("AnyStarted, AnyStopped", HonamiDocLocalization.Get("Static. Started and Stopped of every Director, for systems that do not know which Director will play", "Статичні. Started і Stopped будь-якого Director, для систем, що не знають, який Director гратиме"), "")
            );
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Started and Stopped always come in pairs, so they are the ones to take control on and give it back on. For the common cases you do not need code at all: see Director Reactions.",
                "Started і Stopped завжди приходять парою, тож саме на них забирають і повертають керування. Для типових випадків код взагалі не потрібен: див. Director Reactions."
            ));

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Timeline;
using UnityEngine;

public static class DirectorExtensions
{
    // Lets gameplay code write: await director.PlayAndWait();
    public static Awaitable PlayAndWait(this HonamiDirector director)
    {
        var done = new AwaitableCompletionSource();
        void OnEnd(HonamiDirector d)
        {
            director.Finished -= OnEnd;
            director.Stopped -= OnEnd;
            done.TrySetResult();
        }
        director.Finished += OnEnd;
        director.Stopped += OnEnd;
        director.Play();
        return done.Awaitable;
    }
}");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What a Director does to the scene", "Що Director робить зі сценою"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Each track remembers what it is about to change when the cutscene starts and restores it when it stops: the camera pose and field of view, the active state of objects (per Post Playback), the screen fade, and playing sounds. Animation is the exception: sampled poses are simply left behind, and the character's own animator takes over again on the next frame, which is why Blend Out exists.",
                "Кожен трек на старті катсцени запам'ятовує, що саме збирається змінити, і відновлює це на зупинці: позу й поле зору камери, активність об'єктів (за Post Playback), фейд екрана та звуки. Виняток анімація: насемпльовані пози просто лишаються, а власний аніматор персонажа знову бере керування з наступного кадру, для чого й існує Blend Out."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Director evaluates in LateUpdate, after gameplay Update, Unity's Animators and Honami Animators, so its camera and poses are the ones that get rendered. Disabling the Director or its GameObject stops it.",
                "Director оцінюється в LateUpdate, після ігрового Update, Animator-ів Unity та Honami Animator-ів, тож рендериться саме його камера й пози. Вимкнення Director чи його GameObject зупиняє його."
            ));
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Seek and Play(startTime) never fire events. If a cutscene may be entered in the middle, for example after loading a save, put the state it relies on in tracks (activation, animation) rather than only in events at the start.",
                "Seek і Play(startTime) ніколи не запускають події. Якщо в катсцену можна потрапити з середини, наприклад після завантаження сейву, тримайте потрібний їй стан у треках (активація, анімація), а не лише в подіях на початку."
            ), HonamiDocumentationBuilder.CalloutType.Warning);
        }
    }
}
