using UnityEngine;

namespace Carl
{
    /// <summary>M67-style fragmentation grenade: blast overpressure, knockback and ~250 simulated fragments.</summary>
    public class Grenade : MonoBehaviour
    {
        public float fuse = 3.5f;
        public bool impact;          // goes off when it hits something
        public int shrapnel = 260;
        /// <summary>How big a charge, in hand grenades (the blast reaches about sqrt of this further).</summary>
        public float power = 1f;
        /// <summary>A shaped charge: most of its force goes one way, in a narrow jet (a small, precise blast).</summary>
        public Vector3? jet;
        float armed;
        bool exploded;
        /// <summary>Every live grenade (the Earls watch for them at their feet).</summary>
        public static readonly System.Collections.Generic.List<Grenade> All = new System.Collections.Generic.List<Grenade>();
        /// <summary>Who threw it (-1 nobody in particular; 0 your side, 1 theirs).</summary>
        public int team = -1;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        void OnCollisionEnter(Collision c)
        {
            if (impact && !exploded && armed > 0.12f) Explode();
        }

        public static Grenade Throw(Vector3 pos, Vector3 velocity)
        {
            var go = new GameObject("Grenade");
            go.transform.position = pos;
            if (ModelLibrary.Spawn("Gun_Grenade", go.transform, ModelLibrary.GunMaterial) == null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Ellipsoid(new Vector3(0.032f, 0.042f, 0.032f), 12, 8);
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.25f, 0.3f, 0.2f), 0.3f);
            }
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.035f;
            col.sharedMaterial = new PhysicsMaterial("Grenade") { bounciness = 0.35f, dynamicFriction = 0.5f, staticFriction = 0.6f };
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            return go.AddComponent<Grenade>();
        }

        static readonly Unity.Profiling.ProfilerMarker PM_Update = new Unity.Profiling.ProfilerMarker("Carl.Grenade.Update");

        void Update()
        {

            using var pmScope = PM_Update.Auto();
            if (WorldFreeze.On) return;
            fuse -= Time.deltaTime;
            armed += Time.deltaTime;
            if (fuse <= 0f && !exploded) Explode();
        }

        void Explode()
        {
            exploded = true;
            // the grenade's own shell must not shield the body it is lying on from its blast (the line of sight
            // from just above it ran straight back through it, so whatever it rested on was never hurt)
            foreach (var own in GetComponentsInChildren<Collider>()) own.enabled = false;
            Vector3 c = transform.position + Vector3.up * 0.05f;
            // reach: a bigger charge reaches further (as the square root), a shaped one puts less out sideways
            float reach = Mathf.Sqrt(Mathf.Max(0.2f, power)) * (jet.HasValue ? 0.6f : 1f);
            Fx.Explosion(c, power);
            WindowGlass.Blast(c, 7f * reach);
            Sfx.Play("explosion", c, 1f, 1f, 8f * Mathf.Sqrt(Mathf.Max(1f, power)), 0.05f);
            ArmourSystem.Blast(c, 3f * reach, 90f);
            HumanBrain.BroadcastNoise(c, 120f, 1f);
            SandboxGame.Instance?.Shake(Mathf.Clamp01(1.2f * Mathf.Sqrt(Mathf.Max(1f, power)) - Vector3.Distance(c, SandboxGame.Instance.cam.transform.position) / 25f));

            // scorch
            // (on the ground under it only: a stain on a body or anything loose would hang in the air once it moved)
            float gb = float.MaxValue; RaycastHit g = default;
            foreach (var gh in Physics.RaycastAll(c + Vector3.up * 0.2f, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                if (gh.collider.attachedRigidbody == null && gh.collider.GetComponentInParent<BodyPart>() == null && gh.distance < gb) { gb = gh.distance; g = gh; }
            if (gb < float.MaxValue) BloodSystem.Instance?.Mark(g.point, g.normal, 2.4f, 0.65f);

            // overpressure on bodies (needs line of sight)
            foreach (var h in HumanBody.All.ToArray())
            {
                bool knocked = false;
                // a limb is blown off only at the segment the grenade is against (shin -> off at the knee),
                // not every segment of that limb that happens to be within reach
                var closestInLimb = new System.Collections.Generic.Dictionary<int, (BodyPart bp, float d)>();
                foreach (var bp in h.partList)
                {
                    int limb = LimbKey(bp.id);
                    if (limb < 0 || bp.col == null) continue;
                    // surface distance, with the segment's centre breaking ties where segments meet (knee, elbow)
                    float dd = (Vector3.Distance(bp.col.ClosestPoint(c), c) + 0.3f * Vector3.Distance(bp.transform.position, c)) / reach;
                    if (!closestInLimb.TryGetValue(limb, out var cur) || dd < cur.d) closestInLimb[limb] = (bp, dd);
                }
                foreach (var bp in h.partList)
                {
                    // (distances scaled by the charge's reach: twice the reach counts as half the distance)
                    float d = Vector3.Distance(bp.transform.position, c) / reach;
                    if (d > 7f) continue;
                    // how far the blast is from this part's surface (a grenade against the belly is ~0 m away)
                    float ds = (bp.col != null ? Vector3.Distance(bp.col.ClosestPoint(c), c) : d * reach) / reach;
                    // body parts don't shield each other from the overpressure, nor does loose stuff lying about
                    // (shot-off armour, other grenades); walls and floors do
                    if (Shielded(c, bp.transform.position)) continue;
                    // a plate still worn between the blast and this part takes the worst of it (the vest itself has
                    // just been shredded by ArmourSystem.Blast; if that tore it off, nothing is left to help)
                    Vector3 toPart = bp.transform.position - c;
                    var plate = ArmourSystem.Raycast(c, toPart.normalized, toPart.magnitude, out _, out _, h);
                    if (plate != null && (plate.plateUnder || plate.steel) && plate.part == bp) ds = Mathf.Max(ds, 0.22f);
                    float intensity = Mathf.Pow(Mathf.Clamp01(1f - ds / 2f), 2f); // overpressure only matters up close; fragments do the killing
                    if (plate != null && (plate.plateUnder || plate.steel) && plate.part == bp) intensity *= 0.55f;
                    int lk = LimbKey(bp.id);
                    bool mayLoseLimb = lk < 0 || (closestInLimb.TryGetValue(lk, out var cl) && cl.bp == bp);
                    h.damage.ApplyBlast(bp, c, intensity, ds, mayLoseLimb);
                    if (d < 1.6f) knocked = true;
                    // the flash and the hot gas: small scorches and blistered patches on the skin facing it
                    if (ds < 1.5f && bp.col != null)
                    {
                        float heat = 1f - ds / 1.5f;
                        int marks = 1 + (int)(heat * 3f);
                        for (int m = 0; m < marks; m++)
                        {
                            Vector3 at = bp.col.ClosestPoint(c + Random.insideUnitSphere * 0.25f);
                            bp.AddBurn(at, Random.Range(0.015f, 0.035f) + heat * 0.03f, Random.Range(0.4f, 0.9f) * heat, Random.Range(0.4f, 0.8f));
                        }
                    }
                }
                if (knocked) h.ragdoll.Fall();
            }

            // you: the overpressure (the fragments reach you as projectiles)
            if (PlayerBody.I != null && PlayerAvatar.Body == null)
            {
                Vector3 pc = PlayerBody.I.transform.position + Vector3.up * 0.9f;
                float pd = Vector3.Distance(pc, c) / reach;
                if (pd < 4f && !Shielded(c, pc)) PlayerBody.I.Blast(c, Mathf.Pow(Mathf.Clamp01(1f - pd / 4f), 2f));
            }

            // knockback on everything with a rigidbody
            var cols = Physics.OverlapSphere(c, 5f * reach, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in cols)
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb == GetComponent<Rigidbody>()) continue;
                bool body = rb.GetComponent<BodyPart>() != null;
                rb.AddExplosionForce((body ? 2.2f : 5f) * Mathf.Sqrt(Mathf.Max(1f, power)), c, 5f * reach, 0.25f, ForceMode.VelocityChange);
            }

            // fragments
            if (Ballistics.Instance != null)
            {
                Quaternion aim = jet.HasValue && jet.Value.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(jet.Value) : Quaternion.identity;
                for (int i = 0; i < shrapnel; i++)
                {
                    // a shaped charge: three in four go down the jet, a tight cone of heavy fast fragments
                    if (jet.HasValue && i % 4 != 0)
                    {
                        Vector2 cone = Random.insideUnitCircle * Mathf.Tan(7f * Mathf.Deg2Rad);
                        Vector3 jd = aim * new Vector3(cone.x, cone.y, 1f).normalized;
                        Ballistics.Instance.Fire(c + jd * 0.06f, jd, Random.Range(1500f, 2300f), Random.Range(0.002f, 0.007f), 0.005f, HitKind.Shrapnel, i % 7 == 0);
                        continue;
                    }
                    Vector3 dir = Random.onUnitSphere;
                    if (dir.y < -0.25f) dir.y = -dir.y * 0.3f;
                    float speed = Random.Range(900f, 1500f);
                    float mass = Random.Range(0.0003f, 0.0018f);
                    Ballistics.Instance.Fire(c + dir * 0.06f, dir, speed, mass, 0.004f, HitKind.Shrapnel, i % 9 == 0);
                }
            }
            Destroy(gameObject);
        }

        static readonly RaycastHit[] sightBuf = new RaycastHit[16];

        /// <summary>Is there a wall, floor or other fixed thing between the blast and this point?</summary>
        static bool Shielded(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            int n = Physics.RaycastNonAlloc(from, d.normalized, sightBuf, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = sightBuf[i].collider;
                if (col is CharacterController || col.GetComponentInParent<BodyPart>() != null) continue;
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;          // loose objects get blown away, they don't shield
                return true;
            }
            return false;
        }

        /// <summary>Which limb a part belongs to (0-3), or -1 for torso/head.</summary>
        static int LimbKey(PartId id)
        {
            switch (id)
            {
                case PartId.UpperArmL: case PartId.ForearmL: case PartId.HandL: return 0;
                case PartId.UpperArmR: case PartId.ForearmR: case PartId.HandR: return 1;
                case PartId.ThighL: case PartId.ShinL: case PartId.FootL: return 2;
                case PartId.ThighR: case PartId.ShinR: case PartId.FootR: return 3;
            }
            return -1;
        }
    }
}
