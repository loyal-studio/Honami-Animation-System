using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class HonamiTimelineModuleEditorAttribute : Attribute
    {
        public readonly Type ModuleType;
        public readonly string MenuPath;
        public readonly int MenuOrder;

        public HonamiTimelineModuleEditorAttribute(Type moduleType, string menuPath = null, int menuOrder = 100)
        {
            ModuleType = moduleType;
            MenuPath = menuPath;
            MenuOrder = menuOrder;
        }
    }

    public class HonamiTimelineModuleEditor
    {
        private static readonly HashSet<string> BaseModuleFields = new() { "m_Script", "moduleName", "muted", "locked", "items", "parentId", "bindingKey" };
        private static readonly Color DefaultModuleColor = new(0.46f, 0.49f, 0.58f, 0.96f);

        public Type ModuleType { get; internal set; }
        public Type ItemType { get; internal set; }
        public HonamiTimelineModuleHosts SupportedHosts { get; internal set; }
        public string MenuPath { get; internal set; }
        public int MenuOrder { get; internal set; }

        public virtual string DisplayName
        {
            get
            {
                int slash = MenuPath.LastIndexOf('/');
                return slash >= 0 ? MenuPath.Substring(slash + 1) : MenuPath;
            }
        }

        public virtual string ItemDisplayName => ItemType != null ? NicifyTypeName(ItemType.Name, "Item") : "Item";

        public virtual Color ModuleColor => DefaultModuleColor;
        public virtual Texture Icon => EditorGUIUtility.IconContent("cs Script Icon").image;

        // True when overlapping items mix additively (audio), so the earlier item fades out across the overlap too.
        public virtual bool FadesOutUnderOverlap => false;

        public bool SupportsHost(HonamiTimelineModuleHost host) =>
            (SupportedHosts & (host == HonamiTimelineModuleHost.State ? HonamiTimelineModuleHosts.State : HonamiTimelineModuleHosts.Timeline)) != 0;

        public virtual HonamiTimelineModule CreateModule()
        {
            var module = (HonamiTimelineModule)ScriptableObject.CreateInstance(ModuleType);
            module.moduleName = DisplayName;
            module.name = DisplayName;
            return module;
        }

        public virtual HonamiTimelineItem CreateItem(HonamiTimelineEditContext context, float time) => context.Module.CreateItem(time);

        public virtual string GetItemLabel(HonamiTimelineItem item) => ItemDisplayName;

        public virtual string GetItemLabel(HonamiTimelineEditContext context, HonamiTimelineItem item) => GetItemLabel(item);
        public virtual Color GetItemColor(HonamiTimelineItem item) => ModuleColor;
        public virtual Texture GetItemIcon(HonamiTimelineItem item) => Icon;

        public virtual void DecorateItem(HonamiTimelineItemView view)
        {
            view.AddTrimHandles();
            view.AddBlendHandles(FadesOutUnderOverlap);
        }

        public virtual void OnItemDoubleClicked(HonamiTimelineEditContext context, HonamiTimelineItem item, float time) { }
        public virtual void AddContextMenuItems(HonamiTimelineEditContext context, GenericMenu menu, float time) { }

        public virtual bool AcceptsDrop(Object dropped) => false;
        public virtual HonamiTimelineItem CreateItemFromDrop(HonamiTimelineEditContext context, Object dropped, float time) => null;

        public virtual bool CanSplit(HonamiTimelineItem item) => item != null && !item.IsInstant && item.CanResize;

        // Cuts the item at a module-local time: the item keeps the head and the returned tail is added by the caller.
        public virtual HonamiTimelineItem SplitItem(HonamiTimelineEditContext context, HonamiTimelineItem item, float time)
        {
            if (!CanSplit(item) || time <= item.time + HonamiTimelineItem.MinDuration || time >= item.EndTime - HonamiTimelineItem.MinDuration)
                return null;

            var tail = CloneItem(item);
            tail.TrimStart(time);
            item.SetDuration(time - item.time);

            if (item is IHonamiTimelineBlendItem head) head.BlendOut = 0f;
            if (tail is IHonamiTimelineBlendItem tailBlend) tailBlend.BlendIn = 0f;
            return tail;
        }

        // Called to build the module-level inspector. Override to customise the module panel.
        // moduleSo is the SerializedObject for the module ScriptableObject.
        // The base implementation renders all serialized fields that are not part of the base module
        // (name, muted, locked, items, parentId, bindingKey) - those are always shown by the panel itself.
        public virtual void BuildModuleInspector(HonamiTimelineEditContext context, SerializedObject moduleSo, VisualElement root) =>
            AddModuleFields(moduleSo, root, BaseModuleFields);

        // Called to build the item-level inspector. itemProperty is the SerializedProperty for the item
        // inside the module's "items" array (depth >= 0, not a root iterator).
        public virtual void BuildItemInspector(HonamiTimelineEditContext context, SerializedProperty itemProperty, VisualElement root) =>
            AddChildFields(itemProperty, root, null);

        public virtual void Preview(HonamiTimelinePreviewContext context) { }

        public virtual void StopPreview() { }

        public static HonamiTimelineItem CloneItem(HonamiTimelineItem item)
        {
            var clone = (HonamiTimelineItem)Activator.CreateInstance(item.GetType());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(item), clone);
            clone.ClearId();
            // A copy is a new item: it must not drag along with the group the original belongs to.
            clone.ClearLinkGroup();
            return clone;
        }

        // Renders all top-level visible fields from a SerializedObject, skipping any listed in `skip`.
        // Use this from BuildModuleInspector overrides to render the full module SO.
        protected static void AddModuleFields(SerializedObject so, VisualElement root, ICollection<string> skip)
        {
            var iterator = so.GetIterator();
            if (!iterator.NextVisible(true))
                return;

            // Note: do NOT call GetEndProperty() here - after NextVisible(true), the iterator
            // is at depth 0 (m_Script), so GetEndProperty() would return the next sibling
            // (moduleName), causing the loop to exit immediately after the first field.
            // NextVisible(false) returning false is the correct termination condition.
            do
            {
                if (skip == null || !skip.Contains(iterator.name))
                    root.Add(CreateField(iterator));
            }
            while (iterator.NextVisible(false));
        }

        // Renders visible children of a given SerializedProperty, skipping any listed in `skip`.
        // Use this from BuildItemInspector overrides to render item fields.
        protected static void AddChildFields(SerializedProperty parent, VisualElement root, ICollection<string> skip)
        {
            var iterator = parent.Copy();
            var end = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (skip == null || !skip.Contains(iterator.name))
                    root.Add(CreateField(iterator));
            }
        }

        protected static PropertyField CreateField(SerializedProperty property)
        {
            var field = new PropertyField(property.Copy());
            field.Bind(property.serializedObject);
            return field;
        }

        internal static string NicifyTypeName(string typeName, string suffix)
        {
            if (typeName.StartsWith("Honami", StringComparison.Ordinal) && typeName.Length > "Honami".Length)
                typeName = typeName.Substring("Honami".Length);
            if (typeName.EndsWith(suffix, StringComparison.Ordinal) && typeName.Length > suffix.Length)
                typeName = typeName.Substring(0, typeName.Length - suffix.Length);
            return ObjectNames.NicifyVariableName(typeName);
        }
    }
}
