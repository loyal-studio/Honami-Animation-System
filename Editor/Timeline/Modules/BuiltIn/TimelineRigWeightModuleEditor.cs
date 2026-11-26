using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineRigWeightModule), "Rig Weight", 31)]
    public sealed class TimelineRigWeightModuleEditor : TimelineParameterCurveModuleEditor
    {
        private static readonly Color RigColor = new(0.30f, 0.62f, 0.55f, 0.96f);

        public override Color ModuleColor => RigColor;
        public override Texture Icon => EditorGUIUtility.IconContent("d_AvatarSelector").image;
    }
}
