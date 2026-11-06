// Accretion disc: uv.x runs inner (0) to outer (1) edge, uv.y once around. Hot white-yellow at the inner edge,
// cooling to deep red outward, with swirling streaks that rotate faster closer in (Keplerian), and one side
// brighter (Doppler beaming). HDR output so bloom does the glow. The infalling trails supply the faster inner motion.
Shader "DjVisuals/Accretion"
{
    Properties
    {
        _Spin ("Spin", Float) = 0.7
        _Bright ("Brightness", Float) = 1.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DjNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Spin;
                float _Bright;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float x = i.uv.x;
                float ang = i.uv.y * 6.2831853;
                // Rigid rotation with fixed spiral arms: differential rotation would wind the swirls ever tighter over time.
                float phi = ang + _Time.y * _Spin * 0.6 - x * 3.5;

                float n1 = dj_noise(float3(cos(phi) * 2.2, sin(phi) * 2.2, x * 6.0));
                float n2 = dj_noise(float3(cos(phi * 2.0) * 4.5, sin(phi * 2.0) * 4.5, x * 12.0 + 5.0));
                float streak = lerp(0.45, 1.0, n1 * 0.6 + n2 * 0.4);

                float heat = pow(1.0 - x, 1.7);
                float3 hot = float3(5.0, 3.6, 2.4);
                float3 warm = float3(3.2, 1.1, 0.25);
                float3 cool = float3(1.1, 0.16, 0.05);
                float3 col = lerp(cool, warm, saturate(heat * 1.6));
                col = lerp(col, hot, saturate(heat * heat * heat * 2.0));

                float dop = 0.5 + 0.95 * (0.5 + 0.5 * sin(ang + 1.2));
                float edge = smoothstep(0.0, 0.05, x) * smoothstep(1.0, 0.5, x);
                float a = edge * streak;
                return half4(col * streak * dop * _Bright, saturate(a));
            }
            ENDHLSL
        }
    }
}
