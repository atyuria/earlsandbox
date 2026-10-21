using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>More anatomical shapes for the x-ray and for what shows through wounds.</summary>
    public static class AnatomyMeshes
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); }

        /// <summary>
        /// A long bone along local Y: narrow shaft flaring into knobbly ends (condyles / heads), slightly bowed.
        /// halfLen: half the distance between the joint centres, r: shaft-ish radius.
        /// </summary>
        public static Mesh LongBone(float halfLen, float r)
        {
            string key = $"bone{halfLen:0.000}_{r:0.000}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            const int Seg = 14, Rings = 26;
            var verts = new List<Vector3>(); var tris = new List<int>();
            float total = halfLen + r;
            for (int k = 0; k <= Rings; k++)
            {
                float t = k / (float)Rings;                 // 0..1 along the bone
                float y = Mathf.Lerp(-total, total, t);
                float end = Mathf.Max(Mathf.InverseLerp(0.28f, 0.0f, t), Mathf.InverseLerp(0.72f, 1f, t));   // 0 shaft .. 1 at the ends
                float rr = r * Mathf.Lerp(0.62f, 1.35f, end * end);
                // round off the very tips
                float tip = Mathf.Min(t, 1f - t) / 0.06f;
                if (tip < 1f) rr *= Mathf.Sqrt(Mathf.Max(0f, tip * (2f - tip)));
                float bow = Mathf.Sin(t * Mathf.PI) * r * 0.25f;
                for (int s = 0; s < Seg; s++)
                {
                    float a = s / (float)Seg * Mathf.PI * 2f;
                    // two condyles at the ends: a groove front and back
                    float lobe = 1f + end * 0.18f * Mathf.Cos(2f * a);
                    verts.Add(new Vector3(Mathf.Cos(a) * rr * lobe + bow, y, Mathf.Sin(a) * rr * (1f - end * 0.1f)));
                }
            }
            for (int k = 0; k < Rings; k++)
                for (int s = 0; s < Seg; s++)
                {
                    int s1 = (s + 1) % Seg;
                    int a0 = k * Seg + s, a1 = k * Seg + s1, b0 = a0 + Seg, b1 = a1 + Seg;
                    tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            int bot = verts.Count; verts.Add(new Vector3(0, -total, 0));
            int top = verts.Count; verts.Add(new Vector3(0, total, 0));
            for (int s = 0; s < Seg; s++)
            {
                int s1 = (s + 1) % Seg;
                tris.AddRange(new[] { bot, s, s1 });
                tris.AddRange(new[] { top, Rings * Seg + s1, Rings * Seg + s });
            }
            m = new Mesh { name = "LongBone" };
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        /// <summary>An ellipsoid with brain folds (gyri and sulci) and the deep groove between the halves.</summary>
        public static Mesh Brain(Vector3 radii, bool midline)
        {
            string key = $"brain{radii.x:0.000}_{radii.y:0.000}_{radii.z:0.000}_{midline}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            var src = ProcMesh.Ellipsoid(radii, 40, 30);
            var v = src.vertices;
            float scale = Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z));
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = v[i];
                Vector3 n = new Vector3(p.x / (radii.x * radii.x), p.y / (radii.y * radii.y), p.z / (radii.z * radii.z)).normalized;
                Vector3 q = p / scale;
                // winding folds: ridged noise from a few warped sine fields
                float f = Mathf.Sin(q.x * 9f + Mathf.Sin(q.z * 7f) * 2f) * Mathf.Sin(q.z * 8f + Mathf.Sin(q.y * 6f) * 2f) + 0.5f * Mathf.Sin(q.y * 11f + q.x * 5f);
                float ridge = 1f - Mathf.Abs(f);
                float d = (ridge * ridge - 0.45f) * scale * 0.09f;
                if (midline) d -= Mathf.Max(0f, 1f - Mathf.Abs(p.x) / (radii.x * 0.08f)) * scale * 0.18f * Mathf.Clamp01(p.y / radii.y + 0.4f);
                v[i] = p + n * d;
            }
            m = new Mesh { name = "Brain" };
            m.vertices = v; m.triangles = src.triangles; m.RecalculateNormals(); m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        /// <summary>A soft organ: an ellipsoid with gentle lumps so it doesn't look like a ball.</summary>
        public static Mesh Organ(Vector3 radii, float seed)
        {
            string key = $"organ{radii.x:0.000}_{radii.y:0.000}_{radii.z:0.000}_{(int)seed}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            var src = ProcMesh.Ellipsoid(radii, 24, 18);
            var v = src.vertices;
            float scale = Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z));
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 q = v[i] / scale;
                float n = Mathf.PerlinNoise(q.x * 1.3f + seed, q.y * 1.3f + q.z * 0.7f) - 0.5f;
                v[i] *= 1f + n * 0.22f;
            }
            m = new Mesh { name = "Organ" };
            m.vertices = v; m.triangles = src.triangles; m.RecalculateNormals(); m.RecalculateBounds();
            cache[key] = m;
            return m;
        }
    }
}
