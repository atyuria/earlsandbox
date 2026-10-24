using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A limiter on everything you hear (it sits on the listener, after the mix): the guns are loud now, and a full
    /// auto burst with grenades going off round you would add up past full scale and hard-clip - harsh, ugly,
    /// painful. This turns the whole mix down for a moment instead (instant on, ~150 ms back up), with a soft knee
    /// at the very top, so it stays loud without ever distorting.
    /// </summary>
    public class MasterLimiter : MonoBehaviour
    {
        const float Ceiling = 0.89f;     // about -1 dB
        float gain = 1f, release;
        public static float Reduction;   // (the deepest it went, for the playtest)

        // ---- a blast close by: your ears ring, and right next to it you go half deaf for a while
        /// <summary>0..1: the ringing (a high whine that dies away over many seconds).</summary>
        public static float Ring;
        /// <summary>0..1: deafened (everything muffled and far away, coming back over a few seconds).</summary>
        public static float Deaf;
        static volatile float ringA, deafA;
        int rate = 48000;
        double phase1, phase2;
        float lp0, lp1;

        void Awake()
        {
            rate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            release = Mathf.Exp(-1f / (0.15f * rate));
        }

        /// <summary>An explosion at pos, about power times a hand grenade: how hard it hits your ears.</summary>
        public static void Blast(Vector3 pos, float power = 1f)
        {
            Ensure();
            float d = Vector3.Distance(Sfx.Ears, pos) / Mathf.Sqrt(Mathf.Max(0.2f, power));
            float ring = Mathf.Clamp01((9f - d) / 7.5f), deaf = Mathf.Clamp01((4.5f - d) / 3.5f);
            Ring = Mathf.Max(Ring, ring); Deaf = Mathf.Max(Deaf, deaf);
        }

        void Update()
        {
            // the deafness lifts over ~5 s, the ringing over ~10 (slower at first when it was bad)
            float dt = Time.unscaledDeltaTime;
            Deaf = Mathf.Max(0f, Deaf - dt / 5f);
            Ring = Mathf.Max(0f, Ring - dt / (6f + 6f * Ring));
            ringA = Ring; deafA = Deaf;
        }

        /// <summary>Put one on whatever is listening (it changes: the menu camera, your eyes).</summary>
        public static void Ensure()
        {
            var l = FindListener();
            if (l != null && l.GetComponent<MasterLimiter>() == null) l.gameObject.AddComponent<MasterLimiter>();
        }

        static AudioListener cached;
        static AudioListener FindListener()
        {
            if (cached == null || !cached.isActiveAndEnabled) cached = FindAnyObjectByType<AudioListener>();
            return cached;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float deaf = deafA, ring = ringA;
            // muffled: a low-pass sliding down to a few hundred Hz and everything quieter
            float cut = Mathf.Lerp(18000f, 350f, Mathf.Sqrt(deaf));
            float a = Mathf.Exp(-2f * Mathf.PI * cut / rate);
            float g = Mathf.Lerp(1f, 0.12f, deaf);
            for (int i = 0; i < data.Length; i += channels)
            {
                if (deaf > 0.001f)
                    for (int c = 0; c < channels; c++)
                    {
                        float v = data[i + c];
                        if (c == 0) { lp0 = (1f - a) * v + a * lp0; v = lp0; } else { lp1 = (1f - a) * v + a * lp1; v = lp1; }
                        data[i + c] = v * g;
                    }
                if (ring > 0.001f)
                {
                    // the whine: two close high tones beating against each other
                    phase1 += 2.0 * System.Math.PI * 3900.0 / rate; phase2 += 2.0 * System.Math.PI * 4140.0 / rate;
                    float t = (float)(System.Math.Sin(phase1) * 0.6 + System.Math.Sin(phase2) * 0.4) * 0.07f * ring;
                    for (int c = 0; c < channels; c++) data[i + c] += t;
                }
                float peak = 0f;
                for (int c = 0; c < channels; c++) peak = Mathf.Max(peak, Mathf.Abs(data[i + c]));
                float want = peak > Ceiling ? Ceiling / peak : 1f;
                gain = want < gain ? want : gain * release + (1f - release);
                if (1f - gain > Reduction) Reduction = 1f - gain;
                for (int c = 0; c < channels; c++)
                {
                    float v = data[i + c] * gain, av = Mathf.Abs(v);
                    if (av > 0.8f) v = Mathf.Sign(v) * (0.8f + 0.19f * (float)System.Math.Tanh((av - 0.8f) / 0.19f));
                    data[i + c] = v;
                }
            }
        }
    }
}
