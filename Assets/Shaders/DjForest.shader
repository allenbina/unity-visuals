// Forest props and ground, one faceted look. Flat shading from screen-space derivatives.
// Vertex color (see tools/convert_nature.py): r = part (0 -> _ColA trunk/rock/moss, 1 -> _ColB foliage/dirt),
// g = height up the model (0 base .. 1 top). Foliage and grass sway in the wind, more toward the top (_Sway).
// Foliage glows when you look toward the sun (forward scatter), and everything fades into the mist (DjFog globals).
Shader "DjVisuals/Forest"
{
    Properties
    {
        _ColA ("Part 0 (trunk / rock / moss)", Color) = (0.25, 0.17, 0.1, 1)
        _ColB ("Part 1 (foliage / dirt)", Color) = (0.2, 0.5, 0.12, 1)
        [HDR] _Glow ("Glow", Color) = (0, 0, 0, 1)
        _Sway ("Wind sway", Float) = 0.1
        _Light ("Light", Float) = 1
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
                float4 _ColA;
                float4 _ColB;
                float4 _Glow;
                float _Sway;
                float _Light;
            CBUFFER_END
            float _DjBeat;

            struct Attributes { float4 pos : POSITION; float4 col : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float3 col : COLOR; };

            Varyings vert(Attributes i)
            {
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                float h = i.col.g;
                float wind = sin(_Time.y * 1.3 + wp.x * 0.35 + wp.z * 0.25) + 0.5 * sin(_Time.y * 2.7 + wp.z * 0.6);
                wp.x += wind * _Sway * h * h * (0.35 + 0.65 * i.col.r);   // foliage and grass move more than trunks
                wp.z += wind * _Sway * 0.4 * h * h * i.col.r;

                Varyings o;
                o.pos = TransformWorldToHClip(wp);
                o.posWS = wp;
                o.col = i.col.rgb;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.posWS), ddx(i.posWS)));
                float3 v = normalize(_WorldSpaceCameraPos - i.posWS);
                if (dot(n, v) < 0.0) n = -n;

                Light L = GetMainLight();
                float ndl = saturate(dot(n, L.direction));
                float part = i.col.r;
                float3 albedo = lerp(_ColA.rgb, _ColB.rgb, part) * (0.78 + 0.34 * i.col.g) * (0.82 + 0.36 * i.col.b);

                float3 col = albedo * (0.3 + 0.1 * n.y + ndl * 0.95 * _Light) * L.color.rgb;
                // looking toward the sun: foliage lights up from behind
                float toSun = saturate(dot(-v, L.direction));
                col += albedo * L.color.rgb * pow(toSun, 3.0) * (0.35 + 0.8 * part) * 0.7;
                col += _Glow.rgb * (0.5 + _DjBeat * 0.6);
                col *= 1.0 + _DjBeat * 0.06;
                col = dj_fog(col, i.posWS);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
