using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The FPV goggles losing the drone: the picture goes to analogue snow with a hiss for a moment ("NO SIGNAL"),
    /// then you're back in your own eyes.
    /// </summary>
    public class FpvStatic : MonoBehaviour
    {
        float left, length;
        Texture2D snow;
        AudioSource hiss;
        GUIStyle label;
        static AudioClip hissClip;

        public static void Show(float seconds)
        {
            var s = new GameObject("FpvStatic").AddComponent<FpvStatic>();
            s.left = s.length = seconds;
        }

        void Awake() { useGUILayout = false; }

        void Start()
        {
            snow = new Texture2D(192, 108, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            hiss = gameObject.AddComponent<AudioSource>();
            hiss.clip = HissClip(); hiss.loop = true; hiss.spatialBlend = 0f; hiss.volume = 0.35f * GameSettings.Volume;
            hiss.Play();
        }

        void Update()
        {
            left -= Time.unscaledDeltaTime;
            if (hiss != null) hiss.volume = 0.35f * GameSettings.Volume * Mathf.Clamp01(left / 0.3f);
            if (left <= 0f) Destroy(gameObject);
        }

        void OnDestroy() { if (snow != null) Destroy(snow); }

        /// <summary>Fills a texture with fresh snow: grey speckle, the odd bright streak; alpha sets how much shows.</summary>
        public static void Noise(Texture2D t, float alpha)
        {
            var px = t.GetPixels32();
            int w = t.width;
            for (int y = 0, i = 0; y < t.height; y++)
            {
                byte streak = (byte)(Random.value < 0.04f ? Random.Range(60, 140) : 0);
                for (int x = 0; x < w; x++, i++)
                {
                    byte v = (byte)Mathf.Min(255, Random.Range(0, 256) + streak);
                    px[i] = new Color32(v, v, v, (byte)(alpha * 255f));
                }
            }
            t.SetPixels32(px); t.Apply(false);
        }

        void OnGUI()
        {
            if (snow == null) return;
            GUI.depth = -90;
            float W = Screen.width, H = Screen.height;
            // full snow at first, the last few tenths of a second fade back to your own view
            float a = Mathf.Clamp01(left / 0.35f);
            if (Event.current.type == EventType.Repaint) Noise(snow, 1f);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(new Rect(0, 0, W, H), snow);
            label ??= new GUIStyle { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            if (left > length - 1.0f)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.75f * a);
                GUI.DrawTexture(new Rect(W * 0.5f - 110, H * 0.5f - 24, 220, 48), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (left > length - 1.0f) GUI.Label(new Rect(0, H * 0.5f - 20, W, 40), "NO SIGNAL", label);
            GUI.color = Color.white;
        }

        static AudioClip HissClip()
        {
            if (hissClip != null) return hissClip;
            int rate = 22050;
            var d = new float[rate];
            var rnd = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++) { float n = (float)rnd.NextDouble() * 2f - 1f; lp = Mathf.Lerp(lp, n, 0.6f); d[i] = lp * 0.5f; }
            hissClip = AudioClip.Create("FpvHiss", d.Length, 1, rate, false);
            hissClip.SetData(d, 0);
            return hissClip;
        }
    }
}
