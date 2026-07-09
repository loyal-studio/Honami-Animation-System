using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public enum TimelineMode { HonamiState, HonamiTimeline, HonamiClipEdit }

    public readonly struct KeyframeRef : System.IEquatable<KeyframeRef>
    {
        public readonly int Binding;
        public readonly float Time;

        public KeyframeRef(int binding, float time)
        {
            Binding = binding;
            Time = time;
        }

        public bool Equals(KeyframeRef other) => Binding == other.Binding && Time.Equals(other.Time);
        public override bool Equals(object obj) => obj is KeyframeRef other && Equals(other);
        public override int GetHashCode() => (Binding * 397) ^ Time.GetHashCode();
    }

    public struct KeyframeClipboardEntry
    {
        public int Binding;
        public float RelTime;
        public Keyframe Key;
    }

    public sealed class TimelineState
    {
        public TimelineMode Mode = TimelineMode.HonamiState;
        public GameObject PreviewRoot;

        public HonamiController Controller;
        public HonamiState SelectedState;
        public int SelectedLayerIndex = -1;
        public int RandomPreviewIdx;
        public string[] RandomPreviewOptions;
        public float BlendPreviewValue;

        public HonamiTimeline ActiveTimeline;
        public AnimationClip ActiveClip;
        public GameObject RecordingContext;

        public float PlayheadTime;
        public Vector2 ScrollPos;
        public float SnapLineTime = -1f;
        public bool IsPlaying;
        public bool PreviewEnabled;
        public bool ForceLoop;
        public bool IsRecording;
        public double LastUpdateTime;

        public bool SnapEnabled = true;
        public bool ShowFrames = true;
        public bool ShowKeyframes;
        public float TimeScale = 100f;
        public float TrackHeaderWidth = 200f;
        public float PropsWidth = TimelineTheme.PropsWidth;

        public bool ShowProperties = true;
        public bool ShowAnimTrack = true;
        public bool ShowLocalEventsTrack = true;
        public bool ShowGlobalEventsTrack = true;

        public List<HonamiEventMarker> SelectedEvents = new();
        public Dictionary<HonamiEventMarker, Rect> EventRects = new();
        public static readonly List<HonamiEventMarker> CopiedEvents = new();

        public List<HonamiSequencedAnimationClip> SelectedSeqClips = new();
        public Dictionary<HonamiSequencedAnimationClip, Rect> SeqClipRects = new();

        public List<HonamiTimelineClip> SelectedTimelineClips = new();
        public Dictionary<HonamiTimelineClip, Rect> TimelineClipRects = new();
        public static readonly List<HonamiTimelineClip> CopiedTimelineClips = new();

        public List<HonamiTimelineEvent> SelectedTimelineEvents = new();
        public Dictionary<HonamiTimelineEvent, Rect> TimelineEventRects = new();
        public static readonly List<HonamiTimelineEvent> CopiedTimelineEvents = new();

        public List<HonamiTimelineMarker> SelectedMarkers = new();

        public HonamiTimelineTrack SelectedTimelineTrack;
        public bool IsDraggingTrack;
        public HonamiTimelineTrack DraggedTrack;
        public float DragTrackStartY;
        public float DragTrackStartMouseY;
        public int DragTrackInsertIndex = -1;

        public bool IsResizingTrackHeader;
        public bool IsResizingPropsPanel;
        public bool IsDraggingPlayhead;
        public bool IsBoxSelecting;
        public Vector2 BoxSelectStartPos;
        public Rect BoxSelectRect;

        public bool IsDraggingRandomAnimStart;
        public bool IsDraggingRandomAnimEnd;
        public int RandomDragIndex;
        public float AnimDragOrigVal;
        public float AnimDragStartMouseTime;

        public Transform[] CachedTransforms;
        public GameObject CachedConstraintsTarget;

        public Dictionary<GameObject, Animator> AnimatorCache = new();
        public Dictionary<int, GameObject> ClipTargetCache = new();
        public HashSet<string> ExpandedClipGroups = new();
        public HashSet<string> ExpandedClipBones = new();
        public HashSet<string> MutedChannels = new();
        public HashSet<string> LockedChannels = new();
        public string ClipEditFilter = string.Empty;

        public List<KeyframeRef> SelectedKeyframes = new();
        public Dictionary<KeyframeRef, Rect> KeyframeRects = new();
        public List<KeyframeClipboardEntry> CopiedKeyframes = new();

        public struct MaskCacheData
        {
            public GameObject Root;
            public HonamiAvatarMask Mask;
            public List<(Transform t, float weight)> WeightedTransforms;
            public Vector3[] SavedPos;
            public Quaternion[] SavedRot;
        }
        public MaskCacheData MaskCache;

        public struct BlendCacheData
        {
            public GameObject Target;
            public Transform[] Transforms;
            public Vector3[] BasePositions;
            public Quaternion[] BaseRotations;
            public Vector3[] BaseScales;
            public Vector3[] BlendedPositions;
            public Vector3[] BlendedScales;
            public Quaternion[] BlendedRotations;
            public bool[] HasRotation;
            public float[] BlendWeights;
            public (float threshold, int index)[] BlendSorted;
        }
        public BlendCacheData BlendCache;

        public List<GameObject> Proxies = new();

        public void ClearSelection()
        {
            SelectedEvents.Clear();
            SelectedSeqClips.Clear();
            SelectedTimelineClips.Clear();
            SelectedTimelineEvents.Clear();
            SelectedMarkers.Clear();
            SelectedKeyframes.Clear();
            SelectedTimelineTrack = null;
        }

        public void ClearSelectionExceptKeyframes()
        {
            SelectedEvents.Clear();
            SelectedSeqClips.Clear();
            SelectedTimelineClips.Clear();
            SelectedTimelineEvents.Clear();
            SelectedMarkers.Clear();
        }

        public void ClearCache()
        {
            AnimatorCache.Clear();
            ClipTargetCache.Clear();
            MaskCache = default;
            BlendCache = default;
            CachedTransforms = null;
            CachedConstraintsTarget = null;

            for (int i = 0; i < Proxies.Count; i++)
            {
                if (Proxies[i] != null)
                    Object.DestroyImmediate(Proxies[i]);
            }
            Proxies.Clear();
        }

        public void LoadSettings()
        {
            SnapEnabled = HonamiTimelineSettings.Snap;
            ShowFrames = HonamiTimelineSettings.ShowFrames;
            ShowKeyframes = HonamiTimelineSettings.ShowKeyframes;
            TimeScale = HonamiTimelineSettings.TimeScale;
            TrackHeaderWidth = HonamiTimelineSettings.TrackHeaderWidth;
            PropsWidth = HonamiTimelineSettings.PropsWidth;
            ForceLoop = HonamiTimelineSettings.ForceLoop;
            ShowProperties = HonamiTimelineSettings.ShowProperties;
            ShowAnimTrack = HonamiTimelineSettings.ShowAnimTrack;
            ShowLocalEventsTrack = HonamiTimelineSettings.ShowLocalEventsTrack;
            ShowGlobalEventsTrack = HonamiTimelineSettings.ShowGlobalEventsTrack;
        }

        public void SaveSettings()
        {
            HonamiTimelineSettings.Snap = SnapEnabled;
            HonamiTimelineSettings.ShowFrames = ShowFrames;
            HonamiTimelineSettings.ShowKeyframes = ShowKeyframes;
            HonamiTimelineSettings.TimeScale = TimeScale;
            HonamiTimelineSettings.TrackHeaderWidth = TrackHeaderWidth;
            HonamiTimelineSettings.PropsWidth = PropsWidth;
            HonamiTimelineSettings.ForceLoop = ForceLoop;
            HonamiTimelineSettings.ShowProperties = ShowProperties;
            HonamiTimelineSettings.ShowAnimTrack = ShowAnimTrack;
            HonamiTimelineSettings.ShowLocalEventsTrack = ShowLocalEventsTrack;
            HonamiTimelineSettings.ShowGlobalEventsTrack = ShowGlobalEventsTrack;
        }

        public HonamiAnimationNode AnimNode => SelectedState?.node as HonamiAnimationNode;
        public HonamiRandomAnimationNode RandomNode => SelectedState?.node as HonamiRandomAnimationNode;
        public HonamiSequencerNode SeqNode => SelectedState?.node as HonamiSequencerNode;
        public HonamiBlendTreeNode BlendNode => SelectedState?.node as HonamiBlendTreeNode;
        public HonamiNodeBase node => SelectedState?.node;

        public bool IsAnimState => AnimNode != null;
        public bool IsRandomState => RandomNode != null;
        public bool IsSeqState => SeqNode != null;
        public bool IsBlendState => BlendNode != null;

        public bool IsClipReadOnly => ActiveClip != null && AssetDatabase.GetAssetPath(ActiveClip).ToLower().EndsWith(".fbx");

        public float GetDuration()
        {
            if (Mode == TimelineMode.HonamiClipEdit)
                return ActiveClip != null ? ActiveClip.length : 5f;

            if (Mode == TimelineMode.HonamiTimeline)
                return ActiveTimeline != null ? ActiveTimeline.GetDuration() : 5f;

            if (SelectedState == null || SelectedState.node == null) return 5f;
            var editor = HonamiNodeEditorRegistry.GetEditor(SelectedState.node);
            return editor != null ? editor.GetTimelineDuration(this) : 5f;
        }

        public float GetTimelineWidth(float windowWidth) =>
            Mathf.Max(GetDuration() * TimeScale + 100f, windowWidth - TrackHeaderWidth);

        internal static void TogglePlayback(TimelineState state)
        {
            state.IsPlaying = !state.IsPlaying;
            if (!state.IsPlaying) return;

            state.PreviewEnabled = true;
            state.LastUpdateTime = EditorApplication.timeSinceStartup;

            float duration = state.GetDuration();
            bool isLoop = state.ForceLoop || (state.SelectedState?.loop ?? false);

            if (state.SelectedState != null && state.SelectedState.isReversed)
            {
                if (state.PlayheadTime <= 0f && !isLoop)
                    state.PlayheadTime = duration;
            }
            else
            {
                if (state.PlayheadTime >= duration && !isLoop)
                    state.PlayheadTime = 0f;
            }
        }
    }
}
