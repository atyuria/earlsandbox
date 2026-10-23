using UnityEngine;

namespace Carl
{
    public enum BodyMode { Standing, Down, GettingUp }

    public enum PoseKind { Stand, HandsUp, Cower, Crouch, Curl, Writhe, Crawl, Limp, Brace }

    /// <summary>
    /// Physics-driven body. Every joint is a muscle (slerp drive) pulled toward a target pose.
    /// While standing, the pelvis is held up and upright by balance forces; losing balance,
    /// consciousness or leg support drops it into a ragdoll that keeps whatever muscle tone it has left.
    /// </summary>
    public partial class ActiveRagdoll : MonoBehaviour
    {
        HumanBody body;
        Physiology physio;

        public BodyMode mode = BodyMode.Standing;

        // ---- commands (set by HumanBrain every frame)
        [HideInInspector] public Vector3 moveDir;
        [HideInInspector] public float moveSpeed;
        [HideInInspector] public Vector3 faceDir = Vector3.forward;
        [HideInInspector] public PoseKind pose = PoseKind.Stand;
        [HideInInspector] public BodyPart holdPart;
        [HideInInspector] public Vector3 holdLocal;
        [HideInInspector] public Vector3 crawlDir;
        [HideInInspector] public bool wantCrawl;
        [HideInInspector] public bool wantStand;
        /// <summary>How hard to put down and keep down: 1 for an Earl, more for you (the main character).</summary>
        [HideInInspector] public float grit = 1f;
        /// <summary>Leaning round a corner: -1 left .. +1 right (the spine bends, the head goes out sideways).</summary>
        [HideInInspector] public float lean;
        /// <summary>Multiplier on the arm muscles: lowered while the hands are jointed to something they hold.</summary>
        [HideInInspector] public float armCompliance = 1f;
        [HideInInspector] public Vector3? lookAt;
        [HideInInspector] public float crouch;
        [HideInInspector] public float panic;

        // ---- state
        public float balance = 1f;
        public float downTime;
        public float tone = 1f;
        public bool IsDown => mode == BodyMode.Down;
        public bool IsCrawling { get; private set; }
        public bool IsProne { get; private set; }
        public float GroundHeight { get; private set; }

        float unstableTimer, lowTimer, getUpT, walkPhase, crawlPhase, stumbleTimer;
        Vector3 stumbleVel;
        int fencingSide = 1;
        readonly Quaternion[] target = new Quaternion[17];
        readonly float[] partTone = new float[17];
        readonly RaycastHit[] rayBuf = new RaycastHit[12];
        Vector2[] noiseSeed = new Vector2[17];

        const float StandHeight = 0.98f;
        public static bool DebugFalls = false; // set true to log falls and hits to the Console

        public void Init(HumanBody b)
        {
            body = b;
            physio = b.physio;
            faceDir = b.transform.forward;
            for (int i = 0; i < 17; i++) noiseSeed[i] = new Vector2(Random.value * 100f, Random.value * 100f);
            fencingSide = Random.value < 0.5f ? -1 : 1;
        }

        // =============================================================== external events

        // additive "flinch" rotations per part (degrees), decaying back to zero
        readonly Vector3[] flinch = new Vector3[17];
        readonly float[] legBuckle = new float[2];
        float staggerLean;
        Vector3 staggerDir;

        public void OnHit(BodyPart part, Vector3 dir, float impulse)
        {
            float k = Anatomy.IsLeg(part.id) ? 9f : part.id == PartId.Head ? 14f : 22f;
            // a hurt, bleeding, frightened person is knocked off balance far more easily
            k *= Mathf.Lerp(0.45f, 1f, Steadiness) * grit;
            balance -= impulse / k;
            float s = Mathf.Clamp01(impulse / 6f) * Random.Range(0.75f, 1.25f);
            var chestT = body.Chest.transform;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
            float fwd = Vector3.Dot(flat, Vector3.ProjectOnPlane(chestT.forward, Vector3.up).normalized); // + shot from behind
            float right = Vector3.Dot(flat, Vector3.ProjectOnPlane(chestT.right, Vector3.up).normalized);
            float lateral = Vector3.Dot(part.transform.position - chestT.position, chestT.right);
            int side = Anatomy.Side(part.id);

            // torso snaps away from the bullet, twisting if hit off-centre
            if (Anatomy.IsTorso(part.id) || part.id == PartId.Neck)
            {
                AddFlinch(PartId.Abdomen, new Vector3(fwd * 22f, -lateral * 60f * fwd, -right * 14f) * s);
                AddFlinch(PartId.Chest, new Vector3(fwd * 28f, -lateral * 90f * (fwd >= 0 ? 1 : -1), -right * 18f) * s);
                AddFlinch(PartId.Neck, new Vector3(-fwd * 25f, 0, right * 10f) * s);
            }
            if (part.id == PartId.Head || part.id == PartId.Neck)
            {
                AddFlinch(PartId.Neck, new Vector3(fwd * 45f, Random.Range(-20f, 20f), -right * 30f) * s);
                AddFlinch(PartId.Head, new Vector3(fwd * 30f, Random.Range(-25f, 25f), -right * 25f) * s);
            }
            if (Anatomy.IsArm(part.id))
            {
                PartId ua = side < 0 ? PartId.UpperArmL : PartId.UpperArmR;
                AddFlinch(ua, new Vector3(fwd * 50f, 0, -right * 45f) * s);
                AddFlinch(side < 0 ? PartId.ForearmL : PartId.ForearmR, new Vector3(-40f, 0, 0) * s);
            }
            if (Anatomy.IsLeg(part.id))
            {
                // the leg buckles under the hit
                legBuckle[side < 0 ? 0 : 1] = Mathf.Max(legBuckle[side < 0 ? 0 : 1], Mathf.Clamp01(0.25f + s * 0.45f));
                AddFlinch(side < 0 ? PartId.ThighL : PartId.ThighR, new Vector3(-fwd * 25f, 0, 0) * s);
            }
            // arms fling out for balance
            for (int a = -1; a <= 1; a += 2)
                AddFlinch(a < 0 ? PartId.UpperArmL : PartId.UpperArmR, new Vector3(Random.Range(-35f, 20f), 0, a * Random.Range(20f, 55f)) * s);

            if (mode == BodyMode.Standing)
            {
                stumbleTimer = Mathf.Max(stumbleTimer, 0.5f + s * 0.7f);
                stumbleVel = flat * Mathf.Clamp(impulse * 0.12f, 0.3f, 2f);
                staggerDir = flat;
                staggerLean = Mathf.Max(staggerLean, 10f + s * 18f);
                // a stagger, not a drop: only a really big hit takes them straight down
                if (balance < -1.6f) Fall("blown off their feet");
                else if (balance < 0f) LoseBalance(flat, Mathf.Clamp01(-balance), "knocked off balance");
            }
        }

        void AddFlinch(PartId p, Vector3 euler)
        {
            int i = (int)p;
            flinch[i] = Vector3.ClampMagnitude(flinch[i] + euler, 75f);
        }

        public void OnSevered(BodyPart part)
        {
            if (Anatomy.IsLeg(part.id) || part.id == PartId.Neck || part.id == PartId.Head) Fall();
        }

        public void Fall(string reason = "external")
        {
            if (mode == BodyMode.Down) return;
            if (DebugFalls) Debug.Log($"[Carl] {body.DisplayName} fell: {reason} (tilt {Vector3.Angle(body.Pelvis.transform.up, Vector3.up):0}, h {GroundHeight:0.00}, canStand {physio.CanStand}, t {Time.time - body.spawnTime:0.0}s)");
            mode = BodyMode.Down;
            downTime = 0f;
            IsCrawling = false;
            sinking = false;
        }

        // =============================================================== pose helpers

        static Quaternion AX(float deg) => Quaternion.AngleAxis(deg, Vector3.right);
        static Quaternion AY(float deg) => Quaternion.AngleAxis(deg, Vector3.up);
        static Quaternion AZ(float deg) => Quaternion.AngleAxis(deg, Vector3.forward);

        static Quaternion Shoulder(int s, float flexFwd, float abduct, float twist = 0f) => AZ(s * abduct) * AX(-flexFwd) * AY(s * twist);
        static Quaternion Elbow(float bend) => AX(-bend);
        static Quaternion Hip(int s, float flex, float abduct = 0f) => AZ(s * abduct) * AX(-flex);
        static Quaternion Knee(float bend) => AX(bend);
        static Quaternion Ankle(float plantar) => AX(plantar);
        static Quaternion Spine(float flex, float side = 0f, float twist = 0f) => AY(twist) * AZ(side) * AX(flex);

        void Set(PartId p, Quaternion q) => target[(int)p] = q;
        void target_mul(int i, Quaternion q) => target[i] = target[i] * q;

        void SetArm(int s, Quaternion sh, float elbow, float wrist = 0f)
        {
            Set(s < 0 ? PartId.UpperArmL : PartId.UpperArmR, sh);
            Set(s < 0 ? PartId.ForearmL : PartId.ForearmR, Elbow(elbow));
            Set(s < 0 ? PartId.HandL : PartId.HandR, AX(wrist));
        }

        // =============================================================== falling: hands out
        /// <summary>Which arms throw themselves out to catch a fall: 1 left, 2 right (the hand holding a gun keeps it).</summary>
        [HideInInspector] public int braceArms = 3;
        /// <summary>The arm that isn't bracing holds what it carries tucked in against the chest.</summary>
        [HideInInspector] public bool tuckCarry;
        Vector3 fallDir;

        /// <summary>Going over: down, still well off the floor and dropping, and awake enough to react.</summary>
        public bool Falling => mode == BodyMode.Down && downTime < 1.1f && GroundHeight > 0.32f && !physio.dead && physio.consciousness > 0.45f;

        /// <summary>
        /// Arms out in front of where you are going to land, palms down, nearly straight - the way anyone puts
        /// their hands out going over - and strong enough to take the landing.
        /// </summary>
        void BraceForFall()
        {
            var chest = body.Chest.rb;
            Vector3 v = Vector3.ProjectOnPlane(chest.linearVelocity, Vector3.up);
            // where you're going over to: the way you're moving, or failing that the way your chest is tipping
            Vector3 tip = Vector3.ProjectOnPlane(body.Chest.transform.up, Vector3.up);
            Vector3 dir = v.magnitude > 0.4f ? v.normalized : tip.sqrMagnitude > 0.01f ? tip.normalized : body.Chest.transform.forward;
            fallDir = Vector3.Slerp(fallDir.sqrMagnitude > 0.5f ? fallDir : dir, dir, 0.25f).normalized;
            Vector3 reach = (fallDir * 0.75f + Vector3.down * 0.65f).normalized;
            for (int s = -1; s <= 1; s += 2)
            {
                bool free = (braceArms & (s < 0 ? 1 : 2)) != 0;
                if (!physio.ArmUsable(s)) continue;
                if (free)
                {
                    Vector3 r = body.Chest.transform.InverseTransformDirection(reach + body.Chest.transform.right * (s * 0.18f));
                    ArmTo(s, r, r);
                    Set(s < 0 ? PartId.HandL : PartId.HandR, AX(-35f));   // wrists cocked back, palms to the floor
                }
                else if (tuckCarry) SetArm(s, Shoulder(s, 40f, 12f), 105f);
            }
        }

        // =============================================================== holding a weapon
        /// <summary>World direction the weapon should point (null: arms do whatever the pose says).</summary>
        [HideInInspector] public Vector3? aimDir;
        /// <summary>1 pistol / one hand, 2 rifle (both hands, left on the handguard), 3 blade.</summary>
        [HideInInspector] public int aimHold;
        /// <summary>Blade: 0 guard, then a swing from wind-up (-1) through the cut (+1).</summary>
        [HideInInspector] public float swingPhase;

        /// <summary>An arm pointing along a direction given in the chest's space (identity = hanging down).</summary>
        static Quaternion Point(Vector3 d)
        {
            d.Normalize();
            float flex = Mathf.Acos(Mathf.Clamp(-d.y, -1f, 1f)) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return AY(yaw) * AX(-flex);
        }

        /// <summary>
        /// Arms that hold a weapon the way a person does: a rifle with the right hand at the grip, elbow down, and
        /// the left arm reaching out under the handguard; a pistol pushed out in front; a blade held up in guard
        /// and swung from a wind-up over the shoulder down and across. Weak or missing arms do what they can.
        /// </summary>
        /// <summary>
        /// An arm with the upper arm along one direction and the forearm along another (both in the chest's
        /// space): the upper arm is turned about its own length so the elbow bends the forearm the right way.
        /// </summary>
        void ArmTo(int s, Vector3 upper, Vector3 fore)
        {
            Quaternion q = Point(upper);
            Vector3 fl = Quaternion.Inverse(q) * fore.normalized;
            float bend = Vector3.Angle(Vector3.down, fl);
            float twist = fl.x * fl.x + fl.z * fl.z > 1e-4f ? Mathf.Atan2(fl.x, fl.z) * Mathf.Rad2Deg : 0f;
            Set(s < 0 ? PartId.UpperArmL : PartId.UpperArmR, q * AY(twist));
            Set(s < 0 ? PartId.ForearmL : PartId.ForearmR, Elbow(Mathf.Clamp(bend, 0f, 145f)));
        }

        void AimArms()
        {
            if (!aimDir.HasValue || physio.dead || physio.consciousness < 0.3f || physio.posture != PostureType.None) return;
            Vector3 d = body.Chest.transform.InverseTransformDirection(aimDir.Value);
            // directions below are given as if aiming straight ahead (x right, y up, z along the line)
            Quaternion a = Quaternion.LookRotation(d.normalized, Vector3.up);
            bool right = physio.ArmUsable(1), left = physio.ArmUsable(-1);
            if (aimHold == 3)
            {
                // guard: elbow low, blade hand up in front; a swing goes from high over the right shoulder down
                // and across the body
                float s = Mathf.Clamp(swingPhase, -1f, 1f);
                Vector3 gu = new Vector3(0.3f, -0.7f, 0.4f), gf = new Vector3(-0.1f, 0.5f, 0.85f);
                Vector3 wu = new Vector3(0.75f, 0.35f, -0.2f), wf = new Vector3(-0.2f, 0.9f, -0.3f);
                Vector3 cu = new Vector3(-0.2f, -0.35f, 0.9f), cf = new Vector3(-0.6f, -0.3f, 0.75f);
                Vector3 u = s < 0f ? Vector3.Slerp(gu, wu, -s) : Vector3.Slerp(gu, cu, s);
                Vector3 f = s < 0f ? Vector3.Slerp(gf, wf, -s) : Vector3.Slerp(gf, cf, s);
                if (right) ArmTo(1, a * u, a * f);
                if (left) ArmTo(-1, a * new Vector3(-0.3f, -0.8f, 0.3f), a * new Vector3(0.2f, 0.1f, 1f));
                return;
            }
            if (aimHold == 2 && right && left)
            {
                // rifle: right elbow down and out, forearm rising to the grip in front of the shoulder;
                // left arm reaching nearly straight out under the handguard
                ArmTo(1, a * new Vector3(0.35f, -0.75f, 0.35f), a * new Vector3(-0.5f, 0.55f, 0.65f));
                ArmTo(-1, a * new Vector3(0.3f, -0.35f, 0.85f), a * new Vector3(0.4f, 0f, 0.9f));
                Set(PartId.Neck, AX(10f));
                return;
            }
            // pistol, or a rifle held in whichever arm is left: pushed out to the middle along the line
            int s1 = right ? 1 : left ? -1 : 0;
            if (s1 == 0) return;
            ArmTo(s1, a * new Vector3(-0.3f * s1, -0.05f, 1f), a * new Vector3(-0.25f * s1, 0f, 1f));
            if (aimHold == 1 && right && left) ArmTo(-1, a * new Vector3(0.35f, -0.1f, 1f), a * new Vector3(0.3f, 0f, 1f));
        }

        void SetLeg(int s, float hip, float knee, float ankle, float abduct = 0f)
        {
            Set(s < 0 ? PartId.ThighL : PartId.ThighR, Hip(s, hip, abduct));
            Set(s < 0 ? PartId.ShinL : PartId.ShinR, Knee(knee));
            Set(s < 0 ? PartId.FootL : PartId.FootR, Ankle(ankle));
        }

        void BuildPose(PoseKind kind)
        {
            for (int i = 0; i < 17; i++) target[i] = Quaternion.identity;
            switch (kind)
            {
                case PoseKind.Stand:
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 3f, 7f), 12f); SetLeg(s, 2f, 4f, 0f); }
                    break;
                case PoseKind.HandsUp:
                    for (int s = -1; s <= 1; s += 2) SetArm(s, Shoulder(s, 15f, 78f, 90f), 100f);
                    for (int s = -1; s <= 1; s += 2) SetLeg(s, 4f, 8f, 0f);
                    Set(PartId.Neck, AX(8f));
                    break;
                case PoseKind.Brace:
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 75f, 20f), 110f); SetLeg(s, 25f, 40f, -15f); }
                    Set(PartId.Chest, Spine(15f)); Set(PartId.Neck, AX(25f));
                    break;
                case PoseKind.Cower:
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 140f, 35f, 20f), 135f); SetLeg(s, 75f, 105f, -25f); }
                    Set(PartId.Abdomen, Spine(20f)); Set(PartId.Chest, Spine(25f)); Set(PartId.Neck, AX(35f)); Set(PartId.Head, AX(20f));
                    break;
                case PoseKind.Crouch:
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 30f, 15f), 70f); SetLeg(s, 70f, 100f, -25f); }
                    Set(PartId.Chest, Spine(20f));
                    break;
                case PoseKind.Curl:
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 70f, -5f), 125f, 30f); SetLeg(s, 95f, 120f, 20f); }
                    Set(PartId.Abdomen, Spine(25f)); Set(PartId.Chest, Spine(25f)); Set(PartId.Neck, AX(35f)); Set(PartId.Head, AX(25f));
                    break;
                case PoseKind.Writhe:
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 45f, 10f), 90f); SetLeg(s, 45f, 70f, 10f); }
                    Set(PartId.Abdomen, Spine(15f)); Set(PartId.Chest, Spine(10f)); Set(PartId.Neck, AX(20f));
                    break;
                case PoseKind.Limp:
                    break;
                case PoseKind.Crawl:
                {
                    float c = crawlPhase;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float ph = c + (s < 0 ? 0f : Mathf.PI);
                        float reach = 0.5f + 0.5f * Mathf.Sin(ph);
                        SetArm(s, Shoulder(s, Mathf.Lerp(80f, 145f, reach), 20f), Mathf.Lerp(85f, 35f, reach));
                        float kick = 0.5f + 0.5f * Mathf.Sin(ph + Mathf.PI);
                        SetLeg(s, Mathf.Lerp(8f, 32f, kick), Mathf.Lerp(20f, 60f, kick), 30f, 10f);
                    }
                    Set(PartId.Neck, AX(-30f)); Set(PartId.Head, AX(-15f));
                    Set(PartId.Chest, Spine(-8f, 6f * Mathf.Sin(c)));
                    break;
                }
            }
        }

        void ApplyPosture(PostureType p, float t)
        {
            switch (p)
            {
                case PostureType.Decerebrate:
                    // rigid extension: arms straight and turned in, wrists flexed, legs and feet extended, head thrown back
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, -10f, 4f, -70f), 0f, 65f); SetLeg(s, -5f, 0f, 40f, -5f); }
                    Set(PartId.Abdomen, Spine(-12f)); Set(PartId.Chest, Spine(-10f)); Set(PartId.Neck, AX(-35f)); Set(PartId.Head, AX(-20f));
                    break;
                case PostureType.Decorticate:
                    // arms flexed tight onto the chest, fists clenched, legs stiff
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 30f, -12f, 30f), 135f, 70f); SetLeg(s, 0f, 0f, 35f, -5f); }
                    Set(PartId.Neck, AX(10f));
                    break;
                case PostureType.Fencing:
                    // one arm locked out toward the turned head, the other flexed
                    SetArm(fencingSide, Shoulder(fencingSide, 95f, 15f), 0f);
                    SetArm(-fencingSide, Shoulder(-fencingSide, 20f, 40f, 40f), 125f, 50f);
                    for (int s = -1; s <= 1; s += 2) SetLeg(s, 5f, 10f, 30f);
                    Set(PartId.Neck, AY(35f * fencingSide)); Set(PartId.Head, AY(20f * fencingSide));
                    break;
                case PostureType.Seizure:
                {
                    // tonic-clonic jerking
                    float j = Mathf.Sin(t * 38f) > 0f ? 1f : -0.4f;
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 35f + 25f * j, 25f, -30f), 70f + 40f * j, 40f); SetLeg(s, 10f + 15f * j, 20f + 25f * j, 30f); }
                    Set(PartId.Neck, AX(-25f + 15f * j)); Set(PartId.Chest, Spine(-10f + 12f * j));
                    break;
                }
                case PostureType.Curl:
                    BuildPose(PoseKind.Curl);
                    break;
            }
        }

        void AddWalk(float speed, float dt)
        {
            float run = Mathf.InverseLerp(1.8f, 4.5f, speed);
            float stride = Mathf.Lerp(1.25f, 2.3f, run);
            walkPhase += dt * speed / stride * Mathf.PI * 2f;
            if (speed < 0.05f) return;
            float weight = Mathf.Clamp01(speed / 0.6f);
            float lean = 0f;
            for (int s = -1; s <= 1; s += 2)
            {
                float sup = physio.LegSupport(s) * (1f - legBuckle[s < 0 ? 0 : 1] * 0.7f);
                float hurt = 1f - sup;
                float ph = walkPhase + (s < 0 ? 0f : Mathf.PI);
                // a hurt leg takes short, stiff, dragging steps
                float ampHip = Mathf.Lerp(22f, 45f, run) * Mathf.Lerp(0.35f, 1f, sup);
                float ampKnee = Mathf.Lerp(40f, 95f, run) * Mathf.Lerp(0.2f, 1f, sup);
                float hip = Mathf.Sin(ph) * ampHip;
                float swing = Mathf.Max(0f, Mathf.Cos(ph));
                float knee = 6f + ampKnee * swing + run * 12f;
                Set(s < 0 ? PartId.ThighL : PartId.ThighR, Hip(s, hip * weight + 2f + run * 6f, hurt * 6f));
                Set(s < 0 ? PartId.ShinL : PartId.ShinR, Knee(knee * weight + 4f));
                Set(s < 0 ? PartId.FootL : PartId.FootR, Ankle(-hip * 0.35f * weight + hurt * 25f * swing));
                lean += s * hurt * 12f; // lean over the bad leg
                // opposite arm swings with this leg; running pumps bent arms
                int a = -s;
                if (pose == PoseKind.Stand)
                    SetArm(a, Shoulder(a, hip * (0.8f + run * 0.6f) * weight + run * 12f, 8f + run * 6f), 15f + run * 75f);
            }
            if (pose == PoseKind.Stand)
            {
                Set(PartId.Abdomen, Spine(2f + run * 5f, 0f, -3f * Mathf.Sin(walkPhase) * weight));
                Set(PartId.Chest, Spine(3f + run * 9f, -lean, 5f * Mathf.Sin(walkPhase) * weight * (1f + run)));
            }
        }

        void AddNoise(float amp, float freq)
        {
            if (amp <= 0.01f) return;
            float t = Time.time * freq;
            for (int i = 1; i < 17; i++)
            {
                var n = noiseSeed[i];
                float x = (Mathf.PerlinNoise(n.x + t, n.y) - 0.5f) * 2f * amp;
                float y = (Mathf.PerlinNoise(n.y, n.x + t) - 0.5f) * 2f * amp * 0.6f;
                float z = (Mathf.PerlinNoise(n.x - t, n.y + 3f) - 0.5f) * 2f * amp * 0.6f;
                target[i] = target[i] * Quaternion.Euler(x, y, z);
            }
        }

        void AddLook()
        {
            if (!lookAt.HasValue) return;
            var chest = body.Chest.transform;
            Vector3 to = lookAt.Value - body.Head.transform.position;
            Vector3 local = chest.InverseTransformDirection(to);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -65f, 65f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 35f);
            target[(int)PartId.Neck] = target[(int)PartId.Neck] * AY(yaw * 0.45f) * AX(pitch * 0.4f);
            target[(int)PartId.Head] = target[(int)PartId.Head] * AY(yaw * 0.55f) * AX(pitch * 0.6f);
        }

        // =============================================================== simulation

        // last values written to each joint (see "drive muscles")
        readonly float[] lastSpring = { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f };
        readonly Quaternion[] lastTarget = new Quaternion[17];
        bool settledCheap;

        // this body's own rigidbodies, for ignoring its own limbs in ground and wall probes (far cheaper than
        // looking up a component on every ray hit)
        System.Collections.Generic.HashSet<Rigidbody> ownRbs;
        bool Own(Rigidbody rb)
        {
            if (rb == null) return false;
            if (ownRbs == null)
            {
                ownRbs = new System.Collections.Generic.HashSet<Rigidbody>();
                foreach (var p in body.partList) if (p != null && p.rb != null) ownRbs.Add(p.rb);
            }
            return ownRbs.Contains(rb);
        }

        void FixedUpdate()
        {
            if (body == null || body.Pelvis == null || body.Chest == null) return;
            // the world is frozen (spawn menu open): everyone but you stands still
            if (WorldFreeze.On && body != PlayerAvatar.Body) return;
            float dt = Time.fixedDeltaTime;
            var pelvis = body.Pelvis.rb;
            var chest = body.Chest.rb;
            // a corpse that has stopped moving needs far less solver work (the same body, just cheaper to keep still)
            if (physio.dead && !settledCheap && body.TimeSinceDeath > 4f && pelvis.linearVelocity.sqrMagnitude < 0.01f)
            {
                settledCheap = true;
                foreach (var bp in body.partList) if (bp != null && bp.rb != null) { bp.rb.solverIterations = 6; bp.rb.solverVelocityIterations = 2; }
            }
            if (settledCheap && pelvis.linearVelocity.sqrMagnitude > 1f)
            {
                // knocked about again (shot, kicked, dragged): full quality while it moves
                settledCheap = false;
                foreach (var bp in body.partList) if (bp != null && bp.rb != null) { bp.rb.solverIterations = 14; bp.rb.solverVelocityIterations = 6; }
            }

            // ---- ground probe
            int n = Physics.RaycastNonAlloc(pelvis.position + Vector3.up * 0.1f, Vector3.down, rayBuf, 3f, ~0, QueryTriggerInteraction.Ignore);
            float groundY = pelvis.position.y - 5f;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = rayBuf[i];
                if (Own(h.rigidbody)) continue;   // its own limbs
                if (h.collider is CharacterController) continue;
                // (you stand on the floor, not on the bodies lying on it: your legs pass over them)
                if (grit > 1f && h.rigidbody != null && h.collider.GetComponentInParent<HumanBody>() != null) continue;
                if (h.distance < best) { best = h.distance; groundY = h.point.y; }
            }
            GroundHeight = pelvis.position.y - groundY;

            // ---- decide mode transitions
            bool canStand = physio.CanStand;
            if (mode == BodyMode.Standing || mode == BodyMode.GettingUp)
            {
                if (!canStand && (mode == BodyMode.Standing || getUpT > 0.3f)) Topple("cannot stand", physio.consciousness < 0.65f ? 0.7f : 0.2f);
            }
            if (mode == BodyMode.Down)
            {
                downTime += dt;
                if (wantStand && canStand && downTime > GetUpDelay && !wantCrawl)
                {
                    BeginGetUp();
                }
            }
            UpdateSteadiness(dt);
            // balance comes back quickly when steady, slowly when hurt
            balance = Mathf.Min(1f, balance + dt * Mathf.Lerp(0.2f, 0.8f, Steadiness));

            // ---- muscle tone
            float baseTone;
            if (physio.dead)
            {
                float td = body.TimeSinceDeath;
                baseTone = physio.instantLimp ? 0f : Mathf.Lerp(0.5f, 0f, td / 2.5f);
            }
            else if (physio.posture != PostureType.None)
            {
                baseTone = physio.posture == PostureType.Tremor ? 0.35f : 1.25f;
            }
            else if (physio.consciousness < 0.2f) baseTone = 0.02f;
            else if (mode == BodyMode.Down)
                // the moment of hitting the ground is limp: no landing on the feet like a cat
                baseTone = downTime < 0.7f ? 0.12f : Mathf.Lerp(0.18f, 0.55f, physio.Effort);
            else baseTone = Mathf.Lerp(0.55f, 1f, physio.consciousness);
            tone = Mathf.MoveTowards(tone, baseTone, dt * (baseTone < tone ? 3f : 1.2f));

            // ---- pose
            PoseKind p = pose;
            if (physio.dead || physio.consciousness < 0.2f) p = PoseKind.Limp;
            else if (mode == BodyMode.Down)
            {
                if (IsCrawling) p = PoseKind.Crawl;
                else if (p == PoseKind.Stand || p == PoseKind.HandsUp || p == PoseKind.Crouch) p = PoseKind.Writhe;
            }
            if (p == PoseKind.Crawl && mode == BodyMode.Down) crawlPhase += dt * 2.3f * Mathf.Lerp(0.55f, 1.1f, physio.Effort);
            BuildPose(p);
            if (mode == BodyMode.GettingUp && getUpPhase < 3) GetUpPose();
            if (mode == BodyMode.Down && (p == PoseKind.Writhe || p == PoseKind.Curl)) Writhe(dt, body.Pelvis.rb, body.Chest.rb);

            if (physio.posture != PostureType.None && !physio.dead) ApplyPosture(physio.posture, Time.time);
            else if (physio.dead && !physio.instantLimp && body.TimeSinceDeath < 2.5f) AddNoise(25f * (1f - body.TimeSinceDeath / 2.5f), 9f); // agonal twitching

            if (mode != BodyMode.Down && !physio.dead && physio.posture == PostureType.None)
            {
                if (mode == BodyMode.Standing) UpdateGait(dt, pelvis, chest);
                else ResetGait();
                AddLook();
            }

            float clumsy = physio.Clumsiness;
            AddNoise(clumsy * 22f + panic * 6f, 1.5f + clumsy * 2f);
            if (p == PoseKind.Writhe) AddNoise(6f + physio.EffectivePain * 12f, 0.55f);
            if (physio.posture == PostureType.Tremor) AddNoise(20f, 12f);

            // ---- injury posture while upright: hunch over torso pain, guard a hurt arm
            if (mode != BodyMode.Down && !physio.dead && physio.posture == PostureType.None)
            {
                float pain = physio.EffectivePain;
                if (holdPart != null && (Anatomy.IsTorso(holdPart.id) || holdPart.id == PartId.Neck) && pain > 0.2f)
                {
                    target[(int)PartId.Abdomen] *= AX(14f * pain + 6f);
                    target[(int)PartId.Chest] *= AX(18f * pain + 6f);
                    target[(int)PartId.Neck] *= AX(12f * pain);
                }
                UpdateWallBrace(dt);
                StaggerArms();
                if (staggerLean > 0.5f)
                {
                    // reeling: chest thrown along the hit, arms out
                    Vector3 localDir = body.Pelvis.transform.InverseTransformDirection(staggerDir);
                    target[(int)PartId.Abdomen] *= AX(localDir.z * staggerLean * 0.6f) * AZ(-localDir.x * staggerLean * 0.6f);
                    target[(int)PartId.Chest] *= AX(localDir.z * staggerLean) * AZ(-localDir.x * staggerLean);
                }
            }

            // ---- flinches (decay quickly back to the pose)
            float fd = Mathf.Clamp01(dt * 5f);
            for (int i = 1; i < 17; i++)
            {
                if (flinch[i].sqrMagnitude < 0.01f) continue;
                target[i] = target[i] * Quaternion.Euler(flinch[i]);
                flinch[i] = Vector3.Lerp(flinch[i], Vector3.zero, fd);
            }
            staggerLean = Mathf.MoveTowards(staggerLean, 0f, dt * 25f);
            for (int s = 0; s < 2; s++)
            {
                if (legBuckle[s] <= 0f) continue;
                // the buckling knee folds
                PartId sh = s == 0 ? PartId.ShinL : PartId.ShinR, th = s == 0 ? PartId.ThighL : PartId.ThighR;
                target[(int)sh] = target[(int)sh] * Knee(55f * legBuckle[s]);
                target[(int)th] = target[(int)th] * AX(-25f * legBuckle[s]);
                legBuckle[s] = Mathf.Max(0f, legBuckle[s] - dt / 1.1f);
            }

            // ---- hold wound (pseudo-IK on the hand)
            int holdArm = 0;
            if (holdPart != null && !physio.dead && physio.consciousness > 0.3f && physio.posture == PostureType.None && !IsCrawling)
            {
                int ws = Anatomy.Side(holdPart.id);
                holdArm = ws != 0 && Anatomy.IsArm(holdPart.id) ? -ws : (ws != 0 ? ws : (physio.ArmUsable(1) ? 1 : -1));
                if (!physio.ArmUsable(holdArm)) holdArm = -holdArm;
                bool reachable = !Anatomy.IsLeg(holdPart.id) || mode == BodyMode.Down || holdPart.id == PartId.ThighL || holdPart.id == PartId.ThighR;
                if (!physio.ArmUsable(holdArm) || !reachable) holdArm = 0;
                if (holdArm != 0)
                {
                    var hand = body[holdArm < 0 ? PartId.HandL : PartId.HandR];
                    Vector3 goal = holdPart.transform.TransformPoint(holdLocal);
                    Vector3 err = goal - hand.rb.position;
                    Vector3 f = err * 900f - hand.rb.linearVelocity * 40f;
                    hand.rb.AddForce(Vector3.ClampMagnitude(f, 160f) * Mathf.Clamp01(physio.Effort + 0.3f));
                }
            }

            AimArms();
            bool bracing = Falling;
            if (bracing) BraceForFall();
            if (Mathf.Abs(lean) > 0.01f && mode == BodyMode.Standing)
            {
                // leaning out: the bend is spread up the spine, the neck straightens the head back toward level,
                // and the hips shift the other way so the weight stays over the feet
                float l = Mathf.Clamp(lean, -1f, 1f);
                target[(int)PartId.Abdomen] = target[(int)PartId.Abdomen] * AZ(-l * 14f);
                target[(int)PartId.Chest] = target[(int)PartId.Chest] * AZ(-l * 20f);
                target[(int)PartId.Neck] = target[(int)PartId.Neck] * AZ(l * 12f);
                body.Pelvis.rb.AddForce(-body.Chest.transform.right * l * body.totalMass * 1.2f);
            }

            // ---- drive muscles
            for (int i = 1; i < 17; i++)
            {
                var bp = body.parts[i];
                if (bp.joint == null) continue;
                // hanging off a torn limb: no muscle drives it any more
                if (bp.severed) { if (lastSpring[i] != -5f) { bp.joint.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = 0.5f, maximumForce = float.MaxValue }; lastSpring[i] = -5f; } continue; }
                float motor = physio.MotorFactor(bp.id);
                float t = tone * motor;
                int side = Anatomy.Side(bp.id);
                if (holdArm != 0 && side == holdArm && Anatomy.IsArm(bp.id)) t *= 0.12f;
                // a shot arm hangs; a buckling leg gives way
                if (holdPart != null && Anatomy.IsArm(holdPart.id) && Anatomy.IsArm(bp.id) && side == Anatomy.Side(holdPart.id)) t *= 0.25f;
                if (Anatomy.IsLeg(bp.id) && side != 0) t *= 1f - legBuckle[side < 0 ? 0 : 1] * 0.75f;
                // hands on a held gun: the arm muscles give way to it (they keep their shape, they don't fight it)
                if (Anatomy.IsArm(bp.id)) t *= armCompliance;
                // going over: the arms thrown out to catch it are tensed, not limp like the rest
                if (bracing && Anatomy.IsArm(bp.id)) t = Mathf.Max(t, 0.75f * motor);
                // broken bones cannot hold load
                foreach (var o in bp.organs) if (o.IsBone && o.Fractured && !o.def.shell) { t *= o.BadlyBroken ? 0.08f : 0.35f; break; }
                float spring = HumanBody.SpringFor(bp.id) * t;
                // writing a joint makes PhysX rebuild it: only when it has actually changed (a still or dead body
                // re-sent the same 16 drives every step)
                if (Mathf.Abs(spring - lastSpring[i]) > 0.004f * Mathf.Max(1f, lastSpring[i]) || lastSpring[i] < 0f)
                {
                    bp.joint.slerpDrive = new JointDrive { positionSpring = spring, positionDamper = spring * 0.05f + 1.5f, maximumForce = float.MaxValue };
                    lastSpring[i] = spring;
                }
                var q = Quaternion.Inverse(target[i]);
                if (Quaternion.Angle(q, lastTarget[i]) > 0.15f) { bp.joint.targetRotation = q; lastTarget[i] = q; }
            }

            // ---- balance / locomotion
            bool getUpPhases = mode == BodyMode.GettingUp && UpdateGetUpPhases(dt, pelvis, chest);
            if (getUpPhases) { IsCrawling = false; }
            else if (mode == BodyMode.Standing || mode == BodyMode.GettingUp)
            {
                float upright = 1f;
                float targetH = StandHeight - crouch * 0.38f;
                if (mode == BodyMode.GettingUp)
                {
                    getUpT += dt;
                    float k = Mathf.SmoothStep(0f, 1f, getUpT / 1.8f);
                    upright = k;
                    targetH = Mathf.Lerp(0.45f, targetH, k);
                    if (getUpT > 1.8f) mode = BodyMode.Standing;
                }

                // sinking onto a weak leg while the other foot is in the air
                targetH -= GaitDip();
                targetH -= Mathf.Max(legBuckle[0], legBuckle[1]) * (sinking ? 0.22f : 0.1f);
                if (!sinking && Mathf.Max(legBuckle[0], legBuckle[1]) > 0.6f && Mathf.Min(physio.LegSupport(-1), physio.LegSupport(1)) < 0.5f && Random.value < dt * 2f)
                    Topple("leg gave out", 0.4f);
                UpdateGetUpAttempt(dt);
                if (UpdateSinking(dt, ref targetH, ref upright)) return;

                float M = body.totalMass;
                float h = GroundHeight;
                float vy = pelvis.linearVelocity.y;
                // held up only by legs that are planted under it
                float legSupport = 1f;
                if (mode == BodyMode.Standing && !sinking)
                {
                    float legH = LegHeight(out legSupport);
                    // the body bobs over each step, but doesn't sink into a crouch while the legs hold it
                    targetH = Mathf.Min(targetH, Mathf.Max(legH, targetH - 0.06f));
                    legSupport = Mathf.Clamp01(0.3f + legSupport);
                }
                if (h < targetH + 0.2f)
                {
                    float fy = M * (90f * (targetH - h) - 14f * vy + 9.81f * legSupport) * Mathf.Lerp(0.6f, 1f, upright) * Mathf.Lerp(0.5f, 1f, legSupport);
                    fy = Mathf.Clamp(fy, 0f, M * 9.81f * 2.5f);
                    pelvis.AddForce(Vector3.up * fy * 0.7f);
                    chest.AddForce(Vector3.up * fy * 0.3f);
                }

                // horizontal drive
                Vector3 vDes = moveDir * moveSpeed;
                if (stumbleTimer > 0f) { stumbleTimer -= dt; vDes += stumbleVel; }
                Vector3 vCur = Vector3.ProjectOnPlane(pelvis.linearVelocity, Vector3.up);
                // (you respond to the keys more crisply than a wandering Earl does)
                float driveK = grit > 1f ? 11f : 5f;
                Vector3 fh = Vector3.ClampMagnitude((vDes - vCur) * M * driveK, grit > 1f ? 1700f : 950f) * upright * GaitTraction();
                pelvis.AddForce(fh * 0.6f);
                chest.AddForce(fh * 0.4f);
                StabilityForces(dt, pelvis, chest, M);

                // upright + facing
                Vector3 face = Vector3.ProjectOnPlane(faceDir, Vector3.up);
                if (face.sqrMagnitude < 0.01f) face = Vector3.ProjectOnPlane(pelvis.transform.forward, Vector3.up);
                var yaw = Quaternion.LookRotation(face.normalized, Vector3.up);
                float wobble = (1f - clumsy * 0.55f) * (stumbleTimer > 0f ? 0.55f : 1f) * Mathf.Lerp(1f, 0.6f, Unsteady)
                               * (balance < 0.45f ? Mathf.Lerp(0.45f, 1f, Mathf.Max(0f, balance) / 0.45f) : 1f);
                float lean = pose == PoseKind.Cower ? 20f : pose == PoseKind.Crouch ? 15f : Mathf.InverseLerp(0.8f, 5f, moveSpeed) * 7f;
                // you: held upright and square much more firmly (a steady first-person view), unless really hurt
                float hold = 1f;
                if (grit > 1f) { wobble = Mathf.Lerp(wobble, 1f, 0.65f); hold = 1.9f; }
                PD(pelvis, yaw * Quaternion.Euler(lean * 0.2f, 0f, 0f), 170f * upright * wobble * hold, 20f * upright * Mathf.Sqrt(hold), M * 0.12f);
                PD(chest, yaw * Quaternion.Euler(lean, 0f, 0f), 130f * upright * wobble * hold, 16f * upright * Mathf.Sqrt(hold), M * 0.08f);

                // fall detection
                float tilt = Vector3.Angle(pelvis.transform.up, Vector3.up);
                bool settling = mode == BodyMode.GettingUp && getUpT < 2.5f;
                // (you hold on through a bigger lurch over a lip or a body before it's a fall)
                unstableTimer = tilt > (grit > 1f ? 58f : 42f) && !settling ? unstableTimer + dt : Mathf.Max(0f, unstableTimer - dt);
                lowTimer = h < 0.5f && mode == BodyMode.Standing ? lowTimer + dt : 0f;
                if (!sinking && (unstableTimer > (grit > 1f ? 0.6f : 0.35f) || lowTimer > (grit > 1f ? 1.2f : 0.6f))) Topple(unstableTimer > 0.35f ? "tilted" : "too low", 0.8f);
                if (mode == BodyMode.GettingUp && getUpT > (grit > 1f ? 4f : 3f) && tilt > (grit > 1f ? 50f : 35f)) Topple("failed to get up", 0.6f);
                IsCrawling = false;
            }
            else
            {
                UpdateDown(dt, pelvis, chest);
            }
        }

        /// <summary>Set by the brain: try to push up off the ground (strength 0..1). Too hurt = a feeble attempt.</summary>
        [HideInInspector] public float struggle;

        void UpdateDown(float dt, Rigidbody pelvis, Rigidbody chest)
        {
            if (struggle > 0.01f && !physio.dead && physio.consciousness > 0.4f && physio.posture == PostureType.None)
            {
                // arms push the chest up, knees try to come under; how far it gets depends on what is left
                float M = body.totalMass;
                float lift = struggle * Mathf.Lerp(0.25f, 0.75f, physio.Effort);
                chest.AddForce(Vector3.up * M * 9.81f * lift * 0.55f);
                pelvis.AddForce(Vector3.up * M * 9.81f * lift * 0.2f);
                for (int s = -1; s <= 1; s += 2)
                {
                    if (physio.ArmUsable(s)) SetArm(s, Shoulder(s, 75f, 15f), Mathf.Lerp(90f, 20f, struggle));
                    SetLeg(s, 70f * struggle, 95f * struggle, 20f);
                }
                Set(PartId.Neck, AX(-20f));
            }
            var ct = chest.transform;
            float faceDown = Vector3.Dot(ct.forward, Vector3.down);
            IsProne = faceDown > 0.3f;
            bool able = !physio.dead && physio.consciousness > 0.4f && physio.posture == PostureType.None;
            bool armsOk = physio.ArmUsable(-1) || physio.ArmUsable(1);
            IsCrawling = false;
            if (!able || !wantCrawl || !armsOk || downTime < 1.2f) return;

            Vector3 dir = Vector3.ProjectOnPlane(crawlDir, Vector3.up);
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();

            if (!IsProne)
            {
                // roll onto the front first
                Vector3 longAxis = Vector3.ProjectOnPlane(ct.up, Vector3.up);
                if (longAxis.sqrMagnitude < 0.01f) longAxis = dir;
                var goal = Quaternion.LookRotation(Vector3.down, longAxis.normalized);
                PD(chest, goal, 30f * physio.Effort, 6f, body.totalMass * 0.05f);
                PD(pelvis, goal, 20f * physio.Effort, 5f, body.totalMass * 0.05f);
                return;
            }

            IsCrawling = true;
            // pull with whichever arm is in its pull stroke
            float arms = (physio.ArmUsable(-1) ? 0.5f : 0f) + (physio.ArmUsable(1) ? 0.5f : 0f);
            float stroke = Mathf.Abs(Mathf.Cos(crawlPhase));
            float pull = 210f * arms * stroke * Mathf.Lerp(0.5f, 1.1f, physio.Effort);
            chest.AddForce(dir * pull + Vector3.up * 70f * stroke);
            pelvis.AddForce(dir * pull * 0.25f);
            // turn the body toward the crawl direction
            Vector3 head = Vector3.ProjectOnPlane(ct.up, Vector3.up).normalized;
            float ang = Vector3.SignedAngle(head, dir, Vector3.up);
            chest.AddTorque(Vector3.up * ang * 0.08f, ForceMode.Acceleration);
        }

        static void PD(Rigidbody rb, Quaternion goal, float kp, float kd, float inertia)
        {
            Quaternion q = goal * Quaternion.Inverse(rb.rotation);
            q.ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            if (float.IsNaN(axis.x) || Mathf.Abs(ang) < 0.01f) axis = Vector3.zero;
            Vector3 tq = axis * (ang * Mathf.Deg2Rad * kp) - rb.angularVelocity * kd;
            rb.AddTorque(tq * inertia, ForceMode.Force);
        }
    }
}
