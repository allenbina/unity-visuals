// Soft round additive sprite for particles (stars, blast, streaks). _Color is HDR so bloom picks it up.
Shader "DjVisuals/Additive"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Soft ("Softness", Range(0.2, 6)) = 1.5
        _VC ("Use vertex color", Float) = 1
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Soft;
                float _VC;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.col = i.col;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                float r = saturate(1.0 - dot(d, d));
                float a = pow(r, _Soft);
                float4 c = lerp(float4(1, 1, 1, 1), i.col, _VC) * _Color;
                return half4(c.rgb, c.a * a);
            }
            ENDHLSL
        }
    }
}
