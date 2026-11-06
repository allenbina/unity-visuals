#ifndef DJ_NOISE_INCLUDED
#define DJ_NOISE_INCLUDED

// Cheap value noise + fbm, shared by the procedural space shaders.
float dj_hash(float3 p)
{
    p = 50.0 * frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float dj_noise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(dj_hash(i + float3(0, 0, 0)), dj_hash(i + float3(1, 0, 0)), f.x),
             lerp(dj_hash(i + float3(0, 1, 0)), dj_hash(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(dj_hash(i + float3(0, 0, 1)), dj_hash(i + float3(1, 0, 1)), f.x),
             lerp(dj_hash(i + float3(0, 1, 1)), dj_hash(i + float3(1, 1, 1)), f.x), f.y),
        f.z);
}

float dj_fbm(float3 p)
{
    float a = 0.5;
    float s = 0.0;
    for (int k = 0; k < 5; k++)
    {
        s += a * dj_noise(p);
        p = p * 2.03 + 17.1;
        a *= 0.5;
    }
    return s;
}

#endif
