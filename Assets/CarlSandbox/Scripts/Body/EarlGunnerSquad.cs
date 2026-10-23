using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    public enum SquadOrder { None, Follow, Move, Hold, Heal, HelpUp }

    /// <summary>
    /// Your squad, and what any Earl does for the wounded: a friendly you've recruited (hold R on him) follows
    /// you, goes where you point, holds there; a medic patches up whoever is bleeding on his side when there's
    /// no fighting (or whoever you send him to), and anyone of yours with nothing to shoot at helps you up.
    /// </summary>
    public partial class EarlGunner
    {
        public bool inSquad, medic;
        public SquadOrder order;
        public Vector3 orderPos;
        public HumanBody orderTarget;
        /// <summary>What he's doing, for the squad list.</summary>
        public string Status { get; private set; } = "";

        public static IEnumerable<EarlGunner> Squad { get { foreach (var g in All) if (g != null && g.inSquad && g.Fighting) yield return g; } }

        public void Recruit(bool join)
        {
            inSquad = join;
            order = join ? SquadOrder.Follow : SquadOrder.None;
            orderTarget = null;
        }

        /// <summary>An order that also holds while he can see someone (moving under fire). True if it moved him.</summary>
        bool OrderWhileFighting(float legs)
        {
            if (!inSquad || body.ragdoll.IsDown) return false;
            var me = PlayerAvatar.Body;
            if (order == SquadOrder.Move)
            {
                Status = "MOVING (UNDER FIRE)";
                if (MoveTo(orderPos, 2.6f * legs)) order = SquadOrder.Hold;
                return true;
            }
            if (order == SquadOrder.Follow && me != null && FlatDist(me.Center, body.Center) > 12f)
            {
                Status = "CATCHING UP";
                MoveTo(me.Pelvis.transform.position - Vector3.up * 0.95f, 3f * legs);
                return true;
            }
            Status = "FIGHTING";
            return false;
        }

        /// <summary>Orders, medic work, helping people up. True if that's what he is doing.</summary>
        bool DoOrders(float legs)
        {
            var rag = body.ragdoll;
            bool quiet = SuspectConf < 0.4f || Time.time - suspectTime > 10f;
            if (order == SquadOrder.Heal)
            {
                if (Heal(orderTarget, legs)) return true;
                order = inSquad ? SquadOrder.Follow : SquadOrder.None;
            }
            if (order == SquadOrder.HelpUp)
            {
                if (HelpUp(orderTarget, legs)) return true;
                order = inSquad ? SquadOrder.Follow : SquadOrder.None;
            }
            // on their own, when it's quiet: the medic to the wounded, someone to you if you're down
            if (quiet)
            {
                if (medic)
                {
                    var patient = healing != null && !healing.Dead ? healing : FindPatient(30f);
                    if (patient != null && Heal(patient, legs)) return true;
                }
                if (team == Teams.Friendly && Helper() == this && HelpUp(PlayerAvatar.Body, legs)) return true;
            }
            healing = null;
            if (!inSquad) { Status = ""; return false; }
            var me = PlayerAvatar.Body;
            bool fresh = Time.time - suspectTime < 6f && SuspectConf > 0.4f;
            switch (order)
            {
                case SquadOrder.Move:
                    Status = "MOVING";
                    if (MoveTo(orderPos, 3f * legs)) order = SquadOrder.Hold;
                    AimAt(body.Center + rag.moveDir * 10f, false);
                    return true;
                case SquadOrder.Hold:
                    Status = "HOLDING";
                    if (fresh) return false;
                    if (FlatDist(body.Pelvis.transform.position, orderPos) > 1.5f) MoveTo(orderPos, 2f * legs);
                    else { rag.moveSpeed = 0f; LookAbout(); }
                    return true;
                default:
                    if (me == null || me.Dead) { Status = "WAITING"; rag.moveSpeed = 0f; return true; }
                    Status = "FOLLOWING";
                    float d = FlatDist(me.Center, body.Center);
                    if (fresh && d < 12f) return false;
                    if (d > 4.5f)
                    {
                        MoveTo(me.Pelvis.transform.position - Vector3.up * 0.95f, (d > 10f ? 3.6f : 2.2f) * legs);
                        AimAt(body.Center + rag.moveDir * 10f, false);
                    }
                    else { rag.moveSpeed = 0f; LookAbout(); }
                    return true;
            }
        }

        void LookAbout()
        {
            var rag = body.ragdoll;
            float a = Mathf.PerlinNoise(Time.time * 0.2f, seed) * 360f;
            Vector3 f = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            // (on the trench map, mostly toward the other side)
            if (TrenchNav.Ready) f = Vector3.Slerp(f, team == Teams.Friendly ? Vector3.forward : Vector3.back, 0.5f).normalized;
            rag.faceDir = Vector3.ProjectOnPlane(f, Vector3.up).normalized;
            rag.aimDir = (rag.faceDir * 0.9f + Vector3.down * 0.35f).normalized;
        }

        // ------------------------------------------------------------ the medic

        Wound treating;
        bool treatingSurgery;
        float treatT;
        HumanBody healing;

        /// <summary>The worst-bleeding one on his side close by that a medic can do something for.</summary>
        HumanBody FindPatient(float range)
        {
            HumanBody best = null; float bs = 0f;
            foreach (var h in HumanBody.All)
            {
                if (h == null || h.Dead || Teams.Of(h) != team) continue;
                if (FlatDist(h.Center, body.Center) > range) continue;
                // another medic is already on him
                bool taken = false;
                foreach (var g in All) if (g != this && g != null && g.medic && g.healing == h) taken = true;
                if (taken) continue;
                float bleed = 0f;
                foreach (var bp in h.partList) if (bp != null) foreach (var w in bp.wounds) if (MedKit.CanTreat(w, false) || MedKit.CanTreat(w, true)) bleed += w.extRate + 0.3f;
                if (bleed > bs) { bs = bleed; best = h; }
            }
            return bs > 0.25f ? best : null;
        }

        /// <summary>Go to him, get down beside him and work through his wounds. False when there's nothing (more) to do.</summary>
        /// <summary>How near he is to someone (any of hips, chest or head: lying in a trench you can't always get round to his chest).</summary>
        float Reach(HumanBody p)
        {
            Vector3 me = body.Pelvis.transform.position;
            float d = FlatDist(me, p.Pelvis.transform.position);
            if (p.Chest != null) d = Mathf.Min(d, FlatDist(me, p.Chest.transform.position));
            if (p.Head != null) d = Mathf.Min(d, FlatDist(me, p.Head.transform.position));
            return d;
        }

        bool Heal(HumanBody p, float legs)
        {
            var rag = body.ragdoll;
            if (p == null || p.Dead || p.Pelvis == null) { healing = null; return false; }
            healing = p;
            Vector3 at = p.Pelvis.transform.position;
            if (p == body)
            {
                // patching himself up where he is
                rag.moveSpeed = 0f; rag.crouch = 1f; rag.pose = PoseKind.Crouch; rag.aimDir = null;
            }
            else
            {
                float d = Reach(p);
                if (d > 1.3f || Mathf.Abs(at.y - body.Pelvis.transform.position.y) > 1.2f)
                {
                    Status = "GOING TO HEAL " + p.DisplayName;
                    MoveTo(at - Vector3.up * 0.5f, 3.2f * legs);
                    AimAt(body.Center + body.ragdoll.moveDir * 10f, false);
                    treatT = 0f;
                    return true;
                }
                rag.moveSpeed = 0f;
                rag.crouch = 1f; rag.pose = PoseKind.Crouch;
                rag.faceDir = Vector3.ProjectOnPlane(at - body.Chest.transform.position, Vector3.up).normalized;
                rag.lookAt = p.Center;
                rag.aimDir = null;
            }
            if (treating == null || treating.part == null || treating.part.body != p || !MedKit.CanTreat(treating, treatingSurgery))
            {
                treating = MedKit.NextFor(p, out treatingSurgery);
                treatT = 0f;
                if (treating == null)
                {
                    healing = null; Status = "";
                    if (p == PlayerAvatar.Body) SandboxGame.Instance?.Toast($"{body.DisplayName}: YOU'RE PATCHED UP");
                    return false;
                }
            }
            Status = (treatingSurgery ? "SUTURING " : "BANDAGING ") + p.DisplayName;
            treatT += Time.deltaTime;
            float dur = treatingSurgery ? 8f + Mathf.Clamp(treating.extRate, 0f, 20f) * 0.15f : 3f;
            if (treatT >= dur)
            {
                MedKit.Treat(p, treating, treatingSurgery);
                if (p == PlayerAvatar.Body) SandboxGame.Instance?.Toast($"MEDIC {(treatingSurgery ? "SUTURED" : "BANDAGED")} YOUR {treating.part?.Name}");
                Sfx.Play(treatingSurgery ? "knife" : "casing_dirt", p.Center, 0.4f, 1f, 1f, 0.1f);
                treating = null;
            }
            return true;
        }

        // ------------------------------------------------------------ a hand up

        static EarlGunner helper;
        static float helperUntil;

        /// <summary>The one of yours who goes to help you up (the nearest free one, kept for a while).</summary>
        static EarlGunner Helper()
        {
            var me = PlayerAvatar.Body;
            if (me == null || me.Dead || !me.ragdoll.IsDown || !me.physio.CanStand) { helper = null; return null; }
            if (helper != null && helper.Fighting && !helper.sees && Time.time < helperUntil) return helper;
            helper = null; float bd = 18f;
            foreach (var g in All)
            {
                if (g == null || g.team != Teams.Friendly || !g.Fighting || g.sees || g.body.ragdoll.IsDown || g.healing != null) continue;
                float d = FlatDist(g.body.Center, me.Center);
                if (d < bd) { bd = d; helper = g; }
            }
            helperUntil = Time.time + 15f;
            return helper;
        }

        float helpT;

        /// <summary>Over to him, down beside him, hands under his arms and up. False once he's up (or can't be).</summary>
        bool HelpUp(HumanBody p, float legs)
        {
            var rag = body.ragdoll;
            if (p == null || p == body || p.Dead || !p.ragdoll.IsDown || !p.physio.CanStand) { helpT = 0f; return false; }
            Vector3 at = p.Chest.transform.position;
            float d = Reach(p);
            if (d > 1.2f)
            {
                Status = "GOING TO HELP " + p.DisplayName + " UP";
                MoveTo(at - Vector3.up * 0.2f, 3.4f * legs);
                AimAt(body.Center + rag.moveDir * 10f, false);
                return true;
            }
            Status = "HELPING " + p.DisplayName + " UP";
            rag.moveSpeed = 0f;
            rag.crouch = 1f; rag.pose = PoseKind.Crouch;
            rag.faceDir = Vector3.ProjectOnPlane(at - body.Chest.transform.position, Vector3.up).normalized;
            rag.aimDir = null;
            rag.lookAt = at;
            helpT += Time.deltaTime;
            p.ragdoll.helpUp = 1.6f;
            // lifting under the arms once he's trying
            if (helpT > 0.6f && p.Chest.rb != null) p.Chest.rb.AddForce(Vector3.up * p.totalMass * 3.5f, ForceMode.Force);
            if (helpT > 14f) { helpT = 0f; return false; }
            return true;
        }
    }
}
