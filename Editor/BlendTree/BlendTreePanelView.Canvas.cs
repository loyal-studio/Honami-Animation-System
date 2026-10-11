#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    internal sealed partial class BlendTreePanelView
    {
        private BlendTreeGraphCanvas _canvas;
        private readonly List<BlendTreeMotionCard> _motionNodes = new();
        private BlendTreeOutputCard _outputNode;
        private BlendTreeGraphEdges _edges;
        private Vector2 _lastOutputSize;

        private void BuildCanvasArea()
        {
            _canvas = new BlendTreeGraphCanvas(_state, this);
            _mainArea.Add(_canvas);

            RegenerateGraph();

            if (_state.HasValidViewTransform)
            {
                _canvas.UpdateTransform(new Vector3(_state.ViewPosition.x, _state.ViewPosition.y, 0f), _state.ViewScale);
            }
            else
            {
                _canvas.FrameAllWhenReady();
            }
        }

        private void RegenerateGraph()
        {
            if (_canvas == null) return;

            _canvas.ContentContainer.Clear();
            _motionNodes.Clear();
            _outputNode = null;
            _edges = null;
            _lastOutputSize = _state.OutputNodeMeasuredSize;

            if (_state.Node == null) return;

            _edges = new BlendTreeGraphEdges(_state);
            _canvas.ContentContainer.Add(_edges);

            _outputNode = new BlendTreeOutputCard(_state, OnNodePositionChanged, SetPreviewInputFromMap);
            _canvas.ContentContainer.Add(_outputNode);

            var motions = _state.Node.blendMotions;
            if (motions == null) return;
            var motionsProp = _state.NodeSO.FindProperty("blendMotions");

            for (int i = 0; i < motions.Count; i++)
            {
                var motionProp = motionsProp.GetArrayElementAtIndex(i);
                var nodeView = new BlendTreeMotionCard(motionProp, motions[i], i, motions.Count, _state, OnNodePositionChanged, RemoveMotion);

                _canvas.ContentContainer.Add(nodeView);
                _motionNodes.Add(nodeView);
            }

            AutoLayout();
        }

        private static Vector2 ComputeTreePosition(int index, int count, float motionY)
        {
            float horizontalSpacing = 60f;
            float totalWidth = (count * BlendTreeTheme.NodeWidth) + ((count - 1) * horizontalSpacing);
            float startX = -(totalWidth * 0.5f);
            float x = startX + index * (BlendTreeTheme.NodeWidth + horizontalSpacing);
            return new Vector2(x, motionY);
        }

        private float GetMotionRowY(float outputY)
        {
            const float gap = 40f;
            float outH = _state.OutputNodeMeasuredSize.y > 0f
                ? _state.OutputNodeMeasuredSize.y
                : (_state.ShowPreview ? (_state.IsDirectional2D ? 500f : 250f) : 110f);
            return outputY + outH + gap;
        }

        private void OnNodePositionChanged()
        {
            AutoLayout();
            _edges?.MarkDirtyRepaint();
        }

        public void AutoLayout()
        {
            if (_state.Node == null) return;

            float outputWidth = _state.ShowPreview ? BlendTreeTheme.OutputNodePreviewWidth : BlendTreeTheme.OutputNodeWidth;
            var outPos = new Vector2(-outputWidth * 0.5f, 40f);
            if (_outputNode != null)
            {
                _outputNode.style.left = outPos.x;
                _outputNode.style.top = outPos.y;
            }
            _state.OutputNodePosition = outPos;

            int count = _motionNodes.Count;
            float motionY = GetMotionRowY(40f);
            for (int i = 0; i < count; i++)
            {
                var pos = ComputeTreePosition(i, count, motionY);
                _motionNodes[i].style.left = pos.x;
                _motionNodes[i].style.top = pos.y;
                _state.NodePositions[i] = pos;
            }

            _state.HasNodePositions = true;

            _canvas.schedule.Execute(FrameAll).ExecuteLater(50);
        }

        public void FrameAll()
        {
            _canvas?.FrameAll();
        }

        public void OnCanvasZoomChanged(float scale)
        {
            _zoomSlider?.SetValueWithoutNotify(scale);
        }

        private void UpdateNodeWeights()
        {
            if (_state.Node == null) return;
            _state.CalculateWeights(_state.PreviewValue);

            for (int i = 0; i < _motionNodes.Count && i < _state.Weights.Length; i++)
            {
                _motionNodes[i].UpdateWeight(_state.Weights[i]);
            }

            if (_state.IsDirectional2D)
                _outputNode?.UpdatePreviewInput(_state.PreviewInput);
            else
            {
                _state.GetSliderRange(out float min, out float max);
                _outputNode?.UpdatePreviewValue(_state.PreviewValue, min, max);
            }

            _edges?.MarkDirtyRepaint();
        }

        private void SetPreviewInputFromMap(Vector2 input)
        {
            if (!_state.IsDirectional2D || float.IsNaN(input.x) || float.IsInfinity(input.x) ||
                float.IsNaN(input.y) || float.IsInfinity(input.y))
                return;

            SetPreviewValueX(input.x);
            SetPreviewValueY(input.y);
            _valueFieldX?.SetValueWithoutNotify(_state.PreviewValueX);
            _valueFieldY?.SetValueWithoutNotify(_state.PreviewValueY);
        }
    }
}
#endif
