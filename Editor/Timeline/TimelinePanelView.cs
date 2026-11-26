using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView : VisualElement
    {
        private const float WheelScrollStep = 18f;
        private const float WheelZoomFactor = 1.15f;
        private const float ScrollerSize = 12f;

        private readonly TimelineState _state;
        private readonly Action _rebuild;
        private readonly Action _focus;
        private readonly Func<bool> _isActive;

        private VisualElement _mainArea;
        private VisualElement _timelineRoot;
        private VisualElement _leftColumn;
        private VisualElement _corner;
        private Label _cornerTimecode;
        private VisualElement _headerViewport;
        private VisualElement _headers;
        private VisualElement _rulerViewport;
        private VisualElement _rulerTicks;
        private VisualElement _rulerContent;
        private VisualElement _viewport;
        private VisualElement _gridBackground;
        private VisualElement _canvas;
        private Scroller _hScroller;
        private Scroller _vScroller;
        private VisualElement _headerSplitter;
        private VisualElement _emptyOverlay;
        private VisualElement _properties;
        private VisualElement _propsSplitter;
        private object _propertiesKey;
        private VisualElement _playheadLine;
        private VisualElement _playheadHead;
        private VisualElement _snapLine;
        private readonly Dictionary<object, VisualElement> _elements = new();
        private readonly Dictionary<HonamiTimelineMarker, VisualElement> _markerElements = new();

        private float _canvasWidth;
        private float _canvasHeight;
        private bool _draggingPlayhead;
        private bool _panning;
        private bool _applyingScroll;
        private Vector2 _panStartMouse;
        private Vector2 _panStartScroll;

        public TimelinePanelView(TimelineState state, Func<bool> isActive, Action focus, Action rebuild)
        {
            _state = state;
            _isActive = isActive;
            _focus = focus;
            _rebuild = () =>
            {
                ReleaseStaleProperties();
                rebuild();
            };

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

            BuildSkeleton();
            Build();
        }

        public void Refresh()
        {
            _state.PreModifyState();
            if (_state.Mode != TimelineMode.HonamiClipEdit)
                _state.PruneStaleModuleSelection();
            Build();
        }

        private void BuildSkeleton()
        {
            _timelineRoot = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Row,
                    minWidth = 0,
                    minHeight = 0,
                    borderTopWidth = 2,
                    borderTopColor = TimelineTheme.SubtleLine,
                    backgroundColor = TimelineTheme.PanelBg
                }
            };
            _mainArea.Insert(0, _timelineRoot);
            _timelineRoot.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            _timelineRoot.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            _timelineRoot.RegisterCallback<DragPerformEvent>(OnDragPerform);
            _timelineRoot.RegisterCallback<DragLeaveEvent>(_ => ClearDropGhost());
            _timelineRoot.RegisterCallback<DragExitedEvent>(_ => ClearDropGhost());

            _leftColumn = new VisualElement
            {
                style =
                {
                    flexShrink = 0,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = TimelineTheme.HeaderBg,
                    borderRightWidth = 1,
                    borderRightColor = TimelineTheme.Divider
                }
            };
            _timelineRoot.Add(_leftColumn);

            _corner = new VisualElement
            {
                style =
                {
                    height = TimelineTheme.RulerHeight,
                    flexShrink = 0,
                    backgroundColor = TimelineTheme.RulerBg,
                    borderBottomWidth = 1,
                    borderBottomColor = TimelineTheme.Divider,
                    justifyContent = Justify.Center,
                    paddingLeft = 10
                }
            };
            _cornerTimecode = new Label
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    color = TimelineTheme.Accent,
                    fontSize = 15,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            _corner.Add(_cornerTimecode);
            _leftColumn.Add(_corner);

            _headerViewport = new VisualElement { style = { flexGrow = 1, overflow = Overflow.Hidden, position = Position.Relative } };
            _leftColumn.Add(_headerViewport);
            _headers = new VisualElement { style = { position = Position.Absolute, left = 0, right = 0, top = 0 } };
            _headerViewport.Add(_headers);

            var rightColumn = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column, minWidth = 0, minHeight = 0 } };
            _timelineRoot.Add(rightColumn);

            _rulerViewport = new VisualElement
            {
                style =
                {
                    height = TimelineTheme.RulerHeight,
                    flexShrink = 0,
                    overflow = Overflow.Hidden,
                    position = Position.Relative,
                    backgroundColor = TimelineTheme.RulerBg,
                    borderBottomWidth = 1,
                    borderBottomColor = TimelineTheme.Divider
                }
            };
            rightColumn.Add(_rulerViewport);
            _rulerTicks = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
            };
            _rulerTicks.generateVisualContent += DrawRulerTicks;
            _rulerViewport.Add(_rulerTicks);
            _rulerContent = new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, top = 0, height = TimelineTheme.RulerHeight } };
            _rulerViewport.Add(_rulerContent);
            RegisterRulerEvents(_rulerViewport);

            var viewportRow = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row, minHeight = 0 } };
            rightColumn.Add(viewportRow);

            _viewport = new VisualElement { style = { flexGrow = 1, overflow = Overflow.Hidden, position = Position.Relative, minWidth = 0 } };
            viewportRow.Add(_viewport);
            _gridBackground = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
            };
            _gridBackground.generateVisualContent += DrawGridBackground;
            _viewport.Add(_gridBackground);
            _canvas = new VisualElement { style = { position = Position.Absolute, left = 0, top = 0 } };
            _viewport.Add(_canvas);
            RegisterCanvasEvents(_canvas);
            _viewport.RegisterCallback<GeometryChangedEvent>(_ => OnViewportResized());

            _vScroller = new Scroller(0f, 1f, value => { if (!_applyingScroll) SetScroll(_state.ScrollPos.x, value); }, SliderDirection.Vertical)
            {
                style = { width = ScrollerSize, flexShrink = 0 }
            };
            viewportRow.Add(_vScroller);

            _hScroller = new Scroller(0f, 1f, value => { if (!_applyingScroll) SetScroll(value, _state.ScrollPos.y); }, SliderDirection.Horizontal)
            {
                style = { height = ScrollerSize, flexShrink = 0, marginRight = ScrollerSize }
            };
            rightColumn.Add(_hScroller);

            _headerSplitter = BuildHeaderSplitter();
            _timelineRoot.Add(_headerSplitter);
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            RegisterRecording();
            Undo.undoRedoPerformed += OnUndoRedo;
            evt.destinationPanel.visualTree.RegisterCallback<PointerMoveEvent>(OnGlobalMove, TrickleDown.TrickleDown);
            evt.destinationPanel.visualTree.RegisterCallback<PointerUpEvent>(OnGlobalUp, TrickleDown.TrickleDown);
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            UnregisterRecording();
            Undo.undoRedoPerformed -= OnUndoRedo;
            evt.originPanel.visualTree.UnregisterCallback<PointerMoveEvent>(OnGlobalMove, TrickleDown.TrickleDown);
            evt.originPanel.visualTree.UnregisterCallback<PointerUpEvent>(OnGlobalUp, TrickleDown.TrickleDown);
        }

        private void OnUndoRedo() => _rebuild();

        private void StopSelection()
        {
            _state.IsBoxSelecting = false;
            _draggingPlayhead = false;
            _panning = false;
            _state.IsDraggingPlayhead = false;
            _state.SnapLineTime = -1f;
            _gapClickModule = null;
            _boxSelection?.RemoveFromHierarchy();
            _boxSelection = null;
            UpdatePropertiesPanel(PropertiesVisible());
            _rebuild();
        }

        private void OnGlobalMove(PointerMoveEvent evt)
        {
            if (!_state.IsBoxSelecting && !_draggingPlayhead && !_panning) return;

            if (_panning)
            {
                if ((evt.pressedButtons & 4) == 0)
                {
                    _panning = false;
                    return;
                }
                Vector2 delta = (Vector2)evt.position - _panStartMouse;
                SetScroll(_panStartScroll.x - delta.x, _panStartScroll.y - delta.y);
                return;
            }

            if ((evt.pressedButtons & 1) == 0)
            {
                StopSelection();
                return;
            }

            var pos = _canvas.WorldToLocal(evt.position);
            if (_state.IsBoxSelecting)
            {
                if (Vector2.Distance(_state.BoxSelectStartPos, pos) > DragThreshold)
                    _gapClickModule = null;

                float xMin = Mathf.Min(_state.BoxSelectStartPos.x, pos.x);
                float yMin = Mathf.Min(_state.BoxSelectStartPos.y, pos.y);
                float xMax = Mathf.Max(_state.BoxSelectStartPos.x, pos.x);
                float yMax = Mathf.Max(_state.BoxSelectStartPos.y, pos.y);
                _state.BoxSelectRect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                UpdateBoxSelection(evt.shiftKey || evt.ctrlKey || evt.commandKey);
                UpdateBoxSelectionVisual();
                RefreshSelectionVisuals();
            }
            else if (_draggingPlayhead)
            {
                SetPlayheadFromLocal(pos.x, false, evt.shiftKey);
            }
        }

        private void OnGlobalUp(PointerUpEvent evt)
        {
            bool hasGapClick = _gapClickModule != null && _state.IsBoxSelecting;
            if (hasGapClick) SelectGap(_gapClickModule, _gapClickStart, _gapClickEnd);

            if (_state.IsBoxSelecting || _draggingPlayhead || _panning)
                StopSelection();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (IsStateEmpty()) return;

            Vector2 delta = evt.delta;
            if (evt.ctrlKey || evt.commandKey || evt.altKey)
            {
                float anchor = _viewport.WorldToLocal(evt.mousePosition).x;
                if (anchor < 0f || anchor > ViewportWidth())
                    anchor = PlayheadViewportX();
                ZoomAt(delta.y < 0f ? WheelZoomFactor : 1f / WheelZoomFactor, anchor);
            }
            else if (evt.shiftKey)
            {
                float amount = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : delta.y;
                SetScroll(_state.ScrollPos.x + amount * WheelScrollStep * 2f, _state.ScrollPos.y);
            }
            else
            {
                SetScroll(_state.ScrollPos.x + delta.x * WheelScrollStep * 2f, _state.ScrollPos.y + delta.y * WheelScrollStep);
            }
            evt.StopPropagation();
        }

        private float PlayheadViewportX() => Mathf.Clamp(_state.PlayheadTime * _state.TimeScale - _state.ScrollPos.x, 0f, ViewportWidth());

        private void ZoomAt(float factor, float anchorViewportX)
        {
            float old = _state.TimeScale;
            float next = Mathf.Clamp(old * factor, TimelineState.MinTimeScale, TimelineState.MaxTimeScale);
            if (Mathf.Approximately(old, next)) return;

            float anchorTime = (_state.ScrollPos.x + anchorViewportX) / old;
            _state.TimeScale = next;
            _state.ScrollPos.x = anchorTime * next - anchorViewportX;
            _state.SaveSettings();
            Build();
        }

        private void ZoomToRange(float start, float end)
        {
            float width = ViewportWidth();
            if (width < 10f) return;
            float span = Mathf.Max(end - start, _state.FrameDuration * 4f);
            _state.TimeScale = Mathf.Clamp(width * 0.9f / span, TimelineState.MinTimeScale, TimelineState.MaxTimeScale);
            _state.ScrollPos.x = start * _state.TimeScale - width * 0.05f;
            _state.SaveSettings();
            Build();
        }

        private void FitAll() => ZoomToRange(0f, _state.GetDuration());

        private float ViewportWidth()
        {
            float width = _viewport.layout.width;
            return float.IsNaN(width) || width < 1f ? 800f : width;
        }

        private float ViewportHeight()
        {
            float height = _viewport.layout.height;
            return float.IsNaN(height) || height < 1f ? 300f : height;
        }

        private void OnViewportResized()
        {
            if (IsStateEmpty())
            {
                ApplyScroll();
                return;
            }
            float width = ComputeTimelineWidth();
            float height = ComputeContentHeight();
            if (!Mathf.Approximately(width, _canvasWidth) || !Mathf.Approximately(height, _canvasHeight))
            {
                Build();
                return;
            }
            ApplyScroll();
        }

        private void SetScroll(float x, float y)
        {
            _state.ScrollPos = new Vector2(x, y);
            ApplyScroll();
        }

        private void ApplyScroll()
        {
            float maxX = Mathf.Max(0f, _canvasWidth - ViewportWidth());
            float maxY = Mathf.Max(0f, _canvasHeight - ViewportHeight());
            float x = Mathf.Clamp(_state.ScrollPos.x, 0f, maxX);
            float y = Mathf.Clamp(_state.ScrollPos.y, 0f, maxY);
            _state.ScrollPos = new Vector2(x, y);

            _canvas.style.left = -x;
            _canvas.style.top = -y;
            _rulerContent.style.left = -x;
            _headers.style.top = -y;

            _applyingScroll = true;
            UpdateScroller(_hScroller, x, maxX, ViewportWidth(), _canvasWidth);
            UpdateScroller(_vScroller, y, maxY, ViewportHeight(), _canvasHeight);
            _applyingScroll = false;

            _rulerTicks.MarkDirtyRepaint();
            _gridBackground.MarkDirtyRepaint();
        }

        // Shrinking the range clamps the slider and fires its callback, so the value is set before and after the range.
        private static void UpdateScroller(Scroller scroller, float value, float max, float viewport, float content)
        {
            scroller.slider.SetValueWithoutNotify(value);
            scroller.lowValue = 0f;
            scroller.highValue = Mathf.Max(0.001f, max);
            scroller.slider.pageSize = viewport;
            scroller.Adjust(content > 0f ? Mathf.Clamp01(viewport / content) : 1f);
            scroller.slider.SetValueWithoutNotify(value);
            scroller.SetEnabled(max > 0.5f);
        }

        private void FollowPlayhead()
        {
            if (_state.IsDraggingPlayhead) return;
            float width = ViewportWidth();
            float x = _state.PlayheadTime * _state.TimeScale;
            float left = _state.ScrollPos.x;
            if (x > left + width - 24f)
                SetScroll(x - width * 0.1f, _state.ScrollPos.y);
            else if (x < left)
                SetScroll(x - width * 0.9f, _state.ScrollPos.y);
        }

        private void ModifyModules(Action<IHonamiTimelineModuleContainer> change)
        {
            _state.PreModifyState();
            var container = _state.ActiveContainer;
            if (container == null) return;
            change(container);
            _state.PostModifyState();

            // If preview is active, force a full re-sample so any modules that were
            // just muted (or unmuted) immediately reflect their new state in the scene.
            // Without this, RepaintViews skips the pass when the playhead hasn't moved.
            if (_state.PreviewEnabled)
            {
                HonamiTimelineModuleEditorRegistry.StopAllPreviews();
                _state.LastSampledTime = float.NaN;
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

        private float ComputeTimelineWidth()
        {
            float viewport = ViewportWidth();
            return Mathf.Max(_state.GetDuration() * _state.TimeScale + viewport * 0.5f, viewport);
        }

        private float ComputeContentHeight()
        {
            float addRow = HasAddModuleRow ? AddModuleRowHeight : 0f;
            return Mathf.Max(ViewportHeight(), _rowCount * TimelineTheme.RowHeight + addRow + 40f);
        }

        private void Build()
        {
            // Save any active inline rename before clearing headers
            CommitActiveRename(true);
            _elements.Clear();
            _markerElements.Clear();
            _headers.Clear();
            _canvas.Clear();
            _rulerContent.Clear();
            _playheadLine = _playheadHead = _snapLine = null;
            _dropGhost = null;
            _bladeLine = null;
            _boxSelection = null;
            _emptyOverlay?.RemoveFromHierarchy();
            _emptyOverlay = null;

            _timelineRoot.style.borderTopColor = _isActive() ? TimelineTheme.Accent : TimelineTheme.SubtleLine;
            _leftColumn.style.width = _state.TrackHeaderWidth;
            _headerSplitter.style.left = _state.TrackHeaderWidth - 2f;

            bool empty = IsStateEmpty();
            _rowCount = empty ? 0 : GetRows().Count;
            _canvasWidth = empty ? ViewportWidth() : ComputeTimelineWidth();
            _canvasHeight = empty ? ViewportHeight() : ComputeContentHeight();
            _canvas.style.width = _canvasWidth;
            _canvas.style.height = _canvasHeight;
            _rulerContent.style.width = _canvasWidth;
            _headers.style.height = _canvasHeight;

            if (empty)
            {
                DrawEmptyState();
            }
            else
            {
                DrawRows();
                DrawContent();
                DrawRulerMarkers();
                DrawSnapLine();
                DrawPlayhead();
            }

            ApplyScroll();
            UpdatePropertiesPanel(PropertiesVisible());
            RefreshTransport();
            RefreshCornerTimecode();
        }

        private void RefreshCornerTimecode()
        {
            if (_cornerTimecode == null) return;
            _cornerTimecode.text = IsStateEmpty() ? string.Empty : TimelineTimecode.Format(_state.PlayheadTime, _state.FrameRate, _state.EffectiveTimeFormat);
        }

        private void UpdatePropertiesPanel(bool visible)
        {
            if (!visible)
            {
                _properties?.RemoveFromHierarchy();
                _propsSplitter?.RemoveFromHierarchy();
                _properties = null;
                _propsSplitter = null;
                _propertiesKey = null;
                return;
            }

            if (_properties == null)
            {
                _properties = new VisualElement
                {
                    style =
                    {
                        flexShrink = 0,
                        backgroundColor = TimelineTheme.PanelBg,
                        borderLeftWidth = 2,
                        borderLeftColor = TimelineTheme.AccentDim
                    }
                };
                _propsSplitter = BuildPropsSplitter();
                _mainArea.Add(_properties);
                _mainArea.Add(_propsSplitter);
                _propertiesKey = null;
            }

            float propsWidth = Mathf.Clamp(_state.PropsWidth > 0f ? _state.PropsWidth : TimelineTheme.PropsWidth, 180f, 700f);
            _state.PropsWidth = propsWidth;
            _properties.style.width = propsWidth;
            _properties.style.minWidth = 180f;
            _properties.style.maxWidth = 700f;
            _properties.style.display = DisplayStyle.Flex;
            _propsSplitter.style.right = propsWidth - 2;

            object key = BoundPropertiesKey();
            if (key != null && key.Equals(_propertiesKey) && _properties.childCount > 0) return;

            try
            {
                BuildProperties();
                _propertiesKey = key;
            }
            catch (Exception ex)
            {
                _propertiesKey = null;
                Debug.LogException(ex);
            }
        }

        private VisualElement BuildHeaderSplitter()
        {
            var splitter = CreateSplitter(
                value => _state.IsResizingTrackHeader = value,
                () => _state.IsResizingTrackHeader,
                onFinish: () => { _state.SaveSettings(); _rebuild(); });
            splitter.style.bottom = 0;

            splitter.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_state.IsResizingTrackHeader || !splitter.HasPointerCapture(evt.pointerId)) return;
                float newWidth = Mathf.Clamp(_timelineRoot.WorldToLocal(evt.position).x, 120f, 600f);
                if (Mathf.Abs(_state.TrackHeaderWidth - newWidth) > 0.5f)
                {
                    _state.TrackHeaderWidth = newWidth;
                    _leftColumn.style.width = newWidth;
                    splitter.style.left = newWidth - 2;
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
                onFinish: () => { _state.SaveSettings(); _rebuild(); });
            splitter.style.right = _state.PropsWidth - 2;
            splitter.style.bottom = 0;

            splitter.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_state.IsResizingPropsPanel || !splitter.HasPointerCapture(evt.pointerId)) return;
                float parentWidth = _mainArea.resolvedStyle.width;
                float newWidth = Mathf.Clamp(parentWidth - _mainArea.WorldToLocal(evt.position).x, 180f, 700f);
                if (Mathf.Abs(_state.PropsWidth - newWidth) > 0.5f)
                {
                    _state.PropsWidth = newWidth;
                    _properties.style.width = newWidth;
                    splitter.style.right = newWidth - 2;
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
            splitter.AddManipulator(new HonamiTimelineItemView.CursorManipulator(MouseCursor.SplitResizeLeftRight));

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
