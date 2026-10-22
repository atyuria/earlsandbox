// The glass of an optic, picture-in-picture: the view down the optic (rendered by a second camera into _MainTex),
// the reticle over it, and for a magnified scope the scope shadow - look through it off its axis or from too far
// back and the black ring closes in from that side, as a real exit pupil does.
// _Reticle 0: 4x combat scope (red chevron, drop marks, fine crosshair); 1: 8x sniper (mil-dot, thick posts);
// 2: red dot; 3: holographic (68 MOA ring, centre dot, ticks). _Mask 0 round glass, 1 rectangular window.
// _MOA: how many minutes of angle one unit of glass (centre to edge) covers, so reticles keep their real size.
Shader "Carl/ScopeLens"
{
    Properties
    {
        _MainTex("View", 2D) = "black" {}
        _Reticle("Reticle", Float) = 0
        _Mask("Mask", Float) = 0
        _Aspect("Aspect w/h", Float) = 1
        _MOA("MOA per unit", Float) = 300
        _Pupil("Eye box (m)", Float) = 0.02
        _Relief("Eye relief (m)", Float) = 0.12
        _Shadow("Scope shadow", Float) = 1
        _Glow("Reticle glow", Color) = (1, 0.15, 0.05, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Reticle, _Mask, _Aspect, _MOA, _Pupil, _Relief, _Shadow;
                half4 _Glow;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 eyeOS : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.eyeOS = TransformWorldToObject(_WorldSpaceCameraPos);
                return o;
            }
            float Line(float d, float w, float px) { return saturate((w - d) / max(px, 1e-5) + 0.5); }

            half4 frag(V i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;                 // -1..1 across the glass (y), x scaled by the aspect
                float2 q = float2(p.x * _Aspect, p.y);
                float r = length(p);
                float edgeD;
                if (_Mask < 0.5) { if (r > 1.0) discard; edgeD = r; }
                else { if (length(max(abs(p) - 0.82, 0.0)) > 0.18) discard; edgeD = max(abs(p.x), abs(p.y)); }
                float px = max(fwidth(q.x), fwidth(q.y));
                float3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                col *= lerp(1.0, 0.85, edgeD * edgeD);

                float ink = 0.0, glow = 0.0;
                float2 a = abs(q);
                float rq = length(q);
                float moa = 1.0 / max(_MOA, 1e-3);           // one minute of angle in glass units
                if (_Reticle < 0.5)
                {
                    ink += Line(a.y, 0.004, px) * step(0.18, a.x);
                    ink += Line(a.x, 0.004, px) * step(0.18, -q.y);
                    ink += Line(a.x, 0.004, px) * step(0.45, q.y);
                    float2 c = float2(a.x, -q.y);
                    float along = dot(c, normalize(float2(0.7, 1.0)));
                    float across = abs(c.x - c.y * 0.7) / 1.22;
                    glow += Line(across, 0.011, px) * step(0.0, along) * step(along, 0.13) * step(c.y, 0.0);
                    for (int k = 1; k <= 5; k++)
                        ink += Line(abs(q.y - (-0.11 - k * 0.075)), 0.004, px) * step(a.x, 0.07 - k * 0.009);
                }
                else if (_Reticle < 1.5)
                {
                    float thick = step(0.55, max(a.x, a.y));
                    ink += Line(a.y, lerp(0.0035, 0.02, thick), px);
                    ink += Line(a.x, lerp(0.0035, 0.02, thick), px);
                    for (int k = 1; k <= 5; k++)
                    {
                        ink += Line(length(float2(a.x - k * 0.1, a.y)), 0.011, px);
                        ink += Line(length(float2(a.x, a.y - k * 0.1)), 0.011, px);
                    }
                    glow += Line(rq, 0.006, px);
                }
                else if (_Reticle < 2.5)
                {
                    // red dot: 2 MOA, drawn a bit larger so it reads, with its glow
                    float rr = rq / moa;
                    glow += Line(rr, max(2.6, px / moa * 1.4), px / moa) + 0.3 * exp(-pow(rr / 7.0, 2.0));
                }
                else
                {
                    float rr = rq / moa; float pm = px / moa;
                    float2 aa = a / moa;
                    glow += Line(abs(rr - 34.0), max(1.4, pm * 0.9), pm) * 0.9;
                    glow += Line(rr, max(1.7, pm * 1.3), pm);
                    glow += Line(aa.x, max(1.0, pm * 0.8), pm) * step(34.0, aa.y) * step(aa.y, 41.0) * 0.8;
                    glow += Line(aa.y, max(1.0, pm * 0.8), pm) * step(34.0, aa.x) * step(aa.x, 41.0) * 0.8;
                }
                col = lerp(col, float3(0.01, 0.01, 0.01), saturate(ink));
                col = lerp(col, _Glow.rgb * 1.8, saturate(glow));

                // the scope shadow (magnified scopes only): off the eye box, or too far back, it darkens from that side
                if (_Shadow > 0.5)
                {
                    float2 off = i.eyeOS.xy / max(_Pupil, 1e-4);
                    float back = abs(i.eyeOS.z) / max(_Relief, 1e-4);
                    float far = max(back - 1.3, 0.0) * 0.5;
                    float shadow = smoothstep(1.05, 0.7 - far * 0.3, length(p - off * 0.45) + far);
                    shadow *= smoothstep(1.0, 0.92, r);
                    col *= shadow;
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
