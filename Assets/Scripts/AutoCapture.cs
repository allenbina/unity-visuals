using System;
using System.IO;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// Test harness (only active when launched with -djcapture &lt;dir&gt;): runs a scripted timeline
    /// (sync, beat, warp on/off) and saves PNGs of the wide render, then quits the editor.
    /// </summary>
    public class AutoCapture : MonoBehaviour
    {
        public VisualActions Actions;
        public RenderTexture Wide;
        public string OutDir;

        struct Step { public float t; public string kind; public string arg; }
        Step[] steps;
        int next;
        float t0 = -1f;

        public static string ArgDir()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-djcapture") return a[i + 1];
            return null;
        }

        void Start()
        {
            Directory.CreateDirectory(OutDir);
            steps = new[]
            {
                new Step { t = 0.3f, kind = "sync" },
                new Step { t = 0.4f, kind = "shot", arg = "01_beat" },
                new Step { t = 0.8f, kind = "shot", arg = "02_idle" },
                new Step { t = 1.0f, kind = "sats" },
                new Step { t = 1.6f, kind = "shot", arg = "09_sats" },
                new Step { t = 3.0f, kind = "warp" },
                new Step { t = 3.9f, kind = "shot", arg = "03_charge" },
                new Step { t = 5.0f, kind = "shot", arg = "04_jump" },
                new Step { t = 8.0f, kind = "shot", arg = "05_warp" },
                new Step { t = 10.0f, kind = "shot", arg = "06_warp_b" },
                new Step { t = 10.2f, kind = "warp" },
                new Step { t = 11.4f, kind = "shot", arg = "07_exit" },
                new Step { t = 16.0f, kind = "shot", arg = "08_after" },
                new Step { t = 16.5f, kind = "quit" },
            };
        }

        void Update()
        {
            // Shader compilation stalls the first frames in batch mode; start the clock once frames flow.
            if (t0 < 0f)
            {
                if (Time.frameCount < 90 || Time.deltaTime > 0.1f) return;
                t0 = Time.realtimeSinceStartup;
            }
            float t = Time.realtimeSinceStartup - t0;
            while (next < steps.Length && t >= steps[next].t)
            {
                var s = steps[next++];
                switch (s.kind)
                {
                    case "sync": Actions.SetBpm(120); Actions.Sync(); break;
                    case "warp": Actions.ToggleWarp(); break;
                    case "sats": FindFirstObjectByType<SpaceWorld>().DebugSpawnSatellites(); break;
                    case "shot": Shot(s.arg); break;
                    case "quit":
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.Exit(0);
#else
                        Application.Quit();
#endif
                        break;
                }
            }
        }

        void Shot(string name)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = Wide;
            var tex = new Texture2D(Wide.width, Wide.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Wide.width, Wide.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            // center panel only, easier to inspect
            int w = Wide.width / 3;
            var c = new Texture2D(w, Wide.height, TextureFormat.RGBA32, false);
            c.SetPixels(tex.GetPixels(w, 0, w, Wide.height));
            c.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name + "_center.png"), c.EncodeToPNG());
            Destroy(tex);
            Destroy(c);
            Debug.Log("[DjVisuals] captured " + name);
        }
    }
}
