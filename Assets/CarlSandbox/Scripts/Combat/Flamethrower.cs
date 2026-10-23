using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The flamethrower's stream. Each tick (a tenth of a second of the trigger held) throws burning fuel out of the
    /// nozzle: it carries about five metres, slowing and sagging as it goes and spreading wider. Anyone it washes
    /// over catches fire (Burning) - more the longer he's in it - and where it lands on the ground or a wall it
    /// leaves fuel burning there for a few seconds (GroundFire), which sets alight anyone who walks through it.
    /// The roar of it and the light of it come from here too.
    /// </summary>
    public class Flamethrower : MonoBehaviour
    {
        static Flamethrower I;
        AudioSource roar;
        Light lt;
        float lastSpray;
        Vector3 lastMuzzle;
        static AudioClip roarClip;
        static readonly Collider[] overlap = new Collider[48];
        static readonly RaycastHit[] lineBuf = new RaycastHit[16];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        /// <summary>A tick of flame from the nozzle along a direction (not onto <paramref name="shooter"/>).</summary>
        public static void Spray(Vector3 muzzle, Vector3 dir, HumanBody shooter)
        {
            if (I == null) I = new GameObject("Flamethrower").AddComponent<Flamethrower>();
            I.lastSpray = Time.time;
            I.lastMuzzle = muzzle;
            const float speed = 11f;      // (about five metres of reach)
            Fx.FlameJet(muzzle, dir, speed);
            HumanBrain.BroadcastNoise(muzzle, 25f, 0.6f);
            // follow the stream out: slowing, sagging, spreading
            Vector3 p = muzzle, v = dir * speed;
            var lit = new HashSet<BodyPart>();
            for (int s = 0; s < 12; s++)
            {
                const float dt = 0.045f;
                Vector3 next = p + v * dt;
                v += Physics.gravity * 0.3f * dt;
                v *= 0.94f;
                float r = 0.12f + s * 0.035f;
                // people in it
                int n = Physics.OverlapCapsuleNonAlloc(p, next, r, overlap, ~0, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++)
                {
                    var bp = overlap[k].GetComponent<BodyPart>();
                    if (bp == null || bp.body == null || bp.body == shooter || !lit.Add(bp)) continue;
                    Burning.Ignite(bp, overlap[k].ClosestPoint(p), 0.07f);
                }
                // something solid in the way: it splashes there and burns
                Vector3 seg = next - p;
                int m = Physics.RaycastNonAlloc(p, seg.normalized, lineBuf, seg.magnitude, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue; RaycastHit hit = default;
                for (int k = 0; k < m; k++)
                {
                    var c = lineBuf[k].collider;
                    if (c is CharacterController || c.GetComponentInParent<HumanBody>() != null) continue;
                    if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                    if (lineBuf[k].distance < best) { best = lineBuf[k].distance; hit = lineBuf[k]; }
                }
                if (best < float.MaxValue)
                {
                    GroundFire.At(hit.point + hit.normal * 0.05f, hit.normal);
                    if (Random.value < 0.3f) BloodSystem.Instance?.Mark(hit.point, hit.normal, Random.Range(0.3f, 0.6f), 0.35f);
                    break;
                }
                p = next;
            }
        }

        void Update()
        {
            bool on = Time.time - lastSpray < 0.15f;
            if (roar == null)
            {
                roar = gameObject.AddComponent<AudioSource>();
                roar.clip = Roar(); roar.loop = true; roar.spatialBlend = 0.6f; roar.minDistance = 3f; roar.maxDistance = 60f;
                lt = new GameObject("FlameLight").AddComponent<Light>();
                lt.transform.SetParent(transform, false);
                lt.type = LightType.Point; lt.range = 10f; lt.color = new Color(1f, 0.6f, 0.25f); lt.shadows = LightShadows.None;
            }
            transform.position = lastMuzzle;
            if (on && !roar.isPlaying) roar.Play();
            roar.volume = Mathf.MoveTowards(roar.volume, on ? 0.7f * GameSettings.Volume : 0f, Time.deltaTime * 4f);
            if (!on && roar.volume <= 0.001f && roar.isPlaying) roar.Stop();
            lt.intensity = on ? 4f * (0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 12f, 0f)) : Mathf.MoveTowards(lt.intensity, 0f, Time.deltaTime * 20f);
            lt.transform.localPosition = Vector3.zero;
        }

        /// <summary>A flamethrower: a breathy, roaring rush.</summary>
        static AudioClip Roar()
        {
            if (roarClip != null) return roarClip;
            int rate = 22050, n = rate * 2;
            var d = new float[n];
            var rnd = new System.Random(21);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = (float)rnd.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, w, 0.05f);
                lp2 = Mathf.Lerp(lp2, w, 0.4f);
                d[i] = lp * 2f + lp2 * 0.35f;
            }
            for (int i = 0; i < 1500; i++) { float k = i / 1500f; d[n - 1500 + i] = Mathf.Lerp(d[n - 1500 + i], d[i], k); }
            roarClip = AudioClip.Create("FlameRoar", n, 1, rate, false);
            roarClip.SetData(d, 0);
            return roarClip;
        }
    }

    /// <summary>
    /// Fuel burning where the flamethrower splashed it: flames and smoke for a few seconds (topped up while you keep
    /// spraying it), setting alight anyone standing in it.
    /// </summary>
    public class GroundFire : MonoBehaviour
    {
        static readonly List<GroundFire> all = new List<GroundFire>();
        float life, nextTouch;
        static readonly Collider[] near = new Collider[32];
        const int Max = 40;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        public static void At(Vector3 pos, Vector3 normal)
        {
            foreach (var f in all)
                if (f != null && (f.transform.position - pos).sqrMagnitude < 0.8f * 0.8f) { f.life = Mathf.Min(9f, f.life + 0.6f); return; }
            if (all.Count >= Max) { var old = all[0]; all.RemoveAt(0); if (old != null) Destroy(old.gameObject); }
            var go = new GameObject("GroundFire");
            go.transform.position = pos;
            if (SandboxGame.Instance != null && SandboxGame.Instance.MapRoot != null) go.transform.SetParent(SandboxGame.Instance.MapRoot, true);
            var g = go.AddComponent<GroundFire>();
            g.life = Random.Range(4f, 6f);
            all.Add(g);
        }

        void Update()
        {
            if (WorldFreeze.On) return;
            life -= Time.deltaTime;
            if (life <= 0f) { all.Remove(this); Destroy(gameObject); return; }
            float k = Mathf.Clamp01(life / 3f);
            Fx.BurnFlames(transform.position, k, 0.3f);
            if (Time.time < nextTouch) return;
            nextTouch = Time.time + 0.25f;
            int n = Physics.OverlapSphereNonAlloc(transform.position, 0.6f, near, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var bp = near[i].GetComponent<BodyPart>();
                if (bp != null && bp.body != null) Burning.Ignite(bp, near[i].ClosestPoint(transform.position), 0.08f * k);
            }
        }

        void OnDestroy() { all.Remove(this); }
    }
}
