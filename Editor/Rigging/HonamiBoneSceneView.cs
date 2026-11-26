using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Riggings
{
    [InitializeOnLoad]
    internal static class HonamiBoneSceneView
    {
        private sealed class BoneCache
        {
            public int Version;
            public readonly List<Transform> SegmentStarts = new();
            public readonly List<Transform> SegmentEnds = new();
            public readonly List<Transform> Tips = new();
        }

        private const float MinPickPixels = 4f;
        private const float MaxPickPixels = 15f;
        private const float HandleReferencePixels = 80f;
        private const float BoneRadiusRatio = 0.08f;
        private const float OctahedronPivotRatio = 0.15f;
        private const float JointScreenRatio = 0.05f;
        private const float TipScreenRatio = 0.07f;
        private const float FillAlpha = 0.35f;

        // Octahedron points: 0 start, 1 end, 2..5 ring. Pyramid points: 0 end, 1..4 base.
        private static readonly int[] OctahedronTriangles =
        {
            0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2,
            1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2,
        };
        private static readonly int[] OctahedronEdges =
        {
            0, 2, 0, 3, 0, 4, 0, 5,
            1, 2, 1, 3, 1, 4, 1, 5,
            2, 3, 3, 4, 4, 5, 5, 2,
        };
        private static readonly int[] PyramidTriangles = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1 };
        private static readonly int[] PyramidEdges = { 0, 1, 0, 2, 0, 3, 0, 4, 1, 2, 2, 3, 3, 4, 4, 1 };

        private static readonly int ControlHint = "HonamiBoneHandle".GetHashCode();
        private static readonly Dictionary<HonamiBoneRenderer, BoneCache> Caches = new();
        private static readonly HashSet<Transform> BoneSet = new();
        private static readonly Vector3[] OctahedronPoints = new Vector3[6];
        private static readonly Vector3[] PyramidPoints = new Vector3[5];
        private static readonly Vector3[] TriangleBuffer = new Vector3[3];
        private static readonly Vector3[] QuadBuffer = new Vector3[4];
        private static readonly List<Object> SelectionBuffer = new();

        static HonamiBoneSceneView()
        {
            EditorApplication.hierarchyChanged += Caches.Clear;
            Undo.undoRedoPerformed += Caches.Clear;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var renderers = HonamiBoneRenderer.EditorInstances;
            if (renderers.Count == 0) return;
            if (Tools.viewToolActive && Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;

            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer.Bones.Count == 0) continue;
                DoRenderer(renderer);
            }
        }

        private static void DoRenderer(HonamiBoneRenderer renderer)
        {
            var cache = GetCache(renderer);
            var previousZTest = Handles.zTest;
            Handles.zTest = renderer.XRay ? CompareFunction.Always : CompareFunction.LessEqual;

            for (int i = 0; i < cache.SegmentStarts.Count; i++)
            {
                var start = cache.SegmentStarts[i];
                var end = cache.SegmentEnds[i];
                if (start == null || end == null) continue;
                DoBoneHandle(renderer, start, end.position);
            }

            for (int i = 0; i < cache.Tips.Count; i++)
            {
                var tip = cache.Tips[i];
                if (tip == null) continue;
                DoJointHandle(renderer, tip, TipScreenRatio);
            }

            if (renderer.DrawJoints)
            {
                for (int i = 0; i < cache.SegmentStarts.Count; i++)
                {
                    var start = cache.SegmentStarts[i];
                    if (start == null) continue;
                    DoJointHandle(renderer, start, JointScreenRatio);
                }
            }

            Handles.zTest = previousZTest;
        }

        private static BoneCache GetCache(HonamiBoneRenderer renderer)
        {
            if (Caches.TryGetValue(renderer, out var cache) && cache.Version == renderer.EditorVersion)
                return cache;

            if (cache == null)
            {
                cache = new BoneCache();
                Caches[renderer] = cache;
            }

            cache.Version = renderer.EditorVersion;
            cache.SegmentStarts.Clear();
            cache.SegmentEnds.Clear();
            cache.Tips.Clear();

            BoneSet.Clear();
            var bones = renderer.Bones;
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null) BoneSet.Add(bones[i]);
            }

            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone == null) continue;

                bool hasChildInSet = false;
                for (int c = 0; c < bone.childCount; c++)
                {
                    var child = bone.GetChild(c);
                    if (!BoneSet.Contains(child)) continue;
                    hasChildInSet = true;
                    cache.SegmentStarts.Add(bone);
                    cache.SegmentEnds.Add(child);
                }

                if (!hasChildInSet) cache.Tips.Add(bone);
            }

            BoneSet.Clear();
            return cache;
        }

        private static void DoBoneHandle(HonamiBoneRenderer renderer, Transform bone, Vector3 end)
        {
            int id = GUIUtility.GetControlID(ControlHint, FocusType.Passive);
            Vector3 start = bone.position;
            var eventType = Event.current.GetTypeForControl(id);

            if (eventType == EventType.Repaint)
                DrawBone(renderer, start, end, ResolveColor(renderer, bone, id));
            else if (IsPickLayout(eventType))
                HandleUtility.AddControl(id, HandleUtility.DistanceToLine(start, end) - PickTolerancePixels(renderer, start, end));
            else
                TrySelect(id, bone, eventType);
        }

        private static void DoJointHandle(HonamiBoneRenderer renderer, Transform bone, float screenRatio)
        {
            int id = GUIUtility.GetControlID(ControlHint, FocusType.Passive);
            Vector3 position = bone.position;
            float radius = HandleUtility.GetHandleSize(position) * screenRatio * renderer.JointSize;
            var eventType = Event.current.GetTypeForControl(id);

            if (eventType == EventType.Repaint)
            {
                Handles.color = ResolveColor(renderer, bone, id);
                Handles.SphereHandleCap(id, position, Quaternion.identity, radius * 2f, EventType.Repaint);
            }
            else if (IsPickLayout(eventType))
                HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position, radius));
            else
                TrySelect(id, bone, eventType);
        }

        private static bool IsPickLayout(EventType eventType) => eventType == EventType.Layout || eventType == EventType.MouseMove;

        private static void TrySelect(int id, Transform bone, EventType eventType)
        {
            var evt = Event.current;
            if (eventType != EventType.MouseDown || HandleUtility.nearestControl != id || evt.button != 0 || evt.alt) return;

            SelectBone(bone, evt);
            evt.Use();
        }

        private static float PickTolerancePixels(HonamiBoneRenderer renderer, Vector3 start, Vector3 end)
        {
            Vector3 mid = (start + end) * 0.5f;
            float worldRadius = Vector3.Distance(start, end) * BoneRadiusRatio * renderer.BoneSize;
            float pixels = worldRadius / HandleUtility.GetHandleSize(mid) * HandleReferencePixels;
            return Mathf.Clamp(pixels, MinPickPixels, MaxPickPixels);
        }

        private static Color ResolveColor(HonamiBoneRenderer renderer, Transform bone, int id)
        {
            if (Selection.Contains(bone.gameObject)) return renderer.SelectedColor;
            if (HandleUtility.nearestControl == id && GUIUtility.hotControl == 0) return renderer.HoverColor;
            return renderer.BoneColor;
        }

        private static void SelectBone(Transform bone, Event evt)
        {
            var target = bone.gameObject;

            if (evt.control || evt.command)
            {
                SelectionBuffer.Clear();
                bool removed = false;
                foreach (var selected in Selection.objects)
                {
                    if (selected == target) removed = true;
                    else SelectionBuffer.Add(selected);
                }
                if (!removed) SelectionBuffer.Add(target);
                Selection.objects = SelectionBuffer.ToArray();
                SelectionBuffer.Clear();
            }
            else if (evt.shift)
            {
                if (Selection.Contains(target)) return;
                SelectionBuffer.Clear();
                SelectionBuffer.AddRange(Selection.objects);
                SelectionBuffer.Add(target);
                Selection.objects = SelectionBuffer.ToArray();
                SelectionBuffer.Clear();
            }
            else
            {
                Selection.activeGameObject = target;
            }
        }

        private static void DrawBone(HonamiBoneRenderer renderer, Vector3 start, Vector3 end, Color color)
        {
            switch (renderer.Shape)
            {
                case HonamiBoneRenderer.BoneShape.Line:
                    Handles.color = color;
                    Handles.DrawLine(start, end, 2f);
                    break;
                case HonamiBoneRenderer.BoneShape.Pyramid:
                    DrawPyramid(renderer, start, end, color);
                    break;
                default:
                    DrawOctahedron(renderer, start, end, color);
                    break;
            }
        }

        private static bool TryGetBasis(Vector3 start, Vector3 end, out Vector3 direction, out float length, out Vector3 u, out Vector3 v)
        {
            Vector3 offset = end - start;
            length = offset.magnitude;
            direction = default;
            u = default;
            v = default;
            if (length < 0.0001f) return false;

            direction = offset / length;
            u = Vector3.Cross(direction, Vector3.up);
            if (u.sqrMagnitude < 0.0001f) u = Vector3.Cross(direction, Vector3.right);
            u.Normalize();
            v = Vector3.Cross(direction, u);
            return true;
        }

        private static void DrawOctahedron(HonamiBoneRenderer renderer, Vector3 start, Vector3 end, Color color)
        {
            if (!TryGetBasis(start, end, out var direction, out float length, out var u, out var v)) return;

            float radius = length * BoneRadiusRatio * renderer.BoneSize;
            Vector3 pivot = start + direction * (length * OctahedronPivotRatio);
            OctahedronPoints[0] = start;
            OctahedronPoints[1] = end;
            OctahedronPoints[2] = pivot + u * radius;
            OctahedronPoints[3] = pivot + v * radius;
            OctahedronPoints[4] = pivot - u * radius;
            OctahedronPoints[5] = pivot - v * radius;

            FillTriangles(OctahedronPoints, OctahedronTriangles, color);
            Handles.color = color;
            Handles.DrawLines(OctahedronPoints, OctahedronEdges);
        }

        private static void DrawPyramid(HonamiBoneRenderer renderer, Vector3 start, Vector3 end, Color color)
        {
            if (!TryGetBasis(start, end, out _, out float length, out var u, out var v)) return;

            float radius = length * BoneRadiusRatio * renderer.BoneSize;
            PyramidPoints[0] = end;
            PyramidPoints[1] = start + (u + v) * radius;
            PyramidPoints[2] = start + (u - v) * radius;
            PyramidPoints[3] = start - (u + v) * radius;
            PyramidPoints[4] = start - (u - v) * radius;

            FillTriangles(PyramidPoints, PyramidTriangles, color);
            for (int i = 0; i < QuadBuffer.Length; i++) QuadBuffer[i] = PyramidPoints[i + 1];
            Handles.DrawAAConvexPolygon(QuadBuffer);

            Handles.color = color;
            Handles.DrawLines(PyramidPoints, PyramidEdges);
        }

        private static void FillTriangles(Vector3[] points, int[] triangles, Color color)
        {
            Handles.color = new Color(color.r, color.g, color.b, color.a * FillAlpha);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                TriangleBuffer[0] = points[triangles[i]];
                TriangleBuffer[1] = points[triangles[i + 1]];
                TriangleBuffer[2] = points[triangles[i + 2]];
                Handles.DrawAAConvexPolygon(TriangleBuffer);
            }
        }
    }
}
