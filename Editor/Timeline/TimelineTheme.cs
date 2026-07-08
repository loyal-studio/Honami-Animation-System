#if UNITY_EDITOR
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal static class TimelineTheme
    {
        public const float ToolbarHeight = 38f;
        public const float TransportBarHeight = 32f;
        public const float RulerHeight = 24f;
        public const float RowHeight = 40f;
        public const float ClipHeight = 32f;
        public const float PropsWidth = 280f;

        public static Color Accent => HonamiEditorTheme.Accent;
        public static Color AccentDim => HonamiEditorTheme.AccentDim;
        public static Color AccentSoft => HonamiEditorTheme.AccentSoft;
        public static Color WindowBg => HonamiEditorTheme.WindowBg;
        public static readonly Color PanelBg = new(0.078f, 0.083f, 0.092f);
        public static Color ToolbarBg => HonamiEditorTheme.ToolbarBg;
        public static readonly Color ToolbarButton = HonamiEditorTheme.ToolbarButton;
        public static readonly Color ToolbarButtonHot = HonamiEditorTheme.ToolbarButtonHot;
        public static readonly Color ToolbarButtonPressed = HonamiEditorTheme.ToolbarButtonPressed;
        public static readonly Color RulerBg = new(0.105f, 0.112f, 0.124f);
        public static readonly Color HeaderBg = new(0.095f, 0.102f, 0.112f);
        public static readonly Color BoneGroupHeaderBg = new(0.13f, 0.138f, 0.15f);
        public static readonly Color SummaryHeaderBg = new(0.17f, 0.15f, 0.165f);
        public static readonly Color HeaderSelected = new(0.16f, 0.135f, 0.155f);
        public static readonly Color TrackBgA = new(0.106f, 0.114f, 0.12f);
        public static readonly Color TrackBgB = new(0.092f, 0.098f, 0.106f);
        public static readonly Color Divider = HonamiEditorTheme.Divider;
        public static readonly Color SubtleLine = HonamiEditorTheme.SubtleLine;
        public static readonly Color MajorGrid = new(0f, 0f, 0f, 0.42f);
        public static readonly Color MinorGrid = new(1f, 1f, 1f, 0.04f);
        public static Color Playhead => Accent;
        public static readonly Color EndBoundary = new(1f, 0.34f, 0.48f, 0.82f);
        public static readonly Color AnimationTrack = new(0.24f, 0.45f, 0.72f, 0.96f);
        public static readonly Color EventTrack = new(0.14f, 0.68f, 0.46f, 0.96f);
        public static readonly Color RandomTrack = new(0.68f, 0.28f, 0.58f, 0.96f);
        public static readonly Color SequencerTrack = new(0.35f, 0.62f, 0.38f, 0.96f);
        public static Color PreviewTrack => Accent;
        public static Color Selected => Accent;
        public static readonly Color BoneGroup = new(0.62f, 0.64f, 0.70f, 0.95f);
        public static readonly Color KeyframeFill = new(0.82f, 0.85f, 0.90f);
        public static readonly Color KeyframeSelected = new(1f, 0.75f, 0.30f);

        public static Color CurrentFrameBand
        {
            get
            {
                var color = Accent;
                color.a = 0.07f;
                return color;
            }
        }

        public static Color Text => HonamiEditorTheme.Text;
        public static Color MutedText => HonamiEditorTheme.MutedText;

    }
}
#endif
