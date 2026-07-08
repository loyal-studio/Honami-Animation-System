#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>
    /// Pannable and zoomable node surface: renders the background grid and applies the
    /// view transform to its content container, which holds the edges and node cards.
    /// </summary>
    internal sealed class BlendTreeGraphCanvas : VisualElement
    {
        private readonly BlendTreeState _state;
        private readonly BlendTreePanelView _panel;

        private VisualElement _contentContainer;
        private bool _isPanning;
        private Vector2 _panStartMouse;
        private Vector3 _panStartPosition;
        private bool _pendingFrameAll;

        public VisualElement ContentContainer => _contentContainer;

        public BlendTreeGraphCanvas(BlendTreeState state, BlendTreePanelView panel)
        {
            _state = state;
            _panel = panel;

            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;

            Insert(0, new HonamiGridBackground(10f, 10,
                () => new Vector2(_state.ViewPosition.x, _state.ViewPosition.y),
                () => _state.ViewScale));

            _contentContainer = new VisualElement
            {
                name = "content-container",
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, right = 0, bottom = 0,
                    transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0))
                }
            };
            Add(_contentContainer);

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
        }

        public void SetZoom(float scale)
        {
            scale = Mathf.Clamp(scale, BlendTreeTheme.ZoomMin, BlendTreeTheme.ZoomMax);

            Vector2 center = layout.size * 0.5f;
            Vector2 currentPosition = new Vector2(_state.ViewPosition.x, _state.ViewPosition.y);
            float currentScale = _state.ViewScale;
            if (!IsFinite(currentScale) || currentScale <= 0f) currentScale = 1f;

            if (Mathf.Approximately(currentScale, scale)) return;

            Vector2 world = (center - currentPosition) / currentScale;
            Vector2 newPosition = center - world * scale;

            UpdateTransform(new Vector3(newPosition.x, newPosition.y, 0f), scale);
        }

        public void UpdateTransform(Vector3 position, float scale)
        {
            if (!IsFinite(scale) || scale <= 0f) scale = 1f;
            scale = Mathf.Clamp(scale, BlendTreeTheme.ZoomMin, BlendTreeTheme.ZoomMax);
            if (!IsFinite(position.x) || !IsFinite(position.y)) position = Vector3.zero;

            _state.ViewPosition = position;
            _state.ViewScale = scale;
            _state.HasViewTransform = true;

            _contentContainer.style.translate = new Translate(position.x, position.y, position.z);
            _contentContainer.style.scale = new Scale(new Vector2(scale, scale));

            _panel.OnCanvasZoomChanged(scale);
            MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (evt.shiftKey || evt.altKey) return;

            float zoomDelta = -evt.delta.y * 0.05f;
            float newScale = Mathf.Clamp(_state.ViewScale + zoomDelta, BlendTreeTheme.ZoomMin, BlendTreeTheme.ZoomMax);

            if (!Mathf.Approximately(_state.ViewScale, newScale))
            {
                Vector2 mousePos = evt.localMousePosition;
                Vector2 currentPosition = new Vector2(_state.ViewPosition.x, _state.ViewPosition.y);
                Vector2 world = (mousePos - currentPosition) / _state.ViewScale;
                Vector2 newPosition = mousePos - world * newScale;

                UpdateTransform(new Vector3(newPosition.x, newPosition.y, 0f), newScale);
                evt.StopPropagation();
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                _isPanning = true;
                _panStartMouse = evt.position;
                _panStartPosition = _state.ViewPosition;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_isPanning && this.HasPointerCapture(evt.pointerId))
            {
                Vector2 delta = (Vector2)evt.position - _panStartMouse;
                Vector3 newPos = _panStartPosition + new Vector3(delta.x, delta.y, 0f);
                UpdateTransform(newPos, _state.ViewScale);
                evt.StopPropagation();
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_isPanning && this.HasPointerCapture(evt.pointerId))
            {
                _isPanning = false;
                this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_isPanning)
            {
                _isPanning = false;
            }
        }

        private void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is VisualElement target && FindMotionCard(target) is { } motionCard)
            {
                int index = motionCard.Index;
                evt.menu.AppendAction("Duplicate Motion", _ => _panel.DuplicateMotion(index));
                evt.menu.AppendAction("Remove Motion", _ => _panel.RemoveMotion(index));
                evt.menu.AppendSeparator();
            }

            evt.menu.AppendAction("Add Motion", _ => _panel.AddMotion(null));
            evt.menu.AppendAction("Distribute Thresholds Evenly", _ => _panel.DistributeThresholds(),
                _state.MotionCount > 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Frame All", _ => _panel.FrameAll());
        }

        private static BlendTreeMotionCard FindMotionCard(VisualElement element)
        {
            while (element != null)
            {
                if (element is BlendTreeMotionCard card) return card;
                element = element.parent;
            }
            return null;
        }
        
        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        public void FrameAllWhenReady()
        {
            if (layout.width > 1f && layout.height > 1f)
            {
                FrameAll();
                return;
            }

            _pendingFrameAll = true;
            RegisterCallback<GeometryChangedEvent>(OnGeometryReadyForFrame);
        }

        private void OnGeometryReadyForFrame(GeometryChangedEvent evt)
        {
            if (!_pendingFrameAll || evt.newRect.width <= 1f || evt.newRect.height <= 1f) return;
            _pendingFrameAll = false;
            UnregisterCallback<GeometryChangedEvent>(OnGeometryReadyForFrame);
            FrameAll();
        }

        public void FrameAll()
        {
            if (_contentContainer.childCount == 0) return;

            Rect bounds = new Rect();
            bool first = true;

            foreach (var child in _contentContainer.Children())
            {
                if (child.style.display == DisplayStyle.None) continue;
                if (child is BlendTreeGraphEdges) continue;

                var childRect = child.layout;
                if (float.IsNaN(childRect.x))
                {
                    childRect = new Rect(
                        child.style.left.value.value, 
                        child.style.top.value.value, 
                        child.style.width.value.value > 0 ? child.style.width.value.value : 100f, 
                        child.style.height.value.value > 0 ? child.style.height.value.value : 100f);
                }

                if (first)
                {
                    bounds = childRect;
                    first = false;
                }
                else
                {
                    bounds.xMin = Mathf.Min(bounds.xMin, childRect.xMin);
                    bounds.xMax = Mathf.Max(bounds.xMax, childRect.xMax);
                    bounds.yMin = Mathf.Min(bounds.yMin, childRect.yMin);
                    bounds.yMax = Mathf.Max(bounds.yMax, childRect.yMax);
                }
            }

            if (first) return;

            bounds.xMin -= 50f;
            bounds.xMax += 50f;
            bounds.yMin -= 50f;
            bounds.yMax += 50f;

            if (bounds.width <= 0f || bounds.height <= 0f || !IsFinite(bounds.width) || !IsFinite(bounds.height))
            {
                UpdateTransform(Vector3.zero, 1f);
                return;
            }

            float viewWidth = layout.width > 0 ? layout.width : 800f;
            float viewHeight = layout.height > 0 ? layout.height : 600f;

            float scaleX = viewWidth / bounds.width;
            float scaleY = viewHeight / bounds.height;
            float newScale = Mathf.Min(scaleX, scaleY);
            newScale = Mathf.Clamp(newScale, BlendTreeTheme.ZoomMin, 1f);
            if (!IsFinite(newScale) || newScale <= 0f) newScale = 1f;

            Vector2 center = bounds.center;
            Vector2 viewCenter = new Vector2(viewWidth * 0.5f, viewHeight * 0.5f);
            Vector2 newPosition = viewCenter - center * newScale;

            UpdateTransform(new Vector3(newPosition.x, newPosition.y, 0f), newScale);
        }
    }
}
#endif
