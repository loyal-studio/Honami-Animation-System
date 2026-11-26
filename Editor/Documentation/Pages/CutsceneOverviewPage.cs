using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneOverviewPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Cutscenes with Honami", "Катсцени в Honami");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "cutscene overview timeline director sequencer cinematic asset window catscene катсцена огляд таймлайн директор секвенсор синематик";
        public int Order => 560;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("What the three pieces of a Honami cutscene are and who owns what", "Які три частини має катсцена Honami і що кому належить"),
                HonamiDocLocalization.Get("How a cutscene timeline differs from the timeline of a state", "Чим таймлайн катсцени відрізняється від таймлайну стану"),
                HonamiDocLocalization.Get("Which page of this section answers which question", "Яка сторінка розділу відповідає на яке питання")
            });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A cutscene is a piece of the game that plays on its own: the camera cuts between shots, characters act, music swells, the screen fades, and at the end control returns to the player. In Honami a cutscene is built on the same module system that drives the events of your states, so everything you learn here also applies to a state's timeline, and every track you write for a state can be dropped into a cutscene.",
                "Катсцена це шматок гри, що грає сам: камера ріже між шотами, персонажі грають, музика наростає, екран затемнюється, а наприкінці керування повертається гравцеві. У Honami катсцена побудована на тій самій системі модулів, що керує подіями ваших станів, тож усе вивчене тут стосується й таймлайну стану, а кожен трек, написаний для стану, можна покласти в катсцену."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "The Timeline and Director are in beta. The data format can still change between 0.x versions.",
                "Timeline та Director у beta. Формат даних ще може змінюватися між версіями 0.x."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Three pieces", "Три частини"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("Honami Timeline (asset)", "Honami Timeline (ассет)"), HonamiDocLocalization.Get("What happens and when: tracks of shots, clips, sounds, fades, events and markers. It lives in the Project and knows nothing about any scene, so one timeline can be played in several places.", "Що і коли відбувається: треки шотів, кліпів, звуків, фейдів, подій і маркери. Живе в Project і нічого не знає про жодну сцену, тож один таймлайн можна грати в кількох місцях."), HonamiEditorIcons.TimelineWhite),
                (HonamiDocLocalization.Get("Honami Director (component)", "Honami Director (компонент)"), HonamiDocLocalization.Get("Who takes part: which camera, character, light or audio source each track drives, and where each shot's anchor is. It also plays the timeline at runtime and tells your code when it starts, pauses and ends.", "Хто бере участь: яку камеру, персонажа, світло чи джерело звуку веде кожен трек і де якір кожного шота. Він же грає таймлайн у runtime і повідомляє коду про старт, паузу та кінець."), HonamiEditorIcons.Controller),
                (HonamiDocLocalization.Get("Timeline window (editor)", "Вікно Timeline (редактор)"), HonamiDocLocalization.Get("Where you edit, like in a video editor: a ruler with frames or timecode, trimming, a blade, ripple edits, markers, snapping, keyboard shortcuts and a live preview of the scene, the Game view and audio.", "Де ви монтуєте, як у відеоредакторі: лінійка з кадрами чи таймкодом, обрізка, лезо, ripple-правки, маркери, прилипання, гарячі клавіші та живе прев'ю сцени, Game view і звуку."), HonamiEditorIcons.GraphWhite),
                (HonamiDocLocalization.Get("Modules (tracks)", "Модулі (треки)"), HonamiDocLocalization.Get("Each row is a module: Camera Shots, Animation, Audio, Screen Fade, Activation, events, curves, or your own. Modules can be nested inside the items of other modules, so a line of dialogue can ride along with its shot.", "Кожен рядок це модуль: Camera Shots, Animation, Audio, Screen Fade, Activation, події, криві або ваш власний. Модулі можна вкладати в item-и інших модулів, тож репліка може їхати разом зі своїм шотом."), HonamiEditorIcons.BlendTreeWhite)
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Why the asset does not hold scene objects", "Чому ассет не зберігає об'єкти сцени"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Unity assets cannot point at objects inside a scene. So the timeline stores only keys, and the Director in the scene maps them to real objects: a binding per track (\"this Animation track drives Kira\") and a reference per item that needs its own object (\"this shot sits on the Shot 3 anchor\"). The benefit is reuse: two directors can play the same timeline with different actors, and moving or renaming objects in the Hierarchy breaks nothing.",
                "Ассети Unity не можуть посилатися на об'єкти всередині сцени. Тому таймлайн зберігає лише ключі, а Director у сцені зіставляє їх зі справжніми об'єктами: біндинг на кожен трек («цей трек Animation веде Kira») і посилання на кожен item, якому потрібен свій об'єкт («цей шот стоїть на якорі Shot 3»). Виграш у повторному використанні: два директори можуть грати той самий таймлайн з різними акторами, а переміщення чи перейменування об'єктів у Hierarchy нічого не ламає."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Cutscene timeline or state timeline?", "Таймлайн катсцени чи таймлайн стану?"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                ("", 150),
                (HonamiDocLocalization.Get("Honami Timeline + Director", "Honami Timeline + Director"), 0),
                (HonamiDocLocalization.Get("State of a Honami Controller", "Стан Honami Controller"), 0),
                (HonamiDocLocalization.Get("Plays when", "Грає коли"), HonamiDocLocalization.Get("You call Play, or via Play Trigger (Awake/Start/OnEnable)", "Ви викликаєте Play, або через Play Trigger (Awake/Start/OnEnable)"), HonamiDocLocalization.Get("The state is the current state of its layer", "Стан поточний на своєму шарі")),
                (HonamiDocLocalization.Get("Time comes from", "Час береться з"), HonamiDocLocalization.Get("The Director: game time, unscaled time or your code", "Director: ігровий час, unscaled або ваш код"), HonamiDocLocalization.Get("The state's motion", "Руху стану")),
                (HonamiDocLocalization.Get("Frame rate", "Частота кадрів"), HonamiDocLocalization.Get("The timeline's own Frame Rate", "Власний Frame Rate таймлайну"), HonamiDocLocalization.Get("The frame rate of the state's clip, if it has one", "Частота кліпа стану, якщо він є")),
                (HonamiDocLocalization.Get("Drives", "Керує"), HonamiDocLocalization.Get("Any number of scene objects through bindings", "Будь-якою кількістю об'єктів сцени через біндинги"), HonamiDocLocalization.Get("The character it belongs to", "Персонажем, якому належить")),
                (HonamiDocLocalization.Get("Typical use", "Типово"), HonamiDocLocalization.Get("Cutscenes, scripted sequences, intros, boss entrances", "Катсцени, скриптові сцени, інтро, поява боса"), HonamiDocLocalization.Get("Footstep sounds, hit frames, VFX on an attack", "Звуки кроків, кадри удару, VFX на атаці"))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Where to go next", "Куди далі"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Your First Cutscene", "Перша катсцена"), HonamiDocLocalization.Get("A fifteen-minute walkthrough from an empty asset to a cutscene your game triggers.", "П'ятнадцятихвилинний шлях від порожнього ассета до катсцени, яку запускає гра.")),
                (HonamiDocLocalization.Get("The Timeline Window", "Вікно Timeline"), HonamiDocLocalization.Get("Every part of the window, time display, zoom, preview and markers.", "Кожна частина вікна, показ часу, зум, прев'ю та маркери.")),
                (HonamiDocLocalization.Get("Editing & Shortcuts", "Монтаж і гарячі клавіші"), HonamiDocLocalization.Get("Moving, trimming, blending, cutting, ripple edits and the full shortcut list.", "Переміщення, обрізка, бленди, розрізи, ripple-правки та повний список клавіш.")),
                (HonamiDocLocalization.Get("Camera Shots", "Камерні шоти"), HonamiDocLocalization.Get("Anchors, field of view, cuts, blends and handing the camera back to gameplay.", "Якорі, поле зору, склейки, бленди та повернення камери геймплею.")),
                (HonamiDocLocalization.Get("Animation, Audio & Fades", "Анімація, аудіо та фейди"), HonamiDocLocalization.Get("The other built-in tracks and nesting tracks inside items.", "Інші вбудовані треки та вкладення треків в item-и.")),
                (HonamiDocLocalization.Get("The Director", "Director"), HonamiDocLocalization.Get("Playback settings, bindings, scene references, events and the scripting API.", "Налаштування відтворення, біндинги, посилання на сцену, події та API.")),
                (HonamiDocLocalization.Get("Director Reactions", "Реакції на Director"), HonamiDocLocalization.Get("Components that lock the player, swap level objects or fire events around every cutscene, with no timeline edits.", "Компоненти, що блокують гравця, підміняють об'єкти рівня чи запускають події навколо кожної катсцени, без правок таймлайну.")),
                (HonamiDocLocalization.Get("Cutscene Recipes", "Рецепти катсцен"), HonamiDocLocalization.Get("Skippable cutscenes, taking and returning control, dialogue, reuse and troubleshooting.", "Катсцени з пропуском, забирання й повернення керування, діалоги, повторне використання та проблеми.")),
                (HonamiDocLocalization.Get("Custom Timeline Modules", "Власні модулі Timeline"), HonamiDocLocalization.Get("In Developer API: writing your own track, with its editor, preview and handles.", "У розділі API: як написати власний трек з редактором, прев'ю та ручками."))
            );
        }
    }
}
