using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiLeftPanel
    {
        public VisualElement Root { get; }

        private readonly HonamiGraphWindow _window;
        private VisualElement _layersContent;
        private VisualElement _paramsContent;
        private readonly List<(int layerIndex, VisualElement row, VisualElement props, Label nameLabel, Label arrow, bool isInherited)> _layerRows = new();
        private int _currentTab = 0;
        private VisualElement _tabBar;
        private readonly List<Button> _tabButtons = new();

        private ScrollView _mainScroll;
        private int _dragSourceLayer = -1;
        private int _dropGapIndex = -1;
        private bool _dragActive;
        private bool _dragVisual;
        private Vector2 _dragStartPos;
        private Vector2 _lastDragPos;
        private VisualElement _dragGhost;
        private VisualElement _dropLine;
        private VisualElement _dragSourceRow;
        private IVisualElementScheduledItem _autoScrollItem;
        private bool _suppressNextLayerClick;

        private static readonly Color InheritedLayerTint = new(0.5f, 0.65f, 0.9f, 0.55f);
        private static readonly Color InheritedLayerText = new(0.78f, 0.88f, 1f);
        private static readonly Color LockedLayerText = new(0.6f, 0.6f, 0.6f);
        private static readonly Color FloatParamColor = new(0.35f, 0.85f, 0.55f);
        private static readonly Color IntParamColor = new(0.40f, 0.60f, 1.00f);
        private static readonly Color BoolParamColor = new(0.90f, 0.75f, 0.20f);
        private static readonly Color TriggerParamColor = new(0.90f, 0.45f, 0.45f);
        private static readonly Color RandomParamColor = new(0.45f, 0.80f, 0.90f);

        private static readonly Dictionary<int, (Runtime.Core.HonamiAnimator animator, bool value)> ForcedBools = new();
        private static bool _updateHooked = false;

        private static void GlobalForceUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                ForcedBools.Clear();
                if (_updateHooked)
                {
                    EditorApplication.update -= GlobalForceUpdate;
                    _updateHooked = false;
                }
                return;
            }

            foreach (var kvp in ForcedBools)
            {
                if (kvp.Value.animator != null)
                {
                    kvp.Value.animator.Parameters.SetBool(kvp.Key, kvp.Value.value);
                }
            }
        }

        public HonamiLeftPanel(HonamiGraphWindow window)
        {
            _window = window;
            Root = BuildShell();
        }

        private VisualElement BuildShell()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;

            _tabBar = new VisualElement();
            _tabBar.style.flexDirection = FlexDirection.Row;
            _tabBar.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f);
            _tabBar.style.height = 22;
            root.Add(_tabBar);

            MakeTabButton("Layers", 0, _tabBar);
            MakeTabButton("Parameters", 1, _tabBar);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            _mainScroll = scroll;

            var pad = new VisualElement();
            pad.style.paddingLeft = pad.style.paddingRight =
            pad.style.paddingTop = pad.style.paddingBottom = 6;
            scroll.Add(pad);

            _layersContent = new VisualElement();
            _paramsContent = new VisualElement();
            _paramsContent.style.display = DisplayStyle.None;

            pad.Add(_layersContent);
            pad.Add(_paramsContent);

            root.RegisterCallback<PointerDownEvent>(_ => _suppressNextLayerClick = false, TrickleDown.TrickleDown);

            return root;
        }

        private Button MakeTabButton(string label, int tabIdx, VisualElement parent)
        {
            var btn = new Button(() => SelectTab(tabIdx)) { text = label };
            btn.style.flexGrow = 1;
            btn.style.height = 22;
            btn.style.borderTopWidth = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth = 0;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 0;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.name = $"tab-btn-{tabIdx}";
            parent.Add(btn);
            _tabButtons.Add(btn);
            UpdateTabButtonStyle(btn, tabIdx == _currentTab);
            return btn;
        }

        private void SelectTab(int idx)
        {
            _currentTab = idx;
            _layersContent.style.display = idx == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _paramsContent.style.display = idx == 1 ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _tabButtons.Count; i++)
                UpdateTabButtonStyle(_tabButtons[i], i == idx);
        }

        private static void UpdateTabButtonStyle(Button btn, bool active)
        {
            if (btn == null) return;
            btn.style.backgroundColor = active
                ? HonamiGraphStyles.Accent
                : new Color(0.18f, 0.18f, 0.19f);
            btn.style.color = active ? Color.white : new Color(0.8f, 0.8f, 0.8f);
        }

        public void Rebuild()
        {
            RebuildLayers();
            RebuildParams();
        }


        public void UpdateLayerHighlight()
        {
            for (int i = 0; i < _layerRows.Count; i++)
            {
                var (layerIndex, row, props, _, arrow, isInherited) = _layerRows[i];
                bool sel = _window.CurrentLayerIndex == layerIndex;
                row.style.backgroundColor = sel
                    ? HonamiGraphStyles.AccentDim
                    : HonamiGraphStyles.ListBoxBg;
                props.style.display = sel ? DisplayStyle.Flex : DisplayStyle.None;

                if (arrow != null)
                    arrow.text = sel ? HonamiEditorSymbols.Collapse : HonamiEditorSymbols.Expand;

                if (sel)
                {
                    row.style.borderLeftWidth = 3;
                    row.style.borderLeftColor = HonamiGraphStyles.Accent;
                }
                else if (isInherited)
                {
                    row.style.borderLeftWidth = 2;
                    row.style.borderLeftColor = InheritedLayerTint;
                }
                else
                {
                    row.style.borderLeftWidth = 1;
                    row.style.borderLeftColor = HonamiGraphStyles.ListBoxBorder;
                }
            }
        }

        private void RebuildLayers()
        {
            _layersContent.Clear();
            _layerRows.Clear();

            var sc = _window.SerializedController;
            if (sc == null || sc.targetObject == null) return;
            sc.Update();

            var hdr = HonamiGraphStyles.Row();
            hdr.style.marginBottom = 6;
            hdr.Add(HonamiGraphStyles.SubTitle("Animation Layers"));
            hdr.Add(HonamiGraphStyles.Spacer());
            var addBtn = HonamiGraphStyles.SmallButton("+");
            addBtn.tooltip = "Add new layer";

            bool isOverride = _window.RuntimeController != null && _window.RuntimeController.IsOverride;

            addBtn.clicked += () =>
            {
                var targetSc = isOverride ? new SerializedObject(_window.RuntimeController) : sc;
                var propName = isOverride ? "additionalLayers" : "layers";
                targetSc.Update();
                var lp = targetSc.FindProperty(propName);
                if (lp == null) return;
                int next = lp.arraySize;
                lp.InsertArrayElementAtIndex(next);
                lp.GetArrayElementAtIndex(next).FindPropertyRelative("name").stringValue = "New Layer";
                lp.GetArrayElementAtIndex(next).FindPropertyRelative("weight").floatValue = 1f;
                lp.GetArrayElementAtIndex(next).FindPropertyRelative("parentLayerIndex").intValue = -1;
                targetSc.ApplyModifiedProperties();
                HonamiNotificationPanel.ShowGlobal("Layer Added", "New animation layer created.", HonamiNotificationType.Success);
                RebuildLayers();
            };
            hdr.Add(addBtn);
            _layersContent.Add(hdr);

            var layersProp = sc.FindProperty("layers");
            int globalIndex = 0;
            if (layersProp != null)
            {
                MigrateSerializedLayerInheritance(sc, layersProp);
                NormalizeLayerInheritanceProperties(sc, layersProp, 0);
                BuildLayerTree(sc, layersProp, 0, isOverride);
                globalIndex = layersProp.arraySize;
            }

            if (isOverride)
            {
                var overrideSc = new SerializedObject(_window.RuntimeController);
                var addLayersProp = overrideSc.FindProperty("additionalLayers");
                if (addLayersProp != null)
                {
                    NormalizeLayerInheritanceProperties(overrideSc, addLayersProp, globalIndex);
                    for (int i = 0; i < addLayersProp.arraySize; i++)
                    {
                        BuildLayerRow(overrideSc, addLayersProp, i, globalIndex++, false, 0, false);
                    }
                }
            }

            UpdateLayerHighlight();
        }

        private static void MigrateSerializedLayerInheritance(SerializedObject sc, SerializedProperty layersProp)
        {
            var versionProp = sc.FindProperty("serializedLayerInheritanceVersion");
            if (versionProp == null || versionProp.intValue > 0) return;

            for (int i = 0; i < layersProp.arraySize; i++)
            {
                var parentProp = layersProp.GetArrayElementAtIndex(i).FindPropertyRelative("parentLayerIndex");
                if (parentProp != null) parentProp.intValue = -1;
            }

            versionProp.intValue = 1;
            sc.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void NormalizeLayerInheritanceProperties(SerializedObject sc, SerializedProperty layersProp, int globalOffset)
        {
            bool changed = false;
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                var parentProp = layersProp.GetArrayElementAtIndex(i).FindPropertyRelative("parentLayerIndex");
                if (parentProp == null) continue;

                int globalIndex = globalOffset + i;
                if (parentProp.intValue >= globalIndex || parentProp.intValue < -1)
                {
                    parentProp.intValue = -1;
                    changed = true;
                }
            }

            if (changed)
                sc.ApplyModifiedPropertiesWithoutUndo();
        }

        private void BuildLayerTree(SerializedObject sc, SerializedProperty layersProp, int globalOffset, bool isLockedBase)
        {
            var childrenByParent = new Dictionary<int, List<int>>();
            var roots = new List<int>();

            for (int i = 0; i < layersProp.arraySize; i++)
            {
                var parentProp = layersProp.GetArrayElementAtIndex(i).FindPropertyRelative("parentLayerIndex");
                int globalIndex = globalOffset + i;
                int parentIndex = parentProp != null ? parentProp.intValue : -1;

                if (parentIndex >= globalOffset && parentIndex < globalIndex)
                {
                    if (!childrenByParent.TryGetValue(parentIndex, out var children))
                    {
                        children = new List<int>();
                        childrenByParent[parentIndex] = children;
                    }
                    children.Add(i);
                }
                else
                {
                    roots.Add(i);
                }
            }

            for (int i = 0; i < roots.Count; i++)
                BuildLayerBranch(sc, layersProp, roots[i], globalOffset, isLockedBase, 0, childrenByParent);
        }

        private void BuildLayerBranch(
            SerializedObject sc,
            SerializedProperty layersProp,
            int arrayIdx,
            int globalOffset,
            bool isLockedBase,
            int depth,
            Dictionary<int, List<int>> childrenByParent)
        {
            int globalIdx = globalOffset + arrayIdx;
            BuildLayerRow(sc, layersProp, arrayIdx, globalIdx, isLockedBase, depth, depth > 0);

            if (!childrenByParent.TryGetValue(globalIdx, out var children)) return;

            for (int i = 0; i < children.Count; i++)
                BuildLayerBranch(sc, layersProp, children[i], globalOffset, isLockedBase, depth + 1, childrenByParent);
        }

        private void BuildLayerRow(SerializedObject sc, SerializedProperty layersProp, int arrayIdx, int globalIdx, bool isLockedBase, int depth, bool isInherited)
        {
            var layerProp = layersProp.GetArrayElementAtIndex(arrayIdx);
            var nameProp = layerProp.FindPropertyRelative("name");
            var weightProp = layerProp.FindPropertyRelative("weight");
            var parentProp = layerProp.FindPropertyRelative("parentLayerIndex");
            var maskRefProp = layerProp.FindPropertyRelative("avatarMask");
            var mirrorRefProp = layerProp.FindPropertyRelative("mirror");
            int parentIndex = parentProp != null ? parentProp.intValue : -1;
            bool showsInheritance = isInherited || parentIndex >= 0;
            bool canDrag = !isLockedBase &&
                           _window.RuntimeController != null &&
                           !_window.RuntimeController.IsOverride;

            var outer = new VisualElement();
            HonamiGraphStyles.ApplyListBox(outer);
            outer.style.marginBottom = 3;
            outer.style.marginLeft = depth * 16;

            var hdr = HonamiGraphStyles.Row();
            hdr.style.marginBottom = 0;

            if (canDrag)
            {
                var handle = new Label(HonamiEditorSymbols.DragHandle);
                handle.style.width = 14;
                handle.style.fontSize = 11;
                handle.style.color = HonamiGraphStyles.GreyText;
                handle.style.unityTextAlign = TextAnchor.MiddleCenter;
                handle.style.flexShrink = 0;
                handle.tooltip = "Drag to reorder layers.";
                hdr.Add(handle);
            }

            var arrow = new Label(HonamiEditorSymbols.Expand);
            arrow.style.width = 12;
            arrow.style.fontSize = 9;
            arrow.style.color = HonamiGraphStyles.GreyText;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.style.flexShrink = 0;
            hdr.Add(arrow);

            var idxLabel = new Label(globalIdx.ToString());
            idxLabel.style.minWidth = 14;
            idxLabel.style.fontSize = 9;
            idxLabel.style.color = HonamiGraphStyles.GreyText;
            idxLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            idxLabel.style.flexShrink = 0;
            idxLabel.tooltip = "Layer order. Higher layers are evaluated on top of lower ones.";
            hdr.Add(idxLabel);

            var nameLabel = new Label(nameProp.stringValue);

            if (isLockedBase) nameLabel.style.color = LockedLayerText;
            else if (showsInheritance) nameLabel.style.color = InheritedLayerText;

            nameLabel.style.flexGrow = 1;
            nameLabel.style.flexShrink = 1;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.paddingLeft = 4;
            nameLabel.style.paddingTop = nameLabel.style.paddingBottom = 4;
            if (!isLockedBase) nameLabel.tooltip = "Double-click to rename";
            hdr.Add(nameLabel);

            Label weightLabel = null;
            if (globalIdx == 0)
            {
                hdr.Add(LayerChip("BASE", HonamiGraphStyles.Accent, "Base layer. Always evaluated first at full weight."));
            }
            else
            {
                weightLabel = new Label(FormatWeight(weightProp.floatValue));
                weightLabel.style.fontSize = 9;
                weightLabel.style.color = HonamiGraphStyles.GreyText;
                weightLabel.style.flexShrink = 0;
                weightLabel.style.marginLeft = 3;
                weightLabel.tooltip = "Layer weight";
            }

            if (showsInheritance)
                hdr.Add(LayerChip("CHILD", InheritedLayerText, $"Inherits states and transitions from '{GetLayerName(parentIndex)}'."));

            if (maskRefProp != null && maskRefProp.objectReferenceValue != null)
                hdr.Add(LayerChip("MASK", HonamiGraphStyles.Orange, $"Avatar mask: {maskRefProp.objectReferenceValue.name}"));

            if (mirrorRefProp != null && mirrorRefProp.boolValue)
                hdr.Add(LayerChip("MIR", HonamiGraphStyles.Green, "Layer output is mirrored."));

            if (isLockedBase)
                hdr.Add(LayerChip("LOCKED", LockedLayerText, "Owned by the base controller. Edit it there."));

            if (weightLabel != null) hdr.Add(weightLabel);

            int capturedGlobal = globalIdx;
            int capturedArray = arrayIdx;
            hdr.style.paddingTop = hdr.style.paddingBottom = 2;
            hdr.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                if (_suppressNextLayerClick)
                {
                    _suppressNextLayerClick = false;
                    return;
                }
                if (_window.CurrentLayerIndex != capturedGlobal) _window.SetLayer(capturedGlobal);
                else UpdateLayerHighlight();
            });
            hdr.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (_window.CurrentLayerIndex != capturedGlobal)
                    hdr.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            });
            hdr.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                hdr.style.backgroundColor = StyleKeyword.Null;
            });

            AttachRowDrag(hdr, outer, nameLabel, sc, layersProp, capturedArray, capturedGlobal, canDrag, isLockedBase);

            var menuBtn = HonamiGraphStyles.SmallButton("...");
            if (isLockedBase)
            {
                menuBtn.SetEnabled(false);
                menuBtn.tooltip = "Cannot edit base layers from an Override Controller.";
            }

            menuBtn.clicked += () =>
            {
                if (isLockedBase) return;
                string cName = nameProp.stringValue;
                var menu = new GenericMenu();

                // For override additional layers, we can't use DuplicateLayer from window easily since it duplicates base layer.
                // We will only allow Delete for additional layers if it's an Override Controller for now.
                if (_window.RuntimeController != null && _window.RuntimeController.IsOverride)
                {
                    menu.AddItem(new GUIContent("Delete"), false, () =>
                    {
                        if (EditorUtility.DisplayDialog("Delete Additional Layer", $"Delete '{cName}'?", "Delete", "Cancel"))
                        {
                            sc.Update();
                            layersProp.DeleteArrayElementAtIndex(capturedArray);
                            sc.ApplyModifiedProperties();
                            if (_window.CurrentLayerIndex >= capturedGlobal) _window.SetLayer(Mathf.Max(0, capturedGlobal - 1));
                            RebuildLayers();
                        }
                    });
                }
                else
                {
                    menu.AddItem(new GUIContent("Rename"), false, () =>
                    {
                        StartLayerRename(nameLabel, sc, layersProp, capturedArray);
                    });
                    menu.AddItem(new GUIContent("Duplicate"), false, () => { _window.DuplicateLayer(capturedGlobal); RebuildLayers(); });
                    menu.AddItem(new GUIContent("Create Child Layer"), false, () => { _window.CreateInheritedLayer(capturedGlobal); RebuildLayers(); });
                    if (isInherited && parentProp != null)
                    {
                        menu.AddItem(new GUIContent("Detach From Parent"), false, () =>
                        {
                            sc.Update();
                            var liveParentProp = layersProp.GetArrayElementAtIndex(capturedArray).FindPropertyRelative("parentLayerIndex");
                            if (liveParentProp != null) liveParentProp.intValue = -1;
                            sc.ApplyModifiedProperties();
                            _window.Controller?.ClearEffectiveStateCache();
                            _window.SetLayer(capturedGlobal);
                            RebuildLayers();
                        });
                    }
                    menu.AddItem(new GUIContent("Copy"), false, () => { _window.CopyLayer(capturedGlobal); });

                    if (HonamiGraphWindow.ClipboardController != null)
                        menu.AddItem(new GUIContent("Paste As New Layer"), false, () => { _window.PasteLayer(); RebuildLayers(); });
                    else
                        menu.AddDisabledItem(new GUIContent("Paste As New Layer"));

                    menu.AddSeparator("");
                    int layerCount = layersProp.arraySize;

                    if (capturedGlobal > 0)
                        menu.AddItem(new GUIContent("Move Up"), false, () => _window.MoveLayer(capturedGlobal, capturedGlobal - 1));
                    else
                        menu.AddDisabledItem(new GUIContent("Move Up"));

                    if (capturedGlobal < layerCount - 1)
                        menu.AddItem(new GUIContent("Move Down"), false, () => _window.MoveLayer(capturedGlobal, capturedGlobal + 1));
                    else
                        menu.AddDisabledItem(new GUIContent("Move Down"));

                    if (capturedGlobal > 0)
                        menu.AddItem(new GUIContent("Set As Base Layer"), false, () =>
                        {
                            if (EditorUtility.DisplayDialog("Set As Base Layer",
                                    $"Make '{cName}' the base layer? It moves to the top of the list and its weight becomes 1.",
                                    "Set As Base", "Cancel"))
                                _window.MoveLayer(capturedGlobal, 0);
                        });
                    else
                        menu.AddDisabledItem(new GUIContent("Set As Base Layer"));

                    if (capturedGlobal > 0)
                    {
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Delete"), false, () => { _window.DeleteLayer(capturedGlobal, cName); RebuildLayers(); });
                    }
                    else
                        menu.AddDisabledItem(new GUIContent("Delete (Base Layer)"));
                }
                menu.ShowAsContext();
            };

            hdr.Add(menuBtn);
            outer.Add(hdr);

            VisualElement weightFill = null;
            if (globalIdx > 0)
            {
                var weightTrack = new VisualElement();
                weightTrack.style.height = 2;
                weightTrack.style.marginTop = 2;
                weightTrack.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
                weightFill = new VisualElement();
                weightFill.style.height = 2;
                weightFill.style.backgroundColor = HonamiGraphStyles.Accent;
                weightFill.style.width = Length.Percent(Mathf.Clamp01(weightProp.floatValue) * 100f);
                weightTrack.Add(weightFill);
                outer.Add(weightTrack);
            }

            var props = new VisualElement();
            props.style.marginTop = 4;
            props.style.display = DisplayStyle.None;

            var nameField = new PropertyField(nameProp, "Name");
            nameField.BindProperty(nameProp);
            nameField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                evt.StopImmediatePropagation();
                nameLabel.text = nameProp.stringValue;
            });
            if (isLockedBase) nameField.SetEnabled(false);
            props.Add(nameField);

            if (globalIdx > 0)
            {
                var weightField = new PropertyField(weightProp, "Weight");
                weightField.BindProperty(weightProp);
                weightField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                {
                    float w = weightProp.floatValue;
                    if (weightLabel != null) weightLabel.text = FormatWeight(w);
                    if (weightFill != null) weightFill.style.width = Length.Percent(Mathf.Clamp01(w) * 100f);
                });
                if (isLockedBase) weightField.SetEnabled(false);
                props.Add(weightField);
            }

            var maskProp = layerProp.FindPropertyRelative("avatarMask");
            var maskField = new PropertyField(maskProp, "Avatar Mask");
            maskField.BindProperty(maskProp);
            if (isLockedBase) maskField.SetEnabled(false);
            props.Add(maskField);

            var mirrorProp = layerProp.FindPropertyRelative("mirror");
            var mirrorField = new PropertyField(mirrorProp, "Mirror Layer");
            mirrorField.BindProperty(mirrorProp);
            if (isLockedBase) mirrorField.SetEnabled(false);
            props.Add(mirrorField);

            if (showsInheritance)
                props.Add(HonamiGraphStyles.MiniLabel($"Inherits from {GetLayerName(parentIndex)}", new Color(0.65f, 0.78f, 1f)));

            outer.Add(props);
            _layersContent.Add(outer);
            _layerRows.Add((globalIdx, outer, props, nameLabel, arrow, showsInheritance));
        }

        private void StartLayerRename(Label nameLabel, SerializedObject sc, SerializedProperty layersProp, int arrayIdx)
        {
            if (nameLabel == null || nameLabel.parent == null) return;

            var nameProp = layersProp.GetArrayElementAtIndex(arrayIdx).FindPropertyRelative("name");
            if (nameProp == null) return;

            var field = new TextField { value = nameProp.stringValue };
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.marginLeft = 2;

            var parent = nameLabel.parent;
            int idx = parent.IndexOf(nameLabel);
            nameLabel.style.display = DisplayStyle.None;
            parent.Insert(idx + 1, field);

            bool done = false;
            void Commit(bool apply)
            {
                if (done) return;
                done = true;
                if (apply && !string.IsNullOrWhiteSpace(field.value))
                {
                    sc.Update();
                    nameProp.stringValue = field.value.Trim();
                    sc.ApplyModifiedProperties();
                }
                _layersContent.schedule.Execute(RebuildLayers);
            }

            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    evt.StopPropagation();
                    Commit(true);
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    evt.StopPropagation();
                    Commit(false);
                }
            });
            field.RegisterCallback<FocusOutEvent>(_ => Commit(true));
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.textSelection.SelectAll();
            });
        }

        private static bool IsInButton(IEventHandler target)
        {
            var ve = target as VisualElement;
            while (ve != null)
            {
                if (ve is Button) return true;
                ve = ve.parent;
            }
            return false;
        }

        private void AttachRowDrag(
            VisualElement hdr,
            VisualElement row,
            Label nameLabel,
            SerializedObject sc,
            SerializedProperty layersProp,
            int arrayIdx,
            int globalIdx,
            bool canDrag,
            bool isLockedBase)
        {
            hdr.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || IsInButton(evt.target)) return;

                if (evt.clickCount == 2)
                {
                    if (!isLockedBase)
                    {
                        evt.StopPropagation();
                        StartLayerRename(nameLabel, sc, layersProp, arrayIdx);
                    }
                    return;
                }

                if (!canDrag) return;
                hdr.CapturePointer(evt.pointerId);
                _dragActive = true;
                _dragVisual = false;
                _dragSourceLayer = globalIdx;
                _dragStartPos = evt.position;
                _lastDragPos = _dragStartPos;
                _dropGapIndex = -1;
            });

            hdr.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_dragActive || _dragSourceLayer != globalIdx || !hdr.HasPointerCapture(evt.pointerId)) return;
                _lastDragPos = evt.position;

                if (!_dragVisual)
                {
                    if ((_lastDragPos - _dragStartPos).sqrMagnitude < 16f) return;
                    BeginDragVisual(row, nameLabel.text);
                }

                UpdateDragVisual(_lastDragPos);
            });

            hdr.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_dragActive || _dragSourceLayer != globalIdx) return;
                if (hdr.HasPointerCapture(evt.pointerId)) hdr.ReleasePointer(evt.pointerId);

                bool dropped = _dragVisual;
                int gap = _dropGapIndex;
                int from = _dragSourceLayer;
                EndDragVisual();
                _dragActive = false;
                _dragSourceLayer = -1;
                _dropGapIndex = -1;

                if (dropped)
                {
                    _suppressNextLayerClick = true;
                    CommitDrop(from, gap);
                }
            });

            hdr.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (!_dragActive || _dragSourceLayer != globalIdx) return;
                EndDragVisual();
                _dragActive = false;
                _dragSourceLayer = -1;
                _dropGapIndex = -1;
            });
        }

        private void BeginDragVisual(VisualElement sourceRow, string layerName)
        {
            _dragVisual = true;
            _dragSourceRow = sourceRow;
            sourceRow.style.opacity = 0.35f;

            _dropLine = new VisualElement();
            _dropLine.pickingMode = PickingMode.Ignore;
            _dropLine.style.position = Position.Absolute;
            _dropLine.style.left = 4;
            _dropLine.style.right = 4;
            _dropLine.style.height = 3;
            _dropLine.style.backgroundColor = HonamiGraphStyles.Accent;
            _dropLine.style.borderTopLeftRadius = _dropLine.style.borderTopRightRadius =
            _dropLine.style.borderBottomLeftRadius = _dropLine.style.borderBottomRightRadius = 2;
            _dropLine.style.display = DisplayStyle.None;
            _layersContent.Add(_dropLine);

            _dragGhost = new Label(layerName);
            _dragGhost.pickingMode = PickingMode.Ignore;
            _dragGhost.style.position = Position.Absolute;
            _dragGhost.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 0.95f);
            _dragGhost.style.color = Color.white;
            _dragGhost.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dragGhost.style.fontSize = 11;
            _dragGhost.style.paddingLeft = _dragGhost.style.paddingRight = 8;
            _dragGhost.style.paddingTop = _dragGhost.style.paddingBottom = 4;
            _dragGhost.style.borderTopLeftRadius = _dragGhost.style.borderTopRightRadius =
            _dragGhost.style.borderBottomLeftRadius = _dragGhost.style.borderBottomRightRadius = 4;
            _dragGhost.style.borderTopWidth = _dragGhost.style.borderBottomWidth =
            _dragGhost.style.borderLeftWidth = _dragGhost.style.borderRightWidth = 1;
            _dragGhost.style.borderTopColor = _dragGhost.style.borderBottomColor =
            _dragGhost.style.borderLeftColor = _dragGhost.style.borderRightColor = HonamiGraphStyles.Accent;
            Root.Add(_dragGhost);

            _autoScrollItem = Root.schedule.Execute(AutoScrollTick).Every(16);
        }

        private void UpdateDragVisual(Vector2 panelPos)
        {
            if (!_dragVisual) return;

            if (_dragGhost != null)
            {
                var local = Root.WorldToLocal(panelPos);
                _dragGhost.style.left = local.x + 10;
                _dragGhost.style.top = local.y - 9;
            }

            int sourceDisplay = -1;
            for (int i = 0; i < _layerRows.Count; i++)
            {
                if (_layerRows[i].layerIndex == _dragSourceLayer)
                {
                    sourceDisplay = i;
                    break;
                }
            }

            int gap = -1;
            float lineY = 0f;
            for (int i = 0; i < _layerRows.Count; i++)
            {
                var rowBound = _layerRows[i].row.worldBound;
                if (panelPos.y < rowBound.center.y)
                {
                    gap = i;
                    lineY = _layerRows[i].row.layout.yMin - 2.5f;
                    break;
                }
            }
            if (gap < 0 && _layerRows.Count > 0)
            {
                gap = _layerRows.Count;
                lineY = _layerRows[_layerRows.Count - 1].row.layout.yMax + 0.5f;
            }

            if (gap == sourceDisplay || gap == sourceDisplay + 1) gap = -1;

            _dropGapIndex = gap;
            if (_dropLine != null)
            {
                if (gap >= 0)
                {
                    _dropLine.style.display = DisplayStyle.Flex;
                    _dropLine.style.top = lineY;
                }
                else
                {
                    _dropLine.style.display = DisplayStyle.None;
                }
            }
        }

        private void EndDragVisual()
        {
            _dragVisual = false;

            if (_dragSourceRow != null)
            {
                _dragSourceRow.style.opacity = 1f;
                _dragSourceRow = null;
            }

            _dragGhost?.RemoveFromHierarchy();
            _dragGhost = null;
            _dropLine?.RemoveFromHierarchy();
            _dropLine = null;
            _autoScrollItem?.Pause();
            _autoScrollItem = null;
        }

        private void AutoScrollTick()
        {
            if (!_dragVisual || _mainScroll == null) return;

            var bound = _mainScroll.worldBound;
            float speed = 0f;
            const float edge = 28f;

            if (_lastDragPos.y < bound.yMin + edge)
                speed = -Mathf.Lerp(2f, 12f, Mathf.Clamp01((bound.yMin + edge - _lastDragPos.y) / edge));
            else if (_lastDragPos.y > bound.yMax - edge)
                speed = Mathf.Lerp(2f, 12f, Mathf.Clamp01((_lastDragPos.y - (bound.yMax - edge)) / edge));

            if (speed != 0f)
            {
                var offset = _mainScroll.scrollOffset;
                _mainScroll.scrollOffset = new Vector2(offset.x, Mathf.Max(0f, offset.y + speed));
                UpdateDragVisual(_lastDragPos);
            }
        }

        private void CommitDrop(int fromGlobal, int gap)
        {
            if (gap < 0 || _window.Controller == null) return;

            int count = _window.Controller.layers.Count;
            int to;
            if (gap <= 0)
            {
                to = 0;
            }
            else
            {
                int above = gap - 1 < _layerRows.Count ? _layerRows[gap - 1].layerIndex : fromGlobal;
                to = fromGlobal > above ? above + 1 : above;
            }

            to = Mathf.Clamp(to, 0, count - 1);
            if (to == fromGlobal) return;
            _window.MoveLayer(fromGlobal, to);
        }

        private string GetLayerName(int globalIdx)
        {
            var layers = _window.RuntimeController != null ? _window.RuntimeController.ActiveLayers : null;
            if (layers != null && globalIdx >= 0 && globalIdx < layers.Count && layers[globalIdx] != null)
                return layers[globalIdx].name;
            return $"Layer {globalIdx}";
        }

        private static string FormatWeight(float weight)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(weight) * 100f) + "%";
        }

        private static Label LayerChip(string text, Color color, string tooltip = null)
        {
            var chip = new Label(text);
            chip.style.fontSize = 8;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.color = color;
            chip.style.backgroundColor = new Color(color.r, color.g, color.b, 0.14f);
            chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius =
            chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 3;
            chip.style.paddingLeft = chip.style.paddingRight = 4;
            chip.style.paddingTop = chip.style.paddingBottom = 1;
            chip.style.marginLeft = 3;
            chip.style.flexShrink = 0;
            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (tooltip != null) chip.tooltip = tooltip;
            return chip;
        }

        private static Color ParamTypeColor(HonamiParameterType type) => type switch
        {
            HonamiParameterType.Float => FloatParamColor,
            HonamiParameterType.Int => IntParamColor,
            HonamiParameterType.Bool => BoolParamColor,
            HonamiParameterType.Trigger => TriggerParamColor,
            _ => RandomParamColor,
        };

        private void RebuildParams()
        {
            _paramsContent.Clear();

            var sc = _window.SerializedController;
            if (sc == null || sc.targetObject == null) return;
            sc.Update();

            var hdr = HonamiGraphStyles.Row();
            hdr.style.marginBottom = 6;
            hdr.Add(HonamiGraphStyles.SubTitle("State Parameters"));
            hdr.Add(HonamiGraphStyles.Spacer());
            var addBtn = HonamiGraphStyles.SmallButton("+");
            addBtn.tooltip = "Add new parameter";

            bool isOverride = _window.RuntimeController != null && _window.RuntimeController.IsOverride;

            addBtn.clicked += () =>
            {
                if (isOverride)
                {
                    var overrideSc = new SerializedObject(_window.RuntimeController);
                    overrideSc.Update();
                    var pProp = overrideSc.FindProperty("additionalParameters");
                    if (pProp == null) return;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Float"), false, () => { AddAdditionalParameter(overrideSc, pProp, HonamiParameterType.Float); RebuildParams(); });
                    menu.AddItem(new GUIContent("Int"), false, () => { AddAdditionalParameter(overrideSc, pProp, HonamiParameterType.Int); RebuildParams(); });
                    menu.AddItem(new GUIContent("Bool"), false, () => { AddAdditionalParameter(overrideSc, pProp, HonamiParameterType.Bool); RebuildParams(); });
                    menu.AddItem(new GUIContent("Trigger"), false, () => { AddAdditionalParameter(overrideSc, pProp, HonamiParameterType.Trigger); RebuildParams(); });
                    menu.AddItem(new GUIContent("Random"), false, () => { AddAdditionalParameter(overrideSc, pProp, HonamiParameterType.Random); RebuildParams(); });
                    menu.ShowAsContext();
                    return;
                }

                var baseMenu = new GenericMenu();
                baseMenu.AddItem(new GUIContent("Float"), false, () => { _window.AddParameter(HonamiParameterType.Float); RebuildParams(); });
                baseMenu.AddItem(new GUIContent("Int"), false, () => { _window.AddParameter(HonamiParameterType.Int); RebuildParams(); });
                baseMenu.AddItem(new GUIContent("Bool"), false, () => { _window.AddParameter(HonamiParameterType.Bool); RebuildParams(); });
                baseMenu.AddItem(new GUIContent("Trigger"), false, () => { _window.AddParameter(HonamiParameterType.Trigger); RebuildParams(); });
                baseMenu.AddItem(new GUIContent("Random"), false, () => { _window.AddParameter(HonamiParameterType.Random); RebuildParams(); });
                baseMenu.ShowAsContext();
            };
            hdr.Add(addBtn);
            _paramsContent.Add(hdr);

            int globalIndex = 0;
            var paramsProp = sc.FindProperty("parameters");
            if (paramsProp != null)
            {
                for (int i = 0; i < paramsProp.arraySize; i++)
                    BuildParamRow(sc, paramsProp, i, globalIndex++, isOverride);
            }

            if (isOverride)
            {
                var overrideSc = new SerializedObject(_window.RuntimeController);
                var addParamsProp = overrideSc.FindProperty("additionalParameters");
                if (addParamsProp != null)
                {
                    for (int i = 0; i < addParamsProp.arraySize; i++)
                    {
                        BuildParamRow(overrideSc, addParamsProp, i, globalIndex++, false);
                    }
                }
            }
        }

        private void AddAdditionalParameter(SerializedObject sc, SerializedProperty listProp, HonamiParameterType type)
        {
            int next = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(next);
            var newItem = listProp.GetArrayElementAtIndex(next);
            newItem.FindPropertyRelative("name").stringValue = "New " + type;
            newItem.FindPropertyRelative("type").enumValueIndex = (int)type;
            sc.ApplyModifiedProperties();
        }

        private void BuildParamRow(SerializedObject sc, SerializedProperty paramsProp, int arrayIdx, int globalIdx, bool isLockedBase)
        {
            var pProp = paramsProp.GetArrayElementAtIndex(arrayIdx);
            var nameProp = pProp.FindPropertyRelative("name");
            var typeProp = pProp.FindPropertyRelative("type");
            var pType = (HonamiParameterType)typeProp.enumValueIndex;

            var row = HonamiGraphStyles.ListBox();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var chipColor = isLockedBase ? LockedLayerText : ParamTypeColor(pType);
            var typeChip = LayerChip(pType.ToString().Substring(0, 1), chipColor,
                isLockedBase ? $"{pType} (base controller)" : pType.ToString());
            typeChip.style.marginLeft = 0;
            typeChip.style.marginRight = 4;
            typeChip.style.minWidth = 16;
            row.Add(typeChip);

            if (!isLockedBase)
            {
                row.AddManipulator(new ContextualMenuManipulator(menuEvent =>
                {
                    void ChangeType(HonamiParameterType newType)
                    {
                        sc.Update();
                        var currentParamsProp = sc.FindProperty(paramsProp.propertyPath);
                        var targetProp = currentParamsProp.GetArrayElementAtIndex(arrayIdx);
                        targetProp.FindPropertyRelative("type").enumValueIndex = (int)newType;
                        sc.ApplyModifiedProperties();
                        RebuildParams();
                    }

                    menuEvent.menu.AppendAction("Change Type/Float", _ => ChangeType(HonamiParameterType.Float), pType == HonamiParameterType.Float ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                    menuEvent.menu.AppendAction("Change Type/Int", _ => ChangeType(HonamiParameterType.Int), pType == HonamiParameterType.Int ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                    menuEvent.menu.AppendAction("Change Type/Bool", _ => ChangeType(HonamiParameterType.Bool), pType == HonamiParameterType.Bool ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                    menuEvent.menu.AppendAction("Change Type/Trigger", _ => ChangeType(HonamiParameterType.Trigger), pType == HonamiParameterType.Trigger ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                    menuEvent.menu.AppendAction("Change Type/Random", _ => ChangeType(HonamiParameterType.Random), pType == HonamiParameterType.Random ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }));
            }

            var nameField = new PropertyField(nameProp) { label = "" };
            nameField.BindProperty(nameProp);
            nameField.style.flexGrow = 1;
            nameField.style.minWidth = 50;
            if (isLockedBase) nameField.SetEnabled(false);
            row.Add(nameField);

            bool isPlaying = EditorApplication.isPlaying && _window.RuntimeAnimator != null;
            int paramHash = Runtime.Core.HonamiAnimator.StringToHash(nameProp.stringValue);
            int capturedArray = arrayIdx;
            switch (pType)
            {
                case HonamiParameterType.Float:
                    if (isPlaying)
                    {
                        var fField = new FloatField { value = _window.RuntimeAnimator.Parameters.GetFloat(paramHash) };
                        fField.style.width = 60;
                        fField.RegisterValueChangedCallback(ev => _window.RuntimeAnimator.Parameters.SetFloat(paramHash, ev.newValue));
                        fField.schedule.Execute(() => { if (_window.RuntimeAnimator != null) fField.SetValueWithoutNotify(_window.RuntimeAnimator.Parameters.GetFloat(paramHash)); }).Every(100);
                        row.Add(fField);
                    }
                    else
                    {
                        var fProp = pProp.FindPropertyRelative("defaultFloat");
                        var fField = new PropertyField(fProp) { label = "" };
                        fField.BindProperty(fProp);
                        fField.style.width = 60;
                        row.Add(fField);
                    }
                    break;
                case HonamiParameterType.Int:
                    if (isPlaying)
                    {
                        var iField = new IntegerField { value = _window.RuntimeAnimator.Parameters.GetInteger(paramHash) };
                        iField.style.width = 60;
                        iField.RegisterValueChangedCallback(ev => _window.RuntimeAnimator.Parameters.SetInteger(paramHash, ev.newValue));
                        iField.schedule.Execute(() => { if (_window.RuntimeAnimator != null) iField.SetValueWithoutNotify(_window.RuntimeAnimator.Parameters.GetInteger(paramHash)); }).Every(100);
                        row.Add(iField);
                    }
                    else
                    {
                        var iProp = pProp.FindPropertyRelative("defaultInt");
                        var iField = new PropertyField(iProp) { label = "" };
                        iField.BindProperty(iProp);
                        iField.style.width = 60;
                        row.Add(iField);
                    }
                    break;
                case HonamiParameterType.Bool:
                    if (isPlaying)
                    {
                        var container = new VisualElement();
                        container.style.flexDirection = FlexDirection.Row;

                        bool isForced = ForcedBools.ContainsKey(paramHash);
                        var bField = new Toggle { value = isForced ? ForcedBools[paramHash].value : _window.RuntimeAnimator.Parameters.GetBool(paramHash) };
                        bField.style.width = 30;

                        var forceToggle = new Toggle { tooltip = "Force value (override scripts)", value = isForced };
                        forceToggle.style.width = 30;

                        bField.RegisterValueChangedCallback(ev =>
                        {
                            _window.RuntimeAnimator.Parameters.SetBool(paramHash, ev.newValue);
                            if (forceToggle.value)
                            {
                                ForcedBools[paramHash] = (_window.RuntimeAnimator, ev.newValue);
                            }
                        });

                        forceToggle.RegisterValueChangedCallback(ev =>
                        {
                            if (ev.newValue)
                            {
                                ForcedBools[paramHash] = (_window.RuntimeAnimator, bField.value);
                                if (!_updateHooked)
                                {
                                    EditorApplication.update += GlobalForceUpdate;
                                    _updateHooked = true;
                                }
                            }
                            else
                            {
                                ForcedBools.Remove(paramHash);
                            }
                        });

                        bField.schedule.Execute(() =>
                        {
                            if (_window.RuntimeAnimator != null && !forceToggle.value)
                                bField.SetValueWithoutNotify(_window.RuntimeAnimator.Parameters.GetBool(paramHash));
                        }).Every(100);

                        container.Add(bField);
                        container.Add(forceToggle);
                        row.Add(container);
                    }
                    else
                    {
                        var bProp = pProp.FindPropertyRelative("defaultBool");
                        var bField = new PropertyField(bProp) { label = "" };
                        bField.BindProperty(bProp);
                        bField.style.width = 60;
                        row.Add(bField);
                    }
                    break;
                case HonamiParameterType.Trigger:
                    if (isPlaying)
                    {
                        var tBtn = new Button(() => _window.RuntimeAnimator.Parameters.SetTrigger(paramHash)) { text = "Fire" };
                        tBtn.style.width = 60;
                        tBtn.style.height = 18;
                        tBtn.style.paddingLeft = 0;
                        tBtn.style.paddingRight = 0;
                        tBtn.schedule.Execute(() =>
                        {
                            if (_window.RuntimeAnimator != null)
                            {
                                bool isSet = _window.RuntimeAnimator.Parameters.IsTriggerSet(paramHash);
                                tBtn.style.backgroundColor = isSet ? new Color(0.2f, 0.6f, 0.2f, 1f) : new StyleColor(StyleKeyword.Null);
                            }
                        }).Every(100);
                        row.Add(tBtn);
                    }
                    else
                    {
                        var dash = new Label("-");
                        dash.style.width = 60;
                        dash.style.unityTextAlign = TextAnchor.MiddleCenter;
                        row.Add(dash);
                    }
                    break;
                case HonamiParameterType.Random:
                    if (isPlaying)
                    {
                        var rField = new FloatField { value = _window.RuntimeAnimator.Parameters.GetFloat(paramHash) };
                        rField.style.width = 60;
                        rField.isReadOnly = true;
                        rField.schedule.Execute(() => { if (_window.RuntimeAnimator != null) rField.SetValueWithoutNotify(_window.RuntimeAnimator.Parameters.GetFloat(paramHash)); }).Every(100);
                        row.Add(rField);
                    }
                    else
                    {
                        var dash = new Label("-");
                        dash.style.width = 60;
                        dash.style.unityTextAlign = TextAnchor.MiddleCenter;
                        row.Add(dash);
                    }
                    break;
            }
            var copyBtn = HonamiGraphStyles.SmallButton("C", () =>
            {
                EditorGUIUtility.systemCopyBuffer = nameProp.stringValue;
                HonamiNotificationPanel.ShowGlobal("Copied", $"Parameter '{nameProp.stringValue}' name copied to clipboard.", HonamiNotificationType.Info, 1.5f);
            });
            copyBtn.tooltip = "Copy parameter name";
            row.Add(copyBtn);

            var del = HonamiGraphStyles.SmallButton(HonamiEditorSymbols.Remove);
            if (isLockedBase)
            {
                del.SetEnabled(false);
                del.tooltip = "Cannot delete base parameters in an Override Controller.";
            }

            del.clicked += () =>
            {
                if (isLockedBase) return;
                sc.Update();
                paramsProp.DeleteArrayElementAtIndex(capturedArray);
                sc.ApplyModifiedProperties();
                RebuildParams();
            };
            row.Add(del);

            _paramsContent.Add(row);
        }
    }
}
