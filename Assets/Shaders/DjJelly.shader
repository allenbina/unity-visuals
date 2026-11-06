// Jellyfish: one shared mesh (bell + tentacle ribbons). The bell squashes and the tentacles pull up in the vertex
// shader on each stroke (_Pulse 0..1, set per jellyfish), and the tentacles sway on their own. Additive and glowing.
// Vertex color: r = 1 for tentacle vertices, g = position along the tentacle (0 at the bell rim, 1 at the tip).
Shader "DjVisuals/Jelly"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.3, 1.0, 1.6, 1)
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Phase ("Phase", Float) = 0
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
            #include "DjFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Pulse;
                float _Phase;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float3 normal : NORMAL; float4 col : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; float3 nWS : TEXCOORD0; float3 posWS : TEXCOORD1; float4 col : COLOR; };

            Varyings vert(Attributes i)
            {
                float3 p = i.pos.xyz;
                float k = _Pulse;
                float isTent = i.col.r;
                float L = i.col.g;

                // bell: squeezes in and stretches up on the stroke
                float3 bell = p;
                bell.xz *= lerp(1.0, 0.62, k);
                bell.y *= lerp(1.0, 1.2, k);

                // tentacles: pulled up and narrowed on the stroke, trailing and swaying otherwise
                float3 ten = p;
                ten.xz *= lerp(1.0, 0.78, k);
                ten.y = p.y * (1.0 - 0.3 * k * (1.0 - 0.4 * L)) + 0.45 * k * L;
                float w = _Time.y * 2.0 + L * 5.0 + _Phase;
                ten.x += sin(w + p.z * 2.0) * L * L * 0.4;
                ten.z += cos(w * 0.9 + p.x * 2.0) * L * L * 0.4;

                p = lerp(bell, ten, isTent);

                Varyings o;
                o.pos = TransformObjectToHClip(p);
                o.nWS = TransformObjectToWorldNormal(i.normal);
                o.posWS = TransformObjectToWorld(p);
                o.col = i.col;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                float3 v = normalize(_WorldSpaceCameraPos - i.posWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), 2.0);
                float isTent = i.col.r;
                float L = i.col.g;

                float glow = 0.35 + rim * 1.5 + _Pulse * 1.3;
                float3 c = _Color.rgb * glow;
                float a = lerp(0.18 + rim * 0.5, (1.0 - L) * 0.6 + 0.08, isTent);
                a *= 1.0 + _Pulse * 0.6;
                c *= 1.0 - dj_fogFactor(i.posWS);
                return half4(c, saturate(a));
            }
            ENDHLSL
        }
    }
}
