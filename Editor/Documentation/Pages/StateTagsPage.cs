using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class StateTagsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("State Tags", "Теги станів");
        public string Category => HonamiDocLocalization.Get("08. Developer API", "08. API для розробників");
        public string SearchKeywords => "tags states logic metadata теги стани логіка";
        public int Order => 710;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "State Tags allow you to attach metadata strings to your states. This can be used for gameplay logic, such as determining if a character can be interrupted or if they are currently in a 'Combat' stance.",
                "Теги станів дозволяють прикріплювати рядки метаданих до ваших станів. Це можна використовувати для ігрової логіки, наприклад, для визначення того, чи можна перервати дію персонажа."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Checking Tags", "Перевірка тегів"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HasTag checks the tags of the state currently playing on the given layer. It returns false while no state is active or when the layer index is out of range.",
                "HasTag перевіряє теги стану, що зараз грає на вказаному шарі. Повертає false, коли жоден стан не активний або індекс шару виходить за межі."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Check if the current state on the base layer has a specific tag.
if (honami.HasTag(""Invincible"")) {
    // Character cannot take damage right now.
}

// Tags are per-layer: check the upper-body layer separately.
if (honami.HasTag(""Locked"", layer: 1)) {
    weapon.blockInput = true;
}");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Practical Uses", "Практичне використання"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Vulnerability: Tagging frames where a character is vulnerable to attacks.", "Вразливість: Тегування кадрів, де персонаж вразливий до атак."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Audio/VFX: Triggering specific sounds based on the active state tag.", "Звук/VFX: Запуск певних звуків на основі тегу активного стану."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Movement Logic: Disabling player movement during 'Locked' states.", "Логіка руху: Вимкнення руху гравця під час станів із тегом «Locked»."));
        }
    }
}
