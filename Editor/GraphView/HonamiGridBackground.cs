using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    internal sealed class HonamiGridBackground : ImmediateModeElement
    {
        private const float MinVisibleSpacing = 4f;

        private static Material _lineMaterial;

        private readonly float _baseSpacing;
        private readonly int _thickLineDistance;
        private readonly Func<Vector2> _offsetProvider;
        private readonly Func<float> _scaleProvider;

        private static Material LineMaterial
        {
            get
            {
                if (_lineMaterial == null)
                {
                    _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
                    _lineMaterial.SetInt("_ZWrite", 0);
                    _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
                }

                return _lineMaterial;
            }
        }

        public HonamiGridBackground(float baseSpacing = 30f, int thickLineDistance = 10,
            Func<Vector2> offsetProvider = null, Func<float> scaleProvider = null)
        {
            _baseSpacing = baseSpacing;
            _thickLineDistance = thickLineDistance;
            _offsetProvider = offsetProvider;
            _scaleProvider = scaleProvider;

            pickingMode = PickingMode.Ignore;
            this.StretchToParentSize();
        }

        protected override void ImmediateRepaint()
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            if (!TryGetViewTransform(out Vector2 offset, out float zoom)) return;

            if (!IsFinite(zoom) || zoom <= 0f) zoom = 1f;
            if (!IsFinite(offset.x) || !IsFinite(offset.y)) offset = Vector2.zero;

            LineMaterial.SetPass(0);

            GL.Begin(GL.QUADS);
            GL.Color(HonamiEditorTheme.CanvasBg);
            GL.Vertex3(0f, 0f, 0f);
            GL.Vertex3(rect.width, 0f, 0f);
            GL.Vertex3(rect.width, rect.height, 0f);
            GL.Vertex3(0f, rect.height, 0f);
            GL.End();

            float spacing = _baseSpacing * zoom;
            DrawLines(rect, offset, spacing, HonamiEditorTheme.GridLine);
            DrawLines(rect, offset, spacing * _thickLineDistance, HonamiEditorTheme.GridThickLine);
        }

        private bool TryGetViewTransform(out Vector2 offset, out float zoom)
        {
            if (_offsetProvider != null && _scaleProvider != null)
            {
                offset = _offsetProvider();
                zoom = _scaleProvider();
                return true;
            }

            if (parent is GraphView graphView)
            {
                var containerStyle = graphView.contentViewContainer.resolvedStyle;
                Vector3 translate = containerStyle.translate;
                offset = new Vector2(translate.x, translate.y);
                zoom = containerStyle.scale.value.x;
                return true;
            }

            offset = Vector2.zero;
            zoom = 1f;
            return false;
        }

        private static void DrawLines(Rect rect, Vector2 offset, float spacing, Color color)
        {
            if (spacing < MinVisibleSpacing || color.a <= 0f) return;

            GL.Begin(GL.LINES);
            GL.Color(color);

            for (float x = Mathf.Repeat(offset.x, spacing); x < rect.width; x += spacing)
            {
                GL.Vertex3(x, 0f, 0f);
                GL.Vertex3(x, rect.height, 0f);
            }

            for (float y = Mathf.Repeat(offset.y, spacing); y < rect.height; y += spacing)
            {
                GL.Vertex3(0f, y, 0f);
                GL.Vertex3(rect.width, y, 0f);
            }

            GL.End();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
