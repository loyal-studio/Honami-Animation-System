using System;
using UnityEditor;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiGraphSettings
    {
        private const string MinimapKey = "Honami_ShowMinimap";
        private const string AnimationsKey = "Honami_EnableAnimations";
        private const string ShowGridKey = "Honami_ShowGrid";
        private const string Tactile3DKey = "Honami_Tactile3D";

        public static event Action Changed;

        public static bool ShowMinimap
        {
            get => EditorPrefs.GetBool(MinimapKey, true);
            set => SetBool(MinimapKey, value);
        }

        public static bool EnableAnimations
        {
            get => EditorPrefs.GetBool(AnimationsKey, true);
            set => SetBool(AnimationsKey, value);
        }

        public static bool ShowGrid
        {
            get => EditorPrefs.GetBool(ShowGridKey, true);
            set => SetBool(ShowGridKey, value);
        }

        public static bool EnableTactile3D
        {
            get => EditorPrefs.GetBool(Tactile3DKey, true);
            set => SetBool(Tactile3DKey, value);
        }

        private static void SetBool(string key, bool value)
        {
            if (EditorPrefs.GetBool(key, true) == value) return;
            EditorPrefs.SetBool(key, value);
            Changed?.Invoke();
        }

        [InitializeOnLoadMethod]
        private static void RegisterPreferences()
        {
            HonamiPreferences.Category("Graph")
                .WithDescription("Visual behaviour of the Honami graph canvas. Changes apply to open graph windows immediately. Right-click a field to reset it.")
                .AddToggle("Enable Animations", () => EnableAnimations, value => EnableAnimations = value,
                    "Smooth open and transition animations in the graph canvas", () => EnableAnimations = true)
                .AddToggle("Show Grid", () => ShowGrid, value => ShowGrid = value,
                    "Background grid of the graph canvas", () => ShowGrid = true)
                .AddToggle("Tactile 3D Nodes", () => EnableTactile3D, value => EnableTactile3D = value,
                    "3D tilt feedback on nodes under the cursor", () => EnableTactile3D = true);
        }
    }
}
