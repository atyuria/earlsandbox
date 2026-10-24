using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Fighting in the dark, and the trench battle's two jobs.
    ///
    /// At night (SandboxGame.Night) a man is only seen when something gives him away: a light on him or his own
    /// torch shining, the flash of his rifle, a lamp he's standing under, the looker's own torch beam on him, or
    /// being right up close. Otherwise all an Earl has is what he hears. Each Earl carries a torch on his rifle but
    /// keeps it off most of the time so as not to give himself away: he flicks it on to check the corner where he
    /// thinks you are, to clear a stretch of trench, and keeps it on a man it has caught.
    ///
    /// The battle (TRENCHES): <see cref="assault"/> men go across to take the other side's front line, some over
    /// the top straight across no-man's-land, some through the middle; <see cref="defend"/> men hold their own
    /// line, each on his spot watching the field.
    /// </summary>
    public partial class EarlGunner
    {
        /// <summary>Goes across to take the other side's front line (TRENCHES).</summary>
        public bool assault;
        /// <summary>Holds his own line at <see cref="holdPoint"/>, watching no-man's-land (TRENCHES).</summary>
        public bool defend;
        float nextStep;

        // ------------------------------------------------------------ seeing in the dark

        Light torch;
        float torchUntil, torchNext;
        public bool TorchOn => torch != null && torch.enabled;

        /// <summary>The lamps lit on the map at night (where they are and how far they light).</summary>
        public static readonly List<(Vector3 at, float range)> Lamps = new List<(Vector3, float)>();
        /// <summary>When each man last fired (the flash gives him away in the dark).</summary>
        static readonly Dictionary<HumanBody, float> flashed = new Dictionary<HumanBody, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetNight() { Lamps.Clear(); flashed.Clear(); }

        public static void Flashed(HumanBody shooter) { if (shooter != null) flashed[shooter] = Time.time; }
        /// <summary>For the playtests: frames any torch was on, and guesses made off someone else's light.</summary>
        public static int TorchUses, LightGuesses;

        /// <summary>
        /// A light in the dark is seen a long way off - long before the man behind it is. Any enemy light in front
        /// of him (a torch, your flashlight) gives a guess at where that man is: close to, near enough exact; far
        /// off, roughly that way and roughly that far. (The point of fighting with the light off.)
        /// </summary>
        void SpotLights(Vector3 eye)
        {
            Vector3 face = body.ragdoll.faceDir;
            void Check(Vector3 at)
            {
                Vector3 to = at - eye;
                float d = to.magnitude;
                if (d > 150f || d < 1f) return;
                if (face.sqrMagnitude > 0.01f && Vector3.Angle(face, Vector3.ProjectOnPlane(to, Vector3.up)) > 100f) return;
                if (!Clear(eye, at, body)) return;
                LightGuesses++;
                Learn(at + Intel.Guess(d, 0.03f, 0.0025f), Mathf.Lerp(0.75f, 0.45f, d / 150f), false);
            }
            var me = PlayerAvatar.Body;
            if (WeaponController.PlayerLightOn && me != null && !me.Dead && Teams.Hostile(team, Teams.Friendly) && !Teams.PlayerOutOfIt)
            {
                var w = SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;
                Check(w != null && w.Armed && w.current < w.MuzzleCount ? w.MuzzleOf(w.current).position : me.Head.transform.position);
            }
            foreach (var g in All)
                if (g != null && g != this && g.TorchOn && Teams.Hostile(team, g.team)) Check(g.torch.transform.position);
        }

        /// <summary>Can he make out this man from his eye, in the dark? (Always yes in daylight.)</summary>
        bool Visible(HumanBody h, Vector3 eye, float dist2)
        {
            if (!SandboxGame.Night) return true;
            if (dist2 < 3.5f * 3.5f) return true;                                      // a shape right in front of him
            if (flashed.TryGetValue(h, out float f) && Time.time - f < 0.35f) return true;    // the muzzle flash
            if (h == PlayerAvatar.Body ? WeaponController.PlayerLightOn : h.gunner != null && h.gunner.TorchOn) return true;   // his own light
            Vector3 c = h.Center;
            if (TorchOn && dist2 < 38f * 38f && Vector3.Angle(torch.transform.forward, c - torch.transform.position) < torch.spotAngle * 0.5f) return true;   // caught in the beam
            foreach (var l in Lamps) if ((l.at - c).sqrMagnitude < l.range * l.range * 0.6f && dist2 < 55f * 55f) return true;   // under a lamp
            // anyone else's torch lighting him up
            foreach (var g in All)
                if (g != null && g != this && g.TorchOn && (g.torch.transform.position - c).sqrMagnitude < 30f * 30f
                    && Vector3.Angle(g.torch.transform.forward, c - g.torch.transform.position) < g.torch.spotAngle * 0.5f) return true;
            return false;
        }

        /// <summary>
        /// The rifle torch at night: off by default; flicked on for a few seconds at a time to look where he thinks
        /// someone is (peeking a corner, pushing in, searching), kept on while it has a man in its beam, now and then
        /// a quick sweep on watch.
        /// </summary>
        void UpdateTorch()
        {
            bool night = SandboxGame.Night && Fighting && muzzle != null && !LadderClimber.Busy(body);
            if (!night) { if (torch != null) torch.enabled = false; return; }
            if (torch == null)
            {
                torch = new GameObject("RifleTorch").AddComponent<Light>();
                torch.transform.SetParent(muzzle, false);
                torch.transform.localPosition = new Vector3(0.025f, -0.01f, -0.12f);
                torch.type = LightType.Spot; torch.spotAngle = 38f; torch.innerSpotAngle = 14f;
                torch.range = 40f; torch.intensity = 5f; torch.color = new Color(1f, 0.95f, 0.85f);
                torch.shadows = LightShadows.None;
                torch.enabled = false;
                torchNext = Time.time + Random.Range(2f, 8f);
            }
            bool on = torch.enabled;
            if (sees)
            {
                // a man in the beam stays in it; with the torch off he doesn't light himself up to shoot
                if (on && enemy != null && Vector3.Angle(torch.transform.forward, enemy.Center - torch.transform.position) < torch.spotAngle * 0.6f) torchUntil = Time.time + 0.5f;
            }
            else if (Time.time > torchNext && !on)
            {
                bool hunting = SuspectConf > 0.25f && (stance == Stance.Peek || stance == Stance.Push || stance == Stance.Search || Vector3.Distance(body.Center, suspect) < 25f);
                float chance = hunting ? 0.55f : 0f;
                if (Random.value < chance) torchUntil = Time.time + Random.Range(hunting ? 1.2f : 0.6f, hunting ? 3.2f : 1.4f);
                torchNext = Time.time + Random.Range(hunting ? 2.5f : 6f, hunting ? 6f : 15f);
            }
            torch.enabled = Time.time < torchUntil;
            if (torch.enabled) TorchUses++;
            if (torch.enabled && !on) Sfx.Play("click_empty", muzzle.position, 0.15f, 1f, 1f, 0.05f);
        }

        // ------------------------------------------------------------ the battle


        /// <summary>Their front line's band of z (yours is below zero, theirs above).</summary>
        bool InTheirLine(Vector3 p) => team == Teams.Friendly ? p.z > 16f : p.z < -16f;
        bool TheirLineNode(Vector3 n) => EnemyLineNode(team, n);
        /// <summary>Is this trench point on the front line of the side <paramref name="team"/> is attacking?</summary>
        public static bool EnemyLineNode(int team, Vector3 n) => team == Teams.Friendly ? n.z >= 18f && n.z <= 29f : n.z <= -18f && n.z >= -29f;

        /// <summary>
        /// Going across (TRENCHES): the stretch of their line (its x) his group is going for, so that the attack
        /// comes in at several places at once instead of everyone piling down the same trench. Unset: whatever is
        /// roughly across from where he starts.
        /// </summary>
        public float? laneX;
        /// <summary>Which group of the attack he is in (the men in a group go the same way together).</summary>
        public int group = -1;

        /// <summary>On his spot in his own line, gun on no-man's-land.</summary>
        void HoldLine(float legs)
        {
            var rag = body.ragdoll;
            Vector3 g = Vector3.ProjectOnPlane(holdPoint - body.Pelvis.transform.position, Vector3.up);
            if (g.magnitude > 1.2f) MoveTo(holdPoint, 1.6f * legs);
            else
            {
                rag.moveSpeed = 0f;
                float sweep = Mathf.Sin(Time.time * 0.3f + seed) * 6f;
                AimAt(holdPoint + new Vector3(sweep, 1.5f, team == Teams.Enemy ? -10f : 10f), false);
            }
        }
    }
}
