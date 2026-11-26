using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CustomAnimatorsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Writing a Custom Animator", "Власний аніматор");
        public string Category => HonamiDocLocalization.Get("09. Developer API", "09. API для розробників");
        public string SearchKeywords => "custom animator base HonamiAnimatorBase subclass extend playable graph власний аніматор база розширення";
        public int Order => 705;
        public int EstimatedReadTime => 5;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami ships two animators, HonamiAnimator and HonamiClipPlayer, but nothing about the system is limited to them. Both are thin subclasses of HonamiAnimatorBase, and the base is what carries the weight: the update loop, the FPS cap with interpolation, the initial-pose fallback, the global weight blend, pending actions, the rig chain and Linked registration. Anything you build on it is a first-class animator - the Linked brain, the rig system and every tool typed against the base pick it up with no extra work. You supply the graph and the playback rules; the base supplies the rest.",
                "Honami постачається з двома аніматорами, HonamiAnimator і HonamiClipPlayer, але система ними не обмежена. Обидва - тонкі підкласи HonamiAnimatorBase, і саме база несе на собі все головне: цикл оновлення, FPS-кап з інтерполяцією, фолбек початкової пози, глобальну вагу, відкладені дії, ланцюг рігів і реєстрацію в Linked. Усе, що ви побудуєте на ній, стає повноцінним аніматором - Linked-мозок, система рігів і будь-який тулінг, типізований по базі, підхоплять його без жодних додаткових зусиль. Ви надаєте граф і правила відтворення; решту дає база."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What the base already does", "Що база вже робить"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Concern", "Аспект"), 220),
                (HonamiDocLocalization.Get("Handled by the base", "Обробляє база"), 0),
                ("", 0),
                (HonamiDocLocalization.Get("Lifecycle", "Життєвий цикл"), HonamiDocLocalization.Get("Awake builds the graph, OnEnable/Start run the startup sequence per the Startup field, OnDisable tears down and destroys the graph.", "Awake будує граф, OnEnable/Start виконують стартову послідовність згідно з полем Startup, OnDisable прибирає й знищує граф."), ""),
                (HonamiDocLocalization.Get("Update loop", "Цикл оновлення"), HonamiDocLocalization.Get("Update / LateUpdate / FixedUpdate / Manual routing from Update Mode, plus TimeScale and the catch-up tick after a mid-frame rebuild.", "Маршрутизація Update / LateUpdate / FixedUpdate / Manual з Update Mode, плюс TimeScale і догінний тік після перебудови посеред кадру."), ""),
                ("FPS Cap", HonamiDocLocalization.Get("Accumulator, pose snapshots and interpolation between capped frames.", "Акумулятор, знімки пози та інтерполяція між кадрами з капом."), ""),
                (HonamiDocLocalization.Get("Initial pose", "Початкова поза"), HonamiDocLocalization.Get("Capture, restore-when-idle and the Global Weight blend toward rest.", "Захоплення, відновлення в простої та бленд Global Weight до пози спокою."), ""),
                (HonamiDocLocalization.Get("Rigging", "Рігінг"), HonamiDocLocalization.Get("InsertRigChain splices the HonamiRiggingProcessor into your graph; PrepareRigs and ProcessPostEvaluationRigs run around evaluation.", "InsertRigChain вбудовує HonamiRiggingProcessor у ваш граф; PrepareRigs і ProcessPostEvaluationRigs виконуються навколо оцінювання."), ""),
                ("Honami Linked", HonamiDocLocalization.Get("Action registration, delayed reactions and brain discovery - anything typed as HonamiAnimatorBase is found automatically.", "Реєстрація дій, відкладені реакції та пошук мозком - усе, що типізоване як HonamiAnimatorBase, знаходиться автоматично."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What you implement", "Що реалізуєте ви"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Член"), 260),
                (HonamiDocLocalization.Get("Contract", "Контракт"), 0),
                ("", 0),
                ("BuildGraph()", HonamiDocLocalization.Get("Call EnsureGraphCreated(), build your root playable on the graph it returns, root the output on it, then call InsertRigChain() last. Return without creating anything when there is nothing to play.", "Викличте EnsureGraphCreated(), побудуйте свій кореневий playable на отриманому графі, прив'яжіть до нього вихід, і останнім викличте InsertRigChain(). Якщо грати нічого - просто вийдіть, нічого не створюючи."), ""),
                ("EnsureGraphCreated()", HonamiDocLocalization.Get("Provided by the base, not overridden: creates the graph and its animation output once, starts it on manual evaluation and seeds the output weight from Global Weight Mode. Idempotent. Returns an invalid graph when the GameObject has no Animator.", "Дає база, перевизначати не треба: один раз створює граф і його анімаційний вихід, запускає його в ручному режимі оцінювання та задає вагу виходу згідно з Global Weight Mode. Ідемпотентний. Повертає невалідний граф, якщо на об'єкті немає Animator."), ""),
                ("GraphName", HonamiDocLocalization.Get("Optional. The name your graph shows under in the Playable Graph Visualizer.", "Опційно. Назва, під якою ваш граф видно в Playable Graph Visualizer."), ""),
                ("ResetPlaybackState()", HonamiDocLocalization.Get("Zero out playback so the next startup begins clean. Also used on shutdown unless you override ResetPlaybackForShutdown.", "Обнуліть відтворення, щоб наступний старт починався з чистого стану. Також використовується при вимкненні, якщо не перевизначити ResetPlaybackForShutdown."), ""),
                ("PlayStartupContent()", HonamiDocLocalization.Get("Start whatever plays by default - a default state, a default clip, nothing at all.", "Запустіть те, що грає за замовчуванням - стан, кліп або нічого."), ""),
                ("RegisterLinkedActions()", HonamiDocLocalization.Get("Call HonamiLinkedAction.Register for every action id your content can react to.", "Викличте HonamiLinkedAction.Register для кожного action id, на який реагує ваш контент."), ""),
                ("Tick(double)", HonamiDocLocalization.Get("Advance your own state, then call EvaluateGraph(deltaTime) - it runs rigs, evaluates the graph, restores the idle pose and applies the global weight.", "Просуньте власний стан, потім викличте EvaluateGraph(deltaTime) - він проганяє ріги, оцінює граф, відновлює позу простою та застосовує глобальну вагу."), ""),
                ("IsGraphIdle()", HonamiDocLocalization.Get("True when nothing is driving the pose, so the initial-pose fallback may take over.", "True, коли нічого не керує позою, тож фолбек початкової пози може взяти гору."), ""),
                ("Play / StopAll / IsPlaying / ReactToAction", HonamiDocLocalization.Get("The name-based surface the Linked brain and generic tooling call through.", "Іменований інтерфейс, через який викликають Linked-мозок і загальний тулінг."), "")
            );

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Optional hooks: OnBeforeGraphBuild runs in Awake before the first build (initialize anything BuildGraph reads), OnBeforeStartupEvaluate runs after startup content is chosen but before the first evaluation, ResetPlaybackForShutdown lets teardown differ from the startup reset, and OnGraphEvaluated runs once the frame's pose exists (after the idle pose, post-evaluation rigs and global weight), which is where anything that reads the evaluated time or pose belongs. Override SupportsBatchedEvaluation to return true and, in Normal and UnscaledTime, the base lets Unity evaluate your graph in its multithreaded animation pass together with every other animator, the same switch Honami Animator and Clip Player use. Opt in only when Tick evaluates solely through EvaluateGraph, never calls PlayableGraph.Evaluate itself and does nothing after EvaluateGraph that needs the new pose: under batching the pose exists only once OnGraphEvaluated runs.",
                "Опційні хуки: OnBeforeGraphBuild виконується в Awake перед першою побудовою (ініціалізуйте те, що читає BuildGraph), OnBeforeStartupEvaluate - після вибору стартового контенту, але до першого оцінювання, ResetPlaybackForShutdown дозволяє вимкненню відрізнятись від стартового скидання, а OnGraphEvaluated виконується, щойно поза кадру готова (після пози простою, пост-оцінювальних рігів і глобальної ваги). Саме туди належить усе, що читає оцінений час чи позу. Перевизначте SupportsBatchedEvaluation, щоб він повертав true, і в Normal та UnscaledTime база дозволить Unity оцінювати ваш граф у багатопотоковому анімаційному проході разом з усіма іншими аніматорами. Це той самий перемикач, яким користуються Honami Animator і Clip Player. Вмикайте його лише тоді, коли Tick оцінює граф виключно через EvaluateGraph, ніколи сам не викликає PlayableGraph.Evaluate і нічого не робить після EvaluateGraph з новою позою: при пакетному оцінюванні поза з'являється лише тоді, коли виконується OnGraphEvaluated."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("A minimal animator", "Мінімальний аніматор"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[AddComponentMenu(""Game/Single Clip Animator"")]
public sealed class SingleClipAnimator : HonamiAnimatorBase
{
    [SerializeField] private AnimationClip clip;
    [SerializeField] private HonamiActionID playAction;

    private AnimationClipPlayable _playable;
    private bool _playing;

    protected override string GraphName => gameObject.name + ""_SingleClip"";

    protected override void BuildGraph()
    {
        if (clip == null) return;

        // The base creates the graph and its animation output, and applies Global Weight Mode.
        PlayableGraph graph = EnsureGraphCreated();
        if (!graph.IsValid()) return;

        _playable = AnimationClipPlayable.Create(graph, clip);
        _playable.SetSpeed(0.0);
        _playableOutput.SetSourcePlayable(_playable);

        // Splices any HonamiRiggingProcessor on this GameObject into the graph.
        // Always last: it reads whatever the output is currently rooted on.
        InsertRigChain();
    }

    protected override void ResetPlaybackState()
    {
        _playing = false;
        if (_playable.IsValid()) _playable.SetTime(0.0);
    }

    protected override void PlayStartupContent() => _playing = true;

    protected override void RegisterLinkedActions() => HonamiLinkedAction.Register(this, playAction);

    protected override bool IsGraphIdle() => !_playing;

    public override void Tick(double deltaTime)
    {
        if (IsPaused || !HasPlayableGraph) return;

        _cachedDeltaTime = deltaTime * TimeScale;

        if (_playing && _playable.IsValid())
            _playable.SetTime(_playable.GetTime() + _cachedDeltaTime);

        UpdatePendingActions((float)deltaTime);

        // Rigs, evaluation, idle-pose fallback and global weight in one call.
        EvaluateGraph((float)_cachedDeltaTime);
    }

    public override void Play(string name, float transitionDuration) => _playing = true;

    public override void StopAll() => ResetPlaybackState();

    public override bool IsPlaying(string name) => _playing;

    public override void ReactToAction(HonamiActionID actionId, float transitionDuration)
    {
        if (actionId == playAction) _playing = true;
    }
}");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Everything else comes with the base class, without a line of code on your side: drop the component under a Linked Animator Brain and it appears in LinkedAnimators, reacts to SetActionID, obeys PauseAll/ResumeAll and GlobalWeight, and honours the Update Mode, Time Scale and FPS Cap fields inherited from the base inspector.",
                "Усе інше дає базовий клас, без жодного рядка коду з вашого боку: покладіть компонент під Linked Animator Brain - і він з'явиться в LinkedAnimators, реагуватиме на SetActionID, підкорятиметься PauseAll/ResumeAll і GlobalWeight та шануватиме поля Update Mode, Time Scale і FPS Cap, успадковані з базового інспектора."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Manual driving: set Update Mode to Manual and the base stops ticking you. Call Tick(deltaTime) yourself from a job scheduler, a network step or a deterministic simulation loop.",
                "Ручне керування: встановіть Update Mode у Manual - і база перестане вас тікати. Викликайте Tick(deltaTime) самостійно з планувальника задач, мережевого кроку чи детермінованого циклу симуляції."
            ), HonamiDocumentationBuilder.CalloutType.Info);
        }
    }
}
