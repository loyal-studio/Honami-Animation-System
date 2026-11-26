using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    // Draws a full-screen color through a bare CanvasRenderer, so the package does not depend on uGUI.
    public sealed class HonamiScreenOverlay : IDisposable
    {
        private const float Extent = 100000f;

        private readonly GameObject _root;
        private readonly Canvas _canvas;
        private readonly CanvasRenderer _renderer;
        private readonly Mesh _mesh;
        private Color _color = Color.clear;

        public HonamiScreenOverlay(string name, HideFlags hideFlags, int sortingOrder)
        {
            _root = new GameObject(name) { hideFlags = hideFlags };

            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;

            var quad = new GameObject("Quad", typeof(RectTransform)) { hideFlags = hideFlags };
            quad.transform.SetParent(_root.transform, false);
            _renderer = quad.AddComponent<CanvasRenderer>();

            _mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(new[]
            {
                new Vector3(-Extent, -Extent), new Vector3(-Extent, Extent),
                new Vector3(Extent, Extent), new Vector3(Extent, -Extent)
            });
            _mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            _mesh.SetUVs(0, new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
            _mesh.SetTriangles(new[] { 0, 1, 2, 2, 3, 0 }, 0);

            _renderer.SetMesh(_mesh);
            _renderer.materialCount = 1;
            _renderer.SetMaterial(Canvas.GetDefaultCanvasMaterial(), 0);
            _renderer.SetColor(Color.clear);
            _root.SetActive(false);
        }

        public bool IsAlive => _root != null;
        public Color Color => _color;

        public int SortingOrder
        {
            get => _canvas != null ? _canvas.sortingOrder : 0;
            set
            {
                if (_canvas != null) _canvas.sortingOrder = value;
            }
        }

        public void SetColor(Color color)
        {
            if (_root == null || color == _color) return;
            _color = color;
            _renderer.SetColor(color);
            bool visible = color.a > 0f;
            if (_root.activeSelf != visible)
            {
                _root.SetActive(visible);
                if (visible)
                {
                    _renderer.SetMesh(_mesh);
                    _renderer.SetMaterial(Canvas.GetDefaultCanvasMaterial(), 0);
                    _renderer.SetColor(color);
                }
            }
        }

        public void Dispose()
        {
            if (_root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }
            if (_mesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_mesh);
                else UnityEngine.Object.DestroyImmediate(_mesh);
            }
        }
    }
}
