// Mannequin skin.
//  _BloodTex : the part's simulated blood canvas (cylindrical UVs)
//  vertex colour: g = bruising, b = scrapes, a = raw wound flesh around an injury
//  uv2 (TEXCOORD2): tissue exposed where the flesh has been carved in (rgb colour, a amount) - muscle,
//                   and bone / brain / organs where a crater reaches them
//  _HoleTex / _HoleCount : wound openings, cut out of the skin (a 3D wound channel fills each one) with a torn
//                       ring, abrasion collar and bruise drawn around them on top of the blood
Shader "Carl/Skin"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.86, 0.71, 0.58, 1)
        _Smoothness("Smoothness", Range(0,1)) = 0.22
        _BloodTex("Blood", 2D) = "black" {}
        _HoleTex("Holes", 2D) = "black" {}
        _HoleCount("Hole Count", Float) = 0
        _NbrTex("Neighbour Holes", 2D) = "black" {}
        _NbrCount("Neighbour Hole Count", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            // mark intact skin: anatomy (Carl/RevealOrgan) is only drawn where this skin has been cut away
            Stencil { Ref 64 WriteMask 64 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CarlBloodCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                float _HoleCount;
                float _NbrCount;
                float4 _ClipPlane;
            CBUFFER_END
            #define CARL_HOLES
            #include "CarlWounds.hlsl"
            TEXTURE2D_FLOAT(_NbrTex);
            // neighbouring parts' wounds, in world space: row 0 centre+radius, 1 axis+depth, 2 stretch
            float NeighbourWound(float3 pw)
            {
                int n = (int)_NbrCount;
                [loop] for (int k = 0; k < n; k++)
                {
                    float4 h = LOAD_TEXTURE2D(_NbrTex, int2(k, 0));
                    float4 ax = LOAD_TEXTURE2D(_NbrTex, int2(k, 1));
                    float4 sh = LOAD_TEXTURE2D(_NbrTex, int2(k, 2));
                    float3 d = pw - h.xyz;
                    float along = dot(d, ax.xyz);
                    if (h.w > 0.035 || along < -0.004 || along > ax.w) continue;   // just along a small wound
                    // only where this skin is INSIDE the wounded part's body - never its outer surface
                    float3 bc = LOAD_TEXTURE2D(_NbrTex, int2(k, 3)).xyz;
                    float3 bx = LOAD_TEXTURE2D(_NbrTex, int2(k, 4)).xyz;
                    float3 by = LOAD_TEXTURE2D(_NbrTex, int2(k, 5)).xyz;
                    float3 bz = LOAD_TEXTURE2D(_NbrTex, int2(k, 6)).xyz;
                    float3 r = pw - bc;
                    float3 u = float3(dot(r, bx) / max(dot(bx, bx), 1e-8), dot(r, by) / max(dot(by, by), 1e-8), dot(r, bz) / max(dot(bz, bz), 1e-8));
                    u *= u; u *= u;
                    if (u.x + u.y + u.z > 1.0) continue;
                    float3 q = d - ax.xyz * along;
                    float a = length(sh.xyz);
                    if (a > 1.001) { float3 t = sh.xyz / a; q -= t * dot(q, t) * (1.0 - 1.0 / a); }
                    if (length(q) < h.w * 0.9) return 1;
                }
                return 0;
            }
            TEXTURE2D(_BloodTex); SAMPLER(sampler_BloodTex);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; float4 tissue : TEXCOORD2; float3 restOS : TEXCOORD3; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                float4 tissue : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.uv = v.uv;
                o.positionOS = dot(v.restOS, v.restOS) > 1e-8 ? v.restOS : v.positionOS.xyz; // undeformed, so wounds stay pinned
                o.tissue = v.tissue;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                CARL_CLIP(i.positionOS) clip(HoleCut(i.positionOS) - 0.62);     // the opening; the wound channel mesh shows behind it
                if (_NbrCount > 0.5 && NeighbourWound(i.positionWS) > 0.5) discard;
                float3 n = normalize(i.normalWS);
                // sample the shadow a little off the skin along its normal (receiver bias): the soft, rounded
                // limbs otherwise shadow themselves all over and the whole body goes dull and dark
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS + n * 0.02), i.positionWS, half4(1, 1, 1, 1));
                half atten = ml.shadowAttenuation;
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 h = normalize(ml.direction + vdir);
                half ndl = saturate(dot(n, ml.direction));
                half wrap = saturate((dot(n, ml.direction) + 0.35) / 1.35);
                half3 light = SampleSH(n) + ml.color * lerp(ndl, wrap, 0.35) * atten;

                // ---- skin with bruises and scrapes
                half3 albedo = _BaseColor.rgb;
                albedo = lerp(albedo, half3(0.5, 0.2, 0.3), min(saturate(i.color.g), 0.7) * 0.55);
                albedo = lerp(albedo, half3(0.72, 0.33, 0.27), saturate(i.color.b) * 0.6);
                half spec = pow(saturate(dot(n, h)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness * 0.5;
                half3 col = albedo * light + ml.color * spec * atten;

                // ---- blood on the skin
                float4 b = SAMPLE_TEXTURE2D(_BloodTex, sampler_BloodTex, i.uv);
                float cov = BloodCoverageSkin(b.r, b.g);
                if (cov > 0.001)
                {
                    // fresh blood on skin: bright wet red in thin runs, darker where it is thick or drying
                    half3 bs = BloodShade(b.r, b.g, n, i.positionWS, ml);
                    float fresh = (1.0 - smoothstep(0.25, 1.4, b.r)) * (1.0 - saturate(b.g));
                    bs = lerp(bs, bs * 1.2, fresh);
                    bs += ml.color * pow(saturate(dot(n, h)), 70.0) * 0.12 * (1.0 - saturate(b.g)) * atten;
                    col = lerp(col, bs, cov);
                }

                // ---- raw flesh around a wound, and tissue exposed by carving
                float raw = saturate(i.color.a);
                // muscle fibres: stretched low-frequency noise, not speckle
                float3 fp = i.positionOS * float3(55.0, 140.0, 55.0);
                float grain = WoundNoise(fp) * 0.65 + WoundNoise(i.positionOS * 210.0) * 0.35;
                half3 flesh = lerp(half3(0.36, 0.04, 0.05), half3(0.58, 0.11, 0.11), grain);
                half wetSpec = pow(saturate(dot(n, h)), 60.0) * 0.18 * atten;
                // torn, ragged boundary rather than a smooth painted circle
                float edgeN = WoundNoise(i.positionOS * 90.0 + 3.1);
                float rawMask = smoothstep(0.35, 0.55, raw + (edgeN - 0.5) * 0.45);
                col = lerp(col, flesh * light * 0.85 + ml.color * wetSpec, rawMask);
                float exposed = saturate(i.tissue.a);
                if (exposed > 0.01)
                {
                    half3 t = i.tissue.rgb * (0.88 + 0.24 * grain);
                    col = lerp(col, t * light * 0.8 + ml.color * wetSpec, exposed);
                }

                // ---- skin of this part that lies inside a neighbouring part's wound (parts overlap at joints):
                // it is inside the body there, so it shows as meat, not as skin
                // (cut away, so the wounded part's own flesh and organs show there)
                if (NeighbourWound(i.positionWS) > 0.5) discard;

                // ---- a limb torn in two: ragged raw meat along the tear, bloody skin a little further out
                float tearD = CarlTearDist(i.positionOS);
                if (tearD > -0.05)
                {
                    float near = 1.0 - smoothstep(0.0, 0.016 + 0.01 * edgeN, -tearD);
                    col = lerp(col, flesh * light * 0.8 + ml.color * wetSpec, near);
                    col = lerp(col, half3(0.16, 0.01, 0.02) * light, (1.0 - smoothstep(0.0, 0.004, -tearD)) * 0.8);
                    col = lerp(col, BloodShade(1.0, 0.0, n, i.positionWS, ml), (1.0 - smoothstep(0.012, 0.05, -tearD)) * 0.55 * (1.0 - near));
                }

                // ---- just a dark wet lip right at the edge of each opening (no painted ring around it)
                float cutD = HoleCut(i.positionOS);
                float lip = 1.0 - smoothstep(0.62, 0.8, cutD);
                col = lerp(col, half3(0.16, 0.01, 0.02) * light, lip * 0.85);

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // the inside of the skin: wherever an opening shows the far wall instead of a wound channel,
        // you see dark meat rather than straight through the body
        Pass
        {
            Name "Inside"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            // marks "looking into the body through an opening": the only place organs may be drawn
            Stencil { Ref 128 WriteMask 128 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                float _HoleCount;
                float _NbrCount;
                float4 _ClipPlane;
            CBUFFER_END
            #define CARL_HOLES
            #include "CarlWounds.hlsl"
            struct A { float4 positionOS : POSITION; float3 restOS : TEXCOORD3; };
            struct V { float4 positionCS : SV_POSITION; float3 posOS : TEXCOORD0; half fog : TEXCOORD1; };
            V vert(A v)
            {
                V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.posOS = dot(v.restOS, v.restOS) > 1e-8 ? v.restOS : v.positionOS.xyz;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                if (_HoleCount < 0.5) discard;
                CARL_CLIP(i.posOS) clip(HoleCut(i.posOS) - 0.62);
                // the inside of the body: wet torn muscle, darker the further from the opening
                float g = WoundNoise(i.posOS * float3(60.0, 150.0, 60.0)) * 0.6 + WoundNoise(i.posOS * 260.0) * 0.4;
                half3 c = lerp(half3(0.2, 0.02, 0.03), half3(0.42, 0.06, 0.07), g);
                float open = HoleCut(i.posOS);                 // 0.62 at the opening's edge
                c *= lerp(1.0, 0.45, saturate((open - 0.62) / 2.5));
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }

        // marks every pixel where this part's inside is behind an opening, even if another part's skin is in
        // between - so the flesh layers and organs are drawn there
        Pass
        {
            Name "InsideMark"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Front
            ZTest LEqual
            ZWrite Off
            ColorMask 0
            Stencil { Ref 128 WriteMask 128 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                float _HoleCount;
                float _NbrCount;
                float4 _ClipPlane;
            CBUFFER_END
            #define CARL_HOLES
            #include "CarlWounds.hlsl"
            struct A { float4 positionOS : POSITION; float3 restOS : TEXCOORD3; };
            struct V { float4 positionCS : SV_POSITION; float3 posOS : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.posOS = dot(v.restOS, v.restOS) > 1e-8 ? v.restOS : v.positionOS.xyz; return o; }
            half4 frag(V i) : SV_Target { if (_HoleCount < 0.5) discard; CARL_CLIP(i.posOS) clip(HoleCut(i.posOS) - 0.62); return 0; }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"

        // depth passes cut the same openings, or a depth prepass would hide the wound channels
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Stencil { Ref 64 WriteMask 64 Comp Always Pass Replace }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                float _HoleCount;
                float _NbrCount;
                float4 _ClipPlane;
            CBUFFER_END
            #define CARL_HOLES
            #include "CarlWounds.hlsl"
            struct A { float4 positionOS : POSITION; float3 restOS : TEXCOORD3; };
            struct V { float4 positionCS : SV_POSITION; float3 posOS : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.posOS = dot(v.restOS, v.restOS) > 1e-8 ? v.restOS : v.positionOS.xyz; return o; }
            half frag(V i) : SV_Target { CARL_CLIP(i.posOS) clip(HoleCut(i.posOS) - 0.62); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Stencil { Ref 64 WriteMask 64 Comp Always Pass Replace }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                float _HoleCount;
                float _NbrCount;
                float4 _ClipPlane;
            CBUFFER_END
            #define CARL_HOLES
            #include "CarlWounds.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float3 restOS : TEXCOORD3; };
            struct V { float4 positionCS : SV_POSITION; float3 posOS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.posOS = dot(v.restOS, v.restOS) > 1e-8 ? v.restOS : v.positionOS.xyz; o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(V i) : SV_Target { CARL_CLIP(i.posOS) clip(HoleCut(i.posOS) - 0.62); return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
