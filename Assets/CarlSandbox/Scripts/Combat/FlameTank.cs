using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The flamethrower's fuel tanks, on your back while you carry it: two steel bottles and the pressure bottle
    /// between them. A round into them can set them off (one in three): a blast and a ball of burning fuel that
    /// sets anyone close alight - you first. The tank is gone then, and the flamethrower dry, until you strap on a
    /// new one (R).
    /// </summary>
    public class FlameTank : MonoBehaviour
    {
        static FlameTank I;
        GameObject rig;
        bool gone;
        float goneAt;
        HumanBody fittedTo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public static void Attach(GameObject g) => I = g.AddComponent<FlameTank>();

        /// <summary>Blown up, and not yet replaced: no fuel to throw.</summary>
        public static bool Gone => I != null && I.gone;

        /// <summary>A new tank strapped on (the flamethrower reloaded).</summary>
        public static void Refit() { if (I != null) I.gone = false; }

        static int FlamerIndex(WeaponController w) => w.defs.FindIndex(WeaponController.IsFlamer);

        void LateUpdate()
        {
            var me = PlayerAvatar.Body;
            var w = SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;
            int i = w != null && w.owned != null ? FlamerIndex(w) : -1;
            // (with infinite ammo the gun never needs a reload: a fresh tank turns up a few seconds later)
            if (gone && GameSettings.InfiniteAmmo && Time.time - goneAt > 4f) gone = false;
            bool wear = me != null && !me.Dead && me.Chest != null && i >= 0 && w.owned[i] && !gone;
            if (!wear) { if (rig != null) rig.SetActive(false); return; }
            if (rig == null || fittedTo != me) Build(me);
            rig.SetActive(true);
            // strapped to the back of the chest
            var ch = me.Chest.transform;
            Vector3 back = -ch.forward;
            rig.transform.SetPositionAndRotation(ch.position + back * 0.19f - ch.up * 0.02f, Quaternion.LookRotation(ch.forward, ch.up));
        }

        void Build(HumanBody me)
        {
            if (rig != null) Destroy(rig);
            fittedTo = me;
            rig = new GameObject("FlameTanks");
            var steel = Mats.Lit(new Color(0.24f, 0.27f, 0.2f), 0.35f, 0.4f);
            var brass = Mats.Lit(new Color(0.55f, 0.45f, 0.22f), 0.6f, 0.8f);
            for (int s = -1; s <= 1; s += 2)
            {
                Piece(PrimitiveType.Capsule, new Vector3(s * 0.1f, 0f, 0f), new Vector3(0.12f, 0.24f, 0.12f), steel);
                Piece(PrimitiveType.Cylinder, new Vector3(s * 0.1f, 0.26f, 0f), new Vector3(0.03f, 0.02f, 0.03f), brass);
            }
            Piece(PrimitiveType.Capsule, new Vector3(0f, 0.02f, -0.06f), new Vector3(0.07f, 0.16f, 0.07f), steel);   // the pressure bottle
            Piece(PrimitiveType.Cube, new Vector3(0f, 0.1f, 0.06f), new Vector3(0.3f, 0.03f, 0.02f), Mats.Lit(new Color(0.15f, 0.13f, 0.1f), 0.2f));   // the frame
            var col = rig.AddComponent<BoxCollider>();
            col.size = new Vector3(0.34f, 0.5f, 0.16f);
            // (your own body doesn't bump into it)
            foreach (var bp in me.partList) if (bp != null && bp.col != null) Physics.IgnoreCollision(col, bp.col);
        }

        void Piece(PrimitiveType t, Vector3 at, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(rig.transform, false);
            g.transform.localPosition = at; g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>Is this collider the tanks on your back?</summary>
        public static bool IsTank(Collider c) => I != null && I.rig != null && c != null && c.gameObject == I.rig;

        /// <summary>A round into the tanks on your back (Ballistics).</summary>
        public static void HitAt(Vector3 at, Vector3 dir) { if (I != null) I.Shot(at, dir); }

        /// <summary>A round has hit the tanks: one time in three, they go up.</summary>
        public void Shot(Vector3 at, Vector3 dir)
        {
            if (gone) return;
            Fx.Sparks(at, -dir, 12);
            Sfx.Play("impact_metal", at, 0.9f, 1f, 2f, 0.08f);
            if (Random.value < 1f / 3f) Explode();
            else SandboxGame.Instance?.Toast("YOUR FUEL TANK WAS HIT");
        }

        void Explode()
        {
            gone = true; goneAt = Time.time;
            Vector3 c = rig.transform.position;
            rig.SetActive(false);
            var g = Grenade.Throw(c, Vector3.zero);
            g.fuse = 0.0001f; g.shrapnel = 70; g.team = -1;
            Fx.Explosion(c);
            // a ball of burning fuel: everyone close catches, and it keeps burning on the ground round about
            foreach (var h in HumanBody.All.ToArray())
            {
                if (h == null || h.Dead) continue;
                float d = Vector3.Distance(h.Center, c);
                if (d > 3.5f) continue;
                float k = 1f - d / 3.5f;
                foreach (var bp in h.partList) if (bp != null && !bp.severed && Random.value < 0.35f + 0.6f * k) Burning.Ignite(bp, bp.transform.position, 0.3f + 0.6f * k);
            }
            for (int i = 0; i < 10; i++)
            {
                Vector2 r = Random.insideUnitCircle * 2.5f;
                Vector3 p = c + new Vector3(r.x, 1f, r.y);
                if (Physics.Raycast(p, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<HumanBody>() == null)
                    GroundFire.At(hit.point + Vector3.up * 0.05f, hit.normal);
            }
            var w = SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;
            if (w != null) { int i = FlamerIndex(w); if (i >= 0) w.LoseRounds(i, 100000); }
            SandboxGame.Instance?.Toast("YOUR FUEL TANK WENT UP");
        }
    }
}
