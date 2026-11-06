#ifndef DJ_FOG_INCLUDED
#define DJ_FOG_INCLUDED

// Manual exponential-squared fog for the custom shaders. The underwater world sets these globals; every other world
// leaves _DjFogDensity at 0, so this is a no-op there.
float4 _DjFogColor;
float _DjFogDensity;

float3 dj_fog(float3 col, float3 posWS)
{
    float d = distance(_WorldSpaceCameraPos, posWS);
    float f = 1.0 - exp(-pow(d * _DjFogDensity, 2.0));
    return lerp(col, _DjFogColor.rgb, saturate(f));
}

float dj_fogFactor(float3 posWS)
{
    float d = distance(_WorldSpaceCameraPos, posWS);
    return saturate(1.0 - exp(-pow(d * _DjFogDensity, 2.0)));
}

#endif
