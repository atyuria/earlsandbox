// Real organs and bones inside a body part, seen only through its open wounds.
// A fragment is drawn when it lies inside a wound channel (the hole list in _HoleTex, in part space via
// _PartW2L) or close around it; where a wound has gone straight through a structure (skull, a rib, the
// liver) the core of the channel is cut away so whatever lies deeper shows through.
Shader "Carl/RevealOrgan"
{
    Properties
    {
        _Color("Color", Color) = (0.6, 0.1, 0.1, 1)
        _HoleTex("Holes", 2D) = "black" {}
        _HoleCount("Hole Count", Float) = 0
        _Punchable("Punchable", Float) = 0
        _Pattern("Pattern", Float) = 0
    }
    SubShader
    {
        // drawn after the skin, and never over intact skin (stencil bit 64), so a rib or bone near the surface
        // can never poke out beside a wound
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Punchable;
            float _Pattern;     // 0 organ, 1 gut coils, 2 bone, 3 brain folds
            float4 _BodyCenter; // the part's skin as a rounded box (part space): never draw outside it
            float4 _BodyExtents;
            float4x4 _PartW2L;
            float _HoleCount;
            float4 _ClipPlane;     // part space
        CBUFFER_END
        #define CARL_HOLES
        #include "CarlWounds.hlsl"

        // 1 = show this point, 0 = hidden by flesh or cut away by the wound
        float Revealed(float3 posWS)
        {
            float3 p = mul(_PartW2L, float4(posWS, 1)).xyz;
            if (CarlTearDist(p) > 0.0) return 0;
            if (_BodyExtents.x > 0)
            {
                float3 q = abs(p - _BodyCenter.xyz) / _BodyExtents.xyz;
                q *= q; q *= q;
                if (q.x + q.y + q.z > 1.0) return 0;
            }
            int n = (int)_HoleCount;
            float show = 0;
            [loop] for (int k = 0; k < n; k++)
            {
                float4 h = LOAD_TEXTURE2D(_HoleTex, int2(k, 0));
                float4 ax = LOAD_TEXTURE2D(_HoleTex, int2(k, 1));
                float3 a = normalize(ax.xyz + 1e-5);
                float r = max(h.w, 0.003);
                float along = dot(p - h.xyz, a);
                if (along < 0.002 || along > ax.w + 0.08) continue;   // only under the skin
                float seed;
                float3 q = HoleQ(p, k, h, seed) * r;              // stretched offset, back in metres
                float radial = length(q - a * dot(q, a));
                // smooth, slightly uneven outline (no jagged teeth)
                float reach = r * (1.3 + 0.15 * WoundNoise(p * 30.0 + seed)) + along * 0.3;
                if (radial > reach) continue;
                // skull / ribs: punched out with a clean round broken edge, so what is under shows through
                if (_Punchable > 0.5 && radial < r * (0.72 + 0.08 * WoundNoise(p * 25.0 + seed)) && along < ax.w + 0.03) return 0;
                show = 1;
            }
            return show;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            // only inside the body seen through an opening (skin's inside pass set 128) and never over intact skin (64)
            Stencil { Ref 128 ReadMask 192 Comp Equal }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half fog : TEXCOORD2; float3 positionOS : TEXCOORD3; half4 color : COLOR; };

            V vert(A v)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.positionOS = v.positionOS.xyz;
                o.color = v.color;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                if (_HoleCount < 0.5 || Revealed(i.positionWS) < 0.5) discard;
                float3 n = normalize(i.normalWS);
                float3 P = i.positionOS;
                // surface detail: coiled gut, brain folds, porous bone, smooth wet organ
                float crease = 0, bumpAmt = 0.25;
                if (_Pattern > 0.5 && _Pattern < 1.5)
                {
                    float v = WoundNoise(P * 38.0) + 0.5 * WoundNoise(P * 75.0);
                    crease = 1.0 - smoothstep(0.03, 0.12, abs(frac(v * 2.5) - 0.5));
                    bumpAmt = 0.6;
                }
                else if (_Pattern > 2.5)
                {
                    float v = WoundNoise(P * 70.0) + 0.5 * WoundNoise(P * 140.0);
                    crease = 1.0 - smoothstep(0.02, 0.09, abs(frac(v * 3.0) - 0.5));
                    bumpAmt = 0.5;
                }
                bool bone = _Pattern > 1.5 && _Pattern < 2.5;
                if (bone) bumpAmt = 0.06;                               // bone is smooth and hard, not a sponge
                // bumpy normal from the detail noise
                float3 e = float3(0.002, 0, 0);
                float hc = WoundNoise(P * 90.0) - crease * 0.6;
                float3 grad = float3(WoundNoise((P + e.xyy) * 90.0) - WoundNoise((P - e.xyy) * 90.0),
                                     WoundNoise((P + e.yxy) * 90.0) - WoundNoise((P - e.yxy) * 90.0),
                                     WoundNoise((P + e.yyx) * 90.0) - WoundNoise((P - e.yyx) * 90.0));
                n = normalize(n - TransformObjectToWorldDir(grad) * bumpAmt * 2.0);
                Light ml = GetMainLight();
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 h = normalize(ml.direction + vdir);
                half wrap = saturate((dot(n, ml.direction) + 0.5) / 1.5);
                // inside the body: little direct light, some fill, wet sheen
                half3 light = SampleSH(n) * 0.8 + ml.color * wrap * 0.75;
                float g = WoundNoise(i.positionOS * 220.0) * 0.6 + WoundNoise(i.positionOS * 60.0) * 0.4;
                // the anatomy meshes carry detail in vertex colour (as for the x-ray): alpha 0 shades this
                // structure's own colour, alpha 1 is a structure of its own - mapped from x-ray colours to real ones
                half4 vc = i.color;
                half3 own = vc.rgb;
                if (vc.b > vc.r + 0.2 && vc.g < 0.6) own = half3(0.3, 0.06, 0.13);                       // veins: dark purple-red
                else if (vc.r < 0.5 && vc.g > 0.7 && vc.b > 0.7) own = half3(0.93, 0.88, 0.78);           // spinal cord / nerves: white
                else if (vc.b > vc.r && vc.g > 0.7) own = half3(0.86, 0.87, 0.88);                        // cartilage: pearly
                else if (vc.r > 0.85 && vc.g < 0.2) own = half3(0.62, 0.05, 0.07);                        // arteries
                half3 base = lerp(_Color.rgb * vc.rgb, own, saturate(vc.a));
                half3 c = base * (0.85 + 0.3 * g);
                c *= 1.0 - crease * 0.55;                              // dark folds between coils / gyri
                if (bone)
                {
                    // ivory with a pinkish skin of periosteum and streaks of blood, not speckles
                    c = lerp(base, base * half3(1.0, 0.86, 0.82), WoundNoise(P * 40.0));
                    c = lerp(c, half3(0.45, 0.03, 0.04), smoothstep(0.55, 0.8, WoundNoise(P * float3(25, 70, 25) + 3.0)) * 0.6);
                }
                else c = lerp(c, half3(0.3, 0.0, 0.02), 0.15 * g);    // smeared blood
                half wetness = bone ? 0.35 : 0.6;
                half spec = pow(saturate(dot(n, h)), 60.0) * wetness;
                half3 col = c * light + ml.color * spec;
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Stencil { Ref 64 ReadMask 64 Comp NotEqual }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            V vert(float4 positionOS : POSITION) { V o; o.positionWS = TransformObjectToWorld(positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); return o; }
            half frag(V i) : SV_Target { if (_HoleCount < 0.5 || Revealed(i.positionWS) < 0.5) discard; return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Stencil { Ref 64 ReadMask 64 Comp NotEqual }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            V vert(float4 positionOS : POSITION, float3 normalOS : NORMAL) { V o; o.positionWS = TransformObjectToWorld(positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.normalWS = TransformObjectToWorldNormal(normalOS); return o; }
            half4 frag(V i) : SV_Target { if (_HoleCount < 0.5 || Revealed(i.positionWS) < 0.5) discard; return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
