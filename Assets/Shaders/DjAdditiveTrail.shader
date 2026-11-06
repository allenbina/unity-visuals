// Additive streak for TrailRenderer (shooting stars): fades across the width, color/alpha along the length come from the trail gradient.
Shader "DjVisuals/AdditiveTrail"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Soft ("Softness", Range(0.2, 6)) = 1.2
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
                float across = 1.0 - abs(i.uv.y * 2.0 - 1.0);
                float a = pow(saturate(across), _Soft);
                float4 c = i.col * _Color;
                return half4(c.rgb, c.a * a);
            }
            ENDHLSL
        }
    }
}
