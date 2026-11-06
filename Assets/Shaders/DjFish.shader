// Fish: one shared mesh, +Z is the nose and -Z the tail. The tail wags in the vertex shader from _Wag (radians),
// which the world sets from the beat clock so the tail beats in time. Vertex color g = 1 marks the fins.
Shader "DjVisuals/Fish"
{
    Properties
    {
        _Color ("Back", Color) = (0.25, 0.45, 0.6, 1)
        _Belly ("Belly", Color) = (0.85, 0.92, 0.95, 1)
        _Fin ("Fins", Color) = (0.9, 0.55, 0.2, 1)
        _Wag ("Wag phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DjFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Belly;
                float4 _Fin;
                float _Wag;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float3 normal : NORMAL; float4 col : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; float3 nWS : TEXCOORD0; float3 posWS : TEXCOORD1; float up : TEXCOORD2; float fin : TEXCOORD3; };

            Varyings vert(Attributes i)
            {
                float3 p = i.pos.xyz;
                float tail = saturate((0.2 - p.z) * 0.85);
                tail *= tail;
                p.x += sin(_Wag - p.z * 3.2) * 0.34 * tail;
                p.x += sin(_Wag) * 0.04 * saturate(0.5 - p.z);   // whole body sways a little

                Varyings o;
                o.pos = TransformObjectToHClip(p);
                o.nWS = TransformObjectToWorldNormal(i.normal);
                o.posWS = TransformObjectToWorld(p);
                o.up = i.normal.y;
                o.fin = i.col.g;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                Light L = GetMainLight();
                float ndl = saturate(abs(dot(n, L.direction)));
                float3 albedo = lerp(_Belly.rgb, _Color.rgb, saturate(i.up * 0.5 + 0.55));
                albedo = lerp(albedo, _Fin.rgb, i.fin);
                float3 col = albedo * (0.3 + ndl * 0.95) * L.color.rgb;
                float3 v = normalize(_WorldSpaceCameraPos - i.posWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), 3.0);
                col += float3(0.45, 0.75, 1.0) * rim * 0.35;
                col = dj_fog(col, i.posWS);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
