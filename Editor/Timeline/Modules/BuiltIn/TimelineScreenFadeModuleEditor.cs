using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineScreenFadeModule), "Screen Fade", 25)]
    public sealed class TimelineScreenFadeModuleEditor : HonamiTimelineModuleEditor
    {
        private static readonly Color FadeColor = new(0.34f, 0.36f, 0.42f, 0.96f);
        private static HonamiScreenOverlay _overlay;

        public override Color ModuleColor => FadeColor;
        public override Texture Icon => EditorGUIUtility.IconContent("d_SceneViewFx").image;
        public override string ItemDisplayName => "Fade";

        [InitializeOnLoadMethod]
        private static void RegisterCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DestroyOverlay;
            EditorApplication.playModeStateChanged += _ => DestroyOverlay();
        }

        public override string GetItemLabel(HonamiTimelineItem item)
        {
            var fade = (TimelineScreenFadeItem)item;
            string label;
            if (fade.toOpacity > fade.fromOpacity) label = "Fade Out";
            else if (fade.toOpacity < fade.fromOpacity) label = "Fade In";
            else label = "Hold";
            if (fade.delay > 0f) label = $"{label} (delay {fade.delay:0.##}s)";
            return label;
        }

        public override void DecorateItem(HonamiTimelineItemView view)
        {
            var fade = (TimelineScreenFadeItem)view.Item;
            var gradient = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
            };
            gradient.generateVisualContent += mesh => DrawRamp(mesh, gradient.contentRect, fade);
            view.Element.Add(gradient);
            base.DecorateItem(view);
        }

        public override void Preview(HonamiTimelinePreviewContext context)
        {
            var module = (TimelineScreenFadeModule)context.Module;
            var color = module.Evaluate(context.Time);
            if (_overlay == null || !_overlay.IsAlive)
            {
                if (color.a <= 0f) return;
                _overlay = new HonamiScreenOverlay("Honami Screen Fade Preview", HideFlags.HideAndDontSave, module.sortingOrder);
            }
            _overlay.SortingOrder = module.sortingOrder;
            _overlay.SetColor(color);
        }

        public override void StopPreview() => DestroyOverlay();

        private static void DestroyOverlay()
        {
            _overlay?.Dispose();
            _overlay = null;
        }

        private static void DrawRamp(MeshGenerationContext mesh, Rect rect, TimelineScreenFadeItem fade)
        {
            if (rect.width < 2f || rect.height < 2f) return;
            const int steps = 24;
            var painter = mesh.painter2D;
            var fill = fade.color;
            fill.a = 0.55f;
            painter.fillColor = fill;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
            float totalDuration = fade.Duration;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float opacity = fade.OpacityAt(t * totalDuration);
                painter.LineTo(new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, t), Mathf.Lerp(rect.yMax, rect.yMin + 2f, opacity)));
            }
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
