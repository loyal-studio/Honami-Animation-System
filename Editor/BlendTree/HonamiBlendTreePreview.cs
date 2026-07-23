#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Preview;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>
    /// Embeddable live skeleton preview for a blend tree. Owns an isolated preview stage plus a
    /// generated skeleton and drives blend sampling from the state's preview value and a compact
    /// playback transport. Designed to sit inside the output node card.
    /// </summary>
    internal sealed class HonamiBlendTreePreview : VisualElement, IDisposable
    {
        private const float ViewportHeight = 160f;

        private readonly BlendTreeState _state;
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

        public HonamiBlendTreePreview(BlendTreeState state)
        {
            _state = state;

            name = "honami-blendtree-preview";
            style.flexDirection = FlexDirection.Column;

            Build();

            RegisterCallback<AttachToPanelEvent>(_ => OnAttach());
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
        }

        private void OnAttach()
        {
            _lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
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
                    color = BlendTreeTheme.MutedText,
                    fontSize = 9,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 4
                }
            };
            Add(_emptyHint);

            Add(BuildTransport());
        }

        private VisualElement BuildTransport()
        {
            var bar = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
            };

            _playButton = new Button(TogglePlay) { text = "Play", style = { width = 42, marginRight = 4, fontSize = 10 } };
            bar.Add(_playButton);

            _timeSlider = new Slider(0f, 1f) { value = _time, style = { flexGrow = 1, marginRight = 4 } };
            _timeSlider.RegisterValueChangedCallback(evt => { _time = evt.newValue; MarkDirty(); });
            bar.Add(_timeSlider);

            var loopToggle = new Toggle { value = _loop, tooltip = "Loop", style = { marginRight = 2 } };
            loopToggle.RegisterValueChangedCallback(evt => _loop = evt.newValue);
            bar.Add(loopToggle);

            var fit = new Button(() => { _needsReframe = true; MarkDirty(); }) { text = "Fit", style = { width = 30, fontSize = 10 } };
            bar.Add(fit);

            return bar;
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
            float dt = (float)(now - _lastUpdate);
            _lastUpdate = now;

            if (!_playing) return;

            float duration = CurrentDuration();
            if (duration <= 0.0001f) duration = 1f;

            _time += dt / duration;
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
            var node = _state.Node;
            if (node == null || node.blendMotions == null || node.blendMotions.Count == 0) return 1f;
            return HonamiBlendTreeEvaluator.GetDurationFromMotions(node.blendMotions, _state.PreviewValue);
        }

        private bool EnsureSkeleton()
        {
            var node = _state.Node;
            _clipBuffer.Clear();
            if (node?.blendMotions != null)
            {
                for (int i = 0; i < node.blendMotions.Count; i++)
                {
                    var clip = node.blendMotions[i]?.clip;
                    if (clip != null) _clipBuffer.Add(clip);
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

            var node = _state.Node;
            bool reversed = _state.State != null && _state.State.isReversed;
            _skeleton.SampleBlend(node.blendMotions, _state.PreviewValue, _time, _loop, reversed);

            if (_needsReframe)
            {
                _stage.Frame(_skeleton.ComputeBounds());
                _needsReframe = false;
            }

            Rect r = _viewport.contentRect;
            Texture tex = _stage.Render(r, _skeleton.Bones, _skeleton.ParentIndex);
            if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, false);
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
#endif
