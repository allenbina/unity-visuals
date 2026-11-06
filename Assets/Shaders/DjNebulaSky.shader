// Procedural nebula skybox: layered fbm clouds in three colors, slowly drifting. No textures needed.
// _DjWarp (global, 0..1) brightens and cools the colors during warp.
Shader "DjVisuals/NebulaSky"
{
    Properties
    {
        _ColA ("Cloud A", Color) = (0.05, 0.12, 0.55, 1)
        _ColB ("Cloud B", Color) = (0.65, 0.12, 0.45, 1)
        _ColC ("Core glow", Color) = (0.15, 0.55, 0.75, 1)
        _Intensity ("Intensity", Float) = 1.3
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DjNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColA;
                float4 _ColB;
                float4 _ColC;
                float _Intensity;
            CBUFFER_END
            float _DjWarp;

            struct Attributes { float4 pos : POSITION; };
            struct Varyings { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.dir = i.pos.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float ang = _Time.y * 0.006;
                float s, c;
                sincos(ang, s, c);
                d.xz = float2(d.x * c - d.z * s, d.x * s + d.z * c);
                float t = _Time.y * 0.012;

                float baseN = dj_fbm(d * 1.7 + float3(3.0, 1.0, 7.0));
                float cloud = smoothstep(0.42, 0.82, baseN);
                float detail = dj_fbm(d * 5.5 + float3(t, 0.0, -t));
                float3 col = lerp(_ColA.rgb, _ColB.rgb, saturate(detail * 1.5)) * cloud * (0.3 + detail * 1.1);

                float core = smoothstep(0.6, 0.9, dj_fbm(d * 2.6 + 11.0));
                col += _ColC.rgb * core * core * 1.4;

                // faint galactic band
                float band = exp(-pow(d.y * 2.4 + 0.12 * sin(d.x * 3.0), 2.0));
                col += float3(0.08, 0.09, 0.15) * band * dj_fbm(d * 9.0) * 1.6;

                col += 0.0015;
                col *= _Intensity;
                // warp: brighter, cooler
                col *= 1.0 + _DjWarp * 1.4;
                col = lerp(col, col * float3(0.55, 0.85, 1.5), _DjWarp);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
