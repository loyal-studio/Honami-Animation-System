#if UNITY_EDITOR
using UnityEngine;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    internal static class BlendTreeTheme
    {
        public const float ToolbarHeight = 38f;
        public const float TransportBarHeight = 36f;
        public const float PropsWidth = 340f;
        public const float PropsMinWidth = 260f;
        public const float PropsMaxWidth = 560f;
        public const float NodeWidth = 280f;
        public const float OutputNodeWidth = 200f;
        public const float OutputNodePreviewWidth = 260f;
        public const float ZoomMin = 0.25f;
        public const float ZoomMax = 2.5f;

        public const float ArcRadius = 22f;
        public const float ArcThickness = 5f;

        public static Color Accent => HonamiEditorTheme.Accent;
        public static Color AccentSoft => HonamiEditorTheme.AccentSoft;
        public static Color WindowBg => HonamiEditorTheme.WindowBg;
        public static Color PanelBg => HonamiEditorTheme.PanelBg;
        public static Color ToolbarBg => HonamiEditorTheme.ToolbarBg;
        public static readonly Color Divider = HonamiEditorTheme.Divider;
        public static readonly Color SubtleLine = HonamiEditorTheme.SubtleLine;
        public static Color Text => HonamiEditorTheme.Text;
        public static Color MutedText => HonamiEditorTheme.MutedText;
        public static readonly Color ArcTrack = new(0.08f, 0.085f, 0.095f);

        public static Color MotionColor(int index, int count)
        {
            float hue = count <= 1 ? 0.55f : index / (float)count;
            return Color.HSVToRGB(hue, 0.70f, 0.95f);
        }
    }
}
#endif
