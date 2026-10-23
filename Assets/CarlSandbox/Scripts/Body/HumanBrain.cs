using UnityEngine;

namespace Carl
{
    public enum Activity
    {
        Idle, Wandering, Alert, Fleeing, SeekingCover, Hiding, Surrendering, Cowering, HoldingWound, Limping,
        Down, Crawling, Writhing, GettingUp, Posturing, Seizing, Unconscious, Dead, Stumbling
    }

    /// <summary>
    /// Perception, emotion and decisions. Drives ActiveRagdoll with movement, facing and pose commands.
    /// </summary>
    public class HumanBrain : MonoBehaviour
    {
        HumanBody body;
        Physiology physio;
        ActiveRagdoll rag;

        public Activity activity = Activity.Idle;
        /// <summary>Off: a test dummy - no decisions, it just stands (and reacts physically when hurt).</summary>
        public bool aiEnabled = true;
        /// <summary>Armed and fighting: an EarlGunner is in charge of him.</summary>
        [HideInInspector] public bool hostile;
        /// <summary>This body is the player's.</summary>
        [HideInInspector] public bool playerControlled;
        public float fear;
        public float bravery;     // personality
        public float compliance;  // personality: how likely to put hands up
        public bool canSeePlayer;
        public bool aimedAt;
        public float lastThreatTime = -999f;
        public Vector3 threatPos;

        Vector3 home;
        Vector3 wanderTarget;
        float wanderWait;
        float thinkTimer;
        Vector3? coverPoint;
        float coverSearchTimer;
        Wound holdWound;
        float wanderSeed;
        float downDecide, struggleT;
        int downAction;   // 0 lie / writhe, 1 try to get up, 2 crawl away
        readonly RaycastHit[] buf = new RaycastHit[8];

        public void Init(HumanBody b, System.Random rng)
        {
            body = b;
            physio = b.physio;
            rag = b.ragdoll;
            bravery = (float)rng.NextDouble();
            compliance = (float)rng.NextDouble();
            wanderSeed = (float)rng.NextDouble() * 100f;
            home = b.Pelvis.transform.position;
            wanderTarget = home;
            wanderWait = 1f + (float)rng.NextDouble() * 3f;
        }

        public void OnDeath() { activity = Activity.Dead; }

        public static void BroadcastNoise(Vector3 pos, float radius, float intensity)
        {
            foreach (var h in HumanBody.All)
            {
                if (h.brain == null || h.Dead) continue;
                float d = Vector3.Distance(h.Center, pos);
                if (d > radius) continue;
                float f = intensity * (1f - d / radius);
                h.brain.fear = Mathf.Clamp01(h.brain.fear + f * (1.1f - h.brain.bravery * 0.5f));
                h.brain.lastThreatTime = Time.time;
                h.brain.threatPos = pos;
                h.physio.AddStress(f * 0.3f);
            }
        }

        public string FeelingText()
        {
            if (physio.dead) return "—";
            if (physio.consciousness < 0.2f) return "NOTHING (UNRESPONSIVE)";
            if (physio.EffectivePain > 0.75f) return "IN AGONY";
            if (physio.confusion > 0.45f || physio.consciousness < 0.6f) return "DAZED / CONFUSED";
            if (fear > 0.8f) return "TERRIFIED";
            if (fear > 0.55f) return "PANICKED";
            if (physio.EffectivePain > 0.4f) return "IN PAIN";
            if (fear > 0.3f) return "SCARED";
            if (fear > 0.1f) return "NERVOUS";
            return "CALM";
        }

        public string ActivityText()
        {
            switch (activity)
            {
                case Activity.Idle: return "STANDING IDLE";
                case Activity.Wandering: return "WANDERING";
                case Activity.Alert: return "ALERT / WATCHING THREAT";
                case Activity.Fleeing: return "RUNNING AWAY";
                case Activity.SeekingCover: return "SEEKING COVER / THREAT";
                case Activity.Hiding: return "HIDING BEHIND COVER";
                case Activity.Surrendering: return "HANDS UP / SURRENDERING";
                case Activity.Cowering: return "COWERING";
                case Activity.HoldingWound: return "HOLDING WOUND";
                case Activity.Limping: return "LIMPING AWAY";
                case Activity.Down: return "DOWN";
                case Activity.Crawling: return "CRAWLING AWAY";
                case Activity.Writhing: return "WRITHING IN PAIN";
                case Activity.GettingUp: return "TRYING TO GET UP";
                case Activity.Posturing: return physio.posture.ToString().ToUpper() + " POSTURING";
                case Activity.Seizing: return "SEIZING";
                case Activity.Unconscious: return "UNRESPONSIVE";
                case Activity.Dead: return "—";
                case Activity.Stumbling: return "DISORIENTED / CLUTCHING WOUND";
            }
            return activity.ToString().ToUpper();
        }

        Vector3 PlayerEye => SandboxGame.Instance != null && SandboxGame.Instance.cam != null ? SandboxGame.Instance.cam.transform.position : Vector3.zero;

        void Perceive()
        {
            canSeePlayer = false;
            aimedAt = false;
            var game = SandboxGame.Instance;
            if (game == null || game.cam == null || physio.Blind || physio.consciousness < 0.35f) return;
            var head = body.Head.transform;
            Vector3 eye = head.position + head.up * 0.02f;
            Vector3 to = PlayerEye - eye;
            float dist = to.magnitude;
            if (dist > 80f) return;
            float ang = Vector3.Angle(head.forward, to);
            if (ang > 115f && dist > 3f) return;
            // line of sight, ignoring our own body
            int n = Physics.RaycastNonAlloc(eye, to / dist, buf, dist - 0.3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (buf[i].collider is CharacterController) continue;
                var bp = buf[i].collider.GetComponent<BodyPart>();
                if (bp != null && bp.body == body) continue;
                if (bp != null) continue; // other bodies don't fully block
                return;
            }
            canSeePlayer = true;
            if (game.weapons != null && game.weapons.HasGunOut)
            {
                var ray = new Ray(game.cam.transform.position, game.cam.transform.forward);
                float d = Vector3.Cross(ray.direction, body.Center - ray.origin).magnitude;
                float along = Vector3.Dot(body.Center - ray.origin, ray.direction);
                aimedAt = along > 0f && d < Mathf.Lerp(0.6f, 2f, Mathf.InverseLerp(5f, 40f, along));
            }
        }

        static readonly Unity.Profiling.ProfilerMarker PM_Update = new Unity.Profiling.ProfilerMarker("Carl.HumanBrain.Update");

        void Update()
        {

            using var pmScope = PM_Update.Auto();
            if (body == null || rag == null || body.Pelvis == null || body.Head == null) return;
            if (WorldFreeze.On && body != PlayerAvatar.Body) return;
            float dt = Time.deltaTime;

            if (physio.dead)
            {
                activity = Activity.Dead;
                rag.moveSpeed = 0f; rag.holdPart = null; rag.wantCrawl = false; rag.wantStand = false;
                return;
            }

            // the player's own body: driven by the player's input, never by this brain
            if (playerControlled) { activity = rag.mode == BodyMode.Down ? Activity.Down : Activity.Alert; return; }

            if (!aiEnabled)
            {
                activity = rag.mode == BodyMode.Down ? Activity.Down : Activity.Idle;
                rag.moveSpeed = 0f; rag.wantCrawl = false; rag.wantStand = true; rag.struggle = 0f;
                rag.pose = rag.mode == BodyMode.Down ? PoseKind.Limp : PoseKind.Stand;
                rag.lookAt = null; rag.holdPart = null; rag.panic = 0f;
                return;
            }

            thinkTimer -= dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.2f;
                Perceive();
            }
            // armed and fighting: EarlGunner decides where he goes and what he does (no hands up, no fleeing)
            if (hostile) { fear = Mathf.Min(fear, 0.3f); return; }

            if (aimedAt) { fear = Mathf.Clamp01(fear + dt * 0.35f); lastThreatTime = Time.time; threatPos = PlayerEye; }
            else if (canSeePlayer && Time.time - lastThreatTime < 20f) threatPos = PlayerEye;
            float calmRate = Time.time - lastThreatTime > 12f ? 0.05f : 0.01f;
            fear = Mathf.Max(physio.EffectivePain * 0.5f, fear - dt * calmRate);
            if (physio.confusion > 0.5f) fear *= 0.98f; // frontal damage blunts fear
            rag.panic = Mathf.Clamp01(fear - 0.5f) * 2f;

            ChooseHoldWound();

            // --- incapacitated states
            if (physio.consciousness < 0.2f) { activity = Activity.Unconscious; Stop(); return; }
            if (physio.posture == PostureType.Seizure) { activity = Activity.Seizing; Stop(); return; }
            if (physio.posture != PostureType.None && physio.posture != PostureType.Tremor) { activity = Activity.Posturing; Stop(); return; }

            Vector3 pos = body.Pelvis.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(pos - threatPos, Vector3.up);
            if (away.sqrMagnitude < 0.01f) away = body.transform.forward;
            away.Normalize();
            bool threatened = Time.time - lastThreatTime < 25f;

            if (rag.mode == BodyMode.Down)
            {
                rag.moveSpeed = 0f;
                bool canStand = physio.CanStand;
                bool wantsFlee = (threatened && fear > 0.2f) || fear > 0.35f;
                rag.wantStand = canStand && physio.EffectivePain < 0.8f;
                // can't stand: every few seconds decide what to do - try to push up (feebly if badly hurt),
                // drag themselves away, or lie there hurting. Not everyone crawls.
                downDecide -= dt;
                if (downDecide <= 0f)
                {
                    downDecide = Random.Range(2.5f, 6f);
                    float able = physio.consciousness * Mathf.Clamp01(1.2f - physio.LossFrac * 1.5f) * rag.will;
                    float r = Random.value;
                    downAction = 0;
                    if (!rag.wantStand && physio.consciousness > 0.45f)
                    {
                        if (r < 0.45f * able + 0.1f) downAction = 1;                               // try to get up
                        else if (wantsFlee && r < 0.45f * able + 0.1f + 0.35f * rag.will) downAction = 2; // crawl
                    }
                    struggleT = 0f;
                }
                rag.wantCrawl = !rag.wantStand && downAction == 2;
                rag.struggle = 0f;
                if (!rag.wantStand && downAction == 1)
                {
                    // a push-up attempt that rises and gives out
                    struggleT += dt;
                    float legsUp = Mathf.Max(physio.LegSupport(-1), physio.LegSupport(1));
                    float strength = Mathf.Clamp01(0.25f + legsUp * 0.5f + rag.will * 0.25f) * physio.consciousness * (1f - physio.LossFrac);
                    rag.struggle = struggleT < 1.6f ? Mathf.Sin(Mathf.Clamp01(struggleT / 1.6f) * Mathf.PI) * strength : 0f;
                }
                rag.crawlDir = coverPoint.HasValue && Vector3.Distance(pos, coverPoint.Value) > 1f ? (coverPoint.Value - pos).normalized : away;
                if (rag.wantCrawl && rag.IsCrawling) activity = Activity.Crawling;
                else if (physio.EffectivePain > 0.35f) activity = Activity.Writhing;
                else activity = Activity.Down;
                rag.pose = physio.EffectivePain > 0.6f ? PoseKind.Curl : PoseKind.Writhe;
                if (rag.wantCrawl) rag.pose = PoseKind.Crawl;
                if (rag.mode == BodyMode.Down && rag.wantStand && rag.downTime > 2f) activity = Activity.GettingUp;
                return;
            }

            rag.wantCrawl = false;
            rag.wantStand = true;
            if (rag.mode == BodyMode.GettingUp) { activity = Activity.GettingUp; Stop(); return; }

            float legs = Mathf.Min(physio.LegSupport(-1), physio.LegSupport(1));
            float maxSpeed = Mathf.Lerp(1.1f, 4.2f, physio.Effort) * Mathf.Lerp(0.22f, 1f, legs * legs) * (1f - physio.Clumsiness * 0.5f);
            // unsteady on their feet: slower, more careful steps; staggering: barely moving forward
            maxSpeed *= Mathf.Lerp(0.5f, 1f, rag.Steadiness);
            if (rag.IsStaggering) maxSpeed *= 0.5f;
            rag.crouch = 0f;
            rag.lookAt = threatened ? threatPos : (Vector3?)null;

            // --- decisions
            bool wounded = physio.EffectivePain > 0.3f;

            // badly hurt: attention collapses onto the wound, not the shooter. They stagger about
            // aimlessly, looking down at it, instead of purposefully turning away from you.
            float bleedSeverity = Mathf.Clamp01((physio.bleedOut + physio.bleedIn) / 6f);
            float distress = Mathf.Max(physio.EffectivePain, bleedSeverity) + physio.confusion * 0.5f
                             + physio.LossFrac * 1.5f + (1f - physio.consciousness) * 0.8f;
            if (distress > 0.55f && holdWound != null && !aimedAt && !(threatened && fear > 0.35f && rag.will > 0.45f))
            {
                activity = Activity.Stumbling;
                rag.lookAt = holdWound.part.transform.TransformPoint(holdWound.localPos);
                float t = Time.time * 0.22f + wanderSeed;
                Vector3 wander = Quaternion.Euler(0f, (Mathf.PerlinNoise(t, wanderSeed) - 0.5f) * 540f, 0f) * Vector3.forward;
                float go = Mathf.PerlinNoise(wanderSeed, t * 1.4f);
                if (go > 0.42f) Move(Steer(pos, wander), maxSpeed * Mathf.Lerp(0.12f, 0.35f, go));
                else Stop();
                rag.faceDir = Vector3.Slerp(rag.faceDir, wander, 0.03f);
                rag.pose = PoseKind.Stand;
                return;
            }
            if (aimedAt && canSeePlayer && fear > 0.2f && compliance > 0.35f && fear < 0.9f + compliance * 0.1f && physio.confusion < 0.5f)
            {
                activity = Activity.Surrendering;
                rag.pose = PoseKind.HandsUp;
                rag.moveSpeed = 0f;
                rag.faceDir = -away;
                rag.holdPart = null;
                return;
            }
            if (threatened && fear > 0.45f)
            {
                coverSearchTimer -= dt;
                if (coverSearchTimer <= 0f) { coverSearchTimer = 1.5f; coverPoint = FindCover(pos); }
                if (coverPoint.HasValue && (bravery < 0.6f || wounded))
                {
                    Vector3 to = Vector3.ProjectOnPlane(coverPoint.Value - pos, Vector3.up);
                    if (to.magnitude > 0.8f)
                    {
                        activity = Activity.SeekingCover;
                        Move(Steer(pos, to.normalized), maxSpeed);
                        rag.pose = PoseKind.Stand;
                    }
                    else
                    {
                        activity = Activity.Hiding;
                        rag.moveSpeed = 0f;
                        rag.faceDir = -away;
                        rag.crouch = 1f;
                        rag.pose = PoseKind.Cower;
                    }
                    return;
                }
                if (fear > 0.85f && bravery < 0.3f && !canSeePlayer)
                {
                    activity = Activity.Cowering;
                    rag.moveSpeed = 0f; rag.crouch = 1f; rag.pose = PoseKind.Cower;
                    return;
                }
                activity = legs < 0.7f ? Activity.Limping : Activity.Fleeing;
                Move(Steer(pos, away), maxSpeed);
                rag.pose = PoseKind.Stand;
                return;
            }
            if (wounded)
            {
                activity = legs < 0.7f ? Activity.Limping : Activity.HoldingWound;
                rag.pose = PoseKind.Stand;
                if (threatened || legs < 0.7f) Move(Steer(pos, away), maxSpeed * 0.4f);
                else Stop();
                return;
            }
            if (threatened && fear > 0.15f)
            {
                activity = Activity.Alert;
                Stop();
                rag.faceDir = -away;
                rag.pose = PoseKind.Stand;
                return;
            }

            // --- calm: wander
            rag.pose = PoseKind.Stand;
            Vector3 toW = Vector3.ProjectOnPlane(wanderTarget - pos, Vector3.up);
            if (toW.magnitude < 0.6f)
            {
                activity = Activity.Idle;
                rag.moveSpeed = 0f;
                wanderWait -= dt;
                if (wanderWait <= 0f)
                {
                    wanderWait = Random.Range(2f, 7f);
                    var r = Random.insideUnitCircle * 6f;
                    wanderTarget = home + new Vector3(r.x, 0f, r.y);
                }
            }
            else
            {
                activity = Activity.Wandering;
                Move(Steer(pos, toW.normalized), Mathf.Min(1.2f, maxSpeed));
            }
        }

        void Stop()
        {
            rag.moveSpeed = 0f;
        }

        void Move(Vector3 dir, float speed)
        {
            rag.moveDir = dir;
            rag.moveSpeed = speed;
            rag.faceDir = Vector3.Slerp(rag.faceDir, dir, 0.25f);
        }

        /// <summary>Simple obstacle avoidance with whisker rays at knee height.</summary>
        Vector3 Steer(Vector3 pos, Vector3 dir)
        {
            Vector3 origin = new Vector3(pos.x, pos.y - 0.5f, pos.z);
            for (int k = 0; k < 7; k++)
            {
                float a = k == 0 ? 0f : ((k + 1) / 2) * 30f * (k % 2 == 0 ? 1f : -1f);
                Vector3 d = Quaternion.AngleAxis(a, Vector3.up) * dir;
                if (!Blocked(origin, d, 1.4f)) return d;
            }
            return -dir;
        }

        bool Blocked(Vector3 origin, Vector3 dir, float dist)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, buf, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (buf[i].collider is CharacterController) continue;
                var bp = buf[i].collider.GetComponent<BodyPart>();
                if (bp != null) continue;
                if (buf[i].normal.y > 0.6f) continue; // walkable
                return true;
            }
            return false;
        }

        /// <summary>Samples points around us and picks the nearest one hidden from the threat.</summary>
        Vector3? FindCover(Vector3 pos)
        {
            Vector3 eye = threatPos;
            Vector3? best = null;
            float bestD = float.MaxValue;
            for (int r = 1; r <= 4; r++)
            {
                for (int k = 0; k < 12; k++)
                {
                    float ang = k * 30f;
                    Vector3 p = pos + Quaternion.AngleAxis(ang, Vector3.up) * Vector3.forward * (r * 3f);
                    if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var g, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (g.normal.y < 0.7f) continue;
                    Vector3 stand = g.point + Vector3.up * 0.8f;
                    Vector3 to = stand - eye;
                    if (!Physics.Raycast(eye, to.normalized, out var hit, to.magnitude - 0.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.GetComponent<BodyPart>() || hit.collider is CharacterController) continue;
                    float d = Vector3.Distance(pos, p) + Vector3.Dot((p - pos).normalized, (eye - pos).normalized) * 4f;
                    if (d < bestD) { bestD = d; best = g.point; }
                }
                if (best.HasValue) break;
            }
            return best;
        }

        void ChooseHoldWound()
        {
            // hold the most painful bleeding wound
            Wound best = null; float score = 0f;
            foreach (var bp in body.partList)
            {
                if (bp.severed) continue;
                foreach (var w in bp.wounds)
                {
                    if (w.type == WoundType.Internal) continue;
                    float s = w.extRate + (w.age < 10f ? 3f : 0f) + (w.arterial ? 10f : 0f);
                    if (s > score) { score = s; best = w; }
                }
            }
            holdWound = best;
            if (best != null && physio.EffectivePain > 0.15f && activity != Activity.Surrendering)
            {
                rag.holdPart = best.part;
                rag.holdLocal = best.localPos + best.localNormal * 0.03f;
            }
            else rag.holdPart = null;
        }
    }
}
