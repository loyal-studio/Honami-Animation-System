using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class OverviewPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Welcome to Honami", "Ласкаво просимо до Honami");
        public string Category => HonamiDocLocalization.Get("01. Start Here", "01. Старт");
        public string SearchKeywords => "welcome introduction overview map start reading order where to begin вітання вступ огляд карта з чого почати";
        public int Order => 10;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami is a node-based animation system for Unity. You build a state graph (states, transitions, blend trees) in a visual editor and drive it from gameplay code through the HonamiAnimator component. Under the hood it bypasses the built-in Animator Controller entirely and constructs its own PlayableGraph at runtime.",
                "Honami — це вузлова система анімації для Unity. Ви будуєте граф станів (стейти, транзішни, blend tree) у візуальному редакторі та керуєте ним з ігрового коду через компонент HonamiAnimator. Під капотом система повністю обходить вбудований Animator Controller і будує власний PlayableGraph у рантаймі."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "New to Honami? Read '01. Start Here' and then work through the '02. Tutorial' pages in order — by the end you will have a character with locomotion, a jump and footstep events. Everything else in these docs is reference you can visit when you need it.",
                "Вперше з Honami? Прочитайте розділ «01. Старт» і пройдіть сторінки «02. Туторіал» по порядку — в кінці у вас буде персонаж із локомоушном, стрибком та івентами кроків. Усе інше в цій документації — довідник, до якого можна звертатися за потреби."
            ), HonamiDocumentationBuilder.CalloutType.Tip);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("How this documentation is organized", "Як влаштована ця документація"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Sections are numbered in the recommended reading order. Sections 01–03 are meant to be read sequentially; sections 04–09 are reference material.",
                "Розділи пронумеровані в рекомендованому порядку читання. Розділи 01–03 варто читати послідовно; розділи 04–09 — довідковий матеріал."
            ));

            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("01. Start Here", "01. Старт"), HonamiDocLocalization.Get("Component setup and the first launch of a character.", "Налаштування компонентів і перший запуск персонажа.")),
                (HonamiDocLocalization.Get("02. Tutorial", "02. Туторіал"), HonamiDocLocalization.Get("A step-by-step build of your first character: states, locomotion, transitions, events. One concept per page.", "Покрокове створення першого персонажа: стейти, локомоушн, транзішни, івенти. Один концепт на сторінку.")),
                (HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти"), HonamiDocLocalization.Get("How the system actually works: parameters, transitions, blending and layers, events, avatars, graph reuse.", "Як система працює насправді: параметри, транзішни, блендінг і шари, івенти, аватари, перевикористання графів.")),
                (HonamiDocLocalization.Get("04–05. Nodes & Sub-Nodes", "04–05. Вузли та підвузли"), HonamiDocLocalization.Get("Reference for every node and sub-node type. Look things up as you need them.", "Довідник по кожному типу вузлів та підвузлів. Відкривайте за потреби.")),
                (HonamiDocLocalization.Get("06. Honami Linked", "06. Honami Linked"), HonamiDocLocalization.Get("Orchestrating multiple characters together: Brain, global actions, targeting.", "Оркестрація кількох персонажів разом: Brain, глобальні дії, таргетинг.")),
                (HonamiDocLocalization.Get("07. Tools & Pro", "07. Інструменти та Pro"), HonamiDocLocalization.Get("Rig system, IK, Timeline integration, Humanoid Baker, performance tuning.", "Система рігу, IK, інтеграція з Timeline, Humanoid Baker, оптимізація.")),
                (HonamiDocLocalization.Get("08. Developer API", "08. API для розробників"), HonamiDocLocalization.Get("The full C# surface: playback control, queries, state tags.", "Повний C#-інтерфейс: керування відтворенням, запити, теги станів.")),
                (HonamiDocLocalization.Get("09. Theory & Background", "09. Теорія та довідка"), HonamiDocLocalization.Get("Optional reading: animation theory, comparisons with other approaches, exporting tips, FAQ. Not required to use Honami.", "Необов'язкове читання: теорія анімації, порівняння з іншими підходами, поради з експорту, FAQ. Для роботи з Honami не є обов'язковим."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Where should you start?", "З чого почати саме вам?"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("I'm new here", "Я тут вперше"), HonamiDocLocalization.Get("First Steps → Tutorial pages 1 to 4, in order. About 30 minutes total.", "First Steps → сторінки туторіалу 1–4, по порядку. Разом близько 30 хвилин."), HonamiEditorIcons.GraphWhite),
                (HonamiDocLocalization.Get("I'm an animator", "Я аніматор"), HonamiDocLocalization.Get("After the tutorial: Node Library, Event System, and the Animator's Guide in Theory & Background.", "Після туторіалу: Бібліотека вузлів, Система подій та «Посібник аніматора» в розділі теорії."), HonamiEditorIcons.TimelineWhite),
                (HonamiDocLocalization.Get("I'm a programmer", "Я програміст"), HonamiDocLocalization.Get("After the tutorial: Parameters, Transitions, then the Scripting API reference.", "Після туторіалу: Параметри, Транзішни, далі довідник Scripting API."), HonamiEditorIcons.Controller),
                (HonamiDocLocalization.Get("I need one specific thing", "Мені потрібна конкретна річ"), HonamiDocLocalization.Get("Use the search field in the toolbar — it matches titles, categories and keywords.", "Скористайтеся пошуком у тулбарі — він шукає по назвах, категоріях і ключових словах."), HonamiEditorIcons.BlendTreeWhite)
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Quick Actions", "Швидкі дії"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddActionGroup(root,
                (HonamiDocLocalization.Get("Create New Controller", "Створити новий контролер"), HonamiEditorIcons.TimelineWhite, () => HonamiDocumentationBuilder.CreateController()),
                (HonamiDocLocalization.Get("Open Graph Editor", "Відкрити редактор графа"), HonamiEditorIcons.GraphWhite, () => HonamiGraphWindow.OpenWindow())
            );
        }
    }
}
