using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.BlendSpace;
using HonamiAnimationSystem.Editor.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiBlendSpaceNode), "Complex/Blend Space (2D+)", 41)]
    public sealed class HonamiBlendSpaceNodeEditor : HonamiNodeEditorBase
    {
        private const string NoneLabel = "- None -";
        private static readonly Color NoticeColor = new(0.95f, 0.7f, 0.35f);

        public override string NodeCssClass => "honami-node-blend";
        public override string TopBarCssClass => "honami-node-top-blend";
        public override string AvatarCssClass => "honami-node-avatar-blend";
        public override string IconCssClass => "honami-node-icon-blend";
        public override string Tooltip => "Blends clips across a blend space of any dimension, one float parameter per axis.";

        public override string GetSubtitle(HonamiState state)
        {
            if (state.node is not HonamiBlendSpaceNode node) return "SPACE  -";

            int axisCount = node.AxisCount;
            if (axisCount == 0) return "SPACE  no axes";

            var names = node.blendParameters
                .Select(p => string.IsNullOrEmpty(p) ? "-" : p)
                .ToArray();

            return axisCount <= 2
                ? "SPACE  " + string.Join(" / ", names)
                : $"SPACE {axisCount}D  " + string.Join(" / ", names.Take(2)) + " ...";
        }

        public override void OnDoubleClick(HonamiState state, HonamiController controller)
        {
            HonamiBlendSpaceWindow.OpenWindow();
            HonamiBlendSpaceWindow.InspectState(controller, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController controller)
        {
            Button button = HonamiGraphStyles.TallButton("  Open Blend Space Editor", HonamiEditorIcons.BlendTree);
            button.style.backgroundColor = HonamiGraphStyles.Accent;
            button.clicked += () =>
            {
                HonamiBlendSpaceWindow.OpenWindow();
                HonamiBlendSpaceWindow.InspectState(controller, state.guid);
            };
            root.Add(button);
        }

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            var root = new VisualElement();
            if (state.node is not HonamiBlendSpaceNode node) return root;

            void Rebuild()
            {
                root.Clear();
                root.Add(BuildInspectorBox(node, nodeSO, controller, state, Rebuild));
            }

            Rebuild();
            return root;
        }

        private static VisualElement BuildInspectorBox(
            HonamiBlendSpaceNode node,
            SerializedObject nodeSO,
            HonamiController controller,
            HonamiState state,
            Action rebuild)
        {
            nodeSO.Update();

            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Blend Space Node"));

            AddNodeField(box, nodeSO, "spaceType", "Space Type");

            int fixedAxes = HonamiBlendSpaceSolver.FixedAxisCount(node.spaceType);
            if (!node.HasValidAxisCount)
            {
                box.Add(Notice(fixedAxes > 0
                    ? $"{ObjectNames.NicifyVariableName(node.spaceType.ToString())} needs exactly {fixedAxes} axes, but has {node.AxisCount}. It brackets the sample by angular order, which only exists in a plane."
                    : "Add at least one axis before this node can blend."));
            }

            BuildAxisSection(box, node, controller, rebuild);

            AddNodeField(box, nodeSO, "blendParameterDampTime", "Damp Time");
            AddNodeField(box, nodeSO, "synchronizeSpeed", "Synchronize Speed");
            AddStateField(box, state, "loop", "Loop");
            AddStateField(box, state, "holdLastFrame", "Freeze Last Frame");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");

            box.Add(SectionHeader($"Motions ({node.blendMotions?.Count ?? 0})"));
            AddNodeListField(box, nodeSO, "blendMotions", string.Empty, element =>
            {
                SerializedProperty speed = element.FindPropertyRelative("speed");
                if (Mathf.Approximately(speed.floatValue, 0f)) speed.floatValue = 1f;

                SerializedProperty position = element.FindPropertyRelative("position");
                if (position != null) position.arraySize = node.AxisCount;
            });

            box.Add(HonamiGraphStyles.MiniLabel("Drag motions into place on the blend space field - double-click the node to open it."));

            return box;
        }

        private static void BuildAxisSection(VisualElement parent, HonamiBlendSpaceNode node, HonamiController controller, Action rebuild)
        {
            parent.Add(SectionHeader($"Axes ({node.AxisCount})"));

            VisualElement list = HonamiGraphStyles.ListBox();

            if (node.AxisCount == 0)
            {
                list.Add(HonamiGraphStyles.MiniLabel("No axes yet. Each axis is driven by its own float parameter."));
            }

            var floatParameters = controller.parameters
                .Where(p => p.type == HonamiParameterType.Float)
                .Select(p => p.name)
                .ToList();
            floatParameters.Insert(0, NoneLabel);

            for (int axis = 0; axis < node.AxisCount; axis++)
            {
                int captured = axis;
                VisualElement row = HonamiGraphStyles.Row();

                string current = node.blendParameters[axis];
                var choices = new List<string>(floatParameters);
                if (!string.IsNullOrEmpty(current) && !choices.Contains(current)) choices.Insert(1, current);

                int index = string.IsNullOrEmpty(current) ? 0 : Mathf.Max(0, choices.IndexOf(current));
                var dropdown = new DropdownField(AxisLabel(axis), choices, index);
                StyleCompactLabel(dropdown.labelElement, 46);
                dropdown.style.flexGrow = 1;
                dropdown.style.minWidth = 0;
                dropdown.style.marginRight = 4;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    HonamiEditorController.SetBlendSpaceAxisParameter(node, captured, evt.newValue == NoneLabel ? string.Empty : evt.newValue);
                    rebuild();
                });
                row.Add(dropdown);

                Button remove = HonamiGraphStyles.SmallButton("×", () =>
                {
                    HonamiEditorController.RemoveBlendSpaceAxis(node, captured);
                    rebuild();
                });
                remove.tooltip = "Remove this axis from the node and from every motion";
                remove.style.flexShrink = 0;
                row.Add(remove);

                list.Add(row);
            }

            parent.Add(list);

            Button addAxis = HonamiGraphStyles.TallButton("+ Add Axis", () =>
            {
                HonamiEditorController.AddBlendSpaceAxis(node, string.Empty);
                rebuild();
            });
            addAxis.style.height = 24;
            parent.Add(addAxis);
        }

        public override bool SupportsTimelinePreview => true;

        public override void BuildTimelineControls(TimelineState state, VisualElement container, Action rebuild)
        {
            if (state.SelectedState?.node is not HonamiBlendSpaceNode node) return;

            int axisCount = node.AxisCount;
            if (axisCount == 0) return;

            EnsurePreviewAxes(state, axisCount);

            for (int axis = 0; axis < axisCount; axis++)
            {
                int captured = axis;
                GetAxisRange(node, axis, out float min, out float max);

                string parameterName = node.blendParameters[axis];
                var slider = new Slider(string.IsNullOrEmpty(parameterName) ? AxisLabel(axis) : parameterName, min, max)
                {
                    value = Mathf.Clamp(state.NodePreviewAxes[axis], min, max),
                    style = { width = StyleKeyword.Auto, minWidth = 140, flexGrow = 1, marginRight = 6 }
                };
                slider.RegisterValueChangedCallback(evt =>
                {
                    state.NodePreviewAxes[captured] = evt.newValue;
                    rebuild();
                });
                container.Add(slider);
            }
        }

        public override void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT)
            => TimelinePreview.SampleBlendSpace(state, rootTarget, internalT);

        public override float GetTimelineDuration(TimelineState state)
        {
            if (state.node is not HonamiBlendSpaceNode node) return 5f;

            int count = node.blendMotions?.Count ?? 0;
            if (count == 0) return 5f;

            Span<float> weights = count <= 32 ? stackalloc float[count] : new float[count];
            SolveAtPreview(state, node, weights);
            return HonamiBlendSpaceSolver.GetBlendedDuration(node.blendMotions, weights);
        }

        public override void CollectTimelineRows(TimelineState state, List<HonamiTimelineRow> rows)
        {
            if (state.node is not HonamiBlendSpaceNode node || node.blendMotions == null) return;
            for (int i = 0; i < node.blendMotions.Count; i++)
            {
                var clip = node.blendMotions[i].clip;
                rows.Add(new HonamiTimelineRow(clip != null ? clip.name : $"Empty Motion {i}", HonamiTimelineRow.ClipIcon, TimelineTheme.PreviewTrack));
            }
        }

        public override void DrawTimelineRow(TimelineState state, ITimelineTrackDrawer drawer, int row, float y)
        {
            if (state.node is not HonamiBlendSpaceNode node) return;

            int count = node.blendMotions?.Count ?? 0;
            if (row >= count) return;

            var motion = node.blendMotions[row];
            if (motion.clip == null) return;

            Span<float> weights = count <= 32 ? stackalloc float[count] : new float[count];
            SolveAtPreview(state, node, weights);

            float speed = motion.speed != 0f ? Mathf.Abs(motion.speed) : 1f;
            string label = $"{motion.clip.name}  {weights[row] * 100f:F0}%";
            drawer.AddReadonlyClip(motion.clip, label, 0f, motion.clip.length / speed, y,
                weights[row] > 0.001f ? TimelineTheme.PreviewTrack : TimelineTheme.RandomTrack);
        }

        private static void SolveAtPreview(TimelineState state, HonamiBlendSpaceNode node, Span<float> weights)
        {
            int axisCount = node.AxisCount;
            EnsurePreviewAxes(state, axisCount);

            var sample = axisCount > 0
                ? state.NodePreviewAxes.AsSpan(0, axisCount)
                : ReadOnlySpan<float>.Empty;

            HonamiBlendSpaceSolver.CalculateWeights(node.blendMotions, axisCount, node.spaceType, sample, weights);
        }

        private static void EnsurePreviewAxes(TimelineState state, int axisCount)
        {
            if (state.NodePreviewAxes == null || state.NodePreviewAxes.Length != axisCount)
            {
                state.NodePreviewAxes = new float[axisCount];
            }
        }

        private static void GetAxisRange(HonamiBlendSpaceNode node, int axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;

            if (node.blendMotions != null)
            {
                foreach (var motion in node.blendMotions)
                {
                    float value = HonamiBlendSpaceSolver.GetAxis(motion, axis);
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            if (min > max) { min = -1f; max = 1f; }
            if (Mathf.Approximately(min, max)) { min -= 1f; max += 1f; }
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
            var box = new VisualElement();
            box.style.backgroundColor = new Color(NoticeColor.r, NoticeColor.g, NoticeColor.b, 0.10f);
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = NoticeColor;
            box.Padding(6, 8);
            box.style.marginTop = 4;
            box.style.marginBottom = 4;
            box.Add(HonamiGraphStyles.MiniLabel(text, NoticeColor));
            return box;
        }

        private static void StyleCompactLabel(VisualElement label, float width)
        {
            label.style.minWidth = width;
            label.style.width = width;
            label.style.marginRight = 2;
            label.style.fontSize = 10;
            label.style.color = HonamiGraphStyles.GreyText;
        }

        private static string AxisLabel(int axis) => axis switch
        {
            0 => "Axis X",
            1 => "Axis Y",
            2 => "Axis Z",
            _ => $"Axis {axis}"
        };
    }
}
