using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneCameraShotsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Camera Shots", "Камерні шоти");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "camera shot anchor field of view fov cut blend crossfade curve easing gameplay camera scene view framing cinematography камера шот якір поле зору склейка бленд крива плавність кадрування";
        public int Order => 564;
        public int EstimatedReadTime => 7;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("How a shot places the camera and where its field of view comes from", "Як шот ставить камеру і звідки береться поле зору"),
                HonamiDocLocalization.Get("Framing shots from the Scene view", "Кадрування шотів зі Scene view"),
                HonamiDocLocalization.Get("Cuts, camera moves and handing the camera back to gameplay", "Склейки, рухи камери й повернення камери геймплею"),
                HonamiDocLocalization.Get("Moving shots and shots that follow an actor", "Рухомі шоти та шоти, що йдуть за актором")
            });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Camera Shots track moves one real camera, the one players see, from anchor to anchor. It never renders through extra cameras, so post-processing, render features and UI set up on your gameplay camera keep working in cutscenes. Bind the track to a Camera, or leave it unbound to use Camera.main.",
                "Трек Camera Shots переносить одну справжню камеру, ту, яку бачать гравці, від якоря до якоря. Він ніколи не рендерить через додаткові камери, тож пост-обробка, render feature-и та UI, налаштовані на ігровій камері, працюють і в катсценах. Прив'яжіть трек до Camera або лиште без біндингу, щоб використовувати Camera.main."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Anchors", "Якорі"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Each shot points at an anchor: any Transform in the scene. During the shot the camera takes the anchor's position and rotation. The anchor is a scene reference stored on the Director, so the timeline asset stays free of scene objects. The field of view comes from, in order: Override Field Of View on the shot, a Camera component on the anchor (it can be disabled, it only carries settings), and otherwise the camera's own field of view.",
                "Кожен шот вказує на якір: будь-який Transform у сцені. Під час шота камера бере позицію й поворот якоря. Якір це посилання на сцену, збережене на Director, тож ассет таймлайну лишається вільним від об'єктів сцени. Поле зору береться по черзі: Override Field Of View на шоті, компонент Camera на якорі (може бути вимкненим, він лише несе налаштування), інакше власне поле зору камери."
            ));
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Give anchors a disabled Camera component. Selecting the anchor then shows Unity's camera preview in the Scene view corner, which is the fastest way to judge a framing, and the component remembers the lens.",
                "Давайте якорям вимкнений компонент Camera. Тоді виділення якоря показує превʼю камери Unity в куті Scene view, що є найшвидшим способом оцінити кадр, а компонент пам'ятає об'єктив."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Framing from the Scene view", "Кадрування зі Scene view"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Where", "Де"), 190),
                (HonamiDocLocalization.Get("Command", "Команда"), 0),
                ("", 0),
                (HonamiDocLocalization.Get("Right-click the track", "Правий клік по треку"), HonamiDocLocalization.Get("New Shot From Scene View: a shot at that time with a new anchor (a disabled Camera under the Director) at the current Scene view position, rotation and field of view", "New Shot From Scene View: шот у цей момент із новим якорем (вимкнена Camera під Director) у поточній позиції, повороті й полі зору Scene view"), ""),
                (HonamiDocLocalization.Get("Right-click a shot", "Правий клік по шоту"), HonamiDocLocalization.Get("Align Shot To Scene View moves the anchor to the current view; Move Scene View To Shot does the opposite, so you can adjust from there", "Align Shot To Scene View переносить якір у поточний вигляд; Move Scene View To Shot робить навпаки, щоб підправити звідти"), ""),
                (HonamiDocLocalization.Get("Shot properties", "Властивості шота"), HonamiDocLocalization.Get("The same buttons, plus Create Anchor From Scene View for a shot without one", "Ті самі кнопки, а також Create Anchor From Scene View для шота без якоря"), ""),
                (HonamiDocLocalization.Get("Double-click a shot", "Подвійний клік по шоту"), HonamiDocLocalization.Get("Selects its anchor and brings the Scene view to it", "Виділяє його якір і переносить до нього Scene view"), ""),
                (HonamiDocLocalization.Get("Drag an object onto the track", "Кинути об'єкт на трек"), HonamiDocLocalization.Get("A shot anchored to that object", "Шот із якорем на цьому об'єкті"), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Cuts and camera moves", "Склейки та рухи камери"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Two shots placed edge to edge are a hard cut. Slide the later one over the earlier one and the camera travels from the first anchor to the second across the overlap, easing in and out: a quick overlap reads as a whip, a long one as a slow dolly. Position and rotation are blended, and so is the field of view, which gives you a zoom for free. Blend In Curve and Blend Out Curve on each shot pick the shape of that move: Smooth Step by default, Linear for a constant speed, Back or Elastic for a landing with a little overshoot, or a custom curve for full control. The ramp on the shot previews the chosen curve.",
                "Два шоти впритул це жорстка склейка. Насуньте пізніший на ранній, і камера пройде від першого якоря до другого впродовж перекриття, плавно розганяючись і гальмуючи: коротке перекриття читається як ривок, довге як повільний наїзд. Змішуються позиція і поворот, а також поле зору, що безкоштовно дає зум. Blend In Curve і Blend Out Curve на кожному шоті задають форму цього руху: Smooth Step за замовчуванням, Linear для сталої швидкості, Back чи Elastic для приземлення з легким overshoot, або власна крива для повного контролю. Рампа на шоті показує обрану криву."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Blend In on the first shot glides the camera in from wherever it was when the cutscene started, and Blend Out on the last shot glides it back there. Without them the cutscene cuts in and out. Between two shots that do not touch, the camera returns to its gameplay pose, which is usually what you want only at the very end.",
                "Blend In на першому шоті плавно заводить камеру з того місця, де вона була на старті катсцени, а Blend Out на останньому плавно повертає її туди. Без них катсцена вмикається й вимикається склейкою. Між двома шотами, що не торкаються, камера повертається в ігрову позу, чого зазвичай хочеться лише в самому кінці."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Handing the camera back", "Повернення камери"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "When the Director starts, the track remembers the camera's pose and field of view. When it stops (reaching the end or Skip with Wrap Mode None or Loop, Stop, or the Director being disabled) the camera goes back to exactly that pose. With Wrap Mode Hold the camera stays on the last shot until you call Stop, which is how you hold a final frame under a menu or a title card.",
                "Коли Director стартує, трек запам'ятовує позу й поле зору камери. Коли він зупиняється (кінець чи Skip із Wrap Mode None або Loop, Stop або вимкнення Director), камера повертається рівно в цю позу. З Wrap Mode Hold камера лишається на останньому шоті, доки ви не викличете Stop, і так тримають фінальний кадр під меню чи титром."
            ));
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "The Director applies shots in LateUpdate, after gameplay scripts. A camera script that also moves the camera in LateUpdate can still overwrite the shot depending on script order, and a follow camera that keeps running will snap the view back after a Blend Out. Disable camera control scripts when the cutscene is played and enable them when it finishes.",
                "Director застосовує шоти в LateUpdate, після ігрових скриптів. Скрипт камери, що теж рухає камеру в LateUpdate, залежно від порядку скриптів може перезаписати шот, а follow-камера, яка продовжує працювати, смикне вигляд назад після Blend Out. Вимикайте скрипти керування камерою, коли катсцена стартує, і вмикайте, коли вона закінчилась."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Moving shots", "Рухомі шоти"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "An anchor is an ordinary Transform, so anything that moves it moves the shot. Parent an anchor to a character for a shot that travels with them. Animate an anchor with an Animation track bound to it for a crane or a tracking shot; put that Animation track above the Camera Shots track so the anchor has moved before the camera reads it. A script can aim an anchor at a target for a shot that keeps someone in frame.",
                "Якір це звичайний Transform, тож усе, що його рухає, рухає і шот. Зробіть якір дочірнім до персонажа, щоб шот їхав разом із ним. Анімуйте якір треком Animation, прив'язаним до нього, для крана чи проїзду; ставте цей трек Animation вище за Camera Shots, щоб якір уже зрушив, коли камера його прочитає. Скрипт може націлювати якір на ціль для шота, що тримає когось у кадрі."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Shot properties", "Властивості шота"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                ("Anchor", HonamiDocLocalization.Get("The Transform the camera sits on. Stored on the Director of the scene you edit.", "Transform, на якому стоїть камера. Зберігається на Director сцени, яку ви редагуєте.")),
                ("Length", HonamiDocLocalization.Get("Duration of the shot in seconds; trim the block to change it.", "Тривалість шота в секундах; змінюється обрізкою блока.")),
                ("Blend In / Blend Out", HonamiDocLocalization.Get("Seconds to ease from and back to the gameplay camera. Overlaps with other shots blend on their own.", "Секунди плавного переходу від ігрової камери і назад до неї. Перекриття з іншими шотами блендяться самі.")),
                ("Blend In Curve / Blend Out Curve", HonamiDocLocalization.Get("Shape of each move: an easing preset or a custom curve, previewed by the ramp on the shot.", "Форма кожного руху: пресет плавності або власна крива, яку показує рампа на шоті.")),
                ("Override Field Of View", HonamiDocLocalization.Get("Use Field Of View instead of the anchor's Camera or the camera's own value.", "Брати Field Of View замість Camera якоря чи власного значення камери."))
            );
        }
    }
}
