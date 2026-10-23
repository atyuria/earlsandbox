using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    public enum EarlWeapon { None, AK47, Pistol, Shotgun, Knife, Sword, AR15, Beretta }

    /// <summary>
    /// An Earl that fights back, and means it. The weapon is a real object in his hand and his arms hold it the
    /// way a person does (ActiveRagdoll.aimDir): a rifle shouldered with the other hand under the handguard, a
    /// pistol pushed out, a blade up in guard. Hurt arms shake and sag, one arm can't steady a rifle, recoil throws
    /// the hand. He never puts his hands up: he finds cover he can shoot over, ducks behind it to reload, and if
    /// you hide for long he comes looking. With a blade he closes in, guards, winds up and cuts; blades can be
    /// parried. On the ground some keep fighting while they still can.
    /// </summary>
    public partial class EarlGunner : MonoBehaviour
    {
        public EarlWeapon kind;
        HumanBody body;
        GameObject weapon;
        Transform muzzle;
        Vector3 gripLocal, tipLocal;
        Quaternion restLocal = Quaternion.identity;
        int rounds, magSize;
        float nextShot, reloadUntil, keepFightingDown = -1f, seed;
        bool dropped;
        AmmoType ammo;

        // senses and plans
        float sightCheck, lastSeen = -99f, coverCheck, nextCrouch;
        bool sees;
        Vector3 lastSeenPos;
        Vector3? cover;

        // blade fighting
        enum Swing { Guard, WindUp, Strike, Recover }
        Swing swing;
        float swingT, nextAttack;
        Vector3 lastTip, lastHilt;
        bool tipValid, struck;

        /// <summary>What is fitted to his gun (optic, grip, suppressor, light), or null for a bare gun.</summary>
        public WeaponLoadout kit;
        public bool Suppressed => kit != null && kit.suppressor;

        // squad (TrenchSquad): his place in it, whether he is one to lean out round corners, where he holds
        public TrenchSquad squad;
        public SquadRole role = SquadRole.Point;
        public bool leans = true;
        public Vector3 holdPoint;
        float holdUntil = -1f;
        /// <summary>Still in the fight: alive, conscious, gun in hand.</summary>
        public bool Fighting => body != null && weapon != null && !dropped && !body.physio.dead && body.physio.consciousness > 0.35f;

        public static EarlGunner Arm(HumanBody h, EarlWeapon w, WeaponLoadout kit = null, int team = Teams.Enemy)
        {
            if (h == null || w == EarlWeapon.None) return null;
            var g = h.gameObject.AddComponent<EarlGunner>();
            g.kind = w; g.body = h; g.seed = Random.Range(0f, 500f); g.kit = kit;
            g.aggression = Random.Range(0.2f, 1f);
            h.gunner = g;
            All.Add(g);
            g.Build();
            g.SetTeam(team);
            return g;
        }

        /// <summary>Which side he fights for (0 yours, 1 theirs), with the armband to match.</summary>
        public void SetTeam(int t)
        {
            team = t;
            Teams.Armband(body, t);
        }

        bool Gun => kind == EarlWeapon.AK47 || kind == EarlWeapon.Pistol || kind == EarlWeapon.Shotgun || kind == EarlWeapon.AR15 || kind == EarlWeapon.Beretta;
        bool Rifle => kind == EarlWeapon.AK47 || kind == EarlWeapon.AR15;
        bool Handgun => kind == EarlWeapon.Pistol || kind == EarlWeapon.Beretta;
        bool TwoHanded => Rifle || kind == EarlWeapon.Shotgun;
        string WeaponName => kind == EarlWeapon.AK47 ? "AK-47" : kind == EarlWeapon.AR15 ? "AR-15" : kind == EarlWeapon.Beretta ? "BERETTA M9" : kind == EarlWeapon.Pistol ? "GLOCK 17" : kind == EarlWeapon.Shotgun ? "MOSSBERG 590" : kind == EarlWeapon.Knife ? "KNIFE" : "SWORD";
        public Vector3 BladeTip => weapon != null ? weapon.transform.TransformPoint(tipLocal) : Vector3.zero;
        public Vector3 BladeHilt => weapon != null ? weapon.transform.TransformPoint(gripLocal) : Vector3.zero;
        public bool BladeOut => !Gun && weapon != null && !dropped;

        void Build()
        {
            string model = kind == EarlWeapon.AK47 ? "Gun_AK47" : kind == EarlWeapon.AR15 ? "Gun_AR15" : kind == EarlWeapon.Beretta ? "Gun_Beretta" : kind == EarlWeapon.Pistol ? "Gun_Glock" : kind == EarlWeapon.Shotgun ? "Gun_Shotgun" : kind == EarlWeapon.Knife ? "Gun_Knife" : "Gun_Sword";
            var hand = body[PartId.HandR];
            var m = ModelLibrary.Get(model);
            if (hand == null || m == null) return;
            weapon = ModelLibrary.Spawn(model, null, ModelLibrary.GunMaterial, true);
            gripLocal = m.points.TryGetValue("Grip", out var gp) ? gp : Vector3.zero;
            tipLocal = m.points.TryGetValue("Muzzle", out var mp) ? mp : Vector3.forward * 0.3f;
            Vector3 fwd = Vector3.ProjectOnPlane(body.Chest.transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.1f) fwd = transform.forward;
            weapon.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            weapon.transform.position = hand.transform.position - weapon.transform.rotation * gripLocal;
            weapon.transform.SetParent(hand.transform, true);
            restLocal = weapon.transform.localRotation;
            weapon.layer = hand.gameObject.layer;
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(weapon.transform, false);
            muzzle.localPosition = tipLocal;
            // his attachments: the muzzle moves out to the end of a suppressor
            if (Gun && kit != null) muzzle.localPosition = GunKit.Fit(weapon.transform, model, kit, weapon.layer);
            var col = weapon.AddComponent<BoxCollider>();
            col.center = m.mesh.bounds.center; col.size = m.mesh.bounds.size;
            // while held it is placed every step, so a solid box would ram whatever it overlaps (you, at 10 m/s);
            // what the blade meets is found by the swing's own sweep instead. Solid again once dropped.
            col.isTrigger = true;
            foreach (var bp in body.partList) foreach (var c in bp.GetComponentsInChildren<Collider>()) if (c != col) Physics.IgnoreCollision(col, c, true);
            if (Gun)
            {
                ammo = AmmoTable.For(WeaponName)[0];
                magSize = Rifle ? 30 : kind == EarlWeapon.Pistol ? 17 : kind == EarlWeapon.Beretta ? 15 : 8;
                rounds = magSize;
            }
        }

        // ------------------------------------------------------------ the player

        /// <summary>Line of sight from a point to the player, through bodies and loose objects but not walls.</summary>
        static readonly RaycastHit[] clearBuf = new RaycastHit[32];
        public static bool Clear(Vector3 from, Vector3 to, HumanBody self)
        {
            Vector3 d = to - from; float len = d.magnitude;
            // (no garbage: every Earl checks this several times a second)
            int n = Physics.RaycastNonAlloc(from, d / Mathf.Max(1e-4f, len), clearBuf, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hit = clearBuf[i];
                if (hit.collider is CharacterController) continue;
                var hb = hit.collider.GetComponentInParent<HumanBody>();
                if (hb != null) continue;
                if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
                if (FlameTank.IsTank(hit.collider)) continue;      // (the tanks on your back are you, near enough)
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------ what he can still do

        float ArmHealth(int side)
        {
            PartId u = side < 0 ? PartId.UpperArmL : PartId.UpperArmR, f = side < 0 ? PartId.ForearmL : PartId.ForearmR, hnd = side < 0 ? PartId.HandL : PartId.HandR;
            float h = 1f;
            foreach (var id in new[] { u, f, hnd })
            {
                var bp = body[id];
                if (bp == null || bp.severed) return 0f;
                h = Mathf.Min(h, 1f - bp.tissueDamage);
                foreach (var o in bp.organs) if (o.IsBone && o.Fractured) h *= 0.4f;
            }
            return body.physio.ArmUsable(side) ? h : 0f;
        }

        float Shakiness()
        {
            var p = body.physio;
            bool support = !TwoHanded || ArmHealth(-1) > 0.3f;
            return (1f - ArmHealth(1)) * 2.5f + p.EffectivePain * 1.5f + (1f - p.consciousness) * 3f + (support ? 0f : 2.2f) + (body.ragdoll.IsDown ? 1f : 0f) + p.LossFrac * 2f;
        }

        void Drop()
        {
            if (dropped || weapon == null) return;
            dropped = true;
            if (torch != null) torch.enabled = false;
            // left lying with the map (cleared with it, not carried over into the next one)
            weapon.transform.SetParent(SandboxGame.Instance != null ? SandboxGame.Instance.MapRoot : null, true);
            var rb = weapon.AddComponent<Rigidbody>();
            rb.mass = TwoHanded ? 3.5f : 1f;
            var hand = body[PartId.HandR];
            if (hand != null && hand.rb != null) rb.linearVelocity = hand.rb.linearVelocity;
            var col = weapon.GetComponent<Collider>();
            col.isTrigger = false;
            foreach (var bp in body.partList) foreach (var c in bp.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c, false);
            body.ragdoll.aimDir = null;
            body.brain.hostile = false;
        }

        /// <summary>Struck by your blade: a hard enough knock tears it out of his hand, a lighter one spoils his swing.</summary>
        public void Knocked(Vector3 impulse)
        {
            var hand = body[PartId.HandR];
            if (hand != null && hand.rb != null) hand.rb.AddForce(impulse, ForceMode.Impulse);
            if (swing == Swing.Strike || swing == Swing.WindUp) { swing = Swing.Recover; swingT = 0f; }
            if (impulse.magnitude > 7f * Mathf.Max(0.3f, ArmHealth(1))) Drop();
        }

        // ------------------------------------------------------------ deciding

        static readonly Unity.Profiling.ProfilerMarker PM_LateUpdate = new Unity.Profiling.ProfilerMarker("Carl.EarlGunner.LateUpdate");

        void LateUpdate()
        {
            Think();
            if (body == null || weapon == null || dropped || WorldFreeze.On) return;
            AvoidWalls();
            PlaceWeapon();
        }

        void Think()
        {

            using var pmScope = PM_LateUpdate.Auto();
            if (body == null || weapon == null || dropped || WorldFreeze.On) return;
            var p = body.physio;
            var rag = body.ragdoll;
            var brain = body.brain;
            if (p.dead || ArmHealth(1) <= 0f || body[PartId.HandR] == null || body[PartId.HandR].severed || TaserShock.Active(body) || Burning.Intensity(body) > 0.2f) { Drop(); return; }
            suppression = Mathf.Max(0f, suppression - Time.deltaTime * 0.35f);
            bool able = brain != null && brain.aiEnabled && p.consciousness > 0.35f;
            if (rag.IsDown)
            {
                // on the ground: some keep fighting while they can hold the weapon up, some give up
                if (keepFightingDown < 0f) keepFightingDown = Random.value < 0.6f ? 1f : 0f;
                able &= keepFightingDown > 0.5f && p.consciousness > 0.5f && Gun;
            }
            else keepFightingDown = -1f;
            brain.hostile = able;
            if (!able) { rag.aimDir = null; if (torch != null) torch.enabled = false; return; }
            // (the brain leaves a fighting man to us: knocked down, he gets back up when he can, shooting meanwhile)
            if (rag.IsDown) { rag.wantStand = p.CanStand && p.EffectivePain < 0.8f; rag.wantCrawl = false; }
            // both hands on the rungs
            if (LadderClimber.Busy(body)) { rag.aimDir = null; return; }

            Sense();
            UpdateTorch();
            // his footsteps (louder running, and on the duckboards) carry to anyone close on the other side
            if (Time.time > nextStep && rag.moveSpeed > 0.4f) { nextStep = Time.time + 0.6f; Intel.Noise(body.Center, team, rag.moveSpeed > 2.5f ? 16f : 9f); }
            Vector3 chest = body.Chest.transform.position;
            rag.pose = PoseKind.Stand;
            rag.holdPart = null;
            rag.panic = 0f;
            brain.activity = Activity.Alert;
            rag.aimHold = Gun ? (TwoHanded ? 2 : 1) : 3;
            float legs = Mathf.Clamp01(p.LegSupport(-1) + p.LegSupport(1) - 0.6f);
            if (!rag.IsDown) rag.crouch = 0f;

            // a grenade at his feet comes before everything
            if (!rag.IsDown && FleeGrenade(legs)) { KeepSpacing(); return; }
            // a drone: shoot it down, or get out from under it
            if (!rag.IsDown && DroneBehaviour(legs)) { KeepSpacing(); return; }

            Vector3 target = sees ? enemyAim : suspect;
            Vector3 to = target - chest;
            float dist = to.magnitude;
            if (sees)
            {
                rag.lookAt = target;
                rag.faceDir = Vector3.ProjectOnPlane(to, Vector3.up).normalized;
                rag.aimDir = AimWithError(target);
            }
            else
            {
                Vector3 fwd = rag.faceDir.sqrMagnitude > 0.01f ? rag.faceDir : Vector3.ProjectOnPlane(body.Chest.transform.forward, Vector3.up).normalized;
                rag.aimDir = fwd * 0.9f + Vector3.down * 0.3f;   // gun at the low ready
                rag.lookAt = null;
            }

            if (!Gun)
            {
                bool known = sees || SuspectConf > 0.15f;
                if (!known && !TrenchNav.Ready && team == Teams.Enemy && PlayerAvatar.Body != null) { target = PlayerAvatar.Body.Center; known = true; }
                if (known) BladeBehaviour(sees, target, Vector3.Distance(target, chest), legs);
                else Idle(legs);
                KeepSpacing();
                return;
            }
            if (sees)
            {
                if (enemy != null && enemy.Pelvis != null) ConsiderGrenade(enemy.Pelvis.transform.position - Vector3.up * 0.8f, false);
                if (!OrderWhileFighting(legs)) GunBehaviour(sees, target, dist, legs);
                else if (rag.crouch < 0.5f) Shoot(target);
            }
            else if (rag.IsDown)
            {
                // lying there: gun on where they were
                if (SuspectConf > 0.1f) { rag.aimDir = (suspect - (chest + Vector3.up * 0.15f)).normalized; rag.lookAt = suspect; }
            }
            else if (DoOrders(legs)) { }
            else if (SuspectConf > 0.15f) Investigate(legs);
            else Idle(legs);
            UpdatePrefire();
            KeepSpacing();
        }

        /// <summary>
        /// Never walk into (or over) a mate. Someone going the same way ahead is followed at his pace, not
        /// overtaken; someone crossing or coming the other way is let by. Only a man who isn't going anywhere
        /// (standing, holding an angle) is squeezed past - or one lying there, who is stepped over. Two meeting
        /// head-on in a trench: one stops and the other squeezes by. Anyone right next to you is stepped away from.
        /// </summary>
        void KeepSpacing()
        {
            if (squeezes.Count > 0 && Time.frameCount % 10 == 0) EndSqueezes();
            var rag = body.ragdoll;
            queuing = false; goingSpeed = 0f; yieldDbg = null;
            if (rag.IsDown || body.Pelvis == null || SpacingOff) return;
            Vector3 me = body.Pelvis.transform.position;
            Vector3 push = Vector3.zero;
            bool yieldNow = false, sidestep = false;
            float follow = float.MaxValue, goD = 9f, standD = 9f;
            HumanBody going = null, standing = null;
            foreach (var o in HumanBody.All)
            {
                if (o == null || o == body || o.Dead || o == PlayerAvatar.Body || o.Pelvis == null) continue;
                Vector3 d = Vector3.ProjectOnPlane(o.Pelvis.transform.position - me, Vector3.up);
                float dist = d.magnitude;
                if (dist > 3f || Mathf.Abs(o.Pelvis.transform.position.y - me.y) > 1.2f) continue;
                if (Squeezing(o)) continue;
                // someone lying on the floor is stepped over, not walked round
                if (o.ragdoll.IsDown) continue;
                Vector3 dn = dist > 1e-3f ? d / dist : transform.right;
                if (dist < 1.3f) push -= dn * (1.3f - dist);
                if (rag.moveSpeed > 0.05f && dist < 2.4f && Vector3.Dot(dn, rag.moveDir) > 0.55f)
                {
                    // he is in the way: is he going somewhere (or waiting in line behind someone who is)?
                    var om = o.ragdoll;
                    bool moving = o.gunner != null ? o.gunner.goingSpeed > 0.25f || o.gunner.queuing : om != null && om.moveSpeed > 0.25f;
                    if (moving)
                    {
                        // coming at me - or waiting on me while I wait on him: one of the two has to give way
                        bool headOn = Vector3.Dot(om.moveDir, -dn) > 0.55f || o.gunner != null && o.gunner.waitingFor == body;
                        bool sameWay = om.moveSpeed > 0.25f && Vector3.Dot(om.moveDir, rag.moveDir) > 0.3f;
                        if (headOn && body.number < o.number) { if (TrenchNav.Ready) { yieldNow = true; yieldDbg = o; } else sidestep = true; }
                        else if (sameWay && dist > 1.5f) follow = Mathf.Min(follow, om.moveSpeed);   // just keep behind him
                        else if (dist < goD) { goD = dist; going = o; }
                    }
                    else if (dist < standD) { standD = dist; standing = o; }
                }
            }
            if (follow < float.MaxValue) rag.moveSpeed = Mathf.Min(rag.moveSpeed, follow);
            // waiting on a man who's going somewhere: he'll be out of the way in a moment (squeezed past only if
            // it's jammed solid); a man standing there is squeezed past after a moment
            HumanBody blocker = yieldNow ? null : going ?? standing;
            bool wait = yieldNow || blocker != null;
            queuing = going != null && !yieldNow;
            if (blocker != null && blocker == waitingFor) waitT += Time.deltaTime; else { waitingFor = blocker; waitT = 0f; }
            if (blocker != null && waitT > (going != null ? 6f : 0.7f)) { if (going != null) PassedMoving++; else PassedStill++; Squeeze(body, blocker, 3f); wait = false; waitT = 0f; queuing = false; }
            if (sidestep) { rag.moveDir = Quaternion.AngleAxis(75f, Vector3.up) * rag.moveDir; rag.moveSpeed = Mathf.Min(rag.moveSpeed, 0.9f); }
            else if (wait) rag.moveSpeed = 0f;
            goingSpeed = rag.moveSpeed;   // (where he's trying to go - not being nudged aside below)
            if (push.sqrMagnitude > 1e-4f)
            {
                Vector3 m = rag.moveDir * Mathf.Max(rag.moveSpeed, 0.01f) + push * 2.5f;
                rag.moveDir = m.normalized;
                rag.moveSpeed = Mathf.Max(rag.moveSpeed, Mathf.Min(0.8f, push.magnitude * 2f));
            }
        }

        HumanBody waitingFor;
        float waitT;
        /// <summary>Stopped only because someone ahead is on the move (in a line, not standing there).</summary>
        public bool queuing;
        /// <summary>How fast he means to go this frame (before being nudged out of someone's way).</summary>
        public float goingSpeed;
        /// <summary>For the playtests: squeezes past a man on the move (only when jammed solid) and past one standing there.</summary>
        public static int PassedMoving, PassedStill;
        HumanBody yieldDbg;
        /// <summary>For the playtests: no keeping apart at all (to compare).</summary>
        public static bool SpacingOff;
        public string SpacingDebug => $"wait {(waitingFor != null ? "#" + waitingFor.number : "-")} {waitT:0.0}s" + (yieldDbg != null ? $" giving way to #{yieldDbg.number} ({(yieldDbg.gunner != null ? yieldDbg.gunner.Status + " going " + yieldDbg.gunner.goingSpeed.ToString("0.0") + (yieldDbg.gunner.queuing ? " queuing" : "") : "no gun")} at {yieldDbg.Pelvis.transform.position})" : "");
        static readonly List<(HumanBody a, HumanBody b, float until)> squeezes = new List<(HumanBody, HumanBody, float)>();

        bool Squeezing(HumanBody o)
        {
            foreach (var q in squeezes) if ((q.a == body && q.b == o || q.a == o && q.b == body) && Time.time < q.until) return true;
            return false;
        }

        /// <summary>Two men passing in a narrow trench: their bodies don't block each other for a few seconds.</summary>
        static void Squeeze(HumanBody a, HumanBody b, float secs)
        {
            if (a == null || b == null) return;
            SetPass(a, b, true);
            squeezes.Add((a, b, Time.time + secs));
        }

        static void SetPass(HumanBody a, HumanBody b, bool ignore)
        {
            foreach (var pa in a.partList) if (pa != null && pa.col != null)
                    foreach (var pb in b.partList) if (pb != null && pb.col != null) Physics.IgnoreCollision(pa.col, pb.col, ignore);
        }

        /// <summary>Squeezes that are over: solid to each other again.</summary>
        static void EndSqueezes()
        {
            for (int i = squeezes.Count - 1; i >= 0; i--)
            {
                var q = squeezes[i];
                if (q.a == null || q.b == null) { squeezes.RemoveAt(i); continue; }
                if (Time.time < q.until) continue;
                // (not while they're still tangled up in each other)
                if (q.a.Pelvis != null && q.b.Pelvis != null && Vector3.Distance(q.a.Pelvis.transform.position, q.b.Pelvis.transform.position) < 0.8f) continue;
                SetPass(q.a, q.b, false);
                squeezes.RemoveAt(i);
            }
        }

        /// <summary>Holding a spot and letting you come to him: on the spot, gun up where you'll come from, leaning
        /// out round the corner if he is one to and that's where you would be. After a while he moves up.</summary>
        void HoldBehaviour(bool see, Vector3 target, float legs)
        {
            var rag = body.ragdoll;
            if (holdUntil < 0f) holdUntil = Time.time + Random.Range(35f, 80f);
            // left on his own, or waited long enough: move up behind the others
            int others = 0;
            if (squad != null) foreach (var m in squad.members) if (m != this && TrenchSquad.Active(m)) others++;
            if (Time.time > holdUntil || others == 0) { role = SquadRole.Follow; squad?.Promote(); return; }
            Vector3 g = Vector3.ProjectOnPlane(holdPoint - body.Pelvis.transform.position, Vector3.up);
            rag.moveDir = g.sqrMagnitude > 1e-4f ? g.normalized : rag.faceDir;
            rag.moveSpeed = g.magnitude > 1f ? 1.4f * legs : 0f;
            Vector3 watch = lastSeen > 0f ? lastSeenPos : target;
            if (!see)
            {
                Vector3 chest = body.Chest.transform.position;
                Vector3 look = watch + Vector3.up * 1.2f;
                rag.faceDir = Vector3.ProjectOnPlane(look - chest, Vector3.up).normalized;
                rag.aimDir = (look - (chest + Vector3.up * 0.15f)).normalized;
            }
            rag.crouch = 0f;
            rag.lean = Mathf.MoveTowards(rag.lean, leans && !see ? PeekSide(watch) : 0f, Time.deltaTime * 3f);
            if (see) Shoot(target);
        }

        /// <summary>In file behind the man ahead: keep a few metres back, stop when he stops, look where he looks.</summary>
        bool FollowBehaviour(Vector3 target, float legs)
        {
            var ahead = squad != null ? squad.Ahead(this) : null;
            if (ahead == null) return false;
            var rag = body.ragdoll;
            Vector3 me = body.Pelvis.transform.position, him = ahead.body.Pelvis.transform.position;
            Vector3 g = Vector3.ProjectOnPlane(him - me, Vector3.up);
            float d = g.magnitude;
            const float spacing = 3.5f;
            if (d > spacing + 0.4f) MoveTo(him, (d > 9f ? 3f : 1.5f) * legs);
            else rag.moveSpeed = 0f;
            rag.crouch = 0f;
            rag.lean = Mathf.MoveTowards(rag.lean, 0f, Time.deltaTime * 3f);
            // weapon at the low ready over his shoulder, on where he is going
            Vector3 chest = body.Chest.transform.position;
            Vector3 look = (ahead.lastSeen > 0f ? ahead.lastSeenPos : target) + Vector3.up * 1.2f;
            rag.faceDir = Vector3.ProjectOnPlane(d > spacing + 0.4f ? g : look - chest, Vector3.up).normalized;
            rag.aimDir = ((look - (chest + Vector3.up * 0.15f)).normalized + Vector3.down * 0.35f).normalized;
            rag.lookAt = look;
            return true;
        }

        /// <summary>His aim: at you, plus a wandering error that grows with every injury.</summary>
        Vector3 AimWithError(Vector3 target)
        {
            float t = Time.time, shake = Shakiness();
            Vector3 wobble = new Vector3(Mathf.PerlinNoise(t * 1.3f, seed) - 0.5f, Mathf.PerlinNoise(seed, t * 1.1f) - 0.5f, Mathf.PerlinNoise(t, seed * 0.7f) - 0.5f) * (0.05f + shake * 0.1f);
            Vector3 sh = body.Chest.transform.position + Vector3.up * 0.15f;
            return ((target + wobble * Vector3.Distance(target, sh)) - sh).normalized;
        }

        /// <summary>In a fight he can see: cover he can shoot over, crouch in it to reload, up to shoot.</summary>
        void GunBehaviour(bool see, Vector3 target, float dist, float legs)
        {
            var rag = body.ragdoll;
            bool reloading = Time.time < reloadUntil;
            squad?.Promote();
            stance = Stance.None;
            // some hold where they are and let you come to them
            if (role == SquadRole.Hold && !rag.IsDown) { HoldBehaviour(see, target, legs); return; }
            // cover: somewhere he can duck behind and still shoot over
            if (Time.time > coverCheck && !rag.IsDown)
            {
                coverCheck = Time.time + 3f;
                if (cover == null || !Clear(cover.Value + Vector3.up * 1.5f, target, body)) { cover = FindCover(target); TrenchSquad.Claim(this, cover); }
            }
            Vector3 pos = body.Pelvis.transform.position; pos.y = 0f;
            searchPoint = null; searchCount = 0; searched.Clear();
            Vector3? goal = null;
            if (cover.HasValue && Vector3.Distance(new Vector3(cover.Value.x, 0f, cover.Value.z), pos) > 0.6f) goal = cover;
            else if (dist > 30f) goal = target;
            if (goal.HasValue && !rag.IsDown) MoveTo(goal.Value, 2.4f * legs);
            else rag.moveSpeed = 0f;
            rag.faceDir = Vector3.ProjectOnPlane(target - body.Chest.transform.position, Vector3.up).normalized;
            // in cover: crouched to reload and every so often, up to shoot; pinned down by fire, longer
            bool inCover = cover.HasValue && Vector3.Distance(new Vector3(cover.Value.x, 0f, cover.Value.z), pos) < 0.8f;
            if (inCover && (reloading || Time.time < nextCrouch || suppression > 0.9f)) { rag.crouch = 1f; rag.pose = PoseKind.Crouch; }
            else rag.crouch = 0f;
            if (inCover && !reloading && Time.time > nextCrouch + 4f) nextCrouch = Time.time + Random.Range(1f, 2.2f) + suppression;
            rag.lean = Mathf.MoveTowards(rag.lean, 0f, Time.deltaTime * 3f);
            if (rag.crouch < 0.5f) Shoot(target);
        }

        // ------------------------------------------------------------ hunting you down

        float peekCheck, peekHold; int peekSide;
        Vector3? searchPoint; int searchCount;
        readonly List<Vector3> searched = new List<Vector3>();

        /// <summary>
        /// Which way to lean to see a point round a corner: 0 if it is in plain view (or can't be seen either way).
        /// </summary>
        int PeekSide(Vector3 focus)
        {
            if (Time.time < peekCheck) return peekSide;
            peekCheck = Time.time + 0.3f;
            if (body.Head == null) return peekSide = 0;
            Vector3 head = body.Head.transform.position;
            Vector3 f = Vector3.ProjectOnPlane(focus - head, Vector3.up);
            if (f.sqrMagnitude < 0.01f) return peekSide = 0;
            Vector3 right = Vector3.Cross(Vector3.up, f.normalized);
            Vector3 aim = focus + Vector3.up * 0.3f;
            int was = peekSide;
            if (Clear(head, aim, body)) peekSide = 0;
            else if (Clear(head + right * 0.45f, aim, body)) peekSide = 1;
            else if (Clear(head - right * 0.45f, aim, body)) peekSide = -1;
            else peekSide = 0;
            if (peekSide != 0 && was == 0) peekHold = Time.time + Random.Range(0.8f, 1.4f);   // stop and slice the pie
            return peekSide;
        }

        /// <summary>
        /// Lost you: walk (not run) to where you were last seen or heard with the gun up on that spot, leaning to
        /// check round each corner before stepping past it; then clear the spots round about one by one, working
        /// towards wherever you are likeliest to be.
        /// </summary>
        void Hunt(Vector3 target, float legs)
        {
            var rag = body.ragdoll; var brain = body.brain;
            // a shot or a scream gives you away
            // (his own shots count as noise too: only something heard away from himself)
            if (brain.lastThreatTime > lastSeen + 0.5f && Time.time - brain.lastThreatTime < 6f && searchPoint == null
                && Vector3.Distance(brain.threatPos, body.Chest.transform.position) > 4f) { lastSeenPos = brain.threatPos; lastSeen = brain.lastThreatTime - 0.5f; searched.Clear(); }
            Vector3 focus = searchPoint ?? (lastSeen > 0f ? lastSeenPos : target);
            Vector3 chest = body.Chest.transform.position;
            Vector3 g = Vector3.ProjectOnPlane(focus - body.Pelvis.transform.position, Vector3.up);
            int side = leans ? PeekSide(focus) : 0;
            if (!leans) peekHold = 0f;
            rag.lean = Mathf.MoveTowards(rag.lean, side, Time.deltaTime * 3f);
            bool holding = Time.time < peekHold;
            rag.crouch = 0f;
            rag.moveDir = g.sqrMagnitude > 0.01f ? g.normalized : rag.faceDir;
            rag.moveSpeed = holding || g.magnitude < 1.2f ? 0f : 1.5f * legs;
            // weapon up on where you might be; head with it
            Vector3 look = focus + Vector3.up * 1.2f;
            rag.faceDir = Vector3.ProjectOnPlane(look - chest, Vector3.up).normalized;
            rag.lookAt = look;
            rag.aimDir = (look - (chest + Vector3.up * 0.15f)).normalized;
            if (g.magnitude >= 1.2f) return;
            // got there and you're not here: next spot
            searched.Add(focus);
            searchCount++;
            searchPoint = searchCount > 6 ? (Vector3?)target : NextSearchPoint(target);
        }

        /// <summary>An open spot a few metres off, not already checked, that he can walk to - nearer you scores better.</summary>
        Vector3? NextSearchPoint(Vector3 target)
        {
            Vector3 me = body.Pelvis.transform.position;
            Vector3? best = null; float bestScore = float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f + Random.value * 0.3f;
                float r = Random.Range(3f, 8f);
                Vector3 p = me + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var gh, 4f, ~0, QueryTriggerInteraction.Ignore) || gh.collider.GetComponentInParent<HumanBody>() != null) continue;
                Vector3 feet = gh.point;
                if (!Clear(me + Vector3.up * 0.5f, feet + Vector3.up * 0.5f, body)) continue;
                bool done = false;
                foreach (var s in searched) if (Vector3.Distance(s, feet) < 3f) { done = true; break; }
                if (done) continue;
                float score = Vector3.Distance(feet, target) + Random.Range(0f, 4f);
                if (score < bestScore) { bestScore = score; best = feet; }
            }
            return best ?? target;
        }

        /// <summary>A spot near him where a man crouching is hidden from you but standing can shoot over.</summary>
        Vector3? FindCover(Vector3 target)
        {
            Vector3 me = body.Pelvis.transform.position;
            Vector3? best = null; float bestScore = float.MaxValue;
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f + seed;
                float r = 2f + (i % 3) * 3.5f;
                Vector3 p = me + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var g, 4f, ~0, QueryTriggerInteraction.Ignore) || g.collider.GetComponentInParent<HumanBody>() != null) continue;
                Vector3 feet = g.point;
                bool hiddenLow = !Clear(feet + Vector3.up * 0.8f, target, body);
                bool clearHigh = Clear(feet + Vector3.up * 1.55f, target, body);
                if (!hiddenLow || !clearHigh) continue;
                if (TrenchSquad.Taken(this, feet)) continue;   // a mate is already there
                float score = Vector3.Distance(feet, me) + Mathf.Abs(Vector3.Distance(feet, target) - 15f) * 0.2f;
                if (score < bestScore) { bestScore = score; best = feet; }
            }
            return best;
        }

        void Shoot(Vector3 target)
        {
            if (Time.time < nextShot || Time.time < reloadUntil) return;
            if (rounds <= 0) { rounds = magSize; reloadUntil = Time.time + (kind == EarlWeapon.Shotgun ? 4f : 2.6f); Sfx.Reload(WeaponName, muzzle.position, 2.5f); nextCrouch = reloadUntil; return; }
            Vector3 toT = (target - muzzle.position).normalized;
            if (AllyInLine(target)) return;
            float shake = Shakiness();
            if (Vector3.Angle(weapon.transform.forward, toT) > 8f + shake * 2f) return;
            float spread = (kind == EarlWeapon.Shotgun ? ammo.spread : Rifle ? 0.9f : 1.4f) + shake * 1.1f;
            int pellets = Mathf.Max(1, ammo.pellets);
            Ballistics.FiringBody = body;
            if (weapon.transform.forward.y > 0.45f) Intel.SkyShots++;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), weapon.transform.forward) * Quaternion.AngleAxis(Random.Range(0f, spread), weapon.transform.up) * weapon.transform.forward;
                Ballistics.Instance?.Fire(muzzle.position + dir * 0.02f, dir, ammo.velocity * Random.Range(0.97f, 1.03f), ammo.mass, ammo.diameter, HitKind.Bullet, ammo.tracer && i == 0, null, ammo);
            }
            Ballistics.FiringBody = null;
            rounds--;
            Sfx.Gun(WeaponName, muzzle.position, Suppressed);
            Fx.Muzzle(muzzle.position, weapon.transform.forward, (kind == EarlWeapon.Shotgun ? 1.6f : 1f) * (Suppressed ? 0.25f : 1f));
            {
                // the empty case out of the right side, a little behind the muzzle line
                var wt = weapon.transform;
                var ck = kind == EarlWeapon.AK47 ? CaseKind.SevenSixTwo : kind == EarlWeapon.AR15 ? CaseKind.FiveFiveSix : kind == EarlWeapon.Shotgun ? CaseKind.TwelveGauge : (kind == EarlWeapon.Pistol || kind == EarlWeapon.Beretta) ? CaseKind.Nine : CaseKind.None;
                Casing.Eject(ck, muzzle.position - wt.forward * (kind == EarlWeapon.Pistol || kind == EarlWeapon.Beretta ? 0.12f : 0.45f) + wt.up * 0.02f, wt.right, wt.up, -wt.forward, Vector3.zero);
            }
            Fx.BlastDust(muzzle.position, weapon.transform.forward, (kind == EarlWeapon.Shotgun ? 1.6f : 1f) * (Suppressed ? 0.4f : 1f));
            HumanBrain.BroadcastNoise(muzzle.position, Suppressed ? 22f : 60f, 0.4f);
            Intel.Gunshot(muzzle.position, team, Suppressed ? 22f : 60f, body, target);
            var hand = body[PartId.HandR];
            float oneHand = TwoHanded && ArmHealth(-1) <= 0.3f ? 2.2f : 1f;
            float kick = (kind == EarlWeapon.Shotgun ? 9f : kind == EarlWeapon.AK47 ? 3.5f : kind == EarlWeapon.AR15 ? 2.5f : 2f) * oneHand;
            if (hand != null && hand.rb != null)
            {
                hand.rb.AddForce(-weapon.transform.forward * kick + Vector3.up * kick * 0.6f, ForceMode.Impulse);
                hand.rb.AddTorque(-weapon.transform.right * kick * 0.05f, ForceMode.Impulse);
            }
            float rpm = kind == EarlWeapon.AK47 ? 600f : kind == EarlWeapon.AR15 ? 750f : Handgun ? 240f : 55f;
            nextShot = Time.time + 60f / rpm + (Rifle && Random.value < 0.25f ? Random.Range(0.4f, 1f) : 0f) + shake * 0.08f;
        }

        // ------------------------------------------------------------ blades

        void BladeBehaviour(bool see, Vector3 target, float dist, float legs)
        {
            var rag = body.ragdoll;
            Vector3 flat = Vector3.ProjectOnPlane(target - body.Chest.transform.position, Vector3.up);
            Vector3 goal = see ? target : (Time.time - lastSeen < 12f ? lastSeenPos : target);
            Vector3 g = Vector3.ProjectOnPlane(goal - body.Pelvis.transform.position, Vector3.up);
            float stop = kind == EarlWeapon.Sword ? 1.25f : 0.8f;
            rag.moveDir = g.normalized;
            rag.moveSpeed = (g.magnitude > stop ? 3.8f : 0f) * legs;
            if (see) rag.faceDir = flat.normalized;
            float dt = Time.deltaTime;
            float slow = Mathf.Lerp(1.6f, 1f, ArmHealth(1));       // a hurt arm is slower
            swingT += dt;
            switch (swing)
            {
                case Swing.Guard:
                    rag.swingPhase = Mathf.MoveTowards(rag.swingPhase, 0f, dt * 4f);
                    if (see && dist < stop + 0.25f && Time.time > nextAttack) { swing = Swing.WindUp; swingT = 0f; }
                    break;
                case Swing.WindUp:
                    rag.swingPhase = -Mathf.Clamp01(swingT / (0.35f * slow));
                    if (swingT > 0.35f * slow) { swing = Swing.Strike; swingT = 0f; struck = false; tipValid = false; Sfx.Play("knife", BladeTip, 0.9f, 1f, 2f); }
                    break;
                case Swing.Strike:
                    rag.swingPhase = Mathf.Lerp(-1f, 1f, Mathf.Clamp01(swingT / (0.22f * slow)));
                    var hand = body[PartId.HandR];
                    if (hand != null && hand.rb != null) hand.rb.AddForce((target - hand.rb.position).normalized * 30f * hand.rb.mass, ForceMode.Force);
                    if (!struck) BladeContact();
                    if (swingT > 0.22f * slow) { swing = Swing.Recover; swingT = 0f; }
                    break;
                case Swing.Recover:
                    rag.swingPhase = Mathf.MoveTowards(rag.swingPhase, 0f, dt * 3f);
                    if (swingT > 0.4f * slow) { swing = Swing.Guard; swingT = 0f; nextAttack = Time.time + Random.Range(0.6f, 1.5f) * slow; }
                    break;
            }
            lastTip = BladeTip; lastHilt = BladeHilt; tipValid = true;
        }

        /// <summary>The blade sweeping through space this frame: your blade turns it, your body is cut by it.</summary>
        static readonly RaycastHit[] bladeBuf = new RaycastHit[16];

        void BladeContact()
        {
            Vector3 tip = BladeTip, hilt = BladeHilt;
            if (!tipValid) return;
            // parry: your blade is in its path
            var wc = SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;
            if (wc != null && wc.BladeOut)
            {
                float dSeg = SegmentDistance(lastTip, tip, wc.BladeHilt, wc.BladeTip);
                if (dSeg < 0.07f)
                {
                    Vector3 at = Vector3.Lerp(lastTip, tip, 0.5f);
                    Fx.Sparks(at, (hilt - tip).normalized, 16);
                    Sfx.Play("impact_metal", at, 1f, 1f, 2f);
                    wc.Parried((tip - lastTip).normalized * 4f);
                    Knocked((lastTip - tip).normalized * 3f);
                    struck = true;
                    return;
                }
            }
            for (int k = 0; k <= 3; k++)
            {
                float f = k / 3f;
                Vector3 a = Vector3.Lerp(lastHilt, lastTip, f), b = Vector3.Lerp(hilt, tip, f);
                Vector3 dir = b - a; float len = dir.magnitude;
                // a blade point can't travel half a metre in one frame: that's the weapon being snapped back into
                // the hand, not a cut (and it reached people well out of range)
                if (len < 1e-4f || len > 0.5f) continue;
                dir /= len;
                // nearest thing in the way that isn't his own arm or the blade itself (the first hit usually is)
                int n = Physics.RaycastNonAlloc(a, dir, bladeBuf, len + 0.03f, ~0, QueryTriggerInteraction.Ignore);
                int best = -1;
                var ownCol = weapon.GetComponent<Collider>();
                for (int j = 0; j < n; j++)
                {
                    var cj = bladeBuf[j].collider;
                    if (cj == ownCol || cj is CharacterController) continue;
                    var pj = cj.GetComponentInParent<BodyPart>();
                    if (pj != null && pj.body == body) continue;
                    if (best < 0 || bladeBuf[j].distance < bladeBuf[best].distance) best = j;
                }
                if (best < 0) continue;
                var hit = bladeBuf[best];
                // and never beyond his arm plus the blade from his shoulder
                float reach = 0.7f + Vector3.Distance(hilt, tip) + 0.1f;
                if (Vector3.Distance(hit.point, body.Chest.transform.position) > reach) continue;
                var bp = hit.collider.GetComponentInParent<BodyPart>();
                if (bp != null && bp.body != null)
                {
                    if (ArmourSystem.Raycast(a, dir, len + 0.15f, out float ad, out _, bp.body) != null) { Fx.Sparks(a + dir * ad, -dir, 10); Sfx.Play("impact_metal", a + dir * ad, 0.9f, 1f, 2f); }
                    else
                    {
                        Vector3 sweep = Vector3.ProjectOnPlane(dir, hit.normal).normalized;
                        if (kind == EarlWeapon.Knife) bp.body.damage.ApplyStab(bp, hit.point, dir, Random.Range(0.08f, 0.14f));
                        else bp.body.damage.ApplySlash(bp, hit.point, hit.normal, sweep);
                    }
                    // cutting through flesh slows the blade right down
                    swingT += 0.08f;
                    struck = true;
                    return;
                }
                if (bp == null && hit.collider.attachedRigidbody == null)
                {
                    // a wall: stops dead
                    Fx.Sparks(hit.point, hit.normal, 8);
                    Sfx.Impact(hit.collider, hit.point);
                    swing = Swing.Recover; swingT = 0f;
                    struck = true;
                    return;
                }
            }
        }

        static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            CarlMath.ClosestSegmentSegment(p1, q1, p2, q2, out _, out _, out Vector3 c1, out Vector3 c2);
            return Vector3.Distance(c1, c2);
        }

        // ------------------------------------------------------------ hands on the weapon

        static readonly Unity.Profiling.ProfilerMarker PM_FixedUpdate = new Unity.Profiling.ProfilerMarker("Carl.EarlGunner.FixedUpdate");

        void FixedUpdate()
        {

            using var pmScope = PM_FixedUpdate.Auto();
            if (body == null || weapon == null || dropped || body.physio.dead || WorldFreeze.On) return;
            if (!body.ragdoll.aimDir.HasValue) return;
            // the other hand on the handguard
            if (TwoHanded && ArmHealth(-1) > 0.3f)
            {
                var left = body[PartId.HandL];
                if (left != null && left.rb != null)
                {
                    Vector3 fore = weapon.transform.TransformPoint(Vector3.Lerp(gripLocal, tipLocal, 0.42f));
                    left.rb.AddForce(Vector3.ClampMagnitude((fore - left.rb.position) * 500f - left.rb.linearVelocity * 25f, 120f) * left.rb.mass * Mathf.Lerp(0.3f, 1f, ArmHealth(-1)), ForceMode.Force);
                }
            }
        }

        /// <summary>
        /// The weapon in his hand, placed every rendered frame from where the hand is drawn. (It used to be placed
        /// on the physics step: at the 60 / 80 Hz physics of LOW and MEDIUM graphics, on a faster screen, the gun
        /// twitched against the hand moving smoothly between steps.) The wrist turns it onto the line he is looking
        /// down, as far as a wrist bends (less when hurt); whatever the arm can't take up stays as error. A pistol
        /// with nothing to point it at goes in its holster on the hip, rather than hanging the wrong way round
        /// behind him from a hand that's busy (a medic at work).
        /// </summary>
        void PlaceWeapon()
        {
            if (body.physio.dead) return;
            var hand = body[PartId.HandR];
            if (hand == null) return;
            var aim = body.ragdoll.aimDir;
            var wt = weapon.transform;
            if (!aim.HasValue)
            {
                if (!Handgun || body.Pelvis == null) return;
                var pv = body.Pelvis.transform;
                Quaternion hol = Quaternion.LookRotation(-pv.up, pv.forward);
                wt.rotation = hol;
                wt.position = pv.position + pv.right * 0.19f - pv.up * 0.04f - hol * gripLocal;
                holstered = true;
                return;
            }
            float strength = Mathf.Lerp(0.25f, 1f, ArmHealth(1)) * Mathf.Clamp01(body.physio.consciousness);
            Vector3 want = aim.Value;
            if (!Gun)
            {
                // a blade points up and forward in guard, along the swing when cutting
                float s = body.ragdoll.swingPhase;
                want = s < 0f ? Vector3.Slerp(Vector3.up * 0.8f + aim.Value * 0.4f, Vector3.up - aim.Value * 0.6f, -s)
                               : Vector3.Slerp(Vector3.up * 0.8f + aim.Value * 0.4f, aim.Value - Vector3.up * 0.3f, s);
            }
            // held on the line he is looking down; how the hand happens to be turned only adds error, more of it
            // the weaker the arm (the grip's rest angle was taken with the arm hanging, so it can't lead)
            Quaternion rest = hand.transform.rotation * restLocal;
            Quaternion lim = Quaternion.RotateTowards(Quaternion.LookRotation(want.normalized, Vector3.up), rest, Mathf.Lerp(40f, 2f, strength));
            // (drawn from the holster: straight into the hand)
            wt.rotation = holstered ? lim : Quaternion.Slerp(wt.rotation, lim, 1f - Mathf.Exp(-Time.deltaTime * 14f));
            wt.position = hand.transform.position - wt.rotation * gripLocal;
            holstered = false;
        }

        bool holstered;

        void OnDestroy() { All.Remove(this); if (weapon != null && !dropped) Destroy(weapon); }
    }
}
