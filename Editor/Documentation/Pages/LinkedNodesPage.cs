using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class LinkedNodesPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Brain Graph Nodes", "Вузли Brain Graph");
        public string Category => HonamiDocLocalization.Get("06. Honami Linked", "06. Honami Linked");
        public string SearchKeywords => "brain nodes graph sequences choreography wait condition broadcast вузли хореографія";
        public int Order => 530;
        public int EstimatedReadTime => 5;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Brain Graphs use specialized nodes to control multiple animators simultaneously. Nodes are ScriptableObject assets (Create → Honami Animation → Linked Animator Nodes) chained into sequences; the Brain's executor ticks them every frame until each reports Done.",
                "Brain Graph використовує спеціалізовані вузли для одночасного керування кількома аніматорами. Вузли — це ScriptableObject-ассети (Create → Honami Animation → Linked Animator Nodes), з'єднані в послідовності; екзекютор Мозку тікає їх щокадру, доки кожен не поверне Done."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Node reference", "Довідник вузлів"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Node", "Вузол"), 150),
                (HonamiDocLocalization.Get("What it does", "Що робить"), 0),
                ("", 0),
                ("Play State", HonamiDocLocalization.Get("Commands linked animators to play a named state. Supports target mode (AllLinked / ChildrenOnly / ByTag) and a transition duration.", "Наказує прилінкованим аніматорам відтворити стан за назвою. Підтримує режим таргетингу (AllLinked / ChildrenOnly / ByTag) і тривалість переходу."), ""),
                ("Broadcast Action", HonamiDocLocalization.Get("Fires a HonamiActionID at the group, with the same targeting options.", "Розсилає HonamiActionID по групі, з тими самими опціями таргетингу."), ""),
                ("Set Parameter", HonamiDocLocalization.Get("Writes a Float/Int/Bool value or sets/resets a Trigger on every linked animator.", "Записує значення Float/Int/Bool або вмикає/скидає Trigger на кожному прилінкованому аніматорі."), ""),
                ("Condition", HonamiDocLocalization.Get("Branches into onTrue / onFalse based on a parameter check (FloatGreater/Less, IntEquals/NotEqual, BoolTrue/False). The parameter is read from the first linked animator.", "Розгалужується на onTrue / onFalse за перевіркою параметра (FloatGreater/Less, IntEquals/NotEqual, BoolTrue/False). Параметр читається з першого прилінкованого аніматора."), ""),
                ("Wait", HonamiDocLocalization.Get("Pauses sequence execution for a duration in seconds.", "Призупиняє виконання послідовності на задану кількість секунд."), ""),
                ("Sequence", HonamiDocLocalization.Get("Runs its children strictly one after another, waiting for Running children to finish.", "Виконує дочірні вузли строго один за одним, чекаючи завершення тих, що в стані Running."), ""),
                ("Log", HonamiDocLocalization.Get("Prints a debug message — invaluable when tuning long choreographies.", "Друкує debug-повідомлення — незамінно при налаштуванні довгих хореографій."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Running graphs from code", "Запуск графів із коду"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Trigger a named event authored in the brain graph.
brain.TriggerEvent(""DoorBreach"");

// Or pass the event object directly.
brain.TriggerEvent(breachEvent);

// Abort all running brain sequences.
brain.StopBrainEvents();");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "A typical breach choreography: Sequence → Set Parameter (Alert=true) → Play State ('StackUp', ByTag: pointman) → Wait 0.4 → Broadcast Action (breachAction) → Log. One TriggerEvent call from gameplay runs the whole thing.",
                "Типова хореографія штурму: Sequence → Set Parameter (Alert=true) → Play State («StackUp», ByTag: pointman) → Wait 0.4 → Broadcast Action (breachAction) → Log. Один виклик TriggerEvent з геймплею запускає все це."
            ));
        }
    }
}
