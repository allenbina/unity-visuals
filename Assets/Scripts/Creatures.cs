using System;
using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// Meshes built by the tools in /tools and loaded from Resources: the underwater creatures (tools/convert_fish.py), the
    /// forest animals (tools/convert_nature.py) and the forest props. Creatures are 2 units long, nose toward +Z, with wag
    /// coordinates in uv0 (see DjFacet.shader); vertex alpha carries leg swing weights for the deer.
    /// </summary>
    public static class Creatures
    {
        [Serializable] class Data { public float[] v, c, a, uv; }

        public class Spec
        {
            public string name;
            public float length;      // world length at scale 1
            public float wagAmp;      // tail swing, in mesh units (the mesh is 2 long)
            public float wagUp;       // 1 = swings up and down (whales, dolphins) instead of side to side
            public float flap;        // wing beat (manta, birds)
            public float legAmp;      // leg swing (deer)
        }

        static readonly Dictionary<string, Spec> specs = new Dictionary<string, Spec>
        {
            { "Fish1",      new Spec { name = "Fish1",      length = 3.4f,  wagAmp = 0.30f } },
            { "Fish2",      new Spec { name = "Fish2",      length = 3.0f,  wagAmp = 0.22f } },
            { "Fish3",      new Spec { name = "Fish3",      length = 2.4f,  wagAmp = 0.30f } },
            { "Tang",       new Spec { name = "Tang",       length = 2.6f,  wagAmp = 0.30f } },
            { "YellowTang", new Spec { name = "YellowTang", length = 2.3f,  wagAmp = 0.30f } },
            { "Needlefish", new Spec { name = "Needlefish", length = 4.6f,  wagAmp = 0.18f } },
            { "Puffer",     new Spec { name = "Puffer",     length = 2.2f,  wagAmp = 0.24f } },
            { "Dolphin",    new Spec { name = "Dolphin",    length = 9f,    wagAmp = 0.22f, wagUp = 1f } },
            { "Shark",      new Spec { name = "Shark",      length = 12f,   wagAmp = 0.22f } },
            { "Whale",      new Spec { name = "Whale",      length = 24f,   wagAmp = 0.16f, wagUp = 1f } },
            { "Mantaray",   new Spec { name = "Mantaray",   length = 11f,   wagAmp = 0.04f, wagUp = 1f, flap = 0.32f } },
            { "Bird",       new Spec { name = "Bird",       length = 0.9f,  flap = 0.55f } },
            { "Deer",       new Spec { name = "Deer",       length = 1.7f,  legAmp = 0.26f } },
        };

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public static Spec SpecOf(string name) { return specs[name]; }

        /// <summary>An underwater creature or forest animal by name.</summary>
        public static Mesh Mesh(string name)
        {
            return Load("Models/Fish/" + name, "Models/Animals/" + name, name);
        }

        /// <summary>A forest prop (Tree1..4, Bush1..3, Grass1..3, Rock1..3). Vertex color: r = part (0 trunk/rock, 1 foliage), g = height.</summary>
        public static Mesh Nature(string name)
        {
            return Load("Models/Nature/" + name, null, name);
        }

        static Mesh Load(string path, string altPath, string key)
        {
            Mesh m;
            if (meshes.TryGetValue(key, out m)) return m;
            var ta = Resources.Load<TextAsset>(path);
            if (ta == null && altPath != null) ta = Resources.Load<TextAsset>(altPath);
            if (ta == null) { Debug.LogError("[DjVisuals] mesh missing: " + path); return null; }
            var d = JsonUtility.FromJson<Data>(ta.text);
            int n = d.v.Length / 3;
            bool hasA = d.a != null && d.a.Length == n, hasUv = d.uv != null && d.uv.Length == n * 2;
            var verts = new Vector3[n]; var cols = new Color[n]; var uv = new Vector2[n]; var idx = new int[n];
            for (int i = 0; i < n; i++)
            {
                verts[i] = new Vector3(d.v[i * 3], d.v[i * 3 + 1], d.v[i * 3 + 2]);
                cols[i] = new Color(d.c[i * 3], d.c[i * 3 + 1], d.c[i * 3 + 2], hasA ? d.a[i] : 1f);
                if (hasUv) uv[i] = new Vector2(d.uv[i * 2], d.uv[i * 2 + 1]);
                idx[i] = i;
            }
            m = new Mesh { name = key, indexFormat = n > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.vertices = verts; m.colors = cols; m.uv = uv; m.triangles = idx;
            m.RecalculateNormals();
            m.RecalculateBounds();
            var b = m.bounds; b.Expand(0.8f); m.bounds = b;   // wag, flap and wind move vertices outside the rest bounds
            meshes[key] = m;
            return m;
        }
    }
}
