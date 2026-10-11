#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Core;
using HonamiAnimationSystem.Runtime.Core;

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
            _valueFieldX = null;
            _valueFieldY = null;
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
                _rebuild?.Invoke();
                return;
            }
            RefreshCheap();
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

        public void SetPreviewValueX(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            _state.PreviewValueX = value;
            UpdateNodeWeights();
        }

        public void SetPreviewValueY(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            _state.PreviewValueY = value;
            UpdateNodeWeights();
        }

        public void AddMotion(AnimationClip clip)
        {
            if (_state.Node == null) return;
            if (_state.IsDirectional2D)
            {
                var node = _state.Node;
                node.blendMotions ??= new System.Collections.Generic.List<HonamiBlendTreeMotion>();
                if (node.blendMotions.Count >= HonamiDirectionalBlendSolver.MaximumMotionCount) return;
                Undo.RecordObject(node, "Add Directional Motion");
                node.blendMotions.Add(new HonamiBlendTreeMotion
                {
                    clip = clip,
                    position = FindFreeDirectionalPosition(node, true),
                    speed = 1f
                });
                EditorUtility.SetDirty(node);
                return;
            }
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
            if (_state.IsDirectional2D)
            {
                var node = _state.Node;
                if (node.blendMotions == null || index < 0 || index >= node.blendMotions.Count ||
                    node.blendMotions.Count >= HonamiDirectionalBlendSolver.MaximumMotionCount)
                    return;
                Undo.RecordObject(node, "Duplicate Directional Motion");
                HonamiBlendTreeMotion source = node.blendMotions[index];
                node.blendMotions.Insert(index + 1, new HonamiBlendTreeMotion
                {
                    clip = source.clip,
                    position = FindFreeDirectionalPosition(node, false),
                    speed = source.speed,
                    threshold = source.threshold,
                    cycleOffset = source.cycleOffset
                });
                EditorUtility.SetDirty(node);
                return;
            }
            HonamiEditorController.DuplicateBlendMotion(_state.Node, index);
        }

        public void DistributeThresholds()
        {
            if (_state.Node == null || _state.IsDirectional2D) return;
            HonamiEditorController.DistributeBlendThresholds(_state.Node);
        }

        private static Vector2 FindFreeDirectionalPosition(HonamiBlendTreeNode node, bool allowOrigin)
        {
            if (allowOrigin && !HasOrigin(node))
                return Vector2.zero;

            const float goldenAngle = 2.39996323f;
            float[] radii = { 1f, 0.5f, 0.75f, 1.25f };
            for (int i = 0; i < 64; i++)
            {
                float angle = i * goldenAngle;
                float radius = radii[i % radii.Length];
                var candidate = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                bool used = false;
                for (int motionIndex = 0; motionIndex < node.blendMotions.Count; motionIndex++)
                {
                    HonamiBlendTreeMotion existingMotion = node.blendMotions[motionIndex];
                    if (existingMotion == null) continue;
                    Vector2 existing = existingMotion.position;
                    if ((candidate - existing).sqrMagnitude <= 1e-12f)
                    {
                        used = true;
                        break;
                    }
                }
                if (!used) return candidate;
            }

            // With the 17-motion limit this fallback is practically unreachable, but remains
            // deterministic and distinct if the authored table already uses the spiral candidates.
            return new Vector2(1.37f + node.blendMotions.Count * 0.01f, 0.013f);
        }

        private static bool HasOrigin(HonamiBlendTreeNode node)
        {
            if (node.blendMotions == null) return false;
            for (int i = 0; i < node.blendMotions.Count; i++)
            {
                HonamiBlendTreeMotion motion = node.blendMotions[i];
                if (motion == null) continue;
                Vector2 position = motion.position;
                if (position.x == 0f && position.y == 0f) return true;
            }
            return false;
        }
    }
}
#endif
