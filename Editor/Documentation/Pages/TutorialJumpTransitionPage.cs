using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class TutorialJumpTransitionPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("3. Transitions: the Jump", "3. Транзішни: стрибок");
        public string Category => HonamiDocLocalization.Get("02. Tutorial: First Character", "02. Туторіал: Перший персонаж");
        public string SearchKeywords => "tutorial jump transition trigger condition exit time туторіал стрибок транзішн тригер умова крок";
        public int Order => 120;
        public int EstimatedReadTime => 5;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("How to draw a transition and give it conditions", "Як намалювати транзішн і задати йому умови"),
                    HonamiDocLocalization.Get("What a Trigger is and how it differs from a Bool", "Що таке Trigger і чим він відрізняється від Bool"),
                    HonamiDocLocalization.Get("How Exit Time returns the character to locomotion", "Як Exit Time повертає персонажа до локомоушну")
                },
                new[]
                {
                    HonamiDocLocalization.Get("Tutorial part 2: the Movement blend tree as the default state", "Частина 2 туторіалу: blend tree Movement як default-стейт")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A transition is a rule: 'when these conditions are met, blend from state A to state B over this duration'. We need two: Movement → Jump when the player presses a button, and Jump → Movement when the jump animation is nearly done.",
                "Транзішн — це правило: «коли виконані ці умови, змішайся зі стейту A в стейт B за таку тривалість». Нам потрібні два: Movement → Jump, коли гравець тисне кнопку, і Jump → Movement, коли анімація стрибка майже завершена."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 1 — The Jump trigger and state", "Крок 1 — Тригер і стейт Jump"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "In the Parameters panel add a Trigger named Jump. A trigger is a one-shot flag: SetTrigger raises it, and it resets itself after a transition consumes it — perfect for button presses.",
                "У панелі Parameters додайте Trigger із назвою Jump. Тригер — одноразовий прапорець: SetTrigger піднімає його, і він скидається сам після того, як транзішн його спожив — ідеально для натискань кнопок."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Add an Animation Node with your Jump clip. Leave Loop OFF — a jump plays once.",
                "Додайте Animation Node зі стрибковим кліпом. Loop залиште ВИМКНЕНИМ — стрибок грає один раз."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 2 — Movement → Jump", "Крок 2 — Movement → Jump"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Drag a transition from the Movement node to the Jump node.",
                "Протягніть транзішн від вузла Movement до вузла Jump."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Select the transition and add a condition: If Jump.",
                "Виберіть транзішн і додайте умову: If Jump."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Set Duration to about 0.1 — jumps must feel instant, a long cross-fade here reads as input lag.",
                "Поставте Duration близько 0.1 — стрибок має відчуватися миттєвим, довгий кросфейд тут читається як лаг вводу."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 3 — Jump → Movement", "Крок 3 — Jump → Movement"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Drag a transition back from Jump to Movement.",
                "Протягніть транзішн назад від Jump до Movement."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Enable Has Exit Time and set Exit Time to about 0.8: the transition fires when the jump reaches 80% of its normalized length — no conditions needed.",
                "Увімкніть Has Exit Time і поставте Exit Time близько 0.8: транзішн спрацює, коли стрибок досягне 80% своєї нормалізованої довжини — умови не потрібні."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Set Duration to about 0.2 so the landing melts back into locomotion.",
                "Duration близько 0.2 — щоб приземлення плавно розчинилося в локомоушні."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 4 — Fire the trigger", "Крок 4 — Запустіть тригер"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"private void Update()
{
    var input = new Vector2(Input.GetAxis(""Horizontal""), Input.GetAxis(""Vertical""));
    _honami.SetFloat(SpeedId, Mathf.Clamp01(input.magnitude));

    if (Input.GetKeyDown(KeyCode.Space))
        _honami.SetTrigger(""Jump"");
}");

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "A trigger stays raised until a transition actually consumes it. If the transition 'does nothing', check the parameter name spelling first — writing a non-existent parameter is silently ignored.",
                "Тригер лишається піднятим, доки транзішн його реально не спожиє. Якщо транзішн «нічого не робить» — спершу перевірте написання назви параметра: запис неіснуючого параметра тихо ігнорується."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Code can also switch states directly: honami.PlayState(\"Jump\", transitionDuration: 0.1f). Use authored transitions for rules that live in the graph, PlayState for decisions gameplay code owns.",
                "Код може перемикати стейти й напряму: honami.PlayState(\"Jump\", transitionDuration: 0.1f). Намальовані транзішни — для правил, що живуть у графі; PlayState — для рішень, якими володіє ігровий код."
            ));

            HonamiDocumentationBuilder.AddSeparator(root);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Next: the character moves and jumps, but silently. In part 4 we make the animation talk back to the game with event markers.",
                "Далі: персонаж рухається і стрибає, але беззвучно. У частині 4 навчимо анімацію «говорити» з грою через маркери івентів."
            ));
        }
    }
}
