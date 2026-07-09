using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class LinkedBrainAdvancedPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Advanced Brain Techniques", "Просунуті техніки Brain");
        public string Category => HonamiDocLocalization.Get("07. Honami Linked", "07. Honami Linked");
        public string SearchKeywords => "advanced brain choreography sync netcode events distance profile controller просунуті техніки синхронізація";
        public int Order => 750;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Brain events", "Події Мозку"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A Linked Animator Graph is organized into named events, each owning its root nodes. Gameplay code starts them by name with TriggerEvent; several events can run concurrently and StopBrainEvents aborts them all. The ActiveNodes property exposes what is currently executing — the graph editor uses it for live highlighting.",
                "Linked Animator Graph організований у іменовані події, кожна зі своїми кореневими вузлами. Ігровий код запускає їх за назвою через TriggerEvent; кілька подій можуть виконуватися паралельно, а StopBrainEvents перериває всі. Властивість ActiveNodes показує, що виконується зараз — редактор графа використовує її для живого підсвічування."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Kick off two independent choreographies.
brain.TriggerEvent(""Ambush"");
brain.TriggerEvent(""RadioChatter"");

// Panic button.
brain.StopBrainEvents();

// Debug what is running.
foreach (var node in brain.ActiveNodes)
    Debug.Log(node.name);");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Distance-filtered broadcasts", "Розсилки з фільтром відстані"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "SetActionID and PlayState have overloads that take an origin point, a max distance and an optional limit — only linked animators inside the sphere react. This keeps huge crowds cheap: one brain, spatially scoped commands.",
                "SetActionID і PlayState мають перевантаження з точкою походження, максимальною відстанню та опційним лімітом — реагують лише прилінковані аніматори всередині сфери. Це робить величезні натовпи дешевими: один мозок, просторово обмежені команди."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Grenade lands: only the 6 closest squad members within 12 m dive.
brain.SetActionID(diveAction, grenade.position, maxDistance: 12f,
    transitionDuration: 0.1f, limit: 6);");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Group controller swaps", "Групова зміна контролерів"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Brain forwards SetController and SetProfile to every linked animator, so an entire squad can cross-fade from 'Patrol' to 'Combat' controller sets with one call. SetProfile requires a HonamiControllerProfile on each animator — see the Controller Profiles page.",
                "Мозок транслює SetController і SetProfile кожному прилінкованому аніматору, тож цілий загін може кросфейдом перейти з набору контролерів «Patrol» на «Combat» одним викликом. SetProfile потребує HonamiControllerProfile на кожному аніматорі — див. сторінку Controller Profiles."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"brain.SetProfile(""Combat"", transitionDuration: 0.35f);");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Network Synchronization", "Мережева синхронізація"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "When using Honami Linked in a multiplayer game, you only need to sync the Brain's parameters or state changes. The Brain will ensure that all linked animators stay in sync locally for every client.",
                "При використанні Honami Linked у багатокористувацькій грі вам потрібно лише синхронізувати параметри Мозку або зміни станів. Мозок гарантує, що всі зв'язані аніматори залишаться синхронізованими локально для кожного клієнта."
            ));
        }
    }
}
