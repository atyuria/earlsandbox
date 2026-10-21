// Translucent body shell shown while the x-ray is on.
Shader "Carl/XrayGhost"
{
    Properties
    {
        _Color("Color", Color) = (0.12, 0.13, 0.15, 0.35)
        _RimColor("Rim Color", Color) = (0.55, 0.6, 0.65, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RimColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half fres = pow(1.0 - saturate(dot(n, vdir)), 2.2);
                half3 col = lerp(_Color.rgb, _RimColor.rgb, fres);
                col = lerp(col, half3(0.5, 0.02, 0.03), saturate(i.color.r) * 0.6);
                half a = saturate(_Color.a + fres * 0.45);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
