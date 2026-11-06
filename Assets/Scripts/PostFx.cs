using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DjVisuals
{
    /// <summary>A global post-processing volume built in code: bloom, tonemapping, lens distortion, chromatic aberration, vignette, exposure.</summary>
    public class PostFx : MonoBehaviour
    {
        public Bloom Bloom;
        public LensDistortion Lens;
        public ChromaticAberration Chroma;
        public Vignette Vig;
        public ColorAdjustments Adjust;

        public static PostFx Create(Transform parent)
        {
            var go = new GameObject("PostFx");
            go.transform.SetParent(parent);
            var fx = go.AddComponent<PostFx>();
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            fx.Bloom = profile.Add<Bloom>(true);
            fx.Bloom.threshold.value = 0.9f;
            fx.Bloom.intensity.value = 1.1f;
            fx.Bloom.scatter.value = 0.7f;
            fx.Bloom.highQualityFiltering.value = true;

            var tm = profile.Add<Tonemapping>(true);
            tm.mode.value = TonemappingMode.Neutral;

            fx.Lens = profile.Add<LensDistortion>(true);
            fx.Lens.intensity.value = 0f;

            fx.Chroma = profile.Add<ChromaticAberration>(true);
            fx.Chroma.intensity.value = 0f;

            fx.Vig = profile.Add<Vignette>(true);
            fx.Vig.intensity.value = 0.2f;
            fx.Vig.smoothness.value = 0.6f;

            fx.Adjust = profile.Add<ColorAdjustments>(true);
            fx.Adjust.postExposure.value = 0f;
            fx.Adjust.saturation.value = 10f;
            return fx;
        }
    }
}
