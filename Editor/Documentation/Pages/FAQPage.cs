using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class FAQPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("FAQ", "Часті питання");
        public string Category => HonamiDocLocalization.Get("02. Getting Started", "02. Перші Кроки");
        public string SearchKeywords => "faq questions help support issues common humanoid mixamo asset store marketplace retarget часті питання допомога куплені кліпи";
        public int Order => 120;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Find answers to the most common technical questions about Honami Animation System.",
                "Знайдіть відповіді на найпоширеніші технічні питання про Honami Animation System."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Setup & Basics", "Налаштування та Основи"), HonamiEditorIcons.Controller);

            AddQA(root,
                HonamiDocLocalization.Get("Is Honami just a wrapper over Unity's Animator?", "Чи є Honami просто надбудовою (wrapper) над Unity Animator?"),
                HonamiDocLocalization.Get("NO. This is a common misconception. Honami is a completely independent, high-performance animation engine. It replaces Unity's legacy animation logic entirely with its own evaluation stack, blending engine, and sampling clock. We only use the standard Animator component as a 'silent bridge' to access low-level engine features like Root Motion and bone transforms.", "НІ. Це поширена помилка. Honami — це повністю незалежний, високопродуктивний анімаційний рушій. Він цілком замінює застарілу логіку анімації Unity власною системою обчислень (evaluation stack), рушієм змішування та годинником вибірки. Ми використовуємо стандартний компонент Animator лише як «тихий міст» для доступу до низькорівневих функцій рушія, таких як Root Motion та трансформи кісток."));

            AddQA(root,
                HonamiDocLocalization.Get("Why is the standard Animator component still required?", "Чому стандартний компонент Animator все ще потрібен?"),
                HonamiDocLocalization.Get("The Animator component acts as a low-level bridge to Unity's engine. It provides the necessary infrastructure for bone mapping, Root Motion calculations, and visibility culling that Honami utilizes for its high-level logic.", "Компонент Animator працює як низькорівневий міст до рушія Unity. Він забезпечує необхідну інфраструктуру для мапінгу кісток, розрахунку Root Motion та кулінгу видимості, які Honami використовує для своєї високорівневої логіки."));

            AddQA(root,
                HonamiDocLocalization.Get("Why should the Animator's Controller slot be EMPTY?", "Чому слот Controller в компоненті Animator має бути ПОРОЖНІМ?"),
                HonamiDocLocalization.Get("Honami takes full control over the animation stream. Keeping the slot empty prevents Unity's legacy state machine from wasting CPU cycles while still allowing the Animator component to handle Root Motion and bone transforms.", "Honami повністю контролює потік анімації. Порожній слот запобігає витратам ресурсів CPU застарілою машиною станів Unity, при цьому дозволяючи компоненту Animator обробляти Root Motion та скелет."));

            AddQA(root,
                HonamiDocLocalization.Get("Does Honami work with Root Motion?", "Чи працює Honami з Root Motion?"),
                HonamiDocLocalization.Get("Yes. Enable 'Apply Root Motion' in the Honami Animator's Animator Synchronization section — it is forwarded to the underlying Animator, which feeds delta-movement into Unity as usual.", "Так. Увімкніть «Apply Root Motion» у секції Animator Synchronization компонента Honami Animator — налаштування передається базовому Animator, який як зазвичай передає дельта-переміщення в Unity."));

            AddQA(root,
                HonamiDocLocalization.Get("Can I use Humanoid animations from the Asset Store or Mixamo?", "Чи можу я використовувати Humanoid-анімації з Asset Store або Mixamo?"),
                HonamiDocLocalization.Get("Yes, through the Humanoid Baker (Window ▸ Honami ▸ Tools ▸ Honami Humanoid Baker). Honami itself has no runtime muscle-space retargeting, so raw Humanoid clips will not play — instead, the Baker retargets them onto your character once in the editor and saves ordinary Generic clips authored for your skeleton. Those baked clips work with every Honami feature: masks, mirroring, layers and rigs. Your character simply stays Humanoid — do not convert the model to Generic, just keep the Avatar assigned in the Unity Animator's Avatar field. 'Generic' refers to the baked clips, not the model import type.", "Так, через Humanoid Baker (Window ▸ Honami ▸ Tools ▸ Honami Humanoid Baker). Сам Honami не має muscle-space ретаргетингу в рантаймі, тож сирі Humanoid-кліпи не заграють — натомість бейкер один раз ретаргетить їх на вашого персонажа в редакторі та зберігає звичайні Generic-кліпи під ваш скелет. Запечені кліпи працюють з усіма фічами Honami: масками, mirroring, шарами та ригами. Персонаж просто лишається Humanoid — не конвертуйте модель у Generic, лише тримайте аватар призначеним у полі Avatar компонента Unity Animator. «Generic» стосується запечених кліпів, а не типу імпорту моделі."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Performance & Mobile", "Продуктивність та Mobile"), HonamiEditorIcons.TimelineWhite);

            AddQA(root,
                HonamiDocLocalization.Get("Is Honami ready for mobile/low-end devices?", "Чи підходить Honami для мобільних пристроїв?"),
                HonamiDocLocalization.Get("Absolutely. Honami is built with a zero-allocation philosophy using Span<T> and high-performance C# code. It is significantly faster and lighter than the legacy built-in animation system.", "Безумовно. Honami побудований на філософії zero-allocation з використанням Span<T> та високопродуктивного коду. Він значно швидший та легший за вбудовану застарілу систему анімації."));

            AddQA(root,
                HonamiDocLocalization.Get("How many animators can I have on screen?", "Скільки аніматорів може бути на екрані одночасно?"),
                HonamiDocLocalization.Get("Thanks to the optimized blending engine, you can run hundreds of complex Honami animators with minimal impact on the main thread.", "Завдяки оптимізованому рушію змішування, ви можете запускати сотні складних аніматорів Honami з мінімальним впливом на основний потік (Main Thread)."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Advanced Workflow", "Просунутий Воркфлоу"), HonamiEditorIcons.GraphWhite);

            AddQA(root,
                HonamiDocLocalization.Get("Can I use custom C# logic within the graph?", "Чи можу я використовувати кастомну C# логіку всередині графа?"),
                HonamiDocLocalization.Get("Yes! You can create custom Sub-Nodes or even entirely new Node types to extend Honami's functionality for your specific gameplay needs.", "Так! Ви можете створювати кастомні підвузли (Sub-Nodes) або навіть цілком нові типи вузлів для розширення функціональності Honami під ваші конкретні геймплейні потреби."));

            AddQA(root,
                HonamiDocLocalization.Get("Can I control the animation update loop manually?", "Чи можу я керувати циклом оновлення анімації вручну?"),
                HonamiDocLocalization.Get("Yes. By setting the Update Mode to 'Manual', you can drive the animation from your own C# scripts using the Tick(deltaTime) method. This is ideal for complex networking synchronization or custom simulation steps.", "Так. Встановивши Update Mode у значення «Manual», ви можете керувати анімацією з власних C#-скриптів за допомогою методу Tick(deltaTime). Це ідеально підходить для складної мережевої синхронізації або кастомних кроків симуляції."));

            AddQA(root,
                HonamiDocLocalization.Get("What is the difference between layer inheritance and an Override Controller?", "Яка різниця між спадкуванням шарів та Override Controller?"),
                HonamiDocLocalization.Get("Layer inheritance is for variations inside one controller: a child layer reuses the states and transitions of an earlier layer, then overrides only specific states. Override Controllers are for variants of an entire controller: another character, weapon set, skin, or enemy type can inherit the parent controller and replace nodes or add local layers and parameters.", "Спадкування шарів потрібне для варіацій всередині одного контролера: дочірній шар перевикористовує стани й переходи попереднього шару, а потім перевизначає лише конкретні стани. Override Controllers потрібні для варіантів цілого контролера: інший персонаж, набір зброї, скін або тип ворога може успадкувати parent controller і замінити вузли або додати локальні шари та параметри."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Technical Deep Dive (For Skeptics)", "Технічний Deep Dive (Для скептиків)"), HonamiEditorIcons.GraphWhite);

            AddQA(root,
                HonamiDocLocalization.Get("Isn't this just a wrapper over Unity's Playables API?", "Хіба це не просто обгортка над Unity Playables API?"),
                HonamiDocLocalization.Get("NO. While we use the Playables API as a low-level output stream, Honami implements its own entire Graph Runtime. We don't rely on Unity's default AnimatorController logic or its internal state machine. Every weight calculation, transition curve, and sub-node logic is executed by Honami's custom-built evaluation stack, which is optimized for zero-allocation performance.", "НІ. Хоча ми використовуємо Playables API як низькорівневий потік виводу, Honami реалізує власний повноцінний Graph Runtime. Ми не покладаємося на дефолтну логіку AnimatorController або внутрішню машину станів Unity. Кожне обчислення ваги, крива переходу та логіка підвузлів виконуються кастомним стеком обчислень Honami, оптимізованим для роботи без алокацій."));

            AddQA(root,
                HonamiDocLocalization.Get("Does it suffer from the same 'popping' and frame-rate issues as the legacy system?", "Чи має система ті ж проблеми з «поппінгом» та частотою кадрів, що й застаріла система?"),
                HonamiDocLocalization.Get("Absolutely not. The legacy system is notoriously frame-dependent and prone to 'popping' during sudden state changes. Honami samples animations on a high-precision double clock and offers Victim and Smart transitions to keep interrupts smooth, ensuring continuity even if the game's frame rate drops or fluctuates.", "Зовсім ні. Застаріла система сумнозвісна своєю залежністю від частоти кадрів та схильністю до «поппінгу» при раптових змінах станів. Honami семплить анімації за високоточним double-годинником і пропонує переходи Victim та Smart для плавних переривань, гарантуючи безперервність навіть при падінні чи коливаннях FPS."));

            AddQA(root,
                HonamiDocLocalization.Get("What about GC pressure? Every new graph must be an object, right?", "А як щодо навантаження на GC? Кожен новий граф — це ж об'єкт, так?"),
                HonamiDocLocalization.Get("Incorrect. Honami is built with a data-oriented mindset. We use static buffers and reusable memory pools. During the runtime update loop, Honami performs zero new allocations. Even complex blending trees with dozens of nodes run entirely on pre-allocated memory blocks using Span<T> for high-speed bone transform manipulation.", "Неправильно. Honami побудований з орієнтацією на дані (data-oriented). Ми використовуємо статичні буфери та пули пам'яті, що повторно використовуються. Під час циклу оновлення Honami виконує нуль нових алокацій. Навіть складні дерева змішування з десятками вузлів працюють виключно на заздалегідь виділених блоках пам'яті, використовуючи Span<T> для швидкої маніпуляції трансформ-даними кісток."));

            HonamiDocumentationBuilder.AddSeparator(root);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Still think it's just a wrapper? Open the source code and check the Evaluator logic yourself. Facts don't lie.",
                "Все ще думаєте, що це просто обгортка? Відкрийте вихідний код і перевірте логіку Evaluator самі. Факти не брешуть."
            ));
        }

        private void AddQA(VisualElement root, string q, string a)
        {
            var container = new VisualElement();
            container.style.marginBottom = 24;
            container.style.paddingLeft = 12;
            container.style.borderLeftWidth = 2;
            container.style.borderLeftColor = HonamiGraphStyles.Accent;

            var qLabel = new Label(q);
            qLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            qLabel.style.color = Color.white;
            qLabel.style.fontSize = 15;
            qLabel.style.marginBottom = 6;
            container.Add(qLabel);

            var aLabel = new Label(a);
            aLabel.style.whiteSpace = WhiteSpace.Normal;
            aLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            aLabel.style.fontSize = 13;
            container.Add(aLabel);

            root.Add(container);
        }
    }
}
