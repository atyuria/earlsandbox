using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Hand work on the gun: reloading and clearing jams, done the way they are done - not a dip of the gun but the
    /// moves themselves. The gun is canted to bring the magazine to the support hand; that hand goes to the
    /// magazine, the old one comes out (and falls, a real object on the floor), the hand goes to the pouch on your
    /// chest and comes back with a fresh one, seats it with a jolt, and goes over to rack the charging handle
    /// before it returns to the handguard. A jam is cleared with the moves it needs: a rack for a stovepipe, a slap
    /// on the magazine and a rack for a failure to feed, stripping the magazine, racking and seating it again for a
    /// double feed, and hard racks to work a stuck case out.
    /// The moves steer the support hand's grip spring (it goes where the move says instead of to its place on the
    /// handguard) and add to the gun's held pose; the physics does the rest, so it all still reacts to a knock.
    /// </summary>
    public partial class WeaponController
    {
        public enum Move { MagOut, Pouch, MagIn, Rack, Slap }
        struct Step { public Move move; public float t0, len; public bool keep; }
        readonly List<Step> moves = new List<Step>();
        float movesEnd = -1f;                              // the cant is held until then
        float movesFrom;
        bool magOut;                                       // the magazine is out of the gun
        GameObject heldMag;                                // one travelling in the support hand
        readonly List<(Vector3 top, Vector3 bottom, Vector3 charge)> animPts = new List<(Vector3, Vector3, Vector3)>();

        /// <summary>Where on gun i the moves go (its model's points, or worked out from the grip).</summary>
        void AddAnimPoints(Dictionary<string, Vector3> points, Vector3 grip)
        {
            Vector3 top = points.TryGetValue("MagTop", out var a) ? a : grip + new Vector3(0f, 0.043f, 0.114f);
            Vector3 bot = points.TryGetValue("MagBottom", out var b) ? b : top + new Vector3(0f, -0.17f, 0.06f);
            Vector3 ch = points.TryGetValue("Charge", out var c) ? c : grip + new Vector3(0.03f, 0.08f, 0.12f);
            animPts.Add((top, bot, ch));
        }

        public bool Moving => Time.unscaledTime < movesEnd;

        void Queue(Move m, float t0, float len, bool keep = false) => moves.Add(new Step { move = m, t0 = t0, len = len, keep = keep });

        /// <summary>A reload of the gun in hand, over its reload time.</summary>
        void QueueReload(WeaponDef d, float T)
        {
            moves.Clear(); DropHeld(false);
            float t = Time.unscaledTime;
            movesFrom = t; movesEnd = t + T;
            if (IsFlamer(d) || d.name == "TASER") return;
            if (d.name == "MOSSBERG 590")
            {
                // shell by shell into the tube under the action, then the pump
                for (float s = 0.1f; s < T - 0.5f; s += 0.45f) { Queue(Move.Pouch, t + s, 0.22f); Queue(Move.MagIn, t + s + 0.22f, 0.23f); }
                Queue(Move.Rack, t + T - 0.45f, 0.4f);
                return;
            }
            Queue(Move.MagOut, t + T * 0.05f, T * 0.27f);
            Queue(Move.Pouch, t + T * 0.32f, T * 0.23f);
            Queue(Move.MagIn, t + T * 0.55f, T * 0.2f);
            Queue(Move.Rack, t + T * 0.75f, T * 0.2f);
        }

        /// <summary>One step of clearing a jam (BarrelHeat, on J), over len seconds.</summary>
        public void ClearMove(BarrelHeat.JamKind kind, int step, int total, float len)
        {
            float t = Time.unscaledTime;
            moves.Clear();
            movesFrom = t; movesEnd = t + len + 0.15f;
            switch (kind)
            {
                case BarrelHeat.JamKind.FailureToFeed: Queue(Move.Slap, t, len * 0.4f); Queue(Move.Rack, t + len * 0.4f, len * 0.6f); break;
                case BarrelHeat.JamKind.DoubleFeed:
                    if (step == 0) Queue(Move.MagOut, t, len, true);
                    else if (step == total - 1) Queue(Move.MagIn, t, len);
                    else Queue(Move.Rack, t, len);
                    break;
                default: Queue(Move.Rack, t, len); break;      // a stovepipe, or working a stuck case out
            }
        }

        bool StepNow(out Step st, out float u)
        {
            float t = Time.unscaledTime;
            foreach (var s in moves)
                if (t >= s.t0 && t < s.t0 + s.len) { st = s; u = (t - s.t0) / s.len; return true; }
            st = default; u = 0f;
            return false;
        }

        static float S(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

        /// <summary>The gun's held pose during the moves (in the eye's frame): canted to bring the magazine round.</summary>
        void MovePose(ref Vector3 pos, ref Quaternion rot)
        {
            float t = Time.unscaledTime;
            if (t >= movesEnd + 0.25f) return;
            // in over the first moment, out over the last
            float e = S((t - movesFrom) / 0.18f) * S((movesEnd - t) / 0.22f);
            // brought up in front of the chest where you can see the magazine well, muzzle up a little, rolled over
            // to the support hand
            pos += new Vector3(-0.09f, 0.22f, -0.03f) * e;
            rot *= Quaternion.Euler(-22f * e, -14f * e, -34f * e);
            if (StepNow(out var st, out float u))
            {
                if (st.move == Move.MagIn) { float b = Mathf.Exp(-Mathf.Pow((u - 0.72f) / 0.06f, 2f)); rot *= Quaternion.Euler(-5f * b, 0f, 0f); pos.y += 0.008f * b; }
                if (st.move == Move.Rack) { float b = Mathf.Exp(-Mathf.Pow((u - 0.6f) / 0.07f, 2f)); pos.z += 0.012f * b; rot *= Quaternion.Euler(-3f * b, 0f, 2f * b); }
                if (st.move == Move.Slap) { float b = Mathf.Exp(-Mathf.Pow((u - 0.45f) / 0.05f, 2f)); pos.y += 0.012f * b; rot *= Quaternion.Euler(-4f * b, 0f, 0f); }
            }
        }

        Vector3 gp; Quaternion gr = Quaternion.identity;
        Vector3 GunPt(Vector3 local) => gp + gr * local;
        Vector3 handAt;                                      // where the support hand is drawn this frame (moves)

        /// <summary>Where the support hand is sent this physics step (false: its place on the gun as usual).</summary>
        bool SupportTarget(Hold h, out Vector3 target) => SupportTargetAt(h, simPos, simRot, out target);

        /// <summary>
        /// This frame, how far the drawn support hand is moved off its place on the gun (the hand shape is locked to the
        /// gun; during the moves it goes where the move takes it, and the arm is drawn to it there).
        /// </summary>
        bool SupportVisual(out Vector3 delta)
        {
            delta = Vector3.zero;
            var h = jointL;
            if (h == null || !SupportTargetAt(h, vmRoot.position, vmRoot.rotation, out var t)) return false;
            delta = t - (vmRoot.position + vmRoot.rotation * h.gunLocal);
            handAt = t;
            return true;
        }

        /// <summary>
        /// From a to b without going through the gun: the way between is pushed out from the bore line (under the
        /// gun if it would go through the middle) to keep the hand clear of it; the ends are exactly where they are.
        /// </summary>
        Vector3 Around(Vector3 a, Vector3 b, float t)
        {
            Vector3 p = Vector3.LerpUnclamped(a, b, Mathf.Clamp01(t));
            Quaternion inv = Quaternion.Inverse(gr);
            Vector3 l = inv * (p - gp);
            Vector2 r = new Vector2(l.x, l.y + 0.01f);
            const float clear = 0.065f;
            float m = r.magnitude;
            if (m < clear)
            {
                Vector2 dir = m > 0.012f ? r / m : new Vector2(0f, -1f);
                float push = (clear - m) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
                l += new Vector3(dir.x, dir.y, 0f) * push;
            }
            return gp + gr * l;
        }

        bool SupportTargetAt(Hold h, Vector3 gunPos, Quaternion gunRot, out Vector3 target)
        {
            target = default;
            gp = gunPos; gr = gunRot;
            if (h == null || h != jointL || current >= animPts.Count || !StepNow(out var st, out float u)) return false;
            var me = PlayerAvatar.Body;
            var p = animPts[current];
            Vector3 rest = GunPt(h.gunLocal);
            Vector3 grab = GunPt(Around(p.top, p.bottom, 0.45f));
            Vector3 below = GunPt(Around(p.top, p.bottom, 0.45f) + new Vector3(0f, -0.14f, 0.03f));
            Vector3 down = grab + Vector3.down * 0.18f;
            Vector3 pouch = rest;
            if (me != null && me.Chest != null)
            {
                var ct = me.Chest.transform;
                Vector3 f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized, r = Vector3.Cross(Vector3.up, f);
                pouch = ct.position + f * 0.17f - Vector3.up * 0.1f - r * 0.05f;
            }
            Vector3 ch = GunPt(p.charge + new Vector3(0.02f, 0f, 0f));
            Vector3 chBack = GunPt(p.charge + new Vector3(0.02f, 0f, -0.08f));
            switch (st.move)
            {
                case Move.MagOut:
                    target = u < 0.45f ? Around(rest, grab, S(u / 0.45f)) : Around(grab, st.keep ? below : down, S((u - 0.45f) / 0.55f));
                    break;
                case Move.Pouch:
                    target = u < 0.5f ? Around(down, pouch, S(u / 0.5f)) : Around(pouch, below, S((u - 0.5f) / 0.5f));
                    break;
                case Move.MagIn:
                    target = u < 0.72f ? Around(below, grab, S(u / 0.72f)) : Around(grab, rest, S((u - 0.72f) / 0.28f));
                    break;
                case Move.Rack:
                    target = u < 0.4f ? Around(rest, ch, S(u / 0.4f)) : u < 0.58f ? Around(ch, chBack, S((u - 0.4f) / 0.18f)) : u < 0.66f ? Around(chBack, ch, (u - 0.58f) / 0.08f) : Around(ch, rest, S((u - 0.66f) / 0.34f));
                    break;
                case Move.Slap:
                    Vector3 bot = GunPt(p.bottom), under = GunPt(p.bottom + new Vector3(0f, -0.07f, 0f));
                    target = u < 0.38f ? Around(rest, under, S(u / 0.38f)) : u < 0.46f ? Around(under, bot, (u - 0.38f) / 0.08f) : Around(bot, rest, S((u - 0.46f) / 0.54f));
                    break;
            }
            return true;
        }

        // ------------------------------------------------------------ the magazine itself

        Transform MagOf(int i) => i < models.Count && models[i] != null ? FindMag(models[i].transform) : null;
        static Transform FindMag(Transform t)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == "Mag") return c;
            return null;
        }

        /// <summary>Each frame: the magazine out of the gun, in the hand, falling, going back in.</summary>
        void UpdateMagVisual()
        {
            var mag = MagOf(current);
            if (mag == null) return;
            var hand = jointL != null && jointL.hand != null ? jointL.hand.transform : null;
            bool stepping = StepNow(out var st, out float u);
            if (stepping && st.move == Move.MagOut && u >= 0.45f)
            {
                // pulled out with the hand: a free copy of it in the hand, the one in the gun hidden
                if (!magOut) { magOut = true; mag.gameObject.SetActive(false); heldMag = CloneMag(mag); }
                if (heldMag != null && hand != null) FollowHand(mag);
                if (!st.keep && u > 0.8f) DropHeld(true);
            }
            else if (stepping && st.move == Move.Pouch && u >= 0.5f)
            {
                // a fresh one out of the pouch
                if (heldMag == null) heldMag = CloneMag(mag);
                if (hand != null) FollowHand(mag);
            }
            else if (stepping && st.move == Move.MagIn && heldMag != null)
            {
                if (u < 0.72f) { FollowHand(mag); float k = S((u - 0.45f) / 0.27f); heldMag.transform.position = Vector3.Lerp(heldMag.transform.position, mag.parent.TransformPoint(mag.localPosition), k); heldMag.transform.rotation = mag.parent.rotation * mag.localRotation; }
                else { Destroy(heldMag); heldMag = null; magOut = false; mag.gameObject.SetActive(true); }
            }
            else if (!Moving && magOut && heldMag == null && !stepping) { magOut = false; mag.gameObject.SetActive(true); }
            else if (heldMag != null && hand != null) FollowHand(mag);
        }

        void FollowHand(Transform mag)
        {
            if (heldMag == null || jointL == null || jointL.hand == null) return;
            var p = animPts[current];
            Vector3 grabLocal = Vector3.Lerp(p.top, p.bottom, 0.45f);
            var rot = mag.parent.rotation;
            heldMag.transform.rotation = rot;
            // in the hand as it is drawn (where the move has it), or the physical hand between moves
            Vector3 at = Moving && handAt != Vector3.zero ? handAt : jointL.hand.transform.TransformPoint(jointL.handLocal);
            heldMag.transform.position = at - rot * grabLocal;
        }

        GameObject CloneMag(Transform mag)
        {
            var g = new GameObject("LooseMag");
            foreach (var mf in mag.GetComponentsInChildren<MeshFilter>(true))
            {
                var c = new GameObject("m"); c.transform.SetParent(g.transform, false);
                c.transform.localPosition = mag.InverseTransformPoint(mf.transform.position);
                c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                c.AddComponent<MeshRenderer>().sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
                c.layer = mag.gameObject.layer;
            }
            g.layer = mag.gameObject.layer;
            g.transform.SetPositionAndRotation(mag.position, mag.rotation);
            return g;
        }

        /// <summary>Let go of the magazine in the hand: dropped (it falls and lies on the floor), or just gone.</summary>
        void DropHeld(bool fall)
        {
            if (heldMag == null) return;
            if (!fall) { Destroy(heldMag); heldMag = null; return; }
            var g = heldMag; heldMag = null;
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            var b = new Bounds(); bool any = false;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            var col = g.AddComponent<BoxCollider>();
            col.center = g.transform.InverseTransformPoint(b.center); col.size = Quaternion.Inverse(g.transform.rotation) * b.size;
            col.size = new Vector3(Mathf.Abs(col.size.x), Mathf.Abs(col.size.y), Mathf.Abs(col.size.z));
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = 0.45f;
            rb.linearVelocity = simVel + Vector3.down * 0.6f;
            rb.angularVelocity = Random.insideUnitSphere * 3f;
            Destroy(g, 30f);
        }
    }
}
