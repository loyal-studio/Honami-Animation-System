using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineLogEventModule), "Events/Log", 12)]
    public sealed class TimelineLogEventModuleEditor : HonamiTimelineModuleEditor
    {
        private static readonly Color LogColor = new(0.55f, 0.60f, 0.68f, 0.96f);
        private static readonly Color WarningColor = new(0.92f, 0.74f, 0.25f, 0.96f);
        private static readonly Color ErrorColor = new(0.90f, 0.35f, 0.35f, 0.96f);

        public override Color ModuleColor => LogColor;
        public override Texture Icon => EditorGUIUtility.IconContent("d_console.infoicon.sml").image;
        public override string ItemDisplayName => "Log";

        public override string GetItemLabel(HonamiTimelineItem item) => ((TimelineLogEventItem)item).message;

        public override Texture GetItemIcon(HonamiTimelineItem item) => ((TimelineLogEventItem)item).logType switch
        {
            LogType.Warning => EditorGUIUtility.IconContent("console.warnicon.sml").image,
            LogType.Error or LogType.Exception or LogType.Assert => EditorGUIUtility.IconContent("console.erroricon.sml").image,
            _ => EditorGUIUtility.IconContent("console.infoicon.sml").image
        };

        public override Color GetItemColor(HonamiTimelineItem item) => ((TimelineLogEventItem)item).logType switch
        {
            LogType.Warning => WarningColor,
            LogType.Error or LogType.Exception or LogType.Assert => ErrorColor,
            _ => LogColor
        };
    }
}
