using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [CustomPropertyDrawer(typeof(HonamiBlendCurve))]
    public sealed class HonamiBlendCurveDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            var ease = property.FindPropertyRelative("ease");
            var useCustom = property.FindPropertyRelative("useCustomCurve");
            var curve = property.FindPropertyRelative("customCurve");

            var easeField = new PropertyField(ease, property.displayName)
            {
                tooltip = "Easing preset used for the fade. Smooth Step is the classic Honami crossfade."
            };
            easeField.BindProperty(ease);
            root.Add(easeField);

            var customField = new PropertyField(useCustom, "Custom Curve")
            {
                tooltip = "Use a hand-authored curve instead of the preset above."
            };
            customField.BindProperty(useCustom);
            root.Add(customField);

            var curveField = new PropertyField(curve, "Curve")
            {
                tooltip = "Fade amount over the blend: 0 = nothing faded, 1 = fully faded."
            };
            curveField.BindProperty(curve);
            root.Add(curveField);

            void Refresh()
            {
                bool custom = useCustom.boolValue;
                curveField.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
                easeField.SetEnabled(!custom);
            }

            // Switching to a custom curve with nothing authored yet would leave an
            // unusable empty field, so seed a plain ease that the user can reshape.
            void EnsureCurve()
            {
                if (!useCustom.boolValue) return;

                var existing = curve.animationCurveValue;
                if (existing != null && existing.length > 0) return;

                root.schedule.Execute(() =>
                {
                    if (!useCustom.boolValue) return;
                    var current = curve.animationCurveValue;
                    if (current != null && current.length > 0) return;

                    curve.animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                    curve.serializedObject.ApplyModifiedProperties();
                });
            }

            root.TrackPropertyValue(useCustom, _ =>
            {
                Refresh();
                EnsureCurve();
            });
            Refresh();
            return root;
        }
    }
}
