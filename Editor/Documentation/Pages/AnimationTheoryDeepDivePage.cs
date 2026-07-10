using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class AnimationTheoryDeepDivePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Deep Dive: Theory", "Глибоке занурення: Теорія");
        public string Category => HonamiDocLocalization.Get("09. Theory & Background", "09. Теорія та довідка");
        public string SearchKeywords => "theory deep dive math quaternions interpolation slerp lerp математика теорія";
        public int Order => 810;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Optional background reading: how Honami is structured internally and the math its blending rests on. Nothing here is required to use the system — it explains why it behaves the way it does.",
                "Необов'язкове читання: як Honami влаштований усередині та на якій математиці тримається його блендінг. Нічого з цього не потрібно для роботи з системою — сторінка пояснює, чому вона поводиться саме так."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("1. High-Performance Blending Architecture", "1. Архітектура високопродуктивного змішування"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Unlike traditional systems that allocate memory for every blend, Honami uses a Zero-Allocation approach. It utilizes modern C# features like Span<T> and Memory<T> to manipulate bone transforms directly in contiguous memory blocks.",
                "На відміну від традиційних систем, які виділяють пам'ять для кожного змішування, Honami використовує підхід Zero-Allocation. Він використовує сучасні можливості C#, такі як Span<T> та Memory<T>, для маніпуляції трансформ-даними кісток безпосередньо в безперервних блоках пам'яті."
            ));

            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("Linear Evaluation", "Лінійна евакуація"),
                 HonamiDocLocalization.Get("The graph is evaluated in a single pass, calculating weights and sampling poses with O(1) complexity per node. No recursive overhead.", "Граф обчислюється за один прохід, розраховуючи ваги та вибірку поз з константною складністю O(1) на вузол. Жодних рекурсивних витрат."),
                 HonamiEditorIcons.BlendTreeWhite),
                (HonamiDocLocalization.Get("Cache Efficiency", "Ефективність кешу"),
                 HonamiDocLocalization.Get("Bone data is stored in a way that minimizes CPU cache misses, allowing Honami to blend hundreds of skeletons without hitting memory bottlenecks.", "Дані кісток зберігаються таким чином, щоб мінімізувати пропуски кешу CPU, що дозволяє Honami змішувати сотні скелетів без перевантаження шини пам'яті."),
                 HonamiEditorIcons.Controller)
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("2. Sub-Frame Precision & Synchronization", "2. Субфреймова точність та синхронізація"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Variable frame rates are the enemy of smooth animation. Honami uses an independent 'Evaluation Clock' that samples animations with sub-frame precision, ensuring that transitions are mathematically continuous regardless of FPS fluctuations.",
                "Змінна частота кадрів — ворог плавної анімації. Honami використовує незалежний «Evaluation Clock», який робить вибірку анімацій з субфреймовою точністю, гарантуючи математичну безперервність переходів незалежно від коливань FPS."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Delta Pose Extraction: Root Motion is calculated by extracting the precise difference between the current and previous sampled positions in 4D space, avoiding accumulation errors.",
                "Delta Pose Extraction: Root Motion обчислюється шляхом вилучення точної різниці між поточною та попередньою вибірками позицій у 4D-просторі, уникаючи помилок накопичення."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Phase Sync: When blending two loops (e.g., Walk and Run), Honami synchronizes their normalized time phases to ensure that feet strike the ground at the exact same moment in both clips.",
                "Phase Sync: При змішуванні двох циклів (наприклад, ходьба та біг), Honami синхронізує їхні фази нормованого часу, щоб гарантувати, що стопи торкаються землі в той самий момент в обох кліпах."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("3. The Geometry of Rotations", "3. Геометрія поворотів"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Linear interpolation (LERP) of rotations results in variable angular velocity, making movements feel 'robotic'. Honami uses Spherical Linear Interpolation (SLERP) which treats rotations as points on a 4D hypersphere.",
                "Лінійна інтерполяція (LERP) поворотів призводить до змінної кутової швидкості, через що рухи здаються «роботизованими». Honami використовує сферичну лінійну інтерполяцію (SLERP), яка розглядає повороти як точки на 4D-гіперсфері."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Shortest Path: SLERP automatically finds the most efficient rotational path, preventing the '360-degree flip' bug common in Euler systems.",
                "Shortest Path: SLERP автоматично знаходить найефективніший шлях обертання, запобігаючи багу «перевороту на 360 градусів», поширеному в системах Ейлера."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("4. Transition Dynamics & Curve Integrity", "4. Динаміка переходів та цілісність кривих"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Transitions in Honami are not just simple LERPs over time. We use cubic Hermite splines (S-curves) to ensure that motion has 'acceleration' and 'deceleration' (Ease-In/Out), mimicking biological muscle behavior.",
                "Переходи в Honami — це не просто лінійне змішування. Ми використовуємо кубічні сплайни Ерміта (S-криві), щоб забезпечити «прискорення» та «уповільнення» руху (Ease-In/Out), імітуючи біологічну поведінку м'язів."
            ));

            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("Weight Normalization", "Нормалізація ваги"),
                 HonamiDocLocalization.Get("Honami ensures that the sum of all bone weights always equals exactly 1.0, preventing skeleton 'shrinking' or 'inflation' during multi-layer blending.", "Honami гарантує, що сума всіх ваг кісток завжди дорівнює рівно 1.0, запобігаючи «стисненню» або «роздуванню» скелета під час багатошарового змішування."),
                 HonamiEditorIcons.GraphWhite),
                (HonamiDocLocalization.Get("Inertialization", "Інерціалізація"),
                 HonamiDocLocalization.Get("A high-performance technique that handles sudden state changes by smoothing out the velocity, rather than just the pose, eliminating 'popping' visual bugs.", "Високопродуктивна техніка, яка обробляє раптові зміни станів шляхом згладжування швидкості, а не просто пози, усуваючи візуальні «стрибки» (popping)."),
                 HonamiEditorIcons.TimelineWhite)
            );

            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "By combining sub-frame sampling, SLERP geometry, and cubic transitions, Honami achieves a level of 'Grounding' that was previously only possible in high-end cinematic sequences.",
                    "Поєднуючи субфреймову вибірку, геометрію SLERP та кубічні переходи, Honami досягає рівня «заземлення», який раніше був можливий лише у високорівневих кінематографічних сценах."
                ),
                HonamiDocumentationBuilder.CalloutType.Info);
        }
    }
}
