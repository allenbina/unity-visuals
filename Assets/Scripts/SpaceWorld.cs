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
        // Camera angles and ship placements that Accent cycles through. The ship is placed in the left, center or
        // right third of the wide view (one panel each) relative to whichever angle the camera is moving to.
        class CamPose { public Vector3 pos, look; public float fov; }
        CamPose[] poses;
        int poseIdx;
        int slotIdx;
        static readonly float[] SlotOrder = { 0f, 1f, 2f };   // indexes into slotFrac: 0 home/center, 1 left panel, 2 right panel
        float[] slotFrac;
        Vector3 camPosS, camPosVel, camLookS, camLookVel;
        float camFovS, camFovVel;
        Vector3 shipHome, shipTarget, shipVel;
        const float WideAspect = 3840f / 720f;

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
        // destinations (hyperspace lands in a new region)
        class Dest
        {
            public string name;
            public Color a, b, c;                  // sky cloud colors
            public float intensity, cover, scale, seed;
            public Color sunColor, ambient;
            public float sunIntensity;
            public Vector3 sunEuler;
            public System.Action build;
        }
        Dest[] dests;
        int destIndex;
        Transform systemRoot;
        Material skyMat;
        Light sunLight;
        readonly List<Transform> billboards = new List<Transform>();
        class Rock { public Transform t; public Transform visual; public Vector3 spin; }
        readonly List<Rock> rocks = new List<Rock>();
        const float RockDrift = 5f;
        float reveal;              // arrival glow: swells in, then decays over a few seconds
        bool revealRising;

        // speed stretch: objects elongate along Z in proportion to their speed during a transition
        readonly List<Transform> stretch = new List<Transform>();
        float lastStretch = 1f;
        const float StretchPerSpeed = 0.011f;
        const float StretchMax = 30f;

        // foreground silhouettes whipping past the camera, and the light sweep across the panels
        readonly List<Mover> shards = new List<Mover>();
        Material shardMat;
        Transform sweep;
        Material sweepMat;
        float sweepT = -1f;
        const float SweepDur = 1.9f;

        // transition between regions: old one rushes away, new one decelerates in, camera rolls, iris closes/opens
        bool systemGone;           // old region has left and the next has not arrived yet
        float leaveT = -1f;        // seconds while the old region rushes past, -1 = idle
        float arriveT = -1f;       // seconds while the new region decelerates in, -1 = idle
        bool dropPending;          // Drop without warp: arrive once the leave finishes
        float pulseT = -1f;        // mini warp swell for a warp-less Drop
        float rollAngle, rollGoal, rollVel;
        float rollBlend;           // 0 upright .. 1 upside down
        float vigExtra;
        const float LeaveDur = 1.1f;
        const float ArriveDur = 3.8f;
        const float ArriveDist = 700f;
        bool pendingArrive;        // leaving warp lands at the next destination
        float surge;               // accent kick on the engines
        float dropRelease = -1f;   // Build easing back to 0 after a Drop
        float dropFrom;

        float nextSat = 2f;
        float nextShooter = 1.5f;
        Material trailMat;

        public void Init(VisualActions a, Camera camera, PostFx postFx)
        {
            actions = a;
            cam = camera;
            fx = postFx;

            // Nebula skybox
            skyMat = Mats.NebulaSky();
            RenderSettings.skybox = skyMat;
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.075f, 0.085f, 0.13f);

            BuildPoses();
            camPosS = poses[0].pos; camLookS = poses[0].look; camFovS = poses[0].fov;
            cam.transform.position = camPosS;
            cam.transform.rotation = Quaternion.LookRotation(camLookS - camPosS);

            var sun = sunLight = new GameObject("Sun").AddComponent<Light>();
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
            systemRoot = new GameObject("System").transform;
            systemRoot.SetParent(transform);
            BuildDests();
            BuildShip();
            ApplyDestination(0);
            BuildSweep();

            trailMat = Mats.Trail(new Color(3.5f, 3.8f, 4.5f, 1f));

            actions.Beat += _ => Flare();
            actions.WarpChanged += OnWarpChanged;
            actions.Drop += OnDrop;
            actions.Accent += OnAccent;
            actions.ResetRequested += OnReset;
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

        void BuildHome()
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
            root.SetParent(systemRoot);
            root.position = pos;
            var vis = Mats.Prim(PrimitiveType.Sphere, root, name, Vector3.zero, Vector3.one * radius * 2f, mat).transform;
            var b = new Body { root = root, visual = vis, parent = parent, orbitR = orbitR, speed = speed, tilt = tilt, tiltYaw = Random.Range(0f, 360f), phase = phase, ecc = ecc, spin = spin };
            bodies.Add(b);
            stretch.Add(root);
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

        // =================== camera angles and ship slots ===================

        void BuildPoses()
        {
            poses = new[]
            {
                new CamPose { pos = new Vector3(6.5f, 1.8f, -8f),    look = new Vector3(0f, 0.3f, 3f), fov = 30f },   // the original three-quarter chase
                new CamPose { pos = new Vector3(-6.5f, 0.8f, -8.5f), look = new Vector3(0f, 0.4f, 3f), fov = 30f },   // low, from the left
                new CamPose { pos = new Vector3(0.5f, 6.0f, -10.5f), look = new Vector3(0f, 0f, 4f),   fov = 32f },   // high, behind, looking down
                new CamPose { pos = new Vector3(8.5f, -0.8f, -4.5f), look = new Vector3(0f, 0.3f, 4f), fov = 34f },   // close and low, nacelles up front
                new CamPose { pos = new Vector3(-2.5f, 2.5f, -17f),  look = new Vector3(0f, 0.3f, 4f), fov = 28f },   // far wide chase
            };
            // The first pose with the ship at the origin is the home layout; slotFrac[0] reproduces it exactly.
            var p0 = poses[0];
            var fwd = (p0.look - p0.pos).normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            float d = Vector3.Dot(-p0.pos, fwd);
            float halfW = d * Mathf.Tan(p0.fov * 0.5f * Mathf.Deg2Rad) * WideAspect;
            float homeFrac = Vector3.Dot(-(p0.pos + fwd * d), right) / halfW;
            slotFrac = new[] { homeFrac, -0.62f, 0.62f };
            shipHome = shipTarget = Vector3.zero;
        }

        /// <summary>World position that makes the ship appear at screen-x fraction frac (-1 left edge .. 1 right edge) for a camera pose.</summary>
        Vector3 SlotPos(CamPose p, float frac)
        {
            var fwd = (p.look - p.pos).normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up = Vector3.Cross(fwd, right);
            float d = Vector3.Dot(-p.pos, fwd);   // how deep the ship's default spot sits along the view axis
            var c = p.pos + fwd * d;
            float halfW = d * Mathf.Tan(p.fov * 0.5f * Mathf.Deg2Rad) * WideAspect;
            float yOff = Vector3.Dot(-c, up);
            return c + right * (frac * halfW) + up * yOff;
        }

        void NextShot()
        {
            poseIdx = (poseIdx + 1) % poses.Length;
            slotIdx = (slotIdx + 1) % slotFrac.Length;
            shipTarget = SlotPos(poses[poseIdx], slotFrac[slotIdx]);
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
            // Dropping out of a real jump lands somewhere new; cancelling during the charge-up does not.
            pendingArrive = !on && (systemGone || leaveT >= 0f);
        }

        void OnDrop()
        {
            if (actions.Warping) { actions.ToggleWarp(); return; }   // leaving warp already arrives somewhere new
            if (leaveT >= 0f || systemGone || arriveT >= 0f) return;   // a transition is already running
            flash = 0.6f;
            LeaveSystem();
            dropPending = true;
            pulseT = 0f;
            dropFrom = actions.Build;
            dropRelease = 0f;
        }

        void OnAccent()
        {
            surge = 1f;
            SpawnShootingStar();
            SpawnShootingStar();
            Flare();
            NextShot();   // the ship glides to another panel while the camera swings to a new angle
        }

        void OnReset()
        {
            warpTarget = 0f; warpRaw = 0f; prevRaw = 0f; pendingArrive = false;
            surge = 0f; dropRelease = -1f;
            systemGone = false; leaveT = -1f; arriveT = -1f; dropPending = false; pulseT = -1f;
            rollAngle = rollGoal = rollVel = 0f;
            ApplyDestination(0);
            reveal = 0f;
            flash = 0.8f;
            poseIdx = 0; slotIdx = 0;
            shipTarget = Vector3.zero;
        }

        // =================== destinations ===================

        void BuildDests()
        {
            dests = new[]
            {
                new Dest { name = "Home System", a = new Color(0.05f, 0.12f, 0.55f), b = new Color(0.65f, 0.12f, 0.45f), c = new Color(0.15f, 0.55f, 0.75f),
                    intensity = 1.3f, cover = 0f, scale = 1.7f, seed = 0f, sunColor = new Color(1f, 0.93f, 0.85f), sunIntensity = 1f,
                    sunEuler = new Vector3(18f, 55f, 0f), ambient = new Color(0.075f, 0.085f, 0.13f), build = BuildHome },
                new Dest { name = "Ember Nebula", a = new Color(0.9f, 0.3f, 0.05f), b = new Color(0.5f, 0.06f, 0.18f), c = new Color(0.04f, 0.6f, 0.85f),
                    intensity = 1.05f, cover = 0.12f, scale = 1.2f, seed = 5f, sunColor = new Color(1f, 0.8f, 0.7f), sunIntensity = 0.8f,
                    sunEuler = new Vector3(25f, -60f, 0f), ambient = new Color(0.1f, 0.07f, 0.08f), build = BuildEmberNebula },
                new Dest { name = "Twin Suns", a = new Color(0.4f, 0.12f, 0.03f), b = new Color(0.08f, 0.14f, 0.55f), c = new Color(1f, 0.7f, 0.3f),
                    intensity = 1.0f, cover = -0.04f, scale = 2.2f, seed = 11f, sunColor = new Color(1f, 0.85f, 0.7f), sunIntensity = 1.2f,
                    sunEuler = new Vector3(10f, 90f, 0f), ambient = new Color(0.09f, 0.07f, 0.1f), build = BuildTwinSuns },
                new Dest { name = "Shard Field", a = new Color(0.12f, 0.08f, 0.22f), b = new Color(0.5f, 0.2f, 0.1f), c = new Color(0.2f, 0.3f, 0.5f),
                    intensity = 0.85f, cover = 0f, scale = 1.7f, seed = 23f, sunColor = new Color(1f, 0.95f, 0.9f), sunIntensity = 1.5f,
                    sunEuler = new Vector3(12f, -105f, 0f), ambient = new Color(0.06f, 0.06f, 0.09f), build = BuildAsteroidField },
            };
        }

        void LeaveSystem()
        {
            if (leaveT >= 0f || systemGone) return;
            leaveT = 0f;
            arriveT = -1f;
            SpawnShards(7);
        }

        void ArriveNext()
        {
            ApplyDestination((destIndex + 1) % dests.Length);   // built at the origin, then pushed far ahead
            systemGone = false;
            leaveT = -1f;
            systemRoot.position = new Vector3(0f, 0f, ArriveDist);
            arriveT = 0f;
            reveal = 0f;
            revealRising = true;
            sweepT = 0f;
            SpawnShards(7);
        }

        void UpdateTransition(float dt)
        {
            if (leaveT >= 0f)
            {
                leaveT += dt;
                float k = Mathf.Clamp01(leaveT / LeaveDur);
                systemRoot.position = new Vector3(0f, 0f, -ArriveDist * k * k * k);   // accelerates away behind us
                if (leaveT >= LeaveDur)
                {
                    leaveT = -1f;
                    systemGone = true;
                    systemRoot.gameObject.SetActive(false);
                    if (dropPending) { dropPending = false; ArriveNext(); }
                }
            }
            if (arriveT >= 0f)
            {
                arriveT += dt;
                float k = Mathf.Clamp01(arriveT / ArriveDur);
                float inv = 1f - k;
                float ease = 1f - inv * inv * inv * inv;   // fast at first, settles gently
                systemRoot.position = new Vector3(0f, 0f, ArriveDist * (1f - ease));
                if (k >= 1f) { arriveT = -1f; systemRoot.position = Vector3.zero; }
            }

            // arrival glow swells in over about a third of a second, then fades
            if (revealRising)
            {
                reveal += dt * 2.5f;
                if (reveal >= 0.85f) revealRising = false;
            }
            else reveal = Mathf.Max(0f, reveal - dt / 3.5f);

            // iris: closes while the region is gone, opens slowly on arrival
            float vigTarget = (systemGone || leaveT >= 0f) ? 0.35f : 0f;
            vigExtra = Mathf.MoveTowards(vigExtra, vigTarget, dt * (vigTarget > vigExtra ? 0.7f : 0.12f));

            // warp-less Drop: a mini warp swell so the swap is hidden inside streaking stars
            if (pulseT >= 0f)
            {
                pulseT += dt;
                if (pulseT > 3.8f) pulseT = -1f;
            }

            // Stretch every object in the region along Z in proportion to how fast the region is moving:
            // streaks as it rushes away, long streaks that relax as the new region decelerates in.
            float vel = 0f;
            if (leaveT >= 0f)
            {
                float k = Mathf.Clamp01(leaveT / LeaveDur);
                vel = 3f * ArriveDist * k * k / LeaveDur;
            }
            else if (arriveT >= 0f)
            {
                float inv = 1f - Mathf.Clamp01(arriveT / ArriveDur);
                vel = ArriveDist * 4f * inv * inv * inv / ArriveDur;
            }
            float st = 1f + Mathf.Min(vel * StretchPerSpeed, StretchMax);
            if (!Mathf.Approximately(st, lastStretch))
            {
                lastStretch = st;
                foreach (var t in stretch) if (t != null) t.localScale = new Vector3(1f, 1f, st);
            }

            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var sh = shards[i];
                sh.t.position += sh.vel * dt;
                if (sh.t.position.z < -30f) { Destroy(sh.t.gameObject); shards.RemoveAt(i); }
            }
        }

        /// <summary>Dark elongated shapes that whip past close to the camera: a foreground layer for depth.</summary>
        void SpawnShards(int n)
        {
            if (shardMat == null)
                shardMat = Mats.Planet(new Color(0.02f, 0.02f, 0.03f), new Color(0.12f, 0.1f, 0.12f), 1f, 0f, 4f, new Color(0.3f, 0.4f, 0.7f), 0.5f, 140f);
            for (int i = 0; i < n; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float x = side < 0f ? Random.Range(-22f, -6f) : Random.Range(10f, 24f);   // keep the camera and ship lanes clear
                var pivot = new GameObject("Shard").transform;
                pivot.SetParent(transform);
                pivot.position = new Vector3(x, Random.Range(-8f, 8f), Random.Range(2f, 30f));
                float size = Random.Range(1.4f, 3.6f);
                var rock = Mats.Prim(PrimitiveType.Sphere, pivot, "ShardBody", Vector3.zero, new Vector3(size, size * 0.7f, size * 1.4f), shardMat);
                rock.transform.localRotation = Random.rotation;
                pivot.localScale = new Vector3(1f, 1f, Random.Range(14f, 30f));   // already a streak
                shards.Add(new Mover { t = pivot, vel = new Vector3(0f, 0f, -Random.Range(180f, 300f)) });
            }
        }

        void BuildSweep()
        {
            sweepMat = Mats.Additive(new Color(2.2f, 2.8f, 4f, 0f), 1.1f);
            sweepMat.SetFloat("_VC", 0f);
            var q = Mats.Prim(PrimitiveType.Quad, cam.transform, "LightSweep", new Vector3(0f, 0f, 5f), Vector3.one, sweepMat);
            sweep = q.transform;
            sweep.gameObject.SetActive(false);
        }

        /// <summary>A soft vertical edge of light travels left to right across all three panels while the new region arrives.</summary>
        void UpdateSweep(float dt)
        {
            if (sweepT < 0f) { if (sweep.gameObject.activeSelf) sweep.gameObject.SetActive(false); return; }
            sweepT += dt;
            float k = sweepT / SweepDur;
            if (k >= 1f) { sweepT = -1f; sweep.gameObject.SetActive(false); return; }
            sweep.gameObject.SetActive(true);

            const float dist = 5f;
            float halfH = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            float e = k * k * (3f - 2f * k);                        // ease in/out
            sweep.localPosition = new Vector3(Mathf.Lerp(-1.2f, 1.2f, e) * halfW, 0f, dist);
            sweep.localScale = new Vector3(halfW * 0.16f, halfH * 3f, 1f);
            float a = Mathf.Sin(k * Mathf.PI);
            sweepMat.SetColor("_Color", new Color(1.6f, 2.2f, 3.4f, 0.55f * a));
        }

        void ApplyDestination(int i)
        {
            destIndex = i;
            var d = dests[i];
            systemRoot.gameObject.SetActive(true);
            systemRoot.position = Vector3.zero;
            // clear the old region
            for (int k = systemRoot.childCount - 1; k >= 0; k--) Destroy(systemRoot.GetChild(k).gameObject);
            bodies.Clear();
            billboards.Clear();
            rocks.Clear();
            stretch.Clear();
            lastStretch = -1f;

            skyMat.SetColor("_ColA", d.a);
            skyMat.SetColor("_ColB", d.b);
            skyMat.SetColor("_ColC", d.c);
            skyMat.SetFloat("_Cover", d.cover);
            skyMat.SetFloat("_Scale", d.scale);
            skyMat.SetFloat("_Seed", d.seed);
            sunLight.color = d.sunColor;
            sunLight.intensity = d.sunIntensity;
            sunLight.transform.rotation = Quaternion.Euler(d.sunEuler);
            RenderSettings.ambientLight = d.ambient;

            d.build();   // may override the sun direction
            actions.Status = "Destination: " + d.name;
        }

        Transform Halo(Transform parent, float size, Color hdr)
        {
            var mat = Mats.Additive(hdr, 2f);
            mat.SetFloat("_VC", 0f);
            var q = Mats.Prim(PrimitiveType.Quad, parent, "Halo", Vector3.zero, new Vector3(size, size, 1f), mat);
            billboards.Add(q.transform);
            return q.transform;
        }

        void BuildEmberNebula()
        {
            // Big soft dust clouds drifting past at different depths: parallax makes the camera feel inside the nebula.
            var go = new GameObject("NebulaDust");
            go.transform.SetParent(systemRoot);
            go.transform.position = new Vector3(0f, 0f, 55f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true;
            m.prewarm = true;
            m.playOnAwake = false;
            m.startLifetime = 90f;
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(10f, 28f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.2f, 0.07f), new Color(0.15f, 0.7f, 1f, 0.05f));
            m.maxParticles = 500;
            m.simulationSpace = ParticleSystemSimulationSpace.Local;   // moves with the region when it slides in
            var e = ps.emission; e.rateOverTime = 1.8f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(170f, 55f, 100f);
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.x = -0.6f; v.y = 0f; v.z = 0f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Additive(new Color(1.4f, 1.4f, 1.4f, 1f), 0.8f);
            ps.Play();

            var giant = MakeBody("EmberGiant", null, new Vector3(16f, 9f, 108f), 8f,
                Mats.Planet(new Color(0.5f, 0.12f, 0.25f), new Color(1.0f, 0.55f, 0.3f), 6f, 0.8f, 2.2f, new Color(1f, 0.4f, 0.5f) * 1.8f, 1.5f, 61f),
                0f, 0f, 0f, 0f, 0f, 3f);
            MakeBody("EmberMoon", giant, default, 1.6f,
                Mats.Planet(new Color(0.3f, 0.2f, 0.2f), new Color(0.8f, 0.6f, 0.5f), 1f, 0.05f, 5f, new Color(1f, 0.6f, 0.5f), 0.5f, 70f),
                17f, 10f, 15f, 40f, 0.05f, 8f);
        }

        void BuildTwinSuns()
        {
            var orange = MakeBody("OrangeSun", null, new Vector3(-48f, 16f, 150f), 10f, Mats.Unlit(new Color(7f, 2.6f, 0.5f)), 0f, 0f, 0f, 0f, 0f, 0f);
            var blue = MakeBody("BlueSun", null, new Vector3(58f, -3f, 175f), 5.5f, Mats.Unlit(new Color(2.5f, 4.5f, 9f)), 0f, 0f, 0f, 0f, 0f, 0f);
            Halo(orange.root, 60f, new Color(2.4f, 1.0f, 0.3f, 0.75f));
            Halo(orange.root, 140f, new Color(1.3f, 0.45f, 0.12f, 0.35f));
            Halo(blue.root, 34f, new Color(0.7f, 1.3f, 3f, 0.7f));
            Halo(blue.root, 80f, new Color(0.3f, 0.5f, 1.4f, 0.3f));

            MakeBody("Cinder", orange, default, 2.8f,
                Mats.Planet(new Color(0.15f, 0.05f, 0.04f), new Color(1f, 0.4f, 0.1f), 3f, 0.4f, 3.5f, new Color(1f, 0.4f, 0.1f) * 2f, 1.6f, 80f),
                40f, 5f, 6f, 30f, 0.05f, 8f);
            var tide = MakeBody("Tide", orange, default, 3.8f,
                Mats.Planet(new Color(0.05f, 0.2f, 0.45f), new Color(0.6f, 0.85f, 0.75f), 3f, 0.5f, 2.8f, new Color(0.4f, 0.8f, 1f) * 1.8f, 1.8f, 90f),
                64f, 3f, -9f, 200f, 0.1f, 6f);
            MakeBody("TideMoon", tide, default, 1f,
                Mats.Planet(new Color(0.35f, 0.35f, 0.38f), new Color(0.8f, 0.8f, 0.82f), 1f, 0.05f, 6f, new Color(0.6f, 0.8f, 1f), 0.4f, 95f),
                8f, 30f, 20f, 0f, 0f, 12f);

            // warm light travelling from the orange sun toward the scene
            sunLight.transform.rotation = Quaternion.LookRotation((-orange.root.position).normalized);
        }

        void BuildAsteroidField()
        {
            var mats = new[]
            {
                Mats.Planet(new Color(0.12f, 0.1f, 0.09f), new Color(0.5f, 0.42f, 0.35f), 1f, 0f, 3.5f, new Color(0.5f, 0.4f, 0.3f), 0.3f, 100f),
                Mats.Planet(new Color(0.1f, 0.1f, 0.13f), new Color(0.45f, 0.47f, 0.55f), 1f, 0f, 4.5f, new Color(0.4f, 0.5f, 0.8f), 0.3f, 110f),
                Mats.Planet(new Color(0.2f, 0.09f, 0.06f), new Color(0.7f, 0.4f, 0.25f), 1f, 0f, 3f, new Color(0.9f, 0.5f, 0.3f), 0.3f, 120f),
            };
            var rng = new System.Random(99);
            float R(float lo, float hi) { return lo + (float)rng.NextDouble() * (hi - lo); }
            for (int i = 0; i < 230; i++)
            {
                Vector3 p = Vector3.zero;
                for (int tries = 0; tries < 8; tries++)
                {
                    p = new Vector3(R(-80f, 80f), R(-26f, 26f), R(14f, 150f));
                    // keep a clear lane around the ship and the camera
                    if (p.z > 45f || new Vector2(p.x - 3f, p.y).magnitude > 9f) break;
                }
                float size = rng.NextDouble() < 0.08 ? R(5f, 9f) : R(0.6f, 3.4f);
                // unrotated pivot (so the transition can stretch it along world Z) with the tumbling rock inside
                var pivot = new GameObject("RockPivot" + i).transform;
                pivot.SetParent(systemRoot);
                pivot.position = p;
                var rock = Mats.Prim(PrimitiveType.Sphere, pivot, "Rock" + i, Vector3.zero,
                    new Vector3(size * R(0.6f, 1.4f), size * R(0.6f, 1.4f), size * R(0.6f, 1.4f)), mats[i % mats.Length]);
                rock.transform.localRotation = Random.rotation;
                rocks.Add(new Rock { t = pivot, visual = rock.transform, spin = new Vector3(R(-18f, 18f), R(-18f, 18f), R(-18f, 18f)) });
                stretch.Add(pivot);
            }
            var giant = MakeBody("DistantGiant", null, new Vector3(-26f, 14f, 175f), 17f,
                Mats.Planet(new Color(0.3f, 0.12f, 0.08f), new Color(0.95f, 0.7f, 0.45f), 8f, 0.85f, 2f, new Color(1f, 0.6f, 0.3f) * 1.6f, 1.5f, 130f),
                0f, 0f, 0f, 0f, 0f, 2f);
            AddRing(giant, 1.5f, 2.4f, 20f, new Color(0.9f, 0.75f, 0.6f, 0.7f));
        }

        // =================== per frame ===================

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            UpdateTransition(dt);
            UpdateWarp(dt);
            UpdateShip(dt, t);
            UpdateBodies(dt);
            UpdateCrossings(dt);
            UpdateCamera(t);
            UpdateSweep(dt);
            UpdatePost();
            Shader.SetGlobalFloat("_DjWarp", warp);
        }

        void UpdateWarp(float dt)
        {
            prevRaw = warpRaw;
            float dur = warpTarget > 0f ? 2.4f : 3.0f;
            warpRaw = Mathf.MoveTowards(warpRaw, warpTarget, dt / dur);
            float stateWarp = warpRaw * warpRaw * (3f - 2f * warpRaw);
            float pulseWarp = 0f;
            if (pulseT >= 0f)
            {
                float pk = pulseT < 0.7f ? pulseT / 0.7f : 1f - (pulseT - 0.7f) / 3.1f;
                pk = Mathf.Clamp01(pk);
                pulseWarp = pk * pk * (3f - 2f * pk) * 0.9f;
            }
            warp = Mathf.Max(stateWarp, Mathf.Max(actions.Build * 0.6f, pulseWarp));   // Build / Drop pre-stretch the stars without a full jump

            // The "jump": a white flash as we cross the threshold in either direction.
            if (warpTarget > 0f && prevRaw < 0.5f && warpRaw >= 0.5f) { flash = 0.8f; LeaveSystem(); }
            // Leaving warp: the next region starts gliding in while the stars are still streaking (no hard cut).
            if (warpTarget <= 0f && pendingArrive && warpRaw <= 0.6f && leaveT < 0f)
            {
                pendingArrive = false;
                ArriveNext();
            }
            flash = Mathf.Max(0f, flash - dt * 1.6f);
            surge = Mathf.Max(0f, surge - dt * 1.4f);
            if (dropRelease >= 0f)
            {
                dropRelease += dt;
                actions.SetBuild(Mathf.Lerp(dropFrom, 0f, dropRelease / 2.5f));
                if (dropRelease >= 2.5f) dropRelease = -1f;
            }
            skyMat.SetFloat("_Intensity", dests[destIndex].intensity * (1f + reveal * 1.1f));

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
            shipHome = Vector3.SmoothDamp(shipHome, shipTarget, ref shipVel, 1.1f);
            float bank = Mathf.Clamp(-shipVel.x * 1.1f, -28f, 28f);   // lean into the move
            ship.localPosition = shipHome + new Vector3(Mathf.Sin(t * 47f) * jitter, Mathf.Sin(t * 0.8f) * 0.08f + Mathf.Sin(t * 53f) * jitter, 0);
            ship.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.5f) * 0.8f, 0, Mathf.Sin(t * 0.7f) * 1.5f + bank);

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
            lightPulse = Mathf.Max(lightPulse, surge);
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
            blastMat.SetColor("_Color", Color.Lerp(IdleBlast, WarpBlast, warp) * (1f + lightPulse * 0.5f) * (1f - 0.6f * rollBlend));
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
            foreach (var r in rocks)
            {
                r.t.localPosition += new Vector3(0f, 0f, -RockDrift * dt);
                if (r.t.localPosition.z < -14f) r.t.localPosition += new Vector3(0f, 0f, 165f);
                r.visual.Rotate(r.spin * dt, Space.Self);
            }
            foreach (var bb in billboards) bb.rotation = Quaternion.LookRotation(bb.position - cam.transform.position);
        }

        void UpdateCamera(float t)
        {
            var pose = poses[poseIdx];
            camPosS = Vector3.SmoothDamp(camPosS, pose.pos, ref camPosVel, 1.2f);
            camLookS = Vector3.SmoothDamp(camLookS, pose.look, ref camLookVel, 1.2f);
            camFovS = Mathf.SmoothDamp(camFovS, pose.fov, ref camFovVel, 1.2f);

            var shake = new Vector3(Mathf.PerlinNoise(t * 9f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 9f) - 0.5f, 0f) * (0.12f * warp);
            cam.transform.position = camPosS + shake + new Vector3(0, 0, -warp * 0.8f);
            float sway = Mathf.Sin(t * 0.7f) * 3.5f * warp;   // gentle wobble while in warp (no barrel roll)
            cam.transform.rotation = Quaternion.LookRotation(camLookS - cam.transform.position) * Quaternion.AngleAxis(sway, Vector3.forward);
            cam.fieldOfView = camFovS + warp * 14f + flash * 3f + reveal * reveal * 6f;
        }

        void UpdatePost()
        {
            fx.Bloom.intensity.value = 1.1f + warp * 2.2f + flash * 3f + reveal * 1.0f;
            fx.Adjust.postExposure.value = flash * 1.2f + warp * 0.25f + reveal * 0.3f;
            fx.Lens.intensity.value = warp * 0.7f;
            fx.Chroma.intensity.value = warp * 0.9f + flash * 0.4f + reveal * 0.3f;
            fx.Vig.intensity.value = 0.2f + warp * 0.3f + vigExtra;
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
