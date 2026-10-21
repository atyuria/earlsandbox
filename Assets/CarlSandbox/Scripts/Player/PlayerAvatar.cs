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
        int armsLayerFor = -1;
        float leanCam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Body = null; Armour = new ArmourLoadout(); ArmourPreset = 0; }

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
        /// Your forearms are always in view. Your own hands are swapped for the gripping hands on whatever you hold
        /// (they are locked on its grip and handguard, and your forearms are driven to meet them).
        /// </summary>
        void ArmsVisible(bool holding)
        {
            int want = holding ? 1 : 0;
            if (armsLayerFor == want) return;
            armsLayerFor = want;
            int normal = Body.Pelvis.gameObject.layer;
            // the upper arms never: they sit right by the eyes and fill the screen
            foreach (var id in new[] { PartId.UpperArmL, PartId.UpperArmR })
                if (Body[id] != null && !Body[id].severed) SetLayer(Body[id], OwnLayer);
            foreach (var id in new[] { PartId.ForearmL, PartId.ForearmR })
                if (Body[id] != null && !Body[id].severed) SetLayer(Body[id], normal);
            foreach (var id in new[] { PartId.HandL, PartId.HandR })
                if (Body[id] != null && !Body[id].severed) SetLayer(Body[id], holding ? OwnLayer : normal);
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
            Vector3 look = pc.cam.transform.forward;
            Vector3 flat = Vector3.ProjectOnPlane(look, Vector3.up).normalized;
            rag.faceDir = flat;
            rag.lookAt = pc.cam.transform.position + look * 10f;
            rag.pose = kb.leftCtrlKey.isPressed ? PoseKind.Crouch : PoseKind.Stand;
            rag.crouch = kb.leftCtrlKey.isPressed ? 1f : 0f;
            rag.holdPart = null;
            // what your hands are doing
            var w = SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;
            if (w != null && w.Armed && w.Current.type != WeaponType.Grenade)
            {
                rag.aimDir = look;
                rag.aimHold = w.Current.type == WeaponType.Knife ? 3 : (WeaponController.IsPistol(w.Current) ? 1 : 2);
                rag.swingPhase = 0f;
            }
            else rag.aimDir = null;
            ArmsVisible(w != null && w.Armed);
            // Q / E: lean out round a corner (the camera rides on the head as the spine bends)
            float leanWant = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            if (Cursor.lockState != CursorLockMode.Locked || Menus.Blocking) leanWant = 0f;
            rag.lean = Mathf.MoveTowards(rag.lean, leanWant, dt * 4f);
            leanCam = rag.lean;
            ShowSevered();

            // ---- moving: the same gait as any Earl, so a hurt leg limps and a lost one drops you
            float speed = wishWorld.sqrMagnitude > 0.01f ? (sprint ? 4.2f : 1.7f) : 0f;
            rag.moveDir = wishWorld.sqrMagnitude > 0.01f ? wishWorld.normalized : flat;
            rag.moveSpeed = speed * Mathf.Clamp01(wishWorld.magnitude);
            // Z: go down / try to get up. On the ground: hold space to get up, keys to crawl
            if (kb.zKey.wasPressedThisFrame && !rag.IsDown) rag.Fall("player");
            if (rag.IsDown)
            {
                rag.wantStand = kb.spaceKey.isPressed || (kb.zKey.wasPressedThisFrame);
                rag.wantCrawl = wishWorld.sqrMagnitude > 0.01f;
                rag.crawlDir = rag.wantCrawl ? wishWorld.normalized : Vector3.zero;
            }
            else
            {
                rag.wantStand = true; rag.wantCrawl = false;
                // jump: the whole body pushes off the floor
                if (kb.spaceKey.wasPressedThisFrame && rag.mode == BodyMode.Standing && p.CanStand && Physics.Raycast(h.Pelvis.transform.position, Vector3.down, 1.2f, ~(1 << OwnLayer), QueryTriggerInteraction.Ignore))
                    foreach (var bp in h.partList) if (bp.rb != null && !bp.severed) bp.rb.AddForce(Vector3.up * 3.6f, ForceMode.VelocityChange);
            }
            // the player object follows the body (other code asks where "the player" is)
            Vector3 pel = h.Pelvis.transform.position;
            float gy = Physics.Raycast(pel, Vector3.down, out var g, 2.5f, ~(1 << OwnLayer), QueryTriggerInteraction.Ignore) && g.collider.GetComponentInParent<HumanBody>() == null ? g.point.y : pel.y - 0.95f;
            pc.transform.position = new Vector3(pel.x, gy, pel.z);
            return true;
        }

        /// <summary>The camera is in your head: it goes where the head goes (stumbling, falling, lying on the floor).</summary>
        void LateUpdate()
        {
            if (Body == null || pc == null || pc.flying || Body.Head == null) return;
            var head = Body.Head.transform;
            Vector3 flatFwd = Vector3.ProjectOnPlane(pc.cam.transform.forward, Vector3.up).normalized;
            Vector3 eye = head.position + Vector3.up * 0.05f + flatFwd * 0.09f;
            // the spine bends the head out; a little extra so the eye clears the corner the way it does for real
            eye += Vector3.Cross(Vector3.up, flatFwd) * (leanCam * 0.12f);
            pc.cam.transform.rotation = pc.cam.transform.rotation * Quaternion.Euler(0f, 0f, -leanCam * 12f);
            if (!camInit) { camPos = eye; camInit = true; }
            camPos = Vector3.Lerp(camPos, eye, Mathf.Clamp01(Time.unscaledDeltaTime * 30f));
            pc.cam.transform.position = camPos;
        }

        void OnDestroy() { if (Body != null) { HumanBody.All.Remove(Body); Destroy(Body.gameObject); Body = null; } }
    }
}
