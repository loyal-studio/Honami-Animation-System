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
        private Label _timeLabel;
        private Label _durationLabel;
        private TextField _timeInput;
        private bool _isEditingTime;
        private bool _editingFrames;
        private Toggle _previewToggle;
        private Toggle _loopToggle;
        private Button _recordButton;
        private Image _playIcon;
        private Label _speedLabel;
        private Slider _zoomSlider;
        private string _lastTimeText;

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

            _timeLabel = TimecodeLabel("Click to type a time: 1:05:12 (min:sec:frames), 2.5s or 120f", editFrames: false);
            _timeLabel.style.minWidth = 72;
            root.Add(_timeLabel);

            _durationLabel = new Label
            {
                tooltip = "Duration and frame rate",
                pickingMode = PickingMode.Ignore,
                style = { unityTextAlign = TextAnchor.MiddleLeft, color = TimelineTheme.MutedText, fontSize = 11, marginLeft = 4 }
            };
            root.Add(_durationLabel);

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
                evt.StopPropagation();
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
            _isEditingTime = true;
            SetTimeLabelsVisible(false);
            _timeInput.style.display = DisplayStyle.Flex;

            if (editFrames && !_state.HasFrameRate) editFrames = false;
            _editingFrames = editFrames;
            string value = editFrames
                ? TimelineTimecode.ToFrame(_state.PlayheadTime, _state.FrameRate).ToString()
                : _state.ShowFrames
                    ? _state.PlayheadTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                    : FormatTime(_state.PlayheadTime);
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
                else if (!_state.PreviewEnabled)
                {
                    HonamiTimelineModuleEditorRegistry.StopAllPreviews();
                    if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                    _state.IsPlaying = false;
                    _state.IsRecording = false;
                }
                _rebuild();
            });
            _previewToggle.tooltip = "Drive the scene from the timeline. Turning it off restores everything the preview changed.";
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

            root.Add(TransportButton("Animation.FirstKey", "Go to start (Home)", () => SetPlayhead(0f)));
            root.Add(TransportButton("Animation.PrevKey", "Previous edit point (Up)", () => JumpToEditPoint(-1)));
            root.Add(TransportButton("d_Profiler.PrevFrame", "Previous frame (Left)", () => SetPlayhead(_state.SnapToFrame(_state.PlayheadTime) - _state.FrameDuration)));

            var playButton = HonamiToolbarControls.IconButton("Animation.Play", "Play / Pause (Space)", () =>
            {
                TimelineState.TogglePlayback(_state);
                _rebuild();
            });
            _playIcon = playButton.Q<Image>();
            root.Add(playButton);

            root.Add(TransportButton("d_Profiler.NextFrame", "Next frame (Right)", () => SetPlayhead(_state.SnapToFrame(_state.PlayheadTime) + _state.FrameDuration)));
            root.Add(TransportButton("Animation.NextKey", "Next edit point (Down)", () => JumpToEditPoint(1)));
            root.Add(TransportButton("Animation.LastKey", "Go to end (End)", () => SetPlayhead(_state.GetDuration())));

            _speedLabel = new Label
            {
                tooltip = "Shuttle speed. J/L change it, K stops.",
                style = { color = TimelineTheme.Accent, fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, minWidth = 28, unityTextAlign = TextAnchor.MiddleCenter }
            };
            root.Add(_speedLabel);

            _loopToggle = HonamiToolbarControls.ToolbarToggle("Loop", _state.ForceLoop, value =>
            {
                _state.ForceLoop = value;
                _state.SaveSettings();
                _rebuild();
            });
            root.Add(_loopToggle);

            return root;
        }

        private static Button TransportButton(string icon, string tooltip, System.Action action)
        {
            var button = HonamiToolbarControls.IconButton(icon, tooltip, action);
            button.tooltip = tooltip;
            return button;
        }

        private VisualElement BuildZoomControls()
        {
            var root = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            root.Add(new Label("Zoom") { style = { color = TimelineTheme.MutedText, fontSize = 11, marginLeft = 8, marginRight = 5 } });
            _zoomSlider = new Slider(Mathf.Log(TimelineState.MinTimeScale), Mathf.Log(TimelineState.MaxTimeScale)) { value = Mathf.Log(_state.TimeScale) };
            _zoomSlider.style.width = 110;
            _zoomSlider.tooltip = "Zoom (Ctrl + mouse wheel zooms at the cursor)";
            _zoomSlider.RegisterValueChangedCallback(evt =>
            {
                float scale = Mathf.Exp(evt.newValue);
                ZoomAt(scale / _state.TimeScale, PlayheadViewportX());
            });
            root.Add(_zoomSlider);

            var fitButton = HonamiToolbarControls.ToolbarButton("Fit", FitAll);
            fitButton.tooltip = "Fit the whole duration into the view (\\)";
            root.Add(fitButton);

            return root;
        }

        public void TickRefresh()
        {
            if (_state.IsPlaying) FollowPlayhead();
            ApplyScroll();
            UpdatePlayheadVisual();
            RefreshTimeLabel();
        }

        public void RefreshTransport()
        {
            if (_bottomBar == null) return;

            _bottomBar.SetEnabled(!IsStateEmpty());
            _previewToggle.SetValueWithoutNotify(_state.PreviewEnabled);
            _loopToggle.SetValueWithoutNotify(_state.ForceLoop);
            _zoomSlider.SetValueWithoutNotify(Mathf.Log(_state.TimeScale));

            if (_playIcon != null)
                _playIcon.image = EditorGUIUtility.IconContent(_state.IsPlaying ? "PauseButton" : "Animation.Play").image;

            float speed = _state.PlaybackSpeed;
            _speedLabel.text = _state.IsPlaying && !Mathf.Approximately(speed, 1f) ? $"{speed:0.#}×" : string.Empty;

            bool showRecord = _state.Mode == TimelineMode.HonamiClipEdit;
            _recordButton.style.display = showRecord ? DisplayStyle.Flex : DisplayStyle.None;
            if (showRecord)
            {
                _recordButton.SetEnabled(!_state.IsClipReadOnly);
                _recordButton.style.backgroundColor = _state.IsRecording
                    ? new Color(0.8f, 0.15f, 0.15f, 0.8f)
                    : TimelineTheme.ToolbarButton;
            }

            _lastTimeText = null;
            RefreshTimeLabel();
        }

        private void RefreshTimeLabel()
        {
            if (_timeSecLabel == null || _isEditingTime) return;

            bool empty = IsStateEmpty();
            bool frames = _state.ShowFrames;
            string text;
            if (frames)
            {
                string secondsText = empty ? "0.00s" : $"{Mathf.Round(_state.PlayheadTime * 100f) / 100f:F2}s";
                string framesText = empty || !_state.HasFrameRate ? string.Empty : $"{TimelineTimecode.ToFrame(_state.PlayheadTime, _state.FrameRate)}f";
                text = secondsText + framesText;
                if (text == _lastTimeText) return;
                _timeSecLabel.text = secondsText;
                _timeFrameLabel.text = framesText;
            }
            else
            {
                string timeText = empty ? "--:--:--" : FormatTime(_state.PlayheadTime);
                string durationText = empty ? string.Empty
                    : _state.Mode == TimelineMode.HonamiTimeline ? $"/ {FormatTime(_state.GetDuration())}  ·  {_state.FrameRate:0.##} fps"
                    : $"/ {FormatTime(_state.GetDuration())}";
                text = timeText + durationText;
                if (text == _lastTimeText) return;
                _timeLabel.text = timeText;
                _durationLabel.text = durationText;
            }

            _lastTimeText = text;
            SetTimeLabelsVisible(true);
            RefreshCornerTimecode();
        }

        private void CommitTimeEdit()
        {
            if (!_isEditingTime) return;
            var format = _editingFrames ? TimelineTimeFormat.Frames : _state.ShowFrames ? TimelineTimeFormat.Seconds : _state.EffectiveTimeFormat;
            if (TimelineTimecode.TryParse(_timeInput.value, _state.FrameRate, format, out float time))
            {
                _state.PlayheadTime = Mathf.Clamp(time, 0f, _state.GetDuration());
                _rebuild();
            }
            CancelTimeEdit();
        }

        private void CancelTimeEdit()
        {
            if (!_isEditingTime) return;
            _isEditingTime = false;
            _editingFrames = false;
            _timeInput.style.display = DisplayStyle.None;
            _lastTimeText = null;
            RefreshTimeLabel();
        }

        private void SetTimeLabelsVisible(bool visible)
        {
            var framesMode = visible && _state.ShowFrames ? DisplayStyle.Flex : DisplayStyle.None;
            var frameNumber = visible && _state.ShowFrames && _state.HasFrameRate ? DisplayStyle.Flex : DisplayStyle.None;
            var timecodeMode = visible && !_state.ShowFrames ? DisplayStyle.Flex : DisplayStyle.None;
            _timeSecLabel.style.display = framesMode;
            _timeSepLabel.style.display = frameNumber;
            _timeFrameLabel.style.display = frameNumber;
            _timeLabel.style.display = timecodeMode;
            _durationLabel.style.display = timecodeMode;
        }

        private static VisualElement FlexSpace()
        {
            return new VisualElement { style = { flexGrow = 1 } };
        }
    }
}
