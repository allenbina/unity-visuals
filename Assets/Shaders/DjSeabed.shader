// Seabed / rock / coral surface: sand, one sun from above, animated caustics that flash on the beat,
// bioluminescent patches that light up as the reveal ring (_DjRevealR, expanding from _DjRevealC) sweeps across.
// _CoralMode = 1 makes the whole surface glow (coral), 0 only patches of the seabed.
Shader "DjVisuals/Seabed"
{
    Properties
    {
        _Sand ("Base", Color) = (0.32, 0.28, 0.2, 1)
        _Tint ("Second tone", Color) = (0.12, 0.14, 0.12, 1)
        [HDR] _Glow ("Glow", Color) = (0.2, 1.2, 1.6, 1)
        _CoralMode ("Coral mode", Float) = 0
        _Caustic ("Caustic strength", Float) = 1
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
            #include "DjNoise.hlsl"
            #include "DjFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Sand;
                float4 _Tint;
                float4 _Glow;
                float _CoralMode;
                float _Caustic;
            CBUFFER_END
            float _DjBeat;
            float _DjRevealR;
            float4 _DjRevealC;

            struct Attributes { float4 pos : POSITION; float3 normal : NORMAL; };
            struct Varyings { float4 pos : SV_POSITION; float3 nWS : TEXCOORD0; float3 posWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.nWS = TransformObjectToWorldNormal(i.normal);
                o.posWS = TransformObjectToWorld(i.pos.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // flat-shaded facets from screen-space derivatives, turned toward the camera
                float3 n = normalize(cross(ddy(i.posWS), ddx(i.posWS)));
                if (dot(n, _WorldSpaceCameraPos - i.posWS) < 0.0) n = -n;
                Light L = GetMainLight();
                float ndl = saturate(dot(n, L.direction));

                float3 baseCol = lerp(_Sand.rgb, _Tint.rgb, saturate(dj_noise(i.posWS * 0.25) * 1.4));
                baseCol *= 0.75 + 0.5 * dj_noise(i.posWS * 2.0);
                float3 col = baseCol * (0.22 + ndl * 1.0) * L.color.rgb;

                // caustics: bright, slowly shifting web on upward-facing surfaces
                float t = _Time.y * 0.5;
                float2 p = i.posWS.xz * 0.16;
                float c1 = 1.0 - abs(dj_noise(float3(p * 2.0 + float2(t, t * 0.6), t * 0.4)) * 2.0 - 1.0);
                float c2 = 1.0 - abs(dj_noise(float3(p * 3.1 - float2(t * 0.7, t), t * 0.3 + 5.0)) * 2.0 - 1.0);
                float web = pow(saturate(c1 * 0.55 + c2 * 0.55), 5.0);
                col += float3(0.5, 0.95, 1.0) * web * saturate(n.y) * _Caustic * (0.9 + _DjBeat * 2.2);

                // reef glow, revealed by an expanding ring
                float d = distance(i.posWS.xz, _DjRevealC.xz);
                float inside = smoothstep(_DjRevealR, _DjRevealR - 12.0, d);
                float ring = exp(-pow((d - _DjRevealR) / 9.0, 2.0)) * step(0.0, _DjRevealR) * step(_DjRevealR, 500.0);
                float patch = lerp(smoothstep(0.58, 0.8, dj_noise(i.posWS * 0.11 + 3.0)), 1.0, _CoralMode);
                float pulse = 0.75 + 0.5 * _DjBeat;

                // glow with texture and a bright rim, so reef pieces look lit from within and not like flat color
                float3 vdir = normalize(_WorldSpaceCameraPos - i.posWS);
                float rim = pow(1.0 - saturate(dot(n, vdir)), 2.0);
                float mottle = 0.45 + 0.9 * dj_noise(i.posWS * 1.7) * dj_noise(i.posWS * 0.6 + 9.0) * 2.0;
                float tip = _CoralMode * smoothstep(0.0, 3.5, i.posWS.y - (-17.0));   // brighter toward the tips
                float3 glowCol = _Glow.rgb * (0.3 + 0.8 * rim + 0.45 * mottle + 0.5 * tip);
                col += glowCol * patch * inside * pulse;
                col += float3(1.0, 1.7, 2.1) * ring * 1.6 * (0.4 + 0.6 * saturate(n.y));

                col = dj_fog(col, i.posWS);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
