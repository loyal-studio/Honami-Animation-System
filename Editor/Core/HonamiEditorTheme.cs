#if UNITY_EDITOR
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiEditorTheme
    {
        public static Color Accent => HonamiAppearanceSettings.Accent.Value;
        public static Color AccentDim => HonamiAppearanceSettings.AccentDim.Value;

        public static Color AccentSoft
        {
            get
            {
                var color = Accent;
                color.a = 0.18f;
                return color;
            }
        }

        public static Color WindowBg => HonamiAppearanceSettings.WindowBg.Value;
        public static Color PanelBg => HonamiAppearanceSettings.PanelBg.Value;
        public static Color ToolbarBg => HonamiAppearanceSettings.ToolbarBg.Value;
        public static Color ToolbarButton => BoxBg;
        public static Color ToolbarButtonHot => Scale(BoxBg, 1.40f);
        public static Color ToolbarButtonPressed => Scale(BoxBg, 0.625f);
        public static Color BoxBg => HonamiAppearanceSettings.BoxBg.Value;
        public static readonly Color Divider = new(0f, 0f, 0f, 0.58f);
        public static readonly Color SubtleLine = new(1f, 1f, 1f, 0.07f);
        public static Color Text => HonamiAppearanceSettings.Text.Value;
        public static Color MutedText => HonamiAppearanceSettings.MutedText.Value;
        public static Color CanvasBg => HonamiAppearanceSettings.CanvasBg.Value;
        public static Color GridLine => HonamiAppearanceSettings.GridLine.Value;
        public static Color GridThickLine => HonamiAppearanceSettings.GridThickLine.Value;

        public static Color Scale(Color color, float factor) =>
            new(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}
#endif
