#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private VisualElement _bottomBar;
        private Label _timeSecLabel;
        private Label _timeSepLabel;
        private Label _timeFrameLabel;
        private TextField _timeInput;
        private bool _isEditingTime;
        private bool _editingFrames;
        private Toggle _previewToggle;
        private Toggle _loopToggle;
        private Button _recordButton;
        private Button _playButton;
        private Image _playIcon;
        private Slider _zoomSlider;

        private VisualElement BuildTransportBar()
        {
            _bottomBar = new VisualElement
            {
                style =
                {
                    height = TimelineTheme.TransportBarHeight,
                    flexShrink = 0,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = TimelineTheme.ToolbarBg,
                    borderTopWidth = 1,
                    borderTopColor = TimelineTheme.Divider
                }
            };

            _bottomBar.Add(BuildTimecode());
            _bottomBar.Add(FlexSpace());
            _bottomBar.Add(BuildTransportControls());
            _bottomBar.Add(FlexSpace());
            _bottomBar.Add(BuildZoomControls());

            _bottomBar.schedule.Execute(RefreshTimeLabel).Every(100);
            return _bottomBar;
        }

        private VisualElement BuildTimecode()
        {
            var root = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minWidth = 110 } };

            _timeSecLabel = TimecodeLabel("Click to enter time in seconds", editFrames: false);
            root.Add(_timeSecLabel);

            _timeSepLabel = new Label(" / ")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = TimelineTheme.MutedText,
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            root.Add(_timeSepLabel);

            _timeFrameLabel = TimecodeLabel("Click to enter frame number", editFrames: true);
            root.Add(_timeFrameLabel);

            _timeInput = new TextField();
            _timeInput.style.minWidth = 70;
            _timeInput.style.display = DisplayStyle.None;
            _timeInput.style.fontSize = 12;
            _timeInput.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    CommitTimeEdit();
                else if (evt.keyCode == KeyCode.Escape)
                    CancelTimeEdit();
            });
            _timeInput.RegisterCallback<FocusOutEvent>(_ => CommitTimeEdit());
            root.Add(_timeInput);

            return root;
        }

        private Label TimecodeLabel(string tooltipText, bool editFrames)
        {
            var label = new Label
            {
                tooltip = tooltipText,
                pickingMode = PickingMode.Position,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = TimelineTheme.Text,
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            label.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0) return;
                BeginTimeEdit(editFrames);
                evt.StopPropagation();
            });
            label.RegisterCallback<PointerEnterEvent>(_ => label.style.color = TimelineTheme.Accent);
            label.RegisterCallback<PointerLeaveEvent>(_ => label.style.color = TimelineTheme.Text);
            return label;
        }

        private void BeginTimeEdit(bool editFrames)
        {
            if (editFrames && !TryGetFrameFactor(out _)) editFrames = false;

            _isEditingTime = true;
            _editingFrames = editFrames;
            _timeSecLabel.style.display = DisplayStyle.None;
            _timeSepLabel.style.display = DisplayStyle.None;
            _timeFrameLabel.style.display = DisplayStyle.None;
            _timeInput.style.display = DisplayStyle.Flex;

            string value = editFrames && TryGetFrameFactor(out float factor)
                ? Mathf.FloorToInt(_state.PlayheadTime * factor + 0.0001f).ToString()
                : _state.PlayheadTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            _timeInput.SetValueWithoutNotify(value);

            // Delayed focus: Unity's event system would immediately unfocus the field otherwise
            _bottomBar.schedule.Execute(() =>
            {
                _timeInput.Focus();
                _timeInput.SelectAll();
            });
        }

        private VisualElement BuildTransportControls()
        {
            var root = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            _previewToggle = HonamiToolbarControls.ToolbarToggle("Preview", _state.PreviewEnabled, value =>
            {
                _state.PreviewEnabled = value;
                if (_state.PreviewEnabled && !AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
                else if (!_state.PreviewEnabled && AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                    _state.IsPlaying = false;
                    _state.IsRecording = false;
                }
                _rebuild();
            });
            root.Add(_previewToggle);

            _recordButton = HonamiToolbarControls.IconButton("Animation.Record", "Record", () =>
            {
                _state.IsRecording = !_state.IsRecording;
                if (_state.IsRecording)
                {
                    _state.PreviewEnabled = true;
                    if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
                }
                _rebuild();
            });
            root.Add(_recordButton);

            root.Add(HonamiToolbarControls.IconButton("Animation.FirstKey", "Start", () =>
            {
                _state.PlayheadTime = 0f;
                _rebuild();
            }));

            _playButton = HonamiToolbarControls.IconButton("Animation.Play", "Play / Pause", () =>
            {
                TimelineState.TogglePlayback(_state);
                _rebuild();
            });
            _playIcon = _playButton.Q<Image>();
            root.Add(_playButton);

            root.Add(HonamiToolbarControls.IconButton("Animation.LastKey", "End", () =>
            {
                _state.PlayheadTime = _state.GetDuration();
                _rebuild();
            }));

            _loopToggle = HonamiToolbarControls.ToolbarToggle("Loop", _state.ForceLoop, value =>
            {
                _state.ForceLoop = value;
                _state.SaveSettings();
                _rebuild();
            });
            root.Add(_loopToggle);

            return root;
        }

        private VisualElement BuildZoomControls()
        {
            var root = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            root.Add(new Label("Zoom") { style = { color = TimelineTheme.MutedText, fontSize = 11, marginLeft = 8, marginRight = 5 } });
            _zoomSlider = new Slider(10f, 500f) { value = _state.TimeScale };
            _zoomSlider.style.width = 110;
            _zoomSlider.RegisterValueChangedCallback(evt =>
            {
                _state.TimeScale = evt.newValue;
                _state.SaveSettings();
                _rebuild();
            });
            root.Add(_zoomSlider);

            var fitButton = HonamiToolbarControls.ToolbarButton("Fit", () =>
            {
                float available = ViewportWidth() - _state.TrackHeaderWidth - 110f;
                _state.TimeScale = Mathf.Clamp(available / Mathf.Max(0.01f, _state.GetDuration()), 10f, 500f);
                _state.ScrollPos.x = 0f;
                _state.SaveSettings();
                _rebuild();
            });
            fitButton.tooltip = "Fit the whole duration into the view";
            root.Add(fitButton);

            return root;
        }

        public void TickRefresh()
        {
            if (_scroll == null) return;
            _scroll.scrollOffset = _state.ScrollPos;
            UpdatePlayheadVisual();
            RefreshTimeLabel();
        }

        public void RefreshTransport()
        {
            if (_bottomBar == null) return;

            _bottomBar.SetEnabled(!IsStateEmpty());
            _previewToggle.SetValueWithoutNotify(_state.PreviewEnabled);
            _loopToggle.SetValueWithoutNotify(_state.ForceLoop);
            _zoomSlider.SetValueWithoutNotify(_state.TimeScale);

            if (_playIcon != null)
                _playIcon.image = EditorGUIUtility.IconContent(_state.IsPlaying ? "PauseButton" : "Animation.Play").image;

            bool showRecord = _state.Mode == TimelineMode.HonamiClipEdit;
            _recordButton.style.display = showRecord ? DisplayStyle.Flex : DisplayStyle.None;
            if (showRecord)
            {
                _recordButton.SetEnabled(!_state.IsClipReadOnly);
                _recordButton.style.backgroundColor = _state.IsRecording
                    ? new Color(0.8f, 0.15f, 0.15f, 0.8f)
                    : TimelineTheme.ToolbarButton;
            }

            RefreshTimeLabel();
        }

        private void RefreshTimeLabel()
        {
            if (_timeSecLabel == null || _isEditingTime) return;

            _timeSecLabel.text = $"{_state.PlayheadTime:F2}s";

            if (TryGetFrameFactor(out float factor))
            {
                _timeSepLabel.style.display = DisplayStyle.Flex;
                _timeFrameLabel.style.display = DisplayStyle.Flex;
                _timeFrameLabel.text = $"{Mathf.FloorToInt(_state.PlayheadTime * factor + 0.0001f)}f";
            }
            else
            {
                _timeSepLabel.style.display = DisplayStyle.None;
                _timeFrameLabel.style.display = DisplayStyle.None;
            }
        }

        // Must match the ruler and arrow-key stepping, so raw clip fps without speed multipliers
        private bool TryGetFrameFactor(out float factor)
        {
            factor = DisplayFps();
            return factor > 0f;
        }

        private void CommitTimeEdit()
        {
            if (!_isEditingTime) return;
            string val = _timeInput.value.Replace(',', '.');
            if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                float newTime = _editingFrames && TryGetFrameFactor(out float factor) ? value / factor : value;
                _state.PlayheadTime = Mathf.Clamp(newTime, 0f, _state.GetDuration());
                _rebuild();
            }
            CancelTimeEdit();
        }

        private void CancelTimeEdit()
        {
            if (!_isEditingTime) return;
            _isEditingTime = false;
            _editingFrames = false;
            _timeSecLabel.style.display = DisplayStyle.Flex;
            _timeInput.style.display = DisplayStyle.None;
            RefreshTimeLabel();
        }

        private static VisualElement FlexSpace()
        {
            return new VisualElement { style = { flexGrow = 1 } };
        }
    }
}
#endif
