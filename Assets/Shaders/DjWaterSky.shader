// Underwater "sky": deep blue below, bright hazy cyan toward the surface, with a slow shimmer overhead.
// _DjBeat (global, 0..1) brightens it on the beat.
Shader "DjVisuals/WaterSky"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.005, 0.04, 0.09, 1)
        _Mid ("Mid", Color) = (0.02, 0.2, 0.3, 1)
        _Top ("Surface light", Color) = (0.35, 0.85, 0.95, 1)
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DjNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Deep;
                float4 _Mid;
                float4 _Top;
                float _Intensity;
            CBUFFER_END
            float _DjBeat;

            struct Attributes { float4 pos : POSITION; };
            struct Varyings { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.dir = i.pos.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col = lerp(_Deep.rgb, _Mid.rgb, smoothstep(-0.7, 0.05, h));
                col = lerp(col, _Top.rgb, smoothstep(0.02, 0.85, h));
                col += _Top.rgb * 0.18 * exp(-pow((h - 0.45) * 3.2, 2.0));

                float t = _Time.y * 0.08;
                float shimmer = dj_fbm(float3(d.x / (h + 0.5) * 2.5, d.z / (h + 0.5) * 2.5, t));
                col += _Top.rgb * 0.22 * smoothstep(0.15, 1.0, h) * shimmer;

                col *= _Intensity * (1.0 + _DjBeat * 0.3);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
