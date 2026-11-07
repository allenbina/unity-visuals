using System.Collections.Generic;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// The Cube world: a three-screen take on Etienne de Crecy's Beats 'n' Cubes (Transmusicales 2007).
    /// A 3x3x3 frame of boxes whose edges glow like projected light on scaffolding, around an open center column.
    /// Thirteen phases come from the scenes of the reference video (docs/beats-n-cubes-scenes.md); Progress (P) advances.
    /// Front-on phases show three cubes, one per panel, the outer two echoing the center a beat late; the "looking
    /// down into it" phases wrap one cube around the camera so the side panels show its walls.
    /// All animation is a function of the beat clock, so it stays in time at any BPM.
    /// </summary>
    public class CubeWorld : MonoBehaviour
    {
        const float S = 10f;                 // edge length of the whole cube
        const int MaxN = 7;
        const float WideAspect = 3840f / 720f;

        static readonly string[] PhaseNames =
        {
            "1 Dim red", "2 Red flood / white line", "3 Trace", "4 Reveal", "5 Look down", "6 Tumble", "7 Cells play",
            "8 Slide", "9 Extrude", "10 Dissolve", "11 Build", "12 Breathe", "13 Subdivide",
        };

        static readonly Color Cool = new Color(0.55f, 0.88f, 1f);
        static readonly Color Red = new Color(1f, 0.08f, 0.04f);

        VisualActions actions;
        Camera cam;
        PostFx fx;
        Mesh cubeMesh;
        Material edgeMat;

        class Cell
        {
            public Transform t; public Renderer r; public MaterialPropertyBlock mpb;
            public int id, order;
            public Vector3Int grid;
            public Vector3 basePos;
            public float edge, fill, scale = 1f, width = 0.07f;
            public Vector3 off;
        }
        class Struct
        {
            public Transform root; public List<Cell> pool = new List<Cell>(); public int active;
            public float delay; public float vis = 1f; public float x;
        }
        readonly Struct[] structs = new Struct[3];

        int n = 3;
        int phase;
        float phaseStartBeat;
        bool snapCam = true;
        float accentEnv, dropEnv, accentX;
        Transform glow;
        Material glowMat;

        struct Pose { public Vector3 pos, look; public float fov, roll, spacing; public bool spread; }   // spacing 0 = one cube per panel
        Vector3 camPosS, camPosVel, camLookS, camLookVel;
        float camFovS, camFovVel, rollS, rollVel;

        struct Vals { public float edge, fill, scale, width, tint; public Vector3 off; }

        public void Init(VisualActions a, Camera camera, PostFx postFx)
        {
            actions = a;
            cam = camera;
            fx = postFx;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            edgeMat = new Material(Shader.Find("DjVisuals/BoxEdge"));

            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Cube" + i).transform;
                root.SetParent(transform);
                structs[i] = new Struct { root = root, delay = i == 0 ? 0f : 1f };   // 0 = center, 1/2 = echoes
            }
            // structure order in the array: 0 center, 1 left, 2 right
            glowMat = Mats.Additive(new Color(9f, 9f, 10f, 1f), 1.2f);
            glowMat.SetFloat("_VC", 0f);
            glow = Mats.Prim(PrimitiveType.Quad, transform, "Backlight", Vector3.zero, Vector3.one * 11f, glowMat).transform;

            Layout(3);
            SetPhase(0, true);

            actions.Beat += OnBeat;
            actions.Accent += OnAccent;
            actions.Drop += OnDrop;
            actions.Progressed += OnProgressed;
            actions.ResetRequested += OnReset;
        }

        void OnDestroy()
        {
            if (actions == null) return;
            actions.Beat -= OnBeat;
            actions.Accent -= OnAccent;
            actions.Drop -= OnDrop;
            actions.Progressed -= OnProgressed;
            actions.ResetRequested -= OnReset;
        }

        // =================== layout ===================

        /// <summary>Divide each cube into nxnxn boxes, leaving the center column open for the performer.</summary>
        void Layout(int count)
        {
            n = count;
            float cs = S / n;
            float mid = (n - 1) * 0.5f;
            var coords = new List<Vector3Int>();
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    for (int k = 0; k < n; k++)
                    {
                        if (n % 2 == 1 && i == (n - 1) / 2 && j == (n - 1) / 2) continue;   // open center column
                        coords.Add(new Vector3Int(i, j, k));
                    }
            for (int si = 0; si < 3; si++)
            {
                var st = structs[si];
                while (st.pool.Count < coords.Count)
                {
                    var go = new GameObject("Box");
                    go.transform.SetParent(st.root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = edgeMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    st.pool.Add(new Cell { t = go.transform, r = r, mpb = new MaterialPropertyBlock(), id = st.pool.Count });
                }
                st.active = coords.Count;
                for (int c = 0; c < st.pool.Count; c++)
                {
                    var cell = st.pool[c];
                    bool on = c < coords.Count;
                    cell.t.gameObject.SetActive(on);
                    if (!on) continue;
                    var g = coords[c];
                    cell.id = c;
                    cell.grid = g;
                    cell.order = g.y * 100 + g.x * 10 + g.z;   // build from the bottom, left to right
                    cell.basePos = new Vector3((g.x - mid) * cs, (g.y - mid) * cs, (g.z - mid) * cs);
                    cell.t.localScale = Vector3.one * cs;
                }
                // turn the sort keys into consecutive ranks
                var ranks = new List<int>();
                for (int c = 0; c < st.active; c++) ranks.Add(st.pool[c].order);
                ranks.Sort();
                for (int c = 0; c < st.active; c++) st.pool[c].order = ranks.IndexOf(st.pool[c].order);
            }
        }

        // =================== phases ===================

        void SetPhase(int p, bool instant)
        {
            phase = ((p % PhaseNames.Length) + PhaseNames.Length) % PhaseNames.Length;
            phaseStartBeat = actions.Clock.BeatPos;
            snapCam = true;
            if (phase != 12 && n != 3) Layout(3);
            actions.Status = "Cube: " + PhaseNames[phase];
        }

        void OnProgressed(int level) { SetPhase(level, false); }
        void OnReset() { SetPhase(0, true); accentEnv = 0f; dropEnv = 0f; }
        void OnBeat(int idx) { }
        void OnAccent() { accentEnv = 1f; accentX = -70f; }
        void OnDrop() { dropEnv = 1f; }

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        Pose PoseFor(float pb)
        {
            // front-on, one cube per panel
            var front = new Pose { pos = new Vector3(0f, 0f, -25f), look = Vector3.zero, fov = 32f, roll = 0f, spread = true };
            // inside the front opening, looking down the open center column: the side walls fill the side panels
            var tunnel = new Pose { pos = new Vector3(0f, 0f, -4.4f), look = new Vector3(0f, 0f, 6f), fov = 52f, roll = 0f, spread = false };
            switch (phase)
            {
                case 1: front.roll = 12f; return front;
                case 2:   // close-up: the three cubes packed together so the structure spans all three panels
                    return new Pose { pos = new Vector3(6f, 2f, -16f), look = new Vector3(2f, 0f, 0f), fov = 38f, roll = -14f, spread = true, spacing = 14f };
                case 3: front.pos = new Vector3(0f, 0f, Mathf.Lerp(-19f, -25f, Mathf.Clamp01(pb / 12f))); return front;
                case 4:   // high angle looking down over the packed cubes
                    return new Pose { pos = new Vector3(0f, 11f, -11f), look = new Vector3(0f, -1f, 1f), fov = 46f, roll = 0f, spread = true, spacing = 14f };
                case 5:   // tumble: orbiting with a rolling camera
                {
                    float a = pb * 0.25f;
                    return new Pose { pos = new Vector3(Mathf.Sin(a) * 24f, 5f + Mathf.Sin(a * 0.5f) * 3f, -Mathf.Cos(a) * 24f - 6f), look = Vector3.zero,
                                      fov = 38f, roll = Mathf.Sin(pb * 0.35f) * 25f, spread = true, spacing = 14f };
                }
                case 7: front.pos = new Vector3(0f, 0f, -22f); return front;
                case 9: return tunnel;
                case 12: return pb >= 16f ? tunnel : front;
                default: return front;
            }
        }

        Vals Eval(Cell c, int si, float bp, float pb, int count)
        {
            var v = new Vals { edge = 1.1f, fill = 0f, scale = 1f, width = 0.13f, tint = 1f, off = Vector3.zero };
            float fr = bp - Mathf.Floor(bp);
            float env = Mathf.Exp(-fr * 6f);                 // 1 on the beat, fading
            int bi = Mathf.FloorToInt(bp);
            float rnd = Hash(bi, c.id);
            float mid = (n - 1) * 0.5f;
            float cs = S / n;
            pb = Mathf.Max(pb, 0f);

            switch (phase)
            {
                case 0:   // dim red panels fading toward near-black
                {
                    float fade = Mathf.Clamp01(pb / 24f);
                    v.tint = fade;
                    v.edge = Mathf.Lerp(0.8f, 0.1f, fade) * (0.7f + 0.5f * env);
                    v.fill = (1f - fade) * 0.7f * (c.grid.z == 0 ? 1f : 0.25f) * (0.6f + 0.6f * env);
                    break;
                }
                case 1:   // red flood, then a single white line
                {
                    if (pb < 16f) { v.tint = 0f; v.fill = 0.8f * (0.55f + 0.45f * env); v.edge = 1.4f; }
                    else
                    {
                        v.edge = 0.03f;
                        if (c.grid.y == Mathf.FloorToInt(bp) % n) { v.edge = 2.4f * (0.5f + 0.5f * env); v.width = 0.08f; }
                    }
                    break;
                }
                case 2:   // light tracing the tubes
                {
                    v.edge = 0.3f;
                    float scan = Mathf.Repeat(pb * 0.45f, 1f) * count;
                    float d = Mathf.Abs(c.order - scan);
                    v.edge += 2.2f * Mathf.Exp(-d * d / (count * 0.012f + 2f));
                    v.width = 0.17f;
                    break;
                }
                case 3:   // the reveal: a white flash from the open center, then the clean outline
                    v.edge = 1.15f * (0.55f + 0.45f * env);
                    break;
                case 4:   // looking down into it
                    v.edge = 0.95f * Mathf.Clamp01(pb / 6f) * (0.7f + 0.4f * env);
                    break;
                case 5:   // tumbling camera
                    v.edge = 0.95f * (0.7f + 0.4f * env);
                    break;
                case 6:   // cells play: single cells flood on the beat, some blocks shift up
                {
                    v.edge = 0.7f * (0.8f + 0.3f * env);
                    if (rnd < 0.16f) v.fill = env * 0.95f;
                    if (Hash(bi / 2, c.id + 99) < 0.2f) v.off.y = cs * 0.5f;
                    break;
                }
                case 7:   // rows slide like shelves
                {
                    v.edge = 0.85f;
                    v.off.x = Mathf.Sin(bp * 0.39f + c.grid.y * 1.7f) * S * 0.22f;
                    break;
                }
                case 8:   // boxes pop out of the grid on the beat
                {
                    v.edge = 0.9f;
                    float pop = env * S * 0.13f * (0.6f + Hash(c.id, 5) * 0.8f);
                    if (c.grid.x == 0) v.off.x = -pop;
                    if (c.grid.x == n - 1) v.off.x = pop;
                    if (c.grid.y == n - 1) v.off.y = pop * 0.6f;
                    break;
                }
                case 9:   // dissolve to sparse lines, then black
                {
                    float k = 1f - Mathf.Clamp01(pb / 24f);
                    v.edge = 0.9f * k * k;
                    v.width = Mathf.Lerp(0.03f, 0.13f, k);
                    if (Hash(c.id, 7) > k + 0.15f) v.edge = 0f;
                    break;
                }
                case 10:  // built up cell by cell from the bottom
                {
                    int visible = Mathf.FloorToInt(pb * 2f) + 1;
                    if (c.order >= visible) { v.edge = 0f; v.scale = 0.001f; }
                    else v.edge = (c.order == visible - 1) ? 2.2f : 1f;
                    break;
                }
                case 11:  // the cube breathes; layers drift outward
                {
                    float br = Mathf.Sin(bp * Mathf.PI * 0.5f);
                    v.edge = 0.9f * (0.8f + 0.3f * env);
                    var dir = new Vector3(c.grid.x - mid, c.grid.y - mid, c.grid.z - mid);
                    v.off = dir.normalized * S * 0.04f * br;
                    break;
                }
                case 12:  // finer and finer subdivision
                    v.edge = 0.9f * (0.8f + 0.3f * env);
                    v.width = 0.13f * (3f / n);
                    break;
            }
            return v;
        }

        // =================== per frame ===================

        void Update()
        {
            float dt = Time.deltaTime;
            float bpNow = actions.Clock.BeatPos;
            float pbNow = Mathf.Max(0f, bpNow - phaseStartBeat);

            accentEnv = Mathf.Max(0f, accentEnv - dt * 0.7f);
            accentX += dt * 90f;
            dropEnv = Mathf.Max(0f, dropEnv - dt * 0.45f);

            // phase 13 subdivides over time
            if (phase == 12)
            {
                int want = pbNow < 6f ? 3 : (pbNow < 12f ? 5 : 7);
                if (want != n) Layout(want);
            }

            var pose = PoseFor(pbNow);
            float build = actions.Build;
            if (snapCam)
            {
                camPosS = pose.pos; camLookS = pose.look; camFovS = pose.fov; rollS = pose.roll;
                camPosVel = camLookVel = Vector3.zero; camFovVel = rollVel = 0f;
                snapCam = false;
            }
            else
            {
                camPosS = Vector3.SmoothDamp(camPosS, pose.pos, ref camPosVel, 0.5f);
                camLookS = Vector3.SmoothDamp(camLookS, pose.look, ref camLookVel, 0.5f);
                camFovS = Mathf.SmoothDamp(camFovS, pose.fov, ref camFovVel, 0.5f);
                rollS = Mathf.SmoothDamp(rollS, pose.roll, ref rollVel, 0.5f);
            }
            var sway = new Vector3(Mathf.Sin(Time.time * 0.31f), Mathf.Sin(Time.time * 0.23f), 0f) * (0.15f + build * 0.6f);
            cam.transform.position = camPosS + sway;
            cam.transform.rotation = Quaternion.LookRotation(camLookS - cam.transform.position) * Quaternion.AngleAxis(rollS, Vector3.forward);
            cam.fieldOfView = camFovS;

            // where the three cubes sit: one per panel when spread, otherwise just the center one
            float dist = Mathf.Abs(pose.pos.z);
            float spacing = pose.spacing > 0f ? pose.spacing : 2f * Mathf.Min(dist, 25f) * Mathf.Tan(16f * Mathf.Deg2Rad) * WideAspect / 3f;
            float widthScale = Mathf.Clamp(cam.transform.position.magnitude / 25f, 0.35f, 2f);   // keep the lines a similar thickness on screen
            float breath = phase == 11 ? 1f + 0.1f * Mathf.Sin(bpNow * Mathf.PI * 0.5f) : 1f;

            var white = new Color(1f, 1f, 1.1f);
            for (int si = 0; si < 3; si++)
            {
                var st = structs[si];
                float targetVis = (si == 0 || pose.spread) ? 1f : 0f;
                st.vis = Mathf.MoveTowards(st.vis, targetVis, dt * 6f);
                st.x = si == 0 ? 0f : (si == 1 ? -spacing : spacing);
                st.root.localPosition = new Vector3(st.x, 0f, 0f);
                st.root.localScale = Vector3.one * breath * Mathf.Max(0.001f, st.vis);
                st.root.gameObject.SetActive(st.vis > 0.01f);
                if (st.vis <= 0.01f) continue;

                float bp = bpNow - st.delay;              // echoes run one beat behind the center
                float pb = bp - phaseStartBeat;
                float dim = si == 0 ? 1f : 0.7f;
                for (int c = 0; c < st.active; c++)
                {
                    var cell = st.pool[c];
                    var v = Eval(cell, si, bp, pb, st.active);

                    // light that sweeps across the three panels on Accent
                    float wx = st.x + cell.basePos.x;
                    float sweep = accentEnv > 0f ? Mathf.Exp(-Mathf.Pow((wx - accentX) / 9f, 2f)) * 2.4f * accentEnv : 0f;
                    v.edge += sweep;
                    // Drop floods the whole cube white
                    v.edge += dropEnv * 3f;
                    v.fill = Mathf.Max(v.fill, dropEnv * 0.9f);

                    float k = 1f - Mathf.Exp(-20f * dt);
                    cell.edge = Mathf.Lerp(cell.edge, v.edge * (1f + build * 0.8f) * dim, k);
                    cell.fill = Mathf.Lerp(cell.fill, v.fill * dim, k);
                    cell.scale = Mathf.Lerp(cell.scale, v.scale, k);
                    cell.width = Mathf.Lerp(cell.width, v.width * widthScale, k);
                    cell.off = Vector3.Lerp(cell.off, v.off, 1f - Mathf.Exp(-9f * dt));

                    cell.t.localPosition = cell.basePos + cell.off;
                    cell.t.localScale = Vector3.one * (S / n) * Mathf.Max(0.001f, cell.scale);

                    var col = Color.Lerp(Red, Cool, v.tint);
                    col = Color.Lerp(col, white, Mathf.Clamp01(dropEnv));
                    cell.mpb.SetColor("_Color", col);
                    cell.mpb.SetFloat("_Edge", cell.edge);
                    cell.mpb.SetFloat("_Fill", cell.fill);
                    cell.mpb.SetFloat("_Width", cell.width);
                    cell.r.SetPropertyBlock(cell.mpb);
                }
            }

            // the white backlight in the open center: the flash of the "reveal" phase and of every Drop
            float reveal = 0f;
            if (phase == 3) reveal = pbNow < 2f ? 1f : Mathf.Exp(-(pbNow - 2f) * 0.6f);
            reveal = Mathf.Max(reveal, dropEnv);
            glow.position = Vector3.zero;
            glow.rotation = Quaternion.LookRotation(glow.position - cam.transform.position);
            glow.gameObject.SetActive(reveal > 0.01f && structs[0].vis > 0.5f);
            glowMat.SetColor("_Color", new Color(9f * reveal, 9f * reveal, 10f * reveal, Mathf.Clamp01(reveal * 1.3f)));

            fx.Bloom.intensity.value = 1.5f + build * 0.8f + dropEnv * 2f;
            fx.Bloom.threshold.value = 0.7f;
            fx.Vig.intensity.value = 0.3f;
            fx.Chroma.intensity.value = 0.04f + dropEnv * 0.2f;
        }
    }
}
