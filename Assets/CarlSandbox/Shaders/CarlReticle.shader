// The reticle of a red dot (_Shape 0) or holographic sight (_Shape 1), as it really works: projected to infinity.
// The quad covers the sight's window and faces down the sight line (+z); each pixel works out the direction you
// look through it, and the reticle is drawn at a fixed ANGLE from the sight line (in minutes of angle), so it
// stays on the target wherever your eye is behind the glass and only the window around it moves. The glass
// has its faint coloured coating and a little reflection at its edge.
Shader "Carl/Reticle"
{
    Properties
    {
        _Shape("Shape", Float) = 0
        _Color("Color", Color) = (1, 0.12, 0.06, 1)
        _Tint("Lens tint", Color) = (0.55, 0.75, 1, 0.1)
        _Bright("Brightness", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Shape;
                half4 _Color;
                half4 _Tint;
                float _Bright;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 posWS : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                o.posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.posWS);
                o.uv = v.uv;
                return o;
            }

            static const float MOA = 0.000290888;

            // a soft-edged line feature: d = distance (in MOA) from it, w = half width (MOA), px = MOA per pixel
            float Feature(float d, float w, float px) { return saturate((w - d) / max(px, 1e-4) + 0.5); }

            half4 frag(V i) : SV_Target
            {
                // the window of the sight: round for the red dot, a rounded rectangle for the holo
                float2 q = i.uv - 0.5;
                float win;
                if (_Shape < 0.5) win = length(q) * 2.0;
                else { float2 b = abs(q) - float2(0.5, 0.38) + 0.08; win = (length(max(b, 0.0)) + min(max(b.x, b.y), 0.0) - 0.08) * 2.0 + 1.0; }
                if (win > 1.0) discard;
                float edge = smoothstep(0.75, 1.0, win);

                // where this pixel looks, as an angle off the sight line
                float3 d = TransformWorldToObjectDir(normalize(i.posWS - _WorldSpaceCameraPos), true);
                if (d.z <= 0.0) discard;
                float2 ang = d.xy / d.z / MOA;              // minutes of angle right / up of the line
                float px = max(fwidth(ang.x), fwidth(ang.y));
                float r = length(ang);

                float I = 0.0;
                if (_Shape < 0.5)
                {
                    // a 2 MOA dot, drawn a little bigger so it reads on screen, with the glow an eye sees round it
                    float core = Feature(r, max(2.4, px * 1.6), px);
                    I = core + 0.35 * exp(-pow(r / 7.0, 2.0)) + 0.08 * exp(-pow(r / 18.0, 2.0));
                }
                else
                {
                    // EOTech 68 MOA ring with a 1 MOA dot, and the four ticks on the ring
                    float ring = Feature(abs(r - 34.0), max(1.3, px * 0.9), px);
                    float dotC = Feature(r, max(1.6, px * 1.3), px);
                    float2 aa = abs(ang);
                    float ticks = 0.0;
                    ticks += Feature(aa.x, max(1.0, px * 0.8), px) * step(34.0, aa.y) * step(aa.y, 41.0);
                    ticks += Feature(aa.y, max(1.0, px * 0.8), px) * step(34.0, aa.x) * step(aa.x, 41.0);
                    I = ring * 0.9 + dotC + ticks * 0.8 + 0.12 * exp(-pow(r / 6.0, 2.0));
                }
                // the coating: a faint coloured film over the glass, a bright rim where the light catches the edge
                float a = _Tint.a * (0.6 + 0.8 * edge);
                float3 col = _Tint.rgb * a + _Color.rgb * I * _Bright + edge * edge * 0.06;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
