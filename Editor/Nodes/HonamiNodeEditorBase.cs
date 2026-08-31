#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HonamiNodeEditorAttribute : Attribute
    {
        public readonly Type NodeType;
        public readonly string MenuPath;
        public readonly int MenuOrder;

        public HonamiNodeEditorAttribute(Type nodeType, string menuPath, int menuOrder = 100)
        {
            NodeType = nodeType;
            MenuPath = menuPath;
            MenuOrder = menuOrder;
        }
    }

    public abstract class HonamiNodeEditorBase
    {
        public abstract string NodeCssClass { get; }
        public abstract string TopBarCssClass { get; }
        public abstract string AvatarCssClass { get; }
        public abstract string IconCssClass { get; }
        public abstract string Tooltip { get; }

        public virtual bool HasInput => true;
        public virtual bool HasOutput => true;

        public virtual string GetTitle(HonamiState state) => state.stateName;
        public virtual string GetSubtitle(HonamiState state) => GetType()
            .GetCustomAttributes(typeof(HonamiNodeEditorAttribute), false) is { Length: > 0 } attrs
            ? ((HonamiNodeEditorAttribute)attrs[0]).MenuPath.Split('/')[^1]
            : state.node?.GetType().Name ?? "Unknown";

        public virtual VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
            => null;

        public virtual float GetTimelineDuration(TimelineState state) => 5f;

        public virtual void DrawTimelineTracks(TimelineState state, ITimelineTrackDrawer drawer, ref float y) { }

        public virtual void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT) { }

        public virtual void AppendActionButtons(VisualElement root, HonamiState state, HonamiController controller) { }

        public virtual void OnDoubleClick(HonamiState state, HonamiController controller) { }

        protected static Label SmallLabel(string text) =>
            HonamiGraphStyles.MiniLabel(text, new Color(0.7f, 0.8f, 1f));

        protected static void AddParameterPopup(VisualElement container, SerializedObject nodeSO, HonamiController controller, string propName, string label, HonamiParameterType supportedType = HonamiParameterType.Float)
        {
            SerializedProperty prop = nodeSO.FindProperty(propName);
            if (prop == null) return;

            var paramNames = controller.parameters
                .Where(p => p.type == supportedType)
                .Select(p => p.name)
                .ToList();

            if (!paramNames.Contains(prop.stringValue) && !string.IsNullOrEmpty(prop.stringValue))
                paramNames.Insert(0, prop.stringValue);

            const string noneLabel = "- None -";
            paramNames.Insert(0, noneLabel);

            int currentIndex = string.IsNullOrEmpty(prop.stringValue) ? 0 : paramNames.IndexOf(prop.stringValue);
            if (currentIndex < 0) currentIndex = 0;

            var dropdown = new DropdownField(label, paramNames, currentIndex);
            dropdown.style.marginTop = dropdown.style.marginBottom = 3;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                prop.stringValue = evt.newValue == noneLabel ? "" : evt.newValue;
                prop.serializedObject.ApplyModifiedProperties();
            });

            container.Add(dropdown);
            container.RegisterCallback<AttachToPanelEvent>(_ => dropdown.Bind(nodeSO));
        }

        protected static void AddNodeField(VisualElement container, SerializedObject nodeSO, string propName, string label)
        {
            SerializedProperty prop = nodeSO.FindProperty(propName);
            if (prop == null) return;
            PropertyField field = new PropertyField(prop, label);
            field.style.marginTop = field.style.marginBottom = 3;
            container.Add(field);
            container.RegisterCallback<AttachToPanelEvent>(_ => field.Bind(nodeSO));
        }

        protected static void AddNodeListField(VisualElement container, SerializedObject nodeSO, string propName, string label, Action<SerializedProperty> onAddElement)
        {
            SerializedProperty prop = nodeSO.FindProperty(propName);
            if (prop == null) return;

            PropertyField field = new PropertyField(prop, label);
            field.style.marginTop = field.style.marginBottom = 3;

            int lastSize = prop.arraySize;
            field.TrackPropertyValue(prop, p =>
            {
                if (p.arraySize > lastSize)
                {
                    for (int i = lastSize; i < p.arraySize; i++)
                    {
                        SerializedProperty element = p.GetArrayElementAtIndex(i);
                        onAddElement?.Invoke(element);
                    }
                    p.serializedObject.ApplyModifiedProperties();
                }
                lastSize = p.arraySize;
            });

            container.Add(field);
            container.RegisterCallback<AttachToPanelEvent>(_ => field.Bind(nodeSO));
        }

        private static HonamiState _lastStateFieldTarget;
        private static SerializedObject _lastStateFieldSO;

        protected static void AddStateField(VisualElement container, HonamiState state, string propName, string label)
        {
            SerializedObject so;
            if (_lastStateFieldTarget == state && _lastStateFieldSO != null)
            {
                so = _lastStateFieldSO;
            }
            else
            {
                so = new SerializedObject(state);
                _lastStateFieldTarget = state;
                _lastStateFieldSO = so;
            }
            so.Update();
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null) return;
            PropertyField field = new PropertyField(prop, label);
            field.style.marginTop = field.style.marginBottom = 3;
            container.Add(field);
            container.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                so.Update();
                field.Bind(so);
            });
        }

        protected static Button TimelineButton(HonamiState state, HonamiController ctrl)
        {
            Button btn = HonamiGraphStyles.TallButton("  Open Timeline Editor", HonamiEditorIcons.Timeline);
            btn.style.backgroundColor = HonamiGraphStyles.Accent;
            btn.clicked += () => { HonamiTimelineWindow.OpenWindow(); HonamiTimelineWindow.InspectState(ctrl, state.guid); };
            return btn;
        }

        protected static Button BlendTreeButton(HonamiState state, HonamiController ctrl)
        {
            Button btn = HonamiGraphStyles.TallButton("  Open Blend Tree Editor", HonamiEditorIcons.BlendTree);
            btn.style.backgroundColor = HonamiGraphStyles.Accent;
            btn.clicked += () => { HonamiBlendTreeWindow.OpenWindow(); HonamiBlendTreeWindow.InspectState(ctrl, state.guid); };
            return btn;
        }
    }
}
#endif
