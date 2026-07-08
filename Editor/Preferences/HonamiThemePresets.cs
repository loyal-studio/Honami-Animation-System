using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    internal sealed class HonamiThemePreset
    {
        public string Name { get; }
        public Color[] Colors { get; }

        public HonamiThemePreset(string name, params Color[] colors)
        {
            Name = name;
            Colors = colors;
        }
    }

    internal static class HonamiThemePresets
    {
        // Color order matches HonamiAppearanceSettings.All:
        // Accent, AccentDim, WindowBg, PanelBg, ToolbarBg, BoxBg, Text, MutedText, CanvasBg, GridLine, GridThickLine
        public static readonly HonamiThemePreset[] All =
        {
            new("Honami (Default)",
                HonamiAppearanceSettings.Accent.DefaultValue,
                HonamiAppearanceSettings.AccentDim.DefaultValue,
                HonamiAppearanceSettings.WindowBg.DefaultValue,
                HonamiAppearanceSettings.PanelBg.DefaultValue,
                HonamiAppearanceSettings.ToolbarBg.DefaultValue,
                HonamiAppearanceSettings.BoxBg.DefaultValue,
                HonamiAppearanceSettings.Text.DefaultValue,
                HonamiAppearanceSettings.MutedText.DefaultValue,
                HonamiAppearanceSettings.CanvasBg.DefaultValue,
                HonamiAppearanceSettings.GridLine.DefaultValue,
                HonamiAppearanceSettings.GridThickLine.DefaultValue),

            new("Midnight",
                new Color(0.30f, 0.56f, 1.00f),
                new Color(0.20f, 0.38f, 0.72f),
                new Color(0.050f, 0.056f, 0.072f),
                new Color(0.085f, 0.096f, 0.120f),
                new Color(0.077f, 0.087f, 0.108f),
                new Color(0.115f, 0.130f, 0.160f),
                new Color(0.87f, 0.90f, 0.94f),
                new Color(0.55f, 0.60f, 0.68f),
                new Color(0.062f, 0.070f, 0.092f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(1f, 1f, 1f, 0.08f)),

            new("Viper",
                new Color(0.45f, 0.90f, 0.40f),
                new Color(0.28f, 0.60f, 0.25f),
                new Color(0.050f, 0.060f, 0.052f),
                new Color(0.086f, 0.102f, 0.090f),
                new Color(0.078f, 0.092f, 0.082f),
                new Color(0.116f, 0.138f, 0.122f),
                new Color(0.88f, 0.92f, 0.88f),
                new Color(0.56f, 0.63f, 0.57f),
                new Color(0.060f, 0.074f, 0.064f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(1f, 1f, 1f, 0.08f)),

            new("Ember",
                new Color(1.00f, 0.56f, 0.18f),
                new Color(0.70f, 0.36f, 0.10f),
                new Color(0.064f, 0.056f, 0.050f),
                new Color(0.108f, 0.096f, 0.086f),
                new Color(0.098f, 0.088f, 0.078f),
                new Color(0.148f, 0.130f, 0.116f),
                new Color(0.92f, 0.89f, 0.85f),
                new Color(0.65f, 0.60f, 0.54f),
                new Color(0.080f, 0.070f, 0.060f),
                new Color(1f, 0.95f, 0.88f, 0.03f),
                new Color(1f, 0.95f, 0.88f, 0.08f)),

            new("Ultraviolet",
                new Color(0.66f, 0.50f, 1.00f),
                new Color(0.44f, 0.32f, 0.72f),
                new Color(0.058f, 0.054f, 0.072f),
                new Color(0.100f, 0.094f, 0.122f),
                new Color(0.090f, 0.084f, 0.110f),
                new Color(0.134f, 0.126f, 0.162f),
                new Color(0.89f, 0.88f, 0.93f),
                new Color(0.60f, 0.58f, 0.68f),
                new Color(0.072f, 0.066f, 0.092f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(1f, 1f, 1f, 0.08f)),

            new("Graphite",
                new Color(0.76f, 0.79f, 0.83f),
                new Color(0.50f, 0.53f, 0.57f),
                new Color(0.058f, 0.058f, 0.061f),
                new Color(0.100f, 0.100f, 0.105f),
                new Color(0.090f, 0.090f, 0.094f),
                new Color(0.134f, 0.134f, 0.140f),
                new Color(0.90f, 0.90f, 0.91f),
                new Color(0.60f, 0.60f, 0.63f),
                new Color(0.072f, 0.072f, 0.076f),
                new Color(1f, 1f, 1f, 0.03f),
                new Color(1f, 1f, 1f, 0.08f)),
        };
    }
}
