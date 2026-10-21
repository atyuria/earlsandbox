using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The torn end of a limb that came off: a lumpy, uneven face of meat (never a clean disc), a jagged
    /// splintered bone end sticking out, and ragged flaps of skin hanging around the edge. Every one is
    /// different. Built in the part's local space and parented to it.
    /// </summary>
    public static class StumpCap
    {
        static Material mat;

        static float H(float s, int i) { float x = Mathf.Sin(s * 12.9898f + i * 78.233f) * 43758.5453f; return x - Mathf.Floor(x); }

        public static GameObject Build(BodyPart part, Vector3 localCenter, Vector3 localOut, float radius, Organ bone)
        {
            if (part == null) return null;
            if (mat == null) mat = new Material(Mats.Find("Carl/WoundChannel"));
            float seed = Random.Range(1f, 999f);
            Vector3 n = localOut.normalized;
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(n, u);
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            Color meat = new Color(0.46f, 0.06f, 0.07f, 0.25f), dark = new Color(0.25f, 0.02f, 0.03f, 0.55f), fat = new Color(0.62f, 0.38f, 0.22f, 0.2f);
            Color skin = new Color(0.82f, 0.62f, 0.5f, 0.05f), boneC = new Color(0.86f, 0.82f, 0.72f, 0.15f);

            // ---- lumpy meat face: smooth rings (low-frequency noise around the circle, never per-vertex saw teeth)
            const int Seg = 48, Rings = 6;
            Vector3 c0 = localCenter - n * 0.004f;
            float p1 = H(seed, 1) * 6.28f, p2 = H(seed, 2) * 6.28f, p3 = H(seed, 3) * 6.28f;
            float Edge(float a) => 1f + 0.07f * Mathf.Sin(a * 2f + p1) + 0.05f * Mathf.Sin(a * 3f + p2) + 0.03f * Mathf.Sin(a * 5f + p3);
            float Lump(float a, float t) => Mathf.PerlinNoise(Mathf.Cos(a) * t * 2.2f + seed, Mathf.Sin(a) * t * 2.2f + seed * 0.37f) - 0.5f;
            Color Tissue(float a, float t)
            {
                // marbled meat, darker clots, a ring of yellow fat just under the skin edge
                float m = Mathf.PerlinNoise(Mathf.Cos(a) * t * 3f + seed * 1.7f, Mathf.Sin(a) * t * 3f + 4.1f);
                Color c = Color.Lerp(meat, dark, Mathf.SmoothStep(0.45f, 0.75f, m));
                return Color.Lerp(c, fat, Mathf.SmoothStep(0.72f, 0.92f, t));
            }
            int centre = verts.Count;
            verts.Add(c0 - n * radius * 0.15f); cols.Add(dark);
            for (int r = 1; r <= Rings; r++)
            {
                float t = r / (float)Rings;
                for (int s = 0; s < Seg; s++)
                {
                    float a = s / (float)Seg * Mathf.PI * 2f;
                    float bump = Lump(a, t) * radius * 0.4f * (1f - t * 0.4f);
                    float sink = -radius * 0.18f * (1f - t);              // a little hollow toward the middle
                    Vector3 p = c0 + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius * t * Mathf.Lerp(1f, Edge(a), t) + n * (bump + sink);
                    verts.Add(p);
                    cols.Add(Tissue(a, t));
                }
            }
            for (int s = 0; s < Seg; s++) { int s1 = (s + 1) % Seg; tris.AddRange(new[] { centre, centre + 1 + s1, centre + 1 + s }); }
            for (int r = 1; r < Rings; r++)
                for (int s = 0; s < Seg; s++)
                {
                    int s1 = (s + 1) % Seg;
                    int a0 = centre + 1 + (r - 1) * Seg + s, a1 = centre + 1 + (r - 1) * Seg + s1, b0 = a0 + Seg, b1 = a1 + Seg;
                    tris.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                }

            // ---- ragged skin flaps hanging off the edge: curved, rounded tongues, each side with its own vertices
            int flaps = 3 + (int)(H(seed, 99) * 3f);
            for (int f = 0; f < flaps; f++)
            {
                float a = (f + H(seed, 50 + f) * 0.7f) / flaps * Mathf.PI * 2f;
                Vector3 radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                Vector3 side = Vector3.Cross(n, radial);
                float w = radius * (0.45f + 0.3f * H(seed, 60 + f)), len = radius * (0.14f + 0.16f * H(seed, 70 + f));
                Vector3 root = c0 + radial * radius * Edge(a) * 0.98f - n * 0.002f;
                const int L = 4, Wd = 4;
                for (int sideK = 0; sideK < 2; sideK++)
                {
                    int b = verts.Count;
                    for (int i = 0; i <= L; i++)
                    {
                        float t = i / (float)L;
                        // curls outward and droops back along the limb
                        Vector3 spine = root + n * len * 0.45f * t + radial * len * 0.6f * t - n * len * 0.75f * t * t;   // peels outward and droops back
                        float halfW = w * 0.5f * (1f - t * t * 0.55f);
                        for (int j = 0; j <= Wd; j++)
                        {
                            float sj = j / (float)Wd * 2f - 1f;
                            Vector3 q = spine + side * sj * halfW + radial * (sideK == 0 ? 0.0015f : -0.0015f) - radial * (1f - sj * sj) * 0.003f * t;
                            verts.Add(q);
                            cols.Add(sideK == 0 ? skin : Color.Lerp(fat, meat, t));
                        }
                    }
                    for (int i = 0; i < L; i++)
                        for (int j = 0; j < Wd; j++)
                        {
                            int a0 = b + i * (Wd + 1) + j, a1 = a0 + 1, b0 = a0 + Wd + 1, b1 = b0 + 1;
                            if (sideK == 0) tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                            else tris.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                        }
                }
            }

            // ---- the bone: a splintered end sticking out of the meat
            if (bone != null && bone.def.capsule)
            {
                Vector3 bc = bone.def.a, bab = bone.def.b - bone.def.a;
                Vector3 nearest = bc + bab * Mathf.Clamp01(Vector3.Dot(localCenter - bc, bab) / bab.sqrMagnitude);
                Vector3 bpos = c0 + Vector3.ProjectOnPlane(nearest - c0, n);
                float br = bone.def.radii.x * 0.9f, stick = radius * (0.4f + 0.6f * H(seed, 200));
                const int BS = 10;
                int b0 = verts.Count;
                for (int k = 0; k <= 1; k++)
                    for (int s = 0; s < BS; s++)
                    {
                        float a = s / (float)BS * Mathf.PI * 2f;
                        float h = k == 0 ? -0.01f : stick * (0.45f + 0.9f * H(seed, 210 + s));   // jagged, spiky end
                        verts.Add(bpos + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * br * (k == 0 ? 1f : 0.8f + 0.25f * H(seed, 230 + s)) + n * h);
                        cols.Add(k == 0 ? new Color(0.6f, 0.25f, 0.22f, 0.2f) : boneC);
                    }
                for (int s = 0; s < BS; s++)
                {
                    int s1 = (s + 1) % BS;
                    tris.AddRange(new[] { b0 + s, b0 + s1, b0 + BS + s, b0 + s1, b0 + BS + s1, b0 + BS + s });
                }
                // marrow: dark red centre of the broken end
                int m0 = verts.Count;
                verts.Add(bpos + n * stick * 0.45f); cols.Add(new Color(0.45f, 0.08f, 0.08f, 0.3f));
                for (int s = 0; s < BS; s++) { int s1 = (s + 1) % BS; tris.AddRange(new[] { m0, b0 + BS + s1, b0 + BS + s }); }
            }

            var mesh = new Mesh { name = "Stump" };
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // the shader lights the walls of a pit; for a stump face we want normals pointing out of it
            var nrm = mesh.normals;
            int faceEnd = centre + 1 + Rings * Seg;
            for (int i = 0; i < faceEnd; i++) if (Vector3.Dot(nrm[i], n) < 0f) nrm[i] = -nrm[i];
            mesh.normals = nrm;
            mesh.RecalculateBounds();
            var go = new GameObject("StumpCap");
            go.transform.SetParent(part.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }
    }
}
