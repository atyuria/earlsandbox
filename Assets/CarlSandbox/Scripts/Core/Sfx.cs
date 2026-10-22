using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Sound: guns, reloads, impacts, ricochets, explosions, glass, and the Earls' voices (screams, moans, sobs,
    /// choking on blood, begging, and the giggling of someone whose brain has been hit). Clips live in
    /// Resources/Sounds and are grouped by name (gun_ak_0, gun_ak_1 ... -> "gun_ak").
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        readonly Dictionary<string, List<AudioClip>> groups = new Dictionary<string, List<AudioClip>>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly List<AudioLowPassFilter> poolLp = new List<AudioLowPassFilter>();
        int next;
        struct Later { public float time; public string group; public Vector3 pos; public float vol, spatial, minDist, maxDist, jitter, cutoff, spread; }
        readonly List<Later> delayed = new List<Later>();

        public static void Create()
        {
            if (I != null) return;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Sfx>();
            I.Load();
        }

        void Load()
        {
            foreach (var c in Resources.LoadAll<AudioClip>("Sounds"))
            {
                string n = c.name;
                int u = n.LastIndexOf('_');
                string g = u > 0 && int.TryParse(n.Substring(u + 1), out _) ? n.Substring(0, u) : n;
                if (!groups.TryGetValue(g, out var l)) groups[g] = l = new List<AudioClip>();
                l.Add(c);
            }
            // the layered gun sounds (suppressed, echoes, far off, thump, action, flybys, cases in dirt)
            GunAudio.Build(groups);
            for (int i = 0; i < 48; i++)
            {
                var s = new GameObject("Src" + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.dopplerLevel = 0f;
                pool.Add(s);
                var lp = s.gameObject.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = 22000f; lp.lowpassResonanceQ = 1f;
                poolLp.Add(lp);
            }
        }

        public static AudioClip Clip(string group)
        {
            if (I == null || !I.groups.TryGetValue(group, out var l) || l.Count == 0) return null;
            return l[Random.Range(0, l.Count)];
        }

        /// <summary>One-shot at a point. spatial 0 = in your head (your own gun), 1 = fully 3D. cutoff: a low-pass
        /// (Hz) for distance or something in the way; spread: how much it comes from all round rather than a point.</summary>
        /// <summary>0 .. 1: how much your ears are covered (a visor down over the face): everything you hear is
        /// duller and quieter. Set by VisorView.</summary>
        public static float EarCover;

        public static void Play(string group, Vector3 pos, float volume = 1f, float spatial = 1f, float minDist = 2f, float pitchJitter = 0.06f, float maxDist = 120f, float cutoff = 22000f, float spread = 0f, bool inHead = false)
        {
            if (I == null) return;
            if (!inHead && EarCover > 0f) { volume *= Mathf.Lerp(1f, 0.4f, EarCover); cutoff = Mathf.Min(cutoff, Mathf.Lerp(22000f, 1400f, EarCover)); }
            var clip = Clip(group);
            if (clip == null) return;
            int k = I.next; I.next = (I.next + 1) % I.pool.Count;
            var s = I.pool[k];
            s.transform.position = pos;
            s.clip = clip;
            s.volume = volume * GameSettings.Volume;
            s.spatialBlend = spatial;
            s.minDistance = minDist;
            s.maxDistance = maxDist;
            s.spread = spread;
            s.rolloffMode = maxDist < 60f ? AudioRolloffMode.Linear : AudioRolloffMode.Logarithmic;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            I.poolLp[k].cutoffFrequency = cutoff;
            s.Play();
        }

        public static void PlayLater(float delay, string group, Vector3 pos, float volume = 1f, float spatial = 1f, float minDist = 2f, float maxDist = 120f, float pitchJitter = 0.06f, float cutoff = 22000f, float spread = 0f)
        {
            if (I == null) return;
            if (delay <= 0.005f) { Play(group, pos, volume, spatial, minDist, pitchJitter, maxDist, cutoff, spread); return; }
            I.delayed.Add(new Later { time = Time.time + delay, group = group, pos = pos, vol = volume, spatial = spatial, minDist = minDist, maxDist = maxDist, jitter = pitchJitter, cutoff = cutoff, spread = spread });
        }

        /// <summary>Where you hear from (your head).</summary>
        public static Vector3 Ears
        {
            get
            {
                if (I != null && (I.listener == null || !I.listener.isActiveAndEnabled)) I.listener = FindAnyObjectByType<AudioListener>();
                return I != null && I.listener != null ? I.listener.transform.position : Vector3.zero;
            }
        }

        /// <summary>Hard walls close round a point (a trench, a room, a corridor): the echo is a short slap, not a roll.</summary>
        public static bool Enclosed(Vector3 pos)
        {
            int hits = 0;
            Vector3 o = pos + Vector3.up * 0.2f;
            for (int k = 0; k < 8; k++)
            {
                Vector3 d = Quaternion.AngleAxis(k * 45f, Vector3.up) * Vector3.forward;
                if (Physics.Raycast(o, d, 5f, ~0, QueryTriggerInteraction.Ignore)) hits++;
            }
            bool roof = Physics.Raycast(o, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore);
            return hits >= 5 || roof && hits >= 3;
        }

        static string Family(string weapon) =>
            weapon == "AK-47" || weapon == "AR-15" ? "gun_ak" : weapon == "GLOCK 17" || weapon == "BERETTA M9" ? "gun_glock" : weapon == "AWP" ? "gun_awp" : weapon == ".50 CAL" ? "gun_50" : "gun_shotgun";

        /// <summary>
        /// A shot, as it reaches your ears: the report (late by its distance at the speed of sound, duller the further
        /// off), a thump and the action working when it's your own or right by you, the echo off what is round
        /// about it, and far off just the boom. A suppressor takes the crack off: a thud and a clack, little echo.
        /// </summary>
        public static void Gun(string weapon, Vector3 pos, bool suppressed = false)
        {
            if (weapon == "TASER") { Play("taser_pop", pos, 0.8f, 0.3f, 1f, 0.05f, 25f); return; }
            string g = Family(weapon);
            bool pistol = g == "gun_glock";
            float dist = Vector3.Distance(Ears, pos);
            bool own = dist < 1.6f;
            float spatial = own ? 0.25f : 1f;
            float delay = own ? 0f : dist / 343f;
            float dull = Mathf.Lerp(22000f, 2600f, Mathf.Clamp01(dist / 220f));
            string tail = g + (Enclosed(pos) ? "_tail_tight" : "_tail_open");
            float heavy = g == "gun_50" ? 1.3f : g == "gun_awp" ? 1.15f : g == "gun_shotgun" ? 1.1f : 1f;
            if (suppressed)
            {
                PlayLater(delay, g + "_supp", pos, own ? 0.8f : 1f, spatial, 2f, 80f, 0.05f, dull);
                if (own) Play(pistol ? "mech_pistol" : "mech_rifle", pos, 0.45f, 0f, 1f, 0.05f);
                PlayLater(delay + 0.02f, tail, pos, own ? 0.12f : 0.18f, own ? 0.3f : 0.8f, 4f, 70f, 0.05f, dull, 90f);
                return;
            }
            if (dist < 260f) PlayLater(delay, g, pos, 1f, spatial, own ? 3f : 8f, 450f, 0.04f, dull);
            if (own || dist < 8f) Play("thump", pos, (own ? 0.75f : 0.45f) * heavy, spatial, 3f, 0.08f, 30f);
            if (own) Play(pistol ? "mech_pistol" : "mech_rifle", pos, 0.22f, 0f, 1f, 0.05f);
            // the echo comes from everywhere round, not from the gun
            PlayLater(delay + 0.015f, tail, pos, (own ? 0.5f : 0.7f) * heavy, own ? 0.3f : 0.75f, 12f, 700f, 0.05f, own ? 22000f : Mathf.Lerp(22000f, 5000f, Mathf.Clamp01(dist / 300f)), 120f);
            if (dist > 70f) PlayLater(delay, g + "_far", pos, Mathf.Clamp01(1.3f - dist / 700f) * heavy, 1f, 40f, 1200f, 0.05f, 22000f, 60f);
            // (the cases make their own sound, as they land: Casing)
        }

        /// <summary>A round skipping off something hard: a ping and a falling whine.</summary>
        public static void Ricochet(Vector3 pos)
        {
            float dist = Vector3.Distance(Ears, pos);
            float delay = dist / 343f, dull = Mathf.Lerp(22000f, 3000f, Mathf.Clamp01(dist / 150f));
            PlayLater(delay, "impact_metal", pos, 0.35f, 1f, 2f, 60f, 0.2f, dull);
            PlayLater(delay, "ricochet", pos, 0.9f, 1f, 3f, 140f, 0.22f, dull);
        }

        /// <summary>A round going past your head: the crack of a supersonic one, the whine of a slow one.</summary>
        public static int FlyBys;   // (counted, for the playtest)
        public static void FlyBy(Vector3 at, float speed)
        {
            FlyBys++;
            // a round cracking past your head: a jolt of adrenaline
            if (PlayerAvatar.Body != null && !PlayerAvatar.Body.Dead) PlayerAvatar.Body.physio.AddStress(speed > 360f ? 0.14f : 0.08f);
            if (speed > 360f) Play("flyby_crack", at, 0.85f, 1f, 1.5f, 0.12f, 40f);
            else Play("flyby_whiz", at, 0.7f, 1f, 1.2f, 0.15f, 25f);
        }

        /// <summary>The noises of a reload, spread over its length.</summary>
        public static void Reload(string weapon, Vector3 pos, float length)
        {
            if (weapon == "MOSSBERG 590")
            {
                for (float tt = 0.1f; tt < length - 0.5f; tt += 0.45f) PlayLater(tt, "reload_shell", pos, 0.8f, 0.2f);
                PlayLater(length - 0.35f, "reload_pump", pos, 0.9f, 0.2f);
                return;
            }
            PlayLater(length * 0.12f, "reload_magout", pos, 0.8f, 0.2f);
            PlayLater(length * 0.55f, "reload_magin", pos, 0.9f, 0.2f);
            PlayLater(length * 0.82f, "reload_bolt", pos, 0.9f, 0.2f);
        }

        /// <summary>A round hitting the world: what it sounds like depends on what it hit.</summary>
        public static void Impact(Collider c, Vector3 pos)
        {
            string g = "impact_concrete";
            for (var tr = c != null ? c.transform : null; tr != null; tr = tr.parent)
            {
                string n = tr.name;
                if (n == "Car" || n.Contains("Lamp") || n.Contains("Fridge")) { g = "impact_metal"; break; }
                if (n == "Table" || n == "Chair" || n == "Shelf" || n == "Bed" || n.Contains("Fence") || n.Contains("Door") || n == "Sofa" || n == "Kitchen") { g = "impact_wood"; break; }
            }
            Play(g, pos, 0.8f, 1f, 1.5f);
        }

        void Update()
        {
            for (int i = delayed.Count - 1; i >= 0; i--)
                if (Time.time >= delayed[i].time)
                {
                    var d = delayed[i];
                    delayed.RemoveAt(i);
                    Play(d.group, d.pos, d.vol, d.spatial, d.minDist, d.jitter, d.maxDist, d.cutoff, d.spread);
                }
            UpdateVoices(Time.deltaTime);
        }

        // ============================================================ voices
        class Voice
        {
            public AudioSource src;
            public AudioLowPassFilter muffle;
            public float cooldown = 2f, lastPain, pitch, spokeDeath, vol = 1f;
            public bool wasDead;
        }
        readonly Dictionary<HumanBody, Voice> voices = new Dictionary<HumanBody, Voice>();
        readonly List<HumanBody> gone = new List<HumanBody>();
        AudioListener listener;

        /// <summary>A voice carries this far (m); past it the person can't be heard at all.</summary>
        const float VoiceRange = 40f;
        static AnimationCurve voiceFalloff;

        /// <summary>
        /// Loudness against distance (as a fraction of VoiceRange): full up close, roughly inverse-square after
        /// a couple of metres, and properly silent at the edge, unlike Unity's logarithmic curve that never
        /// reaches zero.
        /// </summary>
        static AnimationCurve VoiceFalloff()
        {
            if (voiceFalloff != null) return voiceFalloff;
            var keys = new List<Keyframe>();
            const float near = 1.5f / VoiceRange;
            keys.Add(new Keyframe(0f, 1f));
            for (int i = 0; i <= 12; i++)
            {
                float x = Mathf.Lerp(near, 1f, i / 12f);
                float d = x * VoiceRange;
                float inv = 1.5f / d;                                      // inverse distance from the near radius
                float edge = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.6f, 1f, x));
                keys.Add(new Keyframe(x, Mathf.Clamp01(inv) * edge));
            }
            voiceFalloff = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < voiceFalloff.length; i++) voiceFalloff.SmoothTangents(i, 0f);
            return voiceFalloff;
        }

        void UpdateVoices(float dt)
        {
            gone.Clear();
            foreach (var kv in voices) if (kv.Key == null) gone.Add(kv.Key);
            foreach (var g in gone) voices.Remove(g);
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            foreach (var h in HumanBody.All)
            {
                if (h == null || h.physio == null || h.Head == null) continue;
                if (!voices.TryGetValue(h, out var v))
                {
                    var s = h.Head.gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false; s.spatialBlend = 1f; s.minDistance = 1.5f; s.maxDistance = VoiceRange;
                    s.rolloffMode = AudioRolloffMode.Custom;
                    s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, VoiceFalloff());
                    s.dopplerLevel = 0f; s.spread = 30f;
                    var lp = h.Head.gameObject.AddComponent<AudioLowPassFilter>();
                    lp.cutoffFrequency = 22000f;
                    v = new Voice { src = s, muffle = lp, pitch = Random.Range(0.9f, 1.1f), cooldown = Random.Range(1f, 3f) };
                    voices[h] = v;
                }
                Hear(h, v);
                Think(h, v, dt);
            }
        }

        /// <summary>Settings and distance: off means silent; far away means quieter (the curve) and duller.</summary>
        void Hear(HumanBody h, Voice v)
        {
            if (!GameSettings.DummyVoices)
            {
                if (v.src.isPlaying) v.src.Stop();
                return;
            }
            v.src.volume = v.vol * GameSettings.Volume * GameSettings.VoiceVolume;
            if (listener == null) return;
            // air soaks up the high end over distance: a scream across the map is a dull, distant cry
            float d = Vector3.Distance(listener.transform.position, h.Head.transform.position);
            float far = Mathf.InverseLerp(4f, VoiceRange, d);
            v.muffle.cutoffFrequency = Mathf.Min(Mathf.Lerp(22000f, 1800f, Mathf.Sqrt(far)), Mathf.Lerp(22000f, 1200f, EarCover));
            v.src.volume *= Mathf.Lerp(1f, 0.4f, EarCover);
        }

        void Say(Voice v, string group, float vol = 1f, float pitchMul = 1f)
        {
            if (!GameSettings.DummyVoices) return;
            var clip = Clip(group) ?? Clip("moan");
            if (clip == null) return;
            v.src.Stop();
            v.src.clip = clip;
            v.vol = vol;
            v.src.volume = vol * GameSettings.Volume * GameSettings.VoiceVolume;
            v.src.pitch = v.pitch * pitchMul * Random.Range(0.97f, 1.03f);
            v.src.Play();
            v.cooldown = clip.length + Random.Range(0.6f, 2.5f);
        }

        /// <summary>What a person in this state would be making noise about.</summary>
        void Think(HumanBody h, Voice v, float dt)
        {
            var p = h.physio;
            v.cooldown -= dt;
            float pain = p.EffectivePain;
            float spike = pain - v.lastPain;
            v.lastPain = Mathf.Lerp(v.lastPain, pain, dt * 2f);
            if (p.dead)
            {
                // one last wet breath
                if (!v.wasDead) { v.wasDead = true; if (Random.value < 0.5f) Say(v, p.airwayBlood > 0.2f ? "gurgle" : "moan", 0.6f, 0.95f); }
                return;
            }
            v.wasDead = false;
            bool talking = v.src.isPlaying;
            // a sudden new injury: an instant scream, cutting off whatever they were saying
            if (spike > 0.18f && p.consciousness > 0.35f && (!talking || v.src.time > 0.4f))
            {
                // a real person's first reaction: a sharp cry or grunt; a bad one makes them scream
                Say(v, spike > 0.4f && Random.value < 0.7f ? "scream" : "grunt", 1f, Random.Range(0.95f, 1.06f));
                v.lastPain = pain;
                return;
            }
            if (talking || v.cooldown > 0f) return;
            if (p.consciousness < 0.25f)
            {
                if (Random.value < 0.3f) Say(v, p.airwayBlood > 0.2f ? "gurgle" : "moan", 0.45f, 0.95f);
                else v.cooldown = Random.Range(2f, 5f);
                return;
            }
            // drowning in their own blood
            if (p.airwayBlood > 0.25f) { Say(v, Random.value < 0.6f ? "gurgle" : "cough", 0.9f); return; }
            // brain injured but alive: giggling, babbling nonsense
            if (p.brainTrauma > 0.3f && p.consciousness > 0.4f) { Say(v, Random.value < 0.6f ? "laugh" : "mad", 0.9f); return; }
            if (pain > 0.55f)
            {
                float r = Random.value;
                Say(v, r < 0.2f ? "scream" : r < 0.5f ? "moan" : r < 0.7f ? "grunt" : "beg", 1f);
                return;
            }
            if (pain > 0.2f || p.LossFrac > 0.25f)
            {
                float r = Random.value;
                Say(v, r < 0.45f ? "moan" : r < 0.65f ? "grunt" : "beg", 0.85f);
                return;
            }
            v.cooldown = Random.Range(2f, 4f);
        }
    }
}
