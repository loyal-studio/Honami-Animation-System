using UnityEditor;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public static class HonamiTimelineSettings
    {
        private const string Prefix = "Honami_Timeline_";
        private const string SnapKey = Prefix + "Snap";
        private const string ShowKeyframesKey = Prefix + "ShowKeyframes";
        private const string TimeScaleKey = Prefix + "TimeScale";
        private const string TrackHeaderKey = Prefix + "TrackHeaderWidth";
        private const string PropsWidthKey = Prefix + "PropsWidth";
        private const string ShowAnimKey = Prefix + "ShowAnimTrack";
        private const string ForceLoopKey = Prefix + "ForceLoop";
        private const string ShowPropertiesKey = Prefix + "ShowProperties";
        private const string ShowFramesKey = Prefix + "ShowFrames";
        private const string SecondsKey = Prefix + "Seconds";
        private const string ShowLinksKey = Prefix + "ShowLinks";
        private const string LinkGapKey = Prefix + "LinkGap";

        public static bool Snap
        {
            get => EditorPrefs.GetBool(SnapKey, true);
            set => EditorPrefs.SetBool(SnapKey, value);
        }

        public static bool ShowKeyframes
        {
            get => EditorPrefs.GetBool(ShowKeyframesKey, false);
            set => EditorPrefs.SetBool(ShowKeyframesKey, value);
        }

        public static float TimeScale
        {
            get => EditorPrefs.GetFloat(TimeScaleKey, 100f);
            set => EditorPrefs.SetFloat(TimeScaleKey, value);
        }

        public static float TrackHeaderWidth
        {
            get => EditorPrefs.GetFloat(TrackHeaderKey, 200f);
            set => EditorPrefs.SetFloat(TrackHeaderKey, value);
        }

        public static float PropsWidth
        {
            get => EditorPrefs.GetFloat(PropsWidthKey, TimelineTheme.PropsWidth);
            set => EditorPrefs.SetFloat(PropsWidthKey, value);
        }

        public static bool ShowAnimTrack
        {
            get => EditorPrefs.GetBool(ShowAnimKey, true);
            set => EditorPrefs.SetBool(ShowAnimKey, value);
        }

        public static bool ForceLoop
        {
            get => EditorPrefs.GetBool(ForceLoopKey, false);
            set => EditorPrefs.SetBool(ForceLoopKey, value);
        }

        public static bool ShowFrames
        {
            get => EditorPrefs.GetBool(ShowFramesKey, true);
            set => EditorPrefs.SetBool(ShowFramesKey, value);
        }

        public static bool Seconds
        {
            get => EditorPrefs.GetBool(SecondsKey, false);
            set => EditorPrefs.SetBool(SecondsKey, value);
        }

        public static bool ShowProperties
        {
            get => EditorPrefs.GetBool(ShowPropertiesKey, true);
            set => EditorPrefs.SetBool(ShowPropertiesKey, value);
        }

        public static bool ShowLinks
        {
            get => EditorPrefs.GetBool(ShowLinksKey, true);
            set => EditorPrefs.SetBool(ShowLinksKey, value);
        }

        // Gap left between items when a link group is laid out end to end.
        public static float LinkGap
        {
            get => EditorPrefs.GetFloat(LinkGapKey, 0f);
            set => EditorPrefs.SetFloat(LinkGapKey, value);
        }
    }
}
