// Drifting dust and smoke (Haze): big soft billboards with a little noise in them, faded where they meet walls
// and floors (no hard edges where a puff cuts into the trench) and as the camera goes into them.
Shader "Carl/Haze"
{
    Properties
    {
        _Soft("Edge softness (m)", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Soft;
            CBUFFER_END

            // (the particle streams pack UV into TEXCOORD0.xy and the centre after it: .zw, then TEXCOORD1.x)
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; float center : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; float3 seed : TEXCOORD2; float eye : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.screen = ComputeScreenPos(o.positionCS);
                o.eye = -TransformWorldToView(w).z;
                o.uv = v.uv.xy;
                o.color = v.color;
                o.seed = float3(v.uv.z, v.uv.w, v.center);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float d = length(c) * 2.0;
                float base = saturate(1.0 - d);
                base = base * base * (3.0 - 2.0 * base);
                float2 s = i.seed.xz * 0.37 + i.seed.y * 0.21;
                float n = vnoise(c * 3.0 + s) * 0.6 + vnoise(c * 7.0 + s * 1.7 + _Time.y * 0.04) * 0.4;
                float a = base * saturate(0.35 + n * 0.9);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(i.screen.xy / i.screen.w), _ZBufferParams);
                a *= saturate((sceneZ - i.eye) / _Soft);
                a *= saturate((i.eye - 0.25) / 1.2);
                return half4(i.color.rgb * (0.92 + n * 0.16), i.color.a * a);
            }
            ENDHLSL
        }
    }
}
