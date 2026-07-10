using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class ControllerProfilesPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Controller Profiles", "Профілі контролерів");
        public string Category => HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти");
        public string SearchKeywords => "controller profile swap runtime setprofile setcontroller loadout weapon profiles профілі контролер зміна";
        public int Order => 270;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("The three pieces of the profile system and how they connect", "Три складові системи профілів і як вони з'єднуються"),
                    HonamiDocLocalization.Get("Switching whole controllers at runtime with SetProfile", "Перемикання цілих контролерів у рантаймі через SetProfile"),
                    HonamiDocLocalization.Get("ContinueEvaluating vs Freeze cross-fade modes", "Режими кросфейду ContinueEvaluating vs Freeze")
                },
                new[]
                {
                    HonamiDocLocalization.Get("The tutorial: you can build and assign a controller", "Туторіал: ви вмієте створити та призначити контролер")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Controller Profiles give runtime controller switching a name and a place. Instead of scattering SetController calls with hardcoded transition settings, you author named profile states — 'Unarmed', 'Rifle', 'Vehicle' — each bundling a controller with its own cross-fade duration, curve and transition mode, and switch by name from anywhere.",
                "Профілі контролерів дають зміні контролера в рантаймі ім'я та місце. Замість розкиданих викликів SetController із зашитими налаштуваннями переходів ви створюєте іменовані стани профілю — «Unarmed», «Rifle», «Vehicle» — кожен поєднує контролер із власними тривалістю кросфейду, кривою та режимом переходу, а перемикання відбувається за назвою звідусіль."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The three pieces", "Три складові"), HonamiEditorIcons.Profile);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Asset / Component", "Ассет / Компонент"), 220),
                (HonamiDocLocalization.Get("Role", "Роль"), 0),
                ("", 0),
                ("HonamiProfileState", HonamiDocLocalization.Get("ScriptableObject (Create → Honami Animation → Profiles → Profile State): a name, a controller, and transition settings (duration, curve, ContinueEvaluating / Freeze).", "ScriptableObject (Create → Honami Animation → Profiles → Profile State): назва, контролер і налаштування переходу (тривалість, крива, ContinueEvaluating / Freeze)."), ""),
                ("HonamiControllerProfileGraph", HonamiDocLocalization.Get("ScriptableObject (Create → ... → Profiles → Profile Graph) holding the profile states plus the default state applied on Awake.", "ScriptableObject (Create → ... → Profiles → Profile Graph), що містить стани профілю та default state, який застосовується в Awake."), ""),
                ("HonamiControllerProfile", HonamiDocLocalization.Get("MonoBehaviour next to the HonamiAnimator (Add Component → Honami Animation → Honami Controller Profile) that resolves names and drives the animator.", "MonoBehaviour поруч із HonamiAnimator (Add Component → Honami Animation → Honami Controller Profile), який резолвить назви та керує аніматором."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Usage", "Використання"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"using HonamiAnimationSystem.Runtime.Core;

// Switch by name — duration/curve/mode come from the profile state.
profile.SetProfile(""Rifle"");

// Override the authored settings for this one switch.
profile.SetProfile(""Vehicle"", transitionDuration: 0.6f,
    mode: HonamiControllerTransitionMode.Freeze);

// SetController through the profile: if the controller is part of the
// graph, its profile settings are applied automatically.
profile.SetController(pistolController);");

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Passing a negative transitionDuration (the default) means 'use the value stored in the profile state'. The same applies to the curve and mode arguments — null keeps the authored settings.",
                "Від'ємний transitionDuration (значення за замовчуванням) означає «взяти значення зі стану профілю». Те саме стосується аргументів кривої та режиму — null залишає авторські налаштування."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Squad-wide profiles", "Профілі для цілого загону"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "HonamiLinkedAnimator.SetProfile(name) forwards the call to the HonamiControllerProfile of every linked animator, so one call can move a whole squad from 'Patrol' to 'Combat' controller sets, each character using its own profile assets.",
                "HonamiLinkedAnimator.SetProfile(name) передає виклик HonamiControllerProfile кожного прилінкованого аніматора, тож один виклик може перевести цілий загін із наборів контролерів «Patrol» на «Combat», причому кожен персонаж використовує власні ассети профілю."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Profiles versus Override Controllers: an Override Controller creates a content variant of one controller (same structure, different clips). Profiles switch between entirely different controllers at runtime. They combine well — a profile state can point to an override controller.",
                "Профілі проти Override Controllers: Override Controller створює контентний варіант одного контролера (та сама структура, інші кліпи). Профілі перемикають між цілком різними контролерами в рантаймі. Вони добре поєднуються — стан профілю може вказувати на override controller."
            ));
        }
    }
}
