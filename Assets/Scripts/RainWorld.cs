using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// First test world: dark rain, one blue virtual uplight.
    ///   Beat        -> light swells, droplets catch it (transient, every beat)
    ///   Accent      -> one-shot splash
    ///   Build       -> rain thickens and speeds up (continuous)
    ///   Drop        -> a sheet of water sweeps across all panels, then Build releases
    ///   Progression -> persistent runoff (puddles + a stream toward camera) that stays until Reset
    /// Everything is built in code so the project needs no authored scene or assets.
    /// </summary>
    public class RainWorld : MonoBehaviour
    {
        public VisualActions Actions;

        const float RainTop = 13f;
        const float SheetDuration = 1.4f;
        static readonly Color LightBlue = new Color(0.22f, 0.48f, 1f);

        Light uplight;
        ParticleSystem rain, splash, sheet, runoff;
        Material rainMat, puddleMat;
        readonly List<GameObject> puddles = new List<GameObject>();

        float pulse;          // beat swell, decays
        float flash;          // accent / drop flash, decays
        float sheetT = -1f;   // drop sweep progress in seconds, -1 = idle
        float releaseT = -1f; // build release after drop, -1 = idle
        float releaseFrom;

        public void Init(VisualActions actions)
        {
            Actions = actions;
            BuildStage();
            BuildRain();
            BuildSplash();
            BuildSheet();
            BuildRunoff();
            Actions.Beat += _ => pulse = 1f;
            Actions.Accent += DoAccent;
            Actions.Drop += DoDrop;
            Actions.Progressed += DoProgress;
            Actions.ResetRequested += DoReset;
        }

        // ---------- construction ----------

        static Shader Unlit()
        {
            var s = Shader.Find("Sprites/Default");
            return s != null ? s : Shader.Find("Unlit/Color");
        }

        void BuildStage()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.01f, 0.012f, 0.02f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform);
            ground.transform.localScale = new Vector3(8, 1, 8);
            ground.GetComponent<Renderer>().material = Mats.Lit(new Color(0.04f, 0.05f, 0.07f), 0.9f, 0.2f);
            Destroy(ground.GetComponent<Collider>());

            // A few dark props so the uplight has something to rake across.
            var rng = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                bool cyl = i % 3 == 0;
                var p = GameObject.CreatePrimitive(cyl ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                p.name = "Prop" + i;
                p.transform.SetParent(transform);
                float h = 1.5f + (float)rng.NextDouble() * 4f;
                p.transform.localScale = new Vector3(0.8f + (float)rng.NextDouble() * 1.2f, cyl ? h * 0.5f : h, 0.8f + (float)rng.NextDouble() * 1.2f);
                p.transform.position = new Vector3(-16f + i * 4f + (float)rng.NextDouble() * 2f, h * 0.5f, 4f + (float)rng.NextDouble() * 9f);
                p.GetComponent<Renderer>().material = Mats.Lit(new Color(0.06f, 0.07f, 0.09f), 0.6f, 0f);
                Destroy(p.GetComponent<Collider>());
            }

            var lg = new GameObject("BlueUplight");
            lg.transform.SetParent(transform);
            lg.transform.position = new Vector3(0, 0.6f, 4f);
            uplight = lg.AddComponent<Light>();
            uplight.type = LightType.Point;
            uplight.color = LightBlue;
            uplight.range = 24f;
            uplight.intensity = 1.5f;
            uplight.shadows = LightShadows.None;
        }

        ParticleSystem MakePS(string name, Material mat, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.playOnAwake = false;
            m.loop = true;
            m.maxParticles = maxParticles;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return ps;
        }

        void BuildRain()
        {
            rainMat = new Material(Unlit()) { color = new Color(0.35f, 0.5f, 0.85f, 0.55f) };
            rain = MakePS("Rain", rainMat, 40000);
            rain.transform.position = new Vector3(0, RainTop, 5f);
            var m = rain.main;
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            m.startColor = new Color(1, 1, 1, 0.8f);
            var sh = rain.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(46f, 0.5f, 22f);
            var r = rain.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.03f;
            r.lengthScale = 1f;
            r.cameraVelocityScale = 0f;
            ApplyBuild(0f);
            rain.Play();
        }

        void BuildSplash()
        {
            var mat = new Material(Unlit()) { color = new Color(0.6f, 0.8f, 1f, 0.9f) };
            splash = MakePS("Splash", mat, 4000);
            splash.transform.rotation = Quaternion.Euler(-90, 0, 0); // cone points up
            var m = splash.main;
            m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            m.gravityModifier = 1.6f;
            var e = splash.emission; e.enabled = false;
            var sh = splash.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 38f; sh.radius = 0.4f;
            var col = splash.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(LightBlue, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
        }

        void BuildSheet()
        {
            var mat = new Material(Unlit()) { color = new Color(0.55f, 0.75f, 1f, 0.7f) };
            sheet = MakePS("DropSheet", mat, 30000);
            sheet.transform.position = new Vector3(-30f, RainTop, 3f);
            var m = sheet.main;
            m.startSpeed = 0f;
            m.startLifetime = 0.8f;
            m.gravityModifier = 4f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            var e = sheet.emission; e.enabled = false;
            var sh = sheet.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(4f, 0.3f, 24f);
            var r = sheet.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f; r.lengthScale = 1f; r.cameraVelocityScale = 0f;
        }

        void BuildRunoff()
        {
            var mat = new Material(Unlit()) { color = new Color(0.3f, 0.55f, 1f, 0.35f) };
            puddleMat = new Material(Unlit()) { color = new Color(0.25f, 0.5f, 1f, 0.28f) };
            runoff = MakePS("Runoff", mat, 6000);
            runoff.transform.position = new Vector3(0, 0.06f, 12f);
            var m = runoff.main;
            m.startLifetime = 5f;
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            var sh = runoff.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(40f, 0.05f, 2f);
            var v = runoff.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = 0f; v.y = 0f;
            v.z = new ParticleSystem.MinMaxCurve(-4.5f, -2.5f);
            runoff.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var e = runoff.emission; e.rateOverTime = 0f;
            runoff.Play();
        }

        // ---------- actions ----------

        void DoAccent()
        {
            splash.transform.position = new Vector3(Random.Range(-14f, 14f), 0.05f, Random.Range(-1f, 9f));
            splash.Emit(140);
            flash = Mathf.Max(flash, 0.6f);
        }

        void DoDrop()
        {
            sheetT = 0f;
            releaseT = -1f;
            flash = 1.2f;
        }

        void DoProgress(int level)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Puddle" + level;
            q.transform.SetParent(transform);
            q.transform.rotation = Quaternion.Euler(90, 0, 0);
            float s = Random.Range(3f, 8f);
            q.transform.localScale = new Vector3(s * Random.Range(1f, 2.2f), s, 1);
            q.transform.position = new Vector3(Random.Range(-16f, 16f), 0.03f + level * 0.0005f, Random.Range(0f, 12f));
            q.GetComponent<Renderer>().sharedMaterial = puddleMat;
            Destroy(q.GetComponent<Collider>());
            puddles.Add(q);
            var e = runoff.emission;
            e.rateOverTime = Mathf.Min(level * 14f, 160f);
        }

        void DoReset()
        {
            pulse = flash = 0f;
            sheetT = releaseT = -1f;
            foreach (var p in puddles) if (p) Destroy(p);
            puddles.Clear();
            var e = runoff.emission; e.rateOverTime = 0f;
            foreach (var ps in new[] { rain, splash, sheet, runoff }) ps.Clear();
            ApplyBuild(0f);
        }

        // ---------- per frame ----------

        void ApplyBuild(float b)
        {
            var e = rain.emission;
            e.rateOverTime = Mathf.Lerp(900f, 9000f, b);
            var m = rain.main;
            float g = Mathf.Lerp(2.6f, 4.2f, b);
            m.gravityModifier = g;
            // lifetime tuned so drops land about when they reach the ground
            m.startLifetime = Mathf.Sqrt(2f * RainTop / (9.81f * g));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            pulse = Mathf.Max(0f, pulse - dt * 6f);
            flash = Mathf.Max(0f, flash - dt * 2.5f);

            float build = Actions.Build;
            ApplyBuild(build);

            // Light swells on the beat; droplets are unlit, so tint them with the same value.
            float swell = pulse * pulse;
            uplight.intensity = 1.2f + build * 1.5f + swell * 7f + flash * 6f;
            float lit = Mathf.Clamp01(swell + flash * 0.6f);
            rainMat.color = Color.Lerp(new Color(0.25f, 0.35f, 0.6f, 0.45f), new Color(0.75f, 0.9f, 1f, 0.95f), lit);

            if (sheetT >= 0f)
            {
                sheetT += dt;
                float k = Mathf.Clamp01(sheetT / SheetDuration);
                sheet.transform.position = new Vector3(Mathf.Lerp(-28f, 28f, k), RainTop, 3f);
                sheet.Emit(Mathf.RoundToInt(28000f * dt));
                if (sheetT >= SheetDuration)
                {
                    sheetT = -1f;
                    releaseT = 0f;
                    releaseFrom = build;
                }
            }
            if (releaseT >= 0f)
            {
                releaseT += dt;
                Actions.SetBuild(Mathf.Lerp(releaseFrom, 0f, releaseT / 2.5f));
                if (releaseT >= 2.5f) releaseT = -1f;
            }
        }
    }
}
