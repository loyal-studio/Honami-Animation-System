using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed partial class HonamiLeftPanel
    {
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
            chip.Radius(3);
            chip.Padding(1, 4);
            chip.style.marginLeft = 3;
            chip.style.flexShrink = 0;
            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (tooltip != null) chip.tooltip = tooltip;
            return chip;
        }
    }
}
