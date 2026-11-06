// Procedural planet / moon: noisy color bands, one directional light, bright atmosphere rim for bloom.
Shader "DjVisuals/Planet"
{
    Properties
    {
        _ColA ("Color A", Color) = (0.6, 0.45, 0.3, 1)
        _ColB ("Color B", Color) = (0.9, 0.8, 0.6, 1)
        _Bands ("Band count", Float) = 5
        _BandAmt ("Band amount", Range(0, 1)) = 0.8
        _Noise ("Noise scale", Float) = 3
        [HDR] _Atmo ("Atmosphere", Color) = (0.4, 0.6, 1.0, 1)
        _AtmoPow ("Atmosphere falloff", Float) = 3
        _AtmoInt ("Atmosphere intensity", Float) = 1.5
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DjNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColA;
                float4 _ColB;
                float _Bands;
                float _BandAmt;
                float _Noise;
                float4 _Atmo;
                float _AtmoPow;
                float _AtmoInt;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float3 normal : NORMAL; };
            struct Varyings
            {
                float4 pos : SV_POSITION;
                float3 nWS : TEXCOORD0;
                float3 posOS : TEXCOORD1;
                float3 posWS : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.nWS = TransformObjectToWorldNormal(i.normal);
                o.posOS = normalize(i.pos.xyz);
                o.posWS = TransformObjectToWorld(i.pos.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                Light mainLight = GetMainLight();
                float ndl = saturate(dot(n, mainLight.direction));

                float3 p = i.posOS + _Seed;
                float warp = dj_fbm(p * _Noise);
                float band = sin(i.posOS.y * _Bands * 3.14159 + warp * 4.0) * 0.5 + 0.5;
                float mixv = lerp(warp, band, _BandAmt);
                float3 albedo = lerp(_ColA.rgb, _ColB.rgb, saturate(mixv));
                albedo *= 0.75 + 0.5 * dj_fbm(p * _Noise * 3.0);

                float3 lit = albedo * (ndl * 1.05 + 0.075);

                float3 v = normalize(GetWorldSpaceViewDir(i.posWS));
                float fres = pow(1.0 - saturate(dot(n, v)), _AtmoPow);
                float3 rim = _Atmo.rgb * fres * _AtmoInt * (0.2 + ndl * 1.3);
                return half4(lit + rim, 1);
            }
            ENDHLSL
        }
    }
}
