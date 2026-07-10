using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class TutorialEventsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("4. Events & What's Next", "4. Івенти та що далі");
        public string Category => HonamiDocLocalization.Get("02. Tutorial: First Character", "02. Туторіал: Перший персонаж");
        public string SearchKeywords => "tutorial events markers landing sound receiver next steps туторіал івенти маркери звук крок що далі";
        public int Order => 130;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("How to place an event marker on a state's timeline", "Як поставити маркер івенту на таймлайн стейту"),
                    HonamiDocLocalization.Get("How a Local Event Receiver maps markers to UnityEvents", "Як Local Event Receiver зіставляє маркери з UnityEvent"),
                    HonamiDocLocalization.Get("Where to go after the tutorial", "Куди рухатися після туторіалу")
                },
                new[]
                {
                    HonamiDocLocalization.Get("Tutorial part 3: the Jump state with transitions", "Частина 3 туторіалу: стейт Jump із транзішнами")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Sounds, particles and gameplay windows must fire at exact frames of an animation. In Honami that is the job of event markers on the state's timeline. We will add a landing sound to the jump.",
                "Звуки, партикли та геймплейні вікна мають спрацьовувати на точних кадрах анімації. У Honami за це відповідають маркери івентів на таймлайні стейту. Додамо звук приземлення до стрибка."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 1 — Place the marker", "Крок 1 — Поставте маркер"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Select the Jump state — its timeline appears in the Timeline panel at the bottom of the Graph Editor.",
                "Виберіть стейт Jump — його таймлайн з'явиться в панелі Timeline внизу Graph Editor."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Right-click the timeline at the moment the feet hit the ground and choose Add Local Event.",
                "Клікніть правою кнопкою по таймлайну в момент, коли стопи торкаються землі, і виберіть Add Local Event."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Select the marker and set its Event Name to Land.",
                "Виберіть маркер і задайте йому Event Name = Land."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 2 — Wire the receiver", "Крок 2 — Підключіть ресівер"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "On the GameObject that has the Honami Animator: Add Component → Honami Animation → Local Event Receiver. Do this before entering Play mode — the receiver is discovered once, when the animator initializes.",
                "На GameObject із Honami Animator: Add Component → Honami Animation → Local Event Receiver. Зробіть це до входу в Play mode — ресівер знаходиться один раз, під час ініціалізації аніматора."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "In its Local Events list add an entry with Event Name = Land (must match the marker exactly) and wire the UnityEvent to an AudioSource.PlayOneShot with your landing clip.",
                "У списку Local Events додайте запис з Event Name = Land (має точно збігатися з маркером) і підключіть UnityEvent до AudioSource.PlayOneShot із кліпом приземлення."));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Play, run, jump: the thud lands exactly on the frame you marked. If you mistype the name, Honami logs a console warning when the marker fires — use it to catch typos.",
                "Play, біжимо, стрибаємо: звук падає точно на позначений кадр. Якщо помилитеся в назві, Honami напише попередження в консоль у момент спрацювання маркера — так зручно ловити одруки."
            ));

            HonamiDocumentationBuilder.AddSeparator(root);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The tutorial is done. Where next?", "Туторіал завершено. Куди далі?"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "You now have every core mechanism in your hands: states, parameters, blending, transitions and events. The rest of the documentation deepens each of them:",
                "Тепер у вас в руках усі базові механізми: стейти, параметри, блендінг, транзішни та івенти. Решта документації поглиблює кожен із них:"
            ));

            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Blending & Layers", "Blending & Layers"), HonamiDocLocalization.Get("Upper-body actions on top of locomotion: layers, masks, additive blending.", "Дії верхньої частини тіла поверх локомоушну: шари, маски, адитивний блендінг.")),
                (HonamiDocLocalization.Get("Transitions", "Transitions"), HonamiDocLocalization.Get("Priorities, interruptions, Victim and Smart transition types.", "Пріоритети, переривання, типи транзішнів Victim і Smart.")),
                (HonamiDocLocalization.Get("Event System", "Event System"), HonamiDocLocalization.Get("Global events, C# state callbacks, marker guarantees on loops.", "Глобальні івенти, C#-колбеки станів, гарантії маркерів на циклах.")),
                (HonamiDocLocalization.Get("Node Library", "Node Library"), HonamiDocLocalization.Get("Random & Sequencer, Any State, Portals and the rest of the toolbox.", "Random і Sequencer, Any State, портали та решта інструментарію.")),
                (HonamiDocLocalization.Get("Scripting API", "Scripting API"), HonamiDocLocalization.Get("The full HonamiAnimator surface for programmers.", "Повний інтерфейс HonamiAnimator для програмістів."))
            );
        }
    }
}
