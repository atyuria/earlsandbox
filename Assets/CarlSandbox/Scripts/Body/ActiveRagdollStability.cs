using UnityEngine;

namespace Carl
{
    /// <summary>
    /// How steady a person is on their feet, and how they lose it. Injury, pain, blood loss, fear and
    /// confusion make them sway, misstep, trip and slip; hits knock their balance, and a hurt person is
    /// knocked off it more easily and recovers it more slowly. Falling is gradual - the knees go, they sink,
    /// hands go out (onto a wall if there is one) - and someone not badly hurt can catch themselves.
    /// </summary>
    public partial class ActiveRagdoll
    {
        /// <summary>1 = steady on their feet, 0 = barely able to stay up.</summary>
        public float Steadiness { get; private set; } = 1f;
        public bool IsSinking => sinking;
        public bool IsStaggering => mode == BodyMode.Standing && balance < 0.45f;
        public bool BracingOnWall { get; private set; }

        bool sinking;
        float sinkT, sinkDur, sinkRecover;
        int fallStyle;            // 0 crumple, 1 pitch along sinkDir, 2 sideways over a leg, 3 sit back
        int fallSide;
        bool critFell;

        // ---- personal way of moving (each person walks a little differently)
        bool styled;
        [HideInInspector] public float styleStride = 1f, styleCadence = 1f, styleArm = 1f, styleLean, styleWidth = 0.1f, styleKnee, styleBounce = 1f;
        /// <summary>How hard this person fights to get away / get up (personality).</summary>
        [HideInInspector] public float will = 0.7f;

        /// <summary>An even, neutral walk with no personal quirks (the player).</summary>
        public void SetPlainStyle()
        {
            styled = true;
            styleStride = styleCadence = styleArm = styleBounce = 1f;
            styleLean = 0f; styleKnee = 0f; styleWidth = 0.1f;
        }

        void EnsureStyle()
        {
            if (styled) return;
            styled = true;
            styleStride = Random.Range(0.85f, 1.15f);
            styleCadence = Random.Range(0.88f, 1.12f);
            styleArm = Random.Range(0.55f, 1.4f);
            styleLean = Random.Range(-2f, 5f);
            styleWidth = Random.Range(0.075f, 0.13f);
            styleKnee = Random.Range(0f, 9f);
            styleBounce = Random.Range(0.7f, 1.3f);
            will = Random.Range(0.35f, 1f);
        }

        /// <summary>Heart stopped, brain or spinal cord wrecked: the body drops almost at once.</summary>
        bool CriticalCollapse => physio.cardiacArrest || physio.perfusion < 0.3f || physio.brainTrauma > 0.4f || physio.lesion != SpineLevel.None;

        /// <summary>
        /// Balance was lost (a hit, a trip, a slip). Usually they stagger - a few hurried catch-steps along the
        /// push - and keep going; the less steady (more hurt, bleeding, scared) the likelier it ends on the floor.
        /// </summary>
        public void LoseBalance(Vector3 dir, float severity, string reason)
        {
            if (mode != BodyMode.Standing || sinking) return;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(body.Chest.transform.forward, Vector3.up);
            flat.Normalize();
            float recover = Mathf.Clamp01(Steadiness * 0.95f + will * 0.15f - severity * 0.35f);
            if (CriticalCollapse) recover = 0f;
            if (Random.value < recover)
            {
                // stagger: hurried steps along the push, arms out, then back to running
                balance = 0.08f;
                stumbleTimer = Mathf.Max(stumbleTimer, Random.Range(0.55f, 1.2f) * (1.2f - Steadiness * 0.4f));
                stumbleVel = flat * Random.Range(0.8f, 1.8f) * (0.6f + severity);
                staggerDir = flat;
                staggerLean = Mathf.Max(staggerLean, Random.Range(14f, 30f));
                if (DebugFalls) Debug.Log($"[Carl] {body.DisplayName} staggered ({reason}, recover {recover:0.00})");
                return;
            }
            sinkDir = flat;
            Topple(reason, severity);
        }
        bool sinkRolled;
        Vector3 sinkDir;
        float getUpPenalty;
        float swaySeed;

        // wall bracing
        Vector3 wallPoint, wallNormal;
        int wallHand;
        float wallCheck;

        void UpdateSteadiness(float dt)
        {
            EnsureStyle();
            if (!critFell && mode == BodyMode.Standing && !physio.dead && CriticalCollapse) { critFell = true; Topple("critical injury", 1f); }
            if (swaySeed == 0f) swaySeed = Random.Range(1f, 99f);
            float l = physio.LegSupport(-1), r = physio.LegSupport(1);
            float legs = Mathf.Min(l, r);
            float s = Mathf.Lerp(0.3f, 1f, legs * legs) * Mathf.Lerp(0.75f, 1f, (l + r) * 0.5f);
            s -= physio.EffectivePain * 0.32f;
            s -= physio.LossFrac * 0.95f;
            s -= (1f - physio.consciousness) * 1.2f;
            s -= physio.Clumsiness * 0.45f;
            s -= panic * 0.08f;
            s = Mathf.Clamp01(s);
            // react fast when things get worse, recover slowly
            s = Mathf.Clamp01(s + (grit - 1f) * 0.25f);
            Steadiness = Mathf.MoveTowards(Steadiness, s, dt * (s < Steadiness ? 2f / grit : 0.25f * grit));
        }

        float Unsteady => 1f - Steadiness;

        /// <summary>
        /// Start going down. Not an instant ragdoll: the legs give, they sink and reach out to catch
        /// themselves. With some steadiness left (or a wall to lean on) they may catch themselves.
        /// </summary>
        public void Topple(string reason, float quick = 0f)
        {
            if (mode == BodyMode.Down || sinking) return;
            // unconscious, dying or seizing bodies just drop
            if (physio.dead || physio.consciousness < 0.3f || physio.posture != PostureType.None) { Fall(reason); return; }
            if (DebugFalls) Debug.Log($"[Carl] {body.DisplayName} going down: {reason} (steady {Steadiness:0.00}, balance {balance:0.00})");
            bool crit = CriticalCollapse;
            if (crit) quick = 1f;
            sinking = true;
            sinkT = 0f;
            sinkRolled = false;
            sinkDur = Mathf.Lerp(0.6f, 1.3f, Steadiness) * Mathf.Lerp(1f, 0.35f, quick) * Random.Range(0.85f, 1.15f);
            sinkRecover = crit ? 0f : Mathf.Clamp01(Steadiness * 0.75f - 0.15f - quick * 0.4f + (grit - 1f) * 0.4f);
            if (mode == BodyMode.GettingUp) sinkRecover *= 0.3f;
            Vector3 v = Vector3.ProjectOnPlane(body.Pelvis.rb.linearVelocity, Vector3.up);
            if (sinkDir.sqrMagnitude < 0.01f || v.sqrMagnitude > 0.5f)
                sinkDir = v.sqrMagnitude > 0.04f ? v.normalized : Vector3.ProjectOnPlane(body.Chest.transform.forward, Vector3.up).normalized;
            // how they go down follows from why: collapse straight down, over a bad leg, or pitched along the push
            float lsup = physio.LegSupport(-1), rsup = physio.LegSupport(1);
            if (crit || physio.consciousness < 0.55f) fallStyle = 0;
            else if (Mathf.Abs(lsup - rsup) > 0.35f) { fallStyle = 2; fallSide = lsup < rsup ? -1 : 1; }
            else if (Vector3.Dot(sinkDir, Vector3.ProjectOnPlane(body.Pelvis.transform.forward, Vector3.up)) < -0.5f && Random.value < 0.6f) fallStyle = 3;
            else fallStyle = Random.value < 0.75f ? 1 : 0;
            if (fallStyle == 2)
            {
                Vector3 rgt = Vector3.ProjectOnPlane(body.Pelvis.transform.right, Vector3.up).normalized;
                sinkDir = (sinkDir * 0.4f + rgt * fallSide).normalized;
            }
        }

        /// <summary>Called every step while upright, inside the balance block. Returns true when fully down.</summary>
        bool UpdateSinking(float dt, ref float targetH, ref float upright)
        {
            if (!sinking) return false;
            float dur = sinkDur * (BracingOnWall ? 1.9f : 1f);
            sinkT += dt;
            float k = Mathf.Clamp01(sinkT / dur);
            // catching themselves: a stumble step, a hand on a wall, and they're back up
            if (!sinkRolled && k > 0.35f)
            {
                sinkRolled = true;
                // nobody catches themselves on a leg that can't hold them
                float chance = physio.CanStand ? sinkRecover + (BracingOnWall ? 0.35f : 0f) : 0f;
                if (Random.value < chance)
                {
                    sinking = false;
                    sinkDir = Vector3.zero;
                    balance = 0.15f;
                    stumbleTimer = Mathf.Max(stumbleTimer, 0.5f);
                    stumbleVel = sinkDir * 0.8f;
                    if (DebugFalls) Debug.Log($"[Carl] {body.DisplayName} caught themselves");
                    return false;
                }
            }
            float ease = k * k * (3f - 2f * k);
            staggerDir = sinkDir;
            switch (fallStyle)
            {
                case 0:   // crumple: knees fold, straight down
                    targetH = Mathf.Lerp(targetH, 0.35f, ease);
                    upright *= Mathf.Lerp(1f, 0.25f, ease);
                    legBuckle[0] = Mathf.Max(legBuckle[0], ease * 0.95f);
                    legBuckle[1] = Mathf.Max(legBuckle[1], ease * 0.95f);
                    staggerLean = Mathf.Max(staggerLean, 15f * ease);
                    break;
                case 1:   // pitch: lurching steps along the push while the body goes over
                    targetH = Mathf.Lerp(targetH, 0.6f, ease);
                    upright *= Mathf.Lerp(1f, 0f, Mathf.Clamp01(ease * 1.6f));
                    legBuckle[0] = Mathf.Max(legBuckle[0], ease * 0.35f);
                    legBuckle[1] = Mathf.Max(legBuckle[1], ease * 0.35f);
                    staggerLean = Mathf.Max(staggerLean, 45f * ease);
                    stumbleTimer = Mathf.Max(stumbleTimer, 0.1f);
                    stumbleVel = sinkDir * Mathf.Lerp(1.2f, 0.3f, k);
                    break;
                case 2:   // the bad leg gives way and they go over sideways onto it
                    targetH = Mathf.Lerp(targetH, 0.45f, ease);
                    upright *= Mathf.Lerp(1f, 0f, Mathf.Clamp01(ease * 1.4f));
                    legBuckle[fallSide < 0 ? 0 : 1] = Mathf.Max(legBuckle[fallSide < 0 ? 0 : 1], ease);
                    legBuckle[fallSide < 0 ? 1 : 0] = Mathf.Max(legBuckle[fallSide < 0 ? 1 : 0], ease * 0.3f);
                    staggerLean = Mathf.Max(staggerLean, 35f * ease);
                    break;
                default:  // sat down backwards
                    targetH = Mathf.Lerp(targetH, 0.3f, ease);
                    upright *= Mathf.Lerp(1f, 0.3f, ease);
                    legBuckle[0] = Mathf.Max(legBuckle[0], ease * 0.8f);
                    legBuckle[1] = Mathf.Max(legBuckle[1], ease * 0.8f);
                    staggerLean = Mathf.Max(staggerLean, 25f * ease);
                    break;
            }
            if (k >= 1f)
            {
                sinking = false;
                sinkDir = Vector3.zero;
                Fall("went down");
                return true;
            }
            return false;
        }

        /// <summary>Unsteady people sway; staggering people get pushed around by their own momentum.</summary>
        void StabilityForces(float dt, Rigidbody pelvis, Rigidbody chest, float M)
        {
            float u = Unsteady;
            if (u > 0.05f)
            {
                float t = Time.time * (0.35f + u * 0.5f);
                Vector3 drift = new Vector3(Mathf.PerlinNoise(swaySeed, t) - 0.5f, 0f, Mathf.PerlinNoise(t, swaySeed) - 0.5f) * 2f;
                chest.AddForce(drift * M * u * 2.2f / (grit * grit));
            }
            // a hand on a nearby wall holds them up
            if (BracingOnWall)
            {
                Vector3 push = wallNormal * M * 2.5f + Vector3.up * M * 2f * (sinking ? 0.6f : 1f);
                chest.AddForce(push);
                balance = Mathf.Min(1f, balance + dt * 0.5f);
            }
        }

        /// <summary>Looks for a wall within reach while staggering or going down, and puts a hand on it.</summary>
        void UpdateWallBrace(float dt)
        {
            BracingOnWall = false;
            bool need = sinking || balance < 0.45f || (Unsteady > 0.55f && moveSpeed < 0.5f);
            if (!need || physio.dead || physio.consciousness < 0.4f) { wallHand = 0; return; }
            wallCheck -= dt;
            if (wallCheck <= 0f)
            {
                wallCheck = 0.15f;
                wallHand = 0;
                var chest = body.Chest.transform;
                Vector3 o = chest.position;
                Vector3 v = Vector3.ProjectOnPlane(body.Pelvis.rb.linearVelocity, Vector3.up);
                Vector3 fwd = Vector3.ProjectOnPlane(chest.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                Vector3[] dirs = { v.sqrMagnitude > 0.04f ? v.normalized : fwd, sinking ? sinkDir : fwd, right, -right, fwd };
                float bestD = 0.85f;
                foreach (var d in dirs)
                {
                    int nh = Physics.RaycastNonAlloc(o, d, rayBuf, 0.85f, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < nh; i++)
                    {
                        var h = rayBuf[i];
                        if (h.collider is CharacterController || h.collider.GetComponent<BodyPart>() != null) continue;
                        if (Mathf.Abs(h.normal.y) > 0.5f) continue;
                        if (h.distance < bestD)
                        {
                            bestD = h.distance; wallPoint = h.point; wallNormal = h.normal;
                            wallHand = Vector3.Dot(h.point - o, right) >= 0f ? 1 : -1;
                        }
                    }
                }
            }
            if (wallHand == 0 || !physio.ArmUsable(wallHand)) return;
            var hand = body[wallHand < 0 ? PartId.HandL : PartId.HandR];
            Vector3 goal = wallPoint + wallNormal * 0.03f + Vector3.up * (sinking ? -0.15f * sinkT : 0.05f);
            Vector3 err = goal - hand.rb.position;
            hand.rb.AddForce(Vector3.ClampMagnitude(err * 900f - hand.rb.linearVelocity * 35f, 200f) * Mathf.Clamp01(physio.Effort + 0.4f));
            // reach for it with the arm
            SetArm(wallHand, Shoulder(wallHand, 70f, 35f), 20f);
            BracingOnWall = err.magnitude < 0.3f;
        }

        /// <summary>Arms come out for balance while staggering; both reach out to catch a fall.</summary>
        void StaggerArms()
        {
            if (sinking)
            {
                for (int s = -1; s <= 1; s += 2)
                    if (s != wallHand) SetArm(s, Shoulder(s, 65f, 25f), 25f);
                return;
            }
            if (balance < 0.45f && pose == PoseKind.Stand)
            {
                float k = Mathf.Clamp01((0.45f - balance) / 0.45f);
                for (int s = -1; s <= 1; s += 2)
                {
                    if (s == wallHand) continue;
                    int i = (int)(s < 0 ? PartId.UpperArmL : PartId.UpperArmR);
                    target[i] = target[i] * AZ(s * 55f * k) * AX(-20f * k);
                }
            }
        }

        // ----------------------------------------------------------------- feet: missteps and slips

        /// <summary>A step starts. Unsteady, scared or confused people sometimes catch a foot.</summary>
        void OnStepStart(Leg l, Vector3 vCur)
        {
            float spd = vCur.magnitude;
            if (spd < 0.3f) return;
            float u = Unsteady;
            float p = Mathf.Pow(u, 1.6f) * 0.16f + panic * 0.006f * gaitRun + physio.Clumsiness * 0.05f;
            p *= Mathf.Lerp(0.5f, 1.4f, Mathf.InverseLerp(0.5f, 4f, spd));
            if (Random.value > p) return;
            // the foot catches: a short, crossed step, and the body pitches over it
            Vector3 short_ = l.from + (l.to - l.from) * Random.Range(0.15f, 0.45f);
            Vector3 cross = Vector3.Cross(Vector3.up, vCur.normalized) * (-l.side) * Random.Range(0.05f, 0.14f);
            l.to = GroundUnder(short_ + cross);
            balance -= Random.Range(0.35f, 0.75f) * (1f + u);
            stumbleTimer = Mathf.Max(stumbleTimer, 0.6f);
            stumbleVel = vCur * 0.5f;
            staggerDir = vCur.normalized;
            staggerLean = Mathf.Max(staggerLean, 15f + 15f * u);
            if (DebugFalls) Debug.Log($"[Carl] {body.DisplayName} tripped (balance {balance:0.00})");
            if (balance < 0f) LoseBalance(vCur, Mathf.Clamp01(spd / 4f), "tripped");
        }

        readonly float[] footBlood = new float[2];

        /// <summary>A foot lands: it smudges blood it lands in, picks some up, and leaves bloody prints after.</summary>
        void FootPrint(Leg l, Vector3 vCur)
        {
            var bs = BloodSystem.Instance;
            if (bs == null || GameSettings.NoBlood) return;
            int fi = l.side < 0 ? 0 : 1;
            Vector3 p = l.plant - Vector3.up * AnkleHeight;
            Vector3 dir = vCur.sqrMagnitude > 0.04f ? vCur.normalized : Vector3.ProjectOnPlane(body.Pelvis.transform.forward, Vector3.up).normalized;
            float there = bs.BloodOnSurface(p, Vector3.up);
            if (there > 0.08f)
            {
                // stepping in it: smear the pool along the step and soak the sole
                Vector3 back = p - dir * Mathf.Clamp(vCur.magnitude * 0.06f, 0.03f, 0.14f);
                bs.SmearSegment(back, p + dir * 0.06f, Vector3.up, 0.07f, there * 1.5f, Random.value * 97f);
                footBlood[fi] = Mathf.Min(1f, footBlood[fi] + there * 0.6f);
            }
            else if (footBlood[fi] > 0.04f)
            {
                // a print: heel and sole, fading with every step
                Vector3 side = Vector3.Cross(Vector3.up, dir) * (l.side * 0.012f);
                bs.SmearSegment(p - dir * 0.09f + side, p - dir * 0.05f + side, Vector3.up, 0.055f, footBlood[fi] * 1.2f, Random.value * 97f);
                bs.SmearSegment(p - dir * 0.01f + side, p + dir * 0.1f + side, Vector3.up, 0.075f, footBlood[fi], Random.value * 97f);
                footBlood[fi] *= 0.72f;
            }
        }

        /// <summary>A foot lands. Running onto blood can slip.</summary>
        void OnFootPlant(Leg l, Vector3 vCur)
        {
            FootPrint(l, vCur);
            float spd = vCur.magnitude;
            if (spd < 0.7f || BloodSystem.Instance == null) return;
            float blood = BloodSystem.Instance.BloodOnSurface(l.plant, Vector3.up);
            if (blood < 0.25f) return;
            float p = (blood - 0.25f) * Mathf.InverseLerp(0.7f, 4f, spd) * 0.6f;
            if (Random.value > p) return;
            // the foot shoots out along the direction of travel
            l.plant = GroundUnder(l.plant + vCur * Random.Range(0.12f, 0.3f));
            var foot = body[FootId(l.side)];
            foot.rb.AddForce(vCur.normalized * 4f, ForceMode.VelocityChange);
            balance -= 0.45f + spd * 0.15f;
            stumbleTimer = Mathf.Max(stumbleTimer, 0.5f);
            stumbleVel = vCur * 0.3f;
            if (DebugFalls) Debug.Log($"[Carl] {body.DisplayName} slipped in blood (balance {balance:0.00})");
            if (balance < 0f) LoseBalance(vCur, 0.6f, "slipped in blood");
        }

        // ----------------------------------------------------------------- getting up

        /// <summary>How long they lie there before trying to get up: quick if barely hurt, longer if hurt.</summary>
        float GetUpDelay => (Mathf.Lerp(5f, 1.8f, Steadiness) + getUpPenalty) / grit;

        void UpdateGetUpAttempt(float dt)
        {
            if (mode != BodyMode.GettingUp || sinking) return;
            // a hurt person's attempt can fail partway up
            if (getUpT > 0.4f && Random.value < Unsteady * Unsteady * dt * 1.2f / (grit * grit))
            {
                getUpPenalty += 1.5f;
                Topple("failed to get up", 0.3f);
            }
            if (getUpT > 1.7f) getUpPenalty = Mathf.Max(0f, getUpPenalty - 1f);
        }
    }
}
