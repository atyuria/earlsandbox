// Blood fluid simulation on a 2D canvas (R = blood thickness, G = dryness).
// Pass 0: stamp up to 16 procedural splats / smears / ooze sources.
// Pass 1: one simulation step (gravity transport with meandering rivulets, viscous spreading, drying).
Shader "Hidden/Carl/BloodSim"
{
    Properties { _MainTex ("Canvas", 2D) = "black" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    float _WrapU;

    float hash11(float n) { return frac(sin(n) * 43758.5453); }
    float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
    float noise2(float2 p)
    {
        float2 i = floor(p), f = frac(p);
        float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + 1);
        float2 u = f * f * (3 - 2 * f);
        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }
    float2 wrapuv(float2 uv) { if (_WrapU > 0.5) uv.x = frac(uv.x); return uv; }
    float2 _TileOffset; // texel offset of this canvas in its surface, so patterns are continuous across tiles
    sampler2D _Mask;    // 1 where the canvas actually lies on a surface (blood never floats past edges)
    float _UseMask;
    float SurfaceMask(float2 uv) { return _UseMask > 0.5 ? tex2Dlod(_Mask, float4(uv, 0, 0)).r : 1.0; }
    ENDCG

    SubShader
    {
        ZTest Always ZWrite Off Cull Off

        // ---------------------------------------------------------------- stamp
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.5
            float4 _S0[16]; // center uv xy, radius uv zw
            float4 _S1[16]; // dir xy, stretch, seed
            float4 _S2[16]; // amount, kind (0 splat, 1 smear, 2 ooze), spatter, 0
            int _Count;

            float splat(float2 uv, float4 s0, float4 s1, float4 s2)
            {
                float2 d = uv - s0.xy;
                if (_WrapU > 0.5) d.x = frac(d.x + 0.5) - 0.5;
                d /= max(s0.zw, 1e-5);
                float2 dir = s1.xy;
                float2 perp = float2(-dir.y, dir.x);
                float2 q = float2(dot(d, dir), dot(d, perp));
                float stretch = max(1.0, s1.z);
                float seed = s1.w;
                float kind = s2.y;
                float v = 0;
                if (kind < 0.5)
                {
                    // main blob: noisy edge, elongated along travel with a pointed leading tail
                    float2 qs = float2(q.x / stretch, q.y);
                    if (q.x > 0) qs.y *= 1.0 + saturate(q.x / stretch) * (stretch - 1.0) * 0.8;
                    float r = length(qs);
                    float ang = atan2(qs.y, qs.x);
                    float edge = 0.72 + 0.22 * noise2(float2(ang * 1.6 + seed * 13.1, seed)) + 0.16 * noise2(float2(ang * 5.0 + seed * 7.3, seed * 2.1));
                    v = saturate((edge - r) * 7.0) * (0.55 + 0.45 * saturate(1.0 - r));
                    // spatter droplets thrown around / ahead of the impact
                    float sat = 0;
                    [loop] for (int k = 0; k < 9; k++)
                    {
                        float h1 = hash11(seed * 17.0 + k * 3.1), h2 = hash11(seed * 29.0 + k * 7.7), h3 = hash11(seed * 41.0 + k * 5.3);
                        float a2 = h1 * 6.2831;
                        float2 c = float2(cos(a2), sin(a2)) * (1.15 + h2 * 2.2 * s2.z);
                        c.x = c.x > 0 ? c.x * stretch * 1.5 : c.x * 0.6;
                        float rr = 0.06 + h3 * 0.16;
                        float2 dd = float2(q.x, q.y) - c;
                        dd.x /= 1.0 + (stretch - 1.0) * 0.4;
                        sat = max(sat, saturate((rr - length(dd)) * 30.0));
                    }
                    v = max(v, sat * saturate(s2.z * 3.0));
                }
                else if (kind < 1.5)
                {
                    // smear: a body dragged through blood leaves a dry-brush mark - fine parallel
                    // streaks, a fuller centre, feathered ragged edges and skips. The pattern lives in
                    // canvas space, so consecutive segments join seamlessly (no repeating bands).
                    float along = q.x / stretch;
                    float across = q.y;
                    float2 pa = uv / _MainTex_TexelSize.xy + _TileOffset;
                    float acrossAbs = dot(pa, perp);
                    float alongAbs = dot(pa, dir);
                    float hairs = noise2(float2(acrossAbs * 0.55, seed)) * 0.6 + noise2(float2(acrossAbs * 1.9, seed + 3.0)) * 0.4;
                    float edge = abs(across) + (noise2(float2(alongAbs * 0.05, acrossAbs * 0.03 + seed)) - 0.5) * 0.55;
                    float body = smoothstep(1.0, 0.3, edge);
                    float ends = smoothstep(1.0, 0.55, abs(along));
                    float core = smoothstep(0.8, 0.0, abs(across));
                    // swipe: solid while there is plenty of blood, breaking into feathered streaks as it runs out
                    float solidity = saturate((s2.x - 0.04) / 0.45);
                    float streaks = smoothstep(lerp(0.55, 0.2, solidity), lerp(0.85, 0.5, solidity), hairs);
                    float density = lerp(streaks, 1.0, max(core * 0.4, solidity * 0.85));
                    float skips = smoothstep(0.12, 0.5, noise2(float2(alongAbs * 0.025 + seed, acrossAbs * 0.07)));
                    v = body * ends * density * lerp(lerp(0.4, 0.9, solidity), 1.0, skips);
                }
                else
                {
                    float r = length(q);
                    v = saturate(1.0 - r);
                    v *= v;
                }
                return v * s2.x;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float add = 0, smear = 0;
                [loop] for (int k = 0; k < _Count; k++)
                {
                    float s = splat(i.uv, _S0[k], _S1[k], _S2[k]);
                    if (_S2[k].y > 0.5 && _S2[k].y < 1.5) smear = max(smear, s); // smears don't stack
                    else add += s;
                }
                c.r = max(max(0, c.r + add), smear) * SurfaceMask(i.uv); // negative stamps wipe blood away
                c.g *= 1.0 - saturate(max(0, add + smear) * 3.0);        // fresh blood is wet again
                return c;
            }
            ENDCG
        }

        // ---------------------------------------------------------------- simulate
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.5
            float4 _Grav;       // world mode: xy = flow dir in uv, z = strength. cylinder mode (w=1): xy = uv-per-metre scale
            float4 _GravLocal;  // cylinder mode: gravity in part-local space
            float _Dt;
            float _Spread;
            float _Seed;

            // neighbouring canvases (world tiles): blood flows and spreads across tile borders
            sampler2D _NL, _NR, _ND, _NU;
            float4 _HasN;       // 1 where a neighbour exists (L, R, D, U)
            float _Body;        // 1 = body canvas (drips off downward-facing skin, slowly thins)

            float sampleT(float2 uv)
            {
                if (_WrapU > 0.5) return tex2Dlod(_MainTex, float4(frac(uv.x), saturate(uv.y), 0, 0)).r;
                if (uv.x < 0) return tex2Dlod(_NL, float4(uv.x + 1, uv.y, 0, 0)).r * _HasN.x;
                if (uv.x > 1) return tex2Dlod(_NR, float4(uv.x - 1, uv.y, 0, 0)).r * _HasN.y;
                if (uv.y < 0) return tex2Dlod(_ND, float4(uv.x, uv.y + 1, 0, 0)).r * _HasN.z;
                if (uv.y > 1) return tex2Dlod(_NU, float4(uv.x, uv.y - 1, 0, 0)).r * _HasN.w;
                return tex2Dlod(_MainTex, float4(uv, 0, 0)).r;
            }

            float thr(float2 px) { return 0.05 + 0.08 * noise2(px * 0.13 + _Seed); }

            float2 gravAt(float2 uv)
            {
                float res = _MainTex_TexelSize.z;
                if (_Grav.w < 0.5) return _Grav.xy * _Grav.z * (res / 256.0);
                float th = (uv.x - 0.5) * 6.2831853;
                float3 t = float3(-sin(th), 0, cos(th));
                return float2(dot(_GravLocal.xyz, t) * _Grav.x, _GravLocal.y * _Grav.y) * (res / 512.0);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float2 ts = _MainTex_TexelSize.xy;
                float2 px = i.uv / ts + _TileOffset;
                float4 c = tex2D(_MainTex, i.uv);
                float T = c.r;

                // gravity transport. Real blood does not slide as a sheet: a splat stays put and only
                // narrow rivulets run down from it. Flow happens in thin "channels" (noise stripes
                // across the slope); inside a channel the excess above a small residue runs down,
                // leaving a drip line. Outside channels only very thick blood creeps.
                float2 g = gravAt(i.uv);
                float gl = length(g);
                float k = saturate(gl * _Dt * 36.0);
                float2 gdir = gl > 1e-4 ? g / gl : float2(0, 0);
                float2 perp = float2(-gdir.y, gdir.x);
                float wob = (noise2(px * 0.05 + _Seed * 3.0) - 0.5) * 0.5;
                float2 off = (gdir + perp * wob) * ts;
                float2 pxUp = px - off / ts;
                float chHere = smoothstep(0.68, 0.73, noise2(float2(dot(px, perp) * 1.1, 7.1 + _Seed)));
                float chUp = smoothstep(0.68, 0.73, noise2(float2(dot(pxUp, perp) * 1.1, 7.1 + _Seed)));
                float thrHere = lerp(0.65 + 0.2 * noise2(px * 0.11 + _Seed), 0.06 + 0.05 * noise2(px * 0.13 + _Seed), chHere);
                float thrUp = lerp(0.65 + 0.2 * noise2(pxUp * 0.11 + _Seed), 0.06 + 0.05 * noise2(pxUp * 0.13 + _Seed), chUp);
                float out0 = max(0, T - thrHere) * k * saturate(0.35 + T);
                float Tup = sampleT(i.uv - off);
                float in0 = max(0, Tup - thrUp) * k * saturate(0.35 + Tup);
                T = T - out0 + in0;

                // viscous spreading of thick blood (pools grow with irregular edges)
                float sp = 0.5 + 0.3 * noise2(px * 0.07 + 11.0);
                float Tl = sampleT(i.uv - float2(ts.x, 0));
                float Tr = sampleT(i.uv + float2(ts.x, 0));
                float Td = sampleT(i.uv - float2(0, ts.y));
                float Tu = sampleT(i.uv + float2(0, ts.y));
                float en = max(0, Tl - sp) + max(0, Tr - sp) + max(0, Td - sp) + max(0, Tu - sp);
                T += _Spread * saturate(_Dt * 5.0) * (en * 0.25 - max(0, c.r - sp));

                if (_Body > 0.5)
                {
                    // blood drips off skin that faces the ground, and slowly wipes/soaks away
                    float th = (i.uv.x - 0.5) * 6.2831853;
                    float3 nrm = float3(cos(th), 0, sin(th));
                    float facingDown = saturate(dot(nrm, normalize(_GravLocal.xyz + 1e-5)) - 0.35);
                    T -= T * facingDown * _Dt * 2.5;
                    T -= _Dt * 0.006 * saturate(T * 4.0);
                    // skin can only hold a thin film: anything thicker runs off (no solid red sheets)
                    T -= max(0, T - 0.45) * _Dt * 2.2;
                }

                c.r = max(0, T) * SurfaceMask(i.uv);
                c.g = saturate(c.g + _Dt / (120.0 + 300.0 * c.r));
                return c;
            }
            ENDCG
        }
    }
}
