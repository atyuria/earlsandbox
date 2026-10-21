using UnityEngine;

namespace Carl
{
    /// <summary>M67-style fragmentation grenade: blast overpressure, knockback and ~250 simulated fragments.</summary>
    public class Grenade : MonoBehaviour
    {
        public float fuse = 3.5f;
        public bool impact;          // goes off when it hits something
        public int shrapnel = 260;
        float armed;
        bool exploded;

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

        void Update()
        {
            fuse -= Time.deltaTime;
            armed += Time.deltaTime;
            if (fuse <= 0f && !exploded) Explode();
        }

        void Explode()
        {
            exploded = true;
            Vector3 c = transform.position + Vector3.up * 0.05f;
            Fx.Explosion(c);
            WindowGlass.Blast(c, 7f);
            Sfx.Play("explosion", c, 1f, 1f, 8f, 0.05f);
            ArmourSystem.Blast(c, 3f, 90f);
            HumanBrain.BroadcastNoise(c, 120f, 1f);
            SandboxGame.Instance?.Shake(Mathf.Clamp01(1.2f - Vector3.Distance(c, SandboxGame.Instance.cam.transform.position) / 25f));

            // scorch
            if (Physics.Raycast(c + Vector3.up * 0.2f, Vector3.down, out var g, 2f, ~0, QueryTriggerInteraction.Ignore))
                BloodSystem.Instance?.Mark(g.point, g.normal, 2.4f, 0.65f);

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
                    float dd = Vector3.Distance(bp.col.ClosestPoint(c), c) + 0.3f * Vector3.Distance(bp.transform.position, c);
                    if (!closestInLimb.TryGetValue(limb, out var cur) || dd < cur.d) closestInLimb[limb] = (bp, dd);
                }
                foreach (var bp in h.partList)
                {
                    float d = Vector3.Distance(bp.transform.position, c);
                    if (d > 7f) continue;
                    // how far the blast is from this part's surface (a grenade against the belly is ~0 m away)
                    float ds = bp.col != null ? Vector3.Distance(bp.col.ClosestPoint(c), c) : d;
                    if (Physics.Linecast(c, bp.transform.position, out var hit, ~0, QueryTriggerInteraction.Ignore))
                    {
                        // body parts don't shield each other from the overpressure; walls and floors do
                        var other = hit.collider.GetComponentInParent<BodyPart>();
                        if (other == null && !(hit.collider is CharacterController)) continue;
                    }
                    float intensity = Mathf.Pow(Mathf.Clamp01(1f - ds / 2f), 2f); // overpressure only matters up close; fragments do the killing
                    int lk = LimbKey(bp.id);
                    bool mayLoseLimb = lk < 0 || (closestInLimb.TryGetValue(lk, out var cl) && cl.bp == bp);
                    h.damage.ApplyBlast(bp, c, intensity, ds, mayLoseLimb);
                    if (d < 1.6f) knocked = true;
                }
                if (knocked) h.ragdoll.Fall();
            }

            // knockback on everything with a rigidbody
            var cols = Physics.OverlapSphere(c, 5f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in cols)
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb == GetComponent<Rigidbody>()) continue;
                bool body = rb.GetComponent<BodyPart>() != null;
                rb.AddExplosionForce(body ? 2.2f : 5f, c, 5f, 0.25f, ForceMode.VelocityChange);
            }

            // fragments
            if (Ballistics.Instance != null)
            {
                for (int i = 0; i < shrapnel; i++)
                {
                    Vector3 dir = Random.onUnitSphere;
                    if (dir.y < -0.25f) dir.y = -dir.y * 0.3f;
                    float speed = Random.Range(900f, 1500f);
                    float mass = Random.Range(0.0003f, 0.0018f);
                    Ballistics.Instance.Fire(c + dir * 0.06f, dir, speed, mass, 0.004f, HitKind.Shrapnel, i % 9 == 0);
                }
            }
            Destroy(gameObject);
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
