using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// Underwater world, in one faceted low-poly style (flat-shaded facets, bloom for the glow).
    /// Two scenes share the same geometry and swap looks: the dark DEEP reef and a bright, colorful CORAL REEF.
    ///   Beat   -> jellyfish stroke (each one on beats 1&amp;3, on 2&amp;4, or ~10% free-running), fish tails and wings lock to the beat
    ///   Accent -> burst of bubbles, schools dart, a big animal (whale, shark, manta, dolphin) glides through
    ///   Build  -> rising current: schools speed up, more bubbles and marine snow, stronger light shafts
    ///   Drop   -> a wall of bubbles rises from the bottom; behind it the world swaps between the deep and the coral reef.
    ///             Arriving in the deep, a ring of light sweeps the seabed and lights the bioluminescent coral.
    ///   Reset  -> dark deep reef again
    /// Creatures come from tools/convert_fish.py (CC0 Quaternius models plus extra reef fish); the reef is built in code.
    /// </summary>
    public class UnderwaterWorld : MonoBehaviour
    {
        const float SeabedY = -13f;
        static readonly Vector3 CamPos = new Vector3(0f, -2.5f, -16f);
        static readonly Vector3 CamLook = new Vector3(0f, -1.2f, 30f);

        VisualActions actions;
        Camera cam;
        PostFx fx;

        Material skyMat, seabedMat, reefSandMat, jellyMatBase, fishMat, reefRockMat;
        Material[] coralMats, reefCoralMats;
        Mesh jellyMesh;
        Light sun;

        // ---- the two looks ----
        class Look
        {
            public Color skyDeep, skyMid, skyTop; public float skyInt;
            public Color fog; public float fogDensity;
            public Color ambient, sun; public float sunInt;
            public float rays, bloom;
        }
        static readonly Look DeepLook = new Look
        {
            skyDeep = new Color(0.005f, 0.04f, 0.09f), skyMid = new Color(0.02f, 0.2f, 0.3f), skyTop = new Color(0.35f, 0.85f, 0.95f), skyInt = 1f,
            fog = new Color(0.015f, 0.12f, 0.19f), fogDensity = 0.0105f,
            ambient = new Color(0.05f, 0.13f, 0.2f), sun = new Color(0.65f, 0.95f, 1f), sunInt = 1.05f, rays = 1f, bloom = 1.3f,
        };
        static readonly Look ReefLook = new Look
        {
            skyDeep = new Color(0.03f, 0.3f, 0.42f), skyMid = new Color(0.12f, 0.62f, 0.78f), skyTop = new Color(0.8f, 1f, 1f), skyInt = 1.1f,
            fog = new Color(0.12f, 0.52f, 0.66f), fogDensity = 0.0052f,
            ambient = new Color(0.2f, 0.38f, 0.45f), sun = new Color(1f, 0.96f, 0.82f), sunInt = 1.45f, rays = 1.9f, bloom = 0.95f,
        };
        bool reef;               // which scene is showing

        // ---- reef pieces: the same geometry wears a deep skin or a reef skin ----
        class Piece
        {
            public MeshRenderer r; public Material deepMat, reefMat; public bool reefOnly;
            public GameObject haloGo;
        }
        readonly List<Piece> pieces = new List<Piece>();

        // ---- jellyfish ----
        class Jelly
        {
            public Transform t; public Renderer r; public MaterialPropertyBlock mpb;
            public int mode;                 // 0: beats 1&3, 1: beats 2&4, 2: free-running
            public float period, timer;      // free-running only
            public float delay = -1f;        // small random lag so the school is not machine-perfect
            public float pulseT = 99f;
            public Vector3 vel;
            public float phase, scale;
            public Color col;
        }
        readonly List<Jelly> jellies = new List<Jelly>();

        // ---- fish ----
        class School
        {
            public Vector3 center; public float dir, speed, y0, z0, phase, kick;
            public bool onBeat; public float cycles;
            public int scene;                // 0 deep, 1 reef
        }
        class Fish
        {
            public Transform t; public Renderer r; public MaterialPropertyBlock mpb;
            public School school; public Vector3 offset, pos, fwd;
            public bool onBeat; public float cycles, wagOffset, freeRate, seed;
        }
        readonly List<School> schools = new List<School>();
        readonly List<Fish> fish = new List<Fish>();

        // ---- big animals that glide through now and then ----
        class Cruiser
        {
            public Transform t; public Renderer r; public MaterialPropertyBlock mpb;
            public int scene; public float speed, cycles, y, z, dir, seed; public bool active;
        }
        readonly List<Cruiser> cruisers = new List<Cruiser>();
        float cruiserTimer = 8f;

        // ---- atmosphere ----
        ParticleSystem snow, bubbles;
        BubbleWall wall;
        bool wallSwapped;
        readonly List<Transform> rays = new List<Transform>();
        readonly List<Material> rayMats = new List<Material>();
        readonly List<float> rayBase = new List<float>();

        class Halo { public Transform t; public Material m; public Color glow; public float a; public Vector2 xz; }
        readonly List<Halo> halos = new List<Halo>();

        float beatEnv;           // 0..1, pulses on every beat
        float flash;             // brief white swell on Drop
        bool revealed;
        float revealT = -1f;
        float revealR = -1f;
        float revealDelay = -1f;
        float surge;             // accent shimmer
        float rayMul = 1f, bloomBase = 1.3f;

        public void Init(VisualActions a, Camera camera, PostFx postFx)
        {
            actions = a;
            cam = camera;
            fx = postFx;

            skyMat = Mats.WaterSky();
            RenderSettings.skybox = skyMat;
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            Shader.SetGlobalFloat("_DjRevealR", -1f);
            Shader.SetGlobalVector("_DjRevealC", new Vector4(0f, 0f, 40f, 0f));

            sun = new GameObject("SurfaceLight").AddComponent<Light>();
            sun.transform.SetParent(transform);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(70f, 15f, 0f);   // from above, slightly ahead

            cam.transform.position = CamPos;
            cam.transform.rotation = Quaternion.LookRotation(CamLook - CamPos);
            cam.fieldOfView = 30f;

            jellyMesh = BuildJellyMesh();
            BuildSeabed();
            BuildReef();
            BuildSchools();
            BuildCruisers();
            BuildJellies();
            BuildAtmosphere();

            ApplyLook(DeepLook);
            SetScene(false, false);

            actions.Beat += OnBeat;
            actions.Accent += OnAccent;
            actions.Drop += OnDrop;
            actions.ResetRequested += OnReset;
            actions.Status = "Underwater";
        }

        void OnDestroy()
        {
            if (actions == null) return;
            actions.Beat -= OnBeat;
            actions.Accent -= OnAccent;
            actions.Drop -= OnDrop;
            actions.ResetRequested -= OnReset;
        }

        void ApplyLook(Look l)
        {
            skyMat.SetColor("_Deep", l.skyDeep); skyMat.SetColor("_Mid", l.skyMid); skyMat.SetColor("_Top", l.skyTop);
            skyMat.SetFloat("_Intensity", l.skyInt);
            RenderSettings.fogColor = l.fog;
            RenderSettings.fogDensity = l.fogDensity * 1.15f;
            RenderSettings.ambientLight = l.ambient;
            Shader.SetGlobalColor("_DjFogColor", l.fog);
            Shader.SetGlobalFloat("_DjFogDensity", l.fogDensity);
            sun.color = l.sun; sun.intensity = l.sunInt;
            rayMul = l.rays; bloomBase = l.bloom;
        }

        /// <summary>Swaps everything that differs between the two scenes. Called while the bubble wall hides the screen.</summary>
        void SetScene(bool toReef, bool arriving)
        {
            reef = toReef;
            ApplyLook(reef ? ReefLook : DeepLook);
            seabedRenderer.sharedMaterial = reef ? reefSandMat : seabedMat;
            foreach (var p in pieces)
            {
                bool on = !p.reefOnly || reef;
                p.r.gameObject.SetActive(on);
                p.r.sharedMaterial = reef ? p.reefMat : p.deepMat;
                if (p.haloGo != null) p.haloGo.SetActive(!reef);
            }
            foreach (var f in fish) f.t.gameObject.SetActive(f.school.scene == (reef ? 1 : 0));
            foreach (var c in cruisers) { c.active = false; c.t.gameObject.SetActive(false); }
            cruiserTimer = Random.Range(6f, 12f);
            for (int i = 0; i < jellies.Count; i++) jellies[i].t.gameObject.SetActive(!reef || i < 9);
            if (reef)
            {
                revealed = false; revealT = -1f; revealR = -1f; revealDelay = -1f;
                Shader.SetGlobalFloat("_DjRevealR", -1f);
            }
            else if (arriving)
            {
                revealed = true; revealT = -1f; revealR = -1f; revealDelay = 1.0f;   // ring of light sweeps the deep coral after the wall clears
            }
        }

        // =================== terrain and reef ===================

        static float Height(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.02f + 10f, z * 0.02f + 4f) * 7f
                    + Mathf.PerlinNoise(x * 0.07f, z * 0.07f) * 2.2f
                    + Mathf.PerlinNoise(x * 0.2f, z * 0.2f) * 0.5f;
            float dune = Mathf.Sin(x * 0.05f + z * 0.03f) * 0.8f;
            return SeabedY + n + dune - 4f;
        }

        MeshRenderer seabedRenderer;

        void BuildSeabed()
        {
            seabedMat = Mats.Seabed(new Color(0.34f, 0.3f, 0.22f), new Color(0.1f, 0.13f, 0.12f), new Color(0.2f, 1.1f, 1.5f), 0f);
            reefSandMat = Mats.Seabed(new Color(0.85f, 0.72f, 0.5f), new Color(0.6f, 0.52f, 0.36f), Color.black, 0f);
            reefSandMat.SetFloat("_Caustic", 1.5f);
            // coarse grid so the facets read as low poly
            const int nx = 56, nz = 36;
            const float x0 = -130f, x1 = 130f, z0 = -30f, z1 = 170f;
            var verts = new Vector3[nx * nz];
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (float)(nx - 1));
                    float z = Mathf.Lerp(z0, z1, j / (float)(nz - 1));
                    verts[j * nx + i] = new Vector3(x, Height(x, z), z);
                }
            int k = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            var mesh = new Mesh { name = "Seabed", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Seabed");
            go.transform.SetParent(transform);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            seabedRenderer = go.AddComponent<MeshRenderer>();
            seabedRenderer.sharedMaterial = seabedMat;
        }

        void BuildReef()
        {
            coralMats = new[]
            {
                Mats.Seabed(new Color(0.18f, 0.06f, 0.14f), new Color(0.25f, 0.08f, 0.2f), new Color(2.2f, 0.35f, 1.4f), 1f),
                Mats.Seabed(new Color(0.05f, 0.15f, 0.18f), new Color(0.06f, 0.22f, 0.24f), new Color(0.2f, 1.6f, 1.9f), 1f),
                Mats.Seabed(new Color(0.2f, 0.12f, 0.04f), new Color(0.28f, 0.16f, 0.05f), new Color(2.2f, 1.1f, 0.25f), 1f),
            };
            // reef coral colors (linear), every one a bright flat color the facets shade
            var palette = new[]
            {
                new Color(1.0f, 0.28f, 0.08f), new Color(0.9f, 0.08f, 0.38f), new Color(1.0f, 0.62f, 0.05f), new Color(0.35f, 0.85f, 0.1f),
                new Color(0.45f, 0.12f, 0.95f), new Color(0.05f, 0.75f, 0.65f), new Color(1.0f, 0.3f, 0.45f),
            };
            reefCoralMats = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                float gray = palette[i].grayscale;
                var soft = Color.Lerp(palette[i], new Color(gray, gray, gray), 0.16f) * 0.92f;   // a little less saturated
                reefCoralMats[i] = Mats.Facet(soft, soft * 0.06f);
            }
            reefRockMat = Mats.Facet(new Color(0.5f, 0.45f, 0.4f), Color.black);

            var rng = new System.Random(5);
            float R(float lo, float hi) { return lo + (float)rng.NextDouble() * (hi - lo); }

            // coral pieces; the first 130 exist in both scenes, then 110 more fill in the reef
            for (int i = 0; i < 240; i++)
            {
                bool reefOnly = i >= 130;
                float x = reefOnly ? R(-60f, 60f) : R(-110f, 110f);
                float z = reefOnly ? R(8f, 80f) : R(4f, 120f);
                if (Mathf.Abs(x) < 6f && z < 30f) continue;   // keep the foreground lane clear
                int type = PickType(rng);
                float s = type == ReefMeshes.Branch ? R(0.9f, 1.7f) : (type == ReefMeshes.Dome ? R(1.0f, 2.0f) : (type == ReefMeshes.Table ? R(1.0f, 1.8f) : R(1.1f, 1.9f)));
                var cluster = new GameObject("Coral" + i).transform;
                cluster.SetParent(transform);
                cluster.position = new Vector3(x, Height(x, z) - 0.2f, z);
                cluster.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                var mgo = new GameObject("Mesh");
                mgo.transform.SetParent(cluster, false);
                mgo.transform.localScale = new Vector3(s, s * R(0.9f, 1.25f), s);
                mgo.AddComponent<MeshFilter>().sharedMesh = ReefMeshes.Coral(type, rng.Next(0, 3));
                var mr = mgo.AddComponent<MeshRenderer>();
                var dm = coralMats[i % coralMats.Length];
                var piece = new Piece { r = mr, deepMat = dm, reefMat = reefCoralMats[rng.Next(0, reefCoralMats.Length)], reefOnly = reefOnly };
                mr.sharedMaterial = dm;
                if (!reefOnly)
                {
                    // soft glow halo that fades in when the reveal ring reaches this cluster (deep scene only)
                    var gcol = dm.GetColor("_Glow");
                    var hm = Mats.Additive(new Color(gcol.r, gcol.g, gcol.b, 0f), 2f);
                    hm.SetFloat("_VC", 0f);
                    float hs = R(7f, 13f);
                    var hq = Mats.Prim(PrimitiveType.Quad, cluster, "Halo", new Vector3(0f, 2.2f, 0f), new Vector3(hs, hs, 1f), hm);
                    halos.Add(new Halo { t = hq.transform, m = hm, glow = gcol, xz = new Vector2(x, z) });
                    piece.haloGo = hq;
                }
                pieces.Add(piece);
            }
            // rocks
            for (int i = 0; i < 34; i++)
            {
                float x = R(-120f, 120f), z = R(2f, 140f);
                float s = R(1.6f, 5f);
                var go = new GameObject("Rock");
                go.transform.SetParent(transform);
                go.transform.position = new Vector3(x, Height(x, z) + s * 0.1f, z);
                go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                go.transform.localScale = new Vector3(s, s * R(0.6f, 1.0f), s * R(0.8f, 1.3f));
                go.AddComponent<MeshFilter>().sharedMesh = ReefMeshes.Rock(i);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = seabedMat;
                pieces.Add(new Piece { r = mr, deepMat = seabedMat, reefMat = reefRockMat });
            }
        }

        static int PickType(System.Random rng)
        {
            double r = rng.NextDouble();
            if (r < 0.30) return ReefMeshes.Branch;
            if (r < 0.50) return ReefMeshes.Dome;
            if (r < 0.65) return ReefMeshes.Table;
            if (r < 0.80) return ReefMeshes.Fan;
            return ReefMeshes.Tube;
        }

        // =================== meshes ===================

        static Mesh BuildJellyMesh()
        {
            var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            const int rings = 5, seg = 10;      // few segments: a faceted bell
            float thMax = Mathf.PI * 0.5f * 1.08f;
            for (int i = 0; i <= rings; i++)
            {
                float th = i / (float)rings * thMax;
                float r = Mathf.Sin(th), y = Mathf.Cos(th) * 0.75f;
                for (int j = 0; j < seg; j++)
                {
                    float a = j / (float)seg * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                    nrm.Add(new Vector3(Mathf.Cos(a) * Mathf.Sin(th), Mathf.Cos(th), Mathf.Sin(a) * Mathf.Sin(th)).normalized);
                    cols.Add(new Color(0f, 0f, 0f, 1f));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * seg + j, b = i * seg + (j + 1) % seg, c = a + seg, d = b + seg;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            // tentacle ribbons hanging from the rim
            float rimR = Mathf.Sin(thMax) * 0.95f, rimY = Mathf.Cos(thMax) * 0.75f;
            const int tents = 9, tsegs = 9;
            for (int k = 0; k < tents; k++)
            {
                float a = k / (float)tents * Mathf.PI * 2f + 0.2f * Mathf.Sin(k * 1.7f);
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-radial.z, 0f, radial.x);
                int start = verts.Count;
                for (int s = 0; s <= tsegs; s++)
                {
                    float L = s / (float)tsegs;
                    var c0 = radial * (rimR * (1f - 0.35f * L)) + new Vector3(0f, rimY - L * 2.6f, 0f);
                    float w = 0.05f * (1f - L * 0.6f);
                    verts.Add(c0 - tangent * w); verts.Add(c0 + tangent * w);
                    nrm.Add(radial); nrm.Add(radial);
                    cols.Add(new Color(1f, L, 0f, 1f)); cols.Add(new Color(1f, L, 0f, 1f));
                }
                for (int s = 0; s < tsegs; s++)
                {
                    int a0 = start + s * 2;
                    tris.Add(a0); tris.Add(a0 + 2); tris.Add(a0 + 1);
                    tris.Add(a0 + 1); tris.Add(a0 + 2); tris.Add(a0 + 3);
                }
            }
            var m = new Mesh { name = "Jelly" };
            m.SetVertices(verts); m.SetNormals(nrm); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.bounds = new Bounds(new Vector3(0f, -1f, 0f), new Vector3(3f, 5f, 3f));
            return m;
        }

        // =================== life ===================

        void BuildSchools()
        {
            fishMat = Mats.Facet(Color.white, Color.black);
            // scene (0 deep, 1 reef), model, lane direction, speed, y, z, count, y phase, tint, tail cycles per beat (0.5 = every other beat)
            // deep: dim light, so fewer, larger-looking fish
            MakeSchool("Fish1", 0, +1f, 6.5f, -2f, 28f, 22, -2f, Color.white, 1f);
            MakeSchool("Fish2", 0, -1f, 5.0f, 4f, 42f, 14, 4f, new Color(0.8f, 0.95f, 1.2f), 0.5f);
            MakeSchool("Fish1", 0, +1f, 5.0f, -6f, 60f, 18, -5f, new Color(0.7f, 0.9f, 1.3f), 1f);
            // reef: a lot of color
            MakeSchool("Fish3", 1, +1f, 5.5f, -4f, 24f, 26, -3f, Color.white, 1f);
            MakeSchool("Tang", 1, -1f, 7.0f, 0f, 40f, 22, 2f, Color.white, 1f);
            MakeSchool("YellowTang", 1, +1f, 6.0f, -2f, 52f, 24, -1f, Color.white, 1f);
            MakeSchool("Fish2", 1, -1f, 4.5f, 3f, 34f, 12, 5f, Color.white, 0.5f);
            MakeSchool("Needlefish", 1, +1f, 12f, 5f, 62f, 10, -4f, Color.white, 1f);
            MakeSchool("Puffer", 1, -1f, 3.2f, -7f, 30f, 6, 1f, Color.white, 0.5f);
            MakeSchool("Fish1", 1, -1f, 7.0f, -3f, 70f, 16, 0f, new Color(1.0f, 1.15f, 0.45f), 1f);
        }

        void MakeSchool(string model, int scene, float dir, float speed, float y0, float z0, int count, float yPhase, Color tint, float cycles)
        {
            var spec = Creatures.SpecOf(model);
            var mesh = Creatures.Mesh(model);
            var sc = new School { dir = dir, speed = speed, y0 = y0, z0 = z0, phase = yPhase, cycles = cycles, onBeat = true, scene = scene };
            sc.center = new Vector3(Random.Range(-90f, 90f), y0, z0);
            schools.Add(sc);
            float spread = spec.length > 4f ? 1.4f : 1f;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(model);
                go.transform.SetParent(transform);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fishMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mpb = new MaterialPropertyBlock();
                go.transform.localScale = Vector3.one * (spec.length * 0.5f * Random.Range(0.85f, 1.2f));
                float v = Random.Range(0.88f, 1.12f);
                mpb.SetColor("_Tint", new Color(tint.r * v, tint.g * v, tint.b * v, 1f));
                mpb.SetFloat("_WagAmp", spec.wagAmp);
                mpb.SetFloat("_WagUp", spec.wagUp);
                mpb.SetFloat("_Flap", spec.flap);
                bool off = Random.value < 0.1f;   // ~10% keep their own tempo so the school looks natural
                var f = new Fish
                {
                    t = go.transform, r = r, mpb = mpb, school = sc, onBeat = !off, cycles = cycles,
                    wagOffset = Random.Range(0f, 1f) * (off ? 6.28f : 0.5f),   // on-beat fish stay nearly in phase
                    freeRate = Random.Range(7f, 13f), seed = Random.Range(0f, 100f),
                    offset = new Vector3(Random.Range(-15f, 15f) * spread, Random.Range(-4.5f, 4.5f), Random.Range(-8f, 8f)),
                };
                f.pos = sc.center + f.offset;
                f.fwd = new Vector3(dir, 0f, 0f);
                go.transform.position = f.pos;
                r.SetPropertyBlock(mpb);
                fish.Add(f);
            }
        }

        void BuildCruisers()
        {
            AddCruiser("Whale", 0, 4.5f, 0.25f);
            AddCruiser("Shark", 0, 8.5f, 0.5f);
            AddCruiser("Mantaray", 0, 6f, 0.25f);
            AddCruiser("Dolphin", 1, 11f, 0.5f);
            AddCruiser("Mantaray", 1, 6f, 0.25f);
            AddCruiser("Dolphin", 1, 10f, 0.5f);
        }

        void AddCruiser(string model, int scene, float speed, float cycles)
        {
            var spec = Creatures.SpecOf(model);
            var go = new GameObject("Cruiser_" + model);
            go.transform.SetParent(transform);
            go.AddComponent<MeshFilter>().sharedMesh = Creatures.Mesh(model);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = fishMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * (spec.length * 0.5f);
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Tint", scene == 0 ? new Color(0.75f, 0.85f, 1f, 1f) : Color.white);
            mpb.SetFloat("_WagAmp", spec.wagAmp);
            mpb.SetFloat("_WagUp", spec.wagUp);
            mpb.SetFloat("_Flap", spec.flap);
            r.SetPropertyBlock(mpb);
            go.SetActive(false);
            cruisers.Add(new Cruiser { t = go.transform, r = r, mpb = mpb, scene = scene, speed = speed, cycles = cycles, seed = Random.Range(0f, 50f) });
        }

        /// <summary>Starts one big animal for the current scene crossing the view, if none is already.</summary>
        void SendCruiser()
        {
            foreach (var c in cruisers) if (c.active) return;
            var pool = new List<Cruiser>();
            foreach (var c in cruisers) if (c.scene == (reef ? 1 : 0)) pool.Add(c);
            if (pool.Count == 0) return;
            var pick = pool[Random.Range(0, pool.Count)];
            pick.active = true;
            pick.dir = Random.value < 0.5f ? 1f : -1f;
            pick.y = Random.Range(-5f, 5f);
            pick.z = Random.Range(55f, 85f);
            pick.t.gameObject.SetActive(true);
            float startX = -pick.dir * 160f;
            pick.t.position = new Vector3(startX, pick.y, pick.z);
            pick.t.rotation = Quaternion.LookRotation(new Vector3(pick.dir, 0f, 0f), Vector3.up);
        }

        void BuildJellies()
        {
            jellyMatBase = Mats.Jelly(Color.white);
            var palette = new[]
            {
                new Color(0.25f, 1.0f, 1.6f), new Color(1.6f, 0.3f, 1.2f), new Color(1.8f, 1.0f, 0.3f), new Color(0.9f, 0.5f, 1.8f),
            };
            for (int i = 0; i < 32; i++)
            {
                var go = new GameObject("Jelly");
                go.transform.SetParent(transform);
                go.AddComponent<MeshFilter>().sharedMesh = jellyMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = jellyMatBase;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float roll = Random.value;
                var j = new Jelly
                {
                    t = go.transform, r = r, mpb = new MaterialPropertyBlock(),
                    mode = roll < 0.45f ? 0 : (roll < 0.9f ? 1 : 2),   // 45% beats 1&3, 45% beats 2&4, 10% free-running
                    period = Random.Range(1.4f, 2.6f),
                    phase = Random.Range(0f, 6.28f),
                    scale = Random.Range(0.9f, 2.4f),
                    col = palette[Random.Range(0, palette.Length)],
                };
                j.timer = Random.Range(0f, j.period);
                j.pulseT = Random.Range(0f, 3f);
                j.t.localScale = Vector3.one * j.scale * 1.3f;
                j.t.position = new Vector3(Random.Range(-70f, 70f), Random.Range(-9f, 12f), Random.Range(6f, 65f));
                jellies.Add(j);
            }
        }

        void BuildAtmosphere()
        {
            // marine snow
            var go = new GameObject("MarineSnow");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(0f, 0f, 45f);
            snow = go.AddComponent<ParticleSystem>();
            snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = snow.main;
            m.loop = true; m.prewarm = true; m.playOnAwake = false;
            m.startLifetime = 30f; m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            m.startColor = new Color(0.7f, 0.95f, 1f, 0.5f);
            m.maxParticles = 1500;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var e = snow.emission; e.rateOverTime = 40f;
            var sh = snow.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(150f, 45f, 100f);
            var v = snow.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.2f); v.y = new ParticleSystem.MinMaxCurve(0.05f, 0.4f); v.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Additive(new Color(1f, 1f, 1f, 1f), 1.3f);
            snow.Play();

            // bubbles: faceted balls, a few steady vents plus bursts on Accent
            var bg = new GameObject("Bubbles");
            bg.transform.SetParent(transform);
            bg.transform.position = new Vector3(0f, SeabedY + 3f, 35f);
            bubbles = bg.AddComponent<ParticleSystem>();
            bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var bm = bubbles.main;
            bm.loop = true; bm.prewarm = true; bm.playOnAwake = false;
            bm.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            bm.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            bm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
            bm.startColor = new Color(1f, 1f, 1f, 0.9f);
            bm.maxParticles = 3000;
            bm.simulationSpace = ParticleSystemSimulationSpace.World;
            var be = bubbles.emission; be.rateOverTime = 14f;
            var bs = bubbles.shape; bs.shapeType = ParticleSystemShapeType.Box; bs.scale = new Vector3(120f, 1f, 60f);
            bg.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // box emits upward
            var bv = bubbles.velocityOverLifetime; bv.enabled = true; bv.space = ParticleSystemSimulationSpace.World;
            bv.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); bv.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); bv.y = 0f;
            var brend = bg.GetComponent<ParticleSystemRenderer>();
            brend.renderMode = ParticleSystemRenderMode.Mesh;
            brend.mesh = BubbleWall.SingleBubble(0.5f);
            brend.sharedMaterial = Mats.Bubble(new Color(1.0f, 1.6f, 2.0f, 1f), 0.1f, false);
            bubbles.Play();

            // the wall of bubbles for the Drop
            wall = gameObject.AddComponent<BubbleWall>();
            wall.Build(CamPos, 3400, cam);

            // light shafts from the surface
            for (int i = 0; i < 16; i++)
            {
                var mat = Mats.Additive(new Color(0.45f, 0.95f, 1f, 1f), 1.1f);
                mat.SetFloat("_VC", 0f);
                float w = Random.Range(5f, 12f);
                var q = Mats.Prim(PrimitiveType.Quad, transform, "Ray", new Vector3(Random.Range(-80f, 80f), 8f, Random.Range(20f, 90f)), new Vector3(w, 70f, 1f), mat);
                q.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(10f, 22f) * (Random.value < 0.5f ? 1f : -1f));
                rays.Add(q.transform);
                rayMats.Add(mat);
                rayBase.Add(Random.Range(0.05f, 0.11f));
            }
        }

        // =================== actions ===================

        void Stroke(Jelly j)
        {
            j.pulseT = 0f;
            j.vel.y += 1.5f + 0.5f * j.scale;               // each stroke pushes it up and a little forward
            j.vel.z += Random.Range(-0.4f, 0.6f);
            j.vel.x += Random.Range(-0.4f, 0.4f);
        }

        void OnBeat(int idx)
        {
            beatEnv = 1f;
            int b = idx < 0 ? -1 : idx % BeatClock.BeatsPerBar;
            foreach (var j in jellies)
            {
                if (j.mode == 2) continue;                    // free-running ones ignore the clock
                bool hit = b < 0 || (j.mode == 0 ? (b == 0 || b == 2) : (b == 1 || b == 3));
                if (hit) j.delay = Random.Range(0f, 0.045f);  // a few tens of ms of jitter
            }
            foreach (var s in schools) s.kick = 1f;
        }

        void OnAccent()
        {
            surge = 1f;
            beatEnv = 1f;
            // a burst of bubbles at a random spot on the seabed in view
            var pos = new Vector3(Random.Range(-40f, 40f), SeabedY + 2f, Random.Range(12f, 55f));
            bubbles.transform.position = pos;
            var shape = bubbles.shape;
            var old = shape.scale;
            shape.scale = new Vector3(6f, 1f, 6f);
            bubbles.Emit(160);
            shape.scale = old;
            bubbles.transform.position = new Vector3(0f, SeabedY + 3f, 35f);
            foreach (var s in schools) s.kick = 2f;           // the schools dart
            SendCruiser();                                     // and something big glides through
        }

        void OnDrop()
        {
            if (wall.Active) return;       // one wall at a time
            wallSwapped = false;
            wall.Play();
        }

        void SwapScenes()
        {
            wallSwapped = true;
            SetScene(!reef, true);
            flash = 0.7f;
            foreach (var j in jellies) j.delay = Random.Range(0f, 0.2f);   // everything strokes at once
            foreach (var s in schools) s.kick = 3f;
            actions.SetBuild(0f);
        }

        void OnReset()
        {
            if (reef) SetScene(false, false);
            revealed = false;
            revealT = -1f;
            revealR = -1f;
            revealDelay = -1f;
            flash = 0f;
            Shader.SetGlobalFloat("_DjRevealR", -1f);
        }

        // =================== per frame ===================

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            float build = actions.Build;

            beatEnv = Mathf.Max(0f, beatEnv - dt * 5f);
            surge = Mathf.Max(0f, surge - dt * 1.5f);
            flash = Mathf.Max(0f, flash - dt * 1.4f);
            Shader.SetGlobalFloat("_DjBeat", Mathf.Clamp01(beatEnv + surge * 0.6f));

            if (wall.Active && !wallSwapped && wall.T >= BubbleWall.SwapAt) SwapScenes();

            UpdateReveal(dt);
            UpdateJellies(dt, t);
            UpdateFish(dt, t, build);
            UpdateCruisers(dt, t);
            UpdateAtmosphere(t, build);
            UpdateHalos(dt);
            UpdateCameraAndPost(t, build);
        }

        void UpdateReveal(float dt)
        {
            if (revealDelay >= 0f) { revealDelay -= dt; if (revealDelay < 0f) revealT = 0f; }
            if (revealT >= 0f)
            {
                revealT += dt;
                revealR = revealT * 150f;                     // the ring crosses the seabed in about 1.7 s
                if (revealR > 320f) { revealR = 1000f; revealT = -1f; }
            }
            else if (!revealed) revealR = -1f;
            Shader.SetGlobalFloat("_DjRevealR", revealR);
        }

        void UpdateJellies(float dt, float t)
        {
            foreach (var j in jellies)
            {
                if (!j.t.gameObject.activeSelf) continue;
                if (j.delay >= 0f) { j.delay -= dt; if (j.delay < 0f) Stroke(j); }
                if (j.mode == 2) { j.timer -= dt; if (j.timer <= 0f) { Stroke(j); j.timer = j.period * Random.Range(0.9f, 1.1f); } }

                j.pulseT += dt;
                float env = j.pulseT < 0.08f ? j.pulseT / 0.08f : Mathf.Exp(-(j.pulseT - 0.08f) * 3.2f);   // fast contraction, slow relax

                j.vel.y -= 0.4f * dt;                                         // sinks between strokes
                j.vel *= Mathf.Exp(-1.1f * dt);
                var p = j.t.position + j.vel * dt + new Vector3(Mathf.Sin(t * 0.2f + j.phase) * 0.3f * dt, 0f, 0f);
                if (p.y > 22f) { p.y = -14f; p.x = Random.Range(-70f, 70f); p.z = Random.Range(6f, 65f); }
                if (p.y < -12f) { p.y = -12f; j.vel.y = Mathf.Max(0f, j.vel.y); }
                j.t.position = p;
                j.t.rotation = Quaternion.Euler(j.vel.z * -6f, t * 3f + j.phase * 20f, j.vel.x * 6f);

                j.mpb.SetFloat("_Pulse", env);
                j.mpb.SetFloat("_Phase", j.phase);
                j.mpb.SetColor("_Color", reef ? j.col * 0.6f : j.col);
                j.r.SetPropertyBlock(j.mpb);
            }
        }

        void UpdateFish(float dt, float t, float build)
        {
            float beatPos = actions.Clock.BeatPos;
            bool synced = actions.Clock.Running;
            foreach (var s in schools)
            {
                if (s.scene != (reef ? 1 : 0)) continue;
                s.kick = Mathf.Max(0f, s.kick - dt * 2.6f);
                float sp = s.speed * (1f + 0.5f * Mathf.Min(s.kick, 1.5f)) * (1f + build * 1.2f);
                s.center.x += s.dir * sp * dt;
                s.center.y = s.y0 + Mathf.Sin(t * 0.25f + s.phase) * 3f;
                s.center.z = s.z0 + Mathf.Sin(t * 0.17f + s.phase * 2f) * 4f;
                float wrap = 105f;
                if (Mathf.Abs(s.center.x) > wrap)
                {
                    float shift = -Mathf.Sign(s.center.x) * wrap * 2f;
                    s.center.x += shift;
                    foreach (var f in fish) if (f.school == s) f.pos.x += shift;
                }
            }
            foreach (var f in fish)
            {
                var s = f.school;
                if (s.scene != (reef ? 1 : 0)) continue;
                float wob = 1f + 0.18f * Mathf.Sin(t * 0.7f + f.seed);
                var target = s.center + f.offset * wob;
                var prev = f.pos;
                f.pos = Vector3.Lerp(f.pos, target, 1f - Mathf.Exp(-2.4f * dt));
                var delta = f.pos - prev;
                var dir = delta.sqrMagnitude > 1e-6f ? delta.normalized : f.fwd;
                f.fwd = Vector3.Slerp(f.fwd, dir, 1f - Mathf.Exp(-5f * dt));
                f.t.position = f.pos;
                f.t.rotation = Quaternion.LookRotation(f.fwd, Vector3.up);

                // tail phase: locked to the beat clock (one flap per beat, or per two beats) unless this fish keeps its own tempo
                float wag = (f.onBeat && synced)
                    ? beatPos * Mathf.PI * 2f * (f.cycles > 0f ? f.cycles : 0.5f) + f.wagOffset
                    : t * f.freeRate + f.seed;
                f.mpb.SetFloat("_Wag", wag);
                f.r.SetPropertyBlock(f.mpb);
            }
        }

        void UpdateCruisers(float dt, float t)
        {
            bool any = false;
            float beatPos = actions.Clock.BeatPos;
            bool synced = actions.Clock.Running;
            foreach (var c in cruisers)
            {
                if (!c.active) continue;
                any = true;
                var p = c.t.position;
                p.x += c.dir * c.speed * (1f + actions.Build * 0.6f) * dt;
                p.y = c.y + Mathf.Sin(t * 0.2f + c.seed) * 2.5f;
                c.t.position = p;
                float wag = synced ? beatPos * Mathf.PI * 2f * c.cycles : t * 1.6f + c.seed;
                c.mpb.SetFloat("_Wag", wag);
                c.r.SetPropertyBlock(c.mpb);
                if (Mathf.Abs(p.x) > 170f && Mathf.Sign(p.x) == Mathf.Sign(c.dir)) { c.active = false; c.t.gameObject.SetActive(false); }
            }
            if (!any)
            {
                cruiserTimer -= dt;
                if (cruiserTimer <= 0f) { SendCruiser(); cruiserTimer = Random.Range(18f, 32f); }
            }
        }

        void UpdateHalos(float dt)
        {
            foreach (var h in halos)
            {
                float dist = Vector2.Distance(h.xz, new Vector2(0f, 40f));
                bool on = !reef && revealed && revealR >= 0f && dist < revealR;
                h.a = Mathf.MoveTowards(h.a, on ? 1f : 0f, dt * 2.2f);
                var c = h.glow * (0.3f * h.a * (0.8f + 0.7f * beatEnv));
                c.a = Mathf.Clamp01(0.55f * h.a);
                h.m.SetColor("_Color", c);
                h.t.rotation = Quaternion.LookRotation(h.t.position - cam.transform.position);
            }
        }

        void UpdateAtmosphere(float t, float build)
        {
            var em = snow.emission; em.rateOverTime = (reef ? 20f : 40f) + build * 120f;
            var be = bubbles.emission; be.rateOverTime = (reef ? 22f : 14f) + build * 90f;
            for (int i = 0; i < rays.Count; i++)
            {
                float a = rayBase[i] * rayMul * (1f + beatEnv * 0.9f + build * 1.2f + flash * 2f);
                var c = new Color(0.45f, 0.95f, 1f, Mathf.Clamp01(a));
                rayMats[i].SetColor("_Color", c);
                rays[i].rotation = Quaternion.Euler(0f, 0f, 15f * (i % 2 == 0 ? 1f : -1f) + Mathf.Sin(t * 0.12f + i) * 4f);
            }
        }

        void UpdateCameraAndPost(float t, float build)
        {
            var pos = CamPos + new Vector3(Mathf.Sin(t * 0.05f) * 5f, Mathf.Sin(t * 0.13f) * 0.8f, 0f);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(CamLook - pos);
            float we = wall.Env;
            cam.fieldOfView = 30f + build * 3f + flash * 2f + we * 4f;

            fx.Bloom.intensity.value = bloomBase + beatEnv * 0.25f + build * 0.5f + flash * 2.5f + we * 1.2f;
            fx.Adjust.postExposure.value = flash * 0.9f;
            fx.Chroma.intensity.value = 0.05f + build * 0.15f + flash * 0.3f + we * 0.35f;
            fx.Vig.intensity.value = 0.3f + build * 0.1f;
        }
    }
}
