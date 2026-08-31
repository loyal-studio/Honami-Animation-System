#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Preview
{
    /// <summary>
    /// Live skeleton preview of a transition. In crossfade mode it plays a small timeline — source
    /// leads in, blends into target over the transition window (curve or linear), then target plays
    /// out — so both clips actually animate while the blend happens. When only one side is poseable
    /// (AnyState source, Exit / portal / null target) it loops that single side. The source can be
    /// overridden from a dropdown to preview an AnyState / interrupt transition from a chosen state.
    /// </summary>
    internal sealed class HonamiTransitionPreview : VisualElement, IDisposable
    {
        private const float ViewportHeight = 170f;
        private const float LeadTailSeconds = 0.75f;

        private enum Mode { Crossfade, SingleSource, SingleTarget }

        private readonly HonamiState _source;
        private readonly HonamiState _target;
        private readonly HonamiTransition _transition;
        private readonly HonamiController _controller;
        private readonly List<HonamiState> _candidates = new();

        private HonamiState _sourceOverride;
        private float _sourceBlend;
        private float _targetBlend;
        private HonamiState _sourceBlendState;

        private readonly HonamiPreviewStage _stage = new();
        private readonly HonamiPreviewSkeleton _skeleton = new();
        private readonly List<AnimationClip> _clipBuffer = new();
        private HonamiPreviewSkeleton.Pose _poseFrom;
        private HonamiPreviewSkeleton.Pose _poseTo;

        private IMGUIContainer _viewport;
        private Slider _progressSlider;
        private Button _playButton;
        private Label _headerLabel;
        private Label _weightLabel;
        private int _lastDisplayedWeightPct = -1;
        private DropdownField _fromDropdown;
        private VisualElement _blendContainer;

        private bool _playing;
        private bool _loop = true;
        private float _progress;
        private bool _needsReframe = true;
        private bool _dragging;
        private int _dragButton;
        private double _lastUpdate;

        public static bool CanPreview(HonamiState state)
        {
            switch (state?.node)
            {
                case HonamiAnimationNode a: return a.clip != null;
                case HonamiBlendTreeNode bt: return bt.blendMotions != null && bt.blendMotions.Count > 0;
                case HonamiRandomAnimationNode r: return FirstClip(r) != null;
                default: return false;
            }
        }

        public static bool CanPreviewTransition(HonamiState source, HonamiState target)
            => CanPreview(source) || CanPreview(target);

        public HonamiTransitionPreview(HonamiState source, HonamiState target, HonamiTransition transition, HonamiController controller)
        {
            _source = source;
            _target = target;
            _transition = transition;
            _controller = controller;

            CollectCandidates();

            if (_target?.node is HonamiBlendTreeNode tbt)
                _targetBlend = MidBlendValue(tbt);

            name = "honami-transition-preview";
            style.flexDirection = FlexDirection.Column;

            Build();
            RefreshChrome();

            RegisterCallback<AttachToPanelEvent>(_ => OnAttach());
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
        }

        private void OnAttach()
        {
            _lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private HonamiState EffectiveSource => _sourceOverride != null ? _sourceOverride : _source;

        private Mode CurrentMode
        {
            get
            {
                bool s = CanPreview(EffectiveSource);
                bool t = CanPreview(_target);
                return s && t ? Mode.Crossfade : (s ? Mode.SingleSource : Mode.SingleTarget);
            }
        }

        private HonamiState PoseState => CurrentMode == Mode.SingleTarget ? _target : EffectiveSource;

        private void CollectCandidates()
        {
            _candidates.Clear();
            if (_controller?.states == null) return;
            for (int i = 0; i < _controller.states.Count; i++)
            {
                var st = _controller.states[i];
                if (CanPreview(st)) _candidates.Add(st);
            }
        }

        private void Build()
        {
            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            _headerLabel = new Label
            {
                style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.7f, 0.7f, 0.7f), flexGrow = 1 }
            };
            headerRow.Add(_headerLabel);

            _weightLabel = new Label("0%")
            {
                style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.5f, 0.7f, 1f) }
            };
            headerRow.Add(_weightLabel);
            Add(headerRow);

            if (_candidates.Count > 0)
                Add(BuildSourcePicker());

            _blendContainer = new VisualElement();
            Add(_blendContainer);
            RebuildBlendControls();

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

            Add(BuildTransport());
        }

        private VisualElement BuildSourcePicker()
        {
            var choices = new List<string> { AutoChoiceLabel() };
            for (int i = 0; i < _candidates.Count; i++)
                choices.Add(_candidates[i].stateName);

            _fromDropdown = new DropdownField("From", choices, 0) { style = { marginBottom = 4 } };
            _fromDropdown.RegisterValueChangedCallback(_ =>
            {
                int idx = _fromDropdown.index;
                _sourceOverride = idx <= 0 ? null : _candidates[idx - 1];
                _progress = 0f;
                _progressSlider?.SetValueWithoutNotify(0f);
                _needsReframe = true;
                RebuildBlendControls();
                RefreshChrome();
                MarkDirty();
            });
            return _fromDropdown;
        }

        private string AutoChoiceLabel()
            => CanPreview(_source) ? $"Source: {_source.stateName}" : "None (target only)";

        private void RebuildBlendControls()
        {
            if (_blendContainer == null) return;
            _blendContainer.Clear();

            var src = EffectiveSource;
            if (src?.node is HonamiBlendTreeNode sourceTree)
            {
                if (_sourceBlendState != src)
                {
                    _sourceBlend = MidBlendValue(sourceTree);
                    _sourceBlendState = src;
                }
                GetBlendRange(sourceTree, out float min, out float max);
                _sourceBlend = Mathf.Clamp(_sourceBlend, min, max);
                _blendContainer.Add(BlendSliderRow($"{src.stateName} blend", min, max, _sourceBlend,
                    v => { _sourceBlend = v; MarkDirty(); }));
            }

            if (_target?.node is HonamiBlendTreeNode targetTree)
            {
                GetBlendRange(targetTree, out float min, out float max);
                _targetBlend = Mathf.Clamp(_targetBlend, min, max);
                _blendContainer.Add(BlendSliderRow($"{_target.stateName} blend", min, max, _targetBlend,
                    v => { _targetBlend = v; MarkDirty(); }));
            }
        }

        private static VisualElement BlendSliderRow(string label, float min, float max, float value, Action<float> onChange)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            row.Add(new Label(label) { style = { fontSize = 9, color = new Color(0.62f, 0.72f, 0.85f), width = 90, flexShrink = 0, overflow = Overflow.Hidden } });

            var slider = new Slider(min, max) { value = value, style = { flexGrow = 1, marginRight = 4 } };
            var valueLabel = new Label(value.ToString("0.##")) { style = { fontSize = 9, color = new Color(0.62f, 0.72f, 0.85f), width = 32, unityTextAlign = TextAnchor.MiddleRight, flexShrink = 0 } };
            slider.RegisterValueChangedCallback(evt =>
            {
                onChange(evt.newValue);
                valueLabel.text = evt.newValue.ToString("0.##");
            });

            row.Add(slider);
            row.Add(valueLabel);
            return row;
        }

        private static void GetBlendRange(HonamiBlendTreeNode node, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            var motions = node.blendMotions;
            for (int i = 0; i < motions.Count; i++)
            {
                float t = motions[i].threshold;
                if (t < min) min = t;
                if (t > max) max = t;
            }
            if (min > max) { min = 0f; max = 1f; }
            if (Mathf.Approximately(min, max)) { min -= 1f; max += 1f; }
        }

        private void RefreshChrome()
        {
            if (_headerLabel != null) _headerLabel.text = HeaderText();
            if (_weightLabel != null)
                _weightLabel.style.display = CurrentMode == Mode.Crossfade ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private string HeaderText()
        {
            string src = EffectiveSource != null ? EffectiveSource.stateName : "AnyState";
            string tgt = _target != null ? _target.stateName : "Exit";
            return CurrentMode switch
            {
                Mode.Crossfade => $"{src}  →  {tgt}",
                Mode.SingleTarget => $"▶  {tgt}",
                _ => $"▶  {src}"
            };
        }

        private VisualElement BuildTransport()
        {
            var bar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            _playButton = new Button(TogglePlay) { text = "Play", style = { width = 46, marginRight = 4, fontSize = 10 } };
            bar.Add(_playButton);

            _progressSlider = new Slider(0f, 1f) { value = _progress, style = { flexGrow = 1, marginRight = 4 } };
            _progressSlider.RegisterValueChangedCallback(evt => { _progress = evt.newValue; MarkDirty(); });
            bar.Add(_progressSlider);

            var loopToggle = new Toggle { value = _loop, tooltip = "Loop playback", style = { marginRight = 2 } };
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
            if (_playing && !_loop && _progress >= 0.999f) _progress = 0f;
            _lastUpdate = EditorApplication.timeSinceStartup;
        }

        public void MarkDirty() => _viewport?.MarkDirtyRepaint();

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastUpdate);
            _lastUpdate = now;

            if (!_playing) return;

            _progress += dt / CurrentDuration();
            if (_loop)
            {
                _progress = Mathf.Repeat(_progress, 1f);
            }
            else if (_progress >= 1f)
            {
                _progress = 1f;
                TogglePlay();
            }

            _progressSlider?.SetValueWithoutNotify(_progress);
            MarkDirty();
        }

        private float CurrentDuration()
        {
            if (CurrentMode != Mode.Crossfade)
                return SafeDuration(PoseState);

            CrossfadeTiming(out _, out _, out float leadIn, out float dur, out float tailOut);
            return Mathf.Max(0.1f, leadIn + dur + tailOut);
        }

        private void CrossfadeTiming(out float srcDur, out float tgtDur, out float leadIn, out float dur, out float tailOut)
        {
            srcDur = SafeDuration(EffectiveSource);
            tgtDur = SafeDuration(_target);
            dur = Mathf.Max(0.02f, _transition != null ? _transition.duration : 0.25f);
            leadIn = _transition != null && _transition.hasExitTime
                ? Mathf.Clamp01(_transition.exitTime) * srcDur
                : Mathf.Min(srcDur, LeadTailSeconds);
            tailOut = Mathf.Min(tgtDur, LeadTailSeconds);
        }

        private bool EnsureSkeleton()
        {
            _clipBuffer.Clear();
            GatherClips(EffectiveSource, _clipBuffer);
            GatherClips(_target, _clipBuffer);
            if (_clipBuffer.Count == 0) return false;

            if (!_skeleton.IsBuilt || _skeleton.NeedsRebuild(_clipBuffer))
            {
                _skeleton.Build(_clipBuffer);
                _poseFrom = _skeleton.CreatePose();
                _poseTo = _skeleton.CreatePose();
                _needsReframe = true;
            }
            return _skeleton.IsBuilt;
        }

        private void OnViewportGUI()
        {
            HandleInput();
            if (Event.current.type != EventType.Repaint) return;
            if (!EnsureSkeleton()) return;

            if (CurrentMode == Mode.Crossfade) SampleCrossfade();
            else SampleState(PoseState, _progress, PoseState == _target ? _targetBlend : _sourceBlend);

            if (_needsReframe)
            {
                _stage.Frame(_skeleton.ComputeBounds());
                _needsReframe = false;
            }

            Rect r = _viewport.contentRect;
            Texture tex = _stage.Render(r, _skeleton.Bones, _skeleton.ParentIndex);
            if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, false);
        }

        private void SampleCrossfade()
        {
            CrossfadeTiming(out float srcDur, out float tgtDur, out float leadIn, out float dur, out float tailOut);
            float total = Mathf.Max(0.1f, leadIn + dur + tailOut);
            float tau = _progress * total;

            float srcNorm = srcDur > 0f ? Mathf.Repeat(tau / srcDur, 1f) : 0f;

            float tgtElapsed = Mathf.Max(0f, tau - leadIn);
            float tgtSeconds = (_transition != null ? _transition.destinationStartTime : 0f) + tgtElapsed;
            float tgtNorm = tgtDur > 0f ? Mathf.Repeat(tgtSeconds / tgtDur, 1f) : 0f;

            float weight;
            if (tau <= leadIn) weight = 0f;
            else if (tau >= leadIn + dur) weight = 1f;
            else
            {
                float p = (tau - leadIn) / dur;
                var blendCurve = _transition?.ResolveBlendCurve();
                weight = blendCurve != null ? blendCurve.Evaluate(p) : p;
            }

            SampleState(EffectiveSource, srcNorm, _sourceBlend);
            _skeleton.CaptureInto(_poseFrom);
            SampleState(_target, tgtNorm, _targetBlend);
            _skeleton.CaptureInto(_poseTo);

            _skeleton.ApplyCrossfade(_poseFrom, _poseTo, weight);
            if (_weightLabel != null)
            {
                int pct = Mathf.RoundToInt(weight * 100f);
                if (pct != _lastDisplayedWeightPct)
                {
                    _lastDisplayedWeightPct = pct;
                    _weightLabel.text = $"{pct}%";
                }
            }
        }

        private bool SampleState(HonamiState state, float normalizedTime, float blendValue)
        {
            bool reversed = state != null && state.isReversed;
            switch (state?.node)
            {
                case HonamiAnimationNode a when a.clip != null:
                    _skeleton.SampleSingle(a.clip, normalizedTime, true, reversed);
                    return true;
                case HonamiBlendTreeNode bt when bt.blendMotions != null && bt.blendMotions.Count > 0:
                    _skeleton.SampleBlend(bt.blendMotions, blendValue, normalizedTime, true, reversed);
                    return true;
                case HonamiRandomAnimationNode r when FirstClip(r) != null:
                    _skeleton.SampleSingle(FirstClip(r), normalizedTime, true, reversed);
                    return true;
                default:
                    return false;
            }
        }

        private static float SafeDuration(HonamiState state)
        {
            float d = HonamiStateInspector.GetStateDuration(state);
            return d > 0f ? d : 1f;
        }

        private static float MidBlendValue(HonamiBlendTreeNode node)
        {
            float min = float.MaxValue, max = float.MinValue;
            var motions = node.blendMotions;
            for (int i = 0; i < motions.Count; i++)
            {
                float t = motions[i].threshold;
                if (t < min) min = t;
                if (t > max) max = t;
            }
            if (min > max) return 0f;
            return (min + max) * 0.5f;
        }

        private static AnimationClip FirstClip(HonamiRandomAnimationNode node)
        {
            var clips = node?.randomClips;
            if (clips == null) return null;
            for (int i = 0; i < clips.Count; i++)
                if (clips[i]?.clip != null) return clips[i].clip;
            return null;
        }

        private static void GatherClips(HonamiState state, List<AnimationClip> into)
        {
            switch (state?.node)
            {
                case HonamiAnimationNode a when a.clip != null:
                    into.Add(a.clip);
                    break;
                case HonamiBlendTreeNode bt when bt.blendMotions != null:
                    for (int i = 0; i < bt.blendMotions.Count; i++)
                        if (bt.blendMotions[i]?.clip != null) into.Add(bt.blendMotions[i].clip);
                    break;
                case HonamiRandomAnimationNode r when r.randomClips != null:
                    for (int i = 0; i < r.randomClips.Count; i++)
                        if (r.randomClips[i]?.clip != null) into.Add(r.randomClips[i].clip);
                    break;
            }
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
