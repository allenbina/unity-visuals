// Jellyfish: one faceted mesh with a solid-looking bell (outer and inner shell, so it reads as a volume), a glowing core,
// four thick oral arms and thin trailing tentacles. The bell squeezes and the rim tucks in on each stroke, then overshoots
// and flares back out; the arms and tentacles trail and sway. _Pulse runs from about -0.35 (overshoot) to 1 (fully squeezed).
// Vertex color: r = 1 for arm and tentacle vertices, g = position along them (0 at the top, 1 at the tip),
// b = how close a bell vertex is to the rim, a = part (1 outer shell, 0.9 inner shell, 0.7 core, 0.5 arm, 0 tentacle).
Shader "DjVisuals/Jelly"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.3, 1.0, 1.6, 1)
        _Pulse ("Pulse", Range(-0.5, 1)) = 0
        _Phase ("Phase", Float) = 0
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
            #include "DjFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Pulse;
                float _Phase;
            CBUFFER_END
            float _DjBeat;

            struct Attributes { float4 pos : POSITION; float4 col : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; float3 posWS : TEXCOORD1; float4 col : COLOR; };

            Varyings vert(Attributes i)
            {
                float3 p = i.pos.xyz;
                float k = _Pulse;
                float isTent = i.col.r;
                float L = i.col.g;
                float rimW = i.col.b;
                float arm = step(0.25, i.col.a) * step(i.col.a, 0.55);

                // bell: squeezes in and stretches up, the rim tucks in even further; overshoots back out when k < 0
                float3 bell = p;
                bell.xz *= lerp(1.0, 0.5, k) * (1.0 - 0.24 * k * rimW);
                bell.y *= lerp(1.0, 1.3, k);

                // arms and tentacles: pulled up and narrowed on the stroke, streaming and swaying otherwise
                float3 ten = p;
                ten.xz *= lerp(1.0, 0.7, k);
                ten.y = p.y * (1.0 - 0.35 * k * (1.0 - 0.4 * L)) + 0.55 * k * L;
                float w = _Time.y * 2.4 + L * 5.0 + _Phase;
                float amp = lerp(0.75, 0.3, arm);
                ten.x += sin(w + p.z * 2.0) * L * L * amp;
                ten.z += cos(w * 0.9 + p.x * 2.0) * L * L * amp;

                p = lerp(bell, ten, isTent);

                Varyings o;
                o.pos = TransformObjectToHClip(p);
                o.posWS = TransformObjectToWorld(p);
                o.col = i.col;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.posWS), ddx(i.posWS)));   // faceted
                float3 v = normalize(_WorldSpaceCameraPos - i.posWS);
                if (dot(n, v) < 0.0) n = -n;
                float fres = pow(1.0 - saturate(dot(n, v)), 2.0);
                float up = saturate(n.y * 0.5 + 0.5);                          // lit from the surface above

                float part = i.col.a;
                float L = i.col.g;
                float squeeze = max(_Pulse, 0.0);
                float beat = 1.0 + squeeze * 0.5 + _DjBeat * 0.25;

                float3 c; float a;
                if (part > 0.95)            // outer shell: shaded facets with a glowing rim
                {
                    c = _Color.rgb * (0.22 + 0.5 * up) + _Color.rgb * fres * 1.5;
                    a = 0.32 + fres * 0.5;
                }
                else if (part > 0.85)       // inner shell: a darker second surface, gives depth
                {
                    c = _Color.rgb * (0.12 + 0.3 * up) + _Color.rgb * fres * 0.6;
                    a = 0.28 + fres * 0.3;
                }
                else if (part > 0.6)        // core: bright, lit from within
                {
                    c = _Color.rgb * 1.4 + float3(0.35, 0.35, 0.35);
                    a = 0.9;
                }
                else if (part > 0.25)       // oral arms
                {
                    c = _Color.rgb * (0.45 + 0.5 * up) + _Color.rgb * fres * 0.6;
                    a = (1.0 - L * 0.55) * 0.75;
                }
                else                        // thin tentacles
                {
                    c = _Color.rgb * (0.55 + 0.4 * up);
                    a = (1.0 - L) * 0.55 + 0.08;
                }
                c *= beat;
                float f = dj_fogFactor(i.posWS);
                c = lerp(c, _DjFogColor.rgb, f * 0.8);
                return half4(c, saturate(a) * (1.0 - f * 0.5));
            }
            ENDHLSL
        }
    }
}
