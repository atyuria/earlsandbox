// Blood on world surfaces (splats, pools, drips, smears) batched into one mesh.
// Vertex colour: r = thickness, g = dryness, a = opacity.
Shader "Carl/BloodStain"
{
    Properties
    {
        _MainTex("Atlas", 2D) = "white" {}
        _Fresh("Fresh", Color) = (0.40, 0.0, 0.015, 1)
        _Dry("Dry", Color) = (0.13, 0.01, 0.01, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Fresh;
                half4 _Dry;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half a = t.a * i.color.a;
                clip(a - 0.02);
                half3 n = normalize(i.normalWS);
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half atten = ml.shadowAttenuation;
                half3 base = lerp(_Fresh.rgb, _Dry.rgb, saturate(i.color.g)) * lerp(0.75, 1.0, t.r);
                base *= lerp(1.0, 0.7, saturate(i.color.r));
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 h = normalize(ml.direction + vdir);
                half gloss = (1.0 - saturate(i.color.g)) * 0.9;
                half spec = pow(saturate(dot(n, h)), 90.0) * gloss * 1.2;
                half3 col = base * (SampleSH(n) * 0.8 + ml.color * saturate(dot(n, ml.direction)) * atten * 0.6) + ml.color * spec * atten;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
