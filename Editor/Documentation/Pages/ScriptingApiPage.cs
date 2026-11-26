using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class ScriptingApiPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Scripting API", "API для скриптів");
        public string Category => HonamiDocLocalization.Get("09. Developer API", "09. API для розробників");
        public string SearchKeywords => "scripting api code access methods animator tick playstate parameters tags pause stop layer controller events callbacks mirror initial pose global weight mode init bind timescale fps cap апі скрипти події параметри глобальна вага режим";
        public int Order => 700;
        public int EstimatedReadTime => 15;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami provides a clean, modern C# API to control your animators from code. All core functions live on the HonamiAnimator component in the HonamiAnimationSystem.Runtime.Core namespace. This page is a full reference: playback, state skipping, pausing and stopping, parameters, tags, layers, C# events, controller switching, initial pose, mirroring and time control.",
                "Honami надає чисте та сучасне C# API для керування аніматорами з коду. Усі основні функції живуть на компоненті HonamiAnimator у просторі імен HonamiAnimationSystem.Runtime.Core. Ця сторінка - повний довідник: відтворення, пропуск станів, пауза та зупинка, параметри, теги, шари, C#-події, зміна контролера, initial pose, віддзеркалення та керування часом."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Playback and parameter methods that take a name also take an int hash (via HonamiAnimator.StringToHash), and state methods add a ...ByGuid variant. Names are convenient, hashes are allocation-free and fast, GUIDs survive state renames. PauseState and StopState are name/GUID only. One rule worth remembering: an int argument always means a name hash - accessors that take a runtime index carry an At suffix (GetStateAt, GetStateDurationAt).",
                "Методи відтворення й параметрів, що приймають назву, приймають також int-хеш (через HonamiAnimator.StringToHash), а методи станів мають ще й варіант ...ByGuid. Назви зручні, хеші швидкі та без алокацій, GUID переживають перейменування станів. PauseState і StopState мають лише варіанти з назвою та GUID. Одне правило варте уваги: int-аргумент завжди означає хеш назви - аксесори, що беруть рантайм-індекс, мають суфікс At (GetStateAt, GetStateDurationAt)."
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

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Reading runtime state", "Читання рантайм-стану"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Playback methods answer \"do this\". These answer \"what is happening\" - useful for HUDs, debug overlays, gameplay gates and custom nodes that need to look around the graph.",
                "Методи відтворення відповідають на «зроби це». Ці - на «що зараз відбувається»: для HUD, дебаг-оверлеїв, ґейтів геймплею та власних вузлів, яким треба озирнутись по графу."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Member", "Член"), 300),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("StateCount / LayerCount", HonamiDocLocalization.Get("Size of the built graph.", "Розмір побудованого графа."), ""),
                ("TryResolveState(name / hash) / TryResolveStateByGuid / TryResolveStateByGuidHash", HonamiDocLocalization.Get("Name, hash or GUID to runtime state index. The guid-hash form matches HonamiTransition.targetStateGuidHash, so custom nodes can follow transitions.", "Назва, хеш чи GUID у індекс рантайм-стану. Форма з guid-хешем відповідає HonamiTransition.targetStateGuidHash, тож власні вузли можуть іти по переходах."), ""),
                ("GetStateAt(index) / GetState(name) / GetStateByGuid(guid)", HonamiDocLocalization.Get("The HonamiState asset behind an index, or null.", "Ассет HonamiState за індексом, або null."), ""),
                ("GetStatesNonAlloc(Span<HonamiState>)", HonamiDocLocalization.Get("Copies every runtime state into your buffer.", "Копіює всі рантайм-стани у ваш буфер."), ""),
                ("GetCurrentState(layer) / GetCurrentStateName(layer) / GetPreviousState(layer)", HonamiDocLocalization.Get("What a layer is playing right now, and what it is blending away from.", "Що шар грає зараз і від чого відходить."), ""),
                ("GetLayerState(layer) / GetPortState(layer, port)", HonamiDocLocalization.Get("Read-only snapshots of the transition and per-port bookkeeping structs.", "Знімки структур переходу та побудового обліку порту тільки для читання."), ""),
                ("GetStateDurationAt(index) / GetStateDuration(name)", HonamiDocLocalization.Get("Unscaled length as the graph currently evaluates it, blend trees resolved.", "Немасштабована довжина так, як її зараз оцінює граф, з розв'язаними blend tree."), ""),
                ("GetCurrentStateProgress(layer)", HonamiDocLocalization.Get("Progress of whatever owns the layer, without needing the port index.", "Прогрес того, що володіє шаром, без потреби знати індекс порту."), ""),
                ("GetStatePlayable(layer, port) / GetStateWeight(layer, port)", HonamiDocLocalization.Get("The raw Playable and its mixer weight, for direct Playables work.", "Сирий Playable та його вага в мікшері - для прямої роботи з Playables."), ""),
                ("GetStateNodeAt(index) / GetStateRuntimeAt(index)", HonamiDocLocalization.Get("The node the controller evaluates for a state after override resolution, and the per-animator runtime object it allocated.", "Вузол, який контролер оцінює для стану після розв'язання оверрайдів, та виділений ним рантайм-об'єкт на аніматор."), ""),
                ("IsStatePausedAt(index, layer) / IsTransitioningController", HonamiDocLocalization.Get("Pause and controller-swap status.", "Стан паузи та підміни контролера."), "")
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
                "Skipping lets you conditionally interrupt a specific state without polluting the graph with parameter checks. All skip methods return bool - true if the transition actually happened.",
                "Пропуск дозволяє умовно перервати конкретний стан, не засмічуючи граф перевірками параметрів. Усі skip-методи повертають bool - true, якщо перехід дійсно відбувся."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Method", "Метод"), 260),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("TrySkipState(skip, target, ...)", HonamiDocLocalization.Get("Transitions to target ONLY if the skipped state is currently playing. Name, hash and GUID variants.", "Переходить до цілі ТІЛЬКИ якщо зараз грає стан, що пропускається. Варіанти з назвою, хешем і GUID."), ""),
                ("TryAutoSkipState(skip, ...)", HonamiDocLocalization.Get("Skips the active state using the first transition authored in the graph - duration, curve and priority come from the editor. ignoreExitTime bypasses exit time; cancelEvents suppresses unfired event markers of the skipped state.", "Скіпає активний стан за першим переходом, намальованим у графі - тривалість, крива та пріоритет беруться з редактора. ignoreExitTime ігнорує exit time; cancelEvents скасовує ще не спрацьовані маркери подій стану."), ""),
                ("AutoSkipStateAt(index, layer, ...)", HonamiDocLocalization.Get("The same skip by runtime index, without the is-it-active check. Useful from custom nodes that already resolved an index.", "Той самий скіп за рантайм-індексом, без перевірки активності. Зручно з власних вузлів, які вже мають індекс."), "")
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
                ("PauseLayer(layer, paused)", HonamiDocLocalization.Get("Pauses a single layer. Check with IsLayerPaused(layer).", "Ставить на паузу окремий шар. Перевірка - IsLayerPaused(layer)."), ""),
                ("PauseState(name, paused, layer)", HonamiDocLocalization.Get("Pauses one specific state without touching the rest of the layer. GUID variant available.", "Ставить на паузу один конкретний стан, не чіпаючи решту шару. Є варіант із GUID."), ""),
                ("Stop(layer)", HonamiDocLocalization.Get("Stops all states on one layer.", "Зупиняє всі стани на одному шарі."), ""),
                ("StopState(name, layer)", HonamiDocLocalization.Get("Stops one state on a layer. GUID variant available.", "Зупиняє один стан на шарі. Є варіант із GUID."), ""),
                ("StopAll()", HonamiDocLocalization.Get("Full reset of the animator state - all layers stop.", "Повне скидання стану аніматора - зупиняються всі шари."), ""),
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
                "The Random parameter type refreshes itself with a new 0..1 value every tick - useful for driving idle variation in blend trees without any code. The Parameters property exposes the raw HonamiParameterStore: Definitions lists every parameter the controller declared, Contains(name) tests existence, and TryGetFloat/TryGetInteger/TryGetBool distinguish \"zero\" from \"not declared\" - which the plain getters cannot.",
                "Тип параметра Random сам оновлюється новим значенням 0..1 кожен тік - зручно для варіацій idle у blend tree без жодного коду. Властивість Parameters відкриває прямий доступ до HonamiParameterStore: Definitions перелічує всі оголошені контролером параметри, Contains(name) перевіряє наявність, а TryGetFloat/TryGetInteger/TryGetBool відрізняють «нуль» від «не оголошено» - чого звичайні геттери не вміють."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Tags and layers", "Теги та шари"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Gameplay tags authored on the current state of a layer.
if (_honami.HasTag(""Locked"", layer: 0))
    _movement.enabled = false;

// Layer weights.
_honami.SetLayerWeight(1, _isAiming ? 1f : 0f);
float aimWeight = _honami.GetLayerWeight(1);

// GlobalWeight attenuates the whole animator (0..1). How it attenuates
// depends on Global Weight Mode - see the Global weight section below.
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
                "For named controller sets with per-state transition settings, add a HonamiControllerProfile component and call SetProfile(\"Rifle\") - see the Controller Profiles page.",
                "Для іменованих наборів контролерів із власними налаштуваннями переходів додайте компонент HonamiControllerProfile і викликайте SetProfile(\"Rifle\") - див. сторінку Controller Profiles."
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

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Global weight", "Global Weight"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "GlobalWeight (0..1) attenuates how much this animator drives the hierarchy - 1 is full animation, 0 is none. Global Weight Mode, set in the inspector or via the GlobalWeightMode property, decides how that attenuation is produced.",
                "GlobalWeight (0..1) регулює, наскільки сильно цей аніматор рухає ієрархію - 1 це повна анімація, 0 це жодної. Global Weight Mode, що задається в інспекторі або через property GlobalWeightMode, визначає, як саме ця атенюація рахується."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Mode", "Режим"), 160),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("Init", HonamiDocLocalization.Get("Default. Blends the evaluated pose toward the captured initial pose after the graph runs, so weight 0 is a clean neutral and the animation output stays at full weight - rotations never drift. Costs a position, rotation and scale interpolation per driven bone on every frame the weight is below 1. Requires a captured initial pose (Capture On Awake); without one, GlobalWeight below 1 has no effect at all.", "Типовий. Після обчислення графа блендить оцінену позу до захопленої initial pose, тож вага 0 дає чистий нейтрал, а output анімації лишається на повній вазі - обертання не пливуть. Коштує інтерполяції позиції, обертання й масштабу на кожну керовану кістку в кожному кадрі, поки вага менша за 1. Потребує захопленої initial pose (Capture On Awake); без неї GlobalWeight нижче 1 не має жодного ефекту."), ""),
                ("Bind", HonamiDocLocalization.Get("Scales the animation output weight itself, so the blend happens inside Unity's animation system: one call, no per-bone work on any frame. Needs no initial pose. Weight 0 falls back to the Animator bind pose rather than your captured rest pose, and fading below 1 can drift rotations (a visible tilt) because Unity mixes against that bind pose.", "Масштабує саму вагу output анімації, тож бленд відбувається всередині анімаційної системи Unity: один виклик, жодної роботи по кістках у жодному кадрі. Initial pose не потрібна. Вага 0 відкочується до bind-пози Animator, а не до вашої захопленої пози спокою, і фейд нижче 1 може «пливти» по обертанню (видимий нахил), бо Unity мішає проти цієї bind-пози."), "")
            );
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Dial the whole animator's influence down to 50%.
_honami.GlobalWeight = 0.5f;

// Choose how weight attenuates (usually set once in the inspector).
_honami.GlobalWeightMode = HonamiGlobalWeightMode.Init; // exact rest pose at 0, per-bone cost
_honami.GlobalWeightMode = HonamiGlobalWeightMode.Bind; // free, but fades toward the bind pose");
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Neither mode is the old one - they trade precision against cost. Use Init for camera or additive rigs where weight 0 must land on an exact rest pose: it is deterministic and leaves the animation output untouched. Use Bind when the weight is held below 1 for long stretches or across many characters, when the GameObject has no captured initial pose to blend toward, or when the bind pose is the fallback you actually want - it moves the blend into the animation system and costs nothing per frame.",
                "Жоден із режимів не є «старим» - вони міняють точність на ціну. Init беріть для камери чи адитивних ригів, де вага 0 має дати точну rest-позу: він детермінований і не чіпає output анімації. Bind беріть, коли вага тримається нижче 1 тривало або на багатьох персонажах, коли на об'єкті немає захопленої initial pose, до якої блендити, або коли саме bind-поза і є потрібним фолбеком - він переносить бленд в анімаційну систему й не коштує нічого за кадр."
            ));

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
                ("Update Mode", HonamiDocLocalization.Get("Normal, AnimatePhysics, UnscaledTime, LateUpdate or Manual - chooses which Unity loop drives the graph.", "Normal, AnimatePhysics, UnscaledTime, LateUpdate або Manual - визначає, який цикл Unity рухає граф."), ""),
                ("BatchedEvaluation", HonamiDocLocalization.Get("Inspector toggle, on by default. False keeps this animator off Unity's batched animation pass even when Update Mode allows it.", "Перемикач з інспектора, увімкнений за замовчуванням. False не пускає аніматор у пакетний анімаційний прохід Unity, навіть коли Update Mode це дозволяє."), ""),
                ("IsBatchEvaluated / SupportsBatching", HonamiDocLocalization.Get("Whether Unity is batch-evaluating the graph right now, and whether this animator type can be batched at all.", "Чи оцінює Unity граф пакетно просто зараз, і чи цей тип аніматора взагалі підтримує пакетне оцінювання."), ""),
                ("Tick(deltaTime)", HonamiDocLocalization.Get("Advances the animator by hand. Intended for Manual mode: deterministic simulations, network ticks, editor preview.", "Просуває аніматор вручну. Для режиму Manual: детерміновані симуляції, мережеві тіки, editor preview."), ""),
                ("RefreshFpsCapHierarchy()", HonamiDocLocalization.Get("Rebuilds the interpolation buffers after child transforms are added or removed at runtime (equipping an item, swapping a limb). Only needed with FPS Cap plus interpolation.", "Перебудовує буфери інтерполяції після додавання чи видалення дочірніх transform у рантаймі (взяли предмет, замінили кінцівку). Потрібно лише при FPS Cap з інтерполяцією."), ""),
                ("RequestCatchUpTick()", HonamiDocLocalization.Get("Queues a zero-delta evaluation for this frame LateUpdate, so a graph rebuilt mid-frame does not hold a stale pose. Ignored in Manual mode.", "Ставить у чергу оцінювання з нульовою дельтою на LateUpdate цього кадру, щоб перебудований посеред кадру граф не тримав стару позу. У режимі Manual ігнорується."), "")
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
                "This page documents HonamiAnimator. Its lighter sibling, HonamiClipPlayer, shares the whole base API - Tick, Pause, Resume, TimeScale, FpsCap, GlobalWeight, CaptureInitialPose, ReactToAction - but replaces states and parameters with a clip list: PlayClip, CrossFade, PlayQueued, Blend, Sample and the player[\"Idle\"] handles. See the Clip Player page in Core Concepts. Both derive from HonamiAnimatorBase, so tooling that should accept any Honami animator types against the base - including animators you write yourself: see Writing a Custom Animator.",
                "Ця сторінка описує HonamiAnimator. Його легший побратим HonamiClipPlayer ділить усе базове API - Tick, Pause, Resume, TimeScale, FpsCap, GlobalWeight, CaptureInitialPose, ReactToAction - але замість станів і параметрів має список кліпів: PlayClip, CrossFade, PlayQueued, Blend, Sample та хендли player[\"Idle\"]. Див. сторінку Clip Player в Основних концептах. Обидва успадковані від HonamiAnimatorBase, тож тулінг, який має приймати будь-який аніматор Honami, типізується по базі - включно з аніматорами, які ви напишете самі: див. «Власний аніматор»."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Broadcasting to many animators at once (SetActionID, PlayState across a squad, ByTag parameter setters) lives on the Linked Animator brain - see the Linked Animation category. The static HonamiLinkedAction class additionally offers global, tag, layer-mask, radius, propagation-wave and closest-N targeting without any brain in the scene, plus Broadcast(id, predicate) for a filter it does not ship.",
                "Розсилання на багато аніматорів одночасно (SetActionID, PlayState по загону, ByTag-сеттери параметрів) живе на «мозку» Linked Animator - див. категорію Linked Animation. Статичний клас HonamiLinkedAction додатково дає глобальне таргетування, за тегом, layer-маскою, радіусом, хвилею поширення та за N найближчими - без жодного мозку в сцені, а також Broadcast(id, predicate) для фільтра, якого немає в комплекті."
            ));
        }
    }
}
