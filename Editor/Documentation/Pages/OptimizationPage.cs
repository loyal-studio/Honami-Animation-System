using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class OptimizationPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Optimization & Performance", "Оптимізація та продуктивність");
        public string Category => HonamiDocLocalization.Get("07. Tools & Pro", "07. Інструменти та Pro");
        public string SearchKeywords => "optimization performance profile garbage collection zero-alloc пам'ять оптимізація";
        public int Order => 640;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami is designed from the ground up for high performance. However, there are still best practices you should follow to keep your game running smoothly.",
                "Honami від самого початку розроблявся для високої продуктивності. Проте існують перевірені практики, яких варто дотримуватися, щоб ваша гра працювала плавно."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Zero-Allocation Runtime", "Рантайм без алокацій"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The core evaluation engine of Honami produces zero garbage (GC) during the update loop. This is achieved using modern C# features like Span<T> and careful memory management.",
                "Основний рушій обчислень Honami не створює «сміття» (Garbage Collection) під час циклу оновлення. Це досягається завдяки сучасним можливостям C#, таким як Span<T>, та ретельному управлінню пам'яттю."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Best Practices", "Найкращі практики"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Culling: set Culling Mode on the Honami Animator; it is forwarded to the underlying Animator. 'Cull Completely' or 'Cull Update Transforms' can save significant CPU time for distant characters.", "Кулінг (Culling): задайте Culling Mode на Honami Animator; він передається базовому Animator. «Cull Completely» або «Cull Update Transforms» суттєво економлять CPU для віддалених персонажів."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("FPS Cap: enable the built-in FPS Cap (with interpolation) on background characters — evaluating a crowd at 15-30 FPS is the cheapest LOD you can get.", "FPS Cap: вмикайте вбудований FPS Cap (з інтерполяцією) на фонових персонажах — оцінка натовпу на 15-30 FPS це найдешевший можливий LOD."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Layer Count: Keep the number of active layers reasonable. Each active layer adds a small amount of blending overhead.", "Кількість шарів: Тримайте кількість активних шарів у розумних межах. Кожен активний шар додає невелике навантаження на змішування."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Parameter Access: cache IDs with HonamiAnimator.StringToHash() and use the int overloads in Update. Honami uses its own hash — do NOT mix in Unity's Animator.StringToHash values.", "Доступ до параметрів: кешуйте ID через HonamiAnimator.StringToHash() і використовуйте int-перевантаження в Update. Honami має власний хеш — НЕ підмішуйте значення з Animator.StringToHash Unity."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Pause what the player cannot see: Pause() or PauseLayer() on off-screen characters costs one bool check per tick; StopAndKeepPose() freezes corpses in place for free.", "Ставте на паузу те, чого гравець не бачить: Pause() чи PauseLayer() на позаекранних персонажах коштує одну перевірку bool за тік; StopAndKeepPose() безкоштовно заморожує тіла на місці."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Group queries: use brain.GetLinkedAnimatorsNonAlloc(span) instead of LINQ over LinkedAnimators when scanning squads every frame.", "Групові запити: використовуйте brain.GetLinkedAnimatorsNonAlloc(span) замість LINQ по LinkedAnimators при щокадровому скануванні загонів."));

            HonamiDocumentationBuilder.AddWarning(root, HonamiDocLocalization.Get(
                "Avoid complex Blend Trees with dozens of clips on every character in a massive crowd. For crowds, consider using simpler setups or LODs.",
                "Уникайте складних дерев змішування з десятками кліпів для кожного персонажа у величезному натовпі. Для натовпів використовуйте простіші налаштування або LOD."
            ));
        }
    }
}
