using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Compact list row for a blend space motion: the clip, its speed and mirror flag on one line and
    /// one field per blend space axis below, so the standard list renders without nested foldouts.
    /// </summary>
    [CustomPropertyDrawer(typeof(HonamiBlendSpaceMotion))]
    public sealed class HonamiBlendSpaceMotionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { style = { marginTop = 2, marginBottom = 5 } };

            var topRow = HonamiGraphStyles.Row();

            var clipField = new ObjectField { objectType = typeof(AnimationClip), allowSceneObjects = false };
            clipField.BindProperty(property.FindPropertyRelative("clip"));
            clipField.style.flexGrow = 1;
            clipField.style.minWidth = 0;
            clipField.style.marginRight = 4;
            topRow.Add(clipField);

            var speedField = new FloatField("Speed") { tooltip = "Playback speed when Synchronize Speed is off." };
            speedField.BindProperty(property.FindPropertyRelative("speed"));
            CompactLabel(speedField, 38);
            speedField.style.width = 84;
            speedField.style.flexShrink = 0;
            speedField.style.marginRight = 4;
            topRow.Add(speedField);

            var mirrorToggle = new Toggle { tooltip = "Mirror this motion" };
            mirrorToggle.BindProperty(property.FindPropertyRelative("mirror"));
            mirrorToggle.style.flexShrink = 0;
            mirrorToggle.style.marginRight = 0;
            topRow.Add(mirrorToggle);

            root.Add(topRow);

            var positionProp = property.FindPropertyRelative("position");
            if (positionProp != null && positionProp.arraySize > 0)
            {
                // Beyond three axes the fields get too narrow to read, so they wrap onto further rows.
                const int perRow = 3;
                int axisCount = positionProp.arraySize;

                for (int start = 0; start < axisCount; start += perRow)
                {
                    var positionRow = HonamiGraphStyles.Row();
                    positionRow.style.marginTop = 2;

                    int end = Mathf.Min(start + perRow, axisCount);
                    for (int axis = start; axis < end; axis++)
                    {
                        var axisField = new FloatField(AxisLabel(axis));
                        axisField.BindProperty(positionProp.GetArrayElementAtIndex(axis));
                        CompactLabel(axisField, 14);
                        axisField.style.flexGrow = 1;
                        axisField.style.flexShrink = 1;
                        axisField.style.minWidth = 0;
                        axisField.style.marginRight = axis < end - 1 ? 4 : 0;
                        positionRow.Add(axisField);
                    }

                    // Keep the last row's fields the same width as a full row instead of stretching.
                    for (int filler = end; filler < start + perRow; filler++)
                    {
                        positionRow.Add(new VisualElement { style = { flexGrow = 1, marginLeft = 4 } });
                    }

                    root.Add(positionRow);
                }
            }
            else
            {
                root.Add(HonamiGraphStyles.MiniLabel("No axes - add one above to give this motion a position."));
            }

            return root;
        }

        private static void CompactLabel(BaseField<float> field, float width)
        {
            var label = field.labelElement;
            label.style.minWidth = width;
            label.style.width = width;
            label.style.marginRight = 2;
            label.style.fontSize = 10;
            label.style.color = HonamiGraphStyles.GreyText;
        }

        private static string AxisLabel(int axis) => axis switch
        {
            0 => "X",
            1 => "Y",
            2 => "Z",
            3 => "W",
            _ => axis.ToString()
        };
    }
}
