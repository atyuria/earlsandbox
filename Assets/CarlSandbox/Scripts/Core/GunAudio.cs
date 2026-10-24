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
            // the recorded families, and the ones made from them: the 5.45 and 5.56 cracks (smaller, faster rounds:
            // the AK's report pitched up), the PKM's full-power 7.62x54R (the magnum rifle's, pitched down a touch)
            var fams = new (string fam, string from, float pitch)[]
            {
                ("gun_ak", "gun_ak", 1f), ("gun_glock", "gun_glock", 1f), ("gun_awp", "gun_awp", 1f), ("gun_50", "gun_50", 1f), ("gun_shotgun", "gun_shotgun", 1f),
                ("gun_545", "gun_ak", 1.07f), ("gun_556", "gun_ak", 1.12f), ("gun_pkm", "gun_awp", 0.94f),
            };
            foreach (var (fam, from, pitch) in fams)
            {
                if (!groups.TryGetValue(from, out var src) || src.Count == 0) continue;
                bool pistol = fam == "gun_glock";
                for (int i = 0; i < src.Count; i++)
                {
                    var x = Pitch(Mono(src[i]), pitch);
                    if (x == null) continue;
                    Add(groups, fam + "_full", Make(fam + "_full_" + i, Full(x, fam, rng), Rate));
                    if (i < 4) Add(groups, fam + "_supp", Make(fam + "_supp_" + i, Suppressed(x, fam, rng), Rate));
                }
                for (int i = 0; i < src.Count && i < 2; i++)
                {
                    var x = Pitch(Mono(src[i]), pitch);
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
                Add(groups, "gear_rattle", Make("gear_rattle_" + i, Rattle(rng), Rate));
            }
            if (groups.TryGetValue("casing", out var cas))
                for (int i = 0; i < cas.Count; i++) { var x = Mono(cas[i]); if (x != null) Add(groups, "casing_dirt", Make("casing_dirt_" + i, Dirt(x), Rate)); }
        }

        /// <summary>Played faster or slower (a smaller or bigger round's report from the same recording).</summary>
        static float[] Pitch(float[] x, float k)
        {
            if (x == null || Mathf.Abs(k - 1f) < 0.001f) return x;
            int n = Mathf.Max(1, Mathf.FloorToInt((x.Length - 1) / k));
            var y = new float[n];
            for (int i = 0; i < n; i++) { float p = i * k; int a = (int)p; float f = p - a; y[i] = x[a] * (1f - f) + x[Mathf.Min(a + 1, x.Length - 1)] * f; }
            return y;
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

        // ------------------------------------------------------------ the report
        //
        // What the recordings lacked (measured): each is one sharp spike - peak 15-20 dB over its own level a
        // moment later - with next to nothing under 150 Hz and gone in a fifth of a second, so even at full volume
        // it reads as a cap gun. A real rifle or pistol is 155-165 dB at your ear: broadband (the blast is
        // strongest at 0.5-1 kHz but runs up past 20 kHz), a pressure wave you feel, and it rings on. The .50's
        // recordings are the opposite - all boom, no crack. So: the crack sharpened, a boom put under it (lower
        // and longer the bigger the gun), the whole thing compressed so it holds its loudness instead of being a
        // spike, and a soft limit on top so it is loud without distorting.

        struct Voice { public float boomHz, boomLow, boomDecay, boom, crack, crackHz, keepLow, rms; }

        static Voice VoiceOf(string fam)
        {
            switch (fam)
            {
                case "gun_glock":   return new Voice { boomHz = 260f, boomLow = 120f, boomDecay = 0.04f, boom = 0.22f, crack = 0.9f, crackHz = 2200f, keepLow = 1f, rms = -12f };
                case "gun_awp":     return new Voice { boomHz = 170f, boomLow = 50f, boomDecay = 0.14f, boom = 0.38f, crack = 1.2f, crackHz = 2300f, keepLow = 1f, rms = -8f };
                case "gun_50":      return new Voice { boomHz = 170f, boomLow = 55f, boomDecay = 0.12f, boom = 0.12f, crack = 1.8f, crackHz = 1600f, keepLow = 0.35f, rms = -9f };
                case "gun_pkm":     return new Voice { boomHz = 180f, boomLow = 58f, boomDecay = 0.11f, boom = 0.32f, crack = 1.15f, crackHz = 2400f, keepLow = 1f, rms = -8.5f };
                case "gun_shotgun": return new Voice { boomHz = 180f, boomLow = 55f, boomDecay = 0.13f, boom = 0.45f, crack = 1.1f, crackHz = 2000f, keepLow = 1f, rms = -8.5f };
                default:            return new Voice { boomHz = 220f, boomLow = 85f, boomDecay = 0.06f, boom = 0.26f, crack = 0.9f, crackHz = 2500f, keepLow = 1f, rms = -10.5f };   // rifles
            }
        }

        static int Onset(float[] x)
        {
            float m = 1e-6f; foreach (var v in x) m = Mathf.Max(m, Mathf.Abs(v));
            for (int i = 0; i < x.Length; i++) if (Mathf.Abs(x[i]) > m * 0.25f) return i;
            return 0;
        }

        /// <summary>A quick drop in pitch with a fast attack: the pressure wave of the blast.</summary>
        static void AddBoom(float[] y, int at, float hi, float lo, float decay, float gain, System.Random rng)
        {
            float ph = 0f, lp = 0f;
            int n = Mathf.Min(y.Length - at, Mathf.RoundToInt((decay * 7f + 0.02f) * Rate));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = lo + (hi - lo) * Mathf.Exp(-t / 0.018f);
                ph += 2f * Mathf.PI * f / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp = lp * 0.93f + noise * 0.07f;
                float att = Mathf.Clamp01(t / 0.0012f);
                y[at + i] += (Mathf.Sin(ph) + lp * 2.2f) * gain * att * Mathf.Exp(-t / decay);
            }
        }

        /// <summary>Noise burst at the front of the shot: the crack (bright, very short).</summary>
        static void AddCrack(float[] y, int at, float hz, float gain, float decay, System.Random rng)
        {
            float lp = 0f, a = Mathf.Exp(-2f * Mathf.PI * hz / Rate);
            int n = Mathf.Min(y.Length - at, Mathf.RoundToInt(decay * 6f * Rate));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp = (1f - a) * noise + a * lp;
                y[at + i] += (noise - lp) * gain * Mathf.Exp(-t / decay);
            }
        }

        /// <summary>
        /// Level held up after the peak (a fast compressor: instant attack, ~70 ms release, 4:1 over the
        /// threshold), brought to a set loudness, then a soft knee into the top so nothing hard-clips.
        /// </summary>
        static float[] Loud(float[] y, float rmsDb, int onset, float window = 0.1f)
        {
            float m = 1e-6f; foreach (var v in y) m = Mathf.Max(m, Mathf.Abs(v));
            for (int i = 0; i < y.Length; i++) y[i] /= m;
            float thr = 0.18f, rel = Mathf.Exp(-1f / (0.07f * Rate)), env = 0f, g = 1f, gRel = Mathf.Exp(-1f / (0.004f * Rate));
            for (int i = 0; i < y.Length; i++)
            {
                float a = Mathf.Abs(y[i]);
                env = a > env ? a : env * rel + a * (1f - rel);
                float want = env > thr ? Mathf.Pow(thr / env, 0.75f) : 1f;
                g = want < g ? want : g * gRel + want * (1f - gRel);
                y[i] *= g;
            }
            // to the loudness wanted over the body of the shot
            int n = Mathf.Min(y.Length - onset, Mathf.RoundToInt(window * Rate));
            double sum = 0; for (int i = 0; i < n; i++) sum += y[onset + i] * y[onset + i];
            float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, n))) + 1e-6f;
            float k = Mathf.Pow(10f, rmsDb / 20f) / rms;
            for (int i = 0; i < y.Length; i++) y[i] *= k;
            return SoftKnee(y);
        }

        /// <summary>A soft knee from 0.6 up into 0.98: louder without the harsh edge of clipping.</summary>
        static float[] SoftKnee(float[] y)
        {
            for (int i = 0; i < y.Length; i++)
            {
                float v = y[i], av = Mathf.Abs(v);
                if (av > 0.6f) y[i] = Mathf.Sign(v) * (0.6f + 0.38f * (float)System.Math.Tanh((av - 0.6f) / 0.38f));
            }
            return y;
        }

        /// <summary>The full report: the recording with its crack sharpened, the blast's boom under it, held loud.</summary>
        static float[] Full(float[] x, string fam, System.Random rng)
        {
            var vo = VoiceOf(fam);
            int len = Mathf.Min(x.Length, Mathf.RoundToInt(1.4f * Rate));
            var y = new float[len];
            System.Array.Copy(x, y, len);
            int on = Onset(y);
            float m = 1e-6f; foreach (var v in y) m = Mathf.Max(m, Mathf.Abs(v));
            for (int i = 0; i < len; i++) y[i] /= m;
            // (the .50's recordings are nearly all sub-bass: thinned before the crack goes on)
            if (vo.keepLow < 1f) { var hp = HighPass(y, 90f, Rate); for (int i = 0; i < len; i++) y[i] = Mathf.Lerp(hp[i], y[i], vo.keepLow); }
            // the crack: the recording's own highs, pushed up for the first few milliseconds, plus a fresh burst
            var hi = HighPass(y, vo.crackHz, Rate);
            for (int i = on; i < len; i++) { float t = (i - on) / (float)Rate; y[i] += hi[i] * vo.crack * 1.6f * Mathf.Exp(-t / 0.012f); }
            AddCrack(y, on, vo.crackHz, vo.crack * 0.55f, 0.006f, rng);
            AddBoom(y, on, vo.boomHz, vo.boomLow, vo.boomDecay, vo.boom, rng);
            y = Loud(y, vo.rms, on);
            // and the crack again on top, after the squeeze, so the boom can't bury it (a big round has both)
            AddCrack(y, on, vo.crackHz * 1.2f, 0.32f * vo.crack, 0.007f, rng);
            return FadeEnd(SoftKnee(y), Rate, 0.08f);
        }

        /// <summary>
        /// A suppressed shot as it really is - not the film "pfft". At your ear a suppressed rifle is still about
        /// 135-145 dB (measured: the can takes 15-25 dB off a metre out, only 6-9 dB at the shooter's right ear),
        /// so it is a hard, flat "whack": the low boom of the blast mostly gone, the top end softened, cut much
        /// shorter, with gas spitting out of the can. A rifle round is still supersonic, so its crack is still
        /// there (the can can't touch that). A 9 mm is quieter - "thwap" - and its slide is loud by comparison.
        /// </summary>
        static float[] Suppressed(float[] x, string fam, System.Random rng)
        {
            bool pistol = fam == "gun_glock";
            var vo = VoiceOf(fam);
            int len = Mathf.Min(x.Length, Mathf.RoundToInt(0.6f * Rate));
            var y = new float[len];
            System.Array.Copy(x, y, len);
            int on = Onset(y);
            float m = 1e-6f; foreach (var v in y) m = Mathf.Max(m, Mathf.Abs(v));
            for (int i = 0; i < len; i++) y[i] /= m;
            // the can: the blast's low end mostly gone, the top softened (not removed), and much shorter
            y = HighPass(y, pistol ? 260f : 200f, Rate);
            y = LowPass(y, pistol ? 3200f : 4200f, Rate, 1);
            for (int i = on; i < len; i++) { float t = (i - on) / (float)Rate; y[i] *= t < 0.006f ? 1f : Mathf.Exp(-(t - 0.006f) / (pistol ? 0.045f : 0.07f)); }
            // a little body left in it: a short, higher thump (the gas still coming out, slowed in the baffles)
            AddBoom(y, on, vo.boomHz * 1.4f, vo.boomLow * 1.6f, vo.boomDecay * 0.35f, vo.boom * 0.35f, rng);
            // gas spitting out of the can: a short sizzle
            AddCrack(y, on + Mathf.RoundToInt(0.0015f * Rate), 2500f, 0.35f, 0.018f, rng);
            // the round leaving at rifle speed: its sonic crack, untouched by the can
            if (!pistol && fam != "gun_shotgun")
            {
                int c0 = on + Mathf.RoundToInt(0.0025f * Rate), w = Mathf.RoundToInt(0.0004f * Rate);
                for (int i = 0; i < w && c0 + i < len; i++) y[c0 + i] += 0.6f * (1f - 2f * i / (float)w);
                AddCrack(y, c0, 3500f, 0.45f, 0.004f, rng);
            }
            // and the action, loud by comparison
            var mech = Mech(rng, pistol);
            int at = on + Mathf.RoundToInt(0.008f * Rate);
            for (int i = 0; i < mech.Length && at + i < len; i++) y[at + i] += mech[i] * (pistol ? 0.5f : 0.4f);
            // only about 5 dB under the full report (7 for the pistol): measured at the shooter's ear a suppressed
            // rifle is still well over 140 dB - a loud, hard gunshot, just without the boom
            return FadeEnd(Loud(y, vo.rms - (pistol ? 7f : 5f), on), Rate, 0.04f);
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

        /// <summary>A plate carrier on the move: the canvas rustling, plates knocking, buckles and mags clinking.</summary>
        static float[] Rattle(System.Random rng)
        {
            int n = Mathf.RoundToInt(0.32f * Rate);
            var y = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp = lp * 0.82f + noise * 0.18f; lp2 = lp2 * 0.5f + lp * 0.5f;
                y[i] = (lp - lp2) * 1.6f * Mathf.Sin(Mathf.PI * t / 0.32f);               // a swish of cloth
            }
            // a dull knock (the plate settling on the step) and a few small clinks
            int k0 = Mathf.RoundToInt((0.02f + 0.03f * (float)rng.NextDouble()) * Rate);
            for (int i = 0; i < Rate / 25 && k0 + i < n; i++) { float t = i / (float)Rate; y[k0 + i] += Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.7f * Mathf.Exp(-t / 0.012f); }
            // (dull plastic and nylon ticks, not ringing metal: the bright pings sounded like breaking glass)
            for (int c = 0; c < 1 + rng.Next(2); c++)
            {
                int at = Mathf.RoundToInt((0.04f + 0.2f * (float)rng.NextDouble()) * Rate);
                float f = 700f + 600f * (float)rng.NextDouble(), g = 0.12f + 0.12f * (float)rng.NextDouble();
                for (int i = 0; i < Rate / 80 && at + i < n; i++) { float t = i / (float)Rate; y[at + i] += Mathf.Sin(2f * Mathf.PI * f * t) * g * Mathf.Exp(-t / 0.0018f); }
            }
            return FadeEnd(Normalize(y, 0.7f), Rate, 0.03f);
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
