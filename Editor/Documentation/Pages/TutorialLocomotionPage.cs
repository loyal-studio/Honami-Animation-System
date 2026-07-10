using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class TutorialLocomotionPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("2. Locomotion Blend Tree", "2. Локомоушн Blend Tree");
        public string Category => HonamiDocLocalization.Get("02. Tutorial: First Character", "02. Туторіал: Перший персонаж");
        public string SearchKeywords => "tutorial locomotion blend tree speed parameter setfloat туторіал локомоушн рух параметр крок";
        public int Order => 110;
        public int EstimatedReadTime => 5;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("How to create a float parameter and drive it from code", "Як створити float-параметр і керувати ним з коду"),
                    HonamiDocLocalization.Get("How a 1D Blend Tree mixes clips along thresholds", "Як 1D Blend Tree змішує кліпи вздовж порогів"),
                    HonamiDocLocalization.Get("What Damp Time does and why locomotion needs it", "Що робить Damp Time і навіщо він локомоушну")
                },
                new[]
                {
                    HonamiDocLocalization.Get("Tutorial part 1: a controller with a looping Idle state", "Частина 1 туторіалу: контролер із зацикленим Idle-стейтом")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "One state per movement speed (IdleState, WalkState, RunState) plus transitions between them is the road to state explosion. Instead, locomotion lives in a single state: a Blend Tree that smoothly interpolates Idle → Walk → Run along one float parameter.",
                "Окремий стейт на кожну швидкість (IdleState, WalkState, RunState) плюс транзішни між ними — прямий шлях до вибуху кількості станів. Натомість локомоушн живе в одному стейті: Blend Tree плавно інтерполює Idle → Walk → Run уздовж одного float-параметра."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 1 — Add the Speed parameter", "Крок 1 — Додайте параметр Speed"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "In the Graph Editor's Parameters panel add a Float named Speed. We will keep it normalized: 0 = standing, 1 = full run. Normalized parameters let the same tree work for characters with different movement speeds.",
                "У панелі Parameters редактора графа додайте Float із назвою Speed. Тримаємо його нормалізованим: 0 = стоїть, 1 = повний біг. Нормалізовані параметри дозволяють тому самому дереву працювати для персонажів із різною швидкістю руху."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 2 — Build the tree", "Крок 2 — Зберіть дерево"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Add a Blend Tree Node (right-click → Blend Tree) and name it Movement.",
                "Додайте Blend Tree Node (правий клік → Blend Tree) і назвіть його Movement."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Set Blend Parameter to Speed.",
                "Встановіть Blend Parameter = Speed."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Add three Motions: Idle at Threshold 0, Walk at 0.5, Run at 1.0.",
                "Додайте три Motions: Idle з Threshold 0, Walk — 0.5, Run — 1.0."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Set Damp Time to about 0.1 so the blend eases toward new values instead of snapping.",
                "Поставте Damp Time близько 0.1, щоб бленд плавно наздоганяв нові значення, а не стрибав."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Enable Loop on the state and mark Movement as the new default state. The Idle node from part 1 is no longer needed — delete it.",
                "Увімкніть Loop на стейті та позначте Movement новим default-стейтом. Idle-вузол із частини 1 більше не потрібен — видаліть його."));

            HonamiDocumentationBuilder.AddNodeVisual(root, "Movement", "BLEND TREE 1D", HonamiGraphStyles.Green);

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "With the default Standard 1D blend type, Honami synchronizes clip playback so walk and run stay in phase — feet touch the ground at matching times and the mix never slides.",
                "З типом змішування Standard 1D (за замовчуванням) Honami синхронізує відтворення кліпів, щоб walk і run лишалися у фазі — стопи торкаються землі в узгоджені моменти, і мікс не «пливе»."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Step 3 — Drive Speed from code", "Крок 3 — Керуйте Speed з коду"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;

public sealed class LocomotionInput : MonoBehaviour
{
    private static readonly int SpeedId = HonamiAnimator.StringToHash(""Speed"");

    private HonamiAnimator _honami;

    private void Awake()
    {
        TryGetComponent(out _honami);
    }

    private void Update()
    {
        var input = new Vector2(Input.GetAxis(""Horizontal""), Input.GetAxis(""Vertical""));
        _honami.SetFloat(SpeedId, Mathf.Clamp01(input.magnitude));
    }
}");

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Press Play and hold W: the character flows from idle through walk into run and back, with no transitions authored at all. Watch the blend weights move live in the Graph Editor.",
                "Натисніть Play і затисніть W: персонаж перетікає з idle через walk у run і назад — без жодного намальованого транзішна. Спостерігайте в Graph Editor, як ваги блендінгу рухаються наживо."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Honami blend trees are 1D by design. For strafing, drive a second layer or use a signed parameter (-1..1) with mirrored clips — see the Blend Tree Node reference for patterns.",
                "Blend tree в Honami навмисно одновимірні. Для стрейфу керуйте другим шаром або використовуйте знаковий параметр (-1..1) із дзеркальними кліпами — патерни описані в довіднику Blend Tree Node."
            ));

            HonamiDocumentationBuilder.AddSeparator(root);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Next: movement is continuous, but a jump is a discrete action. In part 3 we add a Jump state and learn transitions, conditions and triggers.",
                "Далі: рух — безперервний, а стрибок — дискретна дія. У частині 3 додамо стейт Jump і розберемо транзішни, умови та тригери."
            ));
        }
    }
}
