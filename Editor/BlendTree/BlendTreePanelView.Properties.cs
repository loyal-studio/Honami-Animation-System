#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    internal sealed partial class BlendTreePanelView
    {
        private VisualElement _propsPanel;
        private VisualElement _resizeHandle;
        private bool _resizingProps;

        private void BuildPropertiesPanel()
        {
            _resizeHandle = new VisualElement
            {
                style =
                {
                    width = 4,
                    flexShrink = 0,
                    backgroundColor = BlendTreeTheme.Divider
                }
            };
            _resizeHandle.RegisterCallback<PointerDownEvent>(OnResizeDown);
            _resizeHandle.RegisterCallback<PointerMoveEvent>(OnResizeMove);
            _resizeHandle.RegisterCallback<PointerUpEvent>(OnResizeUp);
            _mainArea.Add(_resizeHandle);

            _propsPanel = new VisualElement();
            HonamiEditorLayout.StyleSidePanel(_propsPanel, leftBorder: true);
            _propsPanel.style.width = _state.PropsWidth;
            _propsPanel.style.flexShrink = 0;
            _mainArea.Add(_propsPanel);

            RebuildPropertiesContent();
            ApplyPropertiesVisibility();
        }

        private void ApplyPropertiesVisibility()
        {
            var display = _state.ShowProperties ? DisplayStyle.Flex : DisplayStyle.None;
            if (_propsPanel != null) _propsPanel.style.display = display;
            if (_resizeHandle != null) _resizeHandle.style.display = display;
        }

        private void RebuildPropertiesContent()
        {
            if (_propsPanel == null) return;
            _propsPanel.Clear();

            if (_state.Node == null || _state.NodeSO == null) return;

            var scroll = new ScrollView { style = { flexGrow = 1, minHeight = 0 } };
            scroll.style.paddingLeft = HonamiEditorLayout.InspectorPadding;
            scroll.style.paddingRight = HonamiEditorLayout.InspectorPadding;
            scroll.style.paddingTop = HonamiEditorLayout.InspectorPadding;
            scroll.style.paddingBottom = HonamiEditorLayout.InspectorPadding;
            _propsPanel.Add(scroll);

            var generalBox = HonamiGraphStyles.Box();
            generalBox.Add(HonamiGraphStyles.SubTitle("General Settings"));
            scroll.Add(generalBox);

            AddBoundField(generalBox, _state.NodeSO, "blendType", "Blend Mode");
            if (_state.IsDirectional2D)
            {
                AddParameterDropdown(generalBox, "blendParameter", "X Parameter");
                AddBoundField(generalBox, _state.NodeSO, "blendParameterDampTime", "X Damp Time");
                AddParameterDropdown(generalBox, "blendParameterY", "Y Parameter");
                AddBoundField(generalBox, _state.NodeSO, "blendParameterDampTimeY", "Y Damp Time");
            }
            else
            {
                AddParameterDropdown(generalBox, "blendParameter", "Parameter");
                AddBoundField(generalBox, _state.NodeSO, "blendParameterDampTime", "Damp Time");
            }

            if (_state.StateSO != null)
            {
                AddBoundField(generalBox, _state.StateSO, "speed", "Overall Speed");
                AddBoundField(generalBox, _state.StateSO, "loop", "Loop");
                AddBoundField(generalBox, _state.StateSO, "isReversed", "Reverse");
                AddBoundField(generalBox, _state.StateSO, "mirror", "Mirror State");
            }

            var motionsFoldout = HonamiGraphStyles.Foldout(_state.IsDirectional2D ? "Motions (2D)" : "Motions (1D)");
            scroll.Add(motionsFoldout);

            BuildMotionsList(motionsFoldout);

            if (!_state.IsDirectional2D)
                motionsFoldout.Add(HonamiGraphStyles.TallButton("Distribute Thresholds Evenly", DistributeThresholds));
        }

        private void AddBoundField(VisualElement parent, SerializedObject so, string propName, string label)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;
            var field = new PropertyField(prop, label);
            field.BindProperty(prop);
            parent.Add(field);
        }

        private void AddParameterDropdown(VisualElement parent, string propertyName, string label)
        {
            var prop = _state.NodeSO.FindProperty(propertyName);
            if (prop == null || _state.Controller == null) return;

            var paramNames = _state.Controller.parameters
                .Where(p => p.type == HonamiParameterType.Float)
                .Select(p => p.name)
                .ToList();
            paramNames.Insert(0, "- None -");

            int index = string.IsNullOrEmpty(prop.stringValue) ? 0 : Mathf.Max(0, paramNames.IndexOf(prop.stringValue));
            var dropdown = new DropdownField(label, paramNames, index);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                _state.NodeSO.Update();
                prop.stringValue = evt.newValue == "- None -" ? string.Empty : evt.newValue;
                _state.NodeSO.ApplyModifiedProperties();
                UpdateParamLabel();
                _outputNode?.RefreshInfo(_state);
            });
            parent.Add(dropdown);
        }

        private void BuildMotionsList(VisualElement parent)
        {
            var motionsProp = _state.NodeSO.FindProperty("blendMotions");
            if (motionsProp == null) return;

            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 20,
                    paddingRight = 4,
                    marginTop = 6,
                    marginBottom = 2
                }
            };
            bool directional2D = _state.IsDirectional2D;
            header.Add(ColumnLabel("Clip", flexGrow: 1));
            if (directional2D)
            {
                header.Add(ColumnLabel("X", width: 52));
                header.Add(ColumnLabel("Y", width: 52));
            }
            else
            {
                header.Add(ColumnLabel("Threshold", width: 66));
            }
            header.Add(ColumnLabel("Speed", width: 54));
            header.Add(ColumnLabel("Mirror", width: 42));
            header.Add(new VisualElement { style = { width = 24 } });
            parent.Add(header);

            const float rowHeight = 26f;
            var list = new ListView
            {
                showAddRemoveFooter = false,
                showBoundCollectionSize = false,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = rowHeight,
                selectionType = SelectionType.None,
                showBorder = false,
                style = { marginTop = 0 }
            };

            int rows = Mathf.Max(1, motionsProp.arraySize);
            list.style.height = rows * rowHeight;

            list.bindingPath = "blendMotions";
            list.makeItem = MakeMotionRow;
            list.bindItem = (element, i) => BindMotionRow(element, motionsProp, i);
            list.Bind(_state.NodeSO);

            parent.Add(list);

            var addButton = HonamiGraphStyles.SmallButton("+ Add Motion", () => AddMotion(null));
            addButton.style.width = StyleKeyword.Auto;
            addButton.style.height = 22;
            addButton.style.marginTop = 4;
            parent.Add(addButton);
        }

        private static Label ColumnLabel(string text, float width = 0, float flexGrow = 0)
        {
            var lbl = new Label(text)
            {
                style =
                {
                    fontSize = 10,
                    color = new UnityEngine.Color(0.5f, 0.5f, 0.5f, 1f),
                    unityTextAlign = TextAnchor.MiddleLeft,
                    flexShrink = 0
                }
            };
            if (flexGrow > 0) lbl.style.flexGrow = flexGrow;
            if (width > 0) lbl.style.width = width;
            return lbl;
        }

        private VisualElement MakeMotionRow()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingRight = 4
                }
            };

            var clip = new ObjectField { objectType = typeof(AnimationClip), allowSceneObjects = false, name = "clip" };
            clip.style.flexGrow = 1;
            clip.style.minWidth = 60;
            clip.style.marginRight = 2;
            row.Add(clip);

            if (_state.IsDirectional2D)
            {
                var positionX = new FloatField { name = "positionX" };
                positionX.style.width = 52;
                positionX.style.flexShrink = 0;
                positionX.style.marginRight = 2;
                row.Add(positionX);

                var positionY = new FloatField { name = "positionY" };
                positionY.style.width = 52;
                positionY.style.flexShrink = 0;
                positionY.style.marginRight = 2;
                row.Add(positionY);
            }
            else
            {
                var threshold = new FloatField { name = "threshold" };
                threshold.style.width = 66;
                threshold.style.flexShrink = 0;
                threshold.style.marginRight = 2;
                row.Add(threshold);
            }

            var speed = new FloatField { name = "speed" };
            speed.style.width = 54;
            speed.style.flexShrink = 0;
            speed.style.marginRight = 2;
            row.Add(speed);

            var mirror = new Toggle { name = "mirror", tooltip = "Mirror" };
            mirror.style.width = 42;
            mirror.style.flexShrink = 0;
            row.Add(mirror);

            var deleteBtn = new Button { text = "×", name = "deleteBtn", tooltip = "Remove Motion" };
            deleteBtn.style.width = 20;
            deleteBtn.style.height = 20;
            deleteBtn.style.paddingLeft = 0;
            deleteBtn.style.paddingRight = 0;
            deleteBtn.style.paddingTop = 0;
            deleteBtn.style.paddingBottom = 2;
            deleteBtn.style.marginTop = 0;
            deleteBtn.style.marginBottom = 0;
            deleteBtn.style.marginLeft = 4;
            deleteBtn.style.marginRight = 0;
            deleteBtn.style.backgroundColor = new UnityEngine.Color(0, 0, 0, 0);
            deleteBtn.style.borderTopWidth = 0;
            deleteBtn.style.borderBottomWidth = 0;
            deleteBtn.style.borderLeftWidth = 0;
            deleteBtn.style.borderRightWidth = 0;
            deleteBtn.style.color = new UnityEngine.Color(0.85f, 0.35f, 0.35f, 1f);
            deleteBtn.style.fontSize = 16;
            deleteBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(deleteBtn);

            row.RegisterCallback<ContextClickEvent>(OnMotionRowContext);
            return row;
        }

        private void BindMotionRow(VisualElement element, SerializedProperty motionsProp, int index)
        {
            if (index < 0 || index >= motionsProp.arraySize) return;
            var motionProp = motionsProp.GetArrayElementAtIndex(index);

            element.Q<ObjectField>("clip").BindProperty(motionProp.FindPropertyRelative("clip"));
            if (_state.IsDirectional2D)
            {
                SerializedProperty position = motionProp.FindPropertyRelative("position");
                element.Q<FloatField>("positionX").BindProperty(position.FindPropertyRelative("x"));
                element.Q<FloatField>("positionY").BindProperty(position.FindPropertyRelative("y"));
            }
            else
            {
                element.Q<FloatField>("threshold").BindProperty(motionProp.FindPropertyRelative("threshold"));
            }
            element.Q<FloatField>("speed").BindProperty(motionProp.FindPropertyRelative("speed"));
            element.Q<Toggle>("mirror").BindProperty(motionProp.FindPropertyRelative("mirror"));

            var deleteBtn = element.Q<Button>("deleteBtn");
            deleteBtn.clickable = null;
            deleteBtn.clicked += () => RemoveMotion(index);

            element.userData = index;
        }

        private void OnMotionRowContext(ContextClickEvent evt)
        {
            if (evt.currentTarget is not VisualElement element || element.userData is not int index) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate Motion"), false, () => DuplicateMotion(index));
            menu.AddItem(new GUIContent("Remove Motion"), false, () => RemoveMotion(index));
            menu.ShowAsContext();
            evt.StopPropagation();
        }

        private void OnResizeDown(PointerDownEvent evt)
        {
            _resizingProps = true;
            _resizeHandle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnResizeMove(PointerMoveEvent evt)
        {
            if (!_resizingProps || _propsPanel == null) return;
            float mainWidth = _mainArea.resolvedStyle.width;
            float newWidth = mainWidth - evt.position.x + _mainArea.worldBound.xMin;
            newWidth = Mathf.Clamp(newWidth, BlendTreeTheme.PropsMinWidth, BlendTreeTheme.PropsMaxWidth);
            _state.PropsWidth = newWidth;
            _propsPanel.style.width = newWidth;
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
#endif
