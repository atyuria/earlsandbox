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
        int next;
        readonly List<(float time, string group, Vector3 pos, float vol, float spatial)> delayed = new List<(float, string, Vector3, float, float)>();

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
            for (int i = 0; i < 32; i++)
            {
                var s = new GameObject("Src" + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.dopplerLevel = 0f;
                pool.Add(s);
            }
        }

        public static AudioClip Clip(string group)
        {
            if (I == null || !I.groups.TryGetValue(group, out var l) || l.Count == 0) return null;
            return l[Random.Range(0, l.Count)];
        }

        /// <summary>One-shot at a point. spatial 0 = in your head (your own gun), 1 = fully 3D.</summary>
        public static void Play(string group, Vector3 pos, float volume = 1f, float spatial = 1f, float minDist = 2f, float pitchJitter = 0.06f, float maxDist = 120f)
        {
            if (I == null) return;
            var clip = Clip(group);
            if (clip == null) return;
            var s = I.pool[I.next]; I.next = (I.next + 1) % I.pool.Count;
            s.transform.position = pos;
            s.clip = clip;
            s.volume = volume * GameSettings.Volume;
            s.spatialBlend = spatial;
            s.minDistance = minDist;
            s.maxDistance = maxDist;
            s.rolloffMode = maxDist < 60f ? AudioRolloffMode.Linear : AudioRolloffMode.Logarithmic;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.Play();
        }

        public static void PlayLater(float delay, string group, Vector3 pos, float volume = 1f, float spatial = 1f)
        {
            if (I == null) return;
            I.delayed.Add((Time.time + delay, group, pos, volume, spatial));
        }

        public static void Gun(string weapon, Vector3 pos)
        {
            string g = weapon == "AK-47" ? "gun_ak" : weapon == "GLOCK 17" ? "gun_glock" : weapon == "AWP" ? "gun_awp" : weapon == ".50 CAL" ? "gun_50" : "gun_shotgun";
            Play(g, pos, 1f, 0.25f, 3f, 0.04f);
            PlayLater(Random.Range(0.35f, 0.6f), "casing", pos + Vector3.down * 1.4f, 0.5f, 0.6f);
        }

        /// <summary>The noises of a reload, spread over its length.</summary>
        public static void Reload(string weapon, Vector3 pos, float length)
        {
            if (weapon == "SHOTGUN")
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
                    Play(d.group, d.pos, d.vol, d.spatial);
                }
            UpdateVoices(Time.deltaTime);
        }

        // ============================================================ voices
        class Voice
        {
            public AudioSource src;
            public float cooldown = 2f, lastPain, pitch, spokeDeath;
            public bool wasDead;
        }
        readonly Dictionary<HumanBody, Voice> voices = new Dictionary<HumanBody, Voice>();
        readonly List<HumanBody> gone = new List<HumanBody>();

        void UpdateVoices(float dt)
        {
            gone.Clear();
            foreach (var kv in voices) if (kv.Key == null) gone.Add(kv.Key);
            foreach (var g in gone) voices.Remove(g);
            foreach (var h in HumanBody.All)
            {
                if (h == null || h.physio == null || h.Head == null) continue;
                if (!voices.TryGetValue(h, out var v))
                {
                    var s = h.Head.gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false; s.spatialBlend = 1f; s.minDistance = 1.2f; s.maxDistance = 45f;
                    s.rolloffMode = AudioRolloffMode.Logarithmic; s.dopplerLevel = 0f;
                    v = new Voice { src = s, pitch = Random.Range(0.9f, 1.1f), cooldown = Random.Range(1f, 3f) };
                    voices[h] = v;
                }
                Think(h, v, dt);
            }
        }

        void Say(Voice v, string group, float vol = 1f, float pitchMul = 1f)
        {
            var clip = Clip(group) ?? Clip("moan");
            if (clip == null) return;
            v.src.Stop();
            v.src.clip = clip;
            v.src.volume = vol * GameSettings.Volume;
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
