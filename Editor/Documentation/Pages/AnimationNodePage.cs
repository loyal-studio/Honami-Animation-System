using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class AnimationNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Animation Node", "Animation Node");
        public string Category => HonamiDocLocalization.Get("04. Node Library", "04. Бібліотека вузлів");
        public string SearchKeywords => "animation node clip playback speed looping trim start end reversed вузол анімації кліп відтворення";
        public int Order => 300;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Animation Node is the most basic building block of your graph. It plays a single AnimationClip and lets you trim the played range without touching the asset.",
                "Вузол анімації — це базовий будівельний блок вашого графа. Він відтворює один анімаційний кліп і дозволяє обрізати діапазон відтворення, не чіпаючи сам ассет."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Node Properties", "Властивості вузла"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Clip", "Clip"), HonamiDocLocalization.Get("The AnimationClip to be played.", "Анімаційний кліп для відтворення.")),
                (HonamiDocLocalization.Get("Start Time", "Start Time"), HonamiDocLocalization.Get("Trim: playback begins at this second of the clip.", "Обрізання: відтворення починається з цієї секунди кліпу.")),
                (HonamiDocLocalization.Get("End Time", "End Time"), HonamiDocLocalization.Get("Trim: playback stops here. 0 means 'play to the clip's end'.", "Обрізання: відтворення завершується тут. 0 означає «грати до кінця кліпу»."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("State Properties", "Властивості стану"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Playback behavior lives on the state that hosts the node, so it works identically for blend trees and sequencers:",
                "Поведінка відтворення задається на стані, що містить вузол, тому вона працює однаково для blend tree та секвенсерів:"
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Speed", "Speed"), HonamiDocLocalization.Get("Playback speed multiplier.", "Множник швидкості відтворення.")),
                (HonamiDocLocalization.Get("Loop", "Loop"), HonamiDocLocalization.Get("Whether the animation repeats when it reaches the end. Non-loop states raise OnStateFinished when done.", "Чи повторюється анімація після завершення. Non-loop стани викликають OnStateFinished після завершення.")),
                (HonamiDocLocalization.Get("Reversed", "Reversed"), HonamiDocLocalization.Get("Plays the clip backwards; event markers fire in reverse order too.", "Відтворює кліп у зворотному напрямку; маркери подій також спрацьовують у зворотному порядку.")),
                (HonamiDocLocalization.Get("Mirror", "Mirror"), HonamiDocLocalization.Get("Mirrors the animation across the avatar (requires a Honami Avatar).", "Віддзеркалює анімацію через аватар (потрібен Honami Avatar).")),
                (HonamiDocLocalization.Get("Weight", "Weight"), HonamiDocLocalization.Get("Output weight of this state inside its layer.", "Вихідна вага цього стану всередині його шару."))
            );

            HonamiDocumentationBuilder.AddNodeVisual(root, "Run", "ANIMATION", HonamiGraphStyles.Accent);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Use the Reversed checkbox instead of a negative speed to play a state backwards — duration math, finish detection and event markers all account for it.",
                "Використовуйте чекбокс Reversed замість від'ємної швидкості для зворотного відтворення — розрахунок тривалості, визначення завершення та маркери подій враховують саме його."
            ));
        }
    }
}
