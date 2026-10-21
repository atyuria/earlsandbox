using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Bits torn out of a body by a blast or a limb coming off: lumps of meat with skin on one side, yellow fat,
    /// and splinters of bone. They fly, bounce, roll, leave blood where they land and are cleared away after a while
    /// (and the oldest go first if there are too many).
    /// </summary>
    public class Gib : MonoBehaviour
    {
        static readonly List<Gib> live = new List<Gib>();
        static Material mat;
        const int MaxLive = 140;
        const float Life = 45f;
        float born;
        bool landed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { live.Clear(); mat = null; }

        public enum Kind { Meat, Fat, Bone }

        /// <summary>A burst of bits from a point, flung roughly along dir. size: rough radius of the biggest (m).</summary>
        public static void Burst(Vector3 pos, Vector3 dir, int count, float size, float speed, bool bone = true)
        {
            for (int i = 0; i < count; i++)
            {
                float r = Random.value;
                var kind = bone && r < 0.18f ? Kind.Bone : r < 0.35f ? Kind.Fat : Kind.Meat;
                float s = size * Random.Range(0.35f, 1f);
                Vector3 v = (dir.normalized + Random.insideUnitSphere * 0.9f).normalized * speed * Random.Range(0.4f, 1.1f);
                Spawn(pos + Random.insideUnitSphere * size * 0.5f, v, s, kind);
            }
        }

        public static Gib Spawn(Vector3 pos, Vector3 vel, float size, Kind kind)
        {
            if (mat == null) mat = new Material(Mats.Find("Carl/WoundChannel"));
            while (live.Count >= MaxLive) { var old = live[0]; live.RemoveAt(0); if (old) Destroy(old.gameObject); }
            var go = new GameObject("Gib");
            go.transform.position = pos;
            go.transform.rotation = Random.rotation;
            go.AddComponent<MeshFilter>().sharedMesh = MakeMesh(size, kind);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            var col = go.AddComponent<SphereCollider>();
            col.radius = size * (kind == Kind.Bone ? 0.5f : 0.75f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(size * size * size * 1000f * 4f, 0.01f, 0.8f);
            rb.linearDamping = 0.2f; rb.angularDamping = 0.6f;
            rb.linearVelocity = vel;
            rb.angularVelocity = Random.insideUnitSphere * 25f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            col.sharedMaterial = new PhysicsMaterial("Gib") { bounciness = kind == Kind.Bone ? 0.3f : 0.05f, dynamicFriction = 0.8f, staticFriction = 0.9f };
            var g = go.AddComponent<Gib>();
            g.born = Time.time;
            live.Add(g);
            return g;
        }

        void OnCollisionEnter(Collision c)
        {
            if (landed || c.contactCount == 0 || c.collider.GetComponentInParent<BodyPart>() != null) return;
            landed = true;
            var p = c.GetContact(0);
            BloodSystem.Instance?.Ooze(p.point, p.normal, Random.Range(0.04f, 0.09f), Random.Range(0.4f, 0.8f));
            if (c.relativeVelocity.magnitude > 3f) Sfx.Play("impact_flesh", p.point, 0.35f, 1f, 1.5f, 0.15f, 25f);
        }

        void Update()
        {
            if (Time.time - born > Life) { live.Remove(this); Destroy(gameObject); }
        }

        /// <summary>
        /// A lumpy chunk: meat inside, with a patch of skin on one face (it was torn from the surface); fat is
        /// a pale blob; bone is a splinter, long and pointed. Vertex colours are what the wound shader paints with.
        /// </summary>
        static Mesh MakeMesh(float size, Kind kind)
        {
            Vector3 radii = kind == Kind.Bone ? new Vector3(size * 0.28f, size * 1.1f, size * 0.32f)
                          : kind == Kind.Fat ? new Vector3(size * 0.9f, size * 0.6f, size * 0.8f)
                          : new Vector3(size, size * Random.Range(0.55f, 0.9f), size * Random.Range(0.7f, 1f));
            var m = ProcMesh.Ellipsoid(radii, 10, 8);
            var v = m.vertices;
            var cols = new Color[v.Length];
            float seed = Random.Range(0f, 100f);
            Vector3 skinSide = Random.onUnitSphere;
            Color meat = new Color(0.56f, 0.08f, 0.09f, 0.05f), dark = new Color(0.32f, 0.03f, 0.04f, 0.1f);
            Color fat = new Color(0.88f, 0.72f, 0.44f, 0.05f), skin = new Color(0.82f, 0.6f, 0.5f, 0f), boneC = new Color(0.92f, 0.88f, 0.78f, 0f);
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 q = new Vector3(v[i].x / radii.x, v[i].y / radii.y, v[i].z / radii.z);
                if (kind == Kind.Bone)
                {
                    // taper both ends to splintered points
                    float t = Mathf.Abs(q.y);
                    v[i] = new Vector3(v[i].x * (1f - t * 0.75f), v[i].y, v[i].z * (1f - t * 0.75f));
                    cols[i] = Color.Lerp(boneC, new Color(0.6f, 0.2f, 0.18f, 0.1f), Mathf.PerlinNoise(q.y * 3f + seed, seed) * 0.6f);
                    continue;
                }
                // torn, lumpy surface
                float n = Mathf.PerlinNoise(q.x * 2.3f + seed, q.y * 2.3f + q.z * 1.7f + seed * 0.3f);
                v[i] *= 0.75f + 0.5f * n;
                if (kind == Kind.Fat) { cols[i] = Color.Lerp(fat, meat, Mathf.Clamp01(n - 0.55f) * 1.5f); continue; }
                bool onSkin = Vector3.Dot(q.normalized, skinSide) > 0.55f;
                cols[i] = onSkin ? skin : Color.Lerp(meat, dark, Mathf.SmoothStep(0.5f, 0.75f, n));
                // a thin layer of fat just under the skin
                if (!onSkin && Vector3.Dot(q.normalized, skinSide) > 0.35f) cols[i] = fat;
            }
            m.vertices = v;
            m.colors = cols;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
