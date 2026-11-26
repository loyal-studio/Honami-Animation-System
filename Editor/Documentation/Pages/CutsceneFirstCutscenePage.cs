using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneFirstCutscenePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Your First Cutscene", "Перша катсцена");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "tutorial first cutscene create timeline director shot camera audio fade play trigger туторіал перша катсцена створити таймлайн директор шот камера звук фейд";
        public int Order => 561;
        public int EstimatedReadTime => 12;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("Create a timeline and the Director that plays it", "Створити таймлайн і Director, що його грає"),
                HonamiDocLocalization.Get("Frame camera shots straight from the Scene view", "Кадрувати камерні шоти прямо зі Scene view"),
                HonamiDocLocalization.Get("Add a character's animation, music and a fade", "Додати анімацію персонажа, музику та фейд"),
                HonamiDocLocalization.Get("Start the cutscene from gameplay and give control back when it ends", "Запустити катсцену з геймплею й повернути керування наприкінці")
            }, new[]
            {
                HonamiDocLocalization.Get("A scene with a camera tagged MainCamera and a character", "Сцена з камерою з тегом MainCamera і персонажем"),
                HonamiDocLocalization.Get("An animation clip for that character and an audio clip", "Анімаційний кліп для цього персонажа й аудіокліп")
            });

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("1. The asset and the Director", "1. Ассет і Director"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "In the Project window choose Create → Honami Animation → Timeline and name it, for example, Intro. Double-click it: the Honami Timeline window opens it in a tab. The toolbar shows a red + Director button, because nothing in the scene plays this timeline yet. Press it. A GameObject called Intro Director appears in the open scene with a Honami Director on it, Play Trigger set to Manual. From now on every scene object you connect to the timeline is remembered on that Director.",
                "У вікні Project оберіть Create → Honami Animation → Timeline і назвіть його, наприклад, Intro. Подвійний клік відкриє його у вкладці вікна Honami Timeline. На тулбарі видно червону кнопку + Director, бо в сцені ще ніщо не грає цей таймлайн. Натисніть її. У відкритій сцені з'явиться GameObject Intro Director з компонентом Honami Director і Play Trigger, встановленим у Manual. Відтепер кожен об'єкт сцени, який ви під'єднуєте до таймлайну, запам'ятовується на цьому Director."
            ));
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Check the Frame Rate in the toolbar before you start: 30 is the default, 24 gives a film feel, 60 matches fast gameplay. It sets the edit grid, frame snapping and timecode, and can be changed later.",
                "Перед початком перевірте Frame Rate на тулбарі: типово 30, 24 дає кіношне відчуття, 60 пасує швидкому геймплею. Він задає сітку, прилипання до кадрів і таймкод, і його можна змінити пізніше."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("2. Camera shots", "2. Камерні шоти"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Click Add Module under the tracks and choose Camera Shots. The track works with Camera.main until you bind another camera, so there is nothing to set up. Now fly the Scene view camera to your first framing, right-click the Camera Shots track at 0 and choose New Shot From Scene View. A Shot anchor object is created under the Director at exactly that view, with a disabled Camera component that keeps the field of view and lets you see the framing when you select it. Fly to the next framing, right-click at 3 seconds and repeat.",
                "Натисніть Add Module під треками й оберіть Camera Shots. Трек працює з Camera.main, доки ви не прив'яжете іншу камеру, тож налаштовувати нічого не треба. Тепер підведіть камеру Scene view до першого кадру, клацніть правою кнопкою по треку Camera Shots на позначці 0 і оберіть New Shot From Scene View. Під Director з'явиться якір шота рівно в цьому ракурсі, з вимкненим компонентом Camera, що зберігає поле зору та показує кадр, коли ви виділяєте якір. Перейдіть до наступного ракурсу, клацніть правою кнопкою на 3 секундах і повторіть."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Turn on Preview in the transport bar and drag the playhead along the ruler: the Game view cuts from shot to shot. Drag the second shot a little to the left so it overlaps the first and the cut becomes a smooth camera move across the overlap. Drag the small round handle in the top-left corner of the first shot to give it a Blend In, so the camera glides in from where the player's camera was.",
                "Увімкніть Preview на транспортній панелі й тягніть плейхед по лінійці: Game view перемикається з шота на шот. Потягніть другий шот трохи вліво, щоб він перекрив перший, і склейка стане плавним рухом камери впродовж перекриття. Потягніть маленьку круглу ручку в лівому верхньому куті першого шота, щоб дати йому Blend In, і камера плавно прилетить з того місця, де була камера гравця."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("3. The character", "3. Персонаж"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Drag your character from the Hierarchy onto the empty area under the tracks and choose New Animation Track For it. The track is created and already bound. Now drag the character's animation clip from the Project onto that track at the moment it should start. Scrub: the character moves in the Scene view. Give the clip a Blend In of a few frames with its corner handle, so at runtime it eases out of whatever the character was doing, and a Blend Out so it eases back.",
                "Перетягніть персонажа з Hierarchy на порожнє місце під треками й оберіть New Animation Track For. Трек створиться вже прив'язаним. Тепер перетягніть анімаційний кліп персонажа з Project на цей трек у момент, коли він має початися. Поскрабте: персонаж рухається в Scene view. Дайте кліпу Blend In у кілька кадрів ручкою в куті, щоб у runtime він плавно вийшов із того, що персонаж робив, і Blend Out, щоб так само плавно повернувся."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("4. Music and a fade", "4. Музика та фейд"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Drag an audio clip from the Project onto the empty area: an Audio track is created with the clip on it and its waveform drawn. Drag its corner handles for a fade in and a fade out. Press Space with Preview on and you hear it. Then add a Screen Fade module, right-click it near the end and choose Add Fade. A new fade goes from clear to black (From Opacity 0, To Opacity 1). The screen now fades to black, and stays black until the next fade or until the cutscene ends.",
                "Перетягніть аудіокліп з Project на порожнє місце: створиться трек Audio з цим кліпом і намальованою хвилею. Потягніть ручки в кутах для fade in і fade out. Натисніть Space з увімкненим Preview, і ви його почуєте. Потім додайте модуль Screen Fade, клацніть по ньому правою кнопкою ближче до кінця й оберіть Add Fade. Новий фейд іде від прозорого до чорного (From Opacity 0, To Opacity 1). Тепер екран затемнюється і лишається чорним до наступного фейда або до кінця катсцени."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("5. Playing it from the game", "5. Запуск із гри"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A cutscene usually starts from gameplay: a trigger, a door, the end of a dialogue. Keep a reference to the Director and call Play.",
                "Катсцена зазвичай стартує з геймплею: тригер, двері, кінець діалогу. Тримайте посилання на Director і викликайте Play."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Timeline;
using UnityEngine;

public sealed class IntroTrigger : MonoBehaviour
{
    [SerializeField] private HonamiDirector intro;
    private bool _played;

    private void OnTriggerEnter(Collider other)
    {
        if (_played) return;
        _played = true;
        intro.Play();
    }
}");
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The player must not walk around during the cutscene. Add a Director Behaviour Toggle to the player, leave its Director empty and drag the player's input script into Disable. It switches the script off whenever any Director is running and back on when it is over, without a line of code. The Director Reactions page lists the other reactions.",
                "Гравець не має ходити під час катсцени. Додайте гравцю Director Behaviour Toggle, лишіть його Director порожнім і перетягніть скрипт вводу гравця в Disable. Він вимикає скрипт, щойно працює будь-який Director, і вмикає назад, коли все скінчилось, без жодного рядка коду. Інші реакції описані на сторінці Director Reactions."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Press Play in the editor and walk into the trigger. When the timeline ends the Director hands everything back: the camera returns to where it was, the music stops, the fade clears, and the player can move again.",
                "Натисніть Play у редакторі й зайдіть у тригер. Коли таймлайн закінчиться, Director поверне все назад: камера повернеться на своє місце, музика стихне, фейд зникне, і гравець знову зможе рухатися."
            ));
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "If your gameplay camera is driven by a script (a follow or orbit camera), put that script in the same Disable list. Otherwise the script and the Camera Shots track fight over the camera every frame.",
                "Якщо ігрову камеру веде скрипт (follow чи orbit камера), додайте цей скрипт у той самий список Disable. Інакше скрипт і трек Camera Shots щокадру боротимуться за камеру."
            ), HonamiDocumentationBuilder.CalloutType.Warning);
        }
    }
}
