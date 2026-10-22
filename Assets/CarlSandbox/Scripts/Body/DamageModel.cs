using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Carl
{
    public enum HitKind { Bullet, Shrapnel, Stab, Slash, Blunt, Blast }

    /// <summary>
    /// Turns physical hits into anatomy damage. Projectiles are traced through each body part's
    /// organs in local space, losing energy to flesh and bone; what they pass through decides the outcome.
    /// </summary>
    public class DamageModel : MonoBehaviour
    {
        HumanBody body;
        Physiology physio;

        struct OrganHit { public Organ o; public float t0, t1; public bool direct; }
        readonly List<OrganHit> hits = new List<OrganHit>();

        public void Init(HumanBody b)
        {
            body = b;
            physio = b.physio;
        }

        static float FleshResistance(HitKind kind, float diameter)
        {
            switch (kind)
            {
                case HitKind.Shrapnel: return 3200f;
                case HitKind.Stab: return 0f;
                default: return 1450f * Mathf.Clamp(diameter / 0.009f, 0.75f, 1.9f);
            }
        }

        /// <summary>Only the heart and the great vessels bleed at full rate; everything else bleeds a quarter as much.</summary>
        static float BleedScale(OrganDef d)
        {
            if (d.organ == OrganId.Heart) return 0.45f;
            string n = d.name;
            if (n.Contains("AORTA") || n.Contains("VENA CAVA") || n.Contains("CAROTID") || n.Contains("JUGULAR") ||
                n.Contains("FEMORAL") || n.Contains("ILIAC") || n.Contains("SUBCLAVIAN")) return 0.45f;
            return 0.1f;
        }

        static float SeverThreshold(PartId p)
        {
            switch (p)
            {
                // only a .50 round can take off a hand or foot in one hit; everything else has to be dug through
                case PartId.HandL: case PartId.HandR: case PartId.FootL: case PartId.FootR: return 6000f;
                case PartId.ForearmL: case PartId.ForearmR: case PartId.ShinL: case PartId.ShinR: return 11000f;
            }
            return float.MaxValue;
        }

        // =============================================================== projectile

        /// <summary>Returns the energy left after passing through this part.</summary>
        /// <summary>Wound size multiplier of the round being traced (hollow points &gt; 1, AP &lt; 1).</summary>
        public static float ProjectileExpand = 1f;

        /// <summary>A round stopped by armour: bruising, pain and a shove - big rounds can still crack bone behind the plate.</summary>
        public void ApplyArmourTrauma(BodyPart part, Vector3 point, Vector3 dir, float energy)
        {
            float k = Mathf.Clamp01(energy / 4000f);
            var lp = part.transform.InverseTransformPoint(point);
            lp = part.flesh.SnapToSkin(lp, part.transform.InverseTransformDirection(dir));
            part.flesh.Paint(lp, 0.04f + 0.05f * k, 1, 0.5f + k * 0.5f);   // bruise
            part.flesh.Dent(lp, part.transform.InverseTransformDirection(dir), 0.04f, 0.004f + 0.008f * k, 0.6f + k);
            physio.AddPain(0.25f + 0.5f * k, part.id);
            physio.AddStress(0.3f);
            float impulse = 1f + energy * 0.0009f;
            part.rb.AddForceAtPosition(dir * impulse, point, ForceMode.Impulse);
            body.ragdoll.OnHit(part, dir, impulse * 0.8f);
            if (energy > 5000f)
                foreach (var o in part.organs)
                    if (o.IsBone && (o.def.name.Contains("RIB") || o.def.name.Contains("STERNUM")) && Random.value < (energy - 5000f) / 8000f)
                    { o.health = Mathf.Max(0f, o.health - 0.4f); physio.OnOrganDamaged(o, 0.4f, energy * 0.2f); }
        }

        public float ApplyProjectile(BodyPart part, Vector3 entry, Vector3 dir, float energy, float diameter, HitKind kind, out Vector3 exitPoint, out bool exited)
        {
            exited = false;
            exitPoint = entry;
            if (part.severed && part.parent == null) { }
            dir.Normalize();

            // find where the path leaves this part
            const float probe = 1.2f;
            Vector3 exitGuess = entry + dir * 0.05f;
            if (part.col.Raycast(new Ray(entry + dir * probe, -dir), out var bh, probe)) exitGuess = bh.point;
            float pathLen = Mathf.Max(0.01f, Vector3.Distance(entry, exitGuess));

            Vector3 p0 = part.transform.InverseTransformPoint(entry);
            Vector3 p1 = part.transform.InverseTransformPoint(exitGuess);
            float cavity = 0.002f + 0.0004f * Mathf.Sqrt(energy);
            // heavy rounds (.50 BMG) tear an enormous temporary cavity compared with rifle rounds
            if (energy > 9000f) cavity *= 1f + (energy - 9000f) / 6000f;
            cavity *= ProjectileExpand;
            if (kind == HitKind.Shrapnel) cavity *= 0.6f;

            // gather structures along the path
            hits.Clear();
            foreach (var o in part.organs)
            {
                bool direct = o.def.Intersect(p0, p1, 0f, out float d0, out float d1);
                float inflate = o.IsBone ? cavity * 0.35f : cavity;
                if (direct) hits.Add(new OrganHit { o = o, t0 = d0, t1 = d1, direct = true });
                else if (o.def.Intersect(p0, p1, inflate, out float c0, out float c1)) hits.Add(new OrganHit { o = o, t0 = c0, t1 = c1, direct = false });
            }
            hits.Sort((a, b) => a.t0.CompareTo(b.t0));

            // march energy through flesh and bone
            float res = FleshResistance(kind, diameter);
            if (kind == HitKind.Bullet && energy > 1500f) res *= 1.25f; // rifle rounds yaw and tumble
            float E = energy;
            float stopT = 1f;
            float lastT = 0f;
            var damaged = new List<(Organ o, float dmg)>();
            foreach (var h in hits)
            {
                float fleshCost = (h.t0 - lastT) * pathLen * res;
                if (fleshCost >= E) { stopT = lastT + E / (pathLen * res); E = 0; break; }
                E -= fleshCost;
                lastT = h.t0;
                float organLen = (h.t1 - h.t0) * pathLen;
                float sev = kind == HitKind.Shrapnel ? Mathf.Clamp01(E / 250f) : Mathf.Clamp01(0.3f + E / 1400f);
                float dmg = sev * Mathf.Clamp01(0.35f + organLen / Mathf.Max(0.008f, h.o.def.Size) * 0.8f);
                if (!h.direct)
                {
                    // temporary-cavity damage: bruises nearby tissue, only nicks vessels and cord
                    dmg *= 0.55f;
                    var k = h.o.def.kind;
                    if (k == TissueKind.Artery || k == TissueKind.Vein || k == TissueKind.Spine) dmg = Mathf.Min(dmg, 0.3f);
                }
                if (h.o.IsBone && h.direct)
                {
                    float tough = Mathf.Max(20f, h.o.def.toughness);
                    dmg = Mathf.Clamp01(E / tough * 0.55f) * (h.o.def.shell ? 0.5f : 1f);
                    E -= Mathf.Min(E, tough * (h.o.def.shell ? 0.4f : 1f));
                    if (E <= 0f) { stopT = h.t0; damaged.Add((h.o, dmg)); break; }
                }
                damaged.Add((h.o, dmg));
            }
            if (E > 0f)
            {
                float tail = (1f - lastT) * pathLen * res;
                if (tail >= E) { stopT = lastT + E / (pathLen * res); E = 0f; }
                else E -= tail;
            }
            exited = E > 0f;
            exitPoint = exited ? exitGuess : Vector3.Lerp(entry, exitGuess, Mathf.Clamp01(stopT));
            float deposited = energy - E;

            // apply to structures
            float bleedEntry = 0f, bleedExit = 0f, intBleed = 0f, painSum = 0f;
            bool arterialEntry = false, arterialExit = false;
            foreach (var (o, dmg) in damaged)
            {
                float before = o.health;
                o.health = Mathf.Max(0f, o.health - dmg);
                float delta = before - o.health;
                float bleed = 0f;
                bool vessel = o.def.kind == TissueKind.Artery || o.def.kind == TissueKind.Vein;
                if (vessel)
                {
                    bool cut = o.health < 0.55f;
                    if (cut && !o.severed) { o.severed = true; bleed = o.def.bleed * BleedScale(o.def); }
                    else if (!o.severed) bleed = o.def.bleed * 0.3f * delta * BleedScale(o.def);
                }
                else bleed = o.def.bleed * delta * BleedScale(o.def);
                float ext = bleed * o.def.externalFrac;
                float inn = bleed - ext;
                intBleed += inn;
                // send the external share to the nearest hole
                float t = 0.5f;
                foreach (var h in hits) if (h.o == o) { t = (h.t0 + h.t1) * 0.5f; break; }
                bool toExit = exited && t > 0.5f;
                if (toExit) { bleedExit += ext; arterialExit |= o.def.kind == TissueKind.Artery && o.severed; }
                else { bleedEntry += ext; arterialEntry |= o.def.kind == TissueKind.Artery && o.severed; }
                painSum += o.def.pain * delta;
                physio.OnOrganDamaged(o, delta, deposited);
                // the bone you can see breaks right where the round went through it: a crack for a glancing
                // hit, snapped and splintered for a solid one (worse each time it is hit there)
                if (o.IsBone && delta > 0.004f)
                {
                    float tb2 = 0.5f;
                    foreach (var h in hits) if (h.o == o) { tb2 = (h.t0 + h.t1) * 0.5f; break; }
                    BoneBreaker.Break(o, Vector3.Lerp(p0, p1, tb2), Mathf.Clamp01(0.08f + delta * 1.6f + (o.Fractured ? 0.25f : 0f)));
                }
                if (o.IsBone && o.Fractured && o.def.capsule)
                {
                    float tb = 0.5f;
                    foreach (var h in hits) if (h.o == o) { tb = (h.t0 + h.t1) * 0.5f; break; }
                    body.SplitBone(o, Vector3.Lerp(p0, p1, tb));
                }
            }

            // soft tissue
            float energyScale = Mathf.Clamp(deposited / 400f, 0.2f, 3f);
            part.tissueDamage = Mathf.Clamp01(part.tissueDamage + 0.08f * energyScale + pathLen * 0.6f);
            bleedEntry += (0.35f + diameter * 70f) * 0.1f;
            if (exited) bleedExit += (0.5f + deposited / 700f) * 0.1f;

            var entryW = AddWound(part, entry, -dir, kind == HitKind.Shrapnel ? WoundType.Blast : WoundType.Entry, bleedEntry, intBleed, arterialEntry);
            entryW.desc = kind == HitKind.Shrapnel ? "SHRAPNEL" : "ENTRY WOUND";
            if (exited)
            {
                var exitW = AddWound(part, exitPoint, dir, WoundType.Exit, bleedExit, 0f, arterialExit);
                exitW.desc = "EXIT WOUND";
            }

            // pain, stress and balance
            painSum += 0.18f + 0.12f * energyScale;
            physio.AddPain(Mathf.Clamp01(painSum), part.id);
            physio.AddStress(0.25f);

            // physics: momentum + flesh deformation
            float impulse = 1.5f + deposited * 0.0065f;
            if (kind == HitKind.Shrapnel) impulse *= 0.6f;
            part.rb.AddForceAtPosition(dir * impulse, entry, ForceMode.Impulse);
            body.ragdoll.OnHit(part, dir, impulse);

            var lp = part.transform.InverseTransformPoint(entry);
            var ld = part.transform.InverseTransformDirection(dir);
            lp = part.flesh.SnapToSkin(lp, ld);
            // entry: small punched hole with a bruised collar; exit: torn, everted crater
            part.flesh.Dent(lp, ld, 0.02f + cavity * 0.6f, 0.006f + cavity * 0.35f, 0.4f + deposited / 1400f);
            part.flesh.Paint(lp, 0.005f + cavity * 0.1f, 3, 0.8f);
            // a round arriving at a slant leaves an oval hole stretched along its path
            Vector3 skinN = part.flesh.RestNormalAt(lp);
            float slant = Mathf.Abs(Vector3.Dot(ld.normalized, skinN));
            Vector3 entryStretch = Vector3.ProjectOnPlane(ld, skinN).normalized * Mathf.Clamp(1f / Mathf.Max(0.5f, slant), 1f, 1.9f);
            if (kind == HitKind.Shrapnel) part.AddHole(lp, 0.005f + cavity * 0.22f, 0.02f + cavity * 0.6f, true, entryStretch * 0.6f);
            else part.AddHole(lp, 0.006f + cavity * 0.18f, 0.03f + cavity * 0.7f, true, entryStretch, ld);
            Vector3 lex = exited ? part.flesh.SnapToSkin(part.transform.InverseTransformPoint(exitPoint), ld) : Vector3.zero;
            if (exited) part.AddHole(lex, 0.01f + cavity * 0.5f, 0.03f + cavity * 0.9f, true, default, -ld);
            part.flesh.Paint(lp, 0.02f + cavity * 0.6f, 0, 0.6f);
            part.flesh.Paint(lp, 0.045f + cavity, 1, 0.2f);
            if (exited)
            {
                var le = lex;
                part.flesh.Crater(le, ld, 0.02f + cavity * 0.9f, 0.012f + cavity * 0.7f, 0.6f + deposited / 1200f);
                part.flesh.Paint(le, 0.008f + cavity * 0.3f, 3, 0.8f);
                part.flesh.Paint(le, 0.03f + cavity * 1.2f, 0, 0.8f);
            }
            part.bloodiness = Mathf.Min(1.5f, part.bloodiness + 0.4f);

            // blood spray
            var bs = BloodSystem.Instance;
            if (bs != null)
            {
                bs.Spray(entry, -dir, 1 + (int)(deposited / 400f), 1.5f, 3.5f, 0.6f, 0.005f, part);
                if (exited) bs.Spray(exitPoint, dir, Mathf.Min(45, 3 + (int)(deposited / 90f)), 3f, 4f + Mathf.Min(10f, energy / 600f), 0.35f, 0.007f, part);
                if (part.id == PartId.Head && deposited > 1500f) bs.Mist(exitPoint, dir, 40);
            }

            // limb loss: only where the round has destroyed the cross-section (meat, skin, bone) of the limb
            var li = LimbIntegrity.Of(part);
            if (li != null)
            {
                float R = Mathf.Max(0.02f, part.uvRadius);
                float rc = 0.004f + cavity * 0.4f;                                   // permanent wound channel
                float tissueFrac = 1.2f * rc * pathLen * Mathf.Clamp01(stopT) / (Mathf.PI * R * R);
                float skinR = (0.006f + cavity * 0.18f) + (exited ? 0.01f + cavity * 0.5f : 0f);
                float skinFrac = 2f * skinR / (2f * Mathf.PI * R);
                float boneDmg = 0f;
                foreach (var (o, d) in damaged) if (o.IsBone && !o.def.shell) boneDmg += d * 1.6f;
                li.Damage(Vector3.Lerp(p0, p1, 0.5f), tissueFrac, skinFrac, boneDmg, dir);
            }

            RecordHit(part, entryW, damaged);
            return E;
        }

        /// <summary>
        /// A limb comes off once enough of its flesh is shot or cut away and every bone in it is broken,
        /// e.g. a line of rifle hits across an arm, or repeated knife slashes.
        /// </summary>
        void CheckDismember(BodyPart part, Vector3 dir)
        {
            if (part.severed || part.joint == null) return;
            if (!Anatomy.IsArm(part.id) && !Anatomy.IsLeg(part.id) && part.id != PartId.Neck) return;
            bool bonesGone = true;
            foreach (var o in part.organs) if (o.IsBone && !o.def.shell && !o.BadlyBroken) bonesGone = false;
            // the damage has to be in one place: a single wound torn most of the way across the limb
            if (bonesGone && part.tissueDamage >= 0.85f && part.WorstHoleFraction >= 0.8f) Sever(part, dir, 3f);
        }

        static bool AnyBoneBroken(BodyPart p)
        {
            foreach (var o in p.organs) if (o.IsBone && o.BadlyBroken) return true;
            return false;
        }

        // =============================================================== knife

        public void ApplyStab(BodyPart part, Vector3 entry, Vector3 dir, float depth)
        {
            dir.Normalize();
            Vector3 p0 = part.transform.InverseTransformPoint(entry);
            Vector3 p1 = part.transform.InverseTransformPoint(entry + dir * depth);
            hits.Clear();
            foreach (var o in part.organs)
                if (o.def.Intersect(p0, p1, 0.004f, out float t0, out float t1))
                    hits.Add(new OrganHit { o = o, t0 = t0, t1 = t1, direct = true });
            hits.Sort((a, b) => a.t0.CompareTo(b.t0));

            float ext = 0.15f, inn = 0f, painSum = 0.4f; bool arterial = false;
            var damaged = new List<(Organ o, float dmg)>();
            foreach (var h in hits)
            {
                var o = h.o;
                if (o.IsBone && o.def.toughness > 110f && !o.def.shell)
                {
                    // blade stops on heavy bone
                    float d = 0.15f;
                    o.health = Mathf.Max(0f, o.health - d);
                    damaged.Add((o, d));
                    break;
                }
                float dmg = Mathf.Clamp01(0.45f + (h.t1 - h.t0) * depth / Mathf.Max(0.01f, o.def.Size) * 0.6f);
                float before = o.health;
                o.health = Mathf.Max(0f, o.health - dmg);
                float delta = before - o.health;
                float bleed;
                if (o.def.kind == TissueKind.Artery || o.def.kind == TissueKind.Vein)
                {
                    o.severed = true; bleed = o.def.bleed * 0.8f * BleedScale(o.def);
                    arterial |= o.def.kind == TissueKind.Artery;
                }
                else bleed = o.def.bleed * delta * BleedScale(o.def);
                ext += bleed * o.def.externalFrac;
                inn += bleed * (1f - o.def.externalFrac);
                painSum += o.def.pain * delta;
                damaged.Add((o, delta));
                physio.OnOrganDamaged(o, delta, 200f);
            }
            part.tissueDamage = Mathf.Clamp01(part.tissueDamage + 0.12f);
            Vector3 le;
            {
                le = part.flesh.SnapToSkin(part.transform.InverseTransformPoint(entry), part.transform.InverseTransformDirection(dir));
                // a blade-sized slit (about 2.5 cm long, a few mm wide) that goes deep
                Vector3 li = part.transform.InverseTransformDirection(dir);
                Vector3 bladeEdge = Vector3.ProjectOnPlane(part.transform.InverseTransformDirection(Vector3.Cross(dir, Vector3.up).sqrMagnitude > 0.01f ? Vector3.up : Vector3.right), li).normalized;
                const float slitW = 0.005f, slitHalf = 0.017f;
                part.AddHole(le, slitW, depth * 0.8f, false, bladeEdge * (slitHalf / slitW), li);
            }
            LimbIntegrity.Of(part)?.Damage(le, 0.05f, 0.03f, 0f, dir);
            var w = AddWound(part, entry, -dir, WoundType.Stab, ext, inn, arterial);
            w.desc = "STAB WOUND";
            physio.AddPain(Mathf.Clamp01(painSum), part.id);
            physio.AddStress(0.35f);
            part.rb.AddForceAtPosition(dir * 6f, entry, ForceMode.Impulse);
            body.ragdoll.OnHit(part, dir, 6f);
            var lp = le;
            part.flesh.Dent(lp, part.transform.InverseTransformDirection(dir), 0.012f, 0.004f, 0.3f);
            part.flesh.Paint(lp, 0.006f, 3, 0.7f);
            part.flesh.Paint(lp, 0.05f, 0, 1f);
            part.bloodiness = Mathf.Min(1.5f, part.bloodiness + 0.5f);
            BloodSystem.Instance?.Spray(entry, -dir, 3, 1f, 2.5f, 0.6f, 0.006f, part);
            RecordHit(part, w, damaged);
        }

        public void ApplySlash(BodyPart part, Vector3 point, Vector3 surfaceNormal, Vector3 sweep)
        {
            Vector3 inward = -surfaceNormal.normalized;
            Vector3 a = point - sweep * 0.05f + inward * 0.012f;
            Vector3 b = point + sweep * 0.05f + inward * 0.012f;
            Vector3 la = part.transform.InverseTransformPoint(a), lb = part.transform.InverseTransformPoint(b);
            float ext = 0.18f; bool arterial = false; float painSum = 0.3f;
            var damaged = new List<(Organ o, float dmg)>();
            foreach (var o in part.organs)
            {
                var k = o.def.kind;
                if (k != TissueKind.Artery && k != TissueKind.Vein && k != TissueKind.Airway && k != TissueKind.Eye) continue;
                if (!o.def.Intersect(la, lb, 0.014f, out _, out _)) continue;
                o.health = Mathf.Max(0f, o.health - 0.7f);
                if (k == TissueKind.Artery || k == TissueKind.Vein)
                {
                    o.severed = true;
                    ext += o.def.bleed * 0.85f * BleedScale(o.def);
                    arterial |= k == TissueKind.Artery;
                }
                painSum += o.def.pain * 0.5f;
                damaged.Add((o, 0.7f));
                physio.OnOrganDamaged(o, 0.7f, 100f);
            }
            part.tissueDamage = Mathf.Clamp01(part.tissueDamage + 0.1f);
            {
                Vector3 lp0 = part.flesh.SnapToSkin(part.transform.InverseTransformPoint(point), part.transform.InverseTransformDirection(inward));
                Vector3 li0 = part.transform.InverseTransformDirection(inward);
                // run the gash along the skin, not off into the air where the limb curves away
                Vector3 ls0 = Vector3.ProjectOnPlane(part.transform.InverseTransformDirection(sweep), li0).normalized;
                // one long smooth slit. A fresh cut opens skin and fat; cutting the same line again slices it
                // deeper (and a little wider) each time
                const float halfLen = 0.026f;
                float prior = part.CutDepthAt(lp0);
                float depth = 0.008f + prior * 0.9f + (prior > 0f ? 0.006f : 0f);
                float width = 0.0042f + Mathf.Min(prior, 0.03f) * 0.2f;
                if (!part.DeepenSlit(lp0, ls0, width, depth))
                    part.AddHole(lp0, width, depth, false, ls0 * (halfLen / width));
                foreach (var o in part.organs)
                    if (o.IsBone && !o.def.shell && part.tissueDamage > 0.4f)
                    {
                        o.health = Mathf.Max(0f, o.health - 0.14f);
                        if (o.Fractured && o.def.capsule) body.SplitBone(o, lp0);
                    }
            }
            {
                // each cut through the same line goes deeper: through skin, then muscle, then (hacking) bone
                var lpc = part.flesh.SnapToSkin(part.transform.InverseTransformPoint(point), part.transform.InverseTransformDirection(inward));
                float R = Mathf.Max(0.02f, part.uvRadius);
                float cutDepth = part.CutDepthAt(lpc);
                LimbIntegrity.Of(part)?.Damage(lpc, Mathf.Clamp01(cutDepth / (2f * R)) * 0.6f + 0.05f, 0.12f, part.tissueDamage > 0.4f ? 0.25f : 0f, sweep);
            }
            var w = AddWound(part, point, surfaceNormal, WoundType.Laceration, ext, 0f, arterial);
            w.desc = "LACERATION";
            physio.AddPain(painSum, part.id);
            physio.AddStress(0.3f);
            body.ragdoll.OnHit(part, sweep, 2f);
            var lp = part.flesh.SnapToSkin(part.transform.InverseTransformPoint(point), part.transform.InverseTransformDirection(inward));
            var ls = part.transform.InverseTransformDirection(sweep);
            part.flesh.Groove(lp - ls * 0.05f, lp + ls * 0.05f, part.transform.InverseTransformDirection(inward), 0.011f, 0.009f);
            for (int g = 0; g <= 4; g++) part.flesh.Paint(lp + ls * (g * 0.025f - 0.05f), 0.012f, 3, 1f);
            part.flesh.Paint(lp, 0.06f, 0, 0.8f);
            part.bloodiness = Mathf.Min(1.5f, part.bloodiness + 0.3f);
            BloodSystem.Instance?.Spray(point, surfaceNormal + sweep, 5, 1f, 3f, 0.5f, 0.005f, part);
            RecordHit(part, w, damaged);
        }

        // =============================================================== blunt and blast

        public void ApplyImpact(BodyPart part, Vector3 point, Vector3 normal, float speed, bool fromBody)
        {
            if (part.severed && part.parent == null) return;
            // footsteps and knees knocking the floor while upright are not injuries
            if (Anatomy.IsLeg(part.id) && body.ragdoll.mode != BodyMode.Down && speed < 9f) return;
            if (speed < BodyPart.ImpactHurtSpeed) return;
            // 0 at about a 1.5 m fall, 1 at about a 6 m fall (13.5 m/s)
            float s = Mathf.Clamp01((speed - BodyPart.ImpactHurtSpeed) / 8f);
            var lp = part.transform.InverseTransformPoint(point);
            var ln = part.transform.InverseTransformDirection(-normal);
            part.flesh.Dent(lp, ln, 0.05f, 0.003f * s, 0.6f * s + 0.2f);
            if (speed > 7f) part.flesh.Paint(lp, 0.03f + 0.02f * s, 1, 0.06f + 0.2f * s);
            // a knock that only bruises hurts a little; it takes a real fall to make someone cry out
            physio.AddPain(0.02f + 0.3f * s * s + 0.1f * s, part.id);
            if (body.Dead) return;

            if (part.id == PartId.Head)
            {
                physio.AddConcussion(speed > 6.5f ? 0.05f + s * 0.6f : 0f);
                if (speed > 8f)
                {
                    var w = AddWound(part, point, normal, WoundType.Laceration, 1f + s * 2f, 0f, false);
                    w.desc = "SCALP LACERATION";
                    part.flesh.Paint(lp, 0.06f, 0, 0.7f);
                }
                if (speed > 11f)
                {
                    var skull = part.Find("SKULL");
                    if (skull != null) skull.health = Mathf.Max(0f, skull.health - s * 0.4f);
                    physio.brainTrauma = Mathf.Clamp01(physio.brainTrauma + s * 0.15f);
                }
            }
            if (speed > 9f && !fromBody)
            {
                var w = AddWound(part, point, normal, WoundType.Abrasion, 0.04f + s * 0.1f, 0f, false);
                w.desc = "SCRAPES";
                w.clotRate = 1f / 30f;
                part.flesh.Paint(lp, 0.06f, 2, 0.6f);
            }
            if (Anatomy.IsTorso(part.id) && speed > 9.5f)
            {
                // falls and hard knocks rupture solid organs
                foreach (var o in part.organs)
                {
                    if (o.def.kind != TissueKind.Organ) continue;
                    if (o.def.organ != OrganId.Liver && o.def.organ != OrganId.Spleen && o.def.organ != OrganId.Kidney && o.def.organ != OrganId.Lungs) continue;
                    if (Random.value > s * 0.6f) continue;
                    float d = 0.1f + s * 0.3f;
                    o.health = Mathf.Max(0f, o.health - d);
                    var w = AddWound(part, part.transform.TransformPoint(o.def.a), normal, WoundType.Internal, 0f, o.def.bleed * d * 0.6f, false);
                    w.desc = "INTERNAL BLEEDING (" + o.def.name + ")";
                    physio.OnOrganDamaged(o, d, 100f);
                }
            }
            if (speed > 10f)
            {
                foreach (var o in part.organs)
                {
                    if (!o.IsBone || o.def.shell && part.id == PartId.Head) continue;
                    if (Random.value > s * 0.5f) continue;
                    o.health = Mathf.Max(0f, o.health - s * 0.7f);
                    physio.OnOrganDamaged(o, s * 0.7f, 50f);
                    physio.AddPain(0.4f, part.id);
                }
            }
            if (speed > 8f) RecordText(part, $"{part.Name} / BLUNT IMPACT {speed:0.0} M/S");
        }

        public void ApplyBlast(BodyPart part, Vector3 origin, float intensity, float surfaceDist = 10f, bool mayLoseLimb = true)
        {
            if (intensity <= 0.02f) return;
            if (surfaceDist < 0.35f && !part.severed) { PointBlank(part, origin, surfaceDist, mayLoseLimb); return; }
            Vector3 dir = (part.transform.position - origin).normalized;
            var lp = part.transform.InverseTransformPoint(part.col.ClosestPoint(origin));
            lp = part.flesh.SnapToSkin(lp, part.transform.InverseTransformDirection(dir));
            part.flesh.Paint(lp, 0.09f, 1, intensity * 0.5f);
            part.flesh.Paint(lp, 0.08f, 2, intensity * 0.35f);
            part.flesh.Jiggle(part.transform.InverseTransformDirection(dir), intensity * 2f);
            if (intensity > 0.45f)
            {
                // blast tears a ragged crater straight into the flesh facing the explosion
                float br = 0.015f + 0.05f * (intensity - 0.45f);
                part.flesh.Dent(lp, -part.flesh.RestNormalAt(lp), br * 2f, br * 0.6f, 0.4f);
                part.flesh.Paint(lp, br * 1.6f, 3, 1f);
                part.AddHole(lp, br, br * 2.5f);
                float x = intensity - 0.45f;
                LimbIntegrity.Of(part)?.Damage(lp, x * 1.1f, x * 0.8f, x * 1.4f, dir);
            }
            physio.AddPain(intensity * 0.3f, part.id);
            physio.AddStress(0.4f);
            if (part.id == PartId.Head) physio.AddConcussion(intensity * 0.7f);
            if (part.id == PartId.Chest)
            {
                foreach (var o in part.organs)
                    if (o.def.organ == OrganId.Lungs)
                    {
                        float d = intensity * 0.35f;
                        o.health = Mathf.Max(0f, o.health - d);
                        physio.OnOrganDamaged(o, d, 300f);
                        if (d > 0.1f) { var w = AddWound(part, part.transform.TransformPoint(o.def.a), dir, WoundType.Internal, 0f, 3f * d, false); w.desc = "BLAST LUNG"; }
                    }
            }
        }

        /// <summary>
        /// A grenade going off against the body: rips a large hole through whatever it touches (belly, chest,
        /// head), wrecks the organs under it, and blows a limb off where it sat.
        /// </summary>
        void PointBlank(BodyPart part, Vector3 origin, float dist, bool mayLoseLimb)
        {
            float k = Mathf.InverseLerp(0.35f, 0.04f, dist);            // 1 = touching
            Vector3 dir = (part.transform.position - origin);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.down;
            dir.Normalize();
            Vector3 worldHit = part.col != null ? part.col.ClosestPoint(origin) : part.transform.position;
            var lp = part.flesh.SnapToSkin(part.transform.InverseTransformPoint(worldHit), part.transform.InverseTransformDirection(dir));
            Vector3 inward = -part.flesh.RestNormalAt(lp);
            Vector3 worldIn = part.transform.TransformDirection(inward);

            // the torn crater: one big opening ringed by smaller tears, flesh caved in around it
            float R = Mathf.Lerp(0.025f, Mathf.Max(0.04f, part.uvRadius * 0.85f), k);
            float depth = Mathf.Lerp(0.03f, part.uvRadius * 1.3f, k);
            part.flesh.Dent(lp, inward, R * 1.9f, R * 0.35f, 1.5f);
            part.flesh.Paint(lp, R * 1.8f, 3, 1f);
            part.flesh.Paint(lp, R * 2.6f, 1, 0.7f);
            part.flesh.Paint(lp, R * 2.2f, 0, 1f);
            part.AddHole(lp, R, depth);
            Vector3 t1 = Vector3.Cross(inward, Mathf.Abs(inward.y) < 0.9f ? Vector3.up : Vector3.right).normalized, t2 = Vector3.Cross(inward, t1);
            // extra tears join into the same hole, giving it a ragged, uneven outline
            int tears = Mathf.RoundToInt(Mathf.Lerp(1f, 3f, k));
            for (int i = 0; i < tears; i++)
            {
                float a = (i + Random.value * 0.6f) / tears * Mathf.PI * 2f;
                Vector3 off = (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * R * Random.Range(0.6f, 0.9f);
                part.AddHole(part.flesh.SnapToSkin(lp + off, inward), R * Random.Range(0.35f, 0.5f), depth * Random.Range(0.4f, 0.6f));
            }

            // organs and bone under the blast are wrecked
            float ext = 0f, inn = 0f; bool arterial = false;
            var damaged = new List<(Organ o, float dmg)>();
            foreach (var o in part.organs)
            {
                // nearest point of the structure (a long bone is near the blast where it passes under it)
                Vector3 oc = o.def.a;
                if (o.def.capsule)
                {
                    Vector3 sab = o.def.b - o.def.a;
                    oc = o.def.a + sab * Mathf.Clamp01(Vector3.Dot(lp - o.def.a, sab) / Mathf.Max(1e-6f, sab.sqrMagnitude));
                }
                float od = Mathf.Max(0f, Vector3.Distance(oc, lp) - (o.def.capsule ? o.def.radii.x : 0f));
                float reach = R * 1.6f + o.def.Size * 0.5f + 0.03f;
                if (od > reach) continue;
                float dmg = Mathf.Clamp01(k * (1.1f - od / reach));
                float before = o.health;
                o.health = Mathf.Max(0f, o.health - dmg);
                float delta = before - o.health;
                if (o.def.kind == TissueKind.Artery || o.def.kind == TissueKind.Vein) { o.severed = true; arterial |= o.def.kind == TissueKind.Artery; }
                float bleed = o.def.bleed * delta * BleedScale(o.def) * 1.5f;
                ext += bleed * 0.6f; inn += bleed * 0.4f;
                damaged.Add((o, delta));
                physio.OnOrganDamaged(o, delta, 900f);
                // bone under the blast: snapped, a chunk blown out, or shattered into splinters
                if (o.IsBone && o.def.capsule)
                {
                    Vector3 ba = o.def.a, bab = o.def.b - o.def.a;
                    Vector3 near = ba + bab * Mathf.Clamp01(Vector3.Dot(lp - ba, bab) / bab.sqrMagnitude);
                    BoneBreaker.Break(o, near, Mathf.Clamp01(dmg * 1.2f + 0.15f));
                }
            }
            part.tissueDamage = Mathf.Clamp01(part.tissueDamage + 0.6f * k);
            var w = AddWound(part, worldHit, -worldIn, WoundType.Blast, 0.4f + ext, inn, arterial);
            w.desc = k > 0.6f ? "BLAST - TISSUE DESTROYED" : "BLAST WOUND";
            physio.AddPain(1f, part.id);
            physio.AddStress(1f);
            if (part.id == PartId.Head) physio.AddConcussion(1f);

            BloodSystem.Instance?.Spray(worldHit, -worldIn + Random.insideUnitSphere * 0.4f, Mathf.RoundToInt(Mathf.Lerp(10f, 45f, k)), 2f, 7f, 0.9f, 0.008f, part);
            // what the crater used to hold is thrown out of it
            Gib.Burst(worldHit, -worldIn, Mathf.RoundToInt(Mathf.Lerp(3f, 16f, k)), Mathf.Lerp(0.015f, 0.04f, k), Mathf.Lerp(3f, 10f, k), part.organs.Exists(o => o.IsBone));
            part.bloodiness = Mathf.Min(1.5f, part.bloodiness + 0.8f);
            RecordHit(part, w, damaged);

            // a limb (or the neck) is destroyed right where the grenade sat - touching it takes it clean off there
            if (mayLoseLimb) LimbIntegrity.Of(part)?.Damage(lp, k * 1.4f, k * 1.2f, k * 1.6f, dir);
            part.rb.AddForceAtPosition(dir * 25f * k, worldHit, ForceMode.Impulse);
        }

        // =============================================================== limb loss

        public static Organ NearestBone(BodyPart p, Vector3 local)
        {
            Organ best = null; float bd = 0.07f;
            foreach (var o in p.organs)
            {
                if (!o.IsBone || o.def.shell || !o.def.capsule) continue;
                Vector3 ab = o.def.b - o.def.a;
                Vector3 q = o.def.a + ab * Mathf.Clamp01(Vector3.Dot(local - o.def.a, ab) / ab.sqrMagnitude);
                float d = Vector3.Distance(q, local);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        /// <summary>How far short of the joint a limb torn off there ends (m).</summary>
        const float JointTear = 0.014f;

        public void Sever(BodyPart part, Vector3 dir, float force)
        {
            if (part.severed || part.id == PartId.Pelvis || part.joint == null) return;
            var parent = part.parent;
            // where the joint is on the parent (its rest position there): the limb itself may already have been
            // shoved out of place by the blast that is taking it off, which put the stump in the middle of the arm
            Vector3 stumpWorld = part.joint.connectedBody == parent.rb
                ? parent.transform.TransformPoint(part.joint.connectedAnchor)
                : part.transform.TransformPoint(part.jointLocal);
            Destroy(part.joint);
            part.joint = null;
            foreach (var d in body.Descendants(part)) d.severed = true;
            part.rb.AddForce(dir.normalized * force, ForceMode.Impulse);

            float stumpBleed;
            bool arterial = true;
            switch (part.id)
            {
                case PartId.Neck: case PartId.Head: stumpBleed = 35f; break;
                case PartId.ThighL: case PartId.ThighR: stumpBleed = 18f; break;
                case PartId.UpperArmL: case PartId.UpperArmR: stumpBleed = 5f; break;
                case PartId.ShinL: case PartId.ShinR: stumpBleed = 5f; break;
                case PartId.ForearmL: case PartId.ForearmR: stumpBleed = 3f; break;
                default: stumpBleed = 1.2f; arterial = false; break;
            }
            var w = AddWound(parent, stumpWorld, (stumpWorld - parent.transform.position).normalized, WoundType.Amputation, stumpBleed, 0f, arterial);
            w.desc = "TRAUMATIC AMPUTATION";
            parent.AddHole(parent.transform.InverseTransformPoint(stumpWorld), Mathf.Max(0.035f, part.uvRadius * 1.1f));
            part.AddHole(part.jointLocal, Mathf.Max(0.035f, part.uvRadius * 1.1f));
            // torn, choppy ends: lumpy meat, splintered bone, ragged skin (never a clean cut). Both sides are torn
            // away just short of the joint, so the cap is the end of the limb rather than sitting inside its
            // rounded end under closed layers of flesh. (The trunk is not cut back: a shoulder or hip socket
            // stays a socket.)
            {
                var cb = part.skin.GetComponent<MeshFilter>().sharedMesh.bounds.center;
                Vector3 nC = (part.jointLocal - cb).normalized;
                Vector3 cutC = part.jointLocal - nC * JointTear;
                LimbIntegrity.ClipAt(part, cutC, nC);
                StumpCap.Build(part, cutC, nC, part.uvRadius * 1.02f, NearestBone(part, cutC));
                var plp = parent.transform.InverseTransformPoint(stumpWorld);
                Vector3 nP = parent.transform.InverseTransformDirection(part.transform.position - stumpWorld).normalized;
                bool limbParent = !Anatomy.IsTorso(parent.id);
                Vector3 cutP = limbParent ? plp - nP * JointTear : plp;
                if (limbParent) LimbIntegrity.ClipAt(parent, cutP, nP);
                StumpCap.Build(parent, cutP, nP, Mathf.Max(part.uvRadius, limbParent ? parent.uvRadius * 0.9f : 0f) * 1.02f, NearestBone(parent, cutP));
            }
            var lw = AddWound(part, stumpWorld, (stumpWorld - part.transform.position).normalized, WoundType.Amputation, stumpBleed * 0.4f, 0f, arterial);
            lw.desc = "SEVERED END";
            lw.clotRate = 1f / 20f;
            var lp = parent.transform.InverseTransformPoint(stumpWorld);
            parent.flesh.Paint(lp, 0.08f, 0, 1f);
            part.flesh.Paint(part.jointLocal, 0.08f, 0, 1f);
            parent.flesh.Paint(lp, 0.07f, 3, 1f);
            part.flesh.Paint(part.jointLocal, 0.07f, 3, 1f);
            parent.flesh.Dent(lp, -lp.normalized, 0.06f, 0.03f, 0.5f);
            part.flesh.Dent(part.jointLocal, -part.jointLocal.normalized, 0.06f, 0.03f, 0.5f);
            part.bloodiness = parent.bloodiness = 1.2f;
            physio.AddPain(1f, parent.id);
            physio.AddStress(1f);
            BloodSystem.Instance?.Spray(stumpWorld, dir, 45, 2f, 6f, 0.7f, 0.008f, parent);
            // torn off, not cut: bits of the joint go flying (more the harder it was ripped away)
            Gib.Burst(stumpWorld, dir, Mathf.Clamp(4 + Mathf.RoundToInt(force * 0.15f), 4, 14), Mathf.Clamp(part.uvRadius * 0.45f, 0.012f, 0.04f), Mathf.Clamp(2f + force * 0.08f, 2f, 9f));
            if (part.id == PartId.Neck || part.id == PartId.Head) physio.Kill("DECAPITATION", true);
            body.ragdoll.OnSevered(part);
            RecordText(parent, $"{part.Name} SEVERED / TRAUMATIC AMPUTATION");
        }

        // =============================================================== helpers

        public Wound AddWound(BodyPart part, Vector3 worldPos, Vector3 worldNormal, WoundType type, float ext, float inn, bool arterial)
        {
            var w = new Wound
            {
                part = part,
                localPos = part.transform.InverseTransformPoint(worldPos),
                localNormal = part.transform.InverseTransformDirection(worldNormal).normalized,
                type = type,
                extRate = ext,
                intRate = inn,
                arterial = arterial,
                venous = !arterial && ext > 6f,
                clotRate = arterial ? 0f : ext > 10f ? 1f / 300f : 1f / 90f,
            };
            part.wounds.Add(w);
            return w;
        }

        void RecordHit(BodyPart part, Wound w, List<(Organ o, float dmg)> damaged)
        {
            var sb = new StringBuilder();
            sb.Append(part.Name).Append(" / ").Append(w.Severity());
            Organ worst = null; float score = 0f;
            foreach (var (o, dmg) in damaged)
            {
                float s = dmg;
                switch (o.def.kind)
                {
                    case TissueKind.Brain: s += 3f; break;
                    case TissueKind.Spine: s += o.severed ? 2.5f : 0.5f; break;
                    case TissueKind.Artery: s += o.severed ? 2f : 0.3f; break;
                    case TissueKind.Organ: s += o.def.organ == OrganId.Heart ? 2.4f : 1f; break;
                    case TissueKind.Bone: s += o.Fractured ? 1.2f : 0.2f; break;
                    default: s += 0.5f; break;
                }
                if (s > score) { score = s; worst = o; }
            }
            if (worst != null) sb.Append(" / ").Append(worst.def.name).Append(' ').Append(worst.StatusText());
            body.lastHit = sb.ToString();
            body.lastHitTime = Time.time;
            if (ActiveRagdoll.DebugFalls)
            {
                var hitList = new StringBuilder();
                foreach (var (o, dmg) in damaged) hitList.Append(o.def.name).Append(' ').Append((dmg * 100f).ToString("0")).Append("% ");
                Debug.Log($"[Carl] {body.DisplayName} HIT {body.lastHit} | organs: {hitList} | posture {physio.posture} conc {physio.consciousness:0.00} dead {physio.dead} {physio.causeOfDeath}");
            }
        }

        void RecordText(BodyPart part, string text)
        {
            body.lastHit = text;
            body.lastHitTime = Time.time;
        }
    }
}
