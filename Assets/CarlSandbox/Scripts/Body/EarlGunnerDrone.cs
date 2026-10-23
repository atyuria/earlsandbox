using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Drones, and not walking into walls.
    ///
    /// An Earl who spots one of your FPV drones (FpvDrone) puts rounds into it. He is no marksman against a small
    /// fast thing in the air: his aim swings onto it only so fast and leads it only a little, so a drone that keeps
    /// moving and jinking mostly gets missed, while one that hovers, or comes straight in on a steady line, gets hit.
    /// A dropper high overhead is a speck against the sky he rarely picks out - unless it has just dropped a grenade
    /// that didn't kill him (or someone is already shooting at it), after which everyone round about knows to look
    /// up. A kamikaze coming straight at him sometimes breaks his nerve: he runs off to the side, or throws himself
    /// flat at the last moment.
    ///
    /// Walking, he feels ahead at chest height (the middle and each shoulder) and slides along a wall rather than
    /// marching into it; a shoulder or an arm catching a corner steers him off it; and if he's trying to walk and
    /// getting nowhere, he backs off and goes round.
    /// </summary>
    public partial class EarlGunner
    {
        FpvDrone droneT;
        float droneCheck, droneSeenAt = -99f, fearUntil;
        bool fearRolled;
        Vector3 droneAimPt, droneVelSm, fleeDir;
        /// <summary>For the playtests: rounds fired at drones, and times a man broke and ran from one.</summary>
        public static int DroneShots, DroneFrights, StuckBackoffs;

        /// <summary>Shooting at a drone, or running from one. True if that's what he's doing this frame.</summary>
        bool DroneBehaviour(float legs)
        {
            // (your drones are your side's: only the other side shoots at them, or runs from them)
            if (FpvDrone.Active.Count == 0 || !Teams.Hostile(team, Teams.Friendly)) { droneT = null; return false; }
            if (Time.time > droneCheck)
            {
                droneCheck = Time.time + 0.25f + Random.value * 0.1f;
                var d = SpotDrone();
                if (d != null)
                {
                    if (d != droneT)
                    {
                        // first sight of it: the aim starts well off it and has to swing on
                        droneT = d; fearRolled = false; droneVelSm = Vector3.zero;
                        droneAimPt = d.transform.position + Random.insideUnitSphere * 3f;
                    }
                    droneSeenAt = Time.time;
                }
            }
            if (droneT == null || droneT.Ended || Time.time - droneSeenAt > 1.2f) { droneT = null; return false; }
            var rag = body.ragdoll;
            Vector3 dp = droneT.transform.position, chest = body.Chest.transform.position;
            float dist = Vector3.Distance(dp, chest);
            // a man close by is the bigger worry than a dropper far off
            if (sees && enemy != null && !droneT.IsKamikaze && Vector3.Distance(enemy.Center, chest) < dist * 0.6f) return false;
            Vector3 dv = droneT.Velocity;

            // one coming straight at him: does he hold his nerve?
            bool incoming = droneT.IsKamikaze && dist < 26f && Vector3.Dot(dv, (chest - dp).normalized) > 4f;
            if (incoming && !fearRolled)
            {
                fearRolled = true;
                float p = 0.2f + (1f - aggression) * 0.45f + (Gun ? 0f : 0.35f) + (medic ? 0.25f : 0f) + suppression * 0.15f;
                if (Random.value < p)
                {
                    fearUntil = Time.time + Random.Range(2.5f, 5f);
                    Vector3 away = Vector3.ProjectOnPlane(chest - dp, Vector3.up).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? 1f : -1f);
                    fleeDir = (away * 0.4f + side).normalized;
                    DroneFrights++;
                }
            }
            if (!incoming && dist > 32f) fearRolled = false;    // a fresh pass, a fresh test of nerve
            if (Time.time < fearUntil)
            {
                body.brain.fear = Mathf.Max(body.brain.fear, 0.9f);
                // right on him: flat on the ground
                if (dist < 4f && incoming) { fearUntil = 0f; rag.Fall("threw himself flat from a drone"); return true; }
                rag.aimDir = null; rag.lean = 0f; rag.crouch = 0f; rag.lookAt = dp;
                rag.moveDir = fleeDir; rag.moveSpeed = 4.6f * Mathf.Max(0.4f, legs);
                rag.faceDir = fleeDir;
                Status = "RUNNING FROM A DRONE";
                return true;
            }
            if (!Gun || medic) return false;

            // tracking it: the aim swings onto where it's going at a man's pace, leading it a little
            float dt = Time.deltaTime;
            droneVelSm = Vector3.Lerp(droneVelSm, dv, 1f - Mathf.Exp(-dt * 2.5f));
            float flight = dist / Mathf.Max(100f, ammo != null ? ammo.velocity : 700f);
            Vector3 want = dp + droneVelSm * (flight + 0.08f) * 0.6f;
            float swing = 2.5f + dist * 0.18f;
            droneAimPt = Vector3.MoveTowards(droneAimPt, want, swing * dt);
            // and never dead on: a wandering error that grows with range
            float t = Time.time;
            Vector3 err = new Vector3(Mathf.PerlinNoise(t * 1.7f, seed) - 0.5f, Mathf.PerlinNoise(seed, t * 1.9f) - 0.5f, Mathf.PerlinNoise(t * 1.3f, seed + 3f) - 0.5f) * (0.12f + dist * 0.008f);
            Vector3 aimAt = droneAimPt + err;
            rag.moveSpeed = 0f; rag.crouch = 0f; rag.pose = PoseKind.Stand;
            rag.lean = Mathf.MoveTowards(rag.lean, 0f, dt * 3f);
            rag.lookAt = dp;
            Vector3 flat = Vector3.ProjectOnPlane(aimAt - chest, Vector3.up);
            if (flat.sqrMagnitude > 0.01f) rag.faceDir = flat.normalized;
            rag.aimDir = AimWithError(aimAt);
            Status = "SHOOTING AT A DRONE";
            if (dist < 95f)
            {
                int before = rounds;
                Shoot(aimAt);
                if (rounds < before)
                {
                    DroneShots++;
                    // the noise and the tracers: everyone round about looks for it too
                    droneT.ShotAtUntil = Time.time + 4f;
                }
            }
            return true;
        }

        /// <summary>A drone he can see (or hear right by him), or null.</summary>
        FpvDrone SpotDrone()
        {
            if (body.Head == null) return null;
            Vector3 eye = body.Head.transform.position;
            Vector3 face = body.ragdoll.faceDir;
            FpvDrone best = null; float bd = float.MaxValue;
            foreach (var d in FpvDrone.Active)
            {
                if (d == null || d.Ended) continue;
                Vector3 dp = d.transform.position, to = dp - eye;
                float dist = to.magnitude;
                // revealed: a grenade from it that didn't kill them. Shot at: someone is firing at it, so those
                // about know which way to look - but that alone doesn't pick a speck out high in the sky
                bool revealed = Time.time < d.RevealedUntil, shotAt = Time.time < d.ShotAtUntil;
                float range = SandboxGame.Night ? (revealed ? 45f : 22f) : (revealed || shotAt ? 120f : 75f);
                if (dist > range || dist > bd) continue;
                // the buzz gives it away close by, whichever way he's facing
                bool heard = dist < (d.IsKamikaze ? 28f : 18f);
                Vector3 fl = Vector3.ProjectOnPlane(to, Vector3.up);
                if (!heard && !revealed && !shotAt && face.sqrMagnitude > 0.01f && fl.sqrMagnitude > 1f && Vector3.Angle(face, fl) > 70f) continue;
                // high up, it's a speck against the sky: picked out only now and then
                float above = dp.y - eye.y;
                if (!heard && !revealed && above > 16f && Random.value > Mathf.Lerp(0.012f, 0f, Mathf.InverseLerp(16f, 45f, above))) continue;
                if (!Clear(eye, dp, body)) continue;
                if (Haze.Transmit(eye, dp) < 0.3f) continue;
                best = d; bd = dist;
            }
            return best;
        }

        // ------------------------------------------------------------ walls and corners

        Vector3 lastProgressPos;
        float progressT, unstickUntil;
        Vector3 unstickDir;
        static readonly RaycastHit[] wallBuf = new RaycastHit[8];

        void AvoidWalls()
        {
            var rag = body.ragdoll;
            if (rag.IsDown || body.Chest == null || body.Pelvis == null || LadderClimber.Busy(body)) return;
            Vector3 me = body.Pelvis.transform.position;
            if (rag.moveSpeed < 0.25f) { lastProgressPos = me; progressT = 0f; return; }
            Vector3 dir = Vector3.ProjectOnPlane(rag.moveDir, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            // backing off a corner he walked into
            if (Time.time < unstickUntil) { rag.moveDir = unstickDir; rag.moveSpeed = Mathf.Min(rag.moveSpeed, 1.2f); return; }
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 chest = body.Chest.transform.position - Vector3.up * 0.15f;
            Vector3 push = Vector3.zero;
            bool blocked = false;
            for (int s = -1; s <= 1; s++)
            {
                if (!Wall(chest + right * (s * 0.3f), dir, 0.6f, out var h)) continue;
                blocked = true;
                Vector3 n = Vector3.ProjectOnPlane(h.normal, Vector3.up).normalized;
                float k = 1f - h.distance / 0.6f;
                push += n * k;
                // a shoulder or an arm catching the corner: step away from that side
                if (s != 0) push -= right * s * k * 0.8f;
            }
            if (blocked)
            {
                Vector3 nd = Vector3.ProjectOnPlane(dir + push * 1.6f, Vector3.up);
                // straight into it: slide along the wall instead
                if (nd.sqrMagnitude < 0.01f || Vector3.Dot(nd.normalized, dir) < -0.3f) nd = Vector3.ProjectOnPlane(dir, push.normalized);
                if (nd.sqrMagnitude > 1e-4f) rag.moveDir = nd.normalized;
            }
            // trying to walk and getting nowhere: back off at an angle, then carry on
            if ((me - lastProgressPos).sqrMagnitude > 0.04f) { lastProgressPos = me; progressT = 0f; }
            else if ((progressT += Time.deltaTime) > 0.9f)
            {
                progressT = 0f;
                unstickDir = Quaternion.Euler(0f, Random.value < 0.5f ? 125f : -125f, 0f) * dir;
                unstickUntil = Time.time + Random.Range(0.5f, 0.9f);
                path = null;
                StuckBackoffs++;
            }
        }

        /// <summary>Is there a wall (not a slope he can walk up, not a person, not loose junk) this close ahead?</summary>
        bool Wall(Vector3 from, Vector3 dir, float len, out RaycastHit hit)
        {
            hit = default;
            int n = Physics.RaycastNonAlloc(from, dir, wallBuf, len, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = wallBuf[i];
                if (h.collider is CharacterController) continue;
                if (h.normal.y > 0.5f) continue;
                if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;
                if (h.collider.GetComponentInParent<HumanBody>() != null) continue;
                if (h.distance < best) { best = h.distance; hit = h; }
            }
            return best < float.MaxValue;
        }
    }
}
