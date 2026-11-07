using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// Forest world: a first-person walk down a winding path through an endless faceted forest.
    ///   The forest is generated in chunks ahead of you and thrown away behind you (Quaternius nature pack, CC0, plus a few
    ///   animals built in code). Mist hides the pop-in. Your steps bob on the beat.
    ///   Beat   -> the walk bobs and rolls on the beat, the foliage and the light pulse gently
    ///   Accent -> the birds on the treetops burst into the air and scatter to new perches; the view glances to the side
    ///   Build  -> the pace quickens, more motes in the air, stronger light shafts
    ///   Surge  -> (W) the build-up: you look up toward the sun, it swells, the flare and the anamorphic streak grow, the light
    ///             blows out. Holds until you drop.
    ///   Drop   -> the release: a white-out and the forest changes season (summer, autumn, dusk, winter), and you look back down.
    ///   Reset  -> summer again
    /// Deer wander through the mist in the distance and birds land on the treetops.
    /// </summary>
    public class ForestWorld : MonoBehaviour
    {
        const float ChunkLen = 26f;
        const int Ahead = 5, Behind = 1;
        const float HalfW = 78f;
        static readonly Vector3 SunDir = new Vector3(0.12f, 0.47f, 0.87f);   // toward the sun, in front and a bit to the right

        VisualActions actions;
        Camera cam;
        PostFx fx;

        Material skyMat, treeMat, bushMat, grassMat, rockMat, groundMat, critterMat;
        Light sun;

        // ---------------- seasons ----------------
        class Season
        {
            public Color fog; public float fogDensity;
            public Color skyTop, skyMid;
            public Color sunColor; public float sunInt;
            public Color ambient;
            public Color trunk, leaf, bush, grass, moss, dirt, rock;
            public Color mote; public float moteFall, moteSize;
            public Color flare;
        }
        static readonly Season[] Seasons =
        {
            new Season   // summer
            {
                fog = new Color(0.62f, 0.78f, 0.80f), fogDensity = 0.0135f, skyTop = new Color(0.28f, 0.52f, 0.92f), skyMid = new Color(0.55f, 0.75f, 0.85f),
                sunColor = new Color(1f, 0.93f, 0.75f), sunInt = 1.5f, ambient = new Color(0.28f, 0.38f, 0.34f),
                trunk = new Color(0.2f, 0.12f, 0.07f), leaf = new Color(0.1f, 0.4f, 0.07f), bush = new Color(0.18f, 0.5f, 0.1f), grass = new Color(0.25f, 0.55f, 0.12f),
                moss = new Color(0.12f, 0.3f, 0.06f), dirt = new Color(0.3f, 0.2f, 0.1f), rock = new Color(0.34f, 0.34f, 0.36f),
                mote = new Color(1.3f, 1.2f, 0.7f, 1f), moteFall = 0.15f, moteSize = 0.09f, flare = new Color(1.4f, 1.1f, 0.6f),
            },
            new Season   // autumn
            {
                fog = new Color(0.85f, 0.62f, 0.38f), fogDensity = 0.0145f, skyTop = new Color(0.4f, 0.5f, 0.78f), skyMid = new Color(0.9f, 0.62f, 0.42f),
                sunColor = new Color(1f, 0.72f, 0.4f), sunInt = 1.6f, ambient = new Color(0.4f, 0.3f, 0.22f),
                trunk = new Color(0.22f, 0.12f, 0.06f), leaf = new Color(0.72f, 0.26f, 0.04f), bush = new Color(0.62f, 0.34f, 0.05f), grass = new Color(0.5f, 0.38f, 0.08f),
                moss = new Color(0.33f, 0.22f, 0.05f), dirt = new Color(0.35f, 0.18f, 0.08f), rock = new Color(0.36f, 0.3f, 0.26f),
                mote = new Color(1.5f, 0.8f, 0.3f, 1f), moteFall = -0.6f, moteSize = 0.16f, flare = new Color(1.5f, 0.8f, 0.35f),
            },
            new Season   // dusk
            {
                fog = new Color(0.42f, 0.28f, 0.52f), fogDensity = 0.0125f, skyTop = new Color(0.08f, 0.08f, 0.28f), skyMid = new Color(0.5f, 0.28f, 0.5f),
                sunColor = new Color(1f, 0.45f, 0.5f), sunInt = 1.15f, ambient = new Color(0.2f, 0.17f, 0.34f),
                trunk = new Color(0.1f, 0.07f, 0.1f), leaf = new Color(0.08f, 0.14f, 0.26f), bush = new Color(0.14f, 0.1f, 0.3f), grass = new Color(0.1f, 0.2f, 0.3f),
                moss = new Color(0.06f, 0.1f, 0.18f), dirt = new Color(0.2f, 0.12f, 0.2f), rock = new Color(0.22f, 0.2f, 0.3f),
                mote = new Color(1.0f, 2.0f, 0.5f, 1f), moteFall = 0.05f, moteSize = 0.22f, flare = new Color(1.5f, 0.5f, 0.7f),
            },
            new Season   // winter
            {
                fog = new Color(0.8f, 0.88f, 0.95f), fogDensity = 0.016f, skyTop = new Color(0.55f, 0.68f, 0.88f), skyMid = new Color(0.78f, 0.86f, 0.94f),
                sunColor = new Color(0.95f, 0.97f, 1f), sunInt = 1.25f, ambient = new Color(0.4f, 0.44f, 0.5f),
                trunk = new Color(0.22f, 0.17f, 0.17f), leaf = new Color(0.7f, 0.78f, 0.88f), bush = new Color(0.8f, 0.86f, 0.93f), grass = new Color(0.78f, 0.84f, 0.9f),
                moss = new Color(0.82f, 0.88f, 0.95f), dirt = new Color(0.55f, 0.6f, 0.7f), rock = new Color(0.45f, 0.48f, 0.55f),
                mote = new Color(1.2f, 1.2f, 1.3f, 1f), moteFall = -1.1f, moteSize = 0.13f, flare = new Color(1.1f, 1.2f, 1.5f),
            },
        };
        int season;

        // ---------------- path and ground ----------------
        static float PathX(float z) { return Mathf.Sin(z * 0.011f) * 7f + Mathf.Sin(z * 0.023f + 1.3f) * 2.5f; }

        static float Ground(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.035f + 500f, z * 0.035f + 500f) * 3.2f + Mathf.PerlinNoise(x * 0.11f, z * 0.11f + 70f) * 0.7f - 1.8f;
            float d = Mathf.Abs(x - PathX(z));
            return n * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 10f, d));   // flat along the path
        }

        // ---------------- chunks ----------------
        class Chunk { public int index; public GameObject root; public List<Vector3> perches = new List<Vector3>(); }
        readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        class Part { public List<Vector3> v = new List<Vector3>(); public List<Color> c = new List<Color>(); public List<int> t = new List<int>(); }

        struct Src { public Mesh mesh; public Vector3[] v; public Color[] c; public int[] t; public Vector3 top; public float height; }
        Src[] trees, bushes, grasses, rocks;

        Src MakeSrc(string name)
        {
            var m = Creatures.Nature(name);
            var s = new Src { mesh = m, v = m.vertices, c = m.colors, t = m.triangles };
            float top = 0f;
            foreach (var p in s.v) if (p.y > top) { top = p.y; s.top = p; }
            s.height = top;
            return s;
        }

        static void AddInstance(Part part, Src s, Matrix4x4 mtx, float tint)
        {
            int b = part.v.Count;
            for (int i = 0; i < s.v.Length; i++)
            {
                part.v.Add(mtx.MultiplyPoint3x4(s.v[i]));
                var c = s.c[i]; c.b = tint;
                part.c.Add(c);
            }
            for (int i = 0; i < s.t.Length; i++) part.t.Add(b + s.t[i]);
        }

        GameObject MakePartObject(Transform parent, string name, Part part, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(part.v); m.SetColors(part.c); m.SetTriangles(part.t, 0);
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        void BuildChunk(int ci)
        {
            var ch = new Chunk { index = ci, root = new GameObject("Chunk" + ci) };
            ch.root.transform.SetParent(transform, false);
            var rng = new System.Random(ci * 7919 + 13);
            float F(float lo, float hi) { return lo + (float)rng.NextDouble() * (hi - lo); }
            float z0 = ci * ChunkLen;

            var treeP = new Part(); var bushP = new Part(); var grassP = new Part(); var rockP = new Part();

            for (int i = 0; i < 38; i++)
            {
                float x = F(-HalfW, HalfW), z = z0 + F(0f, ChunkLen);
                if (Mathf.Abs(x - PathX(z)) < 3.6f) continue;
                var s = trees[rng.Next(trees.Length)];
                float sc = F(2.6f, 5.2f), sy = sc * F(0.95f, 1.3f);
                float y = Ground(x, z) - 0.1f;
                var mtx = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(F(-2.5f, 2.5f), F(0f, 360f), F(-2.5f, 2.5f)), new Vector3(sc, sy, sc));
                AddInstance(treeP, s, mtx, (float)rng.NextDouble());
                ch.perches.Add(mtx.MultiplyPoint3x4(s.top));
            }
            for (int i = 0; i < 32; i++)
            {
                float x = F(-HalfW, HalfW), z = z0 + F(0f, ChunkLen);
                if (Mathf.Abs(x - PathX(z)) < 2.4f) continue;
                var s = bushes[rng.Next(bushes.Length)];
                float sc = F(1.2f, 2.6f);
                AddInstance(bushP, s, Matrix4x4.TRS(new Vector3(x, Ground(x, z) - 0.05f, z), Quaternion.Euler(0f, F(0f, 360f), 0f), new Vector3(sc, sc * F(0.8f, 1.2f), sc)), (float)rng.NextDouble());
            }
            for (int i = 0; i < 120; i++)
            {
                float x = F(-HalfW * 0.7f, HalfW * 0.7f), z = z0 + F(0f, ChunkLen);
                if (Mathf.Abs(x - PathX(z)) < 1.0f) continue;
                var s = grasses[rng.Next(grasses.Length)];
                float sc = F(0.9f, 1.9f);
                AddInstance(grassP, s, Matrix4x4.TRS(new Vector3(x, Ground(x, z) - 0.03f, z), Quaternion.Euler(0f, F(0f, 360f), 0f), new Vector3(sc, sc * F(0.8f, 1.3f), sc)), (float)rng.NextDouble());
            }
            for (int i = 0; i < 6; i++)
            {
                float x = F(-HalfW, HalfW), z = z0 + F(0f, ChunkLen);
                if (Mathf.Abs(x - PathX(z)) < 2.0f) continue;
                var s = rocks[rng.Next(rocks.Length)];
                float sc = F(0.8f, 2.4f);
                AddInstance(rockP, s, Matrix4x4.TRS(new Vector3(x, Ground(x, z) - sc * 0.2f, z), Quaternion.Euler(F(-8f, 8f), F(0f, 360f), F(-8f, 8f)), new Vector3(sc, sc * F(0.6f, 1f), sc)), (float)rng.NextDouble());
            }

            MakePartObject(ch.root.transform, "Trees", treeP, treeMat);
            MakePartObject(ch.root.transform, "Bushes", bushP, bushMat);
            MakePartObject(ch.root.transform, "Grass", grassP, grassMat);
            MakePartObject(ch.root.transform, "Rocks", rockP, rockMat);

            // ground: a coarse faceted grid, dirt along the path
            const int nx = 26, nz = 5;
            var gp = new Part();
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var cols = new Color[verts.Length];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(-HalfW * 1.1f, HalfW * 1.1f, i / (float)nx);
                    float z = z0 + j * ChunkLen / nz;
                    float d = Mathf.Abs(x - PathX(z));
                    verts[j * (nx + 1) + i] = new Vector3(x, Ground(x, z), z);
                    cols[j * (nx + 1) + i] = new Color(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.6f, 3.4f, d)), 0f, 0.5f, 1f);
                }
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    int[] q = { a, c, b, b, c, d };
                    foreach (var k in q) { gp.v.Add(verts[k]); gp.c.Add(cols[k]); gp.t.Add(gp.v.Count - 1); }
                }
            MakePartObject(ch.root.transform, "Ground", gp, groundMat);
            chunks[ci] = ch;
        }

        void DestroyChunk(Chunk ch)
        {
            foreach (var mf in ch.root.GetComponentsInChildren<MeshFilter>()) Destroy(mf.sharedMesh);
            Destroy(ch.root);
        }

        void UpdateChunks(bool all)
        {
            int cc = Mathf.FloorToInt(walkZ / ChunkLen);
            int built = 0;
            for (int ci = cc - Behind; ci <= cc + Ahead; ci++)
                if (!chunks.ContainsKey(ci))
                {
                    BuildChunk(ci);
                    if (!all && ++built >= 1) break;     // one new chunk per frame while walking
                }
            var drop = new List<int>();
            foreach (var kv in chunks) if (kv.Key < cc - Behind - 1 || kv.Key > cc + Ahead + 1) drop.Add(kv.Key);
            foreach (var k in drop) { DestroyChunk(chunks[k]); chunks.Remove(k); }
        }

        Vector3 PickPerch(float minAhead, float maxAhead)
        {
            var cands = new List<Vector3>();
            foreach (var kv in chunks)
                foreach (var p in kv.Value.perches)
                    if (p.z > walkZ + minAhead && p.z < walkZ + maxAhead && Mathf.Abs(p.x - PathX(p.z)) < 32f) cands.Add(p);
            if (cands.Count == 0) return new Vector3(PathX(walkZ + 30f), 10f, walkZ + 30f);
            return cands[Random.Range(0, cands.Count)];
        }

        // ---------------- birds and deer ----------------
        class Bird
        {
            public Transform t; public Renderer r; public MaterialPropertyBlock mpb;
            public int state;                // 0 perched, 1 takeoff, 2 flying, 3 landing
            public Vector3 pos, vel, perch, target;
            public float timer, fold = 1f, yaw, delay = -1f, seed;
        }
        readonly List<Bird> birds = new List<Bird>();

        class Deer
        {
            public Transform t; public Renderer r; public MaterialPropertyBlock mpb;
            public Vector3 pos; public float heading, speed, timer, seed; public bool grazing;
        }
        readonly List<Deer> deer = new List<Deer>();

        void BuildCritters()
        {
            critterMat = Mats.Facet(Color.white, Color.black);
            var bs = Creatures.SpecOf("Bird");
            var bm = Creatures.Mesh("Bird");
            var tints = new[] { Color.white, new Color(1.5f, 1.1f, 0.7f), new Color(0.8f, 1f, 1.5f), new Color(1.6f, 0.7f, 0.6f) };
            for (int i = 0; i < 28; i++)
            {
                var go = new GameObject("Bird");
                go.transform.SetParent(transform);
                go.AddComponent<MeshFilter>().sharedMesh = bm;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = critterMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale = Vector3.one * (bs.length * 0.5f * Random.Range(0.9f, 1.25f));
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Tint", tints[Random.Range(0, tints.Length)]);
                var b = new Bird { t = go.transform, r = r, mpb = mpb, perch = PickPerch(8f, 70f), yaw = Random.Range(0f, 360f), seed = Random.Range(0f, 50f), timer = Random.Range(6f, 40f) };
                b.pos = b.perch;
                birds.Add(b);
            }
            var ds = Creatures.SpecOf("Deer");
            var dm = Creatures.Mesh("Deer");
            for (int i = 0; i < 7; i++)
            {
                var go = new GameObject("Deer");
                go.transform.SetParent(transform);
                go.AddComponent<MeshFilter>().sharedMesh = dm;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = critterMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale = Vector3.one * (ds.length * 0.5f * Random.Range(0.9f, 1.15f));
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Tint", Color.white);
                var d = new Deer { t = go.transform, r = r, mpb = mpb, seed = Random.Range(0f, 50f) };
                SpawnDeer(d, true);
                deer.Add(d);
            }
        }

        void SpawnDeer(Deer d, bool anywhere)
        {
            float z = walkZ + (anywhere ? Random.Range(10f, 90f) : Random.Range(60f, 95f));
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = PathX(z) + side * Random.Range(12f, 45f);
            d.pos = new Vector3(x, Ground(x, z), z);
            d.heading = Random.Range(-70f, 70f) + (Random.value < 0.3f ? 180f : 0f);
            d.speed = Random.Range(0.7f, 1.3f);
            d.timer = Random.Range(5f, 14f);
            d.grazing = false;
        }

        void UpdateBirds(float dt, float t)
        {
            float beatPos = actions.Clock.BeatPos;
            bool synced = actions.Clock.Running;
            foreach (var b in birds)
            {
                float flap = 0.05f;
                switch (b.state)
                {
                    case 0:     // perched
                        b.pos = b.perch;
                        b.fold = Mathf.MoveTowards(b.fold, 1f, dt * 3f);
                        if (b.delay >= 0f) { b.delay -= dt; if (b.delay < 0f) TakeOff(b); }
                        b.timer -= dt;
                        if (b.timer <= 0f) TakeOff(b);
                        else if (b.perch.z < walkZ - 12f) { b.perch = PickPerch(14f, 75f); b.pos = b.perch; b.timer = Random.Range(6f, 40f); }
                        b.t.rotation = Quaternion.Euler(0f, b.yaw, 0f);
                        break;
                    case 1:     // takeoff: up and away
                        b.fold = Mathf.MoveTowards(b.fold, 0f, dt * 5f);
                        flap = 0.55f;
                        b.timer += dt;
                        b.pos += b.vel * dt;
                        b.vel.y = Mathf.Max(b.vel.y - 2f * dt, 2.5f);
                        if (b.timer > 0.9f) { b.state = 2; b.target = PickPerch(18f, 80f); }
                        Face(b, dt);
                        break;
                    case 2:     // flying to a new perch
                    {
                        b.fold = Mathf.MoveTowards(b.fold, 0f, dt * 5f);
                        flap = 0.55f;
                        var to = b.target - b.pos;
                        float dist = to.magnitude;
                        var want = to.normalized * 9f + new Vector3(Mathf.Sin(t * 1.3f + b.seed) * 2f, Mathf.Sin(t * 0.9f + b.seed) * 1.2f + Mathf.Clamp(dist * 0.05f, 0f, 3f), 0f);
                        b.vel = Vector3.Lerp(b.vel, want, 1f - Mathf.Exp(-2f * dt));
                        b.pos += b.vel * dt;
                        if (dist < 12f) { b.state = 3; b.perch = b.target; }
                        Face(b, dt);
                        break;
                    }
                    case 3:     // landing
                    {
                        flap = 0.45f;
                        var to = b.perch - b.pos;
                        float dist = to.magnitude;
                        float sp = Mathf.Clamp(dist * 1.1f, 1.2f, 8f);
                        b.vel = Vector3.Lerp(b.vel, to.normalized * sp, 1f - Mathf.Exp(-3f * dt));
                        b.pos += b.vel * dt;
                        if (dist < 3f) b.fold = Mathf.MoveTowards(b.fold, 1f, dt * 1.2f);
                        if (dist < 0.4f) { b.state = 0; b.timer = Random.Range(8f, 40f); b.yaw = Random.Range(0f, 360f); }
                        Face(b, dt);
                        break;
                    }
                }
                b.t.position = b.pos;
                float wag = synced ? beatPos * Mathf.PI * 4f + b.seed : t * 22f + b.seed;
                b.mpb.SetFloat("_Wag", wag);
                b.mpb.SetFloat("_Flap", flap);
                b.mpb.SetFloat("_Fold", b.fold);
                b.r.SetPropertyBlock(b.mpb);
            }
        }

        void Face(Bird b, float dt)
        {
            if (b.vel.sqrMagnitude < 0.01f) return;
            var dir = b.vel.normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, -Mathf.Clamp(b.vel.x * 2f, -25f, 25f));
            b.t.rotation = Quaternion.Slerp(b.t.rotation, rot, 1f - Mathf.Exp(-6f * dt));
        }

        void TakeOff(Bird b)
        {
            if (b.state != 0) return;
            b.state = 1; b.timer = 0f;
            b.vel = new Vector3(Random.Range(-3f, 3f), 4.5f, Random.Range(2f, 5f));
        }

        void UpdateDeer(float dt, float t)
        {
            float beatPos = actions.Clock.BeatPos;
            bool synced = actions.Clock.Running;
            foreach (var d in deer)
            {
                d.timer -= dt;
                if (d.timer <= 0f) { d.grazing = !d.grazing; d.timer = d.grazing ? Random.Range(3f, 8f) : Random.Range(6f, 14f); }
                float sp = d.grazing ? 0f : d.speed;
                var dir = Quaternion.Euler(0f, d.heading, 0f) * Vector3.forward;
                d.pos += dir * sp * dt;
                d.pos.y = Ground(d.pos.x, d.pos.z);
                float lateral = Mathf.Abs(d.pos.x - PathX(d.pos.z));
                if (d.pos.z < walkZ - 14f || d.pos.z > walkZ + 110f || lateral > 70f || lateral < 5f) SpawnDeer(d, false);
                d.t.position = d.pos + Vector3.up * (sp > 0f ? Mathf.Abs(Mathf.Sin(t * 3f + d.seed)) * 0.04f : 0f);
                d.t.rotation = Quaternion.Slerp(d.t.rotation, Quaternion.Euler(0f, d.heading, 0f), 1f - Mathf.Exp(-3f * dt));
                float wag = synced ? beatPos * Mathf.PI + d.seed : t * 3f + d.seed;
                d.mpb.SetFloat("_Wag", wag);
                d.mpb.SetFloat("_LegAmp", sp > 0f ? Creatures.SpecOf("Deer").legAmp : 0f);
                d.r.SetPropertyBlock(d.mpb);
            }
        }

        // ---------------- sky, sun, light ----------------
        Transform sunGlare, sunHalo, streak;
        Material sunMat, haloMat, streakMat;
        readonly List<Transform> ghosts = new List<Transform>();
        readonly List<Material> ghostMats = new List<Material>();
        readonly float[] ghostAt = { 0.45f, 0.85f, 1.25f, -0.4f, 1.7f };
        readonly float[] ghostSize = { 1.6f, 0.7f, 2.4f, 1.1f, 0.9f };
        readonly List<Transform> beams = new List<Transform>();
        readonly List<Material> beamMats = new List<Material>();
        readonly List<Vector3> beamOff = new List<Vector3>();
        ParticleSystem motes;

        Material SoftMat(Color c, float soft)
        {
            var m = Mats.Additive(c, soft);
            m.SetFloat("_VC", 0f);
            return m;
        }

        void BuildSky()
        {
            skyMat = Mats.WaterSky();
            RenderSettings.skybox = skyMat;
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(-SunDir.normalized);

            sunMat = SoftMat(new Color(2f, 1.8f, 1.2f, 1f), 1.6f);
            haloMat = SoftMat(new Color(1f, 0.9f, 0.6f, 0.3f), 1.1f);
            sunGlare = Mats.Prim(PrimitiveType.Quad, transform, "SunGlare", Vector3.zero, Vector3.one * 40f, sunMat).transform;
            sunHalo = Mats.Prim(PrimitiveType.Quad, transform, "SunHalo", Vector3.zero, Vector3.one * 180f, haloMat).transform;

            // lens flare: ghosts along the line through the center of the view, and a long anamorphic streak through the sun
            for (int i = 0; i < ghostAt.Length; i++)
            {
                var gm = SoftMat(new Color(1f, 0.8f, 0.5f, 0f), 1.2f);
                var g = Mats.Prim(PrimitiveType.Quad, cam.transform, "Ghost" + i, new Vector3(0f, 0f, 10f), Vector3.one * ghostSize[i], gm).transform;
                ghosts.Add(g); ghostMats.Add(gm);
            }
            streakMat = SoftMat(new Color(1f, 0.85f, 0.6f, 0f), 1.0f);
            streak = Mats.Prim(PrimitiveType.Quad, cam.transform, "Streak", new Vector3(0f, 0f, 10f), new Vector3(60f, 0.5f, 1f), streakMat).transform;

            // light shafts through the trees
            for (int i = 0; i < 12; i++)
            {
                var bm = SoftMat(new Color(1f, 0.9f, 0.6f, 0f), 1.1f);
                var q = Mats.Prim(PrimitiveType.Quad, transform, "Beam", Vector3.zero, new Vector3(Random.Range(3f, 7f), 40f, 1f), bm).transform;
                beams.Add(q); beamMats.Add(bm);
                beamOff.Add(new Vector3(Random.Range(-32f, 32f), 14f, Random.Range(14f, 75f)));
            }

            // floating motes: pollen, drifting leaves, fireflies or snow, depending on the season
            var go = new GameObject("Motes");
            go.transform.SetParent(transform);
            motes = go.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = motes.main;
            m.loop = true; m.playOnAwake = false; m.prewarm = false;
            m.startLifetime = 10f; m.startSpeed = 0f; m.maxParticles = 2500;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var sh = motes.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(60f, 14f, 70f);
            var e = motes.emission; e.rateOverTime = 60f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Additive(new Color(1f, 1f, 1f, 1f), 1.3f);
            motes.Play();
        }

        void ApplySeason(int s)
        {
            var se = Seasons[s];
            skyMat.SetColor("_Deep", se.fog); skyMat.SetColor("_Mid", se.skyMid); skyMat.SetColor("_Top", se.skyTop); skyMat.SetFloat("_Intensity", 1f);
            RenderSettings.fogColor = se.fog; RenderSettings.fogDensity = se.fogDensity;
            RenderSettings.ambientLight = se.ambient;
            Shader.SetGlobalColor("_DjFogColor", se.fog);
            Shader.SetGlobalFloat("_DjFogDensity", se.fogDensity);
            sun.color = se.sunColor; sun.intensity = se.sunInt;
            treeMat.SetColor("_ColA", se.trunk); treeMat.SetColor("_ColB", se.leaf);
            bushMat.SetColor("_ColA", se.trunk); bushMat.SetColor("_ColB", se.bush);
            grassMat.SetColor("_ColA", se.grass * 0.8f); grassMat.SetColor("_ColB", se.grass);
            rockMat.SetColor("_ColA", se.rock); rockMat.SetColor("_ColB", se.rock);
            groundMat.SetColor("_ColA", se.moss); groundMat.SetColor("_ColB", se.dirt);
            sunMat.SetColor("_Color", new Color(se.flare.r * 1.5f, se.flare.g * 1.5f, se.flare.b * 1.5f, 1f));
            var mm = motes.main; mm.startColor = se.mote; mm.startSize = new ParticleSystem.MinMaxCurve(se.moteSize * 0.6f, se.moteSize * 1.4f);
            var vel = motes.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f); vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            vel.y = new ParticleSystem.MinMaxCurve(se.moteFall - 0.2f, se.moteFall + 0.3f);
            bloomBase = s == 2 ? 1.2f : 1.0f;
        }

        // ---------------- state ----------------
        float walkZ;
        float rush;              // surge build-up 0..1
        bool surgeOn;
        float dropT = -1f;       // seconds since the Drop was pressed
        bool dropSwapped;
        float flash;
        float beatEnv;
        float accentYaw, accentT = -1f;
        float accentTarget;
        float bloomBase = 1f;

        public void Init(VisualActions a, Camera camera, PostFx postFx)
        {
            actions = a;
            cam = camera;
            fx = postFx;

            cam.fieldOfView = 38f;

            trees = new[] { MakeSrc("Tree1"), MakeSrc("Tree2"), MakeSrc("Tree3"), MakeSrc("Tree4") };
            bushes = new[] { MakeSrc("Bush1"), MakeSrc("Bush2"), MakeSrc("Bush3") };
            grasses = new[] { MakeSrc("Grass1"), MakeSrc("Grass2"), MakeSrc("Grass3") };
            rocks = new[] { MakeSrc("Rock1"), MakeSrc("Rock2"), MakeSrc("Rock3") };

            treeMat = Mats.Forest(0.12f); bushMat = Mats.Forest(0.16f); grassMat = Mats.Forest(0.4f); rockMat = Mats.Forest(0f); groundMat = Mats.Forest(0f);

            BuildSky();
            ApplySeason(0);

            walkZ = 0f;
            UpdateChunks(true);
            BuildCritters();

            actions.Beat += OnBeat;
            actions.Accent += OnAccent;
            actions.Drop += OnDrop;
            actions.WarpChanged += OnSurge;
            actions.ResetRequested += OnReset;
            actions.Status = "Forest";
        }

        void OnDestroy()
        {
            if (actions == null) return;
            actions.Beat -= OnBeat;
            actions.Accent -= OnAccent;
            actions.Drop -= OnDrop;
            actions.WarpChanged -= OnSurge;
            actions.ResetRequested -= OnReset;
            foreach (var g in ghosts) if (g != null) Destroy(g.gameObject);    // children of the shared camera
            if (streak != null) Destroy(streak.gameObject);
        }

        void OnBeat(int idx) { beatEnv = 1f; }

        void OnSurge(bool on) { surgeOn = on; }

        void OnAccent()
        {
            beatEnv = 1f;
            foreach (var b in birds) if (b.state == 0 && b.perch.z > walkZ - 4f) b.delay = Random.Range(0f, 0.7f);
            accentTarget = Random.value < 0.5f ? -16f : 16f;
            accentT = 0f;
        }

        void OnDrop()
        {
            if (dropT >= 0f) return;
            dropT = 0f; dropSwapped = false;
        }

        void OnReset()
        {
            if (season != 0) { season = 0; ApplySeason(0); }
            dropT = -1f; flash = 0f;
        }

        // ---------------- per frame ----------------

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            rush = Mathf.MoveTowards(rush, surgeOn ? 1f : 0f, dt * (surgeOn ? 0.17f : 1.0f));
            float lvl = Mathf.Max(actions.Build, rush);
            float rs = Mathf.SmoothStep(0f, 1f, rush);

            beatEnv = Mathf.Max(0f, beatEnv - dt * 5f);
            Shader.SetGlobalFloat("_DjBeat", beatEnv * 0.5f);

            // the drop: white-out, season change at the peak, then it clears
            if (dropT >= 0f)
            {
                dropT += dt;
                flash = dropT < 0.4f ? dropT / 0.4f : Mathf.Exp(-(dropT - 0.4f) * 2.2f);
                if (!dropSwapped && dropT >= 0.4f)
                {
                    dropSwapped = true;
                    if (actions.Warping) actions.ToggleWarp();
                    season = (season + 1) % Seasons.Length;
                    ApplySeason(season);
                    actions.SetBuild(0f);
                }
                if (dropT > 3.5f) { dropT = -1f; flash = 0f; }
            }

            // walking
            float speed = 2.0f * (1f + 0.6f * lvl);
            walkZ += speed * dt;
            UpdateChunks(false);

            float x = PathX(walkZ);
            float slope = (PathX(walkZ + 2f) - PathX(walkZ - 2f)) / 4f;
            float yawPath = Mathf.Atan(slope) * Mathf.Rad2Deg;
            float beatPos = actions.Clock.Running ? actions.Clock.BeatPos : t * 2f;
            float step = Mathf.Sin(beatPos * Mathf.PI);
            float bob = Mathf.Abs(step) * 0.07f * (1f - 0.5f * rs);
            float roll = step * 0.5f;

            if (accentT >= 0f) { accentT += dt; accentYaw = accentTarget * Mathf.Sin(Mathf.Clamp01(accentT / 2.2f) * Mathf.PI); if (accentT > 2.2f) { accentT = -1f; accentYaw = 0f; } }

            var camPos = new Vector3(x, Ground(x, walkZ) + 1.7f + bob, walkZ);
            float pitch = 2f + 26f * rs - 8f * flash;
            float yaw = yawPath * 0.8f + accentYaw;
            cam.transform.SetPositionAndRotation(camPos, Quaternion.Euler(-pitch, yaw, roll));
            cam.fieldOfView = 38f - 7f * rs + flash * 6f;

            UpdateBirds(dt, t);
            UpdateDeer(dt, t);
            UpdateSun(camPos, rs, t);
            UpdateMotes(camPos, lvl);

            fx.Bloom.intensity.value = bloomBase + beatEnv * 0.08f + rs * 1.4f + flash * 2.5f;
            fx.Adjust.postExposure.value = rs * 0.5f + flash * 1.6f;
            fx.Chroma.intensity.value = rs * rs * 0.25f + flash * 0.25f;
            fx.Vig.intensity.value = 0.28f + rs * 0.15f;
        }

        void UpdateSun(Vector3 camPos, float rs, float t)
        {
            var d = SunDir.normalized;
            var sp = camPos + d * 300f;
            sunGlare.position = sp; sunGlare.rotation = Quaternion.LookRotation(sp - camPos);
            sunHalo.position = sp; sunHalo.rotation = sunGlare.rotation;
            float gs = 40f * (1f + 2.2f * rs * rs);
            sunGlare.localScale = Vector3.one * gs;
            sunHalo.localScale = Vector3.one * (180f * (1f + 1.5f * rs));
            var halo = Seasons[season].flare; halo.a = 0.22f + 0.5f * rs;
            haloMat.SetColor("_Color", new Color(halo.r * 0.7f, halo.g * 0.7f, halo.b * 0.7f, halo.a));

            // sun position in the camera's frame, in tangent units
            var sc = cam.worldToCameraMatrix.MultiplyVector(d);
            bool front = sc.z < -0.15f;        // camera space looks down -z
            float ox = front ? sc.x / -sc.z : 0f, oy = front ? sc.y / -sc.z : 0f;
            float vis = front ? Mathf.Clamp01(1.2f - Mathf.Max(Mathf.Abs(ox) / 2.6f, Mathf.Abs(oy) / 0.45f)) : 0f;
            float k = rs * vis;
            var fl = Seasons[season].flare;
            for (int i = 0; i < ghosts.Count; i++)
            {
                float a = ghostAt[i];
                ghosts[i].localPosition = new Vector3(-ox * a * 10f, -oy * a * 10f, 10f);
                ghosts[i].localScale = Vector3.one * ghostSize[i] * (0.8f + 0.5f * rs);
                float shade = 0.4f + 0.15f * i;
                ghostMats[i].SetColor("_Color", new Color(fl.r * shade * 0.8f, fl.g * shade * 0.8f, fl.b * shade * 1.1f, 0.5f * k));
            }
            streak.localPosition = new Vector3(ox * 10f, oy * 10f, 10f);
            streak.localScale = new Vector3(70f * (0.4f + 0.8f * rs), 0.35f + 0.5f * rs, 1f);
            streakMat.SetColor("_Color", new Color(fl.r, fl.g, fl.b, 0.65f * k));

            // light shafts: they follow you down the path
            float yawDeg = cam.transform.eulerAngles.y;
            for (int i = 0; i < beams.Count; i++)
            {
                var o = beamOff[i];
                var p = new Vector3(camPos.x + o.x, camPos.y + o.y - 4f, camPos.z + o.z);
                beams[i].position = p;
                beams[i].rotation = Quaternion.Euler(0f, yawDeg, (i % 2 == 0 ? 1f : -1f) * (10f + 6f * Mathf.Sin(t * 0.1f + i)));
                float a = (0.03f + 0.04f * rs + beatEnv * 0.012f) * (0.6f + 0.4f * Mathf.Sin(t * 0.3f + i * 1.7f));
                var sc2 = Seasons[season].sunColor;
                beamMats[i].SetColor("_Color", new Color(sc2.r, sc2.g * 0.95f, sc2.b * 0.8f, a * (season == 2 ? 0.6f : 1f)));
            }
        }

        void UpdateMotes(Vector3 camPos, float lvl)
        {
            motes.transform.position = camPos + cam.transform.forward * 26f + Vector3.up * 1f;
            var e = motes.emission;
            e.rateOverTime = (season == 3 ? 160f : 70f) + lvl * 160f;
        }
    }
}
