using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>
    /// Background layer that draws the weighted bezier connections from each motion
    /// card to the output card; stroke width and opacity track the live blend weights.
    /// </summary>
    internal sealed class BlendTreeGraphEdges : VisualElement
    {
        private readonly BlendTreeState _state;

        public BlendTreeGraphEdges(BlendTreeState state)
        {
            _state = state;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            
            generateVisualContent += OnDrawEdges;
        }

        private void OnDrawEdges(MeshGenerationContext ctx)
        {
            if (_state == null || _state.Node == null || _state.Node.blendMotions == null) return;
            if (!_state.HasNodePositions) return;

            var painter = ctx.painter2D;
            int count = _state.MotionCount;
            
            Vector2 outPos = _state.OutputNodePosition;
            float outW = _state.OutputNodeMeasuredSize.x > 0f ? _state.OutputNodeMeasuredSize.x : BlendTreeTheme.OutputNodeWidth;
            float outH = _state.OutputNodeMeasuredSize.y > 0f ? _state.OutputNodeMeasuredSize.y : 110f;
            Vector2 endPt = new Vector2(outPos.x + outW * 0.5f, outPos.y + outH);

            for (int i = 0; i < count; i++)
            {
                if (!_state.NodePositions.TryGetValue(i, out Vector2 nodePos))
                    continue;

                Vector2 startPt = new Vector2(nodePos.x + BlendTreeTheme.NodeWidth * 0.5f, nodePos.y);

                float weight = 0f;
                if (_state.Weights != null && i < _state.Weights.Length)
                {
                    weight = _state.Weights[i];
                }

                Color edgeColor = BlendTreeTheme.MotionColor(i, count);

                if (weight < 0.001f)
                {
                    edgeColor.a = 0.3f;
                    painter.lineWidth = 1.5f;
                }
                else
                {
                    edgeColor.a = 0.6f + (weight * 0.4f);
                    painter.lineWidth = 1.5f + (weight * 2.5f);
                }

                painter.strokeColor = edgeColor;
                painter.BeginPath();
                painter.MoveTo(startPt);

                float distY = Mathf.Abs(endPt.y - startPt.y);
                float controlDist = Mathf.Max(distY * 0.5f, 40f);

                Vector2 cp1 = startPt - new Vector2(0, controlDist);
                Vector2 cp2 = endPt + new Vector2(0, controlDist);

                painter.BezierCurveTo(cp1, cp2, endPt);
                painter.Stroke();
            }
        }
    }
}
