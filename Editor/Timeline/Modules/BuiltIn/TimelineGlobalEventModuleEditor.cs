using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineGlobalEventModule), "Events/Global Events", 11)]
    public sealed class TimelineGlobalEventModuleEditor : HonamiTimelineModuleEditor
    {
        public override Color ModuleColor => TimelineTheme.GlobalEventTrack;
        public override Texture Icon => EditorGUIUtility.IconContent("AudioSource Icon").image;
        public override string ItemDisplayName => "Global Event";
        public override string GetItemLabel(HonamiTimelineItem item) => ((TimelineGlobalEventItem)item).globalEventId;
    }
}
