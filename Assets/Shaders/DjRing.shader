// Planet ring: uv.x runs inner (0) to outer (1) edge. Banded, translucent.
Shader "DjVisuals/Ring"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.75, 0.6, 1)
        _Density ("Band density", Float) = 40
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
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
                float4 _Color;
                float _Density;
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
                float bands = dj_noise(float3(x * _Density, 1.7, 0.3)) * 0.65 + dj_noise(float3(x * _Density * 3.1, 4.2, 0.9)) * 0.35;
                float edge = smoothstep(0.0, 0.07, x) * smoothstep(1.0, 0.88, x);
                float gap = smoothstep(0.46, 0.5, x) * smoothstep(0.58, 0.54, x); // a Cassini-style gap
                float a = edge * (0.15 + 0.85 * bands) * (1.0 - 0.85 * gap) * _Color.a;
                float3 col = _Color.rgb * (0.45 + 0.7 * bands);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
