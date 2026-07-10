using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class BlendingAndLayersPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Blending & Layers", "Змішування та шари");
        public string Category => HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти");
        public string SearchKeywords => "blending layers override masking weight inheritance child layer parent layer pipeline attack interact base locomotion змішування шари маски спадкування пайплайн";
        public int Order => 230;
        public int EstimatedReadTime => 9;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("How layers override each other from bottom to top", "Як шари перекривають один одного знизу вгору"),
                    HonamiDocLocalization.Get("Layer weight, Avatar Mask and mirroring", "Вага шару, Avatar Mask та віддзеркалення"),
                    HonamiDocLocalization.Get("The layer-per-action pipeline for characters that act while moving", "Пайплайн «шар під дію» для персонажів, що діють під час руху")
                },
                new[]
                {
                    HonamiDocLocalization.Get("The tutorial: states, blend trees and transitions", "Туторіал: стейти, blend tree і транзішни"),
                    HonamiDocLocalization.Get("Masks are covered in depth on the Avatars & Masks page", "Маски детально розібрані на сторінці «Аватари та маски»")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Layers allow you to run multiple animation states simultaneously. This is essential for complex characters that need to perform actions while moving.",
                "Шари дозволяють запускати кілька станів анімації одночасно. Це необхідно для складних персонажів, яким потрібно виконувати дії під час руху."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Think of a layer as an independent animation graph with its own default state, transitions, weight, mask and optional mirroring. The final pose is produced by blending layer outputs from bottom to top.",
                "Думайте про шар як про незалежний анімаційний граф із власним дефолтним станом, переходами, вагою, маскою та опціональним віддзеркаленням. Фінальна поза створюється змішуванням виходів шарів знизу вгору."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("How layers blend", "Як шари змішуються"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Every layer above the base blends in override mode: on the bones its Avatar Mask allows, it replaces the pose of the layers below proportionally to its weight. Weight 1 fully overrides the masked bones, weight 0 makes the layer invisible.",
                "Кожен шар над базовим змішується в режимі перекриття (override): на кістках, дозволених його Avatar Mask, він замінює позу нижніх шарів пропорційно до своєї ваги. Вага 1 повністю перекриває замасковані кістки, вага 0 робить шар невидимим."
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                ("Weight", HonamiDocLocalization.Get("Blend strength of the layer, 0–1. Editable in the layer panel and via SetLayerWeight at runtime.", "Сила змішування шару, 0–1. Редагується в панелі шарів та через SetLayerWeight у рантаймі.")),
                ("Avatar Mask", HonamiDocLocalization.Get("Limits which bones the layer affects. Individual states can also override the mask.", "Обмежує, на які кістки впливає шар. Окремі стани також можуть перевизначати маску.")),
                ("Mirror Layer", HonamiDocLocalization.Get("Mirrors the layer's output using the Avatar's mirror bones.", "Віддзеркалює вихід шару за mirror-кістками Avatar.")),
                ("Parent Layer", HonamiDocLocalization.Get("Set via Create Child Layer — the layer inherits states and transitions from an earlier layer.", "Задається через Create Child Layer — шар успадковує стани й переходи попереднього шару."))
            );
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Honami layers are override-only. There is no additive layer type yet — for breathing, recoil or other overlay detail use rig constraints or a masked override layer.",
                "Шари Honami працюють лише в режимі перекриття. Адитивного типу шару поки немає — для дихання, віддачі та інших накладених деталей використовуйте риг-констрейнти або замаскований override-шар."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Layer-per-action pipeline", "Пайплайн: шар під кожну дію"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The recommended character setup gives each independent kind of action its own layer, so movement never fights with attacks or interactions.",
                "Рекомендована схема персонажа виділяє кожному незалежному типу дій власний шар, щоб рух ніколи не конфліктував з атаками чи взаємодіями."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Layer", "Шар"), 140),
                (HonamiDocLocalization.Get("Mask", "Маска"), 140),
                (HonamiDocLocalization.Get("Purpose", "Призначення"), 0),
                ("0 — Base Layer", HonamiDocLocalization.Get("None (full body)", "Немає (усе тіло)"), HonamiDocLocalization.Get("Locomotion: idle, walk, run, jump. Weight 1, never disabled.", "Locomotion: idle, ходьба, біг, стрибок. Вага 1, шар ніколи не вимикається.")),
                ("1 — Attack", HonamiDocLocalization.Get("Spine + Arms", "Хребет + руки"), HonamiDocLocalization.Get("Non-loop attack states with damage-window event markers. Legs keep following locomotion.", "Non-loop стани атак з маркерами вікна урону. Ноги продовжують рухатися з locomotion.")),
                ("2 — Interact", HonamiDocLocalization.Get("Arms", "Руки"), HonamiDocLocalization.Get("Pick up, use, open — short one-shot actions triggered from gameplay code.", "Підняти, використати, відчинити — короткі one-shot дії, що запускаються з ігрового коду."))
            );
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Build layer 0 as your locomotion graph (usually a blend tree Idle/Walk/Run) and leave its weight at 1.",
                "Зберіть шар 0 як граф locomotion (зазвичай blend tree Idle/Walk/Run) і залиште його вагу 1."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Press the + button in the Animation Layers panel to add a layer, rename it to Attack and assign an upper-body Avatar Mask. Set its weight to 0 so the layer stays silent until an attack plays.",
                "Натисніть кнопку + у панелі Animation Layers, щоб додати шар, перейменуйте його на Attack і призначте Avatar Mask верхньої частини тіла. Поставте вагу 0, щоб шар мовчав, доки атака не грає."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Create the attack states on that layer as non-loop states. Add event markers for damage windows, sounds and VFX — they fire independently of the Base layer (see the Event System page).",
                "Створіть на цьому шарі стани атак як non-loop стани. Додайте маркери подій для вікон урону, звуків і VFX — вони спрацьовують незалежно від базового шару (див. сторінку Event System)."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "From gameplay code raise the layer weight and play the state on that layer. When the state finishes (OnStateFinished fires for non-loop states), drop the weight back to 0.",
                "З ігрового коду підніміть вагу шару та запустіть стан на цьому шарі. Коли стан завершиться (OnStateFinished викликається для non-loop станів), поверніть вагу назад у 0."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Repeat the same pattern for the Interact layer. Layer index equals the layer's position in the Animation Layers list, base is 0.",
                "Повторіть той самий патерн для шару Interact. Індекс шару дорівнює його позиції у списку Animation Layers, базовий — 0."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"public sealed class CombatAnimationDriver : MonoBehaviour
{
    [SerializeField] private HonamiAnimator honami;

    private const int AttackLayer = 1;
    private const int InteractLayer = 2;

    private void OnEnable() => honami.OnStateFinished += HandleStateFinished;
    private void OnDisable() => honami.OnStateFinished -= HandleStateFinished;

    public void Attack()
    {
        honami.SetLayerWeight(AttackLayer, 1f);
        honami.PlayState(""Attack_Slash"", 0.1f, AttackLayer, forceRestart: true);
    }

    public void Interact()
    {
        honami.SetLayerWeight(InteractLayer, 1f);
        honami.PlayState(""Interact_Use"", 0.1f, InteractLayer, forceRestart: true);
    }

    private void HandleStateFinished(string state)
    {
        if (state == ""Attack_Slash"") honami.SetLayerWeight(AttackLayer, 0f);
        if (state == ""Interact_Use"") honami.SetLayerWeight(InteractLayer, 0f);
    }
}");
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "SetLayerWeight snaps instantly. If you want the action to fade out smoothly instead, lerp the weight over a few frames, or transition the state back to a neutral pose state with Has Exit Time and keep the weight at 1.",
                "SetLayerWeight змінює вагу миттєво. Якщо дія має згасати плавно, лерпайте вагу кілька кадрів або зробіть транзішн стану назад у нейтральну позу через Has Exit Time, тримаючи вагу 1."
            ), HonamiDocumentationBuilder.CalloutType.Tip);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Masking", "Маскування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Avatar Masks define which bones a layer should affect. For example, an 'Upper Body' mask allows a player to reload or wave while the 'Base' layer handles running.",
                "Маски аватара визначають, на які кістки впливає шар. Наприклад, маска «Upper Body» дозволяє гравцеві перезаряджатися або махати рукою, поки базовий шар обробляє біг."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Layer Inheritance", "Спадкування шарів"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Layer inheritance lets a child layer reuse the states and transitions of an earlier parent layer. Honami creates virtual inherited states for the child layer, so the graph stays compact while the runtime still has a complete layer-specific state set.",
                "Спадкування шарів дозволяє дочірньому шару повторно використовувати стани та переходи попереднього батьківського шару. Honami створює віртуальні успадковані стани для дочірнього шару, тому граф залишається компактним, а рантайм усе одно має повний набір станів для конкретного шару."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Create it from a layer's options menu with Create Child Layer.",
                "Створюється з меню шару через Create Child Layer."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "The child layer inherits the parent's states, default state and transitions until you create an override.",
                "Дочірній шар успадковує стани, дефолтний стан і переходи батьківського шару, доки ви не створите override."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Inherited states are read-only in the graph. Use Create Override when this layer needs a different node, sub-nodes, events, tags or transitions.",
                "Успадковані стани в графі read-only. Використовуйте Create Override, коли цьому шару потрібен інший вузол, підвузли, події, теги або переходи."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Only earlier layers can be parents. This prevents circular dependencies and keeps evaluation order deterministic.",
                "Батьківськими можуть бути лише попередні шари. Це запобігає циклічним залежностям і зберігає детермінований порядок обчислення."
            ));

            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "Example: build Base Locomotion once, then create a child layer named Injured Locomotion. Override only Walk and Run with limping clips. Idle, Jump, Fall and their transitions remain inherited.",
                    "Приклад: створіть Base Locomotion один раз, потім зробіть дочірній шар Injured Locomotion. Перевизначте лише Walk і Run кліпами кульгавості. Idle, Jump, Fall та їхні переходи залишаться успадкованими."
                ),
                HonamiDocumentationBuilder.CalloutType.Tip);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Runtime Control", "Керування в рантаймі"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Fade an upper-body action layer in and out.
honami.SetLayerWeight(1, isAiming ? 1f : 0f);

// Trigger a state on a specific layer.
honami.PlayState(""Reload"", transitionDuration: 0.12f, layer: 1, forceRestart: true);

// Mirror the whole avatar smoothly when gameplay asks for it.
honami.SetGlobalMirrorSpeed(8f);
honami.SetGlobalMirror(shouldMirror);");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Keep action-layer masks as narrow as the action allows — the fewer bones a layer overrides, the less it fights the locomotion underneath.",
                "Тримайте маски шарів дій настільки вузькими, наскільки дозволяє дія — що менше кісток перекриває шар, то менше він конфліктує з locomotion під ним."
            ));
        }
    }
}
