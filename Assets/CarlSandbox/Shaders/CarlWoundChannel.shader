// Inside of an open wound: the 3D channel behind a hole cut in the skin.
//  vertex colour rgb = tissue (skin edge, fat, muscle, bone, brain, organ), a = how far down the channel (0 mouth, 1 floor)
Shader "Carl/WoundChannel"
{
    Properties { _ClipPlane("Clip plane", Vector) = (0,0,0,0) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ClipPlane;
            CBUFFER_END
            #include "CarlWounds.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                CARL_CLIP(i.positionOS)
                float3 n = normalize(i.normalWS);
                // muscle (red, not fat / bone / organ) gets the cut-meat surface: bundles, fascia, marbling
                half meatK = saturate((i.color.r - i.color.g - i.color.b) * 3.0);
                n = normalize(n - TransformObjectToWorldDir(MeatBump(i.positionOS, 100.0)) * meatK);
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half atten = lerp(0.45, 1.0, ml.shadowAttenuation);
                half3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 h = normalize(ml.direction + vdir);
                half wrap = saturate((dot(n, ml.direction) + 0.5) / 1.5);
                half3 light = SampleSH(n) * 0.6 + ml.color * wrap * atten * 0.6;

                float t = saturate(i.color.a);
                // fibres and clots
                float grain = WoundNoise(i.positionOS * float3(160, 260, 160)) * 0.6 + WoundNoise(i.positionOS * 420.0) * 0.4;
                half3 tissue = i.color.rgb * (0.78 + 0.4 * grain);
                tissue = lerp(tissue, MeatAlbedo(i.positionOS, i.color.rgb * 1.2, 100.0), meatK);
                // a film of blood over everything, pooling deeper down
                half3 bloodFilm = half3(0.22, 0.0, 0.015);
                // bone, brain and organs stay readable through the blood; muscle soaks it up, but its texture
                // still shows through
                half lum = dot(i.color.rgb, half3(0.3, 0.5, 0.2));
                half film = saturate(0.15 + 0.45 * smoothstep(0.2, 1.0, t)) * (0.6 + 0.4 * grain) * (1.0 - saturate(lum * 1.1) * 0.75) * 0.65;
                tissue = lerp(tissue, bloodFilm, film);
                // less light reaches the bottom of the channel
                half ao = lerp(0.9, 0.3, pow(t, 0.7));
                ao = max(ao, lum * 0.55);   // organs and bone at the bottom still catch some light
                // it is all wet
                half spec = pow(saturate(dot(n, h)), 70.0) * 0.45 * atten * (1.0 - t * 0.5);
                half3 col = tissue * light * ao + ml.color * spec;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V vert(float4 positionOS : POSITION, float3 normalOS : NORMAL) { V o; o.positionCS = TransformObjectToHClip(positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(normalOS); return o; }
            half4 frag(V i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
