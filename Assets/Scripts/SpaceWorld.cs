using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// Space world, step 1: only the beat is wired.
    /// A ship with three engine rings seen from behind; on every beat the rings flare in a ripple
    /// from the hull outward, and the engine light swells. Stars and planets are static dressing.
    /// Accent / Build / Drop / Progression are intentionally not connected yet.
    /// </summary>
    public class SpaceWorld : MonoBehaviour
    {
        const int RingCount = 3;
        const float RippleDelay = 0.07f;   // seconds between rings
        const float Decay = 4.5f;          // higher = snappier flare

        VisualActions actions;
        Transform ship;
        Light engineLight;
        Transform[] rings;
        Material[] ringMats;
        Vector3[] ringBaseScale;
        float[] ringPulse;       // 0..1 per ring, decays
        float[] ringDelay;       // seconds until this ring fires (-1 = idle)

        static readonly Color EngineColor = new Color(0.25f, 0.65f, 1f);

        public void Init(VisualActions a)
        {
            actions = a;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.035f, 0.06f);

            BuildStars();
            BuildPlanets();
            BuildShip();
            actions.Beat += _ => Flare();
        }

        // ---------- construction ----------

        static Shader UnlitShader()
        {
            var s = Shader.Find("Sprites/Default");
            return s != null ? s : Shader.Find("Unlit/Color");
        }

        void BuildStars()
        {
            var go = new GameObject("Stars");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.playOnAwake = false;
            m.loop = false;
            m.duration = 1f;
            m.startLifetime = 1e6f;
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.8f, 1f), Color.white);
            m.maxParticles = 4000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var e = ps.emission; e.enabled = false;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(190f, 60f, 110f);
            go.transform.position = new Vector3(0, 0, 55f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(UnlitShader());
            ps.Emit(2500);
            ps.Pause();
        }

        void BuildPlanets()
        {
            MakePlanet("PlanetA", new Vector3(-42f, 6f, 85f), 22f, new Color(0.22f, 0.18f, 0.35f));
            MakePlanet("PlanetB", new Vector3(55f, -8f, 100f), 12f, new Color(0.35f, 0.2f, 0.15f));
            var sun = new GameObject("KeyLight").AddComponent<Light>();
            sun.transform.SetParent(transform);
            sun.type = LightType.Directional;
            sun.color = new Color(0.55f, 0.6f, 0.8f);
            sun.intensity = 0.8f;
            sun.transform.rotation = Quaternion.Euler(35f, -30f, 0);
        }

        void MakePlanet(string name, Vector3 pos, float size, Color c)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            p.name = name;
            p.transform.SetParent(transform);
            p.transform.position = pos;
            p.transform.localScale = Vector3.one * size;
            var mat = new Material(Shader.Find("Standard")) { color = c };
            mat.SetFloat("_Glossiness", 0.1f);
            p.GetComponent<Renderer>().material = mat;
            Destroy(p.GetComponent<Collider>());
        }

        void BuildShip()
        {
            ship = new GameObject("Ship").transform;
            ship.SetParent(transform);
            ship.position = Vector3.zero;

            var hull = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.58f, 0.65f) };
            hull.SetFloat("_Metallic", 0.6f);
            hull.SetFloat("_Glossiness", 0.55f);

            Part(PrimitiveType.Capsule, "Body", new Vector3(0, 0, 0), Quaternion.Euler(90, 0, 0), new Vector3(1.1f, 1.6f, 1.1f), hull);
            Part(PrimitiveType.Cube, "WingL", new Vector3(-1.5f, -0.1f, -0.4f), Quaternion.Euler(0, 0, 8), new Vector3(2.2f, 0.08f, 1.5f), hull);
            Part(PrimitiveType.Cube, "WingR", new Vector3(1.5f, -0.1f, -0.4f), Quaternion.Euler(0, 0, -8), new Vector3(2.2f, 0.08f, 1.5f), hull);
            Part(PrimitiveType.Cube, "Fin", new Vector3(0, 0.55f, -0.7f), Quaternion.identity, new Vector3(0.08f, 0.7f, 1.0f), hull);

            rings = new Transform[RingCount];
            ringMats = new Material[RingCount];
            ringBaseScale = new Vector3[RingCount];
            ringPulse = new float[RingCount];
            ringDelay = new float[RingCount];
            for (int i = 0; i < RingCount; i++)
            {
                var mat = new Material(Shader.Find("Standard")) { color = new Color(0.05f, 0.1f, 0.18f) };
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", EngineColor * 0.4f);
                float d = 1.15f - i * 0.18f;
                var scale = new Vector3(d, 0.03f, d);
                // Rings sit behind the hull, stacked along -Z toward the camera.
                var r = Part(PrimitiveType.Cylinder, "EngineRing" + i, new Vector3(0, 0, -1.3f - i * 0.55f), Quaternion.Euler(90, 0, 0), scale, mat);
                rings[i] = r;
                ringMats[i] = mat;
                ringBaseScale[i] = scale;
                ringDelay[i] = -1f;
            }

            var lg = new GameObject("EngineLight");
            lg.transform.SetParent(ship);
            lg.transform.localPosition = new Vector3(0, 0, -3.2f);
            engineLight = lg.AddComponent<Light>();
            engineLight.type = LightType.Point;
            engineLight.color = EngineColor;
            engineLight.range = 14f;
            engineLight.intensity = 1.2f;
        }

        Transform Part(PrimitiveType type, string name, Vector3 localPos, Quaternion rot, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(ship);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = mat;
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        // ---------- beat ----------

        void Flare()
        {
            for (int i = 0; i < RingCount; i++) ringDelay[i] = i * RippleDelay;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            // Gentle hover so the ship never looks frozen between beats.
            ship.localPosition = new Vector3(0, Mathf.Sin(t * 0.8f) * 0.08f, 0);
            ship.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.5f) * 0.8f, 0, Mathf.Sin(t * 0.7f) * 1.5f);

            float lightPulse = 0f;
            for (int i = 0; i < RingCount; i++)
            {
                if (ringDelay[i] >= 0f)
                {
                    ringDelay[i] -= dt;
                    if (ringDelay[i] < 0f) { ringPulse[i] = 1f; ringDelay[i] = -1f; }
                }
                ringPulse[i] = Mathf.Max(0f, ringPulse[i] - dt * Decay);
                float p = ringPulse[i] * ringPulse[i];

                float grow = 1f + p * 0.55f;
                rings[i].localScale = new Vector3(ringBaseScale[i].x * grow, ringBaseScale[i].y * (1f + p * 2f), ringBaseScale[i].z * grow);
                ringMats[i].SetColor("_EmissionColor", EngineColor * (0.4f + p * 5f));
                lightPulse = Mathf.Max(lightPulse, p);
            }
            engineLight.intensity = 1.2f + lightPulse * 5f;
        }
    }
}
