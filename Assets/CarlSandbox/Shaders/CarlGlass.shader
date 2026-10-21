// Ballistic visor glass: tinted, glossy, see-through.
Shader "Carl/Glass"
{
    Properties { _Color("Tint", Color) = (0.55, 0.65, 0.7, 0.28) }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; };
            V vert(A v) { V o; o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.normalWS = TransformObjectToWorldNormal(v.normalOS); o.color = v.color; return o; }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (front ? 1 : -1);
                Light ml = GetMainLight();
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 h = normalize(ml.direction + vdir);
                half spec = pow(saturate(dot(n, h)), 120.0) * 1.5;
                half fres = pow(1.0 - saturate(dot(n, vdir)), 3.0);
                half3 col = _Color.rgb * (SampleSH(n) + 0.3) + ml.color * spec + fres * 0.25;
                // cracks / impact marks come in through vertex colour
                half crack = 1.0 - i.color.r;
                return half4(col + crack * 0.6, saturate(_Color.a + fres * 0.4 + spec + crack * 0.6));
            }
            ENDHLSL
        }
    }
}
