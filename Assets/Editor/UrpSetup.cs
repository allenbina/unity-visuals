using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DjVisuals.EditorTools
{
    /// <summary>
    /// Makes the project run on URP with HDR, with no manual clicking:
    /// creates the pipeline + renderer assets and assigns them as the default and for every quality level.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpSetup
    {
        const string Dir = "Assets/Settings";

        static UrpSetup() { EditorApplication.delayCall += Ensure; }

        [MenuItem("DjVisuals/Ensure URP")]
        public static void Ensure()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;

            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, Dir + "/DjRenderer.asset");

            var rp = UniversalRenderPipelineAsset.Create(rendererData);
            rp.supportsHDR = true;
            AssetDatabase.CreateAsset(rp, Dir + "/DjURP.asset");

            GraphicsSettings.defaultRenderPipeline = rp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = rp;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            Debug.Log("[DjVisuals] URP pipeline assets created and assigned.");
        }
    }
}
