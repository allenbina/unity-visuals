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
            var script = ScriptName();
            steps = script == "underwater" ? UnderwaterSteps() : script == "switch" ? SwitchSteps() : script == "cube" ? CubeSteps() : SpaceSteps();
        }

        static string ScriptName()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-djscript") return a[i + 1];
            return "";
        }

        static Step[] CubeSteps()
        {
            // 13 phases, 6 seconds each (12 beats at 120 BPM): a shot early and a shot late in each
            var list = new System.Collections.Generic.List<Step>();
            list.Add(new Step { t = 0.3f, kind = "sync" });
            for (int p = 0; p < 13; p++)
            {
                float t0 = 0.4f + p * 6f;
                if (p > 0) list.Add(new Step { t = t0, kind = "progress" });
                list.Add(new Step { t = t0 + 1.7f, kind = "shot", arg = "p" + (p + 1).ToString("00") + "_a" });
                list.Add(new Step { t = t0 + 4.9f, kind = "shot", arg = "p" + (p + 1).ToString("00") + "_b" });
            }
            list.Add(new Step { t = 0.4f + 13 * 6f, kind = "quit" });
            return list.ToArray();
        }

        static Step[] SwitchSteps()
        {
            return new[]
            {
                new Step { t = 0.3f, kind = "sync" },
                new Step { t = 1.0f, kind = "shot", arg = "01_space" },
                new Step { t = 1.5f, kind = "world", arg = "1" },
                new Step { t = 3.5f, kind = "shot", arg = "02_underwater" },
                new Step { t = 3.8f, kind = "accent" },
                new Step { t = 4.0f, kind = "drop" },
                new Step { t = 4.5f, kind = "world", arg = "0" },
                new Step { t = 6.5f, kind = "shot", arg = "03_space_again" },
                new Step { t = 6.8f, kind = "accent" },
                new Step { t = 7.2f, kind = "drop" },
                new Step { t = 8.0f, kind = "world", arg = "1" },
                new Step { t = 9.5f, kind = "shot", arg = "04_underwater_again" },
                new Step { t = 10.0f, kind = "quit" },
            };
        }

        static Step[] UnderwaterSteps()
        {
            // deep -> surge builds -> Drop (bubble wall, swap 2.0 s in) -> coral reef -> Drop -> deep with the reveal ring
            return new[]
            {
                new Step { t = 0.3f, kind = "sync" },
                new Step { t = 2.0f, kind = "shot", arg = "01_deep" },
                new Step { t = 2.4f, kind = "shot", arg = "02_deep_b" },
                new Step { t = 3.0f, kind = "surge" },
                new Step { t = 5.5f, kind = "shot", arg = "03_surge_early" },
                new Step { t = 8.0f, kind = "shot", arg = "04_surge_mid" },
                new Step { t = 10.5f, kind = "shot", arg = "05_surge_full" },
                new Step { t = 11.0f, kind = "drop" },
                new Step { t = 12.2f, kind = "shot", arg = "06_wall_rising" },
                new Step { t = 12.9f, kind = "shot", arg = "07_wall_swap" },
                new Step { t = 13.8f, kind = "shot", arg = "08_wall_leaving" },
                new Step { t = 16.0f, kind = "shot", arg = "09_reef" },
                new Step { t = 16.4f, kind = "shot", arg = "10_reef_b" },
                new Step { t = 20.0f, kind = "shot", arg = "11_reef_c" },
                new Step { t = 24.0f, kind = "drop" },
                new Step { t = 28.0f, kind = "shot", arg = "12_deep_back" },
                new Step { t = 29.0f, kind = "shot", arg = "13_deep_lit" },
                new Step { t = 29.5f, kind = "quit" },
            };
        }

        static Step[] SpaceSteps()
        {
            return new[]
            {
                new Step { t = 0.3f, kind = "sync" },        // downbeat at 0.3, then a beat every 0.5 s
                new Step { t = 0.42f, kind = "shot", arg = "01_downbeat" },
                new Step { t = 0.70f, kind = "shot", arg = "02_between" },
                new Step { t = 0.86f, kind = "shot", arg = "03_beat2" },
                new Step { t = 1.20f, kind = "shot", arg = "04_between" },
                new Step { t = 1.88f, kind = "shot", arg = "05_beat4" },
                new Step { t = 2.3f, kind = "quit" },
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
                    case "surge": Actions.ToggleWarp(); break;
                    case "progress": Actions.Advance(); break;
                    case "world": Actions.SelectWorld(int.Parse(s.arg)); break;
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
            var shipGo = GameObject.Find("Ship");
            if (shipGo != null) Debug.Log("[DjVisuals] ship " + name + " z=" + shipGo.transform.localPosition.z.ToString("F3") + " pitch=" + shipGo.transform.localEulerAngles.x.ToString("F2"));
            Debug.Log("[DjVisuals] captured " + name);
        }
    }
}
