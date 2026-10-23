using UnityEngine;

namespace Carl
{
    /// <summary>Player-facing settings (Esc menu). Saved between sessions.</summary>
    public static class GameSettings
    {
        /// <summary>0 = potato, 1 = high resolution blood simulation.</summary>
        public static float BloodQuality = 0.66f;
        /// <summary>0 = no blood at all, 0.5 = realistic, 1 = unlimited.</summary>
        public static float BloodAmount = 0.5f;
        public static bool InfiniteAmmo;
        public static float MouseSensitivity = 1f;
        public static float Volume = 0.8f;
        /// <summary>The Earls' screams, moans and begging. Off = silent dummies.</summary>
        public static bool DummyVoices = true;
        /// <summary>Loudness of the Earls' voices, on top of the master volume.</summary>
        public static float VoiceVolume = 1f;
        /// <summary>The crosshair in the middle of the screen (off: aim with your sights, or by feel).</summary>
        public static bool Crosshair = true;
        /// <summary>Graphics: 0 low, 1 medium, 2 high (render scale, shadow distance, detail).</summary>
        public static int Graphics = 0;
        /// <summary>Time of day for the next map: 0 day, 1 dusk, 2 night.</summary>
        public static int TimeOfDay = 0;
        /// <summary>Earls on the maps (off: an empty map to wander).</summary>
        public static bool SpawnNpcs = true;
        /// <summary>TRENCHES: false = you hold your trench against their assault, true = you assault theirs.</summary>
        public static bool TrenchAttack = false;

        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            BloodQuality = PlayerPrefs.GetFloat("earl.bloodQuality", 0.66f);
            BloodAmount = PlayerPrefs.GetFloat("earl.bloodAmount", 0.5f);
            InfiniteAmmo = PlayerPrefs.GetInt("earl.infiniteAmmo", 0) == 1;
            MouseSensitivity = PlayerPrefs.GetFloat("earl.sens", 1f);
            Volume = PlayerPrefs.GetFloat("earl.volume", 0.8f);
            DummyVoices = PlayerPrefs.GetInt("earl.voices", 1) == 1;
            VoiceVolume = PlayerPrefs.GetFloat("earl.voiceVolume", 1f);
            Crosshair = PlayerPrefs.GetInt("earl.crosshair", 1) == 1;
            Graphics = PlayerPrefs.GetInt("earl.graphics", 0);
            TimeOfDay = PlayerPrefs.GetInt("earl.timeOfDay", 0);
            SpawnNpcs = PlayerPrefs.GetInt("earl.npcs", 1) == 1;
            TrenchAttack = PlayerPrefs.GetInt("earl.trenchAttack", 0) == 1;
            ApplyGraphics();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("earl.bloodQuality", BloodQuality);
            PlayerPrefs.SetFloat("earl.bloodAmount", BloodAmount);
            PlayerPrefs.SetInt("earl.infiniteAmmo", InfiniteAmmo ? 1 : 0);
            PlayerPrefs.SetFloat("earl.sens", MouseSensitivity);
            PlayerPrefs.SetFloat("earl.volume", Volume);
            PlayerPrefs.SetInt("earl.voices", DummyVoices ? 1 : 0);
            PlayerPrefs.SetFloat("earl.voiceVolume", VoiceVolume);
            PlayerPrefs.SetInt("earl.crosshair", Crosshair ? 1 : 0);
            PlayerPrefs.SetInt("earl.graphics", Graphics);
            PlayerPrefs.SetInt("earl.timeOfDay", TimeOfDay);
            PlayerPrefs.SetInt("earl.npcs", SpawnNpcs ? 1 : 0);
            PlayerPrefs.SetInt("earl.trenchAttack", TrenchAttack ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Puts the graphics setting into effect: the render pipeline's resolution and shadows, level of detail.</summary>
        public static void ApplyGraphics()
        {
            Graphics = Mathf.Clamp(Graphics, 0, 2);
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Graphics == 0 ? 0.75f : Graphics == 1 ? 0.9f : 1f;
                urp.shadowDistance = Graphics == 0 ? 25f : Graphics == 1 ? 40f : 60f;
                urp.msaaSampleCount = 1;
            }
            QualitySettings.lodBias = Graphics == 0 ? 0.7f : Graphics == 1 ? 1f : 2f;
            QualitySettings.anisotropicFiltering = Graphics == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
            QualitySettings.globalTextureMipmapLimit = Graphics == 0 ? 1 : 0;   // half-size textures on low
        }

        /// <summary>World blood resolution, pixels per metre.</summary>
        public static int WorldBloodRes => QualityStep(new[] { 96, 160, 256, 384, 512 });
        /// <summary>Skin blood canvas resolution per body part.</summary>
        public static int BodyBloodRes => QualityStep(new[] { 64, 128, 256, 384, 512 });

        static int QualityStep(int[] steps)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(BloodQuality * (steps.Length - 1)), 0, steps.Length - 1);
            return steps[i];
        }

        public static string QualityName
        {
            get
            {
                string[] n = { "POTATO", "LOW", "MEDIUM", "HIGH", "ULTRA" };
                return n[Mathf.Clamp(Mathf.RoundToInt(BloodQuality * 4f), 0, 4)];
            }
        }

        /// <summary>How much blood comes out (0 = none, 1 = realistic, 3 at "unlimited").</summary>
        public static float BloodMult => BloodAmount >= 0.99f ? 3f : BloodAmount * 2f;
        /// <summary>Unlimited: wounds never run dry.</summary>
        public static bool UnlimitedBlood => BloodAmount >= 0.99f;
        public static bool NoBlood => BloodAmount <= 0.01f;

        public static string AmountName =>
            NoBlood ? "NONE" : UnlimitedBlood ? "UNLIMITED" : $"{Mathf.RoundToInt(BloodMult * 100f)}%";
    }
}
