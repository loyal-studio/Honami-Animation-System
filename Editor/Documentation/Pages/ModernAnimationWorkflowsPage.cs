using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class ModernAnimationWorkflowsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Modern Workflows", "Сучасні воркфлоу");
        public string Category => HonamiDocLocalization.Get("09. Theory & Background", "09. Теорія та довідка");
        public string SearchKeywords => "workflow best practices modularity reusable components layer inheritance override воркфлоу практики спадкування";
        public int Order => 830;
        public int EstimatedReadTime => 5;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami encourages a modular approach to animation. Instead of one massive graph, think about reusable logic and layered systems.",
                "Honami заохочує модульний підхід до анімації. Замість одного масивного графа думайте про логіку, яку можна використовувати повторно, та шарові системи."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Modularity with Sub-Nodes", "Модульність через підвузли"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Don't clutter your main graph with sound effects or particle triggers. Use Sub-Nodes inside states to handle these side-effects. This keeps the high-level logic clean and understandable.",
                "Не засмічуйте основний граф звуковими ефектами або тригерами частинок. Використовуйте підвузли (Sub-Nodes) всередині станів для обробки цих побічних ефектів. Це зберігає логіку високого рівня чистою та зрозумілою."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Layered Blending", "Шарове змішування"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Separate movement from actions. Use one layer for locomotion (Walk/Run) and an override/additive layer for upper-body actions (Shoot/Reload). This significantly reduces the number of states needed.",
                "Відокремлюйте рух від дій. Використовуйте один шар для локомоції (ходьба/біг) і шар перекриття (override) або адитивний шар для дій верхньої частини тіла (стрільба/перезарядка)."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Reuse with Inheritance", "Перевикористання через спадкування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "When a new animation layer is mostly a variation of an existing one, create a child layer instead of copying the whole graph. Override only the states that need different clips or state-local logic.",
                "Коли новий анімаційний шар є переважно варіацією існуючого, створюйте дочірній шар замість копіювання всього графа. Перевизначайте лише ті стани, яким потрібні інші кліпи або локальна логіка."
            ));

            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Good use", "Вдалий випадок"), HonamiDocLocalization.Get("Injured locomotion inherits the normal locomotion graph and overrides only Walk and Run.", "Injured locomotion успадковує normal locomotion graph і перевизначає лише Walk та Run.")),
                (HonamiDocLocalization.Get("Avoid", "Уникайте"), HonamiDocLocalization.Get("Copying an entire layer just to replace one clip. This creates maintenance debt immediately.", "Копіювання цілого шару лише для заміни одного кліпа. Це одразу створює борг підтримки."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Variant Controllers", "Варіантні контролери"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Use Override Controllers for character families, skins or weapon loadouts. Keep the parent controller as the canonical behavior, then override nodes or add local layers only in variants.",
                "Використовуйте Override Controllers для сімейств персонажів, скінів або loadout зброї. Тримайте parent controller як канонічну поведінку, а у варіантах перевизначайте вузли або додавайте локальні шари лише там, де це потрібно."
            ));

            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "Think of your animation graph as a series of LEGO blocks. Build small, reliable pieces and combine them using layers and masks.",
                    "Думайте про свій анімаційний граф як про набір блоків LEGO. Створюйте малі надійні частини та комбінуйте їх за допомогою шарів і масок."
                ),
                HonamiDocumentationBuilder.CalloutType.Tip);
        }
    }
}
