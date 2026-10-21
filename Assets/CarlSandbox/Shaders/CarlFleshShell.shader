// Solid flesh under the skin: a few nested layers of muscle (copies of the skin pushed inward), seen only
// through wounds. Each wound carves a narrowing crater down through them, so looking into a hole you see torn
// meat stepping down to the organs and bone it reached - not a hollow shell.
Shader "Carl/FleshShell"
{
    Properties
    {
        _HoleTex("Holes", 2D) = "black" {}
        _HoleCount("Hole Count", Float) = 0
        _Inset("Inset (m)", Float) = 0.01
        _Depth01("Depth 0-1", Float) = 0.3
        _Tissue("Tissue (0 fat, 1 muscle)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+5" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _HoleCount;
            float _Inset;
            float _Depth01;
            float _Tissue;
            float4 _ClipPlane;
        CBUFFER_END
        #define CARL_HOLES
        #include "CarlWounds.hlsl"

        // 1 = this layer has been torn away here. Neighbouring craters merge smoothly (smooth minimum of
        // radial/radius) so overlapping hits never leave knife-edged slivers of fat and muscle standing.
        float Carved(float3 p)
        {
            const float K = 6.0;
            float s = 0, hard = 10;
            int n = (int)_HoleCount;
            [loop] for (int k = 0; k < n; k++)
            {
                float4 h = LOAD_TEXTURE2D(_HoleTex, int2(k, 0));
                float4 ax = LOAD_TEXTURE2D(_HoleTex, int2(k, 1));
                float3 a = normalize(ax.xyz + 1e-5);
                float along = dot(p - h.xyz, a);
                float depth = max(ax.w, 0.01);
                if (along < -0.01 || along > depth) continue;
                float seed;
                float3 q = HoleQ(p, k, h, seed) * max(h.w, 0.003);
                float radial = length(q - a * dot(q, a));
                // a wound opens the layers in proportion to its size: a small hole narrows fast into a track,
                // a big blast or exit wound stays wide much deeper
                // fat is torn back nearly as far as the skin (a thin yellow band at the edge), muscle less so
                float rr = h.w * lerp(0.7, 0.86, _Tissue) * exp(-max(along, 0) / min(1.5 * max(h.w, 0.003), 0.03 + max(h.w - 0.02, 0.0) * 2.0)) * (0.9 + 0.2 * WoundNoise(p * 22.0 + seed + _Inset * 120.0));   // gently uneven, no saw teeth
                float d = radial / max(rr, 1e-4);
                s += exp(-K * d);
                hard = min(hard, d);
            }
            // bridge slivers between neighbouring craters, never more (many hits on one spot stay one crater)
            return (s > 1e-6 && max(-log(s) / K, hard - 0.18) < 1.0) ? 1 : 0;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            // only seen through an opening (the skin's inside pass marks those pixels)
            Stencil { Ref 128 ReadMask 192 Comp Equal }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionOS : TEXCOORD2; half fog : TEXCOORD3; };
            V vert(A v)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                CARL_CLIP(i.positionOS) if (_HoleCount < 0.5 || Carved(i.positionOS) > 0.5) discard;
                float3 n = normalize(i.normalWS) * (front ? 1 : -1);
                // torn muscle: fibres, clots, a film of blood; deeper layers get less light
                float3 P = i.positionOS;
                float fib = WoundNoise(P * float3(70, 180, 70)) * 0.6 + WoundNoise(P * 320.0) * 0.4;
                float3 e = float3(0.0015, 0, 0);
                float3 grad = float3(WoundNoise((P + e.xyy) * 160.0) - WoundNoise((P - e.xyy) * 160.0),
                                     WoundNoise((P + e.yxy) * 160.0) - WoundNoise((P - e.yxy) * 160.0),
                                     WoundNoise((P + e.yyx) * 160.0) - WoundNoise((P - e.yyx) * 160.0));
                n = normalize(n - TransformObjectToWorldDir(grad) * 0.6 - TransformObjectToWorldDir(MeatBump(P, 105.0)) * _Tissue);
                half3 c = MeatAlbedo(P, lerp(half3(0.42, 0.04, 0.05), half3(0.66, 0.09, 0.09), fib), 105.0);
                // subcutaneous fat: lumpy, pale yellow, stained with blood
                // globules of fat (small cells), soaked through with blood
                float3 glob = MeatCells(P * 420.0);
                half3 fat = lerp(half3(0.72, 0.52, 0.28), half3(0.86, 0.68, 0.4), glob.z) * (0.85 + 0.15 * smoothstep(0.0, 0.25, glob.y));
                fat = lerp(fat, half3(0.5, 0.08, 0.07), 0.25 + 0.4 * WoundNoise(P * 90.0 + 3.0));
                c = lerp(fat, c, _Tissue);
                c = lerp(c, half3(0.18, 0.0, 0.015), smoothstep(0.6, 0.85, WoundNoise(P * 110.0 + 7.0)) * 0.7);
                c *= lerp(1.0, 0.5, _Depth01) * (front ? 1.0 : 0.6);
                Light ml = GetMainLight();
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 hv = normalize(ml.direction + vdir);
                half wrap = saturate((dot(n, ml.direction) + 0.4) / 1.4);
                half3 light = SampleSH(n) * 0.7 + ml.color * wrap * 0.6;
                half spec = pow(saturate(dot(n, hv)), 70.0) * lerp(0.2, 0.5, _Tissue);
                half3 col = c * light + ml.color * spec * (1.0 - _Depth01 * 0.6);
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R
            Stencil { Ref 128 ReadMask 192 Comp Equal }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
            V vert(float4 positionOS : POSITION) { V o; o.positionCS = TransformObjectToHClip(positionOS.xyz); o.positionOS = positionOS.xyz; return o; }
            half frag(V i) : SV_Target { CARL_CLIP(i.positionOS) if (_HoleCount < 0.5 || Carved(i.positionOS) > 0.5) discard; return 0; }
            ENDHLSL
        }
    }
}
