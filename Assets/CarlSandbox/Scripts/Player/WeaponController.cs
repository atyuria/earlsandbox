using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    public enum WeaponType { Gun, Knife, Grenade, Tool }

    public class WeaponDef
    {
        public string name;
        public WeaponType type;
        public bool auto;
        public float rpm;
        public float muzzleVelocity;   // m/s
        public float bulletMass;       // kg
        public float diameter;         // m
        public int magazine;
        public float reloadTime;
        public float recoil;
        public float spread;           // degrees
        public float zoomFov;
        public bool scope;
        public float loudness;         // hearing radius (m)
        public bool tracer;
        public int pellets = 1;        // shot per trigger pull (buckshot)
        public float Energy => 0.5f * bulletMass * muzzleVelocity * muzzleVelocity;
    }

    /// <summary>Weapons, viewmodels and the physics grab (F).</summary>
    [DefaultExecutionOrder(100)]   // after the camera has gone to your head (PlayerAvatar.LateUpdate)
    public class WeaponController : MonoBehaviour
    {
        public readonly List<WeaponDef> defs = new List<WeaponDef>
        {
            new WeaponDef { name = "AK-47", type = WeaponType.Gun, auto = true, rpm = 600, muzzleVelocity = 715, bulletMass = 0.0079f, diameter = 0.0078f, magazine = 30, reloadTime = 2.4f, recoil = 1.5f, spread = 0.35f, zoomFov = 55, loudness = 75, tracer = true },
            new WeaponDef { name = "GLOCK 17", type = WeaponType.Gun, auto = false, rpm = 500, muzzleVelocity = 375, bulletMass = 0.0075f, diameter = 0.009f, magazine = 17, reloadTime = 1.5f, recoil = 1.1f, spread = 0.5f, zoomFov = 62, loudness = 50, tracer = false },
            new WeaponDef { name = "AWP", type = WeaponType.Gun, auto = false, rpm = 42, muzzleVelocity = 900, bulletMass = 0.0162f, diameter = 0.0086f, magazine = 5, reloadTime = 3.2f, recoil = 4.5f, spread = 0.02f, zoomFov = 11, scope = true, loudness = 95, tracer = true },
            new WeaponDef { name = ".50 CAL", type = WeaponType.Gun, auto = false, rpm = 110, muzzleVelocity = 890, bulletMass = 0.0421f, diameter = 0.0127f, magazine = 10, reloadTime = 3.6f, recoil = 7f, spread = 0.04f, zoomFov = 9, scope = true, loudness = 120, tracer = true },
            // 12-gauge pump, 00 buckshot: nine 8.4 mm pellets per shell
            new WeaponDef { name = "MOSSBERG 590", type = WeaponType.Gun, auto = false, rpm = 70, muzzleVelocity = 400, bulletMass = 0.0035f, diameter = 0.0084f, magazine = 6, reloadTime = 3.2f, recoil = 6f, spread = 2.4f, zoomFov = 55, loudness = 100, tracer = false, pellets = 9 },
            new WeaponDef { name = "KNIFE", type = WeaponType.Knife },
            new WeaponDef { name = "GRENADE", type = WeaponType.Grenade },
            // melee: mass (kg) decides how hard a swing lands; the hammer crushes, the sword cuts
            new WeaponDef { name = "HAMMER", type = WeaponType.Knife, bulletMass = 1.1f },
            new WeaponDef { name = "SWORD", type = WeaponType.Knife, bulletMass = 1.2f },
            // 5.56 carbine, 14.5" barrel, selector on AUTO
            new WeaponDef { name = "AR-15", type = WeaponType.Gun, auto = true, rpm = 800, muzzleVelocity = 940, bulletMass = 0.004f, diameter = 0.0057f, magazine = 30, reloadTime = 2.2f, recoil = 1.1f, spread = 0.25f, zoomFov = 55, loudness = 75, tracer = false },
            new WeaponDef { name = "BERETTA M9", type = WeaponType.Gun, auto = false, rpm = 450, muzzleVelocity = 380, bulletMass = 0.008f, diameter = 0.009f, magazine = 15, reloadTime = 1.6f, recoil = 1.15f, spread = 0.45f, zoomFov = 62, loudness = 50, tracer = false },
            // two barbed probes on insulated wires, 7.6 m cartridge; 5 seconds of current per trigger pull
            new WeaponDef { name = "TASER", type = WeaponType.Gun, auto = false, rpm = 60, muzzleVelocity = 55, bulletMass = 0.0015f, diameter = 0.002f, magazine = 1, reloadTime = 2.2f, recoil = 0.4f, spread = 0f, zoomFov = 62, loudness = 8, tracer = false },
            // always carried: grabs, drags and throws things (and bodies) on a beam
            new WeaponDef { name = "PHYSICS GUN", type = WeaponType.Tool },
        };

        public int current;
        public bool reloading;
        public float aimT;
        public static bool DebugAim;   // tests: hold the sights up without the mouse
        readonly int[] ammo = new int[16];
        /// <summary>What you are carrying: you start with nothing and take weapons in the G menu.</summary>
        public bool[] owned;
        public bool Armed => owned != null && current >= 0 && current < owned.Length && owned[current];

        /// <summary>Take a weapon (and pull it out) or put it back.</summary>
        public void Give(int i, bool on)
        {
            if (owned == null || i < 0 || i >= owned.Length) return;
            if (!on && defs[i].type == WeaponType.Tool) return;      // the physics gun stays with you
            owned[i] = on;
            if (on) Select(i);
            else if (current == i)
            {
                int next = -1;
                for (int k = 0; k < owned.Length; k++) if (owned[k]) { next = k; break; }
                if (next >= 0) Select(next); else for (int k = 0; k < models.Count; k++) models[k].SetActive(false);
            }
        }
        float nextFire, reloadEnd;
        PlayerController pc;
        Camera cam;
        Transform vmRoot;
        readonly List<GameObject> models = new List<GameObject>();
        readonly List<Transform> muzzles = new List<Transform>();
        GameObject flash;
        readonly List<MeshRenderer> flashParts = new List<MeshRenderer>();
        public float AimAmount => aimT;
        public float pointT;     // point aim: canted, up by the eye

        /// <summary>A copy of weapon i as it is set up now (attachments on, no hands), for the gun menu's showcase.</summary>
        public GameObject PreviewModel(int i)
        {
            if (i < 0 || i >= models.Count || models[i] == null) return null;
            var g = Instantiate(models[i]);
            g.SetActive(true);
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("View_Hand") || t.name == "SupportHand" || t.name == "ForegripHand" || t.name == "Reticle") t.gameObject.SetActive(false);
            foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            return g;
        }
        readonly List<MeshRenderer> reticles = new List<MeshRenderer>();
        Light muzzleLight;
        float flashTimer, kick, meleeT;
        int meleeMode; // 1 stab, 2 slash
        bool meleeHitDone;
        Rigidbody grabbed;
        Vector3 grabLocal;
        float grabDist;
        LineRenderer grabLine;
        bool smudging; Vector3 smudgeLast; float smudgeCarry;

        public WeaponDef Current => defs[current];
        public bool HasGunOut => Armed && Current.type == WeaponType.Gun;
        public bool Scoped => IsScope && aimT > 0.92f;

        // ---- customisation (G menu)
        public WeaponLoadout[] loadouts;
        public WeaponLoadout Loadout => loadouts[current];

        bool IsGlock(int i) => defs[i].name == "GLOCK 17";
        /// <summary>Rounds in a full magazine, with whatever magazine is fitted.</summary>
        public int MagSize(int i) => IsGlock(i) && loadouts != null && loadouts[i].extMag ? 33 : defs[i].magazine;
        /// <summary>Holding the trigger keeps firing (rifles, or a Glock with a switch on the back).</summary>
        public bool IsAuto(int i) => defs[i].auto || (IsGlock(i) && loadouts != null && loadouts[i].autoSwitch);
        /// <summary>Rounds per minute: a switched Glock runs away at about 1100.</summary>
        public float Rpm(int i) => IsGlock(i) && loadouts != null && loadouts[i].autoSwitch ? 1100f : defs[i].rpm;
        public AmmoType CurrentAmmo { get { var a = AmmoTable.For(Current.name); return a == null ? null : a[Mathf.Clamp(Loadout.ammo, 0, a.Length - 1)]; } }
        public bool IsScope => Current.type == WeaponType.Gun && (Loadout.sight == SightKind.Scope4x || Loadout.sight == SightKind.Scope8x);
        public float ZoomFov
        {
            get
            {
                switch (Loadout.sight)
                {
                    case SightKind.RedDot: return Loadout.magnifier && MagnifierFor(Current) ? 16f : 48f;
                    case SightKind.Holo: return Loadout.magnifier && MagnifierFor(Current) ? 15f : 46f;
                    case SightKind.Scope4x: return 16f;
                    case SightKind.Scope8x: return 8.5f;
                    default: return Current.zoomFov;
                }
            }
        }
        public static SightKind[] SightsFor(WeaponDef d)
        {
            if (d.type != WeaponType.Gun) return new SightKind[0];
            if (d.scope) return new[] { SightKind.Scope8x, SightKind.Scope4x };
            if (d.name == "GLOCK 17") return new[] { SightKind.Iron, SightKind.RedDot };     // MOS cut: an RMR on the slide
            if (d.name == "BERETTA M9" || d.name == "TASER") return new[] { SightKind.Iron };
            if (d.name == "MOSSBERG 590") return new[] { SightKind.Iron };                     // ghost rings: the receiver has no optic rail
            return new[] { SightKind.Iron, SightKind.RedDot, SightKind.Holo, SightKind.Scope4x };
        }
        public static bool GripsFor(WeaponDef d) => d.type == WeaponType.Gun && !IsPistol(d) && d.name != "MOSSBERG 590";
        public static bool SuppressorFor(WeaponDef d) => d.type == WeaponType.Gun && d.name != "TASER" && d.name != "MOSSBERG 590";
        public static bool LightFor(WeaponDef d) => d.type == WeaponType.Gun && d.name != "TASER" && !d.scope;
        public static bool LaserFor(WeaponDef d) => d.name == "AR-15";
        public static bool MagnifierFor(WeaponDef d) => d.name == "AR-15";
        readonly List<Vector3> mountTop = new List<Vector3>(), mountUnder = new List<Vector3>();
        readonly List<GameObject> attach = new List<GameObject>();
        public bool Grabbing => grabbed != null;

        public string AmmoText
        {
            get
            {
                if (!Armed) return "UNARMED";
                if (Current.type == WeaponType.Knife) return "—";
                if (Current.type == WeaponType.Grenade || Current.type == WeaponType.Tool) return "∞";
                return reloading ? "RELOADING" : $"{ammo[current]:00} / {MagSize(current)}";
            }
        }

        public void Setup(PlayerController player)
        {
            pc = player;
            cam = player.cam;
            for (int i = 0; i < defs.Count; i++) ammo[i] = defs[i].magazine;
            owned = new bool[defs.Count];
            loadouts = new WeaponLoadout[defs.Count];
            for (int i = 0; i < defs.Count; i++) loadouts[i] = new WeaponLoadout { sight = defs[i].scope ? SightKind.Scope8x : SightKind.Iron };
            vmRoot = new GameObject("Viewmodel").transform;
            vmRoot.SetParent(cam.transform, false);
            BuildModels();
            // the sculpted hands built onto each gun: only used when there is no body to hold it with
            foreach (var t in vmRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("View_Hand") || t.name == "SupportHand" || t.name == "ForegripHand")
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { floatHands.Add(r); floatHandIsGun.Add(t.name.StartsWith("View_Hand")); }
            TrimHands();
            var fl = new GameObject("MuzzleLight");
            fl.transform.SetParent(vmRoot, false);
            muzzleLight = fl.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 6f;
            muzzleLight.color = new Color(1f, 0.75f, 0.4f);
            muzzleLight.intensity = 0f;
            muzzleLight.shadows = LightShadows.None;

            flash = new GameObject("MuzzleFlash");
            flash.transform.SetParent(vmRoot, false);
            var flashShader = Mats.Find("Carl/MuzzleFlash");
            for (int k = 0; k < 3; k++)
            {
                // flame petals along the barrel, at different angles round it
                var q = new GameObject("petal");
                q.transform.SetParent(flash.transform, false);
                q.transform.localRotation = Quaternion.Euler(0f, 0f, k * 60f) * Quaternion.Euler(90f, 0f, 0f);
                q.transform.localPosition = new Vector3(0f, 0f, 0.07f);
                q.transform.localScale = new Vector3(0.07f, 0.14f, 1f);
                q.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Quad;
                var mr = q.AddComponent<MeshRenderer>();
                var m = new Material(flashShader); m.SetFloat("_Shape", 1f);
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flashParts.Add(mr);
            }
            var front = new GameObject("star");
            front.transform.SetParent(flash.transform, false);
            front.transform.localScale = Vector3.one * 0.11f;
            front.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            front.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Quad;
            var fm = new Material(flashShader); fm.SetFloat("_Shape", 0f);
            var fr = front.AddComponent<MeshRenderer>(); fr.sharedMaterial = fm; fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flashParts.Add(fr);
            flash.SetActive(false);

            var lg = new GameObject("GrabLine");
            grabLine = lg.AddComponent<LineRenderer>();
            grabLine.positionCount = 2;
            grabLine.startWidth = 0.008f; grabLine.endWidth = 0.003f;
            grabLine.sharedMaterial = Mats.Unlit(new Color(0.4f, 0.8f, 1f, 0.8f), true);
            grabLine.enabled = false;

            current = 0;
            for (int k = 0; k < models.Count; k++) models[k].SetActive(false);
            // you start with only the physics gun: take the rest in the G menu
            int phys = defs.FindIndex(x => x.type == WeaponType.Tool);
            if (phys >= 0) { owned[phys] = true; Select(phys); }
            RefreshAttachments();
        }

        // ------------------------------------------------------------ viewmodels

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default, float smooth = 0.35f, float metal = 0.2f)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localRotation = Quaternion.Euler(euler);
            var mr = g.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit(c, smooth, metal);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        static GameObject Barrel(Transform p, Vector3 pos, float d, float len, Color c) =>
            Part(p, PrimitiveType.Cylinder, pos, new Vector3(d, len * 0.5f, d), c, new Vector3(90, 0, 0), 0.5f, 0.7f);

        static readonly string[] modelNames = { "Gun_AK47", "Gun_Glock", "Gun_AWP", "Gun_Barrett", "Gun_Shotgun", "Gun_Knife", "Gun_Grenade", "Gun_Hammer", "Gun_Sword", "Gun_AR15", "Gun_Beretta", "Gun_Taser", "Gun_PhysGun" };

        readonly List<float> ironY = new List<float>(), scopeY = new List<float>();

        /// <summary>Height of the line you look along for the current weapon and sight (model space).</summary>
        float SightHeight()
        {
            int i = current;
            if (i >= ironY.Count) return 0.12f;
            var d = Current;
            if (d.scope) return scopeY[i];
            switch (Loadout.sight)
            {
                case SightKind.RedDot: return mountTop[i].y + 0.03f;
                case SightKind.Holo: return mountTop[i].y + 0.032f;
                case SightKind.Scope4x: case SightKind.Scope8x: return mountTop[i].y + 0.04f;
                default: return ironY[i];
            }
        }

        void BuildModels()
        {
            if (ModelLibrary.Get(modelNames[0]) == null) { BuildFallbackModels(); return; }
            var skinMat = Mats.Lit(new Color(0.86f, 0.71f, 0.58f), 0.25f);
            foreach (var name in modelNames)
            {
                var root = new GameObject(name + "_VM").transform;
                root.SetParent(vmRoot, false);
                ModelLibrary.Spawn(name, root, ModelLibrary.GunMaterial, false);
                var model = ModelLibrary.Get(name);
                Vector3 grip = model.points.TryGetValue("Grip", out var g) ? g : Vector3.zero;
                Vector3 muzzle = model.points.TryGetValue("Muzzle", out var mz) ? mz : new Vector3(0, 0, 0.3f);
                AddArm(root, grip, skinMat, name);
                AddModel(root.gameObject, muzzle);
                var mv = model.mesh.vertices;
                Vector3 fore;
                if (!model.points.TryGetValue("Fore", out fore))
                    fore = new Vector3(0f, grip.y + 0.05f, Mathf.Lerp(grip.z, muzzle.z, 0.5f));
                gripPts.Add(grip); forePts.Add(fore);
                supportCup.Add(null); supportGrip.Add(null);
                AddSupportHand(root, grip, fore, mv, skinMat, name, supportCup.Count - 1);
                // rail and handguard mount points from the gun's shape
                float zA = Mathf.Lerp(grip.z, muzzle.z, 0.05f), zB = Mathf.Lerp(grip.z, muzzle.z, 0.35f), zC = Mathf.Lerp(grip.z, muzzle.z, 0.5f), zD = Mathf.Lerp(grip.z, muzzle.z, 0.68f);
                float top = float.MinValue, under = float.MaxValue;
                foreach (var v in mv)
                {
                    if (v.z > zA && v.z < zB) top = Mathf.Max(top, v.y);
                    if (v.z > zC && v.z < zD && Mathf.Abs(v.x) < 0.03f) under = Mathf.Min(under, v.y);
                }
                mountTop.Add(new Vector3(0f, top == float.MinValue ? 0.05f : top, (zA + zB) * 0.5f));
                // iron sight line: the top of the front sight (highest point on the centre line near the muzzle)
                float zF = Mathf.Lerp(grip.z, muzzle.z, 0.7f), iron = float.MinValue;
                foreach (var v in mv) if (v.z > zF && Mathf.Abs(v.x) < 0.006f) iron = Mathf.Max(iron, v.y);
                // a model can say exactly where its sight line runs (a hooded front post's top isn't the hood's)
                if (model.points.TryGetValue("Iron", out var ir)) iron = ir.y;
                ironY.Add(iron == float.MinValue ? 0.05f : iron);
                scopeY.Add(model.points.TryGetValue("Scope", out var sc) ? sc.y : (iron == float.MinValue ? 0.08f : iron + 0.04f));
                mountUnder.Add(new Vector3(0f, under == float.MaxValue ? -0.03f : under, (zC + zD) * 0.5f));
            }
        }

        /// <summary>Sculpted gripping hand + forearm (Blender model), rotated to each weapon's grip angle.</summary>
        static void AddArm(Transform root, Vector3 grip, Material skin, string weapon)
        {
            var hand = ModelLibrary.Spawn("View_Hand", root, _ => skin, false);
            if (hand == null) return;
            Quaternion rot;
            switch (weapon)
            {
                case "Gun_AK47": rot = Quaternion.Euler(18f, 0f, 0f); break;
                case "Gun_Glock": case "Gun_Beretta": case "Gun_Taser": case "Gun_PhysGun": rot = Quaternion.Euler(16f, 0f, 0f); break;
                case "Gun_AR15": rot = Quaternion.Euler(20f, 0f, 0f); break;
                case "Gun_AWP": rot = Quaternion.Euler(20f, 0f, 0f); break;
                case "Gun_Barrett": rot = Quaternion.Euler(18f, 0f, 0f); break;
                case "Gun_Shotgun": rot = Quaternion.Euler(30f, 0f, 0f); break;  // wraps the sloped wrist of the stock
                case "Gun_Knife": case "Gun_Hammer": case "Gun_Sword": rot = Quaternion.Euler(90f, 0f, 0f); break;   // handle runs forward
                default: rot = Quaternion.Euler(10f, 0f, -15f); break;           // grenade held in a fist
            }
            hand.transform.localPosition = grip;
            hand.transform.localRotation = rot;
        }

        readonly List<GameObject> supportCup = new List<GameObject>(), supportGrip = new List<GameObject>();
        static Material sleeveMat;

        static Material HandMat(string sub, Material skin)
        {
            if (sub == "Sleeve") { if (sleeveMat == null) sleeveMat = Mats.Lit(new Color(0.24f, 0.27f, 0.2f), 0.15f); return sleeveMat; }
            return skin;
        }

        /// <summary>
        /// The other hand. Long guns: the left hand cups the handguard from below, fingers wrapped up its side
        /// (sized to the handguard there); with a foregrip fitted it holds the grip instead. Pistol: wrapped over
        /// the shooting hand. Knife and grenade stay one-handed.
        /// </summary>
        void AddSupportHand(Transform root, Vector3 grip, Vector3 fore, Vector3[] mv, Material skin, string weapon, int idx)
        {
            if (weapon == "Gun_Knife" || weapon == "Gun_Grenade" || weapon == "Gun_Hammer" || weapon == "Gun_Sword" || weapon == "Gun_PhysGun") return;
            if (weapon == "Gun_Glock")
            {
                var ph = ModelLibrary.Spawn("View_Hand", root, sub => HandMat(sub, skin), false, true);
                if (ph == null) return;
                ph.name = "SupportHand";
                // thumbs-forward two-hand grip: the left hand sits on the left of the grip, a little lower
                ph.transform.localPosition = grip + new Vector3(-0.012f, -0.012f, 0.004f);
                ph.transform.localRotation = Quaternion.Euler(16f, 8f, 0f);
                supportCup[idx] = ph;
                return;
            }
            // find the handguard around the hold point: its centre line and thickness
            float zmin = fore.z - 0.025f, zmax = fore.z + 0.025f;
            float lo = float.MaxValue, hi = float.MinValue, left = float.MaxValue, right = float.MinValue;
            foreach (var v in mv)
            {
                if (v.z < zmin || v.z > zmax || Mathf.Abs(v.x) > 0.05f || v.y > grip.y + 0.15f || v.y < grip.y - 0.02f) continue;
                lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); left = Mathf.Min(left, v.x); right = Mathf.Max(right, v.x);
            }
            Vector3 c = fore; float r = 0.026f;
            if (lo < hi)
            {
                // the hand wraps the lower part (the handguard), not a scope or gas tube on top
                float h = Mathf.Min(hi - lo, 0.06f);
                c = new Vector3((left + right) * 0.5f, lo + h * 0.5f, fore.z);
                r = Mathf.Clamp(Mathf.Max(h, right - left) * 0.5f, 0.018f, 0.034f);
            }
            var cup = ModelLibrary.Spawn("View_SupportHand", root, sub => HandMat(sub, skin), false);
            if (cup == null) return;
            cup.name = "SupportHand";
            cup.transform.localPosition = c;
            cup.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.15f, Mathf.InverseLerp(0.02f, 0.032f, r));
            supportCup[idx] = cup;
            // holding a foregrip: the same left hand that holds a pistol grip, mirrored
            var gh = ModelLibrary.Spawn("View_Hand", root, sub => HandMat(sub, skin), false, true);
            if (gh != null) { gh.name = "ForegripHand"; gh.SetActive(false); supportGrip[idx] = gh; }
        }

        /// <summary>Left hand on the foregrip if one is fitted, otherwise round the handguard.</summary>
        void PlaceSupportHand(int i, GripKind g, Vector3 mount)
        {
            if (i >= supportCup.Count) return;
            var cup = supportCup[i]; var gh = supportGrip[i];
            bool onGrip = g != GripKind.None && gh != null;
            if (cup != null && gh != null) cup.SetActive(!onGrip);
            if (gh == null) return;
            gh.SetActive(onGrip);
            if (!onGrip) return;
            if (g == GripKind.Vertical)
            {
                gh.transform.localPosition = mount + new Vector3(0f, -0.052f, 0f);
                gh.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            }
            else
            {
                // angled grip: the heel of the hand pushed up against the wedge
                gh.transform.localPosition = mount + new Vector3(0f, -0.03f, -0.01f);
                gh.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            }
        }

        void BuildFallbackModels()
        {
            Color metal = new Color(0.16f, 0.16f, 0.17f), wood = new Color(0.78f, 0.45f, 0.24f), poly = new Color(0.12f, 0.12f, 0.13f);
            Color green = new Color(0.3f, 0.38f, 0.26f), skin = new Color(0.86f, 0.71f, 0.58f);

            // AK-47
            var ak = new GameObject("AK47").transform; ak.SetParent(vmRoot, false);
            Part(ak, PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(0.05f, 0.065f, 0.33f), metal);
            Part(ak, PrimitiveType.Cube, new Vector3(0, 0.002f, 0.25f), new Vector3(0.056f, 0.05f, 0.17f), wood, default, 0.4f, 0f);
            Barrel(ak, new Vector3(0, 0.018f, 0.44f), 0.018f, 0.3f, metal);
            Barrel(ak, new Vector3(0, 0.04f, 0.28f), 0.016f, 0.22f, metal);
            Part(ak, PrimitiveType.Cube, new Vector3(0, -0.025f, -0.27f), new Vector3(0.045f, 0.07f, 0.22f), wood, new Vector3(-6, 0, 0), 0.4f, 0f);
            Part(ak, PrimitiveType.Cube, new Vector3(0, -0.105f, 0.07f), new Vector3(0.032f, 0.15f, 0.06f), metal, new Vector3(18, 0, 0));
            Part(ak, PrimitiveType.Cube, new Vector3(0, -0.07f, -0.07f), new Vector3(0.034f, 0.09f, 0.04f), poly, new Vector3(-18, 0, 0));
            Part(ak, PrimitiveType.Cube, new Vector3(0, 0.05f, 0.55f), new Vector3(0.006f, 0.03f, 0.01f), metal);
            Part(ak, PrimitiveType.Capsule, new Vector3(0.015f, -0.09f, -0.06f), new Vector3(0.06f, 0.07f, 0.06f), skin, new Vector3(70, 0, 0), 0.2f, 0f);
            AddModel(ak.gameObject, new Vector3(0, 0.018f, 0.6f));

            // Glock 17
            var gl = new GameObject("Glock").transform; gl.SetParent(vmRoot, false);
            Part(gl, PrimitiveType.Cube, new Vector3(0, 0.02f, 0.02f), new Vector3(0.03f, 0.033f, 0.19f), poly, default, 0.5f, 0.3f);
            Part(gl, PrimitiveType.Cube, new Vector3(0, -0.005f, 0.015f), new Vector3(0.027f, 0.022f, 0.16f), poly);
            Part(gl, PrimitiveType.Cube, new Vector3(0, -0.06f, -0.045f), new Vector3(0.029f, 0.105f, 0.045f), poly, new Vector3(-16, 0, 0));
            Part(gl, PrimitiveType.Capsule, new Vector3(0, -0.07f, -0.06f), new Vector3(0.06f, 0.06f, 0.06f), skin, new Vector3(60, 0, 0), 0.2f, 0f);
            AddModel(gl.gameObject, new Vector3(0, 0.02f, 0.12f));

            // AWP
            var awp = new GameObject("AWP").transform; awp.SetParent(vmRoot, false);
            Part(awp, PrimitiveType.Cube, new Vector3(0, 0, 0.05f), new Vector3(0.055f, 0.07f, 0.62f), green, default, 0.3f, 0.1f);
            Barrel(awp, new Vector3(0, 0.02f, 0.55f), 0.02f, 0.5f, metal);
            Part(awp, PrimitiveType.Cube, new Vector3(0, -0.03f, -0.36f), new Vector3(0.045f, 0.11f, 0.24f), green);
            Barrel(awp, new Vector3(0, 0.085f, 0.06f), 0.045f, 0.32f, metal);
            Barrel(awp, new Vector3(0, 0.085f, 0.23f), 0.055f, 0.05f, metal);
            Part(awp, PrimitiveType.Cube, new Vector3(0, -0.08f, 0.1f), new Vector3(0.03f, 0.07f, 0.06f), metal);
            AddModel(awp.gameObject, new Vector3(0, 0.02f, 0.82f));

            // .50 cal (Barrett-style)
            var bar = new GameObject("Barrett").transform; bar.SetParent(vmRoot, false);
            Part(bar, PrimitiveType.Cube, new Vector3(0, 0, 0.05f), new Vector3(0.07f, 0.1f, 0.72f), metal);
            Barrel(bar, new Vector3(0, 0.02f, 0.65f), 0.03f, 0.6f, metal);
            Part(bar, PrimitiveType.Cube, new Vector3(0, 0.02f, 0.98f), new Vector3(0.07f, 0.045f, 0.09f), metal);
            Barrel(bar, new Vector3(0, 0.1f, 0.05f), 0.05f, 0.36f, poly);
            Part(bar, PrimitiveType.Cube, new Vector3(0, -0.11f, 0.12f), new Vector3(0.04f, 0.13f, 0.08f), metal, new Vector3(10, 0, 0));
            Part(bar, PrimitiveType.Cube, new Vector3(0, -0.03f, -0.4f), new Vector3(0.055f, 0.13f, 0.2f), poly);
            AddModel(bar.gameObject, new Vector3(0, 0.02f, 1.04f));

            // Shotgun (pump)
            var sg = new GameObject("Shotgun").transform; sg.SetParent(vmRoot, false);
            Part(sg, PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(0.042f, 0.066f, 0.215f), metal);
            Barrel(sg, new Vector3(0, 0.02f, 0.38f), 0.024f, 0.56f, metal);
            Barrel(sg, new Vector3(0, -0.012f, 0.27f), 0.045f, 0.18f, wood);
            Part(sg, PrimitiveType.Cube, new Vector3(0, -0.04f, -0.27f), new Vector3(0.045f, 0.09f, 0.3f), wood, new Vector3(-8, 0, 0), 0.4f, 0f);
            AddModel(sg.gameObject, new Vector3(0, 0.02f, 0.665f));

            // Knife
            var kn = new GameObject("Knife").transform; kn.SetParent(vmRoot, false);
            Part(kn, PrimitiveType.Cube, new Vector3(0, 0, 0.13f), new Vector3(0.004f, 0.032f, 0.2f), new Color(0.8f, 0.82f, 0.85f), default, 0.9f, 0.9f);
            Part(kn, PrimitiveType.Cube, new Vector3(0, 0, 0f), new Vector3(0.026f, 0.034f, 0.12f), poly);
            Part(kn, PrimitiveType.Cube, new Vector3(0, 0, 0.065f), new Vector3(0.03f, 0.05f, 0.01f), metal);
            Part(kn, PrimitiveType.Capsule, new Vector3(0, -0.01f, -0.01f), new Vector3(0.06f, 0.06f, 0.06f), skin, new Vector3(80, 0, 0), 0.2f, 0f);
            AddModel(kn.gameObject, new Vector3(0, 0, 0.25f));

            // Grenade
            var gr = new GameObject("GrenadeVM").transform; gr.SetParent(vmRoot, false);
            var body = new GameObject("g"); body.transform.SetParent(gr, false);
            body.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Ellipsoid(new Vector3(0.032f, 0.042f, 0.032f), 12, 8);
            body.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.25f, 0.3f, 0.2f), 0.3f);
            Part(gr, PrimitiveType.Cube, new Vector3(0.01f, 0.045f, 0f), new Vector3(0.012f, 0.02f, 0.02f), metal);
            Part(gr, PrimitiveType.Cube, new Vector3(0.03f, 0.02f, 0f), new Vector3(0.006f, 0.06f, 0.015f), metal, new Vector3(0, 0, -10));
            Part(gr, PrimitiveType.Capsule, new Vector3(0, -0.03f, -0.02f), new Vector3(0.07f, 0.06f, 0.07f), skin, new Vector3(60, 0, 0), 0.2f, 0f);
            AddModel(gr.gameObject, Vector3.zero);
        }

        /// <summary>
        /// Glock extras: a 33-round magazine sticking well out of the grip (with its baseplate), and the switch -
        /// a small block on the back of the slide with its selector lever.
        /// </summary>
        void GlockParts(int i, Transform root, WeaponLoadout L)
        {
            // the gun's mesh sits on a child of the weapon root; build on it, in the mesh's own space
            var mf = models[i].GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            root = mf.transform;
            var b = mf.sharedMesh.bounds;
            var pts = ModelLibrary.Get("Gun_Glock")?.points ?? new Dictionary<string, Vector3>();
            Material poly = ModelLibrary.GunMaterial("Polymer"), steel = ModelLibrary.GunMaterial("Steel"), metal = ModelLibrary.GunMaterial("Metal");
            GameObject Box(string n, Vector3 pos, Vector3 size, Vector3 euler, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = n;
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false);
                g.transform.localPosition = pos; g.transform.localEulerAngles = euler; g.transform.localScale = size;
                var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.layer = models[i].layer;
                attach.Add(g);
                return g;
            }
            if (L.extMag)
            {
                // follows the grip's rake out of the bottom of the frame
                // the model marks the bottom of the magazine well (MagBase); the grip is raked about 17 degrees
                Vector3 baseP = pts.TryGetValue("MagBase", out var mb) ? mb : new Vector3(0f, b.min.y, b.min.z + 0.035f);
                Vector3 down = Quaternion.Euler(-17f, 0f, 0f) * Vector3.down;
                Box("ExtMag", baseP + down * 0.038f, new Vector3(0.0215f, 0.076f, 0.03f), new Vector3(-17f, 0f, 0f), poly);
                Box("ExtMagBase", baseP + down * 0.079f, new Vector3(0.026f, 0.009f, 0.036f), new Vector3(-17f, 0f, 0f), poly);
            }
            if (L.autoSwitch)
            {
                // on the slide's back plate (SlideBack on the model)
                Vector3 back = pts.TryGetValue("SlideBack", out var sbk) ? sbk + new Vector3(0f, 0f, -0.0035f) : new Vector3(0f, b.max.y - 0.012f, b.min.z + 0.012f);
                Box("Switch", back, new Vector3(0.02f, 0.017f, 0.007f), Vector3.zero, metal);
                Box("SwitchLever", back + new Vector3(0.009f, 0.004f, -0.003f), new Vector3(0.006f, 0.004f, 0.012f), new Vector3(0f, 25f, 0f), steel);
            }
        }

        /// <summary>Rebuilds the sight and grip models on the weapon after the loadout changed.</summary>
        public void RefreshAttachments()
        {
            foreach (var a in attach) if (a) Destroy(a);
            attach.Clear();
            reticles.Clear();
            for (int i = 0; i < models.Count && i < mountTop.Count; i++)
            {
                var d = defs[i]; var L = loadouts[i];
                if (d.type != WeaponType.Gun) continue;
                Transform root = models[i].transform;
                Color blk = new Color(0.07f, 0.07f, 0.08f), dark = new Color(0.14f, 0.14f, 0.15f);
                var top = mountTop[i];
                if (!d.scope)
                {
                    string m = L.sight == SightKind.RedDot ? "Att_RedDot" : L.sight == SightKind.Holo ? "Att_Holo" : (L.sight == SightKind.Scope4x || L.sight == SightKind.Scope8x) ? "Att_Scope" : null;
                    if (m != null)
                    {
                        var g = ModelLibrary.Spawn(m, root, ModelLibrary.GunMaterial, false);
                        if (g != null)
                        {
                            g.transform.localPosition = top;
                            attach.Add(g);
                            // the projected reticle on the glass, right on the sight line
                            if (L.sight == SightKind.RedDot || L.sight == SightKind.Holo)
                            {
                                var ret = new GameObject("Reticle");
                                ret.transform.SetParent(g.transform, false);
                                ret.transform.localPosition = L.sight == SightKind.RedDot ? new Vector3(0f, 0.03f, 0.027f) : new Vector3(0f, 0.033f, 0.017f);
                                ret.transform.localScale = Vector3.one * (L.sight == SightKind.RedDot ? 0.02f : 0.03f);
                                ret.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Quad;
                                var rm = new Material(Mats.Find("Carl/Reticle"));
                                rm.SetFloat("_Shape", L.sight == SightKind.Holo ? 1f : 0f);
                                var rr = ret.AddComponent<MeshRenderer>(); rr.sharedMaterial = rm; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                                reticles.Add(rr);
                            }
                        }
                    }
                }
                if (IsGlock(i)) GlockParts(i, root, L);
                bool gripOn = L.grip != GripKind.None && GripsFor(d);
                if (gripOn)
                {
                    var g = ModelLibrary.Spawn(L.grip == GripKind.Vertical ? "Att_GripVertical" : "Att_GripAngled", root, ModelLibrary.GunMaterial, false);
                    if (g != null) { g.transform.localPosition = mountUnder[i]; attach.Add(g); }
                }
                PlaceSupportHand(i, gripOn ? L.grip : GripKind.None, mountUnder[i]);
                RailAndMuzzle(i, root, d, L);
                ShowIrons(i, d.scope || L.sight == SightKind.Iron);
            }
        }

        // ------------------------------------------------------------ iron sights off when an optic goes on
        // Regions of each gun's mesh (model space) holding its iron sights: the AR's folding sights come off, the
        // Glock's slide sights, the AK's front post. The rest of the gun is untouched.
        static readonly Dictionary<string, Bounds[]> ironRegions = new Dictionary<string, Bounds[]>
        {
            ["AR-15"] = new[] { MinMax(-0.03f, 0.031f, 0.37f, 0.03f, 0.09f, 0.416f), MinMax(-0.03f, 0.031f, -0.123f, 0.03f, 0.09f, -0.088f) },
            ["GLOCK 17"] = new[] { MinMax(-0.02f, 0.0515f, -0.098f, 0.02f, 0.07f, -0.068f), MinMax(-0.02f, 0.0515f, 0.094f, 0.02f, 0.07f, 0.118f) },
            ["AK-47"] = new[] { MinMax(-0.012f, 0.0585f, 0.53f, 0.012f, 0.09f, 0.575f) },
        };
        static Bounds MinMax(float x0, float y0, float z0, float x1, float y1, float z1) { var b = new Bounds(); b.SetMinMax(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1)); return b; }
        readonly Dictionary<int, (MeshFilter mf, Mesh full, Mesh bare)> ironMesh = new Dictionary<int, (MeshFilter, Mesh, Mesh)>();

        void ShowIrons(int i, bool show)
        {
            if (!ironRegions.TryGetValue(defs[i].name, out var regions)) return;
            if (!ironMesh.TryGetValue(i, out var e))
            {
                MeshFilter mf = null;
                foreach (var f in models[i].GetComponentsInChildren<MeshFilter>(true)) if (f.name.StartsWith("Gun_")) { mf = f; break; }
                if (mf == null || mf.sharedMesh == null) return;
                var full = mf.sharedMesh;
                var v = full.vertices;
                var bare = Object.Instantiate(full);
                for (int s = 0; s < full.subMeshCount; s++)
                {
                    var tri = full.GetTriangles(s);
                    var keep = new List<int>(tri.Length);
                    for (int k = 0; k < tri.Length; k += 3)
                    {
                        Vector3 c = (v[tri[k]] + v[tri[k + 1]] + v[tri[k + 2]]) / 3f;
                        bool inside = false;
                        foreach (var b in regions) if (b.Contains(c)) { inside = true; break; }
                        if (!inside) { keep.Add(tri[k]); keep.Add(tri[k + 1]); keep.Add(tri[k + 2]); }
                    }
                    bare.SetTriangles(keep, s);
                }
                ironMesh[i] = e = (mf, full, bare);
            }
            e.mf.sharedMesh = show ? e.full : e.bare;
        }

        // ------------------------------------------------------------ rails and muzzle devices
        readonly List<Vector3> baseMuzzle = new List<Vector3>();
        readonly Light[] lights = new Light[16];
        readonly Transform[] laserEmit = new Transform[16];
        GameObject laserDot;

        /// <summary>Where each gun takes a light: under a pistol's dust cover, on the right of a rifle's handguard.</summary>
        static (Vector3 pos, Vector3 euler) LightMount(string gun)
        {
            switch (gun)
            {
                case "GLOCK 17": return (new Vector3(0f, 0.0f, 0.068f), Vector3.zero);
                case "BERETTA M9": return (new Vector3(0f, 0.0f, 0.075f), Vector3.zero);
                case "AR-15": return (new Vector3(0.0236f, 0.0f, 0.33f), new Vector3(0f, 0f, 90f));
                case "AK-47": return (new Vector3(0.026f, -0.004f, 0.215f), new Vector3(0f, 0f, 90f));
                case "MOSSBERG 590": return (new Vector3(0.012f, -0.015f, 0.53f), new Vector3(0f, 0f, 90f));
                default: return (new Vector3(0f, -0.03f, 0.3f), Vector3.zero);
            }
        }

        void RailAndMuzzle(int i, Transform root, WeaponDef d, WeaponLoadout L)
        {
            while (baseMuzzle.Count < muzzles.Count) baseMuzzle.Add(muzzles[baseMuzzle.Count].localPosition);
            muzzles[i].localPosition = baseMuzzle[i];
            lights[i] = null; laserEmit[i] = null;
            if (L.suppressor && SuppressorFor(d))
            {
                // screwed onto the muzzle: the gun gets that much longer, and the muzzle is now its front
                var g = ModelLibrary.Spawn(IsPistol(d) ? "Att_SuppPistol" : "Att_SuppRifle", root, ModelLibrary.GunMaterial, false);
                if (g != null)
                {
                    g.transform.localPosition = baseMuzzle[i];
                    attach.Add(g);
                    var m = ModelLibrary.Get(IsPistol(d) ? "Att_SuppPistol" : "Att_SuppRifle");
                    muzzles[i].localPosition = baseMuzzle[i] + (m != null && m.points.TryGetValue("Front", out var f) ? f : new Vector3(0f, 0f, 0.18f));
                }
            }
            if (L.light && LightFor(d))
            {
                var g = ModelLibrary.Spawn("Att_Light", root, ModelLibrary.GunMaterial, false);
                if (g != null)
                {
                    var mnt = LightMount(d.name);
                    g.transform.localPosition = mnt.pos; g.transform.localRotation = Quaternion.Euler(mnt.euler);
                    attach.Add(g);
                    var lo = new GameObject("Beam");
                    lo.transform.SetParent(g.transform, false);
                    lo.transform.localPosition = new Vector3(0f, -0.022f, 0.05f);
                    var lt = lo.AddComponent<Light>();
                    // 1000 lumens of tight hotspot and wide spill
                    lt.type = LightType.Spot; lt.spotAngle = 38f; lt.innerSpotAngle = 9f; lt.range = 45f; lt.intensity = 9f;
                    lt.color = new Color(1f, 0.97f, 0.92f); lt.shadows = LightShadows.Soft;
                    lights[i] = lt;
                    attach.Add(lo);
                }
            }
            if (L.laser && LaserFor(d))
            {
                var g = ModelLibrary.Spawn("Att_Laser", root, ModelLibrary.GunMaterial, false);
                if (g != null)
                {
                    g.transform.localPosition = new Vector3(0f, 0.0355f, 0.33f);
                    attach.Add(g);
                    var e = new GameObject("LaserEmit").transform;
                    e.SetParent(g.transform, false); e.localPosition = new Vector3(-0.009f, 0.03f, 0.043f);
                    laserEmit[i] = e;
                }
            }
            if (L.magnifier && MagnifierFor(d) && (L.sight == SightKind.RedDot || L.sight == SightKind.Holo) && i < mountTop.Count)
            {
                var g = ModelLibrary.Spawn("Att_Magnifier", root, ModelLibrary.GunMaterial, false);
                if (g != null) { g.transform.localPosition = mountTop[i] + new Vector3(0f, 0f, -0.07f); attach.Add(g); }
                // the red dot / holo moves forward on the rail to make room
                foreach (var a in attach)
                    if (a != null && a != g && a.transform.parent == root && (a.name.StartsWith("Att_RedDot") || a.name.StartsWith("Att_Holo")))
                        a.transform.localPosition = mountTop[i] + new Vector3(0f, 0f, 0.05f);
            }
        }

        /// <summary>The light is on while fitted; the laser puts its dot wherever the barrel line meets something.</summary>
        void UpdateRailGear()
        {
            for (int k = 0; k < lights.Length; k++) if (lights[k] != null) lights[k].enabled = Armed && k == current;
            var e = Armed && current < laserEmit.Length ? laserEmit[current] : null;
            bool on = e != null && vmRoot.gameObject.activeInHierarchy;
            if (laserDot == null && on)
            {
                laserDot = new GameObject("LaserDot");
                laserDot.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Quad;
                var mr = laserDot.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Unlit(new Color(1f, 0.08f, 0.05f, 0.95f), true);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (laserDot == null) return;
            laserDot.SetActive(false);
            if (!on) return;
            Vector3 dir = e.forward;
            if (SelfRay(e.position, dir, out var h, 150f))
            {
                laserDot.SetActive(true);
                laserDot.transform.position = h.point + h.normal * 0.004f;
                laserDot.transform.rotation = Quaternion.LookRotation(-h.normal);
                laserDot.transform.localScale = Vector3.one * (0.006f + h.distance * 0.0006f);
            }
        }

        void AddModel(GameObject g, Vector3 muzzleLocal)
        {
            var m = new GameObject("Muzzle").transform;
            m.SetParent(g.transform, false);
            m.localPosition = muzzleLocal;
            models.Add(g);
            muzzles.Add(m);
            g.SetActive(false);
        }

        public void Select(int i)
        {
            if (i < 0 || i >= defs.Count || (owned != null && !owned[i])) return;
            current = i;
            reloading = false;
            for (int k = 0; k < models.Count; k++) models[k].SetActive(k == i);
            nextFire = Time.unscaledTime + 0.25f;
            kick = -0.6f;
        }

        // ------------------------------------------------------------ input

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || pc == null) return;
            bool locked = Cursor.lockState == CursorLockMode.Locked && !Menus.Blocking;
            float dt = Time.unscaledDeltaTime;

            if (kb.digit1Key.wasPressedThisFrame) Select(0);
            if (kb.digit2Key.wasPressedThisFrame) Select(1);
            if (kb.digit3Key.wasPressedThisFrame) Select(2);
            if (kb.digit4Key.wasPressedThisFrame) Select(3);
            if (kb.digit5Key.wasPressedThisFrame) Select(4);
            if (kb.digit6Key.wasPressedThisFrame) Select(5);
            if (kb.digit7Key.wasPressedThisFrame) Select(6);
            if (kb.digit8Key.wasPressedThisFrame) Select(7);
            if (kb.digit9Key.wasPressedThisFrame) Select(8);
            float scroll = mouse.scroll.ReadValue().y;
            if (!Grabbing && Mathf.Abs(scroll) > 0.1f && !swingFree)
                for (int s = 1; s < defs.Count; s++) { int k = (current + (scroll < 0 ? s : defs.Count - s)) % defs.Count; if (owned[k]) { Select(k); break; } }

            UpdateGrab(kb, mouse, locked, scroll);

            var d = Current;
            var body = PlayerBody.I;
            int arms = body != null ? body.UsableArms : 2;
            // out cold, dead, or no arm left that can hold it: nothing works
            bool able = Armed && arms > 0 && (body == null || (!body.dead && !body.passedOut));
            if (!able) { meleeMode = 0; swingFree = false; suicideT = 0f; }
            // K: put the gun under your chin
            if (able && locked && d.type == WeaponType.Gun && kb.kKey.wasPressedThisFrame && !kb.leftShiftKey.isPressed && suicideT <= 0f) suicideT = 0.0001f;
            if (suicideT > 0f) { suicideT += dt; if (suicideT >= 1.1f) EndIt(d); }
            bool aimHeld = (locked && mouse.rightButton.isPressed || DebugAim) && d.type == WeaponType.Gun && !reloading;
            aimT = Mathf.MoveTowards(aimT, aimHeld ? 1f : 0f, dt * (IsScope ? 5f : Loadout.grip == GripKind.Angled ? 11f : 8f));
            // point aim (LEFT ALT with a gun, not while aiming down the sights)
            bool pointHeld = locked && kb.leftAltKey.isPressed && d.type == WeaponType.Gun && !aimHeld && !reloading;
            pointT = Mathf.MoveTowards(pointT, pointHeld ? 1f : 0f, dt * 7f);
            float fov = Mathf.Lerp(pc.fovNormal, d.type == WeaponType.Gun ? ZoomFov : pc.fovNormal, Mathf.SmoothStep(0, 1, aimT));
            fov = Mathf.Lerp(fov, pc.fovNormal * 0.9f, pointT * (1f - aimT));
            cam.fieldOfView = fov;
            pc.sensitivityScale = fov / pc.fovNormal;

            if (reloading && Time.unscaledTime >= reloadEnd) { reloading = false; ammo[current] = MagSize(current); }
            if (GameSettings.InfiniteAmmo && d.type == WeaponType.Gun) ammo[current] = MagSize(current);
            if (d.type == WeaponType.Gun && ammo[current] > MagSize(current)) ammo[current] = MagSize(current);   // extended mag taken off
            if (locked && kb.rKey.wasPressedThisFrame && d.type == WeaponType.Gun && !reloading && ammo[current] < MagSize(current)) StartReload();

            // ALT held with a blade or hammer: the mouse moves the weapon instead of the view
            swingFree = able && locked && d.type == WeaponType.Knife && kb.leftAltKey.isPressed && suicideT <= 0f;
            if (swingFree) { pc.sensitivityScale = 0f; UpdateFreeSwing(mouse, dt); } else { swingPos = Vector2.Lerp(swingPos, Vector2.zero, dt * 8f); swingTipValid = false; }

            if (locked && !Grabbing && able && !swingFree && suicideT <= 0f)
            {
                switch (d.type)
                {
                    case WeaponType.Gun:
                        bool trigger = mouse.leftButton.wasPressedThisFrame || (IsAuto(current) && mouse.leftButton.isPressed);
                        if (trigger && !reloading && Time.unscaledTime >= nextFire)
                        {
                            bool taserLive = d.name == "TASER" && TaserWires.Current != null && TaserWires.Current.Arrived;
                            if (ammo[current] <= 0 && !taserLive) { Sfx.Play("click_empty", cam.transform.position, 0.6f, 0f); StartReload(); }
                            else FireGun(d);
                        }
                        break;
                    case WeaponType.Knife:
                        // hammer and sword: both buttons swing (left overhead, right across)
                        if (d.bulletMass > 0.5f && meleeMode == 0 && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                        { meleeMode = mouse.leftButton.wasPressedThisFrame ? 3 : 2; meleeT = 0f; meleeHitDone = false; Sfx.Play("knife", cam.transform.position, 0.8f, 0f); break; }
                        if (meleeMode == 0 && mouse.leftButton.wasPressedThisFrame) { meleeMode = 1; meleeT = 0f; meleeHitDone = false; Sfx.Play("knife", cam.transform.position, 0.6f, 0f); }
                        if (meleeMode == 0 && mouse.rightButton.wasPressedThisFrame) { meleeMode = 2; meleeT = 0f; meleeHitDone = false; Sfx.Play("knife", cam.transform.position, 0.7f, 0f); }
                        break;
                    case WeaponType.Grenade:
                        if (Time.unscaledTime >= nextFire && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                        {
                            bool far = mouse.leftButton.wasPressedThisFrame;
                            // more shrapnel = a heavier grenade that doesn't throw as far
                            float heavy = Mathf.Lerp(1.15f, 0.7f, Mathf.InverseLerp(100f, 700f, Loadout.shrapnel));
                            Vector3 v = (cam.transform.forward * (far ? 17f : 7f) + Vector3.up * (far ? 2.5f : 1.5f)) * heavy;
                            var gr = Grenade.Throw(cam.transform.position + cam.transform.forward * 0.5f - cam.transform.up * 0.1f, v);
                            gr.shrapnel = Loadout.shrapnel;
                            if (Loadout.FuseSeconds < 0f) { gr.impact = true; gr.fuse = 30f; } else gr.fuse = Loadout.FuseSeconds;
                            nextFire = Time.unscaledTime + 0.9f;
                            kick = 1.2f;
                        }
                        break;
                }
            }
            UpdateMelee(dt);
            Animate(dt);
            if (!Armed) vmRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ K
        float suicideT;

        void EndIt(WeaponDef d)
        {
            suicideT = 0f;
            Vector3 head = cam.transform.position;
            Sfx.Gun(d.name, head);
            Fx.Muzzle(head - cam.transform.up * 0.1f, cam.transform.up, 1f);
            BloodSystem.Instance?.Spray(head + cam.transform.up * 0.1f, cam.transform.up - cam.transform.forward * 0.3f, 40, 2f, 6f, 0.5f, 0.008f, null);
            if (d.type == WeaponType.Gun) ammo[current] = Mathf.Max(0, ammo[current] - 1);
            var me = PlayerAvatar.Body;
            if (me != null && me.Head != null)
            {
                // a real round up through your own head: the body takes it like any other (and drops)
                var am = CurrentAmmo;
                Vector3 under = me.Head.transform.position - Vector3.up * 0.09f + me.Head.transform.forward * 0.03f;
                me.damage.ApplyProjectile(me.Head, under, Vector3.up, am != null ? am.Energy : d.Energy, am != null ? am.diameter : d.diameter, HitKind.Bullet, out _, out _);
                if (!me.physio.dead) me.physio.Kill("SELF-INFLICTED GUNSHOT", false);
            }
            else PlayerBody.I?.Die("SELF-INFLICTED GUNSHOT");
        }

        static readonly RaycastHit[] selfBuf = new RaycastHit[24];

        /// <summary>A raycast that never stops on your own body (the camera is inside your head).</summary>
        static bool SelfRay(Vector3 o, Vector3 d, out RaycastHit hit, float len)
        {
            hit = default;
            int n = Physics.RaycastNonAlloc(o, d.normalized, selfBuf, len, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool found = false;
            var me = PlayerAvatar.Body;
            for (int i = 0; i < n; i++)
            {
                var c = selfBuf[i].collider;
                if (c is CharacterController) continue;
                if (me != null && c.GetComponentInParent<HumanBody>() == me) continue;
                if (selfBuf[i].distance < best) { best = selfBuf[i].distance; hit = selfBuf[i]; found = true; }
            }
            return found;
        }

        // ---- your blade, for the Earls (parries)
        public bool BladeOut => Armed && Current.type == WeaponType.Knife && current < muzzles.Count;
        public Vector3 BladeTip => muzzles[current].position;
        public Vector3 BladeHilt => models[current].transform.position;

        /// <summary>An Earl's blade met yours: your swing is thrown back.</summary>
        public void Parried(Vector3 worldImpulse)
        {
            Vector3 l = cam.transform.InverseTransformDirection(worldImpulse);
            swingVel += new Vector2(l.x, l.y) * 2f;
            kick = Mathf.Max(kick, 0.6f);
            pc.Shake(0.15f);
        }

        // ------------------------------------------------------------ free swing (ALT)
        bool swingFree, swingTipValid;
        Vector2 swingPos, swingVel;
        Vector3 lastTip, lastHilt;
        float swingCooldown;

        /// <summary>
        /// The weapon hangs in your hand on a spring and follows the mouse; heavier weapons lag and overshoot more.
        /// Whatever the blade or head sweeps through at speed gets hit: cut by the sword and knife, crushed by the
        /// hammer. Armour and walls stop it dead with a clang.
        /// </summary>
        void UpdateFreeSwing(Mouse mouse, float dt)
        {
            var d = Current;
            float mass = d.bulletMass > 0f ? d.bulletMass : 0.25f;
            Vector2 target = swingPos + mouse.delta.ReadValue() * 0.005f;
            // the whole reach: right across the screen, low to high
            target = new Vector2(Mathf.Clamp(target.x, -1.6f, 1.6f), Mathf.Clamp(target.y, -1f, 1.35f));
            // spring toward where the mouse is pushing it (an injured arm is weaker)
            float strength = 120f / mass * Mathf.Lerp(0.4f, 1f, PlayerBody.I != null ? Mathf.Min(PlayerBody.I.armL, PlayerBody.I.armR) : 1f);
            swingVel += (target - swingPos) * strength * dt - swingVel * Mathf.Min(1f, 9f * dt);
            swingPos += swingVel * dt;
            swingPos = new Vector2(Mathf.Clamp(swingPos.x, -1.7f, 1.7f), Mathf.Clamp(swingPos.y, -1.1f, 1.45f));
            swingCooldown -= dt;
            // pushed up hard: a thrust straight ahead
            if (swingPos.y > 1.05f && swingVel.y > 2.5f && swingCooldown <= 0f)
            {
                thrustT = 0.25f;
                if (d.name == "HAMMER") HeavySwing(true); else Stab();
                swingCooldown = 0.45f;
            }
            thrustT = Mathf.Max(0f, thrustT - dt);

            var muzzle = muzzles[current];
            Vector3 tip = muzzle.position, hilt = models[current].transform.position;
            if (swingTipValid && swingCooldown <= 0f)
            {
                float speed = (tip - lastTip).magnitude / Mathf.Max(1e-4f, dt);
                int what = speed > 2.5f ? SweepHit(lastTip, tip, lastHilt, hilt, speed, mass) : 0;
                // through flesh the blade slows right down and keeps going; wood, stone and steel stop it
                if (what == 1) { swingVel *= 0.3f; swingCooldown = 0.08f; }
                else if (what == 2) { swingVel = -swingVel * 0.3f; swingCooldown = 0.15f; pc.Shake(0.1f); }
            }
            lastTip = tip; lastHilt = hilt; swingTipValid = true;
        }

        float thrustT;

        /// <summary>0 nothing, 1 flesh (the swing carries on, slowed), 2 something hard (it stops / bounces).</summary>
        int SweepHit(Vector3 tipA, Vector3 tipB, Vector3 hiltA, Vector3 hiltB, float speed, float mass)
        {
            var d = Current;
            bool hammer = d.name == "HAMMER";
            for (int k = 0; k <= 3; k++)
            {
                float f = k / 3f;
                Vector3 a = Vector3.Lerp(hiltA, tipA, f), b = Vector3.Lerp(hiltB, tipB, f);
                Vector3 dir = b - a; float len = dir.magnitude;
                if (len < 1e-4f) continue;
                dir /= len;
                // armour and the other man's blade: a clang and it bounces off
                var arm = ArmourSystem.Raycast(a, dir, len + 0.02f, out float ad, out Vector3 al, null, PlayerAvatar.Body);
                if (arm != null)
                {
                    Fx.Sparks(a + dir * ad, -dir, 12);
                    Sfx.Play("impact_metal", a + dir * ad, 0.8f, 1f, 2f);
                    arm.AddWear(al, 0.2f * mass);
                    return 2;
                }
                if (!SelfRay(a, dir, out var hit, len + 0.02f) || hit.collider is CharacterController) continue;
                var gunner = hit.collider.GetComponentInParent<EarlGunner>();
                if (gunner != null && hit.collider.GetComponent<BodyPart>() == null)
                {
                    Fx.Sparks(hit.point, hit.normal, 14); Sfx.Play("impact_metal", hit.point, 0.9f, 1f, 2f);
                    gunner.Knocked(dir * speed * mass * 0.2f);
                    return 2;
                }
                var bp = hit.collider.GetComponent<BodyPart>();
                float energy = 0.5f * mass * speed * speed;
                if (bp != null && bp.body != null)
                {
                    if (hammer)
                    {
                        // crushing: the same path as a very slow, very wide projectile that stops in the body
                        bp.body.damage.ApplyProjectile(bp, hit.point, dir, energy * 0.6f, 0.045f, HitKind.Blunt, out _, out _);
                        bp.rb.AddForceAtPosition(dir * energy * 0.08f, hit.point, ForceMode.Impulse);
                    }
                    else
                    {
                        // a cut along the swing; a hard one goes deep enough to take a limb off at the joint
                        Vector3 sweep = Vector3.ProjectOnPlane(dir, hit.normal).normalized;
                        bp.body.damage.ApplySlash(bp, hit.point, hit.normal, sweep);
                        if (energy > 60f) bp.body.damage.ApplySlash(bp, hit.point, hit.normal, sweep);
                        if (energy > 140f && !Anatomy.IsTorso(bp.id) && bp.id != PartId.Head && Random.value < 0.5f) bp.body.damage.Sever(bp, dir, energy * 0.05f);
                        bp.rb.AddForceAtPosition(dir * energy * 0.03f, hit.point, ForceMode.Impulse);
                    }
                    HumanBrain.BroadcastNoise(hit.point, 8f, 0.5f);
                    return 1;
                }
                // a wall or the floor
                Fx.Sparks(hit.point, hit.normal, hammer ? 4 : 8);
                Sfx.Impact(hit.collider, hit.point);
                if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(dir * energy * 0.05f, hit.point, ForceMode.Impulse);
                return 2;
            }
            return 0;
        }

        // ------------------------------------------------------------ arms holding the gun
        Vector2 swayPos, swayVel;
        Vector2 aimSwayDeg;

        /// <summary>Where the gun really points: along its barrel in your hands (or, with no body, the view pushed
        /// about by the swaying spring).</summary>
        public Vector3 AimDirection =>
            cam == null ? Vector3.forward :
            BodyHeld && gInit && Armed && Current.type == WeaponType.Gun ? gRot * Vector3.forward :
            cam.transform.rotation * Quaternion.Euler(-aimSwayDeg.y, aimSwayDeg.x, 0f) * Vector3.forward;

        /// <summary>Where a round would land: down the barrel from the muzzle to whatever is in the way.</summary>
        public Vector3 AimPoint
        {
            get
            {
                Vector3 dir = AimDirection;
                if (!(BodyHeld && gInit && Armed && Current.type == WeaponType.Gun)) return cam.transform.position + dir * 20f;
                Vector3 o = muzzles[current].position - dir * 0.3f;
                return SelfRay(o, dir, out var h, 300f) ? h.point : o + dir * 300f;
            }
        }

        public static bool IsPistol(WeaponDef d) => d.name == "GLOCK 17" || d.name == "BERETTA M9" || d.name == "TASER";
        bool TwoHandedGun(WeaponDef d) => d.type == WeaponType.Gun && !IsPistol(d);

        // ------------------------------------------------------------ the gun in your body's hands
        //
        // The gun is a small rigid body of its own, simulated here: sprung toward where you are holding it (the
        // hip or the sights, relative to the eye) and tied to your body's two real hands, which are in turn pulled
        // onto its grip and handguard (DriveHands). Turning drags it behind on the arms and it swings back;
        // recoil throws it back and up; walking bobs it; a hand stopped by a wall or the floor drags it along. A
        // hurt arm is a weaker spring (it lags, sags and shakes), and one-handed there is nothing holding the
        // muzzle down. Shots and the crosshair go where its barrel points.

        bool BodyHeld => PlayerAvatar.Body != null && !PlayerAvatar.Body.physio.dead && gripPts.Count > 0;
        Vector3 tgtPos; Quaternion tgtRot = Quaternion.identity; float tgtScale = 1f;
        Vector3 gPos, gVel, gAng; Quaternion gRot = Quaternion.identity; bool gInit;
        readonly List<Vector3> gripPts = new List<Vector3>(), forePts = new List<Vector3>();
        readonly List<Renderer> floatHands = new List<Renderer>();
        readonly List<bool> floatHandIsGun = new List<bool>();     // the shooting hand (right) or the support hand (left)

        /// <summary>Which hand holds the grip (the right, or the left if the right is gone) and which supports.</summary>
        void Hands(out BodyPart grip, out BodyPart support, out float gripArm, out float supportArm)
        {
            var me = PlayerAvatar.Body; var pb = PlayerBody.I;
            float r = pb != null ? pb.armR : 1f, l = pb != null ? pb.armL : 1f;
            bool rOk = me.physio.ArmUsable(1) && me[PartId.HandR] != null && !me[PartId.HandR].severed;
            bool lOk = me.physio.ArmUsable(-1) && me[PartId.HandL] != null && !me[PartId.HandL].severed;
            grip = support = null; gripArm = supportArm = 0f;
            if (rOk) { grip = me[PartId.HandR]; gripArm = Mathf.Max(0.15f, r); if (lOk) { support = me[PartId.HandL]; supportArm = Mathf.Max(0.15f, l); } }
            else if (lOk) { grip = me[PartId.HandL]; gripArm = Mathf.Max(0.15f, l); }
            var d = Current;
            // only guns get the second hand (a pistol's wraps the first; blades and grenades are one-handed)
            if (d.type != WeaponType.Gun) { support = null; supportArm = 0f; }
        }

        /// <summary>Gun-space point the support hand holds: the foregrip if fitted, the handguard, or round the pistol grip.</summary>
        Vector3 SupportPoint(int i)
        {
            var d = defs[i]; var L = loadouts[i];
            if (IsPistol(d)) return gripPts[i] + new Vector3(-0.022f, -0.012f, 0.004f);
            if (L.grip != GripKind.None && GripsFor(d) && i < mountUnder.Count) return mountUnder[i] + new Vector3(0f, L.grip == GripKind.Vertical ? -0.05f : -0.03f, 0f);
            return forePts[i];
        }

        /// <summary>A barrel can't go through a wall: when it would, it is pulled back and tipped up out of the way.</summary>
        void Tuck(ref Vector3 tp, ref Quaternion tr)
        {
            if (current >= muzzles.Count) return;
            Vector3 muzzleLocal = muzzles[current].localPosition;
            Vector3 tip = tp + tr * (muzzleLocal + Vector3.forward * 0.04f);
            Vector3 from = cam.transform.position;
            Vector3 d = tip - from; float len = d.magnitude;
            if (len < 0.05f || !SelfRay(from, d / len, out var hit, len)) { tuck = Mathf.MoveTowards(tuck, 0f, Time.deltaTime * 3f); }
            else tuck = Mathf.MoveTowards(tuck, Mathf.Clamp01((len - hit.distance) / 0.45f), Time.deltaTime * 6f);
            if (tuck <= 0f) return;
            tp -= tr * Vector3.forward * (0.12f * tuck);
            tp += cam.transform.up * (0.04f * tuck);
            tr = Quaternion.AngleAxis(-55f * tuck, tr * Vector3.right) * tr;
        }
        float tuck;

        void HoldWithBody()
        {
            if (vmRoot.parent != null) vmRoot.SetParent(null, true);
            float dt = Mathf.Min(Time.deltaTime, 0.04f);
            Vector3 tp = cam.transform.TransformPoint(tgtPos);
            Quaternion tr = cam.transform.rotation * tgtRot;
            var d = Current;
            if (d.type == WeaponType.Gun) Tuck(ref tp, ref tr);
            if (!gInit || (gPos - tp).sqrMagnitude > 4f) { gPos = tp; gRot = tr; gVel = gAng = Vector3.zero; gInit = true; }
            Hands(out var gh, out var sh, out float ga, out float sa);
            var me = PlayerAvatar.Body;
            float awake = Mathf.Clamp01(me.physio.consciousness * 1.3f);
            bool melee = d.type != WeaponType.Gun;
            // how heavy it handles: a Barrett is a different thing to hold up than a Glock
            float heavy = melee ? 0.7f : Mathf.Clamp(d.recoil / 3f, 0.55f, 2.4f);
            float hold = (ga * 0.7f + sa * (TwoHandedGun(d) ? 0.6f : 0.25f)) * awake;
            if (gh == null) hold = 0f;
            float kp = (melee ? 900f : 425f) * Mathf.Max(0.05f, hold) / heavy;
            float kr = (melee ? 900f : 340f) * Mathf.Max(0.05f, TwoHandedGun(d) ? ga * 0.5f + sa * 0.8f : ga) * awake / heavy;
            // aiming down the sights: cheek welded to the stock, the sights held on the eye. The hold goes much
            // stiffer, stops being dragged about by where the arms are, and only the shakes are left
            float ads = melee ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Max(aimT, pointT * 0.6f));
            kp *= 1f + 5f * ads; kr *= 1f + 6f * ads;
            float dp = 2f * Mathf.Sqrt(kp) * 0.8f, dr = 2f * Mathf.Sqrt(kr) * 0.85f;
            if (dt > 0f)
            {
                Vector3 acc = (tp - gPos) * kp - gVel * dp;
                (tr * Quaternion.Inverse(gRot)).ToAngleAxis(out float ang, out Vector3 ax);
                if (ang > 180f) ang -= 360f;
                if (!float.IsFinite(ax.x)) ax = Vector3.zero;
                Vector3 angAcc = ax * (ang * Mathf.Deg2Rad) * kr - gAng * dr;
                // weak arms can't hold the weight up: it sags and the muzzle droops
                float weak = (1f - Mathf.Clamp01(hold)) * (1f - 0.85f * ads);
                acc += Vector3.down * 9.81f * weak * 0.35f * heavy;
                angAcc += (gRot * Vector3.right) * (weak * heavy * 6f);
                // tied to where the arms really are (an arm stopped by a wall, the floor, a hit, drags it)
                GripHands(out var gunHand, out var supHand);
                float kc = kp * 0.3f * (1f - ads);
                if (kc > 0f)
                {
                    if (gh != null && gh.id == PartId.HandR && gunHand != null) Couple(gh, gunHand, kc, ref acc, ref angAcc);
                    if (sh != null && supHand != null) Couple(sh, supHand, kc * 0.8f, ref acc, ref angAcc);
                }
                // shaking: pain, blood loss, damaged arms, the brain
                float tremor = ((PlayerBody.I != null ? PlayerBody.I.Tremor : 0f) + (1f - ga) * 0.4f) * (1f - 0.5f * ads);
                float t = Time.time;
                acc += new Vector3(Mathf.PerlinNoise(t * 7f, 3f) - 0.5f, Mathf.PerlinNoise(5f, t * 7f) - 0.5f, 0f) * tremor * 34f;
                angAcc += new Vector3(Mathf.PerlinNoise(t * 6f, 11f) - 0.5f, Mathf.PerlinNoise(13f, t * 6f) - 0.5f, 0f) * tremor * 25f;
                gVel += acc * dt; gPos += gVel * dt;
                gAng += angAcc * dt;
                float w = gAng.magnitude;
                if (w > 1e-5f) gRot = Quaternion.AngleAxis(w * Mathf.Rad2Deg * dt, gAng / w) * gRot;
                gRot.Normalize();
            }
            vmRoot.SetPositionAndRotation(gPos, gRot);
            vmRoot.localScale = Vector3.one * tgtScale;
        }

        /// <summary>The gun is pulled toward where the body's wrist actually is (relative to where the grip wants it).</summary>
        void Couple(BodyPart hand, Transform grip, float kc, ref Vector3 acc, ref Vector3 angAcc)
        {
            if (!wristOf.TryGetValue(grip, out var w)) return;
            Vector3 local = vmRoot.InverseTransformPoint(grip.TransformPoint(w.wrist));
            Vector3 p = gPos + gRot * Vector3.Scale(local, vmRoot.localScale);
            var hj = hand.GetComponent<ConfigurableJoint>();
            Vector3 wrist = hj != null ? hand.transform.TransformPoint(hj.anchor) : hand.rb.position;
            Vector3 e = Vector3.ClampMagnitude(wrist - p, 0.2f);
            acc += e * kc;
            angAcc += Vector3.Cross(p - gPos, e) * kc * 5f;
        }

        void LateUpdate()
        {
            if (cam == null || vmRoot == null) return;
            bool held = BodyHeld;
            // the gripping hands show unless the arm they belong to is gone or useless
            bool rOk = true, lOk = true;
            if (held) { var me = PlayerAvatar.Body; rOk = me.physio.ArmUsable(1) && me[PartId.HandR] != null && !me[PartId.HandR].severed; lOk = me.physio.ArmUsable(-1) && me[PartId.HandL] != null && !me[PartId.HandL].severed; }
            for (int i = 0; i < floatHands.Count; i++)
                if (floatHands[i]) floatHands[i].enabled = floatHandIsGun[i] ? rOk : lOk;
            if (!held) { gInit = false; UpdateRailGear(); return; }
            if (!Armed) { UpdateRailGear(); return; }
            HoldWithBody();
            UpdateRailGear();
            if (flash.activeSelf && current < muzzles.Count) { flash.transform.position = muzzles[current].position; flash.transform.rotation = muzzles[current].rotation; }
            muzzleLight.transform.position = muzzles[current].position;
        }

        // ---- gripping hands
        // The sculpted hands that grip each gun are cut off at the wrist. They sit locked on the grip and the
        // handguard (they are part of the gun), and your real forearms are driven so their wrists meet them and
        // they run the same way out of them: the arms still swing, lag and get shoved, but the hands never leave
        // the gun. Your body's own (mitten) hands are hidden from your camera while you hold something.
        readonly Dictionary<Transform, (Vector3 wrist, Vector3 axis)> wristOf = new Dictionary<Transform, (Vector3, Vector3)>();

        static Mesh TrimAtWrist(Mesh src, float cut, out Vector3 wrist, out Vector3 axis)
        {
            var v = src.vertices;
            // the forearm runs out of the hand to the mesh's farthest point
            Vector3 far = Vector3.forward; float fd = 0f;
            foreach (var p in v) if (p.sqrMagnitude > fd) { fd = p.sqrMagnitude; far = p; }
            axis = far.normalized;
            wrist = axis * cut;
            var m = new Mesh { indexFormat = src.indexFormat };
            m.vertices = v; m.normals = src.normals;
            if (src.uv != null && src.uv.Length == v.Length) m.uv = src.uv;
            m.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s);
                var keep = new List<int>(tri.Length);
                for (int i = 0; i < tri.Length; i += 3)
                    if (Vector3.Dot(v[tri[i]], axis) < cut && Vector3.Dot(v[tri[i + 1]], axis) < cut && Vector3.Dot(v[tri[i + 2]], axis) < cut)
                    { keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]); }
                m.SetTriangles(keep, s);
            }
            m.RecalculateBounds();
            return m;
        }

        void TrimHands()
        {
            var cache = new Dictionary<Mesh, (Mesh m, Vector3 w, Vector3 a)>();
            foreach (var t in vmRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!(t.name.StartsWith("View_Hand") || t.name == "SupportHand" || t.name == "ForegripHand")) continue;
                var mf = t.GetComponentInChildren<MeshFilter>(true);
                if (mf == null || mf.sharedMesh == null) continue;
                if (!cache.TryGetValue(mf.sharedMesh, out var c))
                {
                    var m = TrimAtWrist(mf.sharedMesh, t.name == "SupportHand" && mf.sharedMesh.vertexCount < 7000 ? 0.1f : 0.09f, out var w, out var a);
                    cache[mf.sharedMesh] = c = (m, w, a);
                }
                mf.sharedMesh = c.m;
                // the wrist and forearm direction in the hand object's own space
                wristOf[t] = (t.InverseTransformPoint(mf.transform.TransformPoint(c.w)), t.InverseTransformDirection(mf.transform.TransformDirection(c.a)).normalized);
            }
        }

        /// <summary>The gripping hand objects on the current gun: the shooting hand and the support hand (null if none showing).</summary>
        void GripHands(out Transform gunHand, out Transform supHand)
        {
            gunHand = supHand = null;
            if (current >= models.Count) return;
            foreach (Transform t in models[current].transform)
            {
                if (t.name.StartsWith("View_Hand")) gunHand = t;
                else if ((t.name == "SupportHand" || t.name == "ForegripHand") && t.gameObject.activeSelf) supHand = t;
            }
        }

        void DriveHands()
        {
            if (!BodyHeld || !Armed || !gInit || current >= gripPts.Count) return;
            var me = PlayerAvatar.Body;
            Hands(out var gh, out var sh, out float ga, out float sa);
            GripHands(out var gunHand, out var supHand);
            float awake = Mathf.Clamp01(me.physio.consciousness * 1.3f);
            // getting up off the floor takes your hands (they push on the ground); the gun just comes along
            var rag = me.ragdoll;
            if (rag.mode == BodyMode.GettingUp || (rag.IsDown && rag.wantStand)) return;
            if (gh != null && gh.id == PartId.HandR && gunHand != null) PullArm(gh, gunHand, ga * awake);
            if (sh != null && supHand != null) PullArm(sh, supHand, sa * awake);
        }

        /// <summary>The body's wrist onto the gripping hand's wrist, the forearm laid along it.</summary>
        void PullArm(BodyPart hand, Transform grip, float strength)
        {
            if (hand == null || hand.rb == null || strength <= 0f || !wristOf.TryGetValue(grip, out var w)) return;
            var me = PlayerAvatar.Body;
            var chest = me.Chest != null ? me.Chest.rb : null;
            float dt = Time.fixedDeltaTime;
            Vector3 target = grip.TransformPoint(w.wrist);
            Vector3 along = grip.TransformDirection(w.axis);
            var hj = hand.GetComponent<ConfigurableJoint>();
            Vector3 wrist = hj != null ? hand.transform.TransformPoint(hj.anchor) : hand.rb.position;
            float k = Mathf.Lerp(0.3f, 1f, strength);
            // velocity drive: get there in a couple of steps, moving with the gun
            Vector3 dv = Vector3.ClampMagnitude((target - wrist) / dt * 0.45f + gVel - hand.rb.linearVelocity, 5f * k);
            hand.rb.AddForce(dv, ForceMode.VelocityChange);
            if (chest != null) chest.AddForce(-dv * hand.rb.mass, ForceMode.Impulse);
            // the forearm: its elbow end pulled out along the gripping hand's forearm direction (gently: the
            // shoulder decides how far it can go)
            var fore = me[hand.id == PartId.HandR ? PartId.ForearmR : PartId.ForearmL];
            if (fore != null && fore.rb != null && !fore.severed)
            {
                var fj = fore.GetComponent<ConfigurableJoint>();
                Vector3 elbow = fj != null ? fore.transform.TransformPoint(fj.anchor) : fore.rb.position;
                float len = Vector3.Distance(elbow, wrist);
                Vector3 elbowGoal = target + along * Mathf.Max(0.2f, len);
                Vector3 dvf = Vector3.ClampMagnitude((elbowGoal - elbow) / dt * 0.12f + gVel * 0.5f - fore.rb.linearVelocity * 0.5f, 2f * k);
                fore.rb.AddForceAtPosition(dvf * fore.rb.mass, elbow, ForceMode.Impulse);
                if (chest != null) chest.AddForce(-dvf * fore.rb.mass, ForceMode.Impulse);
            }
        }

        /// <summary>
        /// How much worse a gun handles in one hand: a rifle or shotgun held out one-handed kicks and wanders far
        /// more than a pistol does.
        /// </summary>
        float OneHandK(WeaponDef d)
        {
            int arms = PlayerBody.I != null ? PlayerBody.I.UsableArms : 2;
            if (arms >= 2) return 1f;
            return IsPistol(d) ? 1.5f : 2.4f;
        }

        /// <summary>
        /// The gun is held by arms, not bolted to the camera: turning drags it behind on a spring and it swings
        /// back, recoil throws it about, and hurt or one-handed arms make that spring weaker and shakier.
        /// </summary>
        void ArmSway(float dt, ref Vector3 pos, ref Quaternion rot)
        {
            var d = Current;
            float oneK = OneHandK(d);
            float heavy = d.type == WeaponType.Gun ? Mathf.Clamp(d.recoil / 3f, 0.6f, 2f) : 0.8f;
            Vector2 md = Mouse.current != null && Cursor.lockState == CursorLockMode.Locked && !swingFree ? Mouse.current.delta.ReadValue() : Vector2.zero;
            swayVel -= md * 0.00035f * heavy * oneK;
            float k = 90f / (heavy * oneK);
            swayVel += -swayPos * k * dt - swayVel * Mathf.Min(1f, 10f * dt);
            swayPos += swayVel * dt;
            swayPos = Vector2.ClampMagnitude(swayPos, 0.12f);
            float tremor = PlayerBody.I != null ? PlayerBody.I.Tremor : 0f;
            float t = Time.unscaledTime;
            Vector2 shakeV = new Vector2(Mathf.PerlinNoise(t * 9f, 1f) - 0.5f, Mathf.PerlinNoise(2f, t * 9f) - 0.5f) * tremor * 0.025f * oneK;
            Vector2 s = swayPos * (1f - aimT * 0.6f) + shakeV;
            // the muzzle follows the sway: shots and the crosshair go where it points (less when aimed carefully)
            aimSwayDeg = s * Mathf.Lerp(28f, 14f, aimT);
            pos += new Vector3(s.x, s.y, 0f);
            rot *= Quaternion.Euler(-s.y * 120f, s.x * 120f, -s.x * 80f);
            // one hand: the gun is held out, canted, lower
            if (oneK > 1f && d.type == WeaponType.Gun) { pos += new Vector3(0.05f, -0.03f, 0.02f) * (1f - aimT * 0.5f); rot *= Quaternion.Euler(0f, 0f, 22f); }
        }

        void StartReload()
        {
            reloading = true;
            if (Current.name == "TASER" && TaserWires.Current != null) Destroy(TaserWires.Current.gameObject);   // spent cartridge off, wires and all
            reloadEnd = Time.unscaledTime + Current.reloadTime;
            Sfx.Reload(Current.name, cam.transform.position, Current.reloadTime);
        }

        /// <summary>Taser: the cartridge fires both probes along the barrel line (the lower one 8 degrees down).</summary>
        void FireTaser(WeaponDef d)
        {
            var root = models[current].transform;
            Transform Bay(string n, Vector3 fallback)
            {
                var t = root.Find(n);
                if (t != null) return t;
                var m = ModelLibrary.Get("Gun_Taser");
                t = new GameObject(n).transform; t.SetParent(root, false);
                t.localPosition = m != null && m.points.TryGetValue(n == "Bay1" ? "Probe1" : "Probe2", out var p) ? p : fallback;
                return t;
            }
            var b1 = Bay("Bay1", new Vector3(0f, 0.025f, 0.085f));
            var b2 = Bay("Bay2", new Vector3(0f, -0.001f, 0.085f));
            Vector3 f = AimDirection;
            Vector3 right = Vector3.Cross(Vector3.up, f).normalized;
            if (right.sqrMagnitude < 0.01f) right = cam.transform.right;
            float spread = Random.Range(0f, 0.6f);
            Vector3 d1 = Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * f;
            Vector3 d2 = Quaternion.AngleAxis(8f, right) * Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * f;
            TaserWires.Fire(b1, b2, d1, d2, PlayerAvatar.Body);
            Sfx.Gun(d.name, b1.position);
            HumanBrain.BroadcastNoise(b1.position, d.loudness, 0.2f);
            gVel += -(gRot * Vector3.forward) * 0.15f;
            kick = Mathf.Min(1.5f, kick + 0.3f);
        }

        void FireGun(WeaponDef d)
        {
            nextFire = Time.unscaledTime + 60f / Rpm(current);
            if (d.name == "TASER")
            {
                // wires still in him: the trigger sends another five seconds instead of needing a fresh cartridge
                var w = TaserWires.Current;
                if (ammo[current] <= 0 && w != null && w.Arrived) { w.Cycle(); return; }
                if (!GameSettings.InfiniteAmmo) ammo[current]--;
                FireTaser(d);
                return;
            }
            if (!GameSettings.InfiniteAmmo) ammo[current]--;
            var am = CurrentAmmo;
            int pellets = am != null ? am.pellets : d.pellets;
            // shot keeps its spread when aiming; single rounds tighten up
            float baseSpread = am != null && am.pellets > 1 ? am.spread : (am != null && am.spread > 0f ? am.spread : d.spread);
            float spread = baseSpread * Mathf.Lerp(1f, pellets > 1 ? 0.85f : 0.25f, Mathf.Max(aimT, pointT * 0.55f));
            // one hand on a long gun, shaking hands, a weak arm: everything goes wider
            float oneK = OneHandK(d);
            spread = spread * oneK + (PlayerBody.I != null ? PlayerBody.I.Tremor * 1.2f : 0f);
            Vector3 origin = cam.transform.position;
            Vector3 dir = cam.transform.forward;
            Vector3 aimF = AimDirection;
            bool held = BodyHeld && gInit;
            // in your hands the round leaves the barrel, wherever the barrel is (lying down, round a corner)
            if (held) origin = muzzles[current].position - aimF * 0.12f;
            Ballistics.FiringBody = PlayerAvatar.Body;       // your own rounds (and their ricochets) never hit you
            for (int p = 0; p < pellets; p++)
            {
                // pellets spread evenly over the pattern (sqrt keeps them from bunching in the middle)
                float ang = (pellets > 1 ? Mathf.Sqrt(Random.value) : Random.value) * spread;
                dir = Quaternion.AngleAxis(Random.Range(0f, 360f), aimF) * Quaternion.AngleAxis(ang, cam.transform.right) * aimF;
                if (am != null)
                    Ballistics.Instance.Fire(origin, dir, am.velocity * Random.Range(0.97f, 1.03f), am.mass, am.diameter, HitKind.Bullet, am.tracer && p == 0, muzzles[current].position, am);
                else
                    Ballistics.Instance.Fire(origin, dir, d.muzzleVelocity * Random.Range(0.97f, 1.03f), d.bulletMass, d.diameter, HitKind.Bullet, d.tracer && p == 0, muzzles[current].position);
            }
            Ballistics.FiringBody = null;

            float gripK = Loadout.grip == GripKind.Vertical ? 0.7f : Loadout.grip == GripKind.Angled ? 0.82f : 1f;
            float ammoK = am != null ? Mathf.Clamp(Mathf.Sqrt(am.Energy / Mathf.Max(1f, d.Energy)), 0.5f, 1.4f) : 1f;
            float camK = held ? 0.45f : 1f;    // held, most of the kick goes into the gun and your arms instead
            pc.AddRecoil(camK * d.recoil * ammoK * gripK * oneK * Random.Range(0.8f, 1.2f) * Mathf.Lerp(1f, 0.6f, aimT), camK * Random.Range(-0.35f, 0.35f) * d.recoil * 0.5f * gripK * oneK * oneK);
            if (held)
            {
                // the gun is thrown back into the hands and the muzzle flips up (more so one-handed or hurt)
                float r = d.recoil * ammoK * gripK;
                gVel += (-(gRot * Vector3.forward) * 0.55f + Vector3.up * 0.12f) * r * Mathf.Sqrt(oneK);
                gAng += (gRot * Vector3.right) * (-r * 2.4f * oneK) + (gRot * Vector3.up) * (Random.Range(-1f, 1f) * r * 0.7f * oneK);
                Hands(out var gh, out var sh, out _, out _);
                if (gh != null) gh.rb.AddForce(-(gRot * Vector3.forward) * r * 0.5f, ForceMode.Impulse);
                if (sh != null) sh.rb.AddForce(Vector3.up * r * 0.25f, ForceMode.Impulse);
            }
            swayVel += new Vector2(Random.Range(-1f, 1f), 1.5f) * d.recoil * 0.02f * oneK;
            if (d.recoil > 4f) pc.Shake(0.15f);
            kick = Mathf.Min(1.5f, kick + 0.6f + d.recoil * 0.08f);
            bool supp = Loadout.suppressor && SuppressorFor(d);
            flashTimer = supp ? 0f : 0.045f;   // the can swallows the flash
            // a fresh flash shape every shot
            float seed = Random.value * 100f;
            for (int fi = 0; fi < flashParts.Count; fi++) { var fr = flashParts[fi]; fr.sharedMaterial.SetFloat("_Seed", seed + fi * 3.7f); fr.sharedMaterial.SetFloat("_Intensity", Random.Range(0.8f, 1.25f)); }
            Fx.Muzzle(muzzles[current].position, AimDirection, Mathf.Clamp(Mathf.Sqrt((am != null ? am.Energy * am.pellets : d.Energy) / 2000f), 0.6f, 2.2f) * (supp ? 0.25f : 1f));
            if (am != null && am.incendiary) Fx.DragonsBreath(muzzles[current].position, cam.transform.forward);
            HumanBrain.BroadcastNoise(origin, d.loudness * (supp ? 0.3f : 1f), 0.35f);
            Sfx.Gun(d.name, muzzles[current].position, supp);

            // near misses frighten people the bullet passes close to
            foreach (var h in HumanBody.All)
            {
                if (h.Dead) continue;
                Vector3 to = h.Center - origin;
                float along = Vector3.Dot(to, dir);
                if (along < 0f) continue;
                float miss = Vector3.Cross(dir, to).magnitude;
                if (miss < 2.5f) { h.brain.fear = Mathf.Clamp01(h.brain.fear + 0.3f * (1f - miss / 2.5f)); h.brain.lastThreatTime = Time.time; h.brain.threatPos = origin; }
            }
        }

        void UpdateMelee(float dt)
        {
            if (meleeMode == 0) return;
            meleeT += dt;
            float hitTime = meleeMode == 1 ? 0.08f : 0.1f;
            if (!meleeHitDone && meleeT >= hitTime)
            {
                meleeHitDone = true;
                if (Current.bulletMass > 0.5f) HeavySwing(meleeMode == 3);
                else if (meleeMode == 1) Stab(); else Slash();
            }
            if (meleeT > (meleeMode == 1 ? 0.35f : meleeMode == 3 ? 0.6f : 0.45f)) meleeMode = 0;
        }

        /// <summary>A full swing of the hammer or sword: overhead (down) or across. Lands on what is in front of you.</summary>
        void HeavySwing(bool overhead)
        {
            Vector3 o = cam.transform.position;
            for (int i = 0; i < 9; i++)
            {
                // sweep an arc in front of you, a few rays deep
                float f = i / 8f;
                Vector3 a = o + Quaternion.AngleAxis(Mathf.Lerp(-35f, 35f, f), overhead ? cam.transform.right : cam.transform.up) * cam.transform.forward * 0.25f;
                Vector3 b = o + Quaternion.AngleAxis(Mathf.Lerp(-35f, 35f, f), overhead ? cam.transform.right : cam.transform.up) * cam.transform.forward * 2.1f;
                if (SweepHit(a, b, a, b, 11f, Current.bulletMass) != 0) return;
            }
        }

        void Stab()
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var arm = ArmourSystem.Raycast(ray.origin, ray.direction, 2f, out float ad, out _, null, PlayerAvatar.Body);
            if (arm != null && (!SelfRay(ray.origin, ray.direction, out var h0, 2f) || ad <= h0.distance + 0.14f))
            {
                // the blade skids off armour (soft armour only stops some stabs)
                if (arm.steel || arm.cls > 2.5f || Random.value < 0.6f)
                {
                    Fx.Sparks(ray.origin + ray.direction * ad, -ray.direction, arm.steel ? 10 : 2);
                    arm.AddWear(arm.transform.InverseTransformPoint(ray.origin + ray.direction * ad), 0.1f);
                    return;
                }
            }
            if (SelfRay(ray.origin, ray.direction, out var hit, 2f) && !(hit.collider is CharacterController))
            {
                var bp = hit.collider.GetComponent<BodyPart>();
                if (bp != null) bp.body.damage.ApplyStab(bp, hit.point, ray.direction, Random.Range(0.12f, 0.18f));
                else { Fx.Sparks(hit.point, hit.normal, 4); BloodSystem.Instance?.Mark(hit.point, hit.normal, 0.03f); }
                HumanBrain.BroadcastNoise(hit.point, 8f, 0.4f);
            }
        }

        void Slash()
        {
            for (int i = 0; i < 7; i++)
            {
                int k = (i + 1) / 2 * (i % 2 == 0 ? 1 : -1);   // 0, -1, +1, -2, +2 ...
                Vector3 dir = Quaternion.AngleAxis(k * 9f, cam.transform.up) * cam.transform.forward;
                if (SelfRay(cam.transform.position, dir, out var hit, 1.9f) && !(hit.collider is CharacterController))
                {
                    var bp = hit.collider.GetComponent<BodyPart>();
                    if (bp != null)
                    {
                        if (ArmourSystem.Raycast(cam.transform.position, dir, hit.distance + 0.14f, out float sad, out _, null, PlayerAvatar.Body) != null)
                        {
                            Fx.Sparks(cam.transform.position + dir * sad, -dir, 8);
                            return;
                        }
                        bp.body.damage.ApplySlash(bp, hit.point, hit.normal, -cam.transform.right);
                        HumanBrain.BroadcastNoise(hit.point, 8f, 0.4f);
                        return;
                    }
                }
            }
        }

        // ------------------------------------------------------------ physics grab

        void UpdateGrab(Keyboard kb, Mouse mouse, bool locked, float scroll)
        {
            // the physics gun in your hands: hold the left button to grab, let go to drop, right button to throw
            bool phys = Armed && Current.type == WeaponType.Tool;
            if (locked && phys && mouse.leftButton.wasPressedThisFrame)
            {
                // grab a helmet visor: flip it up or down
                var vp = ArmourSystem.Raycast(cam.transform.position, cam.transform.forward, 3f, out _, out _, null, PlayerAvatar.Body);
                if (vp != null && !vp.visor && vp.transform.parent != null) { var par = vp.transform.parent.GetComponent<ArmourPiece>(); if (par != null) vp = par; }
                if (vp != null && vp.visor) { vp.ToggleVisor(); return; }
                bool got = SelfRay(cam.transform.position, cam.transform.forward, out var hit, 12f) && hit.rigidbody != null && !hit.rigidbody.isKinematic;
                // the beam is forgiving: anything loose within a hand's width of the crosshair
                if (!got)
                {
                    float best = float.MaxValue;
                    foreach (var h in Physics.SphereCastAll(cam.transform.position, 0.2f, cam.transform.forward, 12f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.rigidbody == null || h.rigidbody.isKinematic || h.collider is CharacterController || h.distance <= 0f) continue;
                        if (PlayerAvatar.Body != null && h.collider.GetComponentInParent<HumanBody>() == PlayerAvatar.Body) continue;
                        if (h.distance < best) { best = h.distance; hit = h; got = true; }
                    }
                }
                if (got)
                {
                    grabbed = hit.rigidbody;
                    grabLocal = grabbed.transform.InverseTransformPoint(hit.point);
                    grabDist = hit.distance;
                    var bp = grabbed.GetComponent<BodyPart>();
                    if (bp != null && bp.body != null) bp.body.ragdoll.Fall();
                }
            }
            // the beam held on a wall or floor up close (nothing to grab): smear the blood about on it
            if (locked && phys && grabbed == null && mouse.leftButton.isPressed && BloodSystem.Instance != null)
            {
                if (SelfRay(cam.transform.position, cam.transform.forward, out var sh, 1.8f) && sh.rigidbody == null && !(sh.collider is CharacterController))
                {
                    if (smudging)
                    {
                        float moved = Vector3.Distance(smudgeLast, sh.point);
                        if (moved > 0.01f && moved < 0.5f)
                        {
                            smudgeCarry += BloodSystem.Instance.Wipe(sh.point, sh.normal, 0.04f, 0.6f) * 2f;
                            float there = BloodSystem.Instance.BloodOnSurface(sh.point, sh.normal);
                            smudgeCarry = Mathf.Min(1.5f, smudgeCarry + there * 0.4f);
                            BloodSystem.Instance.SmearSegment(smudgeLast, sh.point, sh.normal, 0.07f, smudgeCarry, Random.value * 97f);
                            smudgeCarry *= Mathf.Clamp01(1f - moved * 1.4f);
                        }
                    }
                    smudging = true; smudgeLast = sh.point;
                }
                else smudging = false;
            }
            else { smudging = false; smudgeCarry = 0f; }

            if (grabbed != null && (!mouse.leftButton.isPressed || !locked || !phys))
            {
                grabbed = null;
            }
            if (grabbed != null)
            {
                grabDist = Mathf.Clamp(grabDist + scroll * 0.002f, 1f, 10f);
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    grabbed.AddForce(cam.transform.forward * 18f, ForceMode.VelocityChange);
                    grabbed = null;
                }
            }
            grabLine.enabled = grabbed != null;
        }

        void FixedUpdate()
        {
            DriveHands();
            if (grabbed == null) return;
            Vector3 p = grabbed.transform.TransformPoint(grabLocal);
            // held at arm's length: it lags and swings with your arms (and a weak arm holds it worse)
            Vector3 target = cam.transform.position + AimDirection * grabDist;
            Vector3 v = grabbed.GetPointVelocity(p);
            Vector3 f = (target - p) * 220f - v * 22f;
            float armK = PlayerBody.I != null ? Mathf.Lerp(0.25f, 1f, Mathf.Max(PlayerBody.I.armL, PlayerBody.I.armR)) : 1f;
            grabbed.AddForceAtPosition(Vector3.ClampMagnitude(f, 900f * armK) * Mathf.Min(grabbed.mass, 20f), p);
            grabLine.SetPosition(0, muzzles[current].position);
            grabLine.SetPosition(1, p);
        }

        // ------------------------------------------------------------ animation

        void Animate(float dt)
        {
            var d = Current;
            kick = Mathf.MoveTowards(kick, 0f, dt * 6f);
            Vector3 hip = new Vector3(0.17f, -0.17f, 0.36f);
            // aiming puts the sight line (iron sights, the optic's centre or the scope's axis) right on the eye
            bool held = BodyHeld;
            // held by your body the gun is its real size, where real arms put it; without one, the old shrunken
            // viewmodel close to the camera
            float VmScale = held ? 1f : 0.62f;
            bool optic = d.scope || Loadout.sight != SightKind.Iron;
            Vector3 aim = new Vector3(0f, -SightHeight() * VmScale, optic ? 0.2f : 0.24f);
            if (d.type == WeaponType.Gun && IsPistol(d)) { hip = new Vector3(0.17f, -0.17f, 0.38f); aim.z = 0.32f; }
            if (d.scope) { hip = new Vector3(0.25f, -0.25f, 0.3f); aim.z = 0.17f; }
            if (d.type != WeaponType.Gun) aim = new Vector3(0f, -0.075f, 0.24f);
            if (held)
            {
                // cheek on the stock with the eye behind the sights; a pistol pushed out at arm's length
                if (IsPistol(d)) { hip = new Vector3(0.1f, -0.22f, 0.36f); aim.z = 0.42f; }
                else if (d.scope) { hip = new Vector3(0.13f, -0.22f, 0.22f); aim.z = 0.15f; }
                else if (d.type == WeaponType.Gun) { hip = new Vector3(0.08f, -0.2f, 0.2f); aim.z = 0.19f; }
                else { hip = new Vector3(0.16f, -0.24f, 0.36f); aim = hip; }
            }
            Vector3 pos = Vector3.Lerp(hip, aim, Mathf.SmoothStep(0, 1, aimT));
            Quaternion pointRot = Quaternion.identity;
            if (pointT > 0f && d.type == WeaponType.Gun)
            {
                // point aim: brought up near the eye and canted over, looking along the top of the gun rather than
                // through its sights - fast and steadier than the hip, not as exact as the sights
                float k = Mathf.SmoothStep(0f, 1f, pointT) * (1f - aimT);
                Vector3 point = IsPistol(d) ? new Vector3(0.04f, -0.1f, 0.38f) : new Vector3(0.05f, -0.11f, aim.z + 0.03f);
                pos = Vector3.Lerp(pos, point, k);
                pointRot = Quaternion.Euler(-2f * k, 0f, -35f * k);
            }
            Quaternion rot = pointRot;
            pos.z -= kick * 0.05f;
            pos.y += kick * 0.01f;
            rot *= Quaternion.Euler(-kick * 6f, 0, 0);
            if (reloading)
            {
                float t = 1f - (reloadEnd - Time.unscaledTime) / d.reloadTime;
                float dip = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                pos.y -= dip * 0.12f;
                rot *= Quaternion.Euler(dip * 25f, 0, dip * 20f);
            }
            if (meleeMode == 1)
            {
                float t = Mathf.Sin(Mathf.Clamp01(meleeT / 0.3f) * Mathf.PI);
                pos += new Vector3(-0.1f * t, 0.05f * t, 0.25f * t);
            }
            else if (meleeMode == 2)
            {
                float t = Mathf.Clamp01(meleeT / 0.35f);
                pos += new Vector3(Mathf.Lerp(0.15f, -0.35f, t), 0.05f * Mathf.Sin(t * Mathf.PI), 0.1f);
                rot *= Quaternion.Euler(0, Mathf.Lerp(40f, -50f, t), Mathf.Lerp(-30f, 30f, t));
            }
            else if (meleeMode == 3)
            {
                // overhead: raised, then brought down hard
                float t = Mathf.Clamp01(meleeT / 0.45f);
                float up = t < 0.4f ? t / 0.4f : 1f - (t - 0.4f) / 0.6f;
                pos += new Vector3(0f, 0.12f * up - 0.08f * (1f - up) * (t > 0.4f ? 1f : 0f), 0.08f);
                rot *= Quaternion.Euler(Mathf.Lerp(-70f, 60f, Mathf.SmoothStep(0.3f, 0.7f, t)), 0f, 0f);
            }
            // sway with movement
            float bob = Mathf.Sin(Time.unscaledTime * 8f) * 0.004f * (1f - aimT);
            pos.y += bob;
            // with a body the arms do the swaying (HoldWithBody); without one, the spring on the camera
            if (!held) ArmSway(dt, ref pos, ref rot);
            if (swingFree || swingPos.sqrMagnitude > 0.0004f)
            {
                // free swing: the weapon goes where the mouse pushes it, the blade leading
                // across the whole screen and from low to high, the blade leading the hand; a thrust shoots it out
                float thrust = Mathf.Sin(Mathf.Clamp01(thrustT / 0.25f) * Mathf.PI);
                pos += new Vector3(swingPos.x * 0.36f, swingPos.y * 0.24f - 0.03f, 0.1f + 0.06f * Mathf.Abs(swingPos.x) + thrust * 0.3f);
                rot *= Quaternion.Euler(-swingPos.y * 70f - 25f + thrust * 60f, swingPos.x * 70f, -swingPos.x * 35f);
            }
            if (suicideT > 0f)
            {
                // turned round, muzzle up under the chin
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(suicideT / 0.9f));
                pos = Vector3.Lerp(pos, new Vector3(0.03f, -0.2f, 0.13f), t);
                rot = Quaternion.Slerp(rot, Quaternion.Euler(-60f, 175f, 0f), t);
            }
            tgtPos = pos; tgtRot = rot; tgtScale = VmScale;
            if (!held)
            {
                if (vmRoot.parent != cam.transform) vmRoot.SetParent(cam.transform, false);
                vmRoot.localPosition = pos;
                vmRoot.localScale = Vector3.one * VmScale;
                vmRoot.localRotation = rot;
            }
            vmRoot.gameObject.SetActive(!Scoped);
            // the projected reticle is only seen looking through the glass
            foreach (var r in reticles) if (r) r.enabled = aimT > 0.75f;

            flashTimer -= dt;
            bool showFlash = flashTimer > 0f && d.type == WeaponType.Gun;
            flash.SetActive(showFlash && !Scoped);
            if (showFlash)
            {
                flash.transform.position = muzzles[current].position;
                flash.transform.rotation = muzzles[current].rotation;
                flash.transform.localScale = Vector3.one * Random.Range(0.8f, 1.3f) * (d.recoil > 4f ? 1.8f : 1f) * 0.62f / Mathf.Max(0.1f, vmRoot.localScale.x);
            }
            muzzleLight.transform.position = muzzles[current].position;
            muzzleLight.intensity = showFlash ? 3f : 0f;
        }
    }
}
