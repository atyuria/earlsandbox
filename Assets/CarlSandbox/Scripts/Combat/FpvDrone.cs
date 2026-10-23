using System.Collections.Generic;
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
    ///
    /// The camera is bolted to the front of the frame, tilted up a little the way racing quads are, so you see the
    /// front arms and the blur of the props at the edges of the picture and the view leans with the frame. The
    /// Earls can hear it, see it and shoot it down (EarlGunner): keep moving. A dropper high up is hard for them to
    /// make out - until a grenade comes down and doesn't finish the job, after which they know it's up there.
    /// </summary>
    public class FpvDrone : MonoBehaviour
    {
        void Awake() { useGUILayout = false; }   // (its OnGUI draws directly: no layout pass needed)
        public enum Kind { Kamikaze, Bomber }
        public static FpvDrone Piloting;
        public static Kind Selected = Kind.Kamikaze;
        /// <summary>Every drone in the air (the Earls look for these).</summary>
        public static readonly List<FpvDrone> Active = new List<FpvDrone>();

        Kind kind;
        /// <summary>Playtests: fly at this velocity instead of the keys.</summary>
        public Vector3? autopilot;
        public bool Ended => ended;
        public bool IsKamikaze => kind == Kind.Kamikaze;
        public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
        /// <summary>Until when the Earls know it's up there (a drop that didn't kill, or someone already shooting at it).</summary>
        public float RevealedUntil = -1f;
        /// <summary>Until when someone is shooting at it (the tracers and the noise: those near look for it too).</summary>
        public float ShotAtUntil = -1f;
        Rigidbody rb;
        Camera cam;
        Transform visual;
        Transform[] props = new Transform[4];
        AudioSource buzz;
        float yaw, pitch, battery, armed, roll, lean, wobble, hitFlash;
        int bombs, hp;
        bool ended;
        Vector3 home;
        GUIStyle hud, hudR, big;
        static Texture2D grain, vignette;
        // grenades it has dropped, still falling (to see whether they did the job)
        readonly List<(Grenade g, Vector3 last)> falling = new List<(Grenade, Vector3)>();

        // the camera: a little back from the front props, up on the top plate, tilted up
        static readonly Vector3 CamMount = new Vector3(0f, 0.04f, 0.03f);
        const float CamUptilt = 14f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Piloting = null; Selected = Kind.Kamikaze; Active.Clear(); }

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
            hp = kind == Kind.Kamikaze ? 1 : 2;
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = kind == Kind.Kamikaze ? 1.6f : 2.2f;
            rb.useGravity = false;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(0.42f, 0.14f, 0.42f);
            // your own body doesn't knock it out of the air as it takes off
            if (PlayerAvatar.Body != null) foreach (var bp in PlayerAvatar.Body.partList) if (bp != null && bp.col != null) Physics.IgnoreCollision(col, bp.col);

            // the airframe: carbon plate, four arms, motors and props, the camera up front, the payload under it
            visual = new GameObject("Airframe").transform;
            visual.SetParent(transform, false);
            var carbon = Mats.Lit(new Color(0.07f, 0.07f, 0.08f), 0.45f);
            var motor = Mats.Lit(new Color(0.25f, 0.26f, 0.28f), 0.6f, 0.8f);
            var blade = Mats.Lit(new Color(0.12f, 0.12f, 0.13f), 0.3f);
            var blur = Mats.Unlit(new Color(0.1f, 0.1f, 0.1f, 0.28f));
            Part(PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.012f, 0.2f), carbon);                    // bottom plate
            Part(PrimitiveType.Cube, new Vector3(0f, 0.032f, -0.01f), new Vector3(0.08f, 0.008f, 0.12f), carbon);          // top plate
            Part(PrimitiveType.Cube, new Vector3(0f, 0.058f, -0.055f), new Vector3(0.05f, 0.035f, 0.075f), Mats.Lit(new Color(0.15f, 0.3f, 0.6f), 0.3f));   // the battery, strapped on behind the camera
            Part(PrimitiveType.Cube, new Vector3(0f, 0.022f, 0.035f), new Vector3(0.028f, 0.026f, 0.022f), motor);          // the camera in its cage
            Part(PrimitiveType.Cylinder, new Vector3(0.03f, 0.07f, -0.1f), new Vector3(0.006f, 0.04f, 0.006f), motor).localRotation = Quaternion.Euler(-35f, 0f, 0f);   // the video antenna
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 tip = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.17f;
                var arm = Part(PrimitiveType.Cube, tip * 0.5f, new Vector3(0.022f, 0.01f, 0.17f), carbon);
                arm.localRotation = Quaternion.LookRotation(tip);
                Part(PrimitiveType.Cylinder, tip + Vector3.up * 0.014f, new Vector3(0.028f, 0.012f, 0.028f), motor);
                props[i] = Part(PrimitiveType.Cube, tip + Vector3.up * 0.029f, new Vector3(0.13f, 0.003f, 0.014f), blade);
                // the props spinning: a dark blurred disc
                Part(PrimitiveType.Cylinder, tip + Vector3.up * 0.03f, new Vector3(0.13f, 0.0008f, 0.13f), blur);
            }
            if (kind == Kind.Kamikaze)
            {
                // an RPG-7 warhead taped on under it, nose forward
                var olive = Mats.Lit(new Color(0.24f, 0.27f, 0.17f), 0.35f);
                var w = Part(PrimitiveType.Cylinder, new Vector3(0f, -0.045f, 0.04f), new Vector3(0.05f, 0.11f, 0.05f), olive);
                w.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Part(PrimitiveType.Sphere, new Vector3(0f, -0.045f, 0.17f), new Vector3(0.05f, 0.05f, 0.09f), olive);
                Part(PrimitiveType.Cylinder, new Vector3(0f, -0.045f, 0.24f), new Vector3(0.012f, 0.03f, 0.012f), motor).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Part(PrimitiveType.Cube, new Vector3(0f, -0.018f, 0.04f), new Vector3(0.06f, 0.01f, 0.03f), Mats.Lit(new Color(0.7f, 0.7f, 0.65f), 0.2f));   // the tape
            }
            else
            {
                // the release: three small grenades hanging in a row
                for (int i = 0; i < 3; i++) Part(PrimitiveType.Sphere, new Vector3((i - 1) * 0.05f, -0.045f, 0f), new Vector3(0.038f, 0.05f, 0.038f), Mats.Lit(new Color(0.25f, 0.3f, 0.2f), 0.3f)).name = "Bomb" + i;
                Part(PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.16f, 0.012f, 0.03f), motor);
            }

            // its camera (the picture you fly by), bolted to the frame; and its buzz
            var cg = new GameObject("FPV Camera");
            cg.transform.SetParent(visual, false);
            // the kamikaze's looks ahead; the dropper's hangs under it looking straight down at where it'll drop
            cg.transform.localPosition = kind == Kind.Bomber ? new Vector3(0f, -0.07f, 0f) : CamMount;
            cg.transform.localRotation = kind == Kind.Bomber ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(-CamUptilt, 0f, 0f);
            // its own frame, props and payload are never in its picture
            frameParts = visual.GetComponentsInChildren<Renderer>(true);
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += HideFrame;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += ShowFrame;
            cam = cg.AddComponent<Camera>();
            cam.fieldOfView = 92f;                                         // a wide FPV lens (vertical; ~120 across)
            cam.nearClipPlane = 0.01f;                                     // (close enough to see its own arms and props)
            cam.farClipPlane = 1500f;
            cam.depth = 5f;
            cg.tag = "MainCamera";
            cg.AddComponent<AudioListener>();
            Haze.PrepareCamera(cam);
            buzz = gameObject.AddComponent<AudioSource>();
            buzz.clip = BuzzClip();
            buzz.loop = true; buzz.spatialBlend = 1f; buzz.minDistance = 3f; buzz.maxDistance = 120f; buzz.volume = 0.5f * GameSettings.Volume;
            buzz.Play();

            Piloting = this;
            Active.Add(this);
            var main = SandboxGame.Instance.player.cam;
            main.enabled = false;
            var ml = main.GetComponent<AudioListener>(); if (ml) ml.enabled = false;
            SandboxGame.Instance.Toast(NameOf(kind) + " UP  ·  E TO END");
        }

        Renderer[] frameParts;

        void HideFrame(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera c)
        {
            if (c != cam || frameParts == null) return;
            foreach (var r in frameParts) if (r != null) r.enabled = false;
        }

        void ShowFrame(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera c)
        {
            if (c != cam || frameParts == null) return;
            foreach (var r in frameParts) if (r != null) r.enabled = true;
        }

        void Unhook()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= HideFrame;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= ShowFrame;
            if (frameParts != null) foreach (var r in frameParts) if (r != null) r.enabled = true;
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
            armed += Time.deltaTime;
            hitFlash = Mathf.Max(0f, hitFlash - Time.unscaledDeltaTime);
            Menus.LastMenuFrame = Time.frameCount;     // your hands are on the sticks, not the gun
            var me = PlayerAvatar.Body;
            if (me != null && (me.Dead || me.physio.consciousness < 0.4f)) { End("SIGNAL LOST"); return; }
            if (kb != null && kb.eKey.wasPressedThisFrame) { End("DRONE DITCHED"); return; }
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
            WatchDrops();
        }

        /// <summary>
        /// A dropped grenade has gone off: anyone it was meant for who is still standing looks up and knows where
        /// it came from.
        /// </summary>
        void WatchDrops()
        {
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var f = falling[i];
                if (f.g != null) { falling[i] = (f.g, f.g.transform.position); continue; }
                falling.RemoveAt(i);
                int alive = 0;
                foreach (var e in EarlGunner.All)
                {
                    if (e == null || e.team == Teams.Friendly || !e.Fighting || e.Body == null) continue;
                    if (Vector3.Distance(e.Body.Center, f.last) > 10f) continue;
                    alive++;
                    e.Learn(transform.position, 0.9f, false);
                }
                if (alive > 0)
                {
                    bool was = Time.time < RevealedUntil;
                    RevealedUntil = Time.time + 45f;
                    if (!was) SandboxGame.Instance?.Toast("MISSED  ·  THEY KNOW THE DRONE IS UP THERE");
                }
            }
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
            // (the dropper looks straight down: forward is flat ahead of it, the way the top of the picture faces)
            if (kind == Kind.Bomber) look = Quaternion.Euler(0f, yaw, 0f);
            Vector3 wish = (look * Vector3.forward) * input.z * top + flatRight * input.x * top * 0.6f + Vector3.up * vert * (kind == Kind.Kamikaze ? 8f : 6f);
            // out of range: the link drops in and out, it drifts
            float range = Vector3.Distance(transform.position, home);
            if (range > 260f && Random.value < Mathf.InverseLerp(260f, 400f, range)) wish = rb.linearVelocity * 0.98f;
            if (autopilot.HasValue) wish = autopilot.Value;
            float accel = kind == Kind.Kamikaze ? 30f : 18f;
            rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, wish, accel * Time.fixedDeltaTime);
            // shot up: a motor or a prop gone, it wallows and shudders
            if (wobble > 0f) rb.AddForce((Random.insideUnitSphere * 9f + Vector3.down * 2.5f) * wobble, ForceMode.Acceleration);

            // the airframe points where you look, leans into the way it's going and banks into turns; the camera
            // is part of it, so the picture leans and rolls with it
            Vector3 v = rb.linearVelocity;
            Vector3 localV = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * v;
            float dt = Time.fixedDeltaTime;
            lean = Mathf.Lerp(lean, Mathf.Clamp(localV.z * 1.3f, -25f, 25f), dt * 6f);
            roll = Mathf.Lerp(roll, Mathf.Clamp(-localV.x * 2.2f, -35f, 35f), dt * 6f);
            float shake = (0.15f + v.magnitude * 0.012f) + wobble * 1.5f;
            visual.rotation = look * Quaternion.Euler(lean * 0.5f + Random.Range(-shake, shake), Random.Range(-shake, shake), roll + Random.Range(-shake, shake));
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

        /// <summary>A round has hit it: the warhead may go, or a motor; enough of them and it falls.</summary>
        public void Shot(Vector3 at, Vector3 dir)
        {
            if (ended) return;
            Fx.Sparks(at, -dir, 10);
            Sfx.Play("impact_metal", at, 0.7f, 1f, 2f, 0.1f);
            hitFlash = 0.35f;
            hp--;
            if (kind == Kind.Kamikaze && Random.value < 0.35f) { Detonate(); return; }   // the round found the warhead
            if (hp <= 0) { End("SHOT DOWN"); return; }
            wobble += 0.6f;
            battery -= 15f;
            SandboxGame.Instance?.Toast("DRONE HIT  ·  KEEP MOVING");
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
            falling.Add((g, g.transform.position));
            Sfx.Play("click_empty", transform.position, 0.5f, 1f, 1f, 0.05f);
            SandboxGame.Instance?.Toast(bombs > 0 ? $"DROPPED  ·  {bombs} LEFT" : "DROPPED  ·  EMPTY (E TO END)");
        }

        /// <summary>Back to your own eyes. The drone falls out of the sky (or is gone, if it blew up).</summary>
        public void End(string why, bool destroy = false)
        {
            if (ended) return;
            ended = true;
            Active.Remove(this);
            Unhook();
            if (Piloting == this) Piloting = null;
            var g = SandboxGame.Instance;
            if (g != null && g.player != null)
            {
                g.player.cam.enabled = true;
                var ml = g.player.cam.GetComponent<AudioListener>(); if (ml) ml.enabled = true;
            }
            // the goggles: a blast or a crash cuts the picture to static before you're back in your own eyes
            if (why == "DETONATED" || why == "CRASHED" || why == "SIGNAL LOST" || why == "SHOT DOWN") FpvStatic.Show(why == "DETONATED" ? 1.6f : 1.1f);
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

        void OnDestroy() { Active.Remove(this); Unhook(); if (Piloting == this) End("SIGNAL LOST"); }

        // ------------------------------------------------------------ the goggles

        static Texture2D Vignette()
        {
            if (vignette != null) return vignette;
            const int S = 128;
            vignette = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    float r = Mathf.Sqrt(u * u * 0.8f + v * v);
                    px[y * S + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01((r - 0.75f) / 0.6f) * 150f));
                }
            vignette.SetPixels32(px); vignette.Apply(false);
            return vignette;
        }

        void OnGUI()
        {
            if (ended) return;
            GUI.depth = -80;
            if (hud == null)
            {
                hud = new GUIStyle { fontSize = 30, normal = { textColor = new Color(0.97f, 0.98f, 0.97f, 0.95f) } };
                hudR = new GUIStyle(hud) { alignment = TextAnchor.UpperRight };
                big = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(0.95f, 0.98f, 0.95f, 0.8f) } };
            }
            if (Event.current.type != EventType.Repaint) return;
            float W = Screen.width, H = Screen.height;
            float range = Vector3.Distance(transform.position, home);
            float breakup = Mathf.InverseLerp(150f, 400f, range);
            // the analogue link: a light grain all the time, more with range and when it's been hit
            if (grain == null) grain = new Texture2D(240, 135, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            if (Time.frameCount % 2 == 0) FpvStatic.Noise(grain, 0.05f + breakup * 0.35f + wobble * 0.08f + hitFlash);
            GUI.DrawTexture(new Rect(0, 0, W, H), grain);
            GUI.DrawTexture(new Rect(0, 0, W, H), Vignette());
            // the odd rolling line and tear
            int lines = 2 + (int)(breakup * 90f) + (int)(wobble * 6f);
            for (int i = 0; i < lines; i++)
            {
                GUI.color = new Color(1f, 1f, 1f, Random.Range(0.015f, 0.05f + breakup * 0.35f));
                GUI.DrawTexture(new Rect(0, Random.Range(0f, H), W, Random.Range(1f, 3f)), Texture2D.whiteTexture);
            }
            float roll = (Time.unscaledTime * 0.07f % 1f) * H;
            GUI.color = new Color(1f, 1f, 1f, 0.025f);
            GUI.DrawTexture(new Rect(0, roll, W, 26f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // the OSD: mode top left, speed top right, the pack voltage bottom left
            float s = Mathf.Clamp(H / 1080f, 0.6f, 1.6f);
            hud.fontSize = hudR.fontSize = Mathf.RoundToInt(30 * s);
            float bat = Mathf.Clamp01(battery / (kind == Kind.Kamikaze ? 100f : 150f));
            float volts = Mathf.Lerp(3.3f, 4.2f, Mathf.Pow(bat, 0.7f));
            Shadowed(new Rect(56 * s, 40 * s, 400, 50 * s), "Acro", hud);
            Shadowed(new Rect(W - 456 * s - 56 * s, 40 * s, 456 * s, 50 * s), $"{rb.linearVelocity.magnitude * 3.6f:0} km/h", hudR);
            // the battery icon, emptying
            float bx = 70 * s, by = H - 150 * s, bw = 40 * s, bh = 84 * s;
            var bc = bat < 0.2f && Time.unscaledTime % 0.8f < 0.4f ? new Color(1f, 0.35f, 0.3f, 0.9f) : new Color(0.93f, 0.87f, 0.72f, 0.85f);
            GUI.color = bc;
            GUI.DrawTexture(new Rect(bx + bw * 0.3f, by - 7 * s, bw * 0.4f, 7 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx, by, bw, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx, by + bh - 3 * s, bw, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx, by, 3 * s, bh), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx + bw - 3 * s, by, 3 * s, bh), Texture2D.whiteTexture);
            float fill = (bh - 10 * s) * bat;
            GUI.DrawTexture(new Rect(bx + 5 * s, by + bh - 5 * s - fill, bw - 10 * s, fill), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Shadowed(new Rect(bx + bw + 24 * s, by + bh * 0.5f - 20 * s, 300, 50 * s), $"{volts:0.00} v", hud);
            float alt = Physics.Raycast(transform.position, Vector3.down, out var g, 400f, ~0, QueryTriggerInteraction.Ignore) ? g.distance : 400f;
            Shadowed(new Rect(W - 456 * s - 56 * s, H - 110 * s, 456 * s, 50 * s), kind == Kind.Bomber ? $"alt {alt:0} m   n {bombs}" : $"alt {alt:0} m", hudR);
            // a small centre mark, and the link quality when it starts to go
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.DrawTexture(new Rect(W / 2 - 9, H / 2 - 1, 18, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(W / 2 - 1, H / 2 - 9, 2, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // the dropper: where a grenade let go now would come down (it carries the drone's speed with it)
            if (kind == Kind.Bomber && bombs > 0 && alt < 400f)
            {
                Vector3 v0 = rb.linearVelocity, p0 = transform.position - Vector3.up * 0.12f;
                float groundY = transform.position.y - alt, t = 0f;
                for (int k = 0; k < 200; k++) { t += 0.03f; if (p0.y + v0.y * t - 4.905f * t * t <= groundY) break; }
                Vector3 land = p0 + v0 * t + 0.5f * Physics.gravity * t * t;
                Vector3 sp = cam.WorldToScreenPoint(land);
                if (sp.z > 0f)
                {
                    float x = sp.x, y = H - sp.y;
                    GUI.color = new Color(1f, 0.35f, 0.25f, 0.9f);
                    GUI.DrawTexture(new Rect(x - 14, y - 1, 9, 2), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(x + 5, y - 1, 9, 2), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(x - 1, y - 14, 2, 9), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(x - 1, y + 5, 2, 9), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
            if (range > 150f) Shadowed(new Rect(W / 2 - 200, 40 * s, 400, 50 * s), $"RSSI {Mathf.RoundToInt(Mathf.Lerp(99f, 5f, breakup))}", new GUIStyle(hud) { alignment = TextAnchor.UpperCenter, fontSize = Mathf.RoundToInt(22 * s) });
            if (Time.time < RevealedUntil && Time.unscaledTime % 1f < 0.6f) GUI.Label(new Rect(W / 2 - 300, H * 0.22f, 600, 24), "SPOTTED  ·  THEY'RE LOOKING FOR YOU", big);
            if (hitFlash > 0f) { GUI.color = new Color(1f, 1f, 1f, hitFlash * 0.6f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
            GUI.Label(new Rect(W / 2 - 400, H - 34, 800, 24), "MOUSE steer · W/S forward/back · A/D sideways · SPACE up · CTRL down · SHIFT boost · " + (kind == Kind.Kamikaze ? "LEFT CLICK DETONATE" : "LEFT CLICK DROP GRENADE") + " · E end", big);
        }

        static void Shadowed(Rect r, string text, GUIStyle st)
        {
            var c = st.normal.textColor;
            st.normal.textColor = new Color(0f, 0f, 0f, 0.55f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
            st.normal.textColor = c;
            GUI.Label(r, text, st);
        }
    }
}
