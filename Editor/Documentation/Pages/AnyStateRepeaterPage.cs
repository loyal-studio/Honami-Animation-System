using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class AnyStateRepeaterPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Any State & Repeater", "Any State та Repeater");
        public string Category => HonamiDocLocalization.Get("04. Node Library", "04. Бібліотека вузлів");
        public string SearchKeywords => "any state repeater loop cooldown restart global transitions вузол повторювач";
        public int Order => 340;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Any State and Repeater are virtual nodes: they play nothing themselves and exist only as a source of global transitions — transitions that are evaluated no matter which state is currently active on the layer.",
                "Any State і Repeater — віртуальні вузли: самі вони нічого не програють та існують лише як джерело глобальних переходів — переходів, що оцінюються незалежно від того, який стан зараз активний на шарі."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Any State", "Any State"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Draw transitions from Any State to targets that must be reachable from anywhere — deaths, stuns, teleports. Conditions work exactly like normal transitions; enable Can Transition To Self on a transition if the target should be allowed to restart itself.",
                "Малюйте переходи з Any State до цілей, які мають бути досяжними звідусіль — смерті, оглушення, телепорти. Умови працюють так само, як у звичайних переходів; увімкніть Can Transition To Self на переході, якщо ціль повинна мати змогу перезапускати саму себе."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Repeater", "Repeater"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Repeater is an Any State variant built for rapid re-fire: its transitions force-restart the target state every time they trigger. Use it for hit reactions, light attacks and anything that must interrupt itself cleanly.",
                "Repeater — це варіант Any State для швидких повторів: його переходи примусово перезапускають цільовий стан щоразу при спрацюванні. Використовуйте його для реакцій на влучання, легких ударів і всього, що має чисто переривати саме себе."
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Repeat Cooldown", "Repeat Cooldown"), HonamiDocLocalization.Get("Minimum time in seconds between two fires — protects against trigger spam.", "Мінімальний час у секундах між двома спрацюваннями — захист від спаму тригерами.")),
                (HonamiDocLocalization.Get("Max Repeats", "Max Repeats"), HonamiDocLocalization.Get("Caps how many times the repeater may fire. 0 means infinite.", "Обмежує кількість спрацювань повторювача. 0 — без обмежень."))
            );

            HonamiDocumentationBuilder.AddNodeVisual(root, "Hit Reaction", "REPEATER", HonamiGraphStyles.Accent);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Any State fires a target once and stays there until conditions change; the Repeater is the tool when the same target must restart again and again. Pair it with a trigger parameter for mash-friendly combat.",
                "Any State запускає ціль один раз і залишається там, доки умови не зміняться; Repeater — інструмент, коли та сама ціль має перезапускатися знову і знову. Поєднайте його з trigger-параметром для комбату, дружнього до закликування кнопок."
            ));
        }
    }
}
