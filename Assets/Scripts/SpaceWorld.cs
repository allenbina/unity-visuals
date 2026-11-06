using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// Space world.
    ///   Beat   -> engine rings ripple-flare, engine light swells
    ///   Warp   -> toggle: charge, flash, star streaks, fisheye bend, chromatic aberration, big sustained blast
    /// Always alive (not tied to a control): orbiting planet/moons/sub-moons, crossing satellites, shooting stars,
    /// a drifting procedural nebula sky, and a gentle idle engine blast.
    /// </summary>
    public class SpaceWorld : MonoBehaviour
    {
        int ringCount = 3;
        bool glowRings;                    // additive glow quads (model ship) vs emissive discs (primitive ship)
        const float RippleDelay = 0.07f;
        const float OrbitSpeedScale = 1.6f;
        static readonly Color EngineColor = new Color(0.25f, 0.65f, 1f);
        static readonly Color IdleBlast = new Color(0.6f, 0.9f, 1.7f, 0.4f);
        static readonly Color WarpBlast = new Color(0.7f, 1.0f, 2.0f, 0.13f);

        VisualActions actions;
        Camera cam;
        PostFx fx;

        // ship
        Transform ship;
        Light engineLight;
        Transform[] rings;
        Material[] ringMats;
        Vector3[] ringBaseScale;
        float[] ringPulse;
        float[] ringDelay;
        readonly List<ParticleSystem> blasts = new List<ParticleSystem>();
        readonly List<float> blastShare = new List<float>();
        readonly List<float> blastRadius = new List<float>();
        GameObject[] satPrefabs;
        Material blastMat;

        // sky / stars
        ParticleSystem stars;
        Material starMat;
        ParticleSystem warpStars;
        Material warpStarMat;

        // warp state
        float warpTarget;      // 0 or 1
        float warpRaw;         // linear 0..1 toward target
        float warp;            // eased 0..1, drives everything
        float prevRaw;
        float flash;           // white-out, decays

        // camera rig
        static readonly Vector3 CamBase = new Vector3(6.5f, 1.8f, -8f);
        static readonly Vector3 CamLook = new Vector3(0f, 0.3f, 3f);
        const float FovBase = 30f;

        // orbits
        class Body
        {
            public Transform root;      // moves along the orbit
            public Transform visual;    // spins
            public Body parent;
            public float orbitR, speed, tilt, tiltYaw, phase, ecc, spin;
        }
        readonly List<Body> bodies = new List<Body>();

        // crossings
        class Mover { public Transform t; public Vector3 vel; public Vector3 spin; public float life; public Material lamp; public float blink; public TrailRenderer trail; }
        readonly List<Mover> sats = new List<Mover>();
        readonly List<Mover> shooters = new List<Mover>();
        float nextSat = 2f;
        float nextShooter = 1.5f;
        Material trailMat;

        public void Init(VisualActions a, Camera camera, PostFx postFx)
        {
            actions = a;
            cam = camera;
            fx = postFx;

            // Nebula skybox
            RenderSettings.skybox = Mats.NebulaSky();
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.075f, 0.085f, 0.13f);

            cam.transform.position = CamBase;
            cam.transform.rotation = Quaternion.LookRotation(CamLook - CamBase);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.85f);
            sun.intensity = 1.0f;
            sun.transform.rotation = Quaternion.Euler(18f, 55f, 0f);

            // soft cool fill from the camera side so the ship's rear and the satellites' faces are not black
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(transform);
            fill.type = LightType.Directional;
            fill.color = new Color(0.65f, 0.78f, 1f);
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(22f, -14f, 0f);

            BuildStars();
            BuildWarpStars();
            BuildSystem();
            BuildShip();

            trailMat = Mats.Trail(new Color(3.5f, 3.8f, 4.5f, 1f));

            actions.Beat += _ => Flare();
            actions.WarpChanged += OnWarpChanged;
        }

        // =================== construction ===================

        void BuildStars()
        {
            starMat = Mats.Additive(new Color(2.2f, 2.4f, 3f, 1f), 1.2f);
            var go = new GameObject("Stars");
            go.transform.SetParent(transform);
            stars = go.AddComponent<ParticleSystem>();
            stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = stars.main;
            m.playOnAwake = false;
            m.loop = false;
            m.duration = 1f;
            m.startLifetime = 1e6f;
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.5f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.65f, 0.78f, 1f), new Color(1f, 0.88f, 0.75f));
            m.maxParticles = 5000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var e = stars.emission; e.enabled = false;
            var sh = stars.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(220f, 80f, 110f);
            go.transform.position = new Vector3(0, 0, 75f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = starMat;
            stars.Emit(3200);
            stars.Pause();
        }

        void BuildWarpStars()
        {
            warpStarMat = Mats.Additive(new Color(3.5f, 4.2f, 6f, 1f), 1.0f);
            var go = new GameObject("WarpStars");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(0, 0, 150f);
            warpStars = go.AddComponent<ParticleSystem>();
            warpStars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = warpStars.main;
            m.playOnAwake = false;
            m.loop = true;
            m.startSpeed = 0f;
            m.startLifetime = 0.5f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.88f, 1f), Color.white);
            m.maxParticles = 20000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var e = warpStars.emission; e.rateOverTime = 0f;
            var sh = warpStars.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 1f;
            sh.radiusThickness = 0.82f;        // leave the middle clear: the vanishing point stays open
            sh.scale = new Vector3(170f, 45f, 1f);
            var v = warpStars.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = 0f; v.y = 0f; v.z = -60f;
            var col = warpStars.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = warpStarMat;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1f;
            r.cameraVelocityScale = 0f;
            warpStars.Play();
        }

        void BuildSystem()
        {
            // Gas giant (left) -> big moon -> two sub-moons; a second moon; a ringed ice planet (right) with its own moon.
            var gas = MakeBody("GasGiant", null, new Vector3(-36f, 7f, 78f), 12f,
                Mats.Planet(new Color(0.55f, 0.32f, 0.2f), new Color(0.95f, 0.78f, 0.55f), 7f, 0.85f, 2.2f, new Color(1.0f, 0.65f, 0.35f) * 1.8f, 1.6f, 0f),
                0f, 0f, 0f, 0f, 0f, 4f);
            var moon = MakeBody("Moon", gas, default, 2.8f,
                Mats.Planet(new Color(0.28f, 0.28f, 0.3f), new Color(0.7f, 0.68f, 0.64f), 1f, 0.05f, 5f, new Color(0.5f, 0.6f, 0.9f), 0.5f, 3f),
                21f, 9f, 14f, 20f, 0.08f, 6f);
            MakeBody("SubMoonA", moon, default, 0.75f,
                Mats.Planet(new Color(0.35f, 0.3f, 0.28f), new Color(0.75f, 0.7f, 0.65f), 1f, 0.05f, 7f, new Color(0.5f, 0.6f, 0.9f), 0.4f, 8f),
                5.4f, 42f, 8f, 0f, 0f, 20f);
            MakeBody("SubMoonB", moon, default, 0.55f,
                Mats.Planet(new Color(0.3f, 0.34f, 0.4f), new Color(0.65f, 0.75f, 0.85f), 1f, 0.05f, 6f, new Color(0.6f, 0.8f, 1f), 0.6f, 13f),
                8.2f, -30f, 55f, 130f, 0f, 14f);
            MakeBody("Moon2", gas, default, 1.4f,
                Mats.Planet(new Color(0.45f, 0.25f, 0.2f), new Color(0.85f, 0.6f, 0.45f), 1f, 0.1f, 4f, new Color(1f, 0.6f, 0.4f), 0.5f, 21f),
                32f, 5.5f, -22f, 200f, 0.12f, 9f);

            var ice = MakeBody("IcePlanet", null, new Vector3(52f, -5f, 105f), 7.5f,
                Mats.Planet(new Color(0.2f, 0.35f, 0.5f), new Color(0.75f, 0.9f, 1f), 4f, 0.5f, 2.6f, new Color(0.4f, 0.8f, 1f) * 2f, 1.8f, 31f),
                0f, 0f, 0f, 0f, 0f, 3f);
            MakeBody("IceMoon", ice, default, 1.5f,
                Mats.Planet(new Color(0.5f, 0.55f, 0.6f), new Color(0.95f, 0.97f, 1f), 1f, 0.05f, 5f, new Color(0.6f, 0.85f, 1f), 0.7f, 40f),
                15f, 13f, 18f, 60f, 0.05f, 7f);
            AddRing(ice, 1.35f, 2.2f, 24f, new Color(0.8f, 0.9f, 1f, 0.9f));
            AddRing(gas, 1.7f, 2.3f, 8f, new Color(0.9f, 0.7f, 0.5f, 0.55f));
        }

        Body MakeBody(string name, Body parent, Vector3 pos, float radius, Material mat,
                      float orbitR, float speed, float tilt, float phase, float ecc, float spin)
        {
            var root = new GameObject(name + "_root").transform;
            root.SetParent(transform);
            root.position = pos;
            var vis = Mats.Prim(PrimitiveType.Sphere, root, name, Vector3.zero, Vector3.one * radius * 2f, mat).transform;
            var b = new Body { root = root, visual = vis, parent = parent, orbitR = orbitR, speed = speed, tilt = tilt, tiltYaw = Random.Range(0f, 360f), phase = phase, ecc = ecc, spin = spin };
            bodies.Add(b);
            return b;
        }

        void AddRing(Body planet, float innerMul, float outerMul, float tiltDeg, Color c)
        {
            float rad = planet.visual.localScale.x * 0.5f;
            var go = new GameObject("Ring");
            go.transform.SetParent(planet.root, false);
            go.transform.localRotation = Quaternion.Euler(tiltDeg, 0, 12f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = RingMesh(rad * innerMul, rad * outerMul, 96);
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Ring(c);
        }

        static Mesh RingMesh(float inner, float outer, int seg)
        {
            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
                uvs[i * 2] = new Vector2(0, i / (float)seg);
                uvs[i * 2 + 1] = new Vector2(1, i / (float)seg);
                if (i < seg)
                {
                    int k = i * 6, v = i * 2;
                    tris[k] = v; tris[k + 1] = v + 1; tris[k + 2] = v + 2;
                    tris[k + 3] = v + 1; tris[k + 4] = v + 3; tris[k + 5] = v + 2;
                }
            }
            var m = new Mesh { name = "Ring" };
            m.vertices = verts; m.uv = uvs; m.triangles = tris;
            m.bounds = new Bounds(Vector3.zero, Vector3.one * outer * 2f);
            return m;
        }

        void BuildShip()
        {
            ship = new GameObject("Ship").transform;
            ship.SetParent(transform);
            blastMat = Mats.Additive(IdleBlast, 1.3f);

            var prefab = Resources.Load<GameObject>("Models/Ships/Executioner");
            var tex = Resources.Load<Texture2D>("Models/Ships/Executioner_Blue");
            if (prefab == null) { BuildPrimitiveShip(); return; }

            // Quaternius "Executioner": 10 units wide, faces +Z, nozzles at the rear (found by mesh analysis).
            const float scale = 0.5f;
            var model = Instantiate(prefab, ship);
            model.name = "Executioner";
            model.transform.localScale = prefab.transform.localScale * scale;   // keep the FBX unit-conversion scale
            model.transform.localPosition = new Vector3(0f, -0.15f * scale, -1.52f * scale);   // center the hull on the ship pivot
            if (tex != null) Mats.ApplyTexture(model, tex);
            {
                var rs = model.GetComponentsInChildren<Renderer>();
                var bb = rs.Length > 0 ? rs[0].bounds : new Bounds();
                foreach (var r0 in rs) bb.Encapsulate(r0.bounds);
                Debug.Log("[DjVisuals] ship model renderers=" + rs.Length + " tex=" + (tex != null) + " boundsCenter=" + bb.center + " size=" + bb.size + " shader=" + (rs.Length > 0 ? rs[0].sharedMaterial.shader.name : "-") + " enabled=" + (rs.Length > 0 && rs[0].enabled));
            }

            float[] nx = { 1.0f, -1.0f, 4.5f, -4.5f };   // center-out so the beat ripples outward
            ringCount = nx.Length;
            glowRings = true;
            rings = new Transform[ringCount];
            ringMats = new Material[ringCount];
            ringBaseScale = new Vector3[ringCount];
            ringPulse = new float[ringCount];
            ringDelay = new float[ringCount];
            for (int i = 0; i < ringCount; i++)
            {
                bool outer = Mathf.Abs(nx[i]) > 2f;
                var pos = new Vector3(nx[i] * scale, -0.075f, -2.95f * scale - 0.76f);
                float d = outer ? 1.9f : 1.5f;
                var mat = Mats.Additive(EngineColor * 1.2f, 1.6f);
                mat.SetFloat("_VC", 0f);   // quads have no vertex colors
                var q = Mats.Prim(PrimitiveType.Quad, ship, "NozzleGlow" + i, pos + new Vector3(0, 0, -0.03f), new Vector3(d, d, 1f), mat);
                rings[i] = q.transform;
                ringMats[i] = mat;
                ringBaseScale[i] = q.transform.localScale;
                ringDelay[i] = -1f;
                MakeBlast(pos, outer ? 0.34f : 0.28f, 1f / ringCount);
            }

            var lg = new GameObject("EngineLight");
            lg.transform.SetParent(ship, false);
            lg.transform.localPosition = new Vector3(0, 0, -3.6f);
            engineLight = lg.AddComponent<Light>();
            engineLight.type = LightType.Point;
            engineLight.color = EngineColor;
            engineLight.range = 18f;
            engineLight.intensity = 0.7f;
        }

        void BuildPrimitiveShip()
        {
            var hull = Mats.Lit(new Color(0.58f, 0.6f, 0.68f), 0.65f, 0.7f);
            var dark = Mats.Lit(new Color(0.12f, 0.13f, 0.17f), 0.5f, 0.6f);

            var body = Mats.Prim(PrimitiveType.Capsule, ship, "Body", Vector3.zero, new Vector3(1.1f, 1.6f, 1.1f), hull);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var wl = Mats.Prim(PrimitiveType.Cube, ship, "WingL", new Vector3(-1.5f, -0.1f, -0.4f), new Vector3(2.2f, 0.08f, 1.5f), hull);
            wl.transform.localRotation = Quaternion.Euler(0, 0, 8);
            var wr = Mats.Prim(PrimitiveType.Cube, ship, "WingR", new Vector3(1.5f, -0.1f, -0.4f), new Vector3(2.2f, 0.08f, 1.5f), hull);
            wr.transform.localRotation = Quaternion.Euler(0, 0, -8);
            Mats.Prim(PrimitiveType.Cube, ship, "Fin", new Vector3(0, 0.55f, -0.7f), new Vector3(0.08f, 0.7f, 1.0f), hull);
            Mats.Prim(PrimitiveType.Cube, ship, "Spine", new Vector3(0, 0.38f, 0.1f), new Vector3(0.25f, 0.18f, 1.7f), dark);

            ringCount = 3;
            glowRings = false;
            rings = new Transform[ringCount];
            ringMats = new Material[ringCount];
            ringBaseScale = new Vector3[ringCount];
            ringPulse = new float[ringCount];
            ringDelay = new float[ringCount];
            for (int i = 0; i < ringCount; i++)
            {
                var mat = Mats.Lit(new Color(0.05f, 0.1f, 0.18f), 0.4f, 0f);
                Mats.Emission(mat, EngineColor * 0.5f);
                float d = 1.15f - i * 0.18f;
                var scale = new Vector3(d, 0.03f, d);
                var r = Mats.Prim(PrimitiveType.Cylinder, ship, "EngineRing" + i, new Vector3(0, 0, -1.3f - i * 0.55f), scale, mat);
                r.transform.localRotation = Quaternion.Euler(90, 0, 0);
                rings[i] = r.transform;
                ringMats[i] = mat;
                ringBaseScale[i] = scale;
                ringDelay[i] = -1f;
            }

            var lg = new GameObject("EngineLight");
            lg.transform.SetParent(ship, false);
            lg.transform.localPosition = new Vector3(0, 0, -3.2f);
            engineLight = lg.AddComponent<Light>();
            engineLight.type = LightType.Point;
            engineLight.color = EngineColor;
            engineLight.range = 16f;
            engineLight.intensity = 0.7f;

            MakeBlast(new Vector3(0, 0, -1.5f), 0.38f, 1f);
        }

        void MakeBlast(Vector3 localPos, float radius, float share)
        {
            var go = new GameObject("Blast");
            go.transform.SetParent(ship, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back);   // cone emits along local +Z -> world -Z
            var blast = go.AddComponent<ParticleSystem>();
            blast.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = blast.main;
            m.playOnAwake = false;
            m.loop = true;
            m.startLifetime = 0.5f;
            m.startSpeed = 12f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            m.maxParticles = 12000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var e = blast.emission; e.rateOverTime = 420f;
            var sh = blast.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 4f;
            sh.radius = radius;
            var sol = blast.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(0.25f, 1f), new Keyframe(1, 0.15f)));
            var col = blast.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 1f, 1f), 0), new GradientColorKey(new Color(0.45f, 0.75f, 1f), 0.35f), new GradientColorKey(new Color(0.2f, 0.3f, 1f), 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.75f, 0.45f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = blastMat;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1.6f;
            r.cameraVelocityScale = 0f;
            blast.Play();
            blasts.Add(blast);
            blastShare.Add(share);
            blastRadius.Add(radius);
        }

        // =================== actions ===================

        void Flare()
        {
            for (int i = 0; i < ringCount; i++) ringDelay[i] = (glowRings ? i / 2 : i) * RippleDelay * (glowRings ? 1.6f : 1f);
        }

        void OnWarpChanged(bool on)
        {
            warpTarget = on ? 1f : 0f;
            flash = Mathf.Max(flash, 0.35f);
        }

        // =================== per frame ===================

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            UpdateWarp(dt);
            UpdateShip(dt, t);
            UpdateBodies(dt);
            UpdateCrossings(dt);
            UpdateCamera(t);
            UpdatePost();
            Shader.SetGlobalFloat("_DjWarp", warp);
        }

        void UpdateWarp(float dt)
        {
            prevRaw = warpRaw;
            float dur = warpTarget > 0f ? 2.4f : 3.0f;
            warpRaw = Mathf.MoveTowards(warpRaw, warpTarget, dt / dur);
            warp = warpRaw * warpRaw * (3f - 2f * warpRaw);

            // The "jump": a white flash as we cross the threshold in either direction.
            if (warpTarget > 0f && prevRaw < 0.5f && warpRaw >= 0.5f) flash = 1.6f;
            if (warpTarget <= 0f && prevRaw > 0.4f && warpRaw <= 0.4f) flash = 1.0f;
            flash = Mathf.Max(0f, flash - dt * 1.6f);

            // streaking stars
            float speed = Mathf.Lerp(50f, 430f, warp);
            var em = warpStars.emission;
            em.rateOverTime = warp > 0.01f ? warp * warp * 5200f : 0f;
            var m = warpStars.main;
            m.startLifetime = Mathf.Min(2.5f, 170f / speed);
            var v = warpStars.velocityOverLifetime;
            v.z = new ParticleSystem.MinMaxCurve(-speed);

            // regular stars dim as the streaks take over
            float k = 1f - 0.9f * warp;
            starMat.SetColor("_Color", new Color(2.2f * k, 2.4f * k, 3f * k, 1f));
        }

        void UpdateShip(float dt, float t)
        {
            float jitter = warp * 0.03f;
            ship.localPosition = new Vector3(Mathf.Sin(t * 47f) * jitter, Mathf.Sin(t * 0.8f) * 0.08f + Mathf.Sin(t * 53f) * jitter, 0);
            ship.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.5f) * 0.8f, 0, Mathf.Sin(t * 0.7f) * 1.5f);

            float lightPulse = 0f;
            for (int i = 0; i < ringCount; i++)
            {
                if (ringDelay[i] >= 0f)
                {
                    ringDelay[i] -= dt;
                    if (ringDelay[i] < 0f) { ringPulse[i] = 1f; ringDelay[i] = -1f; }
                }
                ringPulse[i] = Mathf.Max(0f, ringPulse[i] - dt * 4.5f);
                float p = ringPulse[i] * ringPulse[i];
                if (glowRings)
                {
                    float g = 1f + p * 0.7f + warp * 0.35f;
                    rings[i].localScale = new Vector3(ringBaseScale[i].x * g, ringBaseScale[i].y * g, 1f);
                    var gc = EngineColor * (1.2f + warp * 2.5f + p * 6f);
                    gc.a = Mathf.Clamp01(0.55f + p * 0.45f + warp * 0.3f);
                    ringMats[i].SetColor("_Color", gc);
                }
                else
                {
                    float grow = 1f + p * 0.55f;
                    rings[i].localScale = new Vector3(ringBaseScale[i].x * grow, ringBaseScale[i].y * (1f + p * 2f), ringBaseScale[i].z * grow);
                    Mats.Emission(ringMats[i], EngineColor * (0.5f + warp * 2f + p * 4f));
                }
                lightPulse = Mathf.Max(lightPulse, p);
            }
            engineLight.intensity = 0.7f + warp * 6f + lightPulse * 4f + flash * 4f;

            // sustained blast: modest at idle, huge in warp; beat adds a kick
            for (int bi = 0; bi < blasts.Count; bi++)
            {
                var b = blasts[bi];
                var em = b.emission;
                em.rateOverTime = Mathf.Lerp(500f, 1000f, warp) * blastShare[bi] * (1f + lightPulse * 0.4f);
                var m = b.main;
                m.startSpeed = Mathf.Lerp(12f, 26f, warp) * (1f + lightPulse * 0.25f);
                m.startLifetime = Mathf.Lerp(0.5f, 0.6f, warp);
                m.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.2f, 0.4f, warp), Mathf.Lerp(0.4f, 0.8f, warp));
                var sh = b.shape;
                sh.radius = blastRadius[bi] * Mathf.Lerp(0.95f, 1.25f, warp);
            }
            blastMat.SetColor("_Color", Color.Lerp(IdleBlast, WarpBlast, warp) * (1f + lightPulse * 0.5f));
        }

        void UpdateBodies(float dt)
        {
            float time = Time.time;
            foreach (var b in bodies)   // parents are always created before children
            {
                if (b.parent != null)
                {
                    float a = (b.phase + b.speed * OrbitSpeedScale * time) * Mathf.Deg2Rad;
                    var local = new Vector3(Mathf.Cos(a) * b.orbitR, 0f, Mathf.Sin(a) * b.orbitR * (1f - b.ecc));
                    local = Quaternion.Euler(b.tilt, b.tiltYaw, 0f) * local;
                    b.root.position = b.parent.root.position + local;
                }
                b.visual.Rotate(0f, b.spin * dt, 0f, Space.Self);
            }
        }

        void UpdateCamera(float t)
        {
            var shake = new Vector3(Mathf.PerlinNoise(t * 9f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 9f) - 0.5f, 0f) * (0.12f * warp);
            cam.transform.position = CamBase + shake + new Vector3(0, 0, -warp * 0.8f);
            cam.transform.rotation = Quaternion.LookRotation(CamLook - cam.transform.position);
            cam.fieldOfView = FovBase + warp * 14f + flash * 3f;
        }

        void UpdatePost()
        {
            fx.Bloom.intensity.value = 1.1f + warp * 2.2f + flash * 4f;
            fx.Adjust.postExposure.value = flash * 1.8f + warp * 0.25f;
            fx.Lens.intensity.value = warp * 0.7f;
            fx.Chroma.intensity.value = warp * 0.9f + flash * 0.4f;
            fx.Vig.intensity.value = 0.2f + warp * 0.3f;
        }

        // =================== satellites and shooting stars ===================

        void UpdateCrossings(float dt)
        {
            nextSat -= dt;
            if (nextSat <= 0f) { SpawnSatellite(); nextSat = Random.Range(8f, 17f); }
            nextShooter -= dt;
            if (nextShooter <= 0f) { SpawnShootingStar(); nextShooter = Random.Range(1.6f, 5.5f); }

            for (int i = sats.Count - 1; i >= 0; i--)
            {
                var s = sats[i];
                s.t.position += s.vel * dt;
                s.t.Rotate(s.spin * dt, Space.Self);
                s.blink += dt;
                float on = Mathf.Repeat(s.blink, 1.3f) < 0.12f ? 1f : 0.05f;
                if (s.lamp != null) Mats.Emission(s.lamp, new Color(6f, 0.6f, 0.4f) * on);
                if (Mathf.Abs(s.t.position.x) > 95f) { Destroy(s.t.gameObject); sats.RemoveAt(i); }
            }
            for (int i = shooters.Count - 1; i >= 0; i--)
            {
                var s = shooters[i];
                s.t.position += s.vel * dt;
                s.life -= dt;
                if (s.life <= 0f)
                {
                    s.trail.emitting = false;
                    Destroy(s.t.gameObject, s.trail.time + 0.1f);
                    shooters.RemoveAt(i);
                }
            }
        }

        /// <summary>Test hook: drop a row of satellites into frame so the headless capture can inspect them.</summary>
        public void DebugSpawnSatellites()
        {
            for (int i = 0; i < 5; i++) SpawnSatellite(-26f + i * 13f);
        }

        void SpawnSatellite(float? startX = null)
        {
            float dir = Random.value < 0.5f ? -1f : 1f;
            var root = new GameObject("Satellite").transform;
            root.SetParent(transform);
            float z = Random.Range(12f, 40f);
            root.position = new Vector3(startX ?? -dir * 70f, Random.Range(-4f, 9f), z);
            if (satPrefabs == null) satPrefabs = Resources.LoadAll<GameObject>("Models/Sats");
            if (satPrefabs.Length > 0)
            {
                SpawnModelSatellite(root, dir);
                return;
            }
            float s = Random.Range(1.2f, 2.2f);
            root.localScale = Vector3.one * s;
            root.rotation = Random.rotation;

            var foil = Mats.Lit(new Color(0.9f, 0.75f, 0.35f), 0.7f, 0.9f);
            var panel = Mats.Lit(new Color(0.04f, 0.09f, 0.3f), 0.85f, 0.9f);
            var metal = Mats.Lit(new Color(0.75f, 0.77f, 0.8f), 0.75f, 1f);
            var lamp = Mats.Lit(Color.black, 0.2f, 0f);
            Mats.Emission(lamp, Color.black);

            Mats.Prim(PrimitiveType.Cube, root, "Bus", Vector3.zero, new Vector3(0.55f, 0.42f, 0.42f), foil);
            Mats.Prim(PrimitiveType.Cube, root, "PanelL", new Vector3(-1.05f, 0, 0), new Vector3(1.5f, 0.02f, 0.6f), panel);
            Mats.Prim(PrimitiveType.Cube, root, "PanelR", new Vector3(1.05f, 0, 0), new Vector3(1.5f, 0.02f, 0.6f), panel);
            Mats.Prim(PrimitiveType.Cylinder, root, "Boom", new Vector3(0, 0.32f, 0), new Vector3(0.04f, 0.18f, 0.04f), metal);
            Mats.Prim(PrimitiveType.Sphere, root, "Dish", new Vector3(0, 0.55f, 0), new Vector3(0.4f, 0.1f, 0.4f), metal);
            Mats.Prim(PrimitiveType.Sphere, root, "Lamp", new Vector3(0, -0.28f, 0), Vector3.one * 0.1f, lamp);

            sats.Add(new Mover
            {
                t = root,
                vel = new Vector3(dir * Random.Range(4.5f, 8f), Random.Range(-0.2f, 0.4f), 0f),
                spin = new Vector3(Random.Range(-14f, 14f), Random.Range(-14f, 14f), Random.Range(-14f, 14f)),
                lamp = lamp,
                blink = Random.Range(0f, 1.3f),
            });
        }

        void SpawnModelSatellite(Transform root, float dir)
        {
            var prefab = satPrefabs[Random.Range(0, satPrefabs.Length)];
            var model = Instantiate(prefab, root);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // The NASA models come in wildly different units (0.1 to 500): normalize to a target size, then center on the pivot.
            Bounds WorldBounds()
            {
                var rs = model.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                return b;
            }
            var size = WorldBounds().size;
            float maxDim = Mathf.Max(0.0001f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
            float target = Random.Range(2.4f, 4.4f) * (prefab.name.Contains("Space_Station") ? 1.8f : 1f);
            model.transform.localScale *= target / maxDim;   // multiply: the import carries its own unit scale
            model.transform.position += root.position - WorldBounds().center;
            root.rotation = Random.rotation;

            sats.Add(new Mover
            {
                t = root,
                vel = new Vector3(dir * Random.Range(4.5f, 8f), Random.Range(-0.2f, 0.4f), 0f),
                spin = new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f)),
                lamp = null,
            });
        }

        void SpawnShootingStar()
        {
            float dir = Random.value < 0.5f ? -1f : 1f;
            var go = new GameObject("ShootingStar");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(-dir * Random.Range(35f, 80f), Random.Range(10f, 28f), Random.Range(45f, 110f));
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.7f;
            tr.widthCurve = AnimationCurve.Linear(0f, 0.7f, 1f, 0f);
            tr.sharedMaterial = trailMat;
            tr.alignment = LineAlignment.View;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.5f, 0.75f, 1f), 0.5f), new GradientColorKey(new Color(0.3f, 0.4f, 1f), 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0, 1) });
            tr.colorGradient = g;
            shooters.Add(new Mover
            {
                t = go.transform,
                vel = new Vector3(dir * Random.Range(70f, 115f), -Random.Range(14f, 38f), 0f),
                life = Random.Range(0.9f, 1.7f),
                trail = tr,
            });
        }
    }
}
