using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    public enum WeaponType { Gun, Knife, Grenade, Tool, Medical }

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
    public partial class WeaponController : MonoBehaviour
    {
        public readonly List<WeaponDef> defs = new List<WeaponDef>
        {
            new WeaponDef { name = "AK-47", type = WeaponType.Gun, auto = true, rpm = 600, muzzleVelocity = 715, bulletMass = 0.0079f, diameter = 0.0078f, magazine = 30, reloadTime = 2.4f, recoil = 1.5f, spread = 0.35f, zoomFov = 55, loudness = 75, tracer = true },
            new WeaponDef { name = "GLOCK 17", type = WeaponType.Gun, auto = false, rpm = 500, muzzleVelocity = 375, bulletMass = 0.0075f, diameter = 0.009f, magazine = 17, reloadTime = 1.5f, recoil = 1.1f, spread = 0.5f, zoomFov = 62, loudness = 50, tracer = false },
            new WeaponDef { name = "AWP", type = WeaponType.Gun, auto = false, rpm = 42, muzzleVelocity = 900, bulletMass = 0.0162f, diameter = 0.0086f, magazine = 5, reloadTime = 3.2f, recoil = 4.5f, spread = 0.02f, zoomFov = 11, scope = true, loudness = 95, tracer = true },
            new WeaponDef { name = ".50 CAL", type = WeaponType.Gun, auto = false, rpm = 110, muzzleVelocity = 890, bulletMass = 0.0421f, diameter = 0.0127f, magazine = 10, reloadTime = 3.6f, recoil = 7f, spread = 0.04f, zoomFov = 9, scope = true, loudness = 120, tracer = true },
            // 12-gauge pump, 00 buckshot: nine 8.4 mm pellets per shell
            new WeaponDef { name = "MOSSBERG 590", type = WeaponType.Gun, auto = false, rpm = 70, muzzleVelocity = 400, bulletMass = 0.0035f, diameter = 0.0084f, magazine = 6, reloadTime = 3.2f, recoil = 8.5f, spread = 2.4f, zoomFov = 55, loudness = 100, tracer = false, pellets = 9 },
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
            // medical: hold it and click to treat one of your wounds (MedKit)
            new WeaponDef { name = "BANDAGE", type = WeaponType.Medical },
            new WeaponDef { name = "SURGERY KIT", type = WeaponType.Medical },
            // the radio: hold it and click to call in air support (Radio) - carried in the fist like the kits
            new WeaponDef { name = "RADIO", type = WeaponType.Medical },
            // a backpack-tank flamethrower: hold the trigger and it throws burning fuel ~5 m; a full tank is 30 s of
            // flame (the "magazine" is tenths of a second of it), R swaps the tank (Flamethrower)
            new WeaponDef { name = "FLAMETHROWER", type = WeaponType.Gun, auto = true, rpm = 600, muzzleVelocity = 16, bulletMass = 0.001f, diameter = 0.01f, magazine = 300, reloadTime = 4.5f, recoil = 0f, spread = 0f, zoomFov = 62, loudness = 35, tracer = false },
            // ---- the Kalashnikovs since the AKM, 5.45x39 (GunInfo has the rest of what each one is)
            new WeaponDef { name = "AK-74", type = WeaponType.Gun, auto = true, rpm = 650, muzzleVelocity = 900, bulletMass = 0.0034f, diameter = 0.0056f, magazine = 30, reloadTime = 2.4f, recoil = 1.05f, spread = 0.3f, zoomFov = 55, loudness = 75, tracer = true },
            new WeaponDef { name = "AK-74M", type = WeaponType.Gun, auto = true, rpm = 650, muzzleVelocity = 900, bulletMass = 0.0034f, diameter = 0.0056f, magazine = 30, reloadTime = 2.3f, recoil = 1.0f, spread = 0.28f, zoomFov = 55, loudness = 75, tracer = true },
            new WeaponDef { name = "AK-105", type = WeaponType.Gun, auto = true, rpm = 650, muzzleVelocity = 840, bulletMass = 0.0034f, diameter = 0.0056f, magazine = 30, reloadTime = 2.3f, recoil = 1.15f, spread = 0.32f, zoomFov = 55, loudness = 78, tracer = true },
            new WeaponDef { name = "AKS-74U", type = WeaponType.Gun, auto = true, rpm = 700, muzzleVelocity = 735, bulletMass = 0.0034f, diameter = 0.0056f, magazine = 30, reloadTime = 2.2f, recoil = 1.3f, spread = 0.45f, zoomFov = 58, loudness = 82, tracer = true },
            new WeaponDef { name = "AK-12", type = WeaponType.Gun, auto = true, rpm = 700, muzzleVelocity = 880, bulletMass = 0.0034f, diameter = 0.0056f, magazine = 30, reloadTime = 2.3f, recoil = 0.95f, spread = 0.25f, zoomFov = 55, loudness = 75, tracer = true },
            // ---- machine guns: the RPK-74 (45-round magazine), the PKM and the M249 off 100/200-round belts
            new WeaponDef { name = "RPK-74", type = WeaponType.Gun, auto = true, rpm = 600, muzzleVelocity = 960, bulletMass = 0.0034f, diameter = 0.0056f, magazine = 45, reloadTime = 2.9f, recoil = 1.0f, spread = 0.26f, zoomFov = 55, loudness = 78, tracer = true },
            new WeaponDef { name = "PKM", type = WeaponType.Gun, auto = true, rpm = 650, muzzleVelocity = 825, bulletMass = 0.0096f, diameter = 0.0079f, magazine = 100, reloadTime = 6.5f, recoil = 2.4f, spread = 0.34f, zoomFov = 55, loudness = 95, tracer = true },
            new WeaponDef { name = "M249", type = WeaponType.Gun, auto = true, rpm = 800, muzzleVelocity = 915, bulletMass = 0.004f, diameter = 0.0057f, magazine = 200, reloadTime = 7f, recoil = 1.3f, spread = 0.34f, zoomFov = 55, loudness = 80, tracer = true },
        };

        /// <summary>Is this the flamethrower (a gun in the hands, but it throws fire, not rounds)?</summary>
        public static bool IsFlamer(WeaponDef d) => d != null && d.name == "FLAMETHROWER";

        /// <summary>Is this the radio (held like a medical item, but it calls in air support instead)?</summary>
        public static bool IsRadio(WeaponDef d) => d != null && d.name == "RADIO";

        public int current;
        public bool reloading;
        public float aimT;
        bool pointOn;            // point aim toggled on (B)
        readonly int[] ammo = new int[32];
        /// <summary>What you are carrying: you start with nothing and take weapons in the G menu.</summary>
        public bool[] owned;
        public bool Armed => owned != null && current >= 0 && current < owned.Length && owned[current];

        /// <summary>
        /// How far your head goes down onto the stock when you aim a long gun (the cheek weld): the eye comes down
        /// to the sights rather than the gun coming up to the eye, so the butt stays in your shoulder.
        /// </summary>
        public float CheekDrop => Armed && Current.type == WeaponType.Gun && !IsPistol(Current) && BodyHeld ? 0.06f * Mathf.SmoothStep(0f, 1f, aimT) : 0f;

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
        public float pointT;     // point aim: canted, held low
        public float pointAimT;  // point aim with right mouse: canted, up at the eye

        /// <summary>A copy of weapon i as it is set up now (attachments on, no hands), for the gun menu's showcase.</summary>
        public GameObject PreviewModel(int i)
        {
            if (i < 0 || i >= models.Count || models[i] == null) return null;
            var g = Instantiate(models[i]);
            g.SetActive(true);
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("View_Hand") || t.name == "SupportHand" || t.name == "ForegripHand" || t.name == "GunHand" || t.name == "SupHand" || t.name == "Reticle") t.gameObject.SetActive(false);
            foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            return g;
        }
        /// <summary>Aim down the sights as if the right button were held (playtests).</summary>
        public static bool ForceAim;
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
        /// <summary>Old full-screen scope view: gone - scopes are picture-in-picture now (ScopeView), so the gun and
        /// the world round the scope stay in view.</summary>
        public bool Scoped => false;
        public GameObject CurrentModel => current < models.Count ? models[current] : null;
        public Camera MainCam => cam;
        public float NormalFov => pc != null ? pc.fovNormal : 70f;

        // ---- customisation (G menu)
        public WeaponLoadout[] loadouts;
        public WeaponLoadout Loadout => loadouts[current];

        bool IsGlock(int i) => defs[i].name == "GLOCK 17";
        /// <summary>Rounds in a full magazine, with whatever magazine is fitted.</summary>
        public int MagSize(int i) => IsGlock(i) && loadouts != null && loadouts[i].extMag ? 33 : defs[i].magazine;
        /// <summary>Holding the trigger keeps firing (rifles, or a Glock with a switch on the back).</summary>
        public bool IsAuto(int i) => FireMode(i) == "AUTO";

        /// <summary>The fire modes a gun has, in the order Z goes through them.</summary>
        public string[] ModesFor(int i)
        {
            if (defs[i].name != "AK-47" && defs[i].name != "AR-15" && GunInfo.Of(defs[i].name) is GunInfo.Info gi) return gi.modes;
            switch (defs[i].name)
            {
                case "AK-47": return new[] { "AUTO", "SEMI" };
                case "AR-15": return new[] { "AUTO", "BURST", "SEMI" };
                case "GLOCK 17": return loadouts != null && loadouts[i].autoSwitch ? new[] { "AUTO", "SEMI" } : new[] { "SEMI" };
                case "MOSSBERG 590": return new[] { "PUMP" };
                case "FLAMETHROWER": return new[] { "AUTO" };
                case "AWP": return new[] { "BOLT" };
                default: return new[] { "SEMI" };
            }
        }
        public string FireMode(int i)
        {
            if (defs[i].type != WeaponType.Gun) return "";
            var m = ModesFor(i);
            int k = loadouts != null ? Mathf.Clamp(loadouts[i].fireMode, 0, m.Length - 1) : 0;
            return m[k];
        }

        // recoil state: which shot of a burst this is, and that burst's pattern
        float lastShotTime = -9f, burstSeed, burstSide = 1f, fireHeat;
        int burstShot, burstLeft;
        /// <summary>0..1: how much firing a hard-kicking gun is slowing you down right now.</summary>
        public float FireSlow => Mathf.Clamp01(fireHeat);
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
                    case SightKind.RedDot: return Loadout.magnifier && MagnifierFor(Current) ? pc.fovNormal * 0.8f : 48f;
                    case SightKind.Holo: return Loadout.magnifier && MagnifierFor(Current) ? pc.fovNormal * 0.8f : 46f;
                    // (the magnification is in the scope's eyepiece; the eye itself zooms in only a little)
                    case SightKind.Scope4x: return Current.zoomFov > 30f ? pc.fovNormal * 0.82f : pc.fovNormal * 0.78f;
                    case SightKind.Scope8x: return pc.fovNormal * 0.75f;
                    default: return Current.zoomFov;
                }
            }
        }
        public static SightKind[] SightsFor(WeaponDef d)
        {
            if (d.type != WeaponType.Gun) return new SightKind[0];
            if (d.scope) return new[] { SightKind.Scope8x, SightKind.Scope4x };
            if (d.name == "GLOCK 17") return new[] { SightKind.Iron, SightKind.RedDot };     // MOS cut: an RMR on the slide
            if (d.name == "BERETTA M9" || d.name == "TASER" || IsFlamer(d)) return new[] { SightKind.Iron };
            if (d.name == "MOSSBERG 590") return new[] { SightKind.Iron };                     // ghost rings: the receiver has no optic rail
            return new[] { SightKind.Iron, SightKind.RedDot, SightKind.Holo, SightKind.Scope4x };
        }
        public static bool GripsFor(WeaponDef d) => d.type == WeaponType.Gun && !IsPistol(d) && d.name != "MOSSBERG 590" && !IsFlamer(d);
        public static bool SuppressorFor(WeaponDef d) => d.type == WeaponType.Gun && d.name != "TASER" && d.name != "MOSSBERG 590" && !IsFlamer(d) && d.name != "PKM" && d.name != "M249";
        public static bool LightFor(WeaponDef d) => d.type == WeaponType.Gun && d.name != "TASER" && !d.scope && !IsFlamer(d);
        public static bool LaserFor(WeaponDef d) => d.type == WeaponType.Gun && d.name != "TASER" && !IsFlamer(d);
        /// <summary>L and J: the weapon light (or, with none fitted, a head torch) and the laser, on and off.</summary>
        public static bool LightOn = true, LaserOn = true, TorchOn;
        /// <summary>Your gun light or head torch is shining now.</summary>
        public static bool PlayerLightOn;
        /// <summary>ALT + A / D held with a gun: it is pushed out round a corner (the view stays where it is, the
        /// mouse works the gun instead, and the strafe keys don't walk you).</summary>
        public static bool BlindHold;
        /// <summary>Playtests: hold the gun out round a corner this side (1 right, -1 left) as if ALT + D / A were held.</summary>
        public static int ForceBlind;
        /// <summary>Playtests: hold the trigger down (as if the left button were held, full auto).</summary>
        public static bool ForceFire;
        /// <summary>How far your head is turned (yaw, pitch degrees) to look where the gun points round the corner.</summary>
        public static Vector2 BlindLook;
        int blindSide = 1;
        float blindT, blindOut = 0.8f;
        Vector2 blindAim;

        /// <summary>Where the laser goes: on top of an AR-15's handguard, on the left side of the other long guns, under a pistol's frame.</summary>
        static (Vector3 pos, Vector3 euler) LaserMount(WeaponDef d)
        {
            if (d.name == "AR-15") return (new Vector3(0f, 0.029f, 0.285f), Vector3.zero);
            var l = LightMount(d.name);
            if (IsPistol(d)) return (l.pos + new Vector3(0f, -0.006f, -0.028f), Vector3.zero);
            return (new Vector3(-l.pos.x, l.pos.y, l.pos.z + 0.02f), new Vector3(l.euler.x, l.euler.y, -l.euler.z));
        }
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
                if (IsRadio(Current)) return Radio.Open ? "PICK THE TARGET" : "LMB CALL IN";
                if (Current.type == WeaponType.Medical) return MedKit.Busy ? "TREATING" : "LMB TREAT";
                if (IsFlamer(Current)) return reloading ? "NEW TANK" : $"FUEL {ammo[current] * 100 / Mathf.Max(1, MagSize(current))}%";
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
            // the old sculpted viewmodel hands are not used any more (every hand is your own, with a PoseHand shape)
            foreach (var r in floatHands) if (r != null) Destroy(r.gameObject);
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

            ScopeView.Attach(this);
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

        static readonly string[] modelNames = { "Gun_AK47", "Gun_Glock", "Gun_AWP", "Gun_Barrett", "Gun_Shotgun", "Gun_Knife", "Gun_Grenade", "Gun_Hammer", "Gun_Sword", "Gun_AR15", "Gun_Beretta", "Gun_Taser", "Gun_PhysGun", "Med_Bandage", "Med_Surgery", "Med_Radio", "Gun_Flamer",
            "Gun_AK74", "Gun_AK74M", "Gun_AK105", "Gun_AKS74U", "Gun_AK12", "Gun_RPK74", "Gun_PKM", "Gun_M249" };

        /// <summary>
        /// The flamethrower (no sculpted model of it either): the wand - a pistol grip and a fore grip on a long tube
        /// with the igniter under the nozzle - and the hose running back to the tanks on your back. Grip at the origin.
        /// </summary>
        static Vector3[] FlamerModel(Transform root)
        {
            var g = new GameObject("Gun_Flamer").transform;
            g.SetParent(root, false);
            Color steel = new Color(0.2f, 0.21f, 0.2f), olive = new Color(0.26f, 0.29f, 0.2f), brass = new Color(0.55f, 0.45f, 0.22f), black = new Color(0.05f, 0.05f, 0.05f);
            var tube = Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0.2f), new Vector3(0.035f, 0.33f, 0.035f), steel, new Vector3(90f, 0f, 0f), 0.4f, 0.6f);
            tube.name = "Gun_FlamerTube";
            Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0.55f), new Vector3(0.045f, 0.03f, 0.045f), brass, new Vector3(90f, 0f, 0f), 0.6f, 0.8f);     // the nozzle
            Part(g, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.5f), new Vector3(0.016f, 0.05f, 0.016f), black, new Vector3(90f, 0f, 0f));                   // the igniter
            Part(g, PrimitiveType.Cube, new Vector3(0f, -0.04f, 0f), new Vector3(0.03f, 0.11f, 0.045f), olive, new Vector3(-16f, 0f, 0f));                         // pistol grip
            Part(g, PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.3f), new Vector3(0.03f, 0.09f, 0.04f), olive, new Vector3(-8f, 0f, 0f));                          // fore grip
            Part(g, PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.05f), new Vector3(0.05f, 0.05f, 0.12f), steel, default, 0.4f, 0.5f);                              // valve block
            Part(g, PrimitiveType.Cylinder, new Vector3(0.03f, -0.02f, -0.12f), new Vector3(0.022f, 0.14f, 0.022f), black, new Vector3(60f, 0f, 20f), 0.2f, 0f);  // the hose, back over your hip
            var mesh = tube.GetComponent<MeshFilter>().sharedMesh.vertices;
            var v = new Vector3[mesh.Length];
            for (int k = 0; k < v.Length; k++) v[k] = root.InverseTransformPoint(tube.transform.TransformPoint(mesh[k]));
            return v;
        }

        /// <summary>The grip and muzzle points of the weapons built here rather than in the model library.</summary>
        static Dictionary<string, Vector3> BuiltPoints(string name) => name == "Gun_Flamer"
            ? new Dictionary<string, Vector3> { ["Grip"] = Vector3.zero, ["Muzzle"] = new Vector3(0f, 0.05f, 0.58f), ["Fore"] = new Vector3(0f, -0.03f, 0.3f) }
            : new Dictionary<string, Vector3>();

        /// <summary>The radio handset (there's no sculpted model of it: a box, a keypad, an antenna), its grip at the origin.</summary>
        static Vector3[] RadioModel(Transform root)
        {
            var g = new GameObject("Gun_Radio").transform;
            g.SetParent(root, false);
            Color olive = new Color(0.2f, 0.23f, 0.17f), black = new Color(0.06f, 0.06f, 0.07f);
            var body = Part(g, PrimitiveType.Cube, new Vector3(0f, 0.045f, 0.01f), new Vector3(0.06f, 0.15f, 0.035f), olive, new Vector3(-15f, 0f, 0f), 0.3f, 0.1f);
            Part(g, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0.03f), new Vector3(0.045f, 0.05f, 0.004f), black, new Vector3(-15f, 0f, 0f), 0.6f, 0f);    // the screen and keys
            Part(g, PrimitiveType.Cylinder, new Vector3(0.018f, 0.16f, -0.02f), new Vector3(0.008f, 0.06f, 0.008f), black, new Vector3(-15f, 0f, 0f));      // the antenna
            Part(g, PrimitiveType.Cylinder, new Vector3(-0.014f, 0.128f, -0.01f), new Vector3(0.012f, 0.008f, 0.012f), black, new Vector3(-15f, 0f, 0f));   // a knob
            body.name = "Gun_RadioBody";
            var mesh = body.GetComponent<MeshFilter>().sharedMesh.vertices;
            var v = new Vector3[mesh.Length];
            for (int k = 0; k < v.Length; k++) v[k] = root.InverseTransformPoint(body.transform.TransformPoint(mesh[k]));
            return v;
        }

        readonly List<float> ironY = new List<float>(), scopeY = new List<float>();
        /// <summary>Where the back of each gun's own scope (its eyepiece glass) is, front to back (NaN: no scope).</summary>
        readonly List<float> ocularZ = new List<float>();

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
                var model = ModelLibrary.Get(name);
                // (anything without a model in the library is built here: the radio)
                Vector3[] built = null;
                if (model != null) ModelLibrary.SpawnWithMag(name, root, ModelLibrary.GunMaterial, false);
                else built = name == "Gun_Flamer" ? FlamerModel(root) : RadioModel(root);
                var points = model != null ? model.points : BuiltPoints(name);
                Vector3 grip = points.TryGetValue("Grip", out var g) ? g : Vector3.zero;
                AddAnimPoints(points, grip);
                Vector3 muzzle = points.TryGetValue("Muzzle", out var mz) ? mz : new Vector3(0, 0, 0.3f);
                AddArm(root, grip, skinMat, name);
                AddModel(root.gameObject, muzzle);
                var mv = model != null ? model.mesh.vertices : built;
                Vector3 fore;
                if (!points.TryGetValue("Fore", out fore))
                    fore = new Vector3(0f, grip.y + 0.05f, Mathf.Lerp(grip.z, muzzle.z, 0.5f));
                gripPts.Add(grip); forePts.Add(fore);
                ejectPts.Add(points.TryGetValue("Eject", out var ej) ? ej : new Vector3(0.015f, 0.01f, 0f));
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
                if (points.TryGetValue("Iron", out var ir)) iron = ir.y;
                ironY.Add(iron == float.MinValue ? 0.05f : iron);
                scopeY.Add(points.TryGetValue("Scope", out var sc) ? sc.y : (iron == float.MinValue ? 0.08f : iron + 0.04f));
                // the eyepiece: the back of the glass (the eye has to sit a few centimetres behind it, wherever the
                // scope is mounted on that gun - on the .50 it's a long way back)
                float oz = float.NaN;
                if (model != null && model.materials != null)
                    for (int sm = 0; sm < model.materials.Length && sm < model.mesh.subMeshCount; sm++)
                    {
                        if (model.materials[sm] != "Glass") continue;
                        foreach (int ti in model.mesh.GetTriangles(sm)) oz = float.IsNaN(oz) ? mv[ti].z : Mathf.Min(oz, mv[ti].z);
                    }
                ocularZ.Add(oz);
                mountUnder.Add(new Vector3(0f, under == float.MaxValue ? -0.03f : under, (zC + zD) * 0.5f));
            }
        }

        /// <summary>Sculpted gripping hand + forearm (Blender model), rotated to each weapon's grip angle.</summary>
        /// <summary>
        /// How the shooting hand sits on each weapon, relative to its grip point: an offset and a rotation (the
        /// sculpted hand wraps a grip that runs down and back at the angle given by x). Pistols also say where the
        /// support hand's grip goes (it wraps over the shooting hand). One table, tuned by eye per gun.
        /// </summary>
        public static readonly Dictionary<string, (Vector3 off, Vector3 rot, Vector3 supOff, Vector3 supRot)> HandPose = new Dictionary<string, (Vector3, Vector3, Vector3, Vector3)>
        {
            ["Gun_AK47"] = (Vector3.zero, new Vector3(18f, 0f, 0f), default, default),
            ["Gun_AK74"] = (Vector3.zero, new Vector3(18f, 0f, 0f), default, default),
            ["Gun_AK74M"] = (Vector3.zero, new Vector3(18f, 0f, 0f), default, default),
            ["Gun_AK105"] = (Vector3.zero, new Vector3(18f, 0f, 0f), default, default),
            ["Gun_AKS74U"] = (Vector3.zero, new Vector3(18f, 0f, 0f), default, default),
            ["Gun_AK12"] = (Vector3.zero, new Vector3(20f, 0f, 0f), default, default),
            ["Gun_RPK74"] = (Vector3.zero, new Vector3(18f, 0f, 0f), default, default),
            ["Gun_PKM"] = (Vector3.zero, new Vector3(20f, 0f, 0f), default, default),
            ["Gun_M249"] = (Vector3.zero, new Vector3(20f, 0f, 0f), default, default),
            ["Gun_Glock"] = (Vector3.zero, new Vector3(16f, 0f, 0f), new Vector3(-0.012f, -0.012f, 0.004f), new Vector3(16f, 8f, 0f)),
            ["Gun_Beretta"] = (Vector3.zero, new Vector3(16f, 0f, 0f), new Vector3(-0.012f, -0.012f, 0.004f), new Vector3(16f, 8f, 0f)),
            ["Gun_Taser"] = (new Vector3(0f, -0.02f, -0.006f), new Vector3(14f, 0f, 0f), new Vector3(-0.012f, -0.032f, -0.002f), new Vector3(14f, 8f, 0f)),   // shorter grip, lower hands
            ["Gun_PhysGun"] = (Vector3.zero, new Vector3(16f, 0f, 0f), default, default),
            ["Gun_AR15"] = (Vector3.zero, new Vector3(20f, 0f, 0f), default, default),
            ["Gun_AWP"] = (Vector3.zero, new Vector3(20f, 0f, 0f), default, default),
            ["Gun_Barrett"] = (Vector3.zero, new Vector3(18f, 0f, 0f), default, default),
            ["Gun_Shotgun"] = (Vector3.zero, new Vector3(30f, 0f, 0f), default, default),    // wraps the sloped wrist of the stock
            ["Gun_Knife"] = (Vector3.zero, new Vector3(90f, 0f, 0f), default, default),      // handle runs forward
            ["Gun_Hammer"] = (Vector3.zero, new Vector3(90f, 0f, 0f), default, default),
            ["Gun_Sword"] = (Vector3.zero, new Vector3(90f, 0f, 0f), default, default),
            ["Gun_Grenade"] = (Vector3.zero, new Vector3(10f, 0f, -15f), default, default),
            ["Med_Bandage"] = (Vector3.zero, new Vector3(10f, 0f, -15f), default, default),
            ["Med_Surgery"] = (Vector3.zero, new Vector3(10f, 0f, -15f), default, default),  // held in a fist
            ["Med_Radio"] = (Vector3.zero, new Vector3(10f, 0f, -15f), default, default),
        };
        static bool PistolModel(string weapon) => weapon == "Gun_Glock" || weapon == "Gun_Beretta" || weapon == "Gun_Taser";
        /// <summary>The left hand round a rifle's handguard: fist along the barrel, forearm back, down and to the left.</summary>
        static readonly Vector3 LongSupportRot = new Vector3(-140f, -100f, -80f);
        readonly List<Vector3> supportCentre = new List<Vector3>();
        readonly List<float> supportRadius = new List<float>();

        static void AddArm(Transform root, Vector3 grip, Material skin, string weapon)
        {
            var hand = ModelLibrary.Spawn("View_Hand", root, sub => HandMat(sub, skin), false);
            if (hand == null) return;
            var p = HandPose.TryGetValue(weapon, out var hp) ? hp : (Vector3.zero, new Vector3(16f, 0f, 0f), default, default);
            hand.transform.localPosition = grip + p.off;
            hand.transform.localRotation = Quaternion.Euler(p.rot);
        }

        /// <summary>Puts every hand back where the pose table says (after tuning it live).</summary>
        public void ApplyHandPoses()
        {
            for (int i = 0; i < models.Count && i < gripPts.Count; i++)
            {
                if (!HandPose.TryGetValue(modelNames[i], out var p)) continue;
                foreach (Transform t in models[i].transform)
                {
                    if (t.name.StartsWith("View_Hand")) { t.localPosition = gripPts[i] + p.off; t.localRotation = Quaternion.Euler(p.rot); }
                    else if (t.name == "SupportHand" && PistolModel(modelNames[i])) { t.localPosition = gripPts[i] + p.supOff; t.localRotation = Quaternion.Euler(p.supRot); }
                    else if (t.name == "SupportHand" && i < supportCentre.Count) { t.localPosition = supportCentre[i] + p.supOff; t.localRotation = Quaternion.Euler(p.supRot == Vector3.zero ? LongSupportRot : p.supRot); }
                }
            }
        }

        readonly List<GameObject> supportCup = new List<GameObject>(), supportGrip = new List<GameObject>();
        static Material sleeveMat;

        static Material HandMat(string sub, Material skin)
        {
            // the sleeve is bare forearm: the mannequin wears nothing
            if (sub == "Sleeve") return skin;
            return skin;
        }

        /// <summary>
        /// The other hand. Long guns: the left hand cups the handguard from below, fingers wrapped up its side
        /// (sized to the handguard there); with a foregrip fitted it holds the grip instead. Pistol: wrapped over
        /// the shooting hand. Knife and grenade stay one-handed.
        /// </summary>
        void AddSupportHand(Transform root, Vector3 grip, Vector3 fore, Vector3[] mv, Material skin, string weapon, int idx)
        {
            if (weapon == "Gun_Knife" || weapon == "Gun_Grenade" || weapon == "Gun_Hammer" || weapon == "Gun_Sword" || weapon == "Gun_PhysGun" || weapon.StartsWith("Med_")) return;
            if (PistolModel(weapon))
            {
                var ph = ModelLibrary.Spawn("View_Hand", root, sub => HandMat(sub, skin), false, true);
                if (ph == null) return;
                ph.name = "SupportHand";
                // thumbs-forward two-hand grip: the left hand sits on the left of the grip, a little lower
                var pose = HandPose[weapon];
                ph.transform.localPosition = grip + pose.supOff;
                ph.transform.localRotation = Quaternion.Euler(pose.supRot);
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
            // the same hand as everywhere else (the shooting hand, mirrored for the left), its fist closed round the
            // handguard and the forearm running back down to the left shoulder
            var cup = ModelLibrary.Spawn("View_Hand", root, sub => HandMat(sub, skin), false, true);
            if (cup == null) return;
            cup.name = "SupportHand";
            while (supportCentre.Count <= idx) supportCentre.Add(Vector3.zero);
            supportCentre[idx] = c;
            while (supportRadius.Count <= idx) supportRadius.Add(0.026f);
            supportRadius[idx] = r;
            (Vector3 off, Vector3 rot, Vector3 supOff, Vector3 supRot) sp = HandPose.TryGetValue(weapon, out var hp) ? hp : (Vector3.zero, Vector3.zero, Vector3.zero, LongSupportRot);
            cup.transform.localPosition = c + sp.supOff;
            cup.transform.localRotation = Quaternion.Euler(sp.supRot == Vector3.zero ? LongSupportRot : sp.supRot);
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
                            // the projected reticle on the glass, right on the sight line (drawn at infinity by
                            // the shader, so the quad must be unscaled: its size is the window's)
                            if (L.sight == SightKind.RedDot || L.sight == SightKind.Holo)
                            {
                                var ret = new GameObject("Reticle");
                                ret.transform.SetParent(g.transform, false);
                                ret.transform.localPosition = L.sight == SightKind.RedDot ? new Vector3(0f, 0.03f, 0.027f) : new Vector3(0f, 0.033f, 0.017f);
                                ret.AddComponent<MeshFilter>().sharedMesh = L.sight == SightKind.RedDot ? ProcMesh.Rect(0.021f, 0.021f) : ProcMesh.Rect(0.031f, 0.023f);
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
                rigKey = null;   // the hands go back on it for the new setup
                ShowIrons(i, d.scope || L.sight == SightKind.Iron);
            }
        }

        // ------------------------------------------------------------ iron sights off when an optic goes on
        // Regions of each gun's mesh (model space) holding its iron sights: the AR's folding sights come off, the
        // Glock's slide sights, the AK's front post. The rest of the gun is untouched.
        static readonly Dictionary<string, Bounds[]> ironRegions = new Dictionary<string, Bounds[]>
        {
            ["AR-15"] = new[] { MinMax(-0.013f, 0.0293f, 0.326f, 0.013f, 0.07f, 0.355f), MinMax(-0.013f, 0.0293f, -0.164f, 0.013f, 0.075f, -0.135f) },
            ["GLOCK 17"] = new[] { MinMax(-0.012f, 0.0503f, -0.0885f, 0.012f, 0.06f, -0.0755f), MinMax(-0.003f, 0.0503f, 0.081f, 0.003f, 0.06f, 0.0895f) },
            ["AK-47"] = new[] { MinMax(-0.0025f, 0.021f, 0.442f, 0.0025f, 0.036f, 0.448f) },
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
        readonly Light[] lights = new Light[32];
        readonly Transform[] laserEmit = new Transform[32];
        GameObject laserDot;

        /// <summary>Where each gun takes a light: under a pistol's dust cover, on the right of a rifle's handguard.</summary>
        static (Vector3 pos, Vector3 euler) LightMount(string gun)
        {
            switch (gun)
            {
                case "GLOCK 17": return (new Vector3(0f, 0.002f, 0.066f), Vector3.zero);
                case "BERETTA M9": return (new Vector3(0f, 0.003f, 0.078f), Vector3.zero);
                case "AR-15": return (new Vector3(0.023f, -0.001f, 0.3f), new Vector3(0f, 0f, 90f));
                case "AK-47": case "AK-74": case "AK-74M": case "AK-12": case "RPK-74": return (new Vector3(0.026f, -0.004f, 0.215f), new Vector3(0f, 0f, 90f));
                case "AK-105": return (new Vector3(0.026f, -0.004f, 0.18f), new Vector3(0f, 0f, 90f));
                case "AKS-74U": return (new Vector3(0.026f, -0.004f, 0.12f), new Vector3(0f, 0f, 90f));
                case "PKM": return (new Vector3(0.02f, -0.03f, 0.3f), new Vector3(0f, 0f, 90f));
                case "M249": return (new Vector3(0.028f, -0.03f, 0.22f), new Vector3(0f, 0f, 90f));
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
                    lt.type = LightType.Spot; lt.spotAngle = 40f; lt.innerSpotAngle = 10f; lt.range = 55f; lt.intensity = 14f;
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
                    var lm = LaserMount(d);
                    g.transform.localPosition = lm.pos; g.transform.localRotation = Quaternion.Euler(lm.euler);
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

        Light torch;
        LineRenderer beam;

        /// <summary>
        /// The light shines while fitted and switched on (L; with no light on the gun, L is a head torch). The laser
        /// (J) puts its dot wherever the barrel line meets something, and its beam shows where there's dust or
        /// smoke in the air for it to catch on - faint in clear air, a bright red line through a cloud.
        /// </summary>
        void UpdateRailGear()
        {
            BarrelHeat.I?.UpdateGlow(Armed && Current.type == WeaponType.Gun && current < muzzles.Count && vmRoot.gameObject.activeInHierarchy ? muzzles[current] : null, current, IsPistol(Current));
            bool gunLight = Armed && current < lights.Length && lights[current] != null;
            for (int k = 0; k < lights.Length; k++) if (lights[k] != null) lights[k].enabled = Armed && k == current && LightOn;
            if (torch == null && TorchOn)
            {
                var tg = new GameObject("HeadTorch");
                tg.transform.SetParent(cam.transform, false);
                tg.transform.localPosition = new Vector3(0.08f, 0.06f, 0.05f);
                torch = tg.AddComponent<Light>();
                torch.type = LightType.Spot; torch.spotAngle = 55f; torch.innerSpotAngle = 20f; torch.range = 32f; torch.intensity = 6f;
                torch.color = new Color(1f, 0.93f, 0.82f); torch.shadows = LightShadows.Soft;
            }
            if (torch != null) torch.enabled = TorchOn && !gunLight && FpvDrone.Piloting == null;
            // (the Earls can see a light from a long way off at night)
            PlayerLightOn = FpvDrone.Piloting == null && (gunLight && LightOn || TorchOn && !gunLight);
            var e = Armed && current < laserEmit.Length && LaserOn ? laserEmit[current] : null;
            bool on = e != null && vmRoot.gameObject.activeInHierarchy;
            if (beam == null)
            {
                var bg = new GameObject("LaserBeam");
                beam = bg.AddComponent<LineRenderer>();
                beam.positionCount = 2; beam.useWorldSpace = true;
                beam.startWidth = 0.003f; beam.endWidth = 0.004f;
                beam.sharedMaterial = new Material(Mats.Unlit(new Color(1f, 0.1f, 0.06f, 1f), true));
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; beam.receiveShadows = false;
            }
            beam.enabled = false;
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
            bool hit = SelfRay(e.position, dir, out var h, 150f);
            Vector3 end = hit ? h.point : e.position + dir * 150f;
            // through dust and smoke the dot dims (the haze swallows it) and the beam lights up along its length
            float clear = Haze.Transmit(e.position, end);
            if (hit)
            {
                laserDot.SetActive(true);
                laserDot.transform.position = h.point + h.normal * 0.004f;
                laserDot.transform.rotation = Quaternion.LookRotation(-h.normal);
                // it spreads a little with range (a real beam is a few mm wide and diverges)
                laserDot.transform.localScale = Vector3.one * (0.006f + h.distance * 0.0006f) * Mathf.Lerp(1.6f, 1f, clear);
                laserDot.GetComponent<MeshRenderer>().enabled = clear > 0.08f;
            }
            float glow = 0.06f + (1f - clear) * 0.7f;
            beam.enabled = true;
            beam.SetPosition(0, e.position);
            beam.SetPosition(1, end);
            var bc = new Color(1f, 0.1f, 0.06f, glow);
            beam.startColor = bc; beam.endColor = new Color(bc.r, bc.g, bc.b, glow * (hit ? 0.8f : 0.1f));
            beam.sharedMaterial.SetColor("_Color", new Color(1f, 0.1f, 0.06f, glow));
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
            // (another gun in the hands: whatever the hands were doing to this one stops, its magazine back in)
            if (i != current) { moves.Clear(); movesEnd = -1f; DropHeld(false); var mg = MagOf(current); if (mg != null) mg.gameObject.SetActive(true); magOut = false; }
            if (i < 0 || i >= defs.Count || (owned != null && !owned[i])) return;
            current = i;
            reloading = false;
            for (int k = 0; k < models.Count; k++) models[k].SetActive(k == i);
            nextFire = Time.unscaledTime + 0.25f;
            kick = -0.6f;
        }

        // ------------------------------------------------------------ input

        /// <summary>The n-th (0-based) weapon you carry, as numbered on the bar.</summary>
        public void SelectSlot(int n)
        {
            for (int i = 0, k = 0; i < defs.Count; i++)
                if (owned[i]) { if (k == n) { Select(i); return; } k++; }
        }

        static readonly Unity.Profiling.ProfilerMarker PM_Update = new Unity.Profiling.ProfilerMarker("Carl.WeaponController.Update");

        void Update()
        {

            using var pmScope = PM_Update.Auto();
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || pc == null) return;
            bool locked = Cursor.lockState == CursorLockMode.Locked && !Menus.Blocking && !pc.flying;   // (flying the free camera: hands off the gun)
            float dt = Time.unscaledDeltaTime;

            // number keys pick the n-th weapon you carry (the slots are numbered in the order you hold them)
            if (!SquadCommand.Open && !Radio.Open)
                for (int n = 0; n < 9; n++)
                    if (kb[Key.Digit1 + n].wasPressedThisFrame) { SelectSlot(n); break; }
            float scroll = SquadCommand.Open || Radio.Open ? 0f : mouse.scroll.ReadValue().y;   // (the squad list uses the wheel)
            if (!Grabbing && Mathf.Abs(scroll) > 0.1f && !swingFree)
                for (int s = 1; s < defs.Count; s++) { int k = (current + (scroll < 0 ? s : defs.Count - s)) % defs.Count; if (owned[k]) { Select(k); break; } }

            UpdateGrab(kb, mouse, locked, scroll);

            var d = Current;
            var body = PlayerBody.I;
            int arms = body != null ? body.UsableArms : 2;
            // out cold, dead, or no arm left that can hold it: nothing works
            // (and not while it's carried in one hand off the floor)
            bool able = Armed && arms > 0 && (body == null || (!body.dead && !body.passedOut)) && !carrying;
            if (!able) { meleeMode = 0; swingFree = false; suicideT = 0f; }
            // K: put the gun under your chin
            if (able && locked && d.type == WeaponType.Gun && kb.kKey.wasPressedThisFrame && !kb.leftShiftKey.isPressed && suicideT <= 0f) suicideT = 0.0001f;
            if (suicideT > 0f) { suicideT += dt; if (suicideT >= 1.1f) EndIt(d); }
            // point aim: B toggles it on and off (a gun only). Off, right mouse aims down the sights; on, the gun is
            // held canted and low, and right mouse brings it up canted to the eye (a canted aim)
            if (locked && kb.bKey.wasPressedThisFrame && d.type == WeaponType.Gun) pointOn = !pointOn;
            // L: the light on the gun, or a head torch if there isn't one;  O: the laser;  J: clear a jam
            if (locked && kb.lKey.wasPressedThisFrame)
            {
                bool gunLight = Armed && current < lights.Length && lights[current] != null;
                if (gunLight) { LightOn = !LightOn; SandboxGame.Instance?.Toast(LightOn ? "WEAPON LIGHT ON" : "WEAPON LIGHT OFF"); }
                else { TorchOn = !TorchOn; SandboxGame.Instance?.Toast(TorchOn ? "HEAD TORCH ON" : "HEAD TORCH OFF"); }
                Sfx.Play("click_empty", cam.transform.position, 0.3f, 0f, 1f, 0.05f);
            }
            if ((locked || ForceFire) && kb.jKey.wasPressedThisFrame && d.type == WeaponType.Gun && BarrelHeat.I != null && BarrelHeat.I.Jammed(current))
                BarrelHeat.I.ClearStep(current, this);
            if (locked && kb.oKey.wasPressedThisFrame)
            {
                bool fitted = Armed && current < laserEmit.Length && laserEmit[current] != null;
                if (!fitted) SandboxGame.Instance?.Toast("NO LASER ON THIS GUN  -  FIT ONE IN THE G MENU");
                else { LaserOn = !LaserOn; SandboxGame.Instance?.Toast(LaserOn ? "LASER ON" : "LASER OFF"); }
                Sfx.Play("click_empty", cam.transform.position, 0.3f, 0f, 1f, 0.05f);
            }
            // ALT + D / ALT + A: the gun out round the corner on that side. Mouse left and right: how far out;
            // hold right mouse and move it to point the gun about while it's out there
            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            int blindWant = able && (locked || ForceBlind != 0) && d.type == WeaponType.Gun && (alt || ForceBlind != 0) && !reloading ? (ForceBlind != 0 ? ForceBlind : kb.dKey.isPressed ? 1 : kb.aKey.isPressed ? -1 : 0) : 0;
            if (blindWant != 0)
            {
                if (blindT < 0.05f) { blindAim = Vector2.zero; }
                blindSide = blindWant;
                Vector2 md = mouse.delta.ReadValue() * 0.09f * GameSettings.MouseSensitivity;
                if (mouse.rightButton.isPressed) blindAim = new Vector2(Mathf.Clamp(blindAim.x + md.x, -40f, 40f), Mathf.Clamp(blindAim.y + md.y, -30f, 30f));
                else blindOut = Mathf.Clamp01(blindOut + md.x * blindSide * 0.012f);
            }
            BlindHold = blindWant != 0;
            blindT = Mathf.MoveTowards(blindT, blindWant != 0 ? 1f : 0f, dt * 5f);
            if (d.type != WeaponType.Gun) pointOn = false;
            bool rmbAim = (ForceAim || locked && mouse.rightButton.isPressed && !BlindHold) && d.type == WeaponType.Gun && !reloading;
            bool aimHeld = rmbAim && !pointOn;
            aimT = Mathf.MoveTowards(aimT, aimHeld ? 1f : 0f, dt * (IsScope ? 5f : Loadout.grip == GripKind.Angled ? 11f : 8f));
            bool pointHeld = pointOn && d.type == WeaponType.Gun && !reloading;
            pointT = Mathf.MoveTowards(pointT, pointHeld ? 1f : 0f, dt * 7f);
            pointAimT = Mathf.MoveTowards(pointAimT, rmbAim && pointOn ? 1f : 0f, dt * 8f);
            float fov = Mathf.Lerp(pc.fovNormal, d.type == WeaponType.Gun ? ZoomFov : pc.fovNormal, Mathf.SmoothStep(0, 1, aimT));
            fov = Mathf.Lerp(fov, pc.fovNormal * 0.9f, pointT * (1f - aimT));
            // point aim raised: a touch of zoom as you focus on the target (not down the sights)
            fov = Mathf.Lerp(fov, pc.fovNormal * 0.85f, Mathf.SmoothStep(0f, 1f, pointAimT));
            cam.fieldOfView = fov;
            pc.sensitivityScale = fov / pc.fovNormal;
            // through a magnified scope the mouse slows with the magnification (you are aiming the scope's picture)
            float mag = ScopeView.Magnification(this);
            if (mag > 1.5f) pc.sensitivityScale = Mathf.Lerp(pc.sensitivityScale, 1.4f / mag, Mathf.SmoothStep(0f, 1f, aimT));

            if (reloading && Time.unscaledTime >= reloadEnd) { reloading = false; ammo[current] = MagSize(current); if (IsFlamer(d)) FlameTank.Refit(); }
            if (GameSettings.InfiniteAmmo && d.type == WeaponType.Gun) ammo[current] = MagSize(current);
            if (d.type == WeaponType.Gun && ammo[current] > MagSize(current)) ammo[current] = MagSize(current);   // extended mag taken off
            // Z: fire mode
            if (locked && kb.zKey.wasPressedThisFrame && d.type == WeaponType.Gun)
            {
                var modes = ModesFor(current);
                if (modes.Length > 1) { Loadout.fireMode = (Mathf.Clamp(Loadout.fireMode, 0, modes.Length - 1) + 1) % modes.Length; burstLeft = 0; Sfx.Play("click_empty", cam.transform.position, 0.35f, 0f, 1f, 0.05f); }
                SandboxGame.Instance?.Toast("FIRE MODE: " + FireMode(current) + (modes.Length > 1 ? "" : " ONLY"));
            }
            if (locked && SquadCommand.ReloadFrame == Time.frameCount && d.type == WeaponType.Gun && !reloading && ammo[current] < MagSize(current)) StartReload();

            // ALT held with a blade or hammer: the mouse moves the weapon instead of the view
            swingFree = able && locked && d.type == WeaponType.Knife && kb.leftAltKey.isPressed && suicideT <= 0f;
            if (swingFree) { pc.sensitivityScale = 0f; UpdateFreeSwing(mouse, dt); } else { swingPos = Vector2.Lerp(swingPos, Vector2.zero, dt * 8f); swingTipValid = false; }

            if ((locked || ForceFire) && !Grabbing && able && !swingFree && suicideT <= 0f)
            {
                switch (d.type)
                {
                    case WeaponType.Gun:
                        // burst: one pull fires three
                        if (FireMode(current) == "BURST" && mouse.leftButton.wasPressedThisFrame && burstLeft <= 0) burstLeft = 3;
                        bool trigger = mouse.leftButton.wasPressedThisFrame || (IsAuto(current) && (mouse.leftButton.isPressed || ForceFire)) || burstLeft > 0;
                        if (trigger && BarrelHeat.I != null && BarrelHeat.I.Jammed(current))
                        {
                            // jammed: the trigger goes, nothing happens (J clears it)
                            burstLeft = 0;
                            if (mouse.leftButton.wasPressedThisFrame || ForceFire) BarrelHeat.I.DeadClick();
                        }
                        else if (trigger && !reloading && Time.unscaledTime >= nextFire)
                        {
                            bool taserLive = d.name == "TASER" && TaserWires.Current != null && TaserWires.Current.Arrived;
                            if (ammo[current] <= 0 && !taserLive) { Sfx.Play("click_empty", cam.transform.position, 0.6f, 0f); StartReload(); }
                            else { using (Prof.Fire.Auto()) FireGun(d); }
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
                        // a throw is a movement: wind up, whip the arm through, let go at the right moment
                        if (throwT < 0f && Time.unscaledTime >= nextFire && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                        {
                            throwFar = mouse.leftButton.wasPressedThisFrame;
                            throwT = 0f; thrown = false;
                        }
                        break;
                }
            }
            UpdateMelee(dt);
            UpdateThrow(dt);
            Animate(dt);
            if (!Armed) vmRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ throwing a grenade
        // Overarm (left click): the arm goes back over the shoulder, then whips forward and lets go at the top of
        // the swing, and follows through; a fresh grenade comes up into the hand. Underhand (right click): a lower,
        // gentler swing from beside the hip. The grenade leaves from where your hand is when you let go.
        float throwT = -1f; bool throwFar, thrown;
        const float FarRelease = 0.46f, LowRelease = 0.36f, ThrowDone = 1.15f;
        public bool Throwing => throwT >= 0f;

        void UpdateThrow(float dt)
        {
            if (throwT < 0f) return;
            if (!Armed || Current.type != WeaponType.Grenade) { throwT = -1f; SetGrenadeShown(true); return; }
            throwT += dt;
            if (!thrown && throwT >= (throwFar ? FarRelease : LowRelease))
            {
                thrown = true;
                // more shrapnel = a heavier grenade that doesn't throw as far
                float heavy = Mathf.Lerp(1.15f, 0.7f, Mathf.InverseLerp(100f, 700f, Loadout.shrapnel));
                Vector3 v = (cam.transform.forward * (throwFar ? 17f : 7f) + Vector3.up * (throwFar ? 2.5f : 1.5f)) * heavy;
                // from the hand (the grenade model as it is now), or in front of you without a body
                Vector3 from = current < models.Count && models[current] != null ? models[current].transform.position : cam.transform.position + cam.transform.forward * 0.5f;
                // never from inside a wall: if the hand is past one, it leaves from in front of your face
                if (Physics.Linecast(cam.transform.position, from, out _, ~(1 << PlayerAvatar.OwnLayer), QueryTriggerInteraction.Ignore)) from = cam.transform.position + cam.transform.forward * 0.3f;
                var gr = Grenade.Throw(from, v + (BodyHeld && gInit ? simVel * 0.3f : Vector3.zero));
                gr.shrapnel = Loadout.shrapnel;
                if (Loadout.FuseSeconds < 0f) { gr.impact = true; gr.fuse = 30f; } else gr.fuse = Loadout.FuseSeconds;
                SetGrenadeShown(false);
                Sfx.Play("knife", cam.transform.position, 0.35f, 0f, 1f, 0.15f);
            }
            if (thrown && throwT > ThrowDone - 0.3f) SetGrenadeShown(true);   // the next one in the hand
            if (throwT >= ThrowDone) { throwT = -1f; nextFire = Time.unscaledTime + 0.15f; }
        }

        void SetGrenadeShown(bool on)
        {
            if (current >= models.Count || models[current] == null) return;
            foreach (var r in models[current].GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith("Gun_") && r.enabled != on) r.enabled = on;
        }

        /// <summary>The arm's movement through the throw, added to where the grenade is held.</summary>
        void ThrowPose(ref Vector3 pos, ref Quaternion rot)
        {
            if (throwT < 0f) return;
            float t = throwT;
            Vector3 back, through; Vector3 eBack, eThrough; float rel;
            if (throwFar)
            {
                // back and up over the shoulder, then over the top and well forward
                back = new Vector3(0.1f, 0.2f, -0.26f); eBack = new Vector3(-65f, 10f, 15f);
                through = new Vector3(-0.05f, 0.04f, 0.36f); eThrough = new Vector3(70f, -15f, -10f);
                rel = FarRelease;
            }
            else
            {
                // down and back beside the hip, then swung forward and up
                back = new Vector3(0.06f, -0.16f, -0.18f); eBack = new Vector3(40f, 0f, 0f);
                through = new Vector3(-0.02f, 0.02f, 0.3f); eThrough = new Vector3(-35f, 0f, 0f);
                rel = LowRelease;
            }
            Vector3 p; Vector3 e;
            if (t < rel * 0.75f)
            {
                // the wind-up: slow, the weight going back
                float u = Mathf.SmoothStep(0f, 1f, t / (rel * 0.75f));
                p = back * u; e = eBack * u;
            }
            else if (t < rel + 0.12f)
            {
                // the throw: fast, the arm whipping through
                float u = Mathf.SmoothStep(0f, 1f, (t - rel * 0.75f) / (rel * 0.25f + 0.12f));
                p = Vector3.Lerp(back, through, u); e = Vector3.Lerp(eBack, eThrough, u);
            }
            else
            {
                // follow through and back to the ready
                float u = Mathf.SmoothStep(0f, 1f, (t - rel - 0.12f) / (ThrowDone - rel - 0.12f));
                p = Vector3.Lerp(through, Vector3.zero, u); e = Vector3.Lerp(eThrough, Vector3.zero, u);
            }
            pos += p;
            rot *= Quaternion.Euler(e);
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
                if (c is CharacterController || c.gameObject.layer == 2) continue;   // (your own gun is on Ignore Raycast)
                if (me != null && c.GetComponentInParent<HumanBody>() == me) continue;
                if (selfBuf[i].distance < best) { best = selfBuf[i].distance; hit = selfBuf[i]; found = true; }
            }
            return found;
        }

        // ---- your blade, for the Earls (parries)
        public int MuzzleCount => muzzles.Count;
        public Transform MuzzleOf(int i) => muzzles[i];
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
        // With a body, the gun is a real rigid body. Your arms drive it toward where you are holding it (the hip,
        // the sights, point aim - all relative to your eye) and the equal and opposite force goes into your chest.
        // Your body's own hands are jointed onto it: firmly at the grip and the handguard, with a few degrees of
        // give, and each hand is still on its wrist joint to its forearm, so the arm, the wrist and the gun move as
        // one chain. Turning drags it, recoil throws it back into your hands, a wall stops it (it has a collider),
        // and a hurt arm is a weaker drive (it lags, sags and shakes). With one hand gone there is one joint and
        // nothing holding the muzzle down. Shots and the crosshair go where its barrel points.

        bool BodyHeld => PlayerAvatar.Body != null && !PlayerAvatar.Body.physio.dead && gripPts.Count > 0;
        Vector3 tgtPos; Quaternion tgtRot = Quaternion.identity; float tgtScale = 1f;
        Rigidbody gunRb; BoxCollider gunCol;
        bool gInit;
        Quaternion gRot => vmRoot.rotation;
        Vector3 lastTp; bool lastTpOk;
        readonly List<Vector3> gripPts = new List<Vector3>(), forePts = new List<Vector3>();
        readonly List<Renderer> floatHands = new List<Renderer>();
        readonly List<bool> floatHandIsGun = new List<bool>();

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
            // only guns get the second hand (a pistol's wraps the first; blades and grenades are one-handed);
            // getting up or going over, the other hand is busy with the floor
            if (d.type != WeaponType.Gun || carrying) { support = null; supportArm = 0f; }
        }

        // ---- carried in one hand: getting up off the floor, or going over
        // The gun is in your grip hand and goes wherever that hand goes (the hand pushes off the floor with it, or
        // tucks it in as you fall); the other hand is free for the floor. Back up, both hands go on it again and
        // it is brought back up to where it is held.
        bool carrying;
        float recoverT;
        public bool Carrying => carrying;
        bool armDrawnR, armDrawnL;
        /// <summary>Which of your arms are drawn on to the gun this frame (the rest are your body's own, seen as they are).</summary>
        public bool ArmDrawnR => armDrawnR;
        public bool ArmDrawnL => armDrawnL;

        Hold GripHold => jointR != null ? jointR : jointL;

        /// <summary>Where the gun is with your grip hand where it is now.</summary>
        bool CarriedPose(out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity;
            var h = GripHold;
            if (h == null || h.hand == null || h.hand.severed) return false;
            rot = h.hand.transform.rotation * Quaternion.Inverse(h.relRot);
            pos = h.hand.transform.TransformPoint(h.handLocal) - rot * h.gunLocal;
            return true;
        }

        /// <summary>A barrel can't go through a wall: when it would, it is pulled back and tipped up out of the way.</summary>
        void Tuck(ref Vector3 tp, ref Quaternion tr)
        {
            if (current >= muzzles.Count) return;
            Vector3 muzzleLocal = muzzles[current].localPosition;
            Vector3 tip = tp + tr * (muzzleLocal + Vector3.forward * 0.04f);
            Vector3 from = Eye.position;
            Vector3 d = tip - from; float len = d.magnitude;
            if (len < 0.05f || !SelfRay(from, d / len, out var hit, len)) { tuck = Mathf.MoveTowards(tuck, 0f, Time.deltaTime * 3f); }
            else tuck = Mathf.MoveTowards(tuck, Mathf.Clamp01((len - hit.distance) / 0.45f), Time.deltaTime * 6f);
            if (tuck <= 0f) return;
            tp -= tr * Vector3.forward * (0.12f * tuck);
            tp += Eye.up * (0.04f * tuck);
            tr = Quaternion.AngleAxis(-55f * tuck, tr * Vector3.right) * tr;
        }
        float tuck;

        /// <summary>What it weighs in the hands (kg).</summary>
        static float GunMass(WeaponDef d)
        {
            if (d.name != "AK-47" && d.name != "AR-15" && GunInfo.Of(d.name) is GunInfo.Info gi) return gi.mass;
            switch (d.name)
            {
                case "GLOCK 17": case "BERETTA M9": return 0.95f;
                case "TASER": return 0.5f;
                case "AK-47": case "MOSSBERG 590": return 3.6f;
                case "AR-15": return 3.1f;
                case "AWP": return 6.5f;
                case ".50 CAL": return 13f;
                case "KNIFE": return 0.3f;
                case "GRENADE": return 0.4f;
                case "BANDAGE": return 0.12f;
                case "SURGERY KIT": return 0.6f;
                case "RADIO": return 0.8f;
                case "FLAMETHROWER": return 4f;      // (the wand: the tanks are on your back)
                case "PHYSICS GUN": return 1.8f;
                default: return d.bulletMass > 0.5f ? d.bulletMass : 1f;
            }
        }

        /// <summary>The gun as a body of its own (made once, on the viewmodel root).</summary>
        void EnsureGunBody()
        {
            if (vmRoot.parent != null) vmRoot.SetParent(null, true);
            vmRoot.localScale = Vector3.one;
            if (gunRb == null)
            {
                gunRb = vmRoot.gameObject.AddComponent<Rigidbody>();
                gunRb.interpolation = RigidbodyInterpolation.None;   // (drawn where your eyes are each frame instead: LateUpdate)
                gunRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                gunRb.maxAngularVelocity = 30f;
                gunRb.solverIterations = 16; gunRb.solverVelocityIterations = 8;
                gunRb.linearDamping = 0.5f; gunRb.angularDamping = 2f;
                var cg = new GameObject("GunCollider");
                cg.layer = 2;   // raycasts (bullets, blood, the crosshair) go straight past it; walls and bodies don't
                cg.transform.SetParent(vmRoot, false);
                gunCol = cg.AddComponent<BoxCollider>();
            }
            if (!gunRb.isKinematic) { gunRb.isKinematic = true; gunInit = false; }   // moved by the arms (HoldPhysics)
            if (!gunCol.enabled) gunInit = false;
            gunCol.enabled = true;
        }
        bool gunInit;

        // ---- the hands on the gun
        // a hand on the gun: which point of the gun it holds, which point of the hand, and how the hand is turned on it
        class Hold { public BodyPart hand; public Vector3 gunLocal, handLocal; public Quaternion relRot; }
        Hold jointR, jointL;
        BodyPart jointedR, jointedL;
        PoseHand visR, visL;              // the gripping hand shape, riding on each of your physical hands
        Vector3 visRelPosR, visRelPosL; Quaternion visRelRotR, visRelRotL;
        string rigKey;

        struct GripSpec { public bool used; public Vector3 centre, n, z, forearm; public float r, tight; public bool trigger; public Vector3? thumb; }

        /// <summary>How each hand closes on weapon i as it is set up now (in the gun's own space).</summary>
        void GripSpecs(int i, out GripSpec gun, out GripSpec sup)
        {
            gun = default; sup = default;
            var d = defs[i]; string model = modelNames[i];
            Vector3 g = i < gripPts.Count ? gripPts[i] : Vector3.zero;
            if (d.type == WeaponType.Knife)
            {
                // a fist round the handle (it runs forward), fingers over the top, forearm down and back
                gun = new GripSpec { used = true, centre = g, r = 0.016f, n = new Vector3(-1f, 0f, 0f), z = Vector3.up, forearm = new Vector3(0.15f, -0.75f, -0.65f), tight = 0.2f };
                return;
            }
            if (d.type == WeaponType.Medical)
            {
                // the roll or the pouch held in a loose fist, like a grenade
                gun = new GripSpec { used = true, centre = g + new Vector3(0.012f, -0.006f, 0f), r = d.name == "BANDAGE" ? 0.028f : 0.026f, n = new Vector3(-0.7f, 0.2f, 0.6f), z = new Vector3(0f, 1f, 0.3f), forearm = new Vector3(0.2f, -0.6f, -0.75f), tight = 0.1f };
                return;
            }
            if (d.type == WeaponType.Grenade)
            {
                gun = new GripSpec { used = true, centre = g + new Vector3(0.01f, -0.01f, -0.005f), r = 0.03f, n = new Vector3(-0.6f, 0.3f, 0.7f), z = new Vector3(0f, 1f, 0.3f), forearm = new Vector3(0.2f, -0.6f, -0.75f), tight = 0.1f };
                return;
            }
            // shooting hand: palm on the right of the grip and round its back strap, fingers round the front, index
            // finger on the trigger, thumb high on the left; forearm back and down to the right
            var gs = GripShape(model);
            Vector3 axis = gs.axis.normalized;
            Vector3 n = Vector3.ProjectOnPlane(new Vector3(-1f, 0f, 0.35f), axis).normalized;
            // (a rifle hand rides low enough on its grip for the fingers to close round it below the trigger guard)
            Vector3 high = IsPistol(d) || d.type != WeaponType.Gun ? Vector3.zero : axis * 0.018f;
            gun = new GripSpec { used = true, centre = g + high, r = gs.r, n = n, z = Vector3.Cross(n, axis), forearm = new Vector3(0.22f, -0.5f, -0.84f), trigger = d.type == WeaponType.Gun,
                // a pistol is held high, the thumb riding forward along the left of the frame (thumbs-forward)
                thumb = IsPistol(d) ? new Vector3(-0.22f, 0.38f, 1f) : (Vector3?)null };
            if (d.type != WeaponType.Gun) return;
            if (IsPistol(d))
            {
                // thumbs-forward two-handed grip: the support palm low on the left of the grip, pressed against it
                // and the shooting hand's fingertips; its fingers run forward and down under the trigger guard and
                // wrap round over the shooting hand's fingers; its thumb lies forward along the frame under the
                // shooting thumb; the support wrist canted forward and down
                Vector3 sn = Vector3.ProjectOnPlane(new Vector3(1f, 0f, 0.1f), axis).normalized;
                Vector3 sz = (Vector3.Cross(axis, sn) + axis * 0.25f).normalized;
                sup = new GripSpec { used = true, centre = g + axis * 0.03f + new Vector3(-0.002f, 0f, 0.002f), r = gs.r + 0.016f, n = sn, z = sz, forearm = new Vector3(-0.35f, -0.45f, -0.82f), tight = 0.3f, thumb = new Vector3(-0.08f, 0.3f, 1f) };
                return;
            }
            if (i >= mountUnder.Count) return;
            var L = loadouts[i];
            if (L.grip != GripKind.None && GripsFor(d))
            {
                // on the foregrip: the same hold as a pistol grip, left-handed
                Vector3 fa = (L.grip == GripKind.Vertical ? Vector3.down : new Vector3(0f, -0.7f, 0.7f)).normalized;
                Vector3 c = mountUnder[i] + (L.grip == GripKind.Vertical ? new Vector3(0f, -0.05f, 0f) : new Vector3(0f, -0.028f, -0.008f));
                Vector3 sn = Vector3.ProjectOnPlane(Vector3.right, fa).normalized;
                sup = new GripSpec { used = true, centre = c, r = 0.016f, n = sn, z = Vector3.Cross(fa, sn), forearm = new Vector3(-0.35f, -0.55f, -0.75f), tight = 0.05f };
            }
            else if (i < supportCentre.Count && supportCentre[i] != Vector3.zero)
            {
                // under the handguard, palm up, fingers curling up the far (right) side, thumb along the near side
                // pointing forward, the forearm running back and down to the elbow
                float r = i < supportRadius.Count ? supportRadius[i] : 0.025f;
                sup = new GripSpec { used = true, centre = supportCentre[i], r = r, n = new Vector3(0.5f, 1f, 0f), z = new Vector3(1f, 0.25f, 0.3f), forearm = new Vector3(-0.35f, -0.6f, -0.72f), tight = 0.45f, thumb = new Vector3(0.1f, 0.6f, 0.8f) };
            }
        }

        /// <summary>The weight of an arm hanging off the gun (its hand, and part of its forearm and upper arm).</summary>
        static float ArmLoad(BodyPart hand)
        {
            if (hand == null || hand.rb == null) return 0f;
            float m = hand.rb.mass;
            if (hand.parent != null && hand.parent.rb != null) { m += hand.parent.rb.mass * 0.6f; if (hand.parent.parent != null && hand.parent.parent.rb != null) m += hand.parent.parent.rb.mass * 0.25f; }
            return m;
        }

        void ReleaseHands()
        {


            jointR = jointL = null; jointedR = jointedL = null;
            SetHandShown(visR, false); SetHandShown(visL, false);
            rigKey = null;
        }

        // the gun's real shape for the hands to close on: its meshes (with whatever is fitted to it) baked into one
        // collider in the gun's own space, parked far out of the world where nothing else touches it
        MeshCollider probe;
        Mesh probeMesh;
        static readonly Vector3 ProbeAt = new Vector3(0f, -800f, 0f);
        const int ProbeLayer = 30;
        System.Collections.Generic.List<(Vector3 a, Vector3 b, float r)> otherCaps;
        float capsSoft = 1f;

        void BuildProbe()
        {
            var parts = new List<CombineInstance>();
            Matrix4x4 inv = vmRoot.worldToLocalMatrix;
            foreach (var mf in models[current].GetComponentsInChildren<MeshFilter>(false))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || flashParts.Contains(mr) || mf.GetComponentInParent<PoseHand>() != null) continue;
                if (mf.name == "Beam" || mf.name.Contains("Hand")) continue;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    parts.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = inv * mf.transform.localToWorldMatrix });
            }
            if (probe == null)
            {
                var go = new GameObject("GunShapeProbe") { layer = ProbeLayer };
                go.transform.position = ProbeAt;
                probe = go.AddComponent<MeshCollider>();
                probeMesh = new Mesh { name = "GunShape", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            }
            probeMesh.Clear();
            probeMesh.CombineMeshes(parts.ToArray(), true, true);
            probe.sharedMesh = null; probe.sharedMesh = probeMesh;
            Physics.SyncTransforms();
        }

        bool ProbeBlocked(Vector3 a, Vector3 b, float r)
        {
            if (probe != null && Physics.CheckCapsule(ProbeAt + a, ProbeAt + b, r, 1 << ProbeLayer, QueryTriggerInteraction.Ignore)) return true;
            if (otherCaps != null)
                // (the other hand is soft: fingers wrapped over it press into it a little rather than stopping dead on it)
                foreach (var c in otherCaps) if (SegDist(a, b, c.a, c.b) < (r + c.r) * capsSoft) return true;
            return false;
        }

        /// <summary>The closest two segments come to each other.</summary>
        static float SegDist(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r), s, t;
            if (a < 1e-9f && e < 1e-9f) return r.magnitude;
            if (a < 1e-9f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e < 1e-9f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), den = a * e - b * b;
                    s = den > 1e-9f ? Mathf.Clamp01((b * f - c * e) / den) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
        }

        /// <summary>
        /// Put the hands on the gun: the gun goes to where it is held, each hand is placed on its grip (the way
        /// the hand shape says it closes there), and a joint ties them.
        /// </summary>
        void Rig()
        {
            ReleaseHands();
            var me = PlayerAvatar.Body;
            if (me == null || current >= models.Count) return;
            Hands(out var gh, out var sh, out _, out _);
            GripSpecs(current, out var gs, out var ss);
            var d = Current;
            gunRb.mass = GunMass(d);
            // the collider: the gun's own mesh bounds (not the hands, not the attachments)
            foreach (var mf in models[current].GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.StartsWith("Gun_") && mf.sharedMesh != null)
                {
                    var b = mf.sharedMesh.bounds;
                    gunCol.center = vmRoot.InverseTransformPoint(mf.transform.TransformPoint(b.center));
                    gunCol.size = Vector3.Scale(b.size, mf.transform.lossyScale) * 0.9f;
                    break;
                }
            foreach (var bp in me.partList) foreach (var c in bp.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(gunCol, c, true);
            if (!gunInit) SnapGun();
            if (visR == null) { visR = PoseHand.Build(null, 1, HandSkin(), "YourRightHand"); visL = PoseHand.Build(null, -1, HandSkin(), "YourLeftHand"); }
            // the right hand on the grip (or the left, if the right arm is gone)
            BuildProbe();
            otherCaps = null;
            if (gh != null && gs.used) { var vis = gh.id == PartId.HandR ? visR : visL; var j = Attach(gh, gs, vis); if (gh.id == PartId.HandR) { jointR = j; jointedR = gh; } else { jointL = j; jointedL = gh; } }
            // the support hand closes round the gun and round the shooting hand where it overlaps it
            otherCaps = gh != null && gs.used ? (gh.id == PartId.HandR ? visR : visL).Caps : null;
            capsSoft = IsPistol(d) ? 0.5f : 1f;   // a pistol support hand wraps right over the shooting hand fingers
            if (sh != null && ss.used) { jointL = Attach(sh, ss, visL); jointedL = sh; }
            otherCaps = null;
            rigKey = RigKey();
        }

        string RigKey()
        {
            var me = PlayerAvatar.Body;
            var L = current < loadouts.Length ? loadouts[current] : null;
            bool r = me.physio.ArmUsable(1) && me[PartId.HandR] != null && !me[PartId.HandR].severed;
            bool l = me.physio.ArmUsable(-1) && me[PartId.HandL] != null && !me[PartId.HandL].severed;
            return current + "|" + r + l + "|" + (L != null ? L.grip.ToString() : "") + "|" + me.number + "_" + me.spawnTime + "|" + carrying;
        }

        Hold Attach(BodyPart hand, GripSpec g, PoseHand vis)
        {
            // shape the hand round the grip in the gun's own space
            vis.transform.SetParent(vmRoot, false);
            vis.side = hand.id == PartId.HandR ? 1 : -1;
            vis.Grip(g.centre, g.r, g.n, g.z, g.forearm, g.tight, g.trigger, g.thumb, ProbeBlocked);
            // the physical hand to match: its wrist where the shaped hand's wrist is, its fingers (local down) along
            // the fingers, its palm (toward the body when the arm hangs) toward the grip
            Vector3 n = g.n.normalized, z = Vector3.ProjectOnPlane(g.z, n).normalized;
            Quaternion local = vis.side > 0 ? Quaternion.LookRotation(Vector3.Cross(n, z), -z) : Quaternion.LookRotation(-Vector3.Cross(n, z), -z);
            Quaternion rot = vmRoot.rotation * local;
            Vector3 wristW = vmRoot.TransformPoint(vis.WristLocal);
            Vector3 anchor = hand.joint != null ? hand.joint.anchor : Vector3.zero;
            Vector3 pos = wristW - rot * anchor;
            hand.rb.position = pos; hand.rb.rotation = rot;
            hand.transform.SetPositionAndRotation(pos, rot);
            hand.rb.linearVelocity = simVel; hand.rb.angularVelocity = Vector3.zero;
            // the shape rides on the hand from now on
            vis.transform.SetParent(null, true);
            if (vis.side > 0) { visRelPosR = vmRoot.InverseTransformPoint(vis.transform.position); visRelRotR = Quaternion.Inverse(vmRoot.rotation) * vis.transform.rotation; }
            else { visRelPosL = vmRoot.InverseTransformPoint(vis.transform.position); visRelRotL = Quaternion.Inverse(vmRoot.rotation) * vis.transform.rotation; }
            SetHandShown(vis, true);
            // the grip (HoldPhysics): this point of the hand is sprung to this point of the gun, turned as it is now
            return new Hold
            {
                hand = hand,
                // held by the wrist: the forearm always arrives where the locked hand shape begins
                gunLocal = vis.WristLocal,
                handLocal = anchor,
                relRot = Quaternion.Inverse(vmRoot.rotation) * hand.transform.rotation,
            };
        }

        /// <summary>
        /// The frame the gun is held in: your view - except that looking far down, the gun stops following it and
        /// stays held low out in front of you (you look down at it and your arms, instead of the gun swinging back
        /// into your chest and your forearms filling the screen). Aiming down the sights it follows further.
        /// </summary>
        /// <summary>
        /// Your body's own eyes, which the gun is held in front of: the camera, except while flying the free camera,
        /// when the gun stays with your body (where your head is, looking where you last looked) instead of
        /// following the camera off and dragging your arms after it.
        /// </summary>
        Transform Eye => pc != null && pc.flying && PlayerAvatar.Body != null && PlayerAvatar.EyeT != null ? PlayerAvatar.EyeT : cam.transform;

        Quaternion HoldFrame()
        {
            Quaternion r = Eye.rotation;
            float down = -Mathf.Asin(Mathf.Clamp((r * Vector3.forward).y, -1f, 1f)) * Mathf.Rad2Deg;
            float maxDown = Mathf.Lerp(50f, 80f, aimT);
            if (down <= maxDown) return r;
            return Quaternion.AngleAxis(-(down - maxDown), r * Vector3.right) * r;
        }

        /// <summary>Straight to where it is held (on first holding it, or after a teleport).</summary>
        void SnapGun()
        {
            snaps++;
            Quaternion hf = HoldFrame();
            Vector3 eye = Eye.position;
            offPos = tgtPos; offRot = tgtRot; offVel = offAng = Vector3.zero;
            simPos = eye + hf * offPos; simRot = hf * offRot; simVel = simAng = Vector3.zero;
            lastEyeP = eye; lastHf = hf; lastSimPos = simPos; lastSimRot = simRot;
            gunRb.position = simPos; gunRb.rotation = simRot;
            vmRoot.SetPositionAndRotation(simPos, simRot);
            lastTp = simPos; lastTpOk = true;
            gunInit = gInit = true;
        }

        // The gun is held in front of your eyes, so it is simulated THERE: as an offset from your eyes (offPos /
        // offRot, in the frame the gun is held in), sprung to where the arms hold it. Walking, turning and leaning
        // carry it along with you exactly; only a little of each movement is left behind as sway, which the arms
        // then take back up. Recoil, a knock, weak or shaking arms move it about its place, never out of a small
        // box round it - so it can't be left behind as you walk into it, or sink back into your chest however hurt
        // you are. simPos / simRot are where that puts it in the world (for the hands and the physics).
        Vector3 simPos, simVel, simAng; Quaternion simRot = Quaternion.identity;
        Vector3 offPos, offVel, offAng; Quaternion offRot = Quaternion.identity;
        Vector3 lastEyeP, lastSimPos; Quaternion lastHf = Quaternion.identity, lastSimRot = Quaternion.identity;
        const float SwayTurn = 0.1f, SwayWalk = 0.06f;   // how much of a turn / a step the gun lags behind before the arms catch it

        int simWeapon = -1;
        Vector3 heldAt;   // where the arms want it this step (eye frame), for the playtest

        /// <summary>
        /// The gun is never inside you: points along it from the grip to the muzzle (the bore line and the
        /// underside: magazine, handguard) are checked against your torso as it is now (a capsule from the pelvis
        /// up through the chest), and the gun is pushed straight out of it - however you turn, walk, look down or
        /// however weak your arms are. (The stock behind the grip belongs against your shoulder.)
        /// </summary>
        void KeepOutOfTorso(HumanBody me, Vector3 eye, Quaternion hf, Quaternion inv) => PushOutOfTorso(me, eye, hf, inv, ref offPos, true);

        // points spread over each gun's own mesh, from a little ahead of the grip on (what must stay out of you)
        readonly Dictionary<int, Vector3[]> torsoProbes = new Dictionary<int, Vector3[]>();
        Vector3[] TorsoProbes(int i)
        {
            if (torsoProbes.TryGetValue(i, out var p)) return p;
            if (i >= models.Count || models[i] == null || i >= gripPts.Count) return null;
            var list = new List<Vector3>();
            foreach (var mf in models[i].GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("Gun_") || mf.sharedMesh == null) continue;
                var v = mf.sharedMesh.vertices;
                var keep = new List<Vector3>();
                for (int k = 0; k < v.Length; k++)
                {
                    Vector3 lp = vmRoot.InverseTransformPoint(mf.transform.TransformPoint(v[k]));
                    if (lp.z > gripPts[i].z + 0.05f) keep.Add(lp);
                }
                int step = Mathf.Max(1, keep.Count / 60);
                for (int k = 0; k < keep.Count; k += step) list.Add(keep[k]);
                // and the extremes: the bottom of the magazine, the sides of the handguard
                if (keep.Count > 0)
                {
                    Vector3 lo = keep[0], hiX = keep[0], loX = keep[0];
                    foreach (var q in keep) { if (q.y < lo.y) lo = q; if (q.x > hiX.x) hiX = q; if (q.x < loX.x) loX = q; }
                    list.Add(lo); list.Add(hiX); list.Add(loX);
                }
                break;
            }
            p = list.Count > 0 ? list.ToArray() : null;
            torsoProbes[i] = p;
            return p;
        }

        void PushOutOfTorso(HumanBody me, Vector3 eye, Quaternion hf, Quaternion inv, ref Vector3 op, bool sim)
        {
            if (me == null || me.Pelvis == null || me.Chest == null || current >= gripPts.Count) return;
            Vector3 tA = me.Pelvis.transform.position, tB = me.Chest.transform.position + me.Chest.transform.up * 0.12f;
            Vector3 ab = tB - tA;
            const float R = 0.145f;                   // (the chest is about 0.13 deep in front of its middle, and a margin)
            var probes = TorsoProbes(current);
            if (probes == null) return;
            for (int it = 0; it < 3; it++)
            {
                Vector3 pos = eye + hf * op; Quaternion rot = hf * offRot;
                float worst = 0f; Vector3 dir = Vector3.zero;
                foreach (var lp in probes)
                {
                    Vector3 w = pos + rot * lp;
                    float t = Mathf.Clamp01(Vector3.Dot(w - tA, ab) / ab.sqrMagnitude);
                    Vector3 d = w - (tA + ab * t);
                    d = Vector3.ProjectOnPlane(d, ab.normalized);
                    float dist = d.magnitude;
                    if (R - dist > worst) { worst = R - dist; dir = dist > 1e-4f ? d / dist : Vector3.ProjectOnPlane(Eye.forward, ab.normalized).normalized; }
                }
                if (worst <= 0f) return;
                Vector3 pushL = inv * (dir * worst);
                op += pushL;
                if (!sim) continue;
                float into = Vector3.Dot(offVel, -pushL.normalized);
                if (into > 0f) offVel += pushL.normalized * into;
            }
        }

        /// <summary>The held gun's pose in the world from its offset and your eyes now.</summary>
        void GunPose(out Vector3 pos, out Quaternion rot)
        {
            Quaternion hf = HoldFrame();
            // (out of your body as it is this frame too: the camera turns at once, your chest a moment later)
            Vector3 op = offPos;
            PushOutOfTorso(PlayerAvatar.Body, Eye.position, hf, Quaternion.Inverse(hf), ref op, false);
            pos = Eye.position + hf * op;
            rot = hf * offRot;
        }

        /// <summary>Physics step: the gun moves where the arms take it, and the hands go with it.</summary>
        void HoldPhysics()
        {
            if (!BodyHeld || !Armed || gunRb == null || !gunInit) return;
            // another weapon in the hands: it starts where it is held (not flung there from the last one)
            if (simWeapon != current) { simWeapon = current; SnapGun(); return; }
            float dt = Time.fixedDeltaTime;
            Quaternion hf = HoldFrame();
            Vector3 eye = Eye.position;
            Quaternion inv = Quaternion.Inverse(hf);
            // where the arms want it (in the eye's frame), pulled in when the muzzle would go into a wall
            Vector3 tp = eye + hf * tgtPos;
            Quaternion tr = hf * tgtRot;
            var d = Current;
            if (d.type == WeaponType.Gun) Tuck(ref tp, ref tr);
            Vector3 tpL = inv * (tp - eye);
            heldAt = tpL;
            Quaternion trL = inv * tr;
            // carried in one hand (getting up, going over): it is where the hand is; nothing pulls it to the eye
            if (carrying && CarriedPose(out var cp, out var cr))
            {
                simVel = (cp - lastSimPos) / dt; simAng = Vector3.zero;
                simPos = cp; simRot = cr;
                offPos = inv * (cp - eye); offRot = inv * cr; offVel = offAng = Vector3.zero;
                lastSimPos = simPos; lastSimRot = simRot; lastEyeP = eye; lastHf = hf;
                gunRb.MovePosition(simPos); gunRb.MoveRotation(simRot);
                return;
            }
            bool recovering = recoverT > 0f;
            if (recovering) recoverT -= dt;
            // a teleport, a respawn, a fall: straight back into the hands (not while it is being brought back up)
            if ((eye - lastEyeP).sqrMagnitude > 1f || !recovering && (Quaternion.Angle(offRot, trL) > 70f || (offPos - tpL).sqrMagnitude > 0.3f * 0.3f)) { SnapGun(); rigKey = null; return; }

            // carried with you: a little of this step's turn and walk is left behind as sway
            Quaternion turn = inv * lastHf;                                   // last step's frame, seen from this one
            Quaternion lag = Quaternion.Slerp(Quaternion.identity, turn, SwayTurn);
            offPos = tpL + lag * (offPos - tpL);
            offRot = lag * offRot;
            offPos -= inv * (eye - lastEyeP) * SwayWalk;

            Hands(out var gh, out var sh, out float ga, out float sa);
            var me = PlayerAvatar.Body;
            float awake = Mathf.Clamp01(me.physio.consciousness * 1.3f);
            bool melee = d.type != WeaponType.Gun;
            float heavy = melee ? 0.7f : Mathf.Clamp(d.recoil / 3f, 0.55f, 2.4f);
            float hold = (ga * 0.7f + sa * (TwoHandedGun(d) ? 0.6f : 0.25f)) * awake;
            if (gh == null) hold = 0f;
            float kp = (melee ? 900f : 560f) * Mathf.Max(0.05f, hold) / heavy;
            float kr = (melee ? 900f : 460f) * Mathf.Max(0.05f, TwoHandedGun(d) ? ga * 0.5f + sa * 0.8f : ga) * awake / heavy;
            // aiming down the sights: cheek on the stock, the sights held on the eye - much stiffer, only the shakes
            float ads = melee ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Max(Mathf.Max(aimT, pointT * 0.5f), pointAimT * 0.9f));
            kp *= 1f + 5f * ads; kr *= 1f + 6f * ads;
            // held out round a corner (ALT+A / ALT+D): no shoulder behind it, arms out - it can't be held still, every
            // shot throws it about and it comes back slowly
            kp *= Mathf.Lerp(1f, 0.25f, blindT); kr *= Mathf.Lerp(1f, 0.2f, blindT);
            kp = Mathf.Max(kp, 60f); kr = Mathf.Max(kr, 40f);              // (even limp arms keep it roughly up)
            float dp = 2f * Mathf.Sqrt(kp) * 0.85f, dr = 2f * Mathf.Sqrt(kr) * 0.85f;

            // (the springs below are stepped in their own small steps; this is everything else pushing on it)
            Vector3 acc = Vector3.zero, angAcc = Vector3.zero;
            // weak arms can't hold the weight up: it sags and the muzzle droops
            float weak = (1f - Mathf.Clamp01(hold)) * (1f - 0.85f * ads);
            acc += (inv * Vector3.down) * 9.81f * weak * 0.35f * heavy;
            angAcc += (offRot * Vector3.right) * (weak * heavy * 6f);
            // shaking: pain, blood loss, damaged arms, the brain
            float rush = PlayerBody.I != null ? PlayerBody.I.Rush : 0f;
            float tremor = (Mathf.Max(0f, (PlayerBody.I != null ? PlayerBody.I.Tremor : 0f) - rush * 0.75f) + (1f - ga) * 0.4f) * (1f - 0.5f * ads);
            float t = Time.time;
            acc += new Vector3(Mathf.PerlinNoise(t * 7f, 3f) - 0.5f, Mathf.PerlinNoise(5f, t * 7f) - 0.5f, 0f) * tremor * 14f;
            angAcc += new Vector3(Mathf.PerlinNoise(t * 6f, 11f) - 0.5f, Mathf.PerlinNoise(13f, t * 6f) - 0.5f, 0f) * tremor * 10f;
            // adrenaline: a fast, jittery shake in the hands that aiming down the sights doesn't steady - the
            // tighter hold only makes the muzzle buzz harder on the target
            float jitter = rush * (0.35f + 0.9f * ads);
            acc += new Vector3(Mathf.PerlinNoise(t * 14f, 21f) - 0.5f, Mathf.PerlinNoise(23f, t * 14f) - 0.5f, 0f) * jitter * 30f;
            angAcc += new Vector3(Mathf.PerlinNoise(t * 12f, 31f) - 0.5f, Mathf.PerlinNoise(37f, t * 12f) - 0.5f, Mathf.PerlinNoise(t * 9f, 41f) - 0.5f) * jitter * 34f;
            // the hold itself: stiff springs (very stiff aiming down the sights), stepped in pieces of 5 ms or less -
            // in one long physics step (60 or 80 a second) they overshoot and the gun rings on the sights
            int subs = Mathf.Max(1, Mathf.CeilToInt(dt / 0.005f));
            float h = dt / subs;
            for (int k = 0; k < subs; k++)
            {
                Vector3 a = (tpL - offPos) * kp - offVel * dp + acc;
                (trL * Quaternion.Inverse(offRot)).ToAngleAxis(out float ang, out Vector3 ax);
                if (ang > 180f) ang -= 360f;
                if (!float.IsFinite(ax.x)) ax = Vector3.zero;
                Vector3 aa = ax * (ang * Mathf.Deg2Rad) * kr - offAng * dr + angAcc;
                offVel = Vector3.ClampMagnitude(offVel + a * h, 4f);
                offAng = Vector3.ClampMagnitude(offAng + aa * h, 22f);
                offPos += offVel * h;
                float w = offAng.magnitude;
                if (w > 1e-5f) offRot = Quaternion.AngleAxis(w * Mathf.Rad2Deg * h, offAng / w) * offRot;
                offRot.Normalize();
            }
            // never out of a small box round where it is held: not back into your chest, not off to the side
            // (a blade or a grenade goes wherever the swing takes it)
            Vector3 dv = melee || recovering ? Vector3.zero : offPos - tpL;
            Vector3 lo = new Vector3(-0.06f, -0.1f, -0.05f), hi = new Vector3(0.06f, 0.06f, 0.05f);
            Vector3 cl = new Vector3(Mathf.Clamp(dv.x, lo.x, hi.x), Mathf.Clamp(dv.y, lo.y, hi.y), Mathf.Clamp(dv.z, lo.z, hi.z));
            if (cl != dv)
            {
                for (int k = 0; k < 3; k++) if (cl[k] != dv[k] && Mathf.Sign(offVel[k]) == Mathf.Sign(dv[k] - cl[k])) offVel[k] = 0f;
                offPos = tpL + cl;
            }
            if (!melee && !recovering && Quaternion.Angle(offRot, trL) > 15f) offRot = Quaternion.RotateTowards(trL, offRot, 15f);
            KeepOutOfTorso(me, eye, hf, inv);

            // where that is in the world, and how fast it is going there (for the hands)
            simPos = eye + hf * offPos;
            simRot = hf * offRot;
            simVel = (simPos - lastSimPos) / dt;
            (simRot * Quaternion.Inverse(lastSimRot)).ToAngleAxis(out float wa, out Vector3 wx);
            if (wa > 180f) wa -= 360f;
            simAng = float.IsFinite(wx.x) ? wx * (wa * Mathf.Deg2Rad / dt) : Vector3.zero;
            lastSimPos = simPos; lastSimRot = simRot; lastEyeP = eye; lastHf = hf;
            gunRb.MovePosition(simPos);
            gunRb.MoveRotation(simRot);

            // the grip: each hand's holding point sprung onto its place on the gun, and the hand turned the way it
            // closes on it - firm, but a spring, so a hand gives a little under recoil, a knock or a swing and comes
            // back, and the wrist joint behind it bends. Exactly the opposite push goes into your chest: arms
            // holding a gun push on the shoulders, they can't move the body as a whole.
            var chest = me.Chest != null ? me.Chest.rb : null;
            GripSpring(jointR, ga * awake, chest);
            GripSpring(jointL, sa * awake, chest);
        }

        void GripSpring(Hold h, float strength, Rigidbody chest)
        {
            if (h == null || h.hand == null || h.hand.rb == null || h.hand.severed) return;
            var rb = h.hand.rb;
            float dt = Time.fixedDeltaTime;
            float k = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(strength));
            // (reloading, clearing a jam: the support hand goes where the move takes it - WeaponMoves)
            bool working = SupportTarget(h, out var moveTo);
            Vector3 target = working ? moveTo : simPos + simRot * h.gunLocal;
            Vector3 at = h.hand.transform.TransformPoint(h.handLocal);
            Vector3 gunVelAt = simVel + Vector3.Cross(simAng, target - simPos);
            Vector3 e = target - at;
            // knocked off the grip (a hard knock, a fall, a teleport): straight back on it, closed round it again
            if (e.sqrMagnitude > 0.12f * 0.12f && !working)
            {
                GripSnaps++;
                rb.position = target - rb.rotation * h.handLocal;
                h.hand.transform.SetPositionAndRotation(rb.position, rb.rotation);
                rb.linearVelocity = gunVelAt; rb.angularVelocity = simAng;
                return;
            }
            // each step: close part of the gap and most of the difference in speed (a stiff hold that can't ring
            // at any frame rate), and carry the arm's own weight
            Vector3 dv = e * (0.3f * k / dt) + (gunVelAt - rb.GetPointVelocity(at)) * (0.6f * k);
            Vector3 f = rb.mass * dv / dt - Physics.gravity * (ArmLoad(h.hand) - rb.mass * 0f);
            // (no more than an arm can pull: swung round hard, the hand trails a little rather than wrenching the
            // body over - and exactly that much back into your chest, so the arms never move you as a whole)
            f = Vector3.ClampMagnitude(f, 200f);
            rb.AddForceAtPosition(f, at, ForceMode.Force);
            if (chest != null) chest.AddForce(-f, ForceMode.Force);
            // the hand turned on the grip the way it closes round it
            Quaternion want = simRot * h.relRot;
            (want * Quaternion.Inverse(rb.rotation)).ToAngleAxis(out float ang, out Vector3 ax);
            if (ang > 180f) ang -= 360f;
            if (!float.IsFinite(ax.x)) return;
            // (only a gentle turn: the hand shape is locked on the gun anyway, and wrenching the wrist past what it
            // can reach just twists the forearm)
            Vector3 dw = ax * (ang * Mathf.Deg2Rad) * (0.08f * k / dt) + (simAng - rb.angularVelocity) * (0.4f * k);
            dw = Vector3.ClampMagnitude(dw, 40f);
            rb.AddTorque(dw / dt, ForceMode.Acceleration);
            Vector3 it = rb.inertiaTensor;
            if (chest != null) chest.AddTorque(-dw / dt * ((it.x + it.y + it.z) / 3f), ForceMode.Force);
        }

        /// <summary>Recoil, a shove: straight into the gun's motion.</summary>
        void KickGun(Vector3 dv, Vector3 dw) { var inv = Quaternion.Inverse(HoldFrame()); offVel += inv * dv; offAng += inv * dw; }


        // ---- your arms as you see them
        // The arms you see while holding something are solved every frame as real arms: the shoulder rides on your
        // physical chest (so it bobs, turns and leans with your body as you walk), the upper arm and forearm are
        // your own body's (same mesh, skin and wounds), hinged at an elbow that only bends one way and hangs down
        // and out the way elbows do, and the wrist bridges from the end of the forearm into the heel of the hand
        // on the gun. Nothing can fold backwards, pass through itself, slide into the wrist or swing up into your
        // face. The physical arms, with all their joints, are still there under them (hidden from your own eyes)
        // carrying the gun's weight, taking hits and bumping into walls.
        const float WristLen = 0.032f;      // from the end of the forearm bones to the heel of the palm
        bool armsDrawn;
        public bool ArmsDrawn => armsDrawn;

        void DrawArms()
        {
            armsDrawn = false;
            dbgOn[0] = dbgOn[1] = false;
            var me = PlayerAvatar.Body;
            var hR = jointedR != null && jointedR.id == PartId.HandR ? jointedR : null;
            var hL = jointedL ?? (jointedR != null && jointedR.id == PartId.HandL ? jointedR : null);
            bool show = me != null && !pc.flying;   // (from the free camera you see your real, physical arms)
            if (visR != null) { if (!show) SetHandShown(visR, false); else if (hR != null && !gunHidden) SetHandShown(visR, true); }
            if (visL != null) { if (!show) SetHandShown(visL, false); else if (hL != null && !gunHidden) SetHandShown(visL, true); }
            bool r = show && !gunHidden && DrawArm(me, visR, hR, 1);
            bool l = show && !gunHidden && DrawArm(me, visL, hL, 0);
            if (visR != null && !r) visR.Wrist(null);
            if (visL != null && !l) visL.Wrist(null);
            // a hand shape not on the gun this time (one-handed: carried, a pistol held in the other hand) is hidden,
            // so there is never a spare hand left floating where it was
            if (visR != null && hR == null) SetHandShown(visR, false);
            if (visL != null && hL == null) SetHandShown(visL, false);
            armsDrawn = show && (hR != null || hL != null);
            armDrawnR = show && hR != null; armDrawnL = show && hL != null;
        }

        bool DrawArm(HumanBody me, PoseHand vis, BodyPart hand, int slot)
        {
            if (vis == null || hand == null || hand.severed || hand.joint == null) return false;
            var fa = hand.parent; var ua = fa != null ? fa.parent : null;
            if (fa == null || ua == null || fa.severed || ua.severed || fa.joint == null || ua.joint == null || ua.joint.connectedBody == null) return false;
            float s = hand.id == PartId.HandR ? 1f : -1f;
            Transform eye = Eye;
            // the hand on the gun: where its heel is, which way it runs (wrist to knuckles), which way the
            // forearm should leave it for this hold
            Vector3 heel = vis.transform.TransformPoint(vis.WristLocal);
            Vector3 handDir = vis.transform.TransformDirection(vis.PalmZ).normalized;
            Vector3 hint = -vis.transform.TransformDirection(vis.ForearmLocal).normalized;   // forearm running INTO the wrist
            // the bones (this body's own proportions)
            Vector3 eUA = fa.joint.connectedAnchor, sUA = ua.joint.anchor;        // in the upper arm
            Vector3 eFA = fa.joint.anchor, wFA = hand.joint.connectedAnchor;       // in the forearm
            float L1 = Vector3.Scale(sUA - eUA, ua.transform.lossyScale).magnitude;
            float L2 = Vector3.Scale(eFA - wFA, fa.transform.lossyScale).magnitude;
            Vector3 S = ua.joint.connectedBody.transform.TransformPoint(ua.joint.connectedAnchor);
            // (the shoulder mostly where your body is turning to: turning fast, your chest follows the view a moment
            // later, and arms drawn from where it lags would cut through it to the gun)
            Vector3 flatF = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            if (flatF.sqrMagnitude > 0.5f && me.Chest != null)
            {
                Quaternion turned = Quaternion.FromToRotation(Vector3.ProjectOnPlane(me.Chest.transform.forward, Vector3.up).normalized, flatF);
                Vector3 piv = me.Chest.transform.position;
                S = Vector3.Lerp(S, piv + turned * (S - piv), 0.7f);
            }
            Vector3 up = eye.up, fwd = eye.forward;
            Vector3 tA = me.Pelvis != null ? me.Pelvis.transform.position : S - up * 0.4f;
            Vector3 tB = me.Chest != null ? me.Chest.transform.position + me.Chest.transform.up * 0.1f : S - up * 0.1f;
            // A real arm keeps the wrist nearly straight and lets the shoulder and elbow go where they must: so the
            // FOREARM is chosen first - in line with the hand as near as it goes - and the elbow put behind it.
            // Directions round that line are tried in widening rings; each is scored on the bend at the wrist, the
            // upper arm still reaching the shoulder (it can't stretch or shrink), the elbow not up over the
            // shoulder, neither bone through your body, and not jumping from where it was.
            Vector3 ideal = (handDir * 0.5f + hint * 0.5f).normalized;
            Vector3 q0 = Vector3.Cross(ideal, up); if (q0.sqrMagnitude < 1e-4f) q0 = Vector3.Cross(ideal, fwd); q0.Normalize();
            Vector3 q1 = Vector3.Cross(ideal, q0);
            Vector3 prev = armFore[slot].sqrMagnitude > 0.5f ? armFore[slot] : ideal;
            float bestCost = float.MaxValue; Vector3 bestF = ideal;
            for (int r = 0; r <= 9; r++)
            {
                float tilt = r * 9f;
                int n = r == 0 ? 1 : 16;
                for (int k = 0; k < n; k++)
                {
                    float az = k * 22.5f * Mathf.Deg2Rad;
                    Vector3 f = Quaternion.AngleAxis(tilt, q0 * Mathf.Cos(az) + q1 * Mathf.Sin(az)) * ideal;
                    Vector3 Wc = heel - (f + handDir).normalized * WristLen;
                    Vector3 E = Wc - f * L2;
                    float cost = tilt * tilt;
                    float reach = (E - S).magnitude - L1;                                   // upper arm too short (+) / too long (-)
                    cost += reach * reach * 4e5f;
                    float high = Vector3.Dot(E - S, up);
                    if (high > 0.04f) cost += (high - 0.04f) * (high - 0.04f) * 2.5e6f;     // elbow up above the shoulder
                    for (int t = 1; t <= 4; t++)
                    {
                        float dF = DistToSeg(Vector3.Lerp(E, Wc, t * 0.25f), tA, tB);
                        if (dF < 0.17f) cost += (0.17f - dF) * (0.17f - dF) * 2e6f;          // forearm through your body
                        float dU = DistToSeg(Vector3.Lerp(S, E, 0.3f + t * 0.175f), tA, tB);
                        if (dU < 0.15f) cost += (0.15f - dU) * (0.15f - dU) * 2e6f;          // upper arm through it
                    }
                    if (Vector3.Dot(f, fwd) < -0.3f) cost += 3000f;                            // forearm pointing back at you
                    float jump = Vector3.Angle(f, prev);
                    cost += jump * jump * 0.3f;
                    if (cost < bestCost) { bestCost = cost; bestF = f; }
                }
            }
            // swung there smoothly (fast, but never a jump)
            Vector3 fwdArm = Vector3.Slerp(prev, bestF, 1f - Mathf.Exp(-Mathf.Max(Time.deltaTime, 0.004f) * 30f)).normalized;
            armFore[slot] = fwdArm;
            Vector3 W = heel - (fwdArm + handDir).normalized * WristLen;   // the wrist bridge splits the bend between its ends
            Vector3 El = W - fwdArm * L2;
            // the upper arm from the elbow to the shoulder joint
            Vector3 Sd = S;   // (the solver keeps the elbow where the upper arm about fits; any difference is taken up along it)
            Vector3 roll = Vector3.Cross(fwdArm, up); if (roll.sqrMagnitude < 1e-4f) roll = q0;
            DrawLimb(ua, sUA, eUA, Sd, El, roll);
            DrawLimb(fa, eFA, wFA, El, W, roll);
            vis.Wrist(W);
            dbgS[slot] = Sd; dbgE[slot] = El; dbgW[slot] = W; dbgHeel[slot] = heel; dbgWrist[slot] = Vector3.Angle(fwdArm, handDir); dbgOn[slot] = true;
            return true;
        }

        static float DistToSeg(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }
        readonly float[] armAng = new float[2];
        readonly Vector3[] dbgS = new Vector3[2], dbgE = new Vector3[2], dbgW = new Vector3[2], dbgHeel = new Vector3[2];
        readonly float[] dbgWrist = new float[2];
        readonly bool[] dbgOn = new bool[2];
        int snaps;
        /// <summary>For the playtests: a hand knocked off the grip and put straight back on it.</summary>
        public static int GripSnaps;

        /// <summary>What the automated playtest (ArmTest) measures: the arms as drawn, the gun and its offset.</summary>
        public struct HoldDebug
        {
            public bool held; public int weapon, snaps;
            public bool[] armOn; public Vector3[] S, E, W, heel; public float[] wrist;
            public Vector3 gunVel, eyeVel; public float offErr; public Transform gun; public MeshFilter gunMesh; public float gripZ;
        }
        public HoldDebug GetHoldDebug() => new HoldDebug
        {
            held = BodyHeld && gInit && Armed, weapon = current, snaps = snaps, armOn = dbgOn, S = dbgS, E = dbgE, W = dbgW, heel = dbgHeel, wrist = dbgWrist,
            gunVel = simVel, offErr = (offPos - heldAt).magnitude, gun = current < models.Count && models[current] != null ? models[current].transform : null,
            gunMesh = current < models.Count && models[current] != null ? models[current].GetComponentInChildren<MeshFilter>() : null,
            gripZ = current < gripPts.Count ? gripPts[current].z : 0f,
        };
        readonly Vector3[] armFore = new Vector3[2];

        /// <summary>Draw a body part's own mesh stretched between two points: its joint at `pLocal` on P, `dLocal` on D.</summary>
        static void DrawLimb(BodyPart bp, Vector3 pLocal, Vector3 dLocal, Vector3 P, Vector3 D, Vector3 roll)
        {
            if (bp.skin == null) return;
            var mf = bp.skin.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            Vector3 axL = pLocal - dLocal; float lenL = axL.magnitude;
            Vector3 ax = P - D; float len = ax.magnitude;
            if (lenL < 1e-4f || len < 1e-4f) return;
            // local frame: the bone's own axis turned onto the world one, rolled to face the bend
            Quaternion toY = Quaternion.FromToRotation(axL / lenL, Vector3.up);
            Vector3 z = Vector3.ProjectOnPlane(roll, ax);
            if (z.sqrMagnitude < 1e-6f) z = Vector3.ProjectOnPlane(Vector3.forward, ax);
            Quaternion R = Quaternion.LookRotation(z.normalized, ax / len) * toY;
            Vector3 sc = bp.transform.lossyScale; sc.y *= len / lenL;
            var M = Matrix4x4.TRS(Vector3.zero, R, sc);
            M = Matrix4x4.TRS(D - M.MultiplyPoint3x4(dLocal), R, sc);
            var mesh = mf.sharedMesh; var mats = bp.skin.sharedMaterials;
            for (int i = 0; i < mesh.subMeshCount && i < mats.Length; i++)
                Graphics.DrawMesh(mesh, M, mats[i], 0, null, i, null, UnityEngine.Rendering.ShadowCastingMode.On, true);
        }

        static void SetHandShown(PoseHand h, bool on)
        {
            if (h == null) return;
            foreach (var r in h.GetComponentsInChildren<MeshRenderer>(true)) if (r.enabled != on) r.enabled = on;
        }

        Material handSkin;
        Material HandSkin()
        {
            if (handSkin != null) return handSkin;
            Color c = new Color(0.86f, 0.71f, 0.58f);
            var me = PlayerAvatar.Body;
            if (me != null && me[PartId.HandR] != null && me[PartId.HandR].skinMaterial != null && me[PartId.HandR].skinMaterial.HasProperty("_BaseColor"))
                c = me[PartId.HandR].skinMaterial.GetColor("_BaseColor");
            handSkin = Mats.Lit(c, 0.25f);
            return handSkin;
        }

        /// <summary>How each gun's grip is shaped: the way its grip runs (down and back) and its half-thickness.</summary>
        static (Vector3 axis, float r) GripShape(string model)
        {
            switch (model)
            {
                // (the front strap of each grip as modelled: Glock 22 degrees of rake, the Beretta hardly any)
                case "Gun_Glock": return (new Vector3(0f, -0.928f, -0.371f), 0.0165f);
                case "Gun_Beretta": return (new Vector3(0f, -0.987f, -0.16f), 0.017f);
                case "Gun_Taser": return (new Vector3(0f, -0.93f, -0.36f), 0.0165f);
                case "Gun_AWP": return (new Vector3(0f, -0.98f, -0.18f), 0.0175f);
                case "Gun_Barrett": return (new Vector3(0f, -0.963f, -0.27f), 0.0185f);
                case "Gun_PhysGun": return (new Vector3(0f, -0.95f, -0.3f), 0.017f);
                case "Gun_AR15": return (new Vector3(0f, -0.953f, -0.302f), 0.0175f);
                case "Gun_AK47": case "Gun_AK74": case "Gun_AK74M": case "Gun_AK105": case "Gun_AKS74U": case "Gun_RPK74": return (new Vector3(0f, -0.959f, -0.283f), 0.0175f);
                case "Gun_AK12": return (new Vector3(0f, -0.94f, -0.33f), 0.0175f);
                case "Gun_PKM": case "Gun_M249": return (new Vector3(0f, -0.959f, -0.283f), 0.019f);
                case "Gun_Shotgun": return (new Vector3(0f, -0.82f, -0.57f), 0.02f);   // the wrist of the stock
                default: return (new Vector3(0f, -0.92f, -0.38f), 0.0175f);
            }
        }

        static readonly Unity.Profiling.ProfilerMarker PM_LateUpdate = new Unity.Profiling.ProfilerMarker("Carl.WeaponController.LateUpdate");

        void LateUpdate()
        {

            using var pmScope = PM_LateUpdate.Auto();
            if (cam == null || vmRoot == null) return;
            armsDrawn = false;
            dbgOn[0] = dbgOn[1] = false;
            UpdatePendingCases();
            bool held = BodyHeld && Armed;
            var me = PlayerAvatar.Body;
            // getting up off the floor, or going over: the gun goes into one hand (which pushes off the floor with
            // it, or keeps it tucked in) and the other hand is free to push or to catch you
            bool gettingUp = me != null && (me.ragdoll.mode == BodyMode.GettingUp || (me.ragdoll.IsDown && me.ragdoll.wantStand));
            bool falling = me != null && me.ragdoll.Falling;
            bool wasCarrying = carrying;
            carrying = held && (gettingUp || falling);
            if (wasCarrying && !carrying) recoverT = 0.7f;
            if (!held)
            {
                if (jointR != null || jointL != null || rigKey != null) ReleaseHands();
                if (me != null) { me.ragdoll.armCompliance = 1f; me.ragdoll.braceArms = 3; me.ragdoll.tuckCarry = false; }
                if (gunRb != null && gunCol.enabled)
                {
                    // dead: let it fall out of your hands; otherwise just put it away
                    if (me != null && me.physio.dead) { if (gunRb.isKinematic) { gunRb.isKinematic = false; gunRb.linearVelocity = simVel; } }
                    else { gunRb.isKinematic = true; gunCol.enabled = false; gInit = gunInit = false; }
                }
                gInit = false;
                armDrawnR = armDrawnL = false;
                UpdateRailGear();
                return;
            }
            EnsureGunBody();
            if (rigKey != RigKey()) Rig();
            if (me != null)
            {
                // your arm muscles go soft round a held gun and let it lead; carrying it, the arm does its own work
                me.ragdoll.armCompliance = carrying ? 1f : jointedR != null || jointedL != null ? 0.15f : 1f;
                // the hand on the gun keeps it; the other one goes out to catch you
                var gh = GripHold != null ? GripHold.hand : null;
                me.ragdoll.braceArms = gh == null ? 3 : gh.id == PartId.HandR ? 1 : 2;
                me.ragdoll.tuckCarry = carrying && falling;
            }
            // drawn where your eyes are this frame (the physics step only says where it is relative to them) -
            // or, carried, where your hand is
            if (carrying && CarriedPose(out var cpos, out var crot)) vmRoot.SetPositionAndRotation(cpos, crot);
            else if (gInit && gunRb != null && gunRb.isKinematic && BodyHeld) { GunPose(out var gp, out var gr); vmRoot.SetPositionAndRotation(gp, gr); }
            // the hand shapes are locked on the gun as they were closed round it (never twisted off it or sunk into
            // it); the physical wrists are pulled to meet them, so the forearms still run into them
            if (jointedR != null && visR != null && jointedR.id == PartId.HandR) visR.transform.SetPositionAndRotation(vmRoot.TransformPoint(visRelPosR), vmRoot.rotation * visRelRotR);
            if ((jointedL != null || jointedR != null && jointedR.id == PartId.HandL) && visL != null)
            {
                // (reloading, clearing a jam: the hand is off doing the move - WeaponMoves)
                Vector3 moved = SupportVisual(out var dv) ? dv : Vector3.zero;
                if (moved == Vector3.zero) handAt = Vector3.zero;
                visL.transform.SetPositionAndRotation(vmRoot.TransformPoint(visRelPosL) + moved, vmRoot.rotation * visRelRotL);
            }
            // and your arms, from your shoulders to those hands
            DrawArms();
            UpdateMagVisual();
            UpdateRailGear();
            if (flash.activeSelf && current < muzzles.Count) { flash.transform.position = muzzles[current].position; flash.transform.rotation = muzzles[current].rotation; }
            muzzleLight.transform.position = muzzles[current].position;
        }

        /// <summary>The scope view hides the gun (and your hands on it), without taking it out of your hands.</summary>
        bool gunHidden;
        void ShowHeldGun(bool on)
        {
            gunHidden = !on;
            if (current < models.Count)
                foreach (var r in models[current].GetComponentsInChildren<Renderer>(true))
                    if (!floatHands.Contains(r) && r.enabled != on) r.enabled = on;
            if (jointedR != null || jointedL != null) { SetHandShown(visR, on && jointedR != null); SetHandShown(visL, on && jointedL != null); }
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

        /// <summary>Rounds lost clearing a jam (thrown out of the gun).</summary>
        public void LoseRounds(int idx, int n)
        {
            if (GameSettings.InfiniteAmmo || idx < 0 || idx >= ammo.Length) return;
            ammo[idx] = Mathf.Max(0, ammo[idx] - n);
        }

        void StartReload()
        {
            reloading = true;
            if (Current.name == "TASER" && TaserWires.Current != null) Destroy(TaserWires.Current.gameObject);   // spent cartridge off, wires and all
            reloadEnd = Time.unscaledTime + Current.reloadTime;
            Sfx.Reload(Current.name, cam.transform.position, Current.reloadTime);
            QueueReload(Current, Current.reloadTime);
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
            KickGun(-(gRot * Vector3.forward) * 0.15f, Vector3.zero);
            kick = Mathf.Min(1.5f, kick + 0.3f);
        }

        // ---- fired cases: the right cartridge for each gun, out of its port (the model's Eject point)
        readonly List<Vector3> ejectPts = new List<Vector3>();
        readonly List<(float t, int gun)> pendingCases = new List<(float, int)>();

        static CaseKind CaseFor(string model) => model switch
        {
            "Gun_Glock" => CaseKind.Nine, "Gun_Beretta" => CaseKind.Nine, "Gun_AR15" => CaseKind.FiveFiveSix, "Gun_AK47" => CaseKind.SevenSixTwo,
            "Gun_AWP" => CaseKind.ThreeThreeEight, "Gun_Barrett" => CaseKind.Fifty, "Gun_Shotgun" => CaseKind.TwelveGauge,
            _ => GunInfo.OfModel(model) is GunInfo.Info gi ? gi.casing : CaseKind.None,
        };

        void EjectCase(int i)
        {
            if (i >= ejectPts.Count || i >= models.Count || models[i] == null) return;
            var root = models[i].transform;
            Vector3 vel = BodyHeld && gInit ? simVel : Vector3.zero;
            using (Prof.Casing.Auto()) Casing.Eject(CaseFor(modelNames[i]), root.TransformPoint(ejectPts[i]), root.right, root.up, -root.forward, vel, gunCol != null && gunCol.enabled ? gunCol : null);
        }

        void UpdatePendingCases()
        {
            for (int k = pendingCases.Count - 1; k >= 0; k--)
            {
                if (Time.time < pendingCases[k].t) continue;
                if (pendingCases[k].gun == current && Armed) EjectCase(pendingCases[k].gun);
                pendingCases.RemoveAt(k);
            }
        }

        void FireGun(WeaponDef d)
        {
            nextFire = Time.unscaledTime + 60f / Rpm(current);
            if (burstLeft > 0) burstLeft--;
            // shooting at people gets the adrenaline going a little (nothing like being shot at)
            var meA = PlayerAvatar.Body;
            if (meA != null && !meA.Dead)
            {
                bool fight = false;
                foreach (var h in HumanBody.All)
                    if (h != null && h != meA && !h.Dead && Vector3.Distance(h.Center, cam.transform.position) < 70f && Vector3.Angle(cam.transform.forward, h.Center - cam.transform.position) < 35f) { fight = true; break; }
                meA.physio.AddStress(fight ? 0.008f : 0.001f);
            }
            if (d.name == "TASER")
            {
                // wires still in him: the trigger sends another five seconds instead of needing a fresh cartridge
                var w = TaserWires.Current;
                if (ammo[current] <= 0 && w != null && w.Arrived) { w.Cycle(); return; }
                if (!GameSettings.InfiniteAmmo) ammo[current]--;
                FireTaser(d);
                return;
            }
            if (IsFlamer(d))
            {
                // a tenth of a second of flame: no round, no kick, the tank going down
                if (FlameTank.Gone) { ammo[current] = 0; SandboxGame.Instance?.Toast("NO FUEL TANK  ·  R FOR A NEW ONE"); return; }
                if (!GameSettings.InfiniteAmmo) ammo[current]--;
                var mz = muzzles[current];
                Vector3 fdir = (AimPoint - mz.position).sqrMagnitude > 0.25f ? (AimPoint - mz.position).normalized : mz.forward;
                Flamethrower.Spray(mz.position, Vector3.Slerp(mz.forward, fdir, 0.5f), PlayerAvatar.Body);
                if (ammo[current] <= 0) SandboxGame.Instance?.Toast("TANK EMPTY  ·  R FOR A NEW ONE");
                return;
            }
            if (!GameSettings.InfiniteAmmo) ammo[current]--;
            var am = CurrentAmmo;
            int pellets = am != null ? am.pellets : d.pellets;
            // shot keeps its spread when aiming; single rounds tighten up
            float baseSpread = am != null && am.pellets > 1 ? am.spread : (am != null && am.spread > 0f ? am.spread : d.spread);
            float spread = baseSpread * Mathf.Lerp(1f, pellets > 1 ? 0.85f : 0.25f, Mathf.Max(Mathf.Max(aimT, pointT * 0.45f), pointAimT * 0.8f));
            // one hand on a long gun, shaking hands, a weak arm: everything goes wider
            float oneK = OneHandK(d);
            spread = spread * oneK + (PlayerBody.I != null ? PlayerBody.I.Tremor * 1.2f : 0f);
            if (BarrelHeat.I != null) spread *= BarrelHeat.I.SpreadMul(current);
            spread *= 1f + 1.5f * blindT;   // (and round a corner it isn't aimed at all)
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
            BarrelHeat.I?.Shot(current, d);

            float gripK = Loadout.grip == GripKind.Vertical ? 0.78f : Loadout.grip == GripKind.Angled ? 0.88f : 1f;
            float ammoK = am != null ? Mathf.Clamp(Mathf.Sqrt(am.Energy / Mathf.Max(1f, d.Energy)), 0.5f, 1.4f) : 1f;
            // held out at arm's length round a corner, one-handed, no shoulder behind it: it jumps all over
            float blindK = Mathf.Lerp(1f, 6f, blindT);
            float suppK = Loadout.suppressor && SuppressorFor(d) ? 0.9f : 1f;    // a can takes a little of the muzzle rise off
            // braced into the shoulder, cheek on the stock: it helps, but a rifle still kicks hard
            float adsK = Mathf.Lerp(1f, 0.62f, aimT);
            var rec = Recoil.For(d.name);
            // the burst: a pause between shots starts a new one with a new (unpredictable) pattern
            if (Time.unscaledTime - lastShotTime > 0.32f) { burstShot = 0; burstSeed = Random.Range(0f, 1000f); burstSide = Random.value < 0.6f ? 1f : -1f; }
            lastShotTime = Time.unscaledTime;
            float k = ammoK * gripK * oneK * adsK * suppK * blindK;
            Vector2 kickDeg = rec.Shot(burstShot, burstSeed, burstSide) * k;
            burstShot++;
            // held by your body most of the kick goes into the gun and your arms (they throw the muzzle about);
            // the rest moves your view
            float camK = (held ? 0.62f : 1f) * (1f - 0.7f * blindT);   // (round the corner it isn't at your eye)
            pc.AddRecoil(camK * kickDeg.y, camK * kickDeg.x);
            // and it rocks your view a little with every shot, more for a harder gun
            pc.Rock((0.012f + 0.009f * d.recoil) * ammoK * suppK * Mathf.Lerp(1f, 0.75f, aimT));
            if (held && gunRb != null)
            {
                // the gun is driven back into the hands, the muzzle flips up and jumps sideways, and it rolls
                float r = 1.3f * d.recoil * ammoK * gripK * suppK * Mathf.Lerp(1f, 0.6f, aimT) * blindK;
                KickGun(-(gRot * Vector3.forward) * (0.55f * r * Mathf.Sqrt(oneK)) + Vector3.up * (0.1f * r),
                    (gRot * Vector3.right) * (-kickDeg.y * rec.flip * 0.9f) + (gRot * Vector3.up) * (kickDeg.x * rec.flip * 0.8f) + (gRot * Vector3.forward) * (Random.Range(-1f, 1f) * kickDeg.y * 0.35f));
            }
            swayVel += new Vector2(kickDeg.x * 0.006f, kickDeg.y * 0.012f);
            if (d.recoil > 4f) pc.Shake(0.15f + 0.02f * d.recoil);
            kick = Mathf.Min(1.6f, kick + 0.5f + d.recoil * 0.1f);
            // a hard-kicking gun slows you down while you're firing it
            fireHeat = Mathf.Min(1.5f, fireHeat + Mathf.Clamp(d.recoil * ammoK, 0.4f, 8f) * 0.07f);
            bool supp = Loadout.suppressor && SuppressorFor(d);
            flashTimer = supp ? 0f : 0.045f;   // the can swallows the flash
            // a fresh flash shape every shot
            float seed = Random.value * 100f;
            for (int fi = 0; fi < flashParts.Count; fi++) { var fr = flashParts[fi]; fr.sharedMaterial.SetFloat("_Seed", seed + fi * 3.7f); fr.sharedMaterial.SetFloat("_Intensity", Random.Range(0.8f, 1.25f)); }
            // the empty case out of the port (a pump or a bolt throws it out when it is worked, a moment later)
            float caseDelay = modelNames[current] == "Gun_Shotgun" ? 0.42f : modelNames[current] == "Gun_AWP" ? 0.65f : 0f;
            if (caseDelay <= 0f) EjectCase(current); else pendingCases.Add((Time.time + caseDelay, current));
            Fx.Muzzle(muzzles[current].position, AimDirection, Mathf.Clamp(Mathf.Sqrt((am != null ? am.Energy * am.pellets : d.Energy) / 2000f), 0.6f, 2.2f) * (supp ? 0.25f : 1f));
            Fx.BlastDust(muzzles[current].position, AimDirection, Mathf.Clamp(Mathf.Sqrt((am != null ? am.Energy * am.pellets : d.Energy) / 2000f), 0.6f, 2.2f) * (supp ? 0.4f : 1f));
            if (am != null && am.incendiary) Fx.DragonsBreath(muzzles[current].position, cam.transform.forward);
            HumanBrain.BroadcastNoise(origin, d.loudness * (supp ? 0.5f : 1f), 0.35f);
            Intel.Gunshot(origin, Teams.Friendly, d.loudness * (supp ? 0.5f : 1f), PlayerAvatar.Body, null);
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

        static readonly Unity.Profiling.ProfilerMarker PM_FixedUpdate = new Unity.Profiling.ProfilerMarker("Carl.WeaponController.FixedUpdate");

        void FixedUpdate()
        {

            using var pmScope = PM_FixedUpdate.Auto();
            HoldPhysics();
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
            fireHeat = Mathf.MoveTowards(fireHeat, 0f, dt * 1.1f);
            Vector3 hip = new Vector3(0.17f, -0.17f, 0.36f);
            // aiming puts the sight line (iron sights, the optic's centre or the scope's axis) right on the eye
            bool held = BodyHeld;
            // held by your body the gun is its real size, where real arms put it; without one, the old shrunken
            // viewmodel close to the camera
            float VmScale = held ? 1f : 0.62f;
            bool optic = d.scope || Loadout.sight != SightKind.Iron;
            Vector3 aim = new Vector3(0f, -SightHeight() * VmScale, optic ? 0.2f : 0.24f);
            if (d.type == WeaponType.Gun && IsPistol(d)) { hip = new Vector3(0.17f, -0.17f, 0.38f); aim.z = 0.32f; }
            // (eye relief: the eye ~6 cm behind the eyepiece, wherever that is on the gun; at the hip it is held
            // that much further out too, or the back of a long scope would be in your face)
            float back = d.scope && current < ocularZ.Count && !float.IsNaN(ocularZ[current]) ? Mathf.Max(0f, -ocularZ[current] - 0.177f) : 0f;
            if (d.scope) { hip = new Vector3(0.25f, -0.25f, 0.3f + back * VmScale); aim.z = 0.17f + back * VmScale; }
            if (d.type != WeaponType.Gun) aim = new Vector3(0f, -0.075f, 0.24f);
            if (held)
            {
                // cheek on the stock with the eye behind the sights; a pistol pushed out at arm's length
                if (IsPistol(d)) { hip = new Vector3(0.05f, -0.15f, 0.46f); aim.z = 0.5f; }   // pushed out, arms nearly straight
                else if (d.scope) { hip = new Vector3(0.12f, -0.32f, 0.3f + back); aim.z = 0.24f + back; }   // (scope eye relief)
                // the butt in the shoulder, the grip a forearm in front of it, the eye 8-10 cm behind the rear sight
                else if (d.type == WeaponType.Gun) { hip = new Vector3(0.1f, -0.32f, 0.3f); aim.z = optic ? 0.25f : 0.27f; }
                else if (d.type == WeaponType.Tool) { hip = new Vector3(0.12f, -0.16f, 0.42f); aim = hip; }
                else if (d.type == WeaponType.Medical) { hip = new Vector3(0.1f, -0.2f, 0.36f); aim = hip; }
                else { hip = new Vector3(0.16f, -0.24f, 0.36f); aim = hip; }
            }
            Vector3 pos = Vector3.Lerp(hip, aim, Mathf.SmoothStep(0, 1, aimT));
            Quaternion pointRot = Quaternion.identity;
            if (pointT > 0f && d.type == WeaponType.Gun)
            {
                // point aim: aiming without the sights. Held canted and well down below the eye, looking over it;
                // with right mouse it comes up and out to just under the eye line, nearly level, pointed where you
                // look - still under your eyes, so you see over it, never down the sights
                float k = Mathf.SmoothStep(0f, 1f, pointT) * (1f - aimT);
                float up = Mathf.SmoothStep(0f, 1f, pointAimT);
                Quaternion roll = Quaternion.Euler(0f, 0f, Mathf.Lerp(28f, 8f, up));
                Vector3 low = new Vector3(0.025f, -0.085f, 0.03f), raised = new Vector3(0.01f, -0.055f, 0.05f);
                Vector3 point = roll * aim + Vector3.Lerp(low, raised, up);
                pos = Vector3.Lerp(pos, point, k);
                pointRot = Quaternion.Slerp(Quaternion.identity, roll, k);
            }
            Quaternion rot = pointRot;
            pos.z -= kick * 0.05f;
            pos.y += kick * 0.01f;
            rot *= Quaternion.Euler(-kick * 6f, 0, 0);
            // reloading, clearing a jam: the gun canted round to the support hand and the moves' jolts (WeaponMoves)
            if (BodyHeld) MovePose(ref pos, ref rot);
            else if (reloading)
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
            if (d.type == WeaponType.Grenade) ThrowPose(ref pos, ref rot);
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
            if (blindT > 0.001f && d.type == WeaponType.Gun)
            {
                // round the corner: pushed out at arm's length to that side, turned out past the edge, pointed
                // by feel (it is a lot further across your body to the left with the gun in your right hand)
                float s = blindSide, k = Mathf.SmoothStep(0f, 1f, blindT);
                // arms out straight in front and over to that side, the gun out past the corner, still pointing
                // mostly forward (your body stays behind the cover)
                float reach = s > 0f ? Mathf.Lerp(0.32f, 0.6f, blindOut) : Mathf.Lerp(0.18f, 0.4f, blindOut);
                Vector3 bp = new Vector3(s * reach, -0.08f, Mathf.Lerp(0.44f, 0.5f, blindOut));
                float gunYaw = s * Mathf.Lerp(8f, 30f, blindOut) + blindAim.x;
                Quaternion br = Quaternion.Euler(-blindAim.y, gunYaw, -s * 14f);
                // your head turns to look where it points; the pose is in the turned view, so it is turned back
                // by as much (the gun stays where it is held, out round the corner)
                BlindLook = new Vector2(gunYaw * 0.5f, blindAim.y * 0.5f) * k;
                Quaternion unturn = Quaternion.Inverse(Quaternion.Euler(-BlindLook.y, BlindLook.x, 0f));
                pos = Vector3.Lerp(pos, unturn * bp, k);
                rot = Quaternion.Slerp(rot, unturn * br, k);
            }
            else BlindLook = Vector2.zero;
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
            // held in your hands the gun stays (it is jointed to them); looking through a scope just hides it
            if (held && Armed) { vmRoot.gameObject.SetActive(true); ShowHeldGun(!Scoped); }
            else vmRoot.gameObject.SetActive(!Scoped);
            // the projected reticle (at infinity, so it is only seen looking more or less through the glass)
            foreach (var r in reticles) if (r) r.enabled = false;   // (the optic draws its reticle in its own picture now: ScopeView)

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
