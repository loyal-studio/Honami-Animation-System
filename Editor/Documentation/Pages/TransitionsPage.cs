using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class TransitionsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Transitions", "Переходи");
        public string Category => HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти");
        public string SearchKeywords => "transitions blending duration curves exit time conditions priority victim smart inertial interrupt переходи змішування умови пріоритет";
        public int Order => 220;
        public int EstimatedReadTime => 8;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("Transition types: Standard, Victim, Smart", "Типи транзішнів: Standard, Victim, Smart"),
                    HonamiDocLocalization.Get("Every transition property: duration, exit time, priority, interruption", "Кожна властивість транзішна: тривалість, exit time, пріоритет, переривання"),
                    HonamiDocLocalization.Get("How conditions combine and when triggers are consumed", "Як поєднуються умови та коли споживаються тригери")
                },
                new[]
                {
                    HonamiDocLocalization.Get("Tutorial part 3: a basic transition with a condition", "Частина 3 туторіалу: базовий транзішн з умовою"),
                    HonamiDocLocalization.Get("Parameters page: parameter types", "Сторінка «Параметри»: типи параметрів")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Transitions define how the system moves from one state to another. Beyond the classic duration-and-conditions setup, Honami transitions have a type (blending strategy), a priority, interruption rules and can even write parameters when they fire.",
                "Переходи визначають, як система рухається від одного стану до іншого. Окрім класичних тривалості та умов, переходи Honami мають тип (стратегію блендінгу), пріоритет, правила переривання і навіть можуть записувати параметри при спрацюванні."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Transition Types", "Типи переходів"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Type", "Тип"), 110),
                (HonamiDocLocalization.Get("Behavior", "Поведінка"), 0),
                ("", 0),
                ("Standard", HonamiDocLocalization.Get("Classic linear (or curve-driven) cross-fade over the duration.", "Класичний лінійний (або керований кривою) кросфейд протягом тривалості."), ""),
                ("Victim", HonamiDocLocalization.Get("One side of the blend is declared the 'victim': its playback speed can be multiplied, its weight can drop on a custom curve or quadratically (Accelerated Weight Drop). Great for snappy attack interrupts that do not pop.", "Одна сторона блендінгу оголошується «жертвою»: її швидкість можна домножити, а вага може падати за власною кривою або квадратично (Accelerated Weight Drop). Чудово для різких переривань атак без візуальних стрибків."), ""),
                ("Smart", HonamiDocLocalization.Get("Adaptive duration: the blend time scales with the remaining time of the current state (Smart Scale Factor). Short remainder — short blend.", "Адаптивна тривалість: час блендінгу масштабується від часу, що лишився поточному стану (Smart Scale Factor). Мало лишилось — короткий бленд."), ""),
                ("Inertial", HonamiDocLocalization.Get("Reserved for a future inertialization blend. Currently behaves like Standard.", "Зарезервовано під майбутній inertialization-бленд. Наразі поводиться як Standard."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Transition Properties", "Властивості переходу"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Duration", "Duration"), HonamiDocLocalization.Get("Cross-fade length in seconds.", "Довжина кросфейду в секундах.")),
                (HonamiDocLocalization.Get("Has Exit Time / Exit Time", "Has Exit Time / Exit Time"), HonamiDocLocalization.Get("When enabled, the transition only fires after the state reaches the normalized time set in Exit Time. Values above 1 act as a cooldown measured in state durations: 1.5 fires after one and a half lengths of the animation — handy for weapons that need recovery time (e.g. a katana).", "Якщо увімкнено, перехід спрацьовує лише після досягнення станом нормалізованого часу, заданого в Exit Time. Значення понад 1 працюють як кулдаун у тривалостях стану: 1.5 спрацює через півтори довжини анімації — зручно для зброї з часом відновлення (напр. катани).")),
                (HonamiDocLocalization.Get("Use Curve", "Use Curve"), HonamiDocLocalization.Get("Replaces linear blending with a custom animation curve.", "Замінює лінійний блендінг власною кривою.")),
                (HonamiDocLocalization.Get("Destination Start Time", "Destination Start Time"), HonamiDocLocalization.Get("Offset in seconds at which the target state starts playing.", "Зсув у секундах, з якого починає грати цільовий стан.")),
                (HonamiDocLocalization.Get("Freeze Mode", "Freeze Mode"), HonamiDocLocalization.Get("Destination: the target holds its first frame while the blend runs and starts playing only when the transition completes — fast attacks stay crisp, the blend lands on a static wind-up pose instead of smearing the swing. Source: the state being left freezes on its current pose and fades out static — useful when leaving an attack for idle so the follow-through doesn't keep playing under the blend.", "Destination: цільовий стан тримає свій перший кадр, поки йде бленд, і починає грати лише після завершення транзішна — швидкі атаки лишаються чіткими, бленд заходить у статичну позу замаху замість того, щоб змазувати удар. Source: стан, з якого виходимо, застигає на поточній позі і фейдиться статичним — зручно при виході з атаки в idle, щоб хвіст удару не догравав під блендом.")),
                (HonamiDocLocalization.Get("Priority", "Priority"), HonamiDocLocalization.Get("Higher-priority transitions can interrupt lower-priority ones even when Can Interrupt is off.", "Переходи з вищим пріоритетом можуть переривати нижчі навіть із вимкненим Can Interrupt.")),
                (HonamiDocLocalization.Get("Can Interrupt Transition", "Can Interrupt Transition"), HonamiDocLocalization.Get("Allows this transition to fire while another blend is still in progress.", "Дозволяє цьому переходу спрацювати, поки інший бленд ще триває.")),
                (HonamiDocLocalization.Get("Sacrifice Existing", "Sacrifice Existing"), HonamiDocLocalization.Get("Instantly kills any other active transitions on the layer when this one fires.", "Миттєво вбиває всі інші активні переходи на шарі при спрацюванні цього.")),
                (HonamiDocLocalization.Get("Can Transition To Self", "Can Transition To Self"), HonamiDocLocalization.Get("Lets AnyState-style transitions restart the state they came from.", "Дозволяє переходам у стилі AnyState перезапускати стан, з якого вони прийшли."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Conditions", "Умови"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Conditions are the logical rules that must be met for a transition to trigger. All conditions on one transition combine with AND logic; to express OR, draw a second transition.",
                "Умови — це логічні правила, які мають бути виконані для спрацювання переходу. Усі умови одного переходу поєднуються логікою «ТА»; для «АБО» намалюйте другий перехід."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("If / IfNot: for bool parameters and triggers.", "If / IfNot: для bool-параметрів і тригерів."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Greater / Less: for float and int parameters.", "Greater / Less: для параметрів float та int."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Equals / NotEqual: for int parameters.", "Equals / NotEqual: для int-параметрів."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Triggers are consumed only when the transition actually fires — a failed condition leaves the trigger raised.", "Тригери споживаються лише коли перехід дійсно спрацьовує — невиконана умова залишає тригер піднятим."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Parameter assignments", "Присвоєння параметрів"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A transition can carry its own Parameter Assignments list — values written the moment the transition fires. Combined with state-level assignments this covers most bookkeeping without any code.",
                "Перехід може мати власний список Parameter Assignments — значення записуються в момент спрацювання переходу. Разом із присвоєннями на рівні станів це покриває більшість службової логіки без жодного коду."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "TryAutoSkipState in the Scripting API reads the first authored transition of a state — its duration, curve and priority — so code-driven skips stay consistent with what animators tuned in the graph.",
                "TryAutoSkipState зі Scripting API читає перший намальований перехід стану — його тривалість, криву та пріоритет — тож пропуски з коду лишаються узгодженими з тим, що аніматори налаштували в графі."
            ));
        }
    }
}
