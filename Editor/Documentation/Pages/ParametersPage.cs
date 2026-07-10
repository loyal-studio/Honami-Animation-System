using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class ParametersPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Parameters", "Параметри");
        public string Category => HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти");
        public string SearchKeywords => "parameters floats bools triggers ints random hash assignments data drive logic параметри дані логіка хеш";
        public int Order => 210;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("The five parameter types and when to use each", "П'ять типів параметрів і коли який використовувати"),
                    HonamiDocLocalization.Get("Reading and writing parameters from code, name vs hash", "Читання й запис параметрів з коду, назва vs хеш"),
                    HonamiDocLocalization.Get("Trigger lifetime and Parameter Assignments on states", "Життєвий цикл тригера та Parameter Assignments на стейтах")
                },
                new[]
                {
                    HonamiDocLocalization.Get("Tutorial parts 2–3: first SetFloat and SetTrigger calls", "Частини 2–3 туторіалу: перші виклики SetFloat і SetTrigger")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Parameters are the primary way to drive your animation graph from external code. They act as the 'variables' of your state machine: transitions read them in conditions, blend trees sample them for weights, and states can even write them back.",
                "Параметри — це основний спосіб керування графом анімації із зовнішнього коду. Вони виступають як «змінні» вашого скінченного автомата: переходи читають їх в умовах, blend tree беруть із них ваги, а стани можуть навіть записувати їх назад."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Parameter Types", "Типи параметрів"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParameterTable(root,
                ("Float", "float", HonamiDocLocalization.Get("Continuous values. Ideal for Speed, Direction, or Blend Weights.", "Безперервні значення. Ідеально для швидкості, напрямку або ваги змішування.")),
                ("Int", "int", HonamiDocLocalization.Get("Whole numbers. Good for weapon IDs or state indices.", "Цілі числа. Підходять для ID зброї або індексів станів.")),
                ("Bool", "bool", HonamiDocLocalization.Get("True/False flags. Used for IsGrounded, IsDead, etc.", "Прапорці True/False. Використовуються для IsGrounded, IsDead тощо.")),
                ("Trigger", "trigger", HonamiDocLocalization.Get("One-shot flags that reset after being consumed by a transition.", "Одноразові прапорці, що скидаються після використання переходом.")),
                ("Random", "float", HonamiDocLocalization.Get("Self-updating float that receives a fresh 0..1 value every tick. Perfect for idle variation and random blend tree picks without code.", "Float, що сам оновлюється новим значенням 0..1 кожен тік. Ідеально для варіацій idle та випадкового вибору в blend tree без коду."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Scripting API", "API для скриптів"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Every setter and getter exists in two flavors: by string name and by int hash. Hash overloads avoid per-call hashing and allocations, so cache the hash once and use it in hot paths.",
                "Кожен сеттер і геттер існує у двох варіантах: за string-назвою та за int-хешем. Hash-перевантаження уникають хешування при кожному виклику та алокацій, тож кешуйте хеш один раз і використовуйте його в гарячих шляхах."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"private static readonly int SpeedId = HonamiAnimator.StringToHash(""Speed"");

private void Update()
{
    // Hot path: hash overload, zero allocations.
    honami.SetFloat(SpeedId, movementInput.magnitude);

    // Occasional calls can use names for readability.
    honami.SetBool(""IsGrounded"", controller.isGrounded);

    if (jumpPressed)
        honami.SetTrigger(""Jump"");
}

// Reading values back:
float speed = honami.GetFloat(SpeedId);
int weapon = honami.GetInteger(""WeaponId"");
bool dead = honami.GetBool(""IsDead"");

// Cancel a trigger that has not been consumed yet:
honami.ResetTrigger(""Jump"");");

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Writing a parameter that does not exist in the controller is silently ignored — no exception, no log in builds. If a transition 'does nothing', first check the parameter name spelling.",
                "Запис параметра, якого немає в контролері, тихо ігнорується — без винятку та без логу в білдах. Якщо перехід «нічого не робить», спершу перевірте написання назви параметра."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Trigger lifetime", "Життєвий цикл тригера"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "SetTrigger raises the flag; it stays raised until a transition consumes it.",
                "SetTrigger піднімає прапорець; він лишається піднятим, доки його не спожиє перехід."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Consumption happens at the end of the tick in which a transition condition used the trigger.",
                "Споживання відбувається наприкінці тіку, в якому умова переходу використала тригер."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "ResetTrigger clears it manually — useful when the character enters a state where the queued action no longer makes sense.",
                "ResetTrigger очищає його вручну — корисно, коли персонаж потрапляє у стан, де відкладена дія вже не має сенсу."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Parameter assignments on states", "Присвоєння параметрів у станах"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "States can write parameters themselves. Each state has a Parameter Assignments list: when the state is entered (or exited, if Execute On Exit is checked) it sets the configured Float/Int/Bool value or fires/clears a Trigger. This keeps simple bookkeeping — like flagging 'InCombat' while an attack state is active — inside the graph, with no code at all.",
                "Стани можуть самі записувати параметри. Кожен стан має список Parameter Assignments: при вході в стан (або при виході, якщо увімкнено Execute On Exit) він встановлює задане значення Float/Int/Bool чи вмикає/скидає Trigger. Це дозволяє тримати просту логіку — на кшталт прапорця «InCombat», поки активний стан атаки — прямо в графі, без жодного коду."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Use 'Normalized' floats (0 to 1) for blend trees to keep your logic consistent across different movement speeds.",
                "Використовуйте нормалізовані float (від 0 до 1) для дерев змішування, щоб логіка була послідовною для різних швидкостей руху."
            ));

            HonamiDocumentationBuilder.AddWarning(root, HonamiDocLocalization.Get(
                "Parameter names are matched by hash. Two parameters of the same storage type whose names collide will log an error in the editor and one of them becomes unreachable — rename one of them.",
                "Назви параметрів зіставляються за хешем. Два параметри одного типу зберігання з колізією назв дадуть помилку в редакторі, і один із них стане недосяжним — перейменуйте один із них."
            ));
        }
    }
}
