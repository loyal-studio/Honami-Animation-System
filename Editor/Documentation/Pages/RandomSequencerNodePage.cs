using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class RandomSequencerNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Random & Sequencer", "Random та Sequencer");
        public string Category => HonamiDocLocalization.Get("04. Node Library", "04. Бібліотека вузлів");
        public string SearchKeywords => "random animation sequencer variations idle variety weighted sequence clips split convert вузол випадковість секвенсор варіації послідовність";
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
                (HonamiDocLocalization.Get("Mirror", "Mirror"), HonamiDocLocalization.Get("Play this specific variation mirrored.", "Відтворити саме цю варіацію віддзеркалено.")),
                (HonamiDocLocalization.Get("Muted", "Muted"), HonamiDocLocalization.Get("Temporarily excludes this clip from the random pool without deleting it - handy for quickly testing specific variations.", "Тимчасово виключає цей кліп із random-пулу без видалення - зручно, щоб швидко протестити конкретні варіації."))
            );
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Enable No Repeat on the node to force unique picks: a clip is never chosen again until every other clip has played (shuffle-bag), and the same clip never plays twice in a row. Weights still bias the order within each cycle.",
                "Увімкніть No Repeat на вузлі, щоб форсити унікальні вибори: кліп не буде обрано знову, доки не зіграють усі інші (shuffle-bag), і той самий кліп ніколи не грає двічі підряд. Weights і далі впливають на порядок у межах кожного циклу."
            ));

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

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Modules inside one clip", "Модулі всередині одного кліпа"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Every clip row of these nodes in the Timeline window has a + button. A module added there (Local Events, Global Events, Activation, Parameter Curve, any custom one) is drawn under the clip and runs only while that clip plays. Modules can be nested further, for example events inside an Activation item of that clip. On a Random node, an attack's EndPunch can sit at the moment of its own clip instead of one marker per variation on a shared row. On a Sequencer, the module's items are timed from the clip's Start Time, so they move with the clip.",
                "Кожен рядок кліпа цих нод у вікні Timeline має кнопку +. Модуль, доданий там (Local Events, Global Events, Activation, Parameter Curve, будь-який власний), малюється під кліпом і працює лише поки грає цей кліп. Модулі можна вкладати й далі, наприклад події всередині item-а Activation цього кліпа. На Random-ноді EndPunch атаки стоїть у моменті свого кліпа, а не по маркеру на кожну варіацію в спільному рядку. На Sequencer-і item-и модуля рахуються від Start Time кліпа, тож рухаються разом із ним."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Parent field in a module's properties, or Parent in its row's right-click menu, moves a module between clips, into another module's item or back to Whole State. Modules of clips other than the one picked in the toolbar are dimmed and left out of the preview. If a clip is removed from the node, its modules stay in the state with a warning and do nothing until you pick a new parent.",
                "Поле Parent у властивостях модуля або Parent у меню правого кліку його рядка переносить модуль між кліпами, в item іншого модуля чи назад на Whole State. Модулі кліпів, окрім вибраного в тулбарі, притемнені й не беруть участі в прев'ю. Якщо кліп видалити з ноди, його модулі лишаються в стані з попередженням і нічого не роблять, доки ви не виберете нового батька."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Both nodes respect the state's Loop, Speed and Reversed settings. Combine the Random node with a Repeater to re-roll a fresh variation on demand.",
                "Обидва вузли враховують налаштування стану Loop, Speed та Reversed. Поєднайте вузол Random із Repeater, щоб на вимогу перекидати нову варіацію."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Need each variation as its own state (e.g. to build a Repeater combo chain)? Right-click the Random node and pick Split Into N Animation States: every active clip becomes a separate Animation state, and all incoming and outgoing transitions are ported to each of them.",
                "Потрібна кожна варіація як окремий стан (напр. для комбо-ланцюжка через Repeater)? Клікніть правою по Random-ноді та оберіть Split Into N Animation States: кожен активний кліп стане окремим Animation-станом, а всі вхідні та вихідні транзішни портуються на кожен із них."
            ));
        }
    }
}
