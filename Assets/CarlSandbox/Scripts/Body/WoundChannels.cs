using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The inside of open wounds. The skin shader cuts the opening out of the skin; behind each opening this
    /// builds a ragged 3D channel that narrows as it goes in, coloured by what it passes through: torn skin,
    /// yellow fat, red muscle, and whatever bone, brain or organ sits at that depth. Repeated hits widen and
    /// deepen a channel, so digging at one spot exposes more of what is underneath.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WoundChannels : MonoBehaviour
    {
        const int Rings = 10, Segs = 24;
        static Material material;
        static readonly Color Dermis = new Color(0.5f, 0.17f, 0.15f);
        static readonly Color Fat = new Color(0.56f, 0.3f, 0.18f);   // blood-soaked fat
        static readonly Color Muscle = new Color(0.42f, 0.05f, 0.06f);

        BodyPart part;
        Mesh mesh;
        bool dirty;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> norms = new List<Vector3>();
        readonly List<Color> cols = new List<Color>();
        readonly List<int> tris = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { material = null; }

        public static WoundChannels Create(BodyPart bp)
        {
            if (material == null)
            {
                var sh = Shader.Find("Carl/WoundChannel");
                material = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit"));
            }
            var go = new GameObject("WoundChannels");
            go.layer = bp.gameObject.layer;
            go.transform.SetParent(bp.transform, false);
            var wc = go.AddComponent<WoundChannels>();
            wc.part = bp;
            wc.mesh = new Mesh { name = "WoundChannels" };
            wc.mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = wc.mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return wc;
        }

        public void MarkDirty() => dirty = true;   // rebuilt once in LateUpdate, however many wounds a blast adds

        void LateUpdate()
        {
            if (!dirty || part == null || part.flesh == null) return;
            dirty = false;
            Rebuild();
        }

        // rim vertices snapped onto the skin, cached per hole (snapping searches the whole mesh)
        readonly Dictionary<int, (Vector4 key, Vector3[] pts)> rimCache = new Dictionary<int, (Vector4, Vector3[])>();

        Vector3 Rim(int hole, int idx, Vector4 key, Vector3 p, Vector3 a, float dm)
        {
            if (!rimCache.TryGetValue(hole, out var e) || e.key != key) { e = (key, new Vector3[Segs * 2]); for (int i = 0; i < e.pts.Length; i++) e.pts[i] = new Vector3(float.NaN, 0, 0); rimCache[hole] = e; }
            if (float.IsNaN(e.pts[idx].x))
            {
                // find the skin under this point along the channel axis, then sit just beneath it
                Vector3 p0 = p - a * dm;
                Vector3 onSkin = part.flesh.SnapToSkin(p0, a);
                // a snap that jumps far (to another side of the body) would make a long shard: keep the ring point
                // accept it unless it jumped to another side of the body (that would make a long shard)
                bool nearSide = Vector3.Dot(onSkin - (Vector3)key, a) < key.w * 0.8f;
                e.pts[idx] = nearSide && (onSkin - p0).magnitude < key.w * 1.6f + 0.004f ? onSkin : p0;
            }
            return e.pts[idx] + a * (dm + 0.0012f);
        }

        /// <summary>Radius of a wound's crater at a depth below the skin - matches Carl/FleshShell.</summary>
        // big wounds step down in terraces (fat, then muscle layers) instead of one straight-sided pit that looks empty
        static float CraterRadius(float r, float depthBelow) => r * 0.86f * Mathf.Exp(-Mathf.Max(0f, depthBelow) / Mathf.Min(1.5f * Mathf.Max(r, 0.003f), 0.03f));

        static float Hash(int a, int b) { uint h = (uint)(a * 374761393 + b * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xffff) / 65535f; }

        void Rebuild()
        {
            verts.Clear(); norms.Clear(); cols.Clear(); tris.Clear(); outside.Clear(); vOwner.Clear();
            var flesh = part.flesh;
            int lastOwner = 0;
            for (int h = 0; h < part.holes.Count; h++)
            {
                Vector3 c = part.holes[h];
                float r = part.holes[h].w;
                if (r > part.uvRadius * 0.3f) continue;   // big openings: the flesh layers show the inside
                // overlapping wounds are one cavity: only the biggest of them keeps a tunnel
                bool swallowed = false;
                for (int o = 0; o < part.holes.Count && !swallowed; o++)
                {
                    if (o == h) continue;
                    float ro = part.holes[o].w;
                    if (ro < r || (ro == r && o > h)) continue;
                    if (Vector3.Distance(c, part.holes[o]) < (r + ro) * 0.75f) swallowed = true;
                }
                if (swallowed) continue;
                // a big opening (blast, point-blank shotgun, stump) has no tunnel: a tube that wide cannot fit
                // in the part. You look straight in at the flesh lining the body and the organs inside it.
                Vector4 ax = part.holeAxes[h];
                Vector3 a = new Vector3(ax.x, ax.y, ax.z).normalized;
                // the wall follows the crater the flesh layers are cut to (Carl/FleshShell): it closes in
                // exponentially, so it fills the space between the layers instead of floating in it
                float depth = Mathf.Min(Mathf.Max(r * 1.2f, ax.w), r * 2.7f);
                Vector4 shape = h < part.holeShapes.Count ? part.holeShapes[h] : new Vector4(0f, 0f, 0f, h * 7.3f);
                Vector3 sxyz = shape;
                float aspect = Mathf.Max(1f, sxyz.magnitude);
                Vector3 u = Vector3.ProjectOnPlane(sxyz, a);
                if (u.sqrMagnitude < 1e-8f) u = Vector3.Cross(a, Mathf.Abs(a.y) < 0.9f ? Vector3.up : Vector3.right);
                u.Normalize();
                Vector3 v = Vector3.Cross(a, u);
                int seed = Mathf.RoundToInt(shape.w * 1000f) + h * 31;
                // follow the flesh where it has been pushed in around the opening, and sit just under the skin
                float sink = Mathf.Max(0f, Vector3.Dot(flesh.DentAt(c + u * r), a)) * 0.9f + 0.0015f;

                // the channel ends where it meets an organ or bone (the same anatomy the x-ray shows), which then
                // fills the bottom of the wound; a deeper wound has gone through the first layers it met
                // (searching a little past the torn depth: the round went through whatever is on its path)
                bool hitOrgan = false; Color organCol = Muscle;
                float reach = Mathf.Max(depth * 1.8f, depth + 0.05f);
                for (float d = 0.012f; d <= reach; d += 0.0025f)
                {
                    Color oc = flesh.TissueAt(c + a * d, out bool o, out bool thin);
                    if (!o) continue;
                    if (thin && d < depth - 0.004f) continue;      // torn through the skull/ribs: look past them
                    if (!thin && d < depth * 0.4f) continue;
                    hitOrgan = true; organCol = oc; depth = d - 0.001f; break;
                }

                while (vOwner.Count < verts.Count) vOwner.Add(lastOwner);
                lastOwner = h;
                int baseIdx = verts.Count;
                for (int k = 0; k <= Rings; k++)
                {
                    float t = k / (float)Rings;
                    float dm = Mathf.Lerp(sink, depth, t);                       // metres below the skin
                    Vector3 rc = c + a * dm;
                    // torn mouth that closes in quickly to a narrow track
                    float rr = k == 0 ? r * 1.05f : CraterRadius(r, dm);
                    // drift sideways a little, like a real wound track
                    rc += (u * (Hash(seed, k) - 0.5f) + v * (Hash(seed + 7, k) - 0.5f)) * r * 0.3f * t;
                    for (int s = 0; s < Segs; s++)
                    {
                        float ang = s / (float)Segs * Mathf.PI * 2f;
                        // big wounds are chunkier and more torn than small punctures
                        float rough = Mathf.Lerp(0.2f, 0.28f, Mathf.InverseLerp(0.01f, 0.04f, r)) * (aspect > 2f ? 0.25f : 1f);   // knife slits are clean
                        // torn but smooth: a few low-frequency lobes around the ring that drift from ring to ring
                        // (independent random radii per vertex gave saw-tooth zigzag rings)
                        float ph1 = Hash(seed, 101) * 6.283f + t * 1.7f, ph2 = Hash(seed, 202) * 6.283f - t * 2.3f, ph3 = Hash(seed, 303) * 6.283f + t * 3.1f;
                        float jag = k == 0 ? 1f : 1f + rough * (0.55f * Mathf.Sin(ang * 2f + ph1) + 0.3f * Mathf.Sin(ang * 3f + ph2) + 0.15f * Mathf.Sin(ang * 5f + ph3));
                        Vector3 radial = u * Mathf.Cos(ang) * aspect + v * Mathf.Sin(ang);
                        Vector3 p = rc + radial * rr * jag;
                        // the mouth hugs the curved skin instead of a flat ring floating over it
                        if (k <= 1) p = Rim(h, k * Segs + s, part.holes[h], p, a, dm);
                        // nothing of the channel may stick out of the body (big holes on small, curved parts)
                        // nothing of the channel may stick out of the body (big holes on small, curved parts):
                        // the mouth is pulled under the skin, deeper wall that would leave the body is dropped
                        bool outsideSkin = !flesh.InsideSkin(p, out Vector3 sk, out Vector3 sn);
                        if (outsideSkin) p = sk - sn * (0.0012f + 0.004f * t);
                        outside.Add(outsideSkin && k >= 2);
                        verts.Add(p);
                        // lit as a pit seen from outside: walls mostly face across the hole, so it stays dark inside
                        norms.Add((-radial.normalized * 0.9f - a * 0.35f).normalized);
                        cols.Add(Layer(flesh, p, dm, t));
                    }
                }
                // floor: the organ or bone it reached bulges up into the hole; otherwise dark torn muscle
                float lastR = r * 0.2f;
                Vector3 bottom = c + a * (hitOrgan ? depth - lastR * 1.5f : depth);
                int bIdx = verts.Count;
                verts.Add(bottom); norms.Add(-a); outside.Add(!flesh.InsideSkin(bottom, out _, out _));
                Color fc = hitOrgan ? organCol : Muscle; fc.a = hitOrgan ? 0.35f : 1f;
                cols.Add(fc);
                if (hitOrgan)
                    for (int s = 0; s < Segs; s++) { var oc = organCol; oc.a = 0.5f; cols[baseIdx + Rings * Segs + s] = oc; }

                for (int k = 0; k < Rings; k++)
                    for (int s = 0; s < Segs; s++)
                    {
                        int s1 = (s + 1) % Segs;
                        int i0 = baseIdx + k * Segs + s, i1 = baseIdx + k * Segs + s1;
                        int j0 = baseIdx + (k + 1) * Segs + s, j1 = baseIdx + (k + 1) * Segs + s1;
                        tris.Add(i0); tris.Add(j0); tris.Add(i1);
                        tris.Add(i1); tris.Add(j0); tris.Add(j1);
                    }
                // reaching an organ or bone, the bottom stays open: the real structure (Carl/RevealOrgan) fills it
                int last = baseIdx + Rings * Segs;
                if (!hitOrgan)
                    for (int s = 0; s < Segs; s++) { tris.Add(last + s); tris.Add(bIdx); tris.Add(last + (s + 1) % Segs); }
            }
            while (vOwner.Count < verts.Count) vOwner.Add(lastOwner);
            DropOutsideWalls();
            // where wounds overlap they become one cavity: drop the walls of one that stand inside another's opening
            if (part.holes.Count > 1) RemoveInnerWalls();
            mesh.Clear();
            if (verts.Count == 0) return;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        readonly List<bool> outside = new List<bool>();

        void DropOutsideWalls()
        {
            keep.Clear();
            for (int t = 0; t < tris.Count; t += 3)
                if (!outside[tris[t]] && !outside[tris[t + 1]] && !outside[tris[t + 2]]) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
            tris.Clear(); tris.AddRange(keep);
        }

        readonly List<int> owner = new List<int>();
        readonly List<int> vOwner = new List<int>();
        readonly List<int> keep = new List<int>();

        void RemoveInnerWalls()
        {
            // which hole each vertex belongs to (each hole adds (Rings+1)*Segs + 1 vertices, in order)
            owner.Clear();
            owner.AddRange(vOwner);   // recorded while building (holes without a tunnel add no vertices)
            keep.Clear();
            for (int t = 0; t < tris.Count; t += 3)
            {
                int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                Vector3 cen = (verts[ia] + verts[ib] + verts[ic]) / 3f;
                int own = owner[ia];
                bool inside = false;
                for (int h = 0; h < part.holes.Count && !inside; h++)
                {
                    if (h == own) continue;
                    Vector3 c = part.holes[h];
                    Vector4 ax = part.holeAxes[h];
                    Vector3 a = new Vector3(ax.x, ax.y, ax.z).normalized;
                    float along = Vector3.Dot(cen - c, a);
                    if (along < -0.003f || along > ax.w) continue;
                    float t01 = Mathf.Clamp01(along / Mathf.Max(0.001f, ax.w));
                    // inside another wound's crater (same profile as the flesh layers)? then it's open space there
                    float rr = CraterRadius(part.holes[h].w, Mathf.Max(0f, along)) * 0.97f;
                    if ((cen - c - a * along).sqrMagnitude < rr * rr) inside = true;
                }
                if (!inside) { keep.Add(ia); keep.Add(ib); keep.Add(ic); }
            }
            tris.Clear(); tris.AddRange(keep);
        }

        /// <summary>Colour of the wall: torn skin, fat, then muscle (organs and bone are real meshes). Alpha = depth.</summary>
        static Color Layer(FleshDeformer flesh, Vector3 p, float metresIn, float t)
        {
            Color c;
            if (metresIn < 0.004f) c = Dermis;
            else if (metresIn < 0.009f) c = Color.Lerp(Fat, Muscle, Mathf.InverseLerp(0.006f, 0.009f, metresIn));
            else c = Muscle;
            c.a = t;
            return c;
        }
    }
}
