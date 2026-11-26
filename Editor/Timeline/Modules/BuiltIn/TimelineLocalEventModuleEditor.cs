using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineLocalEventModule), "Events/Local Events", 10)]
    public sealed class TimelineLocalEventModuleEditor : HonamiTimelineModuleEditor
    {
        public override Color ModuleColor => TimelineTheme.EventTrack;
        public override Texture Icon => EditorGUIUtility.IconContent("AnimationWindowEvent Icon").image;
        public override string ItemDisplayName => "Local Event";
        public override string GetItemLabel(HonamiTimelineItem item) => ((TimelineLocalEventItem)item).eventName;

        public override void BuildModuleInspector(HonamiTimelineEditContext context, SerializedObject moduleSo, VisualElement root) =>
            root.Add(new HelpBox(
                "Each event calls TriggerEvent on a HonamiLocalEventReceiver. In a state that is the receiver on the animator's GameObject unless the Binding Key points elsewhere.",
                HelpBoxMessageType.None));
    }
}
