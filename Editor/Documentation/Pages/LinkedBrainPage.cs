using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class LinkedBrainPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Linked Brain Orchestrator", "Оркестратор Linked Brain");
        public string Category => HonamiDocLocalization.Get("06. Honami Linked", "06. Honami Linked");
        public string SearchKeywords => "brain orchestrator linked animator component link modes prevent linking оркестратор";
        public int Order => 510;
        public int EstimatedReadTime => 6;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The HonamiLinkedAnimator component (the Brain) is the central command for a specific group of animators. It coordinates complex behaviors through a unified API or a visual Graph.",
                "Компонент HonamiLinkedAnimator (Мозок) — це центральна команда для конкретної групи аніматорів. Він координує складну поведінку через єдине API або візуальний граф."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Setup", "Налаштування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("1. Add HonamiLinkedAnimator (Honami Animation → Honami Linked Animator Brain) to a coordinator GameObject.", "1. Додайте HonamiLinkedAnimator (Honami Animation → Honami Linked Animator Brain) до об'єкта-координатора."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("2. Pick a Link Mode: Childs discovers animators automatically, Manual uses an explicit list.", "2. Оберіть Link Mode: Childs знаходить аніматори автоматично, Manual використовує явний список."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("3. Optionally assign a Brain Graph to define choreographed sequences.", "3. За бажанням призначте Brain Graph для хореографічних послідовностей."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Link Modes", "Режими зв'язку"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Mode", "Режим"), 120),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("", 0),
                ("Childs", HonamiDocLocalization.Get("Automatically finds and controls every Honami animator in children — Honami Animators and Clip Players alike, including inactive ones.", "Автоматично знаходить і керує всіма аніматорами Honami у дочірніх об'єктах — і Honami Animator, і Clip Player, включно з неактивними."), ""),
                ("Manual", HonamiDocLocalization.Get("You explicitly specify a list of animators to control.", "Ви явно вказуєте список аніматорів для керування."), "")
            );
            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Animators with Prevent Linking enabled are skipped in both modes. Targeting by tag is not a link mode — it happens per broadcast: pass HonamiBroadcastTargetMode.ByTag plus a HonamiTagID, and only linked animators whose Linking Tag matches will react.",
                "Аніматори з увімкненим Prevent Linking пропускаються в обох режимах. Таргетинг за тегом — не режим зв'язку, а параметр конкретної розсилки: передайте HonamiBroadcastTargetMode.ByTag разом із HonamiTagID, і зреагують лише прилінковані аніматори з відповідним Linking Tag."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Managing links at runtime", "Керування зв'язками в рантаймі"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Add or remove animators dynamically (spawned units, dismemberment...).
brain.Link(spawnedAnimator);
brain.Unlink(deadAnimator);

// Re-run discovery after a hierarchy change in Childs mode.
brain.RefreshLinkedAnimators();

// Inspect the current group.
foreach (var anim in brain.LinkedAnimators)
    Debug.Log(anim.name);

// Zero-allocation snapshot into a reusable buffer (Span-based):
var results = new HonamiAnimator[32];
int count = brain.GetLinkedAnimatorsNonAlloc(results,
    HonamiBroadcastTargetMode.ByTag, squadTag);");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Animators and Clip Players", "Аніматори та Clip Player"), HonamiEditorIcons.Profile);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A group can mix both Honami components. Playback broadcasts — PlayState by name, ActionIDs, StopAll, PauseAll, ResumeAll, GlobalWeight — reach everything the brain drives. Anything that needs a controller behind it — parameters, TrySkipState, SetLayerWeight, SetController, SetProfile — is applied only to the Honami Animators and silently skips Clip Players, which have no parameters to set.",
                "Група може змішувати обидва компоненти Honami. Розсилки відтворення — PlayState за іменем, ActionID, StopAll, PauseAll, ResumeAll, GlobalWeight — дістають усе, чим керує мозок. Усе, що потребує контролера — параметри, TrySkipState, SetLayerWeight, SetController, SetProfile — застосовується лише до Honami Animator і тихо оминає Clip Player, у яких немає параметрів."
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Property", "Властивість"), 200),
                (HonamiDocLocalization.Get("Contains", "Містить"), 0),
                ("", 0),
                ("LinkedAnimators", HonamiDocLocalization.Get("Every linked component, typed as HonamiAnimatorBase.", "Усі прилінковані компоненти, типізовані як HonamiAnimatorBase."), ""),
                ("FullAnimators", HonamiDocLocalization.Get("Only the controller-backed HonamiAnimators — the targets of parameter and state-machine calls.", "Лише HonamiAnimator із контролерами — цілі викликів параметрів і стейт-машини."), "")
            );
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "GetLinkedAnimatorsNonAlloc fills a Span<HonamiAnimator> and therefore only sees the controller-backed subset. Use GetAllLinkedNonAlloc with a Span<HonamiAnimatorBase> when Clip Players should be included.",
                "GetLinkedAnimatorsNonAlloc заповнює Span<HonamiAnimator>, тож бачить лише підмножину з контролерами. Використовуйте GetAllLinkedNonAlloc зі Span<HonamiAnimatorBase>, коли Clip Player теж мають потрапити у вибірку."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Group control API", "API групового керування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Brain mirrors most of the HonamiAnimator API, applied to every linked animator at once: PlayState, TrySkipState / TryAutoSkipState, SetFloat / SetInteger / SetBool / SetTrigger (plus ...ByTag variants), SetLayerWeight, PauseAll / ResumeAll / StopAll, and SetController / SetProfile for whole-group controller swaps. Broadcast targeting, distance filters and ActionIDs are covered on the Targeting and Advanced pages.",
                "Мозок дублює більшість API HonamiAnimator, застосовуючи його одразу до всіх прилінкованих аніматорів: PlayState, TrySkipState / TryAutoSkipState, SetFloat / SetInteger / SetBool / SetTrigger (плюс варіанти ...ByTag), SetLayerWeight, PauseAll / ResumeAll / StopAll, а також SetController / SetProfile для групової зміни контролерів. Таргетинг розсилок, фільтри за відстанню та ActionID описані на сторінках Targeting і Advanced."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// The whole squad ducks — one call.
brain.PlayState(""TakeCover"", transitionDuration: 0.2f);

// Only heavies (by tag) raise shields.
brain.PlayState(""ShieldUp"", HonamiBroadcastTargetMode.ByTag, heavyTag);

// Everyone within 15 m of the blast reacts, closest 5 only.
brain.PlayState(""Flinch"", explosion.position, 15f, transitionDuration: 0.1f, limit: 5);

// Drive a shared parameter for one sub-group.
brain.SetFloatByTag(crowdTag, ""PanicLevel"", 0.8f);

// Query helpers.
if (brain.AnyPlayingState(""Reload"")) { /* ... */ }
if (brain.TryGetAnimatorByTag(bossTag, out var boss)) { /* ... */ }");
        }
    }
}
