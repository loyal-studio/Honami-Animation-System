using UnityEditor;

namespace HonamiAnimationSystem.Editor.BlendSpace
{
    internal static class BlendSpaceSettings
    {
        private const string Prefix = "Honami_BlendSpace_";
        private const string ShowPropertiesKey = Prefix + "ShowProperties";
        private const string ShowPreviewKey = Prefix + "ShowPreview";
        private const string PropsWidthKey = Prefix + "PropsWidth";

        public const float DefaultPropsWidth = 340f;
        public const float PropsMinWidth = 280f;
        public const float PropsMaxWidth = 560f;
        public const float ToolbarHeight = 38f;
        public const float TransportBarHeight = 36f;

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
            get => EditorPrefs.GetFloat(PropsWidthKey, DefaultPropsWidth);
            set => EditorPrefs.SetFloat(PropsWidthKey, value);
        }
    }
}
