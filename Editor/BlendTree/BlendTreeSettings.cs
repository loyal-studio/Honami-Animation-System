using UnityEditor;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    internal static class BlendTreeSettings
    {
        private const string Prefix = "Honami_BlendTree_";
        private const string ShowPropertiesKey = Prefix + "ShowProperties";
        private const string PropsWidthKey = Prefix + "PropsWidth";
        private const string ShowPreviewKey = Prefix + "ShowPreview";

        public static bool ShowProperties
        {
            get => EditorPrefs.GetBool(ShowPropertiesKey, true);
            set => EditorPrefs.SetBool(ShowPropertiesKey, value);
        }

        public static bool ShowPreview
        {
            get => EditorPrefs.GetBool(ShowPreviewKey, true);
            set => EditorPrefs.SetBool(ShowPreviewKey, value);
        }

        public static float PropsWidth
        {
            get => EditorPrefs.GetFloat(PropsWidthKey, BlendTreeTheme.PropsWidth);
            set => EditorPrefs.SetFloat(PropsWidthKey, value);
        }
    }
}
