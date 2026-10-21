#ifndef CARL_WOUNDS
#define CARL_WOUNDS

// Open wounds: up to 40 per body part in object space (xyz centre on the skin, w radius) with an inward
// axis each (_HoleAxes xyz, w channel depth). The skin is cut away inside a ragged opening (HoleCut) and a
// 3D wound channel mesh (Carl/WoundChannel) fills the hole behind it. Around the opening the skin shows a
// ring of torn tissue, a scraped abrasion collar and faint bruising.
// The hole list lives in a small per-part texture (_HoleTex: row 0 centre+radius, row 1 axis+depth) because
// textures are always bound per material; per-material arrays got mixed up between body parts.
// Shaders that use it define CARL_HOLES and declare float _HoleCount in their UnityPerMaterial buffer.
#ifdef CARL_HOLES
TEXTURE2D_FLOAT(_HoleTex);
#endif

float WoundHash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
float WoundNoise(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(WoundHash(i), WoundHash(i + float3(1, 0, 0)), f.x), lerp(WoundHash(i + float3(0, 1, 0)), WoundHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(WoundHash(i + float3(0, 0, 1)), WoundHash(i + float3(1, 0, 1)), f.x), lerp(WoundHash(i + float3(0, 1, 1)), WoundHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

float WoundRagged(float3 q, float seed) { return 0.75 + 0.5 * WoundNoise(q * 2.2 + seed * 3.7) + 0.2 * WoundNoise(q * 7.0 + seed); }

// A limb shot in two: each half is drawn only on its own side of the tear (_ClipPlane: normal xyz, offset w, in
// the space of the point passed; all zero = not torn). The tear wanders a few centimetres either side of the
// plane in ragged tongues and notches, like the bullet holes that caused it - never a clean slice.
// Returns > 0 where this half is torn away, 0 at the torn edge, < 0 on intact skin (metres).
float CarlTearDist(float3 p)
{
    if (dot(_ClipPlane.xyz, _ClipPlane.xyz) < 0.5) return -1e3;
    float jag = (WoundNoise(p * 38.0 + 11.0) - 0.5) * 0.05 + (WoundNoise(p * 110.0 + 3.0) - 0.5) * 0.018 + (WoundNoise(p * 300.0) - 0.5) * 0.005;
    return dot(_ClipPlane.xyz, p) + _ClipPlane.w + jag;
}
#define CARL_CLIP(p) if (CarlTearDist(p) > 0.0) discard;

#ifdef CARL_HOLES
// Offset from hole k in units of its radius, squashed along its stretch direction (row 2 xyz, length = aspect)
// so slanted hits and cuts are oval; seed (row 2 w) makes each wound's torn edge different.
float3 HoleQ(float3 posOS, int k, float4 h, out float seed)
{
    float4 sh = LOAD_TEXTURE2D(_HoleTex, int2(k, 2));
    seed = sh.w;
    float3 q = (posOS - h.xyz) / max(h.w, 0.003);
    float a = length(sh.xyz);
    if (a > 1.001) { float3 t = sh.xyz / a; q -= t * dot(q, t) * (1.0 - 1.0 / a); }
    return q;
}

// Torn edge: ragged for holes and blasts, a clean slightly wavering line for long knife slits.
float HoleEdge(float3 q, int k, float seed)
{
    float a = length(LOAD_TEXTURE2D(_HoleTex, int2(k, 2)).xyz);
    float slit = saturate((a - 1.8) / 2.0);
    return lerp(WoundRagged(q, seed), 0.92 + 0.12 * WoundNoise(q * 1.3 + seed), slit);
}

// Skin on the far side of a limb is not part of this wound.
bool WoundNearSide(float3 posOS, int k)
{
    float3 ax = LOAD_TEXTURE2D(_HoleTex, int2(k, 1)).xyz;
    if (dot(ax, ax) < 0.01) return true;
    float4 h = LOAD_TEXTURE2D(_HoleTex, int2(k, 0));
    return dot(posOS - h.xyz, ax) < h.w * 0.8;
}

// Smallest normalised distance to an opening (below ~0.62 the skin is gone).
// Overlapping holes merge into one opening (a smooth minimum): a hard minimum left thin slivers and
// points of skin between neighbouring holes that showed up as floating triangles.
float HoleCut(float3 posOS)
{
    const float K = 7.0;
    float s = 0, hard = 10;
    int n = (int)_HoleCount;
    [loop] for (int k = 0; k < n; k++)
    {
        float4 h = LOAD_TEXTURE2D(_HoleTex, int2(k, 0));
        float seed;
        float3 q = HoleQ(posOS, k, h, seed);
        if (dot(q, q) > 4.0 || !WoundNearSide(posOS, k)) continue;
        float d = length(q) / HoleEdge(q, k, seed);
        s += exp(-K * d);
        hard = min(hard, d);
    }
    // only ever bridge thin slivers: many holes on one spot must not add up into a much bigger opening
    return s < 1e-6 ? 10.0 : max(-log(s) / K, hard - 0.12);
}

// Returns how strongly this point is covered by a wound (0..1), its colour and wetness.
float WoundSurface(float3 posOS, out float3 col, out float wet)
{
    col = 0; wet = 0;
    float best = 0;
    int n = (int)_HoleCount;
    [loop] for (int k = 0; k < n; k++)
    {
        float4 h = LOAD_TEXTURE2D(_HoleTex, int2(k, 0));
        float seed;
        float3 q = HoleQ(posOS, k, h, seed);
        if (dot(q, q) > 16.0 || !WoundNearSide(posOS, k)) continue;
        float dn = length(q) / HoleEdge(q, k, seed);    // 1 = edge of the open wound
        if (dn > 2.8) continue;

        float grain = WoundNoise(q * 9.0 + seed * 1.3);
        float3 core = lerp(float3(0.05, 0.0, 0.004), float3(0.14, 0.01, 0.012), grain * 0.6);          // deep, clotted
        float3 tissue = lerp(float3(0.38, 0.03, 0.04), float3(0.62, 0.12, 0.12), grain);                  // torn muscle
        tissue = lerp(tissue, float3(0.2, 0.0, 0.01), smoothstep(0.55, 0.8, WoundNoise(q * 14.0 + 5.0 + seed))); // clotted specks
        float3 collar = float3(0.58, 0.22, 0.2);                                                          // abraded skin
        float3 bruise = float3(0.5, 0.25, 0.3);

        float3 c; float w; float wt;
        if (dn < 0.45)      { c = core; w = 1; wt = 1; }
        else if (dn < 1.0)  { c = lerp(core, tissue, smoothstep(0.45, 0.65, dn)); w = 1; wt = 0.8; }
        else if (dn < 1.45) { c = collar; w = 1.0 - smoothstep(1.2, 1.45, dn) * 0.4; wt = 0.2; }
        else                { c = bruise; w = (1.0 - smoothstep(1.45, 2.8, dn)) * 0.35; wt = 0; }

        if (w > best) { best = w; col = c; wet = wt; }
    }
    return best;
}

#endif

#endif
