#ifndef CARL_BLOOD_COMMON
#define CARL_BLOOD_COMMON

// Blood is one colour. Where there is less of it you see more of the surface through it
// (Beer-Lambert style absorption), so thin trails look lighter without the blood changing colour.
float BloodCoverage(float T)
{
    return saturate(1.0 - exp(-max(T, 0.0) * 13.0));
}

// On skin blood forms crisp-edged runs and rivulets, not soft stains
float BloodCoverageSkin(float T, float dry)
{
    // crisp edges, but thin smears stay see-through - only real runs are solid
    return smoothstep(0.06, 0.16, T) * saturate(0.35 + 0.65 * BloodCoverage(T));
}

// Real blood: dark crimson, nearly black-red where it pools thick, only a faint wet sheen.
// Drying only darkens it slightly.
float3 BloodShade(float T, float dry, float3 n, float3 positionWS, Light ml, float rim = 0)
{
    float3 crimson = float3(0.3, 0.008, 0.02);
    // thick runs only get a little darker, so drips, pools and spatter all read as the same blood
    float3 deep = float3(0.25, 0.004, 0.016);
    float3 darkest = float3(0.21, 0.002, 0.013);
    float3 albedo = lerp(crimson, deep, smoothstep(0.25, 0.9, T));
    albedo = lerp(albedo, darkest, smoothstep(0.9, 2.5, T));
    albedo *= 1.0 - saturate(rim) * 0.2;
    albedo *= lerp(1.0, 0.82, saturate(dry));

    float3 v = normalize(GetWorldSpaceViewDir(positionWS));
    float3 h = normalize(ml.direction + v);
    float ndl = saturate(dot(n, ml.direction));
    float wet = (1.0 - saturate(dry) * 0.8) * smoothstep(0.3, 0.9, T);
    float spec = pow(saturate(dot(n, h)), 90.0) * wet * 0.06;
    float atten = ml.shadowAttenuation;
    return albedo * (SampleSH(n) * 0.62 + ml.color * ndl * atten * 0.45) + ml.color * spec * atten;
}

#endif
