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
        public static readonly Color ToolbarButton = new(0.13f, 0.14f, 0.155f);
        public static readonly Color ToolbarButtonHot = new(0.18f, 0.19f, 0.21f);
        public static readonly Color ToolbarButtonPressed = new(0.08f, 0.085f, 0.095f);
        public static Color BoxBg => HonamiAppearanceSettings.BoxBg.Value;
        public static readonly Color Divider = new(0f, 0f, 0f, 0.58f);
        public static readonly Color SubtleLine = new(1f, 1f, 1f, 0.07f);
        public static Color Text => HonamiAppearanceSettings.Text.Value;
        public static Color MutedText => HonamiAppearanceSettings.MutedText.Value;
        public static Color CanvasBg => HonamiAppearanceSettings.CanvasBg.Value;
        public static Color GridLine => HonamiAppearanceSettings.GridLine.Value;
        public static Color GridThickLine => HonamiAppearanceSettings.GridThickLine.Value;
    }
}
#endif
