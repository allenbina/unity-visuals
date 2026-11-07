using System;
using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// The underwater creatures: meshes built by tools/convert_fish.py (the CC0 Quaternius models plus a few more reef fish in
    /// the same faceted style), loaded from Resources/Models/Fish. Each mesh is 2 units long, nose toward +Z, with the wag
    /// coordinates in uv0 (see DjFacet.shader).
    /// </summary>
    public static class Creatures
    {
        [Serializable] class Data { public float[] v, c, uv; }

        public class Spec
        {
            public string name;
            public float length;      // world length at scale 1
            public float wagAmp;      // tail swing, in mesh units (the mesh is 2 long)
            public float wagUp;       // 1 = swings up and down (whales, dolphins) instead of side to side
            public float flap;        // wing beat (manta)
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
        };

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public static Spec SpecOf(string name) { return specs[name]; }

        public static Mesh Mesh(string name)
        {
            Mesh m;
            if (meshes.TryGetValue(name, out m)) return m;
            var ta = Resources.Load<TextAsset>("Models/Fish/" + name);
            if (ta == null) { Debug.LogError("[DjVisuals] creature mesh missing: " + name); return null; }
            var d = JsonUtility.FromJson<Data>(ta.text);
            int n = d.v.Length / 3;
            var verts = new Vector3[n]; var cols = new Color[n]; var uv = new Vector2[n]; var idx = new int[n];
            for (int i = 0; i < n; i++)
            {
                verts[i] = new Vector3(d.v[i * 3], d.v[i * 3 + 1], d.v[i * 3 + 2]);
                cols[i] = new Color(d.c[i * 3], d.c[i * 3 + 1], d.c[i * 3 + 2], 1f);
                uv[i] = new Vector2(d.uv[i * 2], d.uv[i * 2 + 1]);
                idx[i] = i;
            }
            m = new Mesh { name = name, indexFormat = n > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.vertices = verts; m.colors = cols; m.uv = uv; m.triangles = idx;
            m.RecalculateNormals();
            m.RecalculateBounds();
            var b = m.bounds; b.Expand(0.8f); m.bounds = b;   // the wag moves vertices outside the rest bounds
            meshes[name] = m;
            return m;
        }
    }
}
