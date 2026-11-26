using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public enum TimelineMode { HonamiState, HonamiTimeline, HonamiClipEdit }

    public enum TimelineTimeFormat { Timecode, Frames, Seconds }

    public enum TimelineEditTool { Select, Blade }

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
        public const float MinTimeScale = 4f;
        public const float MaxTimeScale = 4000f;
        public const float MaxPlaybackSpeed = 8f;

        public TimelineMode Mode = TimelineMode.HonamiState;
        public GameObject PreviewRoot;

        public HonamiRuntimeController Controller;

        private HonamiState _selectedState;
        private string _selectedStateGuid;

        // Effective/virtual states are destroyed and rebuilt by the controller (OnValidate, override resync),
        // so the guid is the only stable identity; re-resolve whenever the cached reference dies.
        public HonamiState SelectedState
        {
            get
            {
                if (_selectedState != null) return _selectedState;
                if (string.IsNullOrEmpty(_selectedStateGuid) || Controller == null) return null;

                var states = Controller.ActiveStates;
                if (states == null) return null;

                for (int i = 0; i < states.Count; i++)
                {
                    if (states[i] != null && states[i].guid == _selectedStateGuid)
                    {
                        _selectedState = states[i];
                        return _selectedState;
                    }
                }
                return null;
            }
            set
            {
                _selectedState = value;
                _selectedStateGuid = value != null ? value.guid : null;
            }
        }

        public string SelectedStateGuid => _selectedStateGuid;

        public int SelectedLayerIndex = -1;
        public int RandomPreviewIdx;
        public string[] RandomPreviewOptions;
        public float BlendPreviewValue;

        // Per-axis preview values contributed by whichever node editor owns the selected state.
        public float[] NodePreviewAxes = System.Array.Empty<float>();

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

        public float PlaybackSpeed = 1f;
        public float LastSampledTime = float.NaN;
        public TimelineEditTool Tool = TimelineEditTool.Select;
        public bool ShowFrames = true;
        public bool UseSeconds;

        // Frames is the animator's mode; with it off time reads as timecode, or plain seconds when chosen on the ruler.
        public TimelineTimeFormat TimeFormat
        {
            get => ShowFrames ? TimelineTimeFormat.Frames : UseSeconds ? TimelineTimeFormat.Seconds : TimelineTimeFormat.Timecode;
            set
            {
                ShowFrames = value == TimelineTimeFormat.Frames;
                if (!ShowFrames) UseSeconds = value == TimelineTimeFormat.Seconds;
            }
        }

        public bool SnapEnabled = true;
        public bool ShowKeyframes;
        public float TimeScale = 100f;
        public float TrackHeaderWidth = 200f;
        public float PropsWidth = TimelineTheme.PropsWidth;

        public bool ShowProperties = true;
        public bool ShowAnimTrack = true;
        public bool ShowLinks = true;

        public List<HonamiSequencedAnimationClip> SelectedSeqClips = new();
        public Dictionary<HonamiSequencedAnimationClip, Rect> SeqClipRects = new();

        public List<HonamiTimelineItem> SelectedTimelineItems = new();
        public Dictionary<HonamiTimelineItem, Rect> TimelineItemRects = new();
        public static readonly List<HonamiTimelineItem> CopiedTimelineItems = new();

        // A gap is the empty stretch between items on one track, selected by clicking the empty space.
        // Its times are absolute on the timeline; the module keeps the track it belongs to.
        public HonamiTimelineModule SelectedGapModule;
        public float SelectedGapStart;
        public float SelectedGapEnd;
        public bool HasSelectedGap => SelectedGapModule != null && SelectedGapEnd - SelectedGapStart > 0.0001f;

        private HonamiDirector _director;
        private double _nextDirectorSearchTime;

        public List<HonamiTimelineMarker> SelectedMarkers = new();

        public HonamiTimelineModule SelectedModule;
        public bool IsDraggingTrack;
        public HonamiTimelineModule DraggedTrack;
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
            public AnimationClip[] MotionClips;
            public float[] MotionSpeeds;
            public bool[] MotionMirrors;
        }
        public BlendCacheData BlendCache;

        public List<GameObject> Proxies = new();

        public void ClearSelection()
        {
            SelectedSeqClips.Clear();
            SelectedTimelineItems.Clear();
            SelectedMarkers.Clear();
            SelectedKeyframes.Clear();
            SelectedModule = null;
            ClearSelectedGap();
        }

        public void ClearSelectionExceptKeyframes()
        {
            SelectedSeqClips.Clear();
            SelectedTimelineItems.Clear();
            SelectedMarkers.Clear();
            ClearSelectedGap();
        }

        public void ClearSelectedGap()
        {
            SelectedGapModule = null;
            SelectedGapStart = 0f;
            SelectedGapEnd = 0f;
        }

        public IHonamiTimelineModuleContainer ActiveContainer => Mode switch
        {
            TimelineMode.HonamiTimeline => ActiveTimeline,
            TimelineMode.HonamiState => SelectedState,
            _ => null
        };

        public List<HonamiTimelineModule> ActiveModules => ActiveContainer?.Modules;

        public void PruneStaleModuleSelection()
        {
            var modules = ActiveModules;
            if (modules == null)
            {
                SelectedTimelineItems.Clear();
                SelectedModule = null;
                ClearSelectedGap();
                return;
            }

            if (SelectedModule != null && !modules.Contains(SelectedModule))
                SelectedModule = null;

            if (SelectedGapModule != null && !modules.Contains(SelectedGapModule))
                ClearSelectedGap();

            for (int i = SelectedTimelineItems.Count - 1; i >= 0; i--)
            {
                if (FindModuleOf(SelectedTimelineItems[i]) == null)
                    SelectedTimelineItems.RemoveAt(i);
            }
        }

        public HonamiTimelineModule FindModuleOf(HonamiTimelineItem item)
        {
            var modules = ActiveModules;
            if (modules == null || item == null) return null;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] != null && modules[i].Contains(item))
                    return modules[i];
            }
            return null;
        }

        private HonamiNodeBase ModuleNode => Mode == TimelineMode.HonamiState ? node : null;

        public HonamiTimelineModuleParent ResolveParent(HonamiTimelineModule module)
        {
            var modules = ActiveModules;
            if (modules == null || module == null) return HonamiTimelineModuleParent.None;
            if (HonamiTimelineModuleParent.HasCycle(modules, ModuleNode, module)) return HonamiTimelineModuleParent.Missing;
            return HonamiTimelineModuleParent.Resolve(modules, ModuleNode, module);
        }

        public HonamiTimelineModule ParentModuleOf(HonamiTimelineModule module)
        {
            var parent = ResolveParent(module);
            return parent.Kind == HonamiTimelineModuleParentKind.Item ? ActiveModules[parent.ModuleIndex] : null;
        }

        public float GetModuleTimeOffset(HonamiTimelineModule module)
        {
            var parent = ResolveParent(module);
            return parent.Kind switch
            {
                HonamiTimelineModuleParentKind.Item => GetModuleTimeOffset(ActiveModules[parent.ModuleIndex]) + parent.Item.time,
                HonamiTimelineModuleParentKind.Motion => node.GetMotionStart(parent.MotionIndex),
                _ => 0f
            };
        }

        public float GetItemTimeOffset(HonamiTimelineItem item) => GetModuleTimeOffset(FindModuleOf(item));

        public bool IsModulePreviewed(HonamiTimelineModule module)
        {
            if (module == null || module.muted) return false;

            var parent = ResolveParent(module);
            switch (parent.Kind)
            {
                case HonamiTimelineModuleParentKind.Missing:
                    return false;
                case HonamiTimelineModuleParentKind.Item:
                    return IsModulePreviewed(ActiveModules[parent.ModuleIndex]);
                case HonamiTimelineModuleParentKind.Motion:
                    var editor = HonamiNodeEditorRegistry.GetEditor(node);
                    return editor == null || editor.IsMotionPreviewed(this, parent.MotionIndex);
                default:
                    return true;
            }
        }

        public bool IsModuleActiveAt(HonamiTimelineModule module, float time)
        {
            if (!IsModulePreviewed(module)) return false;

            var parent = ResolveParent(module);
            if (parent.Kind == HonamiTimelineModuleParentKind.Item)
            {
                var parentModule = ActiveModules[parent.ModuleIndex];
                float start = GetModuleTimeOffset(parentModule) + parent.Item.time;
                return time >= start && time <= start + parent.Item.Duration && IsModuleActiveAt(parentModule, time);
            }
            if (parent.Kind == HonamiTimelineModuleParentKind.Motion)
            {
                float start = node.GetMotionStart(parent.MotionIndex);
                return time >= start && time <= start + node.GetMotionDuration(parent.MotionIndex);
            }
            return true;
        }

        public GameObject ResolvePreviewRoot()
        {
            var selected = Selection.activeGameObject;
            var animator = selected != null ? selected.GetComponentInParent<HonamiAnimator>(true) : null;
            return animator != null ? animator.gameObject : null;
        }

        public HonamiDirector ResolveDirector()
        {
            if (ActiveTimeline == null) return null;

            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                var selectedDirector = selected.GetComponentInParent<HonamiDirector>(true);
                if (selectedDirector != null && selectedDirector.Timeline == ActiveTimeline)
                    return _director = selectedDirector;
            }

            if (_director != null && _director.Timeline == ActiveTimeline) return _director;
            if (EditorApplication.timeSinceStartup < _nextDirectorSearchTime) return null;

            _nextDirectorSearchTime = EditorApplication.timeSinceStartup + 1.0;
            return _director = HonamiTimelineEditContext.FindDirector(ActiveTimeline);
        }

        public void ForgetDirector()
        {
            _director = null;
            _nextDirectorSearchTime = 0;
        }

        public float FrameRate
        {
            get
            {
                switch (Mode)
                {
                    case TimelineMode.HonamiTimeline:
                        return ActiveTimeline != null ? ActiveTimeline.FrameRate : HonamiTimeline.DefaultFrameRate;
                    case TimelineMode.HonamiClipEdit:
                        return ActiveClip != null ? Mathf.Max(0f, ActiveClip.frameRate) : 0f;
                    default:
                        var clip = PrimaryStateClip();
                        return clip != null ? Mathf.Max(0f, clip.frameRate) : 0f;
                }
            }
        }

        public AnimationClip PrimaryStateClip()
        {
            if (AnimNode?.clip != null) return AnimNode.clip;
            if (IsRandomState && RandomNode.randomClips != null && RandomPreviewIdx >= 0 && RandomPreviewIdx < RandomNode.randomClips.Count)
                return RandomNode.randomClips[RandomPreviewIdx].clip;
            if (IsBlendState && BlendNode.blendMotions != null)
            {
                foreach (var motion in BlendNode.blendMotions)
                {
                    if (motion.clip != null) return motion.clip;
                }
            }
            return null;
        }

        // A state has no frame rate of its own: frames exist only when its clip provides one.
        public bool HasFrameRate => FrameRate > 0f;

        public float FrameDuration => HasFrameRate ? 1f / FrameRate : 1f / 60f;

        // Timecode is a cutscene notion, so states and clips show frames or seconds only.
        public TimelineTimeFormat EffectiveTimeFormat
        {
            get
            {
                if (!HasFrameRate) return TimelineTimeFormat.Seconds;
                if (Mode != TimelineMode.HonamiTimeline) return ShowFrames ? TimelineTimeFormat.Frames : TimelineTimeFormat.Seconds;
                return TimeFormat;
            }
        }

        public float SnapToFrame(float time)
        {
            float rate = FrameRate;
            return rate > 0f ? Mathf.Round(time * rate) / rate : time;
        }

        public bool IsModuleLocked(HonamiTimelineModule module)
        {
            for (int step = 0; module != null && step < 64; step++)
            {
                if (module.locked) return true;
                module = ParentModuleOf(module);
            }
            return false;
        }

        public void ClearCache()
        {
            AnimatorCache.Clear();
            ClipTargetCache.Clear();
            MaskCache = default;
            BlendCache = default;
            CachedTransforms = null;
            CachedConstraintsTarget = null;
            _director = null;
            _nextDirectorSearchTime = 0;

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
            UseSeconds = HonamiTimelineSettings.Seconds;
            ShowKeyframes = HonamiTimelineSettings.ShowKeyframes;
            TimeScale = HonamiTimelineSettings.TimeScale;
            TrackHeaderWidth = HonamiTimelineSettings.TrackHeaderWidth;
            PropsWidth = HonamiTimelineSettings.PropsWidth;
            ForceLoop = HonamiTimelineSettings.ForceLoop;
            ShowProperties = HonamiTimelineSettings.ShowProperties;
            ShowAnimTrack = HonamiTimelineSettings.ShowAnimTrack;
            ShowLinks = HonamiTimelineSettings.ShowLinks;
        }

        public void SaveSettings()
        {
            HonamiTimelineSettings.Snap = SnapEnabled;
            HonamiTimelineSettings.ShowFrames = ShowFrames;
            HonamiTimelineSettings.Seconds = UseSeconds;
            HonamiTimelineSettings.ShowKeyframes = ShowKeyframes;
            HonamiTimelineSettings.TimeScale = TimeScale;
            HonamiTimelineSettings.TrackHeaderWidth = TrackHeaderWidth;
            HonamiTimelineSettings.PropsWidth = PropsWidth;
            HonamiTimelineSettings.ForceLoop = ForceLoop;
            HonamiTimelineSettings.ShowProperties = ShowProperties;
            HonamiTimelineSettings.ShowAnimTrack = ShowAnimTrack;
            HonamiTimelineSettings.ShowLinks = ShowLinks;
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

        public bool IsPlayingBackwards => (SelectedState != null && SelectedState.isReversed) != PlaybackSpeed < 0f;

        internal static void Shuttle(TimelineState state, int direction)
        {
            if (direction == 0)
            {
                state.IsPlaying = false;
                state.PlaybackSpeed = 1f;
                return;
            }

            float speed = state.PlaybackSpeed;
            bool sameDirection = state.IsPlaying && (speed < 0f ? -1 : 1) == direction;
            speed = sameDirection ? Mathf.Clamp(Mathf.Abs(speed) * 2f, 1f, MaxPlaybackSpeed) : 1f;
            state.PlaybackSpeed = speed * direction;

            if (state.IsPlaying) return;
            state.IsPlaying = true;
            state.PreviewEnabled = true;
            state.LastUpdateTime = EditorApplication.timeSinceStartup;
        }

        public void PreModifyState()
        {
            if (Mode != TimelineMode.HonamiState || Controller == null || SelectedState == null) return;
            if (Controller is HonamiOverrideController ov && !ov.IsOwnedState(SelectedState))
            {
                var eff = Editor.Core.HonamiOverrideAuthoring.EnsureEffectiveState(ov, SelectedState);
                if (eff != SelectedState)
                {
                    RemapModuleSelection(SelectedState, eff);

                    if (SelectedState.node is HonamiSequencerNode oldSeq && eff.node is HonamiSequencerNode newSeq)
                    {
                        var newSeqClips = new List<HonamiSequencedAnimationClip>();
                        foreach (var sc in SelectedSeqClips)
                        {
                            int index = oldSeq.sequencedClips?.IndexOf(sc) ?? -1;
                            if (index >= 0 && index < (newSeq.sequencedClips?.Count ?? 0))
                                newSeqClips.Add(newSeq.sequencedClips[index]);
                        }
                        SelectedSeqClips = newSeqClips;
                    }
                    SelectedState = eff;
                }
            }
        }

        // An override's effective state is a deep copy, so selected modules and items are re-found by index in the copy.
        private void RemapModuleSelection(HonamiState from, HonamiState to)
        {
            var oldModules = from.Modules;
            var newModules = to.Modules;

            HonamiTimelineModule MapModule(HonamiTimelineModule module)
            {
                int index = module != null ? oldModules.IndexOf(module) : -1;
                return index >= 0 && index < newModules.Count ? newModules[index] : null;
            }

            SelectedModule = MapModule(SelectedModule);

            var remapped = new List<HonamiTimelineItem>();
            foreach (var item in SelectedTimelineItems)
            {
                for (int m = 0; m < oldModules.Count; m++)
                {
                    int itemIndex = oldModules[m] != null ? oldModules[m].IndexOf(item) : -1;
                    if (itemIndex < 0) continue;
                    var target = MapModule(oldModules[m]);
                    if (target != null && itemIndex < target.ItemCount)
                        remapped.Add(target.GetItem(itemIndex));
                    break;
                }
            }
            SelectedTimelineItems = remapped;
        }

        public void PostModifyState()
        {
            if (Mode != TimelineMode.HonamiState || Controller == null || SelectedState == null) return;
            if (Controller is HonamiOverrideController ov)
            {
                Editor.Core.HonamiOverrideAuthoring.ResolveState(ov, SelectedState, out var entry, out _);
                if (entry != null)
                {
                    Editor.Core.HonamiOverrideAuthoring.RefreshStateModified(ov, entry);
                }
            }
        }

        internal static void TogglePlayback(TimelineState state)
        {
            state.IsPlaying = !state.IsPlaying;
            state.PlaybackSpeed = 1f;
            if (!state.IsPlaying) return;

            state.PreviewEnabled = true;
            state.LastUpdateTime = EditorApplication.timeSinceStartup;

            float duration = state.GetDuration();
            bool isLoop = state.ForceLoop || (state.SelectedState?.loop ?? false);

            if (state.IsPlayingBackwards)
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
