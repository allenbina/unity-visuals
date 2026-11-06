using UnityEditor;
using UnityEditor.SceneManagement;

namespace DjVisuals.EditorTools
{
    /// <summary>
    /// Headless screenshot run, used to check the visuals without a person watching:
    ///   Unity.exe -batchmode -projectPath . -executeMethod DjVisuals.EditorTools.CaptureTool.Run -djcapture &lt;outDir&gt;
    /// Enters Play mode in an empty scene; AutoCapture (runtime) drives the scripted timeline and quits.
    /// </summary>
    public static class CaptureTool
    {
        public static void Run()
        {
            UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorApplication.EnterPlaymode();
        }
    }
}
