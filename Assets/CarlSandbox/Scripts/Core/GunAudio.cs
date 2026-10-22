using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Builds the layered gun sound at load time out of the recorded shots (Resources/Sounds/gun_*), the way a
    /// shot really reaches you: a sharp close report with a low thump under it and the gun's action working; the
    /// echo of it off whatever is round about - a short hard slap in a trench or a room, a long roll across open
    /// ground; far off, only a dull boom arriving late. A suppressor takes off the crack: a muffled thud, the bolt
    /// clattering loud by comparison, hardly any echo. Also the snap of a round going past your head, the whine
    /// of one going past slower, and cases landing in the dirt.
    /// Every clip here is made from the CC0 recordings in the project or synthesised; nothing else is used.
    /// </summary>
    public static class GunAudio
    {
        const int Rate = 44100, TailRate = 22050;

        /// <summary>Adds the built clips to the sound groups (name -> variations).</summary>
        public static void Build(Dictionary<string, List<AudioClip>> groups)
        {
            var rng = new System.Random(1234);
            foreach (var fam in new[] { "gun_ak", "gun_glock", "gun_awp", "gun_50", "gun_shotgun" })
            {
                if (!groups.TryGetValue(fam, out var src) || src.Count == 0) continue;
                bool pistol = fam == "gun_glock";
                for (int i = 0; i < src.Count && i < 3; i++)
                {
                    var x = Mono(src[i]);
                    if (x == null) continue;
                    Add(groups, fam + "_supp", Make(fam + "_supp_" + i, Suppressed(x, pistol, rng), Rate));
                }
                for (int i = 0; i < src.Count && i < 2; i++)
                {
                    var x = Mono(src[i]);
                    if (x == null) continue;
                    var low = Resample(x, 2);   // the echoes are dull anyway: half rate
                    Add(groups, fam + "_tail_open", Make(fam + "_tail_open_" + i, TailOpen(low, fam == "gun_50" ? 1.25f : 1f), TailRate));
                    Add(groups, fam + "_tail_tight", Make(fam + "_tail_tight_" + i, TailTight(low), TailRate));
                    Add(groups, fam + "_far", Make(fam + "_far_" + i, Far(low), TailRate));
                }
            }
            for (int i = 0; i < 3; i++)
            {
                Add(groups, "thump", Make("thump_" + i, Thump(rng), Rate));
                Add(groups, "mech_rifle", Make("mech_rifle_" + i, Mech(rng, false), Rate));
                Add(groups, "mech_pistol", Make("mech_pistol_" + i, Mech(rng, true), Rate));
                Add(groups, "flyby_crack", Make("flyby_crack_" + i, Crack(rng), Rate));
                Add(groups, "flyby_whiz", Make("flyby_whiz_" + i, Whiz(rng), Rate));
                Add(groups, "helmet_ping", Make("helmet_ping_" + i, Ping(rng), Rate));
            }
            if (groups.TryGetValue("casing", out var cas))
                for (int i = 0; i < cas.Count; i++) { var x = Mono(cas[i]); if (x != null) Add(groups, "casing_dirt", Make("casing_dirt_" + i, Dirt(x), Rate)); }
        }

        static void Add(Dictionary<string, List<AudioClip>> g, string name, AudioClip c)
        {
            if (c == null) return;
            if (!g.TryGetValue(name, out var l)) g[name] = l = new List<AudioClip>();
            l.Add(c);
        }

        static AudioClip Make(string name, float[] d, int rate)
        {
            if (d == null || d.Length == 0) return null;
            var c = AudioClip.Create(name, d.Length, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        static float[] Mono(AudioClip c)
        {
            if (c == null) return null;
            if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            var d = new float[c.samples * c.channels];
            if (!c.GetData(d, 0)) return null;
            if (c.channels == 1) return d;
            var m = new float[c.samples];
            for (int i = 0; i < m.Length; i++) { float s = 0f; for (int k = 0; k < c.channels; k++) s += d[i * c.channels + k]; m[i] = s / c.channels; }
            return m;
        }

        // ------------------------------------------------------------ building blocks

        static float[] LowPass(float[] x, float hz, int rate, int poles = 2)
        {
            var y = (float[])x.Clone();
            float a = Mathf.Exp(-2f * Mathf.PI * hz / rate);
            for (int p = 0; p < poles; p++) { float s = 0f; for (int i = 0; i < y.Length; i++) { s = (1f - a) * y[i] + a * s; y[i] = s; } }
            return y;
        }

        static float[] HighPass(float[] x, float hz, int rate)
        {
            var lp = LowPass(x, hz, rate, 1);
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = x[i] - lp[i];
            return y;
        }

        static float[] Resample(float[] x, int by)
        {
            var lp = LowPass(x, Rate / (2.2f * by), Rate, 2);
            var y = new float[x.Length / by];
            for (int i = 0; i < y.Length; i++) y[i] = lp[i * by];
            return y;
        }

        static float[] Pad(float[] x, int extra) { var y = new float[x.Length + extra]; System.Array.Copy(x, y, x.Length); return y; }

        static float[] Normalize(float[] x, float peak)
        {
            float m = 1e-6f; foreach (var v in x) m = Mathf.Max(m, Mathf.Abs(v));
            float k = peak / m;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
            return x;
        }

        /// <summary>Fade the end out so nothing clicks off.</summary>
        static float[] FadeEnd(float[] x, int rate, float sec = 0.05f)
        {
            int n = Mathf.Min(x.Length, Mathf.RoundToInt(sec * rate));
            for (int i = 0; i < n; i++) x[x.Length - 1 - i] *= i / (float)n;
            return x;
        }

        /// <summary>A Schroeder reverb (four combs into two all-passes): only the reflected sound, not the dry.</summary>
        static float[] Reverb(float[] x, int rate, float rt60, float preDelay, float[] early = null)
        {
            int n = x.Length;
            var y = new float[n];
            float[] combMs = { 29.7f, 37.1f, 41.1f, 43.7f };
            foreach (var ms in combMs)
            {
                int d = Mathf.Max(1, Mathf.RoundToInt(ms * 0.001f * rate * Mathf.Lerp(0.6f, 1.6f, Mathf.Clamp01(rt60 / 2.5f))));
                float g = Mathf.Pow(10f, -3f * d / (rt60 * rate));
                var buf = new float[d]; int idx = 0; float lp = 0f;
                for (int i = 0; i < n; i++)
                {
                    float o = buf[idx];
                    lp = o * 0.7f + lp * 0.3f;                       // the walls soak up the highs on every bounce
                    buf[idx] = x[i] + lp * g;
                    idx = (idx + 1) % d;
                    y[i] += o * 0.25f;
                }
            }
            foreach (var (ms, g) in new[] { (5f, 0.7f), (1.7f, 0.7f) })
            {
                int d = Mathf.Max(1, Mathf.RoundToInt(ms * 0.001f * rate));
                var buf = new float[d]; int idx = 0;
                for (int i = 0; i < n; i++)
                {
                    float b = buf[idx];
                    float v = y[i] + b * g;
                    buf[idx] = v;
                    y[i] = b - v * g;
                    idx = (idx + 1) % d;
                }
            }
            // hard surfaces close by: discrete slaps a few milliseconds after the shot
            if (early != null)
                for (int k = 0; k + 1 < early.Length; k += 2)
                {
                    int d = Mathf.RoundToInt(early[k] * 0.001f * rate);
                    for (int i = n - 1; i >= d; i--) y[i] += x[i - d] * early[k + 1];
                }
            int pd = Mathf.RoundToInt(preDelay * rate);
            if (pd > 0) { for (int i = n - 1; i >= pd; i--) y[i] = y[i - pd]; for (int i = 0; i < pd; i++) y[i] = 0f; }
            return y;
        }

        // ------------------------------------------------------------ gun layers

        static float[] Suppressed(float[] x, bool pistol, System.Random rng)
        {
            // the crack gone: what's left is the low body of the report, cut short
            var y = LowPass(x, pistol ? 1300f : 950f, Rate, 3);
            int len = Mathf.Min(y.Length, Mathf.RoundToInt(0.55f * Rate));
            var o = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                o[i] = y[i] * (t < 0.008f ? 1f : Mathf.Exp(-(t - 0.008f) / 0.065f));
            }
            Normalize(o, 0.7f);
            // gas spitting out of the can: a short breathy "pff"
            for (int i = 0; i < Mathf.Min(len, Rate / 20); i++)
            {
                float t = i / (float)Rate;
                o[i] += ((float)rng.NextDouble() * 2f - 1f) * 0.07f * Mathf.Exp(-t / 0.01f);
            }
            // and the action, loud by comparison
            var m = Mech(rng, pistol);
            int at = Mathf.RoundToInt(0.006f * Rate);
            for (int i = 0; i < m.Length && at + i < len; i++) o[at + i] += m[i] * 0.24f;
            return FadeEnd(Normalize(o, 0.85f), Rate);
        }

        static float[] TailOpen(float[] x, float size)
        {
            // across open ground: the report rolls back off tree lines and the ground for a second or two
            var y = Pad(LowPass(x, 2200f, TailRate, 2), Mathf.RoundToInt(1.9f * size * TailRate));
            y = Reverb(y, TailRate, 1.9f * size, 0.07f);
            for (int i = 0; i < y.Length; i++) y[i] *= Mathf.Clamp01(i / (0.03f * TailRate));
            return FadeEnd(Normalize(y, 0.7f), TailRate, 0.3f);
        }

        static float[] TailTight(float[] x)
        {
            // walls a metre or two away: a hard, quick slap and a short, bright decay
            var y = Pad(LowPass(x, 4500f, TailRate, 1), Mathf.RoundToInt(0.7f * TailRate));
            y = Reverb(y, TailRate, 0.55f, 0.004f, new[] { 4f, 0.55f, 7f, 0.45f, 11f, 0.35f, 16f, 0.25f, 23f, 0.18f });
            return FadeEnd(Normalize(y, 0.75f), TailRate, 0.15f);
        }

        static float[] Far(float[] x)
        {
            // a long way off: the highs gone, a low boom with its echo all mixed in
            var dry = Pad(LowPass(x, 420f, TailRate, 3), Mathf.RoundToInt(1.8f * TailRate));
            var wet = Reverb(dry, TailRate, 2.2f, 0.12f);
            for (int i = 0; i < dry.Length; i++) dry[i] = dry[i] * 0.7f + wet[i] * 0.6f;
            return FadeEnd(Normalize(dry, 0.8f), TailRate, 0.3f);
        }

        static float[] Thump(System.Random rng)
        {
            // the pressure wave in your chest: a quick low drop of pitch and a puff of low noise
            int n = Mathf.RoundToInt(0.32f * Rate);
            var y = new float[n];
            float ph = 0f, lp = 0f;
            float f1 = 40f + (float)rng.NextDouble() * 12f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = f1 + 95f * Mathf.Exp(-t / 0.028f);
                ph += 2f * Mathf.PI * f / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp = lp * 0.97f + noise * 0.03f;
                y[i] = Mathf.Sin(ph) * Mathf.Exp(-t / 0.085f) + lp * 6f * Mathf.Exp(-t / 0.02f);
            }
            return FadeEnd(Normalize(y, 0.9f), Rate);
        }

        /// <summary>The bolt or slide going back and slamming home: two metallic clacks.</summary>
        static float[] Mech(System.Random rng, bool pistol)
        {
            int n = Mathf.RoundToInt(0.16f * Rate);
            var y = new float[n];
            void Clack(float at, float gain, float bright)
            {
                float[] fr = { 1900f, 3100f, 4400f, 6300f, 8100f };
                int s0 = Mathf.RoundToInt(at * Rate);
                for (int k = 0; k < fr.Length; k++)
                {
                    float f = fr[k] * bright * (0.92f + 0.16f * (float)rng.NextDouble());
                    float dec = 0.006f + 0.012f * (float)rng.NextDouble();
                    float g = gain * (k == 0 ? 0.6f : 1f / (1f + k * 0.4f));
                    for (int i = s0; i < n; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        y[i] += Mathf.Sin(2f * Mathf.PI * f * t + k) * g * Mathf.Exp(-t / dec);
                    }
                }
                for (int i = s0; i < Mathf.Min(n, s0 + Rate / 400); i++) y[i] += ((float)rng.NextDouble() * 2f - 1f) * gain * 0.8f;
            }
            Clack(0f, 1f, pistol ? 1.25f : 1f);
            Clack(pistol ? 0.028f : 0.052f, 0.75f, pistol ? 1.35f : 0.92f);
            return FadeEnd(Normalize(HighPass(y, 600f, Rate), 0.8f), Rate, 0.02f);
        }

        /// <summary>A supersonic round passing: the sharp snap of its shock wave, then a hiss.</summary>
        static float[] Crack(System.Random rng)
        {
            int n = Mathf.RoundToInt(0.22f * Rate);
            var y = new float[n];
            int w = Mathf.RoundToInt(0.0005f * Rate);
            for (int i = 0; i < w; i++) y[i] = 1f - 2f * i / (float)w;     // the N-wave
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp = lp * 0.6f + noise * 0.4f;
                y[i] += (noise - lp) * 0.35f * Mathf.Exp(-t / 0.03f);
            }
            var r = Reverb(y, Rate, 0.25f, 0.002f, new[] { 3f, 0.3f, 8f, 0.2f });
            for (int i = 0; i < n; i++) y[i] = y[i] + r[i] * 0.4f;
            return FadeEnd(Normalize(y, 0.95f), Rate, 0.03f);
        }

        /// <summary>A round stopped by the helmet you are wearing: a hard crack and the shell ringing like a bell
        /// right against your skull (inharmonic metal partials, slowly dying away).</summary>
        static float[] Ping(System.Random rng)
        {
            int n = Mathf.RoundToInt(2.4f * Rate);
            var y = new float[n];
            float f0 = 820f + 160f * (float)rng.NextDouble();
            float[] ratio = { 1f, 2.32f, 4.25f, 6.63f, 9.38f }, dec = { 1.1f, 0.7f, 0.45f, 0.3f, 0.2f }, amp = { 1f, 0.8f, 0.55f, 0.35f, 0.25f };
            for (int k = 0; k < ratio.Length; k++)
            {
                float f = f0 * ratio[k] * (0.99f + 0.02f * (float)rng.NextDouble()), ph = (float)rng.NextDouble() * 6f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    // a slow beat between the two sides of the shell
                    float beat = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * (2.5f + k) * t);
                    y[i] += Mathf.Sin(2f * Mathf.PI * f * t + ph) * amp[k] * Mathf.Exp(-t / dec[k]) * beat;
                }
            }
            // the impact itself
            for (int i = 0; i < Rate / 60; i++) { float t = i / (float)Rate; y[i] += ((float)rng.NextDouble() * 2f - 1f) * 2.2f * Mathf.Exp(-t / 0.004f); }
            return FadeEnd(Normalize(y, 0.95f), Rate, 0.2f);
        }

        /// <summary>A slower round or a ricochet fragment going by: a falling whine.</summary>
        static float[] Whiz(System.Random rng)
        {
            int n = Mathf.RoundToInt(0.4f * Rate);
            var y = new float[n];
            float ph = 0f, f0 = 2400f + 900f * (float)rng.NextDouble(), lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = t / 0.4f;
                float f = f0 * Mathf.Lerp(1.15f, 0.65f, u);                    // Doppler: higher coming, lower going
                ph += 2f * Mathf.PI * f / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp = lp * 0.85f + noise * 0.15f;
                float env = Mathf.Exp(-Mathf.Pow((u - 0.35f) / 0.2f, 2f));
                y[i] = (Mathf.Sin(ph) * 0.6f + lp * 1.4f) * env;
            }
            return FadeEnd(Normalize(y, 0.6f), Rate);
        }

        static float[] Dirt(float[] x)
        {
            // a case landing in mud and leaf litter: a dull tick, no ring
            var y = LowPass(x, 750f, Rate, 2);
            int len = Mathf.Min(y.Length, Mathf.RoundToInt(0.18f * Rate));
            var o = new float[len];
            for (int i = 0; i < len; i++) o[i] = y[i] * Mathf.Exp(-i / (0.035f * Rate));
            return FadeEnd(Normalize(o, 0.5f), Rate, 0.02f);
        }
    }
}
