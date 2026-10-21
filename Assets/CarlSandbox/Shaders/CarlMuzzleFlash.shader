// Muzzle flash, drawn procedurally on quads (additive): _Shape 0 = the star seen down the barrel, 1 = a flame petal
// along the barrel (uv.y from the muzzle to the tip). _Seed changes every shot so no two flashes look the same.
Shader "Carl/MuzzleFlash"
{
    Properties
    {
        _Shape("Shape", Float) = 0
        _Seed("Seed", Float) = 0
        _Intensity("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            Blend One One
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Shape, _Seed, _Intensity;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }

            float h1(float x) { return frac(sin(x * 127.1 + _Seed * 311.7) * 43758.5453); }
            float n1(float x) { float i = floor(x), f = frac(x); f = f * f * (3 - 2 * f); return lerp(h1(i), h1(i + 1), f); }

            half3 Ramp(float I)
            {
                // deep orange edges, yellow body, white-hot core
                half3 c = lerp(half3(0.9, 0.25, 0.04), half3(1.0, 0.7, 0.25), saturate(I * 1.6));
                return lerp(c, half3(1.0, 0.97, 0.85), saturate(I * 1.6 - 0.8));
            }

            half4 frag(V i) : SV_Target
            {
                float I;
                if (_Shape < 0.5)
                {
                    float2 p = (i.uv - 0.5) * 2.0;
                    float r = length(p);
                    float a = atan2(p.y, p.x);
                    float k = 5.0 + floor(h1(3.0) * 4.0);
                    float spikes = pow(abs(cos(a * k * 0.5 + _Seed * 6.0)), 6.0) * (0.6 + 0.4 * n1(a * 3.0 + 10.0));
                    I = exp(-r * 7.0) * 1.4 + spikes * saturate(1.0 - r / (0.55 + 0.4 * h1(5.0)));
                }
                else
                {
                    float u = (i.uv.x - 0.5) * 2.0, v = i.uv.y;
                    // flame widens past the muzzle into a ragged diamond and tapers to a flickering tip
                    float w = (0.25 + 0.7 * sin(saturate(v * 1.25) * 3.14159)) * (0.75 + 0.5 * n1(v * 9.0 + 2.0));
                    float body = exp(-pow(u / max(w, 0.05), 2.0) * 2.5);
                    float len = 0.55 + 0.45 * h1(7.0);
                    I = body * saturate((len - v) / len) * (0.7 + 0.6 * n1(v * 14.0 + u * 3.0));
                    I *= saturate(v * 12.0);
                }
                I *= _Intensity;
                return half4(Ramp(I) * I, 0);
            }
            ENDHLSL
        }
    }
}
