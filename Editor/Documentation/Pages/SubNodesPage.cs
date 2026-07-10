using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class SubNodesPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Sub-Nodes Overview", "Огляд підвузлів");
        public string Category => HonamiDocLocalization.Get("05. Sub-Nodes", "05. Підвузли");
        public string SearchKeywords => "sub-nodes logic encapsulation states components підвузли логіка інкапсуляція";
        public int Order => 400;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Sub-Nodes are one of Honami's most powerful features. They allow you to attach logic directly to a state, ensuring that it only runs when that state is active.",
                "Підвузли (Sub-Nodes) — одна з найпотужніших функцій Honami. Вони дозволяють приєднувати логіку безпосередньо до стану, гарантуючи, що вона працює лише тоді, коли цей стан активний."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Clean Architecture", "Чиста архітектура"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "By using Sub-Nodes, you keep your main animation graph focused on high-level state decisions, while details like footstep sounds, particle spawning, or procedural jitter are handled internally.",
                "Завдяки підвузлам ваш основний граф анімації залишається сфокусованим на високорівневих рішеннях, тоді як деталі на кшталт звуків кроків, часток або процедурного тремтіння обробляються внутрішньо."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Lifecycle", "Життєвий цикл"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("OnEnter: Runs once when the transition into the parent state begins.", "OnEnter: Виконується один раз, коли починається перехід у батьківський стан."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("OnUpdate: Runs every frame while the parent state is active (including during transitions).", "OnUpdate: Виконується кожного кадру, поки батьківський стан активний."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("OnExit: Runs once when the transition out of the parent state begins.", "OnExit: Виконується один раз, коли починається вихід із батьківського стану."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Built-in Sub-Nodes", "Вбудовані підвузли"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Sub-Node", "Підвузол"), 170),
                (HonamiDocLocalization.Get("Purpose", "Призначення"), 0),
                ("", 0),
                ("Mask Switcher", HonamiDocLocalization.Get("Swaps or blends the avatar mask while the state is active. Has its own page.", "Змінює чи блендить маску аватара, поки стан активний. Має власну сторінку."), ""),
                ("Replacer", HonamiDocLocalization.Get("Applies bone replacements for the duration of the state.", "Застосовує заміни кісток на час дії стану."), ""),
                ("Smooth Bone Weight", HonamiDocLocalization.Get("Eases per-bone mask weights in and out instead of switching instantly.", "Плавно вводить і виводить ваги кісток маски замість миттєвого перемикання."), ""),
                ("Jitter Speed", HonamiDocLocalization.Get("Randomizes playback speed over time for organic variation. Has its own page.", "Випадково змінює швидкість відтворення в часі для органічної варіативності. Має власну сторінку."), ""),
                ("Jitter Weight", HonamiDocLocalization.Get("Randomizes the state's weight over time. Has its own page.", "Випадково змінює вагу стану в часі. Має власну сторінку."), ""),
                ("Log", HonamiDocLocalization.Get("Prints messages on enter/update/exit — a debugging probe. Has its own page.", "Друкує повідомлення на вході/оновленні/виході — зонд для налагодження. Має власну сторінку."), "")
            );

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "You can stack multiple Sub-Nodes on a single state to combine different behaviors. Custom sub-nodes are just ScriptableObjects extending HonamiSubNodeBase: override UpdateRuntime (required) plus optional OnEnter / OnExit.",
                "Ви можете додавати кілька підвузлів до одного стану, щоб комбінувати різні типи поведінки. Власні підвузли — це просто ScriptableObject, успадковані від HonamiSubNodeBase: перевизначте UpdateRuntime (обов'язково) та опційні OnEnter / OnExit."
            ));
        }
    }
}
