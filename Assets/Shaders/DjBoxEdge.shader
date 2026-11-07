// A box drawn as glowing edges, like light projected onto scaffolding tubes, with brighter dots at the corners (the tube
// joints) and an optional soft fill (the translucent fabric). Put it on a unit cube mesh; widths are in WORLD units so
// the line thickness stays the same however the cube is scaled. Additive and see-through, so the far edges show too.
Shader "DjVisuals/BoxEdge"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.55, 0.88, 1, 1)
        _Edge ("Edge intensity", Float) = 1
        _Fill ("Fill", Float) = 0
        _Width ("Line width (world units)", Float) = 0.07
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Edge;
                float _Fill;
                float _Width;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; };
            struct Varyings { float4 pos : SV_POSITION; float3 posOS : TEXCOORD0; float3 scale : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.posOS = i.pos.xyz;
                o.scale = float3(length(unity_ObjectToWorld._m00_m10_m20),
                                 length(unity_ObjectToWorld._m01_m11_m21),
                                 length(unity_ObjectToWorld._m02_m12_m22));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // distance (in world units) from this surface point to each of the box's three face planes
                float3 ds = (0.5 - abs(i.posOS)) * i.scale;
                float m1 = min(ds.x, min(ds.y, ds.z));
                float m3 = max(ds.x, max(ds.y, ds.z));
                float m2 = ds.x + ds.y + ds.z - m1 - m3;   // on a face m1 is ~0; m2 small means "near an edge"

                float edge = 1.0 - smoothstep(0.0, _Width, m2);
                float corner = 1.0 - smoothstep(0.0, _Width * 2.6, m3);   // all three small: a joint
                float k = edge * _Edge + corner * _Edge * 1.4 + _Fill * 0.55;
                return half4(_Color.rgb * k, saturate(k));
            }
            ENDHLSL
        }
    }
}
