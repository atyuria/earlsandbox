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
        _BurnTex("Burns", 2D) = "black" {}
        _BurnCount("Burn Count", Float) = 0
        _Char("Char", Range(0,1)) = 0
        _Heat("Heat", Range(0,1)) = 0
        _Uniform("Uniform (0 skin, 1 EMR, 2 MM-14, 3 boots)", Float) = 0
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CarlLights.hlsl"
            #include "CarlBloodCommon.hlsl"

            // ---- the uniform painted on the body (the mannequins wear no separate clothes): the army's digital camo
            // in the body part's own space, so it moves with the limb; boots on the feet
            #include "CarlCamo.hlsl"
            half3 Uniform(float3 P, float u)
            {
                if (u > 2.5) return half3(0.035, 0.03, 0.025);           // boots
                return DigitalCamo(P, u);
            }
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                float _HoleCount;
                float _NbrCount;
                float4 _ClipPlane;
                float _BurnCount;
                float _Char;
                float _Heat;
                float4 _BurnStyle;
                float4 _MeltCenter;
                float _Uniform;
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
                    // anywhere along the wound, big or small: a blast crater left this part's skin standing inside
                    // the next part's open belly as a solid ring
                    if (along < -0.004 || along > ax.w) continue;
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

            // ---- burns: spots of fire damage in the part's own space (row 0 centre + radius, row 1 char, blister,
            // seed), and _Char: how far the whole part has burned while it was on fire
            TEXTURE2D_FLOAT(_BurnTex);
            void Burns(float3 p, out float charA, out float blister)
            {
                charA = 0; blister = 0;
                if (_Char > 0.001)
                {
                    // a part on fire doesn't char evenly: blotches spread and join up as it goes on
                    float g = WoundNoise(p * 22.0) * 0.6 + WoundNoise(p * 70.0 + 5.3) * 0.4;
                    // (finer, softer blotches than before: it spreads as a crust, not camouflage)
                    g = WoundNoise(p * 40.0) * 0.55 + WoundNoise(p * 120.0 + 5.3) * 0.45;
                    charA = smoothstep(0.1, 0.5, _Char * 1.7 + (g - 0.5) * 0.45);
                    blister = smoothstep(0.0, 0.2, _Char + (g - 0.5) * 0.2) * (1.0 - charA);
                }
                int n = (int)_BurnCount;
                [loop] for (int k = 0; k < n; k++)
                {
                    float4 c = LOAD_TEXTURE2D(_BurnTex, int2(k, 0));
                    float4 a = LOAD_TEXTURE2D(_BurnTex, int2(k, 1));
                    float d = length(p - c.xyz) / max(c.w, 1e-4);
                    if (d > 1.6) continue;
                    float ragged = d + (WoundNoise(p * 55.0 + a.z) - 0.5) * 0.55;
                    float core = 1.0 - smoothstep(0.3, 0.7, ragged);
                    float ring = 1.0 - smoothstep(0.7, 1.15, ragged);
                    charA = max(charA, core * a.x);
                    blister = max(blister, ring * a.y * (1.0 - core * a.x));
                }
            }

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
                // badly burned skin shrinks and buckles: pulled in a little, lumpy where it has cooked hardest
                if (_BurnCount > 0.5 || _Char > 0.001)
                {
                    float3 rest = dot(v.restOS, v.restOS) > 1e-8 ? v.restOS : v.positionOS.xyz;
                    float ch, bl; Burns(rest, ch, bl);
                    float lump = WoundNoise(rest * 40.0);
                    // (a part burned right through shrinks to a thin, knotted limb)
                    // (ashen remains: dried right down, thinner still)
                    v.positionOS.xyz -= v.normalOS * (ch * (0.006 + 0.012 * lump) + _Char * _Char * (0.012 + 0.012 * _BurnStyle.x) + bl * 0.0015 * lump);
                }
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                // melted remains: the body sags down into the floor and spreads out over it where it touches
                if (_BurnStyle.z > 0.001)
                {
                    float m = _BurnStyle.z, d = max(pw.y - _BurnStyle.w, 0.0);
                    pw.y = _BurnStyle.w + d * (1.0 - 0.72 * m) + 0.004;
                    pw.xz += (pw.xz - _MeltCenter.xz) * 0.3 * m * saturate(1.0 - d * 2.5);
                }
                float4 pcs = TransformWorldToHClip(pw);
                o.positionCS = pcs;
                o.positionWS = pw;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(pcs.z);
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
                half3 light = SampleSH(n) + ml.color * lerp(ndl, wrap, 0.35) * atten + CarlOtherLights(i.positionWS, n, i.positionCS, 0.35);

                // ---- skin with bruises and scrapes
                half3 albedo = _BaseColor.rgb;
                albedo = lerp(albedo, half3(0.5, 0.2, 0.3), min(saturate(i.color.g), 0.7) * 0.55);
                albedo = lerp(albedo, half3(0.72, 0.33, 0.27), saturate(i.color.b) * 0.6);
                half clothK = 1.0;
                if (_Uniform > 0.5)
                {
                    // cloth (or boot leather): the camo, bruise and scrape marks don't show through it, and matte
                    // (and the cloth itself: faint weave, soft folds where it bunches)
                    albedo = Uniform(i.positionOS, _Uniform) * (0.9 + 0.12 * WoundNoise(i.positionOS * 900.0)) * (0.88 + 0.2 * WoundNoise(i.positionOS * 45.0));
                    clothK = 0.12;
                }
                half spec = pow(saturate(dot(n, h)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness * 0.5 * clothK;
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

                // ---- burns: sooted, then blistered raw skin, then charred black and cracked (glowing in the cracks
                // while it's still burning)
                if (_BurnCount > 0.5 || _Char > 0.001)
                {
                    float ch, bl; Burns(i.positionOS, ch, bl);
                    // scalded: heat-darkened, deep red-brown raw skin with a little wet sheen, sooted over in places
                    // (no pink, no pale speckle)
                    half3 rawBurn = lerp(half3(0.3, 0.08, 0.055), half3(0.46, 0.14, 0.09), WoundNoise(i.positionOS * 90.0));
                    rawBurn = lerp(rawBurn, rawBurn * 0.45, smoothstep(0.55, 0.8, WoundNoise(i.positionOS * 35.0 + 9.2)) * 0.7);
                    half blSpec = pow(saturate(dot(n, h)), 40.0) * 0.12 * atten;
                    col = lerp(col, rawBurn * light + ml.color * blSpec, smoothstep(0.1, 0.55, bl) * 0.92);
                    // soot round the edge of the char, then the char itself: a dry, crusty black-brown crust, rough
                    // and ashy, with patches of dark raw red showing through where it has split and come away
                    col = lerp(col, col * 0.35, smoothstep(0.05, 0.35, ch) * 0.7);
                    float crust = WoundNoise(i.positionOS * 75.0) * 0.6 + WoundNoise(i.positionOS * 220.0 + 2.3) * 0.4;
                    half3 charC = lerp(half3(0.035, 0.028, 0.022), half3(0.12, 0.085, 0.06), crust);
                    // the ash on the high points of the crust
                    charC = lerp(charC, half3(0.22, 0.2, 0.18), smoothstep(0.72, 0.9, WoundNoise(i.positionOS * 160.0 + 7.1)) * 0.6);
                    // (ashen remains: grey-white ash over much more of it)
                    charC = lerp(charC, half3(0.17, 0.16, 0.15) * (0.8 + 0.4 * crust), _BurnStyle.x * smoothstep(0.4, 0.65, crust) * 0.7);
                    // raw patches: blotchy, matte, deep red-brown - not glowing
                    float patch = smoothstep(0.58 - 0.14 * _BurnStyle.y, 0.7 - 0.14 * _BurnStyle.y, WoundNoise(i.positionOS * 18.0 + 4.4) * 0.7 + WoundNoise(i.positionOS * 60.0) * 0.3);
                    half3 raw2 = lerp(half3(0.24, 0.045, 0.03), half3(0.42, 0.1, 0.06), WoundNoise(i.positionOS * 110.0));
                    charC = lerp(charC, raw2, patch * 0.85 * (1.0 - 0.6 * _BurnStyle.x));
                    half3 charLit = charC * light + ml.color * pow(saturate(dot(n, h)), 12.0) * 0.015 * atten;
                    col = lerp(col, charLit, smoothstep(0.35, 0.75, ch));
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
                float _BurnCount;
                float _Char;
                float _Heat;
                float4 _BurnStyle;
                float4 _MeltCenter;
                float _Uniform;
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
                // the far wall is always solid from inside, even where it has a hole of its own: otherwise a
                // big wound or two overlapping ones let you look straight through the body
                CARL_CLIP(i.posOS)
                // the inside of the body: wet torn muscle, darker the further from the opening
                float g = WoundNoise(i.posOS * float3(60.0, 150.0, 60.0)) * 0.6 + WoundNoise(i.posOS * 260.0) * 0.4;
                half3 c = MeatAlbedo(i.posOS, lerp(half3(0.24, 0.025, 0.035), half3(0.46, 0.07, 0.08), g), 120.0);
                float open = HoleCut(i.posOS);                 // 0.62 at the opening's edge
                c *= lerp(1.0, 0.4, saturate((open - 0.62) / 2.5));
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
                float _BurnCount;
                float _Char;
                float _Heat;
                float4 _BurnStyle;
                float4 _MeltCenter;
                float _Uniform;
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
                float _BurnCount;
                float _Char;
                float _Heat;
                float4 _BurnStyle;
                float4 _MeltCenter;
                float _Uniform;
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
                float _BurnCount;
                float _Char;
                float _Heat;
                float4 _BurnStyle;
                float4 _MeltCenter;
                float _Uniform;
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
