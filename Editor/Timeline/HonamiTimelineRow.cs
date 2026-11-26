using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public readonly struct HonamiTimelineRow
    {
        public const int NoMotion = -1;

        public readonly string Title;
        public readonly Texture Icon;
        public readonly Color Color;
        public readonly int MotionIndex;

        public HonamiTimelineRow(string title, Texture icon, Color color, int motionIndex = NoMotion)
        {
            Title = title;
            Icon = icon;
            Color = color;
            MotionIndex = motionIndex;
        }

        public bool HasMotion => MotionIndex >= 0;

        public static Texture ClipIcon => EditorGUIUtility.IconContent("AnimationClip Icon").image;
    }
}
