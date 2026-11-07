// Light veil for the bubble wall: a soft aqua-white glow that rides up the screen with the wall and swells to cover
// everything at the moment the scene swaps. A quad parented to the camera. _Front is the band's height (0..1 up the
// screen), _Bump the overall swell (0..1), set by BubbleWall every frame.
Shader "DjVisuals/Veil"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.8, 1.5, 1.8, 1)
        _Front ("Front", Float) = 0
        _Bump ("Bump", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Front;
                float _Bump;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float y = i.uv.y;
                float band = exp(-pow((y - _Front) / 0.2, 2.0));           // glow riding up with the wall
                float below = smoothstep(_Front + 0.05, _Front - 0.5, y);  // a milky haze trailing underneath it
                float a = saturate(max(_Bump * 0.9, band * 0.4 + below * 0.22));
                float3 c = _Color.rgb * a;
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
