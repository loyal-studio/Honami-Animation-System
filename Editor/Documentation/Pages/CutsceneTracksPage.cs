using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class CutsceneTracksPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Animation, Audio & Fades", "Анімація, аудіо та фейди");
        public string Category => HonamiDocLocalization.Get("07. Cutscenes", "07. Катсцени");
        public string SearchKeywords => "animation track audio track screen fade activation events log parameter curve nesting waveform loop blend mixer трек анімація аудіо фейд активація події крива вкладення хвиля";
        public int Order => 565;
        public int EstimatedReadTime => 9;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root, new[]
            {
                HonamiDocLocalization.Get("Animating characters and props, including characters driven by a Honami Animator", "Анімація персонажів і пропсів, зокрема тих, що керуються Honami Animator"),
                HonamiDocLocalization.Get("Music, sound effects and voice on Audio tracks", "Музика, звукові ефекти й голос на треках Audio"),
                HonamiDocLocalization.Get("Screen fades, activation, events and parameter curves", "Фейди, активація, події та криві параметрів"),
                HonamiDocLocalization.Get("Nesting tracks inside items so they move together", "Вкладення треків в item-и, щоб вони рухалися разом")
            });

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Animation", "Animation"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "An Animation track plays clips on the GameObject it is bound to. The clip's curves are matched to that object's hierarchy by path, the same way Unity samples a clip, so bind the object the clip was made for: the character root for body animation, a door for its swing. Clips with a Speed play faster or slower, Reversed plays them backwards, and Loop repeats a clip that you stretch past its length, with a thin line marking each repeat.",
                "Трек Animation грає кліпи на GameObject, до якого прив'язаний. Криві кліпа зіставляються з ієрархією цього об'єкта за шляхами, так само як Unity семплює кліп, тож прив'язуйте той об'єкт, для якого зроблено кліп: корінь персонажа для анімації тіла, двері для їхнього руху. Кліпи зі Speed грають швидше чи повільніше, Reversed грає їх задом наперед, а Loop повторює кліп, розтягнутий довше за свою довжину, і тонка лінія позначає кожен повтор."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Director evaluates after Animators and Honami Animators have run, so an Animation track wins over whatever the character's controller is doing and you do not need to switch the controller off. Blend In eases the clip over the pose the character already has, so a cutscene can take over someone who is running or fighting without a pop, and Blend Out eases back into their own animation. Two overlapping clips on one track crossfade. Each clip has its own Blend In Curve and Blend Out Curve, either a preset or a custom curve, and the same curve drives the animator crossfade, so the timing you see in the Timeline window is the timing the character plays.",
                "Director оцінюється після Animator і Honami Animator, тож трек Animation перемагає все, що робить контролер персонажа, і вимикати контролер не потрібно. Blend In плавно накладає кліп на позу, яку персонаж уже має, тож катсцена може перехопити того, хто біжить чи б'ється, без ривка, а Blend Out плавно повертає його до власної анімації. Два кліпи, що перекриваються на одному треку, роблять crossfade. Кожен кліп має власні Blend In Curve і Blend Out Curve, пресет або власну криву, і та сама крива керує crossfade аніматора, тож таймінг у вікні Timeline збігається з тим, що грає персонаж."
            ));
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Double-click a clip to open it in the clip editor of the same window. Trim, split and speed changes never touch the clip asset itself: they only change which part of it the item plays.",
                "Подвійний клік по кліпу відкриває його в редакторі кліпів того ж вікна. Обрізка, розріз і зміна швидкості ніколи не чіпають сам ассет кліпа: вони змінюють лише те, яку його частину грає item."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Audio", "Audio"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "An Audio track plays its clips in sync with the timeline. Each clip has a Volume, fades through its Blend In and Blend Out handles, and overlapping clips crossfade, so music cues can be joined without a gap. The fade shape is configurable as well: use a preset for a natural swell or a custom curve when a linear or exponential volume ramp sounds better. The track's own Volume scales everything on it. Loop repeats a clip stretched past its end, handy for ambience beds. Waveforms are drawn on the items, so you can line up a hit with a cut by eye.",
                "Трек Audio грає свої кліпи синхронно з таймлайном. Кожен кліп має Volume, фейди через ручки Blend In і Blend Out, а кліпи, що перекриваються, роблять crossfade, тож музичні фрагменти з'єднуються без паузи. Форму фейду теж можна налаштувати: оберіть пресет для природного наростання або власну криву, коли лінійна чи експоненційна зміна гучності звучить краще. Власний Volume треку масштабує все на ньому. Loop повторює кліп, розтягнутий за його кінець, що зручно для фонового ембієнту. На item-ах малюється хвиля, тож удар можна вирівняти зі склейкою на око."
            ));
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Unbound, the track plays through its own sources on the Director as plain 2D sound. Bind it to an AudioSource to take that source's mixer group, spatial blend, distances and effects settings: bind a source on a character for a 3D voice line that comes from their mouth, or a source routed to your Music group so the player's volume settings apply. Sound pauses with the Director and stays in sync after a Seek.",
                "Без біндингу трек грає через власні джерела на Director як звичайний 2D-звук. Прив'яжіть його до AudioSource, щоб узяти mixer group, spatial blend, дистанції та ефекти цього джерела: прив'яжіть джерело на персонажі для 3D-репліки, що лунає з його рота, або джерело, спрямоване в групу Music, щоб діяли налаштування гучності гравця. Звук стає на паузу разом із Director і лишається синхронним після Seek."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Screen Fade", "Screen Fade"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A fade goes from one opacity of a colour to another over its length, eased when Smooth is on and shaped by Opacity Curve, which takes a preset or a custom curve. After a fade ends its last opacity holds until the next fade starts, so fade to black, cut to a new location under the black, then fade back in with a second item. When the cutscene stops the screen clears. The overlay draws above your UI; lower the track's Sorting Order if a menu must stay on top.",
                "Фейд переходить від однієї непрозорості кольору до іншої за свою довжину, плавно, коли увімкнено Smooth, а форму задає Opacity Curve, пресет або власна крива. Після кінця фейда його остання непрозорість тримається до наступного, тож затемніть у чорне, перенесіть дію в нове місце під чорним і поверніть картинку другим item-ом. Коли катсцена зупиняється, екран очищається. Оверлей малюється над вашим UI; зменште Sorting Order треку, якщо меню має лишатися зверху."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Activation, events and curves", "Активація, події та криві"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Track", "Трек"), 150),
                (HonamiDocLocalization.Get("What it does", "Що робить"), 0),
                ("", 0),
                ("Activation", HonamiDocLocalization.Get("Keeps the bound GameObject active only inside its ranges: a prop that appears, an effect that runs for three seconds. Post Playback decides what happens when the cutscene stops: Revert to how it was, force Active or Inactive, or Leave As Is.", "Тримає прив'язаний GameObject активним лише всередині своїх діапазонів: проп, що з'являється, ефект на три секунди. Post Playback вирішує, що стається, коли катсцена зупиняється: Revert до попереднього, примусово Active чи Inactive, або Leave As Is."), ""),
                ("Local Events", HonamiDocLocalization.Get("Calls TriggerEvent on the bound Honami Local Event Receiver: start a dialogue line, spawn an enemy, unlock a door.", "Викликає TriggerEvent на прив'язаному Honami Local Event Receiver: запустити репліку, заспавнити ворога, відчинити двері."), ""),
                ("Global Events", HonamiDocLocalization.Get("Executes every Honami Global Event component with the given id, wherever it is. Needs no binding.", "Виконує всі компоненти Honami Global Event із заданим id, де б вони не були. Біндинг не потрібен."), ""),
                ("Log", HonamiDocLocalization.Get("Writes a message to the console. Useful to check that timing is right in a build.", "Пише повідомлення в консоль. Корисно перевірити, що тайминг правильний у білді."), ""),
                ("Parameter Curve", HonamiDocLocalization.Get("Drives a float parameter of the bound Honami Animator along a curve: blend a character's mood, open a blend tree, drive an aim weight.", "Керує float-параметром прив'язаного Honami Animator уздовж кривої: змішати настрій персонажа, відкрити blend tree, керувати вагою прицілу."), "")
            );
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Events fire when playback passes them, forward or backward, and never on a Seek or while scrubbing in the editor. Skip fires every event it jumps over, so make sure an event that sets up gameplay (enabling a door, saving a checkpoint) is not also undone by a later event you might skip past.",
                "Події спрацьовують, коли відтворення їх проходить, уперед чи назад, і ніколи під час Seek чи скрабу в редакторі. Skip запускає всі події, які перестрибує, тож стежте, щоб подія, яка налаштовує геймплей (відчинити двері, зберегти чекпоінт), не скасовувалася пізнішою подією, яку теж можна перестрибнути."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Nesting tracks inside items", "Вкладення треків в item-и"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A track can live inside an item of another track. Right-click a shot and choose Add Module Inside to give it its own Audio track for the line spoken in that shot, or its own Activation for a light that only exists in it. The nested track is timed from the start of its parent item, runs only while that item plays, is muted and locked with it, and moves with it: slide the shot and its dialogue slides along. Nesting can go as deep as you like.",
                "Трек може жити всередині item-а іншого треку. Клацніть правою кнопкою по шоту й оберіть Add Module Inside, щоб дати йому власний трек Audio для репліки, сказаної в цьому шоті, або власний Activation для світла, що існує лише в ньому. Вкладений трек відраховує час від початку свого батьківського item-а, працює лише поки той грає, глушиться й блокується разом із ним і рухається з ним: посуньте шот, і його діалог посунеться слідом. Вкладати можна на будь-яку глибину."
            ));
        }
    }
}
