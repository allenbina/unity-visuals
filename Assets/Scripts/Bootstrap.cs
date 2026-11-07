using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DjVisuals
{
    /// <summary>
    /// Builds the shared part of the experiment at Play time (clock, actions, wide camera, post-processing, panels and
    /// controls) and then loads a world into it. Worlds are swapped with the 1 / 2 keys or the buttons in the strip.
    /// Any empty scene works.
    /// </summary>
    public static class Bootstrap
    {
        const int PanelCount = 3;

        static GameObject root, worldGo;
        static VisualActions actions;
        static Camera cam;
        static PostFx fx;
        static ControlStrip strip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            // Silence whatever the default scene ships with.
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;

            root = new GameObject("DjVisuals");

            var clock = root.AddComponent<BeatClock>();
            actions = root.AddComponent<VisualActions>();
            actions.Clock = clock;

            // One wide view, cropped into PanelCount windows by the control strip.
            // Width scales with panel count so a 3x3 or other layout keeps the same pixel density.
            var rt = new RenderTexture(1280 * PanelCount, 720, 24, RenderTextureFormat.ARGB32) { name = "WideView" };
            rt.Create();

            var camGo = new GameObject("WideCamera");
            camGo.transform.SetParent(root.transform);
            cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.005f, 0.007f, 0.012f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            cam.allowHDR = true;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            fx = PostFx.Create(root.transform);

            // Game view needs a camera that renders to screen or it shows a warning; this one just clears to black.
            var screenCam = new GameObject("ScreenClearCamera").AddComponent<Camera>();
            screenCam.transform.SetParent(root.transform);
            screenCam.clearFlags = CameraClearFlags.SolidColor;
            screenCam.backgroundColor = new Color(0.03f, 0.03f, 0.04f);
            screenCam.cullingMask = 0;
            screenCam.depth = -100;

            strip = root.AddComponent<ControlStrip>();
            strip.Actions = actions;
            strip.Wide = rt;
            strip.PanelCount = PanelCount;

            actions.WorldRequested += LoadWorld;
            LoadWorld(StartWorld());

            var capDir = AutoCapture.ArgDir();
            if (capDir != null)
            {
                var cap = root.AddComponent<AutoCapture>();
                cap.Actions = actions;
                cap.Wide = rt;
                cap.OutDir = capDir;
            }
        }

        // -djworld N on the command line starts in world N (used by the headless capture)
        static int StartWorld()
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "-djworld" && int.TryParse(a[i + 1], out int w) && w >= 0 && w < VisualActions.WorldNames.Length) return w;
            return 0;
        }

        static void LoadWorld(int index)
        {
            if (worldGo != null) Object.Destroy(worldGo);
            worldGo = new GameObject(VisualActions.WorldNames[index] + "World");
            worldGo.transform.SetParent(root.transform);

            actions.CurrentWorld = index;
            actions.Status = "";
            actions.SetBuild(0f);
            fx.ResetDefaults();

            // neutral state for the new world to override
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.fog = false;
            Shader.SetGlobalFloat("_DjFogDensity", 0f);
            Shader.SetGlobalFloat("_DjWarp", 0f);
            Shader.SetGlobalFloat("_DjBeat", 0f);
            Shader.SetGlobalFloat("_DjRevealR", -1f);

            switch (index)
            {
                case 2:
                    worldGo.AddComponent<CubeWorld>().Init(actions, cam, fx);
                    strip.ShowCore = true; strip.ShowWarp = false; strip.ShowAdvanced = true;   // Progress = next phase
                    break;
                case 1:
                    worldGo.AddComponent<UnderwaterWorld>().Init(actions, cam, fx);
                    strip.ShowCore = true; strip.ShowWarp = false; strip.ShowAdvanced = false;
                    break;
                default:
                    worldGo.AddComponent<SpaceWorld>().Init(actions, cam, fx);
                    strip.ShowCore = true; strip.ShowWarp = true; strip.ShowAdvanced = false;
                    break;
            }
        }
    }
}
