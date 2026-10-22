using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A mannequin hand in the same style and skin as the bodies: a palm, four fingers of three segments and a
    /// two-segment thumb, built to close round whatever it holds. Every hand in first person is this one model
    /// (mirrored for the left), posed by Grip(): give it the thing's centre line, its radius, which way the palm
    /// faces it and which way the fingers run, and the fingers curl round it. The hand ends at a wrist where the
    /// body's real forearm is brought to meet it (WristLocal / ForearmLocal), so nothing hangs in the air.
    /// </summary>
    public class PoseHand : MonoBehaviour
    {
        public int side = 1;                       // 1 right, -1 left
        public Vector3 WristLocal, ForearmLocal;   // in the parent's space: where the forearm joins, and which way it runs

        // proportions of an adult hand (metres)
        const float PalmLen = 0.082f, PalmWidth = 0.074f, PalmThick = 0.028f, FingerR = 0.0082f, ThumbR = 0.0098f;
        static readonly float[][] Fingers =
        {
            new[] { 0.039f, 0.024f, 0.02f },    // index
            new[] { 0.043f, 0.027f, 0.021f },   // middle
            new[] { 0.04f, 0.025f, 0.02f },     // ring
            new[] { 0.032f, 0.02f, 0.017f },    // little
        };
        static readonly float[] FingerAcross = { 0.027f, 0.009f, -0.009f, -0.026f };   // along the knuckles, from the thumb side

        Transform palm, wrist;
        readonly Transform[,] seg = new Transform[4, 3];
        readonly Transform[] thumb = new Transform[3];
        static Mesh capsule, ball;

        public static PoseHand Build(Transform parent, int side, Material skin, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<PoseHand>();
            h.side = side;
            if (capsule == null) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Capsule); capsule = tmp.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(tmp); }   // radius 0.5, total height 2
            if (ball == null) ball = ProcMesh.Ellipsoid(Vector3.one * 0.5f, 14, 10);
            h.palm = h.Piece("Palm", ball, skin);
            h.wrist = h.Piece("Wrist", capsule, skin);
            for (int f = 0; f < 4; f++) for (int k = 0; k < 3; k++) h.seg[f, k] = h.Piece("Finger", capsule, skin);
            for (int k = 0; k < 3; k++) h.thumb[k] = h.Piece("Thumb", k == 0 ? ball : capsule, skin);
            return h;
        }

        Transform Piece(string n, Mesh m, Material mat)
        {
            var g = new GameObject(n);
            g.transform.SetParent(transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        /// <summary>A rounded bar from a to b (parent space) of radius rad.</summary>
        static void Bar(Transform t, Vector3 a, Vector3 b, float rad)
        {
            Vector3 d = b - a; float len = d.magnitude;
            t.localPosition = (a + b) * 0.5f;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, len > 1e-5f ? d / len : Vector3.up);
            t.localScale = new Vector3(rad * 2f, (len + rad * 2f) * 0.5f, rad * 2f);
        }

        /// <summary>
        /// Close the hand round a bar. All in the parent's (the weapon's) space: centre = a point on the bar's
        /// centre line, radius = its half-thickness there, palmToBar = the way the palm faces (toward the bar),
        /// fingers = the way the fingers leave the palm (they curl round from there), forearm = the way the forearm
        /// runs out of the wrist. tight 0..1 closes the fingers further (a fist), trigger = the index finger kept
        /// straighter along the frame.
        /// </summary>
        /// <summary>Does a capsule from a to b of radius r (parent space) run into the thing being held?</summary>
        public delegate bool Blocked(Vector3 a, Vector3 b, float r);

        /// <summary>The hand as capsules (parent space), once posed: for the other hand to close round it.</summary>
        public readonly System.Collections.Generic.List<(Vector3 a, Vector3 b, float r)> Caps = new System.Collections.Generic.List<(Vector3, Vector3, float)>();

        public void Grip(Vector3 centre, float radius, Vector3 palmToBar, Vector3 fingers, Vector3 forearm, float tight = 0f, bool trigger = false, Vector3? thumbDir = null, Blocked blocked = null)
        {
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;
            Vector3 n = palmToBar.normalized;
            Vector3 z = Vector3.ProjectOnPlane(fingers, n).normalized;
            Vector3 t = side > 0 ? Vector3.Cross(n, z) : Vector3.Cross(z, n);       // toward the thumb
            Vector3 curlAxis = Vector3.Cross(z, n).normalized;                       // turning z toward n closes the hand
            Caps.Clear();

            Vector3 p = centre - n * (radius + PalmThick * 0.35f);                   // the palm against the bar
            if (blocked != null)
            {
                // the real shape: the knuckles at the front edge of what is held (the palm round its back and
                // side), brought in from well clear until the palm touches the gun
                // (feel for that front edge: come back along the fingers' way until something is there)
                float front = radius;
                for (float s = 0.07f; s > -0.03f; s -= 0.002f)
                    if (blocked(centre + z * s - t * 0.004f, centre + z * s + t * 0.004f, 0.003f)) { front = s; break; }
                centre += z * (front - (PalmLen * 0.5f - FingerR * 0.5f) - 0.002f);
                bool PalmHits(Vector3 q)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector3 o = q + t * (i * PalmWidth * 0.3f);
                        if (blocked(o - z * (PalmLen * 0.32f), o + z * (PalmLen * 0.32f), PalmThick * 0.5f + 0.001f)) return true;
                    }
                    return false;
                }
                Vector3 q0 = centre - n * (radius + 0.07f);
                if (!PalmHits(q0))
                    for (int s = 0; s < 70; s++)
                    {
                        Vector3 nq = q0 + n * 0.002f;
                        if (PalmHits(nq)) { p = q0; break; }
                        if (Vector3.Dot(nq - centre, n) > -PalmThick * 0.3f) break;   // nothing there: keep the bar's
                        q0 = nq;
                    }
            }
            palm.localPosition = p;
            palm.localRotation = Quaternion.LookRotation(z, t);
            palm.localScale = new Vector3(PalmThick, PalmWidth, PalmLen);
            Vector3 w = p - z * (PalmLen * 0.5f);
            WristLocal = w;
            ForearmLocal = forearm.normalized;
            palmN = n; palmT = t; palmZ = z;
            wrist.gameObject.SetActive(false);   // until Wrist() is told where the forearm ends
            for (int i = -1; i <= 1; i++) { Vector3 o = p + t * (i * PalmWidth * 0.3f); Caps.Add((o - z * (PalmLen * 0.32f), o + z * (PalmLen * 0.32f), PalmThick * 0.5f)); }
            if (blocked != null) { Close(p, n, z, t, tight, trigger, thumbDir, w, blocked); return; }

            // fingers: every knuckle sits on a circle hugging the bar, so the fingers lie on it all the way round
            // instead of standing off it (from where the palm ends, round the far side; the index finger stays
            // straighter along the frame when it is on the trigger)
            float R = radius + FingerR * 1.05f;
            Vector3 u = -n, v = z;                       // the circle's plane: palm side, and the way the fingers leave
            float th0 = Mathf.Atan2(PalmLen * 0.5f, radius + PalmThick * 0.5f);
            // a slim grip is closed right round; a handguard the fingers only reach up to its top edge
            float maxWrap = radius > 0.022f ? Mathf.PI * 0.95f : Mathf.PI * 1.55f;
            for (int f = 0; f < 4; f++)
            {
                Vector3 across = t * FingerAcross[f];
                float th = th0;
                Vector3 a = centre + across + (u * Mathf.Cos(th) + v * Mathf.Sin(th)) * R;
                for (int k = 0; k < 3; k++)
                {
                    float L = Fingers[f][k];
                    float dth = 2f * Mathf.Asin(Mathf.Min(0.98f, L / (2f * R)));
                    if (trigger && f == 0) dth *= k == 0 ? 0.3f : 0.7f;
                    dth *= 1f + tight * 0.15f;
                    th = Mathf.Min(th + dth, maxWrap);                                // fingertips stop where they meet the thumb
                    Vector3 b = centre + across + (u * Mathf.Cos(th) + v * Mathf.Sin(th)) * R;
                    if (trigger && f == 0) b = a + (b - a).normalized * L;          // a straighter trigger finger keeps its length
                    Bar(seg[f, k], a, b, FingerR * (k == 2 ? 0.9f : 1f));
                    a = b;
                }
            }
            // thumb: from the heel of the hand on the thumb side, forward and round the other side of the bar
            Vector3 tb = w + z * 0.03f + t * 0.03f;
            thumb[0].localPosition = tb;
            thumb[0].localRotation = Quaternion.LookRotation(z, t);
            thumb[0].localScale = new Vector3(0.019f, 0.022f, 0.03f);
            Vector3 td = thumbDir.HasValue ? thumbDir.Value.normalized : (z * 0.8f + t * 0.15f + n * 0.55f).normalized;
            Vector3 t1 = tb + td * 0.038f;
            Bar(thumb[1], tb, t1, ThumbR);
            Vector3 td2 = Quaternion.AngleAxis(28f, Vector3.Cross(td, n).normalized) * td;
            Bar(thumb[2], t1, t1 + td2 * 0.03f, ThumbR * 0.9f);
        }

        /// <summary>
        /// Close the fingers and thumb on the real shape, joint by joint as a hand does: each joint bends in until
        /// that segment touches the gun and stops there, so nothing sinks into it - round a slim grip they close
        /// right round, under a handguard they come up its side and over the top edge. A segment that would start
        /// inside it is opened out instead. Free of anything, they settle into a relaxed curl.
        /// </summary>
        void Close(Vector3 p, Vector3 n, Vector3 z, Vector3 t, float tight, bool trigger, Vector3? thumbDir, Vector3 w, Blocked blocked)
        {
            Vector3 curl = Vector3.Cross(z, n).normalized;
            for (int f = 0; f < 4; f++)
            {
                bool trig = trigger && f == 0;   // the trigger finger stays long, along the frame
                // the knuckles just clear of what the palm rests on, so each finger starts free to close on it
                Vector3 a = p + z * (PalmLen * 0.5f - FingerR * 0.5f) + t * FingerAcross[f] + n * 0.003f;
                float total = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float L = Fingers[f][k], rad = FingerR * (k == 2 ? 0.9f : 1f);
                    float lim = trig ? (k == 0 ? 25f : 45f) : (k == 1 ? 100f : 85f);
                    total += Bend(a, z, curl, total, L, rad, lim, lim * (0.45f + 0.5f * tight), blocked);
                    Vector3 b = a + Quaternion.AngleAxis(total, curl) * z * L;
                    Bar(seg[f, k], a, b, rad);
                    Caps.Add((a, b, rad));
                    a = b;
                }
            }
            // thumb: from the heel of the hand on the thumb side, round the other side of the gun to meet it
            Vector3 tb = w + z * 0.03f + t * 0.03f;
            thumb[0].localPosition = tb;
            thumb[0].localRotation = Quaternion.LookRotation(z, t);
            thumb[0].localScale = new Vector3(0.019f, 0.022f, 0.03f);
            Vector3 td = thumbDir.HasValue ? thumbDir.Value.normalized : (z * 0.8f + t * 0.15f + n * 0.55f).normalized;
            Vector3 tc = Vector3.Cross(td, n);
            tc = tc.sqrMagnitude > 1e-4f ? tc.normalized : t;
            Vector3 ta = tb; float tot = 0f;
            for (int k = 0; k < 2; k++)
            {
                float L = k == 0 ? 0.038f : 0.03f, rad = ThumbR * (k == 1 ? 0.9f : 1f);
                tot += Bend(ta, td, tc, tot, L, rad, k == 0 ? 50f : 55f, k == 0 ? 30f : 40f, blocked);
                Vector3 tb2 = ta + Quaternion.AngleAxis(tot, tc) * td * L;
                Bar(thumb[k + 1], ta, tb2, rad);
                Caps.Add((ta, tb2, rad));
                ta = tb2;
            }
        }

        Vector3 palmN, palmT, palmZ;   // the posed palm's facing, thumb-ward and finger-ward directions (parent space)
        public Vector3 PalmZ => palmZ;  // wrist to knuckles
        public Vector3 PalmN => palmN;  // the way the palm faces

        /// <summary>
        /// The wrist: a flattened bridge of the same skin from where your real forearm ends (world space) into the
        /// heel of the palm, rebuilt every frame - so as the arm sways and the hand stays on the gun, the wrist bends
        /// and twists between them instead of a gap or a seam. Pass null to hide it.
        /// </summary>
        public void Wrist(Vector3? forearmEnd)
        {
            if (forearmEnd == null) { if (wrist.gameObject.activeSelf) wrist.gameObject.SetActive(false); return; }
            if (!wrist.gameObject.activeSelf) wrist.gameObject.SetActive(true);
            Vector3 a = transform.InverseTransformPoint(forearmEnd.Value);
            Vector3 heel = WristLocal + palmZ * 0.012f;                // a little into the palm, so they merge
            Vector3 d = heel - a; float len = d.magnitude;
            if (len < 1e-4f) { wrist.gameObject.SetActive(false); return; }
            d /= len;
            a -= d * 0.012f;                                           // and a little into the forearm
            len += 0.012f;
            // flat across the back of the hand, as a real wrist is: wide along the knuckles, thinner palm to back
            Vector3 across = Vector3.ProjectOnPlane(palmT, d);
            if (across.sqrMagnitude < 1e-6f) across = Vector3.ProjectOnPlane(palmN, d);
            wrist.localPosition = (a + heel) * 0.5f;
            wrist.localRotation = Quaternion.LookRotation(d, across) * Quaternion.Euler(90f, 0f, 0f);
            const float width = 0.062f, thick = 0.04f;
            wrist.localScale = new Vector3(thick, (len + thick) * 0.5f, width);
        }

        /// <summary>How far one joint bends (degrees, on top of `already`) before its segment touches.</summary>
        static float Bend(Vector3 a, Vector3 dir0, Vector3 axis, float already, float len, float rad, float lim, float rest, Blocked blocked)
        {
            bool Hit(float ph) => blocked(a, a + Quaternion.AngleAxis(already + ph, axis) * dir0 * len, rad + 0.0012f);
            float phi = 0f;
            if (Hit(0f))
            {
                // already against something (the underside of a trigger guard): tuck in under it first, the way a
                // finger does, closing on until it touches again; only if it can't, open it out
                float tuck = 4f;
                while (tuck <= lim && Hit(tuck)) tuck += 4f;
                if (tuck <= lim)
                {
                    while (tuck + 4f <= lim && !Hit(tuck + 4f)) tuck += 4f;
                    return tuck;
                }
                while (phi > -80f && Hit(phi)) phi -= 5f;
                return phi;
            }
            while (phi + 4f <= lim)
            {
                if (Hit(phi + 4f)) return phi;
                phi += 4f;
            }
            return lim;   // (nothing touched: closed right up, the way a gripping hand does)
        }
    }
}
