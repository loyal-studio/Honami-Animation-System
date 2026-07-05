#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView : VisualElement
    {
        private readonly TimelineState _state;
        private readonly Action _rebuild;
        private readonly Action _focus;
        private readonly Func<bool> _isActive;

        private ScrollView _scroll;
        private VisualElement _mainArea;
        private VisualElement _content;
        private VisualElement _headers;
        private VisualElement _canvas;
        private VisualElement _properties;
        private VisualElement _playheadLine;
        private VisualElement _playheadHead;
        private VisualElement _snapLine;
        private readonly Dictionary<object, VisualElement> _elements = new();
        private float _lastTimelineViewportWidth;

        private bool _draggingPlayhead;
        private bool _draggingElement;
        private bool _draggingTimelineMarker;
        private object _dragTarget;
        private HonamiTimelineLoopMarker _dragMarker;
        private float _dragStartTime;
        private float _dragStartMouseTime;
        private float _dragMarkerEndTime;

        public TimelinePanelView(TimelineState state, Func<bool> isActive, Action focus, Action rebuild)
        {
            _state = state;
            _isActive = isActive;
            _focus = focus;
            _rebuild = rebuild;

            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            style.minWidth = 0;
            style.minHeight = 0;
            style.backgroundColor = TimelineTheme.WindowBg;
            focusable = true;
            RegisterCallback<PointerDownEvent>(_ => { Focus(); _focus(); });
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);

            _mainArea = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    minWidth = 0,
                    minHeight = 0,
                    flexDirection = FlexDirection.Row
                }
            };
            Add(_mainArea);
            Add(BuildTransportBar());

            Build();
        }

        public void Refresh()
        {
            float currentWidth = resolvedStyle.width;
            if (currentWidth > 1f)
                _lastTimelineViewportWidth = currentWidth;

            Build();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Abs(evt.newRect.width - evt.oldRect.width) > 1f)
            {
                _lastTimelineViewportWidth = evt.newRect.width;
                Build();
            }
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            RegisterRecording();
            evt.destinationPanel.visualTree.RegisterCallback<PointerMoveEvent>(OnGlobalMove, TrickleDown.TrickleDown);
            evt.destinationPanel.visualTree.RegisterCallback<PointerUpEvent>(OnGlobalUp, TrickleDown.TrickleDown);
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            UnregisterRecording();
            evt.originPanel.visualTree.UnregisterCallback<PointerMoveEvent>(OnGlobalMove, TrickleDown.TrickleDown);
            evt.originPanel.visualTree.UnregisterCallback<PointerUpEvent>(OnGlobalUp, TrickleDown.TrickleDown);
        }

        private void StopSelection()
        {
            _state.IsBoxSelecting = false;
            _draggingPlayhead = false;
            _state.IsDraggingPlayhead = false;
            _rebuild();
        }

        private void OnGlobalMove(PointerMoveEvent evt)
        {
            if (!_state.IsBoxSelecting && !_draggingPlayhead) return;

            if ((evt.pressedButtons & 1) == 0)
            {
                StopSelection();
                return;
            }

            var pos = _canvas.WorldToLocal(evt.position);
            if (_state.IsBoxSelecting)
            {
                float xMin = Mathf.Min(_state.BoxSelectStartPos.x, pos.x);
                float yMin = Mathf.Min(_state.BoxSelectStartPos.y, pos.y);
                float xMax = Mathf.Max(_state.BoxSelectStartPos.x, pos.x);
                float yMax = Mathf.Max(_state.BoxSelectStartPos.y, pos.y);
                _state.BoxSelectRect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                UpdateBoxSelection();
                _rebuild();
            }
            else if (_draggingPlayhead)
            {
                SetPlayheadFromLocal(pos.x, false, evt.shiftKey);
            }
        }

        private void OnGlobalUp(PointerUpEvent evt)
        {
            if (_state.IsBoxSelecting || _draggingPlayhead)
                StopSelection();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (!evt.altKey) return;
            float multiplier = evt.delta.y < 0 ? 1.1f : 1f / 1.1f;
            _state.TimeScale = Mathf.Clamp(_state.TimeScale * multiplier, 10f, 500f);
            _state.SaveSettings();
            _rebuild();
            evt.StopPropagation();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is AnimationClip)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.StopPropagation();
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is AnimationClip clip)
            {
                DragAndDrop.AcceptDrag();
                _state.Mode = TimelineMode.HonamiClipEdit;
                _state.ActiveClip = clip;
                _state.PlayheadTime = 0f;
                _state.IsPlaying = false;
                _state.PreviewEnabled = true;
                _rebuild();
                evt.StopPropagation();
            }
        }

        private void RegisterCanvasEvents(VisualElement canvas)
        {
            canvas.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    _focus();
                    var local = canvas.WorldToLocal(evt.position);
                    if (_state.Mode == TimelineMode.HonamiClipEdit)
                        ShowClipEditContextMenu(local.y);
                    else
                        ShowAddMenu(Mathf.Clamp(TimeAtCanvasX(local.x), 0f, _state.GetDuration()));
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 0) return;
                _focus();

                var pos = canvas.WorldToLocal(evt.position);
                if (!HitsAnyItem(pos))
                {
                    if (!evt.shiftKey && !evt.ctrlKey && !evt.commandKey)
                        _state.ClearSelection();

                    _state.IsBoxSelecting = true;
                    _state.BoxSelectStartPos = pos;
                    _state.BoxSelectRect = new Rect(pos.x, pos.y, 0, 0);
                }
                else
                {
                    _state.IsDraggingPlayhead = true;
                    _draggingPlayhead = true;
                    SetPlayheadFromLocal(pos.x, false, evt.shiftKey);
                }
                evt.StopPropagation();
            });

            canvas.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (_state.IsBoxSelecting || _draggingPlayhead)
                    StopSelection();
            });
        }

        private bool HitsAnyItem(Vector2 pos)
        {
            foreach (var r in _state.EventRects.Values) if (r.Contains(pos)) return true;
            foreach (var r in _state.TimelineClipRects.Values) if (r.Contains(pos)) return true;
            foreach (var r in _state.TimelineEventRects.Values) if (r.Contains(pos)) return true;
            foreach (var r in _state.SeqClipRects.Values) if (r.Contains(pos)) return true;
            foreach (var r in _state.KeyframeRects.Values) if (r.Contains(pos)) return true;
            return false;
        }

        private void UpdateBoxSelection()
        {
            Rect box = _state.BoxSelectRect;
            bool append = Event.current != null && (Event.current.shift || Event.current.control || Event.current.command);

            if (!append)
            {
                _state.SelectedEvents.Clear();
                _state.SelectedTimelineClips.Clear();
                _state.SelectedTimelineEvents.Clear();
                _state.SelectedSeqClips.Clear();
                _state.SelectedKeyframes.Clear();
            }

            foreach (var kv in _state.EventRects)
                if (box.Overlaps(kv.Value) && !_state.SelectedEvents.Contains(kv.Key)) _state.SelectedEvents.Add(kv.Key);

            foreach (var kv in _state.TimelineClipRects)
                if (box.Overlaps(kv.Value) && !_state.SelectedTimelineClips.Contains(kv.Key)) _state.SelectedTimelineClips.Add(kv.Key);

            foreach (var kv in _state.TimelineEventRects)
                if (box.Overlaps(kv.Value) && !_state.SelectedTimelineEvents.Contains(kv.Key)) _state.SelectedTimelineEvents.Add(kv.Key);

            foreach (var kv in _state.SeqClipRects)
                if (box.Overlaps(kv.Value) && !_state.SelectedSeqClips.Contains(kv.Key)) _state.SelectedSeqClips.Add(kv.Key);

            foreach (var kv in _state.KeyframeRects)
                if (box.Overlaps(kv.Value) && !_state.SelectedKeyframes.Contains(kv.Key)) _state.SelectedKeyframes.Add(kv.Key);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.target is VisualElement target &&
                (target is TextField || target is FloatField || target.GetType().Name.Contains("TextInput")))
                return;

            if (evt.keyCode == KeyCode.Space)
            {
                TimelineState.TogglePlayback(_state);
                _rebuild();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow)
            {
                float fps = DisplayFps();
                float step = fps > 0 ? 1f / fps : 0.016f;
                bool toEdge = evt.ctrlKey || evt.commandKey;
                if (evt.keyCode == KeyCode.LeftArrow)
                    _state.PlayheadTime = toEdge ? 0f : Mathf.Max(0f, _state.PlayheadTime - step);
                else
                    _state.PlayheadTime = toEdge ? _state.GetDuration() : Mathf.Min(_state.GetDuration(), _state.PlayheadTime + step);
                _rebuild();
                evt.StopPropagation();
                return;
            }

            if (evt.ctrlKey || evt.commandKey)
            {
                if (evt.keyCode == KeyCode.C)
                {
                    CopySelection();
                    evt.StopPropagation();
                    return;
                }
                if (evt.keyCode == KeyCode.V)
                {
                    PasteSelection();
                    _rebuild();
                    evt.StopPropagation();
                    return;
                }
            }

            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
                DeleteSelection(evt);
        }

        private void DeleteSelection(KeyDownEvent evt)
        {
            bool changed = false;

            if (_state.SelectedTimelineClips.Count > 0)
            {
                HonamiEditorController.DeleteTimelineClips(_state.ActiveTimeline, new List<HonamiTimelineClip>(_state.SelectedTimelineClips));
                _state.SelectedTimelineClips.Clear();
                changed = true;
            }

            if (_state.SelectedTimelineEvents.Count > 0)
            {
                HonamiEditorController.DeleteTimelineEvents(_state.ActiveTimeline, new List<HonamiTimelineEvent>(_state.SelectedTimelineEvents));
                _state.SelectedTimelineEvents.Clear();
                changed = true;
            }

            if (_state.SelectedSeqClips.Count > 0)
            {
                for (int i = _state.SelectedSeqClips.Count - 1; i >= 0; i--)
                    HonamiEditorController.DeleteSequencedClip(_state.SeqNode, _state.SelectedSeqClips[i]);
                _state.SelectedSeqClips.Clear();
                changed = true;
            }

            if (_state.SelectedEvents.Count > 0)
            {
                HonamiEditorController.DeleteStateEvents(_state.SelectedState, new List<HonamiEventMarker>(_state.SelectedEvents));
                _state.SelectedEvents.Clear();
                changed = true;
            }

            if (_state.Mode == TimelineMode.HonamiClipEdit && _state.SelectedKeyframes.Count > 0)
            {
                DeleteSelectedKeyframes();
                changed = true;
            }

            if (changed)
            {
                evt.StopPropagation();
                _rebuild();
            }
        }

        private void CopySelection()
        {
            _state.CopiedEvents.Clear();
            _state.CopiedEvents.AddRange(_state.SelectedEvents);

            _state.CopiedTimelineClips.Clear();
            _state.CopiedTimelineClips.AddRange(_state.SelectedTimelineClips);

            _state.CopiedTimelineEvents.Clear();
            _state.CopiedTimelineEvents.AddRange(_state.SelectedTimelineEvents);

            if (_state.Mode == TimelineMode.HonamiClipEdit)
                CopySelectedKeyframes();
        }

        private void PasteSelection()
        {
            if (_state.Mode == TimelineMode.HonamiClipEdit)
            {
                PasteKeyframes();
                return;
            }

            if (_state.Mode == TimelineMode.HonamiState && _state.SelectedState != null)
            {
                var eventsToPaste = new List<HonamiEventMarker>();
                if (_state.CopiedEvents.Count > 0)
                {
                    eventsToPaste.AddRange(_state.CopiedEvents);
                }
                else if (_state.CopiedTimelineEvents.Count > 0)
                {
                    foreach (var te in _state.CopiedTimelineEvents)
                        eventsToPaste.Add(new HonamiEventMarker { time = te.time, eventType = HonamiEventType.Global, eventName = te.eventId, globalEventId = te.eventId });
                }

                if (eventsToPaste.Count > 0)
                    HonamiEditorController.PasteStateEvents(_state.SelectedState, eventsToPaste, _state.PlayheadTime);
            }
            else if (_state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null && _state.SelectedTimelineTrack != null)
            {
                var clipsToPaste = new List<HonamiTimelineClip>(_state.CopiedTimelineClips);
                var eventsToPaste = new List<HonamiTimelineEvent>();

                if (_state.CopiedTimelineEvents.Count > 0)
                {
                    eventsToPaste.AddRange(_state.CopiedTimelineEvents);
                }
                else if (_state.CopiedEvents.Count > 0)
                {
                    foreach (var se in _state.CopiedEvents)
                        eventsToPaste.Add(new HonamiTimelineEvent { time = se.time, eventId = se.eventType == HonamiEventType.Local ? se.eventName : se.globalEventId });
                }

                HonamiEditorController.PasteTimelineElements(_state.ActiveTimeline, _state.SelectedTimelineTrack, clipsToPaste, eventsToPaste, _state.PlayheadTime);
            }
        }

        private bool IsStateEmpty()
        {
            if (_state == null) return true;
            if (_state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline == null) return true;
            if (_state.Mode == TimelineMode.HonamiState && (_state.Controller == null || _state.SelectedState == null)) return true;
            if (_state.Mode == TimelineMode.HonamiClipEdit && _state.ActiveClip == null) return true;
            return false;
        }

        private bool PropertiesVisible() => HasProperties() && _state.ShowProperties;

        private float ViewportWidth()
        {
            float width = _lastTimelineViewportWidth > 1f ? _lastTimelineViewportWidth : resolvedStyle.width;
            if (PropertiesVisible() && width > _state.PropsWidth)
                width -= _state.PropsWidth;
            return width;
        }

        private float ComputeTimelineWidth(float viewportWidth)
        {
            return IsStateEmpty()
                ? Mathf.Max(viewportWidth - _state.TrackHeaderWidth, 900f)
                : _state.GetTimelineWidth(viewportWidth > 1f ? viewportWidth : 900f);
        }

        private void RefreshCanvasWidth()
        {
            float timelineWidth = ComputeTimelineWidth(ViewportWidth());
            _content.style.width = _state.TrackHeaderWidth + timelineWidth;
            _canvas.style.width = timelineWidth;

            foreach (var child in _canvas.Children())
            {
                if (child is IMGUIContainer imgui)
                    imgui.MarkDirtyRepaint();
            }
        }

        private void Build()
        {
            if (_scroll != null)
                _state.ScrollPos = _scroll.scrollOffset;

            _mainArea.Clear();
            _elements.Clear();

            bool hasProps = PropertiesVisible();
            var timelineRoot = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    minWidth = 0,
                    minHeight = 0,
                    borderTopWidth = 2,
                    borderTopColor = _isActive() ? TimelineTheme.Accent : TimelineTheme.SubtleLine
                }
            };
            _mainArea.Add(timelineRoot);

            _scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scroll.style.flexGrow = 1;
            _scroll.style.backgroundColor = TimelineTheme.PanelBg;
            timelineRoot.Add(_scroll);

            float timelineWidth = ComputeTimelineWidth(ViewportWidth());
            _rowCount = IsStateEmpty() ? 0 : GetRows().Count;
            float contentHeight = Mathf.Max(280f, TimelineTheme.RulerHeight + _rowCount * TimelineTheme.RowHeight + 24f);

            _content = new VisualElement
            {
                style =
                {
                    position = Position.Relative,
                    width = _state.TrackHeaderWidth + timelineWidth,
                    height = contentHeight,
                    flexDirection = FlexDirection.Row
                }
            };
            _scroll.Add(_content);
            _scroll.scrollOffset = _state.ScrollPos;

            if (_headers == null)
            {
                _headers = new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = 0,
                        top = 0,
                        width = _state.TrackHeaderWidth,
                        backgroundColor = TimelineTheme.HeaderBg,
                        overflow = Overflow.Hidden
                    }
                };
            }
            _headers.style.height = contentHeight;
            _headers.Clear();
            _content.Add(_headers);

            if (_canvas == null)
            {
                _canvas = new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = _state.TrackHeaderWidth,
                        top = 0,
                        backgroundColor = TimelineTheme.TrackBgA,
                        overflow = Overflow.Hidden
                    }
                };
                RegisterCanvasEvents(_canvas);
            }
            _canvas.style.width = timelineWidth;
            _canvas.style.height = contentHeight;
            _canvas.Clear();
            _content.Add(_canvas);

            _content.Add(BuildHeaderSplitter(contentHeight));

            if (IsStateEmpty())
            {
                DrawEmptyState();
            }
            else
            {
                DrawTimelineOverlay(contentHeight);
                DrawRows(timelineWidth);
                DrawContent();
                DrawSnapLine(contentHeight);
                DrawBoxSelection();
                DrawPlayhead(contentHeight);
            }

            if (hasProps)
            {
                _properties = new VisualElement
                {
                    style =
                    {
                        width = _state.PropsWidth,
                        flexShrink = 0,
                        backgroundColor = TimelineTheme.PanelBg,
                        borderLeftWidth = 2,
                        borderLeftColor = TimelineTheme.AccentDim
                    }
                };
                _mainArea.Add(_properties);
                _mainArea.Add(BuildPropsSplitter());
                BuildProperties();
            }

            RefreshTransport();
        }

        private VisualElement BuildHeaderSplitter(float contentHeight)
        {
            var splitter = CreateSplitter(
                value => _state.IsResizingTrackHeader = value,
                () => _state.IsResizingTrackHeader,
                onFinish: () => { _state.SaveSettings(); _rebuild(); });
            splitter.style.left = _state.TrackHeaderWidth - 2;
            splitter.style.height = contentHeight;

            splitter.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_state.IsResizingTrackHeader || !splitter.HasPointerCapture(evt.pointerId)) return;
                float newWidth = Mathf.Clamp(_content.WorldToLocal(evt.position).x, 100f, 600f);
                if (Mathf.Abs(_state.TrackHeaderWidth - newWidth) > 0.5f)
                {
                    _state.TrackHeaderWidth = newWidth;
                    _headers.style.width = newWidth;
                    _canvas.style.left = newWidth;
                    splitter.style.left = newWidth - 2;
                    RefreshCanvasWidth();
                }
                evt.StopPropagation();
            });
            return splitter;
        }

        private VisualElement BuildPropsSplitter()
        {
            var splitter = CreateSplitter(
                value => _state.IsResizingPropsPanel = value,
                () => _state.IsResizingPropsPanel,
                onFinish: () => _state.SaveSettings());
            splitter.style.right = _state.PropsWidth - 2;
            splitter.style.bottom = 0;

            splitter.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_state.IsResizingPropsPanel || !splitter.HasPointerCapture(evt.pointerId)) return;
                float parentWidth = resolvedStyle.width;
                if (parentWidth < 1f) parentWidth = _lastTimelineViewportWidth;

                float newWidth = Mathf.Clamp(parentWidth - this.WorldToLocal(evt.position).x, 100f, 600f);
                if (Mathf.Abs(_state.PropsWidth - newWidth) > 0.5f)
                {
                    _state.PropsWidth = newWidth;
                    _properties.style.width = newWidth;
                    splitter.style.right = newWidth - 2;
                    RefreshCanvasWidth();
                }
                evt.StopPropagation();
            });
            return splitter;
        }

        private VisualElement CreateSplitter(Action<bool> setResizing, Func<bool> isResizing, Action onFinish)
        {
            var splitter = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    top = 0,
                    width = 4
                }
            };

            var cursorContainer = new IMGUIContainer(() =>
                EditorGUIUtility.AddCursorRect(new Rect(0, 0, splitter.layout.width, splitter.layout.height), MouseCursor.SplitResizeLeftRight));
            cursorContainer.style.flexGrow = 1;
            splitter.Add(cursorContainer);

            splitter.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _focus();
                setResizing(true);
                splitter.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            splitter.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (isResizing())
                {
                    setResizing(false);
                    splitter.ReleasePointer(evt.pointerId);
                    onFinish();
                }
                evt.StopPropagation();
            });
            splitter.RegisterCallback<PointerEnterEvent>(_ => splitter.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f));
            splitter.RegisterCallback<PointerLeaveEvent>(_ => splitter.style.backgroundColor = Color.clear);
            return splitter;
        }
    }
}
#endif
