using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Core;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiStateInspector
    {
        public static VisualElement Build(
            HonamiState state,
            SerializedObject so,
            HonamiController controller,
            int layerIndex,
            HonamiNode selectedNode,
            HonamiGraphView graphView,
            System.Action onRebuildRequired)
        {
            VisualElement root = new VisualElement();
            root.style.paddingLeft = root.style.paddingRight = 10;
            root.style.paddingTop = root.style.paddingBottom = 12;

            var overrideController = graphView?.RuntimeController as HonamiOverrideController;
            HonamiOverrideEntry overrideEntry = null;
            HonamiState overrideParentState = null;
            HonamiState overrideTransient = null;
            bool isOverrideInherited = false;

            if (overrideController != null)
            {
                HonamiOverrideAuthoring.ResolveState(overrideController, state, out overrideEntry, out overrideParentState);

                if (overrideEntry != null && overrideEntry.effectiveState != null)
                {
                    state = overrideEntry.effectiveState;
                    so = new SerializedObject(state);
                    isOverrideInherited = true;
                }
                else if (overrideParentState != null && !overrideController.IsOwnedState(state))
                {
                    overrideTransient = HonamiOverrideAuthoring.CreateTransientCopy(overrideParentState);
                    state = overrideTransient;
                    so = new SerializedObject(state);
                    isOverrideInherited = true;
                }
            }

            VisualElement breadcrumbs = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = -8 } };
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(controller.name, new Color(0.5f, 0.7f, 1f)));
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(" > ", new Color(0.4f, 0.4f, 0.4f)));
            string layerName = controller.layers != null && layerIndex >= 0 && layerIndex < controller.layers.Count
                ? controller.layers[layerIndex].name
                : "Layer";
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(layerName, new Color(0.7f, 0.7f, 0.7f)));
            root.Add(breadcrumbs);

            Label title = HonamiGraphStyles.Title(state.stateName);
            if (selectedNode != null && selectedNode.IsOverridden)
            {
                var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                titleRow.Add(title);
                var overBadge = new Label("OVERRIDDEN")
                {
                    style = {
                    backgroundColor = new Color(0.9f, 0.49f, 0.13f),
                    color = Color.white,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 2, paddingBottom = 2,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4, borderTopLeftRadius = 4, borderTopRightRadius = 4,
                    marginLeft = 8, fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold
                }
                };
                titleRow.Add(overBadge);
                root.Add(titleRow);
            }
            else
            {
                root.Add(title);
            }

            VisualElement propsBox = HonamiGraphStyles.Box();
            root.Add(propsBox);
            propsBox.Add(HonamiGraphStyles.SubTitle("Properties"));

            SerializedProperty nameProp = so.FindProperty("stateName");
            string lastStateName = nameProp.stringValue;
            PropertyField nameField = new PropertyField(nameProp, "State Name");
            nameField.style.marginTop = nameField.style.marginBottom = 3;
            nameField.tooltip = "Unique name of the state within the controller.";
            SetupPropertyContextMenu(nameField, nameProp);
            nameField.BindProperty(nameProp);
            nameField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                evt.StopImmediatePropagation();
                string newName = nameProp.stringValue;
                if (_handlingNameChange || newName == lastStateName) return;
                _handlingNameChange = true;
                lastStateName = newName;
                try
                {
                    title.text = newName;
                    SerializedProperty mNameProp = so.FindProperty("m_Name");
                    if (mNameProp != null && mNameProp.stringValue != newName)
                    {
                        mNameProp.stringValue = newName;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (selectedNode?.State == state) selectedNode.RefreshView(state.isDefaultState);
                }
                finally { _handlingNameChange = false; }
            });
            propsBox.Add(nameField);

            SerializedProperty defaultProp = so.FindProperty("isDefaultState");
            bool lastDefault = defaultProp.boolValue;
            PropertyField defaultField = new PropertyField(defaultProp, "Is Default State");
            defaultField.style.marginTop = defaultField.style.marginBottom = 3;
            defaultField.tooltip = "If true, this state starts automatically when the layer begins.";
            defaultField.BindProperty(defaultProp);
            defaultField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                evt.StopImmediatePropagation();
                if (defaultProp.boolValue == lastDefault) return;
                lastDefault = defaultProp.boolValue;
                if (defaultProp.boolValue)
                {
                    foreach (HonamiState s in controller.states)
                    {
                        if (s != null && s.layerIndex == layerIndex && s != state && s.isDefaultState)
                        {
                            s.isDefaultState = false;
                            EditorUtility.SetDirty(s);
                        }
                    }
                    if (graphView != null)
                    {
                        foreach (var n in graphView.nodes.ToList())
                            if (n is HonamiNode hn && hn != selectedNode) hn.RefreshView(false);
                    }
                }
                selectedNode?.RefreshView(defaultProp.boolValue);
            });
            propsBox.Add(defaultField);

            propsBox.Add(HonamiGraphStyles.Separator());

            // Node Type Selection
            var nodeTypes = TypeCache.GetTypesDerivedFrom<HonamiNodeBase>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .OrderBy(t => t.Name).ToList();

            List<string> typeNames = nodeTypes.Select(t => t.Name.Replace("Honami", "").Replace("Node", "")).ToList();
            int currentTypeIdx = nodeTypes.FindIndex(t => state.node != null && t == state.node.GetType());

            DropdownField typePopup = new DropdownField("Node Type", typeNames, currentTypeIdx);
            typePopup.style.marginTop = typePopup.style.marginBottom = 3;
            typePopup.tooltip = "Click to change the type of the underlying node.";
            typePopup.RegisterValueChangedCallback(evt =>
            {
                int ni = typeNames.IndexOf(evt.newValue);
                if (ni >= 0 && (state.node == null || nodeTypes[ni] != state.node.GetType()))
                {
                    graphView?.ChangeNodeType(selectedNode, nodeTypes[ni]);
                    onRebuildRequired?.Invoke();
                }
            });
            propsBox.Add(typePopup);

            propsBox.Add(HonamiGraphStyles.Separator());

            float dur = GetStateDuration(state);
            VisualElement infoRow = HonamiGraphStyles.ListBox();
            infoRow.style.flexDirection = FlexDirection.Row;
            infoRow.style.marginTop = 2;
            if (dur > 0)
            {
                infoRow.Add(HonamiGraphStyles.MiniLabel($"Duration: {dur:F2}s"));
                if (state.loop) infoRow.Add(HonamiGraphStyles.MiniLabel("  |  Looping"));
            }
            else
            {
                infoRow.Add(HonamiGraphStyles.MiniLabel(state.node != null
                    ? $"Node: {state.node.GetType().Name}"
                    : "No node assigned"));
            }
            propsBox.Add(infoRow);

            bool isPlayback = state.node is HonamiAnimationNode
                           || state.node is HonamiBlendTreeNode
                           || state.node is HonamiRandomAnimationNode
                           || state.node is HonamiSequencerNode;

            if (isPlayback)
            {
                SerializedProperty weightProp = so.FindProperty("weight");
                PropertyField wf = new PropertyField(weightProp, "Blend Weight");
                wf.style.marginTop = wf.style.marginBottom = 3;
                wf.tooltip = "Influence of this state on the output stream (0.0 to 1.0).";
                SetupPropertyContextMenu(wf, weightProp);
                wf.BindProperty(weightProp);
                propsBox.Add(wf);
                propsBox.Add(new VisualElement { style = { height = 4 } });
            }

            SerializedProperty tagsProp = so.FindProperty("tags");
            if (tagsProp != null)
            {
                PropertyField tagsField = new PropertyField(tagsProp, "State Tags");
                tagsField.style.marginTop = tagsField.style.marginBottom = 3;
                tagsField.tooltip = "Tags used for programmatic querying (e.g. animator.HasTag(\"Attacking\"))";
                tagsField.BindProperty(tagsProp);
                propsBox.Add(tagsField);
                propsBox.Add(new VisualElement { style = { height = 4 } });
            }

            SerializedProperty linkedActionProp = so.FindProperty("linkedActionId");
            if (linkedActionProp != null)
            {
                PropertyField linkedActionField = new PropertyField(linkedActionProp, "Linked Action ID");
                linkedActionField.style.marginTop = linkedActionField.style.marginBottom = 3;
                linkedActionField.tooltip = "Action ID this state reacts to when a linked action is broadcast.";
                linkedActionField.BindProperty(linkedActionProp);
                propsBox.Add(linkedActionField);
                propsBox.Add(new VisualElement { style = { height = 4 } });
            }

            SerializedProperty mirrorProp = so.FindProperty("mirror");
            if (mirrorProp != null)
            {
                PropertyField mirrorField = new PropertyField(mirrorProp, "Mirror State");
                mirrorField.style.marginTop = mirrorField.style.marginBottom = 3;
                mirrorField.tooltip = "If true, this specific animation state will be mirrored.";
                mirrorField.BindProperty(mirrorProp);
                propsBox.Add(mirrorField);
                propsBox.Add(new VisualElement { style = { height = 4 } });
            }

            SerializedProperty maskProp = so.FindProperty("avatarMask");
            if (maskProp != null)
            {
                VisualElement maskRow = HonamiGraphStyles.Row();
                maskRow.style.marginTop = maskRow.style.marginBottom = 3;

                PropertyField maskField = new PropertyField(maskProp, "Avatar Mask");
                maskField.tooltip = "Optional mask to limit the animation to specific bones.";
                maskField.style.flexGrow = 1;
                maskField.BindProperty(maskProp);
                maskRow.Add(maskField);

                Button editBtn = new Button(() =>
                {
                    if (maskProp.objectReferenceValue != null)
                        AssetDatabase.OpenAsset(maskProp.objectReferenceValue);
                })
                { text = "✎", tooltip = "Open Mask Editor" };

                editBtn.style.width = 24;
                editBtn.style.height = 20;
                editBtn.style.marginLeft = 4;
                editBtn.style.display = maskProp.objectReferenceValue != null ? DisplayStyle.Flex : DisplayStyle.None;

                maskField.RegisterValueChangeCallback(evt =>
                {
                    editBtn.style.display = maskProp.objectReferenceValue != null ? DisplayStyle.Flex : DisplayStyle.None;
                });

                maskRow.Add(editBtn);
                propsBox.Add(maskRow);
                propsBox.Add(new VisualElement { style = { height = 4 } });
            }

            SerializedObject overrideNodeSO = null;
            VisualElement overrideNodeUI = null;
            if (state.node != null)
            {
                HonamiNodeEditorBase nodeEditor = HonamiNodeEditorRegistry.GetEditor(state.node);
                if (nodeEditor != null)
                {
                    overrideNodeSO = new SerializedObject(state.node);
                    overrideNodeUI = nodeEditor.BuildInspectorUI(state, overrideNodeSO, controller);
                    if (overrideNodeUI != null) root.Add(overrideNodeUI);
                }
            }
            else
            {
                VisualElement warn = HonamiGraphStyles.Box();
                warn.Add(new HelpBox("No node assigned. Use the graph right-click menu to create a new state, or run Migration tool.", HelpBoxMessageType.Warning));
                root.Add(warn);
            }

            VisualElement constrFoldout = HonamiGraphStyles.Foldout("Animation Constraints", false);
            constrFoldout.Add(BuildConstraints(so, so.FindProperty("constraints"), "Settings"));
            root.Add(constrFoldout);

            if (state.node != null)
            {
                HonamiNodeEditorBase nodeEditor = HonamiNodeEditorRegistry.GetEditor(state.node);
                nodeEditor?.AppendActionButtons(root, state, controller);
            }

            VisualElement paramFoldout = HonamiGraphStyles.Foldout("State Parameters", state.parameterAssignments?.Count > 0);
            paramFoldout.Add(BuildParameterAssignments(so, so.FindProperty("parameterAssignments"), controller));
            root.Add(paramFoldout);

            VisualElement subNodesFoldout = HonamiGraphStyles.Foldout("Sub-Nodes", state.subNodes?.Count > 0);
            subNodesFoldout.Add(BuildSubNodes(so, so.FindProperty("subNodes"), controller, selectedNode, graphView, onRebuildRequired));
            root.Add(subNodesFoldout);

            root.Add(new VisualElement { style = { height = 4 } });
            TextField guidField = new TextField("Node GUID") { value = state.guid, isReadOnly = true };
            guidField.style.marginTop = 3;
            root.Add(guidField);

            if (isOverrideInherited && overrideParentState != null)
            {
                var ov = overrideController;
                string parentGuid = overrideParentState.guid;
                var parentSnapshot = overrideParentState;
                System.Action refreshMarkers = null;

                void HandleChange()
                {
                    if (overrideTransient != null)
                    {
                        if (!HonamiOverrideAuthoring.DiffersFromParent(overrideTransient, parentSnapshot))
                        {
                            return;
                        }

                        overrideEntry = HonamiOverrideAuthoring.PromoteTransient(ov, parentGuid, overrideTransient);
                        overrideTransient = null;
                    }

                    if (overrideEntry != null)
                    {
                        HonamiOverrideAuthoring.RefreshStateModified(ov, overrideEntry);
                    }

                    refreshMarkers?.Invoke();
                    selectedNode?.RefreshView(state.isDefaultState);
                }

                refreshMarkers = () =>
                {
                    DecorateOverrideScope(propsBox, false, ov, () => overrideEntry, onRebuildRequired);
                    DecorateOverrideScope(constrFoldout, false, ov, () => overrideEntry, onRebuildRequired);
                    DecorateOverrideScope(paramFoldout, false, ov, () => overrideEntry, onRebuildRequired);
                    DecorateOverrideScope(subNodesFoldout, false, ov, () => overrideEntry, onRebuildRequired);
                    if (overrideNodeUI != null)
                    {
                        DecorateOverrideScope(overrideNodeUI, true, ov, () => overrideEntry, onRebuildRequired);
                    }
                };

                root.TrackSerializedObjectValue(so, _ => HandleChange());
                if (overrideNodeSO != null)
                {
                    root.TrackSerializedObjectValue(overrideNodeSO, _ => HandleChange());
                }

                root.Insert(0, BuildOverrideHeader(ov, () => overrideEntry, graphView, layerIndex, onRebuildRequired));
                refreshMarkers();

                root.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    HonamiOverrideAuthoring.DestroyTransient(overrideTransient);
                    overrideTransient = null;
                });
            }

            return root;
        }

        private static void DecorateOverrideScope(VisualElement scope, bool isNode,
            HonamiOverrideController ov, System.Func<HonamiOverrideEntry> entryGetter, System.Action onRebuildRequired)
        {
            if (scope == null) return;

            scope.Query<PropertyField>().ForEach(pf =>
            {
                string path = pf.bindingPath;
                if (string.IsNullOrEmpty(path)) return;
                string field = HonamiOverrideAuthoring.TopLevelField(path);

                var entry = entryGetter();
                bool modified = entry != null && (isNode ? entry.IsNodeFieldModified(field) : entry.IsStateFieldModified(field));

                pf.style.borderLeftWidth = modified ? 2 : 0;
                pf.style.borderLeftColor = new Color(0.29f, 0.55f, 0.9f);
                pf.style.paddingLeft = modified ? 5 : 0;

                Label label = pf.Q<Label>();
                if (label != null) label.style.unityFontStyleAndWeight = modified ? FontStyle.Bold : FontStyle.Normal;

                if (!(pf.userData is string tag) || tag != "ov-menu")
                {
                    pf.userData = "ov-menu";
                    pf.AddManipulator(new ContextualMenuManipulator(evt =>
                    {
                        var e = entryGetter();
                        bool mod = e != null && (isNode ? e.IsNodeFieldModified(field) : e.IsStateFieldModified(field));
                        if (!mod) return;

                        evt.menu.AppendAction("Revert Field", _ =>
                        {
                            if (isNode) HonamiOverrideAuthoring.RevertNodeField(ov, entryGetter(), field);
                            else HonamiOverrideAuthoring.RevertStateField(ov, entryGetter(), field);
                            onRebuildRequired?.Invoke();
                        });
                        evt.menu.AppendAction("Apply to Parent", _ =>
                        {
                            if (isNode) HonamiOverrideAuthoring.ApplyNodeFieldToParent(ov, entryGetter(), field);
                            else HonamiOverrideAuthoring.ApplyStateFieldToParent(ov, entryGetter(), field);
                            onRebuildRequired?.Invoke();
                        });
                    }));
                }
            });
        }

        private static VisualElement BuildOverrideHeader(HonamiOverrideController ov,
            System.Func<HonamiOverrideEntry> entryGetter, HonamiGraphView graphView, int layerIndex, System.Action onRebuildRequired)
        {
            var entry = entryGetter();
            int count = entry == null
                ? 0
                : (entry.modifiedStatePaths.Count + entry.modifiedNodePaths.Count + (entry.nodeTypeOverridden ? 1 : 0));

            var box = HonamiGraphStyles.Box();
            box.style.marginBottom = 4;
            var row = HonamiGraphStyles.Row();

            var badge = new Label(count > 0 ? $"OVERRIDE · {count} field(s)" : "INHERITED");
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.fontSize = 10;
            badge.style.color = count > 0 ? new Color(0.9f, 0.49f, 0.13f) : new Color(0.6f, 0.6f, 0.6f);
            row.Add(badge);
            row.Add(HonamiGraphStyles.Spacer());

            if (count > 0)
            {
                var revertAll = HonamiGraphStyles.SmallButton("Revert All");
                revertAll.style.width = 80;
                revertAll.clicked += () =>
                {
                    HonamiOverrideAuthoring.RevertAll(ov, entryGetter());
                    graphView?.PopulateView(graphView.RuntimeController, layerIndex);
                    onRebuildRequired?.Invoke();
                };
                row.Add(revertAll);
            }

            box.Add(row);
            return box;
        }

        private static bool _handlingNameChange = false;

        public static float GetStateDuration(HonamiState state)
        {
            if (state?.node == null) return 0f;
            return state.node.GetDuration(state, 0, null, 0f);
        }

        public static VisualElement BuildConstraints(SerializedObject so, SerializedProperty constraintsProp,
            string label = "Animation Constraints")
        {
            VisualElement box = HonamiGraphStyles.Box();
            VisualElement hdr = HonamiGraphStyles.Row();
            hdr.Add(HonamiGraphStyles.SubTitle(label));
            box.Add(hdr);
            box.Add(new VisualElement { style = { height = 4 } });

            box.Add(BuildConstraintRow(so, constraintsProp, "Position", "lockPositionX", "lockPositionY", "lockPositionZ"));
            box.Add(BuildConstraintRow(so, constraintsProp, "Rotation", "lockRotationX", "lockRotationY", "lockRotationZ"));
            box.Add(BuildConstraintRow(so, constraintsProp, "Scale", "lockScaleX", "lockScaleY", "lockScaleZ"));

            return box;
        }

        private static VisualElement BuildConstraintRow(SerializedObject so, SerializedProperty constraintsProp,
            string label, string xProp, string yProp, string zProp)
        {
            VisualElement row = HonamiGraphStyles.Row();
            row.style.marginBottom = 4;

            Label lbl = new Label(label);
            lbl.style.width = 70;
            lbl.style.color = new Color(0.8f, 0.8f, 0.8f);
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.fontSize = 11;
            row.Add(lbl);
            row.Add(HonamiGraphStyles.Spacer());

            row.Add(BuildAxisToggle(so, constraintsProp, xProp, "X", HonamiGraphStyles.AxisRed));
            row.Add(new VisualElement { style = { width = 2 } });
            row.Add(BuildAxisToggle(so, constraintsProp, yProp, "Y", HonamiGraphStyles.AxisGreen));
            row.Add(new VisualElement { style = { width = 2 } });
            row.Add(BuildAxisToggle(so, constraintsProp, zProp, "Z", HonamiGraphStyles.AxisBlue));
            return row;
        }

        private static Button BuildAxisToggle(SerializedObject so, SerializedProperty parent,
            string propName, string axis, Color activeColor)
        {
            SerializedProperty prop = parent.FindPropertyRelative(propName);
            Button btn = new Button();
            btn.text = axis;
            btn.style.width = 32;
            btn.style.height = 18;
            btn.style.fontSize = 10;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.paddingLeft = btn.style.paddingRight = 0;
            btn.style.paddingTop = btn.style.paddingBottom = 0;
            RefreshAxisBtn(btn, prop.boolValue, activeColor);
            btn.clicked += () =>
            {
                so.Update();
                prop.boolValue = !prop.boolValue;
                so.ApplyModifiedProperties();
                RefreshAxisBtn(btn, prop.boolValue, activeColor);
            };
            return btn;
        }

        private static void RefreshAxisBtn(Button btn, bool active, Color activeColor)
        {
            btn.style.backgroundColor = active ? activeColor : new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }

        public static VisualElement BuildParameterAssignments(SerializedObject so,
            SerializedProperty assignmentsProp, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            VisualElement hdr = HonamiGraphStyles.Row();
            hdr.Add(HonamiGraphStyles.SubTitle("State Parameters"));
            hdr.Add(HonamiGraphStyles.Spacer());

            VisualElement listContainer = new VisualElement();
            Button addBtn = HonamiGraphStyles.SmallButton("+");
            hdr.Add(addBtn);
            box.Add(hdr);
            box.Add(listContainer);

            void Rebuild()
            {
                listContainer.Clear();
                so.Update();
                if (assignmentsProp.arraySize == 0)
                {
                    listContainer.Add(HonamiGraphStyles.MiniLabel("No parameters assigned.", new Color(0.5f, 0.5f, 0.5f)));
                    return;
                }
                List<string> paramNames = controller.parameters.Select(p => p.name).ToList();
                for (int i = 0; i < assignmentsProp.arraySize; i++)
                    listContainer.Add(BuildAssignmentRow(so, assignmentsProp, i, controller, paramNames, Rebuild));
            }

            addBtn.clicked += () =>
            {
                so.Update();
                int idx = assignmentsProp.arraySize;
                assignmentsProp.InsertArrayElementAtIndex(idx);
                SerializedProperty na = assignmentsProp.GetArrayElementAtIndex(idx);
                if (controller.parameters.Count > 0)
                {
                    na.FindPropertyRelative("parameterName").stringValue = controller.parameters[0].name;
                    na.FindPropertyRelative("type").enumValueIndex = (int)controller.parameters[0].type;
                }
                so.ApplyModifiedProperties();
                Rebuild();
            };

            Rebuild();
            return box;
        }

        private static VisualElement BuildAssignmentRow(SerializedObject so, SerializedProperty assignmentsProp,
            int i, HonamiController controller, List<string> paramNames, System.Action rebuild)
        {
            VisualElement outer = HonamiGraphStyles.ListBox();
            VisualElement row1 = HonamiGraphStyles.Row();

            SerializedProperty ap = assignmentsProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = ap.FindPropertyRelative("parameterName");
            SerializedProperty typeProp = ap.FindPropertyRelative("type");
            HonamiParameterType pType = (HonamiParameterType)typeProp.enumValueIndex;

            int curIdx = paramNames.Count > 0 ? Mathf.Max(0, paramNames.IndexOf(nameProp.stringValue)) : 0;
            DropdownField popup = new DropdownField("Parameter", paramNames.Count > 0 ? paramNames : new List<string> { "No params" }, curIdx);
            popup.style.flexGrow = 1;
            popup.RegisterValueChangedCallback(evt =>
            {
                int ni = paramNames.IndexOf(evt.newValue);
                if (ni >= 0)
                {
                    so.Update();
                    nameProp.stringValue = paramNames[ni];
                    typeProp.enumValueIndex = (int)controller.parameters[ni].type;
                    so.ApplyModifiedProperties();
                    rebuild();
                }
            });
            row1.Add(popup);

            int captured = i;
            Button del = HonamiGraphStyles.SmallButton(HonamiEditorSymbols.Remove);
            del.clicked += () =>
            {
                so.Update();
                assignmentsProp.DeleteArrayElementAtIndex(captured);
                so.ApplyModifiedProperties();
                rebuild();
            };
            row1.Add(del);
            outer.Add(row1);

            VisualElement row2 = HonamiGraphStyles.Row();
            switch (pType)
            {
                case HonamiParameterType.Float:
                    SerializedProperty fp = ap.FindPropertyRelative("targetFloat");
                    PropertyField ff = new PropertyField(fp, "Set Value"); ff.style.marginTop = ff.style.marginBottom = 3; ff.BindProperty(fp); row2.Add(ff); break;
                case HonamiParameterType.Int:
                    SerializedProperty ip = ap.FindPropertyRelative("targetInt");
                    PropertyField iif = new PropertyField(ip, "Set Value"); iif.style.marginTop = iif.style.marginBottom = 3; iif.BindProperty(ip); row2.Add(iif); break;
                case HonamiParameterType.Bool:
                    SerializedProperty bp = ap.FindPropertyRelative("targetBool");
                    PropertyField bf = new PropertyField(bp, "Set Value"); bf.style.marginTop = bf.style.marginBottom = 3; bf.BindProperty(bp); row2.Add(bf); break;
                case HonamiParameterType.Trigger:
                    SerializedProperty st = ap.FindPropertyRelative("setTrigger");
                    DropdownField td = new DropdownField("Action", new List<string> { "Set", "Reset" }, st.boolValue ? 0 : 1);
                    td.style.marginTop = td.style.marginBottom = 3;
                    td.RegisterValueChangedCallback(ev =>
                    {
                        so.Update(); st.boolValue = ev.newValue == "Set"; so.ApplyModifiedProperties();
                    });
                    row2.Add(td);
                    break;
            }

            SerializedProperty oep = ap.FindPropertyRelative("executeOnExit");
            DropdownField timingDd = new DropdownField("Timing", new List<string> { "On Enter", "On Exit" }, oep.boolValue ? 1 : 0);
            timingDd.style.width = 130;
            timingDd.RegisterValueChangedCallback(ev =>
            {
                so.Update(); oep.boolValue = ev.newValue == "On Exit"; so.ApplyModifiedProperties();
            });
            row2.Add(timingDd);
            outer.Add(row2);
            return outer;
        }

        public static void SetupPropertyContextMenu(VisualElement elt, SerializedProperty prop)
        {
            elt.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Copy Value", _ =>
                {
                    EditorGUIUtility.systemCopyBuffer = GetPropStringValue(prop);
                    HonamiNotificationPanel.ShowGlobal("Copied", "Value copied to clipboard.", HonamiNotificationType.Info, 1.5f);
                });
                evt.menu.AppendAction("Reset to Default", _ =>
                {
                    prop.serializedObject.Update();
                    prop.ResetDefaultValue();
                    prop.serializedObject.ApplyModifiedProperties();
                });
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Copy Property Path", _ => EditorGUIUtility.systemCopyBuffer = prop.propertyPath);
            }));
        }

        private static string GetPropStringValue(SerializedProperty prop) => prop.propertyType switch
        {
            SerializedPropertyType.Float => prop.floatValue.ToString("F3"),
            SerializedPropertyType.Integer => prop.intValue.ToString(),
            SerializedPropertyType.Boolean => prop.boolValue.ToString(),
            SerializedPropertyType.String => prop.stringValue,
            SerializedPropertyType.ObjectReference => prop.objectReferenceValue?.name ?? "None",
            SerializedPropertyType.Enum => prop.enumNames[prop.enumValueIndex],
            _ => "..."
        };

        public static VisualElement BuildSubNodes(SerializedObject so, SerializedProperty subNodesProp, HonamiController controller, HonamiNode node, HonamiGraphView graphView, System.Action onRebuildRequired)
        {
            VisualElement box = HonamiGraphStyles.Box();
            VisualElement hdr = HonamiGraphStyles.Row();
            hdr.Add(HonamiGraphStyles.SubTitle("Sub-Nodes Management"));
            box.Add(hdr);

            VisualElement listContainer = new VisualElement();
            box.Add(listContainer);

            void Rebuild()
            {
                listContainer.Clear();
                if (subNodesProp == null) return;
                so.Update();
                if (subNodesProp.arraySize == 0)
                {
                    listContainer.Add(HonamiGraphStyles.MiniLabel("No sub-nodes attached. Right-click the node in graph to add.", new Color(0.5f, 0.5f, 0.5f)));
                    return;
                }

                for (int i = 0; i < subNodesProp.arraySize; i++)
                {
                    int index = i;
                    SerializedProperty snProp = subNodesProp.GetArrayElementAtIndex(i);
                    HonamiSubNodeBase sn = snProp.objectReferenceValue as HonamiSubNodeBase;

                    VisualElement row = HonamiGraphStyles.ListBox();
                    VisualElement r1 = HonamiGraphStyles.Row();

                    Button editBtn = new Button(() => graphView?.OnSubNodeSelected?.Invoke(node, sn));
                    editBtn.text = sn != null ? sn.DisplayName : "Missing Sub-Node";
                    editBtn.style.flexGrow = 1;
                    editBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                    editBtn.style.backgroundColor = Color.clear;
                    editBtn.style.borderLeftWidth = 0;
                    editBtn.style.borderRightWidth = 0;
                    editBtn.style.borderTopWidth = 0;
                    editBtn.style.borderBottomWidth = 0;
                    editBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                    editBtn.style.color = HonamiGraphStyles.SubTitleClr;
                    r1.Add(editBtn);

                    Button delBtn = HonamiGraphStyles.SmallButton(HonamiEditorSymbols.Remove);
                    delBtn.clicked += () =>
                    {
                        if (graphView?.RuntimeController is HonamiOverrideController ovSub)
                        {
                            if (sn != null && HonamiOverrideAuthoring.FindSubNodeOwner(ovSub, sn, out var subEntry))
                            {
                                HonamiOverrideAuthoring.RemoveSubNodeFromOverride(ovSub, subEntry, sn.OverrideId);
                                graphView.PopulateView(ovSub, graphView.currentLayerIndex);
                                onRebuildRequired?.Invoke();
                                return;
                            }

                            var stateObj = so.targetObject as HonamiState;
                            if (stateObj != null && !ovSub.IsOwnedState(stateObj))
                            {
                                var eff = HonamiOverrideAuthoring.EnsureEffectiveState(ovSub, stateObj);
                                HonamiOverrideAuthoring.ResolveState(ovSub, eff, out var e2, out _);
                                if (e2 != null && eff.subNodes != null && index < eff.subNodes.Count && eff.subNodes[index] != null)
                                {
                                    HonamiOverrideAuthoring.RemoveSubNodeFromOverride(ovSub, e2, eff.subNodes[index].OverrideId);
                                }
                                graphView.PopulateView(ovSub, graphView.currentLayerIndex);
                                onRebuildRequired?.Invoke();
                                return;
                            }
                        }

                        so.Update();
                        if (sn != null) Undo.DestroyObjectImmediate(sn);
                        subNodesProp.DeleteArrayElementAtIndex(index);
                        so.ApplyModifiedProperties();
                        HonamiGraphView.DeferredSave();
                        node.RefreshView(false); // Update badges

                        onRebuildRequired?.Invoke();
                    };
                    r1.Add(delBtn);
                    row.Add(r1);

                    listContainer.Add(row);
                }
            }

            Rebuild();
            return box;
        }

        private static void ResetDefaultValue(this SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float: prop.floatValue = prop.name is "speed" or "weight" ? 1f : 0f; break;
                case SerializedPropertyType.Integer: prop.intValue = 0; break;
                case SerializedPropertyType.Boolean: prop.boolValue = false; break;
                case SerializedPropertyType.ObjectReference: prop.objectReferenceValue = null; break;
                case SerializedPropertyType.String: prop.stringValue = ""; break;
            }
        }
    }
}
