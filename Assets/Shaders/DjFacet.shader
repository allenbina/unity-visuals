// Faceted creature / reef-piece shader: the one look for the underwater world.
// Flat shading comes from screen-space derivatives, so any mesh reads as low-poly. Albedo is the mesh vertex color times _Tint.
// Creature meshes (see tools/convert_fish.py) carry uv0 = (L, lat): L is -1 at the tail to +1 at the nose, lat is |x| / widest x.
// The vertex shader wags the tail (_Wag phase in radians, _WagAmp, _WagUp = 1 swings up/down like a whale) and flaps wings (_Flap).
// Reef pieces have uv0 = 0 and _WagAmp = 0, so nothing moves.
Shader "DjVisuals/Facet"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _Glow ("Glow", Color) = (0, 0, 0, 1)
        _Wag ("Wag phase", Float) = 0
        _WagAmp ("Wag amplitude", Float) = 0
        _WagUp ("Wag vertical (0 side, 1 up)", Float) = 0
        _Flap ("Wing flap", Float) = 0
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
                float4 _Tint;
                float4 _Glow;
                float _Wag;
                float _WagAmp;
                float _WagUp;
                float _Flap;
                float _Light;
            CBUFFER_END
            float _DjBeat;

            struct Attributes { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float4 col : COLOR; };

            Varyings vert(Attributes i)
            {
                float3 p = i.pos.xyz;
                float L = i.uv.x;
                float lat = i.uv.y;
                float tw = saturate((0.15 - L) / 1.15);
                tw *= tw;
                float swing = sin(_Wag - L * 3.0) * _WagAmp * tw;
                p.x += swing * (1.0 - _WagUp);
                p.y += swing * _WagUp;
                p.x += sin(_Wag) * _WagAmp * 0.12 * saturate(0.5 - L) * (1.0 - _WagUp);   // the whole body sways a little
                p.y += sin(_Wag) * _Flap * lat * lat;                                      // wings beat

                Varyings o;
                o.pos = TransformObjectToHClip(p);
                o.posWS = TransformObjectToWorld(p);
                o.col = i.col;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.posWS), ddx(i.posWS)));
                float3 v = normalize(_WorldSpaceCameraPos - i.posWS);
                if (dot(n, v) < 0.0) n = -n;

                Light L = GetMainLight();
                float ndl = saturate(dot(n, L.direction));
                float3 albedo = i.col.rgb * _Tint.rgb;
                float3 col = albedo * (0.28 + 0.12 * n.y + ndl * 0.95 * _Light) * L.color.rgb;
                float rim = pow(1.0 - saturate(dot(n, v)), 3.0);
                col += float3(0.4, 0.7, 1.0) * rim * 0.22;
                col += _Glow.rgb * (0.55 + rim) * (1.0 + _DjBeat * 0.8);
                col *= 1.0 + _DjBeat * 0.18;
                col = dj_fog(col, i.posWS);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
