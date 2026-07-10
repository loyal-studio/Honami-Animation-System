using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class JitterSpeedSubNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Jitter: Speed", "Jitter: Speed");
        public string Category => HonamiDocLocalization.Get("05. Sub-Nodes", "05. Підвузли");
        public string SearchKeywords => "jitter speed subnode procedural variation perlin noise scale frequency тремтіння швидкість варіація шум";
        public int Order => 410;
        public int EstimatedReadTime => 2;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Jitter Speed Sub-Node adds subtle procedural variation to the playback speed of a state using Perlin noise. It keeps Idle and other looping animations from looking robotic.",
                "Підвузол «Jitter Speed» додає тонку процедурну варіацію швидкості відтворення стану за допомогою шуму Перліна. Це не дає станам Idle та іншим циклічним анімаціям виглядати роботизовано."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Parameters", "Параметри"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Scale", "Scale"), HonamiDocLocalization.Get("Maximum deviation from the base speed, as a fraction. 0.15 means the speed drifts up to ±15%.", "Максимальне відхилення від базової швидкості у частках. 0.15 означає, що швидкість коливається до ±15%.")),
                (HonamiDocLocalization.Get("Frequency", "Frequency"), HonamiDocLocalization.Get("How fast the noise evolves, i.e. how quickly the speed wanders.", "Наскільки швидко змінюється шум, тобто як швидко «блукає» швидкість."))
            );

            HonamiDocumentationBuilder.AddInfoBox(root, HonamiDocLocalization.Get(
                "There is no Seed field. Each state automatically gets a unique noise phase offset (derived from its index), so two states using the same settings still jitter differently.",
                "Поля Seed немає. Кожен стан автоматично отримує унікальне зсув фази шуму (виводиться з його індексу), тож два стани з однаковими налаштуваннями все одно тремтять по-різному."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Keep Scale small (0.05–0.2). The modifier multiplies the state's base speed (respecting Reversed), so large values make timing feel unstable.",
                "Тримайте Scale малим (0.05–0.2). Модифікатор множиться на базову швидкість стану (враховуючи Reversed), тож великі значення роблять таймінг нестабільним."
            ));
        }
    }
}
