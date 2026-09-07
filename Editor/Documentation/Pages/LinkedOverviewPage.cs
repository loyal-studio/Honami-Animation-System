using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class LinkedOverviewPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Linked Systems Overview", "Огляд систем Linked");
        public string Category => HonamiDocLocalization.Get("06. Honami Linked", "06. Honami Linked");
        public string SearchKeywords => "linked overview orchestration brain actionid multi-animator оркестрація";
        public int Order => 500;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami Linked is a suite of tools designed to synchronize and orchestrate multiple animators. Whether you have a character split into pieces or a group of NPCs reacting to a single event, the Linked system provides the logic to keep them in sync.",
                "Honami Linked — це набір інструментів, розроблених для синхронізації та оркестрації кількох аніматорів. Незалежно від того, чи розділений ваш персонаж на частини, чи це група NPC, що реагують на одну подію, система Linked забезпечує логіку для їх синхронізації."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The Two Core Systems", "Дві основні системи"), HonamiEditorIcons.GraphWhite);

            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("Global Actions (ActionID)", "Глобальні дії (ActionID)"), HonamiDocLocalization.Get("A decentralized event system where animators 'listen' for specific actions (like an explosion or a shout) and react locally.", "Децентралізована система подій, де аніматори «слухають» певні дії (вибух, крик) та реагують локально."), HonamiEditorIcons.Controller),
                (HonamiDocLocalization.Get("Linked Brain", "Linked Brain"), HonamiDocLocalization.Get("A centralized orchestrator component that manages a specific group of animators using a visual Graph.", "Централізований компонент-оркестратор, який керує конкретною групою аніматорів за допомогою візуального графа."), HonamiEditorIcons.TimelineWhite)
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Which one to use?", "Що вибрати?"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Use Global Actions when any character in the world might need to react to an event.", "Використовуйте Глобальні дії, коли будь-який персонаж у світі може зреагувати на подію."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Use Linked Brain when you have a specific group of objects (like Body, Face, Armor) that MUST move together.", "Використовуйте Linked Brain, коли у вас є конкретна група об'єктів, які ПОВИННІ рухатися разом."));

            HonamiDocumentationBuilder.AddInfoBox(root, HonamiDocLocalization.Get(
                "Both systems work on Honami Animators and Clip Players alike — an NPC or a scene prop on a Clip Player reacts to an ActionID exactly like a controller-backed character does. Calls that need a controller behind them (parameters, state skipping) simply skip the Clip Players.",
                "Обидві системи працюють і з Honami Animator, і з Clip Player — NPC чи пропс на Clip Player реагує на ActionID так само, як персонаж із контролером. Виклики, що потребують контролера (параметри, пропуск станів), просто оминають Clip Player."
            ));
        }
    }
}
