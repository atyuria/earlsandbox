// Ballistic visor glass: tinted, glossy, see-through.
// Hits (_DmgTex, one texel each: object-space centre, w = radius, > 0 went through) crack it: a crushed white core,
// radial cracks of uneven length running out from it and a few broken rings around it. A round that went through
// leaves a hole in the middle. Window glass and sights never get hits, so they draw plain.
Shader "Carl/Glass"
{
    Properties
    {
        _Color("Tint", Color) = (0.55, 0.65, 0.7, 0.28)
        _DmgTex("Damage", 2D) = "black" {}
        _DmgCount("Damage Count", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CarlLights.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _DmgCount;
                float _PlateUnder;
            CBUFFER_END
            TEXTURE2D_FLOAT(_DmgTex);
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionOS : TEXCOORD2; half4 color : COLOR; };
            V vert(A v)
            {
                V o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.color = v.color;
                return o;
            }

            float H1(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float N1(float x) { float i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f); return lerp(H1(i), H1(i + 1.0), f); }

            // how cracked this point is (0..1), and whether it is in a hole a round punched through
            float Cracks(float3 P, out float hole)
            {
                hole = 0;
                float crack = 0;
                int n = (int)_DmgCount;
                [loop] for (int k = 0; k < n; k++)
                {
                    float4 d = LOAD_TEXTURE2D(_DmgTex, int2(k, 0));
                    float r = abs(d.w);
                    float3 v = P - d.xyz;
                    float dist = length(v);
                    float reach = r * 4.5 + 0.01;
                    if (dist > reach) continue;
                    float seed = frac(d.x * 913.7 + d.y * 271.3 + d.z * 57.1) * 100.0;
                    if (d.w > 0.0 && dist < r * 0.55) { hole = 1; return 1; }
                    // crushed core: white and opaque
                    float core = 1.0 - smoothstep(r * 0.55, r * 1.05, dist + (N1(atan2(v.y, v.x) * 4.0 + seed) - 0.5) * r * 0.4);
                    crack = max(crack, core);
                    // radial cracks of uneven length, wandering a little
                    const float SPOKES = 13.0;
                    float a = atan2(v.y, v.x) / 6.2831853 + 0.5;
                    float wander = (N1(dist * 260.0 + seed) - 0.5) * 0.22;
                    float s = a * SPOKES + wander;
                    float sector = floor(s);
                    float len = reach * lerp(0.35, 1.0, H1(sector + seed));
                    float off = abs(frac(s) - 0.5) * (6.2831853 / SPOKES) * dist;   // distance to the spoke (m)
                    float spoke = (1.0 - smoothstep(0.00025, 0.0007, off)) * (1.0 - smoothstep(len * 0.75, len, dist));
                    crack = max(crack, spoke * 0.9);
                    // broken rings round the impact, only some segments of each
                    [unroll] for (int j = 1; j <= 3; j++)
                    {
                        float ring = reach * (0.22 + 0.22 * j) * (0.92 + 0.16 * N1(a * 9.0 + seed + j));
                        float on = step(0.45, H1(floor(a * (6.0 + j * 3.0)) + seed * j));
                        float band = 1.0 - smoothstep(0.0003, 0.0008, abs(dist - ring));
                        crack = max(crack, band * on * 0.7);
                    }
                }
                return crack;
            }

            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float hole = 0;
                float crack = 0;
                if (_DmgCount > 0.5) crack = Cracks(i.positionOS, hole);
                if (hole > 0.5) discard;
                float3 n = normalize(i.normalWS) * (front ? 1 : -1);
                Light ml = GetMainLight();
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 h = normalize(ml.direction + vdir);
                half spec = pow(saturate(dot(n, h)), 120.0) * 1.5;
                half fres = pow(1.0 - saturate(dot(n, vdir)), 3.0);
                half3 lit = SampleSH(n) + ml.color * 0.3 + CarlOtherLights(i.positionWS, n, i.positionCS, 0.0);
                half3 col = _Color.rgb * lit + ml.color * spec + fres * 0.4 * (SampleSH(reflect(-vdir, n)) + ml.color * 0.15);
                // older scuffs come in through vertex colour
                half scuff = 1.0 - i.color.r;
                half3 cracked = half3(0.9, 0.93, 0.95) * (SampleSH(n) * 0.6 + lit * 0.5);
                col = lerp(col + scuff * 0.4, cracked, crack);
                half a = saturate(_Color.a + fres * 0.4 + spec + scuff * 0.4 + crack * 0.85);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
