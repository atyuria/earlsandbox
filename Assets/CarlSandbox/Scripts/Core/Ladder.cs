using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A trench ladder: poles and rungs leant against the wall, the way out over the top. Anyone standing at its foot
    /// and walking into it climbs (you: walk into it with W; on top, walk back over the lip toward it to climb down).
    /// </summary>
    public class Ladder
    {
        public static readonly List<Ladder> All = new List<Ladder>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        /// <summary>Where you stand to climb (trench floor), the lip it goes over, where you stand once out.</summary>
        public Vector3 foot, lip, top;
        /// <summary>Flat, from the trench out toward the field this ladder climbs onto.</summary>
        public Vector3 outward;

        /// <summary>The nearest ladder whose foot (or top) is within reach of these feet.</summary>
        public static Ladder Near(Vector3 feet, float reach, out bool atTop)
        {
            Ladder best = null; float bd = reach; atTop = false;
            foreach (var l in All)
            {
                float d = Flat(l.foot - feet);
                if (d < bd && Mathf.Abs(l.foot.y - feet.y) < 0.7f) { bd = d; best = l; atTop = false; }
                d = Flat(l.top - feet);
                if (d < bd && Mathf.Abs(l.top.y - feet.y) < 0.7f) { bd = d; best = l; atTop = true; }
            }
            return best;
        }

        /// <summary>The ladder nearest a point (for the AI planning a way out), any distance.</summary>
        public static Ladder Nearest(Vector3 p, System.Func<Ladder, bool> ok = null)
        {
            Ladder best = null; float bd = float.MaxValue;
            foreach (var l in All)
            {
                if (ok != null && !ok(l)) continue;
                float d = (l.foot - p).sqrMagnitude;
                if (d < bd) { bd = d; best = l; }
            }
            return best;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }

    /// <summary>
    /// Carries a body up (or down) a ladder: the whole ragdoll is moved along the climb together, legs and arms keep
    /// working the pose. Falls off if knocked down or killed on the way.
    /// </summary>
    public class LadderClimber : MonoBehaviour
    {
        HumanBody body;
        Ladder ladder;
        bool up;
        readonly List<Vector3> path = new List<Vector3>();
        int seg;
        float t, stuck;

        public static bool Busy(HumanBody h) => h != null && h.GetComponent<LadderClimber>() != null;
        public bool Up => up;

        public static LadderClimber Begin(HumanBody h, Ladder l, bool up)
        {
            if (h == null || l == null || Busy(h) || h.ragdoll.IsDown || h.physio.dead) return null;
            var c = h.gameObject.AddComponent<LadderClimber>();
            c.body = h; c.ladder = l; c.up = up;
            // pelvis positions: standing at the foot, high on the ladder, over the lip, standing on top
            float hip = 0.95f;
            float lipH = l.lip.y - l.foot.y;
            var p = up
                ? new List<Vector3>
                {
                    l.foot + Vector3.up * hip,
                    l.foot + l.outward * 0.15f + Vector3.up * (lipH + 0.45f),
                    l.lip + l.outward * 0.3f + Vector3.up * (hip + 0.05f),
                    l.top + Vector3.up * hip,
                }
                // down: back over the trench first, high enough that the legs clear the lip, then straight down the rungs
                : new List<Vector3>
                {
                    l.lip + l.outward * 0.2f + Vector3.up * (hip + 0.2f),
                    l.foot + l.outward * 0.15f + Vector3.up * (lipH + hip + 0.15f),
                    l.foot + l.outward * 0.15f + Vector3.up * (lipH + 0.3f),
                    l.foot + Vector3.up * hip,
                };
            // start from where the pelvis is now
            c.path.Add(h.Pelvis.transform.position);
            c.path.AddRange(p);
            Sfx.Play("casing_dirt", l.foot, 0.35f, 1f, 1f, 0.1f);
            return c;
        }

        void FixedUpdate()
        {
            if (body == null || body.Pelvis == null || body.physio.dead || body.ragdoll.IsDown || seg >= path.Count - 1) { Destroy(this); return; }
            var rag = body.ragdoll;
            Vector3 a = path[seg], b = path[seg + 1];
            float len = Mathf.Max(0.01f, Vector3.Distance(a, b));
            // the rungs are slow, getting over the lip slower still, the last step quick
            float speed = up ? (seg == 1 ? 0.85f : seg == 2 ? 0.7f : 1.4f) : (seg == 1 ? 0.8f : seg == 2 ? 0.9f : 0.8f);
            speed *= Mathf.Lerp(0.5f, 1f, rag.Steadiness);
            t += speed * Time.fixedDeltaTime / len;
            if (t >= 1f) { t = 0f; seg++; if (seg >= path.Count - 1) { Land(); return; } a = path[seg]; b = path[seg + 1]; }
            Vector3 goal = Vector3.Lerp(a, b, t);
            var pel = body.Pelvis.rb;
            Vector3 err = goal - pel.position;
            // too far off the line (shoved, blown off): let go
            stuck = err.magnitude > 0.9f ? stuck + Time.fixedDeltaTime : 0f;
            if (stuck > 0.5f) { Destroy(this); return; }
            Vector3 v = (b - a).normalized * speed + err * 8f;
            foreach (var bp in body.partList)
                if (bp != null && bp.rb != null && !bp.severed)
                    bp.rb.linearVelocity = Vector3.Lerp(bp.rb.linearVelocity, v, 0.6f);
            Vector3 flat = Vector3.ProjectOnPlane(v, Vector3.up);
            rag.moveDir = flat.sqrMagnitude > 1e-4f ? flat.normalized : ladder.outward;
            rag.moveSpeed = flat.magnitude;
            rag.faceDir = ladder.outward;
            rag.wantStand = true;
            rag.ladderT = Time.time;
            // kept upright on the rungs
            foreach (var rb in new[] { body.Pelvis.rb, body.Chest.rb })
            {
                var tr = rb.transform;
                Vector3 fwd = Vector3.ProjectOnPlane(tr.forward, Vector3.up);
                float yawErr = fwd.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(fwd, ladder.outward, Vector3.up) * Mathf.Deg2Rad : 0f;
                rb.angularVelocity = Vector3.Cross(tr.up, Vector3.up) * 8f + Vector3.up * yawErr * 3f;
            }
        }

        /// <summary>Off the ladder: stop dead on the feet instead of carrying the climb's speed into a fall.</summary>
        void Land()
        {
            foreach (var bp in body.partList)
                if (bp != null && bp.rb != null && !bp.severed) bp.rb.linearVelocity *= 0.15f;
            body.ragdoll.moveSpeed = 0f;
            Destroy(this);
        }
    }
}
