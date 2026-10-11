#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>Read-only authored motion points with a draggable, preview-only XY input.</summary>
    internal sealed class BlendTreeDirectionalMap : VisualElement
    {
        private const float MinimumExtent = 0.5f;
        private const float RangePadding = 1.15f;

        private readonly BlendTreeState _state;
        private readonly Action<Vector2> _onInputChanged;
        private readonly VisualElement _plot;
        private readonly Label _axisLabel;
        private readonly Label _inputLabel;
        private readonly Label[] _xTicks = new Label[3];
        private readonly Label[] _yTicks = new Label[3];
        private Vector2 _input;
        private Rect _gestureRect;
        private float _extent = 1f;
        private float _gestureExtent;
        private int _positionsHash;
        private bool _hasPositionsHash;
        private bool _dragging;
        private int _pointerId = -1;

        public BlendTreeDirectionalMap(BlendTreeState state, Action<Vector2> onInputChanged)
        {
            _state = state;
            _onInputChanged = onInputChanged;
            _input = state != null ? state.PreviewInput : Vector2.zero;
            name = "honami-directional-map";
            style.height = 172;
            style.flexShrink = 0;
            style.marginBottom = 4;
            style.flexDirection = FlexDirection.Column;

            var header = new VisualElement { style = { height = 16, flexShrink = 0, flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            _axisLabel = new Label { style = { flexGrow = 1, fontSize = 9, color = BlendTreeTheme.MutedText, overflow = Overflow.Hidden, whiteSpace = WhiteSpace.NoWrap } };
            _inputLabel = new Label { style = { fontSize = 9, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(1f, 0.35f, 0.32f), unityTextAlign = TextAnchor.MiddleRight, flexShrink = 0 } };
            header.Add(_axisLabel);
            header.Add(_inputLabel);
            Add(header);

            _plot = new VisualElement { name = "directional-map-plot", pickingMode = PickingMode.Position };
            _plot.style.flexGrow = 1;
            _plot.style.minHeight = 146;
            _plot.style.backgroundColor = new Color(0.105f, 0.115f, 0.13f, 1f);
            _plot.style.borderTopWidth = _plot.style.borderBottomWidth = _plot.style.borderLeftWidth = _plot.style.borderRightWidth = 1;
            _plot.style.borderTopColor = _plot.style.borderBottomColor = _plot.style.borderLeftColor = _plot.style.borderRightColor = BlendTreeTheme.Divider;
            _plot.generateVisualContent += DrawMap;
            _plot.RegisterCallback<GeometryChangedEvent>(_ => UpdateTickLabels());
            _plot.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _plot.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _plot.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _plot.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            _plot.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            _plot.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());

            for (int i = 0; i < 3; i++)
            {
                _xTicks[i] = MakeTick(TextAnchor.UpperCenter);
                _yTicks[i] = MakeTick(TextAnchor.MiddleRight);
                _plot.Add(_xTicks[i]);
                _plot.Add(_yTicks[i]);
            }

            Add(_plot);
            RegisterCallback<DetachFromPanelEvent>(_ => EndGesture(true));
            RecalculateRange(true);
            RefreshHeader();
        }

        public void SetPreviewInput(Vector2 input)
        {
            if (!Finite(input.x) || !Finite(input.y)) return;
            _input = input;
            EnsurePositionsCurrent();
            if (!_dragging) RecalculateRange(true);
            RefreshHeader();
            _plot.MarkDirtyRepaint();
        }

        private static Label MakeTick(TextAnchor alignment)
        {
            return new Label { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, width = 38, height = 12, fontSize = 8, color = BlendTreeTheme.MutedText, unityTextAlign = alignment } };
        }

        private void RefreshHeader()
        {
            string x = !string.IsNullOrEmpty(_state?.Node?.blendParameter) ? _state.Node.blendParameter : "X";
            string y = !string.IsNullOrEmpty(_state?.Node?.blendParameterY) ? _state.Node.blendParameterY : "Y";
            string error = _state != null ? _state.DirectionalPreviewError : null;
            _axisLabel.text = string.IsNullOrEmpty(error) ? $"X: {x}   Y: {y}" : $"Invalid blend · X: {x}   Y: {y}";
            _axisLabel.tooltip = error ?? string.Empty;
            _inputLabel.text = $"{_input.x:0.##}, {_input.y:0.##}";
        }

        private void DrawMap(MeshGenerationContext context)
        {
            EnsurePositionsCurrent();
            Rect rect = PlotRect(_plot.contentRect);
            if (rect.width < 8f) return;
            Painter2D p = context.painter2D;
            DrawGrid(p, rect);
            DrawMotionPoints(p, rect);
            DrawInputPoint(p, rect);
        }

        private static Rect PlotRect(Rect content)
        {
            const float left = 34f, right = 4f, top = 4f, bottom = 22f;
            float width = Mathf.Max(0f, content.width - left - right);
            float height = Mathf.Max(0f, content.height - top - bottom);
            float side = Mathf.Min(width, height);
            return new Rect(left + (width - side) * 0.5f, top + (height - side) * 0.5f, side, side);
        }

        private void DrawGrid(Painter2D p, Rect r)
        {
            for (int i = 0; i <= 4; i++)
            {
                float t = i * 0.25f, x = r.x + r.width * t, y = r.y + r.height * t;
                bool axis = i == 2;
                p.strokeColor = axis ? new Color(0.58f, 0.62f, 0.68f, 0.82f) : new Color(0.32f, 0.35f, 0.4f, 0.42f);
                p.lineWidth = axis ? 1.1f : 0.7f;
                p.BeginPath(); p.MoveTo(new Vector2(x, r.y)); p.LineTo(new Vector2(x, r.yMax)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(r.x, y)); p.LineTo(new Vector2(r.xMax, y)); p.Stroke();
            }
            p.strokeColor = new Color(0.55f, 0.58f, 0.63f, 0.72f); p.lineWidth = 1f;
            p.BeginPath(); p.MoveTo(r.min); p.LineTo(new Vector2(r.xMax, r.yMin)); p.LineTo(r.max); p.LineTo(new Vector2(r.xMin, r.yMax)); p.ClosePath(); p.Stroke();
        }

        private void DrawMotionPoints(Painter2D p, Rect rect)
        {
            var motions = _state?.Node?.blendMotions;
            if (motions == null) return;
            for (int i = 0; i < motions.Count; i++)
            {
                HonamiBlendTreeMotion motion = motions[i];
                if (motion == null || !Finite(motion.position.x) || !Finite(motion.position.y)) continue;
                float weight = _state.Weights != null && i < _state.Weights.Length ? Mathf.Clamp01(_state.Weights[i]) : 0f;
                Color color = BlendTreeTheme.MotionColor(i, motions.Count);
                color.a = 0.28f + 0.72f * weight;
                float radius = 3.2f + 3.8f * Mathf.Sqrt(weight);
                DrawDiamond(p, ToScreen(motion.position, rect) + DuplicateOffset(motions, i), radius, color);
            }
        }

        private static Vector2 DuplicateOffset(IReadOnlyList<HonamiBlendTreeMotion> motions, int index)
        {
            HonamiBlendTreeMotion current = motions[index];
            int count = 0, ordinal = 0;
            for (int i = 0; i < motions.Count; i++)
            {
                HonamiBlendTreeMotion candidate = motions[i];
                if (candidate == null || candidate.position != current.position) continue;
                if (i < index) ordinal++;
                count++;
            }
            if (count < 2) return Vector2.zero;
            float angle = ordinal * (Mathf.PI * 2f / count) - Mathf.PI * 0.5f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Min(5.5f, 2.5f + count * 0.35f);
        }

        private void DrawInputPoint(Painter2D p, Rect rect)
        {
            Vector2 point = ToScreen(_input, rect);
            point.x = Mathf.Clamp(point.x, rect.xMin + 3f, rect.xMax - 3f);
            point.y = Mathf.Clamp(point.y, rect.yMin + 3f, rect.yMax - 3f);
            p.fillColor = new Color(1f, 0.27f, 0.25f, 1f); p.BeginPath(); p.Arc(point, 5f, 0f, 360f); p.Fill();
            p.strokeColor = Color.white; p.lineWidth = 1.2f; p.BeginPath(); p.Arc(point, 5f, 0f, 360f); p.Stroke();
        }

        private static void DrawDiamond(Painter2D p, Vector2 c, float r, Color color)
        {
            p.fillColor = color; p.BeginPath();
            p.MoveTo(c + Vector2.up * r); p.LineTo(c + Vector2.right * r); p.LineTo(c - Vector2.up * r); p.LineTo(c - Vector2.right * r); p.ClosePath(); p.Fill();
            p.strokeColor = new Color(0.08f, 0.09f, 0.1f, 0.9f); p.lineWidth = 0.8f; p.BeginPath();
            p.MoveTo(c + Vector2.up * r); p.LineTo(c + Vector2.right * r); p.LineTo(c - Vector2.up * r); p.LineTo(c - Vector2.right * r); p.ClosePath(); p.Stroke();
        }

        private Vector2 ToScreen(Vector2 v, Rect r, float extent = -1f)
        {
            if (extent <= 0f) extent = _extent;
            return new Vector2(r.x + (v.x + extent) / (extent * 2f) * r.width, r.y + (extent - v.y) / (extent * 2f) * r.height);
        }

        private Vector2 ToWorld(Vector2 p, Rect r, float extent)
        {
            float diameter = extent * 2f;
            return new Vector2((p.x - r.x) / r.width * diameter - extent, extent - (p.y - r.y) / r.height * diameter);
        }

        private void UpdateTickLabels()
        {
            if (_xTicks[0] == null) return;
            Rect r = PlotRect(_plot.contentRect);
            if (r.width < 1f) return;
            for (int i = 0; i < 3; i++)
            {
                float t = i * 0.5f;
                _xTicks[i].text = Mathf.Lerp(-_extent, _extent, t).ToString("0.##");
                _xTicks[i].style.left = r.x + r.width * t - 19f; _xTicks[i].style.top = r.yMax + 3f;
                _yTicks[i].text = Mathf.Lerp(_extent, -_extent, t).ToString("0.##");
                _yTicks[i].style.left = r.x - 40f; _yTicks[i].style.top = r.y + r.height * t - 6f;
            }
        }

        private void EnsurePositionsCurrent()
        {
            int hash = 17;
            var motions = _state?.Node?.blendMotions;
            hash = hash * 31 + (motions != null ? motions.Count : -1);
            if (motions != null)
                for (int i = 0; i < motions.Count; i++) hash = hash * 31 + (motions[i] != null ? motions[i].position.GetHashCode() : 0);
            if (_hasPositionsHash && hash == _positionsHash) return;
            _hasPositionsHash = true; _positionsHash = hash;
            if (!_dragging) RecalculateRange(true);
        }

        private void RecalculateRange(bool includeInput)
        {
            float max = MinimumExtent;
            var motions = _state?.Node?.blendMotions;
            if (motions != null)
                for (int i = 0; i < motions.Count; i++)
                {
                    HonamiBlendTreeMotion motion = motions[i];
                    if (motion == null || !Finite(motion.position.x) || !Finite(motion.position.y)) continue;
                    max = Mathf.Max(max, Mathf.Abs(motion.position.x), Mathf.Abs(motion.position.y));
                }
            if (includeInput) max = Mathf.Max(max, Mathf.Abs(_input.x), Mathf.Abs(_input.y));
            _extent = Mathf.Max(MinimumExtent, max * RangePadding);
            if (_plot != null)
            {
                _plot.schedule.Execute(UpdateTickLabels);
                _plot.MarkDirtyRepaint();
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.altKey) return;
            EnsurePositionsCurrent();
            _gestureRect = PlotRect(_plot.contentRect);
            Vector2 local = _plot.WorldToLocal(evt.position);
            if (!_gestureRect.Contains(local)) return;
            _gestureExtent = _extent; _dragging = true; _pointerId = evt.pointerId;
            _plot.CapturePointer(_pointerId); SetFromPointer(local); evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId || !_plot.HasPointerCapture(_pointerId)) return;
            SetFromPointer(_plot.WorldToLocal(evt.position)); evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt) { if (evt.button == 0 && IsGestureEvent(evt.pointerId)) { evt.StopPropagation(); EndGesture(true); } }
        private void OnPointerCancel(PointerCancelEvent evt) { if (IsGestureEvent(evt.pointerId)) { evt.StopPropagation(); EndGesture(true); } }
        private bool IsGestureEvent(int id) => _dragging && id == _pointerId;
        private void OnPointerCaptureOut(PointerCaptureOutEvent _) { if (_dragging) EndGesture(true, false); }

        private void SetFromPointer(Vector2 local)
        {
            _input = ToWorld(local, _gestureRect, _gestureExtent);
            if (!Finite(_input.x) || !Finite(_input.y)) return;
            RefreshHeader(); _onInputChanged?.Invoke(_input); _plot.MarkDirtyRepaint();
        }

        private void EndGesture(bool reframe, bool releaseCapture = true)
        {
            int id = _pointerId; _dragging = false; _pointerId = -1;
            if (releaseCapture && id >= 0 && _plot.HasPointerCapture(id)) _plot.ReleasePointer(id);
            _input = _state != null ? _state.PreviewInput : _input;
            if (reframe) RecalculateRange(true);
            RefreshHeader(); _plot.MarkDirtyRepaint();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
#endif
