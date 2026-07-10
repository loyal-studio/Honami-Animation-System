using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class EventNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Event Node", "Event Node");
        public string Category => HonamiDocLocalization.Get("04. Node Library", "04. Бібліотека вузлів");
        public string SearchKeywords => "event node logic no animation empty state timer delay markers івент нода логіка без анімації таймер маркери";
        public int Order => 330;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Event Node is a state with no animation at all. It exists purely to run logic: it fires event markers, applies parameter assignments and executes Sub-Nodes for a duration you set in seconds. Use it when the graph needs a beat of gameplay logic - a delay, a scripted chain of events, a decision point - without playing a clip.",
                "Event Node — це стейт зовсім без анімації. Він існує суто для логіки: фаєрить івент-маркери, застосовує parameter assignments і виконує саб-ноди протягом заданої в секундах тривалості. Використовуйте його, коли графу потрібен «біт» геймплейної логіки — затримка, скриптований ланцюжок івентів, точка прийняття рішення — без програвання кліпу."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("How it behaves", "Як він поводиться"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Duration is the state's whole timeline. Event markers, Has Exit Time and OnStateFinished all measure against it, and Speed scales it like on any other state.", "Duration — це весь таймлайн стейту. Івент-маркери, Has Exit Time та OnStateFinished рахуються відносно неї, а Speed масштабує її, як у будь-якого іншого стейту."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("It is a full, real state: transitions in and out work as usual, Sub-Nodes get their OnEnter / Update / OnExit, parameters can be assigned on enter and exit, and PlayState / TrySkipState target it from code like any other state.", "Це повноцінний, реальний стейт: транзішни в нього та з нього працюють як завжди, саб-ноди отримують свої OnEnter / Update / OnExit, параметри призначаються на вході й виході, а PlayState / TrySkipState таргетять його з коду, як будь-який інший стейт."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Non-looping by default: it plays its duration once, fires OnStateFinished and lets exit-time transitions continue the flow. Turn Loop on to repeat its markers every cycle - a code-free logic ticker.", "За замовчуванням без лупу: стейт відпрацьовує свою тривалість один раз, фаєрить OnStateFinished і віддає потік транзішнам з exit time. Увімкніть Loop, щоб маркери повторювалися щоциклу — логічний «тікер» без коду."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Open the Timeline to author it like any state: drag the Logic Window block to set the duration and drop Local / Global event markers exactly where they should fire.", "Відкрийте Timeline і працюйте з ним як зі звичайним стейтом: тягніть блок Logic Window, щоб задати тривалість, і ставте Local / Global івент-маркери точно там, де вони мають спрацювати."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("When to reach for it", "Коли він потрібен"), HonamiEditorIcons.Timeline);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("A series of actions with no animation: chain several Event Nodes with exit-time transitions and each one fires its events on schedule.", "Серія дій без анімації: з'єднайте кілька Event Node транзішнами з exit time — і кожен фаєрить свої івенти за розкладом."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Timers and delays inside the graph: 'wait 0.4s, then set InCombat and move on' needs zero C#.", "Таймери й затримки прямо в графі: «почекай 0.4с, потім вистав InCombat і йди далі» — без жодного рядка C#."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Pure logic layers: a whole layer of Event Nodes can drive spawning, sounds, doors or scripted sequences through Global Events while the visual layers stay untouched.", "Суто логічні шари: цілий шар з Event Node може драйвити спавн, звуки, двері чи скриптовані секвенції через Global Events, не чіпаючи візуальні шари."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Decision points: land in an Event Node, let its Sub-Nodes or assignments update parameters, and let condition-driven transitions pick the next state.", "Точки рішень: заходите в Event Node, його саб-ноди чи assignments оновлюють параметри, а транзішни з умовами обирають наступний стейт."));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "The node writes no pose. Entering it with an instant (0s) transition freezes the character on the last written pose, which is usually what you want. A timed cross-fade into it blends toward 'no animation', so on a visible base layer prefer instant transitions - or keep your logic chains on a dedicated logic layer where the output does not matter.",
                "Нода не пише позу. Вхід у неї миттєвим (0с) транзішном заморожує персонажа на останній записаній позі — зазвичай саме це й потрібно. Кросфейд із тривалістю блендить у «відсутність анімації», тож на видимому базовому шарі використовуйте миттєві транзішни — або тримайте логічні ланцюжки на окремому логічному шарі, де вихідна поза не має значення."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Event Nodes keep the division of labour intact: the animator authors the whole logic beat - timing, markers, parameters - in the UI, and the programmer only handles the resulting events in gameplay code.",
                "Event Node зберігає розподіл праці: аніматор авторить весь логічний біт — таймінг, маркери, параметри — в UI, а програміст лише обробляє отримані івенти в геймплейному коді."
            ));
        }
    }
}
