using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    /// <summary>First-person walker with a noclip fly mode (V).</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Camera cam;
        public bool flying;
        public float sensitivity = 0.09f;
        public float fovNormal = 75f;
        [HideInInspector] public float sensitivityScale = 1f;

        CharacterController cc;
        float yaw, pitch;
        float recoilPitch, recoilYaw;
        Vector3 velocity;
        float shake;

        public static PlayerController Create(Vector3 pos, float yawDeg)
        {
            var go = new GameObject("Player");
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            var pc = go.AddComponent<PlayerController>();
            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.65f, 0);
            pc.cam = camGo.AddComponent<Camera>();
            pc.cam.nearClipPlane = 0.03f;
            pc.cam.farClipPlane = 1500f;
            pc.cam.fieldOfView = pc.fovNormal;
            camGo.AddComponent<AudioListener>();
            pc.yaw = yawDeg;
            return pc;
        }

        void Awake() { cc = GetComponent<CharacterController>(); }

        public void Teleport(Vector3 pos, float yawDeg)
        {
            cc.enabled = false;
            transform.position = pos;
            var av = GetComponent<PlayerAvatar>();
            if (av != null) av.Spawn(pos, yawDeg);           // a fresh body there
            else cc.enabled = !flying;
            down = false;
            yaw = yawDeg; pitch = 0f;
            velocity = Vector3.zero;
        }

        public void AddRecoil(float up, float side)
        {
            recoilPitch += up;
            recoilYaw += side;
            // most of the climb comes back down once you stop firing (you settle the gun back on target)
            settle += up * 0.7f;
            lastRecoil = Time.time;
        }
        float settle, lastRecoil;
        bool flyToggle;

        /// <summary>A shot rocks the view: a quick tilt to one side and a bump, springing back.</summary>
        public void Rock(float amount)
        {
            // (about half a degree of tilt for a rifle, nearer two for the .50)
            rockVel += new Vector2(Random.Range(-1f, 1f) * amount * 300f, amount * 200f);
        }
        Vector2 rock, rockVel;   // (x roll, y pitch bump, degrees)

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        public void LookAt(Vector3 world)
        {
            Vector3 d = world - cam.transform.position;
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            recoilPitch = recoilYaw = 0f;
        }

        public void ToggleFly()
        {
            flying = !flying;
            cc.enabled = !flying;
            velocity = Vector3.zero;
        }

        static readonly Unity.Profiling.ProfilerMarker PM_Update = new Unity.Profiling.ProfilerMarker("Carl.PlayerController.Update");

        void Update()
        {

            using var pmScope = PM_Update.Auto();
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            float dt = Time.unscaledDeltaTime;
            // flying a drone, or on the radio's map: your body stands where you left it, the mouse and keys are theirs
            if (FpvDrone.Piloting != null || Radio.Open)
            {
                var av0 = GetComponent<PlayerAvatar>();
                if (av0 != null && PlayerAvatar.Body != null) av0.Drive(Vector3.zero, false, kb, dt);
                return;
            }

            // (ALT + A / D: the mouse is working the gun out round the corner, not your head)
            if (Cursor.lockState == CursorLockMode.Locked && !WeaponController.BlindHold)
            {
                Vector2 md = mouse.delta.ReadValue() * sensitivity * sensitivityScale;
                yaw += md.x;
                pitch = Mathf.Clamp(pitch - md.y, -88f, 88f);
            }
            // recoil recovers toward zero
            float rec = Mathf.Min(1f, dt * 9f);
            float rp = recoilPitch * rec, ry = recoilYaw * rec;
            pitch = Mathf.Clamp(pitch - rp, -88f, 88f);
            yaw += ry;
            recoilPitch -= rp; recoilYaw -= ry;
            if (settle > 0.01f && Time.time - lastRecoil > 0.12f)
            {
                float back = Mathf.Min(settle, settle * Mathf.Clamp01(dt * 7f) + dt * 2f);
                pitch = Mathf.Clamp(pitch + back, -88f, 88f);
                settle -= back;
            }
            else if (settle > 25f) settle = 25f;

            shake = Mathf.Max(0f, shake - dt * 1.5f);
            // the rock from a shot: a stiff spring back to level
            rockVel += (-rock * 160f - rockVel * 16f) * dt;
            rock += rockVel * dt;
            Vector3 shakeOff = shake > 0f ? Random.insideUnitSphere * shake * 1.2f : Vector3.zero;
            var body = PlayerBody.I;
            // a damaged cerebellum or a lot of lost blood: the world swims and the aim drifts
            float wob = body != null ? body.Wobble : 0f;
            float t = Time.unscaledTime;
            if (wob > 0.01f && Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += (Mathf.PerlinNoise(t * 0.7f, 1.3f) - 0.5f) * wob * 40f * dt;
                pitch += (Mathf.PerlinNoise(2.1f, t * 0.6f) - 0.5f) * wob * 30f * dt;
            }
            float roll = (Mathf.PerlinNoise(t * 0.4f, 5f) - 0.5f) * wob * 25f + (down && PlayerAvatar.Body == null ? 14f : 0f);   // in a body the head lies how it lies
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cam.transform.localRotation = Quaternion.Euler(pitch + shakeOff.x - rock.y, shakeOff.y, shakeOff.z + roll + rock.x)
                * Quaternion.Euler(-WeaponController.BlindLook.y, WeaponController.BlindLook.x, 0f);   // (ALT + A / D: looking where the gun is pointing round the corner)
            var avatar = GetComponent<PlayerAvatar>();
            bool hasBody = avatar != null && PlayerAvatar.Body != null;
            // dead or out cold: no control at all
            if (body != null && (body.dead || body.passedOut)) { if (hasBody) { avatar.Drive(Vector3.zero, false, kb, dt); return; } down = true; Settle(dt); return; }
            // hold V: free camera (let go and you are back in your body)
            // V: free camera on / off (press again to be back in your body)
            if (kb.vKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked) flyToggle = !flyToggle;
            bool wantFly = flyToggle && (body == null || !body.Paralysed);
            if (!wantFly) flyToggle = false;
            if (wantFly != flying) { flying = wantFly; cc.enabled = !flying && !hasBody; velocity = Vector3.zero; }

            Vector3 input = Vector3.zero;
            if (kb.wKey.isPressed) input.z += 1f;
            if (kb.sKey.isPressed) input.z -= 1f;
            bool altHeld = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;   // (ALT + A / D is the gun round a corner, not a step)
            if (kb.dKey.isPressed && !altHeld) input.x += 1f;
            if (kb.aKey.isPressed && !altHeld) input.x -= 1f;
            input = Vector3.ClampMagnitude(input, 1f);
            bool sprint = kb.leftShiftKey.isPressed;

            if (flying)
            {
                // your body stays where you left it, standing still (not walking on blind, or dragged after the camera)
                if (hasBody) avatar.Drive(Vector3.zero, false, kb, dt);
                Vector3 move = cam.transform.TransformDirection(input);
                if (kb.spaceKey.isPressed) move += Vector3.up;
                if (kb.leftCtrlKey.isPressed) move += Vector3.down;
                float sp = sprint ? 22f : 8f;
                transform.position += move * sp * dt;
                return;
            }

            // your own Earl body walks, falls, crawls and gets up by itself (ActiveRagdoll) - the keys just steer it
            if (hasBody && avatar.Drive(transform.TransformDirection(input), sprint, kb, dt)) return;

            // ---- no body (fallback): Z to drop (and Z to try to get up), legs that can't carry you, a trip
            bool canStand = body == null || body.CanStand;
            if (kb.zKey.wasPressedThisFrame) { if (!down) GoDown(0.3f); else if (canStand) getUp = 1f; }
            if (!canStand && !down) GoDown(0.6f);
            if (down)
            {
                // hold space to get back up (if your legs and spine let you)
                if (canStand && kb.spaceKey.isPressed) getUp += dt; else if (getUp < 1f) getUp = 0f;
                if (getUp >= 0.8f) { down = false; getUp = 0f; }
                // crawl on your elbows - not at all without arms or below a broken neck
                float crawl = body == null ? 1f : body.Paralysed ? 0f : 0.35f + 0.65f * body.UsableArms / 2f;
                input *= 0.9f * crawl;
                sprint = false;
            }
            float speed = (sprint ? 7f : 4.3f) * (body != null ? body.MoveScale : 1f);
            if (body != null && body.MoveScale < 0.6f) sprint = false;
            Vector3 wish = transform.TransformDirection(input) * (down ? 1f : speed);
            float gdt = Time.deltaTime > 0f ? dt : 0f;
            if (cc.isGrounded)
            {
                velocity.x = wish.x; velocity.z = wish.z;
                velocity.y = -1f;
                if (kb.spaceKey.wasPressedThisFrame && !down) velocity.y = 5.2f;
                // a bad leg gives way now and then
                if (!down && body != null && Mathf.Min(body.legL, body.legR) < 0.5f && input.sqrMagnitude > 0.1f && Random.value < dt * (0.6f - Mathf.Min(body.legL, body.legR))) GoDown(0.4f);
            }
            else
            {
                velocity.x = Mathf.Lerp(velocity.x, wish.x, gdt * 3f);
                velocity.z = Mathf.Lerp(velocity.z, wish.z, gdt * 3f);
                velocity.y += Physics.gravity.y * gdt;
            }
            lastSprint = sprint && input.z > 0.5f;
            float crouchH = down ? 0.5f : kb.leftCtrlKey.isPressed ? 1.1f : 1.8f;
            cc.height = Mathf.Lerp(cc.height, crouchH, dt * (down ? 6f : 10f));
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
            cam.transform.localPosition = new Vector3(0, cc.height - 0.15f, 0);
            // the player moves in real time so slow-mo feels like bullet time
            cc.Move(velocity * dt);
            if (transform.position.y < -60f) Teleport(SandboxGame.Instance.SpawnPoint, yaw);
        }

        // ------------------------------------------------------------ on the ground
        /// <summary>Lying on the ground (Z, a trip, a leg that won't hold you). You can still look and shoot.</summary>
        public bool down;
        float getUp;
        bool lastSprint;

        public void GoDown(float jolt)
        {
            if (down) return;
            down = true; getUp = 0f;
            Shake(jolt);
            recoilPitch -= 12f * jolt;          // the view drops as you go over
            Sfx.Play("impact_flesh", transform.position, 0.5f, 0f);
        }

        /// <summary>Out cold / dead: slump to the floor and stay there.</summary>
        void Settle(float dt)
        {
            if (flying) { flying = false; cc.enabled = true; }
            cc.height = Mathf.Lerp(cc.height, 0.5f, dt * 4f);
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
            cam.transform.localPosition = new Vector3(0, cc.height - 0.15f, 0);
            velocity.x = velocity.z = 0f;
            velocity.y = cc.isGrounded ? -1f : velocity.y + Physics.gravity.y * dt;
            cc.Move(velocity * dt);
        }

        // running into something low (a kerb, a crate, a body) at a sprint trips you over
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (down || !lastSprint || hit.normal.y > 0.4f) return;
            float h = hit.point.y - transform.position.y;
            if (h < 0.08f || h > 0.5f) return;
            if (Physics.Raycast(transform.position + Vector3.up * 0.7f, transform.forward, 0.8f, ~0, QueryTriggerInteraction.Ignore)) return;   // a wall, not a trip
            if (Random.value < 0.35f) GoDown(0.7f);
        }
    }
}
