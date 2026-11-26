using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class AvatarsAndMasksPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Avatars & Masks", "Аватари та маски");
        public string Category => HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти");
        public string SearchKeywords => "avatar mask skeleton rigging remap bones non humanoid arbitrary creature avatar masks аватари маски скелет не гуманоїд";
        public int Order => 250;
        public int EstimatedReadTime => 7;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("Why Honami has its own Avatar and Mask assets", "Навіщо Honami власні ассети Avatar і Mask"),
                    HonamiDocLocalization.Get("Creating an avatar: bone lists, mirror pairs, skeleton remapping", "Створення аватара: списки кісток, mirror-пари, ремапінг скелета"),
                    HonamiDocLocalization.Get("Building per-bone weighted masks for any hierarchy", "Створення масок із вагою на кістку для будь-якої ієрархії")
                },
                new[]
                {
                    HonamiDocLocalization.Get("Blending & Layers: where masks are applied", "«Blending & Layers»: де маски застосовуються")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Honami uses its own Avatar and Mask assets because Unity's Humanoid Avatar workflow is a poor fit for a game full of strange enemy shapes. Honami does not need a skeleton to pass as a human before it can be blended, masked or mirrored.",
                "Honami використовує власні Avatar та Mask assets, бо Unity Humanoid Avatar workflow погано підходить для гри з великою кількістю дивних форм ворогів. Honami не вимагає, щоб скелет пройшов як людина, перш ніж його можна буде змішувати, маскувати або віддзеркалювати."
            ));

            HonamiDocumentationBuilder.AddCallout(root,
                HonamiDocLocalization.Get(
                    "The rule is simple: if it is a Transform hierarchy, Honami can treat it as an animation skeleton. It can be humanoid, quadruped, insect, tentacle, weapon, modular armor or a split character body.",
                    "Правило просте: якщо це Transform hierarchy, Honami може ставитися до неї як до animation skeleton. Це може бути humanoid, quadruped, insect, tentacle, weapon, modular armor або split character body."
                ),
                HonamiDocumentationBuilder.CalloutType.Tip);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The Humanoid Tax", "Податок Humanoid"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Unity workflow", "Unity workflow"), 190),
                (HonamiDocLocalization.Get("Honami workflow", "Honami workflow"), 0),
                ("", 0),
                (HonamiDocLocalization.Get("Configure a valid Humanoid Avatar before advanced blending feels usable.", "Налаштувати valid Humanoid Avatar, перш ніж advanced blending стане зручним."), HonamiDocLocalization.Get("List the bones you care about by path. No muscle mapping required.", "Вкажіть потрібні кістки за path. Muscle mapping не потрібен."), ""),
                (HonamiDocLocalization.Get("Avatar Masks are built around humanoid body sections and imported clips.", "Avatar Masks побудовані навколо humanoid body sections та imported clips."), HonamiDocLocalization.Get("Honami masks are weighted per bone path and work on any hierarchy.", "Honami masks мають вагу на bone path і працюють на будь-якій hierarchy."), ""),
                (HonamiDocLocalization.Get("Retargeting can distort non-human movement.", "Retargeting може ламати non-human movement."), HonamiDocLocalization.Get("Animation stays authored for the actual creature skeleton.", "Анімація лишається створеною під реальний скелет істоти."), ""),
                (HonamiDocLocalization.Get("Mirroring usually means extra clips or fragile setup.", "Mirroring часто означає extra clips або крихке налаштування."), HonamiDocLocalization.Get("Mirror pairs live in HonamiAvatar and can be driven globally or per rule.", "Mirror pairs живуть у HonamiAvatar і можуть керуватися глобально або через rule."), "")
            );

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Avoiding the Humanoid tax does not mean losing Humanoid content. Marketplace Humanoid clips can be retargeted onto your skeleton and baked to Generic with the Humanoid Baker tool (see the Tools & Pro section) - the baked clips then work with Honami avatars, masks and mirroring like any hand-authored clip.",
                "Уникати податку Humanoid не означає втрачати Humanoid-контент. Куплені Humanoid-кліпи можна ретаргетнути на ваш скелет і забейкати в Generic тулзою Humanoid Baker (див. розділ Tools & Pro) - запечені кліпи працюють з Honami-аватарами, масками та mirroring як будь-який кліп, зроблений вручну."
            ), HonamiDocumentationBuilder.CalloutType.Tip);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Honami Avatar Asset", "Ассет Honami Avatar"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The HonamiAvatar asset stores bone names and bone paths for the skeleton pieces that Honami should process. It can also define mirror pairs: left/right bones or any custom pair that should swap during mirroring.",
                "HonamiAvatar asset зберігає bone names та bone paths для частин скелета, які Honami має обробляти. Він також може визначати mirror pairs: left/right bones або будь-яку кастомну пару, яка має мінятися під час mirroring."
            ));

            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Bones", "Bones"), HonamiDocLocalization.Get("Each entry stores boneName, bonePath and enabled. Disabled entries are ignored.", "Кожен entry зберігає boneName, bonePath та enabled. Disabled entries ігноруються.")),
                (HonamiDocLocalization.Get("Mirror Bones", "Mirror Bones"), HonamiDocLocalization.Get("Pairs of bone paths used by the mirror job. They do not have to be humanoid limbs.", "Пари bone paths для mirror job. Це не обов'язково humanoid limbs.")),
                (HonamiDocLocalization.Get("Bone Replacer", "Bone Replacer"), HonamiDocLocalization.Get("Honami can redirect animation output to replacement bones when a character is assembled from modular parts.", "Honami може перенаправляти animation output у replacement bones, коли персонаж зібраний із modular parts.")),
                (HonamiDocLocalization.Get("Missing bones", "Missing bones"), HonamiDocLocalization.Get("The processor can create hidden proxy transforms for paths that exist in the avatar data but not in the current hierarchy.", "Processor може створювати hidden proxy transforms для paths, які є в avatar data, але відсутні в поточній hierarchy."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Creating an Avatar from a Model", "Створення аватара з моделі"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Select an imported model (FBX, OBJ, glTF...) in the Project window and the import inspector shows a Honami Avatar row right above Unity's Model / Rig / Animation / Materials tabs. Its Create button writes <Model>_HonamiAvatar.asset next to the model, fills it with every bone path and auto-binds left/right mirror pairs. It is the Honami counterpart of picking Humanoid in the Rig tab, minus the requirement that the skeleton look human. Once an avatar is linked the row becomes a foldout holding the options below.",
                "Виберіть імпортовану модель (FBX, OBJ, glTF...) у Project window - в інспекторі імпорту з'явиться рядок Honami Avatar одразу над вкладками Unity Model / Rig / Animation / Materials. Кнопка Create створює <Model>_HonamiAvatar.asset поряд із моделлю, заповнює його всіма bone paths і автоматично зв'язує left/right mirror pairs. Це аналог вибору Humanoid у вкладці Rig, але без вимоги, щоб скелет був схожий на людський. Коли аватар прив'язаний, рядок стає фолдаутом з опціями нижче."
            ));

            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Refresh On Reimport", "Refresh On Reimport"), HonamiDocLocalization.Get("Rebuilds the bone list every time the model is imported again. Enabled flags and mirror pairs of surviving bones are kept, bones the model no longer has are dropped.", "Перебудовує список кісток після кожного повторного імпорту моделі. Enabled-прапорці та mirror pairs наявних кісток зберігаються, кістки, яких у моделі більше немає, видаляються.")),
                (HonamiDocLocalization.Get("Skinned Bones Only", "Skinned Bones Only"), HonamiDocLocalization.Get("Keeps only the bones referenced by the model's SkinnedMeshRenderers and their parents, so mesh and helper nodes stay out of the avatar.", "Залишає лише кістки, на які посилаються SkinnedMeshRenderers моделі, та їхніх батьків, тож mesh і helper nodes не потрапляють в аватар.")),
                (HonamiDocLocalization.Get("Include Inactive", "Include Inactive"), HonamiDocLocalization.Get("Includes transforms whose GameObject is inactive in the imported model.", "Включає transforms, чий GameObject неактивний в імпортованій моделі.")),
                (HonamiDocLocalization.Get("Auto Bind Mirrors", "Auto Bind Mirrors"), HonamiDocLocalization.Get("Pairs bones by the usual naming conventions (_L/_R, .l/.r, Left/Right, L_/R_) after every rebuild.", "Зв'язує кістки за звичними конвенціями іменування (_L/_R, .l/.r, Left/Right, L_/R_) після кожної перебудови."))
            );

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("The same avatar can be built from the Project window: right-click the model and choose Create Honami Avatar From Model.", "Той самий аватар можна створити з Project window: правий клік на моделі та Create Honami Avatar From Model."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("An avatar authored by hand can be attached to a model later by dropping it into the object field of that row.", "Аватар, зроблений вручну, можна прив'язати до моделі пізніше, перетягнувши його в поле об'єкта цього рядка."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("The foldout warns when the model and the avatar drift apart, and Rebuild From Model resyncs them without losing per-bone settings.", "Фолдаут попереджає, коли модель і аватар розходяться, а Rebuild From Model синхронізує їх, не втрачаючи налаштувань окремих кісток."));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Unity's importer tabs are built by internal classes, so no package can add a tab of its own. The row is drawn through the public inspector-header hook and sits above the tabs instead of inside them. Turn it off in Preferences > Honami > Import if you do not want it on every model.",
                "Вкладки імпортера Unity будують internal-класи, тому жоден пакет не може додати власну вкладку. Рядок малюється через публічний inspector-header hook і стоїть над вкладками, а не всередині них. Вимкнути його можна в Preferences > Honami > Import, якщо він не потрібен на кожній моделі."
            ), HonamiDocumentationBuilder.CalloutType.Info);

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Runtime Masking", "Маскування в рантаймі"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "A HonamiAvatarMask is a list of weighted bone paths. Layer masks, state masks and Mask Switcher rules all feed into Honami's internal mask atlas, so the runtime blends exactly the bones you choose.",
                "HonamiAvatarMask - це список weighted bone paths. Layer masks, state masks та Mask Switcher rules потрапляють у внутрішній mask atlas Honami, тому runtime змішує саме ті кістки, які ви вибрали."
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Weight 1 means the animated layer fully affects that bone.", "Weight 1 означає, що animated layer повністю впливає на цю кістку."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Weight 0 blocks the bone and keeps the previous/base pose.", "Weight 0 блокує кістку й залишає previous/base pose."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Intermediate weights are valid, so masks can softly blend regions instead of hard cutting them.", "Проміжні weights валідні, тому masks можуть м'яко змішувати regions замість жорсткого cut."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("excludeUnlisted=true behaves like a strict mask: bones not listed are blocked.", "excludeUnlisted=true працює як strict mask: bones, яких немає в списку, блокуються."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("excludeUnlisted=false lets unlisted bones pass through at full weight.", "excludeUnlisted=false пропускає unlisted bones із повною вагою."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Mask Examples", "Приклади масок"), HonamiEditorIcons.BlendTreeWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Mask", "Маска"), 150),
                (HonamiDocLocalization.Get("Skeleton type", "Тип скелета"), 160),
                (HonamiDocLocalization.Get("Use", "Використання"), 0),
                ("Upper Body", "Humanoid / mutant", HonamiDocLocalization.Get("Reload or attack while locomotion keeps legs moving.", "Перезарядка або атака, поки locomotion рухає ногами.")),
                ("Front Legs", "Quadruped", HonamiDocLocalization.Get("Stagger, claw, pounce or terrain correction only on front supports.", "Stagger, claw, pounce або terrain correction лише на front supports.")),
                ("Tail Only", "Creature", HonamiDocLocalization.Get("Additive tail attack, aim correction or pseudo-physics without touching body motion.", "Additive tail attack, aim correction або pseudo-physics без впливу на body motion.")),
                ("Turret Head", "Drone / mech", HonamiDocLocalization.Get("Aim a rotating weapon/head assembly separately from the chassis.", "Aim rotating weapon/head assembly окремо від chassis.")),
                ("Tentacle Segment", "Boss", HonamiDocLocalization.Get("Blend damage, grab or idle motion on one tentacle chain.", "Змішування damage, grab або idle motion на одному tentacle chain."))
            );

            HonamiDocumentationBuilder.AddCodeBlock(root,
@"// Example: modify an existing Honami mask at runtime.
// The mask should already be registered or used by a layer/state/sub-node.
mask.boneWeights[0].weight = 0f;
mask.InvalidateCache();
honami.RebakeAvatarMask(mask);

// Rebuild all mask weights if several masks changed.
honami.RebakeAllAvatarMasks();");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "For creature-heavy projects, build avatars around gameplay regions, not anatomy names Unity expects: FrontLegs, BackLegs, Tail, HeadCluster, ArmorShell, WeaponRig, Tentacles.",
                "Для проєктів із великою кількістю істот будуйте avatars навколо gameplay regions, а не anatomy names, яких очікує Unity: FrontLegs, BackLegs, Tail, HeadCluster, ArmorShell, WeaponRig, Tentacles."
            ));
        }
    }
}
