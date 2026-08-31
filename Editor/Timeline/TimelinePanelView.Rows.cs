#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private struct RowData
        {
            public int Index;
            public string Title;
            public string IconName;
            public Color Color;
            public bool IsSelected;
            public HonamiTimelineTrack Track;

            public bool IsGroup;
            public bool IsBoneGroup;
            public bool IsSummaryRow;
            public string BonePath;
            public string GroupId;
            public bool IsExpanded;
            public int Depth;
            public int BindingStart;
            public int BindingCount;
        }

        private sealed class ClipCurveCache
        {
            public AnimationClip Clip;
            public EditorCurveBinding[] Bindings;
            public AnimationCurve[] Curves;

            public void Clear()
            {
                Clip = null;
                Bindings = null;
                Curves = null;
            }
        }

        private readonly List<RowData> _cachedRows = new();
        private readonly ClipCurveCache _curveCache = new();

        private void EnsureCurveCache()
        {
            if (_curveCache.Clip == _state.ActiveClip && _curveCache.Bindings != null) return;

            _curveCache.Clear();
            _curveCache.Clip = _state.ActiveClip;
            _curveCache.Bindings = AnimationUtility.GetCurveBindings(_state.ActiveClip);
            int count = _curveCache.Bindings.Length;
            _curveCache.Curves = new AnimationCurve[count];
            for (int i = 0; i < count; i++)
                _curveCache.Curves[i] = AnimationUtility.GetEditorCurve(_state.ActiveClip, _curveCache.Bindings[i]);
        }

        private bool HasProperties()
        {
            return _state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null
                   || _state.Mode == TimelineMode.HonamiState && _state.SelectedState != null
                   || _state.Mode == TimelineMode.HonamiClipEdit && _state.ActiveClip != null;
        }

        private List<RowData> GetRows()
        {
            _cachedRows.Clear();

            switch (_state.Mode)
            {
                case TimelineMode.HonamiTimeline:
                    CollectTimelineRows();
                    break;
                case TimelineMode.HonamiClipEdit:
                    CollectClipEditRows();
                    break;
                default:
                    CollectStateRows();
                    break;
            }
            return _cachedRows;
        }

        private void CollectTimelineRows()
        {
            if (_state.ActiveTimeline == null) return;
            var tracks = _state.ActiveTimeline.tracks;
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                _cachedRows.Add(new RowData
                {
                    Index = i,
                    Title = string.IsNullOrEmpty(track.trackName) ? track.trackType.ToString() : track.trackName,
                    Track = track,
                    IconName = track.trackType == HonamiTimelineTrackType.Animation ? "AnimationClip Icon" : "AnimationWindowEvent Icon",
                    Color = track.trackType == HonamiTimelineTrackType.Animation ? TimelineTheme.AnimationTrack : TimelineTheme.EventTrack,
                    IsSelected = _state.SelectedTimelineTrack == track
                });
            }
        }

        private void CollectClipEditRows()
        {
            if (_state.ActiveClip == null) return;

            EnsureCurveCache();

            var bindings = _curveCache.Bindings;
            string filter = _state.ClipEditFilter;
            bool hasFilter = !string.IsNullOrEmpty(filter);

            if (!hasFilter && bindings.Length > 0)
            {
                _cachedRows.Add(new RowData
                {
                    Index = 0,
                    Title = "Summary",
                    Color = TimelineTheme.KeyframeFill,
                    IsSummaryRow = true,
                    BindingStart = 0,
                    BindingCount = bindings.Length
                });
            }

            int boneStart = 0;
            while (boneStart < bindings.Length)
            {
                string path = bindings[boneStart].path;
                int boneEnd = boneStart;
                while (boneEnd < bindings.Length && bindings[boneEnd].path == path) boneEnd++;
                int boneCount = boneEnd - boneStart;

                string boneName = string.IsNullOrEmpty(path) ? "root" : System.IO.Path.GetFileName(path);
                bool boneNameMatches = !hasFilter || boneName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;

                bool boneVisible = boneNameMatches;
                if (hasFilter && !boneVisible)
                {
                    for (int j = boneStart; j < boneEnd && !boneVisible; j++)
                        if (bindings[j].propertyName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            boneVisible = true;
                }

                if (!boneVisible)
                {
                    boneStart = boneEnd;
                    continue;
                }

                bool boneExpanded = hasFilter || _state.ExpandedClipBones.Contains(path);

                _cachedRows.Add(new RowData
                {
                    Index = _cachedRows.Count,
                    Title = boneName,
                    IconName = "Transform Icon",
                    Color = TimelineTheme.BoneGroup,
                    IsGroup = true,
                    IsBoneGroup = true,
                    BonePath = path,
                    GroupId = "bone:" + path,
                    IsExpanded = boneExpanded,
                    Depth = 0,
                    BindingStart = boneStart,
                    BindingCount = boneCount
                });

                if (boneExpanded)
                    CollectBoneProperties(bindings, boneStart, boneEnd, boneNameMatches, filter, hasFilter);

                boneStart = boneEnd;
            }
        }

        private void CollectBoneProperties(EditorCurveBinding[] bindings, int boneStart, int boneEnd, bool boneNameMatches, string filter, bool hasFilter)
        {
            int j = boneStart;
            while (j < boneEnd)
            {
                var b = bindings[j];
                string prop = b.propertyName;
                bool isVector = prop.StartsWith("m_LocalPosition") || prop.StartsWith("m_LocalRotation") || prop.StartsWith("m_LocalScale");

                if (isVector)
                {
                    string baseProp = prop.Substring(0, prop.LastIndexOf('.'));
                    int count = 1;
                    for (int k = j + 1; k < boneEnd; k++)
                    {
                        if (bindings[k].propertyName.StartsWith(baseProp)) count++;
                        else break;
                    }

                    bool propMatches = boneNameMatches || !hasFilter ||
                                       baseProp.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!propMatches) { j += count; continue; }

                    string propDisplay = baseProp == "m_LocalPosition" ? "Position" :
                                         baseProp == "m_LocalRotation" ? "Rotation" : "Scale";
                    string groupId = b.path + ":" + baseProp;
                    bool isExpanded = _state.ExpandedClipGroups.Contains(groupId);

                    _cachedRows.Add(new RowData
                    {
                        Index = _cachedRows.Count,
                        Title = propDisplay,
                        Color = PropertyColor(baseProp),
                        IsGroup = true,
                        GroupId = groupId,
                        IsExpanded = isExpanded,
                        Depth = 1,
                        BindingStart = j,
                        BindingCount = count
                    });

                    if (isExpanded)
                    {
                        for (int k = 0; k < count; k++)
                        {
                            var sub = bindings[j + k];
                            string axis = sub.propertyName.Substring(sub.propertyName.LastIndexOf('.') + 1).ToUpperInvariant();
                            _cachedRows.Add(new RowData
                            {
                                Index = _cachedRows.Count,
                                Title = axis,
                                Color = AxisColor(axis),
                                Depth = 2,
                                BindingStart = j + k,
                                BindingCount = 1
                            });
                        }
                    }
                    j += count;
                }
                else
                {
                    bool propMatches = boneNameMatches || !hasFilter ||
                                       prop.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!propMatches) { j++; continue; }

                    bool isBlendShape = prop.StartsWith("blendShape.");
                    string display = isBlendShape ? prop.Substring("blendShape.".Length) : prop;
                    _cachedRows.Add(new RowData
                    {
                        Index = _cachedRows.Count,
                        Title = display,
                        Color = isBlendShape ? new Color(0.82f, 0.45f, 0.82f) : new Color(0.4f, 0.68f, 0.95f),
                        Depth = 1,
                        BindingStart = j,
                        BindingCount = 1
                    });
                    j++;
                }
            }
        }

        private static Color PropertyColor(string baseProp) => baseProp switch
        {
            "m_LocalPosition" => new Color(0.55f, 0.78f, 0.55f),
            "m_LocalRotation" => new Color(0.55f, 0.66f, 0.85f),
            "m_LocalScale" => new Color(0.85f, 0.72f, 0.5f),
            _ => new Color(0.72f, 0.72f, 0.76f)
        };

        private static Color AxisColor(string axis) => axis switch
        {
            "X" => new Color(0.90f, 0.35f, 0.38f),
            "Y" => new Color(0.45f, 0.82f, 0.42f),
            "Z" => new Color(0.36f, 0.55f, 0.95f),
            _ => new Color(0.78f, 0.72f, 0.40f)
        };

        private void CollectStateRows()
        {
            int index = 0;
            if (_state.ShowAnimTrack)
            {
                if (_state.IsRandomState && _state.RandomNode.randomClips != null)
                {
                    var clips = _state.RandomNode.randomClips;
                    for (int i = 0; i < clips.Count; i++)
                        _cachedRows.Add(new RowData { Index = index++, Title = clips[i].clip != null ? clips[i].clip.name : $"Empty Slot {i}", IconName = "AnimationClip Icon", Color = TimelineTheme.RandomTrack });
                }
                else if (_state.IsBlendState && _state.BlendNode.blendMotions != null)
                {
                    var motions = _state.BlendNode.blendMotions;
                    for (int i = 0; i < motions.Count; i++)
                    {
                        var motion = motions[i];
                        string name = motion.clip != null ? motion.clip.name : $"Empty Motion {i}";
                        _cachedRows.Add(new RowData { Index = index++, Title = $"{name}  ({motion.threshold:F2})", IconName = "AnimationClip Icon", Color = TimelineTheme.PreviewTrack });
                    }
                }
                else if (_state.IsSeqState && _state.SeqNode.sequencedClips != null)
                {
                    var clips = _state.SeqNode.sequencedClips;
                    for (int i = 0; i < clips.Count; i++)
                        _cachedRows.Add(new RowData { Index = index++, Title = clips[i].clip != null ? clips[i].clip.name : $"Empty Sequence {i}", IconName = "AnimationClip Icon", Color = TimelineTheme.SequencerTrack });
                }
                else
                {
                    _cachedRows.Add(new RowData { Index = index++, Title = "Animation Clip", IconName = "AnimationClip Icon", Color = TimelineTheme.AnimationTrack });
                }
            }
            if (_state.ShowLocalEventsTrack) _cachedRows.Add(new RowData { Index = index++, Title = "Local Events", IconName = "AnimationWindowEvent Icon", Color = TimelineTheme.EventTrack });
            if (_state.ShowGlobalEventsTrack) _cachedRows.Add(new RowData { Index = index, Title = "Global Events", IconName = "AudioSource Icon", Color = TimelineTheme.GlobalEventTrack });
        }

        private float DisplayFps()
        {
            if (_state.Mode == TimelineMode.HonamiClipEdit)
                return _state.ActiveClip != null ? _state.ActiveClip.frameRate : 0f;

            var clip = _state.AnimNode?.clip;
            if (clip == null && _state.IsRandomState && _state.RandomNode.randomClips != null
                && _state.RandomPreviewIdx >= 0 && _state.RandomPreviewIdx < _state.RandomNode.randomClips.Count)
                clip = _state.RandomNode.randomClips[_state.RandomPreviewIdx].clip;
            if (clip == null && _state.IsBlendState && _state.BlendNode.blendMotions != null)
            {
                var motions = _state.BlendNode.blendMotions;
                for (int i = 0; i < motions.Count; i++)
                {
                    if (motions[i].clip != null)
                    {
                        clip = motions[i].clip;
                        break;
                    }
                }
            }
            return clip != null ? clip.frameRate : 0f;
        }

        private static VisualElement RectElement(float x, float y, float width, float height, Color color)
        {
            return new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = x,
                    top = y,
                    width = width,
                    height = height,
                    backgroundColor = color
                }
            };
        }

        private static void IgnorePicking(VisualElement element)
        {
            if (element != null)
                element.pickingMode = PickingMode.Ignore;
        }

        private static VisualElement ColorBar(Color color)
        {
            return new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    width = 4,
                    height = Length.Percent(100),
                    backgroundColor = color
                }
            };
        }

        private static VisualElement RowIcon(string iconName)
        {
            return new Image
            {
                image = EditorGUIUtility.IconContent(iconName).image,
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 13,
                    top = 12,
                    width = 16,
                    height = 16
                }
            };
        }

        private static Label RowLabel(string text)
        {
            return new Label(text)
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 36,
                    right = 30,
                    top = 10,
                    height = 20,
                    color = TimelineTheme.Text,
                    fontSize = 12,
                    overflow = Overflow.Hidden
                }
            };
        }

        private Button MuteButton(HonamiTimelineTrack track)
        {
            var button = new Button(() =>
            {
                HonamiEditorController.ToggleTrackMute(_state.ActiveTimeline, track);
                _rebuild();
            })
            {
                text = track.muted ? "M" : "V",
                tooltip = track.muted ? "Muted" : "Visible"
            };
            button.style.position = Position.Absolute;
            button.style.right = 6;
            button.style.top = 9;
            button.style.width = 22;
            button.style.height = 22;
            button.style.backgroundColor = TimelineTheme.ToolbarButton;
            button.style.borderTopWidth = button.style.borderRightWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = 1;
            button.style.borderTopColor = button.style.borderRightColor = button.style.borderBottomColor = button.style.borderLeftColor = TimelineTheme.SubtleLine;
            button.style.color = TimelineTheme.Text;
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = TimelineTheme.ToolbarButtonHot);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = TimelineTheme.ToolbarButton);
            button.RegisterCallback<PointerDownEvent>(_ => button.style.backgroundColor = TimelineTheme.ToolbarButtonPressed);
            button.RegisterCallback<PointerUpEvent>(_ => button.style.backgroundColor = TimelineTheme.ToolbarButtonHot);
            return button;
        }
    }
}
#endif
