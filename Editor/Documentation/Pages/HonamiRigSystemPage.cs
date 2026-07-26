using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class HonamiRigSystemPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Rig System", "Система рігу (Rig)");
        public string Category => HonamiDocLocalization.Get("07. Tools & Pro", "07. Інструменти та Pro");
        public string SearchKeywords => "rigging ik constraints lookat physics foot ik point constraint pivot fixer pseudo physics non humanoid creatures ріг констрейни не гуманоїд";
        public int Order => 600;
        public int EstimatedReadTime => 9;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Honami Rig System exists because real game creatures are rarely clean Humanoids. Monsters, drones, crawlers, dolls, tentacles, armor pieces and weapon rigs all need animation logic without being forced through Unity's Humanoid Avatar workflow.",
                "Система рігу Honami існує тому, що реальні ігрові істоти рідко є чистими Humanoid. Монстри, дрони, повзуни, ляльки, щупальця, броня та weapon-rigs потребують анімаційної логіки без примусового проходження через Unity Humanoid Avatar workflow."
            ));

            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "Honami rigs operate on Transform references and authored bone paths. They do not require Mecanim muscle mapping, humanoid retargeting, or Unity Avatar Masks. If the creature has bones, Honami can reason about them.",
                    "Honami rigs працюють із Transform-посиланнями та bone paths. Їм не потрібні Mecanim muscle mapping, humanoid retargeting або Unity Avatar Masks. Якщо істота має кістки, Honami може з ними працювати."
                ),
                HonamiDocumentationBuilder.CalloutType.Tip);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Why Not Humanoid?", "Чому не Humanoid?"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Unity Humanoid pain", "Біль Unity Humanoid"), 190),
                (HonamiDocLocalization.Get("Honami answer", "Відповідь Honami"), 0),
                ("", 0),
                (HonamiDocLocalization.Get("Requires a body shaped like Unity expects.", "Вимагає тіло такої форми, як очікує Unity."), HonamiDocLocalization.Get("Works with arbitrary transform hierarchies: quadrupeds, insects, props, split bodies.", "Працює з довільними transform-ієрархіями: чотириногі, комахи, props, розділені тіла."), ""),
                (HonamiDocLocalization.Get("Avatar setup becomes busywork for every weird skeleton.", "Avatar setup стає рутиною для кожного дивного скелета."), HonamiDocLocalization.Get("Honami Avatar stores only the bones you care about by path.", "Honami Avatar зберігає лише потрібні кістки за path."), ""),
                (HonamiDocLocalization.Get("Masks are tied to Unity's Avatar assumptions.", "Маски прив'язані до припущень Unity Avatar."), HonamiDocLocalization.Get("Honami masks are weighted per bone and can be layered, switched and rebaked.", "Honami masks мають вагу на кожну кістку, їх можна шарувати, перемикати та rebake-ити."), ""),
                (HonamiDocLocalization.Get("Retargeting helps humans, then fights creatures.", "Retargeting допомагає людям, а потім заважає істотам."), HonamiDocLocalization.Get("No forced retargeting. Use the skeleton's actual structure.", "Немає примусового retargeting. Використовуйте реальну структуру скелета."), "")
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Rig Components", "Компоненти рігу"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("Honami Rigging Processor", "Honami Rigging Processor"), HonamiDocLocalization.Get("Collects HonamiRig components in children and evaluates them after animation in LateUpdate.", "Збирає HonamiRig компоненти в дочірніх об'єктах і обчислює їх після анімації в LateUpdate."), HonamiEditorIcons.Controller),
                (HonamiDocLocalization.Get("Foot IK Rig", "Foot IK Rig"), HonamiDocLocalization.Get("Two-bone leg solver with ground casting, toe alignment, pelvis/root lowering and anti-sliding foot sticking.", "Two-bone leg solver із ground casting, toe alignment, опусканням pelvis/root та anti-sliding фіксацією стоп."), HonamiEditorIcons.TimelineWhite),
                (HonamiDocLocalization.Get("LookAt Constraint", "LookAt Constraint"), HonamiDocLocalization.Get("Rotates one or many bones toward a target with aim/up axes, additive mode, smoothing and angle limits.", "Повертає одну або багато кісток до цілі з aim/up axes, additive mode, smoothing та angle limits."), HonamiEditorIcons.GraphWhite),
                (HonamiDocLocalization.Get("Point Constraint", "Point Constraint"), HonamiDocLocalization.Get("Makes a transform follow another transform's position and/or rotation with offsets and interpolation.", "Змушує transform слідувати позиції та/або ротації іншого transform із offsets та interpolation."), HonamiEditorIcons.Controller),
                (HonamiDocLocalization.Get("Pivot Fixer", "Pivot Fixer"), HonamiDocLocalization.Get("Repositions or rerotates a target so an arbitrary pivot becomes the effective anchor.", "Переставляє або повертає target так, щоб довільний pivot став ефективною точкою прив'язки."), HonamiEditorIcons.BlendTreeWhite),
                (HonamiDocLocalization.Get("Pseudo-Physics Constraint", "Pseudo-Physics Constraint"), HonamiDocLocalization.Get("Springy secondary motion, or a real hanging chain simulation, on any list of bones.", "Spring secondary motion або справжня симуляція підвішеного ланцюга на будь-якому списку кісток."), HonamiEditorIcons.TimelineWhite)
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("1. Honami Rigging Processor", "1. Honami Rigging Processor"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Place this component on the animated root. It finds all enabled HonamiRig components in children, resets them when refreshed, and runs them after animation evaluation. This keeps procedural rigging as a final correction pass.",
                "Додайте цей компонент на animated root. Він знаходить усі enabled HonamiRig компоненти в дочірніх об'єктах, скидає їх при refresh і запускає після обчислення анімації. Так procedural rigging працює як фінальний correction pass."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// When creating or enabling rigs dynamically:
var processor = GetComponent<HonamiRiggingProcessor>();
processor.RefreshRigs();");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("2. Foot IK Rig", "2. Foot IK Rig"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Foot IK is not limited to two human feet. The rig stores an array of legs, so you can configure two legs, four legs, spider legs, mech feet or any repeated two-bone support chain.",
                "Foot IK не обмежений двома людськими стопами. Rig зберігає масив legs, тому можна налаштувати дві ноги, чотири ноги, spider legs, mech feet або будь-який повторюваний two-bone support chain."
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Leg bones", "Кістки ноги"), HonamiDocLocalization.Get("Assign thigh, calf, foot and optional toes for every leg.", "Призначте thigh, calf, foot і опціональні toes для кожної ноги.")),
                (HonamiDocLocalization.Get("Ground cast", "Ground cast"), HonamiDocLocalization.Get("Use groundLayers, raycastRadius, up offset and distance to find contact points.", "Використовуйте groundLayers, raycastRadius, up offset і distance для пошуку contact points.")),
                (HonamiDocLocalization.Get("Root bone", "Root bone"), HonamiDocLocalization.Get("Optional pelvis/body bone moves down so legs remain in reach.", "Опціональна pelvis/body кістка опускається, щоб ноги залишалися в межах reach.")),
                (HonamiDocLocalization.Get("Sticking", "Sticking"), HonamiDocLocalization.Get("Pins a foot horizontally while it is close to the ground to reduce sliding.", "Фіксує стопу горизонтально, поки вона близько до землі, щоб зменшити sliding."))
            );
            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "For a quadruped, use one Foot IK Rig with four leg entries. For a spider, use multiple entries and tune each leg's weightMultiplier if some legs are decorative.",
                "Для quadruped використовуйте один Foot IK Rig із чотирма leg entries. Для павука додайте більше entries і налаштуйте weightMultiplier, якщо частина ніг декоративна."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("3. LookAt Constraint", "3. LookAt Constraint"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "LookAt rotates bones toward a target using the bones' own local axes. It can drive a single head bone, a chain of spine/neck/head bones, eye bones, turrets, weapon barrels, antennae or creature sensors.",
                "LookAt повертає кістки до цілі з урахуванням їхніх локальних осей. Він може керувати однією head bone, ланцюгом spine/neck/head, eye bones, turrets, weapon barrels, antennae або creature sensors."
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Target Mode", "Target Mode"), HonamiDocLocalization.Get("Single processes the first target entry. Multiple processes the whole targets array.", "Single обробляє перший target entry. Multiple обробляє весь масив targets.")),
                (HonamiDocLocalization.Get("LookAt Type", "LookAt Type"), HonamiDocLocalization.Get("Absolute sets the target orientation. Additive applies the look delta on top of the current animation.", "Absolute ставить цільову орієнтацію. Additive додає look delta поверх поточної анімації.")),
                (HonamiDocLocalization.Get("Aim/Up axes", "Aim/Up axes"), HonamiDocLocalization.Get("Per-bone axes let non-standard bones point correctly without Humanoid assumptions.", "Осі на кожну кістку дозволяють нестандартним bones дивитися правильно без Humanoid assumptions.")),
                (HonamiDocLocalization.Get("Limits", "Limits"), HonamiDocLocalization.Get("Use maxAngle and per-axis weights to keep motion believable.", "Використовуйте maxAngle і per-axis weights, щоб рух лишався правдоподібним."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("4. Point Constraint", "4. Point Constraint"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Point Constraint is the simple hard-working rig: bind one transform to another transform's position and/or rotation. Use it for props, weapon hands, muzzle anchors, armor plates, held objects or synchronized split-body pieces.",
                "Point Constraint — простий робочий rig: прив'язує один transform до позиції та/або ротації іншого transform. Використовуйте для props, weapon hands, muzzle anchors, armor plates, held objects або синхронізованих split-body pieces."
            ));
            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Common setup idea:
// point  = weapon socket, armor plate, dangling prop
// target = animated hand bone, muzzle bone, mechanical joint
// lockPosition/lockRotation decide which channels follow.
// positionOffset/rotationOffset keep authored grip offsets.");

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("5. Pivot Fixer", "5. Pivot Fixer"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Pivot Fixer solves a common imported-animation problem: the animated object has one pivot, but gameplay needs another anchor. It can keep a weapon grip, door hinge, tool handle or creature attachment point stable while preserving selected animation channels.",
                "Pivot Fixer вирішує типову проблему імпортованих анімацій: animated object має один pivot, а gameplay потребує іншу точку прив'язки. Він може стабілізувати weapon grip, door hinge, tool handle або creature attachment point, зберігаючи вибрані animation channels."
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Fix Mode", "Fix Mode"), HonamiDocLocalization.Get("Choose position+rotation, position only, or rotation only.", "Виберіть position+rotation, only position або only rotation.")),
                (HonamiDocLocalization.Get("Block Mode", "Block Mode"), HonamiDocLocalization.Get("Static, SnapToPivot and KeepOffset control how animation channels are preserved or blocked.", "Static, SnapToPivot і KeepOffset керують тим, як animation channels зберігаються або блокуються.")),
                (HonamiDocLocalization.Get("Axis masks", "Axis masks"), HonamiDocLocalization.Get("Fix only the axes that matter for the current asset.", "Фіксуйте лише ті осі, які важливі для конкретного asset.")),
                (HonamiDocLocalization.Get("Manual offsets", "Manual offsets"), HonamiDocLocalization.Get("Fine-tune imported content without rebaking the source animation.", "Підкручуйте imported content без rebake source animation."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("6. Pseudo-Physics Constraint", "6. Pseudo-Physics Constraint"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The constraint runs in one of two modes. PseudoPhysics adds reactive spring motion to bones after the authored animation is sampled: no Rigidbody chain, just controlled secondary motion that always springs back to the animated pose. Simulation turns the listed bones into a real hanging chain — particles with gravity, inertia and a rigid bone length that swing and settle on their own.",
                "Констрейн працює в одному з двох режимів. PseudoPhysics додає reactive spring motion до кісток після sampling authored animation: без Rigidbody chain, лише контрольований secondary motion, який завжди повертається до animated pose. Simulation перетворює перелічені кістки на справжній підвішений ланцюг — партікли з gravity, inertia та жорсткою довжиною кістки, які самі розгойдуються й заспокоюються."
            ));
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("PseudoPhysics targets", "Цілі для PseudoPhysics"), HonamiDocLocalization.Get("Hair, cloth strips, tails, antennae, soft armor, weapon sway — anything that should stick to the animated pose.", "Hair, cloth strips, tails, antennae, soft armor, weapon sway — усе, що має триматися animated pose.")),
                (HonamiDocLocalization.Get("Simulation targets", "Цілі для Simulation"), HonamiDocLocalization.Get("Keychains, pendants, straps, hanging props — anything that hangs down and swings freely.", "Брелки, підвіски, ремінці, hanging props — усе, що звисає вниз і вільно гойдається.")),
                (HonamiDocLocalization.Get("Position physics", "Position physics"), HonamiDocLocalization.Get("PseudoPhysics only: drag, inertia, stiffness, damping, max offset and local gravity.", "Лише PseudoPhysics: drag, inertia, stiffness, damping, max offset та local gravity.")),
                (HonamiDocLocalization.Get("Rotation physics", "Rotation physics"), HonamiDocLocalization.Get("PseudoPhysics only: angular drag/inertia with rotation stiffness, damping and max rotation.", "Лише PseudoPhysics: angular drag/inertia з rotation stiffness, damping та max rotation.")),
                (HonamiDocLocalization.Get("Chain physics", "Chain physics"), HonamiDocLocalization.Get("Simulation only: world gravity, damping, stiffness toward the animated pose, inertia and an angle limit.", "Лише Simulation: world gravity, damping, stiffness до animated pose, inertia та angle limit.")),
                (HonamiDocLocalization.Get("Stiffness 0", "Stiffness 0"), HonamiDocLocalization.Get("With no spring to the bind pose the chain starts already settled along Gravity — the only way an object modelled sticking up can hang down instead of balancing on an inverted pendulum.", "Без spring до bind pose ланцюг стартує вже осілим уздовж Gravity — єдиний спосіб для об'єкта, змодельованого догори, звисати вниз, а не балансувати на перевернутому маятнику.")),
                (HonamiDocLocalization.Get("Freeze Axis", "Freeze Axis"), HonamiDocLocalization.Get("Simulation only: locks the chain into a plane through the root bone. Use it for a ring on a peg that swings but never wanders sideways.", "Лише Simulation: замикає ланцюг у площині через root bone. Використовуйте для кільця на штирі, яке гойдається, але не блукає вбік.")),
                (HonamiDocLocalization.Get("Per-bone axes", "Per-bone axes"), HonamiDocLocalization.Get("Simulation only: Up Axis rolls the bone while physics aims it; Tip Direction and Tip Length give the last bone of a chain something to swing.", "Лише Simulation: Up Axis крутить кістку по roll, поки фізика її націлює; Tip Direction і Tip Length дають останній кістці ланцюга те, чим гойдати.")),
                (HonamiDocLocalization.Get("Facing", "Facing"), HonamiDocLocalization.Get("Simulation only, per bone: a constant — a world direction, a transform or the main camera — that the bone keeps turning back to. Physics still decides where it hangs; Facing only rolls it about that direction, so a flat charm keeps showing its face to the player instead of drifting edge-on. Strength, Speed and Max Angle decide how hard, how fast and how far.", "Лише Simulation, для кожної кістки: константа — world direction, transform або main camera — до якої кістка постійно повертається. Фізика все одно вирішує, куди вона звисає; Facing лише крутить її по roll навколо цього напрямку, тож плоский брелок тримається лицем до гравця, а не дрейфує ребром. Strength, Speed і Max Angle задають, наскільки сильно, наскільки швидко й наскільки далеко.")),
                (HonamiDocLocalization.Get("Axis masks", "Axis masks"), HonamiDocLocalization.Get("PseudoPhysics only: restrict motion to the local axes that should move.", "Лише PseudoPhysics: обмежуйте рух локальними осями, які мають рухатися."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Creature Examples", "Приклади істот"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Creature", "Істота"), 150),
                (HonamiDocLocalization.Get("Useful rigs", "Корисні ріги"), 190),
                (HonamiDocLocalization.Get("Example", "Приклад"), 0),
                ("Quadruped", "Foot IK + LookAt + Pseudo-Physics", HonamiDocLocalization.Get("Four leg entries for terrain, neck LookAt for target tracking, tail physics for weight.", "Чотири leg entries для terrain, neck LookAt для target tracking, tail physics для ваги.")),
                ("Spider / crawler", "Foot IK + Point Constraint", HonamiDocLocalization.Get("Many support legs with different weights, abdomen/socket constraints for attached parts.", "Багато support legs із різними weights, abdomen/socket constraints для прикріплених частин.")),
                ("Flying drone", "LookAt + Pivot Fixer", HonamiDocLocalization.Get("Turret/camera bones track targets, pivot fixer keeps weapon anchors stable.", "Turret/camera bones відстежують цілі, pivot fixer стабілізує weapon anchors.")),
                ("Tentacle enemy", "LookAt + Pseudo-Physics", HonamiDocLocalization.Get("Segments aim toward the player while tips keep springy follow-through.", "Segments дивляться на гравця, а tips мають springy follow-through.")),
                ("Modular boss", "Point Constraint + Pivot Fixer", HonamiDocLocalization.Get("Armor and weapon pieces follow animated sockets without becoming Humanoid bones.", "Armor і weapon pieces слідують animated sockets без перетворення на Humanoid bones."))
            );
        }
    }
}
