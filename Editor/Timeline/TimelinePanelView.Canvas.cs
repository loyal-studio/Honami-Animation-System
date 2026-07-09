#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView : ITimelineTrackDrawer
    {
        private int _rowCount;

        private void DrawTimelineOverlay(float height)
        {
            float rulerHeight = TimelineTheme.RulerHeight;

            var rulerContainer = new IMGUIContainer(() => DrawIMGUIRuler(Mathf.Max(10f, Mathf.Max(_canvas.layout.width, _canvas.style.width.value.value))))
            {
                style = { position = Position.Absolute, left = 0, top = 0, width = new StyleLength(new Length(100, LengthUnit.Percent)), height = rulerHeight }
            };
            _canvas.Add(rulerContainer);
            RegisterPlayheadDrag(rulerContainer);

            var bgContainer = new IMGUIContainer(() => DrawIMGUIBackground(Mathf.Max(10f, Mathf.Max(_canvas.layout.width, _canvas.style.width.value.value)), height))
            {
                style = { position = Position.Absolute, left = 0, top = 0, width = new StyleLength(new Length(100, LengthUnit.Percent)), height = height }
            };
            IgnorePicking(bgContainer);
            _canvas.Add(bgContainer);

            var headerSpacer = RectElement(0, 0, 0, rulerHeight, TimelineTheme.RulerBg);
            headerSpacer.style.left = 0;
            headerSpacer.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            IgnorePicking(headerSpacer);
            _headers.Add(headerSpacer);

            DrawCurrentFrameBand(height);
        }

        private void ShowAddMenu(float time)
        {
            var menu = new GenericMenu();

            if (_state.Mode == TimelineMode.HonamiState && _state.SelectedState != null)
            {
                menu.AddItem(new GUIContent("Add Local Event"), false, () => AddStateEvent(time, HonamiEventType.Local));
                menu.AddItem(new GUIContent("Add Global Event"), false, () => AddStateEvent(time, HonamiEventType.Global));
                if (_state.IsSeqState)
                    menu.AddItem(new GUIContent("Add Clip"), false, () => AddSequencedClip(time));
            }
            else if (_state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null)
            {
                if (_state.SelectedTimelineTrack?.trackType == HonamiTimelineTrackType.Animation)
                    menu.AddItem(new GUIContent("Add Clip on Selected Track"), false, () => AddTimelineClip(time));
                else
                    menu.AddDisabledItem(new GUIContent("Add Clip on Selected Track"));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Add Animation Track"), false, () => AddTimelineTrack(HonamiTimelineTrackType.Animation, "Anim Track"));
                menu.AddItem(new GUIContent("Add Event Track"), false, () => AddTimelineTrack(HonamiTimelineTrackType.Event, "Event Track"));
            }

            if (menu.GetItemCount() > 0)
                menu.ShowAsContext();
        }

        private void AddStateEvent(float time, HonamiEventType eventType)
        {
            if (_state.SelectedState == null) return;
            Undo.RecordObject(_state.SelectedState, eventType == HonamiEventType.Local ? "Add Local Event" : "Add Global Event");
            _state.SelectedState.events ??= new List<HonamiEventMarker>();
            _state.SelectedState.events.Add(new HonamiEventMarker
            {
                time = time,
                eventType = eventType,
                eventName = "NewEvent",
                globalEventId = eventType == HonamiEventType.Global ? "NewEvent" : string.Empty
            });
            EditorUtility.SetDirty(_state.SelectedState);
            _rebuild();
        }

        private void AddSequencedClip(float time)
        {
            if (!_state.IsSeqState) return;
            Undo.RecordObject(_state.SeqNode, "Add Sequenced Clip");
            _state.SeqNode.sequencedClips.Add(new HonamiSequencedAnimationClip { startTime = time });
            EditorUtility.SetDirty(_state.SeqNode);
            _rebuild();
        }

        private void AddTimelineClip(float time)
        {
            if (_state.SelectedTimelineTrack?.trackType != HonamiTimelineTrackType.Animation) return;
            Undo.RecordObject(_state.ActiveTimeline, "Add Timeline Clip");
            _state.SelectedTimelineTrack.clips.Add(new HonamiTimelineClip { startTime = time });
            EditorUtility.SetDirty(_state.ActiveTimeline);
            _rebuild();
        }

        private void AddTimelineTrack(HonamiTimelineTrackType trackType, string trackName)
        {
            if (_state.ActiveTimeline == null) return;
            Undo.RecordObject(_state.ActiveTimeline, "Add Timeline Track");
            _state.ActiveTimeline.tracks.Add(new HonamiTimelineTrack { trackType = trackType, trackName = trackName });
            EditorUtility.SetDirty(_state.ActiveTimeline);
            _rebuild();
        }

        private void DrawIMGUIRuler(float width)
        {
            if (Event.current.type != EventType.Repaint) return;

            float rulerHeight = TimelineTheme.RulerHeight;
            float timeScale = _state.TimeScale;
            float fps = DisplayFps();

            EditorGUI.DrawRect(new Rect(0, 0, width, rulerHeight), TimelineTheme.RulerBg);

            float step = 1f;
            if (timeScale > 200f) step = 0.1f;
            else if (timeScale > 100f) step = 0.2f;
            else if (timeScale > 50f) step = 0.5f;
            else if (timeScale < 20f) step = 2f;
            else if (timeScale < 10f) step = 5f;

            float duration = width / timeScale;
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 9, normal = { textColor = TimelineTheme.MutedText } };

            for (float t = 0; t <= duration; t += step)
            {
                float x = t * timeScale;
                if (x < 0 || x > width) continue;
                EditorGUI.DrawRect(new Rect(x, rulerHeight - 8, 1, 8), TimelineTheme.MajorGrid);
                string txt = _state.ShowFrames && fps > 0 ? $"{(t * fps):F0}f" : $"{t:F1}s";
                GUI.Label(new Rect(x + 3, 2, 60, 15), txt, labelStyle);
            }
        }

        private void DrawIMGUIBackground(float width, float height)
        {
            if (Event.current.type != EventType.Repaint) return;

            float timeScale = _state.TimeScale;
            float rowHeight = TimelineTheme.RowHeight;

            float step = 1f;
            if (timeScale > 200f) step = 0.1f;
            else if (timeScale > 100f) step = 0.5f;
            else if (timeScale < 20f) step = 2f;

            float duration = width / timeScale;
            for (float t = 0; t <= duration; t += step)
                EditorGUI.DrawRect(new Rect(t * timeScale, 0, 1, height), TimelineTheme.MajorGrid);

            float y = TimelineTheme.RulerHeight;
            for (int i = 0; i < _rowCount; i++)
            {
                Color c = i % 2 == 0 ? TimelineTheme.TrackBgA : TimelineTheme.TrackBgB;
                EditorGUI.DrawRect(new Rect(0, y, width, rowHeight), c);
                EditorGUI.DrawRect(new Rect(0, y + rowHeight - 1, width, 1), TimelineTheme.Divider);
                y += rowHeight;
            }
        }

        private void DrawRows(float width)
        {
            float y = TimelineTheme.RulerHeight;
            var rows = GetRows();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                Color headerBg = row.IsSelected ? TimelineTheme.HeaderSelected
                    : row.IsBoneGroup ? TimelineTheme.BoneGroupHeaderBg
                    : TimelineTheme.HeaderBg;
                var header = RectElement(0, y, _state.TrackHeaderWidth, TimelineTheme.RowHeight, headerBg);
                RegisterRowHeaderEvents(header, row);
                _headers.Add(header);

                if (_state.Mode == TimelineMode.HonamiClipEdit)
                {
                    FillClipChannelHeader(header, row);
                    y += TimelineTheme.RowHeight;
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

                if (!string.IsNullOrEmpty(row.IconName))
                {
                    var icon = RowIcon(row.IconName);
                    icon.style.left = labelLeft;
                    header.Add(icon);
                    labelLeft += 20f;
                }

                var label = RowLabel(row.Title);
                label.style.left = labelLeft;
                if (row.IsBoneGroup) label.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.Add(label);
                if (row.Track != null)
                    header.Add(MuteButton(row.Track));
                y += TimelineTheme.RowHeight;
            }

            if (_state.IsDraggingTrack && _state.DragTrackInsertIndex >= 0)
            {
                float insertY = TimelineTheme.RulerHeight + _state.DragTrackInsertIndex * TimelineTheme.RowHeight;
                var insertLine = RectElement(0, insertY - 1, _state.TrackHeaderWidth, 2, TimelineTheme.Accent);
                insertLine.style.opacity = 0.8f;
                IgnorePicking(insertLine);
                _headers.Add(insertLine);
            }
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

                if (row.Track != null)
                {
                    _state.SelectedTimelineTrack = row.Track;
                    _state.SelectedTimelineClips.Clear();
                    _state.SelectedTimelineEvents.Clear();

                    if (evt.ctrlKey || evt.commandKey)
                    {
                        _state.IsDraggingTrack = true;
                        _state.DraggedTrack = row.Track;
                        _state.DragTrackStartY = header.resolvedStyle.top;
                        _state.DragTrackStartMouseY = evt.position.y;
                        _state.DragTrackInsertIndex = _state.ActiveTimeline.tracks.IndexOf(row.Track);
                        header.CapturePointer(evt.pointerId);
                    }
                    else
                    {
                        _rebuild();
                    }
                }
                header.style.backgroundColor = TimelineTheme.HeaderSelected;
                evt.StopPropagation();
            });

            header.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_state.IsDraggingTrack || _state.DraggedTrack != row.Track) return;

                float deltaY = evt.position.y - _state.DragTrackStartMouseY;
                float maxDrag = TimelineTheme.RulerHeight + (_state.ActiveTimeline.tracks.Count - 1) * TimelineTheme.RowHeight;
                float newY = Mathf.Clamp(_state.DragTrackStartY + deltaY, TimelineTheme.RulerHeight, maxDrag);

                header.style.top = newY;
                header.BringToFront();
                header.style.opacity = 0.8f;

                int newIndex = Mathf.FloorToInt((newY - TimelineTheme.RulerHeight + TimelineTheme.RowHeight * 0.5f) / TimelineTheme.RowHeight);
                _state.DragTrackInsertIndex = Mathf.Clamp(newIndex, 0, _state.ActiveTimeline.tracks.Count - 1);

                _rebuild();
                evt.StopPropagation();
            });

            header.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_state.IsDraggingTrack && _state.DraggedTrack == row.Track)
                {
                    _state.IsDraggingTrack = false;
                    header.ReleasePointer(evt.pointerId);

                    if (_state.ActiveTimeline != null && _state.DragTrackInsertIndex >= 0)
                    {
                        int oldIndex = _state.ActiveTimeline.tracks.IndexOf(row.Track);
                        if (oldIndex != -1 && oldIndex != _state.DragTrackInsertIndex)
                        {
                            Undo.RecordObject(_state.ActiveTimeline, "Reorder Timeline Tracks");
                            _state.ActiveTimeline.tracks.RemoveAt(oldIndex);
                            _state.ActiveTimeline.tracks.Insert(_state.DragTrackInsertIndex, row.Track);
                            EditorUtility.SetDirty(_state.ActiveTimeline);
                        }
                    }
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

        private void DrawContent()
        {
            _state.EventRects.Clear();
            _state.TimelineClipRects.Clear();
            _state.TimelineEventRects.Clear();
            _state.SeqClipRects.Clear();
            _state.KeyframeRects.Clear();

            float y = TimelineTheme.RulerHeight;

            if (_state.Mode == TimelineMode.HonamiClipEdit)
            {
                DrawClipEditContent(y);
                return;
            }

            if (_state.Mode == TimelineMode.HonamiTimeline)
            {
                if (_state.ActiveTimeline == null) return;
                foreach (var track in _state.ActiveTimeline.tracks)
                {
                    if (track.trackType == HonamiTimelineTrackType.Animation)
                    {
                        foreach (var clip in track.clips)
                        {
                            if (clip.clip == null) continue;
                            AddClipElement(clip, clip.clip.name, clip.startTime, clip.GetDuration(), y, _state.SelectedTimelineClips.Contains(clip), TimelineTheme.AnimationTrack);
                        }
                    }
                    else
                    {
                        foreach (var evt in track.events)
                            AddEventElement(evt, evt.eventId, evt.time, y, _state.SelectedTimelineEvents.Contains(evt), TimelineTheme.EventTrack);
                    }
                    y += TimelineTheme.RowHeight;
                }
                AddTimelineMarkers();
                return;
            }

            if (_state.ShowAnimTrack)
            {
                var editor = _state.SelectedState?.node != null ? HonamiNodeEditorRegistry.GetEditor(_state.SelectedState.node) : null;
                if (editor != null) editor.DrawTimelineTracks(_state, this, ref y);
                else y += TimelineTheme.RowHeight;
            }

            if (_state.ShowLocalEventsTrack)
            {
                AddStateEvents(HonamiEventType.Local, y);
                y += TimelineTheme.RowHeight;
            }
            if (_state.ShowGlobalEventsTrack)
            {
                AddStateEvents(HonamiEventType.Global, y);
            }
        }

        private void AddStateEvents(HonamiEventType type, float y)
        {
            if (_state.SelectedState?.events == null) return;
            var events = _state.SelectedState.events;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt.eventType == type)
                    AddEventElement(evt, type == HonamiEventType.Local ? evt.eventName : evt.globalEventId, evt.time, y, _state.SelectedEvents.Contains(evt),
                        type == HonamiEventType.Local ? TimelineTheme.EventTrack : TimelineTheme.GlobalEventTrack);
            }
        }

        private void AddTimelineMarkers()
        {
            if (_state.ActiveTimeline?.markers == null) return;
            foreach (var marker in _state.ActiveTimeline.markers)
            {
                if (marker is not HonamiTimelineLoopMarker loop) continue;
                float x1 = loop.time * _state.TimeScale;
                float x2 = loop.endTime * _state.TimeScale;
                var bar = RectElement(x1, 7, Mathf.Max(8, x2 - x1), 12, _state.SelectedMarkers.Contains(marker) ? TimelineTheme.Accent : TimelineTheme.EndBoundary);
                bar.tooltip = marker.id;
                bar.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    _focus();
                    SelectOnly(_state.SelectedMarkers, marker, evt.shiftKey || evt.ctrlKey || evt.commandKey);
                    _draggingTimelineMarker = true;
                    _dragMarker = loop;
                    _dragStartTime = loop.time;
                    _dragMarkerEndTime = loop.endTime;
                    _dragStartMouseTime = TimeAtPointer(evt);
                    bar.CapturePointer(evt.pointerId);
                    UpdateMarkerVisual(bar, loop);
                    evt.StopPropagation();
                });
                bar.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!_draggingTimelineMarker || _dragMarker != loop) return;

                    float delta = TimeAtPointer(evt) - _dragStartMouseTime;
                    float duration = Mathf.Max(0.01f, _dragMarkerEndTime - _dragStartTime);
                    float newStart = Mathf.Max(0f, _dragStartTime + delta);
                    newStart = SnapTime(newStart, loop, ShouldSnap(evt.shiftKey));
                    float newEnd = Mathf.Min(_state.GetDuration(), newStart + duration);
                    if (newEnd - newStart < duration)
                        newStart = Mathf.Max(0f, newEnd - duration);

                    Undo.RecordObject(_state.ActiveTimeline, "Move Timeline Marker");
                    loop.time = newStart;
                    loop.endTime = newEnd;
                    EditorUtility.SetDirty(_state.ActiveTimeline);
                    UpdateMarkerVisual(bar, loop);
                    evt.StopPropagation();
                });
                bar.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (_draggingTimelineMarker && _dragMarker == loop)
                    {
                        _state.SnapLineTime = -1f;
                        _draggingTimelineMarker = false;
                        _dragMarker = null;
                        bar.ReleasePointer(evt.pointerId);
                        _rebuild();
                    }
                    evt.StopPropagation();
                });
                _canvas.Add(bar);
            }
        }

        public void AddReadonlyClip(AnimationClip clipAsset, string label, float start, float duration, float y, Color color)
        {
            var clip = ClipElement(label, start, duration, y, false, color);
            if (clipAsset != null)
            {
                clip.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.clickCount == 2)
                        OpenClipEdit(clipAsset);
                    evt.StopPropagation();
                });
            }
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
            var body = ClipElement(label, start / speed, duration / speed, y, false, themeColor);
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
            var startHandle = TrimHandle((start / speed) * _state.TimeScale, y, $"Start: {start:F3}s — drag to trim");
            startHandle.style.borderTopLeftRadius = startHandle.style.borderBottomLeftRadius = 4;
            _canvas.Add(startHandle);

            var endHandle = TrimHandle(((start + duration) / speed) * _state.TimeScale - handleWidth, y, $"End: {start + duration:F3}s — drag to trim");
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
                _state.IsDraggingRandomAnimEnd = false;
                _state.SnapLineTime = -1f;
                endHandle.ReleasePointer(evt.pointerId);
                onDragEnd?.Invoke();
                _rebuild();
                evt.StopPropagation();
            });
        }

        private static VisualElement TrimHandle(float x, float y, string tooltip)
        {
            var handle = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = x,
                    top = y + 4,
                    width = 8f,
                    height = TimelineTheme.ClipHeight,
                    backgroundColor = new Color(1f, 1f, 1f, 0.18f)
                },
                tooltip = tooltip
            };
            handle.RegisterCallback<PointerEnterEvent>(_ => handle.style.backgroundColor = new Color(1f, 1f, 1f, 0.36f));
            handle.RegisterCallback<PointerLeaveEvent>(_ => handle.style.backgroundColor = new Color(1f, 1f, 1f, 0.18f));
            return handle;
        }

        public void AddClipElement(object target, string label, float start, float duration, float y, bool selected, Color color)
        {
            var clip = ClipElement(label, start, duration, y, selected, color);
            _elements[target] = clip;
            clip.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                if (evt.clickCount == 2)
                {
                    var animClip = GetClip(target);
                    if (animClip != null)
                        OpenClipEdit(animClip);
                    evt.StopPropagation();
                    return;
                }

                _focus();
                SelectClipTarget(target, evt.shiftKey || evt.ctrlKey || evt.commandKey);
                _draggingElement = true;
                _dragTarget = target;
                _dragStartTime = GetStartTime(target);
                _dragStartMouseTime = TimeAtCanvasX(clip.resolvedStyle.left + evt.localPosition.x);
                clip.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            clip.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingElement || _dragTarget != target) return;
                float delta = TimeAtCanvasX(clip.resolvedStyle.left + evt.localPosition.x) - _dragStartMouseTime;
                float time = SnapTime(Mathf.Max(0f, _dragStartTime + delta), target, ShouldSnap(evt.shiftKey));
                SetStartTime(target, time);
                UpdateDraggedElementVisual(target);
                UpdateSnapLineVisual();
                evt.StopPropagation();
            });
            clip.RegisterCallback<PointerUpEvent>(evt =>
            {
                _state.SnapLineTime = -1f;
                _draggingElement = false;
                _dragTarget = null;
                clip.ReleasePointer(evt.pointerId);
                _rebuild();
            });

            var rect = new Rect(start * _state.TimeScale, y + 4, Mathf.Max(1f, duration * _state.TimeScale), TimelineTheme.ClipHeight);
            if (target is HonamiTimelineClip tc) _state.TimelineClipRects[tc] = rect;
            else if (target is HonamiSequencedAnimationClip sc) _state.SeqClipRects[sc] = rect;

            _canvas.Add(clip);
        }

        private void OpenClipEdit(AnimationClip clip)
        {
            _state.Mode = TimelineMode.HonamiClipEdit;
            _state.ActiveClip = clip;
            _rebuild();
        }

        private VisualElement ClipElement(string label, float start, float duration, float y, bool selected, Color color)
        {
            float paddingY = (TimelineTheme.RowHeight - TimelineTheme.ClipHeight) * 0.5f;
            var root = RectElement(start * _state.TimeScale, y + paddingY, Mathf.Max(1f, duration * _state.TimeScale), TimelineTheme.ClipHeight, selected ? TimelineTheme.Selected : color);

            root.style.borderTopWidth = root.style.borderRightWidth = root.style.borderBottomWidth = root.style.borderLeftWidth = selected ? 2 : 1;
            root.style.borderTopColor = root.style.borderRightColor = root.style.borderBottomColor = root.style.borderLeftColor = selected ? Color.white : new Color(0, 0, 0, 0.5f);
            root.style.borderTopLeftRadius = root.style.borderTopRightRadius = root.style.borderBottomLeftRadius = root.style.borderBottomRightRadius = 6;
            root.style.overflow = Overflow.Hidden;
            root.tooltip = label;

            var text = new Label(label);
            IgnorePicking(text);
            text.style.position = Position.Absolute;
            text.style.left = 10;
            text.style.right = 8;
            text.style.top = (TimelineTheme.ClipHeight - 16f) * 0.5f;
            text.style.height = 16;
            text.style.color = Color.white;
            text.style.unityFontStyleAndWeight = FontStyle.Bold;
            text.style.fontSize = 11;
            text.style.overflow = Overflow.Hidden;
            root.Add(text);

            root.RegisterCallback<PointerEnterEvent>(_ => root.style.backgroundColor = Color.Lerp(selected ? TimelineTheme.Selected : color, Color.white, 0.1f));
            root.RegisterCallback<PointerLeaveEvent>(_ => root.style.backgroundColor = selected ? TimelineTheme.Selected : color);

            return root;
        }

        private void AddEventElement(object target, string label, float time, float y, bool selected, Color color)
        {
            var marker = RectElement(time * _state.TimeScale - 8, y + 10, 16, 20, selected ? TimelineTheme.Accent : color);
            _elements[target] = marker;
            marker.style.borderTopLeftRadius = marker.style.borderTopRightRadius = marker.style.borderBottomLeftRadius = marker.style.borderBottomRightRadius = 3;
            marker.tooltip = $"{label}\n{time:F2}s";
            var eventLabel = new Label("E")
            {
                style =
                {
                    color = Color.black,
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            IgnorePicking(eventLabel);
            marker.Add(eventLabel);
            marker.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _focus();
                SelectEventTarget(target, evt.shiftKey || evt.ctrlKey || evt.commandKey);
                _draggingElement = true;
                _dragTarget = target;
                _dragStartTime = GetStartTime(target);
                _dragStartMouseTime = TimeAtCanvasX(marker.resolvedStyle.left + evt.localPosition.x);
                marker.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            marker.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingElement || _dragTarget != target) return;
                float delta = TimeAtCanvasX(marker.resolvedStyle.left + evt.localPosition.x) - _dragStartMouseTime;
                float newTime = SnapTime(Mathf.Max(0f, _dragStartTime + delta), target, ShouldSnap(evt.shiftKey));
                SetStartTime(target, newTime);
                UpdateDraggedElementVisual(target);
                UpdateSnapLineVisual();
                evt.StopPropagation();
            });
            marker.RegisterCallback<PointerUpEvent>(evt =>
            {
                _state.SnapLineTime = -1f;
                _draggingElement = false;
                _dragTarget = null;
                marker.ReleasePointer(evt.pointerId);
                _rebuild();
            });

            var rect = new Rect(time * _state.TimeScale - 8, y + 10, 16, 20);
            if (target is HonamiEventMarker em) _state.EventRects[em] = rect;
            else if (target is HonamiTimelineEvent te) _state.TimelineEventRects[te] = rect;

            _canvas.Add(marker);
        }

        private void DrawPlayhead(float height)
        {
            float x = _state.PlayheadTime * _state.TimeScale;
            _playheadLine = RectElement(x - 1, 0, 2, height, TimelineTheme.Playhead);
            _canvas.Add(_playheadLine);
            _playheadHead = RectElement(x - 6, 2, 12, 16, TimelineTheme.Playhead);
            _playheadHead.style.borderTopLeftRadius = _playheadHead.style.borderTopRightRadius = _playheadHead.style.borderBottomLeftRadius = _playheadHead.style.borderBottomRightRadius = 3;
            _canvas.Add(_playheadHead);
            RegisterPlayheadDrag(_playheadLine);
            RegisterPlayheadDrag(_playheadHead);
        }

        private void RegisterPlayheadDrag(VisualElement target)
        {
            target.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _focus();
                _draggingPlayhead = true;
                _state.IsDraggingPlayhead = true;
                SetPlayheadFromLocal(PointerXInCanvas(evt), false, evt.shiftKey);
                target.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            target.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingPlayhead || !target.HasPointerCapture(evt.pointerId)) return;
                SetPlayheadFromLocal(PointerXInCanvas(evt), false, evt.shiftKey);
                evt.StopPropagation();
            });
            target.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (target.HasPointerCapture(evt.pointerId))
                    target.ReleasePointer(evt.pointerId);
                _state.SnapLineTime = -1f;
                _draggingPlayhead = false;
                _state.IsDraggingPlayhead = false;
                _rebuild();
                evt.StopPropagation();
            });
        }

        private void UpdatePlayheadVisual()
        {
            float x = _state.PlayheadTime * _state.TimeScale;
            if (_playheadLine != null) _playheadLine.style.left = x - 1;
            if (_playheadHead != null) _playheadHead.style.left = x - 6;
            UpdateCurrentFrameBand();
            UpdateSnapLineVisual();
        }

        private void DrawSnapLine(float height)
        {
            if (_state.SnapLineTime < 0f) return;
            float x = _state.SnapLineTime * _state.TimeScale;
            _snapLine = RectElement(x - 1f, 0, 2f, height, TimelineTheme.Accent);
            _snapLine.style.opacity = 0.9f;
            IgnorePicking(_snapLine);
            _canvas.Add(_snapLine);
        }

        private void UpdateSnapLineVisual()
        {
            if (_state.SnapLineTime < 0f)
            {
                if (_snapLine != null)
                    _snapLine.style.display = DisplayStyle.None;
                return;
            }

            if (_snapLine == null)
            {
                float height = Mathf.Max(200f, _canvas.resolvedStyle.height);
                _snapLine = RectElement(0f, 0f, 2f, height, TimelineTheme.Accent);
                _snapLine.style.opacity = 0.9f;
                IgnorePicking(_snapLine);
                _canvas.Add(_snapLine);
            }

            _snapLine.style.display = DisplayStyle.Flex;
            _snapLine.style.left = _state.SnapLineTime * _state.TimeScale - 1f;
        }

        private void UpdateDraggedElementVisual(object target)
        {
            if (!_elements.TryGetValue(target, out VisualElement element)) return;

            float startTime = GetStartTime(target);
            element.style.left = target is HonamiEventMarker || target is HonamiTimelineEvent
                ? startTime * _state.TimeScale - 8f
                : startTime * _state.TimeScale;
        }

        private void UpdateMarkerVisual(VisualElement element, HonamiTimelineLoopMarker marker)
        {
            element.style.left = marker.time * _state.TimeScale;
            element.style.width = Mathf.Max(8f, (marker.endTime - marker.time) * _state.TimeScale);
            UpdateSnapLineVisual();
        }

        private void DrawBoxSelection()
        {
            if (!_state.IsBoxSelecting) return;

            var borderColor = new Color(0.4f, 0.7f, 1f, 0.8f);
            var box = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = _state.BoxSelectRect.x,
                    top = _state.BoxSelectRect.y,
                    width = _state.BoxSelectRect.width,
                    height = _state.BoxSelectRect.height,
                    backgroundColor = new Color(0.18f, 0.45f, 0.85f, 0.25f),
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = borderColor,
                    borderBottomColor = borderColor,
                    borderLeftColor = borderColor,
                    borderRightColor = borderColor
                }
            };
            IgnorePicking(box);
            _canvas.Add(box);
        }

        private float PointerXInCanvas(IPointerEvent evt) => _canvas.WorldToLocal(evt.position).x;

        private float TimeAtPointer(IPointerEvent evt) => TimeAtCanvasX(PointerXInCanvas(evt));

        private float TimeAtCanvasX(float x) => x / _state.TimeScale;

        private void SetPlayheadFromLocal(float localX, bool rebuild = true, bool shiftHeld = false)
        {
            float time = SnapTime(localX / _state.TimeScale, null, ShouldSnap(shiftHeld));
            _state.PlayheadTime = Mathf.Clamp(time, 0f, _state.GetDuration());
            UpdatePlayheadVisual();
            if (rebuild)
                _rebuild();
        }

        private bool ShouldSnap(bool shiftHeld) => _state.SnapEnabled != shiftHeld;

        private float SnapTime(float time, object ignore, bool useSnap)
        {
            if (!useSnap)
            {
                _state.SnapLineTime = -1f;
                return time;
            }

            float threshold = 40f / Mathf.Max(1f, _state.TimeScale);
            float best = time;
            float minDistance = float.MaxValue;

            var points = new List<float> { 0f, _state.GetDuration() };
            AddStateSnapPoints(points, ignore);
            AddTimelineSnapPoints(points, ignore);

            if (!_state.IsDraggingPlayhead)
                points.Add(_state.PlayheadTime);

            foreach (float point in points)
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

            float fps = Mathf.Max(1f, DisplayFps());
            float frameStep = 1f / fps;
            float frameTime = Mathf.Round(time / frameStep) * frameStep;
            if (Mathf.Abs(time - frameTime) < threshold)
            {
                _state.SnapLineTime = frameTime;
                return frameTime;
            }

            _state.SnapLineTime = -1f;
            return time;
        }

        private void AddStateSnapPoints(List<float> points, object ignore)
        {
            if (_state.SelectedState?.events != null)
            {
                foreach (var evt in _state.SelectedState.events)
                {
                    if (ReferenceEquals(evt, ignore) || _state.SelectedEvents.Contains(evt)) continue;
                    points.Add(evt.time);
                }
            }

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
            if (_state.Mode != TimelineMode.HonamiTimeline || _state.ActiveTimeline == null) return;

            foreach (var track in _state.ActiveTimeline.tracks)
            {
                foreach (var clip in track.clips)
                {
                    if (ReferenceEquals(clip, ignore) || _state.SelectedTimelineClips.Contains(clip)) continue;
                    points.Add(clip.startTime);
                    points.Add(clip.startTime + clip.GetDuration());
                }

                foreach (var evt in track.events)
                {
                    if (ReferenceEquals(evt, ignore) || _state.SelectedTimelineEvents.Contains(evt)) continue;
                    points.Add(evt.time);
                }
            }

            if (_state.ActiveTimeline.markers == null) return;
            foreach (var marker in _state.ActiveTimeline.markers)
            {
                if (ReferenceEquals(marker, ignore) || _state.SelectedMarkers.Contains(marker)) continue;
                points.Add(marker.time);
                if (marker is HonamiTimelineLoopMarker loop)
                    points.Add(loop.endTime);
            }
        }

        private static float GetStartTime(object target)
        {
            return target switch
            {
                HonamiSequencedAnimationClip clip => clip.startTime,
                HonamiTimelineClip clip => clip.startTime,
                HonamiEventMarker evt => evt.time,
                HonamiTimelineEvent evt => evt.time,
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
                case HonamiTimelineClip clip:
                    Undo.RecordObject(_state.ActiveTimeline, "Move Timeline Clip");
                    clip.startTime = value;
                    EditorUtility.SetDirty(_state.ActiveTimeline);
                    break;
                case HonamiEventMarker evt:
                    Undo.RecordObject(_state.SelectedState, "Move Event");
                    evt.time = Mathf.Clamp(value, 0f, _state.GetDuration());
                    EditorUtility.SetDirty(_state.SelectedState);
                    break;
                case HonamiTimelineEvent evt:
                    Undo.RecordObject(_state.ActiveTimeline, "Move Timeline Event");
                    evt.time = value;
                    EditorUtility.SetDirty(_state.ActiveTimeline);
                    break;
            }
        }

        private void SelectClipTarget(object target, bool additive)
        {
            switch (target)
            {
                case HonamiSequencedAnimationClip clip:
                    SelectOnly(_state.SelectedSeqClips, clip, additive);
                    _state.SelectedEvents.Clear();
                    break;
                case HonamiTimelineClip clip:
                    SelectOnly(_state.SelectedTimelineClips, clip, additive);
                    _state.SelectedTimelineEvents.Clear();
                    break;
            }
        }

        private void SelectEventTarget(object target, bool additive)
        {
            switch (target)
            {
                case HonamiEventMarker evt:
                    SelectOnly(_state.SelectedEvents, evt, additive);
                    _state.SelectedSeqClips.Clear();
                    break;
                case HonamiTimelineEvent evt:
                    SelectOnly(_state.SelectedTimelineEvents, evt, additive);
                    _state.SelectedTimelineClips.Clear();
                    break;
            }
        }

        private static void SelectOnly<T>(List<T> list, T item, bool additive)
        {
            if (!additive) list.Clear();
            if (list.Contains(item)) list.Remove(item);
            else list.Add(item);
        }

        private static AnimationClip GetClip(object target)
        {
            return target switch
            {
                HonamiSequencedAnimationClip sc => sc.clip,
                HonamiTimelineClip tc => tc.clip,
                _ => null
            };
        }
    }
}
#endif
