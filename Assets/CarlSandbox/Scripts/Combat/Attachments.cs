using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>One real attachment: an optic, a foregrip, a weapon light or an aiming laser.</summary>
    public class AttInfo
    {
        public string id, name, desc, model;
        /// <summary>0 optic, 1 foregrip, 2 light, 3 laser.</summary>
        public int kind;
        // optics: how it behaves (zoom and reticle), the sight line over the rail, the projected reticle's window
        public SightKind sight;
        public float eye;
        public Vector3 reticle;
        public Vector2 retSize;
        public float retShape;
        // foregrips: how the hand holds it
        public GripKind grip;
    }

    /// <summary>
    /// The attachments that go on the rails, each modelled on a real one (BlenderTools/build_attachments.py and
    /// optics.py): red dots, holographic and prism sights, scopes, foregrips, lights and lasers. A loadout names
    /// one of each by id; with none named, the first of its kind goes on.
    /// </summary>
    public static class Attachments
    {
        public static readonly List<AttInfo> All = new List<AttInfo>();

        static AttInfo Optic(string id, string name, string model, SightKind s, float eye, Vector3 ret, Vector2 size, float shape, string desc)
        { var a = new AttInfo { id = id, name = name, model = model, kind = 0, sight = s, eye = eye, reticle = ret, retSize = size, retShape = shape, desc = desc }; All.Add(a); return a; }
        static void Add(int kind, string id, string name, string model, string desc, GripKind g = GripKind.None)
            => All.Add(new AttInfo { id = id, name = name, model = model, kind = kind, grip = g, desc = desc });

        static Attachments()
        {
            Optic("acro", "Aimpoint ACRO P-2", "Att_RedDot", SightKind.RedDot, 0.03f, new Vector3(0f, 0.03f, 0.027f), new Vector2(0.021f, 0.021f), 0f, "Enclosed red dot on a low mount: small, tough, a 3.5 MOA dot.");
            Optic("rmr", "Trijicon RMR Type 2", "Att_RMR", SightKind.RedDot, 0.031f, new Vector3(0f, 0.031f, 0.0084f), new Vector2(0.017f, 0.013f), 0f, "The little forged red dot on a riser: a small window, quick to the eye.");
            Optic("hs510c", "Holosun HS510C", "Att_HS510C", SightKind.RedDot, 0.03f, new Vector3(0f, 0.03f, 0.0218f), new Vector2(0.026f, 0.024f), 0f, "Open reflex with a big window, solar panel, QD mount.");
            Optic("kobra", "Kobra EKP-8-18", "Att_Kobra", SightKind.RedDot, 0.033f, new Vector3(0f, 0.033f, 0.0248f), new Vector2(0.029f, 0.029f), 0f, "The Russian reflex sight: a deep square hood, the big dial on the side.");
            Optic("exps3", "EOTech EXPS3", "Att_Holo", SightKind.Holo, 0.032f, new Vector3(0f, 0.033f, 0.017f), new Vector2(0.031f, 0.023f), 1f, "Holographic: the 68 MOA ring and dot, a big window, both eyes open.");
            Optic("acog", "Trijicon ACOG TA31 4x32", "Att_ACOG", SightKind.Scope4x, 0.04f, Vector3.zero, Vector2.zero, 0f, "The forged 4x prism sight: bright, short, tough.");
            Optic("pso1", "PSO-1 4x24", "Att_PSO1", SightKind.Scope4x, 0.046f, Vector3.zero, Vector2.zero, 0f, "The Soviet marksman's scope on a rail adapter: rangefinder reticle, long eye relief.");
            Optic("lpvo4", "Vortex Razor 1-8x LPVO (at 4x)", "Att_Scope", SightKind.Scope4x, 0.04f, Vector3.zero, Vector2.zero, 0f, "Low-power variable optic in a cantilever mount, dialled to 4x.");
            Optic("lpvo8", "Vortex Razor 1-8x LPVO (at 8x)", "Att_Scope", SightKind.Scope8x, 0.04f, Vector3.zero, Vector2.zero, 0f, "The same scope turned all the way up.");

            Add(1, "bcm", "BCM Gunfighter vertical grip", "Att_GripVertical", "A full vertical grip: -30% recoil.", GripKind.Vertical);
            Add(1, "rvg", "Magpul RVG", "Att_RVG", "Short ribbed vertical grip.", GripKind.Vertical);
            Add(1, "rk2", "Zenit RK-2", "Att_RK2", "Russian vertical grip, finger grooves and a palm shelf.", GripKind.Vertical);
            Add(1, "afg", "Magpul AFG-2", "Att_GripAngled", "Angled foregrip: faster on target.", GripKind.Angled);
            Add(1, "stop", "Magpul M-LOK hand stop", "Att_HandStop", "A low stop for the heel of the hand.", GripKind.Angled);

            Add(2, "x300", "SureFire X300 Ultra", "Att_Light", "1000 lumen pistol-style light.");
            Add(2, "m600", "SureFire M600 Scout", "Att_M600", "The rifleman's 1\" light on an offset mount.");
            Add(2, "klesch", "Zenit Klesch-2P", "Att_Klesch", "Russian tactical light, a wide head and a pressure pad.");

            Add(3, "peq15", "L3 PEQ-15", "Att_Laser", "Visible and IR lasers in the familiar tan box.");
            Add(3, "perst", "Zenit Perst-4", "Att_Perst", "Compact Russian laser module.");
            Add(3, "dbal", "Steiner DBAL-A2", "Att_DBAL", "Dual-beam aiming laser, sloped front.");
        }

        public static List<AttInfo> Of(int kind) { var l = new List<AttInfo>(); foreach (var a in All) if (a.kind == kind) l.Add(a); return l; }
        static AttInfo Find(int kind, string id) { if (id != null) foreach (var a in All) if (a.kind == kind && a.id == id) return a; return null; }
        static AttInfo First(int kind, System.Predicate<AttInfo> ok) { foreach (var a in All) if (a.kind == kind && ok(a)) return a; return null; }

        /// <summary>The optic on it (null: irons).</summary>
        public static AttInfo OpticOf(WeaponLoadout L)
        {
            if (L == null || L.sight == SightKind.Iron) return null;
            var a = Find(0, L.optic);
            return a != null && a.sight == L.sight ? a : First(0, x => x.sight == L.sight);
        }
        public static AttInfo GripOf(WeaponLoadout L)
        {
            if (L == null || L.grip == GripKind.None) return null;
            var a = Find(1, L.foregrip);
            return a != null && a.grip == L.grip ? a : First(1, x => x.grip == L.grip);
        }
        public static AttInfo LightOf(WeaponLoadout L) => L == null || !L.light ? null : Find(2, L.lightId) ?? First(2, x => true);
        public static AttInfo LaserOf(WeaponLoadout L) => L == null || !L.laser ? null : Find(3, L.laserId) ?? First(3, x => true);

        /// <summary>Which of each an Earl has on: any that fits, the Russian kit more often on a Russian.</summary>
        public static void Randomise(WeaponLoadout L, int nation = -1)
        {
            string Pick(int kind, System.Predicate<AttInfo> ok)
            {
                var l = new List<AttInfo>(); foreach (var a in All) if (a.kind == kind && ok(a)) l.Add(a);
                if (l.Count == 0) return null;
                if (nation == Nations.Russia && Random.value < 0.5f) foreach (var a in l) if (a.id == "kobra" || a.id == "pso1" || a.id == "rk2" || a.id == "klesch" || a.id == "perst") return a.id;
                return l[Random.Range(0, l.Count)].id;
            }
            if (L.sight != SightKind.Iron) L.optic = Pick(0, a => a.sight == L.sight);
            if (L.grip != GripKind.None) L.foregrip = Pick(1, a => a.grip == L.grip);
            L.lightId = Pick(2, a => true);
            L.laserId = Pick(3, a => true);
        }
    }
}
