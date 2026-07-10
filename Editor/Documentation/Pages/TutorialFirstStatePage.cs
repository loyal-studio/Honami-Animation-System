using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class TutorialFirstStatePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("1. Your First State", "1. Перший стейт");
        public string Category => HonamiDocLocalization.Get("02. Tutorial: First Character", "02. Туторіал: Перший персонаж");
        public string SearchKeywords => "tutorial first state idle controller graph default туторіал перший стейт крок initial";
        public int Order => 100;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "This tutorial series builds one character from scratch, one concept per page: an idle state (this page), locomotion with a blend tree (page 2), a jump with transitions (page 3) and sound events (page 4). By the end you will have touched every core mechanism of Honami.",
                "Ця серія туторіалів будує одного персонажа з нуля, по одному концепту на сторінку: idle-стейт (ця сторінка), локомоушн із blend tree (сторінка 2), стрибок із транзішнами (сторінка 3) та звукові івенти (сторінка 4). Наприкінці ви торкнетеся кожного базового механізму Honami."
            ));

            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("What a state is and what the graph does every frame", "Що таке стейт і що граф робить кожен кадр"),
                    HonamiDocLocalization.Get("How to create a controller and add an Animation Node", "Як створити контролер і додати Animation Node"),
                    HonamiDocLocalization.Get("How to set the default state and see it play", "Як призначити стейт за замовчуванням і побачити його в грі")
                },
                new[]
                {
                    HonamiDocLocalization.Get("'First Steps' completed: a character with Animator (empty Controller slot) and Honami Animator components", "Пройдено «First Steps»: персонаж із компонентами Animator (порожній слот Controller) та Honami Animator"),
                    HonamiDocLocalization.Get("Three AnimationClips: Idle, Walk or Run, and Jump", "Три AnimationClip: Idle, Walk або Run, та Jump")
                });

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What a state actually is", "Що таке стейт"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A state is a node in the graph that the layer can be 'in'. What it plays depends on the node type: an Animation Node plays one clip, a Blend Tree mixes several, Random and Sequencer nodes pick or chain clips — the full catalogue lives in the '04. Node Library' section. The rules are the same for every type: at any moment each layer has exactly one current state, and that is what you see on the character (during a transition there are briefly two — the old one fading out, the new one fading in). The state owns its playback settings — Loop, Speed, Weight — and can carry event markers on its timeline.",
                "Стейт — це вузол графа, в якому шар може «перебувати». Що саме він грає — залежить від типу вузла: Animation Node відтворює один кліп, Blend Tree змішує кілька, Random і Sequencer обирають кліп або грають ланцюжок — повний каталог є в розділі «04. Бібліотека вузлів». Правила однакові для всіх типів: у кожного шару в будь-який момент є рівно один поточний стейт, і саме його ви бачите на персонажі (під час транзішна їх коротко два — старий виблендується, новий вблендується). Стейт володіє своїми налаштуваннями відтворення — Loop, Speed, Weight — і може нести маркери івентів на своєму таймлайні."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "In this tutorial we start with the simplest type — the Animation Node.",
                "У цьому туторіалі починаємо з найпростішого типу — Animation Node."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 1 — Create and assign a controller", "Крок 1 — Створіть і призначте контролер"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "In the Project window: right-click → Create → Honami Animation → Controller. Name it after your character.",
                "У вікні Project: правий клік → Create → Honami Animation → Controller. Назвіть його на честь персонажа."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Drag the asset into the Controller field of the Honami Animator component on your character.",
                "Перетягніть ассет у поле Controller компонента Honami Animator на персонажі."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 2 — Add the Idle state", "Крок 2 — Додайте Idle-стейт"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Open the controller in the Graph Editor (double-click the asset or the Open button in the inspector).",
                "Відкрийте контролер у Graph Editor (подвійний клік по ассету або кнопка Open в інспекторі)."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "The fastest way to add a state: drag your Idle AnimationClip from the Project window straight onto the canvas — an Animation Node is created with the clip already assigned. Alternatively right-click → Animation Node and assign the Clip in the inspector.",
                "Найшвидший спосіб додати стейт: перетягніть Idle-кліп із вікна Project прямо на полотно — створиться Animation Node з уже призначеним кліпом. Або правий клік → Animation Node і призначте Clip в інспекторі."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Select the node and enable Loop in its state properties — idle must repeat forever.",
                "Виберіть вузол і увімкніть Loop у властивостях стану — idle має повторюватися нескінченно."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Mark the node as the default state — this is the state the character enters on startup.",
                "Позначте вузол стейтом за замовчуванням — саме в нього персонаж увійде на старті."));

            HonamiDocumentationBuilder.AddNodeVisual(root, "Idle", "ANIMATION", HonamiGraphStyles.Accent);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 3 — Press Play", "Крок 3 — Натисніть Play"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Enter Play mode. The default state starts automatically and the character breathes in its idle loop. Keep the Graph Editor open: the active state is highlighted and shows its weight in real time — this live view is your main debugging tool from now on.",
                "Запустіть Play mode. Стейт за замовчуванням стартує автоматично, і персонаж «дихає» у своєму idle-циклі. Тримайте Graph Editor відкритим: активний стейт підсвічується та показує свою вагу в реальному часі — це ваш головний інструмент дебагу надалі."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Nothing plays? Check the two classic causes: the Animator's Controller slot is not empty, or no state is marked as default.",
                "Нічого не грає? Перевірте дві класичні причини: слот Controller в Animator не порожній, або жоден стейт не позначений як default."
            ));

            HonamiDocumentationBuilder.AddSeparator(root);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Next: the character stands — let's make it move. In part 2 we replace the single Idle state with a locomotion blend tree driven by a Speed parameter.",
                "Далі: персонаж стоїть — навчімо його рухатися. У частині 2 ми замінимо одиночний Idle на локомоушн-blend tree, керований параметром Speed."
            ));
        }
    }
}
