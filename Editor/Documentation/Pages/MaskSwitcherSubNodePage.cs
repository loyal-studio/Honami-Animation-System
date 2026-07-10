using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class MaskSwitcherSubNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Mask Switcher Sub-Node", "Підвузол Mask Switcher");
        public string Category => HonamiDocLocalization.Get("05. Sub-Nodes", "05. Підвузли");
        public string SearchKeywords => "mask switcher avatar mask dynamic conditions перемикач масок аватар маска умови";
        public int Order => 420;
        public int EstimatedReadTime => 2;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Mask Switcher Sub-Node is a powerful tool for dynamic skeletal control. It allows you to change the Avatar Mask of the current state at runtime based on parameter conditions without duplicating states or layers.",
                "Підвузол перемикання масок — це потужний інструмент для динамічного контролю скелета. Він дозволяє змінювати Avatar Mask поточного стану в реальному часі на основі умов параметрів, не дублюючи стани або шари."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("How it works", "Як це працює"), HonamiEditorIcons.GraphWhite);

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The node evaluates a list of Rules from top to bottom. Each rule consists of a list of Conditions and an Avatar Mask to apply.",
                "Вузол оцінює список правил зверху вниз. Кожне правило складається зі списку умов та Avatar Mask, яку потрібно застосувати."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "First Match: The first rule that satisfies all its conditions will apply its mask to the state.",
                "Перша відповідність: Перше правило, яке задовольняє всі свої умови, застосує свою маску до стану."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Default Fallback: If no rules match, the sub-node restores the original Avatar Mask defined in the State Inspector.",
                "Дефолтний фолбек: Якщо жодне правило не підходить, підвузол відновлює оригінальну Avatar Mask, визначену в інспекторі стану."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Always True Rules: A rule with no conditions is always considered true and can be used as a custom 'default' if placed at the end of the list.",
                "Завжди істинні правила: Правило без умов завжди вважається істинним і може використовуватися як кастомний 'дефолт', якщо розміщене в кінці списку."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Use Cases", "Варіанти використання"), HonamiEditorIcons.TimelineWhite);

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Dual Wielding: Swap between FullBody, LeftHandOnly, or RightHandOnly masks based on which weapons are equipped.",
                "Парна зброя: Перемикайтеся між масками FullBody, LeftHandOnly або RightHandOnly залежно від того, яка зброя екіпірована."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Directional Hit Reactions: Apply a mask to only the left or right side of the body based on the impact direction parameter.",
                "Направлені реакції: Застосовуйте маску лише до лівої або правої частини тіла залежно від параметра напрямку удару."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Example: Dual Wielding Setup", "Приклад: Налаштування Dual Wielding"), HonamiEditorIcons.GraphWhite);

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Imagine you have a single 'Attack' state, but you want it to behave differently depending on whether you hold one or two swords.",
                "Уявіть, що у вас є один стан 'Attack', але ви хочете, щоб він поводився по-різному залежно від того, тримаєте ви один чи два мечі."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("1. Parameters", "1. Параметри"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Create two Boolean parameters in your Honami Controller: 'HasLeft' and 'HasRight'.",
                "Створіть два Boolean параметри у вашому Honami Controller: 'HasLeft' та 'HasRight'."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("2. Masks", "2. Маски"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("FullBody: Includes all bones.", "FullBody: Включає всі кістки."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("LeftArmOnly: Includes only the left arm hierarchy.", "LeftArmOnly: Включає лише ієрархію лівої руки."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("RightArmOnly: Includes only the right arm hierarchy.", "RightArmOnly: Включає лише ієрархію правої руки."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("3. Rule Configuration", "3. Налаштування правил"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "In the Mask Switcher Sub-Node, add rules in this specific order. Use 'Mirror Animation' to reuse your Right Hand animations for the Left Hand.",
                "У підвузлі Mask Switcher додайте правила у такому порядку. Використовуйте 'Mirror Animation', щоб перевикористати анімації правої руки для лівої."
            ));

            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Rule 0 (Both)", "Правило 0 (Обидві)"), HonamiDocLocalization.Get("Conditions: HasLeft == true, HasRight == true. Mask: FullBody. Mirror: False.", "Умови: HasLeft == true, HasRight == true. Маска: FullBody. Mirror: False.")),
                (HonamiDocLocalization.Get("Rule 1 (Left only)", "Правило 1 (Тільки ліва)"), HonamiDocLocalization.Get("Conditions: HasLeft == true. Mask: LeftArmOnly. Mirror: True.", "Умови: HasLeft == true. Маска: LeftArmOnly. Mirror: True.")),
                (HonamiDocLocalization.Get("Rule 2 (Right only)", "Правило 2 (Тільки права)"), HonamiDocLocalization.Get("Conditions: HasRight == true. Mask: RightArmOnly. Mirror: False.", "Умови: HasRight == true. Маска: RightArmOnly. Mirror: False."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Mirroring Support", "Підтримка віддзеркалення"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Mask Switcher can also dynamically control animation mirroring. If 'Mirror Animation' is enabled for a rule, Honami will flip the animation axis for the current state. This allows you to reuse a single Right-Hand animation for the Left-Hand seamlessly.",
                "Mask Switcher також може динамічно керувати віддзеркаленням анімації. Якщо 'Mirror Animation' увімкнено для правила, Honami віддзеркалить вісь анімації для поточного стану. Це дозволяє безшовно перевикористовувати одну анімацію правої руки для лівої."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Reuse & Optimization: Mirroring is integrated into the animation graph. Turning it on via a rule has near-zero performance cost and is much more efficient than having separate left/right animation clips.",
                "Перевикористання та оптимізація: Віддзеркалення інтегроване в граф анімації. Його ввімкнення через правило має майже нульову вартість продуктивності та набагато ефективніше, ніж наявність окремих кліпів для лівої та правої рук."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Performance", "Продуктивність"), HonamiEditorIcons.Controller);

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Mask switching is handled efficiently via Honami's internal mask atlas. All masks used in the rules are automatically registered during the controller's bake process, ensuring zero garbage allocation and minimal overhead at runtime.",
                "Перемикання масок обробляється ефективно через внутрішній атлас масок Honami. Усі маски, що використовуються в правилах, автоматично реєструються під час процесу запікання контролера, що гарантує відсутність алокацій сміття та мінімальні витрати ресурсів у рантаймі."
            ));
        }
    }
}
