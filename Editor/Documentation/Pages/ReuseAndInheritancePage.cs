using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class ReuseAndInheritancePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Reuse & Inheritance", "Повторне використання та спадкування");
        public string Category => HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти");
        public string SearchKeywords => "reuse inheritance override controller inherited layer child parent variation перевикористання спадкування override контролер шар";
        public int Order => 260;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("Layer inheritance vs Override Controllers — and when to pick which", "Спадкування шарів vs Override Controller — і коли що обирати"),
                    HonamiDocLocalization.Get("The Create Child Layer / Create Override workflow", "Воркфлоу Create Child Layer / Create Override"),
                    HonamiDocLocalization.Get("Building families of characters without duplicating graphs", "Побудова сімейств персонажів без дублювання графів")
                },
                new[]
                {
                    HonamiDocLocalization.Get("Blending & Layers: what a layer is", "«Blending & Layers»: що таке шар")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami is designed for reusable animation architecture. Instead of duplicating huge graphs for every character, weapon or character condition, you can reuse proven logic and override only the pieces that actually change.",
                "Honami створений для архітектури анімації з повторним використанням. Замість дублювання великих графів для кожного персонажа, зброї або стану персонажа, ви можете перевикористовувати перевірену логіку та перевизначати лише те, що справді змінюється."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Two Inheritance Levels", "Два рівні спадкування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Feature", "Фіча"), 180),
                (HonamiDocLocalization.Get("Best for", "Найкраще для"), 180),
                (HonamiDocLocalization.Get("What is inherited", "Що успадковується"), 0),
                (HonamiDocLocalization.Get("Layer Inheritance", "Спадкування шарів"), HonamiDocLocalization.Get("Variants inside one controller", "Варіанти всередині одного контролера"), HonamiDocLocalization.Get("States, default state and transitions from an earlier layer.", "Стани, дефолтний стан і переходи з попереднього шару.")),
                (HonamiDocLocalization.Get("Override Controller", "Override Controller"), HonamiDocLocalization.Get("Character or weapon variants", "Варіанти персонажів або зброї"), HonamiDocLocalization.Get("Parent layers, parameters, states and nodes, plus local additions.", "Батьківські шари, параметри, стани й вузли, плюс локальні доповнення."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Layer Inheritance Workflow", "Воркфлоу спадкування шарів"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Use layer inheritance when a new layer should behave almost exactly like an existing layer, but with a few state-level differences. The child layer receives virtual inherited states. They appear in the graph, but they are protected until you explicitly create an override.",
                "Використовуйте спадкування шарів, коли новий шар має поводитися майже так само, як існуючий, але з кількома відмінностями на рівні станів. Дочірній шар отримує віртуальні успадковані стани. Вони видимі в графі, але захищені, доки ви явно не створите override."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Right-click a layer and choose Create Child Layer.", "Відкрийте меню шару та виберіть Create Child Layer."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Select the child layer. Inherited states are shown with inherited styling.", "Виберіть дочірній шар. Успадковані стани показуються спеціальним inherited-стилем."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Select an inherited state and press Create Override when you need to edit it.", "Виберіть успадкований стан і натисніть Create Override, коли його потрібно редагувати."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Detach From Parent turns the layer back into an independent layer.", "Detach From Parent перетворює шар назад на незалежний."));

            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "A child layer inherits transitions too. If Walk transitions to Run on the parent layer, the inherited Walk on the child layer will transition to the inherited Run on the same child layer.",
                    "Дочірній шар успадковує також переходи. Якщо Walk переходить у Run на батьківському шарі, успадкований Walk на дочірньому шарі перейде в успадкований Run на цьому ж дочірньому шарі."
                ),
                HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Override Controller Workflow", "Воркфлоу Override Controller"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Use an Override Controller when the whole controller should be reused by another character or loadout. The override controller references a parent Honami controller, inherits its layers and parameters, and can replace individual nodes or add extra layers and parameters.",
                "Використовуйте Override Controller, коли цілий контролер потрібно перевикористати для іншого персонажа або loadout. Override Controller посилається на батьківський Honami Controller, успадковує його шари й параметри, та може замінювати окремі вузли або додавати власні шари й параметри."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Create it from Assets/Create/Honami Animation/Override Controller.", "Створюється через Assets/Create/Honami Animation/Override Controller."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Assign the parent controller or create the override while the parent is selected.", "Призначте parent controller або створіть override, коли parent вибраний у Project."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Use State Overrides to replace only the states that need different clips or logic.", "Використовуйте State Overrides, щоб замінити лише ті стани, яким потрібні інші кліпи або логіка."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Additional layers and parameters stay local to the override controller.", "Additional layers та parameters залишаються локальними для override controller."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Practical Examples", "Практичні приклади"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Enemy family", "Сімейство ворогів"), HonamiDocLocalization.Get("One base controller for idle/chase/attack/death. Override only attack clips for Infantry, Heavy and Boss variants.", "Один базовий контролер для idle/chase/attack/death. Перевизначайте лише attack-кліпи для Infantry, Heavy та Boss.")),
                (HonamiDocLocalization.Get("Weapon loadouts", "Набори зброї"), HonamiDocLocalization.Get("Base locomotion stays shared. Rifle and pistol override upper-body attack/reload states.", "Базова locomotion лишається спільною. Rifle та pistol перевизначають upper-body attack/reload стани.")),
                (HonamiDocLocalization.Get("Injured movement", "Поранений рух"), HonamiDocLocalization.Get("Child layer inherits locomotion transitions and overrides only Walk/Run with injured variants.", "Дочірній шар успадковує locomotion-переходи й перевизначає лише Walk/Run пораненими варіантами.")),
                (HonamiDocLocalization.Get("Regional reactions", "Локальні реакції"), HonamiDocLocalization.Get("Reuse one reaction flow across layers, but apply different masks for torso, head or arm hits.", "Перевикористовуйте один reaction-flow на різних шарах, але застосовуйте різні маски для torso, head або arm hits."))
            );

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Prefer inheritance when the structure is the same and only content changes. Prefer a separate graph when the decision logic itself becomes different.",
                "Вибирайте спадкування, коли структура та сама, а змінюється лише контент. Окремий граф краще тоді, коли змінюється сама логіка прийняття рішень."
            ));
        }
    }
}
