#if UNITY_EDITOR
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    internal sealed partial class BlendTreePanelView
    {
        private Slider _slider;
        private FloatField _valueField;
        private Label _paramLabel;
        private Label _minLabel;
        private Label _maxLabel;
        private Slider _zoomSlider;
        private FloatField _valueFieldX;
        private FloatField _valueFieldY;

        private VisualElement BuildTransportBar()
        {
            if (_state.IsDirectional2D)
                return BuildDirectionalTransportBar();

            var bar = new VisualElement
            {
                style =
                {
                    height = BlendTreeTheme.TransportBarHeight,
                    flexShrink = 0,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = BlendTreeTheme.ToolbarBg,
                    borderTopWidth = 1,
                    borderTopColor = BlendTreeTheme.Divider
                }
            };

            var paramPill = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = BlendTreeTheme.AccentSoft,
                    paddingLeft = 7,
                    paddingRight = 7,
                    paddingTop = 2,
                    paddingBottom = 2,
                    borderTopLeftRadius = 10,
                    borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    marginRight = 8,
                    flexShrink = 0
                }
            };

            _paramLabel = new Label
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = BlendTreeTheme.Accent
                }
            };
            paramPill.Add(_paramLabel);
            bar.Add(paramPill);

            _valueField = new FloatField { value = _state.PreviewValue };
            _valueField.style.width = 60;
            _valueField.style.flexShrink = 0;
            _valueField.style.marginRight = 4;
            _valueField.RegisterValueChangedCallback(evt => SetPreviewValue(evt.newValue));
            bar.Add(_valueField);

            var resetBtn = new Button(() =>
            {
                _state.GetSliderRange(out float mn, out float mx);
                SetPreviewValue((mn + mx) * 0.5f);
            })
            {
                text = "↺",
                tooltip = "Reset preview value to midpoint",
                style =
                {
                    fontSize = 14,
                    width = 24,
                    height = 24,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                    marginRight = 6,
                    flexShrink = 0,
                    backgroundColor = BlendTreeTheme.ArcTrack,
                    color = BlendTreeTheme.MutedText,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    unityTextAlign = UnityEngine.TextAnchor.MiddleCenter
                }
            };
            bar.Add(resetBtn);

            _minLabel = new Label
            {
                style =
                {
                    color = BlendTreeTheme.MutedText,
                    fontSize = 10,
                    marginLeft = 4,
                    marginRight = 6,
                    flexShrink = 0
                }
            };
            bar.Add(_minLabel);

            _slider = new Slider(-1f, 1f) { value = _state.PreviewValue };
            _slider.style.flexGrow = 1;
            _slider.RegisterValueChangedCallback(evt => SetPreviewValue(evt.newValue));
            bar.Add(_slider);

            _maxLabel = new Label
            {
                style =
                {
                    color = BlendTreeTheme.MutedText,
                    fontSize = 10,
                    marginLeft = 6,
                    marginRight = 8,
                    flexShrink = 0
                }
            };
            bar.Add(_maxLabel);

            var zoomCaption = new Label("Zoom")
            {
                style =
                {
                    fontSize = 9,
                    color = BlendTreeTheme.MutedText,
                    marginRight = 3,
                    flexShrink = 0
                }
            };
            bar.Add(zoomCaption);

            _zoomSlider = new Slider(BlendTreeTheme.ZoomMin, BlendTreeTheme.ZoomMax) { value = _state.ViewScale };
            _zoomSlider.style.width = 80;
            _zoomSlider.style.flexShrink = 0;
            _zoomSlider.style.marginRight = 4;
            _zoomSlider.RegisterValueChangedCallback(evt => _canvas?.SetZoom(evt.newValue));
            bar.Add(_zoomSlider);

            bar.Add(HonamiToolbarControls.ToolbarButton("Fit", FrameAll));

            UpdateParamLabel();
            return bar;
        }

        private VisualElement BuildDirectionalTransportBar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    height = BlendTreeTheme.TransportBarHeight,
                    flexShrink = 0,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = BlendTreeTheme.ToolbarBg,
                    borderTopWidth = 1,
                    borderTopColor = BlendTreeTheme.Divider
                }
            };

            var captionX = ParameterCaption(_state.Node.blendParameter);
            captionX.AddToClassList("unity-base-field__label--with-dragger");
            bar.Add(captionX);
            _valueFieldX = new FloatField { value = _state.PreviewValueX, tooltip = "Preview X parameter" };
            _valueFieldX.style.width = 68;
            _valueFieldX.style.marginRight = 8;
            _valueFieldX.RegisterValueChangedCallback(evt => SetPreviewValueX(evt.newValue));
            bar.Add(_valueFieldX);
            new FieldMouseDragger<float>(_valueFieldX).SetDragZone(captionX);

            var captionY = ParameterCaption(_state.Node.blendParameterY);
            captionY.AddToClassList("unity-base-field__label--with-dragger");
            bar.Add(captionY);
            _valueFieldY = new FloatField { value = _state.PreviewValueY, tooltip = "Preview Y parameter" };
            _valueFieldY.style.width = 68;
            _valueFieldY.style.marginRight = 8;
            _valueFieldY.RegisterValueChangedCallback(evt => SetPreviewValueY(evt.newValue));
            bar.Add(_valueFieldY);
            new FieldMouseDragger<float>(_valueFieldY).SetDragZone(captionY);

            bar.Add(HonamiToolbarControls.ToolbarButton("Center", () =>
            {
                SetPreviewValueX(0f);
                SetPreviewValueY(0f);
                _valueFieldX?.SetValueWithoutNotify(0f);
                _valueFieldY?.SetValueWithoutNotify(0f);
            }));

            bar.Add(new VisualElement { style = { flexGrow = 1 } });
            AddZoomControls(bar);
            return bar;
        }

        private static Label ParameterCaption(string text)
        {
            return new Label(string.IsNullOrEmpty(text) ? "—" : text)
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = BlendTreeTheme.Accent,
                    marginRight = 4
                }
            };
        }

        private void AddZoomControls(VisualElement bar)
        {
            var zoomCaption = new Label("Zoom")
            {
                style =
                {
                    fontSize = 9,
                    color = BlendTreeTheme.MutedText,
                    marginRight = 3,
                    flexShrink = 0
                }
            };
            bar.Add(zoomCaption);

            _zoomSlider = new Slider(BlendTreeTheme.ZoomMin, BlendTreeTheme.ZoomMax) { value = _state.ViewScale };
            _zoomSlider.style.width = 80;
            _zoomSlider.style.flexShrink = 0;
            _zoomSlider.style.marginRight = 4;
            _zoomSlider.RegisterValueChangedCallback(evt => _canvas?.SetZoom(evt.newValue));
            bar.Add(_zoomSlider);
            bar.Add(HonamiToolbarControls.ToolbarButton("Fit", FrameAll));
        }

        private void UpdateSliderRange()
        {
            if (_state.IsDirectional2D)
            {
                _valueFieldX?.SetValueWithoutNotify(_state.PreviewValueX);
                _valueFieldY?.SetValueWithoutNotify(_state.PreviewValueY);
                return;
            }
            if (_slider == null) return;

            _state.GetSliderRange(out float min, out float max);
            _slider.lowValue = min;
            _slider.highValue = max;

            _state.PreviewValue = Mathf.Clamp(_state.PreviewValue, min, max);
            _slider.SetValueWithoutNotify(_state.PreviewValue);
            _valueField?.SetValueWithoutNotify(_state.PreviewValue);

            if (_minLabel != null) _minLabel.text = min.ToString("0.##");
            if (_maxLabel != null) _maxLabel.text = max.ToString("0.##");
        }

        private void UpdateParamLabel()
        {
            if (_paramLabel == null) return;
            if (_state.IsDirectional2D)
            {
                _paramLabel.text = $"{_state.Node.blendParameter} / {_state.Node.blendParameterY}";
                _paramLabel.style.color = BlendTreeTheme.Accent;
                return;
            }
            string param = _state.Node != null ? _state.Node.blendParameter : null;
            if (string.IsNullOrEmpty(param))
            {
                _paramLabel.text = "No Param";
                _paramLabel.style.color = BlendTreeTheme.MutedText;
            }
            else
            {
                _paramLabel.text = param;
                _paramLabel.style.color = BlendTreeTheme.Accent;
            }
        }
    }
}
#endif
