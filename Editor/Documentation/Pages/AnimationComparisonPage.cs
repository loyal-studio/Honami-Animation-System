using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class AnimationComparisonPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Comparison of Approaches", "Порівняння підходів");
        public string Category => HonamiDocLocalization.Get("01. Welcome & Fundamentals", "01. Вступ та Основи");
        public string SearchKeywords => "comparison mecanim animator playables tweening timeline advantages limits when to use migration retargeting порівняння переваги обмеження коли використовувати заміна міграція";
        public int Order => 30;
        public int EstimatedReadTime => 7;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "This page is an honest look at where Honami fits, where it does not, and how it relates to Unity's built-in Animator. Honami is a specialized tool built for code-driven action games — it is not a drop-in upgrade for every project, and this page is explicit about both sides.",
                "Ця сторінка — чесний погляд на те, де Honami підходить, де ні, і як він співвідноситься з вбудованим Unity Animator. Honami — це спеціалізований інструмент для екшн-ігор із керуванням з коду. Він не є універсальним апгрейдом під будь-який проєкт, і ця сторінка відверто говорить про обидві сторони."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What Honami actually is", "Чим Honami є насправді"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami is not built on top of the Animator Controller. It builds and drives a Unity PlayableGraph directly — the same low-level Playables API that Unity itself uses. It still needs a Unity Animator component as the output bridge for skeletal evaluation, culling and Root Motion, but the Controller slot stays empty because Honami replaces the AnimatorController logic entirely with its own graph, parameter store, transition engine and node library.",
                "Honami не побудований поверх Animator Controller. Він будує та керує Unity PlayableGraph напряму — тим самим низькорівневим Playables API, який використовує сам Unity. Йому все ще потрібен компонент Unity Animator як міст виводу для обчислення скелета, кулінгу та Root Motion, але слот Controller лишається порожнім, бо Honami повністю замінює логіку AnimatorController власним графом, parameter store, движком переходів і бібліотекою вузлів."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Honami vs. Unity Animator (Mecanim)", "Honami проти Unity Animator (Mecanim)"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The differences below are architectural. Anywhere the built-in Animator already does the job well, it is not listed — this table only covers where the two genuinely diverge.",
                "Різниці нижче — архітектурні. Там, де вбудований Animator і так добре справляється, воно не вказане — ця таблиця показує лише те, де підходи справді розходяться."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Area", "Аспект"), 150),
                ("Unity Animator", 0),
                ("Honami", 0),
                (HonamiDocLocalization.Get("Runtime", "Рантайм"), HonamiDocLocalization.Get("AnimatorController evaluation — a black box", "Обчислення AnimatorController — чорний ящик"), HonamiDocLocalization.Get("Own evaluation stack over a PlayableGraph you can inspect", "Власний стек обчислення над PlayableGraph, який можна інспектувати")),
                (HonamiDocLocalization.Get("Control style", "Стиль керування"), HonamiDocLocalization.Get("SetFloat / CrossFade; logic bolted onto the graph", "SetFloat / CrossFade; логіка прикручена до графа"), HonamiDocLocalization.Get("Code-first API: play/skip/pause states, runtime priority", "Code-first API: play/skip/pause станів, пріоритет у рантаймі")),
                (HonamiDocLocalization.Get("Transitions", "Переходи"), HonamiDocLocalization.Get("Authored duration and exit time; runtime control limited to CrossFade duration", "Авторська тривалість та exit time; рантайм-контроль обмежений тривалістю CrossFade"), HonamiDocLocalization.Get("Runtime-parametric: priority interrupts, victim weighting, adaptive (Smart) duration", "Рантайм-параметричні: пріоритетні переривання, victim-зважування, адаптивна (Smart) тривалість")),
                (HonamiDocLocalization.Get("Reuse", "Перевикористання"), HonamiDocLocalization.Get("Layer duplication; override controllers mostly swap clips", "Дублювання шарів; override-контролери переважно свопають кліпи"), HonamiDocLocalization.Get("Layer inheritance with virtual inherited states; override controllers inherit layers, params and states", "Спадкування шарів із virtual inherited states; override-контролери успадковують шари, параметри й стани")),
                (HonamiDocLocalization.Get("Extensibility", "Розширюваність"), HonamiDocLocalization.Get("StateMachineBehaviour for per-state logic", "StateMachineBehaviour для логіки на стан"), HonamiDocLocalization.Get("Custom node types + ScriptableObject sub-nodes (shareable assets)", "Кастомні типи вузлів + ScriptableObject-сабноди (шаровані ассети)")),
                (HonamiDocLocalization.Get("Many characters", "Багато персонажів"), HonamiDocLocalization.Get("No built-in cross-animator coordination", "Нема вбудованої координації між аніматорами"), HonamiDocLocalization.Get("Linked/Brain broadcast: react across many animators by tag, radius, wave or closest-N", "Linked/Brain broadcast: реакція на багатьох аніматорів за тегом, радіусом, хвилею чи N-найближчими")),
                (HonamiDocLocalization.Get("Debugging", "Дебаг"), HonamiDocLocalization.Get("Limited runtime visibility", "Обмежена видимість у рантаймі"), HonamiDocLocalization.Get("Live node highlighting, state progress, layer weights, transition values", "Живе підсвічування вузлів, прогрес станів, ваги шарів, значення переходів"))
            );
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Many of these traits (a PlayableGraph runtime, code-first control, allocation-free evaluation) are shared with other Playables-based animation solutions — they are not unique to Honami. What is specific to Honami is the combination: a visual node graph, a built-in node library, and the multi-character Linked layer in one package.",
                "Багато з цих рис (рантайм на PlayableGraph, code-first контроль, обчислення без алокацій) спільні з іншими анімаційними рішеннями на базі Playables — вони не унікальні для Honami. Специфічним для Honami є саме поєднання: візуальний нодовий граф, вбудована бібліотека вузлів і мультиперсонажний Linked-шар в одному пакеті."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Non-Humanoid & Exotic Rigs", "Non-Humanoid та екзотичні риги"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "First, a myth to kill: you do NOT need a Humanoid rig to blend, layer or mask animations. Unity blends, layers and masks Generic rigs perfectly well — blending happens in local space regardless of rig type. Only retargeting a clip from one skeleton onto a different one requires a Humanoid Avatar.",
                "Спершу вбиймо міф: НЕ потрібен Humanoid-риг, щоб блендити, шарувати чи маскувати анімації. Unity чудово блендить, шарує й маскує Generic-риги — бленд відбувається в local space незалежно від типу рига. Лише ретаргетинг кліпу з одного скелета на інший вимагає Humanoid Avatar."
            ), HonamiDocumentationBuilder.CalloutType.Warning);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The real friction with non-humanoid characters is authoring, not capability. A Unity Avatar Mask for a Generic rig is a manual transform tree — you tick bones by hand — and there is no body-part diagram to help. Honami's masks are bone-path native, built for arbitrary hierarchies (tentacles, weapon rigs, armor shells, split bodies), and it blends and mirrors them through its own avatar and mask atlas without any Humanoid setup. That workflow — not raw blending power — is the reason Honami is comfortable on strange silhouettes.",
                "Справжнє тертя з non-humanoid персонажами — це авторинг, а не можливості. Avatar Mask для Generic-рига в Unity — це ручне transform-дерево, де кістки тикаєш вручну, без діаграми частин тіла на допомогу. Маски Honami — bone-path native, зроблені під довільні ієрархії (тентаклі, weapon rigs, armor shells, split bodies), і він блендить та віддзеркалює їх через власний avatar і mask atlas без жодного Humanoid-сетапу. Саме цей воркфлоу — а не сира потужність бленду — робить Honami зручним на дивних силуетах."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What Honami does not do", "Чого Honami не робить"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "No Humanoid muscle-space retargeting at runtime. Honami binds animation by transform path, so a clip authored on one skeleton will not automatically play on a different one. Marketplace Humanoid clips are still supported: the Humanoid Baker tool (Window ▸ Honami ▸ Tools) retargets them onto your skeleton once in the editor and bakes them to Generic clips.",
                "Нема Humanoid muscle-space ретаргетингу в рантаймі. Honami прив'язує анімацію по transform-шляху, тож кліп, зроблений на одному скелеті, не заграє автоматично на іншому. Куплені Humanoid-кліпи все ж підтримуються: тулза Humanoid Baker (Window ▸ Honami ▸ Tools) один раз ретаргетить їх на ваш скелет у редакторі й бейкає в Generic-кліпи."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Blend Trees are 1D only. There is no 2D blend space. For strafe-style locomotion, decompose into a signed 1D parameter plus states or layers.",
                "Blend Tree лише 1D. 2D blend space немає. Для страйф-локомоції розкладайте на знаковий 1D-параметр плюс стани чи шари."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Overlays (aiming, holding) are done with masked layers. Additive / aim-offset blending is not built in yet.",
                "Оверлеї (прицілювання, тримання) робляться масковими шарами. Additive / aim-offset бленду поки нема з коробки."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Code Comparison", "Порівняння в коді"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Unity Animator style
animator.SetFloat(""Speed"", speed);
animator.SetTrigger(""Reload"");
animator.CrossFade(""Reload"", 0.12f, 1);

// Honami style
honami.SetFloat(""Speed"", speed);
honami.PlayState(""Reload"", transitionDuration: 0.12f, layer: 1, forceRestart: true);");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Honami vs. Tweening & Timeline", "Honami проти твінів і Timeline"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Tweens (DOTween/LeanTween) are great for UI panels, pickups and simple prop motion, but they have no skeletal blending, masking or state logic. Timeline is for non-interactive cutscenes with fixed timing. Reach for Honami when the pose must blend with locomotion, masks, transitions, Root Motion or state tags and react instantly to input.",
                "Твіни (DOTween/LeanTween) чудові для UI-панелей, підбираних предметів і простого руху props, але в них нема скелетного бленду, маскування чи логіки станів. Timeline — для неінтерактивних катсцен із фіксованим таймінгом. Бери Honami, коли поза має змішуватися з локомоцією, масками, переходами, Root Motion чи state tags і миттєво реагувати на введення."
            ));
        }
    }
}
