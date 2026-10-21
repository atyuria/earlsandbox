// Armour seen during x-ray: a faint tinted film over the body with bright edges and a slow scan line, so you can
// see what someone is wearing and still look through it at what is underneath.
Shader "Carl/XrayArmour"
{
    Properties
    {
        _Color("Color", Color) = (0.55, 0.75, 0.95, 1)
        _Fill("Fill", Range(0,1)) = 0.13
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+30" "RenderPipeline"="UniversalPipeline" }
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
                half _Fill;
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
                half fres = pow(1.0 - saturate(dot(n, vdir)), 2.5);
                // fine horizontal bands drifting upward, like a scanner sweep
                half band = frac(i.positionWS.y * 80.0 - _Time.y * 0.6);
                half lines = smoothstep(0.0, 0.15, band) * smoothstep(0.5, 0.35, band) * 0.06;
                half3 tint = _Color.rgb;
                half3 col = lerp(tint * 0.85, saturate(tint * 1.4 + 0.25), fres);
                half a = saturate(_Fill + fres * 0.6 + lines);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
