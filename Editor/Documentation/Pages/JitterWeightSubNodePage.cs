using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class JitterWeightSubNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Jitter: Weight", "Jitter: Weight");
        public string Category => HonamiDocLocalization.Get("05. Sub-Nodes", "05. Підвузли");
        public string SearchKeywords => "jitter weight subnode procedural variation тремтіння вага варіація";
        public int Order => 411;
        public int EstimatedReadTime => 2;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Jitter Weight Sub-Node applies procedural noise to the weight of a layer or a sub-layer. This is useful for breathing effects or shaky hand logic.",
                "Підвузол «Jitter Weight» застосовує процедурний шум до ваги шару або підшару. Це корисно для ефектів дихання або логіки «тремтячих рук»."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Parameters", "Параметри"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Scale", "Scale"), HonamiDocLocalization.Get("Maximum deviation of the state's weight, as a fraction of its current weight. 0.1 = up to ±10%.", "Максимальне відхилення ваги стану у частках від поточної ваги. 0.1 = до ±10%.")),
                (HonamiDocLocalization.Get("Frequency", "Frequency"), HonamiDocLocalization.Get("How fast the Perlin noise evolves — how quickly the weight wobbles.", "Наскільки швидко змінюється шум Перліна — як швидко коливається вага."))
            );
            HonamiDocumentationBuilder.AddInfoBox(root, HonamiDocLocalization.Get(
                "The jitter multiplies the weight the state already has (the result is clamped to 0..1), and each state gets a unique noise phase automatically. A state at weight 0 stays untouched.",
                "Тремтіння множиться на вагу, яку стан уже має (результат обмежується 0..1), і кожен стан автоматично отримує унікальну фазу шуму. Стан із вагою 0 лишається недоторканим."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Use Cases", "Сценарії використання"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Breathing: Subtle oscillation of an additive layer.", "Дихання: Тонка осциляція адитивного шару."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Weapon Shake: Adding instability to an aiming pose.", "Тремтіння зброї: Додавання нестабільності до пози прицілювання."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Stress: Increasing jitter when a character's health is low.", "Стрес: Збільшення тремтіння при низькому здоров'ї персонажа."));
        }
    }
}
