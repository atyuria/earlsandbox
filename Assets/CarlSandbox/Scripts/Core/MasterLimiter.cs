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

        void Awake() => release = Mathf.Exp(-1f / (0.15f * Mathf.Max(8000, AudioSettings.outputSampleRate)));

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
            for (int i = 0; i < data.Length; i += channels)
            {
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
