using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class RandomSequencerNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Random & Sequencer", "Random та Sequencer");
        public string Category => HonamiDocLocalization.Get("04. Node Library", "04. Бібліотека вузлів");
        public string SearchKeywords => "random animation sequencer variations idle variety weighted sequence clips вузол випадковість секвенсор варіації послідовність";
        public int Order => 320;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "These are two distinct multi-clip nodes. The Random Animation node picks one clip each time the state is entered; the Sequencer node plays several clips one after another inside a single state.",
                "Це два різні вузли з кількома кліпами. Вузол Random Animation обирає один кліп під час кожного входу в стан; вузол Sequencer програє кілька кліпів один за одним у межах одного стану."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Random Animation Node", "Вузол Random Animation"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "When the state is entered, one clip is chosen from the list. This is perfect for adding variety to Idle or Attack states so they never look identical twice.",
                "Під час входу в стан зі списку обирається один кліп. Це ідеально для додавання варіативності станам Idle чи Attack, щоб вони не виглядали однаково двічі."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Each entry in the list exposes:",
                "Кожен запис у списку має:"
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Clip", "Clip"), HonamiDocLocalization.Get("The animation to potentially play.", "Анімація, яку потенційно буде відтворено.")),
                (HonamiDocLocalization.Get("Weight", "Weight"), HonamiDocLocalization.Get("Relative probability of this clip being picked (higher = more likely).", "Відносна ймовірність вибору цього кліпу (більше = ймовірніше).")),
                (HonamiDocLocalization.Get("Speed", "Speed"), HonamiDocLocalization.Get("Per-clip playback speed multiplier.", "Множник швидкості відтворення для окремого кліпу.")),
                (HonamiDocLocalization.Get("Start / End Time", "Start / End Time"), HonamiDocLocalization.Get("Optional trim range within the clip (End 0 = to the clip's end).", "Опціональний діапазон обрізки в межах кліпу (End 0 = до кінця кліпу).")),
                (HonamiDocLocalization.Get("Mirror", "Mirror"), HonamiDocLocalization.Get("Play this specific variation mirrored.", "Відтворити саме цю варіацію віддзеркалено."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Sequencer Node", "Вузол Sequencer"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Sequencer arranges several clips on a shared timeline. Each clip has a Start Time offset; as the state's playhead advances, the clip whose window is active is shown. Use it to stitch short clips (e.g. wind-up → hit → recover) into one state without extra transitions.",
                "Sequencer розкладає кілька кліпів на спільному таймлайні. Кожен кліп має зміщення Start Time; коли головка відтворення стану рухається, показується той кліп, чиє вікно активне. Використовуйте його, щоб зшити короткі кліпи (напр. замах → удар → відновлення) в один стан без зайвих переходів."
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Clip", "Clip"), HonamiDocLocalization.Get("A segment of the sequence.", "Сегмент послідовності.")),
                (HonamiDocLocalization.Get("Start Time", "Start Time"), HonamiDocLocalization.Get("When (in seconds along the state) this clip becomes active.", "Коли (у секундах уздовж стану) цей кліп стає активним.")),
                (HonamiDocLocalization.Get("Speed", "Speed"), HonamiDocLocalization.Get("Per-clip playback speed multiplier.", "Множник швидкості для окремого кліпу."))
            );

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Both nodes respect the state's Loop, Speed and Reversed settings. Combine the Random node with a Repeater to re-roll a fresh variation on demand.",
                "Обидва вузли враховують налаштування стану Loop, Speed та Reversed. Поєднайте вузол Random із Repeater, щоб на вимогу перекидати нову варіацію."
            ));
        }
    }
}
