using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneEditingPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Editing & Shortcuts", "Монтаж і гарячі клавіші");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "editing shortcuts keyboard select move snap trim blend curve easing blade split ripple delete duplicate copy paste nudge lock drag drop монтаж клавіші виділення переміщення прилипання обрізка крива плавність лезо розріз дублювання копіювання блокування";
        public int Order => 563;
        public int EstimatedReadTime => 8;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("Selecting, moving and snapping items", "Виділення, переміщення та прилипання item-ів"),
                HonamiDocLocalization.Get("Trimming, blends and crossfades", "Обрізка, бленди та crossfade"),
                HonamiDocLocalization.Get("Cutting, ripple edits, duplicating and the clipboard", "Розрізи, ripple-правки, дублювання та буфер обміну"),
                HonamiDocLocalization.Get("Dragging assets and scene objects in", "Перетягування ассетів і об'єктів сцени"),
                HonamiDocLocalization.Get("Linking items so they move as one group", "Лінкування item-ів, щоб вони рухалися як одна група"),
                HonamiDocLocalization.Get("Every keyboard shortcut", "Усі гарячі клавіші")
            });

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Selecting and moving", "Виділення та переміщення"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Click an item to select it. Shift or Ctrl adds to or removes from the selection, dragging across empty space draws a selection box, Ctrl+A selects everything and double-clicking a track header selects that track. Clicking empty space clears the selection and shows the timeline itself in the properties, unless the click lands in the empty stretch between two items of a track: that selects the gap (it lights up), and Delete then ripple deletes it, sliding everything after it on the track left. Right-clicking a gap offers the same Ripple Delete Gap command.",
                "Клік по item-у виділяє його. Shift чи Ctrl додає до виділення або прибирає з нього, перетягування по порожньому місцю малює рамку, Ctrl+A виділяє все, а подвійний клік по заголовку треку виділяє цей трек. Клік по порожньому місцю знімає виділення й показує у властивостях сам таймлайн, якщо тільки він не влучив у порожню ділянку між двома item-ами треку: тоді виділяється дірка (вона підсвічується), і Delete ripple-видаляє її, зсуваючи все, що після неї на треку, вліво. Правий клік по дірці дає ту саму команду Ripple Delete Gap."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Drag any selected item and the whole selection moves together, keeping its spacing. It stops at zero, so nothing slides before the start. When every selected item sits on one track, dragging up or down onto another track of the same kind moves them there; the target row lights up. Hold Alt when you release to leave the originals in place and drop copies instead. Alt+Left and Alt+Right nudge the selection by one frame, with Shift by ten. One drag is always one undo step.",
                "Потягніть будь-який виділений item, і все виділення рухається разом, зберігаючи відстані. Воно зупиняється на нулі, тож нічого не з'їжджає перед початок. Коли всі виділені item-и на одному треку, перетягування вгору чи вниз на інший трек того ж типу переносить їх туди; цільовий рядок підсвічується. Тримайте Alt у момент відпускання, щоб лишити оригінали на місці й покласти копії. Alt+Вліво та Alt+Вправо зсувають виділення на кадр, із Shift на десять. Одне перетягування завжди один крок undo."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Linking items", "Лінкування item-ів"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Select two or more items, on one track or across several, and press Ctrl+L to link them. Linked items then drag as one group: grab any member and the whole group follows by the same amount, each item keeping its own offset, so a run of camera shots, sounds or props can be slid along the timeline as a unit without losing its rhythm. The same works for markers on the ruler, including LOOP regions and WAIT markers: link a loop region with the cue markers around it and dragging any of them moves the whole set, the loop keeping its length. Clips and markers can share one group too, so a LOOP region or a WAIT marker can be tied to the shot it belongs to and keep its place when that shot moves: click the clip, Shift-click the marker, then press Ctrl+L. Linking grows the selection to the groups it touches and merges them, so no member is pulled out of a group it already had. The Links button in the toolbar offers the same commands, and every linked item or marker is marked with a coloured strip or cap, one colour per group. Select Linked Group in that menu re-selects the whole group from any member.",
                "Виділіть два або більше item-ів, на одному треку чи на кількох, і натисніть Ctrl+L, щоб злінкувати їх. Злінковані item-и далі тягнуться як одна група: схопіть будь-який член, і вся група зсунеться на ту саму величину, кожен item зберігає власний зсув, тож низку камерних шотів, звуків чи пропів можна посунути таймлайном як одне ціле, не втративши ритму. Те саме працює для маркерів на лінійці, зокрема LOOP-регіонів і маркерів WAIT: злінкуйте loop-регіон із сусідніми cue-маркерами, і перетягування будь-якого з них зсуне весь набір, а loop збереже довжину. Item-и й маркери можуть бути в одній групі, тож LOOP-регіон чи маркер WAIT можна прив'язати до шоту, якому він належить, і він збереже місце, коли шот зсувається: клацніть item, Shift-клік по маркеру, тоді Ctrl+L. Лінкування розширює виділення до груп, яких воно торкається, і об'єднує їх, тож жоден член не випадає зі своєї попередньої групи. Кнопка Links на тулбарі дає ті самі команди, а кожен злінкований item чи маркер позначено кольоровою смужкою або шапкою, свій колір на групу. Пункт Select Linked Group у тому меню повторно виділяє всю групу з будь-якого її члена."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Ctrl+Shift+L links the selection and lays it out end to end in its current order, so the items end up one after another with the Sequence Gap left between them (change it in the Links menu). Unlink Selected, in the item's right-click menu or the Links menu, breaks the group without moving anything. A link is an editing aid only: it never touches playback, and copies made with Alt-drag, Ctrl+D or paste are new items and start unlinked.",
                "Ctrl+Shift+L лінкує виділене й розкладає його підряд у поточному порядку, тож item-и стають один за одним із проміжком Sequence Gap (змінюється в меню Links). Unlink Selected у контекстному меню item-а чи в меню Links розриває групу, нічого не зсуваючи. Лінк це лише інструмент монтажу: він не впливає на відтворення, а копії, зроблені Alt-drag, Ctrl+D чи вставкою, є новими item-ами й починають без лінку."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Snapping", "Прилипання"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "With Snap on, the start and the end of whatever you drag stick to the edges of other items, markers and the loop region, the playhead, the start and the end of the timeline, and when nothing is near, to whole frames. A vertical line shows what it snapped to. Hold Shift while dragging to invert Snap for that drag. Trim handles, blend handles, markers and the playhead snap the same way.",
                "Коли Snap увімкнено, початок і кінець того, що ви тягнете, прилипають до країв інших item-ів, маркерів і loop-регіону, плейхеда, початку й кінця таймлайну, а коли поруч нічого немає, до цілих кадрів. Вертикальна лінія показує, до чого прилипло. Тримайте Shift під час перетягування, щоб інвертувати Snap на цей раз. Ручки обрізки, ручки блендів, маркери й плейхед прилипають так само."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Trimming and blends", "Обрізка та бленди"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Drag the left or right edge of a block to trim it. Trimming the start of an animation or audio clip does not slide its content: the clip keeps playing the same frame at the same moment, only its beginning is hidden, exactly like a video editor. Q or [ trims the start of the selection to the playhead, W or ] trims the end; with nothing selected they trim whatever lies under the playhead.",
                "Тягніть лівий або правий край блока, щоб обрізати його. Обрізка початку анімаційного чи аудіокліпа не зсуває вміст: кліп грає той самий кадр у той самий момент, лише його початок сховано, рівно як у відеоредакторі. Q або [ обрізає початок виділеного до плейхеда, W або ] обрізає кінець; без виділення вони обрізають те, що лежить під плейхедом."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Items that can blend (camera shots, animation clips, audio) show two small round handles in their top corners when you hover them. Drag the left one to set Blend In and the right one for Blend Out; a shaded ramp shows the curve. Overlapping two items on one track makes a crossfade across the overlap without touching any handle: shots move the camera from one to the other, clips blend their poses, and sounds fade one out while the other fades in. The shape of each fade is yours to choose: in the properties panel Blend In Curve and Blend Out Curve offer an easing preset or a custom curve, and the ramps in the window redraw to match. A back or elastic curve gives the move a slight overshoot at the end of the blend.",
                "Item-и, що вміють блендитися (камерні шоти, анімаційні кліпи, аудіо), при наведенні показують дві маленькі круглі ручки у верхніх кутах. Ліва задає Blend In, права Blend Out; затінена рампа показує криву. Перекриття двох item-ів на одному треку робить crossfade впродовж перекриття, без жодних ручок: шоти ведуть камеру від одного до іншого, кліпи змішують пози, а звуки один згасає, поки інший наростає. Форму кожного фейду ви обираєте самі: у панелі властивостей Blend In Curve і Blend Out Curve пропонують пресет плавності або власну криву, а рампи у вікні перемальовуються відповідно. Крива Back чи Elastic додає легкий overshoot наприкінці бленду."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Cutting and ripple edits", "Розрізи та ripple-правки"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Press B for the Blade tool: a red line follows the cursor and clicking an item cuts it there. V or Esc returns to Select. S or Ctrl+K cuts the selection at the playhead, or every item under the playhead when nothing is selected, so one key cuts all tracks at once (only the highlighted track, if you clicked a track header). Both halves keep playing seamlessly, and a Parameter Curve is cut into two curves that together keep the original shape.",
                "Натисніть B для інструмента Blade: червона лінія йде за курсором, а клік по item-у розрізає його там. V або Esc повертає Select. S або Ctrl+K розрізає виділене на плейхеді, або всі item-и під плейхедом, якщо нічого не виділено, тож одна клавіша ріже всі треки разом (лише підсвічений трек, якщо ви клацнули по його заголовку). Обидві половини грають без шва, а Parameter Curve ріжеться на дві криві, які разом зберігають початкову форму."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Delete removes the selection. Shift+Delete is a ripple delete: it removes the items and slides everything after them on the same track left, closing the gap. Ctrl+D duplicates the selection right after itself. Ctrl+C, Ctrl+X and Ctrl+V copy, cut and paste at the playhead; pasted items keep their spacing and land on the selected track when they fit it, otherwise on the track they were copied from.",
                "Delete видаляє виділене. Shift+Delete це ripple-видалення: воно прибирає item-и й зсуває все, що після них на тому ж треку, вліво, закриваючи дірку. Ctrl+D дублює виділене одразу за ним. Ctrl+C, Ctrl+X і Ctrl+V копіюють, вирізають і вставляють на плейхед; вставлені item-и зберігають відстані й лягають на виділений трек, якщо підходять до нього, інакше на трек, з якого їх скопіювали."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Locking", "Блокування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The padlock in a track header locks the track: its items are hatched, can still be selected and inspected, but cannot be moved, trimmed, cut, pasted onto or deleted. Locking a track also locks the modules nested inside its items. Lock the music once it is timed, then edit freely around it.",
                "Замок у заголовку блокує трек: його item-и заштриховані, їх можна виділяти й дивитися, але не можна рухати, обрізати, різати, вставляти на них чи видаляти. Блокування треку блокує й модулі, вкладені в його item-и. Заблокуйте музику, щойно вона вирівняна, і монтуйте навколо неї вільно."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Dragging things in", "Перетягування ззовні"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Drop", "Що кинути"), 170),
                (HonamiDocLocalization.Get("On a track", "На трек"), 0),
                (HonamiDocLocalization.Get("On empty space", "На порожнє місце"), 0),
                (HonamiDocLocalization.Get("Animation Clips", "Animation Clip-и"), HonamiDocLocalization.Get("Added to an Animation track at the drop point, several clips end to end", "Додаються на трек Animation у точці падіння, кілька кліпів підряд"), HonamiDocLocalization.Get("A new Animation track with the clips", "Новий трек Animation з цими кліпами")),
                (HonamiDocLocalization.Get("Audio Clips", "Audio Clip-и"), HonamiDocLocalization.Get("Added to an Audio track", "Додаються на трек Audio"), HonamiDocLocalization.Get("A new Audio track", "Новий трек Audio")),
                (HonamiDocLocalization.Get("A scene GameObject", "GameObject зі сцени"), HonamiDocLocalization.Get("On Camera Shots: a shot anchored to it. On any track: bind the track to it.", "На Camera Shots: шот із якорем на ньому. На будь-який трек: прив'язати трек до нього."), HonamiDocLocalization.Get("A menu of new tracks bound to it: Animation, Activation, Camera Shots for a camera, Audio for an audio source, and so on", "Меню нових треків, прив'язаних до нього: Animation, Activation, Camera Shots для камери, Audio для джерела звуку тощо"))
            );
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Binding a scene object creates a Director first if the timeline has none. A ghost block shows where dropped assets will land.",
                "Прив'язка об'єкта сцени спершу створює Director, якщо в таймлайну його немає. Напівпрозорий блок показує, куди ляжуть кинуті ассети."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("All shortcuts", "Усі гарячі клавіші"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Keys", "Клавіші"), 150),
                (HonamiDocLocalization.Get("Action", "Дія"), 0),
                ("", 0),
                ("Space", HonamiDocLocalization.Get("Play / pause", "Відтворення / пауза"), ""),
                ("J  K  L", HonamiDocLocalization.Get("Shuttle backward, stop, forward; J or L again doubles the speed up to 8×", "Шатл назад, стоп, уперед; повторне J чи L подвоює швидкість до 8×"), ""),
                ("← →", HonamiDocLocalization.Get("Previous / next frame, ten with Shift", "Попередній / наступний кадр, десять із Shift"), ""),
                ("↑ ↓", HonamiDocLocalization.Get("Previous / next edit point: item edges and markers", "Попередня / наступна точка монтажу: краї item-ів і маркери"), ""),
                ("Home  End  Ctrl+← →", HonamiDocLocalization.Get("Start / end", "Початок / кінець"), ""),
                ("V  B  Esc", HonamiDocLocalization.Get("Select tool, Blade tool, back to Select or clear the selection", "Інструмент виділення, лезо, назад до виділення чи зняти виділення"), ""),
                ("S  Ctrl+K", HonamiDocLocalization.Get("Split at the playhead", "Розрізати на плейхеді"), ""),
                ("Q  [   W  ]", HonamiDocLocalization.Get("Trim start / end to the playhead", "Обрізати початок / кінець до плейхеда"), ""),
                ("Alt+← →", HonamiDocLocalization.Get("Nudge the selection by a frame, ten with Shift", "Зсунути виділення на кадр, на десять із Shift"), ""),
                ("Del  Shift+Del", HonamiDocLocalization.Get("Delete / ripple delete; on a selected gap, collapse it", "Видалити / ripple-видалити; на виділеній дірці закрити її"), ""),
                ("Ctrl+C  X  V", HonamiDocLocalization.Get("Copy, cut, paste at the playhead", "Копіювати, вирізати, вставити на плейхед"), ""),
                ("Ctrl+D  Ctrl+A", HonamiDocLocalization.Get("Duplicate, select all", "Дублювати, виділити все"), ""),
                ("Ctrl+L", HonamiDocLocalization.Get("Link the selected items or markers; Ctrl+Shift+L links and lays them out one after another", "Злінкувати виділені item-и чи маркери; Ctrl+Shift+L лінкує й розкладає підряд"), ""),
                ("M  I  O", HonamiDocLocalization.Get("Add a marker, loop region in, loop region out", "Додати маркер, початок loop-регіону, кінець loop-регіону"), ""),
                ("F  \\  =  -", HonamiDocLocalization.Get("Frame the selection, fit everything, zoom in, zoom out", "Показати виділене, вмістити все, наблизити, віддалити"), ""),
                ("Ctrl+wheel", HonamiDocLocalization.Get("Zoom at the cursor", "Зум у точці курсора"), ""),
                ("Shift+wheel  Middle drag", HonamiDocLocalization.Get("Scroll through time, pan", "Гортати час, панорамувати"), ""),
                ("Alt (on release)", HonamiDocLocalization.Get("Drop copies instead of moving", "Покласти копії замість переміщення"), ""),
                ("Shift (while dragging)", HonamiDocLocalization.Get("Invert Snap", "Інвертувати Snap"), ""),
                ("?  F1", HonamiDocLocalization.Get("Shortcut sheet in the window", "Шпаргалка клавіш у вікні"), "")
            );
        }
    }
}
