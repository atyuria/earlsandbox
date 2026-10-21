// Armour: digital camo cloth (EMR style, object-space so it needs no UVs), olive/painted steel, polished plate
// steel or chainmail. Vertex colour darkens where it has been shot (scorches, holes, scuffs).
// Damage (_DmgTex, one texel per hit: object-space centre + radius): w > 0 is a hole torn right through (discarded,
// frayed / scorched rim), w < 0 a round that was stopped: the cloth is ripped open showing the plate under it,
// or on bare steel the paint is knocked off down to bright metal.
Shader "Carl/Armour"
{
    Properties
    {
        _Color("Color", Color) = (0.36, 0.4, 0.27, 1)
        _Metal("Metal", Range(0,1)) = 0
        _Gloss("Gloss", Range(0,1)) = 0.2
        _Pattern("Pattern (0 plain, 1 camo, 2 mail, 3 webbing)", Float) = 0
        _DmgTex("Damage", 2D) = "black" {}
        _DmgCount("Damage Count", Float) = 0
        _PlateUnder("Plate under the cloth", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Metal, _Gloss;
            float _Pattern;
            float _DmgCount;
            float _PlateUnder;
        CBUFFER_END
        TEXTURE2D_FLOAT(_DmgTex);

        float AH(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
        float ANoise(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(AH(i), AH(i + float3(1, 0, 0)), f.x), lerp(AH(i + float3(0, 1, 0)), AH(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(AH(i + float3(0, 0, 1)), AH(i + float3(1, 0, 1)), f.x), lerp(AH(i + float3(0, 1, 1)), AH(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        // hole: 1 = torn away here; rip: 0..1 opened cloth / chipped paint; fray: 0..1 ring of damage around either
        void ArmourDamage(float3 P, out float hole, out float rip, out float fray)
        {
            hole = 0; rip = 0; fray = 0;
            int n = (int)_DmgCount;
            [loop] for (int k = 0; k < n; k++)
            {
                float4 d = LOAD_TEXTURE2D(_DmgTex, int2(k, 0));
                float r = abs(d.w);
                float dist = length(P - d.xyz);
                if (dist > r * 3.0) continue;
                // ragged, torn outline: lobes plus fine threads
                float3 q = (P - d.xyz) / max(r, 1e-4);
                float edge = r * (0.78 + 0.32 * ANoise(q * 2.5 + k * 7.3) + 0.12 * ANoise(P * 900.0));
                if (d.w > 0) { if (dist < edge) hole = 1; }
                else
                {
                    // torn cloth: a jagged star of tears, not a round patch
                    float tear = r * (0.35 + 0.95 * pow(ANoise(q * 4.0 + k * 3.1), 2.0) + 0.25 * ANoise(P * 1400.0));
                    rip = max(rip, 1.0 - smoothstep(tear * 0.85, tear, dist));
                    edge = tear;
                }
                fray = max(fray, 1.0 - saturate((dist - edge) / (r * 0.45)));
            }
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionOS : TEXCOORD2; half4 color : COLOR; half fog : TEXCOORD3; };

            float H(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }

            V vert(A v)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (front ? 1 : -1);
                half3 albedo = _Color.rgb;
                float3 P = i.positionOS;
                float hole, rip, fray;
                ArmourDamage(P, hole, rip, fray);
                if (hole > 0.5) discard;
                half metal = _Metal, gloss = _Gloss;
                if (_Pattern > 0.5 && _Pattern < 1.5)
                {
                    // EMR digital camo: blocky pixels in 4 shades (light sand, light green, dark green, brown)
                    float3 c1 = floor(P * 55.0), c2 = floor(P * 22.0);
                    float a = H(c1), b = H(c2 + 7.0);
                    half3 sand = half3(0.55, 0.56, 0.4), lg = half3(0.38, 0.45, 0.27), dg = half3(0.2, 0.27, 0.16), br = half3(0.3, 0.26, 0.17);
                    albedo = lg;
                    if (b > 0.62) albedo = dg;
                    if (a > 0.78) albedo = sand;
                    else if (a < 0.18) albedo = br;
                    else if (a < 0.4 && b > 0.4) albedo = dg;
                }
                else if (_Pattern > 1.5 && _Pattern < 2.5)
                {
                    // chainmail: rows of little rings
                    float2 g = float2(P.x + P.z, P.y) * 260.0;
                    g.x += floor(g.y) * 0.5;
                    float d = length(frac(g) - 0.5);
                    albedo *= lerp(0.35, 1.15, smoothstep(0.2, 0.32, d) * (1.0 - smoothstep(0.42, 0.5, d)));
                }
                else if (_Pattern > 2.5 && _Pattern < 3.5)
                {
                    // nylon webbing weave
                    albedo *= 0.85 + 0.15 * sin(P.y * 900.0) * sin(P.x * 900.0 + P.z * 900.0);
                }
                else if (_Pattern > 3.5)
                {
                    // baggy cloth helmet cover: soft folds, stitched seams, faded patches
                    float folds = ANoise(P * 38.0) * 0.6 + ANoise(P * 90.0 + 3.0) * 0.4;
                    float3 e = float3(0.002, 0, 0);
                    float3 g = float3(ANoise((P + e.xyy) * 38.0) - ANoise((P - e.xyy) * 38.0),
                                      ANoise((P + e.yxy) * 38.0) - ANoise((P - e.yxy) * 38.0),
                                      ANoise((P + e.yyx) * 38.0) - ANoise((P - e.yyx) * 38.0));
                    n = normalize(n - TransformObjectToWorldDir(g) * 1.6);
                    albedo *= 0.9 + 0.14 * folds;
                    float seam = 1.0 - smoothstep(0.0, 0.0018, abs(P.x)) * 1.0;
                    albedo *= 1.0 - seam * 0.35;
                    albedo = lerp(albedo, albedo * half3(1.1, 1.08, 0.95), smoothstep(0.55, 0.75, ANoise(P * 12.0 + 9.0)) * 0.5);
                    albedo *= 0.92 + 0.08 * sin(P.y * 1500.0 + P.x * 700.0);   // weave
                }
                bool cloth = _Metal < 0.5 && _Pattern != 2.0;
                if (cloth)
                {
                    // torn nylon: pale frayed threads around the tear, the plate inside where it is ripped open
                    half threads = 0.6 + 0.4 * sin(P.y * 2400.0 + ANoise(P * 300.0) * 6.0);
                    albedo = lerp(albedo, half3(0.55, 0.53, 0.42) * threads, fray * fray * 0.7);
                    half3 inside = _PlateUnder > 0.5 ? half3(0.36, 0.36, 0.35) * (0.8 + 0.3 * ANoise(P * 500.0)) : half3(0.12, 0.12, 0.09);
                    albedo = lerp(albedo, inside, rip);
                    gloss = lerp(gloss, _PlateUnder > 0.5 ? 0.45 : 0.1, rip);
                    metal = lerp(metal, _PlateUnder > 0.5 ? 0.4 : 0.0, rip);
                }
                else
                {
                    // painted / polished steel: paint knocked off to bright scraped metal, scorched ring around holes
                    albedo = lerp(albedo, half3(0.68, 0.68, 0.7) * (0.85 + 0.25 * ANoise(P * 700.0)), rip);
                    metal = lerp(metal, 1.0, rip); gloss = lerp(gloss, 0.6, rip);
                    albedo *= 1.0 - fray * 0.55 * (1.0 - rip);
                }
                albedo *= i.color.rgb;

                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half atten = ml.shadowAttenuation;
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 h = normalize(ml.direction + vdir);
                half ndl = saturate(dot(n, ml.direction));
                half3 diffuse = albedo * (SampleSH(n) + ml.color * ndl * atten) * (1.0 - metal * 0.7);
                half specPow = exp2(10.0 * gloss + 1.0);
                half3 specCol = lerp(half3(0.04, 0.04, 0.04), albedo, metal);
                half3 spec = specCol * ml.color * pow(saturate(dot(n, h)), specPow) * gloss * 3.0 * atten;
                // cheap reflection of the sky / floor for metal
                half3 r = reflect(-vdir, n);
                half3 env = lerp(half3(0.25, 0.25, 0.27), half3(0.75, 0.78, 0.82), saturate(r.y * 0.5 + 0.5));
                half fres = pow(1.0 - saturate(dot(n, vdir)), 4.0);
                half3 col = diffuse + spec + env * specCol * metal * gloss + env * fres * 0.06 * (0.3 + gloss);
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"

        // depth with the holes cut out (a plain depth pass would hide what is behind a hole)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
            V vert(float4 positionOS : POSITION) { V o; o.positionCS = TransformObjectToHClip(positionOS.xyz); o.positionOS = positionOS.xyz; return o; }
            half frag(V i) : SV_Target { float hole, rip, fray; ArmourDamage(i.positionOS, hole, rip, fray); if (hole > 0.5) discard; return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            V vert(float4 positionOS : POSITION, float3 normalOS : NORMAL) { V o; o.positionCS = TransformObjectToHClip(positionOS.xyz); o.positionOS = positionOS.xyz; o.normalWS = TransformObjectToWorldNormal(normalOS); return o; }
            half4 frag(V i) : SV_Target { float hole, rip, fray; ArmourDamage(i.positionOS, hole, rip, fray); if (hole > 0.5) discard; return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
