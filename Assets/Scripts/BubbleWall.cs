using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// A wall of bubbles that rises from below the screen, thick enough to hide the scene, then clears off the top.
    /// One baked mesh holds every bubble; the vertex shader (DjBubble) lifts them from the wall clock, so the whole thing is
    /// a handful of draw calls and the timing is exact: the screen is covered around SwapAt, which is when the world swaps scenes.
    /// </summary>
    public class BubbleWall : MonoBehaviour
    {
        public const float SwapAt = 2.0f;       // seconds after Play when the screen is fully covered
        public const float Duration = 4.7f;

        Material mat;
        GameObject go;
        float t = -1f;

        public bool Active { get { return t >= 0f; } }
        public float T { get { return t; } }

        public static Mesh SingleBubble(float radius)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            ReefMeshes.Ico(0, verts, tris);
            var v = new Vector3[tris.Count]; var idx = new int[tris.Count];
            for (int i = 0; i < tris.Count; i++) { v[i] = verts[tris[i]] * radius; idx[i] = i; }
            var m = new Mesh { name = "Bubble" };
            m.vertices = v; m.triangles = idx;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Bakes the bubbles for a camera at camPos looking along +z (with half-height factor of its field of view).</summary>
        public void Build(Vector3 camPos, int count)
        {
            var rng = new System.Random(42);
            float F(float lo, float hi) { return lo + (float)rng.NextDouble() * (hi - lo); }

            var ico = new List<Vector3>(); var itris = new List<int>();
            ReefMeshes.Ico(0, ico, itris);
            var V = new List<Vector3>(count * ico.Count);
            var C = new List<Color>(count * ico.Count);
            var U1 = new List<Vector3>(count * ico.Count);
            var U2 = new List<Vector3>(count * ico.Count);
            var I = new List<int>(count * itris.Count);

            for (int b = 0; b < count; b++)
            {
                float d = Mathf.Lerp(12f, 58f, Mathf.Pow((float)rng.NextDouble(), 0.85f));
                float half = 1.46f * d * 1.05f + 8f;
                float x = camPos.x + F(-half, half);
                float z = camPos.z + d;
                float size = F(0.9f, 2.4f) * (0.5f + d / 45f);          // far bubbles bigger so the wall covers evenly
                float spd = F(19f, 25f);
                float y0 = camPos.y - 0.34f * d - size - F(0f, 52f);
                float phase = F(0f, 6.28f);
                float a = F(0.65f, 1f);
                int start = V.Count;
                for (int k = 0; k < ico.Count; k++)
                {
                    V.Add(ico[k]);
                    C.Add(new Color(1f, 1f, 1f, a));
                    U1.Add(new Vector3(x, y0, z));
                    U2.Add(new Vector3(size, spd, phase));
                }
                foreach (var ix in itris) I.Add(start + ix);
            }
            var m = new Mesh { name = "BubbleWall", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(V); m.SetColors(C); m.SetUVs(1, U1); m.SetUVs(2, U2); m.SetTriangles(I, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);

            mat = Mats.Bubble(new Color(0.8f, 1.5f, 1.9f, 1f), 0.5f, true);
            go = new GameObject("BubbleWallMesh");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
        }

        public void Play()
        {
            t = 0f;
            mat.SetFloat("_WallT", 0f);
            go.SetActive(true);
        }

        void Update()
        {
            if (t < 0f) return;
            t += Time.deltaTime;
            mat.SetFloat("_WallT", t);
            if (t > Duration) { t = -1f; go.SetActive(false); }
        }
    }
}
