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
        /// <summary>You can't be hurt: rounds, blades, blasts, falls and fire do nothing to you.</summary>
        public static bool Invincible;
        public static float MouseSensitivity = 1f;
        /// <summary>How well the Earls shoot, 0 arcade .. 1 realistic: your side's and the enemy's.</summary>
        public static float FriendlySkill = 0.6f, EnemySkill = 0.6f;
        public static string SkillName(float s) => s < 0.2f ? "ARCADE" : s < 0.4f ? "CASUAL" : s < 0.6f ? "TRAINED" : s < 0.8f ? "VETERAN" : "REALISTIC";
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
        /// <summary>Your army in the trenches: 0 Ukraine, 1 Russia (the other side is the enemy).</summary>
        public static int Side = 0;
        /// <summary>The houses map: 0 a quiet street, 1 HOOD (gang war), 2 WAR (Ukraine v Russia on a shelled street).</summary>
        public static int HouseMode = 0;
        public static bool HouseAttack = true;
        /// <summary>Your gang in HOOD: 0 Crips (blue), 1 Bloods (red).</summary>
        public static int Gang = 0;

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
            Invincible = PlayerPrefs.GetInt("earl.invincible", 0) == 1;
            MouseSensitivity = PlayerPrefs.GetFloat("earl.sens", 1f);
            FriendlySkill = Mathf.Clamp01(PlayerPrefs.GetFloat("earl.skillF", 0.6f));
            EnemySkill = Mathf.Clamp01(PlayerPrefs.GetFloat("earl.skillE", 0.6f));
            Volume = PlayerPrefs.GetFloat("earl.volume", 0.8f);
            DummyVoices = PlayerPrefs.GetInt("earl.voices", 1) == 1;
            VoiceVolume = PlayerPrefs.GetFloat("earl.voiceVolume", 1f);
            Crosshair = PlayerPrefs.GetInt("earl.crosshair", 1) == 1;
            Graphics = PlayerPrefs.GetInt("earl.graphics", 0);
            TimeOfDay = PlayerPrefs.GetInt("earl.timeOfDay", 0);
            SpawnNpcs = PlayerPrefs.GetInt("earl.npcs", 1) == 1;
            TrenchAttack = PlayerPrefs.GetInt("earl.trenchAttack", 0) == 1;
            Side = Mathf.Clamp(PlayerPrefs.GetInt("earl.side", 0), 0, 1);
            HouseMode = Mathf.Clamp(PlayerPrefs.GetInt("earl.houseMode", 0), 0, 2);
            HouseAttack = PlayerPrefs.GetInt("earl.houseAttack", 1) == 1;
            Gang = Mathf.Clamp(PlayerPrefs.GetInt("earl.gang", 0), 0, 1);
            ApplyGraphics();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("earl.bloodQuality", BloodQuality);
            PlayerPrefs.SetFloat("earl.bloodAmount", BloodAmount);
            PlayerPrefs.SetInt("earl.infiniteAmmo", InfiniteAmmo ? 1 : 0);
            PlayerPrefs.SetInt("earl.invincible", Invincible ? 1 : 0);
            PlayerPrefs.SetFloat("earl.sens", MouseSensitivity);
            PlayerPrefs.SetFloat("earl.skillF", FriendlySkill);
            PlayerPrefs.SetFloat("earl.skillE", EnemySkill);
            PlayerPrefs.SetFloat("earl.volume", Volume);
            PlayerPrefs.SetInt("earl.voices", DummyVoices ? 1 : 0);
            PlayerPrefs.SetFloat("earl.voiceVolume", VoiceVolume);
            PlayerPrefs.SetInt("earl.crosshair", Crosshair ? 1 : 0);
            PlayerPrefs.SetInt("earl.graphics", Graphics);
            PlayerPrefs.SetInt("earl.timeOfDay", TimeOfDay);
            PlayerPrefs.SetInt("earl.npcs", SpawnNpcs ? 1 : 0);
            PlayerPrefs.SetInt("earl.trenchAttack", TrenchAttack ? 1 : 0);
            PlayerPrefs.SetInt("earl.side", Side);
            PlayerPrefs.SetInt("earl.houseMode", HouseMode);
            PlayerPrefs.SetInt("earl.houseAttack", HouseAttack ? 1 : 0);
            PlayerPrefs.SetInt("earl.gang", Gang);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// How often the physics runs: 100 Hz on every setting. (Slower steps were tried on low and medium: the
        /// bodies are tuned at 100 Hz, and below it a sprinting body folds forward until its head is at its waist,
        /// and the guns ring in the hands - so the savings on low come from elsewhere.)
        /// </summary>
        public static float PhysicsStep => DbgStep > 0f ? DbgStep : 0.01f;
        /// <summary>For the playtests: a fixed physics step whatever the setting (0 = the setting's own).</summary>
        public static float DbgStep;

        /// <summary>
        /// Puts the graphics setting into effect.
        ///   LOW (anything): 60 Hz physics, 75% resolution, one shadow cascade at 1024 over 25 m with hard edges,
        ///     no lamp shadows, no ambient occlusion, no HDR, half-size textures, bodies stop casting shadows
        ///     past 12 m, blood painted at a lower resolution.
        ///   MEDIUM (most): 80 Hz physics, 90% resolution, two cascades at 2048 over 40 m, soft (low) shadows,
        ///     lamp shadows at 1024, ambient occlusion, bodies' shadows out to 25 m.
        ///   HIGH (a 40-series card): 100 Hz physics, full resolution, four cascades at 2048 over 60 m, soft
        ///     (high) shadows, lamp shadows at 2048, ambient occlusion, HDR, every shadow.
        /// The colour-buffer copy (nothing uses it) is off on all three.
        /// </summary>
        public static void ApplyGraphics()
        {
            Graphics = Mathf.Clamp(Graphics, 0, 2);
            Time.fixedDeltaTime = PhysicsStep * (Time.timeScale > 0f ? Time.timeScale : 1f);
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Graphics == 0 ? 0.75f : Graphics == 1 ? 0.9f : 1f;
                urp.shadowDistance = Graphics == 0 ? 25f : Graphics == 1 ? 40f : 60f;
                urp.shadowCascadeCount = Graphics == 0 ? 1 : Graphics == 1 ? 2 : 4;
                urp.mainLightShadowmapResolution = Graphics == 0 ? 1024 : 2048;
                urp.additionalLightsShadowmapResolution = Graphics == 0 ? 512 : Graphics == 1 ? 1024 : 2048;
                urp.msaaSampleCount = 1;
                urp.supportsHDR = Graphics == 2;
                urp.supportsCameraOpaqueTexture = false;
                SetPrivate(urp, "m_SoftShadowsSupported", Graphics > 0);
                SetPrivate(urp, "m_SoftShadowQuality", Graphics == 2 ? 3 : 1);          // high / low
                SetPrivate(urp, "m_AdditionalLightShadowsSupported", Graphics > 0);
                // ambient occlusion: a full-screen pass, off on low
                var datas = GetPrivate(urp, "m_RendererDataList") as UnityEngine.Rendering.Universal.ScriptableRendererData[];
                if (datas != null)
                    foreach (var d in datas)
                        if (d != null)
                            foreach (var f in d.rendererFeatures)
                                if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) f.SetActive(Graphics > 0);
            }
            QualitySettings.lodBias = Graphics == 0 ? 0.7f : Graphics == 1 ? 1f : 2f;
            QualitySettings.anisotropicFiltering = Graphics == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
            QualitySettings.globalTextureMipmapLimit = Graphics == 0 ? 1 : 0;   // half-size textures on low
            QualitySettings.shadowCascades = Graphics == 0 ? 1 : Graphics == 1 ? 2 : 4;
            BodyLod.Reset();
        }

        /// <summary>How far away a body still casts a shadow (its 40-odd pieces are drawn again for each cascade).</summary>
        public static float BodyShadowRange => Graphics == 0 ? 12f : Graphics == 1 ? 25f : 60f;

        static System.Reflection.FieldInfo Field(object o, string name) =>
            o.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        static object GetPrivate(object o, string name) => Field(o, name)?.GetValue(o);

        static void SetPrivate(object o, string name, object v)
        {
            var f = Field(o, name);
            if (f == null) return;
            if (f.FieldType.IsEnum) v = System.Enum.ToObject(f.FieldType, v);
            f.SetValue(o, v);
        }

        /// <summary>World blood resolution, pixels per metre.</summary>
        public static int WorldBloodRes => Mathf.Min(QualityStep(new[] { 96, 160, 256, 384, 512 }), Graphics == 0 ? 160 : Graphics == 1 ? 256 : 512);
        /// <summary>Skin blood canvas resolution per body part (capped by the graphics setting).</summary>
        public static int BodyBloodRes => Mathf.Min(QualityStep(new[] { 64, 128, 256, 384, 512 }), Graphics == 0 ? 128 : Graphics == 1 ? 256 : 512);

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
