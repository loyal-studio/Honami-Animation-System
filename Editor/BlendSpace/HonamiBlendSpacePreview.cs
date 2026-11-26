using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Preview;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendSpace
{
    /// <summary>
    /// Live skeleton preview for a blend space: samples the node at the window's current axis values
    /// so dragging a motion or the sample handle updates the pose immediately.
    /// </summary>
    internal sealed class HonamiBlendSpacePreview : VisualElement, IDisposable
    {
        private const float ViewportHeight = 190f;

        private readonly Func<HonamiBlendSpaceNode> _nodeGetter;
        private readonly Func<float[]> _sampleGetter;
        private readonly Func<bool> _reversedGetter;
        private readonly HonamiPreviewStage _stage = new();
        private readonly HonamiPreviewSkeleton _skeleton = new();
        private readonly List<AnimationClip> _clipBuffer = new();

        private IMGUIContainer _viewport;
        private Slider _timeSlider;
        private Button _playButton;
        private Label _emptyHint;

        private bool _playing;
        private float _time;
        private bool _loop = true;
        private bool _needsReframe = true;
        private bool _dragging;
        private int _dragButton;
        private double _lastUpdate;

        public HonamiBlendSpacePreview(Func<HonamiBlendSpaceNode> nodeGetter, Func<float[]> sampleGetter, Func<bool> reversedGetter)
        {
            _nodeGetter = nodeGetter;
            _sampleGetter = sampleGetter;
            _reversedGetter = reversedGetter;

            name = "honami-blendspace-preview";
            style.flexDirection = FlexDirection.Column;
            style.flexShrink = 0;

            Build();

            RegisterCallback<AttachToPanelEvent>(_ => OnAttach());
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
        }

        private void Build()
        {
            _viewport = new IMGUIContainer(OnViewportGUI)
            {
                style =
                {
                    height = ViewportHeight,
                    flexShrink = 0,
                    marginBottom = 4,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    overflow = Overflow.Hidden
                }
            };
            Add(_viewport);

            _emptyHint = new Label("No clips with transform curves.")
            {
                style =
                {
                    display = DisplayStyle.None,
                    color = HonamiEditorTheme.MutedText,
                    fontSize = 9,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 4
                }
            };
            Add(_emptyHint);

            var bar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            _playButton = new Button(TogglePlay) { text = "Play", style = { width = 46, marginRight = 4, fontSize = 10 } };
            bar.Add(_playButton);

            _timeSlider = new Slider(0f, 1f) { value = _time, style = { flexGrow = 1, marginRight = 4 } };
            _timeSlider.RegisterValueChangedCallback(evt => { _time = evt.newValue; MarkDirty(); });
            bar.Add(_timeSlider);

            var loopToggle = new Toggle { value = _loop, tooltip = "Loop", style = { marginRight = 2 } };
            loopToggle.RegisterValueChangedCallback(evt => _loop = evt.newValue);
            bar.Add(loopToggle);

            bar.Add(new Button(() => { _needsReframe = true; MarkDirty(); }) { text = "Fit", style = { width = 32, fontSize = 10 } });

            Add(bar);
        }

        private void OnAttach()
        {
            _lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void TogglePlay()
        {
            _playing = !_playing;
            if (_playButton != null) _playButton.text = _playing ? "Pause" : "Play";
            _lastUpdate = EditorApplication.timeSinceStartup;
        }

        public void MarkDirty() => _viewport?.MarkDirtyRepaint();

        public void Refresh()
        {
            _needsReframe = true;
            MarkDirty();
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastUpdate);
            _lastUpdate = now;

            if (!_playing) return;

            float duration = CurrentDuration();
            if (duration <= 0.0001f) duration = 1f;

            _time += delta / duration;
            if (_loop)
            {
                _time = Mathf.Repeat(_time, 1f);
            }
            else
            {
                _time = Mathf.Clamp01(_time);
                if (_time >= 1f) TogglePlay();
            }

            _timeSlider?.SetValueWithoutNotify(_time);
            MarkDirty();
        }

        private float CurrentDuration()
        {
            var node = _nodeGetter();
            int count = node?.blendMotions?.Count ?? 0;
            if (count == 0) return 1f;

            Span<float> weights = count <= 64 ? stackalloc float[count] : new float[count];
            HonamiBlendSpaceSolver.CalculateWeights(node.blendMotions, node.AxisCount, node.spaceType, _sampleGetter(), weights);
            return HonamiBlendSpaceSolver.GetBlendedDuration(node.blendMotions, weights);
        }

        private bool EnsureSkeleton()
        {
            var node = _nodeGetter();
            _clipBuffer.Clear();

            if (node?.blendMotions != null)
            {
                foreach (var motion in node.blendMotions)
                {
                    if (motion?.clip != null) _clipBuffer.Add(motion.clip);
                }
            }

            if (_clipBuffer.Count == 0) return false;

            if (!_skeleton.IsBuilt || _skeleton.NeedsRebuild(_clipBuffer))
            {
                _skeleton.Build(_clipBuffer);
                _needsReframe = true;
            }

            return _skeleton.IsBuilt;
        }

        private void OnViewportGUI()
        {
            HandleInput();

            if (Event.current.type != EventType.Repaint) return;

            bool ready = EnsureSkeleton();
            _emptyHint.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            if (!ready) return;

            var node = _nodeGetter();
            _skeleton.SampleBlendSpace(node.blendMotions, node.AxisCount, node.spaceType, _sampleGetter(), _time, _loop, _reversedGetter());

            if (_needsReframe)
            {
                _stage.Frame(_skeleton.ComputeBounds());
                _needsReframe = false;
            }

            Rect rect = _viewport.contentRect;
            Texture texture = _stage.Render(rect, _skeleton.Bones, _skeleton.ParentIndex);
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        private void HandleInput()
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    _dragging = true;
                    _dragButton = e.button;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    _dragging = false;
                    break;
                case EventType.MouseDrag:
                    if (!_dragging) break;
                    if (_dragButton == 0) _stage.Orbit(e.delta);
                    else _stage.Pan(e.delta);
                    MarkDirty();
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    _stage.Zoom(e.delta.y);
                    MarkDirty();
                    e.Use();
                    break;
            }
        }

        public void Dispose()
        {
            EditorApplication.update -= OnEditorUpdate;
            _stage?.Dispose();
            _skeleton?.Dispose();
        }
    }
}
