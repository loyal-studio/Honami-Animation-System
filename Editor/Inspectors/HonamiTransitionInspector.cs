using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Core;
using HonamiAnimationSystem.Editor.Preview;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiTransitionInspector
    {
        internal static readonly string[] EaseOptionNames =
        {
            "Linear",
            "In Sine", "Out Sine", "In-Out Sine",
            "In Quad", "Out Quad", "In-Out Quad",
            "In Cubic", "Out Cubic", "In-Out Cubic",
            "In Quart", "Out Quart", "In-Out Quart",
            "In Quint", "Out Quint", "In-Out Quint",
            "In Expo", "Out Expo", "In-Out Expo",
            "In Circ", "Out Circ", "In-Out Circ",
            "In Back", "Out Back", "In-Out Back",
            "In Elastic", "Out Elastic", "In-Out Elastic",
            "In Bounce", "Out Bounce", "In-Out Bounce",
            "Custom Curve"
        };

        public static VisualElement Build(
            HonamiTransition transition,
            int transitionIndex,
            HonamiState owner,
            SerializedObject so,
            HonamiController controller,
            HonamiGraphView graphView)
        {
            var overrideController = graphView?.RuntimeController as HonamiOverrideController;
            HonamiOverrideEntry overrideEntry = null;
            HonamiState overrideParent = null;
            HonamiState overrideTransient = null;
            bool isOverrideInherited = false;

            if (overrideController != null && owner != null)
            {
                HonamiOverrideAuthoring.ResolveState(overrideController, owner, out overrideEntry, out overrideParent);
                if (overrideEntry != null && overrideEntry.effectiveState != null)
                {
                    owner = overrideEntry.effectiveState;
                    so = new SerializedObject(owner);
                    int remapped = FindTransitionIndexById(owner.transitions, transition?.id);
                    if (remapped >= 0)
                    {
                        transitionIndex = remapped;
                        transition = owner.transitions[remapped];
                    }
                    isOverrideInherited = true;
                }
                else if (overrideParent != null && !overrideController.IsOwnedState(owner))
                {
                    overrideTransient = HonamiOverrideAuthoring.CreateTransientForTransitions(overrideController, overrideParent);
                    owner = overrideTransient;
                    so = new SerializedObject(owner);
                    isOverrideInherited = true;
                }
            }

            var transProp = so.FindProperty("transitions");
            if (transProp == null || transitionIndex < 0 || transitionIndex >= transProp.arraySize)
            {
                var err = new VisualElement();
                err.Add(HonamiGraphStyles.MiniLabel("Transition no longer exists."));
                return err;
            }

            var tp = transProp.GetArrayElementAtIndex(transitionIndex);

            HonamiState target = null;
            if (!string.IsNullOrEmpty(transition.targetStateGuid))
            {
                IReadOnlyList<HonamiState> activeStates = graphView != null && graphView.RuntimeController != null
                    ? graphView.RuntimeController.ActiveStates
                    : controller.states;

                for (int i = 0; i < activeStates.Count; i++)
                {
                    var state = activeStates[i];
                    if (state != null && state.guid == transition.targetStateGuid)
                    {
                        target = state;
                        break;
                    }
                }
            }

            var root = new VisualElement();
            root.style.paddingLeft = root.style.paddingRight = 10;
            root.style.paddingTop = root.style.paddingBottom = 12;

            if (isOverrideInherited && overrideParent != null)
            {
                var ovc = overrideController;
                string parentGuid = overrideParent.guid;
                var parentSnapshot = overrideParent;

                root.TrackSerializedObjectValue(so, _ =>
                {
                    if (overrideTransient != null)
                    {
                        if (!HonamiOverrideAuthoring.TransitionsDifferFromParent(ovc, overrideTransient, parentSnapshot)) return;
                        overrideEntry = HonamiOverrideAuthoring.PromoteTransient(ovc, parentGuid, overrideTransient);
                        overrideTransient = null;
                    }

                    if (overrideEntry != null)
                    {
                        var currentParent = HonamiOverrideAuthoring.GetParentState(ovc, parentGuid);
                        if (currentParent != null) HonamiOverrideAuthoring.RefreshTransitionModified(ovc, overrideEntry, currentParent);
                    }
                });

                root.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    HonamiOverrideAuthoring.DestroyTransient(overrideTransient);
                    overrideTransient = null;
                });
            }

            if (overrideController != null && overrideEntry != null && !string.IsNullOrEmpty(transition?.id) &&
                overrideEntry.IsTransitionModified(transition.id))
            {
                string revertId = transition.id;
                var ovHeader = HonamiGraphStyles.Box();
                ovHeader.style.marginBottom = 4;
                var ovRow = HonamiGraphStyles.Row();
                ovRow.Add(new Label("OVERRIDDEN TRANSITION")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, color = new Color(0.9f, 0.49f, 0.13f) }
                });
                ovRow.Add(HonamiGraphStyles.Spacer());
                var revertBtn = HonamiGraphStyles.SmallButton("Revert");
                revertBtn.style.width = 70;
                revertBtn.clicked += () =>
                {
                    HonamiOverrideAuthoring.RevertTransition(overrideController, overrideEntry, revertId);
                    graphView?.PopulateView(graphView.RuntimeController, graphView.currentLayerIndex);
                };
                ovRow.Add(revertBtn);
                ovHeader.Add(ovRow);
                root.Add(ovHeader);
            }

            // ── Breadcrumbs ───────────────────────────────────────────────────
            var breadcrumbs = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = -8 } };
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(owner != null ? owner.stateName : "AnyState", new Color(0.7f, 0.7f, 0.7f)));
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(" > ", new Color(0.4f, 0.4f, 0.4f)));
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(target != null ? target.stateName : "Exit", new Color(0.5f, 0.7f, 1f)));
            root.Add(breadcrumbs);

            var titleRow = HonamiGraphStyles.Row();
            titleRow.style.justifyContent = Justify.Center;
            var title = HonamiGraphStyles.Title(target != null ? $"> {target.stateName}" : "Transition Inspector");
            title.style.flexGrow = 1;
            titleRow.Add(title);

            if (target != null)
            {
                var gotoBtn = HonamiGraphStyles.SmallButton("⊙", () =>
                {
                    var n = graphView.nodes.OfType<HonamiNode>().FirstOrDefault(nd => nd.StateGuid == target.guid);
                    if (n != null) { graphView.ClearSelection(); graphView.AddToSelection(n); graphView.FrameSelection(); }
                });
                gotoBtn.tooltip = "Focus target node";
                titleRow.Add(gotoBtn);
            }
            root.Add(titleRow);

            if (target != null)
            {
                var infoBox = HonamiGraphStyles.ListBox();
                var infoHdr = HonamiGraphStyles.Row();
                infoHdr.Add(new Label($"{(target.node != null ? target.node.GetType().Name : "State")} Info") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
                infoBox.Add(infoHdr);

                float dur = HonamiStateInspector.GetStateDuration(target);
                if (dur > 0)
                {
                    infoBox.Add(HonamiGraphStyles.MiniLabel($"Duration: {dur:F2}s"));
                    if (target.node is HonamiAnimationNode animNode && animNode.clip != null)
                        infoBox.Add(HonamiGraphStyles.MiniLabel($"Clip: {animNode.clip.name}"));
                }
                root.Add(infoBox);
                root.Add(new VisualElement { style = { height = 5 } });
            }

            if (HonamiTransitionPreview.CanPreviewTransition(owner, target))
            {
                var previewBox = HonamiGraphStyles.Box();
                previewBox.Add(HonamiGraphStyles.SubTitle("Transition Preview"));
                previewBox.Add(new HonamiTransitionPreview(owner, target, transition, controller));
                root.Add(previewBox);
                root.Add(new VisualElement { style = { height = 6 } });
            }

            if (owner?.node is HonamiPortalExitNode)
            {
                root.Add(BuildPortalExitUI(transition, owner, target, controller, graphView, so));
                return root;
            }

            var timingBox = HonamiGraphStyles.Box();
            timingBox.Add(HonamiGraphStyles.SubTitle("Timing"));

            bool canExit = owner?.node is HonamiAnimationNode
                        || owner?.node is HonamiBlendTreeNode
                        || owner?.node is HonamiRandomAnimationNode;

            if (canExit)
            {
                var exitProp = tp.FindPropertyRelative("hasExitTime");
                var exitField = new PropertyField(exitProp, "Has Exit Time");
                exitField.style.marginTop = exitField.style.marginBottom = 3;
                exitField.tooltip = "If enabled, the transition will only fire after a specific point in the animation.";
                exitField.BindProperty(exitProp);
                timingBox.Add(exitField);

                var exitTimeContainer = new VisualElement();
                exitTimeContainer.style.paddingLeft = 12;
                exitTimeContainer.style.display = exitProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                var etProp = tp.FindPropertyRelative("exitTime");
                var etf = new PropertyField(etProp, "Exit Time (Normalized)");
                etf.style.marginTop = etf.style.marginBottom = 3;
                etf.tooltip = "Normalized time when the transition should occur. Values above 1 act as a cooldown: 1.5 fires after one and a half state durations.";
                HonamiStateInspector.SetupPropertyContextMenu(etf, etProp);
                etf.BindProperty(etProp);
                exitTimeContainer.Add(etf);
                timingBox.Add(exitTimeContainer);

                exitField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
                {
                    evt.StopImmediatePropagation();
                    exitTimeContainer.style.display = exitProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                });
                timingBox.Add(HonamiGraphStyles.Separator());
            }
            else if (owner?.node is HonamiAnyStateNode)
            {
                timingBox.Add(HonamiGraphStyles.MiniLabel("AnyState transitions ignore Exit Time.", new Color(0.8f, 0.6f, 0.3f)));
            }

            var typeProp = tp.FindPropertyRelative("type");
            var typeField = new PropertyField(typeProp, "Transition Type");
            typeField.style.marginTop = typeField.style.marginBottom = 3;
            typeField.BindProperty(typeProp);
            timingBox.Add(typeField);

            var durProp = tp.FindPropertyRelative("duration");
            var durField = new PropertyField(durProp, "Transition Duration (s)");
            durField.style.marginTop = durField.style.marginBottom = 3;
            durField.tooltip = "Time (in seconds) to fade between the source and target states.";
            HonamiStateInspector.SetupPropertyContextMenu(durField, durProp);
            durField.BindProperty(durProp);
            timingBox.Add(durField);

            var freezeProp = tp.FindPropertyRelative("freezeMode");
            var freezeField = new PropertyField(freezeProp, "Freeze Mode");
            freezeField.style.marginTop = freezeField.style.marginBottom = 3;
            freezeField.tooltip = "Destination: the target holds its first frame during the blend and starts playing when the transition completes — keeps fast attacks crisp. Source: the state being left freezes on its current pose and fades out static.";
            freezeField.BindProperty(freezeProp);
            timingBox.Add(freezeField);

            var smartContainer = new VisualElement();
            smartContainer.style.paddingLeft = 12;
            smartContainer.style.display = typeProp.enumValueIndex == (int)HonamiTransitionType.Smart ? DisplayStyle.Flex : DisplayStyle.None;

            var adaptiveProp = tp.FindPropertyRelative("adaptiveDuration");
            var adaptiveField = new PropertyField(adaptiveProp, "Adaptive Duration");
            adaptiveField.BindProperty(adaptiveProp);
            smartContainer.Add(adaptiveField);

            var scaleProp = tp.FindPropertyRelative("smartScaleFactor");
            var scaleField = new PropertyField(scaleProp, "Smart Scale Factor");
            scaleField.BindProperty(scaleProp);
            smartContainer.Add(scaleField);

            timingBox.Add(smartContainer);

            typeField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                smartContainer.style.display = typeProp.enumValueIndex == (int)HonamiTransitionType.Smart ? DisplayStyle.Flex : DisplayStyle.None;
            });

            if (target != null && target.node is not HonamiExitNode)
            {
                float targetDur = HonamiStateInspector.GetStateDuration(target);
                var dstProp = tp.FindPropertyRelative("destinationStartTime");

                if (targetDur > 0)
                {
                    var sliderRow = HonamiGraphStyles.Row();
                    var slider = new Slider("Start Offset (s)", 0f, targetDur);
                    slider.tooltip = "Start the target animation from this point (s).";
                    slider.BindProperty(dstProp);
                    slider.style.flexGrow = 1;

                    var zeroBtn = new Button(() =>
                    {
                        so.Update(); dstProp.floatValue = 0f; so.ApplyModifiedProperties();
                        slider.SetValueWithoutNotify(0f);
                    })
                    { text = "0" };
                    zeroBtn.style.width = 20;

                    sliderRow.Add(slider);
                    sliderRow.Add(zeroBtn);
                    timingBox.Add(sliderRow);

                    float pct = (dstProp.floatValue / targetDur) * 100f;
                    var pctLbl = HonamiGraphStyles.MiniLabel($"{pct:F1}% of target animation playing");
                    timingBox.Add(pctLbl);
                    slider.RegisterValueChangedCallback(ev =>
                        pctLbl.text = $"{(ev.newValue / targetDur * 100f):F1}% of target animation playing");
                }
                else
                {
                    var dstField = new PropertyField(dstProp, "Start Time Offset (s)");
                    dstField.style.marginTop = dstField.style.marginBottom = 3;
                    dstField.tooltip = "Offset when the destination animation starts.";
                    HonamiStateInspector.SetupPropertyContextMenu(dstField, dstProp);
                    dstField.BindProperty(dstProp);
                    timingBox.Add(dstField);
                }
            }
            root.Add(timingBox);

            root.Add(new VisualElement { style = { height = 6 } });

            var curveBox = HonamiGraphStyles.Box();
            curveBox.Add(HonamiGraphStyles.SubTitle("Blending & Interruption"));

            var useCurveProp = tp.FindPropertyRelative("useCurve");
            var easeProp = tp.FindPropertyRelative("ease");

            var easeOptions = new List<string>(EaseOptionNames);
            int easeIndex = useCurveProp.boolValue
                ? easeOptions.Count - 1
                : Mathf.Clamp(easeProp.enumValueIndex, 0, easeOptions.Count - 2);

            var easeField = new DropdownField("Easing", easeOptions, easeIndex);
            easeField.tooltip = "Shapes the blend weight over the transition. Presets need no setup; Custom Curve exposes a hand-editable curve.";
            easeField.style.marginTop = easeField.style.marginBottom = 3;
            curveBox.Add(easeField);

            var curveContainer = new VisualElement();
            curveContainer.style.paddingLeft = 12;
            curveContainer.style.display = useCurveProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            var curveProp = tp.FindPropertyRelative("curve");
            var curveField = new PropertyField(curveProp, "Curve"); curveField.BindProperty(curveProp);
            curveField.style.marginTop = curveField.style.marginBottom = 3;
            curveContainer.Add(curveField);
            curveBox.Add(curveContainer);

            easeField.RegisterValueChangedCallback(ev =>
            {
                int ni = easeOptions.IndexOf(ev.newValue);
                if (ni < 0) return;
                so.Update();
                bool isCustom = ni == easeOptions.Count - 1;
                useCurveProp.boolValue = isCustom;
                if (!isCustom) easeProp.enumValueIndex = ni;
                so.ApplyModifiedProperties();
                curveContainer.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
            });

            if (target != null && target.node is not HonamiExitNode)
            {
                var selfProp = tp.FindPropertyRelative("canTransitionToSelf");
                var selfField = new PropertyField(selfProp, "Allow Self Transition"); selfField.BindProperty(selfProp);
                selfField.style.marginTop = selfField.style.marginBottom = 3;
                curveBox.Add(selfField);
            }

            {
                var interruptProp = tp.FindPropertyRelative("canInterruptTransition");
                var interruptField = new PropertyField(interruptProp, "Can Interrupt Active Transition");
                interruptField.style.marginTop = interruptField.style.marginBottom = 3;
                interruptField.BindProperty(interruptProp);
                curveBox.Add(interruptField);
            }

            var victimContainer = new VisualElement();
            victimContainer.style.paddingLeft = 12;
            victimContainer.style.display = typeProp.enumValueIndex == (int)HonamiTransitionType.Victim ? DisplayStyle.Flex : DisplayStyle.None;
            victimContainer.Add(HonamiGraphStyles.MiniLabel("Victim Settings (Advanced Blending)", new Color(0.8f, 0.4f, 0.4f)));

            var vModeProp = tp.FindPropertyRelative("victimMode");
            var vNames = new List<string> { "None" };
            vNames.Add($"{(owner != null ? owner.stateName : "AnyState")} (Source)");
            vNames.Add($"{(target != null ? target.stateName : "Exit")} (Destination)");

            var vModeField = new DropdownField("Victim State", vNames, vModeProp.enumValueIndex);
            vModeField.RegisterValueChangedCallback(ev =>
            {
                so.Update();
                vModeProp.enumValueIndex = vNames.IndexOf(ev.newValue);
                so.ApplyModifiedProperties();
            });
            victimContainer.Add(vModeField);

            var vSpeedProp = tp.FindPropertyRelative("sacrificeSpeedMultiplier");
            var vSpeedField = new PropertyField(vSpeedProp, "Sacrifice Speed Mult");
            vSpeedField.BindProperty(vSpeedProp);
            victimContainer.Add(vSpeedField);

            var vCustomProp = tp.FindPropertyRelative("useCustomVictimCurve");
            var vCustomField = new PropertyField(vCustomProp, "Use Custom Weight Curve");
            vCustomField.BindProperty(vCustomProp);
            victimContainer.Add(vCustomField);

            var vCurveProp = tp.FindPropertyRelative("victimWeightCurve");
            var vCurveField = new PropertyField(vCurveProp, "Victim Weight Curve");
            vCurveField.BindProperty(vCurveProp);
            vCurveField.style.display = vCustomProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            victimContainer.Add(vCurveField);

            vCustomField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                vCurveField.style.display = vCustomProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            var vAccelProp = tp.FindPropertyRelative("acceleratedWeightDrop");
            var vAccelField = new PropertyField(vAccelProp, "Accelerated Weight Drop (Quadratic)");
            vAccelField.BindProperty(vAccelProp);
            vAccelField.style.display = vCustomProp.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
            victimContainer.Add(vAccelField);

            vCustomField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                vAccelField.style.display = vCustomProp.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
            }, TrickleDown.TrickleDown);

            var priorityProp = tp.FindPropertyRelative("priority");
            var priorityField = new PropertyField(priorityProp, "Interruption Priority");
            priorityField.BindProperty(priorityProp);
            victimContainer.Add(priorityField);

            var sacrificeProp = tp.FindPropertyRelative("sacrificeExisting");
            var sacrificeField = new PropertyField(sacrificeProp, "Kill Active Transitions");
            sacrificeField.BindProperty(sacrificeProp);
            victimContainer.Add(sacrificeField);

            curveBox.Add(victimContainer);

            typeField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                victimContainer.style.display = typeProp.enumValueIndex == (int)HonamiTransitionType.Victim ? DisplayStyle.Flex : DisplayStyle.None;
                smartContainer.style.display = typeProp.enumValueIndex == (int)HonamiTransitionType.Smart ? DisplayStyle.Flex : DisplayStyle.None;
            });
            root.Add(curveBox);

            root.Add(new VisualElement { style = { height = 4 } });

            var condFoldout = HonamiGraphStyles.Foldout("Conditions", transition.conditions?.Count > 0);
            condFoldout.Add(BuildConditions(so, tp.FindPropertyRelative("conditions"), controller));
            root.Add(condFoldout);

            var assignFoldout = HonamiGraphStyles.Foldout("Parameter Assignments", transition.parameterAssignments?.Count > 0);
            assignFoldout.Add(HonamiStateInspector.BuildParameterAssignments(so, tp.FindPropertyRelative("parameterAssignments"), controller));
            root.Add(assignFoldout);

            return root;
        }

        private static int FindTransitionIndexById(System.Collections.Generic.List<HonamiTransition> transitions, string id)
        {
            if (transitions == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < transitions.Count; i++)
            {
                if (transitions[i] != null && transitions[i].id == id) return i;
            }
            return -1;
        }

        private static VisualElement BuildPortalExitUI(
            HonamiTransition transition,
            HonamiState owner,
            HonamiState target,
            HonamiController controller,
            HonamiGraphView graphView,
            SerializedObject so)
        {
            var box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Portal Output Wire"));
            box.Add(new HelpBox(
                "This connection only determines the target of the portal. It does not store transition parameters.\n\n" +
                "Settings like Duration or Conditions should be set on the 'Portal Entrance' wire instead.",
                HelpBoxMessageType.Info));

            string portalName = (owner.node as HonamiPortalExitNode)?.portalName ?? "";

            var sources = controller.states.Where(s => s?.transitions?.Any(t =>
            {
                var ts = controller.states.Find(st => st?.guid == t.targetStateGuid);
                return ts?.node is HonamiPortalEntranceNode pe && pe.portalName == portalName;
            }) == true).ToList();

            if (sources.Count > 0)
            {
                var filterNames = new List<string> { "Any State (No Filter)" };
                filterNames.AddRange(sources.Select(s => s.stateName));
                int curFilterIdx = 0;
                for (int i = 0; i < sources.Count; i++)
                    if (transition.portalSourceFilterGuid == sources[i].guid) { curFilterIdx = i + 1; break; }

                var filterDd = new DropdownField("Source Filter", filterNames, curFilterIdx);
                filterDd.RegisterValueChangedCallback(ev =>
                {
                    int ni = filterNames.IndexOf(ev.newValue);
                    Undo.RecordObject(owner, "Change Portal Source Filter");
                    transition.portalSourceFilterGuid = ni <= 0 ? "" : sources[ni - 1].guid;
                    EditorUtility.SetDirty(owner);
                });
                box.Add(filterDd);
            }

            var finalRow = HonamiGraphStyles.Row();
            finalRow.Add(new Label("Final Target")
            { style = { unityFontStyleAndWeight = FontStyle.Bold, width = 100 } });
            finalRow.Add(new Label(target != null ? target.stateName : "None"));
            box.Add(finalRow);

            box.Add(new VisualElement { style = { height = 8 } });
            box.Add(new Label("Incoming Traffic (Information)")
            { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });

            string selectedGuid = "";
            VisualElement sourceList = new VisualElement();
            box.Add(sourceList);

            void BuildSources()
            {
                sourceList.Clear();
                foreach (var src in sources)
                {
                    if (src == null) continue;
                    bool expanded = selectedGuid == src.guid;
                    bool filtered = transition.portalSourceFilterGuid == src.guid;

                    var srcItem = new VisualElement();
                    HonamiGraphStyles.ApplyListBox(srcItem);
                    srcItem.style.marginBottom = 2;
                    srcItem.style.backgroundColor = expanded
                        ? new Color(0.24f, 0.48f, 0.90f)
                        : HonamiGraphStyles.ListBoxBg;

                    var srcRow = HonamiGraphStyles.Row();
                    string labelTxt = filtered ? $" {src.stateName} (Source Filter)" : $" {src.stateName}";
                    var srcLbl = new Label(labelTxt);
                    srcLbl.style.flexGrow = 1;
                    srcLbl.RegisterCallback<ClickEvent>(_ =>
                    {
                        selectedGuid = expanded ? "" : src.guid;
                        BuildSources();
                    });
                    srcRow.Add(srcLbl);

                    var gotoBtn = new Button(() =>
                    {
                        var node = graphView.nodes.OfType<HonamiNode>()
                                   .FirstOrDefault(n => n.StateGuid == src.guid);
                        if (node != null) { graphView.ClearSelection(); graphView.AddToSelection(node); graphView.FrameSelection(); }
                    })
                    { text = "⊙" };
                    gotoBtn.style.width = 24;
                    srcRow.Add(gotoBtn);
                    srcItem.Add(srcRow);

                    if (expanded)
                    {
                        var srcTrans = src.transitions.Find(t =>
                        {
                            var ts = controller.states.Find(st => st?.guid == t.targetStateGuid);
                            return ts?.node is HonamiPortalEntranceNode pEnt && pEnt.portalName == portalName;
                        });

                        var detail = new VisualElement();
                        detail.style.paddingLeft = 8;
                        detail.style.marginTop = 4;
                        if (srcTrans != null)
                        {
                            detail.Add(new Label("Entrance Parameters")
                            { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10 } });
                            detail.Add(HonamiGraphStyles.MiniLabel($"Duration: {srcTrans.duration}s"));
                            if (srcTrans.useCurve) detail.Add(HonamiGraphStyles.MiniLabel("Curve: Custom"));
                            else if (srcTrans.ease != HonamiTransitionEase.Linear) detail.Add(HonamiGraphStyles.MiniLabel($"Easing: {srcTrans.ease}"));
                            if (srcTrans.conditions?.Count > 0)
                                detail.Add(HonamiGraphStyles.MiniLabel($"Conditions ({srcTrans.conditions.Count})"));
                        }
                        else detail.Add(HonamiGraphStyles.MiniLabel("(No direct entrance wire found)"));
                        srcItem.Add(detail);
                    }

                    sourceList.Add(srcItem);
                }

                if (sources.Count == 0)
                    sourceList.Add(HonamiGraphStyles.MiniLabel("No incoming transitions found for this portal name."));
            }

            BuildSources();
            return box;
        }

        private static VisualElement BuildConditions(SerializedObject so,
            SerializedProperty condProp, HonamiController controller)
        {
            var box = HonamiGraphStyles.Box();
            var hdr = HonamiGraphStyles.Row();
            hdr.Add(HonamiGraphStyles.SubTitle("Conditions"));
            hdr.Add(HonamiGraphStyles.Spacer());
            VisualElement listEl = null;

            var addBtn = HonamiGraphStyles.SmallButton("+");
            hdr.Add(addBtn);
            box.Add(hdr);
            listEl = new VisualElement();
            box.Add(listEl);

            var paramNames = controller.parameters.Select(p => p.name).ToList();
            var paramTypes = controller.parameters.Select(p => p.type).ToList();

            void Rebuild()
            {
                listEl.Clear();
                so.Update();
                if (condProp.arraySize == 0)
                {
                    listEl.Add(HonamiGraphStyles.MiniLabel("No conditions - transition fires immediately."));
                    return;
                }
                for (int i = 0; i < condProp.arraySize; i++)
                    listEl.Add(BuildConditionRow(so, condProp, i, paramNames, paramTypes, Rebuild));
            }

            addBtn.clicked += () =>
            {
                so.Update();
                int idx = condProp.arraySize;
                condProp.InsertArrayElementAtIndex(idx);
                var c = condProp.GetArrayElementAtIndex(idx);
                c.FindPropertyRelative("parameter").stringValue = paramNames.Count > 0 ? paramNames[0] : "";
                c.FindPropertyRelative("mode").enumValueIndex = (int)HonamiConditionMode.If;
                so.ApplyModifiedProperties();
                Rebuild();
            };

            Rebuild();
            return box;
        }

        private static VisualElement BuildConditionRow(SerializedObject so, SerializedProperty condProp, int i,
            List<string> paramNames, List<HonamiParameterType> paramTypes, System.Action rebuild)
        {
            var row = HonamiGraphStyles.ListBox();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            int captured = i;
            var cp = condProp.GetArrayElementAtIndex(i);
            var paramP = cp.FindPropertyRelative("parameter");
            var modeP = cp.FindPropertyRelative("mode");
            var threshP = cp.FindPropertyRelative("threshold");

            int curP = Mathf.Max(0, paramNames.IndexOf(paramP.stringValue));
            var pDd = new DropdownField(paramNames.Count > 0 ? paramNames : new List<string> { "No params" }, curP);
            pDd.style.flexGrow = 1;
            pDd.style.minWidth = 80;
            row.Add(pDd);

            HonamiParameterType curType = curP >= 0 && curP < paramTypes.Count
                ? paramTypes[curP] : HonamiParameterType.Float;

            List<HonamiConditionMode> GetAllowedModes(HonamiParameterType pt) =>
                (pt == HonamiParameterType.Bool || pt == HonamiParameterType.Trigger)
                    ? new List<HonamiConditionMode> { HonamiConditionMode.If, HonamiConditionMode.IfNot }
                    : System.Enum.GetValues(typeof(HonamiConditionMode)).Cast<HonamiConditionMode>().ToList();

            List<string> ToStrings(List<HonamiConditionMode> modes) => modes.Select(m => m.ToString()).ToList();

            var allowedModes = GetAllowedModes(curType);
            var curMode = (HonamiConditionMode)modeP.enumValueIndex;
            int curModeIdx = Mathf.Max(0, allowedModes.IndexOf(curMode));

            var modeDd = new DropdownField(ToStrings(allowedModes), curModeIdx);
            modeDd.style.width = 75;
            row.Add(modeDd);

            bool isNumeric = curType == HonamiParameterType.Float || curType == HonamiParameterType.Int || curType == HonamiParameterType.Random;
            bool needsThresh = isNumeric && curModeIdx > 1;
            var threshField = new PropertyField(threshP) { label = "" };
            threshField.BindProperty(threshP);
            threshField.style.width = 50;
            threshField.style.display = needsThresh ? DisplayStyle.Flex : DisplayStyle.None;
            row.Add(threshField);

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            row.Add(spacer);

            var del = HonamiGraphStyles.SmallButton(HonamiEditorSymbols.Remove);
            del.clicked += () =>
            {
                so.Update(); condProp.DeleteArrayElementAtIndex(captured); so.ApplyModifiedProperties(); rebuild();
            };
            row.Add(del);

            pDd.RegisterValueChangedCallback(ev =>
            {
                int ni = paramNames.IndexOf(ev.newValue);
                if (ni < 0) return;
                so.Update();
                paramP.stringValue = paramNames[ni];
                so.ApplyModifiedProperties();
                rebuild();
            });

            modeDd.RegisterValueChangedCallback(ev =>
            {
                int ni = allowedModes.FindIndex(m => m.ToString() == ev.newValue);
                if (ni < 0) return;
                so.Update();
                modeP.enumValueIndex = (int)allowedModes[ni];
                so.ApplyModifiedProperties();
                threshField.style.display = (isNumeric && ni > 1) ? DisplayStyle.Flex : DisplayStyle.None;
            });

            return row;
        }
    }
}
