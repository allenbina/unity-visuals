// Thin bright photon ring hugging the black hole's shadow, plus broader lensed arcs above and below it
// (the far side of the disc bent over the top). Drawn on a camera-facing quad just in front of the shadow.
Shader "DjVisuals/PhotonRing"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (4, 2.4, 1.2, 1)
        _RingR ("Ring radius (0..1 of quad half-size)", Float) = 0.33
        _RingW ("Ring width", Float) = 0.02
        _ArcAmt ("Arc strength", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" }
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
                float _RingR;
                float _RingW;
                float _ArcAmt;
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
                float2 d = i.uv * 2.0 - 1.0;
                float r = length(d);
                float t0 = (r - _RingR) / _RingW;
                float ring = exp(-t0 * t0);

                float a = atan2(d.y, d.x);
                float up = pow(abs(sin(a)), 2.5);
                float t1 = (r - _RingR * 1.14) / (_RingW * 6.0);
                float arc = exp(-t1 * t1) * up * _ArcAmt;

                float k = ring * 1.6 + arc * 0.8;
                return half4(_Color.rgb * k, saturate(ring + arc));
            }
            ENDHLSL
        }
    }
}
