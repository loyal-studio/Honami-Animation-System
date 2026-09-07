using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class ClipPlayerPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Clip Player", "Clip Player");
        public string Category => HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти");
        public string SearchKeywords => "clip player legacy animation component simple animator crossfade playqueued blend wrap mode once loop pingpong clampforever npc character background crowd behaviour tree prop door environment sample scrub pose addclip removeclip rebuild runtime clips currentclip isplayingany rewind trygetstate preview inspector recipes кліп плеєр простий аніматор кросфейд черга оточення пропси двері нпс персонаж фонові персонажі натовп семпл поза скрабінг додати кліп перебудова рантайм прев'ю інспектор рецепти";
        public int Order => 280;
        public int EstimatedReadTime => 13;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami Clip Player is the Honami runtime with the graph taken out. Nodes, transitions, conditions, exit times, parameters, blend trees, sub-nodes and masks are not missing from it — they were removed on purpose, because for most objects that machinery is something you author, debug and maintain in order to express a decision your code had already made. What is left is the part that actually produces the animation: clips, weights, crossfades, layers and wrap modes. It is what Unity's old Animation component used to be, rebuilt on the same PlayableGraph core as the rest of Honami.",
                "Honami Clip Player — це рантайм Honami з вийнятим графом. Ноди, переходи, умови, exit time, параметри, blend tree, підвузли та маски в ньому не відсутні — їх прибрали навмисно, бо для більшості об'єктів уся ця машинерія існує лише для того, щоб її авторити, дебажити й підтримувати заради вираження рішення, яке ваш код уже прийняв. Лишилось те, що насправді робить анімацію: кліпи, ваги, кросфейди, шари й режими wrap. Це те, чим був старий компонент Animation в Unity, перебудований на тому ж ядрі PlayableGraph, що й решта Honami."
            ));

            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("When a clip list beats a controller graph", "Коли список кліпів кращий за граф-контролер"),
                    HonamiDocLocalization.Get("Playing, crossfading, queueing and blending clips", "Відтворення, кросфейд, черга та змішування кліпів"),
                    HonamiDocLocalization.Get("Wrap modes, and what happens when a one-shot ends", "Режими wrap і що стається, коли одноразовий кліп завершується"),
                    HonamiDocLocalization.Get("Clip layers and how their weights are normalized", "Шари кліпів і як нормалізуються їхні ваги"),
                    HonamiDocLocalization.Get("Sampling a clip as a pose you drive by hand", "Семплінг кліпу як пози, яку ви ведете вручну"),
                    HonamiDocLocalization.Get("Adding and removing clips while the game runs", "Додавання й видалення кліпів під час гри"),
                    HonamiDocLocalization.Get("Auditioning the whole list without entering Play Mode", "Прослуховування всього списку без входу в Play Mode"),
                    HonamiDocLocalization.Get("Living alongside a Linked Animator brain", "Співіснування з мозком Linked Animator")
                },
                new[]
                {
                    HonamiDocLocalization.Get("First Steps — an Animator component with an empty Controller slot", "Перші кроки — компонент Animator з порожнім слотом Controller")
                });

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Same hard requirement as the Honami Animator: a Unity Animator must sit on the same GameObject with its Controller slot EMPTY. The Clip Player builds and drives its own PlayableGraph.",
                "Та сама жорстка вимога, що й для Honami Animator: на тому ж GameObject має бути Unity Animator із ПОРОЖНІМ слотом Controller. Clip Player будує та веде власний PlayableGraph."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            // --- 1. Which component ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("1. Animator or Clip Player?", "1. Animator чи Clip Player?"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Both components inherit the same base, so the Animator host, initial pose, FPS cap, rig chain, update modes, global weight and linking all behave identically. The difference is only in what decides which animation plays.",
                "Обидва компоненти успадковують спільну базу, тож хост Animator, Initial Pose, FPS Cap, ланцюг рігів, режими оновлення, Global Weight і лінкування працюють однаково. Різниця лише в тому, що саме вирішує, яка анімація грає."
            ));

            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Question", "Питання"), 230),
                (HonamiDocLocalization.Get("Honami Animator", "Honami Animator"), 190),
                (HonamiDocLocalization.Get("Clip Player", "Clip Player"), 0),
                (HonamiDocLocalization.Get("What decides the animation?", "Що вирішує, яка анімація грає?"),
                 HonamiDocLocalization.Get("Transitions and conditions", "Переходи та умови"),
                 HonamiDocLocalization.Get("Your code, by clip name", "Ваш код, за іменем кліпу")),
                (HonamiDocLocalization.Get("Where does the data live?", "Де живуть дані?"),
                 HonamiDocLocalization.Get("Controller asset, shared", "Ассет-контролер, спільний"),
                 HonamiDocLocalization.Get("On the object, local", "На об'єкті, локально")),
                (HonamiDocLocalization.Get("Parameters, blend trees, masks?", "Параметри, blend tree, маски?"),
                 HonamiDocLocalization.Get("Yes", "Так"),
                 HonamiDocLocalization.Get("No", "Ні")),
                (HonamiDocLocalization.Get("Good for", "Добре для"),
                 HonamiDocLocalization.Get("Behaviour authored in the graph — the player, bosses, locomotion that needs blend trees or masks", "Поведінка, зверстана в графі — гравець, боси, локомоція, якій потрібні blend tree чи маски"),
                 HonamiDocLocalization.Get("Behaviour decided elsewhere — NPCs, background characters, creatures, machines, props", "Поведінка, вирішена деінде — NPC, фонові персонажі, істоти, механізми, пропси"))
            );

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "That line has nothing to do with how important the object is, and a Clip Player is not a props-only component. Most NPCs never needed a state machine: their idle, walk, talk and two reactions are already chosen by a behaviour tree, a dialogue system or a spawner, and wrapping that in a controller graph only means authoring the same decision twice. Give those characters a clip list and let the code that already knows keep deciding.",
                "Ця межа не має нічого спільного з тим, наскільки важливий об'єкт, і Clip Player — не компонент «лише для пропсів». Більшості NPC стейт-машина ніколи й не була потрібна: їхні idle, walk, talk і дві реакції вже обирає behaviour tree, система діалогів чи спавнер, а загортання цього в граф-контролер означає лише зверстати те саме рішення двічі. Дайте таким персонажам список кліпів і лишіть рішення тому коду, який його вже приймає."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "So the question is not what the Clip Player is missing, it is what the graph would be buying you. It pays for itself when the behaviour genuinely lives inside it: a 1D locomotion blend between motions, a weighted upper-body mask, an override controller per variant, conditions and exit times an animator should tune without touching code. When none of that is true, every node and transition is ceremony around a call your code was going to make anyway.",
                "Тож питання не в тому, чого Clip Player не має, а в тому, що вам купив би граф. Він окуповується, коли поведінка справді живе всередині нього: 1D-бленд локомоції між моушенами, зважена маска верхньої частини тіла, override-контролер на кожен варіант, умови та exit time, які аніматор має крутити без коду. Коли нічого з цього немає, кожна нода й кожен перехід — це церемонія навколо виклику, який ваш код усе одно збирався зробити."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Rule of thumb: if you would end up with a controller whose every transition is fired by PlayState from code, you wanted a Clip Player — and that is as true of a character as it is of a door. If two objects should share the same authored logic, you wanted a controller.",
                "Правило: якщо у вас виходить контролер, у якому кожен перехід усе одно смикається з коду через PlayState — вам потрібен Clip Player, і для персонажа це так само вірно, як для дверей. Якщо два об'єкти мають ділити ту саму авторську логіку — потрібен контролер."
            ));

            // --- 2. Setup ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("2. Setup", "2. Налаштування"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "1. Add Honami Animation ▸ Honami Clip Player. The required Animator is added for you — clear its Controller slot.",
                "1. Додайте Honami Animation ▸ Honami Clip Player. Потрібний Animator додасться автоматично — очистіть його слот Controller."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "2. Fill the Clips list. Leave Name empty to fall back to the AnimationClip's own name.",
                "2. Заповніть список Clips. Лишіть Name порожнім, щоб використалося ім'я самого AnimationClip."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "3. Mark one clip as the default with the circle button in its row and enable Play Automatically if the object should animate from the start. With no clip marked, the first entry is used.",
                "3. Позначте один кліп дефолтним кнопкою-кружечком у його рядку і ввімкніть Play Automatically, якщо об'єкт має анімуватися одразу. Якщо не позначено жодного, береться перший запис."));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Every row in the Clips list has a play button. In play mode it drives the real component, using the Preview Fade slider under the list. In edit mode it poses the scene object directly; pressing stop puts the hierarchy back exactly where it was.",
                "Кожен рядок списку Clips має кнопку відтворення. У режимі гри вона керує справжнім компонентом і використовує повзунок Preview Fade під списком. У режимі редагування вона позує об'єкт сцени напряму; зупинка повертає ієрархію рівно туди, де вона була."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get("Per-clip fields:", "Поля кожного кліпу:"));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                ("Name", HonamiDocLocalization.Get("The key used by Play, CrossFade and everything else. Must be unique — a duplicate name is dropped when the lookup table is built, and every call resolves to the first entry that claimed it.", "Ключ для Play, CrossFade та всього іншого. Має бути унікальним — дублікат відкидається при побудові таблиці пошуку, і всі виклики потрапляють у перший запис, що зайняв це ім'я.")),
                ("Clip", HonamiDocLocalization.Get("The AnimationClip. Entries with no clip are skipped when the graph is built.", "Сам AnimationClip. Записи без кліпа пропускаються при побудові графа.")),
                ("Speed", HonamiDocLocalization.Get("Playback multiplier. Negative values play the clip backwards from its end.", "Множник відтворення. Від'ємні значення програють кліп задом наперед, починаючи з кінця.")),
                ("Wrap Mode", HonamiDocLocalization.Get("What happens when the playhead reaches the end. See the next section.", "Що стається, коли плейхед доходить до кінця. Див. наступний розділ.")),
                ("Layer", HonamiDocLocalization.Get("Clips on a higher layer take weight away from lower ones instead of blending against them.", "Кліпи на вищому шарі забирають вагу в нижчих, а не змішуються з ними.")),
                ("Linked Action Id", HonamiDocLocalization.Get("Optional ActionID that makes this clip play on a Linked Animator or HonamiLinkedAction broadcast.", "Необов'язковий ActionID, який змушує цей кліп грати при розсилці через Linked Animator або HonamiLinkedAction."))
            );

            // --- 3. The inspector ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("3. The inspector", "3. Інспектор"), HonamiEditorIcons.Timeline);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The whole component fits into one inspector, and it is built so that you can audition the entire list without entering Play Mode.",
                "Увесь компонент вміщається в один інспектор, і зроблений так, щоб ви могли прослухати весь список, не заходячи в Play Mode."
            ));

            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Row layout", "Будова рядка"),
                 HonamiDocLocalization.Get("Every row is a foldout: the resolved clip name, the AnimationClip field, a Default toggle and a play button. Expanding it reveals Name, Speed, Wrap Mode, Layer and Linked Action Id.", "Кожен рядок — це фолдаут: обчислене ім'я кліпу, поле AnimationClip, перемикач Default і кнопка відтворення. Розгортання відкриває Name, Speed, Wrap Mode, Layer і Linked Action Id.")),
                (HonamiDocLocalization.Get("Reordering", "Перестановка"),
                 HonamiDocLocalization.Get("The list is a standard reorderable list. Order only matters for the fallback default clip — the first entry is used when no row is marked.", "Список — стандартний reorderable list. Порядок важливий лише для запасного дефолтного кліпу: якщо не позначено жодного рядка, береться перший запис.")),
                (HonamiDocLocalization.Get("Default toggle", "Перемикач Default"),
                 HonamiDocLocalization.Get("Exclusive: switching one on clears every other row. It stays disabled until the row has a clip assigned.", "Ексклюзивний: увімкнення одного скидає всі інші рядки. Залишається неактивним, доки в рядку немає кліпу.")),
                (HonamiDocLocalization.Get("Play button in edit mode", "Кнопка відтворення в режимі редагування"),
                 HonamiDocLocalization.Get("Plays the clip on the real scene object, honouring its authored speed and wrap mode. Once releases at the end on its own, ClampForever holds the last frame until you press stop, Loop and PingPong run until you do.", "Програє кліп прямо на об'єкті сцени, з урахуванням його швидкості та режиму wrap. Once сам відпускає в кінці, ClampForever тримає останній кадр до натискання стоп, Loop і PingPong крутяться, доки ви не зупините.")),
                (HonamiDocLocalization.Get("Play button in play mode", "Кнопка відтворення в Play Mode"),
                 HonamiDocLocalization.Get("Calls the live component's PlayClip with forceRestart, using the Preview Fade slider under the list as the fade length. It is the real playback path, not a preview.", "Викликає на живому компоненті PlayClip із forceRestart, беручи довжину фейду зі слайдера Preview Fade під списком. Це справжній шлях відтворення, а не прев'ю.")),
                (HonamiDocLocalization.Get("Runtime Debug", "Runtime Debug"),
                 HonamiDocLocalization.Get("In play mode the inspector grows a live panel: one bar per clip with its weight and normalized time, the name of the dominant clip, and Stop All / Pause buttons.", "У Play Mode інспектор дорощує живу панель: смужка на кожен кліп із його вагою та нормалізованим часом, ім'я домінантного кліпу і кнопки Stop All / Pause."))
            );

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Edit-mode preview snapshots every local transform under the object before it samples the first frame, and writes the snapshot back when you press stop or select something else. It never enables Unity's global AnimationMode, so it will not fight a Timeline or an Animation window that happens to be open.",
                "Прев'ю в режимі редагування знімає знімок кожного локального трансформа під об'єктом до того, як засемплити перший кадр, і повертає цей знімок, коли ви тиснете стоп або обираєте інший об'єкт. Воно ніколи не вмикає глобальний AnimationMode Unity, тож не конфліктує з відкритим Timeline чи вікном Animation."
            ));

            HonamiDocumentationBuilder.AddInfoBox(root, HonamiDocLocalization.Get(
                "Preview needs a hierarchy to pose, so it is disabled while a prefab asset is selected in the Project window. Open the prefab, or drop it into a scene, and the play buttons come back.",
                "Прев'ю потрібна ієрархія, яку можна поставити в позу, тому воно вимкнене, коли в Project вибрано ассет префаба. Відкрийте префаб або киньте його в сцену — і кнопки відтворення повернуться."
            ));

            // --- 4. Wrap modes ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("4. Wrap modes", "4. Режими Wrap"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Mode", "Режим"), 150),
                (HonamiDocLocalization.Get("At the end of the clip", "У кінці кліпу"), 0),
                ("", 0),
                ("Once", HonamiDocLocalization.Get("Fires OnClipFinished and drops to weight 0 — the clip releases the hierarchy.", "Викликає OnClipFinished і падає у вагу 0 — кліп відпускає ієрархію."), ""),
                ("ClampForever", HonamiDocLocalization.Get("Fires OnClipFinished and holds the last frame at full weight.", "Викликає OnClipFinished і тримає останній кадр із повною вагою."), ""),
                ("Loop", HonamiDocLocalization.Get("Wraps back to the start. Never finishes.", "Повертається на початок. Ніколи не завершується."), ""),
                ("PingPong", HonamiDocLocalization.Get("Reverses direction at both ends. Never finishes.", "Розвертає напрямок на обох кінцях. Ніколи не завершується."), "")
            );

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "The one gotcha worth remembering: with Once, the clip stops driving the object when it ends. If nothing else is playing and Restore When Idle is on (the default), the object snaps back to its captured initial pose. A door that must stay open after its opening animation wants ClampForever, not Once.",
                "Єдина пастка, яку варто запам'ятати: з Once кліп перестає керувати об'єктом, коли завершується. Якщо більше нічого не грає, а Restore When Idle увімкнено (за замовчуванням), об'єкт відскакує до захопленої початкової пози. Двері, що мають лишитися відчиненими після анімації відчинення, потребують ClampForever, а не Once."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "This mirrors how the Honami Animator treats a finished non-loop state on a layer with no default state — see Release Finished Without Default in its Initial Pose section.",
                "Це дзеркалить те, як Honami Animator обробляє завершений не-циклічний стан на шарі без дефолтного стану — див. Release Finished Without Default у його секції Initial Pose."
            ));

            // --- 5. Playing clips ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("5. Playing clips", "5. Відтворення кліпів"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"[SerializeField] private HonamiClipPlayer _player;

// Snap to a clip, cutting everything else on its layer.
_player.PlayClip(""Open"");

// Same thing, blended over 0.25s.
_player.CrossFade(""Idle"", 0.25f);

// Play after the current one-shot finishes.
_player.PlayQueued(""Idle"", HonamiQueueMode.CompleteOthers);

// Add a clip on top without cutting the others.
_player.Blend(""Sway"", 0.4f, 0.5f);

_player.Stop(""Sway"");   // one clip
_player.StopAll();       // everything

// Pose a single frame without starting playback.
_player.Sample(""Open"", 1f);");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "PlayClip does not rewind a clip that is already playing, exactly like the old Animation.Play. That makes it safe to call every frame from movement code. Pass forceRestart: true when you do want it to start over.",
                "PlayClip не перемотує кліп, який уже грає — рівно як старий Animation.Play. Завдяки цьому його безпечно викликати щокадру з коду руху. Передайте forceRestart: true, коли перезапуск таки потрібен."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Every one of these returns a bool: false means there is no clip with that name. PlayQueued with PlayNow skips the queue; with CompleteOthers it waits for the one-shots to end — Loop and PingPong clips never finish, so they do not hold the queue back.",
                "Кожен із цих методів повертає bool: false означає, що кліпу з таким іменем немає. PlayQueued із PlayNow ігнорує чергу; із CompleteOthers чекає завершення одноразових кліпів — Loop і PingPong не завершуються ніколи, тож чергу вони не тримають."
            ));

            // --- 6. Clip states ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("6. Clip handles", "6. Хендли кліпів"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The indexer returns a live handle to one clip, the way the legacy AnimationState did. It is null when the name is unknown.",
                "Індексер повертає живий хендл на один кліп — так само, як робив легасі AnimationState. Повертає null, якщо ім'я невідоме."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"var idle = _player[""Idle""];
idle.Speed = 1.5f;
idle.NormalizedTime = 0.5f;
idle.WrapMode = HonamiClipWrapMode.Loop;

if (_player[""Open""].IsPlaying) { }

foreach (var state in _player.States) { }");

            HonamiDocumentationBuilder.AddParameterTable(root,
                ("Speed", "float", HonamiDocLocalization.Get("Playback multiplier for this clip. Negative plays backwards.", "Множник відтворення цього кліпу. Від'ємний програє назад.")),
                ("CurrentTime", "float", HonamiDocLocalization.Get("Playhead in seconds. Setting it seeks without touching weight.", "Плейхед у секундах. Присвоєння перемотує, не чіпаючи вагу.")),
                ("NormalizedTime", "float", HonamiDocLocalization.Get("Playhead as 0..1 of the clip length.", "Плейхед як 0..1 від довжини кліпу.")),
                ("Weight", "float", HonamiDocLocalization.Get("Blend weight before layer normalization. Setting it cancels any fade in progress.", "Вага змішування до нормалізації по шарах. Присвоєння скасовує поточний фейд.")),
                ("WrapMode", "HonamiClipWrapMode", HonamiDocLocalization.Get("Overrides the authored wrap mode at runtime.", "Перевизначає авторський режим wrap у рантаймі.")),
                ("IsPlaying", "bool", HonamiDocLocalization.Get("True while this clip contributes to the pose.", "True, поки цей кліп впливає на позу.")),
                ("Length", "float", HonamiDocLocalization.Get("Clip length in seconds.", "Довжина кліпу в секундах."))
            );

            HonamiDocumentationBuilder.AddInfoBox(root, HonamiDocLocalization.Get(
                "Handles are recreated whenever the graph is rebuilt — on a disable/enable cycle, or after AddClip, RemoveClip and Rebuild. Do not cache them across those points.",
                "Хендли перестворюються щоразу, коли граф перебудовується — при циклі disable/enable, а також після AddClip, RemoveClip і Rebuild. Не кешуйте їх через ці точки."
            ));

            // --- 7. Reading playback state ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("7. Reading playback state", "7. Читання стану відтворення"), HonamiEditorIcons.Profile);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Besides the per-clip handles, the player answers a few questions about itself. None of it allocates, so it is safe to poll from gameplay code every frame.",
                "Окрім хендлів окремих кліпів, плеєр відповідає й на кілька питань про себе. Жодне з них не алокує, тож їх безпечно опитувати щокадру з ігрового коду."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"if (!_player.IsPlayingAny) _player.CrossFade(""Idle"", 0.3f);

string dominant = _player.CurrentClip;   // heaviest playing clip, null when idle
int total = _player.ClipCount;

if (_player.TryGetState(""Open"", out var open) && open.NormalizedTime > 0.9f) { }

_player.Rewind();          // every clip back to its start
_player.Rewind(""Open"");    // just this one

var first = _player[0];    // index overload, null when out of range");

            HonamiDocumentationBuilder.AddParameterTable(root,
                ("CurrentClip", "string", HonamiDocLocalization.Get("Name of the playing clip with the highest raw weight. Null when nothing is playing.", "Ім'я кліпу, що грає з найбільшою сирою вагою. Null, коли нічого не грає.")),
                ("ClipCount", "int", HonamiDocLocalization.Get("How many clips made it into the graph. Rows with no AnimationClip are skipped when it is built.", "Скільки кліпів потрапило в граф. Рядки без AnimationClip пропускаються під час побудови.")),
                ("IsPlayingAny", "bool", HonamiDocLocalization.Get("True while at least one clip contributes to the pose.", "True, поки хоча б один кліп впливає на позу.")),
                ("IsPlaying(name)", "bool", HonamiDocLocalization.Get("The same question about one specific clip.", "Те саме питання про один конкретний кліп.")),
                ("States", "IReadOnlyList<HonamiClipState>", HonamiDocLocalization.Get("Every handle in graph order. Iterate it instead of guessing names.", "Усі хендли в порядку графа. Ітеруйте його замість того, щоб вгадувати імена.")),
                ("TryGetState(name, out)", "bool", HonamiDocLocalization.Get("Handle lookup that tells you when the name is unknown, instead of quietly returning null.", "Пошук хендла, який повідомляє, що ім'я невідоме, замість тихого null.")),
                ("this[int]", "HonamiClipState", HonamiDocLocalization.Get("Handle by graph index. Null when the index is out of range.", "Хендл за індексом у графі. Null, якщо індекс поза межами."))
            );

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "CurrentClip picks the heaviest clip across all layers, so during a crossfade it flips to the incoming clip the moment it overtakes the outgoing one. It is the right thing for a debug readout and the wrong thing for a gameplay condition — IsPlaying(name) is the precise question.",
                "CurrentClip обирає найважчий кліп по всіх шарах, тож під час кросфейду він перемикається на вхідний кліп у мить, коли той обганяє вихідний. Для дебаг-виводу це те що треба, для ігрової умови — ні: точне питання ставить IsPlaying(name)."
            ));

            // --- 8. Sampling and scrubbing ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("8. Sampling and scrubbing", "8. Семплінг і скрабінг"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A Clip Player does not have to play anything. It also works as a poser: hand it a clip and a normalized time and it evaluates exactly one frame.",
                "Clip Player не зобов'язаний нічого програвати. Він також працює як позер: дайте йому кліп і нормалізований час — і він обчислить рівно один кадр."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Freeze the door fully open, without playing the opening animation.
_player.Sample(""Open"", 1f);

// Drive a clip from a value instead of from time.
_player.Sample(""LeverPull"", _handleInput01);");

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Sample clears every weight, pins the chosen clip at weight 1 with its playhead where you asked, evaluates the graph once and stops advancing. The pose then simply holds until you play something else — so calling Sample every frame with a changing value turns an AnimationClip into a curve you drive by hand: a dial, a slider-controlled machine, a lever that follows the player's grip, a blend-shape expression dialled by a mood value.",
                "Sample обнуляє всі ваги, фіксує обраний кліп на вазі 1 з плейхедом там, де ви попросили, один раз обчислює граф і припиняє просування. Далі поза просто тримається, доки ви не заграєте щось інше — тож виклик Sample щокадру зі змінним значенням перетворює AnimationClip на криву, яку ви ведете вручну: циферблат, механізм на повзунку, важіль, що йде за рукою гравця, вираз обличчя на блендшейпах, який крутить значення настрою."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Sample is the runtime twin of what the play button does in edit mode. It also clears the queue, so a clip waiting there cannot sneak in behind the pose you just set.",
                "Sample — рантайм-двійник того, що робить кнопка відтворення в режимі редагування. Він також очищає чергу, тож кліп, який там чекав, не прослизне за спину щойно виставленої пози."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Only want to move the playhead of a clip that is already running? The handle seeks without touching weight: _player[\"Idle\"].NormalizedTime = 0.5f, or CurrentTime in seconds. Sample is the heavier hammer that also takes the pose away from everything else.",
                "Хочете лише зсунути плейхед кліпу, який уже грає? Хендл перемотує, не чіпаючи вагу: _player[\"Idle\"].NormalizedTime = 0.5f або CurrentTime у секундах. Sample — важчий молоток, який ще й забирає позу в усіх інших."
            ));

            // --- 9. Layers ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("9. Layers and weights", "9. Шари та ваги"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "All clips feed one mixer. Within a layer, weights are normalized when they sum above 1. Layers are then resolved from the highest down, each one consuming from the weight the layers above it left over — so a clip at full weight on layer 1 completely covers layer 0.",
                "Усі кліпи живлять один мікшер. У межах шару ваги нормалізуються, якщо їхня сума перевищує 1. Далі шари розв'язуються згори вниз: кожен забирає з ваги, яку лишили шари над ним — тож кліп із повною вагою на шарі 1 повністю перекриває шар 0."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "PlayClip and CrossFade only fade out the clips on the target clip's own layer. Clips on other layers keep playing.",
                "PlayClip і CrossFade гасять лише ті кліпи, що на тому ж шарі, що й цільовий. Кліпи на інших шарах продовжують грати."
            ));
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "There are no avatar masks here — a layer overrides everything under it, not a selected set of bones. If you need an upper-body layer, that is a Honami Animator job.",
                "Масок аватара тут немає — шар перекриває все, що під ним, а не вибраний набір кісток. Якщо потрібен шар для верхньої частини тіла, це задача для Honami Animator."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            // --- 10. Events ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("10. Events", "10. Події"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"private void OnEnable()
{
    _player.OnClipStarted  += HandleStarted;
    _player.OnClipFinished += HandleFinished;
}

private void OnDisable()
{
    _player.OnClipStarted  -= HandleStarted;
    _player.OnClipFinished -= HandleFinished;
}

private void HandleFinished(string clipName)
{
    if (clipName == ""Open"") _door.MarkOpen();
}");
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "OnClipFinished fires for Once and ClampForever when the playhead reaches the end. A clip that was faded out and replaced before it got there stops silently — it never finished.",
                "OnClipFinished спрацьовує для Once і ClampForever, коли плейхед доходить до кінця. Кліп, який згасили й замінили раніше, зупиняється тихо — він не завершився."
            ));

            // --- 11. Runtime clip library ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("11. A clip library you can change at runtime", "11. Бібліотека кліпів, яку можна міняти в рантаймі"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The clip list is not frozen at build time. Entries can be added and removed while the game runs, which is what makes the Clip Player a good fit for objects that are assembled rather than authored: a machine whose animation set depends on the module bolted onto it, a creature that learns a new attack, a prop built from downloadable content.",
                "Список кліпів не заморожений на етапі білду. Записи можна додавати й прибирати просто під час гри — саме тому Clip Player добре пасує об'єктам, які збирають, а не авторять: механізм, чий набір анімацій залежить від причепленого модуля, істота, що вивчає нову атаку, пропс, зібраний із завантаженого контенту."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"_player.AddClip(_openClip, ""Open"", HonamiClipWrapMode.ClampForever);
_player.AddClip(_idleClip, ""Idle"", HonamiClipWrapMode.Loop, layer: 1);

_player.RemoveClip(""Open"");

// After changing the serialized list some other way.
_player.Rebuild();");

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "AddClip and RemoveClip rebuild the graph for you; Rebuild is the manual version. A rebuild destroys the PlayableGraph, drops the queue, cancels delayed ActionIDs and starts again from an empty pose.",
                "AddClip і RemoveClip перебудовують граф самі; Rebuild — ручний варіант. Перебудова знищує PlayableGraph, скидає чергу, скасовує відкладені ActionID і починає з порожньої пози."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Every handle you were holding dies together with the old graph, and nothing is playing once the rebuild is done — the player does not restore what was on screen a frame earlier. Rebuild behind a load screen, a cut, or while the object is idle, then re-fetch handles and call PlayClip yourself.",
                "Усі хендли, які ви тримали, помирають разом зі старим графом, і після перебудови не грає нічого — плеєр не відновлює те, що було на екрані кадром раніше. Перебудовуйте за екраном завантаження, під час склейки або поки об'єкт стоїть, а потім заново візьміть хендли й самі викличте PlayClip."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "AddClip with no name falls back to the AnimationClip's own name, exactly like an empty Name field in the inspector. Keep names unique: a duplicate is silently ignored when the lookup table is built, and every call resolves to the first entry that claimed the name.",
                "AddClip без імені бере ім'я самого AnimationClip — так само, як порожнє поле Name в інспекторі. Тримайте імена унікальними: дублікат тихо ігнорується при побудові таблиці пошуку, і всі виклики потрапляють у перший запис, що зайняв це ім'я."
            ));

            // --- 12. Linked ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("12. Linked Animator and ActionID", "12. Linked Animator та ActionID"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A Clip Player is a full citizen of the Linked system. A brain in Childs mode picks it up together with the animators, and Prevent Linking and Linking Tag work the same way.",
                "Clip Player — повноцінний учасник системи Linked. Мозок у режимі Childs підхоплює його разом з аніматорами, а Prevent Linking і Linking Tag працюють так само."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Broadcast", "Розсилка"), 240),
                (HonamiDocLocalization.Get("Reaches a Clip Player?", "Дістає Clip Player?"), 0),
                ("", 0),
                ("SetActionID(...)", HonamiDocLocalization.Get("Yes — plays the clip whose Linked Action Id matches.", "Так — грає кліп, чий Linked Action Id збігається."), ""),
                ("PlayState(name, ...)", HonamiDocLocalization.Get("Yes — treated as a clip name.", "Так — трактується як ім'я кліпу."), ""),
                ("StopAll / PauseAll / ResumeAll", HonamiDocLocalization.Get("Yes.", "Так."), ""),
                ("GlobalWeight", HonamiDocLocalization.Get("Yes.", "Так."), ""),
                ("SetFloat / SetBool / SetTrigger", HonamiDocLocalization.Get("No — a Clip Player has no parameters. Only controller-backed animators receive these.", "Ні — у Clip Player немає параметрів. Їх отримують лише аніматори з контролером."), ""),
                ("SetController / TrySkipState / SetLayerWeight", HonamiDocLocalization.Get("No — controller-only.", "Ні — лише для контролерів."), "")
            );
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The brain exposes both sets: LinkedAnimators is everything it drives, FullAnimators is just the controller-backed subset that parameter and state-machine calls apply to.",
                "Мозок віддає обидві множини: LinkedAnimators — усе, чим він керує, FullAnimators — лише підмножина з контролерами, до якої застосовні виклики параметрів і стейт-машини."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Delayed reactions work here too, and are dropped if the graph goes away.
_player.ReactToAction(_alarmAction, 0.2f, delay: 0.5f);
_player.CancelPendingActions();");

            // --- 13. Recipes ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("13. Recipes", "13. Рецепти"), HonamiEditorIcons.Graph);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Shapes that come up over and over. None of them needs a controller asset, and none needs more than a few lines of gameplay code.",
                "Форми, які трапляються знову й знову. Жодній із них не потрібен ассет-контролер, і жодній не треба більше за кілька рядків ігрового коду."
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("An NPC your AI already steers", "NPC, яким уже керує ваш ШІ"),
                 HonamiDocLocalization.Get("Idle, Walk and Talk as Loop clips on layer 0, Flinch and Wave as Once clips on layer 1. The behaviour tree calls CrossFade when it changes its mind and PlayClip for a reaction — the same two calls it would have made into a controller, minus the controller.", "Idle, Walk і Talk — кліпи Loop на шарі 0, Flinch і Wave — кліпи Once на шарі 1. Behaviour tree викликає CrossFade, коли передумав, і PlayClip на реакцію — ті самі два виклики, які він робив би в контролер, тільки без контролера.")),
                (HonamiDocLocalization.Get("A crowd of background characters", "Натовп фонових персонажів"),
                 HonamiDocLocalization.Get("One Loop clip each, a randomized Speed and NormalizedTime per instance so they fall out of sync, and an FPS Cap of 15 with interpolation. A Linked Brain broadcasts an ActionID when something startles them. No controller assets to author, load or keep in memory.", "По одному кліпу Loop на кожного, рандомізовані Speed і NormalizedTime на екземпляр, щоб вони розсинхронізувались, і FPS Cap 15 з інтерполяцією. Linked Brain розсилає ActionID, коли їх щось налякало. Жодних ассетів-контролерів, які треба верстати, вантажити й тримати в пам'яті.")),
                (HonamiDocLocalization.Get("A door that stays open", "Двері, що лишаються відчиненими"),
                 HonamiDocLocalization.Get("Open and Close as two ClampForever clips on layer 0. PlayClip switches between them and each one holds its last frame, so the door never snaps back to the initial pose. OnClipFinished is where the collider changes.", "Open і Close — два кліпи ClampForever на шарі 0. PlayClip перемикає між ними, кожен тримає свій останній кадр, тож двері ніколи не відскакують у початкову позу. Колайдер міняється в OnClipFinished.")),
                (HonamiDocLocalization.Get("An idle that reactions cover", "Idle, який перекривають реакції"),
                 HonamiDocLocalization.Get("Idle as Loop on layer 0, the reactions as Once on layer 1 — a jammed machine, a flinching creature, a villager waving back. The reaction covers the idle for as long as it plays and releases it at the end: no transitions to author, and no code to put the idle back.", "Idle як Loop на шарі 0, реакції як Once на шарі 1 — заклинений механізм, істота, що сахається, селянин, який махає у відповідь. Реакція перекриває idle, доки грає, і відпускає його в кінці: не треба ані авторити переходи, ані писати код повернення в idle.")),
                (HonamiDocLocalization.Get("A wind-blown banner", "Прапор на вітрі"),
                 HonamiDocLocalization.Get("One Loop clip, marked default, Play Automatically on. That is the entire setup: no controller asset and not one line of code.", "Один кліп Loop, позначений дефолтним, увімкнений Play Automatically. Це вся настройка: ані ассета-контролера, ані рядка коду.")),
                (HonamiDocLocalization.Get("A lever the player drags", "Важіль, який тягне гравець"),
                 HonamiDocLocalization.Get("One clip that is never played. Sample(\"Pull\", grip01) every frame while the player holds it, then CrossFade into a settle clip on release.", "Один кліп, який ніколи не грає. Sample(\"Pull\", grip01) щокадру, поки гравець тримає, а на відпусканні — CrossFade у кліп заспокоєння.")),
                (HonamiDocLocalization.Get("A scripted sequence", "Скриптована послідовність"),
                 HonamiDocLocalization.Get("PlayQueued chains one-shots back to back, and the Loop clip at the end never finishes, so the queue naturally stops there. OnClipStarted drives audio and VFX per step.", "PlayQueued зчіплює одноразові кліпи один за одним, а кліп Loop у кінці ніколи не завершується, тож черга природно там і зупиняється. OnClipStarted веде звук і VFX на кожному кроці."))
            );
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"_player.PlayClip(""Approach"");
_player.PlayQueued(""Unlock"");
_player.PlayQueued(""Open"", HonamiQueueMode.CompleteOthers, 0.15f);
_player.PlayQueued(""OpenIdle"");   // Loop - the queue ends here");

            // --- 14. Limits ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("14. What was cut, and what survived", "14. Що вирізали, а що лишилось"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "This list is the point of the component, not the price of it. Every item below is graph machinery that was taken out deliberately, and taking it out is what turns a controller asset you have to open, lay out and maintain into a list of clips on the object.",
                "Цей список — сенс компонента, а не плата за нього. Кожен пункт нижче — це машинерія графа, яку прибрали навмисно, і саме це прибирання перетворює ассет-контролер, який треба відкривати, розкладати й підтримувати, на список кліпів на об'єкті."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("No parameters, conditions or automatic transitions — your code decides what plays.", "Немає параметрів, умов і автоматичних переходів — що грати, вирішує ваш код."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("No blend trees, sub-nodes, portals, Any State or state tags.", "Немає blend tree, підвузлів, порталів, Any State і тегів станів."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("No avatar, avatar masks, mirroring or bone replacement.", "Немає аватара, масок аватара, віддзеркалення та заміни кісток."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("No event markers on the timeline — use OnClipStarted and OnClipFinished.", "Немає маркерів подій на таймлайні — користуйтесь OnClipStarted і OnClipFinished."));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "What survived the cut is everything that still had to be there for animation to look right: crossfading weights, layers, wrap modes, a playback queue, per-clip speed and direction, a playhead you can seek, and start/finish events. Nothing that affects the animation itself was traded away — only the authoring layer above it.",
                "Те, що пережило різанину — це все, без чого анімація перестала б виглядати правильно: ваги з кросфейдом, шари, режими wrap, черга відтворення, швидкість і напрямок на кожен кліп, плейхед, який можна перемотати, та події старту й завершення. Нічого з того, що впливає на саму анімацію, не віддали — лише шар авторингу над нею."
            ));

            // --- 15. Shared ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("15. What it shares with the Animator", "15. Що спільного з Animator"), HonamiEditorIcons.Profile);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The cut stopped at the authoring layer. Everything below it — the parts that cost performance, drive rigs, or bite you at runtime — is the same code the Honami Animator runs, so choosing a clip list never costs you a runtime feature.",
                "Різанина зупинилась на шарі авторингу. Усе, що нижче — те, що коштує продуктивності, керує рігами чи кусає в рантаймі — це той самий код, який виконує Honami Animator, тож вибір списку кліпів ніколи не коштує вам рантайм-можливості."
            ));

            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("Initial Pose", "Initial Pose"),
                 HonamiDocLocalization.Get("Capture On Awake, Restore When Idle, Include Root Transform, plus CaptureInitialPose and RestoreInitialPose at runtime.", "Capture On Awake, Restore When Idle, Include Root Transform, а також CaptureInitialPose і RestoreInitialPose у рантаймі."),
                 HonamiEditorIcons.Controller),
                (HonamiDocLocalization.Get("FPS Cap", "FPS Cap"),
                 HonamiDocLocalization.Get("Evaluate at a fixed rate with optional interpolation between ticks — the same cost saver used on background characters.", "Обчислення з фіксованою частотою та опційною інтерполяцією між тіками — та сама економія, що й на фонових персонажах."),
                 HonamiEditorIcons.TimelineWhite),
                (HonamiDocLocalization.Get("Rig System", "Система рігу"),
                 HonamiDocLocalization.Get("Add a Honami Rigging Processor and rigs are spliced into this graph exactly as they are into an Animator's.", "Додайте Honami Rigging Processor — і риги вбудуються в цей граф рівно так само, як в граф Animator."),
                 HonamiEditorIcons.GraphWhite),
                (HonamiDocLocalization.Get("Update Modes", "Режими оновлення"),
                 HonamiDocLocalization.Get("Normal, Unscaled, Late, Animate Physics, or Manual with your own Tick(deltaTime).", "Normal, Unscaled, Late, Animate Physics або Manual із власним Tick(deltaTime)."),
                 HonamiEditorIcons.Timeline),
                (HonamiDocLocalization.Get("Global Weight", "Global Weight"),
                 HonamiDocLocalization.Get("Init or Bind mode, to fade the whole component's influence in and out.", "Режим Init або Bind, щоб плавно вводити й виводити вплив усього компонента."),
                 HonamiEditorIcons.BlendTree),
                (HonamiDocLocalization.Get("Time Control", "Керування часом"),
                 HonamiDocLocalization.Get("Time Scale, Pause and Resume behave identically on both components.", "Time Scale, Pause і Resume поводяться однаково на обох компонентах."),
                 HonamiEditorIcons.Profile)
            );

            HonamiDocumentationBuilder.AddSeparator(root);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Both components derive from HonamiAnimatorBase, which is also the type the Linked system and the rig system look for. If you write tooling that should accept either one, type it against the base.",
                "Обидва компоненти успадковані від HonamiAnimatorBase — саме цей тип шукають система Linked і система рігів. Якщо пишете тулінг, який має приймати будь-який із них, типізуйте його по базі."
            ));
        }
    }
}
