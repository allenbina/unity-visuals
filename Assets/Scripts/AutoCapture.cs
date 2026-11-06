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
                new Step { t = 0.4f, kind = "shot", arg = "01_home" },
                new Step { t = 1.0f, kind = "accent" },
                new Step { t = 1.5f, kind = "shot", arg = "02_accent_mid" },
                new Step { t = 3.2f, kind = "shot", arg = "02b_pose1_left" },
                new Step { t = 3.5f, kind = "accent" },
                new Step { t = 5.4f, kind = "shot", arg = "03_pose2_right" },
                new Step { t = 5.8f, kind = "accent" },
                new Step { t = 7.6f, kind = "shot", arg = "04_pose3_center" },
                new Step { t = 8.0f, kind = "accent" },
                new Step { t = 9.8f, kind = "shot", arg = "05_pose4_left" },
                new Step { t = 10.2f, kind = "accent" },
                new Step { t = 12.0f, kind = "shot", arg = "06_pose0_right" },
                new Step { t = 12.5f, kind = "warp" },
                new Step { t = 14.8f, kind = "shot", arg = "07_warp" },
                new Step { t = 15.3f, kind = "warp" },                // exit: next region streams in (no roll)
                new Step { t = 16.9f, kind = "shot", arg = "08_arrive_a" },
                new Step { t = 18.0f, kind = "shot", arg = "08_arrive_b" },
                new Step { t = 21.0f, kind = "shot", arg = "09_settled" },
                new Step { t = 21.5f, kind = "quit" },
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
                    case "accent": Actions.FireAccent(); break;
                    case "drop": Actions.FireDrop(); break;
                    case "build": Actions.SetBuild(0.8f); break;
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
