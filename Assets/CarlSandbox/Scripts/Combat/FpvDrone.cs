using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    /// <summary>
    /// FPV drones (G menu, MISC, or P to launch the last one): you fly it through its own camera while your body
    /// stands where you left it.
    ///   KAMIKAZE: an RPG warhead strapped under a racing quad. It goes off when it hits anything hard enough, or
    ///             when you click.
    ///   GRENADE DROPPER: a slower quad carrying three impact grenades; click drops one where you're looking down.
    /// Controls: MOUSE steers (it flies where you look), W / S forward and back, A / D sideways, SPACE up, CTRL
    /// down, SHIFT boost, LEFT CLICK detonate / drop, E back to your body. The battery runs down; fly too far and
    /// the picture breaks up.
    /// </summary>
    public class FpvDrone : MonoBehaviour
    {
        public enum Kind { Kamikaze, Bomber }
        public static FpvDrone Piloting;
        public static Kind Selected = Kind.Kamikaze;

        Kind kind;
        /// <summary>Playtests: fly at this velocity instead of the keys.</summary>
        public Vector3? autopilot;
        public bool Ended => ended;
        Rigidbody rb;
        Camera cam;
        Transform visual;
        Transform[] props = new Transform[4];
        AudioSource buzz;
        float yaw, pitch, battery, armed;
        int bombs;
        bool ended;
        Vector3 home;
        GUIStyle hud, big;
        RenderTexture feed;
        static Texture2D grain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Piloting = null; Selected = Kind.Kamikaze; }

        public static string NameOf(Kind k) => k == Kind.Kamikaze ? "FPV KAMIKAZE DRONE" : "FPV GRENADE DRONE";

        /// <summary>Off your hand, a metre in front of you, and you're flying it.</summary>
        public static FpvDrone Launch(Kind k)
        {
            var g = SandboxGame.Instance;
            if (g == null || Piloting != null) return null;
            var me = PlayerAvatar.Body;
            if (me != null && (me.Dead || me.physio.consciousness < 0.5f)) return null;
            Selected = k;
            var pc = g.player.cam.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(pc.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            var go = new GameObject(NameOf(k));
            go.transform.position = pc.position + fwd * 0.9f + Vector3.up * 0.25f;
            var d = go.AddComponent<FpvDrone>();
            d.kind = k;
            d.yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            d.Build();
            return d;
        }

        void Build()
        {
            home = transform.position;
            battery = kind == Kind.Kamikaze ? 100f : 150f;
            bombs = kind == Kind.Bomber ? 3 : 0;
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = kind == Kind.Kamikaze ? 1.6f : 2.2f;
            rb.useGravity = false;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(0.36f, 0.12f, 0.36f);
            // your own body doesn't knock it out of the air as it takes off
            if (PlayerAvatar.Body != null) foreach (var bp in PlayerAvatar.Body.partList) if (bp != null && bp.col != null) Physics.IgnoreCollision(col, bp.col);

            // the airframe: carbon plate, four arms, motors and props, the camera up front, the payload under it
            visual = new GameObject("Airframe").transform;
            visual.SetParent(transform, false);
            var carbon = Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.5f);
            var motor = Mats.Lit(new Color(0.25f, 0.26f, 0.28f), 0.6f, 0.8f);
            var prop = Mats.Lit(new Color(0.85f, 0.35f, 0.1f), 0.4f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.03f, 0.16f), carbon);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.02f), new Vector3(0.06f, 0.035f, 0.1f), Mats.Lit(new Color(0.15f, 0.3f, 0.6f), 0.3f));   // the battery
            Part(PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.085f), new Vector3(0.03f, 0.03f, 0.025f), motor);    // the camera
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 tip = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.17f;
                var arm = Part(PrimitiveType.Cube, tip * 0.5f, new Vector3(0.018f, 0.012f, 0.17f), carbon);
                arm.localRotation = Quaternion.LookRotation(tip);
                Part(PrimitiveType.Cylinder, tip + Vector3.up * 0.015f, new Vector3(0.028f, 0.012f, 0.028f), motor);
                props[i] = Part(PrimitiveType.Cube, tip + Vector3.up * 0.03f, new Vector3(0.13f, 0.003f, 0.012f), prop);
            }
            if (kind == Kind.Kamikaze)
            {
                // an RPG-7 warhead taped on under it, nose forward
                var olive = Mats.Lit(new Color(0.24f, 0.27f, 0.17f), 0.35f);
                var w = Part(PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0.04f), new Vector3(0.05f, 0.11f, 0.05f), olive);
                w.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var nose = Part(PrimitiveType.Sphere, new Vector3(0f, -0.05f, 0.17f), new Vector3(0.05f, 0.05f, 0.09f), olive);
                Part(PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0.24f), new Vector3(0.012f, 0.03f, 0.012f), motor).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Part(PrimitiveType.Cube, new Vector3(0f, -0.022f, 0.04f), new Vector3(0.06f, 0.01f, 0.03f), Mats.Lit(new Color(0.7f, 0.7f, 0.65f), 0.2f));   // the tape
            }
            else
            {
                // the release: three small grenades hanging in a row
                for (int i = 0; i < 3; i++) Part(PrimitiveType.Sphere, new Vector3((i - 1) * 0.05f, -0.045f, 0f), new Vector3(0.038f, 0.05f, 0.038f), Mats.Lit(new Color(0.25f, 0.3f, 0.2f), 0.3f)).name = "Bomb" + i;
                Part(PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.16f, 0.012f, 0.03f), motor);
            }

            // its camera (the picture you fly by), and its buzz
            var cg = new GameObject("FPV Camera");
            cg.transform.SetParent(transform, false);
            cg.transform.localPosition = new Vector3(0f, 0.045f, 0.3f);   // (out ahead of the warhead's nose)
            cam = cg.AddComponent<Camera>();
            cam.fieldOfView = 112f;                                        // a wide FPV lens
            // an analogue video link: the picture is small and soft (drawn blown up in OnGUI)
            feed = new RenderTexture(400, 225, 16) { filterMode = FilterMode.Bilinear };
            cam.targetTexture = feed;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 1500f;
            cg.tag = "MainCamera";
            cg.AddComponent<AudioListener>();
            Haze.PrepareCamera(cam);
            buzz = gameObject.AddComponent<AudioSource>();
            buzz.clip = BuzzClip();
            buzz.loop = true; buzz.spatialBlend = 1f; buzz.minDistance = 3f; buzz.maxDistance = 120f; buzz.volume = 0.5f * GameSettings.Volume;
            buzz.Play();

            Piloting = this;
            var main = SandboxGame.Instance.player.cam;
            main.enabled = false;
            var ml = main.GetComponent<AudioListener>(); if (ml) ml.enabled = false;
            SandboxGame.Instance.Toast(NameOf(kind) + " UP  ·  E TO END");
        }

        Transform Part(PrimitiveType t, Vector3 at, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            var c = g.GetComponent<Collider>(); c.enabled = false; Destroy(c);
            g.transform.SetParent(visual, false);
            g.transform.localPosition = at;
            g.transform.localScale = size;
            var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        static AudioClip buzzClip;

        /// <summary>Four little motors: a reedy whine with a beat in it.</summary>
        static AudioClip BuzzClip()
        {
            if (buzzClip != null) return buzzClip;
            int rate = 44100, n = rate;
            var d = new float[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = 0f;
                foreach (float f in new[] { 180f, 186f, 191f, 197f })
                {
                    float ph = (t * f) % 1f;
                    s += (ph * 2f - 1f) * 0.18f;                                 // saw: the motor whine
                    s += Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.05f;          // prop harmonics
                }
                s += ((float)rnd.NextDouble() * 2f - 1f) * 0.06f;               // air
                d[i] = s * 0.6f;
            }
            buzzClip = AudioClip.Create("FpvBuzz", n, 1, rate, false);
            buzzClip.SetData(d, 0);
            return buzzClip;
        }

        void Update()
        {
            if (ended) return;
            var kb = Keyboard.current; var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;
            armed += Time.deltaTime;
            Menus.LastMenuFrame = Time.frameCount;     // your hands are on the sticks, not the gun
            var me = PlayerAvatar.Body;
            if (me != null && (me.Dead || me.physio.consciousness < 0.4f)) { End("SIGNAL LOST"); return; }
            if (kb != null && kb.eKey.wasPressedThisFrame) { End(kind == Kind.Kamikaze ? "DRONE DITCHED" : "DRONE DITCHED"); return; }
            if (Cursor.lockState == CursorLockMode.Locked && mouse != null)
            {
                Vector2 md = mouse.delta.ReadValue() * 0.09f * GameSettings.MouseSensitivity;
                yaw += md.x; pitch = Mathf.Clamp(pitch - md.y, -85f, 70f);
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && armed > 0.3f)
            {
                if (kind == Kind.Kamikaze) { Detonate(); return; }
                Drop();
            }
            battery -= Time.deltaTime;
            if (battery <= 0f) { End("BATTERY FLAT"); return; }
            // props spin, the buzz rises with the effort
            float spd = rb.linearVelocity.magnitude;
            for (int i = 0; i < 4; i++) if (props[i]) props[i].Rotate(0f, (i % 2 == 0 ? 1f : -1f) * 3000f * Time.deltaTime, 0f, Space.Self);
            buzz.pitch = (0.85f + spd * 0.025f + (kb != null && kb.leftShiftKey.isPressed ? 0.15f : 0f)) * Time.timeScale;
            // the enemy can hear it coming
            if (Time.frameCount % 30 == 0) Intel.Noise(transform.position, Teams.Friendly, 30f);
        }

        void FixedUpdate()
        {
            if (ended || WorldFreeze.On) return;
            var kb = Keyboard.current;
            Vector3 input = Vector3.zero; float vert = 0f; bool boost = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed) input.z += 1f;
                if (kb.sKey.isPressed) input.z -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
                if (kb.spaceKey.isPressed) vert += 1f;
                if (kb.leftCtrlKey.isPressed) vert -= 1f;
                boost = kb.leftShiftKey.isPressed;
            }
            var look = Quaternion.Euler(pitch, yaw, 0f);
            if (autopilot.HasValue) { input = Vector3.zero; vert = 0f; }
            Vector3 flatRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float top = kind == Kind.Kamikaze ? (boost ? 30f : 16f) : (boost ? 16f : 9f);
            Vector3 wish = (look * Vector3.forward) * input.z * top + flatRight * input.x * top * 0.6f + Vector3.up * vert * (kind == Kind.Kamikaze ? 8f : 6f);
            // out of range: the link drops in and out, it drifts
            float range = Vector3.Distance(transform.position, home);
            if (range > 260f && Random.value < Mathf.InverseLerp(260f, 400f, range)) wish = rb.linearVelocity * 0.98f;
            if (autopilot.HasValue) wish = autopilot.Value;
            float accel = kind == Kind.Kamikaze ? 30f : 18f;
            rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, wish, accel * Time.fixedDeltaTime);
            // the airframe banks into where it's going; the camera looks where you look
            Vector3 v = rb.linearVelocity;
            Vector3 localV = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * v;
            visual.rotation = Quaternion.Euler(Mathf.Clamp(localV.z * 1.8f, -35f, 35f), yaw, Mathf.Clamp(-localV.x * 2.2f, -35f, 35f));
            // the camera is bolted to the frame: it rolls with the bank and shakes with the motors
            float bankRoll = Mathf.Clamp(-localV.x * 2.2f, -35f, 35f) * 0.6f;
            float shake = 0.25f + v.magnitude * 0.02f;
            cam.transform.rotation = look * Quaternion.Euler(Random.Range(-shake, shake), Random.Range(-shake, shake), bankRoll + Random.Range(-shake, shake));
        }

        void OnCollisionEnter(Collision c)
        {
            if (ended) return;
            float hit = c.relativeVelocity.magnitude;
            if (kind == Kind.Kamikaze)
            {
                // the fuse: a solid knock (anything at speed, a person at any speed)
                if (armed > 0.4f && (hit > 3f || c.collider.GetComponentInParent<HumanBody>() != null)) Detonate();
                return;
            }
            if (hit > 9f) { Fx.Sparks(c.GetContact(0).point, c.GetContact(0).normal, 10); End("CRASHED"); }
        }

        public void Detonate()
        {
            if (ended) return;
            Vector3 p = transform.position;
            var g = Grenade.Throw(p, Vector3.zero);
            g.team = Teams.Friendly;
            g.fuse = 0.0001f;
            g.shrapnel = 320;           // a shaped charge's case and the drone's own bits
            HumanBrain.BroadcastNoise(p, 120f, 1f);
            End("DETONATED", true);
        }

        public void Drop()
        {
            if (bombs <= 0) { SandboxGame.Instance?.Toast("NO GRENADES LEFT"); return; }
            bombs--;
            var hanging = visual.Find("Bomb" + bombs);
            if (hanging) hanging.gameObject.SetActive(false);
            var g = Grenade.Throw(transform.position - Vector3.up * 0.12f, rb.linearVelocity);
            g.team = Teams.Friendly;
            g.impact = true;            // goes off on whatever it lands on
            g.fuse = 12f;
            foreach (var mine in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(mine, g.GetComponent<Collider>());
            Sfx.Play("click_empty", transform.position, 0.5f, 1f, 1f, 0.05f);
            SandboxGame.Instance?.Toast(bombs > 0 ? $"DROPPED  ·  {bombs} LEFT" : "DROPPED  ·  EMPTY (E TO END)");
        }

        /// <summary>Back to your own eyes. The drone falls out of the sky (or is gone, if it blew up).</summary>
        public void End(string why, bool destroy = false)
        {
            if (ended) return;
            ended = true;
            Piloting = null;
            var g = SandboxGame.Instance;
            if (g != null && g.player != null)
            {
                g.player.cam.enabled = true;
                var ml = g.player.cam.GetComponent<AudioListener>(); if (ml) ml.enabled = true;
            }
            // the goggles: a blast or a crash cuts the picture to static before you're back in your own eyes
            if (why == "DETONATED" || why == "CRASHED" || why == "SIGNAL LOST") FpvStatic.Show(why == "DETONATED" ? 1.6f : 1.1f);
            if (cam != null) cam.targetTexture = null;
            if (feed != null) { feed.Release(); Destroy(feed); feed = null; }
            if (cam != null) { cam.enabled = false; var l = cam.GetComponent<AudioListener>(); if (l) Destroy(l); cam.tag = "Untagged"; }
            g?.Toast(why);
            if (destroy) { Destroy(gameObject); return; }
            // unpowered: it drops
            buzz.Stop();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.angularDamping = 0.5f;
            Destroy(gameObject, 30f);
        }

        void OnDestroy() { if (Piloting == this) End("SIGNAL LOST"); }

        // ------------------------------------------------------------ the goggles

        void OnGUI()
        {
            if (ended) return;
            GUI.depth = -80;
            if (hud == null)
            {
                hud = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.95f, 0.98f, 0.95f) } };
                big = new GUIStyle(hud) { fontSize = 13, alignment = TextAnchor.UpperCenter };
            }
            float W = Screen.width, H = Screen.height;
            // the picture itself, then the analogue grain over it
            if (feed != null) GUI.DrawTexture(new Rect(0, 0, W, H), feed, ScaleMode.ScaleAndCrop, false);
            if (grain == null) grain = new Texture2D(160, 90, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            if (Time.frameCount % 2 == 0) FpvStatic.Noise(grain, 0.12f);
            GUI.DrawTexture(new Rect(0, 0, W, H), grain);
            float range = Vector3.Distance(transform.position, home);
            // breaking up with range, a little all the time
            int lines = 6 + (int)(Mathf.InverseLerp(150f, 400f, range) * 120f);
            for (int i = 0; i < lines; i++)
            {
                GUI.color = new Color(1f, 1f, 1f, Random.Range(0.02f, 0.08f + Mathf.InverseLerp(150f, 400f, range) * 0.4f));
                GUI.DrawTexture(new Rect(0, Random.Range(0f, H), W, Random.Range(1f, 4f)), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            // the crosshair and a horizon line
            GUI.DrawTexture(new Rect(W / 2 - 12, H / 2 - 1, 24, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(W / 2 - 1, H / 2 - 12, 2, 24), Texture2D.whiteTexture);
            float hy = H / 2 + Mathf.Tan(pitch * Mathf.Deg2Rad) * (H / 2) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(W / 2 - 160, hy, 120, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(W / 2 + 40, hy, 120, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float alt = Physics.Raycast(transform.position, Vector3.down, out var g, 200f, ~0, QueryTriggerInteraction.Ignore) ? g.distance : 200f;
            float bat = battery / (kind == Kind.Kamikaze ? 100f : 150f);
            GUI.Label(new Rect(30, 30, 400, 24), $"{(kind == Kind.Kamikaze ? "KAMIKAZE" : "DROPPER")}   ALT {alt:0.0} M   SPD {rb.linearVelocity.magnitude * 3.6f:0} KM/H", hud);
            GUI.Label(new Rect(30, 54, 400, 24), $"BAT {bat * 100f:0}%   RANGE {range:0} M" + (kind == Kind.Bomber ? $"   GRENADES {bombs}" : ""), hud);
            if (bat < 0.2f && Time.unscaledTime % 1f < 0.5f) GUI.Label(new Rect(W / 2 - 100, 80, 200, 24), "LOW BATTERY", big);
            GUI.Label(new Rect(W / 2 - 400, H - 50, 800, 24), "MOUSE steer · W/S forward/back · A/D sideways · SPACE up · CTRL down · SHIFT boost · " + (kind == Kind.Kamikaze ? "LEFT CLICK DETONATE" : "LEFT CLICK DROP GRENADE") + " · E end", big);
        }
    }
}
