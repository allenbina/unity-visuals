using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// Faceted reef pieces built in code: branching coral, brain domes, table coral, sea fans, tube coral and rocks.
    /// Few segments, no shared normals (the shaders flat-shade from derivatives), a gray-scale gradient in the vertex color
    /// (dark at the base, light at the tips) that the material tint colors. Meshes are cached and shared between scenes.
    /// </summary>
    public static class ReefMeshes
    {
        public const int Branch = 0, Dome = 1, Table = 2, Fan = 3, Tube = 4, TypeCount = 5;
        const int Variants = 3;
        static Mesh[,] coral;
        static Mesh[] rocks;

        public static Mesh Coral(int type, int variant)
        {
            if (coral == null) coral = new Mesh[TypeCount, Variants];
            variant = Mathf.Abs(variant) % Variants;
            if (coral[type, variant] == null)
            {
                var rng = new System.Random(type * 101 + variant * 17 + 3);
                var fb = new FB();
                switch (type)
                {
                    case Branch: MakeBranch(fb, rng); break;
                    case Dome: MakeDome(fb, rng); break;
                    case Table: MakeTable(fb, rng); break;
                    case Fan: MakeFan(fb, rng); break;
                    default: MakeTubes(fb, rng); break;
                }
                coral[type, variant] = fb.ToMesh("Coral" + type + "_" + variant);
            }
            return coral[type, variant];
        }

        public static Mesh Rock(int variant)
        {
            if (rocks == null) rocks = new Mesh[Variants];
            variant = Mathf.Abs(variant) % Variants;
            if (rocks[variant] == null)
            {
                var rng = new System.Random(900 + variant);
                var fb = new FB();
                Blob(fb, rng, 1, 1f, 0.65f, 1.15f, 0.35f, -0.25f, 0.5f, 1f);
                rocks[variant] = fb.ToMesh("Rock" + variant);
            }
            return rocks[variant];
        }

        // ---------------- builder ----------------

        class FB
        {
            public List<Vector3> v = new List<Vector3>();
            public List<Color> c = new List<Color>();
            public void Tri(Vector3 a, Vector3 b, Vector3 d, float ga, float gb, float gd)
            {
                v.Add(a); v.Add(b); v.Add(d);
                c.Add(new Color(ga, ga, ga, 1f)); c.Add(new Color(gb, gb, gb, 1f)); c.Add(new Color(gd, gd, gd, 1f));
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, float ga, float gb, float gd, float ge)
            {
                Tri(a, b, d, ga, gb, gd); Tri(a, d, e, ga, gd, ge);
            }
            public Mesh ToMesh(string name)
            {
                var idx = new int[v.Count];
                for (int i = 0; i < idx.Length; i++) idx[i] = i;
                var m = new Mesh { name = name };
                m.SetVertices(v); m.SetColors(c); m.SetTriangles(idx, 0);
                var uv = new Vector2[v.Count];
                m.uv = uv;
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static float F(System.Random r, float lo, float hi) { return lo + (float)r.NextDouble() * (hi - lo); }
        static float Shade(float y, float top) { return 0.55f + 0.5f * Mathf.Clamp01(y / top); }

        static void Basis(Vector3 dir, out Vector3 u, out Vector3 w)
        {
            u = Vector3.Cross(dir, Vector3.right);
            if (u.sqrMagnitude < 0.01f) u = Vector3.Cross(dir, Vector3.forward);
            u.Normalize();
            w = Vector3.Cross(dir, u).normalized;
        }

        // ---------------- branching coral: a tree of tapered 5-sided limbs ----------------

        static void Limb(FB fb, System.Random rng, Vector3 p, Vector3 dir, float len, float r0, float r1, int depth, float top)
        {
            Vector3 u, w; Basis(dir, out u, out w);
            const int sides = 5;
            var end = p + dir * len;
            for (int k = 0; k < sides; k++)
            {
                float a0 = Mathf.PI * 2f * k / sides, a1 = Mathf.PI * 2f * (k + 1) / sides;
                var p0 = p + (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * r0;
                var p1 = p + (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * r0;
                var q0 = end + (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * r1;
                var q1 = end + (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * r1;
                fb.Quad(p0, p1, q1, q0, Shade(p.y, top), Shade(p.y, top), Shade(end.y, top), Shade(end.y, top));
                if (depth == 0) fb.Tri(q0, q1, end + dir * r1 * 1.8f, Shade(end.y, top), Shade(end.y, top), Shade(end.y + 0.4f, top));
            }
            if (depth > 0)
            {
                int kids = rng.Next(2, 4);
                for (int i = 0; i < kids; i++)
                {
                    float ang = Mathf.PI * 2f * i / kids + F(rng, 0f, 1f);
                    var side = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                    var nd = (dir * 1.0f + side * F(rng, 0.45f, 0.85f) + Vector3.up * 0.35f).normalized;
                    Limb(fb, rng, end, nd, len * F(rng, 0.7f, 0.85f), r1, r1 * 0.6f, depth - 1, top);
                }
            }
        }

        static void MakeBranch(FB fb, System.Random rng)
        {
            Limb(fb, rng, new Vector3(0f, -0.2f, 0f), Vector3.up, F(rng, 1.2f, 1.6f), 0.5f, 0.34f, 2, 4.5f);
        }

        // ---------------- blobs: domes and rocks from a subdivided icosahedron ----------------

        public static void Ico(int subdiv, List<Vector3> verts, List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v0 = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            foreach (var v in v0) verts.Add(v.normalized);
            int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            tris.AddRange(f);
            for (int s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int r;
                    if (cache.TryGetValue(key, out r)) return r;
                    verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                    cache[key] = verts.Count - 1;
                    return verts.Count - 1;
                }
                var nt = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris.Clear(); tris.AddRange(nt);
            }
        }

        /// <summary>A lumpy blob, flattened and cut at yCut; radius noise makes it faceted and uneven. offsetY lifts it.</summary>
        static void Blob(FB fb, System.Random rng, int subdiv, float rx, float ry, float rz, float lump, float yCut, float lo, float hi)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            Ico(subdiv, verts, tris);
            var p = new Vector3[verts.Count];
            for (int i = 0; i < p.Length; i++)
            {
                float k = 1f + F(rng, -lump, lump) * 0.8f;
                p[i] = new Vector3(verts[i].x * rx * k, verts[i].y * ry * k, verts[i].z * rz * k);
            }
            for (int i = 0; i < tris.Count; i += 3)
            {
                var a = p[tris[i]]; var b = p[tris[i + 1]]; var c = p[tris[i + 2]];
                if ((a.y + b.y + c.y) / 3f < yCut) continue;
                float ga = Mathf.Lerp(lo, hi, Mathf.InverseLerp(-ry, ry, a.y));
                float gb = Mathf.Lerp(lo, hi, Mathf.InverseLerp(-ry, ry, b.y));
                float gc = Mathf.Lerp(lo, hi, Mathf.InverseLerp(-ry, ry, c.y));
                fb.Tri(a, b, c, ga, gb, gc);
            }
        }

        static void MakeDome(FB fb, System.Random rng)
        {
            Blob(fb, rng, 1, 1.5f, 1.0f, 1.5f, 0.22f, -0.1f, 0.55f, 1.05f);
        }

        // ---------------- table coral: a stalk and a wide plate ----------------

        static void MakeTable(FB fb, System.Random rng)
        {
            float h = F(rng, 1.1f, 1.7f);
            Limb(fb, rng, new Vector3(0f, -0.2f, 0f), Vector3.up, h, 0.5f, 0.38f, 0, 4f);
            const int n = 8;
            float R = F(rng, 2.2f, 2.8f);
            var inner = new Vector3[n]; var outer = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n + F(rng, -0.12f, 0.12f);
                float ro = R * F(rng, 0.85f, 1.12f);
                inner[i] = new Vector3(Mathf.Cos(a) * R * 0.5f, h + 0.05f, Mathf.Sin(a) * R * 0.5f);
                outer[i] = new Vector3(Mathf.Cos(a) * ro, h - F(rng, 0.2f, 0.4f), Mathf.Sin(a) * ro);
            }
            var top = new Vector3(0f, h + 0.28f, 0f);
            var under = new Vector3(0f, h - 0.12f, 0f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                fb.Tri(top, inner[i], inner[j], 1.05f, 0.95f, 0.95f);            // top
                fb.Quad(inner[i], outer[i], outer[j], inner[j], 0.95f, 0.8f, 0.8f, 0.95f);   // sloping rim
                fb.Tri(under, outer[j], outer[i], 0.5f, 0.55f, 0.55f);           // underside
            }
        }

        // ---------------- sea fan: a thin wavy plate on a short stalk ----------------

        static void MakeFan(FB fb, System.Random rng)
        {
            float h = F(rng, 0.7f, 1.0f);
            Limb(fb, rng, new Vector3(0f, -0.2f, 0f), Vector3.up, h, 0.3f, 0.2f, 0, 4f);
            const int n = 9;
            var inner = new Vector3[n]; var outer = new Vector3[n];
            float R = F(rng, 2.3f, 3.0f);
            for (int i = 0; i < n; i++)
            {
                float t = (i / (float)(n - 1) - 0.5f) * 2f;      // -1..1
                float ang = t * 1.15f;                           // fan opening about 130 degrees
                float ro = R * (i % 2 == 0 ? F(rng, 0.9f, 1.05f) : F(rng, 0.65f, 0.85f));
                float wave = Mathf.Sin(t * 3.2f) * 0.35f;
                inner[i] = new Vector3(Mathf.Sin(ang) * R * 0.45f, h + Mathf.Cos(ang) * R * 0.45f, wave * 0.5f);
                outer[i] = new Vector3(Mathf.Sin(ang) * ro, h + Mathf.Cos(ang) * ro, wave);
            }
            var hub = new Vector3(0f, h, 0f);
            for (int i = 0; i < n - 1; i++)
            {
                fb.Tri(hub, inner[i], inner[i + 1], 0.6f, 0.8f, 0.8f);
                fb.Quad(inner[i], outer[i], outer[i + 1], inner[i + 1], 0.8f, 1.05f, 1.05f, 0.8f);
            }
        }

        // ---------------- tube coral: a cluster of open tubes ----------------

        static void MakeTubes(FB fb, System.Random rng)
        {
            int tubes = rng.Next(5, 8);
            for (int t = 0; t < tubes; t++)
            {
                float a = F(rng, 0f, Mathf.PI * 2f), d = F(rng, 0f, 0.9f);
                var b = new Vector3(Mathf.Cos(a) * d, -0.2f, Mathf.Sin(a) * d);
                float h = F(rng, 1.3f, 3.2f), r = F(rng, 0.2f, 0.34f);
                const int sides = 6;
                var topC = b + Vector3.up * h;
                for (int k = 0; k < sides; k++)
                {
                    float a0 = Mathf.PI * 2f * k / sides, a1 = Mathf.PI * 2f * (k + 1) / sides;
                    var p0 = b + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r * 1.2f;
                    var p1 = b + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r * 1.2f;
                    var q0 = topC + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r;
                    var q1 = topC + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
                    fb.Quad(p0, p1, q1, q0, 0.55f, 0.55f, Shade(h, 3.4f), Shade(h, 3.4f));
                    var i0 = topC + new Vector3(Mathf.Cos(a0), -0.3f, Mathf.Sin(a0)) * r * 0.55f;
                    var i1 = topC + new Vector3(Mathf.Cos(a1), -0.3f, Mathf.Sin(a1)) * r * 0.55f;
                    fb.Quad(q0, q1, i1, i0, 1.1f, 1.1f, 0.35f, 0.35f);                         // lip into the dark mouth
                    fb.Tri(i0, i1, topC + Vector3.down * 0.9f, 0.3f, 0.3f, 0.1f);
                }
            }
        }
    }
}
