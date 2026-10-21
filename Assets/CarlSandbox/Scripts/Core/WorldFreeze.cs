using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Stops the world while the spawn menu is open, except you: every other body, loose object, bullet, grenade
    /// and drop of blood holds where it is (their rigidbodies are pinned, their brains and bleeding pause), and
    /// carries on with exactly the motion it had once the menu closes. You can still walk about and place things.
    /// </summary>
    public static class WorldFreeze
    {
        public static bool On { get; private set; }
        static readonly Dictionary<Rigidbody, (Vector3 v, Vector3 w, bool kin)> held = new Dictionary<Rigidbody, (Vector3, Vector3, bool)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { On = false; held.Clear(); }

        public static void Set(bool on)
        {
            if (on == On) return;
            On = on;
            if (on)
            {
                foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude)) Pin(rb);
                return;
            }
            foreach (var kv in held)
            {
                var rb = kv.Key;
                if (rb == null) continue;
                rb.isKinematic = kv.Value.kin;
                if (!rb.isKinematic) { rb.linearVelocity = kv.Value.v; rb.angularVelocity = kv.Value.w; }
            }
            held.Clear();
        }

        static bool Mine(Rigidbody rb)
        {
            var me = PlayerAvatar.Body;
            if (me == null) return false;
            var bp = rb.GetComponent<BodyPart>();
            return bp != null && bp.body == me;
        }

        static void Pin(Rigidbody rb)
        {
            if (rb == null || held.ContainsKey(rb) || Mine(rb)) return;
            held[rb] = (rb.isKinematic ? Vector3.zero : rb.linearVelocity, rb.isKinematic ? Vector3.zero : rb.angularVelocity, rb.isKinematic);
            rb.isKinematic = true;
        }

        /// <summary>Called every frame: anything spawned while frozen (an Earl from the menu) is pinned too.</summary>
        public static void Tick()
        {
            if (!On) return;
            foreach (var h in HumanBody.All)
            {
                if (h == null || h == PlayerAvatar.Body) continue;
                foreach (var bp in h.partList) if (bp != null && bp.rb != null && !held.ContainsKey(bp.rb)) Pin(bp.rb);
            }
        }

        /// <summary>Something you grab or throw while frozen is yours to move.</summary>
        public static void Release(Rigidbody rb)
        {
            if (rb == null || !held.TryGetValue(rb, out var s)) return;
            rb.isKinematic = s.kin;
            held.Remove(rb);
        }
    }
}
