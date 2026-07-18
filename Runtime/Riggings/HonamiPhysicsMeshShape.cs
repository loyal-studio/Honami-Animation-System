using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    public sealed class HonamiPhysicsMeshShape
    {
        private struct Node
        {
            public Vector3 boundsMin;
            public Vector3 boundsMax;
            public int left;
            public int right;
            public int triStart;
            public int triCount;
        }

        private const int LeafTriangleCount = 8;

        private Vector3[] _a;
        private Vector3[] _b;
        private Vector3[] _c;
        private Node[] _nodes;
        private readonly int[] _stack = new int[128];

        public float BoundsSize { get; private set; }

        public static HonamiPhysicsMeshShape Build(Mesh mesh, Vector3 scale)
            => Build(mesh.vertices, mesh.triangles, scale);

        public static HonamiPhysicsMeshShape Build(Vector3[] vertices, int[] triangles, Vector3 scale)
        {
            if (vertices == null || triangles == null) return null;
            int triCount = triangles.Length / 3;
            if (triCount == 0) return null;

            var ta = new Vector3[triCount];
            var tb = new Vector3[triCount];
            var tc = new Vector3[triCount];
            int valid = 0;
            for (int t = 0; t < triCount; t++)
            {
                Vector3 a = Vector3.Scale(vertices[triangles[t * 3]], scale);
                Vector3 b = Vector3.Scale(vertices[triangles[t * 3 + 1]], scale);
                Vector3 c = Vector3.Scale(vertices[triangles[t * 3 + 2]], scale);
                // a degenerate triangle breaks the barycentric solve in ClosestPointOnTriangle
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-14f) continue;
                ta[valid] = a;
                tb[valid] = b;
                tc[valid] = c;
                valid++;
            }
            if (valid == 0) return null;

            var shape = new HonamiPhysicsMeshShape();
            shape.BuildTree(ta, tb, tc, valid);
            return shape;
        }

        private void BuildTree(Vector3[] ta, Vector3[] tb, Vector3[] tc, int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            var keys = new float[count];

            int capacity = 2 * ((count + LeafTriangleCount - 1) / LeafTriangleCount) + 1;
            _nodes = new Node[Mathf.Max(capacity, 3)];
            int nodeCount = 0;

            BuildNode(ta, tb, tc, order, keys, 0, count, ref nodeCount);
            Array.Resize(ref _nodes, nodeCount);

            _a = new Vector3[count];
            _b = new Vector3[count];
            _c = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                _a[i] = ta[order[i]];
                _b[i] = tb[order[i]];
                _c[i] = tc[order[i]];
            }

            BoundsSize = (_nodes[0].boundsMax - _nodes[0].boundsMin).magnitude;
        }

        private int BuildNode(Vector3[] ta, Vector3[] tb, Vector3[] tc, int[] order, float[] keys,
            int start, int count, ref int nodeCount)
        {
            int index = nodeCount++;
            if (index >= _nodes.Length) Array.Resize(ref _nodes, _nodes.Length * 2);

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 cMin = min;
            Vector3 cMax = max;
            for (int i = start; i < start + count; i++)
            {
                int t = order[i];
                min = Vector3.Min(min, Vector3.Min(ta[t], Vector3.Min(tb[t], tc[t])));
                max = Vector3.Max(max, Vector3.Max(ta[t], Vector3.Max(tb[t], tc[t])));
                Vector3 centroid = (ta[t] + tb[t] + tc[t]) / 3f;
                cMin = Vector3.Min(cMin, centroid);
                cMax = Vector3.Max(cMax, centroid);
            }

            if (count <= LeafTriangleCount)
            {
                _nodes[index] = new Node { boundsMin = min, boundsMax = max, left = -1, right = -1, triStart = start, triCount = count };
                return index;
            }

            Vector3 cSize = cMax - cMin;
            int axis = cSize.x >= cSize.y ? (cSize.x >= cSize.z ? 0 : 2) : (cSize.y >= cSize.z ? 1 : 2);
            for (int i = start; i < start + count; i++)
            {
                int t = order[i];
                Vector3 centroid = (ta[t] + tb[t] + tc[t]) / 3f;
                keys[i] = axis == 0 ? centroid.x : (axis == 1 ? centroid.y : centroid.z);
            }
            Array.Sort(keys, order, start, count);

            int mid = count / 2;
            int left = BuildNode(ta, tb, tc, order, keys, start, mid, ref nodeCount);
            int right = BuildNode(ta, tb, tc, order, keys, start + mid, count - mid, ref nodeCount);
            _nodes[index] = new Node { boundsMin = min, boundsMax = max, left = left, right = right, triStart = 0, triCount = 0 };
            return index;
        }

        public Vector3 ClosestPoint(Vector3 point)
        {
            float bestSq = float.MaxValue;
            Vector3 best = point;

            int sp = 0;
            _stack[sp++] = 0;
            while (sp > 0)
            {
                ref Node node = ref _nodes[_stack[--sp]];
                if (SqDistToBounds(point, node.boundsMin, node.boundsMax) >= bestSq) continue;

                if (node.left < 0)
                {
                    for (int i = 0; i < node.triCount; i++)
                    {
                        int t = node.triStart + i;
                        Vector3 q = ClosestPointOnTriangle(point, _a[t], _b[t], _c[t]);
                        float d = (q - point).sqrMagnitude;
                        if (d < bestSq)
                        {
                            bestSq = d;
                            best = q;
                        }
                    }
                }
                else
                {
                    float dl = SqDistToBounds(point, _nodes[node.left].boundsMin, _nodes[node.left].boundsMax);
                    float dr = SqDistToBounds(point, _nodes[node.right].boundsMin, _nodes[node.right].boundsMax);
                    // push the farther child first so the nearer one shrinks bestSq before it is tested
                    if (dl < dr)
                    {
                        _stack[sp++] = node.right;
                        _stack[sp++] = node.left;
                    }
                    else
                    {
                        _stack[sp++] = node.left;
                        _stack[sp++] = node.right;
                    }
                }
            }

            return best;
        }

        private static float SqDistToBounds(Vector3 p, Vector3 min, Vector3 max)
        {
            float dx = p.x < min.x ? min.x - p.x : (p.x > max.x ? p.x - max.x : 0f);
            float dy = p.y < min.y ? min.y - p.y : (p.y > max.y ? p.y - max.y : 0f);
            float dz = p.z < min.z ? min.z - p.z : (p.z > max.z ? p.z - max.z : 0f);
            return dx * dx + dy * dy + dz * dz;
        }

        private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = p - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));

            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }
    }
}
