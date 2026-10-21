// Organs, bones and vessels drawn inside the body during x-ray.
// _Damage tints toward deep red, _Flash pulses for actively bleeding/critical tissue.
Shader "Carl/XrayOrgan"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _Damage("Damage", Range(0,1)) = 0
        _Flash("Flash", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst", Float) = 0
        [Toggle] _ZWrite("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Damage;
                half _Flash;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndv = saturate(dot(n, vdir));
                half3 col = _Color.rgb * (0.45 + 0.55 * ndv) + pow(1.0 - ndv, 3.0) * 0.25;
                half3 hurt = lerp(half3(0.75, 0.05, 0.05), half3(0.12, 0.0, 0.0), saturate(_Damage * 1.4 - 0.4));
                col = lerp(col, hurt, saturate(_Damage));
                half pulse = 0.5 + 0.5 * sin(_Time.y * 10.0);
                col = lerp(col, half3(1.0, 0.1, 0.1), _Flash * pulse * 0.6);
                return half4(col, _Color.a);
            }
            ENDHLSL
        }
    }
}
