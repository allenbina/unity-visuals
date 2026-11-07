// Faceted bubble: a low-poly ball that is mostly clear with a bright rim, so it matches the faceted fish and coral.
// Two uses on the same shader:
//  - ambient and accent bubbles: mesh particles (_Wall = 0); particle color arrives as vertex color.
//  - the bubble wall: one baked mesh with thousands of bubbles (_Wall = 1). Each bubble's center rides in uv1, and
//    uv2 = (size, rise speed, phase). The vertex shader lifts every bubble from _WallT (seconds since the wall started).
// Blending is premultiplied alpha, so a thick enough wall hides what is behind it.
Shader "DjVisuals/Bubble"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.7, 1.2, 1.5, 1)
        _Fill ("Fill alpha", Float) = 0.12
        _Wall ("Wall mode", Float) = 0
        _WallT ("Wall time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DjFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fill;
                float _Wall;
                float _WallT;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float4 col : COLOR; float3 ctr : TEXCOORD1; float3 prm : TEXCOORD2; };
            struct Varyings { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float4 col : COLOR; };

            Varyings vert(Attributes i)
            {
                float3 w;
                if (_Wall > 0.5)
                {
                    float3 c = i.ctr;
                    float ph = i.prm.z;
                    c.y += i.prm.y * _WallT;
                    c.x += sin(_WallT * 2.3 + ph) * 0.9;
                    c.z += cos(_WallT * 1.7 + ph) * 0.5;
                    w = c + i.pos.xyz * i.prm.x;
                }
                else
                {
                    w = TransformObjectToWorld(i.pos.xyz);
                }
                Varyings o;
                o.pos = TransformWorldToHClip(w);
                o.posWS = w;
                o.col = i.col;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.posWS), ddx(i.posWS)));
                float3 v = normalize(_WorldSpaceCameraPos - i.posWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), 1.4);
                float a = saturate((_Fill + rim * 0.75) * i.col.a);
                float3 c = _Color.rgb * (0.45 + rim * 1.5) * a;
                float vis = 1.0 - dj_fogFactor(i.posWS) * 0.7;
                return half4(c * vis, a * vis);
            }
            ENDHLSL
        }
    }
}
