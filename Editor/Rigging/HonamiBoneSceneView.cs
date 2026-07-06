using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Rigging
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

        private static readonly int ControlHint = "HonamiBoneHandle".GetHashCode();
        private static readonly List<HonamiBoneRenderer> Renderers = new();
        private static readonly Dictionary<HonamiBoneRenderer, BoneCache> Caches = new();
        private static readonly HashSet<Transform> BoneSet = new();
        private static readonly Vector3[] TriangleBuffer = new Vector3[3];
        private static readonly Vector3[] QuadBuffer = new Vector3[4];
        private static readonly List<Object> SelectionBuffer = new();

        private static bool _rescanPending;

        static HonamiBoneSceneView()
        {
            HonamiBoneRenderer.EditorEnabled += OnRendererEnabled;
            HonamiBoneRenderer.EditorDisabled += OnRendererDisabled;
            EditorApplication.hierarchyChanged += InvalidateCaches;
            Undo.undoRedoPerformed += InvalidateCaches;
            SceneView.duringSceneGui += OnSceneGUI;
            _rescanPending = true;
        }

        private static void OnRendererEnabled(HonamiBoneRenderer renderer)
        {
            if (!Renderers.Contains(renderer)) Renderers.Add(renderer);
            SceneView.RepaintAll();
        }

        private static void OnRendererDisabled(HonamiBoneRenderer renderer)
        {
            Renderers.Remove(renderer);
            Caches.Remove(renderer);
            SceneView.RepaintAll();
        }

        private static void InvalidateCaches()
        {
            Caches.Clear();
            _rescanPending = true;
        }

        private static void RescanIfPending()
        {
            if (!_rescanPending) return;
            _rescanPending = false;

            var found = Object.FindObjectsByType<HonamiBoneRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i].isActiveAndEnabled && !Renderers.Contains(found[i])) Renderers.Add(found[i]);
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            RescanIfPending();
            if (Renderers.Count == 0) return;
            if (Tools.viewToolActive && Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;

            for (int i = Renderers.Count - 1; i >= 0; i--)
            {
                var renderer = Renderers[i];
                if (renderer == null)
                {
                    Renderers.RemoveAt(i);
                    continue;
                }
                if (!renderer.isActiveAndEnabled || renderer.Bones.Count == 0) continue;

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

        private static void DoBoneHandle(HonamiBoneRenderer renderer, Transform bone, Vector3 endPosition)
        {
            int id = GUIUtility.GetControlID(ControlHint, FocusType.Passive);
            var evt = Event.current;
            Vector3 start = bone.position;

            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                case EventType.MouseMove:
                    HandleUtility.AddControl(id, HandleUtility.DistanceToLine(start, endPosition) - PickTolerancePixels(renderer, start, endPosition));
                    break;

                case EventType.MouseDown:
                    if (HandleUtility.nearestControl == id && evt.button == 0 && !evt.alt)
                    {
                        SelectBone(bone, evt);
                        evt.Use();
                    }
                    break;

                case EventType.Repaint:
                    DrawBone(renderer, start, endPosition, ResolveColor(renderer, bone, id));
                    break;
            }
        }

        private static void DoJointHandle(HonamiBoneRenderer renderer, Transform bone, float screenRatio)
        {
            int id = GUIUtility.GetControlID(ControlHint, FocusType.Passive);
            var evt = Event.current;
            Vector3 position = bone.position;
            float radius = HandleUtility.GetHandleSize(position) * screenRatio * renderer.JointSize;

            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                case EventType.MouseMove:
                    HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position, radius));
                    break;

                case EventType.MouseDown:
                    if (HandleUtility.nearestControl == id && evt.button == 0 && !evt.alt)
                    {
                        SelectBone(bone, evt);
                        evt.Use();
                    }
                    break;

                case EventType.Repaint:
                    Handles.color = ResolveColor(renderer, bone, id);
                    Handles.SphereHandleCap(id, position, Quaternion.identity, radius * 2f, EventType.Repaint);
                    break;
            }
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
            if (evt.control || evt.command)
            {
                SelectionBuffer.Clear();
                var current = Selection.objects;
                bool removed = false;
                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i] == bone.gameObject) { removed = true; continue; }
                    SelectionBuffer.Add(current[i]);
                }
                if (!removed) SelectionBuffer.Add(bone.gameObject);
                Selection.objects = SelectionBuffer.ToArray();
                SelectionBuffer.Clear();
            }
            else if (evt.shift)
            {
                if (!Selection.Contains(bone.gameObject))
                {
                    SelectionBuffer.Clear();
                    SelectionBuffer.AddRange(Selection.objects);
                    SelectionBuffer.Add(bone.gameObject);
                    Selection.objects = SelectionBuffer.ToArray();
                    SelectionBuffer.Clear();
                }
            }
            else
            {
                Selection.activeGameObject = bone.gameObject;
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

        private static void GetPerpendicularAxes(Vector3 direction, out Vector3 u, out Vector3 v)
        {
            u = Vector3.Cross(direction, Vector3.up);
            if (u.sqrMagnitude < 0.0001f) u = Vector3.Cross(direction, Vector3.right);
            u.Normalize();
            v = Vector3.Cross(direction, u);
        }

        private static void DrawOctahedron(HonamiBoneRenderer renderer, Vector3 start, Vector3 end, Color color)
        {
            Vector3 offset = end - start;
            float length = offset.magnitude;
            if (length < 0.0001f) return;

            Vector3 direction = offset / length;
            GetPerpendicularAxes(direction, out var u, out var v);

            float radius = length * BoneRadiusRatio * renderer.BoneSize;
            Vector3 pivot = start + direction * (length * OctahedronPivotRatio);
            Vector3 m0 = pivot + u * radius;
            Vector3 m1 = pivot + v * radius;
            Vector3 m2 = pivot - u * radius;
            Vector3 m3 = pivot - v * radius;

            var fillColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
            Handles.color = fillColor;
            FillTriangle(start, m0, m1);
            FillTriangle(start, m1, m2);
            FillTriangle(start, m2, m3);
            FillTriangle(start, m3, m0);
            FillTriangle(end, m0, m1);
            FillTriangle(end, m1, m2);
            FillTriangle(end, m2, m3);
            FillTriangle(end, m3, m0);

            Handles.color = color;
            Handles.DrawLine(start, m0);
            Handles.DrawLine(start, m1);
            Handles.DrawLine(start, m2);
            Handles.DrawLine(start, m3);
            Handles.DrawLine(end, m0);
            Handles.DrawLine(end, m1);
            Handles.DrawLine(end, m2);
            Handles.DrawLine(end, m3);
            Handles.DrawLine(m0, m1);
            Handles.DrawLine(m1, m2);
            Handles.DrawLine(m2, m3);
            Handles.DrawLine(m3, m0);
        }

        private static void DrawPyramid(HonamiBoneRenderer renderer, Vector3 start, Vector3 end, Color color)
        {
            Vector3 offset = end - start;
            float length = offset.magnitude;
            if (length < 0.0001f) return;

            Vector3 direction = offset / length;
            GetPerpendicularAxes(direction, out var u, out var v);

            float radius = length * BoneRadiusRatio * renderer.BoneSize;
            Vector3 b0 = start + (u + v) * radius;
            Vector3 b1 = start + (u - v) * radius;
            Vector3 b2 = start - (u + v) * radius;
            Vector3 b3 = start - (u - v) * radius;

            var fillColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
            Handles.color = fillColor;
            FillTriangle(end, b0, b1);
            FillTriangle(end, b1, b2);
            FillTriangle(end, b2, b3);
            FillTriangle(end, b3, b0);
            QuadBuffer[0] = b0; QuadBuffer[1] = b1; QuadBuffer[2] = b2; QuadBuffer[3] = b3;
            Handles.DrawAAConvexPolygon(QuadBuffer);

            Handles.color = color;
            Handles.DrawLine(end, b0);
            Handles.DrawLine(end, b1);
            Handles.DrawLine(end, b2);
            Handles.DrawLine(end, b3);
            Handles.DrawLine(b0, b1);
            Handles.DrawLine(b1, b2);
            Handles.DrawLine(b2, b3);
            Handles.DrawLine(b3, b0);
        }

        private static void FillTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            TriangleBuffer[0] = a;
            TriangleBuffer[1] = b;
            TriangleBuffer[2] = c;
            Handles.DrawAAConvexPolygon(TriangleBuffer);
        }
    }
}
