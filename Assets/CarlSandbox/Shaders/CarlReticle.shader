// Projected sight reticle on the glass of a red dot (_Shape 0: dot) or holographic sight (_Shape 1: ring and dot).
Shader "Carl/Reticle"
{
    Properties
    {
        _Shape("Shape", Float) = 0
        _Color("Color", Color) = (1, 0.12, 0.06, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend One One
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Shape;
                half4 _Color;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0;
                float dot = exp(-pow(r / 0.06, 2.0)) + 0.25 * exp(-pow(r / 0.14, 2.0));
                float ring = _Shape > 0.5 ? exp(-pow((r - 0.62) / 0.035, 2.0)) * 0.9 : 0.0;
                float I = dot + ring;
                return half4(_Color.rgb * I * 1.6, 0);
            }
            ENDHLSL
        }
    }
}
