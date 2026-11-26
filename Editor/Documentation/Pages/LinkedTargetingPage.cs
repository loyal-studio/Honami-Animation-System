using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class LinkedTargetingPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Targeting & Tags", "Таргетинг та теги");
        public string Category => HonamiDocLocalization.Get("06. Honami Linked", "06. Honami Linked");
        public string SearchKeywords => "targeting tags linked brain filtering broadcast bytag children таргетування теги фільтрація";
        public int Order => 540;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "When using a Linked Brain, you might not want every command to affect every animator. Targeting lets you filter which linked animators receive a specific command.",
                "При використанні Linked Brain ви можете не хотіти, щоб кожна команда впливала на кожного аніматора. Таргетинг дозволяє фільтрувати, які саме прилінковані аніматори отримують конкретну команду."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Linking Tags", "Теги лінкування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Each HonamiAnimator has a Linking Tag slot that takes a HonamiTagID asset (Create → Honami Animation → Tag ID). Tags are assets, not strings, so renaming them never breaks references. One animator carries one tag - think of it as its role in the group: 'Body', 'Turret', 'Heavy'.",
                "Кожен HonamiAnimator має слот Linking Tag, куди призначається ассет HonamiTagID (Create → Honami Animation → Tag ID). Теги - це ассети, а не рядки, тож перейменування нічого не ламає. Один аніматор має один тег - сприймайте його як роль у групі: «Body», «Turret», «Heavy»."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Broadcast target modes", "Режими таргетингу розсилок"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Mode", "Режим"), 140),
                (HonamiDocLocalization.Get("Who receives the command", "Хто отримує команду"), 0),
                ("", 0),
                ("AllLinked", HonamiDocLocalization.Get("Default. Every animator linked to the Brain.", "Типово. Кожен аніматор, прилінкований до Мозку."), ""),
                ("ChildrenOnly", HonamiDocLocalization.Get("Only linked animators that are children of the Brain's transform.", "Лише прилінковані аніматори, що є дочірніми для transform Мозку."), ""),
                ("ByTag", HonamiDocLocalization.Get("Only linked animators whose Linking Tag equals the passed HonamiTagID.", "Лише прилінковані аніматори, чий Linking Tag дорівнює переданому HonamiTagID."), "")
            );

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Broadcast an ActionID to a filtered sub-group.
brain.SetActionID(duckAction, target: HonamiLinkedTarget.ByTag(infantryTag));

// Play a state on children only.
brain.PlayState(""Alert"", target: HonamiLinkedTarget.Children);

// Tag-scoped parameter writes.
brain.SetBool(""IsFiring"", true, HonamiLinkedTarget.ByTag(turretTag));
brain.SetTrigger(""Recoil"", HonamiLinkedTarget.ByTag(turretTag));

// Distance targeting needs no tags at all.
brain.SetActionID(duckAction, target: HonamiLinkedTarget.Within(blast.position, 20f, limit: 8));");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Combining filters", "Комбінування фільтрів"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The filters compose, because they are one value rather than a set of overloads. A HonamiLinkedTarget carries a mode, an optional tag, an optional sphere and an optional cap in one readonly struct, so \"heavies, and only the three nearest the breach\" is a single expression. Build it once and reuse it across calls.",
                "Фільтри комбінуються, бо це одне значення, а не набір перевантажень. HonamiLinkedTarget несе режим, опційний тег, опційну сферу та опційний ліміт в одній readonly-структурі, тож «важкі, і лише троє найближчих до пролому» - це один вираз. Зберіть його раз і перевикористовуйте."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Heavies, but only the three nearest the breach.
var target = HonamiLinkedTarget.ByTag(heavyTag)
    .WithinSphere(breach.position, radius: 8f, limit: 3);

brain.SetActionID(braceAction, transitionDuration: 0.1f, target: target);
brain.PlayState(""Brace"", target: target);

// Reuse the same target to ask who it would have hit.
var hit = new HonamiAnimatorBase[8];
int n = brain.GetAllLinkedNonAlloc(hit, target);

// Ready-made targets: All, Children, ByTag(tag), Within(origin, radius, limit).
// Omitting the argument is the same as passing All.
brain.PlayState(""Alert"", target: HonamiLinkedTarget.Children);");
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "A limit of zero means no cap - that is what makes the default value accept everything. Set a positive limit only when you actually want to stop early.",
                "Ліміт нуль означає «без обмежень» - саме тому типове значення приймає всіх. Ставте додатний ліміт лише тоді, коли справді треба зупинитись раніше."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "There is no 'exclude by tag' mode. To split a squad into reacting and non-reacting halves, give the halves different tags and broadcast to the one you want. If you need a filter no mode expresses, the brain-free HonamiLinkedAction.Broadcast takes an arbitrary predicate - see Global Actions.",
                "Режиму «виключити за тегом» немає. Щоб розділити загін на тих, хто реагує, і тих, хто ні, дайте половинам різні теги та розсилайте тій, яка потрібна. Якщо потрібен фільтр, який не виражається режимом, - HonamiLinkedAction.Broadcast без мозку приймає довільний предикат; див. «Глобальні дії»."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "The Play State and Broadcast Action nodes of the Brain Graph expose the same target mode + tag pair in their inspectors, so choreography assets can target sub-groups exactly like code does. The Set Parameter node always writes to all linked animators.",
                "Ноди Play State і Broadcast Action у Brain Graph мають ті самі поля режиму таргетингу і тега у своїх інспекторах, тож ассети хореографії таргетують підгрупи так само, як код. Нода Set Parameter завжди пише всім прилінкованим аніматорам."
            ));
        }
    }
}
