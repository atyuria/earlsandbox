using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Carl
{
    /// <summary>
    /// Dust and smoke that hang in the air and block the view: thrown off the earth by bullets, muzzle blast and
    /// grenades, and out of the barrel. Each puff spreads and thins as it ages and is carried off by the wind -
    /// slowly down in a trench, where the air hardly moves, quickly once it rises over the top. Sight lines
    /// through it (the Earls' eyes, <see cref="Transmit"/>) are cut by however much is in the way.
    /// </summary>
    public class Haze : MonoBehaviour
    {
        void Awake() { useGUILayout = false; }   // (its OnGUI draws directly: no layout pass needed)
        struct Cloud
        {
            public Vector3 p, v;
            public float r, dens, age, grow;
            public Color c;
            public bool smoke;
        }

        static Haze I;
        const int Max = 1400;
        readonly Cloud[] puffs = new Cloud[Max];
        int count;
        ParticleSystem ps;
        ParticleSystem.Particle[] parts = new ParticleSystem.Particle[Max];
        float gustSeed;

        /// <summary>Below this height the air is sheltered (inside a trench). Set by the map; -1000 = nowhere.</summary>
        public static float ShelterHeight = -1000f;
        /// <summary>The wind (m/s) out in the open.</summary>
        public static Vector3 Wind = new Vector3(1.6f, 0f, 0.7f);
        /// <summary>How thick the haze is right at the camera (0..1), for the screen tint.</summary>
        public static float AtCamera { get; private set; }
        public static int Count => I != null ? I.count : 0;
        /// <summary>Everything thrown up is this much thinner than it was first tuned (it was hiding too much).</summary>
        const float Thickness = 0.32f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; ShelterHeight = -1000f; AtCamera = 0f; }

        static Haze Get()
        {
            if (I != null) return I;
            var go = new GameObject("Haze");
            I = go.AddComponent<Haze>();
            I.Init();
            return I;
        }

        void Init()
        {
            gustSeed = Random.Range(0f, 100f);
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Max;
            main.playOnAwake = false;
            main.startLifetime = 1000f;
            main.simulationSpeed = 0f;          // moved here, not by the particle system
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var r = GetComponent<ParticleSystemRenderer>();
            var m = new Material(Mats.Find("Carl/Haze"));
            m.renderQueue = 3010;
            r.sharedMaterial = m;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 0.5f;   // (a puff right at the lens covering the screen four times over was most of the GPU in a firefight)
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.Center });
            ps.Play();
        }

        // ------------------------------------------------------------ making it

        /// <summary>One puff: centre, drift, radius (m), density (1 = you can't see through a metre of it), colour.</summary>
        public static void Puff(Vector3 pos, Vector3 vel, float radius, float density, Color c, bool smoke = false)
        {
            density *= Thickness;
            if (density < 0.01f) return;
            var h = Get();
            int i;
            if (h.count < Max) i = h.count++;
            else
            {
                // full: replace the thinnest
                i = 0; float lo = float.MaxValue;
                for (int k = 0; k < Max; k += 3) { float w = h.puffs[k].dens * h.puffs[k].r; if (w < lo) { lo = w; i = k; } }
            }
            h.puffs[i] = new Cloud { p = pos, v = vel, r = Mathf.Max(0.05f, radius), dens = Mathf.Min(density, 1.5f), c = c, smoke = smoke, grow = radius * 0.9f + 0.15f };
        }

        static readonly Color DirtCol = new Color(0.45f, 0.39f, 0.31f), DustCol = new Color(0.58f, 0.55f, 0.5f), SmokeCol = new Color(0.62f, 0.62f, 0.6f), DarkSmoke = new Color(0.2f, 0.19f, 0.18f);

        /// <summary>A bullet into earth: a decent cloud of dirt off the face it went into.</summary>
        public static void DirtHit(Vector3 pos, Vector3 normal, float power)
        {
            power = Mathf.Clamp(power, 0.3f, 2.5f);
            int n = 1 + Mathf.RoundToInt(power * 0.8f);
            for (int i = 0; i < n; i++)
                Puff(pos + normal * Random.Range(0.1f, 0.35f) + Random.insideUnitSphere * 0.12f, normal * Random.Range(0.3f, 1.1f) * power + Random.insideUnitSphere * 0.3f,
                    Random.Range(0.22f, 0.38f) * Mathf.Sqrt(power), Random.Range(0.35f, 0.6f), Tint(DirtCol));
        }

        /// <summary>A bullet into something hard: a little grey dust.</summary>
        public static void HardHit(Vector3 pos, Vector3 normal)
        {
            Puff(pos + normal * 0.12f, normal * 0.5f, 0.16f, 0.25f, Tint(DustCol));
        }

        /// <summary>The muzzle blast kicking dust off earth right beside the gun.</summary>
        public static void BlastDust(Vector3 at, Vector3 normal, Vector3 along, float k)
        {
            Puff(at + normal * 0.15f, (along * 1.2f + normal * 0.4f) * k, 0.22f + 0.25f * k, 0.25f + 0.35f * k, Tint(DirtCol));
        }

        /// <summary>Smoke out of the barrel: a wisp for a pistol, a proper puff for a rifle.</summary>
        public static void MuzzleSmoke(Vector3 muzzle, Vector3 dir, float power)
        {
            Puff(muzzle + dir * 0.25f, dir * (0.6f + power * 0.5f) + Vector3.up * 0.1f, 0.1f + 0.07f * power, 0.18f + 0.1f * power, Tint(SmokeCol), true);
        }

        static readonly RaycastHit[] rays = new RaycastHit[6];

        /// <summary>A grenade: a big dirty cloud off the floor and walls round it, and a smoke column.</summary>
        public static void Blast(Vector3 c)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f;
                Puff(c + d * Random.Range(0.2f, 1.2f), d * Random.Range(1f, 3f) + Vector3.up * 0.6f, Random.Range(0.7f, 1.3f), Random.Range(0.7f, 1f), Tint(DirtCol));
            }
            for (int i = 0; i < 4; i++)
                Puff(c + Vector3.up * Random.Range(0.5f, 2f), Vector3.up * Random.Range(0.6f, 1.4f) + Random.insideUnitSphere * 0.3f, Random.Range(0.6f, 1f), 0.7f, Tint(DarkSmoke), true);
            // every wall and floor within a few metres throws its own dirt
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                Vector3 dir = new Vector3(Mathf.Cos(a), k % 4 == 0 ? -0.6f : Random.Range(-0.2f, 0.25f), Mathf.Sin(a)).normalized;
                if (!Physics.Raycast(c + Vector3.up * 0.2f, dir, out var h, 4f, ~0, QueryTriggerInteraction.Ignore) || !Fx.IsDirt(h.collider)) continue;
                float near = 1f - h.distance / 4f;
                for (int j = 0; j < 2; j++)
                    Puff(h.point + h.normal * Random.Range(0.3f, 0.7f) + Vector3.up * Random.Range(0f, 0.6f), h.normal * Random.Range(0.6f, 1.6f) * near + Vector3.up * 0.4f,
                        Random.Range(0.6f, 1f), 0.5f + 0.4f * near, Tint(DirtCol));
            }
        }

        static Color Tint(Color c) { float s = Random.Range(0.9f, 1.08f); return new Color(c.r * s, c.g * s, c.b * s, 1f); }

        // ------------------------------------------------------------ what it does to seeing

        /// <summary>How much of the light gets from a to b through the haze (1 = clear, 0 = can't see a thing).</summary>
        public static float Transmit(Vector3 a, Vector3 b)
        {
            if (I == null || I.count == 0) return 1f;
            Vector3 d = b - a; float len = d.magnitude;
            if (len < 1e-3f) return 1f;
            d /= len;
            float tau = 0f;
            var P = I.puffs;
            for (int i = 0; i < I.count; i++)
            {
                Vector3 to = P[i].p - a;
                float t = Vector3.Dot(to, d);
                float r = P[i].r;
                if (t < -r || t > len + r) continue;
                float perp2 = to.sqrMagnitude - t * t;
                float r2 = r * r;
                if (perp2 >= r2) continue;
                // the chord through the puff (the soft falloff halves it on average)
                float chord = 2f * Mathf.Sqrt(r2 - perp2) * 0.55f;
                tau += P[i].dens * chord * 2.6f;
                if (tau > 6f) break;
            }
            return Mathf.Exp(-tau);
        }

        // ------------------------------------------------------------ drifting, spreading, thinning

        void Update()
        {
            if (WorldFreeze.On) { Push(); return; }
            float dt = Time.deltaTime;
            float t = Time.time + gustSeed;
            float gust = 0.55f + 0.9f * Mathf.PerlinNoise(t * 0.15f, 0.3f);
            Vector3 wind = Quaternion.Euler(0f, (Mathf.PerlinNoise(t * 0.05f, 4.1f) - 0.5f) * 50f, 0f) * Wind * gust;
            var cam = Camera.main;
            Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
            float atCam = 0f;
            for (int i = 0; i < count; i++)
            {
                ref Cloud q = ref puffs[i];
                q.age += dt;
                bool sheltered = q.p.y < ShelterHeight;
                // the air the puff sits in: still in a trench, the wind over the top (it rises out as it warms/spreads)
                Vector3 air = sheltered ? wind * 0.1f : wind;
                if (q.smoke) air += Vector3.up * (sheltered ? 0.12f : 0.3f);
                else air += Vector3.down * 0.04f;
                q.v = Vector3.Lerp(q.v, air, 1f - Mathf.Exp(-dt * (q.age < 0.6f ? 1.4f : 0.8f)));
                q.p += q.v * dt;
                // spreads out quickly at first, then slowly; spreading thins it
                float g = q.grow * dt / (1f + q.age * 0.6f);
                float r0 = q.r;
                q.r = Mathf.Min(q.r + g, sheltered ? 2.4f : 3.5f);
                q.dens *= r0 * r0 / (q.r * q.r) * 0.6f + 0.4f;
                // and it settles out / is carried away
                float life = sheltered ? (q.smoke ? 40f : 32f) : (q.smoke ? 14f : 11f);
                q.dens -= q.dens * dt / life + dt * 0.002f;
                if (q.dens < 0.012f) { puffs[i] = puffs[--count]; i--; continue; }
                float dc = (q.p - cp).magnitude;
                if (dc < q.r) atCam += q.dens * (1f - dc / q.r) * 0.5f;
            }
            AtCamera = Mathf.Lerp(AtCamera, Mathf.Clamp01(atCam), 1f - Mathf.Exp(-dt * 6f));
            Push();
        }

        void Push()
        {
            if (ps == null) return;
            for (int i = 0; i < count; i++)
            {
                ref Cloud q = ref puffs[i];
                var c = q.c;
                c.a = Mathf.Clamp01(q.dens * 0.9f);
                parts[i] = new ParticleSystem.Particle
                {
                    position = q.p,
                    startSize = q.r * 2.3f,
                    startColor = c,
                    remainingLifetime = 999f,
                    startLifetime = 1000f,
                    rotation = (i * 37) % 360,
                };
            }
            ps.SetParticles(parts, count);
        }

        /// <summary>Make sure the camera renders a depth texture (the puffs fade into walls with it).</summary>
        public static void PrepareCamera(Camera cam)
        {
            if (cam == null) return;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.requiresDepthOption = CameraOverrideOption.On;
        }

        /// <summary>Clear it all (a map change, the clear key).</summary>
        public static void Clear() { if (I != null) { I.count = 0; I.Push(); } }

        void OnGUI()
        {
            // in the thick of it: the screen goes brown-grey
            if (AtCamera < 0.02f || Event.current.type != EventType.Repaint) return;
            GUI.color = new Color(0.5f, 0.46f, 0.4f, Mathf.Min(0.6f, AtCamera * 0.6f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
