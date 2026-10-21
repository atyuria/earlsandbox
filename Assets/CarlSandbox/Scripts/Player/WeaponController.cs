using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    public enum WeaponType { Gun, Knife, Grenade }

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

    /// <summary>Weapons, viewmodels and the physics grab (E).</summary>
    public class WeaponController : MonoBehaviour
    {
        public readonly List<WeaponDef> defs = new List<WeaponDef>
        {
            new WeaponDef { name = "AK-47", type = WeaponType.Gun, auto = true, rpm = 600, muzzleVelocity = 715, bulletMass = 0.0079f, diameter = 0.0078f, magazine = 30, reloadTime = 2.4f, recoil = 1.5f, spread = 0.35f, zoomFov = 55, loudness = 75, tracer = true },
            new WeaponDef { name = "GLOCK 17", type = WeaponType.Gun, auto = false, rpm = 500, muzzleVelocity = 375, bulletMass = 0.0075f, diameter = 0.009f, magazine = 17, reloadTime = 1.5f, recoil = 1.1f, spread = 0.5f, zoomFov = 62, loudness = 50, tracer = false },
            new WeaponDef { name = "AWP", type = WeaponType.Gun, auto = false, rpm = 42, muzzleVelocity = 900, bulletMass = 0.0162f, diameter = 0.0086f, magazine = 5, reloadTime = 3.2f, recoil = 4.5f, spread = 0.02f, zoomFov = 11, scope = true, loudness = 95, tracer = true },
            new WeaponDef { name = ".50 CAL", type = WeaponType.Gun, auto = false, rpm = 110, muzzleVelocity = 890, bulletMass = 0.0421f, diameter = 0.0127f, magazine = 10, reloadTime = 3.6f, recoil = 7f, spread = 0.04f, zoomFov = 9, scope = true, loudness = 120, tracer = true },
            // 12-gauge pump, 00 buckshot: nine 8.4 mm pellets per shell
            new WeaponDef { name = "SHOTGUN", type = WeaponType.Gun, auto = false, rpm = 70, muzzleVelocity = 400, bulletMass = 0.0035f, diameter = 0.0084f, magazine = 6, reloadTime = 3.2f, recoil = 6f, spread = 2.4f, zoomFov = 55, loudness = 100, tracer = false, pellets = 9 },
            new WeaponDef { name = "KNIFE", type = WeaponType.Knife },
            new WeaponDef { name = "GRENADE", type = WeaponType.Grenade },
        };

        public int current;
        public bool reloading;
        public float aimT;
        readonly int[] ammo = new int[8];
        float nextFire, reloadEnd;
        PlayerController pc;
        Camera cam;
        Transform vmRoot;
        readonly List<GameObject> models = new List<GameObject>();
        readonly List<Transform> muzzles = new List<Transform>();
        GameObject flash;
        readonly List<MeshRenderer> flashParts = new List<MeshRenderer>();
        public float AimAmount => aimT;

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
        public bool HasGunOut => Current.type == WeaponType.Gun;
        public bool Scoped => IsScope && aimT > 0.92f;

        // ---- customisation (G menu)
        public WeaponLoadout[] loadouts;
        public WeaponLoadout Loadout => loadouts[current];
        public AmmoType CurrentAmmo { get { var a = AmmoTable.For(Current.name); return a == null ? null : a[Mathf.Clamp(Loadout.ammo, 0, a.Length - 1)]; } }
        public bool IsScope => Current.type == WeaponType.Gun && (Loadout.sight == SightKind.Scope4x || Loadout.sight == SightKind.Scope8x);
        public float ZoomFov
        {
            get
            {
                switch (Loadout.sight)
                {
                    case SightKind.RedDot: return 48f;
                    case SightKind.Holo: return 46f;
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
            if (d.name == "GLOCK 17") return new[] { SightKind.Iron, SightKind.RedDot };
            return new[] { SightKind.Iron, SightKind.RedDot, SightKind.Holo, SightKind.Scope4x };
        }
        public static bool GripsFor(WeaponDef d) => d.type == WeaponType.Gun && d.name != "GLOCK 17";
        readonly List<Vector3> mountTop = new List<Vector3>(), mountUnder = new List<Vector3>();
        readonly List<GameObject> attach = new List<GameObject>();
        public bool Grabbing => grabbed != null;

        public string AmmoText
        {
            get
            {
                if (Current.type == WeaponType.Knife) return "—";
                if (Current.type == WeaponType.Grenade) return "∞";
                return reloading ? "RELOADING" : $"{ammo[current]:00} / {Current.magazine}";
            }
        }

        public void Setup(PlayerController player)
        {
            pc = player;
            cam = player.cam;
            for (int i = 0; i < defs.Count; i++) ammo[i] = defs[i].magazine;
            loadouts = new WeaponLoadout[defs.Count];
            for (int i = 0; i < defs.Count; i++) loadouts[i] = new WeaponLoadout { sight = defs[i].scope ? SightKind.Scope8x : SightKind.Iron };
            vmRoot = new GameObject("Viewmodel").transform;
            vmRoot.SetParent(cam.transform, false);
            BuildModels();
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

            Select(0);
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

        static readonly string[] modelNames = { "Gun_AK47", "Gun_Glock", "Gun_AWP", "Gun_Barrett", "Gun_Shotgun", "Gun_Knife", "Gun_Grenade" };

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
                case "Gun_Glock": rot = Quaternion.Euler(16f, 0f, 0f); break;
                case "Gun_AWP": rot = Quaternion.Euler(20f, 0f, 0f); break;
                case "Gun_Barrett": rot = Quaternion.Euler(18f, 0f, 0f); break;
                case "Gun_Shotgun": rot = Quaternion.Euler(30f, 0f, 0f); break;  // wraps the sloped wrist of the stock
                case "Gun_Knife": rot = Quaternion.Euler(90f, 0f, 0f); break;   // handle runs forward
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
            if (weapon == "Gun_Knife" || weapon == "Gun_Grenade") return;
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
                bool gripOn = L.grip != GripKind.None && GripsFor(d);
                if (gripOn)
                {
                    var g = ModelLibrary.Spawn(L.grip == GripKind.Vertical ? "Att_GripVertical" : "Att_GripAngled", root, ModelLibrary.GunMaterial, false);
                    if (g != null) { g.transform.localPosition = mountUnder[i]; attach.Add(g); }
                }
                PlaceSupportHand(i, gripOn ? L.grip : GripKind.None, mountUnder[i]);
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
            if (i < 0 || i >= defs.Count) return;
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
            float scroll = mouse.scroll.ReadValue().y;
            if (!Grabbing && Mathf.Abs(scroll) > 0.1f) Select((current + (scroll < 0 ? 1 : defs.Count - 1)) % defs.Count);

            UpdateGrab(kb, mouse, locked, scroll);

            var d = Current;
            bool aimHeld = locked && mouse.rightButton.isPressed && d.type == WeaponType.Gun && !reloading;
            aimT = Mathf.MoveTowards(aimT, aimHeld ? 1f : 0f, dt * (IsScope ? 5f : Loadout.grip == GripKind.Angled ? 11f : 8f));
            float fov = Mathf.Lerp(pc.fovNormal, d.type == WeaponType.Gun ? ZoomFov : pc.fovNormal, Mathf.SmoothStep(0, 1, aimT));
            cam.fieldOfView = fov;
            pc.sensitivityScale = fov / pc.fovNormal;

            if (reloading && Time.unscaledTime >= reloadEnd) { reloading = false; ammo[current] = d.magazine; }
            if (GameSettings.InfiniteAmmo && d.type == WeaponType.Gun) ammo[current] = d.magazine;
            if (locked && kb.rKey.wasPressedThisFrame && d.type == WeaponType.Gun && !reloading && ammo[current] < d.magazine) StartReload();

            if (locked && !Grabbing)
            {
                switch (d.type)
                {
                    case WeaponType.Gun:
                        bool trigger = mouse.leftButton.wasPressedThisFrame || (d.auto && mouse.leftButton.isPressed);
                        if (trigger && !reloading && Time.unscaledTime >= nextFire)
                        {
                            if (ammo[current] <= 0) { Sfx.Play("click_empty", cam.transform.position, 0.6f, 0f); StartReload(); }
                            else FireGun(d);
                        }
                        break;
                    case WeaponType.Knife:
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
        }

        void StartReload()
        {
            reloading = true;
            reloadEnd = Time.unscaledTime + Current.reloadTime;
            Sfx.Reload(Current.name, cam.transform.position, Current.reloadTime);
        }

        void FireGun(WeaponDef d)
        {
            nextFire = Time.unscaledTime + 60f / d.rpm;
            if (!GameSettings.InfiniteAmmo) ammo[current]--;
            var am = CurrentAmmo;
            int pellets = am != null ? am.pellets : d.pellets;
            // shot keeps its spread when aiming; single rounds tighten up
            float baseSpread = am != null && am.pellets > 1 ? am.spread : (am != null && am.spread > 0f ? am.spread : d.spread);
            float spread = baseSpread * Mathf.Lerp(1f, pellets > 1 ? 0.85f : 0.25f, aimT);
            Vector3 origin = cam.transform.position;
            Vector3 dir = cam.transform.forward;
            for (int p = 0; p < pellets; p++)
            {
                // pellets spread evenly over the pattern (sqrt keeps them from bunching in the middle)
                float ang = (pellets > 1 ? Mathf.Sqrt(Random.value) : Random.value) * spread;
                dir = Quaternion.AngleAxis(Random.Range(0f, 360f), cam.transform.forward) * Quaternion.AngleAxis(ang, cam.transform.right) * cam.transform.forward;
                if (am != null)
                    Ballistics.Instance.Fire(origin, dir, am.velocity * Random.Range(0.97f, 1.03f), am.mass, am.diameter, HitKind.Bullet, am.tracer && p == 0, muzzles[current].position, am);
                else
                    Ballistics.Instance.Fire(origin, dir, d.muzzleVelocity * Random.Range(0.97f, 1.03f), d.bulletMass, d.diameter, HitKind.Bullet, d.tracer && p == 0, muzzles[current].position);
            }

            float gripK = Loadout.grip == GripKind.Vertical ? 0.7f : Loadout.grip == GripKind.Angled ? 0.82f : 1f;
            float ammoK = am != null ? Mathf.Clamp(Mathf.Sqrt(am.Energy / Mathf.Max(1f, d.Energy)), 0.5f, 1.4f) : 1f;
            pc.AddRecoil(d.recoil * ammoK * gripK * Random.Range(0.8f, 1.2f) * Mathf.Lerp(1f, 0.6f, aimT), Random.Range(-0.35f, 0.35f) * d.recoil * 0.5f * gripK);
            if (d.recoil > 4f) pc.Shake(0.15f);
            kick = Mathf.Min(1.5f, kick + 0.6f + d.recoil * 0.08f);
            flashTimer = 0.045f;
            // a fresh flash shape every shot
            float seed = Random.value * 100f;
            for (int fi = 0; fi < flashParts.Count; fi++) { var fr = flashParts[fi]; fr.sharedMaterial.SetFloat("_Seed", seed + fi * 3.7f); fr.sharedMaterial.SetFloat("_Intensity", Random.Range(0.8f, 1.25f)); }
            Fx.Muzzle(muzzles[current].position, cam.transform.forward, Mathf.Clamp(Mathf.Sqrt((am != null ? am.Energy * am.pellets : d.Energy) / 2000f), 0.6f, 2.2f));
            HumanBrain.BroadcastNoise(origin, d.loudness, 0.35f);
            Sfx.Gun(d.name, muzzles[current].position);

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
                if (meleeMode == 1) Stab(); else Slash();
            }
            if (meleeT > (meleeMode == 1 ? 0.35f : 0.45f)) meleeMode = 0;
        }

        void Stab()
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var arm = ArmourSystem.Raycast(ray.origin, ray.direction, 2f, out float ad, out _);
            if (arm != null && (!Physics.Raycast(ray, out var h0, 2f, ~0, QueryTriggerInteraction.Ignore) || ad <= h0.distance + 0.14f))
            {
                // the blade skids off armour (soft armour only stops some stabs)
                if (arm.steel || arm.cls > 2.5f || Random.value < 0.6f)
                {
                    Fx.Sparks(ray.origin + ray.direction * ad, -ray.direction, arm.steel ? 10 : 2);
                    arm.AddWear(arm.transform.InverseTransformPoint(ray.origin + ray.direction * ad), 0.1f);
                    return;
                }
            }
            if (Physics.Raycast(ray, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore) && !(hit.collider is CharacterController))
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
                if (Physics.Raycast(cam.transform.position, dir, out var hit, 1.9f, ~0, QueryTriggerInteraction.Ignore) && !(hit.collider is CharacterController))
                {
                    var bp = hit.collider.GetComponent<BodyPart>();
                    if (bp != null)
                    {
                        if (ArmourSystem.Raycast(cam.transform.position, dir, hit.distance + 0.14f, out float sad, out _) != null)
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
            if (locked && kb.eKey.wasPressedThisFrame)
            {
                // grab a helmet visor: flip it up or down
                var vp = ArmourSystem.Raycast(cam.transform.position, cam.transform.forward, 3f, out _, out _);
                if (vp != null && !vp.visor && vp.transform.parent != null) { var par = vp.transform.parent.GetComponent<ArmourPiece>(); if (par != null) vp = par; }
                if (vp != null && vp.visor) { vp.ToggleVisor(); return; }
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != null && !hit.rigidbody.isKinematic)
                {
                    grabbed = hit.rigidbody;
                    grabLocal = grabbed.transform.InverseTransformPoint(hit.point);
                    grabDist = hit.distance;
                    var bp = grabbed.GetComponent<BodyPart>();
                    if (bp != null && bp.body != null) bp.body.ragdoll.Fall();
                }
            }
            // E held against a wall or floor (nothing to grab): smear the blood on it with your hand
            if (locked && grabbed == null && kb.eKey.isPressed && BloodSystem.Instance != null)
            {
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var sh, 1.8f, ~0, QueryTriggerInteraction.Ignore) && sh.rigidbody == null && !(sh.collider is CharacterController))
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

            if (grabbed != null && (!kb.eKey.isPressed || !locked))
            {
                grabbed = null;
            }
            if (grabbed != null)
            {
                grabDist = Mathf.Clamp(grabDist + scroll * 0.002f, 1f, 6f);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    grabbed.AddForce(cam.transform.forward * 18f, ForceMode.VelocityChange);
                    grabbed = null;
                }
            }
            grabLine.enabled = grabbed != null;
        }

        void FixedUpdate()
        {
            if (grabbed == null) return;
            Vector3 p = grabbed.transform.TransformPoint(grabLocal);
            Vector3 target = cam.transform.position + cam.transform.forward * grabDist;
            Vector3 v = grabbed.GetPointVelocity(p);
            Vector3 f = (target - p) * 220f - v * 22f;
            grabbed.AddForceAtPosition(Vector3.ClampMagnitude(f, 900f) * Mathf.Min(grabbed.mass, 20f), p);
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
            const float VmScale = 0.62f;
            bool optic = d.scope || Loadout.sight != SightKind.Iron;
            Vector3 aim = new Vector3(0f, -SightHeight() * VmScale, optic ? 0.2f : 0.24f);
            if (d.type == WeaponType.Gun && d.name == "GLOCK 17") { hip = new Vector3(0.17f, -0.17f, 0.38f); aim.z = 0.32f; }
            if (d.scope) { hip = new Vector3(0.25f, -0.25f, 0.3f); aim.z = 0.17f; }
            if (d.type != WeaponType.Gun) aim = new Vector3(0f, -0.075f, 0.24f);
            Vector3 pos = Vector3.Lerp(hip, aim, Mathf.SmoothStep(0, 1, aimT));
            Quaternion rot = Quaternion.identity;
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
            // sway with movement
            float bob = Mathf.Sin(Time.unscaledTime * 8f) * 0.004f * (1f - aimT);
            pos.y += bob;
            vmRoot.localPosition = pos;
            vmRoot.localScale = Vector3.one * VmScale;
            vmRoot.localRotation = rot;
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
                flash.transform.localScale = Vector3.one * Random.Range(0.8f, 1.3f) * (d.recoil > 4f ? 1.8f : 1f);
            }
            muzzleLight.transform.position = muzzles[current].position;
            muzzleLight.intensity = showFlash ? 3f : 0f;
        }
    }
}
