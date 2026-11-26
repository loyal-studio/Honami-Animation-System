using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    public enum HonamiTimelineModuleParentKind
    {
        None,
        Item,
        Motion,
        Missing
    }

    public readonly struct HonamiTimelineModuleParent
    {
        public readonly HonamiTimelineModuleParentKind Kind;
        public readonly int ModuleIndex;
        public readonly HonamiTimelineItem Item;
        public readonly int MotionIndex;

        private HonamiTimelineModuleParent(HonamiTimelineModuleParentKind kind, int moduleIndex, HonamiTimelineItem item, int motionIndex)
        {
            Kind = kind;
            ModuleIndex = moduleIndex;
            Item = item;
            MotionIndex = motionIndex;
        }

        public static readonly HonamiTimelineModuleParent None = new(HonamiTimelineModuleParentKind.None, -1, null, -1);
        public static readonly HonamiTimelineModuleParent Missing = new(HonamiTimelineModuleParentKind.Missing, -1, null, -1);

        public static HonamiTimelineModuleParent ForItem(int moduleIndex, HonamiTimelineItem item) =>
            new(HonamiTimelineModuleParentKind.Item, moduleIndex, item, -1);

        public static HonamiTimelineModuleParent ForMotion(int motionIndex) =>
            new(HonamiTimelineModuleParentKind.Motion, -1, null, motionIndex);

        public bool IsMissing => Kind == HonamiTimelineModuleParentKind.Missing;

        public static HonamiTimelineModuleParent Resolve(IReadOnlyList<HonamiTimelineModule> modules, HonamiNodeBase node, HonamiTimelineModule module)
        {
            if (module == null || !module.HasParent) return None;

            var moduleType = module.GetType();
            for (int i = 0; i < modules.Count; i++)
            {
                var candidate = modules[i];
                if (candidate == null || candidate == module) continue;

                var item = candidate.FindItem(module.parentId);
                if (item == null) continue;
                return candidate.CanHostModule(item, moduleType) ? ForItem(i, item) : Missing;
            }

            int motion = node != null ? node.FindMotion(module.parentId) : -1;
            if (motion >= 0 && node.CanHostModule(motion, moduleType))
                return ForMotion(motion);

            return Missing;
        }

        public static bool IsWithin(IReadOnlyList<HonamiTimelineModule> modules, HonamiNodeBase node, HonamiTimelineModule module, HonamiTimelineModule ancestor)
        {
            var current = module;
            for (int step = 0; step <= modules.Count && current != null; step++)
            {
                if (current == ancestor) return true;
                current = ParentModule(modules, node, current);
            }
            return false;
        }

        // A chain longer than the module list must revisit a module, so the module sits in or under a loop.
        public static bool HasCycle(IReadOnlyList<HonamiTimelineModule> modules, HonamiNodeBase node, HonamiTimelineModule module)
        {
            var current = module;
            for (int step = 0; step <= modules.Count; step++)
            {
                current = ParentModule(modules, node, current);
                if (current == null) return false;
            }
            return true;
        }

        private static HonamiTimelineModule ParentModule(IReadOnlyList<HonamiTimelineModule> modules, HonamiNodeBase node, HonamiTimelineModule module)
        {
            var parent = Resolve(modules, node, module);
            return parent.Kind == HonamiTimelineModuleParentKind.Item ? modules[parent.ModuleIndex] : null;
        }
    }
}
