// Round soft particle using vertex colour (blood droplets, smoke, sparks).
Shader "Carl/SoftParticle"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst", Float) = 10
        _Hardness("Hardness", Range(0.01, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            Blend [_SrcBlend] [_DstBlend]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Hardness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half d = length(i.uv - 0.5) * 2.0;
                half a = saturate((1.0 - d) / max(0.01, 1.0 - _Hardness));
                half shade = lerp(1.0, 0.65, d);
                return half4(i.color.rgb * shade, i.color.a * a);
            }
            ENDHLSL
        }
    }
}
