using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    [CustomEditor(typeof(HonamiMaskSwitcherSubNode))]
    public sealed class HonamiMaskSwitcherSubNodeEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.style.paddingTop = 4;

            SerializedProperty rulesProp = serializedObject.FindProperty("rules");

            HonamiController controller = GetActiveController();

            VisualElement header = HonamiGraphStyles.Row();
            header.Add(HonamiGraphStyles.SubTitle("Mask Switching Rules"));
            header.Add(HonamiGraphStyles.Spacer());

            Button addRuleBtn = HonamiGraphStyles.SmallButton("+ Rule", () =>
            {
                serializedObject.Update();
                rulesProp.InsertArrayElementAtIndex(rulesProp.arraySize);
                serializedObject.ApplyModifiedProperties();
                RebuildRules(root, rulesProp, controller);
            });
            header.Add(addRuleBtn);
            root.Add(header);

            VisualElement rulesContainer = new VisualElement();
            root.Add(rulesContainer);

            RebuildRules(rulesContainer, rulesProp, controller);

            root.Bind(serializedObject);
            return root;
        }

        private void RebuildRules(VisualElement container, SerializedProperty rulesProp, HonamiController controller)
        {
            container.Clear();
            serializedObject.Update();

            if (rulesProp.arraySize == 0)
            {
                container.Add(HonamiGraphStyles.MiniLabel("No rules defined. Add a rule to start switching masks.", new Color(0.5f, 0.5f, 0.5f)));
                return;
            }

            for (int i = 0; i < rulesProp.arraySize; i++)
            {
                int index = i;
                SerializedProperty ruleProp = rulesProp.GetArrayElementAtIndex(i);

                VisualElement ruleBox = HonamiGraphStyles.Box();
                ruleBox.style.marginBottom = 8;

                VisualElement ruleHeader = HonamiGraphStyles.Row();
                ruleHeader.Add(new Label($"Rule {index}") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });

                Button delBtn = HonamiGraphStyles.SmallButton(HonamiEditorSymbols.Remove, () =>
                {
                    serializedObject.Update();
                    rulesProp.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    RebuildRules(container, rulesProp, controller);
                });
                ruleHeader.Add(delBtn);
                ruleBox.Add(ruleHeader);

                // Conditions
                SerializedProperty condsProp = ruleProp.FindPropertyRelative("conditions");
                ruleBox.Add(BuildConditionsList(condsProp, controller));

                // Mask
                SerializedProperty maskProp = ruleProp.FindPropertyRelative("mask");
                PropertyField maskField = new PropertyField(maskProp, "Apply Mask");
                maskField.style.marginTop = 4;
                maskField.BindProperty(maskProp);
                ruleBox.Add(maskField);

                // Mirror
                SerializedProperty mirrorProp = ruleProp.FindPropertyRelative("mirror");
                PropertyField mirrorField = new PropertyField(mirrorProp, "Mirror Animation");
                mirrorField.style.marginTop = 2;
                mirrorField.BindProperty(mirrorProp);
                ruleBox.Add(mirrorField);

                container.Add(ruleBox);
            }
        }

        private VisualElement BuildConditionsList(SerializedProperty condsProp, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.ListBox();
            box.style.marginTop = 4;

            VisualElement hdr = HonamiGraphStyles.Row();
            hdr.Add(HonamiGraphStyles.MiniLabel("Conditions (ALL must be met)", HonamiGraphStyles.SubTitleClr));
            hdr.Add(HonamiGraphStyles.Spacer());

            Button addBtn = HonamiGraphStyles.SmallButton("+", () =>
            {
                condsProp.serializedObject.Update();
                int idx = condsProp.arraySize;
                condsProp.InsertArrayElementAtIndex(idx);

                if (controller != null && controller.parameters.Count > 0)
                {
                    var c = condsProp.GetArrayElementAtIndex(idx);
                    c.FindPropertyRelative("parameter").stringValue = controller.parameters[0].name;
                }

                condsProp.serializedObject.ApplyModifiedProperties();
                RefreshConditions(box, condsProp, controller);
            });
            hdr.Add(addBtn);
            box.Add(hdr);

            VisualElement list = new VisualElement();
            box.Add(list);

            RefreshConditions(list, condsProp, controller);
            return box;
        }

        private void RefreshConditions(VisualElement container, SerializedProperty condsProp, HonamiController controller)
        {
            container.Clear();
            condsProp.serializedObject.Update();

            if (condsProp.arraySize == 0)
            {
                container.Add(HonamiGraphStyles.MiniLabel("No conditions - always true.", new Color(0.5f, 0.5f, 0.5f)));
                return;
            }

            var paramNames = controller != null ? controller.parameters.Select(p => p.name).ToList() : new List<string>();
            var paramTypes = controller != null ? controller.parameters.Select(p => p.type).ToList() : new List<HonamiParameterType>();

            for (int i = 0; i < condsProp.arraySize; i++)
            {
                int index = i;
                SerializedProperty cp = condsProp.GetArrayElementAtIndex(i);
                container.Add(BuildConditionRow(cp, index, condsProp, paramNames, paramTypes, () => RefreshConditions(container, condsProp, controller)));
            }
        }

        private VisualElement BuildConditionRow(SerializedProperty cp, int i, SerializedProperty condsProp, List<string> paramNames, List<HonamiParameterType> paramTypes, System.Action rebuild)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            SerializedProperty paramP = cp.FindPropertyRelative("parameter");
            SerializedProperty modeP = cp.FindPropertyRelative("mode");
            SerializedProperty threshP = cp.FindPropertyRelative("threshold");

            int curP = paramNames.IndexOf(paramP.stringValue);
            DropdownField pDd = new DropdownField(paramNames.Count > 0 ? paramNames : new List<string> { "None" }, Mathf.Max(0, curP));
            pDd.style.flexGrow = 1;
            pDd.style.minWidth = 60;
            pDd.style.height = 18;
            row.Add(pDd);

            HonamiParameterType curType = (curP >= 0 && curP < paramTypes.Count) ? paramTypes[curP] : HonamiParameterType.Float;

            List<HonamiConditionMode> allowedModes = (curType == HonamiParameterType.Bool || curType == HonamiParameterType.Trigger)
                ? new List<HonamiConditionMode> { HonamiConditionMode.If, HonamiConditionMode.IfNot }
                : System.Enum.GetValues(typeof(HonamiConditionMode)).Cast<HonamiConditionMode>().ToList();

            List<string> modeNames = allowedModes.Select(m => m.ToString()).ToList();
            int curModeIdx = allowedModes.IndexOf((HonamiConditionMode)modeP.enumValueIndex);

            DropdownField modeDd = new DropdownField(modeNames, Mathf.Max(0, curModeIdx));
            modeDd.style.width = 65;
            modeDd.style.height = 18;
            row.Add(modeDd);

            bool isNumeric = curType == HonamiParameterType.Float || curType == HonamiParameterType.Int;
            bool needsThresh = isNumeric && modeDd.index > 1;

            PropertyField threshField = new PropertyField(threshP, "");
            threshField.style.width = 40;
            threshField.style.display = needsThresh ? DisplayStyle.Flex : DisplayStyle.None;
            threshField.BindProperty(threshP);
            row.Add(threshField);

            Button del = HonamiGraphStyles.SmallButton(HonamiEditorSymbols.Remove, () =>
            {
                condsProp.serializedObject.Update();
                condsProp.DeleteArrayElementAtIndex(i);
                condsProp.serializedObject.ApplyModifiedProperties();
                rebuild?.Invoke();
            });
            row.Add(del);

            pDd.RegisterValueChangedCallback(evt =>
            {
                int ni = paramNames.IndexOf(evt.newValue);
                if (ni >= 0)
                {
                    cp.serializedObject.Update();
                    paramP.stringValue = paramNames[ni];
                    cp.serializedObject.ApplyModifiedProperties();
                    rebuild?.Invoke();
                }
            });

            modeDd.RegisterValueChangedCallback(evt =>
            {
                int ni = modeNames.IndexOf(evt.newValue);
                if (ni >= 0)
                {
                    cp.serializedObject.Update();
                    modeP.enumValueIndex = (int)allowedModes[ni];
                    cp.serializedObject.ApplyModifiedProperties();
                    threshField.style.display = (isNumeric && ni > 1) ? DisplayStyle.Flex : DisplayStyle.None;
                }
            });

            return row;
        }

        private HonamiController GetActiveController()
        {
            // Try to find the controller from open windows or selection
            var window = EditorWindow.GetWindow<HonamiGraphWindow>(null, false);
            return window != null ? window.Controller as HonamiController : null;
        }
    }
}
