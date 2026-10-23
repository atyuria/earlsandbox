using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Sure feet for you: you step over the bodies on the floor instead of tangling your legs in them, and a little
    /// lip or step in the ground is stepped up rather than tripped on.
    /// </summary>
    public static class PlayerFooting
    {
        static readonly Dictionary<HumanBody, bool> ignoring = new Dictionary<HumanBody, bool>();
        static HumanBody forBody;
        static float nextScan;
        static readonly RaycastHit[] buf = new RaycastHit[12];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ignoring.Clear(); forBody = null; nextScan = 0f; }

        public static void Update(HumanBody me, Vector3 wish, float dt)
        {
            if (me == null || me.Pelvis == null) return;
            if (forBody != me) { ignoring.Clear(); forBody = me; }
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.25f;
                Vector3 p = me.Pelvis.transform.position;
                foreach (var o in HumanBody.All)
                {
                    if (o == null || o == me || o.Pelvis == null) continue;
                    if ((o.Pelvis.transform.position - p).sqrMagnitude > 25f) continue;
                    // someone lying on the floor: you step over (through) them; a standing one you still bump into
                    bool lying = o.Dead || o.ragdoll.IsDown;
                    if (ignoring.TryGetValue(o, out bool was) && was == lying) continue;
                    ignoring[o] = lying;
                    // (every collider on both: parts carry more than their main one)
                    var theirs = o.GetComponentsInChildren<Collider>();
                    foreach (var mine in me.GetComponentsInChildren<Collider>())
                        foreach (var c in theirs) Physics.IgnoreCollision(mine, c, lying);
                }
                // (forget the ones that are gone)
                var gone = new List<HumanBody>();
                foreach (var k in ignoring.Keys) if (k == null) gone.Add(k);
                foreach (var k in gone) ignoring.Remove(k);
            }

            // step up a lip: something at shin height ahead with nothing above it
            var rag = me.ragdoll;
            if (rag.IsDown || rag.mode != BodyMode.Standing || wish.sqrMagnitude < 0.04f) return;
            Vector3 dir = Vector3.ProjectOnPlane(wish, Vector3.up).normalized;
            Vector3 pel = me.Pelvis.transform.position;
            float ground = pel.y - rag.GroundHeight;
            if (Blocked(me, new Vector3(pel.x, ground + 0.12f, pel.z), dir, 0.55f) && !Blocked(me, new Vector3(pel.x, ground + 0.62f, pel.z), dir, 0.7f))
            {
                foreach (var bp in me.partList)
                    if (bp != null && bp.rb != null && !bp.severed && bp.rb.linearVelocity.y < 1.6f)
                        bp.rb.AddForce((Vector3.up * 16f + dir * 3f) * dt, ForceMode.VelocityChange);
            }
        }

        static bool Blocked(HumanBody me, Vector3 from, Vector3 dir, float dist)
        {
            int n = Physics.RaycastNonAlloc(from, dir, buf, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = buf[i].collider;
                if (c is CharacterController) continue;
                var rb = buf[i].rigidbody;
                if (rb != null && !rb.isKinematic) continue;     // bodies and loose things don't count
                if (Vector3.Angle(buf[i].normal, Vector3.up) < 40f) continue;   // a slope you just walk up
                return true;
            }
            return false;
        }
    }
}
