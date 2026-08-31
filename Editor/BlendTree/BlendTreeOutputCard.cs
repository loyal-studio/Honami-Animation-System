#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>
    /// Draggable node card representing the blend tree's final output: shows the blend type,
    /// driving parameter and a live bar reflecting the current preview value.
    /// </summary>
    internal sealed class BlendTreeOutputCard : VisualElement
    {
        private readonly BlendTreeState _state;
        private float _lastDisplayedValue = float.NaN;
        private Label _paramLabel;
        private Label _previewValueLabel;
        private Label _blendTypeBadge;
        private VisualElement _liveFill;
        private HonamiBlendTreePreview _preview;

        private Action _onPositionChanged;

        public BlendTreeOutputCard(BlendTreeState state, Action onPositionChanged)
        {
            _state = state;
            _onPositionChanged = onPositionChanged;

            name = "output-card";
            AddToClassList("honami-node-blend");
            style.position = Position.Absolute;
            style.width = _state.ShowPreview ? BlendTreeTheme.OutputNodePreviewWidth : BlendTreeTheme.OutputNodeWidth;
            style.backgroundColor = HonamiGraphStyles.BoxBg;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = HonamiGraphStyles.BoxBorder;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 6;
            style.overflow = Overflow.Hidden;

            var topBar = new VisualElement();
            topBar.AddToClassList("honami-node-top");
            topBar.AddToClassList("honami-node-top-blend");
            Add(topBar);



            var body = new VisualElement();
            body.AddToClassList("honami-bt-node-body");
            Add(body);

            var headerRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 }
            };

            var avatar = new VisualElement();
            avatar.AddToClassList("honami-node-avatar");
            avatar.AddToClassList("honami-node-avatar-blend");
            var icon = new VisualElement();
            icon.AddToClassList("honami-node-icon");
            icon.AddToClassList("honami-node-icon-blend");
            avatar.Add(icon);
            headerRow.Add(avatar);

            var textContainer = new VisualElement
            {
                style = { flexGrow = 1, flexDirection = FlexDirection.Column, marginLeft = 8, overflow = Overflow.Hidden }
            };

            _blendTypeBadge = new Label
            {
                style =
                {
                    fontSize = 9,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = BlendTreeTheme.Accent,
                    marginBottom = 2
                }
            };
            textContainer.Add(_blendTypeBadge);

            var outputLabel = new Label("Final Output")
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = BlendTreeTheme.Text
                }
            };
            textContainer.Add(outputLabel);

            headerRow.Add(textContainer);
            body.Add(headerRow);

            var separator = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = BlendTreeTheme.SubtleLine,
                    marginTop = 2,
                    marginBottom = 8,
                    marginLeft = -8,
                    marginRight = -8
                }
            };
            body.Add(separator);

            var paramRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 }
            };
            paramRow.Add(Caption("Parameter"));
            _paramLabel = new Label
            {
                style =
                {
                    flexGrow = 1,
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleRight,
                    color = BlendTreeTheme.MutedText,
                    overflow = Overflow.Hidden
                }
            };
            paramRow.Add(_paramLabel);
            body.Add(paramRow);

            var previewRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
            };
            previewRow.Add(Caption("Preview"));

            var liveBar = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    flexGrow = 1,
                    height = 6,
                    backgroundColor = BlendTreeTheme.ArcTrack,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    overflow = Overflow.Hidden
                }
            };
            _liveFill = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    height = Length.Percent(100),
                    width = Length.Percent(0),
                    backgroundColor = BlendTreeTheme.Accent,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };
            liveBar.Add(_liveFill);
            previewRow.Add(liveBar);

            _previewValueLabel = new Label("0.00")
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = BlendTreeTheme.Accent,
                    marginLeft = 8,
                    flexShrink = 0,
                    unityTextAlign = TextAnchor.MiddleRight,
                    minWidth = 36
                }
            };
            previewRow.Add(_previewValueLabel);

            body.Add(previewRow);

            if (_state.ShowPreview)
            {
                var previewSeparator = new VisualElement
                {
                    style =
                    {
                        height = 1,
                        backgroundColor = BlendTreeTheme.SubtleLine,
                        marginTop = 8,
                        marginBottom = 8,
                        marginLeft = -8,
                        marginRight = -8
                    }
                };
                body.Add(previewSeparator);

                _preview = new HonamiBlendTreePreview(_state);
                body.Add(_preview);
            }

            RefreshInfo(state);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            _state.OutputNodeMeasuredSize = new Vector2(evt.newRect.width, evt.newRect.height);
            _onPositionChanged?.Invoke();
        }



        public void RefreshInfo(BlendTreeState state)
        {
            string param = state.Node != null ? state.Node.blendParameter : null;
            bool hasParam = !string.IsNullOrEmpty(param);
            _paramLabel.text = hasParam ? param : "None";
            _paramLabel.style.color = hasParam ? BlendTreeTheme.Accent : BlendTreeTheme.MutedText;

            _blendTypeBadge.text = state.Node != null ? state.Node.blendType.ToString().ToUpperInvariant() : string.Empty;
        }

        public void UpdatePreviewValue(float paramValue, float min, float max)
        {
            float rounded = Mathf.Round(paramValue * 100f) / 100f;
            if (rounded != _lastDisplayedValue)
            {
                _lastDisplayedValue = rounded;
                _previewValueLabel.text = rounded.ToString("0.00");
            }

            float range = max - min;
            float t = range > 0.0001f ? Mathf.Clamp01((paramValue - min) / range) : 0f;
            _liveFill.style.width = Length.Percent(t * 100f);

            _preview?.MarkDirty();
        }

        private static Label Caption(string text)
        {
            var label = new Label(text);
            label.AddToClassList("honami-bt-node-caption");
            return label;
        }
    }
}
#endif
