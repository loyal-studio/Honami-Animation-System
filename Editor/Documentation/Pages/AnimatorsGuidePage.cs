using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class AnimatorsGuidePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Animator's Guide", "Посібник аніматора");
        public string Category => HonamiDocLocalization.Get("02. Getting Started", "02. Перші Кроки");
        public string SearchKeywords => "artist guide workflow export blender maya fbx tips аніматор посібник воркфлоу";
        public int Order => 110;
        public int EstimatedReadTime => 3;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "This is the definitive technical guide for animators. It explains NOT just what to do, but WHY things are done this way to avoid common pitfalls in Unity.",
                "Це ультимативний технічний посібник для аніматорів. Він пояснює НЕ лише те, що робити, а й ЧОМУ це робиться саме так, щоб уникнути типових проблем у Unity."
            ));

            // --- Section 1: Coordinates ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("1. The Coordinate Conflict", "1. Конфлікт систем координат"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Why do characters 'lie down' after export? It's a heritage issue: \n\n" +
                "• Blender (Z-Up): Born from architecture. Looking down at a floor (XY). Z is height.\n" +
                "• Unity (Y-Up): Born from screen-space CG. Looking at a screen (XY). Z is depth, Y is height.\n\n" +
                "Also, Blender is Right-Handed, Unity is Left-Handed. Without correction, your character will be rotated 90 degrees.",
                "Чому персонажі «лягають» після експорту? Це питання спадковості ПЗ:\n\n" +
                "• Blender (Z-Up): Походить з архітектури. Ми дивимося зверху на підлогу (XY). Z — це висота.\n" +
                "• Unity (Y-Up): Походить з комп'ютерної графіки. Ми дивимося на екран (XY). Z — глибина, Y — висота.\n\n" +
                "Крім того, Blender — правостороння система, Unity — лівостороння. Без корекції ваш персонаж буде повернутий на 90 градусів."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "THE SOLUTION (Bake Axis): In Blender FBX export, set Forward: -Z Forward and Up: Y Up. Check 'Apply Transform'. This 'bakes' the 90-degree fix so Unity doesn't have to guess.",
                "РІШЕННЯ (Bake Axis): В експорті FBX з Blender встановіть Forward: -Z Forward та Up: Y Up. Увімкніть «Apply Transform». Це «запікає» виправлення на 90 градусів, щоб Unity не доводилося вгадувати."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            // --- Section 2: Procedural vs Keyframed ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("2. Keyframes vs Procedural", "2. Ключові кадри vs Процедурність"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Stop baking everything! Modern workflows use procedural logic for secondary motion:",
                "Припиніть запікати абсолютно все! Сучасні робочі процеси використовують процедурну логіку для вторинних рухів:"
            ));

            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Task", "Завдання"), 140),
                (HonamiDocLocalization.Get("Keyframed (Bad)", "Ключі (Погано)"), 180),
                (HonamiDocLocalization.Get("Procedural (Good)", "Процедурно (Добре)"), 0),
                ("Screen Shake", HonamiDocLocalization.Get("Baked clip (loops, static)", "Запечений кліп (циклічний)"), HonamiDocLocalization.Get("Jitter Sub-Node (infinite, dynamic)", "Підвузол Jitter (нескінченний)")),
                ("Look At", HonamiDocLocalization.Get("Baked blendshapes (limited)", "Блендшейпи (обмежено)"), HonamiDocLocalization.Get("IK Solver (reactive, 360°)", "IK солвер (реактивний, 360°)")),
                ("Recoil/Hit", HonamiDocLocalization.Get("Library of 100 clips", "Бібліотека зі 100 кліпів"), HonamiDocLocalization.Get("Mathematical impulse", "Математичний імпульс"))
            );

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Use Keyframes for 'Biology' (body language, weight, acting). Use Procedural logic for 'Physics' (shaking, looking, reacting).",
                "Використовуйте ключі для «біології» (мова тіла, вага, акторська гра). Використовуйте процедурність для «фізики» (тряска, погляд, реакції)."
            ));

            // --- Section 3: Humanoid vs Generic ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("3. Humanoid vs Generic", "3. Humanoid чи Generic?"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Many developers default to 'Humanoid', but for high-end production, it's often a trap. Humanoid introduces a complex 'Retargeting' layer that converts rotations into 'Muscle Space'. This causes:",
                "Багато розробників обирають «Humanoid» за замовчуванням, але для серйозного продакшну це часто пастка. Humanoid додає складний шар ретаргетингу, який конвертує повороти в «м'язовий простір» (Muscle Space). Це спричиняє:"
            ));

            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Performance Loss: Unity spends CPU cycles re-calculating bone positions every frame to fit a virtual skeleton.",
                "Втрата продуктивності: Unity витрачає ресурси CPU на перерахунок позицій кісток кожного кадру, щоб підігнати їх під віртуальний скелет."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Precision Loss: Fine animation details (fingers, subtle weight shifts) can be 'averaged out' or distorted by the retargeter.",
                "Втрата точності: Дрібні деталі анімації (пальці, тонкі зміщення ваги) можуть бути «усереднені» або спотворені ретаргетером."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "The Rigidity Trap: Humanoid is a 'Biped-only' cage. You cannot use it for spiders, quadrupeds, or multi-armed creatures. It forces you into a rigid 15-bone minimum hierarchy.",
                "Пастка жорсткості: Humanoid — це клітка «тільки для двоногих». Ви не можете використати його для павуків, чотирилапих чи істот з багатьма руками. Він змушує вас дотримуватися жорсткої ієрархії мінімум з 15 кісток."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Honami's True Freedom: Honami is rig-agnostic. Whether it's a human, a dragon, or a complex vehicle, Honami treats them all with the same high-performance logic, allowing shared animations through virtual mapping without the 'Humanoid' restrictions.",
                "Справжня свобода Honami: Honami — ріг-агностик. Чи це людина, чи дракон, чи складна техніка — Honami обробляє їх усіх з однаковою високопродуктивною логікою, дозволяючи спільні анімації через віртуальний мапінг без обмежень «Humanoid»."
            ));

            HonamiDocumentationBuilder.AddFeatureGrid(root,
                (HonamiDocLocalization.Get("Honami Avatar", "Honami Avatar"),
                 HonamiDocLocalization.Get("True sharing for ANY rig type. Share animations between a human and a spider? Possible with custom mapping.", "Справжній обмін для БУДЬ-ЯКОГО типу рігу. Поділитися анімацією між людиною та павуком? Можливо з кастомним мапінгом."),
                 HonamiEditorIcons.Controller),
                (HonamiDocLocalization.Get("Honami Mask", "Honami Mask"),
                 HonamiDocLocalization.Get("Bitwise masking that doesn't care about 'Muscle Groups'. Mask a tail, a wing, or a sword bone with 100% precision.", "Бітове маскування, якому байдуже на «м'язові групи». Маскуйте хвіст, крило або кістку меча зі 100% точністю."),
                 HonamiEditorIcons.GraphWhite)
            );

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "In short: Humanoid is NOT flexible—it's restrictive. Honami + Generic is the only way to achieve true freedom for any character design, from humans to monsters, with zero performance compromises.",
                "Коротко: Humanoid НЕ гнучкий — він обмежує. Honami + Generic — це єдиний шлях до справжньої свободи дизайну персонажів, від людей до монстрів, без компромісів у продуктивності."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Humanoid still has one great use: as an IMPORT format. When you buy Humanoid animation packs (Asset Store, Mixamo), the Humanoid Baker tool (Window ▸ Honami ▸ Tools) retargets them onto your skeleton once in the editor and bakes them to Generic clips. You get the marketplace content without carrying the runtime retargeting tax.",
                "У Humanoid лишається одне чудове застосування: як ФОРМАТ ІМПОРТУ. Коли ви купуєте Humanoid-паки анімацій (Asset Store, Mixamo), тулза Humanoid Baker (Window ▸ Honami ▸ Tools) один раз ретаргетить їх на ваш скелет у редакторі та бейкає в Generic-кліпи. Ви отримуєте контент з маркетплейсу без вічної рантайм-плати за ретаргетинг."
            ));

            // --- Section 4: Technical Standards ---
            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("4. Technical Standards", "4. Технічні стандарти"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Scale: Always Apply Scale (Ctrl+A) in Blender. The final FBX must have a scale of 1,1,1.",
                "Масштаб: Завжди застосовуйте масштаб (Ctrl+A) у Blender. Фінальний FBX повинен мати масштаб 1,1,1."
            ));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get(
                "Root Motion: Ensure your root bone is at 0,0,0 and faces the Forward direction (Blue arrow in Unity).",
                "Root Motion: Переконайтеся, що коренева кістка знаходиться в 0,0,0 і дивиться вперед (синя стрілка в Unity)."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "A perfect FBX is one that you can drop into Unity and it 'just works' without any rotation or scale adjustments in the Inspector.",
                "Ідеальний FBX — це той, який ви кидаєте в Unity, і він «просто працює» без жодних коригувань повороту чи масштабу в інспекторі."
            ));
        }
    }
}
