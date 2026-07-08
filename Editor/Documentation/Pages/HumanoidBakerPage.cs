using HonamiAnimationSystem.Editor.Windows;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class HumanoidBakerPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Humanoid Baker", "Бейкер Humanoid");
        public string Category => HonamiDocLocalization.Get("03. Editor & Workflow", "03. Редактор та Воркфлоу");
        public string SearchKeywords => "humanoid baker retarget retargeting bake generic mixamo asset store marketplace muscle space clips convert бейк бейкер ретаргет ретаргетинг куплені кліпи маркетплейс конвертація";
        public int Order => 315;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Humanoid Baker brings marketplace animation packs (Asset Store, Mixamo, motion libraries) into Honami. It retargets Humanoid clips onto your character once, in the editor, and saves the result as ordinary Generic clips that Honami plays natively — with masks, mirroring and rigs fully working.",
                "Humanoid Baker відкриває Honami для анімаційних паків з маркетплейсів (Asset Store, Mixamo, motion-бібліотеки). Він один раз, у редакторі, ретаргетить Humanoid-кліпи на вашого персонажа і зберігає результат як звичайні Generic-кліпи, які Honami грає нативно — з повністю робочими масками, mirroring та ригами."
            ));

            HonamiDocumentationBuilder.AddActionButton(root,
                HonamiDocLocalization.Get("Open Humanoid Baker", "Відкрити Humanoid Baker"),
                HonamiEditorIcons.BlendTreeWhite,
                HonamiHumanoidBakerWindow.ShowWindow);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The Problem It Solves", "Проблема, яку він вирішує"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A Humanoid clip does not store bone rotations — it stores an abstract Muscle Space pose ('the spine is bent 40% of its range'). Turning that back into concrete bone movement requires Mecanim's retargeting layer. Honami plays clips directly by transform path and deliberately has no such layer, so a raw Humanoid clip cannot drive a Honami state.",
                "Humanoid-кліп не зберігає повороти кісток — він зберігає абстрактну позу в Muscle Space («хребет зігнутий на 40% свого діапазону»). Щоб перетворити це назад у рух конкретних кісток, потрібен шар ретаргетингу Mecanim. Honami грає кліпи напряму по transform-шляхах і свідомо не має такого шару, тому сирий Humanoid-кліп не може керувати Honami-стейтом."
            ));
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "The Baker's trick: Unity already knows how to retarget a Humanoid clip onto any model with a valid Humanoid Avatar. So the expensive, Honami-incompatible retargeting happens exactly once in the editor — and the output is a plain Generic clip authored for YOUR skeleton, indistinguishable from a clip you animated by hand.",
                "Трюк бейкера: Unity і так вміє ретаргетити Humanoid-кліп на будь-яку модель з валідним Humanoid Avatar. Тож дорогий і несумісний з Honami ретаргетинг відбувається рівно один раз у редакторі — а на виході звичайний Generic-кліп, створений під ВАШ скелет, який не відрізнити від анімації, зробленої вручну."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What Happens When You Press Bake", "Що відбувається після натискання Bake"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "For each clip in the list, the tool performs one invisible 'play and record' pass. It takes a fraction of a second and everything happens in memory:",
                "Для кожного кліпу зі списку тулза виконує один невидимий прохід «програй і запиши». Він триває долі секунди, і все відбувається в пам'яті:"));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Step 1 — a temporary clone. The tool spawns a throwaway copy of the Target Character to play the animation on. Your original prefab, FBX and scene are never touched or modified. The clone does not appear in the Hierarchy, is never saved anywhere, and is deleted automatically the moment the clip finishes baking.",
                "Крок 1 — тимчасовий клон. Тулза створює одноразову копію Target Character, на якій буде програно анімацію. Ваш оригінальний префаб, FBX і сцена не чіпаються і не змінюються. Клон не з'являється в Hierarchy, ніде не зберігається і автоматично видаляється, щойно кліп добейкався."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Step 2 — scripts on the clone are switched off. Components like Foot IK, Look At or physics constraints run even in the editor and would add their own corrections on top of the animation. We want to record the clean retargeted motion only, so on the clone (and only on the clone) they are disabled.",
                "Крок 2 — скрипти на клоні вимикаються. Компоненти на кшталт Foot IK, Look At чи фізичних констрейнів працюють навіть у редакторі й додавали б свої поправки поверх анімації. Нам треба записати лише чистий ретаргетнутий рух, тому на клоні (і тільки на ньому) вони вимикаються."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Step 3 — the clip plays on the clone. Unity's own Humanoid retargeting maps the source motion onto your skeleton — exactly what Mecanim would do at runtime, just once and offline.",
                "Крок 3 — кліп грає на клоні. Рідний Humanoid-ретаргетинг Unity мапить вихідний рух на ваш скелет — рівно те, що Mecanim робив би в рантаймі, тільки один раз і офлайн."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Step 4 — the pose is recorded frame by frame. At every sample step (30 times per animation second by default) the tool reads where each bone of the clone actually ended up and stores its position and rotation.",
                "Крок 4 — поза записується кадр за кадром. На кожному кроці семплінгу (за замовчуванням 30 разів на секунду анімації) тулза зчитує, де реально опинилась кожна кістка клона, і зберігає її позицію та ротацію."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Step 5 — a normal .anim file is written. The recorded values become animation curves bound to your bone names, loop settings are copied from the source clip, and bones that never moved are stored as cheap two-key curves. The result is an ordinary Generic clip — as if it had been animated for this skeleton by hand.",
                "Крок 5 — записується звичайний .anim-файл. Записані значення стають анімаційними кривими, прив'язаними до імен ваших кісток, loop-налаштування копіюються з вихідного кліпу, а кістки, що не рухалися, зберігаються дешевими кривими з двох ключів. Результат — звичайний Generic-кліп, ніби його анімували під цей скелет вручну."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Workflow", "Воркфлоу"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "One-time setup: the character model must be imported with Animation Type = Humanoid and a valid Avatar. If your characters are already Humanoid — and most existing projects are — nothing changes: keep them exactly as they are.",
                "Одноразовий сетап: модель персонажа має бути імпортована з Animation Type = Humanoid і валідним Avatar. Якщо ваші персонажі вже Humanoid — а в більшості наявних проєктів так і є — нічого не змінюється: лишіть їх як є."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Open Window ▸ Honami ▸ Tools ▸ Honami Humanoid Baker, or right-click clips in the Project window and choose 'Bake Humanoid Clips to Generic (Honami)'.",
                "Відкрийте Window ▸ Honami ▸ Tools ▸ Honami Humanoid Baker, або правий клік по кліпах у Project — «Bake Humanoid Clips to Generic (Honami)»."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Assign the Target Character — a prefab, a scene object, or the character FBX dragged straight from the Project window. The clip tools unlock once the target has a valid Humanoid Avatar; if the FBX is not imported as Humanoid yet, the window offers a one-click switch.",
                "Вкажіть Target Character — префаб, об'єкт сцени або сам FBX персонажа, перетягнутий прямо з Project. Інструменти кліпів розблокуються, коли ціль має валідний Humanoid Avatar; якщо FBX ще не імпортований як Humanoid, вікно запропонує перемкнути одним кліком."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Fill the clip list: Auto Fill scans the whole project for Humanoid clips (including clips inside FBX files), or use Add Selected Clips for manual control.",
                "Наповніть список кліпів: Auto Fill сканує весь проєкт на Humanoid-кліпи (включно з кліпами всередині FBX), або додайте вручну через Add Selected Clips."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Press Bake to Generic Clips, pick an output folder, and drop the resulting .anim files into Honami states like any other clip.",
                "Натисніть Bake to Generic Clips, оберіть папку і кидайте отримані .anim-файли в Honami-стейти як будь-які інші кліпи."));

            HonamiDocumentationBuilder.AddWarning(root, HonamiDocLocalization.Get(
                "Do NOT convert your character model to Generic. 'Generic' describes the baked clips (transform-path curves), not the model import type. The character stays Humanoid at runtime — the only requirement is that the Unity Animator component keeps its Avatar assigned in the Avatar field. Switching an established Humanoid character to Generic import can break existing architecture: avatar references, masks, ragdolls and prefab bindings.",
                "НЕ конвертуйте модель персонажа в Generic. «Generic» описує запечені кліпи (transform-path криві), а не тип імпорту моделі. Персонаж лишається Humanoid у рантаймі — єдина вимога: у компоненті Unity Animator в полі Avatar має стояти призначений аватар. Перемикання усталеного Humanoid-персонажа на Generic-імпорт може зламати наявну архітектуру: посилання на аватар, маски, регдоли та прив'язки префабів."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Settings", "Налаштування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Use Clip Frame Rate", "Use Clip Frame Rate"), HonamiDocLocalization.Get("Samples at the source clip's own frame rate. Disable to set a custom rate.", "Семплінг з frame rate вихідного кліпу. Вимкніть, щоб задати кастомний rate.")),
                (HonamiDocLocalization.Get("Apply Foot IK", "Apply Foot IK"), HonamiDocLocalization.Get("Lets Unity's retargeter correct foot contacts during the bake so feet do not slide or sink. Keep it on for locomotion.", "Дозволяє ретаргетеру Unity виправляти контакт стоп під час бейку, щоб ноги не ковзали й не провалювались. Для локомоції тримайте увімкненим.")),
                (HonamiDocLocalization.Get("Root Motion: Discard", "Root Motion: Discard"), HonamiDocLocalization.Get("The character animates in place — the right choice for code-driven movement.", "Персонаж анімується на місці — правильний вибір для руху, керованого кодом.")),
                (HonamiDocLocalization.Get("Root Motion: Bake Into Hips", "Root Motion: Bake Into Hips"), HonamiDocLocalization.Get("Travel from the clip is written into the top-level bones, so the motion lives inside the skeleton.", "Переміщення з кліпу записується в кістки верхнього рівня, тож рух живе всередині скелета.")),
                (HonamiDocLocalization.Get("Bake Scale Curves", "Bake Scale Curves"), HonamiDocLocalization.Get("Also records localScale. Rarely needed for humanoid motion.", "Додатково записує localScale. Для humanoid-руху потрібно рідко.")),
                (HonamiDocLocalization.Get("Compress Constant Curves", "Compress Constant Curves"), HonamiDocLocalization.Get("Bones that never move keep a two-key pinning curve instead of a full track, shrinking the asset.", "Кістки, що не рухаються, отримують pinning-криву з двох ключів замість повного треку — ассет менший.")),
                (HonamiDocLocalization.Get("Output Suffix", "Output Suffix"), HonamiDocLocalization.Get("Appended to baked file names, '_Generic' by default.", "Додається до імен файлів, за замовчуванням «_Generic».")));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Limitations & Tips", "Обмеження та поради"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "A baked clip is authored for one specific skeleton. Several different skeletons in the project means one bake pass per skeleton.",
                "Запечений кліп створений під один конкретний скелет. Кілька різних скелетів у проєкті — один прогін бейку на кожен."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Only clips imported as Humanoid are visible to Auto Fill and the bake. If a pack sits in the project as Generic, switch its import Animation Type first.",
                "Auto Fill та бейк бачать лише кліпи, імпортовані як Humanoid. Якщо пак лежить у проєкті як Generic — спершу перемкніть Animation Type в імпорті."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Finger and toe detail depends on how completely the source clip and your Avatar map those bones — inspect a baked clip before batch-processing a whole pack.",
                "Деталізація пальців залежить від того, наскільки повно вихідний кліп і ваш Avatar мапують ці кістки — перевірте один запечений кліп, перш ніж проганяти цілий пак."));
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Baking is also a performance upgrade: the runtime plays cheap Generic curves instead of paying the per-frame Humanoid retargeting cost that Mecanim projects carry forever.",
                "Бейк — це ще й апгрейд продуктивності: рантайм грає дешеві Generic-криві замість вічної покадрової плати за Humanoid-ретаргетинг, яку несуть Mecanim-проєкти."));
        }
    }
}
