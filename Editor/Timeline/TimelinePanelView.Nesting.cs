using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private readonly struct ParentChoice
        {
            public readonly string Label;
            public readonly int Motion;
            public readonly HonamiTimelineModule Module;
            public readonly HonamiTimelineItem Item;

            public ParentChoice(string label, int motion, HonamiTimelineModule module, HonamiTimelineItem item)
            {
                Label = label;
                Motion = motion;
                Module = module;
                Item = item;
            }

            public bool IsRoot => Motion < 0 && Item == null;
        }

        private ParentChoice RootChoice => new(_state.Mode == TimelineMode.HonamiState ? "Whole State" : "Timeline", -1, null, null);

        private ParentChoice MotionChoice(int motion) => new($"Clip {motion}: {_state.node.GetMotionName(motion)}", motion, null, null);

        private ParentChoice ItemChoice(HonamiTimelineModule module, HonamiTimelineItem item)
        {
            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
            string moduleName = string.IsNullOrEmpty(module.moduleName) ? editor.DisplayName : module.moduleName;
            float start = _state.GetModuleTimeOffset(module) + item.time;
            return new ParentChoice($"{moduleName}: {editor.GetItemLabel(item)} @ {start:F2}s", -1, module, item);
        }

        private bool Accepts(ParentChoice choice, Type moduleType)
        {
            if (choice.IsRoot) return true;
            if (choice.Item != null) return choice.Module.CanHostModule(choice.Item, moduleType);
            return _state.node != null && _state.node.CanHostModule(choice.Motion, moduleType);
        }

        // A module can never move under one of its own items or descendants.
        private List<ParentChoice> CollectParentChoices(HonamiTimelineModule module)
        {
            var choices = new List<ParentChoice> { RootChoice };
            var node = _state.Mode == TimelineMode.HonamiState ? _state.node : null;
            var moduleType = module.GetType();

            if (node != null)
            {
                for (int i = 0; i < node.MotionCount; i++)
                {
                    if (node.CanHostModule(i, moduleType))
                        choices.Add(MotionChoice(i));
                }
            }

            var modules = _state.ActiveModules;
            foreach (var candidate in modules)
            {
                if (candidate == null || HonamiTimelineModuleParent.IsWithin(modules, node, candidate, module)) continue;
                int count = candidate.ItemCount;
                for (int i = 0; i < count; i++)
                {
                    var item = candidate.GetItem(i);
                    if (candidate.CanHostModule(item, moduleType))
                        choices.Add(ItemChoice(candidate, item));
                }
            }
            return choices;
        }

        private bool IsCurrentParent(HonamiTimelineModule module, ParentChoice choice)
        {
            var parent = _state.ResolveParent(module);
            if (choice.IsRoot) return parent.Kind == HonamiTimelineModuleParentKind.None;
            if (choice.Item != null) return parent.Kind == HonamiTimelineModuleParentKind.Item && parent.Item == choice.Item;
            return parent.Kind == HonamiTimelineModuleParentKind.Motion && parent.MotionIndex == choice.Motion;
        }

        private ParentChoice? CurrentParentChoice(HonamiTimelineModule module)
        {
            var parent = _state.ResolveParent(module);
            return parent.Kind switch
            {
                HonamiTimelineModuleParentKind.Item => ItemChoice(_state.ActiveModules[parent.ModuleIndex], parent.Item),
                HonamiTimelineModuleParentKind.Motion => MotionChoice(parent.MotionIndex),
                _ => null
            };
        }

        // Ids are assigned before ModifyModules so an override's effective copy of the state inherits them.
        private string EnsureParentId(ParentChoice choice)
        {
            if (choice.IsRoot) return null;
            var container = _state.ActiveContainer;
            return choice.Item != null
                ? HonamiEditorController.EnsureItemId(container, choice.Item)
                : HonamiEditorController.EnsureMotionId(container, choice.Motion);
        }

        private void AddModuleTypeItems(GenericMenu menu, string prefix, ParentChoice? parent = null)
        {
            var container = _state.ActiveContainer;
            if (container == null) return;

            bool any = false;
            foreach (var moduleType in HonamiTimelineModuleEditorRegistry.ModuleTypesFor(container.Host))
            {
                if (parent.HasValue && !Accepts(parent.Value, moduleType)) continue;

                any = true;
                var editor = HonamiTimelineModuleEditorRegistry.GetEditor(moduleType);
                menu.AddItem(new GUIContent(prefix + editor.MenuPath), false, () =>
                {
                    string parentId = parent.HasValue ? EnsureParentId(parent.Value) : null;
                    if (parent.HasValue && !parent.Value.IsRoot && parentId == null) return;

                    HonamiTimelineModule added = null;
                    ModifyModules(target => added = HonamiEditorController.AddModule(target, moduleType, parentId));
                    _state.SelectedModule = added;
                    _state.SelectedTimelineItems.Clear();
                    _rebuild();
                });
            }

            if (!any)
                menu.AddDisabledItem(new GUIContent(prefix + "No module types available"));
        }

        private void ShowNestedModuleMenu(ParentChoice parent, Rect? dropDownAt = null)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent($"Runs inside {parent.Label}"));
            menu.AddSeparator("");
            AddModuleTypeItems(menu, string.Empty, parent);
            if (dropDownAt.HasValue) menu.DropDown(dropDownAt.Value);
            else menu.ShowAsContext();
        }

        private void AddParentItems(GenericMenu menu, string prefix, HonamiTimelineModule module)
        {
            var choices = CollectParentChoices(module);
            if (choices.Count <= 1 && !module.HasParent) return;

            foreach (var choice in choices)
            {
                var captured = choice;
                menu.AddItem(new GUIContent(prefix + choice.Label.Replace('/', '∕')), IsCurrentParent(module, choice), () => SetModuleParent(module, captured));
            }
        }

        private void SetModuleParent(HonamiTimelineModule module, ParentChoice choice)
        {
            string parentId = EnsureParentId(choice);
            if (!choice.IsRoot && parentId == null) return;

            ModifyModules(target => HonamiEditorController.SetModuleParent(target, module, parentId));
            _rebuild();
        }

        private bool CanHostAnyModule(HonamiTimelineModule module, HonamiTimelineItem item)
        {
            foreach (var moduleType in HonamiTimelineModuleEditorRegistry.ModuleTypesFor(_state.ActiveContainer.Host))
            {
                if (module.CanHostModule(item, moduleType)) return true;
            }
            return false;
        }

        private List<HonamiTimelineItem> HostItemsOf(HonamiTimelineModule module)
        {
            var items = new List<HonamiTimelineItem>();
            int count = module.ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = module.GetItem(i);
                if (item != null && CanHostAnyModule(module, item))
                    items.Add(item);
            }
            return items;
        }

        private void ShowModuleNestMenu(HonamiTimelineModule module, Rect dropDownAt)
        {
            var items = HostItemsOf(module);
            if (items.Count == 1)
            {
                ShowNestedModuleMenu(ItemChoice(module, items[0]), dropDownAt);
                return;
            }

            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
            float offset = _state.GetModuleTimeOffset(module);
            var menu = new GenericMenu();
            for (int i = 0; i < items.Count; i++)
            {
                string itemLabel = $"{i + 1}. {editor.GetItemLabel(items[i])} @ {offset + items[i].time:F2}s".Replace('/', '∕');
                AddModuleTypeItems(menu, itemLabel + "/", ItemChoice(module, items[i]));
            }
            menu.DropDown(dropDownAt);
        }

        private HonamiTimelineItem HostItemAt(HonamiTimelineModule module, Vector2 canvasPosition)
        {
            if (module == null) return null;
            foreach (var pair in _state.TimelineItemRects)
            {
                if (pair.Value.Contains(canvasPosition) && module.Contains(pair.Key))
                    return pair.Key;
            }
            return null;
        }
    }
}
