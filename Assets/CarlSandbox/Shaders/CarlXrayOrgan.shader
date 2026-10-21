// Organs, bones and vessels drawn inside the body during x-ray.
// Vertex colour carries detail: alpha 0 = rgb shades _Color, alpha 1 = rgb is a colour of its own (bone in the
// spine, vessels on the heart...). Wet highlights, wrapped light and a little mottling keep tissue from looking
// like plastic. _Damage tints toward deep red, _Flash pulses for actively bleeding/critical tissue.
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.color = v.color;
                return o;
            }

            float hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float vnoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1,0,0)), f.x),
                                 lerp(hash3(i + float3(0,1,0)), hash3(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0,0,1)), hash3(i + float3(1,0,1)), f.x),
                                 lerp(hash3(i + float3(0,1,1)), hash3(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndv = saturate(dot(n, vdir));

                half3 baseCol = lerp(_Color.rgb * i.color.rgb, i.color.rgb, i.color.a);
                // fine mottling: blood in the tissue, grain in the bone
                float m = vnoise(i.positionOS * 140.0) * 0.6 + vnoise(i.positionOS * 420.0) * 0.4;
                baseCol *= 0.88 + 0.22 * m;

                // a key light from above and behind the viewer, wrapped so the dark side still reads
                half3 L = normalize(vdir + half3(0, 0.7, 0));
                half wrap = saturate(dot(n, L) * 0.5 + 0.5);
                half diffuse = 0.32 + 0.68 * wrap * wrap;
                half spec = pow(saturate(dot(n, normalize(L + vdir))), 48.0) * 0.4;
                half rim = pow(1.0 - ndv, 3.0);
                half3 col = baseCol * diffuse + spec + rim * (baseCol * 0.6 + 0.12);

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
