using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class LinkedActionPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Global Actions (ActionID)", "Глобальні дії (ActionID)");
        public string Category => HonamiDocLocalization.Get("06. Honami Linked", "06. Honami Linked");
        public string SearchKeywords => "actionid global actions events broadcasting nearby propagated closest reacttoaction децентралізація події радіус хвиля";
        public int Order => 520;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Global Actions use the ActionID asset to broadcast events across the world without any Brain in the scene. Animators whose states carry that ActionID register themselves automatically and force-play those states when the action fires.",
                "Глобальні дії використовують ассет ActionID для трансляції подій по всьому світу без жодного «мозку» в сцені. Аніматори, чиї стани мають цей ActionID, реєструються автоматично та примусово відтворюють ці стани, коли дія спрацьовує."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The Workflow", "Воркфлоу"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("1. Create an ActionID asset: Create → Honami Animation → Action ID (e.g., 'Heavy_Explosion').", "1. Створіть ассет ActionID: Create → Honami Animation → Action ID (наприклад, «Heavy_Explosion»)."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("2. On the reaction state in the graph, set its Linked Action ID field to that asset. Registration happens automatically when the animator initializes.", "2. У стані-реакції в графі заповніть поле Linked Action ID цим ассетом. Реєстрація відбувається автоматично при ініціалізації аніматора."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("3. Call HonamiLinkedAction.PlayGlobal(actionID) from your gameplay code — every registered animator plays its reaction state (force-restart, on the state's own layer).", "3. Викличте HonamiLinkedAction.PlayGlobal(actionID) з ігрового коду — кожен зареєстрований аніматор відтворить свій стан-реакцію (з force-restart, на власному шарі стану)."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Targeting variants", "Варіанти таргетингу"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Method", "Метод"), 220),
                (HonamiDocLocalization.Get("Who reacts", "Хто реагує"), 0),
                ("", 0),
                ("PlayGlobal(id, duration, limit)", HonamiDocLocalization.Get("Every registered animator, optionally capped by limit.", "Кожен зареєстрований аніматор, опційно обмежено limit."), ""),
                ("PlayByTag(id, tag, ...)", HonamiDocLocalization.Get("Only animators whose Linking Tag matches the HonamiTagID.", "Лише аніматори, чий Linking Tag збігається з HonamiTagID."), ""),
                ("PlayByLayer(id, layerMask, ...)", HonamiDocLocalization.Get("Only animators on matching Unity GameObject layers.", "Лише аніматори на відповідних Unity-шарах GameObject."), ""),
                ("PlayInHierarchy(id, root, ...)", HonamiDocLocalization.Get("Only animators under the given Transform.", "Лише аніматори під вказаним Transform."), ""),
                ("PlayNearby(id, origin, radius, ...)", HonamiDocLocalization.Get("Only animators within the radius; overload with a LayerMask filter exists.", "Лише аніматори в радіусі; є перевантаження з фільтром LayerMask."), ""),
                ("PlayClosest(id, origin, radius, limit)", HonamiDocLocalization.Get("The N closest animators inside the radius.", "N найближчих аніматорів у радіусі."), ""),
                ("PlayPropagated(id, origin, radius, speed)", HonamiDocLocalization.Get("Everyone in the radius, but delayed by distance / speed — the reaction spreads like a shockwave.", "Усі в радіусі, але із затримкою distance / speed — реакція розходиться, як ударна хвиля."), "")
            );

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Core;

// Everyone reacts.
HonamiLinkedAction.PlayGlobal(explosionAction, transitionDuration: 0.15f);

// Only characters within 10 meters.
HonamiLinkedAction.PlayNearby(explosionAction, transform.position, 10f);

// Shockwave: reactions ripple outward at 25 m/s.
HonamiLinkedAction.PlayPropagated(explosionAction, transform.position,
    radius: 40f, speed: 25f);

// The three closest bystanders flinch.
HonamiLinkedAction.PlayClosest(flinchAction, transform.position,
    radius: 12f, limit: 3);

// Direct hit on a single animator, delayed by half a second.
victim.ReactToAction(flinchAction, transitionDuration: 0.1f, delay: 0.5f);");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "ReactToAction with a delay queues the reaction on the animator itself, so it still fires even if the caller is destroyed. PlayPropagated is built on exactly this mechanism.",
                "ReactToAction із затримкою ставить реакцію в чергу на самому аніматорі, тож вона спрацює, навіть якщо викликача знищено. PlayPropagated побудований саме на цьому механізмі."
            ));
        }
    }
}
