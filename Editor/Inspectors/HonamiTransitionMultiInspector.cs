using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiTransitionMultiInspector
    {
        public readonly struct Entry
        {
            public readonly HonamiTransition Transition;
            public readonly HonamiState Owner;
            public readonly HonamiState Target;

            public Entry(HonamiTransition transition, HonamiState owner, HonamiState target)
            {
                Transition = transition;
                Owner = owner;
                Target = target;
            }
        }

        public static VisualElement Build(List<Entry> entries)
        {
            var root = new VisualElement();
            root.style.paddingLeft = root.style.paddingRight = 10;
            root.style.paddingTop = root.style.paddingBottom = 12;

            root.Add(HonamiGraphStyles.Title($"{entries.Count} Transitions"));
            root.Add(HonamiGraphStyles.MiniLabel("Multi-Edit Mode", HonamiGraphStyles.SubTitleClr));

            var listBox = HonamiGraphStyles.ListBox();
            foreach (var entry in entries)
            {
                var row = HonamiGraphStyles.Row();
                row.Add(HonamiGraphStyles.MiniLabel(entry.Owner != null ? entry.Owner.stateName : "AnyState", new Color(0.7f, 0.7f, 0.7f)));
                row.Add(HonamiGraphStyles.MiniLabel(" > ", new Color(0.4f, 0.4f, 0.4f)));
                row.Add(HonamiGraphStyles.MiniLabel(entry.Target != null ? entry.Target.stateName : "Exit", new Color(0.5f, 0.7f, 1f)));
                listBox.Add(row);
            }
            root.Add(listBox);
            root.Add(new VisualElement { style = { height = 6 } });

            bool allCanExit = true;
            bool allValidTargets = true;
            foreach (var entry in entries)
            {
                bool canExit = entry.Owner?.node is HonamiAnimationNode
                            || entry.Owner?.node is HonamiBlendTreeNode
                            || entry.Owner?.node is HonamiRandomAnimationNode;
                if (!canExit) allCanExit = false;
                if (entry.Target == null || entry.Target.node is HonamiExitNode) allValidTargets = false;
            }

            var timingBox = HonamiGraphStyles.Box();
            timingBox.Add(HonamiGraphStyles.SubTitle("Timing"));

            VisualElement exitTimeContainer = null;
            if (allCanExit)
            {
                var exitToggle = BindField(new Toggle("Has Exit Time"), entries, "Edit Transitions Exit Time",
                    t => t.hasExitTime, (t, v) => t.hasExitTime = v);
                exitToggle.tooltip = "If enabled, the transition will only fire after a specific point in the animation.";
                timingBox.Add(exitToggle);

                exitTimeContainer = new VisualElement();
                exitTimeContainer.style.paddingLeft = 12;
                var exitTimeField = BindField(new FloatField("Exit Time (Normalized)"), entries, "Edit Transitions Exit Time",
                    t => t.exitTime, (t, v) => t.exitTime = v);
                exitTimeField.tooltip = "Normalized time (0.0 to 1.0) when the transition should occur.";
                exitTimeContainer.Add(exitTimeField);
                timingBox.Add(exitTimeContainer);
                timingBox.Add(HonamiGraphStyles.Separator());
            }

            VisualElement smartContainer = null;
            VisualElement victimContainer = null;

            void RefreshConditionalVisibility()
            {
                if (exitTimeContainer != null)
                    exitTimeContainer.style.display = All(entries, t => t.hasExitTime) ? DisplayStyle.Flex : DisplayStyle.None;
                if (smartContainer != null)
                    smartContainer.style.display = All(entries, t => t.type == HonamiTransitionType.Smart) ? DisplayStyle.Flex : DisplayStyle.None;
                if (victimContainer != null)
                    victimContainer.style.display = All(entries, t => t.type == HonamiTransitionType.Victim) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var typeField = BindField(new EnumField("Transition Type", entries[0].Transition.type), entries, "Edit Transitions Type",
                t => (Enum)t.type, (t, v) => t.type = (HonamiTransitionType)v, RefreshConditionalVisibility);
            timingBox.Add(typeField);

            var durationField = BindField(new FloatField("Transition Duration (s)"), entries, "Edit Transitions Duration",
                t => t.duration, (t, v) => t.duration = Mathf.Max(0f, v));
            durationField.tooltip = "Time (in seconds) to fade between the source and target states.";
            timingBox.Add(durationField);

            smartContainer = new VisualElement();
            smartContainer.style.paddingLeft = 12;
            smartContainer.Add(BindField(new Toggle("Adaptive Duration"), entries, "Edit Transitions Adaptive Duration",
                t => t.adaptiveDuration, (t, v) => t.adaptiveDuration = v));
            smartContainer.Add(BindField(new FloatField("Smart Scale Factor"), entries, "Edit Transitions Smart Scale",
                t => t.smartScaleFactor, (t, v) => t.smartScaleFactor = Mathf.Clamp(v, 0.1f, 2f)));
            timingBox.Add(smartContainer);

            if (allValidTargets)
            {
                var dstField = BindField(new FloatField("Start Time Offset (s)"), entries, "Edit Transitions Start Offset",
                    t => t.destinationStartTime, (t, v) => t.destinationStartTime = Mathf.Max(0f, v));
                dstField.tooltip = "Offset when the destination animation starts.";
                timingBox.Add(dstField);
            }
            root.Add(timingBox);

            root.Add(new VisualElement { style = { height = 6 } });

            var curveBox = HonamiGraphStyles.Box();
            curveBox.Add(HonamiGraphStyles.SubTitle("Blending & Interruption"));

            var curveContainer = new VisualElement();
            curveContainer.style.paddingLeft = 12;

            void RefreshCurveVisibility()
            {
                curveContainer.style.display = All(entries, t => t.useCurve) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            curveBox.Add(BindField(new Toggle("Use Custom Curve"), entries, "Edit Transitions Curve",
                t => t.useCurve, (t, v) => t.useCurve = v, RefreshCurveVisibility));

            var curveField = new CurveField("Curve") { value = entries[0].Transition.curve };
            curveField.style.marginTop = curveField.style.marginBottom = 3;
            curveField.RegisterValueChangedCallback(ev =>
            {
                var keys = ev.newValue != null ? ev.newValue.keys : System.Array.Empty<Keyframe>();
                ApplyToAll(entries, "Edit Transitions Curve", t => t.curve = new AnimationCurve(keys));
            });
            curveContainer.Add(curveField);
            curveBox.Add(curveContainer);
            RefreshCurveVisibility();

            if (allValidTargets)
            {
                curveBox.Add(BindField(new Toggle("Allow Self Transition"), entries, "Edit Transitions Self Transition",
                    t => t.canTransitionToSelf, (t, v) => t.canTransitionToSelf = v));
            }

            curveBox.Add(BindField(new Toggle("Can Interrupt Active Transition"), entries, "Edit Transitions Interrupt",
                t => t.canInterruptTransition, (t, v) => t.canInterruptTransition = v));

            victimContainer = new VisualElement();
            victimContainer.style.paddingLeft = 12;
            victimContainer.Add(HonamiGraphStyles.MiniLabel("Victim Settings (Advanced Blending)", new Color(0.8f, 0.4f, 0.4f)));

            victimContainer.Add(BindField(new EnumField("Victim State", entries[0].Transition.victimMode), entries, "Edit Transitions Victim Mode",
                t => (Enum)t.victimMode, (t, v) => t.victimMode = (HonamiVictimMode)v));
            victimContainer.Add(BindField(new FloatField("Sacrifice Speed Mult"), entries, "Edit Transitions Sacrifice Speed",
                t => t.sacrificeSpeedMultiplier, (t, v) => t.sacrificeSpeedMultiplier = v));

            CurveField victimCurveField = null;
            Toggle accelToggle = null;

            void RefreshVictimCurveVisibility()
            {
                bool allCustom = All(entries, t => t.useCustomVictimCurve);
                victimCurveField.style.display = allCustom ? DisplayStyle.Flex : DisplayStyle.None;
                accelToggle.style.display = allCustom ? DisplayStyle.None : DisplayStyle.Flex;
            }

            victimContainer.Add(BindField(new Toggle("Use Custom Weight Curve"), entries, "Edit Transitions Victim Curve",
                t => t.useCustomVictimCurve, (t, v) => t.useCustomVictimCurve = v, RefreshVictimCurveVisibility));

            victimCurveField = new CurveField("Victim Weight Curve") { value = entries[0].Transition.victimWeightCurve };
            victimCurveField.style.marginTop = victimCurveField.style.marginBottom = 3;
            victimCurveField.RegisterValueChangedCallback(ev =>
            {
                var keys = ev.newValue != null ? ev.newValue.keys : System.Array.Empty<Keyframe>();
                ApplyToAll(entries, "Edit Transitions Victim Curve", t => t.victimWeightCurve = new AnimationCurve(keys));
            });
            victimContainer.Add(victimCurveField);

            accelToggle = BindField(new Toggle("Accelerated Weight Drop (Quadratic)"), entries, "Edit Transitions Weight Drop",
                t => t.acceleratedWeightDrop, (t, v) => t.acceleratedWeightDrop = v);
            victimContainer.Add(accelToggle);
            RefreshVictimCurveVisibility();

            victimContainer.Add(BindField(new IntegerField("Interruption Priority"), entries, "Edit Transitions Priority",
                t => t.priority, (t, v) => t.priority = v));
            victimContainer.Add(BindField(new Toggle("Kill Active Transitions"), entries, "Edit Transitions Sacrifice",
                t => t.sacrificeExisting, (t, v) => t.sacrificeExisting = v));

            curveBox.Add(victimContainer);
            root.Add(curveBox);
            RefreshConditionalVisibility();

            root.Add(new VisualElement { style = { height = 6 } });
            root.Add(new HelpBox("Conditions and Parameter Assignments are per-transition. Select a single transition to edit them.",
                HelpBoxMessageType.Info));

            return root;
        }

        private static bool All(List<Entry> entries, Func<HonamiTransition, bool> predicate)
        {
            foreach (var entry in entries)
                if (!predicate(entry.Transition)) return false;
            return true;
        }

        private static TField BindField<TField, TValue>(TField field, List<Entry> entries, string undoName,
            Func<HonamiTransition, TValue> get, Action<HonamiTransition, TValue> set, Action onChanged = null)
            where TField : BaseField<TValue>
        {
            var firstValue = get(entries[0].Transition);
            field.SetValueWithoutNotify(firstValue);

            var comparer = EqualityComparer<TValue>.Default;
            for (int i = 1; i < entries.Count; i++)
            {
                if (!comparer.Equals(get(entries[i].Transition), firstValue))
                {
                    field.showMixedValue = true;
                    break;
                }
            }

            field.RegisterValueChangedCallback(ev =>
            {
                ApplyToAll(entries, undoName, t => set(t, ev.newValue));
                field.showMixedValue = false;
                onChanged?.Invoke();
            });

            field.style.marginTop = field.style.marginBottom = 3;
            return field;
        }

        private static void ApplyToAll(List<Entry> entries, string undoName, Action<HonamiTransition> apply)
        {
            var owners = new HashSet<HonamiState>();
            foreach (var entry in entries)
                if (entry.Owner != null) owners.Add(entry.Owner);

            var ownerArray = new UnityEngine.Object[owners.Count];
            int idx = 0;
            foreach (var owner in owners) ownerArray[idx++] = owner;

            Undo.RecordObjects(ownerArray, undoName);
            foreach (var entry in entries) apply(entry.Transition);
            foreach (var owner in owners) EditorUtility.SetDirty(owner);
        }
    }
}
