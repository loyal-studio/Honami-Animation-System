using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneRecipesPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Cutscene Recipes", "Рецепти катсцен");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "recipes skip skippable input control dialogue subtitles pause menu spawn actors reuse troubleshooting camera fights character snaps no sound warning рецепти пропуск керування діалог пауза спавн проблеми камера смикається звук";
        public int Order => 568;
        public int EstimatedReadTime => 10;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("Taking control from the player and giving it back cleanly", "Як чисто забрати керування у гравця й повернути його"),
                HonamiDocLocalization.Get("Skippable cutscenes, pause menus and dialogue", "Катсцени з пропуском, меню паузи та діалоги"),
                HonamiDocLocalization.Get("Casting actors spawned at runtime", "Як призначати акторів, заспавнених у runtime"),
                HonamiDocLocalization.Get("What to check when something looks wrong", "Що перевірити, коли щось не так")
            });

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Taking and returning control", "Забрати й повернути керування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Almost every cutscene needs the same bracket around it: switch off player input and the gameplay camera script when it plays, switch them back on when it is over. Add a Director Behaviour Toggle to the player with those scripts in Disable and nothing to write. Leave its Director empty and it covers every cutscene in the game, including ones started from a timeline event, a save load or the inspector.",
                "Майже кожній катсцені потрібні ті самі дужки: вимкнути ввід гравця і скрипт ігрової камери, коли вона грає, і ввімкнути їх, коли все закінчилось. Додайте гравцю Director Behaviour Toggle з цими скриптами в Disable, і писати нічого не треба. Лишіть його Director порожнім, і він покриє всі катсцени гри, зокрема запущені подією таймлайну, завантаженням сейву чи з інспектора."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "When your player has its own way of being switched off, derive from HonamiDirectorReaction instead and call it from OnDirectorStarted and OnDirectorStopped. The Director Reactions page shows how.",
                "Коли у вашого гравця є власний спосіб вимикання, успадкуйтеся від HonamiDirectorReaction і викликайте його з OnDirectorStarted та OnDirectorStopped. Як саме, показано на сторінці Director Reactions."
            ));
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "With Wrap Mode Hold, the Director stays active over the held last frame until you call Stop, so reactions keep the player locked. Use Finished to show a prompt over that frame, and call Stop when the player presses on.",
                "З Wrap Mode Hold Director лишається активним на утримуваному останньому кадрі, доки ви не викличете Stop, тож реакції тримають гравця заблокованим. Використовуйте Finished, щоб показати підказку над цим кадром, і викликайте Stop, коли гравець натисне далі."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Skippable cutscenes", "Катсцени з пропуском"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Call Skip on the skip button. It jumps to the end and fires every event on the way, so a door the cutscene opens is still open and a quest step it grants is still granted. Put anything that must not happen twice or must not happen on skip (a one-shot sound through an event, for example) on a track instead, since tracks do not fire on a jump. To skip only to the next beat instead of the end, place markers on the beats and SeekToMarker the next one: Seek fires no events, so it suits beats that only change what is on screen.",
                "Викликайте Skip на кнопці пропуску. Він перестрибує в кінець і запускає всі події по дорозі, тож двері, які відчиняє катсцена, все одно відчинені, а крок квесту все одно виданий. Усе, що не має статися двічі чи не має статися при пропуску (наприклад, одноразовий звук через подію), кладіть на трек, бо треки на стрибку не спрацьовують. Щоб пропускати лише до наступного біту, а не до кінця, розставте маркери на бітах і робіть SeekToMarker до наступного: Seek не запускає подій, тож підходить бітам, що змінюють лише картинку."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Pause menus", "Меню паузи"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "If your pause menu sets Time.timeScale to 0, a Director on Game Time freezes with the game and its sound pauses with it. If the menu does not touch time scale, call Pause on the Director when the menu opens and Resume when it closes. Use Unscaled Game Time for cutscenes that must keep playing while gameplay is frozen, such as a tutorial overlay.",
                "Якщо ваше меню паузи ставить Time.timeScale у 0, Director на Game Time завмирає разом із грою, і його звук теж стає на паузу. Якщо меню не чіпає time scale, викликайте Pause на Director, коли меню відкривається, і Resume, коли закривається. Беріть Unscaled Game Time для катсцен, що мають грати, поки геймплей заморожено, як-от оверлей туторіалу."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Dialogue", "Діалоги"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Voice lines belong on Audio tracks, nested inside the shots they are spoken in, so re-timing a shot carries its line along. Subtitles and dialogue boxes belong to your dialogue system: fire a Local Event per line at the moment the line starts and let the receiver show the text. For a line that waits for the player, end the shot with a Hold (a Director with Wrap Mode Hold, or Pause from the event) and Resume when the player confirms.",
                "Репліки кладіть на треки Audio, вкладені в шоти, у яких їх кажуть, тож зміна таймингу шота забирає репліку з собою. Субтитри й діалогові вікна належать вашій діалоговій системі: запускайте Local Event на кожну репліку в момент її початку, і нехай ресівер показує текст. Для репліки, що чекає гравця, закінчуйте шот утриманням (Director з Wrap Mode Hold або Pause з події) і викликайте Resume, коли гравець підтвердить."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Actors spawned at runtime", "Актори, заспавнені в runtime"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "When the hero or the boss only exists after loading, bind them from code before Play. Find the track in the timeline by its name or type and call SetBinding; for per-item references, SetReference with the item's key.",
                "Коли герой чи бос існує лише після завантаження, прив'язуйте їх з коду перед Play. Знайдіть трек у таймлайні за назвою чи типом і викличте SetBinding; для посилань окремих item-ів SetReference з ключем item-а."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"public void PlayBossIntro(GameObject boss, Camera camera)
{
    foreach (var module in intro.Timeline.Modules)
    {
        if (module is TimelineCameraModule)
            intro.SetBinding(module, camera);
        else if (module is TimelineAnimationModule && module.moduleName == ""Boss"")
            intro.SetBinding(module, boss);
    }
    intro.Play();
}");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Troubleshooting", "Проблеми"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Symptom", "Симптом"), 200),
                (HonamiDocLocalization.Get("Cause and fix", "Причина й рішення"), 0),
                ("", 0),
                (HonamiDocLocalization.Get("The camera jitters or ignores shots at runtime", "Камера тремтить або ігнорує шоти в runtime"), HonamiDocLocalization.Get("A camera script is still moving it. Put the script in a Director Behaviour Toggle.", "Її досі рухає скрипт камери. Додайте цей скрипт у Director Behaviour Toggle."), ""),
                (HonamiDocLocalization.Get("A track has a warning icon", "На треку значок попередження"), HonamiDocLocalization.Get("It needs a binding it does not have. Select the track and set its target in Binding, or drop the object on it.", "Йому потрібен біндинг, якого немає. Виділіть трек і задайте ціль у Binding або киньте на нього об'єкт."), ""),
                (HonamiDocLocalization.Get("A shot reads (Missing Anchor)", "Шот показує (Missing Anchor)"), HonamiDocLocalization.Get("The anchor object was deleted, or you are looking at a Director in another scene. Create a new anchor from the Scene view or select the right Director.", "Об'єкт якоря видалено, або ви дивитеся на Director в іншій сцені. Створіть новий якір зі Scene view або виділіть потрібний Director."), ""),
                (HonamiDocLocalization.Get("A shot reads Shot (no Director)", "Шот показує Shot (no Director)"), HonamiDocLocalization.Get("No Director in the open scenes plays this timeline. Press + Director.", "У відкритих сценах жоден Director не грає цей таймлайн. Натисніть + Director."), ""),
                (HonamiDocLocalization.Get("The character does not move", "Персонаж не рухається"), HonamiDocLocalization.Get("The clip was made for a different hierarchy, or the track is bound to a child instead of the root the clip expects.", "Кліп зроблено для іншої ієрархії, або трек прив'язано до дочірнього об'єкта замість кореня, якого чекає кліп."), ""),
                (HonamiDocLocalization.Get("The character pops at the start or end", "Персонаж смикається на початку чи в кінці"), HonamiDocLocalization.Get("Give the clip a Blend In and a Blend Out of a few frames.", "Дайте кліпу Blend In і Blend Out у кілька кадрів."), ""),
                (HonamiDocLocalization.Get("No sound in the editor", "Немає звуку в редакторі"), HonamiDocLocalization.Get("Preview is off, the Game view has Mute Audio on, or the shuttle speed is not 1×; sound plays only at normal speed.", "Preview вимкнено, у Game view увімкнено Mute Audio, або швидкість шатла не 1×; звук грає лише на нормальній швидкості."), ""),
                (HonamiDocLocalization.Get("An event fired twice", "Подія спрацювала двічі"), HonamiDocLocalization.Get("Playback crossed it twice: a Loop wrap mode, or playing backwards over it. Events fire in both directions.", "Відтворення пройшло її двічі: Wrap Mode Loop або відтворення назад через неї. Події спрацьовують в обох напрямках."), ""),
                (HonamiDocLocalization.Get("The screen stays black", "Екран лишається чорним"), HonamiDocLocalization.Get("A fade to black holds until the next fade. Add a fade back in, or let the cutscene stop.", "Затемнення тримається до наступного фейда. Додайте фейд назад або дайте катсцені зупинитися."), "")
            );
        }
    }
}
