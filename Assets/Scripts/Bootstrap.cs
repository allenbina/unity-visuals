using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// Builds the whole experiment at Play time, so any empty scene works:
    /// clock -> actions -> rain world -> one wide camera -> RenderTexture -> three panels + controls.
    /// </summary>
    public static class Bootstrap
    {
        const int PanelCount = 3;

        // Which world to run. Space is wired for the beat only; rain has every control.
        static readonly bool UseSpace = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            // Silence whatever the default scene ships with.
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;

            var root = new GameObject("DjVisuals");

            var clock = root.AddComponent<BeatClock>();
            var actions = root.AddComponent<VisualActions>();
            actions.Clock = clock;

            if (UseSpace)
            {
                var space = new GameObject("SpaceWorld").AddComponent<SpaceWorld>();
                space.transform.SetParent(root.transform);
                space.Init(actions);
            }
            else
            {
                var rain = new GameObject("RainWorld").AddComponent<RainWorld>();
                rain.transform.SetParent(root.transform);
                rain.Init(actions);
            }

            // One wide view, cropped into PanelCount windows by the control strip.
            // Width scales with panel count so a 3x3 or other layout keeps the same pixel density.
            var rt = new RenderTexture(1280 * PanelCount, 720, 24, RenderTextureFormat.ARGB32) { name = "WideView" };
            rt.Create();

            var camGo = new GameObject("WideCamera");
            camGo.transform.SetParent(root.transform);
            if (UseSpace)
            {
                // Chase view: behind the ship, which sits at the origin facing +Z.
                camGo.transform.position = new Vector3(0, 1.4f, -9f);
                camGo.transform.rotation = Quaternion.Euler(4f, 0, 0);
            }
            else
            {
                camGo.transform.position = new Vector3(0, 3.2f, -16f);
                camGo.transform.rotation = Quaternion.Euler(4f, 0, 0);
            }
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.005f, 0.007f, 0.012f);
            cam.fieldOfView = 30f;   // vertical; horizontal is very wide because the target is 48:9
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;

            // Game view needs a camera that renders to screen or it shows a warning; this one just clears to black.
            var screenCam = new GameObject("ScreenClearCamera").AddComponent<Camera>();
            screenCam.transform.SetParent(root.transform);
            screenCam.clearFlags = CameraClearFlags.SolidColor;
            screenCam.backgroundColor = new Color(0.03f, 0.03f, 0.04f);
            screenCam.cullingMask = 0;
            screenCam.depth = -100;

            var strip = root.AddComponent<ControlStrip>();
            strip.Actions = actions;
            strip.Wide = rt;
            strip.PanelCount = PanelCount;
            strip.ShowAdvanced = !UseSpace;   // hide controls that aren't wired to the space world yet
        }
    }
}
