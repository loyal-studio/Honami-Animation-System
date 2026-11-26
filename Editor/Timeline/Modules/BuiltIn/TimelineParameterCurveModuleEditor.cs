using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineParameterCurveModule), "Parameter Curve", 30)]
    public class TimelineParameterCurveModuleEditor : HonamiTimelineModuleEditor
    {
        private const int CurveSamples = 48;
        private const float CurveInset = 4f;
        private const float KeySize = 9f;
        private const float MinKeySpacing = 0.001f;
        private static readonly Color CurveColor = new(0.58f, 0.40f, 0.78f, 0.96f);
        private static readonly Color CurveLine = new(1f, 1f, 1f, 0.75f);
        private static readonly Color KeyFill = new(1f, 1f, 1f, 0.9f);
        private static readonly Color KeyHot = new(1f, 0.78f, 0.3f, 1f);

        public override Color ModuleColor => CurveColor;
        public override Texture Icon => EditorGUIUtility.IconContent("d_curvekeyframe").image;
        public override string ItemDisplayName => "Curve";
        public override string GetItemLabel(HonamiTimelineItem item) => string.Empty;

        public override void DecorateItem(HonamiTimelineItemView view)
        {
            base.DecorateItem(view);

            var item = (TimelineParameterCurveItem)view.Item;
            item.curve ??= AnimationCurve.Linear(0f, 0f, 1f, 1f);
            GetValueRange(item.curve, out float min, out float range);

            var overlay = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = CurveInset, bottom = CurveInset }
            };
            overlay.generateVisualContent += context => DrawCurve(context, overlay.contentRect, item.curve, min, range);
            view.Element.Add(overlay);

            var keys = item.curve.keys;
            for (int i = 0; i < keys.Length; i++)
                overlay.Add(CreateKeyHandle(view, item, i, min, range));
        }

        public override void OnItemDoubleClicked(HonamiTimelineEditContext context, HonamiTimelineItem item, float time)
        {
            var curveItem = (TimelineParameterCurveItem)item;
            if (curveItem.curve == null) return;

            float t = Mathf.Clamp01((time - curveItem.time) / curveItem.length);
            context.RecordUndo("Add Curve Key");
            int index = curveItem.curve.AddKey(t, curveItem.curve.Evaluate(t));
            if (index >= 0) SmoothAround(curveItem.curve, index);
            context.MarkChanged();
        }

        public override HonamiTimelineItem SplitItem(HonamiTimelineEditContext context, HonamiTimelineItem item, float time)
        {
            var curveItem = (TimelineParameterCurveItem)item;
            if (curveItem.curve == null) return base.SplitItem(context, item, time);

            float cut = Mathf.Clamp01((time - curveItem.time) / curveItem.length);
            var original = new AnimationCurve(curveItem.curve.keys);
            var tail = base.SplitItem(context, item, time) as TimelineParameterCurveItem;
            if (tail == null) return null;

            curveItem.curve = Remap(original, 0f, cut);
            tail.curve = Remap(original, cut, 1f);
            return tail;
        }

        private static AnimationCurve Remap(AnimationCurve source, float from, float to)
        {
            float span = Mathf.Max(0.0001f, to - from);
            var result = new AnimationCurve();
            result.AddKey(new Keyframe(0f, source.Evaluate(from)));
            foreach (var key in source.keys)
            {
                if (key.time <= from + MinKeySpacing || key.time >= to - MinKeySpacing) continue;
                result.AddKey(new Keyframe((key.time - from) / span, key.value, key.inTangent * span, key.outTangent * span));
            }
            result.AddKey(new Keyframe(1f, source.Evaluate(to)));
            SmoothAround(result, 0);
            SmoothAround(result, result.length - 1);
            return result;
        }

        private static VisualElement CreateKeyHandle(HonamiTimelineItemView view, TimelineParameterCurveItem item, int index, float min, float range)
        {
            var key = item.curve.keys[index];
            var handle = new VisualElement
            {
                tooltip = "Drag to move the key. Right-click to delete. Double-click the curve to add a key.",
                style =
                {
                    position = Position.Absolute,
                    width = KeySize,
                    height = KeySize,
                    marginLeft = -KeySize * 0.5f,
                    marginTop = -KeySize * 0.5f,
                    backgroundColor = KeyFill,
                    rotate = new Rotate(45f)
                }
            };
            PlaceHandle(handle, key.time, (key.value - min) / range);

            handle.RegisterCallback<PointerEnterEvent>(_ => handle.style.backgroundColor = KeyHot);
            handle.RegisterCallback<PointerLeaveEvent>(_ => handle.style.backgroundColor = KeyFill);
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                if (item.curve.length > 2)
                    view.Modify("Delete Curve Key", () => item.curve.RemoveKey(index));
                evt.StopPropagation();
            });

            view.AddHandle(handle, "Move Curve Key", drag =>
            {
                var keys = item.curve.keys;
                if (index >= keys.Length) return;

                float height = Mathf.Max(1f, view.Element.layout.height - CurveInset * 2f);

                float absoluteTime = view.SnapTime(view.LocalXToTime(drag.LocalPosition.x), drag.ShiftHeld);
                float t = Mathf.Clamp01((absoluteTime - item.time) / item.length);
                float lower = index > 0 ? keys[index - 1].time + MinKeySpacing : 0f;
                float upper = index < keys.Length - 1 ? keys[index + 1].time - MinKeySpacing : 1f;
                t = Mathf.Clamp(t, lower, upper);

                float normalized = 1f - Mathf.Clamp01((drag.LocalPosition.y - CurveInset) / height);
                keys[index].time = t;
                keys[index].value = min + normalized * range;
                item.curve.keys = keys;
                SmoothAround(item.curve, index);

                PlaceHandle(handle, t, normalized);
            });

            return handle;
        }

        private static void PlaceHandle(VisualElement handle, float t, float normalizedValue)
        {
            handle.style.left = Length.Percent(t * 100f);
            handle.style.top = Length.Percent((1f - Mathf.Clamp01(normalizedValue)) * 100f);
        }

        private static void SmoothAround(AnimationCurve curve, int index)
        {
            for (int i = Mathf.Max(0, index - 1); i <= Mathf.Min(curve.length - 1, index + 1); i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
        }

        private static void GetValueRange(AnimationCurve curve, out float min, out float range)
        {
            min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i <= CurveSamples; i++)
            {
                float value = curve.Evaluate(i / (float)CurveSamples);
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }
            foreach (var key in curve.keys)
            {
                min = Mathf.Min(min, key.value);
                max = Mathf.Max(max, key.value);
            }

            float padding = Mathf.Max((max - min) * 0.1f, 0.5f);
            min -= padding;
            range = max - min + padding;
        }

        private static void DrawCurve(MeshGenerationContext context, Rect rect, AnimationCurve curve, float min, float range)
        {
            if (curve == null || curve.length == 0 || rect.width < 2f || rect.height < 2f) return;

            var painter = context.painter2D;
            painter.strokeColor = CurveLine;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            for (int i = 0; i <= CurveSamples; i++)
            {
                float t = i / (float)CurveSamples;
                var point = new Vector2(rect.xMin + t * rect.width, rect.yMax - (curve.Evaluate(t) - min) / range * rect.height);
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }
            painter.Stroke();
        }
    }
}
