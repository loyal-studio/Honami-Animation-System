#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HonamiAnimationSystem.Editor.Preview
{
    /// <summary>
    /// Isolated 3D viewport built on PreviewRenderUtility. Renders a skeleton as joint spheres and
    /// bone cubes with an orbit camera and a ground grid. Rendering-only and reusable by any editor
    /// surface that needs to show a Honami pose (blend tree, transition inspector, ...).
    /// </summary>
    internal sealed class HonamiPreviewStage : IDisposable
    {
        private PreviewRenderUtility _preview;
        private Mesh _sphereMesh;
        private Mesh _cubeMesh;
        private Mesh _gridMesh;
        private Material _jointMat;
        private Material _boneMat;
        private Material _gridMat;

        private float _yaw = 130f;
        private float _pitch = 12f;
        private float _distance = 4f;
        private Vector3 _pivot = Vector3.zero;
        private float _extent = 1f;
        private float _groundY;

        private Color _background = new(0.16f, 0.17f, 0.19f, 1f);

        private static readonly List<HonamiPreviewStage> LiveStages = new();

        // PreviewRenderUtility owns a native preview scene that survives a domain reload while the
        // managed owner does not, so an editor window that never gets a DetachFromPanel (recompile,
        // editor quit) would leak the scene forever.
        static HonamiPreviewStage()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
        }

        private static void DisposeAll()
        {
            for (int i = LiveStages.Count - 1; i >= 0; i--) LiveStages[i].Dispose();
            LiveStages.Clear();
        }

        private void EnsureResources()
        {
            if (_preview == null)
            {
                _preview = new PreviewRenderUtility();
                _preview.camera.fieldOfView = 45f;
                _preview.camera.nearClipPlane = 0.01f;
                _preview.camera.farClipPlane = 2000f;
                _preview.camera.clearFlags = CameraClearFlags.SolidColor;
                _preview.camera.backgroundColor = _background;
                LiveStages.Add(this);
            }

            if (_sphereMesh == null) _sphereMesh = PrimitiveMesh(PrimitiveType.Sphere);
            if (_cubeMesh == null) _cubeMesh = PrimitiveMesh(PrimitiveType.Cube);
            if (_gridMesh == null) _gridMesh = BuildGridMesh(10, 1f);

            if (_jointMat == null) _jointMat = ColoredMaterial(new Color(0.95f, 0.78f, 0.35f), true);
            if (_boneMat == null) _boneMat = ColoredMaterial(new Color(0.62f, 0.72f, 0.85f), true);
            if (_gridMat == null) _gridMat = ColoredMaterial(new Color(0.32f, 0.34f, 0.38f, 1f), true);
        }

        public void Orbit(Vector2 delta)
        {
            _yaw += delta.x * 0.4f;
            _pitch = Mathf.Clamp(_pitch + delta.y * 0.4f, -89f, 89f);
        }

        public void Zoom(float scrollDelta)
        {
            _distance = Mathf.Clamp(_distance * (1f + scrollDelta * 0.1f), _extent * 0.2f, _extent * 12f);
        }

        public void Pan(Vector2 delta)
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 right = rot * Vector3.right;
            Vector3 up = rot * Vector3.up;
            float scale = _distance * 0.0015f;
            _pivot += (-right * delta.x + up * delta.y) * scale;
        }

        public void Frame(Bounds bounds)
        {
            _pivot = bounds.center;
            _extent = Mathf.Max(0.25f, bounds.size.magnitude);
            _distance = _extent * 1.35f;
            _groundY = bounds.min.y;
        }

        public Texture Render(Rect rect, IReadOnlyList<Transform> bones, int[] parentIndex)
        {
            if (rect.width < 4f || rect.height < 4f) return null;

            EnsureResources();
            _preview.BeginPreview(rect, GUIStyle.none);

            var cam = _preview.camera;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = _pivot - rot * Vector3.forward * _distance;
            cam.backgroundColor = _background;

            DrawGrid();
            DrawSkeleton(bones, parentIndex);

            cam.Render();
            return _preview.EndPreview();
        }

        private void DrawGrid()
        {
            if (_gridMesh == null || _gridMat == null) return;
            float s = Mathf.Max(0.05f, _extent * 0.12f);
            Matrix4x4 m = Matrix4x4.TRS(new Vector3(_pivot.x, _groundY, _pivot.z), Quaternion.identity, new Vector3(s, s, s));
            _preview.DrawMesh(_gridMesh, m, _gridMat, 0);
        }

        private void DrawSkeleton(IReadOnlyList<Transform> bones, int[] parentIndex)
        {
            if (bones == null || bones.Count == 0) return;

            float jointSize = Mathf.Max(0.004f, _extent * 0.02f);
            float boneThick = Mathf.Max(0.002f, _extent * 0.008f);
            var jointScale = new Vector3(jointSize, jointSize, jointSize);

            for (int i = 0; i < bones.Count; i++)
            {
                Transform t = bones[i];
                if (t == null) continue;
                Vector3 p = t.position;

                _preview.DrawMesh(_sphereMesh, Matrix4x4.TRS(p, Quaternion.identity, jointScale), _jointMat, 0);

                int pi = parentIndex != null && i < parentIndex.Length ? parentIndex[i] : -1;
                if (pi < 0 || pi >= bones.Count || bones[pi] == null) continue;

                Vector3 pp = bones[pi].position;
                Vector3 dir = p - pp;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;

                Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / len);
                Matrix4x4 m = Matrix4x4.TRS((p + pp) * 0.5f, rot, new Vector3(boneThick, len, boneThick));
                _preview.DrawMesh(_cubeMesh, m, _boneMat, 0);
            }
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            go.hideFlags = HideFlags.HideAndDontSave;
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(go);
            return mesh;
        }

        private static Mesh BuildGridMesh(int half, float step)
        {
            var verts = new List<Vector3>((half * 2 + 1) * 4);
            float e = half * step;
            for (int i = -half; i <= half; i++)
            {
                float a = i * step;
                verts.Add(new Vector3(a, 0f, -e));
                verts.Add(new Vector3(a, 0f, e));
                verts.Add(new Vector3(-e, 0f, a));
                verts.Add(new Vector3(e, 0f, a));
            }

            var indices = new int[verts.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(verts);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            return mesh;
        }

        private static Material ColoredMaterial(Color color, bool opaque)
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_Color", color);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", opaque ? 1 : 0);
            if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)(opaque ? BlendMode.One : BlendMode.SrcAlpha));
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)(opaque ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Back);
            return mat;
        }

        public void Dispose()
        {
            if (_preview != null)
            {
                _preview.Cleanup();
                LiveStages.Remove(this);
            }
            _preview = null;

            if (_gridMesh != null) UnityEngine.Object.DestroyImmediate(_gridMesh);
            if (_jointMat != null) UnityEngine.Object.DestroyImmediate(_jointMat);
            if (_boneMat != null) UnityEngine.Object.DestroyImmediate(_boneMat);
            if (_gridMat != null) UnityEngine.Object.DestroyImmediate(_gridMat);

            _gridMesh = null;
            _jointMat = _boneMat = _gridMat = null;
            _sphereMesh = _cubeMesh = null;
        }
    }
}
#endif
