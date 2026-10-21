using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Breaks the real bone you see through a wound where it was hit. Every break is procedural: a light hit
    /// leaves a crack, a solid one snaps the bone with splintered, jagged ends knocked off-line, and a blast
    /// blows a chunk out and leaves splinters lying in the wound. A bone can break in several places.
    /// </summary>
    public static class BoneBreaker
    {
        const int Segs = 10;

        /// <summary>Break a bone at a point (part space). severity 0..1: crack .. snapped .. shattered.</summary>
        public static void Break(Organ o, Vector3 localPoint, float severity)
        {
            if (o != null && o.part != null && severity > 0.25f) Sfx.Play("bonebreak", o.part.transform.TransformPoint(localPoint), Mathf.Lerp(0.12f, 0.25f, severity), 1f, 0.3f, 0.06f, 5f);   // a quiet crunch you only hear up close
            if (o == null || o.reveal == null || !o.def.capsule || o.def.shell) return;
            if (o.def.kind != TissueKind.Bone && o.def.kind != TissueKind.Spine) return;
            Vector3 a = o.def.a, b = o.def.b, ab = b - a;
            float len = ab.magnitude;
            if (len < 0.02f) return;
            float t = Mathf.Clamp(Vector3.Dot(localPoint - a, ab) / ab.sqrMagnitude, 0.07f, 0.93f);
            severity = Mathf.Clamp01(severity);
            // another hit on the same break makes it worse rather than adding a second one
            for (int i = 0; i < o.breaks.Count; i++)
            {
                var br = o.breaks[i];
                if (Mathf.Abs(br.x - t) * len < 0.03f)
                {
                    o.breaks[i] = new Vector4((br.x + t) * 0.5f, Mathf.Clamp01(Mathf.Max(br.y, severity) + 0.3f * Mathf.Min(br.y, severity)), br.z, 0f);
                    Rebuild(o);
                    return;
                }
            }
            o.breaks.Add(new Vector4(t, severity, Random.Range(1f, 9999f), 0f));
            Rebuild(o);
        }

        static float Hash(float seed, int i) { float x = Mathf.Sin(seed * 12.9898f + i * 78.233f) * 43758.5453f; return x - Mathf.Floor(x); }

        static readonly List<Vector3> V = new List<Vector3>();
        static readonly List<int> T = new List<int>();

        static void Rebuild(Organ o)
        {
            o.breaks.Sort((x, y) => x.x.CompareTo(y.x));
            Vector3 a = o.def.a, b = o.def.b, ab = b - a;
            float len = ab.magnitude;
            Vector3 ax = ab / len;
            Vector3 u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized, w = Vector3.Cross(ax, u);
            float r = o.def.kind == TissueKind.Spine ? o.def.radii.x * 0.75f : o.def.radii.x;

            V.Clear(); T.Clear();
            // walk the bone: [start, break0) [break0, break1) ... [breakN, end]
            float segStart = 0f; Vector3 startOff = Vector3.zero; float startJag = -1f; float startSeed = 0f;
            for (int i = 0; i <= o.breaks.Count; i++)
            {
                float segEnd; Vector3 endOff = Vector3.zero; float endJag = -1f, endSeed = 0f;
                float nextStart = 1f; Vector3 nextOff = Vector3.zero;
                if (i < o.breaks.Count)
                {
                    var br = o.breaks[i];
                    float s = br.y, seed = br.z;
                    // gap: a hairline crack, a snapped bone, or a chunk blown clean out
                    float gap = r * (0.06f + 1.1f * s * s) + (s > 0.85f ? (0.015f + 0.03f * Hash(seed, 1)) : 0f);
                    float halfT = gap * 0.5f / len;
                    segEnd = br.x - halfT;
                    nextStart = br.x + halfT;
                    // the bone is knocked off-line at the break: the near piece a little, the far one more
                    float ang = Hash(seed, 2) * Mathf.PI * 2f;
                    Vector3 side = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                    float shove = r * (s < 0.3f ? 0.05f : 1.4f * s);
                    endOff = -side * shove * 0.35f;
                    nextOff = side * shove * 0.65f + ax * 0f;
                    endJag = s; endSeed = seed;
                    if (s > 0.6f) AddSplinters(a + ab * br.x, ax, side, r, s, seed);
                }
                else segEnd = 1f;

                if (segEnd - segStart > 0.01f)
                    AddSegment(a + ab * segStart + startOff, a + ab * segEnd + endOff, r, startJag, startSeed, endJag, endSeed);

                segStart = nextStart; startOff = nextOff; startJag = endJag; startSeed = endSeed;
            }

            var mesh = new Mesh { name = o.def.name + " (broken)" };
            mesh.SetVertices(V);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var mf = o.reveal.GetComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            // the broken mesh is built in part space
            o.reveal.transform.localPosition = Vector3.zero;
            o.reveal.transform.localRotation = Quaternion.identity;
            o.reveal.transform.localScale = Vector3.one;
        }

        /// <summary>A length of bone. jag &lt; 0 = the natural rounded end; otherwise a fractured, splintered end.</summary>
        static void AddSegment(Vector3 p0, Vector3 p1, float r, float jag0, float seed0, float jag1, float seed1)
        {
            Vector3 d = p1 - p0;
            float L = d.magnitude;
            if (L < 1e-4f) return;
            Vector3 ax = d / L;
            Vector3 u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized, w = Vector3.Cross(ax, u);
            int rings = Mathf.Max(3, Mathf.CeilToInt(L / 0.012f));
            int baseIdx = V.Count;
            for (int k = 0; k <= rings; k++)
            {
                float t = k / (float)rings;
                for (int s = 0; s < Segs; s++)
                {
                    float ang = s / (float)Segs * Mathf.PI * 2f;
                    float along = 0f;
                    float rr = r * (0.94f + 0.12f * Hash(seed0 + seed1 + 3.1f, k * 31 + s));
                    // the very end ring is torn into spikes along the bone
                    if (k == 0 && jag0 >= 0f) { along = Fracture(seed0, ang, s) * r * (0.4f + 2.2f * jag0); rr *= 0.9f + 0.15f * Hash(seed0, s + 50); }
                    if (k == rings && jag1 >= 0f) { along = Fracture(seed1, ang, s) * r * (0.4f + 2.2f * jag1); rr *= 0.9f + 0.15f * Hash(seed1, s + 50); }
                    V.Add(p0 + d * t + ax * along + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * rr);
                }
            }
            for (int k = 0; k < rings; k++)
                for (int s = 0; s < Segs; s++)
                {
                    int s1 = (s + 1) % Segs;
                    int i0 = baseIdx + k * Segs + s, i1 = baseIdx + k * Segs + s1, j0 = i0 + Segs, j1 = i1 + Segs;
                    T.Add(i0); T.Add(i1); T.Add(j0);
                    T.Add(i1); T.Add(j1); T.Add(j0);
                }
            // ends: a rounded joint end, or a broken face recessed into the marrow
            Cap(baseIdx, p0, -ax, r, jag0, true);
            Cap(baseIdx + rings * Segs, p1, ax, r, jag1, false);
        }

        /// <summary>
        /// Shape of a broken end around the bone (-0.5..0.5): an oblique, wavy break with one or two sharp points,
        /// like a real spiral / oblique fracture - not a crown of random spikes.
        /// </summary>
        static float Fracture(float seed, float ang, int s)
        {
            float p1 = Hash(seed, 7) * 6.283f, p2 = Hash(seed, 8) * 6.283f;
            float f = 0.42f * Mathf.Sin(ang + p1) + 0.18f * Mathf.Sin(ang * 2f + p2);
            float spike = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(ang - p2)), 12f) * 0.35f;   // one long point
            return f + spike + (Hash(seed, s) - 0.5f) * 0.06f;
        }

        static void Cap(int ring, Vector3 at, Vector3 outward, float r, float jag, bool start)
        {
            Vector3 c = jag >= 0f ? at - outward * r * (0.25f + 0.4f * jag) : at + outward * r * 0.8f;
            int ci = V.Count;
            V.Add(c);
            for (int s = 0; s < Segs; s++)
            {
                int s1 = (s + 1) % Segs;
                if (start) { T.Add(ci); T.Add(ring + s1); T.Add(ring + s); }
                else { T.Add(ci); T.Add(ring + s); T.Add(ring + s1); }
            }
        }

        /// <summary>Loose bone splinters lying around a bad break.</summary>
        static void AddSplinters(Vector3 at, Vector3 ax, Vector3 side, float r, float s, float seed)
        {
            int n = 1 + Mathf.RoundToInt(s * 2.5f);
            Vector3 u = Vector3.Cross(ax, side).normalized;
            for (int i = 0; i < n; i++)
            {
                float ang = Hash(seed, 100 + i) * Mathf.PI * 2f;
                Vector3 radial = side * Mathf.Cos(ang) + u * Mathf.Sin(ang);
                Vector3 c = at + radial * r * (1.2f + 1.6f * Hash(seed, 120 + i)) + ax * (Hash(seed, 140 + i) - 0.5f) * r * 3f;
                Vector3 dir = (ax * (Hash(seed, 160 + i) - 0.5f) * 2f + radial * 0.4f + Random.insideUnitSphere * 0.3f).normalized;
                float l = r * (0.7f + 1.1f * Hash(seed, 180 + i)), th = r * (0.25f + 0.2f * Hash(seed, 200 + i));
                Vector3 p1 = c + dir * l, n1 = Vector3.Cross(dir, radial).normalized * th, n2 = Vector3.Cross(dir, n1).normalized * th;
                int bi = V.Count;
                V.Add(c + n1); V.Add(c - n1 * 0.5f + n2); V.Add(c - n1 * 0.5f - n2); V.Add(p1);
                // each face wound to point away from the shard's centre
                Vector3 cen = (V[bi] + V[bi + 1] + V[bi + 2] + V[bi + 3]) * 0.25f;
                int[] f = { 0, 1, 2, 0, 3, 1, 1, 3, 2, 2, 3, 0 };
                for (int q = 0; q < f.Length; q += 3)
                {
                    Vector3 A = V[bi + f[q]], B = V[bi + f[q + 1]], C = V[bi + f[q + 2]];
                    bool outward = Vector3.Dot(Vector3.Cross(B - A, C - A), (A + B + C) / 3f - cen) > 0f;
                    T.Add(bi + f[q]);
                    if (outward) { T.Add(bi + f[q + 1]); T.Add(bi + f[q + 2]); }
                    else { T.Add(bi + f[q + 2]); T.Add(bi + f[q + 1]); }
                }
            }
        }
    }
}
