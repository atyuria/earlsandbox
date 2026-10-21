// Renders a simulated blood canvas on a surface tile as glossy liquid.
// Thickness drives coverage (crisp merging edges), colour depth and a bump normal from its gradient.
Shader "Carl/BloodSurface"
{
    Properties
    {
        _BloodTex("Blood", 2D) = "black" {}
        _Mask("Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-60" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -2, -2
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CarlBloodCommon.hlsl"

            TEXTURE2D(_BloodTex); SAMPLER(sampler_BloodTex);
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            float4 _BloodTex_TexelSize;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 ts = _BloodTex_TexelSize.xy;
                float4 c = SAMPLE_TEXTURE2D(_BloodTex, sampler_BloodTex, i.uv);
                float T = c.r;
                float a = BloodCoverage(T) * smoothstep(0.35, 0.65, SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.uv).r);
                clip(a - 0.01);
                float tl = SAMPLE_TEXTURE2D(_BloodTex, sampler_BloodTex, i.uv - float2(ts.x, 0)).r;
                float tr = SAMPLE_TEXTURE2D(_BloodTex, sampler_BloodTex, i.uv + float2(ts.x, 0)).r;
                float td = SAMPLE_TEXTURE2D(_BloodTex, sampler_BloodTex, i.uv - float2(0, ts.y)).r;
                float tu = SAMPLE_TEXTURE2D(_BloodTex, sampler_BloodTex, i.uv + float2(0, ts.y)).r;
                float bump = 0.9 * saturate(T * 1.5);
                float rim = length(float2(tl - tr, td - tu)) * 2.5;
                float3 nTS = normalize(float3((tl - tr) * bump, (td - tu) * bump, 1));
                float3 N = normalize(i.normalWS);
                float3 Tg = normalize(i.tangentWS.xyz);
                float3 B = cross(N, Tg) * i.tangentWS.w;
                float3 n = normalize(Tg * nTS.x + B * nTS.y + N * nTS.z);
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 col = BloodShade(T, c.g, n, i.positionWS, ml, rim);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
