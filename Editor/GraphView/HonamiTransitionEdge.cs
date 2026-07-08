using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiTransitionEdge : Edge
    {
        private readonly EdgeRenderer _edgeRenderer;
        private readonly VisualElement _badge;
        private readonly BadgeIconElement _badgeIcon;
        private Vector2 _start;
        private Vector2 _end;

        public new string tooltip
        {
            get => base.tooltip;
            set
            {
                base.tooltip = value;
                if (_badge != null) _badge.tooltip = value;
            }
        }

        public HonamiTransitionEdge()
        {
            _edgeRenderer = new EdgeRenderer();
            Add(_edgeRenderer);

            _badge = new VisualElement();
            _badge.AddToClassList("honami-transition-badge");

            _badgeIcon = new BadgeIconElement();
            _badge.Add(_badgeIcon);
            Add(_badge);

            if (edgeControl != null)
            {
                edgeControl.interceptWidth = 25f;
                edgeControl.style.opacity = 0f;
            }

            RegisterCallback<MouseEnterEvent>(e => _badge.AddToClassList("honami-transition-badge-hover"));
            RegisterCallback<MouseLeaveEvent>(e => _badge.RemoveFromClassList("honami-transition-badge-hover"));
        }

        public override void OnSelected()
        {
            base.OnSelected();
            SetBadgeSelected(true);
            _edgeRenderer.UpdateSelection(true);
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            SetBadgeSelected(false);
            _edgeRenderer.UpdateSelection(false);
        }

        private void SetBadgeSelected(bool isSelected)
        {
            if (isSelected) _badge.AddToClassList("honami-transition-badge-selected");
            else _badge.RemoveFromClassList("honami-transition-badge-selected");
            HonamiGraphAccent.SetSelectionBorder(_badge, isSelected);
        }

        public void RefreshAccent()
        {
            if (_badge.ClassListContains("honami-transition-badge-preview"))
            {
                HonamiGraphAccent.SetBorderColor(_badge, HonamiGraphStyles.Accent);
                return;
            }

            HonamiGraphAccent.SetSelectionBorder(_badge, this.selected);
            _badge.MarkDirtyRepaint();
        }

        private bool _lastActive = false;
        private IVisualElementScheduledItem _repaintSchedule;

        public void UpdateRuntimeState(bool isActive)
        {
            if (isActive == _lastActive) return;
            _lastActive = isActive;

            if (isActive) AddToClassList("honami-transition-edge-active");
            else RemoveFromClassList("honami-transition-edge-active");

            _edgeRenderer.UpdateRuntimeState(isActive);

            if (isActive)
            {
                if (_repaintSchedule == null)
                    _repaintSchedule = schedule.Execute(() => MarkDirtyRepaint()).Every(32);
                else
                    _repaintSchedule.Resume();
            }
            else
            {
                _repaintSchedule?.Pause();
            }
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            if (_badge.layout.Contains(localPoint)) return true;

            float l2 = (_end - _start).sqrMagnitude;
            if (l2 <= 0.0001f) return Vector2.Distance(localPoint, _start) < 20f;

            float t = Mathf.Max(0, Mathf.Min(1, Vector2.Dot(localPoint - _start, _end - _start) / l2));
            Vector2 proj = _start + t * (_end - _start);

            return Vector2.Distance(localPoint, proj) < 20f;
        }

        public override bool Overlaps(Rect rectangle)
        {
            if (rectangle.Overlaps(_badge.layout)) return true;
            if (rectangle.Contains(_start) || rectangle.Contains(_end)) return true;

            Rect bounds = new Rect(Mathf.Min(_start.x, _end.x), Mathf.Min(_start.y, _end.y), Mathf.Abs(_start.x - _end.x), Mathf.Abs(_start.y - _end.y));
            if (rectangle.Overlaps(bounds)) return true;

            return base.Overlaps(rectangle);
        }

        public override bool UpdateEdgeControl()
        {
            var trans = userData as HonamiTransition;
            if (trans != null && _badgeIcon != null)
            {
                var targetNode = input?.node as HonamiNode;
                bool isToExit = targetNode != null && targetNode.State != null && targetNode.State.node is HonamiExitNode;
                bool effectiveSelf = trans.canTransitionToSelf && !isToExit;

                if (_badgeIcon.IsSelfTransition != effectiveSelf)
                {
                    _badgeIcon.IsSelfTransition = effectiveSelf;
                    _badgeIcon.MarkDirtyRepaint();
                }

                if (effectiveSelf)
                {
                    _badge.AddToClassList("honami-transition-badge-self");
                }
                else
                {
                    _badge.RemoveFromClassList("honami-transition-badge-self");
                }
            }

            bool updated = base.UpdateEdgeControl();

            if (edgeControl != null && edgeControl.style.opacity != 0f)
            {
                edgeControl.style.opacity = 0f;
            }

            if (edgeControl == null || edgeControl.controlPoints == null || edgeControl.controlPoints.Length < 4)
                return updated;

            if (output != null && input != null && output.node != null && input.node != null)
            {
                var sourceNode = output.node as HonamiNode;
                var targetNode = input.node as HonamiNode;

                if (sourceNode != null && targetNode != null)
                {
                    Rect sourceRect = HonamiGraphView.GetNodeWorldRect(sourceNode);
                    Rect targetRect = HonamiGraphView.GetNodeWorldRect(targetNode);

                    if (sourceNode == targetNode)
                    {
                        _start = sourceRect.center + new Vector2(-10, -sourceRect.height / 2);
                        _end = sourceRect.center + new Vector2(10, -sourceRect.height / 2);

                        _edgeRenderer.UpdateLoopEdge(sourceRect, this.selected);

                        Vector2 capPos = sourceRect.center + new Vector2(0, -sourceRect.height / 2 - 40);
                        UpdateBadgePositionAndRotation(capPos, Vector2.right);

                        return true;
                    }

                    Vector2 startCenter = sourceRect.center;
                    Vector2 endCenter = targetRect.center;

                    Vector2 rawDir = endCenter - startCenter;
                    float rawDist = rawDir.magnitude;
                    Vector2 dir = rawDist > 0.001f ? rawDir / rawDist : Vector2.right;
                    Vector2 perp = new Vector2(-dir.y, dir.x);

                    float offsetStr = 0f;
                    int connCount = 0;
                    int myIdx = -1;

                    if (output.connections != null)
                    {
                        foreach (Edge c in output.connections)
                        {
                            if (c.input != null && c.input.node == targetNode)
                            {
                                if (c == this) myIdx = connCount;
                                connCount++;
                            }
                        }
                    }

                    bool hasReverse = false;
                    if (targetNode.OutputPort?.connections != null)
                    {
                        foreach (Edge c in targetNode.OutputPort.connections)
                        {
                            if (c.input != null && c.input.node == sourceNode)
                            {
                                hasReverse = true;
                                break;
                            }
                        }
                    }

                    if (hasReverse) offsetStr += 15f;
                    if (myIdx >= 0 && connCount > 1) offsetStr += (myIdx - (connCount - 1) / 2f) * 20f;

                    Vector2 startBorder = HonamiGraphView.RaycastRect(sourceRect, startCenter, dir);
                    Vector2 endBorder = HonamiGraphView.RaycastRect(targetRect, endCenter, -dir);

                    startBorder += perp * offsetStr;
                    endBorder += perp * offsetStr;

                    _start = HonamiGraphView.FixNaN(startBorder);
                    _end = HonamiGraphView.FixNaN(endBorder);

                    float dist = Vector2.Distance(_start, _end);
                    Vector2 lineDir = dist > 0.001f ? (_end - _start) / dist : Vector2.right;

                    edgeControl.controlPoints[0] = _start;
                    edgeControl.controlPoints[1] = _start + lineDir * (dist * 0.33f);
                    edgeControl.controlPoints[2] = _start + lineDir * (dist * 0.66f);
                    edgeControl.controlPoints[3] = _end;

                    for (int i = 4; i < edgeControl.controlPoints.Length; i++)
                        edgeControl.controlPoints[i] = _end;

                    edgeControl.UpdateLayout();

                    _edgeRenderer.UpdateEdge(_start, _end, lineDir, this.selected);

                    Vector2 mid = (_start + _end) * 0.5f;
                    UpdateBadgePositionAndRotation(mid, lineDir);

                    return true;
                }
            }
            else if (output != null && input == null)
            {
                var sourceNode = output.node as HonamiNode;
                if (sourceNode != null)
                {
                    Rect sourceRect = HonamiGraphView.GetNodeWorldRect(sourceNode);
                    Vector2 startCenter = sourceRect.center;
                    Vector2 endPos = candidatePosition;

                    if (sourceRect.Contains(endPos)) return updated;

                    Vector2 rawDir = endPos - startCenter;
                    float rawDist = rawDir.magnitude;
                    Vector2 dir = rawDist > 0.001f ? rawDir / rawDist : Vector2.right;

                    Vector2 startBorder = HonamiGraphView.RaycastRect(sourceRect, startCenter, dir);
                    float dist = Vector2.Distance(startBorder, endPos);

                    if (dist < 1f) return updated;
                    Vector2 lineDir = (endPos - startBorder) / dist;

                    _start = HonamiGraphView.FixNaN(startBorder);
                    _end = HonamiGraphView.FixNaN(endPos);

                    edgeControl.controlPoints[0] = _start;
                    edgeControl.controlPoints[1] = _start + lineDir * (dist * 0.33f);
                    edgeControl.controlPoints[2] = _start + lineDir * (dist * 0.66f);
                    edgeControl.controlPoints[3] = _end;
                    for (int i = 4; i < edgeControl.controlPoints.Length; i++)
                        edgeControl.controlPoints[i] = _end;

                    edgeControl.UpdateLayout();

                    _edgeRenderer.UpdateEdge(_start, _end, dir, false);

                    Vector2 mid = (_start + _end) * 0.5f;
                    UpdateBadgePositionAndRotation(mid, lineDir);
                }
            }

            return updated;
        }

        private void UpdateBadgePositionAndRotation(Vector2 position, Vector2 direction)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y))
                position = Vector2.zero;

            if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsInfinity(direction.x) || float.IsInfinity(direction.y) || direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;

            _badge.style.left = position.x - 12f;
            _badge.style.top = position.y - 12f;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (float.IsNaN(angle) || float.IsInfinity(angle)) angle = 0f;

            _badge.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));

            SetBadgeSelected(this.selected);
        }

        private class BadgeIconElement : VisualElement
        {
            public bool IsSelfTransition;

            public BadgeIconElement()
            {
                style.width = 16;
                style.height = 16;
                style.alignSelf = Align.Center;
                AddToClassList("honami-transition-icon");
                generateVisualContent += OnGenerateContent;
            }

            private void OnGenerateContent(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                Color color = resolvedStyle.color;
                if (color.a == 0) color = IsSelfTransition ? new Color(0.17f, 0.77f, 0.71f, 1f) : new Color(0.91f, 0.91f, 0.91f, 1f);

                float cx = 8f;
                float cy = 8f;

                if (IsSelfTransition)
                {
                    p.strokeColor = color;
                    p.lineWidth = 1.8f;
                    p.BeginPath();
                    p.Arc(new Vector2(cx, cy), 4.5f, 0f, 270f);
                    p.Stroke();

                    Vector2 tip = new Vector2(cx, cy - 4.5f);
                    p.fillColor = color;
                    p.BeginPath();
                    p.MoveTo(tip + new Vector2(3.5f, 0f));
                    p.LineTo(tip + new Vector2(-3.5f, -3.5f));
                    p.LineTo(tip + new Vector2(-3.5f, 3.5f));
                    p.ClosePath();
                    p.Fill();
                }
                else
                {
                    cx += 1f;
                    p.fillColor = color;
                    p.BeginPath();
                    p.MoveTo(new Vector2(cx - 3.5f, cy - 4.5f));
                    p.LineTo(new Vector2(cx + 4.5f, cy));
                    p.LineTo(new Vector2(cx - 3.5f, cy + 4.5f));
                    p.ClosePath();
                    p.Fill();
                }
            }
        }

        private class EdgeRenderer : VisualElement
        {
            private Vector2 _localStart;
            private Vector2 _localEnd;
            private Vector2 _dir;
            private bool _isSelected;
            private bool _isLoop;
            private Rect _loopRect;
            private bool _valid;
            private Vector2 _boundsOffset;

            public EdgeRenderer()
            {
                style.position = Position.Absolute;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += OnGenerateContent;
            }

            public void UpdateSelection(bool isSelected)
            {
                if (_isSelected != isSelected)
                {
                    _isSelected = isSelected;
                    MarkDirtyRepaint();
                }
            }

            private bool _isRuntimeActive;
            public void UpdateRuntimeState(bool isActive)
            {
                if (_isRuntimeActive != isActive)
                {
                    _isRuntimeActive = isActive;
                    MarkDirtyRepaint();
                }
            }

            public void UpdateEdge(Vector2 start, Vector2 end, Vector2 dir, bool isSelected)
            {
                _isLoop = false;
                Vector2 desiredLocalStart = start - _boundsOffset;
                Vector2 desiredLocalEnd = end - _boundsOffset;
                if (!_valid || _localStart != desiredLocalStart || _localEnd != desiredLocalEnd || _dir != dir || _isSelected != isSelected)
                {
                    Rect bounds = new Rect(
                        Mathf.Min(start.x, end.x) - 40f,
                        Mathf.Min(start.y, end.y) - 40f,
                        Mathf.Abs(start.x - end.x) + 80f,
                        Mathf.Abs(start.y - end.y) + 80f
                    );

                    bounds = HonamiGraphView.FixRectNaN(bounds);

                    style.left = bounds.x;
                    style.top = bounds.y;
                    style.width = bounds.width;
                    style.height = bounds.height;

                    _boundsOffset = bounds.position;
                    _localStart = start - _boundsOffset;
                    _localEnd = end - _boundsOffset;
                    _dir = dir;
                    _isSelected = isSelected;
                    _valid = true;

                    MarkDirtyRepaint();
                }
                else if (_isSelected != isSelected)
                {
                    _isSelected = isSelected;
                    MarkDirtyRepaint();
                }
            }

            public void UpdateLoopEdge(Rect sourceNodeRect, bool isSelected)
            {
                _isLoop = true;
                _loopRect = sourceNodeRect;
                if (!_valid || _isSelected != isSelected)
                {
                    _isSelected = isSelected;
                    _valid = true;

                    Rect bounds = sourceNodeRect;
                    bounds.yMin -= 100f;
                    bounds.xMin -= 80f;
                    bounds.xMax += 80f;

                    bounds = HonamiGraphView.FixRectNaN(bounds);

                    style.left = bounds.x;
                    style.top = bounds.y;
                    style.width = bounds.width;
                    style.height = bounds.height;

                    MarkDirtyRepaint();
                }
            }

            private void OnGenerateContent(MeshGenerationContext ctx)
            {
                if (!_valid) return;

                var p = ctx.painter2D;

                Color edgeColor = _isSelected
                    ? new Color(0.29f, 0.83f, 1.00f, 1f)
                    : (_isRuntimeActive ? new Color(0.55f, 0.73f, 0.36f, 1f) : new Color(0.40f, 0.42f, 0.45f, 1f));

                p.strokeColor = edgeColor;
                p.lineWidth = (_isSelected || _isRuntimeActive) ? 3.5f : 2.5f;

                if (_isRuntimeActive)
                {
                    float time = (float)EditorApplication.timeSinceStartup;
                    float dashOffset = (time * 80.0f) % 40.0f;
                }

                if (_isLoop)
                {
                    Vector2 nodeCenter = new Vector2(style.width.value.value / 2, style.height.value.value - _loopRect.height / 2);
                    Vector2 startPoint = nodeCenter + new Vector2(-20, -_loopRect.height / 2);
                    Vector2 endPoint = nodeCenter + new Vector2(20, -_loopRect.height / 2);

                    Vector2 cp1 = startPoint + new Vector2(-60, -80);
                    Vector2 cp2 = endPoint + new Vector2(60, -80);

                    p.BeginPath();
                    p.MoveTo(startPoint);
                    p.BezierCurveTo(cp1, cp2, endPoint);
                    p.Stroke();

                    if (_isRuntimeActive)
                    {
                        float time = (float)EditorApplication.timeSinceStartup;
                        float t_offset = (time * 1.5f) % 0.3f;
                        p.strokeColor = new Color(1, 1, 1, 0.6f);
                        p.lineWidth = 5.0f;

                        for (float t = t_offset; t < 1.0f; t += 0.3f)
                        {
                            Vector2 p1 = GetBezierPoint(startPoint, cp1, cp2, endPoint, t);
                            Vector2 p2 = GetBezierPoint(startPoint, cp1, cp2, endPoint, Mathf.Min(t + 0.05f, 1.0f));
                            p.BeginPath();
                            p.MoveTo(p1);
                            p.LineTo(p2);
                            p.Stroke();
                        }
                    }

                    Vector2 endDir = (endPoint - cp2).normalized;
                    DrawArrow(p, endPoint + endDir * 2f, endDir, edgeColor);
                }
                else
                {
                    p.BeginPath();
                    p.MoveTo(_localStart);
                    p.LineTo(_localEnd);
                    p.Stroke();

                    if (_isRuntimeActive)
                    {
                        float time = (float)EditorApplication.timeSinceStartup;
                        float t_offset = (time * 2.0f) % 0.4f;
                        p.strokeColor = new Color(1, 1, 1, 0.7f);
                        p.lineWidth = 5.0f;

                        for (float t = t_offset; t < 1.0f; t += 0.4f)
                        {
                            Vector2 p1 = Vector2.Lerp(_localStart, _localEnd, t);
                            Vector2 p2 = Vector2.Lerp(_localStart, _localEnd, Mathf.Min(t + 0.1f, 1.0f));
                            p.BeginPath();
                            p.MoveTo(p1);
                            p.LineTo(p2);
                            p.Stroke();
                        }
                    }

                    DrawArrow(p, _localEnd, _dir, edgeColor);
                }
            }

            private void DrawArrow(Painter2D p, Vector2 tip, Vector2 dir, Color color)
            {
                Vector2 forward = dir.normalized;
                Vector2 sideways = new Vector2(-forward.y, forward.x);
                float arrowLen = 14f;
                float arrowWidth = 9f;

                Vector2 center = tip - forward * 2f;
                Vector2 left = center - forward * arrowLen + sideways * arrowWidth;
                Vector2 right = center - forward * arrowLen - sideways * arrowWidth;

                p.fillColor = color;
                p.BeginPath();
                p.MoveTo(tip);
                p.LineTo(left);
                p.LineTo(right);
                p.ClosePath();
                p.Fill();
            }

            private Vector2 GetBezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
            {
                float u = 1 - t;
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;

                Vector2 p = uuu * p0;
                p += 3 * uu * t * p1;
                p += 3 * u * tt * p2;
                p += ttt * p3;

                return p;
            }
        }
    }
}
