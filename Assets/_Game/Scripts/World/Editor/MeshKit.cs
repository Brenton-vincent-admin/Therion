using System.Collections.Generic;
using Dino.World.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dino.World.EditorTools
{
    /// <summary>Small procedural mesh toolkit for the placeholder vegetation and props.</summary>
    public class MeshKit
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<List<int>> _sub = new List<List<int>>();

        public MeshKit(int submeshes = 1)
        {
            for (int i = 0; i < submeshes; i++) _sub.Add(new List<int>());
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.subMeshCount = _sub.Count;
            for (int i = 0; i < _sub.Count; i++) m.SetTriangles(_sub[i], i);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        int Vert(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(p); _n.Add(n.normalized); _uv.Add(uv);
            return _v.Count - 1;
        }

        void Tri(int sub, int a, int b, int c) { _sub[sub].Add(a); _sub[sub].Add(b); _sub[sub].Add(c); }

        /// <summary>Tapered cylinder from 'a' to 'b'.</summary>
        public void Cylinder(int sub, Vector3 a, Vector3 b, float r0, float r1, int sides, bool caps = false)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 fwd = Vector3.Cross(axis, side);
            int start = _v.Count;
            for (int i = 0; i <= sides; i++)
            {
                float t = (float)i / sides * Mathf.PI * 2f;
                Vector3 dir = side * Mathf.Cos(t) + fwd * Mathf.Sin(t);
                Vert(a + dir * r0, dir, new Vector2((float)i / sides, 0));
                Vert(b + dir * r1, dir, new Vector2((float)i / sides, 1));
            }
            for (int i = 0; i < sides; i++)
            {
                int k = start + i * 2;
                Tri(sub, k, k + 3, k + 1);
                Tri(sub, k, k + 2, k + 3);
            }
            if (caps)
            {
                int ca = Vert(a, -axis, new Vector2(0.5f, 0.5f)), cb = Vert(b, axis, new Vector2(0.5f, 0.5f));
                int ra = _v.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float t = (float)i / sides * Mathf.PI * 2f;
                    Vector3 dir = side * Mathf.Cos(t) + fwd * Mathf.Sin(t);
                    Vert(a + dir * r0, -axis, new Vector2(0.5f + Mathf.Cos(t) * 0.5f, 0.5f + Mathf.Sin(t) * 0.5f));
                    Vert(b + dir * r1, axis, new Vector2(0.5f + Mathf.Cos(t) * 0.5f, 0.5f + Mathf.Sin(t) * 0.5f));
                }
                for (int i = 0; i < sides; i++)
                {
                    Tri(sub, ca, ra + (i + 1) * 2, ra + i * 2);
                    Tri(sub, cb, ra + i * 2 + 1, ra + (i + 1) * 2 + 1);
                }
            }
        }

        /// <summary>Cone with its base centred at 'a' and tip at 'tip'.</summary>
        public void Cone(int sub, Vector3 a, Vector3 tip, float r, int sides, float jitter = 0f, int seed = 0)
        {
            Vector3 axis = (tip - a).normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 fwd = Vector3.Cross(axis, side);
            float h = Vector3.Distance(a, tip);
            for (int i = 0; i < sides; i++)
            {
                float t0 = (float)i / sides * Mathf.PI * 2f, t1 = (float)(i + 1) / sides * Mathf.PI * 2f;
                float j0 = 1f + jitter * (IslandNoise.Hash01(i, seed, 3) - 0.5f), j1 = 1f + jitter * (IslandNoise.Hash01((i + 1) % sides, seed, 3) - 0.5f);
                Vector3 d0 = side * Mathf.Cos(t0) + fwd * Mathf.Sin(t0), d1 = side * Mathf.Cos(t1) + fwd * Mathf.Sin(t1);
                Vector3 p0 = a + d0 * r * j0, p1 = a + d1 * r * j1;
                Vector3 n0 = (d0 * h + axis * r).normalized, n1 = (d1 * h + axis * r).normalized;
                int v0 = Vert(p0, n0, new Vector2(0, 0)), v1 = Vert(p1, n1, new Vector2(1, 0)), v2 = Vert(tip, (n0 + n1) * 0.5f, new Vector2(0.5f, 1));
                Tri(sub, v0, v1, v2);
                int c = Vert(a, -axis, new Vector2(0.5f, 0.5f));
                int b0 = Vert(p0, -axis, new Vector2(0, 0)), b1 = Vert(p1, -axis, new Vector2(1, 0));
                Tri(sub, c, b1, b0);
            }
        }

        /// <summary>A lumpy sphere (subdivided octahedron with noise), for canopies, bushes and rocks.</summary>
        public void Blob(int sub, Vector3 center, Vector3 radii, int detail, float lumpiness, int seed)
        {
            var verts = new List<Vector3>
            {
                Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
            };
            var tris = new List<int> { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var cache = new Dictionary<long, int>();
            for (int d = 0; d < detail; d++)
            {
                var next = new List<int>();
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    int ab = Mid(verts, cache, a, b), bc = Mid(verts, cache, b, c), ca = Mid(verts, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
            int start = _v.Count;
            foreach (var p in verts)
            {
                float n = IslandNoise.Fbm(p.x * 1.7f + seed * 3.1f, p.z * 1.7f + p.y * 2.3f, seed, 3);
                float k = 1f + lumpiness * (n - 0.5f) * 2f;
                Vector3 q = Vector3.Scale(p * k, radii);
                Vert(center + q, new Vector3(p.x / radii.x, p.y / radii.y, p.z / radii.z), new Vector2(p.x * 0.5f + 0.5f, p.y * 0.5f + 0.5f));
            }
            for (int t = 0; t < tris.Count; t += 3) Tri(sub, start + tris[t], start + tris[t + 1], start + tris[t + 2]);
        }

        static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int i)) return i;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }

        /// <summary>A double-sided curved blade/frond quad strip from 'root' along 'dir', bending downward.</summary>
        public void Frond(int sub, Vector3 root, Vector3 dir, float length, float width, float droop, int segments = 4)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * width * 0.5f;
            int start = _v.Count;
            for (int s = 0; s <= segments; s++)
            {
                float t = (float)s / segments;
                Vector3 p = root + dir * (length * t) + Vector3.down * (droop * t * t);
                float w = Mathf.Sin(Mathf.Clamp01(t * 1.1f) * Mathf.PI) * 0.9f + 0.1f;
                Vector3 up = Vector3.Cross(dir, side).normalized;
                if (up.y < 0) up = -up;
                Vert(p - side * w, up, new Vector2(0, t));
                Vert(p + side * w, up, new Vector2(1, t));
            }
            for (int s = 0; s < segments; s++)
            {
                int k = start + s * 2;
                Tri(sub, k, k + 2, k + 1); Tri(sub, k + 1, k + 2, k + 3);
                Tri(sub, k, k + 1, k + 2); Tri(sub, k + 1, k + 3, k + 2); // back face
            }
        }

        /// <summary>Vertical double-sided quad (grass card).</summary>
        public void Card(int sub, Vector3 center, float yaw, float width, float height)
        {
            Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right * (width * 0.5f);
            Vector3 n = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            Vector3 up = Vector3.up * height;
            int a = Vert(center - right, Vector3.up, new Vector2(0, 0));
            int b = Vert(center + right, Vector3.up, new Vector2(1, 0));
            int c = Vert(center + right + up, Vector3.up + n * 0.2f, new Vector2(1, 1));
            int d = Vert(center - right + up, Vector3.up + n * 0.2f, new Vector2(0, 1));
            Tri(sub, a, d, c); Tri(sub, a, c, b);
            Tri(sub, a, c, d); Tri(sub, a, b, c);
        }
    }
}
