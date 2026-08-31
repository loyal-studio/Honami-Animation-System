#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using System;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>
    /// Draggable node card for a single blend motion: exposes the clip, threshold, speed
    /// and mirror fields, and visualizes its live blend weight as a radial arc.
    /// </summary>
    internal sealed class BlendTreeMotionCard : VisualElement
    {
        public int Index { get; }

        private readonly Color _color;
        private readonly BlendTreeState _state;
        private float _weight;
        private int _lastDisplayedPct = -1;

        private VisualElement _arcContainer;
        private Label _arcLabel;

        private Action _onPositionChanged;
        private Action<int> _onRemove;

        public BlendTreeMotionCard(SerializedProperty motionProp, HonamiBlendTreeMotion motion, int index, int count, BlendTreeState state, Action onPositionChanged, Action<int> onRemove)
        {
            Index = index;
            _color = BlendTreeTheme.MotionColor(index, count);
            _state = state;
            _onPositionChanged = onPositionChanged;
            _onRemove = onRemove;

            name = $"motion-card-{index}";
            AddToClassList("honami-node-animation");
            style.position = Position.Absolute;
            style.width = BlendTreeTheme.NodeWidth;
            style.backgroundColor = HonamiGraphStyles.BoxBg;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = HonamiGraphStyles.BoxBorder;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 6;
            style.overflow = Overflow.Hidden;

            var topBar = new VisualElement();
            topBar.AddToClassList("honami-node-top");
            topBar.style.backgroundColor = _color;
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
            var ring = new Color(_color.r, _color.g, _color.b, 0.7f);
            avatar.style.borderTopColor = avatar.style.borderBottomColor = avatar.style.borderLeftColor = avatar.style.borderRightColor = ring;
            var icon = new VisualElement();
            icon.AddToClassList("honami-node-icon");
            icon.style.backgroundColor = _color;
            avatar.Add(icon);
            headerRow.Add(avatar);

            var textContainer = new VisualElement
            {
                style = { flexGrow = 1, flexDirection = FlexDirection.Column, marginLeft = 8, overflow = Overflow.Hidden }
            };

            string titleStr = motion != null && motion.clip != null ? motion.clip.name : "Unset Motion";
            var titleLabel = new Label(titleStr);
            titleLabel.AddToClassList("honami-node-label");
            textContainer.Add(titleLabel);

            var subtitleLabel = new Label($"Motion {index}");
            subtitleLabel.AddToClassList("honami-node-subtitle");
            textContainer.Add(subtitleLabel);

            headerRow.Add(textContainer);

            var deleteButton = new Button(() => _onRemove?.Invoke(Index))
            {
                text = "X",
                tooltip = "Remove Motion",
                style =
                {
                    width = 20,
                    height = 20,
                    backgroundColor = new Color(0, 0, 0, 0.2f),
                    color = BlendTreeTheme.MutedText,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0
                }
            };
            
            // Hover effects
            deleteButton.RegisterCallback<MouseEnterEvent>(e => {
                deleteButton.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
                deleteButton.style.color = Color.white;
            });
            deleteButton.RegisterCallback<MouseLeaveEvent>(e => {
                deleteButton.style.backgroundColor = new Color(0, 0, 0, 0.2f);
                deleteButton.style.color = BlendTreeTheme.MutedText;
            });

            headerRow.Add(deleteButton);

            body.Add(headerRow);

            var separator = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = BlendTreeTheme.SubtleLine,
                    marginTop = 2,
                    marginBottom = 10,
                    marginLeft = -8,
                    marginRight = -8
                }
            };
            body.Add(separator);

            var topRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 10 }
            };
            body.Add(topRow);

            _arcContainer = new VisualElement
            {
                style =
                {
                    width = BlendTreeTheme.ArcRadius * 2f + 4f,
                    height = BlendTreeTheme.ArcRadius * 2f + 4f,
                    marginRight = 10,
                    flexShrink = 0,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            _arcContainer.generateVisualContent += OnDrawArc;
            topRow.Add(_arcContainer);

            _arcLabel = new Label("0%")
            {
                style =
                {
                    position = Position.Absolute,
                    fontSize = 9,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    width = BlendTreeTheme.ArcRadius * 2f + 4f,
                    height = BlendTreeTheme.ArcRadius * 2f + 4f,
                    left = 0,
                    top = 0
                }
            };
            _arcContainer.Add(_arcLabel);

            var clipField = new ObjectField { objectType = typeof(AnimationClip), allowSceneObjects = false };
            clipField.AddToClassList("honami-bt-node-field");
            clipField.style.flexGrow = 1;
            clipField.style.flexShrink = 1;
            clipField.style.minWidth = 0;
            clipField.style.overflow = Overflow.Hidden;
            clipField.BindProperty(motionProp.FindPropertyRelative("clip"));
            clipField.RegisterValueChangedCallback(evt =>
            {
                titleLabel.text = evt.newValue != null ? evt.newValue.name : "Unset Motion";
            });
            GuardInput(clipField);
            topRow.Add(clipField);

            var thresholdField = new FloatField();
            thresholdField.AddToClassList("honami-bt-node-field");
            thresholdField.BindProperty(motionProp.FindPropertyRelative("threshold"));
            GuardInput(thresholdField);

            var speedField = new FloatField();
            speedField.AddToClassList("honami-bt-node-field");
            speedField.BindProperty(motionProp.FindPropertyRelative("speed"));
            GuardInput(speedField);

            var mirrorToggle = new Toggle { tooltip = "Mirror this motion" };
            mirrorToggle.BindProperty(motionProp.FindPropertyRelative("mirror"));
            GuardInput(mirrorToggle);

            var valuesRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart }
            };
            valuesRow.Add(FieldColumn("Threshold", thresholdField, flexGrow: 1, minWidth: 48));
            valuesRow.Add(FieldColumn("Speed", speedField, flexGrow: 1, minWidth: 44));
            valuesRow.Add(FieldColumn("Mirror", mirrorToggle, flexGrow: 0, minWidth: 0, marginRight: 0));
            body.Add(valuesRow);
        }



        private void OnDrawArc(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            Rect rect = _arcContainer.contentRect;
            Vector2 center = new(rect.width * 0.5f, rect.height * 0.5f);
            float r = BlendTreeTheme.ArcRadius;
            float thickness = BlendTreeTheme.ArcThickness;

            painter.strokeColor = BlendTreeTheme.ArcTrack;
            painter.lineWidth = thickness;
            painter.BeginPath();
            painter.Arc(center, r, -225f, 45f);
            painter.Stroke();

            if (_weight > 0.001f)
            {
                float endAngle = -225f + _weight * 270f;
                painter.strokeColor = _color;
                painter.lineWidth = thickness;
                painter.BeginPath();
                painter.Arc(center, r, -225f, endAngle);
                painter.Stroke();
            }

            float dotR = thickness * 0.4f;
            painter.fillColor = _weight > 0.01f ? _color : BlendTreeTheme.ArcTrack;
            painter.BeginPath();
            painter.Arc(center, dotR, 0, 360);
            painter.Fill();
        }

        public void UpdateWeight(float weight)
        {
            _weight = Mathf.Clamp01(weight);
            int pct = Mathf.RoundToInt(_weight * 100f);
            if (pct != _lastDisplayedPct)
            {
                _lastDisplayedPct = pct;
                _arcLabel.text = $"{pct}%";
            }
            _arcLabel.style.color = _weight > 0.05f ? Color.white : BlendTreeTheme.MutedText;
            _arcContainer.MarkDirtyRepaint();
        }

        private static VisualElement FieldColumn(string caption, VisualElement field, float flexGrow, float minWidth, float marginRight = 8f)
        {
            var column = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = flexGrow,
                    flexShrink = 1,
                    minWidth = minWidth,
                    marginRight = marginRight
                }
            };

            var cap = FieldCaption(caption);
            cap.style.marginBottom = 3;
            column.Add(cap);

            field.style.marginTop = 0;
            field.style.marginLeft = 0;
            field.style.marginRight = 0;
            column.Add(field);

            if (field is FloatField floatField)
            {
                cap.AddToClassList("unity-base-field__label--with-dragger");
                new FieldMouseDragger<float>(floatField).SetDragZone(cap);
            }

            return column;
        }

        private static Label FieldCaption(string text)
        {
            var label = new Label(text);
            label.AddToClassList("honami-bt-node-caption");
            return label;
        }

        private static void GuardInput(VisualElement element)
        {
            element.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            element.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            element.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
        }
    }
}
#endif
