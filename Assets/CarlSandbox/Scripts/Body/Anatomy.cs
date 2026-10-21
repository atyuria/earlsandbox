using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    public enum PartId
    {
        Pelvis, Abdomen, Chest, Neck, Head,
        UpperArmL, ForearmL, HandL, UpperArmR, ForearmR, HandR,
        ThighL, ShinL, FootL, ThighR, ShinR, FootR
    }

    public enum TissueKind { Organ, Bone, Artery, Vein, Brain, Spine, Airway, Eye }

    public enum BrainRegion { None, Frontal, MotorLeft, MotorRight, TemporalLeft, TemporalRight, Occipital, Deep, Cerebellum, Midbrain, Medulla }

    /// <summary>Spinal levels, ordered from the head down. A lesion paralyses everything below it.</summary>
    public enum SpineLevel { None = 0, HighCervical = 1, LowCervical = 2, Thoracic = 3, Lumbar = 4, Sacral = 5 }

    public enum OrganId
    {
        None,
        Heart, Lungs, Liver, Stomach, Spleen, Kidney, Pancreas, SmallIntestine, Colon, Bladder, Esophagus,
        Trachea, Eye, Brain
    }

    /// <summary>Static description of one anatomical structure, positioned in its body part's local space (metres).</summary>
    public class OrganDef
    {
        public string name;
        public TissueKind kind;
        public OrganId organ;
        public PartId part;
        public bool capsule;
        public Vector3 a, b;          // ellipsoid: a = centre. capsule: a..b
        public Vector3 radii;         // ellipsoid radii, or radii.x = capsule radius
        public Color color = Color.white;
        public float alpha = 1f;
        public float bleed;           // ml/s when fully destroyed
        public float externalFrac = 0.5f; // share of bleeding that leaves through the wound
        public float toughness;       // J absorbed by bone when penetrated
        public float pain = 0.3f;     // pain added at full damage
        public bool shell;            // bone shell (skull, ribs, pelvis) - hit when the path crosses its surface
        public BrainRegion brain;
        public SpineLevel spine;
        public int side;              // -1 left, +1 right, 0 midline

        public bool Intersect(Vector3 p0, Vector3 p1, float inflate, out float t0, out float t1)
        {
            if (capsule) return CarlMath.SegmentCapsule(p0, p1, a, b, radii.x + inflate, out t0, out t1);
            if (shell)
            {
                // a shell is only crossed if the segment starts outside or ends outside it
                bool hit = CarlMath.SegmentEllipsoid(p0, p1, a, radii + Vector3.one * inflate, out t0, out t1);
                if (!hit) return false;
                Vector3 inner = radii * 0.82f;
                bool hitInner = CarlMath.SegmentEllipsoid(p0, p1, a, inner, out float i0, out float i1);
                if (!hitInner) return true;
                // path through shell wall: entry portion only
                t1 = Mathf.Max(t0 + 0.0001f, i0);
                return true;
            }
            return CarlMath.SegmentEllipsoid(p0, p1, a, radii + Vector3.one * inflate, out t0, out t1);
        }

        /// <summary>Is a point (part-local) inside this structure? Shell bones only count within their wall.</summary>
        public bool Contains(Vector3 p)
        {
            if (capsule)
            {
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                return (p - (a + ab * t)).sqrMagnitude <= radii.x * radii.x;
            }
            Vector3 q = CarlMath.Div(p - a, radii);
            float m = q.sqrMagnitude;
            if (m > 1f) return false;
            if (shell) return m > 0.82f * 0.82f;
            return true;
        }

        public float Size => capsule ? radii.x * 2f : Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z)) * 2f;
    }

    public static class Anatomy
    {
        static readonly Color Bone = new Color(0.93f, 0.89f, 0.78f);
        static readonly Color Artery = new Color(0.92f, 0.1f, 0.12f);
        static readonly Color Vein = new Color(0.22f, 0.36f, 0.95f);
        static readonly Color Cord = new Color(0.3f, 0.9f, 0.95f);

        public static string PartName(PartId p)
        {
            switch (p)
            {
                case PartId.Pelvis: return "PELVIS";
                case PartId.Abdomen: return "ABDOMEN";
                case PartId.Chest: return "CHEST";
                case PartId.Neck: return "NECK";
                case PartId.Head: return "HEAD";
                case PartId.UpperArmL: return "LEFT UPPER ARM";
                case PartId.ForearmL: return "LEFT FOREARM";
                case PartId.HandL: return "LEFT HAND";
                case PartId.UpperArmR: return "RIGHT UPPER ARM";
                case PartId.ForearmR: return "RIGHT FOREARM";
                case PartId.HandR: return "RIGHT HAND";
                case PartId.ThighL: return "LEFT THIGH";
                case PartId.ShinL: return "LEFT SHIN";
                case PartId.FootL: return "LEFT FOOT";
                case PartId.ThighR: return "RIGHT THIGH";
                case PartId.ShinR: return "RIGHT SHIN";
                case PartId.FootR: return "RIGHT FOOT";
            }
            return p.ToString();
        }

        public static int Side(PartId p)
        {
            switch (p)
            {
                case PartId.UpperArmL: case PartId.ForearmL: case PartId.HandL:
                case PartId.ThighL: case PartId.ShinL: case PartId.FootL: return -1;
                case PartId.UpperArmR: case PartId.ForearmR: case PartId.HandR:
                case PartId.ThighR: case PartId.ShinR: case PartId.FootR: return 1;
            }
            return 0;
        }

        public static bool IsLeg(PartId p) => p == PartId.ThighL || p == PartId.ShinL || p == PartId.FootL || p == PartId.ThighR || p == PartId.ShinR || p == PartId.FootR;
        public static bool IsArm(PartId p) => p == PartId.UpperArmL || p == PartId.ForearmL || p == PartId.HandL || p == PartId.UpperArmR || p == PartId.ForearmR || p == PartId.HandR;
        public static bool IsTorso(PartId p) => p == PartId.Pelvis || p == PartId.Abdomen || p == PartId.Chest;

        static OrganDef E(PartId part, string name, TissueKind kind, Vector3 c, Vector3 r, Color col, float bleed = 0f, float pain = 0.3f)
            => new OrganDef { part = part, name = name, kind = kind, a = c, radii = r, color = col, bleed = bleed, pain = pain };

        static OrganDef C(PartId part, string name, TissueKind kind, Vector3 a, Vector3 b, float r, Color col, float bleed = 0f, float pain = 0.3f)
            => new OrganDef { part = part, name = name, kind = kind, capsule = true, a = a, b = b, radii = new Vector3(r, r, r), color = col, bleed = bleed, pain = pain };

        static string SideName(int s) => s < 0 ? "LEFT " : "RIGHT ";

        /// <summary>All structures inside one body part.</summary>
        public static List<OrganDef> Build(PartId part)
        {
            var L = new List<OrganDef>();
            int s = Side(part);
            string sn = SideName(s);
            switch (part)
            {
                case PartId.Head:
                {
                    var skull = E(part, "SKULL", TissueKind.Bone, new Vector3(0, 0.005f, 0), new Vector3(0.084f, 0.108f, 0.099f), Bone, 0, 0.5f);
                    skull.shell = true; skull.toughness = 180f; skull.alpha = 0.35f;
                    L.Add(skull);
                    L.Add(Brain(part, "FRONTAL LOBE", BrainRegion.Frontal, new Vector3(0, 0.04f, 0.045f), new Vector3(0.06f, 0.045f, 0.04f), new Color(1f, 0.85f, 0.35f)));
                    L.Add(Brain(part, "LEFT MOTOR CORTEX", BrainRegion.MotorLeft, new Vector3(-0.035f, 0.07f, -0.005f), new Vector3(0.032f, 0.025f, 0.04f), new Color(1f, 0.72f, 0.3f), -1));
                    L.Add(Brain(part, "RIGHT MOTOR CORTEX", BrainRegion.MotorRight, new Vector3(0.035f, 0.07f, -0.005f), new Vector3(0.032f, 0.025f, 0.04f), new Color(1f, 0.72f, 0.3f), 1));
                    L.Add(Brain(part, "LEFT TEMPORAL LOBE", BrainRegion.TemporalLeft, new Vector3(-0.058f, 0.005f, 0.01f), new Vector3(0.022f, 0.03f, 0.045f), new Color(0.95f, 0.75f, 0.4f), -1));
                    L.Add(Brain(part, "RIGHT TEMPORAL LOBE", BrainRegion.TemporalRight, new Vector3(0.058f, 0.005f, 0.01f), new Vector3(0.022f, 0.03f, 0.045f), new Color(0.95f, 0.75f, 0.4f), 1));
                    L.Add(Brain(part, "OCCIPITAL LOBE", BrainRegion.Occipital, new Vector3(0, 0.035f, -0.065f), new Vector3(0.05f, 0.04f, 0.028f), new Color(1f, 0.8f, 0.45f)));
                    L.Add(Brain(part, "THALAMUS", BrainRegion.Deep, new Vector3(0, 0.02f, -0.005f), new Vector3(0.025f, 0.02f, 0.025f), new Color(1f, 0.55f, 0.25f)));
                    L.Add(Brain(part, "CEREBELLUM", BrainRegion.Cerebellum, new Vector3(0, -0.03f, -0.06f), new Vector3(0.05f, 0.024f, 0.03f), new Color(0.95f, 0.6f, 0.35f)));
                    var mid = C(part, "MIDBRAIN / PONS", TissueKind.Brain, new Vector3(0, 0f, -0.02f), new Vector3(0, -0.04f, -0.035f), 0.014f, new Color(0.9f, 0.45f, 0.25f), 2f, 0.2f);
                    mid.brain = BrainRegion.Midbrain; mid.organ = OrganId.Brain; L.Add(mid);
                    var med = C(part, "MEDULLA (BRAINSTEM)", TissueKind.Brain, new Vector3(0, -0.04f, -0.035f), new Vector3(0, -0.085f, -0.045f), 0.011f, new Color(0.85f, 0.4f, 0.3f), 2f, 0.2f);
                    med.brain = BrainRegion.Medulla; med.organ = OrganId.Brain; L.Add(med);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var eye = E(part, SideName(i) + "EYE", TissueKind.Eye, new Vector3(0.032f * i, 0.012f, 0.088f), Vector3.one * 0.0125f, new Color(0.96f, 0.96f, 0.96f), 0.5f, 0.8f);
                        eye.side = i; eye.organ = OrganId.Eye; L.Add(eye);
                    }
                    var jaw = E(part, "JAW", TissueKind.Bone, new Vector3(0, -0.07f, 0.05f), new Vector3(0.05f, 0.022f, 0.045f), Bone, 1.5f, 0.7f);
                    jaw.toughness = 100f; jaw.externalFrac = 0.9f; L.Add(jaw);
                    break;
                }
                case PartId.Neck:
                {
                    var c1 = C(part, "CERVICAL SPINE C1-C4", TissueKind.Spine, new Vector3(0, 0.07f, -0.025f), new Vector3(0, 0f, -0.028f), 0.016f, Cord, 0, 0.6f);
                    c1.spine = SpineLevel.HighCervical; c1.toughness = 150f; L.Add(c1);
                    var c2 = C(part, "CERVICAL SPINE C5-C7", TissueKind.Spine, new Vector3(0, 0f, -0.028f), new Vector3(0, -0.075f, -0.03f), 0.016f, Cord, 0, 0.6f);
                    c2.spine = SpineLevel.LowCervical; c2.toughness = 150f; L.Add(c2);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var car = C(part, SideName(i) + "CAROTID ARTERY", TissueKind.Artery, new Vector3(0.022f * i, 0.075f, 0.018f), new Vector3(0.024f * i, -0.075f, 0.012f), 0.0055f, Artery, 32f, 0.3f);
                        car.side = i; car.externalFrac = 0.9f; L.Add(car);
                        var jug = C(part, SideName(i) + "JUGULAR VEIN", TissueKind.Vein, new Vector3(0.034f * i, 0.075f, 0.008f), new Vector3(0.036f * i, -0.075f, 0.004f), 0.0075f, Vein, 20f, 0.3f);
                        jug.side = i; jug.externalFrac = 0.9f; L.Add(jug);
                    }
                    var tr = C(part, "TRACHEA", TissueKind.Airway, new Vector3(0, 0.07f, 0.032f), new Vector3(0, -0.075f, 0.03f), 0.011f, new Color(0.75f, 0.85f, 0.95f), 1.5f, 0.6f);
                    tr.organ = OrganId.Trachea; L.Add(tr);
                    var es = C(part, "ESOPHAGUS", TissueKind.Organ, new Vector3(0, 0.07f, 0.008f), new Vector3(0, -0.075f, 0.006f), 0.007f, new Color(0.95f, 0.6f, 0.55f), 1f, 0.5f);
                    es.organ = OrganId.Esophagus; L.Add(es);
                    break;
                }
                case PartId.Chest:
                {
                    var ribs = E(part, "RIBCAGE", TissueKind.Bone, Vector3.zero, new Vector3(0.185f, 0.14f, 0.115f), Bone, 0.8f, 0.6f);
                    ribs.shell = true; ribs.toughness = 60f; L.Add(ribs);
                    var heart = E(part, "HEART", TissueKind.Organ, new Vector3(-0.025f, -0.02f, 0.035f), new Vector3(0.045f, 0.055f, 0.045f), new Color(0.88f, 0.22f, 0.32f), 70f, 0.6f);
                    heart.organ = OrganId.Heart; heart.externalFrac = 0.3f; L.Add(heart);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var lung = E(part, SideName(i) + "LUNG", TissueKind.Organ, new Vector3(0.085f * i, 0.015f, -0.005f), new Vector3(i < 0 ? 0.06f : 0.065f, 0.11f, 0.08f), new Color(0.75f, 0.7f, 1f), 6f, 0.5f);
                        lung.side = i; lung.organ = OrganId.Lungs; lung.alpha = 0.55f; lung.externalFrac = 0.3f; L.Add(lung);
                        var cl = C(part, SideName(i) + "CLAVICLE", TissueKind.Bone, new Vector3(0.02f * i, 0.135f, 0.075f), new Vector3(0.17f * i, 0.145f, 0f), 0.011f, Bone, 0.5f, 0.6f);
                        cl.toughness = 70f; cl.side = i; L.Add(cl);
                        var sub = C(part, SideName(i) + "SUBCLAVIAN ARTERY", TissueKind.Artery, new Vector3(0.03f * i, 0.12f, 0.05f), new Vector3(0.17f * i, 0.125f, 0f), 0.006f, Artery, 18f, 0.3f);
                        sub.side = i; sub.externalFrac = 0.6f; L.Add(sub);
                        var sc = E(part, SideName(i) + "SCAPULA", TissueKind.Bone, new Vector3(0.11f * i, 0.06f, -0.1f), new Vector3(0.06f, 0.07f, 0.012f), Bone, 0.5f, 0.5f);
                        sc.toughness = 70f; sc.side = i; L.Add(sc);
                    }
                    var ao = C(part, "THORACIC AORTA", TissueKind.Artery, new Vector3(0f, 0.06f, 0.03f), new Vector3(-0.01f, -0.15f, -0.06f), 0.012f, Artery, 120f, 0.3f);
                    ao.externalFrac = 0.25f; L.Add(ao);
                    var svc = C(part, "SUPERIOR VENA CAVA", TissueKind.Vein, new Vector3(0.025f, 0.12f, 0.03f), new Vector3(0.02f, 0f, 0.03f), 0.01f, Vein, 45f, 0.2f);
                    svc.externalFrac = 0.25f; L.Add(svc);
                    var st = C(part, "STERNUM", TissueKind.Bone, new Vector3(0, 0.11f, 0.11f), new Vector3(0, -0.08f, 0.112f), 0.012f, Bone, 0.5f, 0.6f);
                    st.toughness = 80f; L.Add(st);
                    var t = C(part, "THORACIC SPINE T1-T12", TissueKind.Spine, new Vector3(0, 0.15f, -0.1f), new Vector3(0, -0.15f, -0.095f), 0.017f, Cord, 0, 0.6f);
                    t.spine = SpineLevel.Thoracic; t.toughness = 150f; L.Add(t);
                    break;
                }
                case PartId.Abdomen:
                {
                    var liver = E(part, "LIVER", TissueKind.Organ, new Vector3(0.05f, 0.05f, 0.02f), new Vector3(0.09f, 0.05f, 0.07f), new Color(0.58f, 0.2f, 0.16f), 16f, 0.5f);
                    liver.organ = OrganId.Liver; liver.externalFrac = 0.3f; L.Add(liver);
                    var stom = E(part, "STOMACH", TissueKind.Organ, new Vector3(-0.055f, 0.04f, 0.035f), new Vector3(0.05f, 0.04f, 0.04f), new Color(1f, 0.6f, 0.35f), 2.5f, 0.7f);
                    stom.organ = OrganId.Stomach; L.Add(stom);
                    var spl = E(part, "SPLEEN", TissueKind.Organ, new Vector3(-0.105f, 0.04f, -0.035f), new Vector3(0.025f, 0.035f, 0.03f), new Color(0.55f, 0.25f, 0.55f), 12f, 0.5f);
                    spl.organ = OrganId.Spleen; spl.externalFrac = 0.3f; L.Add(spl);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var k = E(part, SideName(i) + "KIDNEY", TissueKind.Organ, new Vector3(0.06f * i, i < 0 ? 0f : -0.01f, -0.065f), new Vector3(0.025f, 0.04f, 0.02f), new Color(0.6f, 0.3f, 0.72f), 8f, 0.8f);
                        k.organ = OrganId.Kidney; k.side = i; k.externalFrac = 0.35f; L.Add(k);
                    }
                    var pan = E(part, "PANCREAS", TissueKind.Organ, new Vector3(-0.01f, 0.02f, -0.02f), new Vector3(0.06f, 0.012f, 0.015f), new Color(1f, 0.85f, 0.55f), 3f, 0.9f);
                    pan.organ = OrganId.Pancreas; L.Add(pan);
                    var si = E(part, "SMALL INTESTINE", TissueKind.Organ, new Vector3(0, -0.045f, 0.035f), new Vector3(0.095f, 0.045f, 0.055f), new Color(0.95f, 0.6f, 0.65f), 2f, 0.7f);
                    si.organ = OrganId.SmallIntestine; L.Add(si);
                    var ao = C(part, "ABDOMINAL AORTA", TissueKind.Artery, new Vector3(-0.012f, 0.1f, -0.05f), new Vector3(-0.012f, -0.1f, -0.05f), 0.011f, Artery, 100f, 0.3f);
                    ao.externalFrac = 0.25f; L.Add(ao);
                    var ivc = C(part, "INFERIOR VENA CAVA", TissueKind.Vein, new Vector3(0.016f, 0.1f, -0.05f), new Vector3(0.016f, -0.1f, -0.05f), 0.012f, Vein, 45f, 0.2f);
                    ivc.externalFrac = 0.25f; L.Add(ivc);
                    var lum = C(part, "LUMBAR SPINE L1-L5", TissueKind.Spine, new Vector3(0, 0.1f, -0.085f), new Vector3(0, -0.1f, -0.085f), 0.018f, Cord, 0, 0.7f);
                    lum.spine = SpineLevel.Lumbar; lum.toughness = 150f; L.Add(lum);
                    break;
                }
                case PartId.Pelvis:
                {
                    var pel = E(part, "PELVIC BONE", TissueKind.Bone, Vector3.zero, new Vector3(0.165f, 0.1f, 0.11f), Bone, 2f, 0.8f);
                    pel.shell = true; pel.toughness = 160f; L.Add(pel);
                    var bl = E(part, "BLADDER", TissueKind.Organ, new Vector3(0, -0.04f, 0.055f), new Vector3(0.035f, 0.03f, 0.03f), new Color(1f, 0.95f, 0.5f), 1.5f, 0.6f);
                    bl.organ = OrganId.Bladder; L.Add(bl);
                    var co = E(part, "LARGE INTESTINE", TissueKind.Organ, new Vector3(0, 0.04f, 0.025f), new Vector3(0.11f, 0.04f, 0.06f), new Color(0.6f, 0.82f, 0.45f), 2f, 0.7f);
                    co.organ = OrganId.Colon; L.Add(co);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var ia = C(part, SideName(i) + "ILIAC ARTERY", TissueKind.Artery, new Vector3(0.01f * i, 0.11f, -0.04f), new Vector3(0.09f * i, -0.07f, 0.035f), 0.008f, Artery, 30f, 0.3f);
                        ia.side = i; ia.externalFrac = 0.4f; L.Add(ia);
                        var iv = C(part, SideName(i) + "ILIAC VEIN", TissueKind.Vein, new Vector3(0.02f * i, 0.11f, -0.045f), new Vector3(0.095f * i, -0.07f, 0.025f), 0.009f, Vein, 18f, 0.2f);
                        iv.side = i; iv.externalFrac = 0.4f; L.Add(iv);
                    }
                    var sac = C(part, "SACRUM / CAUDA EQUINA", TissueKind.Spine, new Vector3(0, 0.08f, -0.085f), new Vector3(0, -0.05f, -0.095f), 0.022f, Cord, 0, 0.7f);
                    sac.spine = SpineLevel.Sacral; sac.toughness = 140f; L.Add(sac);
                    break;
                }
                case PartId.UpperArmL: case PartId.UpperArmR:
                {
                    var h = C(part, sn + "HUMERUS", TissueKind.Bone, new Vector3(0, 0.15f, 0), new Vector3(0, -0.15f, 0), 0.013f, Bone, 1f, 0.8f);
                    h.toughness = 160f; h.side = s; L.Add(h);
                    var br = C(part, sn + "BRACHIAL ARTERY", TissueKind.Artery, new Vector3(-s * 0.02f, 0.15f, 0.012f), new Vector3(-s * 0.012f, -0.15f, 0.02f), 0.0045f, Artery, 12f, 0.3f);
                    br.side = s; br.externalFrac = 0.85f; L.Add(br);
                    break;
                }
                case PartId.ForearmL: case PartId.ForearmR:
                {
                    var r = C(part, sn + "RADIUS", TissueKind.Bone, new Vector3(0.012f * s, 0.14f, 0.005f), new Vector3(0.015f * s, -0.14f, 0.005f), 0.008f, Bone, 0.5f, 0.8f);
                    r.toughness = 70f; r.side = s; L.Add(r);
                    var u = C(part, sn + "ULNA", TissueKind.Bone, new Vector3(-0.012f * s, 0.14f, -0.005f), new Vector3(-0.012f * s, -0.14f, -0.005f), 0.008f, Bone, 0.5f, 0.8f);
                    u.toughness = 70f; u.side = s; L.Add(u);
                    var ra = C(part, sn + "RADIAL ARTERY", TissueKind.Artery, new Vector3(0.016f * s, 0.1f, 0.02f), new Vector3(0.02f * s, -0.15f, 0.022f), 0.003f, Artery, 5f, 0.3f);
                    ra.side = s; ra.externalFrac = 0.95f; L.Add(ra);
                    var ua = C(part, sn + "ULNAR ARTERY", TissueKind.Artery, new Vector3(-0.014f * s, 0.1f, 0.02f), new Vector3(-0.016f * s, -0.15f, 0.022f), 0.003f, Artery, 4f, 0.3f);
                    ua.side = s; ua.externalFrac = 0.95f; L.Add(ua);
                    break;
                }
                case PartId.HandL: case PartId.HandR:
                {
                    var hb = E(part, sn + "HAND BONES", TissueKind.Bone, new Vector3(0, -0.02f, 0), new Vector3(0.012f, 0.06f, 0.035f), Bone, 1f, 0.9f);
                    hb.toughness = 40f; hb.side = s; hb.externalFrac = 0.95f; L.Add(hb);
                    break;
                }
                case PartId.ThighL: case PartId.ThighR:
                {
                    var f = C(part, sn + "FEMUR", TissueKind.Bone, new Vector3(0.015f * s, 0.21f, 0), new Vector3(0, -0.21f, 0), 0.016f, Bone, 3f, 1f);
                    f.toughness = 260f; f.side = s; L.Add(f);
                    var fa = C(part, sn + "FEMORAL ARTERY", TissueKind.Artery, new Vector3(-s * 0.025f, 0.22f, 0.035f), new Vector3(-s * 0.01f, -0.2f, -0.02f), 0.0065f, Artery, 28f, 0.3f);
                    fa.side = s; fa.externalFrac = 0.85f; L.Add(fa);
                    var fv = C(part, sn + "FEMORAL VEIN", TissueKind.Vein, new Vector3(-s * 0.035f, 0.22f, 0.03f), new Vector3(-s * 0.018f, -0.2f, -0.025f), 0.0075f, Vein, 14f, 0.2f);
                    fv.side = s; fv.externalFrac = 0.85f; L.Add(fv);
                    break;
                }
                case PartId.ShinL: case PartId.ShinR:
                {
                    var t = C(part, sn + "TIBIA", TissueKind.Bone, new Vector3(0, 0.21f, 0.015f), new Vector3(0, -0.21f, 0.012f), 0.014f, Bone, 1f, 1f);
                    t.toughness = 180f; t.side = s; L.Add(t);
                    var fi = C(part, sn + "FIBULA", TissueKind.Bone, new Vector3(0.025f * s, 0.2f, -0.008f), new Vector3(0.022f * s, -0.2f, -0.005f), 0.007f, Bone, 0.5f, 0.7f);
                    fi.toughness = 60f; fi.side = s; L.Add(fi);
                    var pt = C(part, sn + "TIBIAL ARTERY", TissueKind.Artery, new Vector3(-s * 0.005f, 0.2f, -0.025f), new Vector3(-s * 0.01f, -0.2f, -0.015f), 0.003f, Artery, 5f, 0.3f);
                    pt.side = s; pt.externalFrac = 0.9f; L.Add(pt);
                    break;
                }
                case PartId.FootL: case PartId.FootR:
                {
                    var fb = E(part, sn + "FOOT BONES", TissueKind.Bone, Vector3.zero, new Vector3(0.03f, 0.025f, 0.11f), Bone, 1f, 0.9f);
                    fb.toughness = 50f; fb.side = s; fb.externalFrac = 0.95f; L.Add(fb);
                    break;
                }
            }
            foreach (var o in L)
            {
                if (o.kind == TissueKind.Artery || o.kind == TissueKind.Vein)
                {
                    if (o.side == 0) o.side = s;
                }
            }
            return L;
        }

        static OrganDef Brain(PartId part, string name, BrainRegion region, Vector3 c, Vector3 r, Color col, int side = 0)
        {
            var o = E(part, name, TissueKind.Brain, c, r, col, 2f, 0.2f);
            o.brain = region; o.organ = OrganId.Brain; o.side = side; o.externalFrac = 0.6f;
            return o;
        }
    }
}
