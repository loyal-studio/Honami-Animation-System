using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class ScriptingApiPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Scripting API", "API для скриптів");
        public string Category => HonamiDocLocalization.Get("08. Developer API", "08. API для розробників");
        public string SearchKeywords => "scripting api code access methods animator tick playstate parameters tags pause stop layer controller events callbacks mirror initial pose timescale fps cap апі скрипти події параметри";
        public int Order => 700;
        public int EstimatedReadTime => 15;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami provides a clean, modern C# API to control your animators from code. All core functions live on the HonamiAnimator component in the HonamiAnimationSystem.Runtime.Core namespace. This page is a full reference: playback, state skipping, pausing and stopping, parameters, tags, layers, C# events, controller switching, initial pose, mirroring and time control.",
                "Honami надає чисте та сучасне C# API для керування аніматорами з коду. Усі основні функції живуть на компоненті HonamiAnimator у просторі імен HonamiAnimationSystem.Runtime.Core. Ця сторінка — повний довідник: відтворення, пропуск станів, пауза та зупинка, параметри, теги, шари, C#-події, зміна контролера, initial pose, віддзеркалення та керування часом."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Every method that takes a state or parameter name also has an overload that takes an int hash (via HonamiAnimator.StringToHash) and, for states, a ...ByGuid variant. Names are convenient, hashes are allocation-free and fast, GUIDs survive state renames.",
                "Кожен метод, що приймає назву стану чи параметра, має перевантаження з int-хешем (через HonamiAnimator.StringToHash), а для станів — ще й варіант ...ByGuid. Назви зручні, хеші швидкі та без алокацій, GUID переживають перейменування станів."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Playback", "Відтворення"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Method", "Метод"), 260),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("PlayState(name / hash / ...ByGuid)", HonamiDocLocalization.Get("Transitions to a state. Optional: transitionDuration (default 0.25), layer, forceRestart, custom blend curve, destinationStartTime (start offset in seconds).", "Переходить у стан. Опційно: transitionDuration (типово 0.25), layer, forceRestart, власна крива блендінгу, destinationStartTime (стартовий зсув у секундах)."), ""),
                ("PlayStateByGuidWithPriority(...)", HonamiDocLocalization.Get("Low-level entry: explicit transition priority plus victim-weighting settings (victimMode, victimSpeedMultiplier, acceleratedWeightDrop, victimCurve).", "Низькорівневий вхід: явний пріоритет переходу плюс налаштування victim-зважування (victimMode, victimSpeedMultiplier, acceleratedWeightDrop, victimCurve)."), ""),
                ("IsStateActive(name / hash / ...ByGuid)", HonamiDocLocalization.Get("Checks if the state is currently playing on the given layer.", "Перевіряє, чи грає стан на вказаному шарі."), ""),
                ("GetActiveStateIndex(layer)", HonamiDocLocalization.Get("Index of the current state on a layer, -1 if none.", "Індекс поточного стану на шарі, -1 якщо стану немає."), ""),
                ("GetPreviousStateIndex(layer)", HonamiDocLocalization.Get("Index of the state that was active before the current one.", "Індекс стану, що був активним перед поточним."), ""),
                ("GetTransitionWeight(layer)", HonamiDocLocalization.Get("Current cross-fade weight of the running transition on a layer.", "Поточна вага кросфейду активного переходу на шарі."), ""),
                ("GetStateProgress(layer, portIdx)", HonamiDocLocalization.Get("Normalized playback progress of a state port.", "Нормалізований прогрес відтворення порту стану."), "")
            );

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;

public sealed class WeaponAnimation : MonoBehaviour
{
    private HonamiAnimator _honami;
    private static readonly int ReloadHash = HonamiAnimator.StringToHash(""Reload"");

    private void Awake()
    {
        if (!TryGetComponent(out _honami))
            Debug.LogError(""HonamiAnimator missing"", this);
    }

    public void Reload()
    {
        // Hash overload: zero allocations, safe to call every frame.
        _honami.PlayState(ReloadHash, transitionDuration: 0.12f, layer: 1, forceRestart: true);
    }

    public void EquipSkippingWindup()
    {
        // destinationStartTime starts the state 0.5 seconds in.
        _honami.PlayState(""Equip"", transitionDuration: 0.1f, destinationStartTime: 0.5f);
    }
}");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("State skipping", "Пропуск станів"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Skipping lets you conditionally interrupt a specific state without polluting the graph with parameter checks. All skip methods return bool — true if the transition actually happened.",
                "Пропуск дозволяє умовно перервати конкретний стан, не засмічуючи граф перевірками параметрів. Усі skip-методи повертають bool — true, якщо перехід дійсно відбувся."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Method", "Метод"), 260),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("TrySkipState(skip, target, ...)", HonamiDocLocalization.Get("Transitions to target ONLY if the skipped state is currently playing. Name, hash and GUID variants.", "Переходить до цілі ТІЛЬКИ якщо зараз грає стан, що пропускається. Варіанти з назвою, хешем і GUID."), ""),
                ("TryAutoSkipState(skip, ...)", HonamiDocLocalization.Get("Skips the active state using the first transition authored in the graph — duration, curve and priority come from the editor. ignoreExitTime bypasses exit time; cancelEvents suppresses unfired event markers of the skipped state.", "Скіпає активний стан за першим переходом, намальованим у графі — тривалість, крива та пріоритет беруться з редактора. ignoreExitTime ігнорує exit time; cancelEvents скасовує ще не спрацьовані маркери подій стану."), "")
            );
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Interrupt the weapon draw only if it is still playing.
if (_honami.TrySkipState(""DrawWeapon"", ""SlideBlendTree"", transitionDuration: 0.15f))
{
    // Transition happened.
}

// Use the transition settings baked in the graph, skip exit time,
// and cancel any event markers the state has not fired yet.
_honami.TryAutoSkipState(""Equip"", ignoreExitTime: true, cancelEvents: true);");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Pausing and stopping", "Пауза та зупинка"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Method", "Метод"), 260),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("Pause() / Resume()", HonamiDocLocalization.Get("Freezes / resumes the whole animator. IsPaused reflects the flag.", "Заморожує / відновлює весь аніматор. IsPaused відображає прапорець."), ""),
                ("PauseLayer(layer, paused)", HonamiDocLocalization.Get("Pauses a single layer. Check with IsLayerPaused(layer).", "Ставить на паузу окремий шар. Перевірка — IsLayerPaused(layer)."), ""),
                ("PauseState(name, paused, layer)", HonamiDocLocalization.Get("Pauses one specific state without touching the rest of the layer. GUID variant available.", "Ставить на паузу один конкретний стан, не чіпаючи решту шару. Є варіант із GUID."), ""),
                ("Stop(layer)", HonamiDocLocalization.Get("Stops all states on one layer.", "Зупиняє всі стани на одному шарі."), ""),
                ("StopState(name, layer)", HonamiDocLocalization.Get("Stops one state on a layer. GUID variant available.", "Зупиняє один стан на шарі. Є варіант із GUID."), ""),
                ("StopAll()", HonamiDocLocalization.Get("Full reset of the animator state — all layers stop.", "Повне скидання стану аніматора — зупиняються всі шари."), ""),
                ("StopAndKeepPose()", HonamiDocLocalization.Get("Stops updates but keeps the last sampled pose on the character.", "Зупиняє оновлення, але залишає на персонажі останню семпловану позу."), ""),
                ("ResetToDefault()", HonamiDocLocalization.Get("Returns every layer to its default state.", "Повертає кожен шар до його стану за замовчуванням."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Parameters", "Параметри"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Set/Get methods exist for Float, Integer, Bool and Trigger, each with string and int-hash overloads. Triggers are consumed automatically at the end of the tick in which a transition used them; ResetTrigger clears one manually. Setting a parameter that does not exist in the controller is silently ignored.",
                "Set/Get-методи існують для Float, Integer, Bool і Trigger, кожен зі string- та int-hash перевантаженнями. Тригери споживаються автоматично наприкінці тіку, в якому їх використав перехід; ResetTrigger очищає тригер вручну. Запис параметра, якого немає в контролері, тихо ігнорується."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"private static readonly int SpeedId = HonamiAnimator.StringToHash(""Speed"");
private static readonly int JumpId = HonamiAnimator.StringToHash(""Jump"");

private void Update()
{
    // Hash overloads keep Update allocation-free.
    _honami.SetFloat(SpeedId, _input.magnitude);
    _honami.SetBool(""IsGrounded"", _grounded);

    if (_jumpPressed)
        _honami.SetTrigger(JumpId);
}

// Reading values back:
float speed = _honami.GetFloat(SpeedId);
bool grounded = _honami.GetBool(""IsGrounded"");");
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "The Random parameter type refreshes itself with a new 0..1 value every tick — useful for driving idle variation in blend trees without any code. The Parameters property exposes the raw HonamiParameterStore if you need direct access.",
                "Тип параметра Random сам оновлюється новим значенням 0..1 кожен тік — зручно для варіацій idle у blend tree без жодного коду. Властивість Parameters відкриває прямий доступ до HonamiParameterStore."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Tags and layers", "Теги та шари"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Gameplay tags authored on the current state of a layer.
if (_honami.HasTag(""Locked"", layer: 0))
    _movement.enabled = false;

// Layer weights.
_honami.SetLayerWeight(1, _isAiming ? 1f : 0f);
float aimWeight = _honami.GetLayerWeight(1);

// GlobalWeight fades the whole animator output (0..1),
// e.g. blending to ragdoll.
_honami.GlobalWeight = 0.5f;");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("C# events", "C#-події"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiAnimator raises three C# events. OnStateEntered and OnStateFinished pass the state name. OnStateExited passes a HonamiStateExitInfo struct that tells you exactly why the state ended and what comes next.",
                "HonamiAnimator має три C#-події. OnStateEntered і OnStateFinished передають назву стану. OnStateExited передає структуру HonamiStateExitInfo, яка точно каже, чому стан завершився і що йде далі."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Поле"), 200),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("StateName / StateGuid", HonamiDocLocalization.Get("Identity of the state that exited.", "Ідентичність стану, що завершився."), ""),
                ("Layer / PortIndex / StateIndex", HonamiDocLocalization.Get("Where the state lived at runtime.", "Де стан жив у рантаймі."), ""),
                ("Reason", HonamiDocLocalization.Get("Completed, Transition, Interrupted, Stopped, Restarted or ControllerChanged.", "Completed, Transition, Interrupted, Stopped, Restarted або ControllerChanged."), ""),
                ("NextStateName / NextStateGuid", HonamiDocLocalization.Get("The state that replaced it, if any.", "Стан, який його замінив, якщо є."), ""),
                ("WasCompleted / WasInterrupted", HonamiDocLocalization.Get("Convenience flags derived from Reason.", "Зручні прапорці, похідні від Reason."), "")
            );
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"private void OnEnable()
{
    _honami.OnStateEntered += HandleStateEntered;
    _honami.OnStateExited += HandleStateExited;
}

private void OnDisable()
{
    _honami.OnStateEntered -= HandleStateEntered;
    _honami.OnStateExited -= HandleStateExited;
}

private void HandleStateEntered(string stateName)
{
    if (stateName == ""Death"")
        _collider.enabled = false;
}

private void HandleStateExited(HonamiStateExitInfo info)
{
    // Only grant the combo window if the attack played to the end.
    if (info.StateName == ""Attack1"" && info.WasCompleted)
        _combo.OpenWindow();
}");
            HonamiDocumentationBuilder.AddWarning(root, HonamiDocLocalization.Get(
                "Always unsubscribe in OnDisable/OnDestroy. A destroyed listener that stays subscribed will leak and throw once the event fires.",
                "Завжди відписуйтесь у OnDisable/OnDestroy. Знищений слухач, що лишився підписаним, призведе до витоку та помилок при спрацюванні події."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Switching controllers at runtime", "Зміна контролера в рантаймі"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "SetController swaps the whole runtime controller, optionally cross-fading between the old and new graphs. HonamiControllerTransitionMode decides whether the outgoing controller keeps evaluating during the blend (ContinueEvaluating) or freezes on its last pose (Freeze). CurrentController returns the active asset.",
                "SetController замінює весь runtime-контролер, опційно з кросфейдом між старим і новим графами. HonamiControllerTransitionMode визначає, чи старий контролер продовжує оцінюватись під час блендінгу (ContinueEvaluating), чи заморожується на останній позі (Freeze). CurrentController повертає активний ассет."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Instant swap.
_honami.SetController(riflePoseController);

// Half-second cross-fade; the old controller keeps animating while fading out.
_honami.SetController(pistolPoseController,
    transitionDuration: 0.5f,
    mode: HonamiControllerTransitionMode.ContinueEvaluating);");
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "For named controller sets with per-state transition settings, add a HonamiControllerProfile component and call SetProfile(\"Rifle\") — see the Controller Profiles page.",
                "Для іменованих наборів контролерів із власними налаштуваннями переходів додайте компонент HonamiControllerProfile і викликайте SetProfile(\"Rifle\") — див. сторінку Controller Profiles."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Initial pose", "Initial Pose"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The animator can snapshot the hierarchy pose and use it as a safe fallback whenever no state is active. Capture happens automatically on Awake when enabled in the inspector, or manually from code.",
                "Аніматор може зробити знімок пози ієрархії та використовувати його як безпечний fallback, коли жоден стан не активний. Знімок робиться автоматично в Awake (якщо ввімкнено в інспекторі) або вручну з коду."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Method", "Метод"), 300),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("CaptureInitialPose()", HonamiDocLocalization.Get("Snapshots the current hierarchy pose.", "Знімає поточну позу ієрархії."), ""),
                ("CaptureInitialPoseFromDefaultState()", HonamiDocLocalization.Get("Snapshots the final frame of the default state instead of the current pose.", "Знімає останній кадр стану за замовчуванням замість поточної пози."), ""),
                ("RestoreInitialPose()", HonamiDocLocalization.Get("Applies the captured pose immediately.", "Негайно застосовує збережену позу."), ""),
                ("HasInitialPose", HonamiDocLocalization.Get("True once a pose has been captured.", "True, щойно позу було збережено."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Mirroring", "Віддзеркалення"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Mirror every evaluated state across the avatar; blend into it over time.
_honami.SetGlobalMirrorSpeed(4f);
_honami.SetGlobalMirror(true);

bool mirrored = _honami.MirrorAvatar;");
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Individual states can also be mirrored via their Mirror checkbox in the graph; SetGlobalMirror flips the whole avatar on top of that. A blend speed of 0 switches instantly.",
                "Окремі стани також можна віддзеркалювати чекбоксом Mirror у графі; SetGlobalMirror додатково перевертає весь аватар. Швидкість блендінгу 0 перемикає миттєво."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Time control and manual ticking", "Керування часом і ручний тік"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Поле"), 220),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("TimeScale", HonamiDocLocalization.Get("Per-animator playback speed multiplier (0..10 in the inspector).", "Множник швидкості відтворення для цього аніматора (0..10 в інспекторі)."), ""),
                ("FpsCap / TargetFPS", HonamiDocLocalization.Get("Evaluates the graph at a fixed rate (1-120 FPS) for LOD-style savings; fpsCapInterpolate smooths between evaluated frames.", "Оцінює граф із фіксованою частотою (1-120 FPS) для економії в стилі LOD; fpsCapInterpolate згладжує між кадрами."), ""),
                ("Update Mode", HonamiDocLocalization.Get("Normal, AnimatePhysics, UnscaledTime, LateUpdate or Manual — chooses which Unity loop drives the graph.", "Normal, AnimatePhysics, UnscaledTime, LateUpdate або Manual — визначає, який цикл Unity рухає граф."), ""),
                ("Tick(deltaTime)", HonamiDocLocalization.Get("Advances the animator by hand. Intended for Manual mode: deterministic simulations, network ticks, editor preview.", "Просуває аніматор вручну. Для режиму Manual: детерміновані симуляції, мережеві тіки, editor preview."), "")
            );
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Drive Honami from your own simulation clock (Update Mode = Manual).
public void SimulationStep(float fixedDelta)
{
    _honami.Tick(fixedDelta);
}

// Slow motion for one character only.
_honami.TimeScale = 0.2f;

// Cheap background characters: evaluate at 15 FPS with interpolation.
_honami.FpsCap = true;
_honami.TargetFPS = 15;");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Avatar mask utilities", "Утиліти масок аватара"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Method", "Метод"), 260),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("RegisterAvatarMask(mask)", HonamiDocLocalization.Get("Registers a mask created or modified at runtime.", "Реєструє маску, створену чи змінену в рантаймі."), ""),
                ("RebakeAvatarMask(mask)", HonamiDocLocalization.Get("Rebakes one mask after its bone set changed. Returns false if the mask is unknown.", "Перезапікає одну маску після зміни набору кісток. Повертає false, якщо маска невідома."), ""),
                ("RebakeAllAvatarMasks()", HonamiDocLocalization.Get("Rebakes every registered mask.", "Перезапікає всі зареєстровані маски."), ""),
                ("RefreshBoneReplacements()", HonamiDocLocalization.Get("Re-applies bone replacer bindings after swapping skeleton parts.", "Повторно застосовує заміни кісток після зміни частин скелета."), "")
            );

            HonamiDocumentationBuilder.AddSeparator(root);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Broadcasting to many animators at once (SetActionID, PlayState across a squad, ByTag parameter setters) lives on the Linked Animator brain — see the Linked Animation category. The static HonamiLinkedAction class additionally offers global, tag, layer-mask, radius, propagation-wave and closest-N targeting without any brain in the scene.",
                "Розсилання на багато аніматорів одночасно (SetActionID, PlayState по загону, ByTag-сеттери параметрів) живе на «мозку» Linked Animator — див. категорію Linked Animation. Статичний клас HonamiLinkedAction додатково дає глобальне таргетування, за тегом, layer-маскою, радіусом, хвилею поширення та за N найближчими — без жодного мозку в сцені."
            ));
        }
    }
}
