using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>The empty case a gun throws out of its ejection port, by cartridge.</summary>
    public enum CaseKind { None, Nine, FiveFiveSix, SevenSixTwo, ThreeThreeEight, Fifty, TwelveGauge }

    /// <summary>
    /// Fired cases: each a real little body thrown out of the ejection port to the right, spinning, that bounces
    /// and rattles on whatever it lands on (a tinkle on hard floors) and lies there three seconds before it goes.
    /// Every cartridge has its own true shape (turned from its drawing dimensions): 9x19 brass for the pistols,
    /// 5.56x45 brass for the AR, lacquered steel 7.62x39 for the AK, .338 Lapua for the AWM, .50 BMG for the
    /// Barrett, 12 gauge shells (red hull, brass head) for the Mossberg.
    /// </summary>
    public class Casing : MonoBehaviour
    {
        const float LieTime = 3f, MaxLife = 8f;
        public CaseKind kind;
        Rigidbody rb;
        float born, landed = -1f, lastTink;

        static readonly Dictionary<CaseKind, Mesh> meshes = new Dictionary<CaseKind, Mesh>();
        static Material brass, steelCase, hull;
        static readonly List<Casing> live = new List<Casing>();
        const int MaxLive = 90;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { meshes.Clear(); live.Clear(); brass = steelCase = hull = null; }

        /// <summary>Throw a case out of a port: `right` is the gun's right, `up` its top, `back` toward the shooter.</summary>
        public static void Eject(CaseKind kind, Vector3 port, Vector3 right, Vector3 up, Vector3 back, Vector3 gunVelocity, Collider ignore = null)
        {
            if (kind == CaseKind.None) return;
            while (live.Count >= MaxLive) { var old = live[0]; live.RemoveAt(0); if (old) Destroy(old.gameObject); }
            var go = new GameObject("Case_" + kind);
            go.layer = 2;   // bullets, blood and the crosshair look straight past it
            go.transform.position = port;
            // it leaves the port lying across the gun, base toward the chamber
            go.transform.rotation = Quaternion.LookRotation(right, up) * Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(-20f, 20f), Random.Range(0f, 360f));
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(kind, out float len, out float rad);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Materials(kind);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 2; col.radius = rad; col.height = Mathf.Max(len, rad * 2f); col.center = new Vector3(0f, 0f, len * 0.5f);
            var pm = new PhysicsMaterial("Case") { bounciness = kind == CaseKind.TwelveGauge ? 0.2f : 0.35f, dynamicFriction = 0.6f, staticFriction = 0.8f, bounceCombine = PhysicsMaterialCombine.Maximum };
            col.sharedMaterial = pm;
            if (ignore != null) Physics.IgnoreCollision(col, ignore, true);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mass(kind);
            rb.linearDamping = 0.25f; rb.angularDamping = 1.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            float sp = Speed(kind);
            rb.linearVelocity = gunVelocity + (right * Random.Range(0.85f, 1.15f) + up * Random.Range(0.45f, 0.75f) + back * Random.Range(0.05f, 0.35f)).normalized * sp * Random.Range(0.85f, 1.15f);
            rb.angularVelocity = up * Random.Range(-25f, 25f) + Random.insideUnitSphere * 18f;
            var c = go.AddComponent<Casing>();
            c.kind = kind; c.rb = rb; c.born = Time.time;
            live.Add(c);
        }

        void Update()
        {
            if (landed < 0f && Time.time - born > 0.25f && rb.linearVelocity.sqrMagnitude < 0.04f) landed = Time.time;
            if ((landed >= 0f && Time.time - landed > LieTime) || Time.time - born > MaxLife) { live.Remove(this); Destroy(gameObject); }
        }

        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v < 0.6f || Time.time - lastTink < 0.08f) return;
            lastTink = Time.time;
            // brass and steel ring on a hard floor; a plastic shell just clacks; dirt and bodies deaden it
            bool soft = c.collider.GetComponentInParent<BodyPart>() != null || (c.collider.GetComponentInParent<Surface>() is Surface s && s.kind == SurfaceKind.Dirt);
            float vol = Mathf.Clamp01(v / 4f) * (soft ? 0.12f : kind == CaseKind.TwelveGauge ? 0.3f : 0.45f) * (kind == CaseKind.Fifty ? 1.3f : 1f);
            Sfx.Play("casing", transform.position, vol, 1f, 1f, kind == CaseKind.Fifty || kind == CaseKind.TwelveGauge ? 0.02f : 0.12f, 25f);
        }

        // ---- per cartridge
        static float Mass(CaseKind k) => k switch { CaseKind.Nine => 0.004f, CaseKind.FiveFiveSix => 0.006f, CaseKind.SevenSixTwo => 0.007f, CaseKind.ThreeThreeEight => 0.015f, CaseKind.Fifty => 0.058f, _ => 0.012f };
        // (thrown about as far as the real thing: a pistol drops them a metre or two away, an AR two or three)
        static float Speed(CaseKind k) => k switch { CaseKind.Nine => 2.6f, CaseKind.FiveFiveSix => 3.3f, CaseKind.SevenSixTwo => 3.6f, CaseKind.ThreeThreeEight => 1.6f, CaseKind.Fifty => 2.4f, _ => 1.9f };

        static Material[] Materials(CaseKind k)
        {
            if (brass == null)
            {
                brass = Mats.Lit(new Color(0.78f, 0.58f, 0.24f), 0.72f, 0.95f);
                steelCase = Mats.Lit(new Color(0.3f, 0.32f, 0.26f), 0.45f, 0.6f);   // lacquered steel, greenish
                hull = Mats.Lit(new Color(0.62f, 0.07f, 0.06f), 0.35f);
            }
            if (k == CaseKind.TwelveGauge) return new[] { brass, hull };
            return new[] { k == CaseKind.SevenSixTwo ? steelCase : brass };
        }

        /// <summary>
        /// The case's profile, base to mouth: (distance along it, radius) in millimetres, from the cartridge
        /// drawings - rim, extractor groove, body taper, shoulder and neck.
        /// </summary>
        static (float z, float r)[] Profile(CaseKind k) => k switch
        {
            CaseKind.Nine => new[] { (0f, 4.98f), (1.27f, 4.98f), (1.27f, 4.3f), (2.1f, 4.3f), (2.6f, 4.96f), (19.15f, 4.83f) },
            CaseKind.FiveFiveSix => new[] { (0f, 4.79f), (1.14f, 4.79f), (1.14f, 4.17f), (1.9f, 4.17f), (2.4f, 4.79f), (36.5f, 4.5f), (39.6f, 3.2f), (44.7f, 3.15f) },
            CaseKind.SevenSixTwo => new[] { (0f, 5.66f), (1.5f, 5.66f), (1.5f, 4.73f), (2.6f, 4.73f), (3.2f, 5.6f), (30.5f, 5.03f), (33f, 4.3f), (38.6f, 4.3f) },
            CaseKind.ThreeThreeEight => new[] { (0f, 7.47f), (1.5f, 7.47f), (1.5f, 6.4f), (3.1f, 6.4f), (3.6f, 7.43f), (54.9f, 7.15f), (60f, 4.7f), (69.2f, 4.7f) },
            CaseKind.Fifty => new[] { (0f, 10.2f), (2.1f, 10.2f), (2.1f, 8.6f), (4.2f, 8.6f), (4.8f, 10.2f), (76.4f, 9.4f), (82.9f, 7.1f), (99.3f, 7.1f) },
            _ => new[] { (0f, 11.2f), (1.4f, 11.2f), (1.4f, 10.4f), (12f, 10.4f), (12f, 10.1f), (70f, 10.1f) },   // 12 gauge, 2 3/4"
        };

        static Mesh MeshFor(CaseKind k, out float len, out float rad)
        {
            var prof = Profile(k);
            len = prof[prof.Length - 1].z * 0.001f;
            rad = 0f; foreach (var p in prof) rad = Mathf.Max(rad, p.r * 0.001f);
            if (meshes.TryGetValue(k, out var m) && m) return m;
            // a lathe: rings at each profile point, a solid base, the mouth open a little way (dark inside)
            const int seg = 14;
            var v = new List<Vector3>(); var n = new List<Vector3>();
            var head = new List<int>(); var body = new List<int>();
            int split = k == CaseKind.TwelveGauge ? 3 : 99;   // the shell's brass head stops at the fourth ring
            void Ring(float z, float r, Vector3 nrm)
            {
                for (int s = 0; s <= seg; s++)
                {
                    float a = s * Mathf.PI * 2f / seg;
                    var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    v.Add(new Vector3(dir.x * r, dir.y * r, z) * 0.001f);
                    n.Add(nrm == Vector3.zero ? dir : nrm);
                }
            }
            // base cap
            int c0 = v.Count; v.Add(Vector3.zero); n.Add(Vector3.back);
            Ring(0f, prof[0].r, Vector3.back);
            for (int s = 0; s < seg; s++) head.AddRange(new[] { c0, c0 + 1 + s + 1, c0 + 1 + s });
            // sides
            for (int i = 0; i < prof.Length - 1; i++)
            {
                int a0 = v.Count; Ring(prof[i].z, prof[i].r, Vector3.zero);
                int b0 = v.Count; Ring(prof[i + 1].z, prof[i + 1].r, Vector3.zero);
                var list = i < split ? head : body;
                for (int s = 0; s < seg; s++) list.AddRange(new[] { a0 + s, a0 + s + 1, b0 + s, a0 + s + 1, b0 + s + 1, b0 + s });
            }
            // the open mouth: a short dark inner wall
            var last = prof[prof.Length - 1];
            int m0 = v.Count; Ring(last.z, last.r, Vector3.zero);
            int i0 = v.Count; Ring(last.z, last.r * 0.85f, Vector3.zero);
            int i1 = v.Count; Ring(last.z - 3f, last.r * 0.85f, Vector3.zero);
            var mouth = k == CaseKind.TwelveGauge ? body : head.Count > 0 && split == 99 ? head : body;
            for (int s = 0; s < seg; s++)
            {
                mouth.AddRange(new[] { m0 + s, m0 + s + 1, i0 + s, m0 + s + 1, i0 + s + 1, i0 + s });
                mouth.AddRange(new[] { i0 + s, i0 + s + 1, i1 + s, i0 + s + 1, i1 + s + 1, i1 + s });
            }
            m = new Mesh { name = "Case_" + k };
            m.SetVertices(v); m.SetNormals(n);
            if (k == CaseKind.TwelveGauge) { m.subMeshCount = 2; m.SetTriangles(head, 0); m.SetTriangles(body, 1); }
            else { head.AddRange(body); m.SetTriangles(head, 0); }
            m.RecalculateNormals();
            m.RecalculateBounds();
            meshes[k] = m;
            return m;
        }
    }
}
