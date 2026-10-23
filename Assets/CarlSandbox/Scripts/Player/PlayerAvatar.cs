using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    /// <summary>
    /// The player is an Earl too: a full HumanBody (the same ragdoll, organs, bones, arteries, brain and spine,
    /// the same wounds and the same way of losing limbs), driven by your keys instead of a brain, with the camera
    /// in its head. It walks with the same gait and limps the same way, falls over when it is knocked down or a
    /// leg goes, crawls, and gets up again if it can. Bullets, blades and blasts hit it like any other Earl.
    /// </summary>
    public class PlayerAvatar : MonoBehaviour
    {
        public static HumanBody Body;
        public static ArmourLoadout Armour = new ArmourLoadout();
        public static int ArmourPreset;
        public const int OwnLayer = 29;      // your own head (and arms while holding a weapon): not drawn by your camera
        PlayerController pc;
        Vector3 camPos;
        bool camInit;
        int armsLayerFor = -1, handKids = -1;
        float leanCam, nextStepNoise;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Body = null; Armour = new ArmourLoadout(); ArmourPreset = 0; EyeT = null; }

        /// <summary>Your body's eyes (what the gun is held in front of): the camera, or while you fly the free camera
        /// your head, looking the way you were when you took off.</summary>
        Ladder ladderWant; float ladderHold;   // walking straight into a ladder, and for how long
        public static Transform EyeT;
        bool wasFlying;

        public static PlayerAvatar Attach(PlayerController pc)
        {
            var a = pc.gameObject.AddComponent<PlayerAvatar>();
            a.pc = pc;
            return a;
        }

        /// <summary>A new body at a spot (on start, on respawn, on a map change).</summary>
        public void Spawn(Vector3 feet, float yaw)
        {
            if (Body != null) { HumanBody.All.Remove(Body); Destroy(Body.gameObject); Body = null; }
            var h = HumanBody.Spawn(SandboxGame.ClearSpot(feet), yaw);
            if (h == null) return;
            h.name = "You";
            h.brain.aiEnabled = false;
            h.brain.playerControlled = true;
            h.ragdoll.grit = 1.8f;           // the main character: harder to put down, quicker back up
            // a plain, even walk (Earls each get a quirky one)
            h.ragdoll.SetPlainStyle();
            Body = h;
            ArmourSystem.Equip(h, Armour.Clone());
            HideOwnHead();
            camInit = false;
            armsLayerFor = -1;
            var cc = pc.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            pc.cam.cullingMask &= ~(1 << OwnLayer);
        }

        /// <summary>Put on armour (from the gun menu).</summary>
        public static void Wear(ArmourLoadout L)
        {
            Armour = L.Clone();
            if (Body == null) return;
            ArmourSystem.Remove(Body);
            ArmourSystem.Equip(Body, Armour.Clone());
            FindFirstObjectByType<PlayerAvatar>()?.HideOwnHead();
        }

        static void SetLayer(BodyPart bp, int layer)
        {
            if (bp == null) return;
            foreach (var t in bp.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        void HideOwnHead()
        {
            if (Body == null) return;
            SetLayer(Body.Head, OwnLayer);
            SetLayer(Body[PartId.Neck], OwnLayer);
            armsLayerFor = -1;
        }

        /// <summary>Your arms are the viewmodel's while you hold something; empty-handed you see your own.</summary>
        /// <summary>
        /// Holding something, what you see of your arms are the sculpted arms on it (hand wrapped on the grip, forearm
        /// running back to you); your body's own arms still hold and move it, everyone else sees those. Empty-handed
        /// you see your body's forearms and hands. The upper arms never: they sit right by the eyes.
        /// </summary>
        void ArmsVisible(bool drawnR, bool drawnL)
        {
            int want = (drawnR ? 1 : 0) | (drawnL ? 2 : 0);
            // (also redone whenever something else has put a hand back on its own layer - x-ray, gore, getting
            // up - or your own hand shows as a stub between the forearm and the hand on the gun)
            bool handsRight = true;
            foreach (var (id, drawn) in new[] { (PartId.HandL, drawnL), (PartId.HandR, drawnR) })
            {
                var h = Body[id];
                if (h != null && !h.severed && (h.gameObject.layer == OwnLayer) != drawn) handsRight = false;
            }
            // (and anything grown on them since - a wound, blood - goes the same way)
            int kids = 0;
            foreach (var id in new[] { PartId.HandL, PartId.HandR, PartId.ForearmL, PartId.ForearmR }) if (Body[id] != null) kids += Body[id].transform.childCount;
            if (kids != handKids) { handKids = kids; handsRight = false; }
            if (armsLayerFor == want && handsRight) return;
            armsLayerFor = want;
            int normal = Body.Pelvis.gameObject.layer;
            foreach (var id in new[] { PartId.UpperArmL, PartId.UpperArmR })
                if (Body[id] != null && !Body[id].severed) SetLayer(Body[id], OwnLayer);
            // an arm drawn on to the gun (WeaponController.DrawArms, riding on these) takes the place of that
            // physical forearm and hand in your own view - only that side: the other arm, busy with the floor or
            // hanging free, is your own as it is. Never both for the same arm, so never a third one.
            foreach (var (fa, hd, drawn) in new[] { (PartId.ForearmL, PartId.HandL, drawnL), (PartId.ForearmR, PartId.HandR, drawnR) })
            {
                if (Body[fa] != null && !Body[fa].severed) SetLayer(Body[fa], drawn ? OwnLayer : normal);
                if (Body[hd] != null && !Body[hd].severed) SetLayer(Body[hd], drawn ? OwnLayer : normal);
            }
        }

        /// <summary>Anything cut off you is no longer yours to hide: you should see your arm on the floor.</summary>
        void ShowSevered()
        {
            int normal = Body.Pelvis.gameObject.layer;
            foreach (var bp in Body.partList)
                if (bp != null && bp.severed && bp.gameObject.layer == OwnLayer && bp != Body.Head) SetLayer(bp, normal);
        }

        /// <summary>Called by PlayerController every frame with what the keys say. Returns false if there is no body.</summary>
        public bool Drive(Vector3 wishWorld, bool sprint, Keyboard kb, float dt)
        {
            var h = Body;
            if (h == null) return false;
            var rag = h.ragdoll;
            var p = h.physio;
            pc.down = rag.IsDown;
            if (p.dead) { rag.moveSpeed = 0f; return true; }
            // (flying the free camera, your body stands still and keeps looking where it was; the keys fly the camera)
            bool keys = !pc.flying && FpvDrone.Piloting == null;
            Transform eyes = pc.flying && EyeT != null ? EyeT : pc.cam.transform;
            Vector3 look = eyes.forward;
            // (ALT + A / D: your head glances after the gun, but your body stays square behind the cover)
            if (WeaponController.BlindLook != Vector2.zero && !pc.flying) look = eyes.rotation * Quaternion.Inverse(Quaternion.Euler(-WeaponController.BlindLook.y, WeaponController.BlindLook.x, 0f)) * Vector3.forward;
            Vector3 flat = Vector3.ProjectOnPlane(look, Vector3.up).normalized;
            rag.faceDir = flat;
            rag.lookAt = eyes.position + look * 10f;
            bool crouch = keys && kb.leftCtrlKey.isPressed;
            rag.pose = crouch ? PoseKind.Crouch : PoseKind.Stand;
            rag.crouch = crouch ? 1f : 0f;
            rag.holdPart = null;
            // what your hands are doing
            var w = SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;
            // (carried in one hand off the floor or going over: the arms are busy with that, not aiming)
            if (w != null && w.Armed && w.Current.type != WeaponType.Grenade && w.Current.type != WeaponType.Medical && !w.Carrying)
            {
                rag.aimDir = look;
                rag.aimHold = w.Current.type == WeaponType.Knife ? 3 : (WeaponController.IsPistol(w.Current) ? 1 : 2);
                rag.swingPhase = 0f;
            }
            else rag.aimDir = null;
            ArmsVisible(w != null && w.Armed && w.ArmDrawnR, w != null && w.Armed && w.ArmDrawnL);
            // Q / E: lean out round a corner (the camera rides on the head as the spine bends)
            float leanWant = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            if (Cursor.lockState != CursorLockMode.Locked || Menus.Blocking || !keys) leanWant = 0f;
            rag.lean = Mathf.MoveTowards(rag.lean, leanWant, dt * 4f);
            leanCam = rag.lean;
            ShowSevered();

            // ---- ladders: walk into one at its foot to climb out, walk back over the lip at its top to climb down. Only
            // on purpose: right up against it, walking and looking straight at it, for a moment - walking past one
            // (or brushing it sideways) never grabs you
            if (LadderClimber.Busy(h)) { Follow(h, keys); return true; }
            Ladder want = null; bool wantTop = false;
            if (keys && !rag.IsDown && rag.mode == BodyMode.Standing && wishWorld.sqrMagnitude > 0.25f)
            {
                var lad = Ladder.Near(pc.transform.position, 0.55f, out bool atTop);
                Vector3 into = lad == null ? Vector3.zero : atTop ? -lad.outward : lad.outward;
                Vector3 viewFlat = Vector3.ProjectOnPlane(pc.transform.forward, Vector3.up).normalized;
                if (lad != null && Vector3.Dot(wishWorld.normalized, into) > 0.85f && Vector3.Dot(viewFlat, into) > 0.6f) { want = lad; wantTop = atTop; }
            }
            ladderHold = want != null && want == ladderWant ? ladderHold + dt : 0f;
            ladderWant = want;
            if (want != null)
            {
                var lad = want; bool atTop = wantTop;
                if (ladderHold > 0.3f && LadderClimber.Begin(h, lad, !atTop) != null)
                {
                    rag.lean = 0f; leanCam = 0f;
                    Follow(h, keys);
                    return true;
                }
            }
            if (keys) PlayerFooting.Update(h, wishWorld, dt);

            // ---- moving: the same gait as any Earl, so a hurt leg limps and a lost one drops you
            float speed = wishWorld.sqrMagnitude > 0.01f ? (sprint ? 4.8f : 2.3f) : 0f;
            // backing off and side-stepping are slower than walking forward (the legs can only step back so fast)
            if (speed > 0f) { float fw = Vector3.Dot(wishWorld.normalized, flat); speed *= fw < -0.2f ? 0.62f : fw < 0.5f ? 0.8f : 1f; if (fw < 0.5f) speed = Mathf.Min(speed, 2.4f); }
            rag.moveDir = wishWorld.sqrMagnitude > 0.01f ? wishWorld.normalized : flat;
            // firing a hard-kicking gun you slow right down to hold it
            if (w != null) speed *= 1f - 0.55f * w.FireSlow;
            rag.moveSpeed = speed * Mathf.Clamp01(wishWorld.magnitude);
            // the first steps after getting back up are steadier ones, building up to full speed
            if (rag.standGrace > 0f) rag.moveSpeed *= Mathf.Lerp(1f, 0.45f, rag.standGrace / 2.5f);
            // footsteps: running is heard a good way off, creeping crouched hardly at all
            if (rag.moveSpeed > 0.3f && Time.time > nextStepNoise)
            {
                nextStepNoise = Time.time + 0.45f;
                Intel.Noise(h.Pelvis.transform.position, Teams.Friendly, crouch ? 2.5f : sprint ? 16f : 7f);
            }
            // Z: go down / try to get up. On the ground: hold space to get up, keys to crawl
            if (keys && kb.yKey.wasPressedThisFrame && !rag.IsDown) rag.Fall("player");
            if (rag.IsDown)
            {
                rag.wantStand = !keys || kb.spaceKey.isPressed || kb.yKey.wasPressedThisFrame || rag.helpUp > 0.3f;   // (or a mate is helping you up)
                rag.wantCrawl = wishWorld.sqrMagnitude > 0.01f;
                rag.crawlDir = rag.wantCrawl ? wishWorld.normalized : Vector3.zero;
            }
            else
            {
                rag.wantStand = true; rag.wantCrawl = false;
                // jump: the whole body pushes off the floor
                if (keys && kb.spaceKey.wasPressedThisFrame && rag.mode == BodyMode.Standing && p.CanStand && Physics.Raycast(h.Pelvis.transform.position, Vector3.down, 1.2f, ~(1 << OwnLayer), QueryTriggerInteraction.Ignore))
                    foreach (var bp in h.partList) if (bp.rb != null && !bp.severed) bp.rb.AddForce(Vector3.up * 3.6f, ForceMode.VelocityChange);
            }
            Follow(h, keys);
            return true;
        }

        /// <summary>The player object follows the body (other code asks where "the player" is).</summary>
        void Follow(HumanBody h, bool keys)
        {
            Vector3 pel = h.Pelvis.transform.position;
            float gy = Physics.Raycast(pel, Vector3.down, out var g, 2.5f, ~(1 << OwnLayer), QueryTriggerInteraction.Ignore) && g.collider.GetComponentInParent<HumanBody>() == null ? g.point.y : pel.y - 0.95f;
            if (keys) pc.transform.position = new Vector3(pel.x, gy, pel.z);   // (not while the camera is off flying)
        }

        /// <summary>The camera is in your head: it goes where the head goes (stumbling, falling, lying on the floor).</summary>
        static readonly Unity.Profiling.ProfilerMarker PM_LateUpdate = new Unity.Profiling.ProfilerMarker("Carl.PlayerAvatar.LateUpdate");
        void LateUpdate()
        {
            using var pmScope = PM_LateUpdate.Auto();
            if (Body == null || pc == null || Body.Head == null) return;
            var head = Body.Head.transform;
            if (EyeT == null) EyeT = new GameObject("YourEyes").transform;
            // from the free camera you see all of yourself (your head too); back in it, not your own head and arms
            int mask = pc.flying ? pc.cam.cullingMask | (1 << OwnLayer) : pc.cam.cullingMask & ~(1 << OwnLayer);
            if (mask != pc.cam.cullingMask) pc.cam.cullingMask = mask;
            if (pc.flying)
            {
                // the camera is off flying: your eyes stay in your head, still looking where they were
                if (!wasFlying) EyeT.rotation = pc.cam.transform.rotation;
                wasFlying = true;
                Vector3 ff = Vector3.ProjectOnPlane(EyeT.forward, Vector3.up).normalized;
                EyeT.position = head.position + Vector3.up * 0.05f + ff * 0.09f;
                return;
            }
            wasFlying = false;
            Vector3 flatFwd = Vector3.ProjectOnPlane(pc.cam.transform.forward, Vector3.up).normalized;
            Vector3 eye = head.position + Vector3.up * 0.05f + flatFwd * 0.09f;
            // the spine bends the head out; a little extra so the eye clears the corner the way it does for real
            eye += Vector3.Cross(Vector3.up, flatFwd) * (leanCam * 0.12f);
            // aiming a long gun: the head goes down onto the stock
            var wpn = SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;
            if (wpn != null) eye -= Vector3.up * wpn.CheekDrop;
            pc.cam.transform.rotation = pc.cam.transform.rotation * Quaternion.Euler(0f, 0f, -leanCam * 12f);
            if (!camInit) { camPos = eye; camInit = true; }
            camPos = Vector3.Lerp(camPos, eye, Mathf.Clamp01(Time.unscaledDeltaTime * 30f));
            pc.cam.transform.position = camPos;
            EyeT.SetPositionAndRotation(camPos, pc.cam.transform.rotation);
        }

        void OnDestroy() { if (Body != null) { HumanBody.All.Remove(Body); Destroy(Body.gameObject); Body = null; } }
    }
}
