// Digital camo the way the real patterns are built: small square pixels (here 5 mm), grouped into organic
// blotches of each colour layered over a ground colour - not a random colour per square.
//   kind 1: EMR "tsifra" (Russia): green-khaki ground, mid green and dark green blotches, near-black splinters
//   kind 2: MM-14 (Ukraine): khaki ground, olive and dark olive-green blotches, brown accents
// Colours are the cloth's, converted to linear.
#ifndef CARL_CAMO_INCLUDED
#define CARL_CAMO_INCLUDED

float CamoHash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float CamoNoise(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(CamoHash3(i), CamoHash3(i + float3(1, 0, 0)), f.x), lerp(CamoHash3(i + float3(0, 1, 0)), CamoHash3(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(CamoHash3(i + float3(0, 0, 1)), CamoHash3(i + float3(1, 0, 1)), f.x), lerp(CamoHash3(i + float3(0, 1, 1)), CamoHash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

half3 DigitalCamo(float3 P, float kind)
{
    const float px = 0.005;
    float3 q = (floor(P / px) + 0.5) * px;                 // every pixel takes one colour
    float j = (CamoHash3(floor(P / px) + 11.0) - 0.5) * 0.09;   // ragged, stepped edges on the blotches
    // the blotches: a large shape and some breakup in each layer (MM-14 runs a little taller, EMR a little wider)
    float3 s = kind > 1.5 ? float3(1.0, 0.7, 1.0) : float3(0.8, 1.15, 0.8);
    float3 Q = q * s;
    float a = CamoNoise(Q * 12.0 + 3.1) * 0.68 + CamoNoise(Q * 30.0 + 7.7) * 0.32 + j;
    float b = CamoNoise(Q * 14.0 + 19.3) * 0.66 + CamoNoise(Q * 34.0 + 2.9) * 0.34 + j;
    float c = CamoNoise(Q * 18.0 + 41.0) * 0.6 + CamoNoise(Q * 44.0 + 13.0) * 0.4 + j;
    half3 col;
    if (kind > 1.5)
    {
        // MM-14
        col = half3(0.33, 0.31, 0.2);                         // greyish khaki ground
        if (a > 0.47) col = half3(0.17, 0.175, 0.085);        // olive
        if (b > 0.55) col = half3(0.07, 0.08, 0.038);         // dark olive-green
        if (c > 0.64) col = half3(0.14, 0.09, 0.048);         // brown
    }
    else
    {
        // EMR
        col = half3(0.23, 0.25, 0.13);                        // green-khaki ground
        if (a > 0.44) col = half3(0.1, 0.145, 0.055);         // mid green
        if (b > 0.54) col = half3(0.042, 0.062, 0.026);       // dark green
        if (c > 0.66) col = half3(0.024, 0.021, 0.015);       // near-black
    }
    return col;
}

#endif
