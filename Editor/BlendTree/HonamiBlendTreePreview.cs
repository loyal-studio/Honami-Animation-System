#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
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
        private readonly List<AnimationClip> _slotClips = new();
        private readonly HonamiHumanoidPreviewBinder _humanoid = new();
        private string _buildHint;

        private IMGUIContainer _viewport;
        private Slider _timeSlider;
        private Button _playButton;
        private Label _emptyHint;
        private ObjectField _modelField;

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
            // Humanoid clips have no transform curves, so they are posed on a copy of a real model.
            _modelField = new ObjectField("Preview Model")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false,
                value = _state.PreviewModel != null ? _state.PreviewModel : HonamiHumanoidPreviewBinder.LoadModel(_state.Controller),
                tooltip = "Humanoid model (prefab or model asset with an Animator) used to show Humanoid clips. " +
                          "When empty, the Animator of a scene object using this controller is used.",
                style = { display = DisplayStyle.None, marginBottom = 4 }
            };
            _state.PreviewModel = _modelField.value as GameObject;
            _modelField.RegisterValueChangedCallback(evt =>
            {
                _state.PreviewModel = evt.newValue as GameObject;
                HonamiHumanoidPreviewBinder.SaveModel(_state.Controller, _state.PreviewModel);
                _humanoid.Reset();
                MarkDirty();
            });
            Add(_modelField);

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
            _humanoid.Reset();
            // The state forgets its model when it is retargeted; the remembered one is the source of truth.
            if (_state.PreviewModel == null)
                _state.PreviewModel = HonamiHumanoidPreviewBinder.LoadModel(_state.Controller);
            _modelField?.SetValueWithoutNotify(_state.PreviewModel);
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
            if (_state.IsDirectional2D)
                return _state.GetDirectionalPreviewDuration();

            var node = _state.Node;
            if (node == null || node.blendMotions == null || node.blendMotions.Count == 0) return 1f;
            return HonamiBlendTreeEvaluator.GetDurationFromMotions(node.blendMotions, _state.PreviewValue);
        }

        private bool EnsureSkeleton()
        {
            _buildHint = null;
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

            HonamiPreviewRigKind rig = HonamiPreviewSkeleton.Classify(_clipBuffer);
            _modelField.style.display = rig == HonamiPreviewRigKind.Humanoid ? DisplayStyle.Flex : DisplayStyle.None;
            if (rig == HonamiPreviewRigKind.Mixed)
            {
                ReleaseHumanoid();
                _buildHint = HonamiHumanoidPreviewBinder.MixedRigHint;
                return false;
            }
            if (rig == HonamiPreviewRigKind.Humanoid)
                return EnsureHumanoidSkeleton(node);

            if (!_skeleton.IsBuilt || _skeleton.IsHumanoid || _skeleton.NeedsRebuild(_clipBuffer))
            {
                _skeleton.Build(_clipBuffer);
                _needsReframe = true;
            }
            return _skeleton.IsBuilt;
        }

        private bool EnsureHumanoidSkeleton(HonamiBlendTreeNode node)
        {
            // One graph slot per motion keeps repeated clips independent (they may run at different speeds).
            _slotClips.Clear();
            for (int i = 0; i < node.blendMotions.Count; i++)
                _slotClips.Add(node.blendMotions[i]?.clip);

            bool ok = _humanoid.Ensure(_skeleton, _stage, _slotClips, _state.PreviewModel, _state.Controller,
                out bool rebuilt);
            if (rebuilt) _needsReframe = true;
            _buildHint = ok ? null : _humanoid.Error;
            return ok;
        }

        // Drops a previously built Humanoid rig (and its model copy) once the clips stop being Humanoid.
        private void ReleaseHumanoid()
        {
            if (!_skeleton.IsHumanoid) return;
            _stage.SetModelRoot(null);
            _skeleton.Dispose();
            _humanoid.Reset();
        }

        private void OnViewportGUI()
        {
            HandleInput();

            if (Event.current.type != EventType.Repaint) return;

            bool ready = EnsureSkeleton();
            _emptyHint.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            if (!ready)
            {
                _emptyHint.text = string.IsNullOrEmpty(_buildHint) ? "No clips with transform curves." : _buildHint;
                return;
            }

            var node = _state.Node;
            if (_state.IsDirectional2D && !string.IsNullOrEmpty(_state.DirectionalPreviewError))
            {
                _emptyHint.text = _state.DirectionalPreviewError;
                _emptyHint.style.display = DisplayStyle.Flex;
                return;
            }
            _emptyHint.text = "No clips with transform curves.";
            bool reversed = _state.State != null && _state.State.isReversed;
            if (_skeleton.IsHumanoid)
            {
                if (_state.IsDirectional2D)
                    _skeleton.SampleHumanoidDirectional(node.blendMotions, _state.Weights, _time, reversed, false);
                else
                    _skeleton.SampleHumanoidBlend(node.blendMotions, _state.PreviewValue, _time, reversed, false);
            }
            else if (_state.IsDirectional2D)
                _skeleton.SampleDirectionalBlend(node.blendMotions, _state.Weights, _time, reversed);
            else
                _skeleton.SampleBlend(node.blendMotions, _state.PreviewValue, _time, _loop, reversed);

            if (_needsReframe)
            {
                if (!_stage.FrameModel()) _stage.Frame(_skeleton.ComputeBounds());
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
                    if (e.button != 1 && e.button != 2) break;
                    _dragging = true;
                    _dragButton = e.button;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (!_dragging || e.button != _dragButton) break;
                    _dragging = false;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (!_dragging) break;
                    if (_dragButton == 1) _stage.Orbit(e.delta);
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
            _stage?.SetModelRoot(null);
            // Skeleton first: the stage's PreviewRenderUtility cleanup destroys the model copy, after which
            // the skeleton could no longer unregister itself from its live list.
            _skeleton?.Dispose();
            _stage?.Dispose();
        }
    }
}
#endif
