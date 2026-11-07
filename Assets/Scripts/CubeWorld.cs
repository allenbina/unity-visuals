using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DjVisuals
{
    /// <summary>
    /// The Cube world: a three-screen take on Etienne de Crecy's Beats 'n' Cubes (Transmusicales 2007).
    /// Exactly ONE cube per screen, always. Each cube is a bold outer frame divided into a 3x3x3 grid of glowing boxes
    /// (thinner and dimmer), with an open center column where the DJ stood. The camera stays fixed; the cubes turn, tilt,
    /// tumble and zoom in place. Thirteen phases come from the scenes of the reference video
    /// (docs/beats-n-cubes-scenes.md); Progress (P) advances. The outer two cubes echo the center one a beat late.
    /// All animation is a function of the beat clock, so it stays in time at any BPM.
    /// </summary>
    public class CubeWorld : MonoBehaviour
    {
        const float S = 10f;                 // edge length of one cube
        const float WideAspect = 3840f / 720f;
        const float CamDist = 25f;

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
        readonly Camera[] cams = new Camera[3];   // one camera per screen, indexed like structs: 0 center, 1 left, 2 right
        Mesh cubeMesh;
        Material edgeMat;

        class Cell
        {
            public Transform t; public Renderer r; public MaterialPropertyBlock mpb;
            public int id, order;
            public Vector3Int grid;
            public Vector3 basePos;
            public float edge, fill, scale = 1f, width = 0.12f;
            public Vector3 off;
        }
        class Struct
        {
            public Transform root; public List<Cell> pool = new List<Cell>(); public int active;
            public Cell shell;                      // the one bold outer frame
            public float delay, x;
            public Quaternion rot = Quaternion.identity;
            public float zoom = 1f;
            public Transform glow; public Material glowMat;
        }
        readonly Struct[] structs = new Struct[3];   // 0 center, 1 left, 2 right

        int n = 3;
        int phase;
        float phaseStartBeat;
        float accentEnv, dropEnv, accentX;

        struct Vals { public float edge, fill, scale, width, tint; public Vector3 off; }

        public void Init(VisualActions a, Camera camera, PostFx postFx)
        {
            actions = a;
            cam = camera;
            fx = postFx;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 32f;
            cam.transform.position = new Vector3(0f, 0f, -CamDist);
            cam.transform.rotation = Quaternion.identity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            edgeMat = new Material(Shader.Find("DjVisuals/BoxEdge"));

            float spacing = 2f * CamDist * Mathf.Tan(16f * Mathf.Deg2Rad) * WideAspect / 3f;   // one cube per panel
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Cube" + i).transform;
                root.SetParent(transform);
                var st = new Struct { root = root, delay = i == 0 ? 0f : 1f, x = i == 0 ? 0f : (i == 1 ? -spacing : spacing) };
                root.localPosition = new Vector3(st.x, 0f, 0f);

                // the bold outer frame
                var sg = new GameObject("Frame");
                sg.transform.SetParent(root, false);
                sg.transform.localScale = Vector3.one * S;
                sg.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                var sr = sg.AddComponent<MeshRenderer>();
                sr.sharedMaterial = edgeMat;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                st.shell = new Cell { t = sg.transform, r = sr, mpb = new MaterialPropertyBlock(), width = 0.2f };

                // the white backlight in the open center (the "reveal" flash and every Drop)
                st.glowMat = Mats.Additive(new Color(9f, 9f, 10f, 1f), 1.2f);
                st.glowMat.SetFloat("_VC", 0f);
                st.glow = Mats.Prim(PrimitiveType.Quad, root, "Backlight", Vector3.zero, Vector3.one * 9f, st.glowMat).transform;
                structs[i] = st;
            }

            // One camera per screen, each looking straight at its own cube, so the three cubes are seen identically
            // (a single wide camera would show the outer two from the side).
            var tex = cam.targetTexture;
            cam.rect = new Rect(0f, 0f, 1f / 3f, 1f);
            cams[1] = cam;
            cams[0] = MakeCam("CameraCenter", new Rect(1f / 3f, 0f, 1f / 3f, 1f), tex);
            cams[2] = MakeCam("CameraRight", new Rect(2f / 3f, 0f, 1f / 3f, 1f), tex);

            Layout(3);
            SetPhase(0);

            actions.Beat += OnBeat;
            actions.Accent += OnAccent;
            actions.Drop += OnDrop;
            actions.Progressed += OnProgressed;
            actions.ResetRequested += OnReset;
        }

        Camera MakeCam(string name, Rect r, RenderTexture tex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var c = go.AddComponent<Camera>();
            c.targetTexture = tex;
            c.rect = r;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            c.nearClipPlane = 0.3f;
            c.farClipPlane = 600f;
            c.allowHDR = true;
            c.fieldOfView = 32f;
            var d = go.AddComponent<UniversalAdditionalCameraData>();
            d.renderPostProcessing = true;
            d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            return c;
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
            foreach (var st in structs)
            {
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
                var keys = new List<int>();
                for (int c = 0; c < st.pool.Count; c++)
                {
                    var cell = st.pool[c];
                    bool on = c < coords.Count;
                    cell.t.gameObject.SetActive(on);
                    if (!on) continue;
                    var g = coords[c];
                    cell.id = c;
                    cell.grid = g;
                    cell.basePos = new Vector3((g.x - mid) * cs, (g.y - mid) * cs, (g.z - mid) * cs);
                    cell.t.localScale = Vector3.one * cs;
                    cell.order = g.y * 100 + g.x * 10 + g.z;    // build from the bottom, left to right
                    keys.Add(cell.order);
                }
                keys.Sort();
                for (int c = 0; c < st.active; c++) st.pool[c].order = keys.IndexOf(st.pool[c].order);
            }
        }

        // =================== phases ===================

        void SetPhase(int p)
        {
            phase = ((p % PhaseNames.Length) + PhaseNames.Length) % PhaseNames.Length;
            phaseStartBeat = actions.Clock.BeatPos;
            if (phase != 12 && n != 3) Layout(3);
            actions.Status = "Cube: " + PhaseNames[phase];
        }

        void OnProgressed(int level) { SetPhase(level); }
        void OnReset() { SetPhase(0); accentEnv = 0f; dropEnv = 0f; }
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

        /// <summary>How each cube is turned this phase. The camera never moves, so every screen always shows one whole cube.</summary>
        Quaternion Orient(int si, float pb)
        {
            switch (phase)
            {
                case 1: return Quaternion.Euler(0f, 0f, 12f);
                case 2: return Quaternion.Euler(0f, 28f, -14f);
                case 4: return Quaternion.Euler(-52f, 0f, 0f);                    // tipped toward us: a high-angle view
                case 5: return Quaternion.Euler(pb * 12f + si * 40f, pb * 20f + si * 70f, pb * 7f);   // tumbling in place
                case 9: return Quaternion.Euler(-36f, 0f, 0f);
                case 12: return pb >= 16f ? Quaternion.Euler(-14f, 0f, 0f) : Quaternion.identity;
                default: return Quaternion.Euler(Mathf.Sin(pb * 0.2f) * 3f, Mathf.Sin(pb * 0.17f) * 5f, 0f);
            }
        }

        float ZoomFor(float pb)
        {
            switch (phase)
            {
                case 2: return 1.5f;                                              // close-up
                case 12: return pb >= 16f ? 1.9f : 1f;                           // in toward the open column
                default: return 1f;
            }
        }

        float TintFor(float pb)
        {
            if (phase == 0) return Mathf.Clamp01(pb / 24f);                      // red to cool
            if (phase == 1) return pb < 16f ? 0f : 1f;                           // the red flood, then cool
            return 1f;
        }

        Vals Eval(Cell c, float bp, float pb, int count)
        {
            var v = new Vals { edge = 0.75f, fill = 0f, scale = 1f, width = 0.1f, tint = TintFor(pb), off = Vector3.zero };
            float fr = bp - Mathf.Floor(bp);
            float env = Mathf.Exp(-fr * 6f);                 // 1 on the beat, fading
            int bi = Mathf.FloorToInt(bp);
            float rnd = Hash(bi, c.id);
            float mid = (n - 1) * 0.5f;
            float cs = S / n;
            pb = Mathf.Max(pb, 0f);

            switch (phase)
            {
                case 0:
                {
                    float fade = Mathf.Clamp01(pb / 24f);
                    v.edge = Mathf.Lerp(0.5f, 0.08f, fade) * (0.7f + 0.5f * env);
                    v.fill = (1f - fade) * 0.5f * (c.grid.z == 0 ? 1f : 0.25f) * (0.6f + 0.6f * env);
                    break;
                }
                case 1:
                {
                    if (pb < 16f) { v.fill = 0.65f * (0.55f + 0.45f * env); v.edge = 1.1f; }
                    else
                    {
                        v.edge = 0.03f;
                        if (c.grid.y == Mathf.FloorToInt(bp) % n) { v.edge = 2f * (0.5f + 0.5f * env); v.width = 0.07f; }
                    }
                    break;
                }
                case 2:
                {
                    v.edge = 0.25f;
                    float scan = Mathf.Repeat(pb * 0.45f, 1f) * count;
                    float d = Mathf.Abs(c.order - scan);
                    v.edge += 2f * Mathf.Exp(-d * d / (count * 0.012f + 2f));
                    v.width = 0.14f;
                    break;
                }
                case 3: v.edge = 0.9f * (0.55f + 0.45f * env); break;
                case 4: v.edge = 0.8f * Mathf.Clamp01(pb / 6f) * (0.7f + 0.4f * env); break;
                case 5: v.edge = 0.8f * (0.7f + 0.4f * env); break;
                case 6:   // single cells flood on the beat; a few blocks nudge up
                {
                    v.edge = 0.65f * (0.8f + 0.3f * env);
                    if (rnd < 0.16f) v.fill = env * 0.9f;
                    if (Hash(bi / 2, c.id + 99) < 0.2f) v.off.y = cs * 0.35f;
                    break;
                }
                case 7:   // rows slide like shelves (kept small so the cube still reads as one)
                    v.edge = 0.7f;
                    v.off.x = Mathf.Sin(bp * 0.39f + c.grid.y * 1.7f) * S * 0.12f;
                    break;
                case 8:   // boxes pop out on the beat
                {
                    v.edge = 0.75f;
                    float pop = env * S * 0.09f * (0.6f + Hash(c.id, 5) * 0.8f);
                    if (c.grid.x == 0) v.off.x = -pop;
                    if (c.grid.x == n - 1) v.off.x = pop;
                    if (c.grid.y == n - 1) v.off.y = pop * 0.6f;
                    break;
                }
                case 9:   // dissolve to sparse lines
                {
                    float k = 1f - Mathf.Clamp01(pb / 24f);
                    v.edge = 0.8f * k * k;
                    v.width = Mathf.Lerp(0.03f, 0.1f, k);
                    if (Hash(c.id, 7) > k + 0.15f) v.edge = 0f;
                    break;
                }
                case 10:  // built up cell by cell from the bottom, inside the frame
                {
                    int visible = Mathf.FloorToInt(pb * 2f) + 1;
                    if (c.order >= visible) { v.edge = 0f; v.scale = 0.001f; }
                    else v.edge = (c.order == visible - 1) ? 2f : 0.8f;
                    break;
                }
                case 11:  // breathe: layers drift outward and back
                {
                    float br = Mathf.Sin(bp * Mathf.PI * 0.5f);
                    v.edge = 0.75f * (0.8f + 0.3f * env);
                    var dir = new Vector3(c.grid.x - mid, c.grid.y - mid, c.grid.z - mid);
                    v.off = dir.normalized * S * 0.035f * br;
                    break;
                }
                case 12:  // finer and finer subdivision
                    v.edge = 0.75f * (0.8f + 0.3f * env);
                    v.width = 0.1f * (3f / n);
                    break;
            }
            return v;
        }

        /// <summary>Brightness of the bold outer frame this phase.</summary>
        float FrameEdge(float bp, float pb)
        {
            float fr = bp - Mathf.Floor(bp);
            float env = Mathf.Exp(-fr * 6f);
            pb = Mathf.Max(pb, 0f);
            float pulse = 0.75f + 0.4f * env;
            switch (phase)
            {
                case 0: return Mathf.Lerp(1.2f, 0.12f, Mathf.Clamp01(pb / 24f)) * pulse;
                case 1: return pb < 16f ? 1.8f : 0.12f;
                case 3: return 2.2f * pulse;
                case 9: { float k = 1f - Mathf.Clamp01(pb / 24f); return 1.8f * k * k * pulse; }
                default: return 1.7f * pulse;
            }
        }

        // =================== per frame ===================

        void Update()
        {
            float dt = Time.deltaTime;
            float bpNow = actions.Clock.BeatPos;
            float pbNow = Mathf.Max(0f, bpNow - phaseStartBeat);
            float build = actions.Build;

            accentEnv = Mathf.Max(0f, accentEnv - dt * 0.7f);
            accentX += dt * 90f;
            dropEnv = Mathf.Max(0f, dropEnv - dt * 0.45f);

            if (phase == 12)
            {
                int want = pbNow < 6f ? 3 : (pbNow < 12f ? 5 : 7);
                if (want != n) Layout(want);
            }

            // the camera stays put; only a slow drift and, for the reveal, a pull back
            var sway = new Vector3(Mathf.Sin(Time.time * 0.31f), Mathf.Sin(Time.time * 0.23f), 0f) * (0.1f + build * 0.5f);
            float dolly = phase == 3 ? Mathf.Lerp(5f, 0f, Mathf.Clamp01(pbNow / 12f)) : 0f;
            for (int ci = 0; ci < 3; ci++)
            {
                cams[ci].transform.position = new Vector3(structs[ci].x, 0f, -CamDist + dolly) + sway;
                cams[ci].transform.rotation = Quaternion.identity;
                cams[ci].fieldOfView = 32f;
            }

            var white = new Color(1f, 1f, 1.1f);
            float reveal = 0f;
            if (phase == 3) reveal = pbNow < 2f ? 1f : Mathf.Exp(-(pbNow - 2f) * 0.6f);
            reveal = Mathf.Max(reveal, dropEnv);

            for (int si = 0; si < 3; si++)
            {
                var st = structs[si];
                float bp = bpNow - st.delay;              // the outer two run one beat behind the center
                float pb = bp - phaseStartBeat;
                float dim = si == 0 ? 1f : 0.75f;

                // orientation and zoom: the whole cube turns in place
                var target = Orient(si, Mathf.Max(0f, pb));
                st.rot = Quaternion.Slerp(st.rot, target, 1f - Mathf.Exp(-7f * dt));
                st.zoom = Mathf.Lerp(st.zoom, ZoomFor(pbNow), 1f - Mathf.Exp(-5f * dt));
                float breath = phase == 11 ? 1f + 0.08f * Mathf.Sin(bp * Mathf.PI * 0.5f) : 1f;
                st.root.localRotation = st.rot;
                st.root.localScale = Vector3.one * st.zoom * breath;

                // the outer frame
                float wx = st.x;
                float sweepF = accentEnv > 0f ? Mathf.Exp(-Mathf.Pow((wx - accentX) / 12f, 2f)) * 2.4f * accentEnv : 0f;
                var sh = st.shell;
                float fe = (FrameEdge(bp, pb) + sweepF + dropEnv * 3f) * (1f + build * 0.6f) * dim;
                sh.edge = Mathf.Lerp(sh.edge, fe, 1f - Mathf.Exp(-20f * dt));
                var shCol = Color.Lerp(Red, Cool, TintFor(pb));
                shCol = Color.Lerp(shCol, white, Mathf.Clamp01(dropEnv));
                float shFill = phase == 1 && pb < 16f ? 0.3f : (phase == 0 ? 0.2f * (1f - Mathf.Clamp01(pb / 24f)) : 0f);
                shFill = Mathf.Max(shFill, dropEnv * 0.5f);
                sh.mpb.SetColor("_Color", shCol);
                sh.mpb.SetFloat("_Edge", sh.edge);
                sh.mpb.SetFloat("_Fill", shFill);
                sh.mpb.SetFloat("_Width", sh.width);
                sh.r.SetPropertyBlock(sh.mpb);
                sh.t.gameObject.SetActive(sh.edge > 0.01f || shFill > 0.01f);

                // the inner boxes
                for (int c = 0; c < st.active; c++)
                {
                    var cell = st.pool[c];
                    var v = Eval(cell, bp, pb, st.active);

                    // a bar of light sweeping across the three screens on Accent
                    float sweep = accentEnv > 0f ? Mathf.Exp(-Mathf.Pow((wx + cell.basePos.x * st.zoom - accentX) / 9f, 2f)) * 2f * accentEnv : 0f;
                    v.edge += sweep + dropEnv * 2.2f;
                    v.fill = Mathf.Max(v.fill, dropEnv * 0.7f);

                    float k = 1f - Mathf.Exp(-20f * dt);
                    cell.edge = Mathf.Lerp(cell.edge, v.edge * (1f + build * 0.6f) * dim, k);
                    cell.fill = Mathf.Lerp(cell.fill, v.fill * dim, k);
                    cell.scale = Mathf.Lerp(cell.scale, v.scale, k);
                    cell.width = Mathf.Lerp(cell.width, v.width, k);
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

                // backlight in this cube's open center
                st.glow.rotation = Quaternion.LookRotation(st.root.position - cams[si].transform.position);
                st.glow.gameObject.SetActive(reveal > 0.01f);
                st.glowMat.SetColor("_Color", new Color(9f * reveal, 9f * reveal, 10f * reveal, Mathf.Clamp01(reveal * 1.3f)));
            }

            fx.Bloom.intensity.value = 1.5f + build * 0.8f + dropEnv * 2f;
            fx.Bloom.threshold.value = 0.7f;
            fx.Vig.intensity.value = 0.3f;
            fx.Chroma.intensity.value = 0.04f + dropEnv * 0.2f;
        }
    }
}
