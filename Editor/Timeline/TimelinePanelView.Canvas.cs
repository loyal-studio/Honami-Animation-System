using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView : ITimelineTrackDrawer, ITimelineItemViewHost
    {
        private const float MarkerLaneHeight = 16f;
        private const float RulerLabelTop = MarkerLaneHeight + 2f;
        private const float MajorTickHeight = 12f;
        private const float MinorTickHeight = 6f;
        private const float MinLabelSpacing = 70f;
        private const float MinMinorSpacing = 7f;
        private const float AddModuleRowHeight = 30f;
        private const float MinItemDisplayDuration = 0.1f;

        private static readonly float[] SecondSteps = { 1f, 2f, 5f, 10f, 15f, 30f, 60f, 120f, 300f, 600f, 1800f, 3600f };
        private static readonly float[] FrameSteps = { 1f, 2f, 5f, 10f };
        private static readonly float[] FractionSteps = { 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.5f };
        private static readonly int[] FrameCountSteps = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 };

        private static readonly Color MinorTick = new(1f, 1f, 1f, 0.16f);
        private static readonly Color MajorTick = new(1f, 1f, 1f, 0.34f);
        private static readonly Color MajorGridLine = new(0f, 0f, 0f, 0.38f);
        private static readonly Color MinorGridLine = new(0f, 0f, 0f, 0.14f);
        private static readonly Color PastEndShade = new(0f, 0f, 0f, 0.22f);
        private static readonly Color LockedHatch = new(1f, 1f, 1f, 0.07f);

        private const float RulerLabelFontSize = 10f;
        private const int MaxQuadsPerMesh = ushort.MaxValue / 4;

        private int _rowCount;
        private readonly List<(Rect rect, Color32 color)> _quadBuffer = new();
        private readonly List<float> _tickCandidates = new();
        private readonly List<float> _snapPointsBuffer = new();
        private bool _draggingItemHandle;
        private bool _draggingMarker;

        private bool HasAddModuleRow => _state.Mode != TimelineMode.HonamiClipEdit && _state.ActiveContainer != null;

        private bool IsInteractingWithCanvas() =>
            _itemDrag != null || _draggingItemHandle || _draggingMarker || _draggingPlayhead || _state.IsBoxSelecting || _state.IsDraggingTrack
            || _state.IsResizingPropsPanel || _state.IsResizingTrackHeader || _panning;

        #region Ticks

        private void ComputeTickSpacing(out float major, out float minor)
        {
            float scale = Mathf.Max(0.0001f, _state.TimeScale);
            float fps = Mathf.Max(1f, _state.FrameRate);
            var candidates = _tickCandidates;
            candidates.Clear();

            switch (_state.EffectiveTimeFormat)
            {
                case TimelineTimeFormat.Frames:
                    foreach (int frames in FrameCountSteps) candidates.Add(frames / fps);
                    break;
                case TimelineTimeFormat.Seconds:
                    candidates.AddRange(FractionSteps);
                    candidates.AddRange(SecondSteps);
                    break;
                default:
                    foreach (float frames in FrameSteps)
                    {
                        if (frames < fps) candidates.Add(frames / fps);
                    }
                    candidates.AddRange(SecondSteps);
                    break;
            }

            major = candidates[candidates.Count - 1];
            int majorIndex = candidates.Count - 1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] * scale < MinLabelSpacing) continue;
                major = candidates[i];
                majorIndex = i;
                break;
            }

            minor = major;
            for (int i = majorIndex - 1; i >= 0; i--)
            {
                float candidate = candidates[i];
                if (candidate * scale < MinMinorSpacing) break;
                float ratio = major / candidate;
                if (Mathf.Abs(ratio - Mathf.Round(ratio)) > 0.001f) continue;
                minor = candidate;
            }
        }

        private int _timeAxisSignature;

        // Drags can move the end or markers without a rebuild, and the cached ruler and grid would not follow on their own.
        public void RefreshTimeAxisIfChanged()
        {
            int signature = TimeAxisSignature();
            if (signature == _timeAxisSignature) return;
            _timeAxisSignature = signature;
            _rulerTicks.MarkDirtyRepaint();
            _gridBackground.MarkDirtyRepaint();
        }

        private int TimeAxisSignature()
        {
            if (IsStateEmpty()) return 0;

            var hash = new HashCode();
            hash.Add(_state.GetDuration());
            hash.Add(_state.FrameRate);
            hash.Add(_state.EffectiveTimeFormat);
            hash.Add(_rowCount);

            if (_state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null)
            {
                var markers = _state.ActiveTimeline.Markers;
                for (int i = 0; i < markers.Count; i++)
                {
                    var marker = markers[i];
                    if (marker is HonamiTimelineLoopMarker loop)
                    {
                        hash.Add(loop.time);
                        hash.Add(loop.endTime);
                    }
                    else if (marker is HonamiTimelineCueMarker cue)
                    {
                        hash.Add(cue.time);
                        hash.Add(cue.color);
                    }
                }
            }
            return hash.ToHashCode();
        }

        private void AddQuad(float x, float y, float width, float height, Color color) =>
            _quadBuffer.Add((new Rect(x, y, width, height), color));

        // All ticks and rows go into one cached mesh, which is only rebuilt when the element is marked dirty.
        private void FlushQuads(MeshGenerationContext mesh)
        {
            int count = Mathf.Min(_quadBuffer.Count, MaxQuadsPerMesh);
            if (count > 0)
            {
                var data = mesh.Allocate(count * 4, count * 6);
                for (int i = 0; i < count; i++)
                {
                    var (rect, color) = _quadBuffer[i];
                    data.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = color });
                    data.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = color });
                    data.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = color });
                    data.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = color });

                    ushort v = (ushort)(i * 4);
                    data.SetNextIndex(v);
                    data.SetNextIndex((ushort)(v + 1));
                    data.SetNextIndex((ushort)(v + 2));
                    data.SetNextIndex(v);
                    data.SetNextIndex((ushort)(v + 2));
                    data.SetNextIndex((ushort)(v + 3));
                }
            }
            _quadBuffer.Clear();
        }

        private void DrawRulerTicks(MeshGenerationContext mesh)
        {
            var rect = _rulerTicks.contentRect;
            if (rect.width < 1f || IsStateEmpty()) return;

            float scale = _state.TimeScale;
            float scrollX = _state.ScrollPos.x;
            float height = rect.height;
            float fps = Mathf.Max(1f, _state.FrameRate);

            float endX = _state.GetDuration() * scale - scrollX;
            if (endX < rect.width)
                AddQuad(Mathf.Max(0f, endX), MarkerLaneHeight, rect.width - Mathf.Max(0f, endX), height - MarkerLaneHeight, PastEndShade);

            ComputeTickSpacing(out float major, out float minor);

            bool labelFrames = major < 0.999f;
            long first = (long)Math.Floor(scrollX / scale / minor);
            for (long index = first; ; index++)
            {
                float time = index * minor;
                float x = Mathf.Round(time * scale - scrollX);
                if (x > rect.width) break;
                if (time < 0f) continue;

                float majorRatio = time / major;
                bool isMajor = Mathf.Abs(majorRatio - Mathf.Round(majorRatio)) < 0.001f;
                if (isMajor) AddQuad(x, height - MajorTickHeight, 1f, MajorTickHeight, MajorTick);
                else AddQuad(x, height - MinorTickHeight, 1f, MinorTickHeight, MinorTick);
            }

            AddQuad(0f, MarkerLaneHeight, rect.width, 1f, new Color(0f, 0f, 0f, 0.25f));
            FlushQuads(mesh);

            var font = _rulerTicks.resolvedStyle.unityFontDefinition.fontAsset;
            for (long index = (long)Math.Floor(scrollX / scale / major); ; index++)
            {
                float time = index * major;
                float x = Mathf.Round(time * scale - scrollX);
                if (x > rect.width) break;
                if (time < 0f) continue;
                mesh.DrawText(TimelineTimecode.FormatRulerLabel(time, fps, _state.EffectiveTimeFormat, labelFrames), new Vector2(x + 3f, RulerLabelTop), RulerLabelFontSize, TimelineTheme.MutedText, font);
            }
        }

        private void DrawGridBackground(MeshGenerationContext mesh)
        {
            var rect = _gridBackground.contentRect;
            AddQuad(rect.x, rect.y, rect.width, rect.height, TimelineTheme.TrackBgB);
            if (rect.width < 1f || IsStateEmpty())
            {
                FlushQuads(mesh);
                return;
            }

            float scale = _state.TimeScale;
            float scrollX = _state.ScrollPos.x;
            float scrollY = _state.ScrollPos.y;
            float rowHeight = TimelineTheme.RowHeight;

            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollY / rowHeight));
            for (int i = firstRow; i < _rowCount; i++)
            {
                float y = i * rowHeight - scrollY;
                if (y > rect.height) break;
                AddQuad(0f, y, rect.width, rowHeight, i % 2 == 0 ? TimelineTheme.TrackBgA : TimelineTheme.TrackBgB);
                AddQuad(0f, y + rowHeight - 1f, rect.width, 1f, TimelineTheme.Divider);
            }

            ComputeTickSpacing(out float major, out float minor);
            long first = (long)Math.Floor(scrollX / scale / minor);
            bool drawMinor = minor * scale >= 14f;
            for (long index = first; ; index++)
            {
                float time = index * minor;
                float x = Mathf.Round(time * scale - scrollX);
                if (x > rect.width) break;
                if (time < 0f) continue;
                float majorRatio = time / major;
                bool isMajor = Mathf.Abs(majorRatio - Mathf.Round(majorRatio)) < 0.001f;
                if (isMajor) AddQuad(x, 0f, 1f, rect.height, MajorGridLine);
                else if (drawMinor) AddQuad(x, 0f, 1f, rect.height, MinorGridLine);
            }

            if (_state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null)
            {
                foreach (var marker in _state.ActiveTimeline.Markers)
                {
                    if (marker is HonamiTimelineLoopMarker loop)
                    {
                        float x0 = loop.time * scale - scrollX;
                        float x1 = loop.endTime * scale - scrollX;
                        var band = TimelineTheme.Accent;
                        band.a = 0.05f;
                        AddQuad(x0, 0f, x1 - x0, rect.height, band);
                    }
                    else if (marker is HonamiTimelineCueMarker cue)
                    {
                        var line = cue.color;
                        line.a = 0.5f;
                        AddQuad(Mathf.Round(cue.time * scale - scrollX), 0f, 1f, rect.height, line);
                    }
                }
            }

            float endX = _state.GetDuration() * scale - scrollX;
            if (endX < rect.width)
            {
                AddQuad(Mathf.Max(0f, endX), 0f, rect.width - Mathf.Max(0f, endX), rect.height, PastEndShade);
                AddQuad(endX, 0f, 1f, rect.height, TimelineTheme.EndBoundary);
            }
            FlushQuads(mesh);
        }

        #endregion

        #region Ruler

        private void RegisterRulerEvents(VisualElement ruler)
        {
            ruler.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (IsStateEmpty()) return;
                _focus();
                Focus();
                float time = TimeAtCanvasX(_rulerContent.WorldToLocal(evt.position).x);

                if (evt.button == 1)
                {
                    ShowRulerMenu(Mathf.Max(0f, time));
                    evt.StopPropagation();
                    return;
                }
                if (evt.button != 0) return;

                if (evt.clickCount == 2 && _state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null)
                {
                    var marker = HonamiEditorController.AddCueMarker(_state.ActiveTimeline, SnapTime(Mathf.Max(0f, time), null, ShouldSnap(evt.shiftKey)));
                    _state.ClearSelection();
                    _state.SelectedMarkers.Add(marker);
                    _rebuild();
                    evt.StopPropagation();
                    return;
                }

                _draggingPlayhead = true;
                _state.IsDraggingPlayhead = true;
                ruler.CapturePointer(evt.pointerId);
                SetPlayheadFromLocal(_rulerContent.WorldToLocal(evt.position).x, false, evt.shiftKey);
                evt.StopPropagation();
            });
            ruler.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingPlayhead || !ruler.HasPointerCapture(evt.pointerId)) return;
                SetPlayheadFromLocal(_rulerContent.WorldToLocal(evt.position).x, false, evt.shiftKey);
                evt.StopPropagation();
            });
            ruler.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!ruler.HasPointerCapture(evt.pointerId)) return;
                ruler.ReleasePointer(evt.pointerId);
                StopSelection();
                evt.StopPropagation();
            });
        }

        private void ShowRulerMenu(float time)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Move Playhead Here"), false, () =>
            {
                _state.PlayheadTime = Mathf.Clamp(time, 0f, _state.GetDuration());
                _rebuild();
            });

            if (_state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null)
            {
                var timeline = _state.ActiveTimeline;
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Add Marker  (M)"), false, () =>
                {
                    var marker = HonamiEditorController.AddCueMarker(timeline, time);
                    _state.ClearSelection();
                    _state.SelectedMarkers.Add(marker);
                    _rebuild();
                });
                menu.AddItem(new GUIContent("Add Wait"), false, () =>
                {
                    var marker = HonamiEditorController.AddWaitMarker(timeline, time);
                    _state.ClearSelection();
                    _state.SelectedMarkers.Add(marker);
                    _rebuild();
                });
                menu.AddItem(new GUIContent("Add Runtime Loop Region"), false, () =>
                {
                    float end = time + 2f;
                    var marker = HonamiEditorController.AddRuntimeLoopMarker(timeline, time, end);
                    _state.ClearSelection();
                    _state.SelectedMarkers.Add(marker);
                    _rebuild();
                });
                menu.AddItem(new GUIContent("Set Loop In  (I)"), false, () => SetLoopPoint(time, true));
                menu.AddItem(new GUIContent("Set Loop Out  (O)"), false, () => SetLoopPoint(time, false));
                if (timeline.Markers.Count > 0)
                {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Delete All Markers"), false, () =>
                    {
                        HonamiEditorController.DeleteMarkers(timeline, new List<HonamiTimelineMarker>(timeline.Markers));
                        _state.SelectedMarkers.Clear();
                        _rebuild();
                    });
                }
            }

            if (_state.Mode != TimelineMode.HonamiTimeline)
            {
                menu.ShowAsContext();
                return;
            }

            menu.AddSeparator("");
            foreach (TimelineTimeFormat format in Enum.GetValues(typeof(TimelineTimeFormat)))
            {
                var captured = format;
                menu.AddItem(new GUIContent($"Display/{ObjectNames.NicifyVariableName(format.ToString())}"), _state.TimeFormat == format, () =>
                {
                    _state.TimeFormat = captured;
                    _state.SaveSettings();
                    _rebuild();
                });
            }
            menu.ShowAsContext();
        }

        private void DrawRulerMarkers()
        {
            if (_state.Mode != TimelineMode.HonamiTimeline || _state.ActiveTimeline == null) return;

            foreach (var marker in _state.ActiveTimeline.Markers)
            {
                switch (marker)
                {
                    case HonamiTimelineLoopMarker loop:
                        AddLoopMarkerElement(loop);
                        break;
                    case HonamiTimelineCueMarker cue:
                        AddCueMarkerElement(cue);
                        break;
                    case HonamiTimelineWaitMarker wait:
                        AddWaitMarkerElement(wait);
                        break;
                }
            }
        }

        private void AddCueMarkerElement(HonamiTimelineCueMarker cue)
        {
            bool selected = _state.SelectedMarkers.Contains(cue);
            float x = cue.time * _state.TimeScale;
            var flag = new VisualElement
            {
                tooltip = string.IsNullOrEmpty(cue.note) ? $"{cue.id}\n{FormatTime(cue.time)}" : $"{cue.id}\n{FormatTime(cue.time)}\n{cue.note}",
                style =
                {
                    position = Position.Absolute,
                    left = x - 1f,
                    top = 2,
                    height = MarkerLaneHeight - 3f,
                    flexDirection = FlexDirection.Row
                }
            };
            var pole = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { width = 9, height = Length.Percent(100), backgroundColor = cue.color }
            };
            pole.style.borderTopRightRadius = pole.style.borderBottomRightRadius = 4;
            if (selected) pole.Border(1.5f, Color.white);
            flag.Add(pole);
            var label = new Label(cue.id)
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = 9,
                    color = selected ? Color.white : TimelineTheme.Text,
                    marginLeft = 2,
                    paddingTop = 0,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            flag.Add(label);
            RegisterMarkerDrag(flag, cue, null);
            _rulerContent.Add(flag);
            _markerElements[cue] = flag;
            DecorateMarkerLink(flag, cue);
        }

        private void AddLoopMarkerElement(HonamiTimelineLoopMarker loop)
        {
            bool selected = _state.SelectedMarkers.Contains(loop);
            float x0 = loop.time * _state.TimeScale;
            float x1 = loop.endTime * _state.TimeScale;
            var loopColor = loop.mode switch
            {
                HonamiTimelineLoopMode.RuntimeLoop => TimelineTheme.Accent,
                HonamiTimelineLoopMode.RuntimeLoopUntilSignal => TimelineTheme.RandomTrack,
                _ => TimelineTheme.EndBoundary
            };
            var color = selected ? TimelineTheme.Accent : loopColor;
            var bar = RectElement(x0, 3f, Mathf.Max(8f, x1 - x0), 12f, color);
            bar.Radius(3);
            bar.style.opacity = 0.85f;
            string runtime = loop.mode == HonamiTimelineLoopMode.PreviewOnly ? "Preview only" : loop.mode == HonamiTimelineLoopMode.RuntimeLoop ? "Runtime loop" : $"Runtime loop until '{loop.signal}'";
            bar.tooltip = $"{loop.id}: {FormatTime(loop.time)} - {FormatTime(loop.endTime)}\n{runtime}";
            RegisterMarkerDrag(bar, loop, null);

            var modeLabel = new Label(loop.mode == HonamiTimelineLoopMode.PreviewOnly ? "LOOP" : loop.mode == HonamiTimelineLoopMode.RuntimeLoop ? "∞ LOOP" : "LOOP · WAIT")
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 7, right = 7, top = 0, bottom = 0, fontSize = 8, color = Color.white, unityTextAlign = TextAnchor.MiddleCenter, overflow = Overflow.Hidden }
            };
            bar.Add(modeLabel);

            var inHandle = RectElement(0f, 0f, 6f, 12f, new Color(1f, 1f, 1f, 0.35f));
            inHandle.AddManipulator(new HonamiTimelineItemView.CursorManipulator(MouseCursor.ResizeHorizontal));
            RegisterMarkerDrag(inHandle, loop, true);
            bar.Add(inHandle);

            var outHandle = RectElement(0f, 0f, 6f, 12f, new Color(1f, 1f, 1f, 0.35f));
            outHandle.style.left = StyleKeyword.Auto;
            outHandle.style.right = 0;
            outHandle.AddManipulator(new HonamiTimelineItemView.CursorManipulator(MouseCursor.ResizeHorizontal));
            RegisterMarkerDrag(outHandle, loop, false);
            bar.Add(outHandle);

            _rulerContent.Add(bar);
            _markerElements[loop] = bar;
            DecorateMarkerLink(bar, loop);
        }

        private void AddWaitMarkerElement(HonamiTimelineWaitMarker wait)
        {
            bool selected = _state.SelectedMarkers.Contains(wait);
            float x = wait.time * _state.TimeScale;
            var flag = new VisualElement
            {
                tooltip = $"{wait.id}: wait for '{wait.signal}'\n{FormatTime(wait.time)}",
                style = { position = Position.Absolute, left = x - 3f, top = 1, width = 16, height = MarkerLaneHeight - 2f, backgroundColor = new Color(0.68f, 0.45f, 0.96f) }
            };
            flag.Radius(3);
            if (selected) flag.Border(1.5f, Color.white);
            flag.Add(new Label("W")
            {
                pickingMode = PickingMode.Ignore,
                style = { color = Color.white, fontSize = 9, unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 }
            });
            RegisterMarkerDrag(flag, wait, null);
            _rulerContent.Add(flag);
            _markerElements[wait] = flag;
            DecorateMarkerLink(flag, wait);
        }

        // edge: null drags the whole marker, true the loop start, false the loop end.
        private void RegisterMarkerDrag(VisualElement element, HonamiTimelineMarker marker, bool? edge)
        {
            float pointerStart = 0f;
            float timeStart = 0f;
            float endStart = 0f;
            int undoGroup = -1;
            bool moved = false;
            // Linked markers move as a group when the marker is dragged by its body, not by a loop edge.
            // The group can also contain clips, which follow the same shift.
            var peers = new List<HonamiTimelineMarker>();
            var peerStarts = new List<float>();
            var peerEnds = new List<float>();
            var itemPeers = new List<HonamiTimelineItem>();
            var itemStarts = new List<float>();
            bool recordedItems = false;

            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    SelectOnly(_state.SelectedMarkers, marker, false, keepIfSelected: true);
                    ShowMarkerMenu(marker);
                    evt.StopPropagation();
                    return;
                }
                if (evt.button != 0) return;
                _focus();
                Focus();

                if (evt.clickCount == 2)
                {
                    _state.PlayheadTime = marker.time;
                    _rebuild();
                    evt.StopPropagation();
                    return;
                }

                bool additive = evt.shiftKey || evt.ctrlKey || evt.commandKey;
                if (!additive) _state.ClearSelection();
                _state.ClearSelectedGap();
                SelectOnly(_state.SelectedMarkers, marker, true, keepIfSelected: !additive);
                pointerStart = TimeAtCanvasX(_rulerContent.WorldToLocal(evt.position).x);
                timeStart = marker.time;
                endStart = marker is HonamiTimelineLoopMarker loop ? loop.endTime : marker.time;
                moved = false;

                peers.Clear();
                peerStarts.Clear();
                peerEnds.Clear();
                itemPeers.Clear();
                itemStarts.Clear();
                recordedItems = false;
                if (edge == null)
                {
                    peers.Add(marker);
                    ExpandLinkGroup(itemPeers, peers);
                    foreach (var peer in peers)
                    {
                        peerStarts.Add(peer.time);
                        peerEnds.Add(peer is HonamiTimelineLoopMarker linkedLoop ? linkedLoop.endTime : peer.time);
                    }
                    foreach (var item in itemPeers) itemStarts.Add(GetStartTime(item));
                }
                _draggingMarker = true;
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                element.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingMarker || !element.HasPointerCapture(evt.pointerId)) return;
                float delta = TimeAtCanvasX(_rulerContent.WorldToLocal(evt.position).x) - pointerStart;
                if (!moved && Mathf.Abs(delta * _state.TimeScale) < 2f) return;
                moved = true;

                Undo.RecordObject(_state.ActiveTimeline, "Move Marker");
                bool snap = ShouldSnap(evt.shiftKey);
                if (marker is HonamiTimelineLoopMarker loop && edge.HasValue)
                {
                    if (edge.Value) loop.time = Mathf.Clamp(SnapTime(timeStart + delta, marker, snap), 0f, loop.endTime - 1f / _state.FrameRate);
                    else loop.endTime = Mathf.Max(SnapTime(endStart + delta, marker, snap), loop.time + 1f / _state.FrameRate);
                }
                else
                {
                    float start = Mathf.Max(0f, SnapTime(timeStart + delta, marker, snap));
                    float shift = start - timeStart;

                    // Keep clips in the group from being pushed before zero when the marker is the later member.
                    if (itemPeers.Count > 0)
                    {
                        float earliestItem = float.MaxValue;
                        foreach (float itemStart in itemStarts) earliestItem = Mathf.Min(earliestItem, itemStart);
                        shift = Mathf.Max(shift, -earliestItem);
                        start = timeStart + shift;
                    }

                    marker.time = start;
                    if (marker is HonamiTimelineLoopMarker body) body.endTime = start + (endStart - timeStart);

                    for (int i = 1; i < peers.Count; i++)
                    {
                        var peer = peers[i];
                        float peerStart = Mathf.Max(0f, peerStarts[i] + shift);
                        peer.time = peerStart;
                        if (peer is HonamiTimelineLoopMarker peerLoop)
                            peerLoop.endTime = peerStart + (peerEnds[i] - peerStarts[i]);
                        if (_markerElements.TryGetValue(peer, out var peerElement))
                            RepositionRulerMarker(peerElement, peer, null);
                    }

                    for (int i = 0; i < itemPeers.Count; i++)
                    {
                        if (!recordedItems)
                        {
                            var container = _state.ActiveContainer;
                            if (container != null) HonamiEditorController.RecordUndo(container, "Move Linked Items");
                            recordedItems = true;
                        }

                        SetStartTime(itemPeers[i], itemStarts[i] + shift);
                        if (_elements.TryGetValue(itemPeers[i], out var itemElement))
                        {
                            float left = GetStartTime(itemPeers[i]) * _state.TimeScale;
                            itemElement.style.left = itemPeers[i] is HonamiTimelineItem { IsInstant: true } ? left - 8f : left;
                        }
                    }
                }
                EditorUtility.SetDirty(_state.ActiveTimeline);
                UpdateSnapLineVisual();
                _gridBackground.MarkDirtyRepaint();
                RepositionRulerMarker(element, marker, edge);
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!element.HasPointerCapture(evt.pointerId)) return;
                element.ReleasePointer(evt.pointerId);
                _draggingMarker = false;
                if (undoGroup >= 0) Undo.CollapseUndoOperations(undoGroup);
                if (itemPeers.Count > 0)
                {
                    var container = _state.ActiveContainer;
                    if (container != null) HonamiEditorController.MarkModulesChanged(container);
                }
                _state.SnapLineTime = -1f;
                _rebuild();
                evt.StopPropagation();
            });
        }

        private void RepositionRulerMarker(VisualElement element, HonamiTimelineMarker marker, bool? edge)
        {
            float scale = _state.TimeScale;
            var target = edge.HasValue ? element.parent : element;
            if (target == null) return;
            if (marker is HonamiTimelineLoopMarker loop)
            {
                target.style.left = loop.time * scale;
                target.style.width = Mathf.Max(8f, (loop.endTime - loop.time) * scale);
            }
            else
            {
                target.style.left = marker.time * scale - 1f;
            }
        }

        private void ShowMarkerMenu(HonamiTimelineMarker marker)
        {
            var timeline = _state.ActiveTimeline;
            if (timeline == null) return;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Move Playhead To Marker"), false, () =>
            {
                _state.PlayheadTime = marker.time;
                _rebuild();
            });
            menu.AddItem(new GUIContent("Delete Marker"), false, () =>
            {
                HonamiEditorController.DeleteMarkers(timeline, new List<HonamiTimelineMarker> { marker });
                _state.SelectedMarkers.Remove(marker);
                _rebuild();
            });

            // Markers count together with any selected items, so a LOOP or WAIT marker can be linked
            // straight to the clip it belongs to.
            bool canLink = _state.SelectedMarkers.Count + _state.SelectedTimelineItems.Count >= 2;
            bool hasLinked = HasLinkedSelection();

            menu.AddSeparator("");
            AddMenuItem(menu, "Link Selected  (Ctrl+L)", canLink, () => LinkSelection(false));
            AddMenuItem(menu, "Link And Sequence Selected  (Ctrl+Shift+L)", canLink, () => LinkSelection(true));
            AddMenuItem(menu, "Unlink Selected", hasLinked, UnlinkSelection);
            AddMenuItem(menu, "Select Linked Group", hasLinked, SelectLinkedGroup);

            menu.ShowAsContext();
            _rebuild();
        }

        private void SetLoopPoint(float time, bool isIn)
        {
            var timeline = _state.ActiveTimeline;
            if (timeline == null) return;

            HonamiTimelineLoopMarker loop = null;
            foreach (var marker in timeline.Markers)
            {
                if (marker is HonamiTimelineLoopMarker candidate)
                {
                    loop = candidate;
                    break;
                }
            }

            float frame = 1f / Mathf.Max(1f, _state.FrameRate);
            if (loop == null)
            {
                float start = isIn ? time : Mathf.Max(0f, time - 2f);
                float end = isIn ? Mathf.Max(time + 2f, time + frame) : time;
                HonamiEditorController.AddLoopMarker(timeline, start, end);
            }
            else
            {
                Undo.RecordObject(timeline, isIn ? "Set Loop In" : "Set Loop Out");
                if (isIn) loop.time = Mathf.Min(time, loop.endTime - frame);
                else loop.endTime = Mathf.Max(time, loop.time + frame);
                EditorUtility.SetDirty(timeline);
            }
            _rebuild();
        }

        #endregion

        #region Rows

        private VisualElement BuildAddModuleRow(float y)
        {
            var row = new VisualElement
            {
                tooltip = "Add a module: animation, camera shots, audio, events, activation, screen fade...",
                style = { position = Position.Absolute, left = 0, right = 0, top = y, height = AddModuleRowHeight }
            };

            var plus = new Label("+")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 13,
                    width = 16,
                    top = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    color = TimelineTheme.MutedText
                }
            };
            var label = new Label("Add Module")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 36,
                    right = 8,
                    top = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    fontSize = 12,
                    color = TimelineTheme.MutedText
                }
            };
            row.Add(plus);
            row.Add(label);

            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                row.style.backgroundColor = Color.Lerp(TimelineTheme.HeaderBg, Color.white, 0.04f);
                plus.style.color = label.style.color = TimelineTheme.Accent;
            });
            row.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                row.style.backgroundColor = Color.clear;
                plus.style.color = label.style.color = TimelineTheme.MutedText;
            });
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _focus();
                var menu = new GenericMenu();
                AddModuleTypeItems(menu, string.Empty);
                menu.DropDown(row.worldBound);
                evt.StopPropagation();
            });
            return row;
        }

        private void DrawRows()
        {
            var rows = _cachedRows;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float y = RowY(i);
                Color headerBg = row.IsSelected ? TimelineTheme.HeaderSelected
                    : row.IsBoneGroup ? TimelineTheme.BoneGroupHeaderBg
                    : TimelineTheme.HeaderBg;
                var header = new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = 0,
                        right = 0,
                        top = y,
                        height = TimelineTheme.RowHeight,
                        backgroundColor = headerBg,
                        borderBottomWidth = 1,
                        borderBottomColor = TimelineTheme.Divider
                    }
                };
                header.userData = row.Index;
                RegisterRowHeaderEvents(header, row);
                _headers.Add(header);

                if (_state.Mode == TimelineMode.HonamiClipEdit)
                {
                    FillClipChannelHeader(header, row);
                    continue;
                }

                header.Add(ColorBar(row.Color));

                float labelLeft = 12f + row.Depth * 15f;

                if (row.IsGroup)
                {
                    var foldout = new Label(row.IsExpanded ? "▼" : "▶")
                    {
                        pickingMode = PickingMode.Ignore,
                        style = { position = Position.Absolute, left = labelLeft, top = 12, fontSize = 9, color = TimelineTheme.MutedText, unityFontStyleAndWeight = FontStyle.Bold }
                    };
                    header.Add(foldout);
                    header.RegisterCallback<PointerEnterEvent>(_ => foldout.style.color = Color.white);
                    header.RegisterCallback<PointerLeaveEvent>(_ => foldout.style.color = TimelineTheme.MutedText);
                    labelLeft += 15f;
                }

                var rowIcon = row.Icon != null ? row.Icon
                    : !string.IsNullOrEmpty(row.IconName) ? EditorGUIUtility.IconContent(row.IconName).image : null;
                if (rowIcon != null)
                {
                    var icon = RowIcon(rowIcon);
                    icon.style.left = labelLeft;
                    header.Add(icon);
                    labelLeft += 20f;
                }

                var label = RowLabel(row.Title);
                label.style.left = labelLeft;
                if (row.IsBoneGroup) label.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.Add(label);
                if (row.Module != null)
                {
                    var module = row.Module;
                    if (module.muted || row.IsDimmed) label.style.color = TimelineTheme.MutedText;
                    if (row.IsDimmed) header.tooltip = "Its clip is not the one being previewed.";

                    float right = 6f;
                    header.Add(HeaderToggle(module.muted ? "d_scenevis_hidden_hover" : "d_scenevis_visible_hover", module.muted,
                        module.muted ? "Muted: this module does nothing. Click to enable it." : "Enabled. Click to mute this module.", right,
                        () => ModifyModules(container => HonamiEditorController.ToggleModuleMute(container, module))));
                    right += 24f;
                    header.Add(HeaderToggle(module.locked ? "InspectorLock" : "IN LockButton", module.locked,
                        module.locked ? "Locked: items cannot be moved, trimmed or deleted. Click to unlock." : "Click to lock this module against edits.", right,
                        () => ModifyModules(container => HonamiEditorController.SetModuleLocked(container, module, !module.locked))));
                    right += 24f;

                    if (HostItemsOf(module).Count > 0)
                    {
                        header.Add(AddNestedModuleButton("Add a module inside this module's items", rect => ShowModuleNestMenu(module, rect), right));
                        right += 24f;
                    }
                    if (!string.IsNullOrEmpty(row.Problem))
                    {
                        var problemIcon = ProblemIcon(row.Problem);
                        problemIcon.style.right = right + 2f;
                        header.Add(problemIcon);
                        right += 22f;
                    }
                    label.style.right = right;
                }
                else if (row.IsNodeRow && row.MotionIndex >= 0)
                {
                    label.style.right = 30;
                    var choice = MotionChoice(row.MotionIndex);
                    header.Add(AddNestedModuleButton($"Add a module that runs inside {choice.Label}", rect => ShowNestedModuleMenu(choice, rect), 6f));
                }
            }

            if (HasAddModuleRow)
                _headers.Add(BuildAddModuleRow(RowY(rows.Count)));

            if (_state.IsDraggingTrack && _state.DragTrackInsertIndex >= 0)
            {
                int draggedRow = RowOfModule(_state.DraggedTrack);
                float insertY = RowY(_state.DragTrackInsertIndex) + (_state.DragTrackInsertIndex > draggedRow ? TimelineTheme.RowHeight : 0f);
                var insertLine = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = 0, right = 0, top = insertY - 1, height = 2, backgroundColor = TimelineTheme.Accent, opacity = 0.8f }
                };
                _headers.Add(insertLine);
            }
        }

        private VisualElement HeaderToggle(string iconName, bool active, string tooltip, float right, Action toggle)
        {
            var button = new VisualElement
            {
                tooltip = tooltip,
                style =
                {
                    position = Position.Absolute,
                    right = right,
                    top = (TimelineTheme.RowHeight - 22f) * 0.5f,
                    width = 22,
                    height = 22,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            button.Radius(3);

            var icon = new Image
            {
                image = EditorGUIUtility.IconContent(iconName).image,
                pickingMode = PickingMode.Ignore,
                style = { width = 16, height = 16, opacity = active ? 1f : 0.45f }
            };
            if (active) icon.tintColor = TimelineTheme.Accent;
            button.Add(icon);

            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                button.style.backgroundColor = TimelineTheme.ToolbarButtonHot;
                icon.style.opacity = 1f;
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                button.style.backgroundColor = Color.clear;
                icon.style.opacity = active ? 1f : 0.45f;
            });
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                toggle();
                _rebuild();
                evt.StopPropagation();
            });
            return button;
        }

        private int RowOfModule(HonamiTimelineModule module)
        {
            for (int i = 0; i < _cachedRows.Count; i++)
            {
                if (_cachedRows[i].Module == module) return i;
            }
            return -1;
        }

        private bool InSameGroup(int rowIndex, string groupKey) =>
            _cachedRows[rowIndex].Module != null && _cachedRows[rowIndex].GroupKey == groupKey;

        private void GetModuleGroupRange(string groupKey, out int first, out int last)
        {
            first = -1;
            last = -1;
            for (int i = 0; i < _cachedRows.Count; i++)
            {
                if (!InSameGroup(i, groupKey)) continue;
                if (first < 0) first = i;
                last = i;
            }
        }

        // Siblings are not always adjacent: a sibling's own nested modules can sit between them.
        private int NearestGroupRow(string groupKey, int first, int last, float y)
        {
            int best = first;
            float bestDistance = float.MaxValue;
            for (int i = first; i <= last; i++)
            {
                if (!InSameGroup(i, groupKey)) continue;
                float distance = Mathf.Abs(RowY(i) - y);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        private VisualElement AddNestedModuleButton(string tooltip, Action<Rect> onClick, float right)
        {
            var button = new Label("+")
            {
                tooltip = tooltip,
                style =
                {
                    position = Position.Absolute,
                    right = right,
                    top = (TimelineTheme.RowHeight - 22f) * 0.5f,
                    width = 22,
                    height = 22,
                    fontSize = 15,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = TimelineTheme.MutedText
                }
            };
            button.Radius(3);
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                button.style.backgroundColor = TimelineTheme.ToolbarButtonHot;
                button.style.color = TimelineTheme.Accent;
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                button.style.backgroundColor = Color.clear;
                button.style.color = TimelineTheme.MutedText;
            });
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _focus();
                onClick(button.worldBound);
                evt.StopPropagation();
            });
            return button;
        }

        private void RegisterRowHeaderEvents(VisualElement header, RowData row)
        {
            header.RegisterCallback<PointerDownEvent>(evt =>
            {
                _focus();

                if (row.IsGroup)
                {
                    if (row.IsBoneGroup)
                    {
                        if (_state.ExpandedClipBones.Contains(row.BonePath)) _state.ExpandedClipBones.Remove(row.BonePath);
                        else _state.ExpandedClipBones.Add(row.BonePath);
                    }
                    else
                    {
                        if (row.IsExpanded) _state.ExpandedClipGroups.Remove(row.GroupId);
                        else _state.ExpandedClipGroups.Add(row.GroupId);
                    }
                    _rebuild();
                    evt.StopPropagation();
                    return;
                }

                if (row.IsNodeRow && row.MotionIndex >= 0 && evt.button == 1)
                {
                    ShowNestedModuleMenu(MotionChoice(row.MotionIndex));
                    evt.StopPropagation();
                    return;
                }

                if (row.Module != null)
                {
                    _state.SelectedModule = row.Module;
                    _state.SelectedTimelineItems.Clear();
                    _state.SelectedMarkers.Clear();
                    _state.ClearSelectedGap();

                    if (evt.button == 1)
                    {
                        _rebuild();
                        ShowModuleHeaderMenu(row.Module, header);
                    }
                    else if (evt.clickCount == 2)
                    {
                        BeginModuleRename(row.Module, header);
                    }
                    else
                    {
                        _state.IsDraggingTrack = true;
                        _state.DraggedTrack = row.Module;
                        _state.DragTrackStartY = RowY(row.Index);
                        _state.DragTrackStartMouseY = evt.position.y;
                        _state.DragTrackInsertIndex = row.Index;
                        header.CapturePointer(evt.pointerId);
                        RefreshHeaderSelection();
                    }
                }
                evt.StopPropagation();
            });

            header.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_state.IsDraggingTrack || _state.DraggedTrack != row.Module || !header.HasPointerCapture(evt.pointerId)) return;

                float deltaY = evt.position.y - _state.DragTrackStartMouseY;
                if (Mathf.Abs(deltaY) < 4f) return;

                GetModuleGroupRange(row.GroupKey, out int first, out int last);
                if (first < 0) return;
                float newY = Mathf.Clamp(_state.DragTrackStartY + deltaY, RowY(first), RowY(last));

                header.style.top = newY;
                header.BringToFront();
                header.style.opacity = 0.8f;

                int insert = NearestGroupRow(row.GroupKey, first, last, newY);
                if (insert != _state.DragTrackInsertIndex)
                {
                    _state.DragTrackInsertIndex = insert;
                    UpdateTrackInsertLine(row.Module);
                }
                evt.StopPropagation();
            });

            header.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_state.IsDraggingTrack && _state.DraggedTrack == row.Module)
                {
                    _state.IsDraggingTrack = false;
                    header.ReleasePointer(evt.pointerId);

                    int insertIndex = _state.DragTrackInsertIndex;
                    var slotModule = insertIndex >= 0 && insertIndex < _cachedRows.Count && InSameGroup(insertIndex, row.GroupKey) ? _cachedRows[insertIndex].Module : null;
                    if (slotModule != null && slotModule != row.Module)
                        ModifyModules(target => HonamiEditorController.MoveModule(target, row.Module, target.Modules.IndexOf(slotModule)));
                    _state.DraggedTrack = null;
                    _state.DragTrackInsertIndex = -1;
                    _rebuild();
                }
                evt.StopPropagation();
            });

            header.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!_state.IsDraggingTrack)
                    header.style.backgroundColor = Color.Lerp(row.IsSelected ? TimelineTheme.HeaderSelected : TimelineTheme.HeaderBg, Color.white, 0.05f);
            });
            header.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (!_state.IsDraggingTrack)
                    header.style.backgroundColor = row.IsSelected ? TimelineTheme.HeaderSelected : TimelineTheme.HeaderBg;
            });
        }

        private VisualElement _trackInsertLine;

        private void UpdateTrackInsertLine(HonamiTimelineModule dragged)
        {
            if (_trackInsertLine == null || _trackInsertLine.parent != _headers)
            {
                _trackInsertLine = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = 0, right = 0, height = 2, backgroundColor = TimelineTheme.Accent }
                };
                _headers.Add(_trackInsertLine);
            }
            int draggedRow = RowOfModule(dragged);
            int index = _state.DragTrackInsertIndex;
            _trackInsertLine.style.top = RowY(index) + (index > draggedRow ? TimelineTheme.RowHeight : 0f) - 1f;
            _trackInsertLine.BringToFront();
        }

        private void RefreshHeaderSelection()
        {
            foreach (var child in _headers.Children())
            {
                if (child.userData is not int index || index < 0 || index >= _cachedRows.Count) continue;
                var row = _cachedRows[index];
                if (row.Module == null) continue;
                child.style.backgroundColor = row.Module == _state.SelectedModule ? TimelineTheme.HeaderSelected : TimelineTheme.HeaderBg;
            }
        }

        private static VisualElement ProblemIcon(string problem)
        {
            var icon = new Image
            {
                image = EditorGUIUtility.IconContent("console.warnicon.sml").image,
                tooltip = char.ToUpperInvariant(problem[0]) + problem.Substring(1),
                style = { position = Position.Absolute, right = 32, top = (TimelineTheme.RowHeight - 16f) * 0.5f, width = 16, height = 16 }
            };
            return icon;
        }

        #endregion

        #region Content

        private void DrawContent()
        {
            _state.TimelineItemRects.Clear();
            _state.SeqClipRects.Clear();
            _state.KeyframeRects.Clear();

            if (_state.Mode == TimelineMode.HonamiClipEdit)
            {
                DrawClipEditContent(0f);
                return;
            }

            var nodeEditor = _state.Mode == TimelineMode.HonamiState && _state.node != null
                ? HonamiNodeEditorRegistry.GetEditor(_state.node)
                : null;

            for (int i = 0; i < _cachedRows.Count; i++)
            {
                var row = _cachedRows[i];
                float y = RowY(i);
                if (row.IsNodeRow)
                    nodeEditor?.DrawTimelineRow(_state, this, row.NodeRow, y);
                else if (row.Module != null)
                    DrawModuleItems(row.Module, y, row.IsDimmed);
            }

            DrawGapSelection();
            BringSelectedElementsToFront();
        }

        private void DrawGapSelection()
        {
            if (!_state.HasSelectedGap) return;
            int rowIndex = RowOfModule(_state.SelectedGapModule);
            if (rowIndex < 0) return;

            var fill = TimelineTheme.Accent;
            fill.a = 0.12f;
            float x = _state.SelectedGapStart * _state.TimeScale;
            float width = Mathf.Max(2f, (_state.SelectedGapEnd - _state.SelectedGapStart) * _state.TimeScale);
            var gap = RectElement(x, RowY(rowIndex) + 3f, width, TimelineTheme.RowHeight - 6f, fill);
            gap.Radius(3).Border(1f, TimelineTheme.AccentDim);
            IgnorePicking(gap);
            _canvas.Add(gap);
        }

        private void DrawModuleItems(HonamiTimelineModule module, float y, bool dimmed)
        {
            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
            var context = CreateEditContext(module);
            float offset = context.TimeOffset;
            bool locked = _state.IsModuleLocked(module);
            int count = module.ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = module.GetItem(i);
                if (item == null) continue;

                bool selected = _state.SelectedTimelineItems.Contains(item);
                string label = editor.GetItemLabel(context, item);
                Color color = editor.GetItemColor(item);
                if (module.muted || dimmed) color.a *= 0.4f;

                if (item.IsInstant)
                    AddEventElement(item, label, editor.GetItemIcon(item), editor.ItemDisplayName, offset + item.time, y, selected, color, locked);
                else
                    AddClipBlock(item, label, offset + item.time, Mathf.Max(item.Duration, MinItemDisplayDuration), y, selected, color, locked);

                if (_elements.TryGetValue(item, out var element))
                {
                    editor.DecorateItem(new HonamiTimelineItemView(this, context, item, element));
                    DecorateLink(element, item);
                }
            }
        }

        internal HonamiTimelineEditContext CreateEditContext(HonamiTimelineModule module, float? playheadTime = null)
        {
            float offset = _state.GetModuleTimeOffset(module);
            return new(_state.ActiveContainer, module,
                _state.Mode == TimelineMode.HonamiTimeline ? _state.ResolveDirector() : null,
                _state.Mode == TimelineMode.HonamiState ? _state.ResolvePreviewRoot() : null,
                offset,
                (playheadTime ?? _state.PlayheadTime) - offset,
                _state.SelectedTimelineItems,
                () => { _state.PostModifyState(); _rebuild(); },
                OpenClipEdit,
                _state.FrameRate);
        }

        float ITimelineItemViewHost.PixelsPerSecond => _state.TimeScale;

        float ITimelineItemViewHost.SnapItemTime(float time, HonamiTimelineItem ignore, bool shiftHeld)
        {
            float snapped = SnapTime(Mathf.Max(0f, time), ignore, ShouldSnap(shiftHeld));
            UpdateSnapLineVisual();
            return snapped;
        }

        void ITimelineItemViewHost.ClearSnap()
        {
            _state.SnapLineTime = -1f;
            UpdateSnapLineVisual();
        }

        void ITimelineItemViewHost.SetHandleDragging(bool dragging) => _draggingItemHandle = dragging;

        float ITimelineItemViewHost.TimeAtWorldX(float worldX) => TimeAtCanvasX(_canvas.WorldToLocal(new Vector2(worldX, 0f)).x);

        bool ITimelineItemViewHost.IsLocked(HonamiTimelineModule module) => _state.IsModuleLocked(module);

        void ITimelineItemViewHost.SelectItem(HonamiTimelineItem item, bool additive)
        {
            if (additive) ToggleTarget(item);
            else if (!IsTargetSelected(item)) SelectTarget(item, false);
            RefreshSelectionVisuals();
            UpdatePropertiesPanel(PropertiesVisible());
        }

        void ITimelineItemViewHost.BringItemToFront(HonamiTimelineItem item)
        {
            if (_elements.TryGetValue(item, out var element) && element.parent == _canvas)
            {
                element.BringToFront();
                BringOverlaysToFront();
            }
        }

        #endregion

        #region Node editor drawing

        public void AddReadonlyClip(AnimationClip clipAsset, string label, float start, float duration, float y, Color color)
        {
            var clip = ClipBlock(label, start, duration, y, false, color, false);
            clip.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2 && clipAsset != null)
                    OpenClipEdit(clipAsset);
                if (evt.button == 0) evt.StopPropagation();
            });
            _canvas.Add(clip);
        }

        public void AddClipWithHandles(
            int dragId,
            string label,
            float start,
            float duration,
            float speed,
            float y,
            Color themeColor,
            Action<float> onTrimStart,
            Action<float> onTrimEnd,
            Action onDoubleClick,
            Action onDragEnd,
            float maxDuration = 1000f)
        {
            var body = ClipBlock(label, start / speed, duration / speed, y, false, themeColor, false);
            body.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                if (evt.clickCount == 2)
                {
                    onDoubleClick?.Invoke();
                    _rebuild();
                }
                evt.StopPropagation();
            });
            _canvas.Add(body);

            const float handleWidth = 8f;
            float top = y + (TimelineTheme.RowHeight - TimelineTheme.ClipHeight) * 0.5f;
            var startHandle = TrimHandle((start / speed) * _state.TimeScale, top, $"Start: {start:F3}s. Drag to trim");
            startHandle.style.borderTopLeftRadius = startHandle.style.borderBottomLeftRadius = 4;
            _canvas.Add(startHandle);

            var endHandle = TrimHandle(((start + duration) / speed) * _state.TimeScale - handleWidth, top, $"End: {start + duration:F3}s. Drag to trim");
            endHandle.style.borderTopRightRadius = endHandle.style.borderBottomRightRadius = 4;
            _canvas.Add(endHandle);

            startHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _focus();
                _state.IsDraggingRandomAnimStart = true;
                _state.RandomDragIndex = dragId;
                _state.AnimDragOrigVal = start;
                _state.AnimDragStartMouseTime = TimeAtPointer(evt) * speed;
                startHandle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            startHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_state.IsDraggingRandomAnimStart || _state.RandomDragIndex != dragId) return;
                float delta = TimeAtPointer(evt) * speed - _state.AnimDragStartMouseTime;
                float stop = start + duration;
                float newStart = Mathf.Clamp(_state.AnimDragOrigVal + delta, 0f, stop - 0.01f);
                newStart = SnapTime(newStart / speed, null, ShouldSnap(evt.shiftKey)) * speed;
                onTrimStart?.Invoke(newStart);

                startHandle.style.left = (newStart / speed) * _state.TimeScale;
                float newDur = Mathf.Max(0f, stop - newStart);
                body.style.left = (newStart / speed) * _state.TimeScale;
                body.style.width = Mathf.Max(1f, (newDur / speed) * _state.TimeScale);
                startHandle.tooltip = $"Start: {newStart:F3}s";
                UpdateSnapLineVisual();
                evt.StopPropagation();
            });
            startHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                _state.PostModifyState();
                _state.IsDraggingRandomAnimStart = false;
                _state.SnapLineTime = -1f;
                startHandle.ReleasePointer(evt.pointerId);
                onDragEnd?.Invoke();
                _rebuild();
                evt.StopPropagation();
            });

            endHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _focus();
                _state.IsDraggingRandomAnimEnd = true;
                _state.RandomDragIndex = dragId;
                _state.AnimDragOrigVal = start + duration;
                _state.AnimDragStartMouseTime = TimeAtPointer(evt) * speed;
                endHandle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            endHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_state.IsDraggingRandomAnimEnd || _state.RandomDragIndex != dragId) return;
                float delta = TimeAtPointer(evt) * speed - _state.AnimDragStartMouseTime;
                float newEnd = Mathf.Clamp(_state.AnimDragOrigVal + delta, start + 0.01f, maxDuration);
                newEnd = SnapTime(newEnd / speed, null, ShouldSnap(evt.shiftKey)) * speed;
                onTrimEnd?.Invoke(newEnd);

                float newDur = Mathf.Max(0f, newEnd - start);
                body.style.width = Mathf.Max(1f, (newDur / speed) * _state.TimeScale);
                endHandle.style.left = ((start + newDur) / speed) * _state.TimeScale - handleWidth;
                endHandle.tooltip = $"End: {newEnd:F3}s";
                UpdateSnapLineVisual();
                evt.StopPropagation();
            });
            endHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                _state.PostModifyState();
                _state.IsDraggingRandomAnimEnd = false;
                _state.SnapLineTime = -1f;
                endHandle.ReleasePointer(evt.pointerId);
                onDragEnd?.Invoke();
                _rebuild();
                evt.StopPropagation();
            });
        }

        private static VisualElement TrimHandle(float x, float top, string tooltip)
        {
            var handle = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = x,
                    top = top,
                    width = 8f,
                    height = TimelineTheme.ClipHeight,
                    backgroundColor = new Color(1f, 1f, 1f, 0.14f)
                },
                tooltip = tooltip
            };
            handle.RegisterCallback<PointerEnterEvent>(_ => handle.style.backgroundColor = new Color(1f, 1f, 1f, 0.36f));
            handle.RegisterCallback<PointerLeaveEvent>(_ => handle.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f));
            handle.AddManipulator(new HonamiTimelineItemView.CursorManipulator(MouseCursor.ResizeHorizontal));
            return handle;
        }

        public void AddClipElement(object target, string label, float start, float duration, float y, bool selected, Color color) =>
            AddClipBlock(target, label, start, duration, y, selected, color, false);

        #endregion

        #region Item visuals

        private void AddClipBlock(object target, string label, float start, float duration, float y, bool selected, Color color, bool locked)
        {
            var clip = ClipBlock(label, start, duration, y, selected, color, locked);
            clip.userData = color;
            _elements[target] = clip;
            RegisterItemInteraction(clip, target);

            var rect = new Rect(start * _state.TimeScale, y + (TimelineTheme.RowHeight - TimelineTheme.ClipHeight) * 0.5f, Mathf.Max(1f, duration * _state.TimeScale), TimelineTheme.ClipHeight);
            if (target is HonamiTimelineItem ti) _state.TimelineItemRects[ti] = rect;
            else if (target is HonamiSequencedAnimationClip sc) _state.SeqClipRects[sc] = rect;

            _canvas.Add(clip);
        }

        private VisualElement ClipBlock(string label, float start, float duration, float y, bool selected, Color color, bool locked)
        {
            float paddingY = (TimelineTheme.RowHeight - TimelineTheme.ClipHeight) * 0.5f;
            var root = RectElement(start * _state.TimeScale, y + paddingY, Mathf.Max(1f, duration * _state.TimeScale), TimelineTheme.ClipHeight, color);
            root.Radius(6);
            root.style.overflow = Overflow.Hidden;
            root.tooltip = label;
            StyleClipBlock(root, color, selected, false);

            var text = new Label(label)
            {
                name = "clip-label",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 10,
                    right = 8,
                    top = (TimelineTheme.ClipHeight - 16f) * 0.5f,
                    height = 16,
                    color = locked ? new Color(1f, 1f, 1f, 0.55f) : Color.white,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };
            root.Add(text);

            if (locked)
            {
                var hatch = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
                };
                hatch.generateVisualContent += DrawHatch;
                root.Add(hatch);
            }

            root.RegisterCallback<PointerEnterEvent>(_ => StyleClipBlock(root, (Color)(root.userData ?? color), IsElementSelected(root), true));
            root.RegisterCallback<PointerLeaveEvent>(_ => StyleClipBlock(root, (Color)(root.userData ?? color), IsElementSelected(root), false));
            return root;
        }

        private static void StyleClipBlock(VisualElement block, Color color, bool selected, bool hovered)
        {
            var fill = selected ? TimelineTheme.Selected : color;
            if (hovered) fill = Color.Lerp(fill, Color.white, 0.1f);
            block.style.backgroundColor = fill;
            block.Border(selected ? 2f : 1f, selected ? Color.white : new Color(0f, 0f, 0f, 0.5f));
        }

        private bool IsElementSelected(VisualElement element)
        {
            foreach (var pair in _elements)
            {
                if (pair.Value == element) return IsTargetSelected(pair.Key);
            }
            return false;
        }

        private static void DrawHatch(MeshGenerationContext mesh)
        {
            var element = mesh.visualElement;
            var rect = element.contentRect;
            var painter = mesh.painter2D;
            painter.strokeColor = LockedHatch;
            painter.lineWidth = 1.5f;
            for (float x = -rect.height; x < rect.width; x += 9f)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.height));
                painter.LineTo(new Vector2(x + rect.height, 0f));
                painter.Stroke();
            }
        }

        private void AddEventElement(object target, string label, Texture icon, string kind, float time, float y, bool selected, Color color, bool locked)
        {
            Color background = Color.Lerp(TimelineTheme.PanelBg, color, selected ? 0.55f : 0.22f);
            var marker = RectElement(time * _state.TimeScale - 8, y + 10, 16, 20, background);
            marker.userData = color;
            _elements[target] = marker;
            marker.Radius(3);
            marker.Border(selected ? 2 : 1, selected ? TimelineTheme.Accent : color);
            marker.tooltip = $"{kind}: {label}\n{FormatTime(time)}";
            if (locked) marker.style.opacity = 0.6f;

            if (icon != null)
            {
                var glyph = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    style = { position = Position.Absolute, left = 1, right = 1, top = 3, bottom = 3 }
                };
                IgnorePicking(glyph);
                marker.Add(glyph);
            }
            else
            {
                var letter = new Label(string.IsNullOrEmpty(kind) ? "?" : kind.Substring(0, 1))
                {
                    style =
                    {
                        flexGrow = 1,
                        color = color,
                        fontSize = 10,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                IgnorePicking(letter);
                marker.Add(letter);
            }

            if (!string.IsNullOrEmpty(label) && _state.TimeScale >= 60f)
            {
                var caption = new Label(label)
                {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        position = Position.Absolute,
                        left = 18,
                        top = 2,
                        fontSize = 10,
                        color = TimelineTheme.Text,
                        whiteSpace = WhiteSpace.NoWrap,
                        overflow = Overflow.Visible
                    }
                };
                marker.style.overflow = Overflow.Visible;
                marker.Add(caption);
            }

            RegisterItemInteraction(marker, target);

            var rect = new Rect(time * _state.TimeScale - 8, y + 10, 16, 20);
            if (target is HonamiTimelineItem ti) _state.TimelineItemRects[ti] = rect;

            _canvas.Add(marker);
        }

        private void RefreshSelectionVisuals()
        {
            RefreshSelectionVisualsOnly();
            BringSelectedElementsToFront();
        }

        private void RefreshSelectionVisualsOnly()
        {
            foreach (var pair in _elements)
            {
                bool selected = IsTargetSelected(pair.Key);
                var element = pair.Value;
                var color = element.userData is Color c ? c : TimelineTheme.AnimationTrack;
                if (pair.Key is HonamiTimelineItem { IsInstant: true })
                {
                    element.style.backgroundColor = Color.Lerp(TimelineTheme.PanelBg, color, selected ? 0.55f : 0.22f);
                    element.Border(selected ? 2 : 1, selected ? TimelineTheme.Accent : color);
                }
                else
                {
                    StyleClipBlock(element, color, selected, false);
                }
            }
        }

        private void BringOverlaysToFront()
        {
            if (_playheadBand != null && _playheadBand.parent == _canvas) _playheadBand.BringToFront();
            if (_dropGhost != null && _dropGhost.parent == _canvas) _dropGhost.BringToFront();
            if (_bladeLine != null && _bladeLine.parent == _canvas) _bladeLine.BringToFront();
            if (_snapLine != null && _snapLine.parent == _canvas) _snapLine.BringToFront();
            if (_playheadLine != null && _playheadLine.parent == _canvas) _playheadLine.BringToFront();
            if (_boxSelection != null && _boxSelection.parent == _canvas) _boxSelection.BringToFront();
        }

        private void BringSelectedElementsToFront()
        {
            foreach (var item in _state.SelectedTimelineItems)
            {
                if (_elements.TryGetValue(item, out var element) && element.parent == _canvas)
                    element.BringToFront();
            }

            foreach (var clip in _state.SelectedSeqClips)
            {
                if (_elements.TryGetValue(clip, out var element) && element.parent == _canvas)
                    element.BringToFront();
            }

            BringOverlaysToFront();
        }

        private bool IsTargetSelected(object target) => target switch
        {
            HonamiTimelineItem item => _state.SelectedTimelineItems.Contains(item),
            HonamiSequencedAnimationClip clip => _state.SelectedSeqClips.Contains(clip),
            _ => false
        };

        #endregion

        #region Playhead and snapping

        private void DrawPlayhead()
        {
            float x = _state.PlayheadTime * _state.TimeScale;
            _playheadLine = RectElement(x - 1, 0, 2, _canvasHeight, TimelineTheme.Playhead);
            IgnorePicking(_playheadLine);
            _canvas.Add(_playheadLine);

            _playheadHead = RectElement(x - 6, TimelineTheme.RulerHeight - 18f, 12, 16, TimelineTheme.Playhead);
            _playheadHead.Radius(3);
            IgnorePicking(_playheadHead);
            _rulerContent.Add(_playheadHead);

            var stem = RectElement(x - 1, TimelineTheme.RulerHeight - 2f, 2, 2, TimelineTheme.Playhead);
            IgnorePicking(stem);
            _rulerContent.Add(stem);
            _playheadStem = stem;
        }

        private VisualElement _playheadStem;

        private void UpdatePlayheadVisual()
        {
            float x = _state.PlayheadTime * _state.TimeScale;
            if (_playheadLine != null) _playheadLine.style.left = x - 1;
            if (_playheadHead != null) _playheadHead.style.left = x - 6;
            if (_playheadStem != null) _playheadStem.style.left = x - 1;
            UpdateCurrentFrameBand();
            UpdateSnapLineVisual();
            RefreshCornerTimecode();
        }

        private void DrawSnapLine()
        {
            if (_state.SnapLineTime < 0f) return;
            UpdateSnapLineVisual();
        }

        private void UpdateSnapLineVisual()
        {
            if (_state.SnapLineTime < 0f)
            {
                if (_snapLine != null)
                    _snapLine.style.display = DisplayStyle.None;
                return;
            }

            if (_snapLine == null || _snapLine.parent != _canvas)
            {
                _snapLine = RectElement(0f, 0f, 1f, _canvasHeight, TimelineTheme.Accent);
                _snapLine.style.opacity = 0.9f;
                IgnorePicking(_snapLine);
                _canvas.Add(_snapLine);
            }

            _snapLine.style.display = DisplayStyle.Flex;
            _snapLine.style.left = _state.SnapLineTime * _state.TimeScale;
            _snapLine.BringToFront();
        }

        private string FormatTime(float time) => TimelineTimecode.Format(time, _state.FrameRate, _state.EffectiveTimeFormat);

        private float PointerXInCanvas(IPointerEvent evt) => _canvas.WorldToLocal(evt.position).x;

        private float TimeAtPointer(IPointerEvent evt) => TimeAtCanvasX(PointerXInCanvas(evt));

        private float TimeAtCanvasX(float x) => x / _state.TimeScale;

        private void SetPlayheadFromLocal(float localX, bool rebuild = true, bool shiftHeld = false)
        {
            float time = SnapTime(localX / _state.TimeScale, null, ShouldSnap(shiftHeld));
            _state.PlayheadTime = Mathf.Clamp(time, 0f, _state.GetDuration());
            UpdatePlayheadVisual();
            RefreshTimeLabel();
            if (rebuild)
                _rebuild();
        }

        private bool ShouldSnap(bool shiftHeld) => _state.SnapEnabled != shiftHeld;

        private float SnapThreshold => 10f / Mathf.Max(1f, _state.TimeScale);

        private float SnapTime(float time, object ignore, bool useSnap)
        {
            if (!useSnap)
            {
                _state.SnapLineTime = -1f;
                return time;
            }

            CollectSnapPoints(_snapPointsBuffer, ignore);
            if (!_state.IsDraggingPlayhead)
                _snapPointsBuffer.Add(_state.PlayheadTime);

            float threshold = SnapThreshold;
            float best = time;
            float minDistance = float.MaxValue;
            foreach (float point in _snapPointsBuffer)
            {
                float distance = Mathf.Abs(time - point);
                if (distance < threshold && distance < minDistance)
                {
                    minDistance = distance;
                    best = point;
                }
            }

            if (minDistance < threshold)
            {
                _state.SnapLineTime = best;
                return best;
            }

            _state.SnapLineTime = -1f;
            return _state.SnapToFrame(time);
        }

        private void CollectSnapPoints(List<float> points, object ignore)
        {
            points.Clear();
            points.Add(0f);
            points.Add(_state.GetDuration());
            AddStateSnapPoints(points, ignore);
            AddTimelineSnapPoints(points, ignore);
        }

        private void AddStateSnapPoints(List<float> points, object ignore)
        {
            if (_state.IsSeqState && _state.SeqNode?.sequencedClips != null)
            {
                foreach (var clip in _state.SeqNode.sequencedClips)
                {
                    if (ReferenceEquals(clip, ignore) || _state.SelectedSeqClips.Contains(clip) || clip.clip == null) continue;
                    float speed = clip.speed != 0 ? Mathf.Abs(clip.speed) : 1f;
                    points.Add(clip.startTime);
                    points.Add(clip.startTime + clip.clip.length / speed);
                }
            }
        }

        private void AddTimelineSnapPoints(List<float> points, object ignore)
        {
            var modules = _state.ActiveModules;
            if (modules == null) return;

            foreach (var module in modules)
            {
                if (module == null) continue;
                float offset = _state.GetModuleTimeOffset(module);
                int count = module.ItemCount;
                for (int i = 0; i < count; i++)
                {
                    var item = module.GetItem(i);
                    if (item == null || ReferenceEquals(item, ignore) || _state.SelectedTimelineItems.Contains(item)) continue;
                    points.Add(offset + item.time);
                    if (!item.IsInstant)
                        points.Add(offset + item.EndTime);
                }
            }

            if (_state.Mode != TimelineMode.HonamiTimeline || _state.ActiveTimeline == null) return;
            foreach (var marker in _state.ActiveTimeline.Markers)
            {
                if (ReferenceEquals(marker, ignore) || _state.SelectedMarkers.Contains(marker)) continue;
                points.Add(marker.time);
                if (marker is HonamiTimelineLoopMarker loop)
                    points.Add(loop.endTime);
            }
        }

        private float GetStartTime(object target)
        {
            return target switch
            {
                HonamiSequencedAnimationClip clip => clip.startTime,
                HonamiTimelineItem item => _state.GetItemTimeOffset(item) + item.time,
                _ => 0f
            };
        }

        private float GetEndTime(object target)
        {
            return target switch
            {
                HonamiSequencedAnimationClip clip => clip.startTime + (clip.clip != null ? clip.clip.length / (clip.speed != 0f ? Mathf.Abs(clip.speed) : 1f) : 0f),
                HonamiTimelineItem item => _state.GetItemTimeOffset(item) + item.EndTime,
                _ => 0f
            };
        }

        private void SetStartTime(object target, float value)
        {
            switch (target)
            {
                case HonamiSequencedAnimationClip clip:
                    Undo.RecordObject(_state.SeqNode, "Move Sequenced Clip");
                    clip.startTime = value;
                    EditorUtility.SetDirty(_state.SeqNode);
                    break;
                case HonamiTimelineItem item:
                    if (_state.ActiveContainer != null)
                        HonamiEditorController.MoveTimelineItem(_state.ActiveContainer, item, value - _state.GetItemTimeOffset(item));
                    break;
            }
        }

        private static void SelectOnly<T>(List<T> list, T item, bool additive, bool keepIfSelected = false)
        {
            if (keepIfSelected && list.Contains(item)) return;
            if (!additive) list.Clear();
            if (list.Contains(item)) list.Remove(item);
            else list.Add(item);
        }

        private void OpenClipEdit(AnimationClip clip)
        {
            _state.Mode = TimelineMode.HonamiClipEdit;
            _state.ActiveClip = clip;
            _rebuild();
        }

        #endregion
    }
}
