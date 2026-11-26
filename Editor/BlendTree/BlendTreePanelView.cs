using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Core;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>
    /// Main content view for the blend tree editor: hosts the node canvas, the
    /// properties side panel, and the preview transport bar for a single state.
    /// </summary>
    internal sealed partial class BlendTreePanelView : VisualElement
    {
        private readonly BlendTreeState _state;
        private readonly Action _rebuild;

        private VisualElement _mainArea;
        private int _topologySignature;
        private bool _builtWithPreview;
        private bool _rebuildQueued;

        public BlendTreePanelView(BlendTreeState state, Action rebuild)
        {
            _state = state;
            _rebuild = rebuild;

            name = "honami-blendtree-panel";
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            style.minHeight = 0;
            style.backgroundColor = BlendTreeTheme.PanelBg;

            Build();
        }

        private void Build()
        {
            Clear();
            _mainArea = null;
            _canvas = null;
            _motionNodes.Clear();
            _outputNode = null;
            _edges = null;
            _slider = null;
            _valueField = null;
            _paramLabel = null;
            _minLabel = null;
            _maxLabel = null;
            _zoomSlider = null;
            _propsPanel = null;

            _state.Resolve();

            if (_state.Node == null)
            {
                BuildEmptyState();
                return;
            }

            _topologySignature = _state.TopologySignature();
            _builtWithPreview = _state.ShowPreview;

            _mainArea = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row, minHeight = 0, overflow = Overflow.Hidden } };
            Add(_mainArea);

            BuildCanvasArea();
            BuildPropertiesPanel();
            Add(BuildTransportBar());

            var watcher = new VisualElement { style = { display = DisplayStyle.None } };
            watcher.TrackSerializedObjectValue(_state.NodeSO, OnNodeDataChanged);
            Add(watcher);

            UpdateSliderRange();
            UpdateNodeWeights();
        }

        public void Refresh()
        {
            _state.Resolve();

            bool hasContent = _mainArea != null && _canvas != null;
            bool wantsContent = _state.Node != null;

            if (hasContent != wantsContent
                || (wantsContent && _topologySignature != _state.TopologySignature())
                || (wantsContent && _builtWithPreview != _state.ShowPreview))
            {
                Build();
                return;
            }

            if (!wantsContent)
            {
                BuildEmptyState();
                return;
            }

            RefreshCheap();
        }

        private void OnNodeDataChanged(UnityEditor.SerializedObject _)
        {
            _state.Resolve();
            if (_state.Node == null || _topologySignature != _state.TopologySignature())
            {
                QueueRebuild();
                return;
            }
            RefreshCheap();
        }

        private void QueueRebuild()
        {
            if (_rebuildQueued) return;
            _rebuildQueued = true;
            schedule.Execute(() =>
            {
                _rebuildQueued = false;
                Build();
            }).ExecuteLater(1);
        }

        private void RefreshCheap()
        {
            UpdateSliderRange();
            UpdateNodeWeights();
            UpdateParamLabel();
            _outputNode?.RefreshInfo(_state);
            ApplyPropertiesVisibility();
        }

        public void SetPreviewValue(float value)
        {
            if (_slider != null)
                value = Mathf.Clamp(value, _slider.lowValue, _slider.highValue);

            _state.PreviewValue = value;
            _slider?.SetValueWithoutNotify(value);
            _valueField?.SetValueWithoutNotify(value);
            UpdateNodeWeights();
        }

        public void AddMotion(AnimationClip clip)
        {
            if (_state.Node == null) return;
            HonamiEditorController.AddBlendMotion(_state.Node, clip);
        }

        public void RemoveMotion(int index)
        {
            if (_state.Node == null) return;
            HonamiEditorController.RemoveBlendMotion(_state.Node, index);
        }

        public void DuplicateMotion(int index)
        {
            if (_state.Node == null) return;
            HonamiEditorController.DuplicateBlendMotion(_state.Node, index);
        }

        public void DistributeThresholds()
        {
            if (_state.Node == null) return;
            HonamiEditorController.DistributeBlendThresholds(_state.Node);
        }
    }
}
