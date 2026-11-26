using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Core;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendSpace
{
    /// <summary>
    /// Interactive blend space plot. Spaces with more than two axes are shown as a projection onto a
    /// chosen pair; dragging edits only those two coordinates and leaves the remaining axes alone.
    /// Animation clips dropped onto the field become motions at the drop position.
    /// </summary>
    internal sealed class HonamiBlendSpaceField : VisualElement
    {
        private const float PointRadius = 6f;
        private const float PickRadius = 12f;
        private const float SampleRadius = 7f;
        private const int GridDivisions = 4;
        private const float Padding = 34f;

        private readonly BlendSpaceState _state;
        private readonly Action _onSampleChanged;
        private readonly Action _onMotionsChanged;
        private readonly List<Label> _motionLabels = new();

        private Label _axisXLabel;
        private Label _axisYLabel;
        private Label _hint;
        private VisualElement _dropOverlay;

        private int _draggedMotion = -1;
        private bool _draggingSample;
        private float _extent = 1f;

        public HonamiBlendSpaceField(BlendSpaceState state, Action onSampleChanged, Action onMotionsChanged)
        {
            _state = state;
            _onSampleChanged = onSampleChanged;
            _onMotionsChanged = onMotionsChanged;

            name = "honami-blendspace-field";
            style.flexGrow = 1;
            style.minWidth = 0;
            style.minHeight = 0;
            style.backgroundColor = new Color(0.08f, 0.085f, 0.095f);
            style.overflow = Overflow.Hidden;
            focusable = true;

            generateVisualContent += OnGenerateVisualContent;

            BuildChrome();

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<GeometryChangedEvent>(_ => Refresh());

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DragLeaveEvent>(_ => SetDropHighlight(false));
            RegisterCallback<DragExitedEvent>(_ => SetDropHighlight(false));
        }

        private void BuildChrome()
        {
            _axisXLabel = AxisCaption();
            Add(_axisXLabel);

            _axisYLabel = AxisCaption();
            Add(_axisYLabel);

            _hint = new Label
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    fontSize = 11,
                    color = HonamiEditorTheme.MutedText,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace = WhiteSpace.Normal,
                    display = DisplayStyle.None
                }
            };
            Add(_hint);

            _dropOverlay = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    display = DisplayStyle.None,
                    borderTopWidth = 2,
                    borderBottomWidth = 2,
                    borderLeftWidth = 2,
                    borderRightWidth = 2,
                    backgroundColor = new Color(HonamiEditorTheme.Accent.r, HonamiEditorTheme.Accent.g, HonamiEditorTheme.Accent.b, 0.08f)
                }
            };
            _dropOverlay.BorderColor(HonamiEditorTheme.Accent);
            Add(_dropOverlay);
        }

        private static Label AxisCaption() => new()
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                position = Position.Absolute,
                fontSize = 10,
                unityFontStyleAndWeight = FontStyle.Bold,
                color = HonamiEditorTheme.MutedText
            }
        };

        public void Refresh()
        {
            _extent = _state.GetPlotExtent();
            RebuildMotionLabels();
            LayoutChrome();
            MarkDirtyRepaint();
        }

        private void RebuildMotionLabels()
        {
            var motions = _state.Node?.blendMotions;
            int count = motions?.Count ?? 0;

            while (_motionLabels.Count > count)
            {
                Remove(_motionLabels[^1]);
                _motionLabels.RemoveAt(_motionLabels.Count - 1);
            }

            while (_motionLabels.Count < count)
            {
                var label = new Label
                {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        position = Position.Absolute,
                        fontSize = 9,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        width = 150
                    }
                };
                Add(label);
                _motionLabels.Add(label);
            }

            for (int i = 0; i < _motionLabels.Count; i++)
            {
                var motion = motions[i];
                float weight = i < _state.Weights.Length ? _state.Weights[i] : 0f;
                string name = motion?.clip != null ? motion.clip.name : $"Motion {i}";

                _motionLabels[i].text = weight > 0.001f ? $"{name}  {weight * 100f:F0}%" : name;

                Color color = MotionColor(i, _motionLabels.Count);
                _motionLabels[i].style.color = weight > 0.001f
                    ? color
                    : new Color(color.r, color.g, color.b, 0.55f);
            }
        }

        private void LayoutChrome()
        {
            Rect rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f) return;

            bool hasNode = _state.Node != null;
            bool hasAxes = _state.AxisCount > 0;

            _axisXLabel.text = hasAxes ? _state.AxisName(_state.AxisX) + " →" : string.Empty;
            _axisYLabel.text = hasAxes ? "↑ " + _state.AxisName(_state.AxisY) : string.Empty;

            Vector2 center = Center;
            _axisXLabel.style.left = rect.width - 130f;
            _axisXLabel.style.top = center.y + 6f;
            _axisXLabel.style.width = 120f;
            _axisXLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            _axisYLabel.style.left = center.x + 8f;
            _axisYLabel.style.top = 8f;
            _axisYLabel.style.width = 160f;
            _axisYLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            for (int i = 0; i < _motionLabels.Count; i++)
            {
                Vector2 pixel = ToPixel(_state.Node.blendMotions[i]);
                _motionLabels[i].style.left = pixel.x - 75f;
                _motionLabels[i].style.top = pixel.y + PointRadius + 3f;
            }

            string hintText = null;
            if (!hasNode) hintText = null;
            else if (!hasAxes) hintText = "This blend space has no axes yet.\nAdd one in the properties panel - each axis is driven by a float parameter.";
            else if (_state.MotionCount == 0) hintText = "No motions yet.\nDrop animation clips here, or use + Add Motion.";

            _hint.text = hintText ?? string.Empty;
            _hint.style.display = string.IsNullOrEmpty(hintText) ? DisplayStyle.None : DisplayStyle.Flex;
            _hint.style.left = 0;
            _hint.style.width = rect.width;
            _hint.style.top = rect.height * 0.5f - 40f;
        }

        private Vector2 Center => new(contentRect.width * 0.5f, contentRect.height * 0.5f);

        private float HalfSize => Mathf.Max(1f, Mathf.Min(contentRect.width, contentRect.height) * 0.5f - Padding);

        private Vector2 ToPixel(float x, float y)
        {
            float scale = HalfSize / _extent;
            Vector2 center = Center;
            return new Vector2(center.x + x * scale, center.y - y * scale);
        }

        private Vector2 ToPixel(HonamiBlendSpaceMotion motion)
            => ToPixel(HonamiBlendSpaceSolver.GetAxis(motion, _state.AxisX), HonamiBlendSpaceSolver.GetAxis(motion, _state.AxisY));

        private Vector2 ToWorld(Vector2 pixel)
        {
            float scale = HalfSize / _extent;
            Vector2 center = Center;
            return new Vector2((pixel.x - center.x) / scale, -(pixel.y - center.y) / scale);
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_state.Node == null) return;

            _extent = _state.GetPlotExtent();

            var painter = ctx.painter2D;
            Rect rect = contentRect;
            Vector2 center = Center;
            float half = HalfSize;

            painter.lineWidth = 1f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.05f);
            for (int step = -GridDivisions; step <= GridDivisions; step++)
            {
                if (step == 0) continue;
                float offset = step / (float)GridDivisions * half;

                painter.BeginPath();
                painter.MoveTo(new Vector2(center.x + offset, 0f));
                painter.LineTo(new Vector2(center.x + offset, rect.height));
                painter.Stroke();

                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, center.y + offset));
                painter.LineTo(new Vector2(rect.width, center.y + offset));
                painter.Stroke();
            }

            painter.strokeColor = new Color(1f, 1f, 1f, 0.18f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x, 0f));
            painter.LineTo(new Vector2(center.x, rect.height));
            painter.Stroke();
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, center.y));
            painter.LineTo(new Vector2(rect.width, center.y));
            painter.Stroke();

            if (_state.Node.spaceType != HonamiBlendSpaceType.FreeformCartesian)
            {
                painter.strokeColor = new Color(1f, 1f, 1f, 0.10f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.Arc(center, half / _extent, 0f, 360f);
                painter.Stroke();
            }

            DrawMotions(painter);
            if (_state.AxisCount > 0) DrawSample(painter, center);
        }

        private void DrawMotions(Painter2D painter)
        {
            var motions = _state.Node.blendMotions;
            if (motions == null) return;

            int count = motions.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 pixel = ToPixel(motions[i]);
                float weight = i < _state.Weights.Length ? _state.Weights[i] : 0f;
                Color color = MotionColor(i, count);

                if (weight > 0.001f)
                {
                    painter.fillColor = new Color(color.r, color.g, color.b, 0.18f);
                    painter.BeginPath();
                    painter.Arc(pixel, PointRadius + weight * 30f, 0f, 360f);
                    painter.Fill();
                }

                bool unassigned = motions[i]?.clip == null;
                painter.fillColor = weight > 0.001f ? color : new Color(color.r, color.g, color.b, 0.4f);
                painter.BeginPath();
                painter.Arc(pixel, PointRadius, 0f, 360f);
                painter.Fill();

                if (unassigned)
                {
                    painter.strokeColor = new Color(1f, 1f, 1f, 0.7f);
                    painter.lineWidth = 1f;
                    painter.BeginPath();
                    painter.Arc(pixel, PointRadius + 2.5f, 0f, 360f);
                    painter.Stroke();
                }
            }
        }

        private void DrawSample(Painter2D painter, Vector2 center)
        {
            Vector2 pixel = ToPixel(AxisValue(_state.AxisX), AxisValue(_state.AxisY));

            painter.strokeColor = new Color(HonamiEditorTheme.Accent.r, HonamiEditorTheme.Accent.g, HonamiEditorTheme.Accent.b, 0.6f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(center);
            painter.LineTo(pixel);
            painter.Stroke();

            painter.fillColor = HonamiEditorTheme.Accent;
            painter.BeginPath();
            painter.Arc(pixel, SampleRadius, 0f, 360f);
            painter.Fill();

            painter.strokeColor = Color.white;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.Arc(pixel, SampleRadius, 0f, 360f);
            painter.Stroke();
        }

        private float AxisValue(int axis)
            => _state.Sample != null && axis >= 0 && axis < _state.Sample.Length ? _state.Sample[axis] : 0f;

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_state.Node == null || _state.AxisCount == 0) return;

            Vector2 local = evt.localPosition;
            _draggedMotion = PickMotion(local);
            _draggingSample = _draggedMotion < 0;

            if (_draggingSample) SetSample(local);

            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;

            Vector2 local = evt.localPosition;
            if (_draggingSample)
            {
                SetSample(local);
            }
            else if (_draggedMotion >= 0)
            {
                Vector2 world = ToWorld(local);
                if (evt.ctrlKey || evt.commandKey)
                {
                    world = new Vector2(Mathf.Round(world.x * 10f) * 0.1f, Mathf.Round(world.y * 10f) * 0.1f);
                }

                HonamiEditorController.SetBlendSpaceMotionAxis(_state.Node, _draggedMotion, _state.AxisX, world.x);
                HonamiEditorController.SetBlendSpaceMotionAxis(_state.Node, _draggedMotion, _state.AxisY, world.y);
                _onMotionsChanged?.Invoke();
            }

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;
            this.ReleasePointer(evt.pointerId);
            _draggedMotion = -1;
            _draggingSample = false;
            evt.StopPropagation();
        }

        private void SetSample(Vector2 local)
        {
            if (_state.Sample == null) return;

            Vector2 world = ToWorld(local);
            if (_state.AxisX < _state.Sample.Length) _state.Sample[_state.AxisX] = world.x;
            if (_state.AxisY < _state.Sample.Length) _state.Sample[_state.AxisY] = world.y;

            _onSampleChanged?.Invoke();
        }

        private int PickMotion(Vector2 local)
        {
            var motions = _state.Node?.blendMotions;
            if (motions == null) return -1;

            int best = -1;
            float bestDistance = PickRadius;

            for (int i = 0; i < motions.Count; i++)
            {
                float distance = Vector2.Distance(local, ToPixel(motions[i]));
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (_state.Node == null || _state.AxisCount == 0 || !DraggingClips()) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            SetDropHighlight(true);
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (_state.Node == null || _state.AxisCount == 0 || !DraggingClips()) return;

            DragAndDrop.AcceptDrag();
            SetDropHighlight(false);

            Vector2 world = ToWorld(this.WorldToLocal(evt.mousePosition));
            foreach (var dragged in DragAndDrop.objectReferences)
            {
                if (dragged is not AnimationClip clip) continue;

                HonamiEditorController.AddBlendSpaceMotion(_state.Node, clip);
                int index = _state.Node.blendMotions.Count - 1;
                HonamiEditorController.SetBlendSpaceMotionAxis(_state.Node, index, _state.AxisX, world.x);
                HonamiEditorController.SetBlendSpaceMotionAxis(_state.Node, index, _state.AxisY, world.y);
            }

            _onMotionsChanged?.Invoke();
            evt.StopPropagation();
        }

        private static bool DraggingClips()
        {
            foreach (var dragged in DragAndDrop.objectReferences)
            {
                if (dragged is AnimationClip) return true;
            }
            return false;
        }

        private void SetDropHighlight(bool active)
            => _dropOverlay.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

        public static Color MotionColor(int index, int count)
        {
            float hue = count <= 1 ? 0.55f : index / (float)count;
            return Color.HSVToRGB(hue, 0.70f, 0.95f);
        }
    }
}
