using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class IKAndConstraintsPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("IK & Constraints", "IK та констрейни");
        public string Category => HonamiDocLocalization.Get("07. Tools & Pro", "07. Інструменти та Pro");
        public string SearchKeywords => "ik inverse kinematics constraints bones rigging foot ik lookat point constraint pivot fixer pseudo physics non humanoid процедурна анімація";
        public int Order => 610;
        public int EstimatedReadTime => 7;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "IK and Constraints are where Honami becomes especially different from Unity's Humanoid-centered workflow. The goal is not to make every creature look like a human. The goal is to let any rig interact with the world using its own bone layout.",
                "IK та констрейни — це місце, де Honami особливо відрізняється від Unity Humanoid-centered workflow. Ціль не в тому, щоб кожну істоту зробити схожою на людину. Ціль у тому, щоб будь-який rig взаємодіяв зі світом через власну bone layout."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("What Honami Ships With", "Що є в Honami"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Component", "Компонент"), 170),
                (HonamiDocLocalization.Get("Core job", "Основна задача"), 0),
                (HonamiDocLocalization.Get("Use when", "Коли використовувати"), 0),
                ("Foot IK Rig", HonamiDocLocalization.Get("Two-bone leg solving, ground normal matching and foot sticking.", "Two-bone leg solving, ground normal matching та foot sticking."), HonamiDocLocalization.Get("Feet or support legs must stay believable on terrain.", "Стопи або support legs мають правдоподібно стояти на terrain.")),
                ("LookAt Constraint", HonamiDocLocalization.Get("Aim bones toward a target using authored aim/up axes.", "Спрямовує bones на target через задані aim/up axes."), HonamiDocLocalization.Get("Heads, eyes, turrets, sensors, tails or weapons track something.", "Голови, очі, turrets, sensors, tails або weapons відстежують щось.")),
                ("Point Constraint", HonamiDocLocalization.Get("Follow another transform's position and/or rotation.", "Слідує позиції та/або ротації іншого transform."), HonamiDocLocalization.Get("Props, sockets, armor or split-body pieces must stay aligned.", "Props, sockets, armor або split-body pieces мають лишатися aligned.")),
                ("Pivot Fixer", HonamiDocLocalization.Get("Move/rotate a target around a custom pivot.", "Рухає/повертає target навколо custom pivot."), HonamiDocLocalization.Get("Imported pivots are wrong for gameplay anchors.", "Imported pivots неправильні для gameplay anchors.")),
                ("Pseudo-Physics", HonamiDocLocalization.Get("Springy inertia and secondary motion on arbitrary bones.", "Spring inertia та secondary motion на довільних bones."), HonamiDocLocalization.Get("Hair, tails, cloth strips, weapon sway or soft armor need motion.", "Hair, tails, cloth strips, weapon sway або soft armor потребують руху."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Setup Pattern", "Патерн налаштування"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Add Honami Rigging Processor to the animated root object.", "Додайте Honami Rigging Processor на animated root object."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Add rig components anywhere under that root.", "Додайте rig components будь-де під цим root."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Assign explicit Transform references. Honami does not guess from Humanoid bone names.", "Призначте явні Transform references. Honami не вгадує їх із Humanoid bone names."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Tune the component weight first, then per-bone or per-leg multipliers.", "Спочатку налаштуйте component weight, потім per-bone або per-leg multipliers."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Use masks/layers for animation blending, rigs for final physical correction.", "Використовуйте masks/layers для animation blending, а rigs — для фінальної physical correction."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Foot IK Example", "Приклад Foot IK"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Humanoid", "Humanoid"), HonamiDocLocalization.Get("Two leg entries: thigh/calf/foot/toes for left and right legs. Root bone is pelvis.", "Два leg entries: thigh/calf/foot/toes для left та right legs. Root bone — pelvis.")),
                (HonamiDocLocalization.Get("Quadruped", "Quadruped"), HonamiDocLocalization.Get("Four leg entries. Root bone can be chest, pelvis or central body depending on the skeleton.", "Чотири leg entries. Root bone може бути chest, pelvis або central body залежно від skeleton.")),
                (HonamiDocLocalization.Get("Mech", "Mech"), HonamiDocLocalization.Get("Use larger raycastRadius and lower ankleRotationSpeed for heavy, mechanical feet.", "Використовуйте більший raycastRadius і нижчий ankleRotationSpeed для важких mechanical feet.")),
                (HonamiDocLocalization.Get("Crawler", "Crawler"), HonamiDocLocalization.Get("Use many entries and reduce weightMultiplier on cosmetic legs.", "Використовуйте багато entries і зменшуйте weightMultiplier на cosmetic legs."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("LookAt Example", "Приклад LookAt"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "For a creature with a long neck, set Target Mode to Multiple and add spine, neck and head bones as targets. Give the lower spine small axis weights and the head full weight, then enable maxAngle so the creature never twists impossibly.",
                "Для істоти з довгою шиєю встановіть Target Mode у Multiple і додайте spine, neck та head bones як targets. Нижньому spine дайте малі axis weights, head — повну вагу, потім увімкніть maxAngle, щоб істота не скручувалась неможливо."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Constraint Examples", "Приклади констрейнів"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Point Constraint: keep a weapon muzzle, armor plate or detached body part locked to an animated socket.", "Point Constraint: тримайте weapon muzzle, armor plate або detached body part прив'язаними до animated socket."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Pivot Fixer: correct a sword, hinge, shield or tool when its imported pivot is wrong for gameplay.", "Pivot Fixer: виправляйте sword, hinge, shield або tool, коли imported pivot неправильний для gameplay."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Pseudo-Physics: add follow-through to hair, tails, cloth strips, antennae, cables or soft armor.", "Pseudo-Physics: додавайте follow-through для hair, tails, cloth strips, antennae, cables або soft armor."));

            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "Do not solve everything with IK. Let authored animation do the big readable motion, then use rigs to correct contacts, anchors, aiming and secondary motion.",
                    "Не вирішуйте все через IK. Нехай authored animation робить великий читабельний рух, а rigs виправляють contacts, anchors, aiming та secondary motion."
                ),
                HonamiDocumentationBuilder.CalloutType.Tip);
        }
    }
}
