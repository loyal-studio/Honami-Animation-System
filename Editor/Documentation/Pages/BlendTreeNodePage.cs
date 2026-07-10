using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class BlendTreeNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Blend Tree Node", "Blend Tree Node");
        public string Category => HonamiDocLocalization.Get("04. Node Library", "04. Бібліотека вузлів");
        public string SearchKeywords => "blend tree 1d standard simple motion blending interpolation threshold damping дерево змішування інтерполяція поріг демпфування";
        public int Order => 310;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A Blend Tree smoothly interpolates between several clips along a single float parameter (1D). Each clip is placed at a Threshold on that axis, and Honami crossfades the two neighbouring clips based on the current parameter value. Ideal for locomotion (Idle → Walk → Run).",
                "Дерево змішування плавно інтерполює між кількома кліпами вздовж одного float-параметра (1D). Кожен кліп розміщується на своєму Порозі (Threshold) на цій осі, а Honami перехресно змішує два сусідні кліпи залежно від поточного значення параметра. Ідеально для локомоції (Idle → Walk → Run)."
            ));

            HonamiDocumentationBuilder.AddInfoBox(root, HonamiDocLocalization.Get(
                "Honami Blend Trees are 1D only — there is no 2D blend space. For multi-directional movement, drive a 1D tree with a signed parameter (e.g. -1..1) or split the motion across layers and states.",
                "Дерева змішування Honami лише 1D — 2D-простору змішування немає. Для багатонаправленого руху керуйте 1D-деревом знаковим параметром (напр. -1..1) або розділіть рух між шарами та станами."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Blend Types", "Типи змішування"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Standard 1D", "Standard 1D"), HonamiDocLocalization.Get("Synchronizes clip playback speeds so the blended clips stay in phase (foot timing matches). Each clip's speed is derived from the interpolated duration of the blend.", "Синхронізує швидкості кліпів, щоб змішані кліпи лишалися у фазі (таймінг кроків збігається). Швидкість кожного кліпу виводиться з інтерпольованої тривалості змішування.")),
                (HonamiDocLocalization.Get("Simple 1D", "Simple 1D"), HonamiDocLocalization.Get("Plays each clip at its own authored Speed, without any time synchronization.", "Відтворює кожен кліп на його власній заданій швидкості (Speed), без синхронізації часу."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Properties", "Властивості"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Blend Parameter", "Blend Parameter"), HonamiDocLocalization.Get("The float parameter that drives the blend position.", "Float-параметр, що визначає позицію блендінгу.")),
                (HonamiDocLocalization.Get("Damp Time", "Damp Time"), HonamiDocLocalization.Get("Smoothing (SmoothDamp) applied to the parameter so the blend eases instead of snapping. 0 = instant.", "Згладжування (SmoothDamp), що застосовується до параметра, аби бленд плавно наздоганяв значення, а не стрибав. 0 = миттєво.")),
                (HonamiDocLocalization.Get("Motions", "Motions"), HonamiDocLocalization.Get("The list of clips. Each entry has a Clip, a Threshold (its position on the axis), a Speed and a Mirror toggle.", "Список кліпів. Кожен запис має Clip, Threshold (позицію на осі), Speed та перемикач Mirror."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("How blending works", "Як працює змішування"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Below the first threshold or above the last one, that end clip plays at full weight.", "Нижче першого порога або вище останнього повну вагу отримує відповідний крайній кліп."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Between two thresholds, the two adjacent clips crossfade proportionally to the parameter.", "Між двома порогами два сусідні кліпи перехресно змішуються пропорційно до параметра."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Enable Mirror on a motion to reuse a clip for the opposite side without a separate asset.", "Увімкніть Mirror на мотиві, щоб перевикористати кліп для протилежного боку без окремого ассету."));

            HonamiDocumentationBuilder.AddNodeVisual(root, "Movement", "BLEND TREE 1D", HonamiGraphStyles.Green);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Keep thresholds sorted and use a normalized parameter (e.g. 0 = idle, 1 = full sprint) so the same tree works across different movement speeds.",
                "Тримайте пороги відсортованими та використовуйте нормалізований параметр (напр. 0 = idle, 1 = повний спринт), щоб те саме дерево працювало для різних швидкостей руху."
            ));
        }
    }
}
