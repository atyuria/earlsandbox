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
            cc.enabled = !flying;
            yaw = yawDeg; pitch = 0f;
            velocity = Vector3.zero;
        }

        public void AddRecoil(float up, float side)
        {
            recoilPitch += up;
            recoilYaw += side;
        }

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

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            float dt = Time.unscaledDeltaTime;

            if (Cursor.lockState == CursorLockMode.Locked)
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

            shake = Mathf.Max(0f, shake - dt * 1.5f);
            Vector3 shakeOff = shake > 0f ? Random.insideUnitSphere * shake * 1.2f : Vector3.zero;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cam.transform.localRotation = Quaternion.Euler(pitch + shakeOff.x, shakeOff.y, shakeOff.z);

            Vector3 input = Vector3.zero;
            if (kb.wKey.isPressed) input.z += 1f;
            if (kb.sKey.isPressed) input.z -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            input = Vector3.ClampMagnitude(input, 1f);
            bool sprint = kb.leftShiftKey.isPressed;

            if (flying)
            {
                Vector3 move = cam.transform.TransformDirection(input);
                if (kb.spaceKey.isPressed) move += Vector3.up;
                if (kb.leftCtrlKey.isPressed) move += Vector3.down;
                float sp = sprint ? 22f : 8f;
                transform.position += move * sp * dt;
                return;
            }

            float speed = sprint ? 7f : 4.3f;
            Vector3 wish = transform.TransformDirection(input) * speed;
            float gdt = Time.deltaTime > 0f ? dt : 0f;
            if (cc.isGrounded)
            {
                velocity.x = wish.x; velocity.z = wish.z;
                velocity.y = -1f;
                if (kb.spaceKey.wasPressedThisFrame) velocity.y = 5.2f;
            }
            else
            {
                velocity.x = Mathf.Lerp(velocity.x, wish.x, gdt * 3f);
                velocity.z = Mathf.Lerp(velocity.z, wish.z, gdt * 3f);
                velocity.y += Physics.gravity.y * gdt;
            }
            float crouchH = kb.leftCtrlKey.isPressed ? 1.1f : 1.8f;
            cc.height = Mathf.Lerp(cc.height, crouchH, dt * 10f);
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
            cam.transform.localPosition = new Vector3(0, cc.height - 0.15f, 0);
            // the player moves in real time so slow-mo feels like bullet time
            cc.Move(velocity * dt);
            if (transform.position.y < -60f) Teleport(SandboxGame.Instance.SpawnPoint, yaw);
        }
    }
}
