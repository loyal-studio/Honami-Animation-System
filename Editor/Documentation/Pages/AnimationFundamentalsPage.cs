using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class AnimationFundamentalsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Animation Fundamentals", "Основи анімації");
        public string Category => HonamiDocLocalization.Get("09. Theory & Background", "09. Теорія та довідка");
        public string SearchKeywords => "fundamentals theory logic states transitions normalized time poses основы теорія логіка";
        public int Order => 800;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Before diving into the tools, it's crucial to understand the underlying logic of game animation. In Honami, we treat animation not as a series of movie clips, but as a dynamic data stream that responds to the game world.",
                "Перш ніж переходити до інструментів, важливо зрозуміти фундаментальну логіку ігрової анімації. У Honami ми розглядаємо анімацію не як набір відеороликів, а як динамічний потік даних, що реагує на ігровий світ."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What is an Animation Graph Really?", "Що таке граф анімації насправді?"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A Graph is far more than just a visual tool for playing clips; it is a profound logic engine. It represents a continuous Decision Tree for Motion. Sixty times a second, it answers a fundamental question: 'Given the character's current velocity, the player's input, and the physics of the world, what exact pose should the skeleton assume right now?'",
                "Граф — це набагато більше, ніж просто візуальний інструмент для відтворення кліпів; це потужний логічний рушій. Він є безперервним деревом рішень для руху. Шістдесят разів на секунду він відповідає на фундаментальне питання: «Враховуючи поточну швидкість персонажа, введення гравця та фізику світу, яку саме позу має прийняти скелет прямо зараз?»"
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Key Terminology", "Ключова термінологія"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Term", "Термін"), 150),
                (HonamiDocLocalization.Get("Definition", "Визначення"), 0),
                ("", 0),
                ("State", HonamiDocLocalization.Get("A logical condition of the character (e.g., Idle, Running). It holds either a single animation clip or a complex blend tree.", "Логічний стан персонажа (наприклад, Idle, Running). Він містить або один анімаційний кліп, або складне дерево змішування."), ""),
                ("Transition", HonamiDocLocalization.Get("The connective 'Bridge' between states that defines exactly when and how (duration, curve) to blend from one animation to the next.", "Логічний «міст» між станами, який визначає, коли саме та як (тривалість, крива) змішувати одну анімацію з наступною."), ""),
                ("Normalized Time", HonamiDocLocalization.Get("Time expressed as a value from 0.0 to 1.0, regardless of the actual length of the animation in seconds. Essential for syncing different loops.", "Час, виражений значенням від 0.0 до 1.0, незалежно від реальної тривалості анімації в секундах. Життєво важливо для синхронізації різних циклів."), ""),
                ("Pose", HonamiDocLocalization.Get("A single, instantaneous frame snapshot containing the specific rotations and positions of every bone in the skeleton.", "Миттєвий знімок кадру, що містить конкретні значення поворотів (rotations) та позицій кожної кістки в скелеті."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The Illusion of Weight & Impact", "Ілюзія ваги та впливу"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Outstanding game animation isn't just about moving bones mathematically; it's about conveying physics, momentum, and anticipation. When a heavy character stops running, they shouldn't just freeze - they should 'overshoot' slightly, absorb the momentum, and settle.",
                "Видатна ігрова анімація — це не лише математичне переміщення кісток; це передача фізики, імпульсу та передчуття дії (anticipation). Коли важкий персонаж зупиняє біг, він не повинен просто завмерти — він має трохи «проскочити» вперед, поглинути імпульс і стабілізуватися."
            ));

            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "To see these terms in practice, go through the '02. Tutorial' series — every concept on this page appears there on a live character.",
                    "Щоб побачити ці терміни на практиці, пройдіть серію «02. Туторіал» — кожен концепт із цієї сторінки з'являється там на живому персонажі."
                ),
                HonamiDocumentationBuilder.CalloutType.Info);
        }
    }
}
