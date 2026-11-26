using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Core;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendSpace
{
    /// <summary>
    /// Main content view for the blend space editor: the plot, the properties side panel and the
    /// per-axis preview transport.
    /// </summary>
    internal sealed class BlendSpacePanelView : VisualElement
    {
        private const string NoneLabel = "- None -";

        private readonly BlendSpaceState _state;
        private readonly Action _rebuild;

        private VisualElement _mainArea;
        private VisualElement _sidePanel;
        private VisualElement _previewHost;
        private VisualElement _propsPanel;
        private VisualElement _resizeHandle;
        private VisualElement _transportBar;
        private HonamiBlendSpaceField _field;
        private HonamiBlendSpacePreview _preview;

        private readonly List<Slider> _axisSliders = new();
        private readonly List<FloatField> _axisValueFields = new();
        private readonly List<Label> _motionWeightLabels = new();
        private bool _resizingProps;
        private int _topologySignature;
        private bool _rebuildQueued;

        public BlendSpacePanelView(BlendSpaceState state, Action rebuild)
        {
            _state = state;
            _rebuild = rebuild;

            name = "honami-blendspace-panel";
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            style.minHeight = 0;
            style.backgroundColor = HonamiEditorTheme.PanelBg;

            Build();
        }

        private void Build()
        {
            Clear();
            _axisSliders.Clear();
            _axisValueFields.Clear();
            _motionWeightLabels.Clear();
            _preview = null;
            _previewHost = null;
            _sidePanel = null;
            _field = null;

            _state.Resolve();

            if (_state.Node == null)
            {
                BuildEmptyState();
                return;
            }

            _state.Node.OnValidateNode();
            _state.CalculateWeights();
            _topologySignature = _state.TopologySignature();

            _mainArea = new VisualElement
            {
                style = { flexGrow = 1, flexDirection = FlexDirection.Row, minHeight = 0, overflow = Overflow.Hidden }
            };
            Add(_mainArea);

            _field = new HonamiBlendSpaceField(_state, OnSampleChanged, OnMotionsChanged);
            _mainArea.Add(_field);

            BuildSidePanel();
            Add(BuildTransportBar());

            var watcher = new VisualElement { style = { display = DisplayStyle.None } };
            watcher.TrackSerializedObjectValue(_state.NodeSO, _ => OnNodeDataChanged());
            Add(watcher);

            _field.Refresh();
        }

        public void Refresh()
        {
            _state.Resolve();

            bool hasContent = _mainArea != null && _field != null;
            bool wantsContent = _state.Node != null;

            if (hasContent != wantsContent || (wantsContent && _topologySignature != _state.TopologySignature()))
            {
                Build();
                return;
            }

            if (!wantsContent)
            {
                BuildEmptyState();
                return;
            }

            _state.CalculateWeights();
            _field?.Refresh();
            _preview?.MarkDirty();
            ApplyPropertiesVisibility();
            SyncAxisSliders();
        }

        private void OnNodeDataChanged()
        {
            _state.Resolve();
            if (_state.Node == null || _topologySignature != _state.TopologySignature())
            {
                QueueRebuild();
                return;
            }

            _state.CalculateWeights();
            _field?.Refresh();
            _preview?.MarkDirty();
        }

        private void QueueRebuild()
        {
            if (_rebuildQueued) return;
            _rebuildQueued = true;
            schedule.Execute(() =>
            {
                _rebuildQueued = false;
                Build();
            }).ExecuteLater(1);
        }

        private void OnSampleChanged()
        {
            _state.CalculateWeights();
            SyncAxisSliders();
            _field?.Refresh();
            _preview?.MarkDirty();
            RefreshMotionWeights();
        }

        private void OnMotionsChanged()
        {
            _state.CalculateWeights();
            _field?.Refresh();
            _preview?.MarkDirty();
            RefreshMotionWeights();
        }

        // ---------------------------------------------------------------- empty state

        private void BuildEmptyState()
        {
            Clear();
            _mainArea = null;
            _field = null;

            var container = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { flexGrow = 1, alignItems = Align.Center, justifyContent = Justify.Center }
            };
            Add(container);

            if (_state.Controller == null)
            {
                var box = HonamiEmptyStatePanel.Box(HonamiEditorIcons.BlendTree, "Honami Animation System", "No Controller Selected");
                box.Add(HonamiEmptyStatePanel.HintLabel("Drop a Honami Controller into the toolbar field above, or double-click a Blend Space node in the Graph window."));
                container.Add(box);
                return;
            }

            var stateBox = HonamiEmptyStatePanel.Box(HonamiEditorIcons.BlendTree, "Honami Animation System", "No Blend Space Selected");
            container.Add(stateBox);

            bool any = false;
            if (_state.Controller.states != null)
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { maxHeight = 260 } };
                scroll.contentContainer.style.alignItems = Align.Center;

                foreach (var candidate in _state.Controller.states)
                {
                    if (candidate == null || candidate.node is not HonamiBlendSpaceNode) continue;
                    any = true;
                    string guid = candidate.guid;
                    scroll.Add(HonamiEmptyStatePanel.CTAButton(candidate.stateName, () =>
                    {
                        _state.StateGuid = guid;
                        _state.ResetForNewTarget();
                        _rebuild();
                    }));
                }

                if (any) stateBox.Add(scroll);
            }

            if (!any)
            {
                stateBox.Add(HonamiEmptyStatePanel.HintLabel("This controller has no Blend Space states. Add one from the Graph window with Complex / Blend Space (2D+), or convert a Blend Tree with Change Type."));
            }
        }

        // ---------------------------------------------------------------- properties

        private void BuildSidePanel()
        {
            bool wantsSide = _state.ShowProperties || _state.ShowPreview;

            _resizeHandle = new VisualElement
            {
                tooltip = "Drag to resize the panel",
                style = { width = 5, flexShrink = 0, backgroundColor = HonamiEditorTheme.Divider }
            };
            _resizeHandle.RegisterCallback<PointerDownEvent>(OnResizeDown);
            _resizeHandle.RegisterCallback<PointerMoveEvent>(OnResizeMove);
            _resizeHandle.RegisterCallback<PointerUpEvent>(OnResizeUp);
            _mainArea.Add(_resizeHandle);

            _sidePanel = new VisualElement { style = { flexDirection = FlexDirection.Column, minHeight = 0 } };
            HonamiEditorLayout.StyleSidePanel(_sidePanel, leftBorder: true);
            _sidePanel.style.width = _state.PropsWidth;
            _sidePanel.style.flexShrink = 0;
            _mainArea.Add(_sidePanel);

            if (_state.ShowPreview)
            {
                _previewHost = new VisualElement
                {
                    style =
                    {
                        flexShrink = 0,
                        paddingLeft = HonamiEditorLayout.InspectorPadding,
                        paddingRight = HonamiEditorLayout.InspectorPadding,
                        paddingTop = HonamiEditorLayout.InspectorPadding
                    }
                };
                _sidePanel.Add(_previewHost);
                BuildPreview();
            }

            _propsPanel = new VisualElement { style = { flexGrow = 1, minHeight = 0 } };
            _sidePanel.Add(_propsPanel);

            RebuildPropertiesContent();
            ApplyPropertiesVisibility();

            if (!wantsSide)
            {
                _sidePanel.style.display = DisplayStyle.None;
                _resizeHandle.style.display = DisplayStyle.None;
            }
        }

        private void BuildPreview()
        {
            if (_previewHost == null) return;
            _previewHost.Clear();
            _preview = null;

            if (!_state.HasClips)
            {
                _previewHost.Add(HonamiGraphStyles.MiniLabel("Assign a clip to a motion to see the skeleton preview."));
                return;
            }

            _preview = new HonamiBlendSpacePreview(
                () => _state.Node,
                () => _state.Sample,
                () => _state.State != null && _state.State.isReversed);
            _previewHost.Add(_preview);
        }

        private void ApplyPropertiesVisibility()
        {
            bool wantsSide = _state.ShowProperties || _state.ShowPreview;

            if (_propsPanel != null)
                _propsPanel.style.display = _state.ShowProperties ? DisplayStyle.Flex : DisplayStyle.None;
            if (_previewHost != null)
                _previewHost.style.display = _state.ShowPreview ? DisplayStyle.Flex : DisplayStyle.None;

            var sideDisplay = wantsSide ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sidePanel != null) _sidePanel.style.display = sideDisplay;
            if (_resizeHandle != null) _resizeHandle.style.display = sideDisplay;
        }

        private void RebuildPropertiesContent()
        {
            if (_propsPanel == null) return;
            _propsPanel.Clear();
            _motionWeightLabels.Clear();

            var scroll = new ScrollView { style = { flexGrow = 1, minHeight = 0 } };
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.paddingLeft = HonamiEditorLayout.InspectorPadding;
            scroll.style.paddingRight = HonamiEditorLayout.InspectorPadding;
            scroll.style.paddingTop = HonamiEditorLayout.InspectorPadding;
            scroll.style.paddingBottom = HonamiEditorLayout.InspectorPadding;
            _propsPanel.Add(scroll);

            var generalBox = HonamiGraphStyles.Box();
            generalBox.Add(HonamiGraphStyles.SubTitle("General Settings"));
            scroll.Add(generalBox);

            AddBoundField(generalBox, _state.NodeSO, "spaceType", "Space Type");

            int fixedAxes = HonamiBlendSpaceSolver.FixedAxisCount(_state.Node.spaceType);
            if (!_state.Node.HasValidAxisCount)
            {
                generalBox.Add(Notice(fixedAxes > 0
                    ? $"{ObjectNames.NicifyVariableName(_state.Node.spaceType.ToString())} needs exactly {fixedAxes} axes, but has {_state.AxisCount}."
                    : "Add at least one axis before this space can blend."));
            }

            BuildAxisSection(generalBox);

            AddBoundField(generalBox, _state.NodeSO, "blendParameterDampTime", "Damp Time");
            AddBoundField(generalBox, _state.NodeSO, "synchronizeSpeed", "Synchronize Speed");

            if (_state.StateSO != null)
            {
                AddBoundField(generalBox, _state.StateSO, "speed", "Overall Speed");
                AddBoundField(generalBox, _state.StateSO, "loop", "Loop");
                AddBoundField(generalBox, _state.StateSO, "holdLastFrame", "Freeze Last Frame");
                AddBoundField(generalBox, _state.StateSO, "isReversed", "Reverse");
                AddBoundField(generalBox, _state.StateSO, "mirror", "Mirror State");
            }

            var motionsFoldout = HonamiGraphStyles.Foldout($"Motions ({_state.MotionCount})");
            scroll.Add(motionsFoldout);
            BuildMotionsList(motionsFoldout);
        }

        private void BuildAxisSection(VisualElement parent)
        {
            parent.Add(SectionHeader($"Axes ({_state.AxisCount})"));

            var list = HonamiGraphStyles.ListBox();

            if (_state.AxisCount == 0)
            {
                list.Add(HonamiGraphStyles.MiniLabel("No axes yet. Each axis is driven by its own float parameter."));
            }

            var parameters = _state.Controller.parameters
                .Where(p => p.type == HonamiParameterType.Float)
                .Select(p => p.name)
                .ToList();
            parameters.Insert(0, NoneLabel);

            for (int axis = 0; axis < _state.AxisCount; axis++)
            {
                int captured = axis;
                var row = HonamiGraphStyles.Row();

                string current = _state.Node.blendParameters[axis];
                var choices = new List<string>(parameters);
                if (!string.IsNullOrEmpty(current) && !choices.Contains(current)) choices.Insert(1, current);

                int index = string.IsNullOrEmpty(current) ? 0 : Mathf.Max(0, choices.IndexOf(current));
                var dropdown = new DropdownField(_state.ShortAxisName(axis), choices, index);
                dropdown.labelElement.style.minWidth = 16;
                dropdown.labelElement.style.width = 16;
                dropdown.labelElement.style.fontSize = 10;
                dropdown.labelElement.style.color = HonamiGraphStyles.GreyText;
                dropdown.style.flexGrow = 1;
                dropdown.style.minWidth = 0;
                dropdown.style.marginRight = 4;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    HonamiEditorController.SetBlendSpaceAxisParameter(_state.Node, captured, evt.newValue == NoneLabel ? string.Empty : evt.newValue);
                    _rebuild();
                });
                row.Add(dropdown);

                var remove = HonamiGraphStyles.SmallButton("×", () =>
                {
                    HonamiEditorController.RemoveBlendSpaceAxis(_state.Node, captured);
                    _rebuild();
                });
                remove.tooltip = "Remove this axis from the space and from every motion";
                remove.style.flexShrink = 0;
                row.Add(remove);

                list.Add(row);
            }

            parent.Add(list);

            var addAxis = HonamiGraphStyles.TallButton("+ Add Axis", () =>
            {
                HonamiEditorController.AddBlendSpaceAxis(_state.Node, string.Empty);
                _rebuild();
            });
            addAxis.style.height = 24;
            parent.Add(addAxis);
        }

        // Column layout shared by the header and every motion row, so the two can never drift apart.
        private const float ColumnGap = 3f;
        private const float WeightColumnWidth = 36f;
        private const float SpeedColumnWidth = 44f;
        private const float MirrorColumnWidth = 18f;
        private const float RemoveColumnWidth = 25f;
        private const float ClipColumnGrow = 2f;
        private const float AxisColumnGrow = 1f;

        private static void ApplyColumn(VisualElement element, float width, float grow, float minWidth, bool last = false)
        {
            if (grow > 0f)
            {
                element.style.flexGrow = grow;
                element.style.flexShrink = 1;

                // flex-grow only shares out space left over above flex-basis, and the default basis is
                // the element's own content width. A header Label and the field under it have very
                // different content widths, so without a zero basis the same grow yields different
                // column widths and the two rows drift apart.
                element.style.flexBasis = 0f;
                element.style.width = StyleKeyword.Auto;
            }
            else
            {
                element.style.flexGrow = 0;
                element.style.flexShrink = 0;
                element.style.flexBasis = StyleKeyword.Auto;
                element.style.width = width;
            }

            element.style.minWidth = minWidth;
            element.style.marginLeft = 0;
            element.style.marginRight = last ? 0f : ColumnGap;
        }

        private VisualElement BuildMotionColumns(Func<int, VisualElement> axisCell, params VisualElement[] fixedCells)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 }
            };

            ApplyColumn(fixedCells[0], WeightColumnWidth, 0f, WeightColumnWidth);
            row.Add(fixedCells[0]);

            ApplyColumn(fixedCells[1], 0f, ClipColumnGrow, 50f);
            row.Add(fixedCells[1]);

            for (int axis = 0; axis < _state.AxisCount; axis++)
            {
                var cell = axisCell(axis);
                ApplyColumn(cell, 0f, AxisColumnGrow, 30f);
                row.Add(cell);
            }

            ApplyColumn(fixedCells[2], SpeedColumnWidth, 0f, SpeedColumnWidth);
            row.Add(fixedCells[2]);

            ApplyColumn(fixedCells[3], MirrorColumnWidth, 0f, MirrorColumnWidth);
            row.Add(fixedCells[3]);

            ApplyColumn(fixedCells[4], RemoveColumnWidth, 0f, RemoveColumnWidth, last: true);
            row.Add(fixedCells[4]);

            return row;
        }

        private void BuildMotionsList(VisualElement parent)
        {
            var header = BuildMotionColumns(
                axis => ColumnLabel(_state.ShortAxisName(axis)),
                ColumnLabel(string.Empty),
                ColumnLabel("Clip"),
                ColumnLabel("Speed"),
                ColumnLabel("M"),
                ColumnLabel(string.Empty));
            header.style.marginTop = 6;
            parent.Add(header);

            for (int i = 0; i < _state.MotionCount; i++)
            {
                parent.Add(BuildMotionRow(i));
            }

            parent.Add(HonamiGraphStyles.TallButton("+ Add Motion", () =>
            {
                HonamiEditorController.AddBlendSpaceMotion(_state.Node, null);
                _rebuild();
            }));

            parent.Add(HonamiGraphStyles.Row());

            var arrange = HonamiGraphStyles.Row();
            var circle = HonamiGraphStyles.TallButton("Arrange In Circle", () =>
            {
                HonamiEditorController.DistributeBlendSpacePositions(_state.Node, false, _state.AxisX, _state.AxisY);
                OnMotionsChanged();
            });
            circle.style.height = 24;
            circle.style.flexGrow = 1;
            circle.style.marginRight = 4;
            arrange.Add(circle);

            var centered = HonamiGraphStyles.TallButton("Center First", () =>
            {
                HonamiEditorController.DistributeBlendSpacePositions(_state.Node, true, _state.AxisX, _state.AxisY);
                OnMotionsChanged();
            });
            centered.style.height = 24;
            centered.style.flexGrow = 1;
            arrange.Add(centered);
            parent.Add(arrange);
        }

        private VisualElement BuildMotionRow(int index)
        {
            var motion = _state.Node.blendMotions[index];
            Color color = HonamiBlendSpaceField.MotionColor(index, _state.MotionCount);
            float weight = index < _state.Weights.Length ? _state.Weights[index] : 0f;

            var weightCell = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, overflow = Overflow.Hidden }
            };
            weightCell.Add(new VisualElement
            {
                style = { width = 3, height = 15, backgroundColor = color, marginRight = 3, flexShrink = 0 }
            });

            var weightLabel = new Label($"{weight * 100f:F0}%")
            {
                style =
                {
                    flexGrow = 1,
                    fontSize = 9,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleRight,
                    color = weight > 0.001f ? color : HonamiEditorTheme.MutedText
                }
            };
            weightCell.Add(weightLabel);
            _motionWeightLabels.Add(weightLabel);

            var clipField = new ObjectField { objectType = typeof(AnimationClip), allowSceneObjects = false, value = motion.clip };
            StripFieldLabel(clipField);
            clipField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_state.Node, "Set Blend Space Clip");
                motion.clip = evt.newValue as AnimationClip;
                EditorUtility.SetDirty(_state.Node);
                _rebuild();
            });

            var speedField = new FloatField { value = motion.speed, tooltip = "Playback speed when Synchronize Speed is off" };
            StripFieldLabel(speedField);
            speedField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_state.Node, "Set Blend Space Speed");
                motion.speed = evt.newValue;
                EditorUtility.SetDirty(_state.Node);
                OnMotionsChanged();
            });

            var mirrorToggle = new Toggle { value = motion.mirror, tooltip = "Mirror this motion" };
            StripFieldLabel(mirrorToggle);
            mirrorToggle.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_state.Node, "Set Blend Space Mirror");
                motion.mirror = evt.newValue;
                EditorUtility.SetDirty(_state.Node);
            });

            var remove = HonamiGraphStyles.SmallButton("×", () =>
            {
                HonamiEditorController.RemoveBlendSpaceMotion(_state.Node, index);
                _rebuild();
            });
            remove.tooltip = "Remove this motion";

            var row = BuildMotionColumns(
                axis =>
                {
                    int capturedAxis = axis;
                    var axisField = new FloatField { value = HonamiBlendSpaceSolver.GetAxis(motion, axis) };
                    StripFieldLabel(axisField);
                    axisField.RegisterValueChangedCallback(evt =>
                    {
                        HonamiEditorController.SetBlendSpaceMotionAxis(_state.Node, index, capturedAxis, evt.newValue);
                        OnMotionsChanged();
                    });
                    return axisField;
                },
                weightCell, clipField, speedField, mirrorToggle, remove);

            row.userData = index;
            row.RegisterCallback<ContextClickEvent>(OnMotionContext);
            return row;
        }

        // A label-less BaseField still reserves its label's min-width, which is what pushed these
        // rows wider than the panel.
        private static void StripFieldLabel(VisualElement field)
        {
            var label = field.Q<Label>(className: "unity-base-field__label");
            if (label != null)
            {
                label.style.display = DisplayStyle.None;
                label.style.minWidth = 0;
                label.style.width = 0;
            }

            var input = field.Q(className: "unity-base-field__input");
            if (input != null)
            {
                input.style.minWidth = 0;
                input.style.flexGrow = 1;
            }
        }

        private void OnMotionContext(ContextClickEvent evt)
        {
            if (evt.currentTarget is not VisualElement element || element.userData is not int index) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate Motion"), false, () =>
            {
                HonamiEditorController.DuplicateBlendSpaceMotion(_state.Node, index);
                _rebuild();
            });
            menu.AddItem(new GUIContent("Remove Motion"), false, () =>
            {
                HonamiEditorController.RemoveBlendSpaceMotion(_state.Node, index);
                _rebuild();
            });
            menu.ShowAsContext();
            evt.StopPropagation();
        }

        private void RefreshMotionWeights()
        {
            for (int i = 0; i < _motionWeightLabels.Count && i < _state.Weights.Length; i++)
            {
                float weight = _state.Weights[i];
                _motionWeightLabels[i].text = $"{weight * 100f:F0}%";
                _motionWeightLabels[i].style.color = weight > 0.001f
                    ? HonamiBlendSpaceField.MotionColor(i, _state.MotionCount)
                    : HonamiEditorTheme.MutedText;
            }
        }

        // ---------------------------------------------------------------- transport

        private VisualElement BuildTransportBar()
        {
            _transportBar = new VisualElement
            {
                style =
                {
                    minHeight = BlendSpaceSettings.TransportBarHeight,
                    flexShrink = 0,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexWrap = Wrap.Wrap,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 3,
                    paddingBottom = 3,
                    backgroundColor = HonamiEditorTheme.ToolbarBg,
                    borderTopWidth = 1,
                    borderTopColor = HonamiEditorTheme.Divider
                }
            };

            _transportBar.Add(new Label("Preview")
            {
                style = { fontSize = 10, color = HonamiEditorTheme.MutedText, marginRight = 8, flexShrink = 0 }
            });

            _axisSliders.Clear();
            _axisValueFields.Clear();

            for (int axis = 0; axis < _state.AxisCount; axis++)
            {
                int captured = axis;
                _state.GetAxisRange(axis, out float min, out float max);

                var group = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexGrow = 1, minWidth = 190, marginRight = 10 }
                };

                var slider = new Slider(_state.AxisName(axis), min, max)
                {
                    value = Mathf.Clamp(_state.Sample[axis], min, max),
                    style = { flexGrow = 1, minWidth = 120 }
                };
                slider.labelElement.style.fontSize = 10;
                slider.labelElement.style.minWidth = 60;
                slider.labelElement.style.color = HonamiGraphStyles.GreyText;
                slider.RegisterValueChangedCallback(evt =>
                {
                    _state.Sample[captured] = evt.newValue;
                    OnSampleChanged();
                });
                group.Add(slider);
                _axisSliders.Add(slider);

                var valueField = new FloatField { value = _state.Sample[axis] };
                valueField.style.width = 52;
                valueField.style.flexShrink = 0;
                valueField.style.marginLeft = 4;
                valueField.RegisterValueChangedCallback(evt =>
                {
                    _state.Sample[captured] = evt.newValue;
                    OnSampleChanged();
                });
                group.Add(valueField);
                _axisValueFields.Add(valueField);

                _transportBar.Add(group);
            }

            if (_state.AxisCount == 0)
            {
                _transportBar.Add(HonamiGraphStyles.MiniLabel("Add an axis to scrub this blend space."));
            }
            else
            {
                _transportBar.Add(HonamiToolbarControls.ToolbarButton("Reset", () =>
                {
                    Array.Clear(_state.Sample, 0, _state.Sample.Length);
                    OnSampleChanged();
                }));
            }

            return _transportBar;
        }

        private void SyncAxisSliders()
        {
            for (int i = 0; i < _axisSliders.Count && i < _state.Sample.Length; i++)
            {
                _axisSliders[i].SetValueWithoutNotify(_state.Sample[i]);
                if (i < _axisValueFields.Count) _axisValueFields[i].SetValueWithoutNotify(_state.Sample[i]);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void AddBoundField(VisualElement parent, SerializedObject so, string propName, string label)
        {
            if (so == null) return;
            var prop = so.FindProperty(propName);
            if (prop == null) return;

            var field = new PropertyField(prop, label);
            field.BindProperty(prop);
            parent.Add(field);
        }

        private static Label SectionHeader(string text) => new(text)
        {
            style =
            {
                fontSize = 11,
                unityFontStyleAndWeight = FontStyle.Bold,
                color = HonamiGraphStyles.GreyText,
                marginTop = 10,
                marginBottom = 3
            }
        };

        private static VisualElement Notice(string text)
        {
            var color = new Color(0.95f, 0.7f, 0.35f);
            var box = new VisualElement();
            box.style.backgroundColor = new Color(color.r, color.g, color.b, 0.10f);
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = color;
            box.Padding(6, 8);
            box.style.marginTop = 4;
            box.style.marginBottom = 4;
            box.Add(HonamiGraphStyles.MiniLabel(text, color));
            return box;
        }

        // The inset matches the padding Unity's field inputs put before their own text, so a header
        // sits over the value it names rather than over the field's border.
        private const float HeaderTextInset = 3f;

        private static Label ColumnLabel(string text) => new(text)
        {
            style =
            {
                fontSize = 9,
                color = new Color(0.5f, 0.5f, 0.5f, 1f),
                unityTextAlign = TextAnchor.MiddleLeft,
                overflow = Overflow.Hidden,
                paddingLeft = HeaderTextInset
            }
        };

        private void OnResizeDown(PointerDownEvent evt)
        {
            _resizingProps = true;
            _resizeHandle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnResizeMove(PointerMoveEvent evt)
        {
            if (!_resizingProps || _sidePanel == null) return;

            float rightEdge = _mainArea.worldBound.xMax;
            float newWidth = rightEdge - evt.position.x;
            newWidth = Mathf.Clamp(newWidth, BlendSpaceSettings.PropsMinWidth, BlendSpaceSettings.PropsMaxWidth);

            _state.PropsWidth = newWidth;
            _sidePanel.style.width = newWidth;
            evt.StopPropagation();
        }

        private void OnResizeUp(PointerUpEvent evt)
        {
            if (!_resizingProps) return;
            _resizingProps = false;
            _resizeHandle.ReleasePointer(evt.pointerId);
            _state.SaveSettings();
            evt.StopPropagation();
        }
    }
}
