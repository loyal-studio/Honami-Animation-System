using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneTimelineWindowPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("The Timeline Window", "Вікно Timeline");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "timeline window ruler frames timecode seconds zoom scroll preview marker loop region tab track header properties transport вікно лінійка кадри таймкод зум прев'ю маркер loop трек властивості";
        public int Order => 562;
        public int EstimatedReadTime => 8;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("What every part of the window does", "Що робить кожна частина вікна"),
                HonamiDocLocalization.Get("How time is shown in frames, timecode or seconds", "Як час показується в кадрах, таймкоді чи секундах"),
                HonamiDocLocalization.Get("How preview drives the scene and cleans up after itself", "Як прев'ю керує сценою і прибирає за собою"),
                HonamiDocLocalization.Get("Markers and loop regions", "Маркери та loop-регіони")
            });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Open it from Window → Honami → Honami Timeline, by double-clicking a timeline asset, or with Open in Timeline Window on a Director. Each tab holds one timeline, one state or one clip, and tabs survive a script reload. The same window edits the timelines of states, so everything below except markers and the Director works there too.",
                "Відкривається через Window → Honami → Honami Timeline, подвійним кліком по ассету таймлайну або кнопкою Open in Timeline Window на Director. Кожна вкладка тримає один таймлайн, один стан чи один кліп, і вкладки переживають перезавантаження скриптів. Те саме вікно редагує таймлайни станів, тож усе нижче, крім маркерів і Director, працює і там."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Layout", "Будова"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Toolbar", "Тулбар"), HonamiDocLocalization.Get("The timeline asset, its Frame Rate, the Director that plays it (or + Director when there is none), the Select and Blade tools, Snap, Frames, Props and ? for the shortcut sheet.", "Ассет таймлайну, його Frame Rate, Director, що його грає (або + Director, якщо його немає), інструменти Select і Blade, Snap, Frames, Props і ? для шпаргалки клавіш.")),
                (HonamiDocLocalization.Get("Track headers", "Заголовки треків"), HonamiDocLocalization.Get("One row per module, with its colour, icon and name. The eye mutes the track, the padlock locks it, + adds a module inside its items, and a warning icon means its binding is missing. Drag a header up or down to reorder tracks, double-click it to select all its items, right-click for more.", "Рядок на кожен модуль з його кольором, іконкою та назвою. Око глушить трек, замок блокує, + додає модуль усередину його item-ів, а значок попередження означає, що бракує біндингу. Тягніть заголовок угору чи вниз, щоб переставити треки, подвійний клік виділяє всі item-и, правий клік дає більше.")),
                (HonamiDocLocalization.Get("Ruler", "Лінійка"), HonamiDocLocalization.Get("Pinned to the top. Click or drag it to move the playhead, double-click to add a marker, right-click for markers, loop points and the time display. The strip along its top edge holds markers and the loop region.", "Закріплена вгорі. Клік чи перетягування рухає плейхед, подвійний клік додає маркер, правий клік дає маркери, точки loop і вибір показу часу. Смуга вздовж верхнього краю тримає маркери та loop-регіон.")),
                (HonamiDocLocalization.Get("Tracks area", "Область треків"), HonamiDocLocalization.Get("Items as blocks (ranges) or markers (instants). Right-click an empty spot to add an item there or a new module; right-click an item for split, trim, copy, delete and module-specific actions.", "Item-и як блоки (проміжки) чи маркери (миттєві). Правий клік на порожньому місці додає item або новий модуль; правий клік по item-у дає розріз, обрізку, копіювання, видалення та дії конкретного модуля.")),
                (HonamiDocLocalization.Get("Properties", "Властивості"), HonamiDocLocalization.Get("Everything about the selection: the timeline with its Director settings when nothing is selected, a module with its binding, an item, or a marker. Props in the toolbar hides it; drag its edge to resize.", "Усе про виділене: таймлайн з налаштуваннями Director, коли нічого не виділено, модуль з біндингом, item або маркер. Props на тулбарі ховає панель; тягніть її край, щоб змінити ширину.")),
                (HonamiDocLocalization.Get("Transport", "Транспорт"), HonamiDocLocalization.Get("The current time (click to type one), Preview, go to start, previous edit point, previous frame, play, next frame, next edit point, go to end, the shuttle speed, Loop and the zoom slider with Fit.", "Поточний час (клік, щоб увести), Preview, на початок, попередня точка монтажу, попередній кадр, відтворення, наступний кадр, наступна точка, у кінець, швидкість шатла, Loop і повзунок зуму з Fit."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Time: frames, timecode, seconds", "Час: кадри, таймкод, секунди"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Animators count frames, editors count timecode. The Frames toggle switches between the two. With Frames on, the ruler is numbered in frames and the transport shows seconds and the frame number, 0.50s / 15f. With Frames off, a cutscene shows timecode, minutes:seconds:frames, with the duration and frame rate next to it, 00:00:15 / 00:06:00 · 30 fps; right-click the ruler for plain seconds instead. The ruler picks its spacing from the zoom, down to single frames.",
                "Аніматори рахують кадри, монтажери рахують таймкод. Перемикач Frames обирає між ними. Коли Frames увімкнено, лінійка пронумерована в кадрах, а транспорт показує секунди й номер кадру, 0.50s / 15f. Коли вимкнено, катсцена показує таймкод, хвилини:секунди:кадри, з тривалістю й частотою поруч, 00:00:15 / 00:06:00 · 30 fps; правий клік по лінійці перемикає на звичайні секунди. Крок поділок лінійка бере від зуму, аж до окремих кадрів."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Click the time in the transport bar to type one. 120 or 120f is a frame, 2.5s is seconds, 1:05:12 is minutes, seconds and frames. A cutscene's frame rate is its own Frame Rate setting; a state has none of its own and borrows the frame rate of its clip, and without a clip it shows seconds only.",
                "Клацніть по часу на транспорті, щоб увести його. 120 або 120f це кадр, 2.5s секунди, 1:05:12 хвилини, секунди й кадри. Частота кадрів катсцени це її власне налаштування Frame Rate; у стану власної немає, він бере частоту свого кліпа, а без кліпа показує лише секунди."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The timeline ends where its longest track ends, and never before one second. Set Duration Override in the timeline's properties to fix the length, for example to hold on the last shot. The area past the end is shaded and bounded by a red line.",
                "Таймлайн закінчується там, де закінчується найдовший трек, і не раніше ніж через секунду. Задайте Duration Override у властивостях таймлайну, щоб зафіксувати довжину, наприклад щоб потриматися на останньому шоті. Область після кінця затінена й обмежена червоною лінією."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Zoom and navigation", "Зум і навігація"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Ctrl + mouse wheel (or Alt + wheel) zooms at the cursor: the moment under the mouse stays put.", "Ctrl + коліщатко (або Alt + коліщатко) зумить у точці курсора: момент під мишею лишається на місці."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("The wheel scrolls tracks, Shift + wheel scrolls through time, the middle button pans both ways.", "Коліщатко гортає треки, Shift + коліщатко гортає час, середня кнопка панорамує в обидва боки."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("F frames the selection, \\ fits the whole timeline, = and - zoom around the playhead.", "F показує виділене, \\ вміщує весь таймлайн, = і - зумлять навколо плейхеда."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("During playback the view pages along with the playhead.", "Під час відтворення вигляд перегортається слідом за плейхедом."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Preview", "Прев'ю"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "With Preview on, the scene follows the playhead in edit mode: characters take their poses, the camera sits on the current shot, objects switch on and off, the Game view fades, and audio plays during playback. While you drag the playhead, audio plays short snippets so you can find a beat by ear. Everything preview changes is recorded, and turning Preview off or closing the tab puts it all back exactly as it was. Events, logs and parameter curves do not run in preview: they are gameplay, and firing them in edit mode could change your scene for good.",
                "Коли Preview увімкнено, сцена в edit mode йде за плейхедом: персонажі стають у пози, камера стоїть на поточному шоті, об'єкти вмикаються й вимикаються, Game view затемнюється, а під час відтворення грає звук. Поки ви тягнете плейхед, звук грає короткі фрагменти, щоб можна було знайти біт на слух. Усе, що змінює прев'ю, записується, і вимкнення Preview чи закриття вкладки повертають усе рівно так, як було. Події, логи й криві параметрів у прев'ю не виконуються: це геймплей, і запуск в edit mode міг би назавжди змінити сцену."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Space plays, J, K and L shuttle backward, stop and forward, and pressing J or L again doubles the speed up to 8×. Loop in the transport repeats the whole timeline.",
                "Space відтворює, J, K і L дають шатл назад, стоп і вперед, а повторне J чи L подвоює швидкість до 8×. Loop на транспорті повторює весь таймлайн."
            ));
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "When two directors in the open scenes play the same timeline, preview and bindings use the one selected in the Hierarchy. Select the other one to edit its actors.",
                "Коли у відкритих сценах два директори грають той самий таймлайн, прев'ю та біндинги беруть той, що виділений у Hierarchy. Виділіть інший, щоб редагувати його акторів."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Markers", "Маркери"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Markers name moments: the beat where the music drops, the line where a door opens, the point a skip should land on. Double-click the ruler or press M to add one at the playhead. Drag it to move it, click it to see its name, colour and note in the properties, double-click it to jump there, right-click to delete. Markers draw a thin line down through the tracks, items snap to them, and the Up and Down keys stop on them. At runtime SeekToMarker jumps a Director to a marker by name. Link markers with Ctrl+L and they move together, so a LOOP region can follow the cue markers around it; a marker and a clip can share a link group as well, so the region can follow the shot it belongs to.",
                "Маркери дають імена моментам: біт, де падає музика, репліка, на якій відчиняються двері, точка, куди має перескочити пропуск. Подвійний клік по лінійці або M додає маркер на плейхеді. Тягніть його, щоб перемістити, клацніть, щоб побачити назву, колір і примітку у властивостях, подвійний клік переносить туди плейхед, правий клік видаляє. Маркери малюють тонку лінію крізь треки, item-и до них прилипають, а клавіші вгору й вниз на них зупиняються. У runtime SeekToMarker переносить Director до маркера за назвою. Злінкуйте кілька маркерів через Ctrl+L, і вони рухатимуться разом, тож LOOP-регіон може йти за cue-маркерами навколо нього. Маркер і item можуть бути в одній групі, тож регіон може йти за шотом, якому належить."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Loop region", "Loop-регіон"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Press I and O to mark the start and end of a part you are polishing. While the playhead is inside the region, playback in the window loops it, so you can watch a single camera move or a single line over and over. Drag the bar to move the region or its ends to resize it. The loop region is an editing aid: a Director at runtime plays straight through it.",
                "Натисніть I та O, щоб позначити початок і кінець фрагмента, який ви доводите. Поки плейхед усередині регіону, відтворення у вікні його повторює, тож можна дивитися один рух камери чи одну репліку знову й знову. Тягніть смугу, щоб перемістити регіон, або її краї, щоб змінити розмір. Loop-регіон це інструмент монтажу: Director у runtime грає крізь нього без зупинки."
            ));
        }
    }
}
