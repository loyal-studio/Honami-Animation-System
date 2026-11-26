using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class BlendSpaceNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Blend Space Node", "Blend Space Node");
        public string Category => HonamiDocLocalization.Get("04. Node Library", "04. Бібліотека вузлів");
        public string SearchKeywords => "blend space 2d 3d nd any dimension directional freeform cartesian simple strafe locomotion axis axes position простір змішування вимір напрямок вісь позиція страйф";
        public int Order => 311;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A Blend Space blends clips across any number of axes. Each axis is driven by its own float parameter and each motion carries one coordinate per axis, so a character can strafe, back up and move diagonally out of a single state. Two axes cover classic locomotion; a third or fourth can add slope, lean or crouch without splitting the set across states.",
                "Blend Space змішує кліпи вздовж довільної кількості осей. Кожну вісь веде власний float-параметр, а кожен мотив несе по одній координаті на вісь, тож персонаж може йти вбік, задкувати й рухатися по діагоналі в межах одного стану. Дві осі покривають класичну локомоцію; третя чи четверта додають нахил, крен або присід без розщеплення набору на кілька станів."
            ));

            HonamiDocumentationBuilder.AddInfoBox(root, HonamiDocLocalization.Get(
                "For a single parameter use the Blend Tree node instead. It resolves a sorted 1D blend between two neighbouring thresholds directly and does not pay for the per-axis work a blend space does.",
                "Для одного параметра беріть натомість вузол Blend Tree. Він напряму розв'язує сортований 1D-бленд між двома сусідніми порогами й не платить за пооосьову роботу, яку виконує blend space."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Space Types", "Типи простору"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Freeform Directional", "Freeform Directional"), HonamiDocLocalization.Get("Any number of axes. Direction and distance from the origin are weighted separately, so several clips may share a direction at different magnitudes - walk forward and run forward on the same heading. The usual choice for locomotion.", "Будь-яка кількість осей. Напрямок і відстань від центру зважуються окремо, тож кілька кліпів можуть ділити напрямок на різних величинах - walk forward і run forward на одному курсі. Звичний вибір для локомоції.")),
                (HonamiDocLocalization.Get("Freeform Cartesian", "Freeform Cartesian"), HonamiDocLocalization.Get("Any number of axes, with no special meaning for direction or distance from the origin. Use it when the axes are unrelated quantities - speed and turn angle, for example.", "Будь-яка кількість осей, без особливого значення напрямку чи відстані від центру. Використовуйте, коли осі - незалежні величини, наприклад швидкість і кут повороту.")),
                (HonamiDocLocalization.Get("Simple Directional", "Simple Directional"), HonamiDocLocalization.Get("Exactly two axes. One clip per direction plus an optional clip at the centre; only the two motions bracketing the sample direction play, and the centre takes the weight that is left. It brackets by angular order around a circle, which only exists in a plane - the node reports a warning if it has any other axis count.", "Рівно дві осі. По одному кліпу на напрямок плюс необов'язковий кліп у центрі; грають лише два мотиви, що обрамляють напрямок вибірки, а центр забирає решту ваги. Він обрамляє за кутовим порядком навколо кола, який існує лише на площині - за іншої кількості осей вузол показує попередження."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Properties", "Властивості"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Axes", "Осі"), HonamiDocLocalization.Get("One float parameter per axis. Adding or removing an axis resizes every motion's position to match.", "По одному float-параметру на вісь. Додавання чи видалення осі змінює розмір позиції кожного мотиву відповідно.")),
                (HonamiDocLocalization.Get("Damp Time", "Damp Time"), HonamiDocLocalization.Get("Smoothing (SmoothDamp) applied to every axis so the blend eases instead of snapping. The damped values live per animator, not on the shared node asset. 0 = instant.", "Згладжування (SmoothDamp) для кожної осі, аби бленд плавно наздоганяв значення, а не стрибав. Згладжені значення живуть окремо для кожного аніматора, а не на спільному ассеті вузла. 0 = миттєво.")),
                (HonamiDocLocalization.Get("Synchronize Speed", "Synchronize Speed"), HonamiDocLocalization.Get("Derive each clip's speed from the blended duration so mixed-length clips stay in phase. Turn it off to play every clip at its own authored Speed.", "Виводити швидкість кожного кліпу з блендованої тривалості, щоб кліпи різної довжини лишалися у фазі. Вимкніть, щоб кожен кліп грав на власній заданій швидкості.")),
                (HonamiDocLocalization.Get("Motions", "Motions"), HonamiDocLocalization.Get("The list of clips. Each entry has a Clip, one coordinate per axis, a Speed and a Mirror toggle.", "Список кліпів. Кожен запис має Clip, по координаті на вісь, Speed та перемикач Mirror."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Authoring", "Авторинг"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Double-click the node in the Graph window to open the Blend Space window, or use the Blend Space button in the inspector.", "Двічі клацніть вузол у вікні Graph, щоб відкрити вікно Blend Space, або скористайтеся кнопкою Blend Space в інспекторі."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Drag empty space to move the preview sample; drag a motion dot to move that clip. Hold Ctrl while dragging a dot to snap to 0.1 steps.", "Тягніть порожнє місце, щоб рухати вибірку прев'ю; тягніть точку мотиву, щоб пересунути кліп. Утримуйте Ctrl під час перетягування точки для прив'язки з кроком 0.1."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("With more than two axes the field shows a projection: pick which two axes to plot, and dragging edits only those coordinates while the rest stay put.", "За понад двох осей поле показує проєкцію: оберіть, які дві осі відображати, і перетягування змінює лише ці координати, решта лишаються на місці."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Arrange In Circle spreads the motions evenly around the plotted plane; Center First parks motion 0 at the origin for the idle clip.", "Arrange In Circle рівномірно розкладає мотиви по відображеній площині; Center First ставить мотив 0 у центр - під кліп idle."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("A live skeleton preview sits beside the field, so dragging a motion or the sample updates the pose straight away. The Timeline window adds one scrubbing slider per axis and previews on the real character in the scene.", "Поряд із полем - живий скелетний превʼю, тож перетягування мотиву чи вибірки одразу оновлює позу. Вікно Timeline додає по слайдеру на кожну вісь і показує превʼю на справжньому персонажі у сцені."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Enable Mirror on a motion to reuse a clip for the opposite side without a separate asset - a strafe-right clip becomes strafe-left.", "Увімкніть Mirror на мотиві, щоб перевикористати кліп для протилежного боку без окремого ассету - strafe-right стає strafe-left."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("How weights are solved", "Як розв'язуються ваги"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Weights are always normalized to sum to 1, so the pose never fades out - even when the sample sits far outside the ring of motions.", "Ваги завжди нормалізуються до суми 1, тож поза ніколи не згасає - навіть коли вибірка далеко за межами кільця мотивів."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("The freeform types use gradient band interpolation, which needs no triangulation and accepts motions placed anywhere.", "Freeform-типи використовують інтерполяцію градієнтними смугами, якій не потрібна тріангуляція і яка приймає мотиви в довільних позиціях."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Solving allocates nothing at runtime: weights are resolved into stack memory each frame.", "Розв'язання не алокує нічого в рантаймі: ваги обчислюються у стековій пам'яті щокадру."));

            HonamiDocumentationBuilder.AddNodeVisual(root, "Locomotion", "BLEND SPACE", HonamiGraphStyles.Green);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "For strafing locomotion, feed the character's local move direction scaled by normalized speed into two axes: X = right/left, Y = forward/back. Put idle at the origin and the walk clips on the unit circle, and one Simple Directional space covers the whole ground movement set.",
                "Для страйф-локомоції подавайте локальний напрямок руху персонажа, помножений на нормалізовану швидкість, у дві осі: X = праворуч/ліворуч, Y = вперед/назад. Поставте idle у центр, а кліпи ходьби - на одиничне коло, і один простір Simple Directional покриє весь набір наземного руху."
            ));
        }
    }
}
