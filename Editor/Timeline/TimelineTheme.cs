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
        public static Color ToolbarBg => HonamiEditorTheme.ToolbarBg;
        public static Color ToolbarButton => HonamiEditorTheme.ToolbarButton;
        public static Color ToolbarButtonHot => HonamiEditorTheme.ToolbarButtonHot;
        public static Color ToolbarButtonPressed => HonamiEditorTheme.ToolbarButtonPressed;

        // Scale/lerp factors reproduce the stock dark look under the default theme.
        public static Color PanelBg => HonamiEditorTheme.Scale(HonamiEditorTheme.PanelBg, 0.82f);
        public static Color RulerBg => HonamiEditorTheme.Scale(HonamiEditorTheme.PanelBg, 1.10f);
        public static Color HeaderBg => HonamiEditorTheme.PanelBg;
        public static Color BoneGroupHeaderBg => HonamiEditorTheme.BoxBg;
        public static Color SummaryHeaderBg => Color.Lerp(HonamiEditorTheme.BoxBg, Accent, 0.05f);
        public static Color HeaderSelected => Color.Lerp(HonamiEditorTheme.PanelBg, Accent, 0.08f);
        public static Color TrackBgA => HonamiEditorTheme.Scale(HonamiEditorTheme.PanelBg, 1.12f);
        public static Color TrackBgB => HonamiEditorTheme.Scale(HonamiEditorTheme.PanelBg, 0.965f);

        public static Color Divider => HonamiEditorTheme.Divider;
        public static Color SubtleLine => HonamiEditorTheme.SubtleLine;
        public static readonly Color MajorGrid = new(0f, 0f, 0f, 0.42f);
        public static readonly Color MinorGrid = new(1f, 1f, 1f, 0.04f);
        public static Color Playhead => Accent;
        public static readonly Color EndBoundary = new(1f, 0.34f, 0.48f, 0.82f);
        public static readonly Color AnimationTrack = new(0.24f, 0.45f, 0.72f, 0.96f);
        public static readonly Color EventTrack = new(0.14f, 0.68f, 0.46f, 0.96f);
        public static readonly Color GlobalEventTrack = new(0.75f, 0.42f, 0.22f, 0.95f);
        public static readonly Color RandomTrack = new(0.68f, 0.28f, 0.58f, 0.96f);
        public static readonly Color SequencerTrack = new(0.35f, 0.62f, 0.38f, 0.96f);
        public static Color PreviewTrack => Accent;
        public static Color Selected => Accent;

        public static Color BoneGroup
        {
            get
            {
                var color = HonamiEditorTheme.Scale(MutedText, 1.07f);
                color.a = 0.95f;
                return color;
            }
        }

        public static Color KeyframeFill => HonamiEditorTheme.Scale(Text, 0.94f);
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
