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
            int paramHash = HonamiAnimator.StringToHash(nameProp.stringValue);
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
