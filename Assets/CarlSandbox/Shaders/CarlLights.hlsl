// The other lights on a body (torches, flashlights, lamps, muzzle flashes, fire): the bodies' own shaders only
// did the sun/moon and the sky, so in a torch beam at night an Earl stayed a black shape while the ground round
// him lit up. Diffuse only (wrapped a little for soft skin), each light's own falloff and shadow.
// Needs, in the pass:
//   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
//   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#ifndef CARL_LIGHTS_INCLUDED
#define CARL_LIGHTS_INCLUDED

half3 CarlOtherLights(float3 positionWS, half3 n, float4 positionCS, half wrapK)
{
    half3 sum = 0;
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light l = GetAdditionalLight(lightIndex, positionWS);
        half d = dot(n, l.direction);
        half k = lerp(saturate(d), saturate((d + 0.35) / 1.35), wrapK);
        sum += l.color * (k * l.distanceAttenuation * l.shadowAttenuation);
    LIGHT_LOOP_END
    // (a torch a metre from a face: bright, but rolled off rather than burnt out white)
    sum = sum / (1.0 + sum * 0.5);
#endif
    return sum;
}

#endif
