using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class EventSystemPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Event System", "Система подій");
        public string Category => HonamiDocLocalization.Get("04. Core Systems", "04. Основні системи");
        public string SearchKeywords => "events markers animation events callbacks sounds effects local global receiver unityevent setup eventid події маркери колбеки налаштування ресівер";
        public int Order => 440;
        public int EstimatedReadTime => 9;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Honami Event System triggers logic precisely at specific moments of an animation. It has two halves: event markers authored on states, and C# events raised by the animator itself when states start and end.",
                "Система подій Honami запускає логіку точно у визначені моменти анімації. Вона складається з двох частин: маркери подій, розставлені на станах, і C#-події, які сам аніматор викликає на початку та в кінці станів."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Event Markers", "Маркери подій"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Markers are placed on the state's timeline. When the playhead passes a marker, its event fires. Each marker is either Local or Global.",
                "Маркери розміщуються на таймлайні стану. Коли головка відтворення проходить маркер, спрацьовує його подія. Кожен маркер є або Local, або Global."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Type", "Тип"), 100),
                (HonamiDocLocalization.Get("How it fires", "Як спрацьовує"), 0),
                ("", 0),
                ("Local", HonamiDocLocalization.Get("Calls TriggerEvent(eventName) on the HonamiLocalEventReceiver component next to the animator. The receiver maps names to UnityEvents you wire up in the inspector — no code required.", "Викликає TriggerEvent(eventName) на компоненті HonamiLocalEventReceiver поруч з аніматором. Ресівер зіставляє назви з UnityEvent, які ви налаштовуєте в інспекторі — код не потрібен."), ""),
                ("Global", HonamiDocLocalization.Get("Finds every active HonamiGlobalEvent component in the scene whose eventId matches — on any GameObject, not just the animator's — and calls its ExecuteEvent(). Subclass HonamiGlobalEvent to write your own reusable event behaviours.", "Знаходить у сцені кожен активний компонент HonamiGlobalEvent з відповідним eventId — на будь-якому GameObject, не лише на об'єкті аніматора — і викликає його ExecuteEvent(). Наслідуйте HonamiGlobalEvent, щоб писати власні перевикористовувані події."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Placing markers in the editor", "Розстановка маркерів у редакторі"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Open the controller in the Honami editor and select a state — its timeline appears in the Timeline panel at the bottom of the window.",
                "Відкрийте контролер у редакторі Honami та виберіть стан — його таймлайн з'явиться в панелі Timeline внизу вікна."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Right-click an empty spot on the timeline at the moment you need and choose Add Local Event or Add Global Event. A marker is created at that time.",
                "Клікніть правою кнопкою по порожньому місцю таймлайна в потрібний момент часу та виберіть Add Local Event або Add Global Event. Маркер створиться в цій точці."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Click the marker to select it. The Properties panel shows Time (seconds on the state's timeline), Type, and Event Name for Local or Global ID for Global markers. Remove Event deletes the marker.",
                "Клікніть по маркеру, щоб вибрати його. Панель Properties покаже Time (секунди на таймлайні стану), Type, а також Event Name для Local або Global ID для Global маркерів. Remove Event видаляє маркер."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Local event: full setup", "Local подія: повне налаштування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Place a Local marker on the state and set its Event Name, e.g. Footstep.",
                "Поставте Local маркер на стані та задайте йому Event Name, наприклад Footstep."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "On the same GameObject that has the HonamiAnimator, use Add Component → Honami Animation → Local Event Receiver.",
                "На тому самому GameObject, де висить HonamiAnimator, виконайте Add Component → Honami Animation → Local Event Receiver."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "In the receiver's Local Events list add an entry whose Event Name exactly matches the marker's name and wire the UnityEvent to your handlers (audio source, VFX, any method).",
                "У списку Local Events ресівера додайте запис, чий Event Name точно збігається з назвою маркера, і підключіть до UnityEvent ваші хендлери (аудіо, VFX, будь-який метод)."
            ));
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Several entries with the same Event Name all fire together. If the marker's name is not found in the receiver, a warning is logged to the console — use it to catch typos.",
                "Кілька записів з однаковим Event Name спрацюють усі разом. Якщо назву маркера в ресівері не знайдено, у консоль пишеться попередження — так зручно ловити одруки."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Global event: full setup", "Global подія: повне налаштування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Create a subclass of HonamiGlobalEvent with your logic in ExecuteEvent().",
                "Створіть підклас HonamiGlobalEvent з вашою логікою в ExecuteEvent()."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Add the component to any GameObject in the scene — a door, a camera rig, a VFX manager — and set its eventId.",
                "Додайте компонент на будь-який GameObject у сцені — двері, camera rig, VFX-менеджер — і задайте його eventId."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Place a Global marker and set its Global ID to the same value. Every HonamiGlobalEvent whose eventId matches will execute.",
                "Поставте Global маркер і впишіть у його Global ID те саме значення. Виконається кожен HonamiGlobalEvent, чий eventId збігається."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Events;
using UnityEngine;

public sealed class FootstepEvent : HonamiGlobalEvent
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] clips;

    public override void ExecuteEvent()
    {
        if (clips.Length > 0)
            source.PlayOneShot(clips[Random.Range(0, clips.Length)]);
    }
}
// Set eventId = ""Footstep"" in the inspector and reference it
// from a Global marker on any state.");
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiRigWeightEvent is a ready-made HonamiGlobalEvent that sets the weight of a HonamiRig when executed — handy for enabling IK exactly on the frame a hand grabs a ladder.",
                "HonamiRigWeightEvent — готова HonamiGlobalEvent, яка при виконанні встановлює вагу HonamiRig — зручно, щоб увімкнути IK точно на кадрі, коли рука хапає драбину."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Discovery rules differ. HonamiLocalEventReceiver is found once, on the same GameObject as the HonamiAnimator, when the animator initializes — add it next to the animator before Play mode starts. HonamiGlobalEvent components register themselves in OnEnable and unregister in OnDisable, so they work anywhere in the scene, can be added or removed at runtime, and only fire while their GameObject is active and the component is enabled.",
                "Правила виявлення різні. HonamiLocalEventReceiver шукається один раз, на тому самому GameObject, що й HonamiAnimator, під час ініціалізації аніматора — додавайте його поруч з аніматором до старту Play mode. Компоненти HonamiGlobalEvent реєструють себе в OnEnable і знімають реєстрацію в OnDisable, тому працюють будь-де в сцені, їх можна додавати й прибирати в рантаймі, і спрацьовують вони лише поки їхній GameObject активний, а компонент увімкнений."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Events and layers", "Події та шари"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Each layer evaluates markers independently and only for its current state: a state that is blending out no longer fires markers, and paused states or paused layers fire nothing. This makes markers a natural fit for the layer-per-action pipeline — a damage-window marker on an Attack-layer state fires regardless of what the Base layer's locomotion is doing. See Blending & Layers for the full pipeline walkthrough.",
                "Кожен шар обробляє маркери незалежно й лише для свого поточного стану: стан, який виблендується (fade out), маркери більше не викликає, а стани та шари на паузі не викликають нічого. Тому маркери природно лягають у пайплайн «шар під дію» — маркер вікна урону на стані шару Attack спрацює незалежно від того, що робить locomotion на базовому шарі. Повний розбір пайплайна — на сторінці Blending & Layers."
            ));

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Marker guarantees: on looping states markers re-fire every loop (if several loops pass in one tick, at most 3 are replayed); on reversed states markers fire when the playhead moves back past them; TryAutoSkipState with cancelEvents: true suppresses markers the skipped state has not fired yet.",
                "Гарантії маркерів: на loop-станах маркери спрацьовують кожен цикл (якщо за один тік минуло кілька циклів — програється щонайбільше 3); на реверсних станах маркери спрацьовують, коли головка проходить їх у зворотному напрямку; TryAutoSkipState із cancelEvents: true скасовує маркери, які пропущений стан ще не встиг викликати."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("C# state events", "C#-події станів"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Independently of markers, HonamiAnimator raises three C# events: OnStateEntered(string), OnStateFinished(string) for non-loop states that reached their end, and OnStateExited(HonamiStateExitInfo) with the exact exit reason. See the Scripting API page for the full HonamiStateExitInfo reference.",
                "Незалежно від маркерів, HonamiAnimator викликає три C#-події: OnStateEntered(string), OnStateFinished(string) для non-loop станів, що дійшли до кінця, та OnStateExited(HonamiStateExitInfo) з точною причиною виходу. Повний довідник HonamiStateExitInfo — на сторінці Scripting API."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"private void OnEnable()
{
    honami.OnStateEntered += HandleEntered;
    honami.OnStateFinished += HandleFinished;
}

private void OnDisable()
{
    honami.OnStateEntered -= HandleEntered;
    honami.OnStateFinished -= HandleFinished;
}

private void HandleEntered(string state)
{
    if (state == ""Reload"") ammoUI.ShowReloadSpinner();
}

private void HandleFinished(string state)
{
    if (state == ""Reload"") weapon.FillMagazine();
}");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Use markers for content-driven moments (footsteps, VFX, damage windows) and C# events for structural logic (state machines, combos, UI). Markers live with the animation data; C# events live with your gameplay code.",
                "Використовуйте маркери для контентних моментів (кроки, VFX, вікна урону), а C#-події — для структурної логіки (стейт-машини, комбо, UI). Маркери живуть разом з анімаційними даними; C#-події — разом з ігровим кодом."
            ));
        }
    }
}
