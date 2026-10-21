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

        // ============================================================ detailed x-ray anatomy
        // Vertex colours carry the detail: alpha 0 = shade of the structure's own colour (rgb is a multiplier),
        // alpha 1 = a colour of its own (bone in the spine, vessels on the heart, teeth in the jaw...).

        static Color Shade(float k) => new Color(k, k, k, 0f);
        static readonly Color Own = new Color(1f, 1f, 1f, 0f);
        static readonly Color BoneC = new Color(0.93f, 0.89f, 0.78f, 1f);
        static readonly Color CartilageC = new Color(0.72f, 0.84f, 0.95f, 1f);
        static readonly Color ArteryC = new Color(0.92f, 0.12f, 0.14f, 1f);
        static readonly Color VeinC = new Color(0.25f, 0.38f, 0.95f, 1f);
        static readonly Color PulmonaryC = new Color(0.5f, 0.4f, 0.92f, 1f);
        static readonly Color CordC = new Color(0.3f, 0.9f, 0.95f, 1f);
        static readonly Color ToothC = new Color(0.99f, 0.97f, 0.9f, 1f);
        static readonly Color BronchusC = new Color(0.86f, 0.93f, 1f, 1f);

        /// <summary>Meshes drawn with the x-ray shader need vertex colours: plain white-alpha-0 means "its own colour".</summary>
        public static void EnsureColors(Mesh m)
        {
            if (m == null || (m.colors != null && m.colors.Length == m.vertexCount)) return;
            var c = new Color[m.vertexCount];
            for (int i = 0; i < c.Length; i++) c[i] = Own;
            m.colors = c;
        }

        /// <summary>Collects pieces (tubes, blobs, existing meshes) into one mesh with per-vertex colours.</summary>
        class MB
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<Color> c = new List<Color>();
            readonly List<int> t = new List<int>();

            public void Add(Mesh m, Matrix4x4 M, Color col, System.Func<Vector3, Color> colFn = null)
            {
                int b = v.Count;
                var mv = m.vertices; var mn = m.normals;
                if (mn == null || mn.Length != mv.Length) { m.RecalculateNormals(); mn = m.normals; }
                for (int i = 0; i < mv.Length; i++)
                {
                    v.Add(M.MultiplyPoint3x4(mv[i]));
                    n.Add(M.MultiplyVector(mn[i]).normalized);
                    c.Add(colFn != null ? colFn(mv[i]) : col);
                }
                foreach (int idx in m.triangles) t.Add(b + idx);
            }

            public void Blob(Vector3 centre, Vector3 radii, Quaternion rot, Color col, int seg = 12, int rings = 9)
                => Add(ProcMesh.Ellipsoid(radii, seg, rings), Matrix4x4.TRS(centre, rot, Vector3.one), col);

            public void Blob(Vector3 centre, Vector3 radii, Color col, int seg = 12, int rings = 9) => Blob(centre, radii, Quaternion.identity, col, seg, rings);

            public void Tube(IList<Vector3> pts, float r, Color col, int sides = 8) => Tube(pts, i => r, i => col, sides);

            /// <summary>A capped tube along a polyline with a radius and colour per point.</summary>
            public void Tube(IList<Vector3> pts, System.Func<int, float> rad, System.Func<int, Color> colAt, int sides = 8)
            {
                if (pts.Count < 2) return;
                int b0 = v.Count;
                Vector3 prevN = Vector3.zero, t0 = Vector3.zero, tl = Vector3.zero;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 tg = i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1];
                    tg.Normalize();
                    if (i == 0) t0 = tg;
                    tl = tg;
                    Vector3 nr = i == 0 ? Vector3.Cross(tg, Mathf.Abs(tg.y) < 0.9f ? Vector3.up : Vector3.right).normalized : Vector3.ProjectOnPlane(prevN, tg).normalized;
                    if (nr.sqrMagnitude < 0.001f) nr = Vector3.Cross(tg, Vector3.right).normalized;
                    prevN = nr;
                    Vector3 bn = Vector3.Cross(tg, nr);
                    float r = rad(i); var col = colAt(i);
                    for (int s = 0; s < sides; s++)
                    {
                        float a = s / (float)sides * Mathf.PI * 2f;
                        Vector3 d = nr * Mathf.Cos(a) + bn * Mathf.Sin(a);
                        v.Add(pts[i] + d * r); n.Add(d); c.Add(col);
                    }
                }
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    int a0 = b0 + i * sides, a1 = b0 + (i + 1) * sides;
                    for (int s = 0; s < sides; s++)
                    {
                        int nx = (s + 1) % sides;
                        t.Add(a0 + s); t.Add(a1 + nx); t.Add(a1 + s);
                        t.Add(a0 + s); t.Add(a0 + nx); t.Add(a1 + nx);
                    }
                }
                int c0 = v.Count; v.Add(pts[0]); n.Add(-t0); c.Add(colAt(0));
                int c1 = v.Count; v.Add(pts[pts.Count - 1]); n.Add(tl); c.Add(colAt(pts.Count - 1));
                int last = b0 + (pts.Count - 1) * sides;
                for (int s = 0; s < sides; s++)
                {
                    int nx = (s + 1) % sides;
                    t.Add(c0); t.Add(b0 + nx); t.Add(b0 + s);
                    t.Add(c1); t.Add(last + s); t.Add(last + nx);
                }
            }

            /// <summary>A closed ring of tube (orbits, the pelvic inlet, tracheal rings).</summary>
            public void Loop(Vector3 centre, Vector3 axisA, Vector3 axisB, float r, Color col, int pts = 18, int sides = 6)
            {
                var p = new List<Vector3>();
                for (int i = 0; i <= pts; i++) { float a = i / (float)pts * Mathf.PI * 2f; p.Add(centre + axisA * Mathf.Cos(a) + axisB * Mathf.Sin(a)); }
                Tube(p, r, col, sides);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Catmull-Rom through the control points, sampled per segment.</summary>
        static List<Vector3> Spline(IList<Vector3> c, int per)
        {
            var o = new List<Vector3>();
            for (int i = 0; i < c.Count - 1; i++)
            {
                Vector3 p0 = c[Mathf.Max(0, i - 1)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(c.Count - 1, i + 2)];
                for (int k = 0; k < per; k++)
                {
                    float t = k / (float)per, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(c[c.Count - 1]);
            return o;
        }

        static Vector3 Mul(Vector3 a, Vector3 b) => Vector3.Scale(a, b);
        static string K(Vector3 v) => $"{v.x:0.0000}_{v.y:0.0000}_{v.z:0.0000}";

        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = make();
            cache[key] = m;
            return m;
        }

        /// <summary>The small bowel: one long tube looping back and forth in layers to fill the belly.</summary>
        public static Mesh Intestine(Vector3 radii) => Cached($"gut{K(radii)}", () =>
        {
            var mb = new MB();
            var pts = new List<Vector3>();
            const int layers = 3, rows = 4, per = 22;
            int row = 0;
            for (int L = 0; L < layers; L++)
            {
                float z = Mathf.Lerp(-0.5f, 0.5f, L / (float)(layers - 1)) * radii.z;
                for (int R0 = 0; R0 < rows; R0++, row++)
                {
                    int R = L % 2 == 0 ? R0 : rows - 1 - R0;
                    float y = Mathf.Lerp(0.55f, -0.55f, R / (float)(rows - 1)) * radii.y;
                    for (int k = 0; k < per; k++)
                    {
                        float u = k / (float)(per - 1);
                        if (row % 2 == 1) u = 1f - u;
                        var p = new Vector3(Mathf.Lerp(-0.78f, 0.78f, u) * radii.x,
                            y + Mathf.Sin(u * 23f + R * 1.7f + L) * radii.y * 0.13f,
                            z + Mathf.Cos(u * 17f + R) * radii.z * 0.12f);
                        float q = CarlMath.Div(p, radii).magnitude;
                        if (q > 0.85f) p *= 0.85f / q;
                        pts.Add(p);
                    }
                }
            }
            float r = Mathf.Min(0.011f, Mathf.Min(radii.y, radii.z) * 0.2f);
            mb.Tube(pts, i => r * (0.92f + 0.08f * Mathf.Sin(i * 1.9f)), i => Shade(0.8f + 0.2f * Mathf.Sin(i * 0.9f)), 7);
            return mb.Build("SmallIntestine");
        });

        /// <summary>The large bowel framing the gut: caecum and appendix, up, across, down, the sigmoid loop and rectum, puckered into haustra.</summary>
        public static Mesh Colon(Vector3 radii) => Cached($"colon{K(radii)}", () =>
        {
            var mb = new MB();
            var ctrl = new[]
            {
                new Vector3(0.62f, -0.55f, 0.1f), new Vector3(0.72f, -0.1f, 0.2f), new Vector3(0.74f, 0.55f, 0.25f),
                new Vector3(0.45f, 0.72f, 0.55f), new Vector3(0f, 0.7f, 0.7f), new Vector3(-0.45f, 0.72f, 0.55f),
                new Vector3(-0.74f, 0.55f, 0.25f), new Vector3(-0.74f, -0.1f, 0.2f), new Vector3(-0.6f, -0.6f, 0.2f),
                new Vector3(-0.25f, -0.7f, 0.45f), new Vector3(0.05f, -0.55f, 0.1f), new Vector3(0.02f, -0.75f, -0.45f),
            };
            for (int i = 0; i < ctrl.Length; i++) ctrl[i] = Mul(ctrl[i], radii);
            var pts = Spline(ctrl, 9);
            float r = Mathf.Min(radii.y, radii.z) * 0.42f;
            int n = pts.Count;
            mb.Tube(pts, i => r * (1f + 0.16f * Mathf.Abs(Mathf.Sin(i * 1.3f))) * (i > n - 10 ? 0.8f : 1f),
                i => Shade(0.82f + 0.18f * Mathf.Abs(Mathf.Sin(i * 1.3f))), 9);
            // appendix
            mb.Tube(new[] { ctrl[0], ctrl[0] + new Vector3(-0.012f, -0.02f, 0.006f), ctrl[0] + new Vector3(-0.02f, -0.03f, 0.014f) }, r * 0.2f, Shade(0.9f), 6);
            return mb.Build("Colon");
        });

        /// <summary>J-shaped stomach from the oesophagus down to the pylorus, rugae as shading, and the first bend of the duodenum.</summary>
        public static Mesh Stomach(Vector3 radii) => Cached($"stomach{K(radii)}", () =>
        {
            var mb = new MB();
            var ctrl = new[]
            {
                new Vector3(-0.45f, 1.05f, -0.1f), new Vector3(-0.55f, 0.55f, 0f), new Vector3(-0.5f, 0f, 0.15f),
                new Vector3(-0.2f, -0.55f, 0.25f), new Vector3(0.3f, -0.6f, 0.25f), new Vector3(0.75f, -0.25f, 0.15f), new Vector3(0.9f, 0.15f, 0.05f),
            };
            float[] cr = { 0.007f, 0.03f, 0.033f, 0.03f, 0.022f, 0.014f, 0.008f };
            float sc = Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z)) / 0.04f;
            for (int i = 0; i < ctrl.Length; i++) ctrl[i] = Mul(ctrl[i], radii);
            const int per = 8;
            var pts = Spline(ctrl, per);
            mb.Tube(pts, i =>
            {
                float f = i / (float)per; int a = Mathf.Min(cr.Length - 1, (int)f), b = Mathf.Min(cr.Length - 1, a + 1);
                return Mathf.Lerp(cr[a], cr[b], f - a) * sc;
            }, i => Shade(0.84f + 0.16f * Mathf.Sin(i * 2.1f)), 12);
            // duodenum
            var d = new[] { ctrl[6], Mul(new Vector3(1.15f, -0.2f, 0.05f), radii), Mul(new Vector3(1.0f, -0.75f, 0.1f), radii), Mul(new Vector3(0.6f, -0.95f, 0.1f), radii) };
            mb.Tube(Spline(d, 5), 0.009f * sc, Shade(0.92f), 8);
            return mb.Build("Stomach");
        });

        /// <summary>The heart: tapered to a left-down-forward apex, atria darker, with the aortic arch and its branches, pulmonary trunk, venae cavae and the coronary arteries on its surface.</summary>
        public static Mesh Heart(Vector3 radii) => Cached($"heart{K(radii)}", () =>
        {
            var mb = new MB();
            var src = ProcMesh.Ellipsoid(radii, 28, 20);
            Vector3 apex = new Vector3(-0.45f, -0.75f, 0.5f).normalized;
            float size = (radii.x + radii.y + radii.z) / 3f;
            Vector3 Deform(Vector3 p)
            {
                float along = Vector3.Dot(p, apex);
                float proj = along / size;
                if (proj <= 0f) return p;
                Vector3 perp = p - apex * along;
                return apex * along * 1.12f + perp * (1f - 0.5f * proj * proj);
            }
            var v = src.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = Deform(v[i]);
            src.vertices = v; src.RecalculateNormals();
            mb.Add(src, Matrix4x4.identity, Own, p => p.y > radii.y * 0.4f ? Shade(0.78f) : Shade(1f));

            float s = radii.y / 0.055f;
            Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * s;
            // aortic arch and the three vessels off its top
            var arch = Spline(new[] { S(0.004f, 0.03f, 0f), S(0.01f, 0.075f, 0.005f), S(0f, 0.095f, -0.02f), S(-0.012f, 0.08f, -0.045f), S(-0.015f, 0.04f, -0.05f) }, 5);
            mb.Tube(arch, 0.011f * s, ArteryC, 10);
            foreach (var b in new[] { S(0.008f, 0.09f, -0.005f), S(0f, 0.097f, -0.018f), S(-0.008f, 0.093f, -0.03f) })
                mb.Tube(new[] { b, b + S(0.002f, 0.03f, 0.002f) }, 0.004f * s, ArteryC, 6);
            // pulmonary trunk splitting to both lungs
            var pt = new[] { S(-0.012f, 0.025f, 0.03f), S(-0.02f, 0.06f, 0.025f), S(-0.025f, 0.075f, 0.005f) };
            mb.Tube(Spline(pt, 4), 0.009f * s, PulmonaryC, 8);
            mb.Tube(Spline(new[] { pt[2], S(-0.045f, 0.075f, -0.005f), S(-0.065f, 0.068f, -0.012f) }, 3), 0.0065f * s, PulmonaryC, 7);
            mb.Tube(Spline(new[] { pt[2], S(0.0f, 0.078f, -0.012f), S(0.04f, 0.072f, -0.015f) }, 3), 0.0065f * s, PulmonaryC, 7);
            // venae cavae
            mb.Tube(new[] { S(0.035f, 0.02f, 0f), S(0.036f, 0.06f, 0f), S(0.035f, 0.095f, 0f) }, 0.008f * s, VeinC, 8);
            mb.Tube(new[] { S(0.03f, -0.04f, -0.01f), S(0.035f, -0.075f, -0.015f) }, 0.009f * s, VeinC, 8);
            // coronary arteries running over the surface
            Vector3 Surf(Vector3 dir) { dir.Normalize(); return Deform(new Vector3(dir.x * radii.x, dir.y * radii.y, dir.z * radii.z) * 1.04f); }
            void Coronary(Vector3 from, Vector3 to, Vector3 via, float r)
            {
                var p = new List<Vector3>();
                for (int i = 0; i <= 12; i++)
                {
                    float t = i / 12f;
                    Vector3 d = Vector3.Lerp(Vector3.Lerp(from, via, t), Vector3.Lerp(via, to, t), t);
                    p.Add(Surf(d));
                }
                mb.Tube(p, r, ArteryC, 5);
            }
            Coronary(new Vector3(-0.1f, 0.5f, 1f), apex, new Vector3(-0.3f, 0f, 1f), 0.0022f * s);           // LAD
            Coronary(new Vector3(0.5f, 0.4f, 0.8f), new Vector3(0.4f, -0.4f, -0.8f), new Vector3(1f, 0f, 0f), 0.002f * s);   // right coronary
            Coronary(new Vector3(-0.4f, 0.45f, 0.8f), new Vector3(-0.5f, 0f, -0.85f), new Vector3(-1f, 0.2f, 0f), 0.002f * s); // circumflex
            return mb.Build("Heart");
        });

        /// <summary>A lung: flat against the heart, a cardiac notch on the left, the oblique fissure between lobes, and the bronchial tree with its arteries branching inside.</summary>
        public static Mesh Lung(Vector3 radii, int side, int seed) => Cached($"lung{K(radii)}_{side}_{seed}", () =>
        {
            var mb = new MB();
            var src = Object.Instantiate(Organ(radii, seed));
            float medial = -Mathf.Sign(side == 0 ? 1 : side);
            var v = src.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                var p = v[i];
                if (p.x * medial > 0f)
                {
                    p.x *= 0.55f;
                    if (side < 0 && p.y < 0f && p.z > 0f) p.x *= 0.6f;     // room for the heart
                }
                v[i] = p;
            }
            src.vertices = v; src.RecalculateNormals();
            Vector3 fissure = new Vector3(0f, 0.75f, 0.66f).normalized;
            mb.Add(src, Matrix4x4.identity, Own, p =>
            {
                float d = Vector3.Dot(p, fissure) / radii.y;
                return Mathf.Abs(d) < 0.05f ? Shade(0.55f) : d > 0f ? Shade(1f) : Shade(0.9f);
            });

            var rng = new System.Random(seed * 31 + side);
            float Rand() => (float)rng.NextDouble();
            void Branch(Vector3 p, Vector3 dir, float len, float r, int depth)
            {
                Vector3 end = p + dir * len;
                float q = CarlMath.Div(end, radii).magnitude;
                if (q > 0.86f) end *= 0.86f / q;
                Vector3 side2 = Vector3.Cross(dir, Vector3.forward).normalized * r * 1.7f;
                mb.Tube(new[] { p, Vector3.Lerp(p, end, 0.5f), end }, r, BronchusC, 6);
                mb.Tube(new[] { p + side2, end + side2 }, r * 0.6f, PulmonaryC, 5);
                if (depth == 0) return;
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 axis = Vector3.Cross(dir, new Vector3(Rand() - 0.5f, Rand() - 0.5f, Rand() - 0.5f)).normalized;
                    if (axis.sqrMagnitude < 0.01f) axis = Vector3.Cross(dir, Vector3.right).normalized;
                    Vector3 nd = Quaternion.AngleAxis(k * Mathf.Lerp(24f, 42f, Rand()), axis) * dir;
                    Branch(end, nd, len * 0.72f, r * 0.72f, depth - 1);
                }
            }
            Vector3 root = new Vector3(medial * radii.x * 0.5f, radii.y * 0.45f, 0f);
            Branch(root, new Vector3(-medial * 0.45f, -0.85f, 0.1f).normalized, radii.y * 0.42f, 0.0062f, 4);
            return mb.Build("Lung");
        });

        /// <summary>Liver: a thick right lobe tapering to a thin left one, flat underneath, with the gallbladder, portal vein and hepatic veins.</summary>
        public static Mesh Liver(Vector3 radii) => Cached($"liver{K(radii)}", () =>
        {
            var mb = new MB();
            var src = ProcMesh.Ellipsoid(radii, 28, 20);
            var v = src.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                var p = v[i];
                float u = Mathf.SmoothStep(0f, 1f, (p.x / radii.x + 1f) * 0.5f);
                p.y *= Mathf.Lerp(0.3f, 1.15f, u);
                if (p.y < 0f) p.y *= 0.75f;
                p.z *= Mathf.Lerp(0.6f, 1.05f, u);
                v[i] = p;
            }
            src.vertices = v; src.RecalculateNormals();
            mb.Add(src, Matrix4x4.identity, Own, p =>
            {
                float u = (p.x / radii.x + 1f) * 0.5f;
                return Mathf.Abs(u - 0.38f) < 0.025f ? Shade(0.7f) : Shade(0.92f + 0.08f * Mathf.PerlinNoise(p.x * 60f, p.z * 60f));
            });
            mb.Blob(Mul(new Vector3(0.25f, -0.5f, 0.55f), radii), new Vector3(0.012f, 0.022f, 0.012f), Quaternion.Euler(0f, 0f, 30f), new Color(0.3f, 0.62f, 0.25f, 1f));
            mb.Tube(new[] { Mul(new Vector3(-0.15f, -0.4f, -0.3f), radii), Mul(new Vector3(-0.45f, -1.1f, -0.5f), radii) }, 0.006f, PulmonaryC, 7);
            mb.Tube(new[] { Mul(new Vector3(0f, 0.3f, -0.4f), radii), Mul(new Vector3(-0.1f, 1.05f, -0.55f), radii) }, 0.005f, VeinC, 7);
            return mb.Build("Liver");
        });

        /// <summary>Bean-shaped kidney with its hilum facing the spine, renal artery and vein, ureter and the adrenal gland on top.</summary>
        public static Mesh Kidney(Vector3 radii, int side) => Cached($"kidney{K(radii)}_{side}", () =>
        {
            var mb = new MB();
            var src = ProcMesh.Ellipsoid(radii, 20, 14);
            Vector3 m = new Vector3(-(side == 0 ? 1 : side), 0f, 0f);
            var v = src.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                var p = v[i];
                float d = Vector3.Dot(CarlMath.Div(p, radii).normalized, m);
                float band = 1f - Mathf.Abs(p.y) / (radii.y * 0.45f);
                if (d > 0.55f && band > 0f) p -= m * radii.x * 0.45f * ((d - 0.55f) / 0.45f) * band;
                v[i] = p;
            }
            src.vertices = v; src.RecalculateNormals();
            mb.Add(src, Matrix4x4.identity, Own, p => Vector3.Dot(p, m) > radii.x * 0.2f ? Shade(0.72f) : Shade(1f));
            Vector3 h = m * radii.x * 0.5f;
            mb.Tube(new[] { h + new Vector3(0f, 0.004f, 0f), h + m * 0.02f + new Vector3(0f, 0.008f, 0f), h + m * 0.035f + new Vector3(0f, 0.01f, 0f) }, 0.0035f, ArteryC, 6);
            mb.Tube(new[] { h + new Vector3(0f, -0.003f, 0.004f), h + m * 0.035f + new Vector3(0f, -0.001f, 0.006f) }, 0.004f, VeinC, 6);
            mb.Tube(Spline(new[] { h + new Vector3(0f, -0.008f, 0f), h + m * 0.01f + new Vector3(0f, -0.035f, 0.003f), m * 0.012f + new Vector3(0f, -0.075f, 0.006f) }, 4), 0.0025f, new Color(0.95f, 0.9f, 0.6f, 1f), 5);
            mb.Blob(new Vector3(0f, radii.y * 1.02f, 0f) - m * 0.003f, new Vector3(0.013f, 0.008f, 0.007f), new Color(0.95f, 0.75f, 0.3f, 1f));
            return mb.Build("Kidney");
        });

        /// <summary>Pancreas: a lobular tongue from its head in the duodenum's curve to its tail at the spleen.</summary>
        public static Mesh Pancreas(Vector3 radii) => Cached($"pancreas{K(radii)}", () =>
        {
            var mb = new MB();
            var ctrl = new[] { new Vector3(0.85f, -0.2f, 0.3f), new Vector3(0.4f, 0.1f, 0.1f), new Vector3(0f, 0.2f, 0f), new Vector3(-0.5f, 0.4f, -0.2f), new Vector3(-0.9f, 0.6f, -0.4f) };
            for (int i = 0; i < ctrl.Length; i++) ctrl[i] = Mul(ctrl[i], radii);
            var pts = Spline(ctrl, 6);
            float sc = radii.z / 0.015f;
            mb.Tube(pts, i => Mathf.Lerp(0.013f, 0.006f, i / (float)(pts.Count - 1)) * sc * (0.92f + 0.08f * Mathf.Sin(i * 2.7f)),
                i => Shade(0.86f + 0.14f * Mathf.Sin(i * 2.7f)), 9);
            return mb.Build("Pancreas");
        });

        /// <summary>
        /// A stretch of spine along local Y: vertebral bodies in front, discs between them, spinous and transverse
        /// processes behind and to the sides, the cord in its canal and a pair of nerve roots at every level.
        /// front: which local direction is the body's front.
        /// </summary>
        public static Mesh Spine(float halfLen, float r, Vector3 front) => Cached($"spine{halfLen:0.000}_{r:0.000}_{front.x:0.0}{front.z:0.0}", () =>
        {
            var mb = new MB();
            front = Vector3.ProjectOnPlane(front, Vector3.up).normalized;
            if (front.sqrMagnitude < 0.5f) front = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, front);
            var rot = Quaternion.LookRotation(front, Vector3.up);
            int count = Mathf.Max(2, Mathf.RoundToInt(2f * halfLen / 0.026f));
            float step = 2f * halfLen / count;
            for (int k = 0; k < count; k++)
            {
                float y = -halfLen + step * (k + 0.5f);
                Vector3 Y = Vector3.up * y;
                mb.Blob(Y + front * r * 0.9f, new Vector3(r * 1.05f, step * 0.36f, r * 0.85f), rot, BoneC, 12, 7);
                if (k < count - 1) mb.Blob(Y + Vector3.up * step * 0.5f + front * r * 0.9f, new Vector3(r * 0.95f, step * 0.1f, r * 0.75f), rot, CartilageC, 10, 5);
                // the arch round the canal, then the spinous process sticking out the back
                mb.Loop(Y - front * r * 0.05f, side * r * 0.62f, front * r * 0.55f, r * 0.16f, BoneC, 12, 5);
                mb.Tube(new[] { Y - front * r * 0.55f, Y - front * r * 1.7f - Vector3.up * step * 0.25f }, r * 0.22f, BoneC, 6);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    mb.Tube(new[] { Y - front * r * 0.2f, Y + side * sd * r * 1.55f - front * r * 0.35f }, r * 0.18f, BoneC, 5);
                    mb.Tube(new[] { Y - front * r * 0.05f, Y + side * sd * r * 1.3f - Vector3.up * step * 0.2f + front * r * 0.1f }, r * 0.08f, CordC, 4);
                }
            }
            float ext = halfLen + r * 0.3f;
            mb.Tube(new[] { Vector3.down * ext - front * r * 0.05f, Vector3.up * ext - front * r * 0.05f }, r * 0.42f, CordC, 8);
            return mb.Build("Spine");
        });

        /// <summary>Trachea: the tube and its C-shaped cartilage rings.</summary>
        public static Mesh Trachea(float halfLen, float r) => Cached($"trachea{halfLen:0.000}_{r:0.000}", () =>
        {
            var mb = new MB();
            mb.Tube(new[] { Vector3.down * (halfLen + r * 0.5f), Vector3.up * (halfLen + r * 0.5f) }, r * 0.92f, Own, 10);
            for (float y = -halfLen; y <= halfLen; y += 0.011f)
                mb.Loop(Vector3.up * y, Vector3.right * r, Vector3.forward * r, r * 0.16f, CartilageC, 14, 4);
            return mb.Build("Trachea");
        });

        /// <summary>A vessel with a slight meander, a gentle taper and a couple of side branches.</summary>
        public static Mesh Vessel(float halfLen, float r, int seed) => Cached($"vessel{halfLen:0.000}_{r:0.0000}_{seed}", () =>
        {
            var mb = new MB();
            var pts = new List<Vector3>();
            float ext = halfLen + r * 0.5f;
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f, y = Mathf.Lerp(-ext, ext, t);
                float w = Mathf.Sin(t * Mathf.PI) * r * 0.6f;
                pts.Add(new Vector3(Mathf.Sin(t * 7f + seed) * w, y, Mathf.Cos(t * 5f + seed * 0.7f) * w));
            }
            mb.Tube(pts, i => r * Mathf.Lerp(1f, 0.85f, i / 10f), i => Own, 8);
            if (halfLen > 0.06f)
                for (int k = 0; k < 2; k++)
                {
                    float t = k == 0 ? 0.3f : 0.68f;
                    Vector3 b = pts[Mathf.RoundToInt(t * 10f)];
                    float a = seed * 1.3f + k * 2.4f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), k == 0 ? -0.6f : 0.6f, Mathf.Sin(a)).normalized;
                    float len = Mathf.Min(0.045f, halfLen * 0.4f);
                    mb.Tube(new[] { b, b + dir * len * 0.5f + Vector3.up * len * 0.1f, b + dir * len }, r * 0.45f, Own, 6);
                }
            return mb.Build("Vessel");
        });

        /// <summary>Hand skeleton: carpals, metacarpals and finger bones down local -Y, spread across Z, thumb to +Z.</summary>
        public static Mesh Hand(Vector3 radii) => Cached($"hand{K(radii)}", () =>
        {
            var mb = new MB();
            Vector3 S(float x, float y, float z) => new Vector3(x * radii.x / 0.012f, y * radii.y / 0.06f, z * radii.z / 0.035f);
            for (int i = 0; i < 8; i++)
                mb.Blob(S(i % 2 == 0 ? 0.002f : -0.002f, 0.042f - (i / 4) * 0.009f, Mathf.Lerp(-0.016f, 0.016f, (i % 4) / 3f)), new Vector3(0.0045f, 0.004f, 0.0045f), BoneC, 8, 6);
            float[] fingerLen = { 0.024f, 0.027f, 0.025f, 0.02f };
            for (int f = 0; f < 4; f++)
            {
                float z = Mathf.Lerp(0.021f, -0.021f, f / 3f);
                Vector3 a = S(0f, 0.032f, z * 0.8f), b = S(0f, -0.025f, z);
                mb.Tube(new[] { a, b }, 0.0032f, BoneC, 6);
                mb.Blob(b, Vector3.one * 0.0043f, BoneC, 8, 6);
                Vector3 p = b, dir = (b - a).normalized;
                float len = fingerLen[f];
                for (int seg = 0; seg < 3; seg++)
                {
                    Vector3 q = p + dir * (len + 0.002f);
                    mb.Tube(new[] { p + dir * 0.003f, q }, Mathf.Lerp(0.003f, 0.0022f, seg / 2f), BoneC, 6);
                    if (seg < 2) mb.Blob(q, Vector3.one * 0.0033f, BoneC, 7, 5);
                    p = q; len *= 0.68f;
                }
            }
            // thumb
            Vector3 ta = S(0.004f, 0.03f, 0.026f), tb = S(0.012f, 0.002f, 0.04f);
            mb.Tube(new[] { ta, tb }, 0.0036f, BoneC, 6);
            Vector3 td = (tb - ta).normalized, tp = tb;
            for (int seg = 0; seg < 2; seg++)
            {
                Vector3 q = tp + td * (seg == 0 ? 0.022f : 0.016f);
                mb.Blob(tp, Vector3.one * 0.0042f, BoneC, 7, 5);
                mb.Tube(new[] { tp + td * 0.003f, q }, 0.003f, BoneC, 6);
                tp = q;
            }
            return mb.Build("HandBones");
        });

        /// <summary>Foot skeleton along local +Z: heel, talus, the tarsal row, five metatarsals and the toes, big toe on the inside.</summary>
        public static Mesh Foot(Vector3 radii, int side) => Cached($"foot{K(radii)}_{side}", () =>
        {
            var mb = new MB();
            float mx = -(side == 0 ? 1 : side);
            Vector3 S(float x, float y, float z) => new Vector3(x * radii.x / 0.03f, y * radii.y / 0.025f, z * radii.z / 0.11f);
            mb.Blob(S(0f, -0.008f, -0.075f), new Vector3(0.016f, 0.018f, 0.028f), BoneC, 12, 8);
            mb.Blob(S(0f, 0.012f, -0.045f), new Vector3(0.015f, 0.012f, 0.018f), BoneC, 10, 7);
            for (int i = 0; i < 4; i++) mb.Blob(S(Mathf.Lerp(0.018f, -0.018f, i / 3f) * mx, 0.004f, -0.02f), new Vector3(0.0065f, 0.009f, 0.009f), BoneC, 8, 6);
            for (int t = 0; t < 5; t++)
            {
                float x = Mathf.Lerp(0.02f, -0.022f, t / 4f) * mx;
                bool big = t == 0;
                Vector3 a = S(x * 0.7f, 0.004f, -0.01f), b = S(x, -0.012f, 0.05f - t * 0.004f);
                mb.Tube(new[] { a, b }, big ? 0.0055f : 0.0038f, BoneC, 6);
                mb.Blob(b, Vector3.one * (big ? 0.0065f : 0.0045f), BoneC, 8, 6);
                Vector3 p = b, dir = new Vector3(0f, -0.1f, 1f).normalized;
                int segs = big ? 2 : 3;
                float len = big ? 0.02f : 0.012f;
                for (int s = 0; s < segs; s++)
                {
                    Vector3 q = p + dir * len;
                    mb.Tube(new[] { p + dir * 0.002f, q }, big ? 0.0045f : 0.003f, BoneC, 6);
                    p = q; len *= 0.75f;
                }
            }
            return mb.Build("FootBones");
        });

        /// <summary>The bony pelvis: flaring iliac wings, the pelvic ring, the hip sockets, the obturator holes and the sit bones.</summary>
        public static Mesh Pelvis(Vector3 radii) => Cached($"pelvis{K(radii)}", () =>
        {
            var mb = new MB();
            float s = radii.x / 0.157f;
            Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * s;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                mb.Blob(S(sd * 0.095f, 0.035f, -0.015f), new Vector3(0.011f, 0.055f, 0.07f) * s, Quaternion.Euler(0f, -sd * 40f, sd * 15f), BoneC, 16, 10);
                // iliac crest along the top of the wing
                var crest = new List<Vector3>();
                for (int i = 0; i <= 8; i++) { float a = Mathf.Lerp(-1.2f, 1.2f, i / 8f); crest.Add(S(sd * (0.095f + Mathf.Cos(a) * 0.02f), 0.085f - a * a * 0.01f, -0.015f + Mathf.Sin(a) * 0.06f)); }
                mb.Tube(crest, 0.006f * s, BoneC, 6);
                mb.Loop(S(sd * 0.1f, -0.04f, 0.01f), S(0f, 0.018f, 0f), S(0f, 0f, 0.018f), 0.0045f * s, BoneC, 14, 6);
                mb.Loop(S(sd * 0.055f, -0.062f, 0.04f), S(sd * 0.008f, 0.015f, 0f), S(0f, 0f, 0.02f), 0.006f * s, BoneC, 14, 6);
                mb.Blob(S(sd * 0.06f, -0.085f, -0.01f), new Vector3(0.014f, 0.016f, 0.018f) * s, BoneC, 10, 7);
            }
            mb.Loop(S(0f, -0.03f, 0.005f), S(0.085f, 0f, 0f), S(0f, -0.012f, 0.065f), 0.011f * s, BoneC, 24, 7);
            mb.Blob(S(0f, -0.055f, 0.066f), new Vector3(0.006f, 0.012f, 0.006f) * s, CartilageC, 8, 6);
            return mb.Build("Pelvis");
        });

        /// <summary>Skull shell plus the eye sockets, cheekbones, nasal opening, upper teeth and the hole the spinal cord leaves through.</summary>
        public static Mesh Skull(Vector3 radii) => Cached($"skull{K(radii)}", () =>
        {
            var mb = new MB();
            mb.Add(ProcMesh.Ellipsoid(radii, 28, 20), Matrix4x4.identity, Own);
            float s = radii.y / 0.1026f;
            Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * s;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                mb.Loop(S(sd * 0.032f, 0.007f, 0.083f), S(0.019f, 0f, 0f), S(0f, 0.017f, 0f), 0.0035f * s, BoneC, 16, 5);
                mb.Tube(Spline(new[] { S(sd * 0.05f, -0.008f, 0.072f), S(sd * 0.072f, -0.014f, 0.035f), S(sd * 0.076f, -0.02f, 0f) }, 4), 0.005f * s, BoneC, 6);
            }
            mb.Loop(S(0f, -0.024f, 0.094f), S(0.01f, 0f, 0f), S(0f, 0.013f, 0f), 0.0028f * s, BoneC, 12, 5);
            mb.Loop(S(0f, -0.095f, -0.015f), S(0.016f, 0f, 0f), S(0f, 0f, 0.019f), 0.004f * s, BoneC, 14, 5);
            Teeth(mb, S(0f, -0.057f, 0.05f), 0.036f * s, 0.03f * s, -1f, s);
            return mb.Build("Skull");
        });

        /// <summary>The mandible: a U of bone with the rami rising to the jaw joints, and the lower teeth.</summary>
        public static Mesh Jaw(Vector3 radii) => Cached($"jaw{K(radii)}", () =>
        {
            var mb = new MB();
            float s = radii.x / 0.05f;
            var arc = new List<Vector3>();
            for (int i = 0; i <= 14; i++)
            {
                float a = Mathf.Lerp(-1.35f, 1.35f, i / 14f);
                arc.Add(new Vector3(Mathf.Sin(a) * 0.042f, -0.004f + 0.006f * Mathf.Cos(a), Mathf.Cos(a) * 0.034f - 0.006f) * s);
            }
            mb.Tube(arc, 0.0075f * s, BoneC, 7);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector3 end = arc[sd < 0 ? 0 : arc.Count - 1];
                var ramus = new[] { end, end + new Vector3(sd * 0.003f, 0.03f, -0.014f) * s, end + new Vector3(sd * 0.002f, 0.045f, -0.01f) * s };
                mb.Tube(Spline(ramus, 3), 0.006f * s, BoneC, 6);
                mb.Blob(ramus[2], new Vector3(0.006f, 0.004f, 0.005f) * s, BoneC, 8, 6);
            }
            Teeth(mb, new Vector3(0f, 0.009f, -0.006f) * s, 0.039f * s, 0.032f * s, 1f, s);
            return mb.Build("Jaw");
        });

        /// <summary>A row of teeth round an arc (rx wide, rz deep); up = +1 for lower teeth (crowns up), -1 for upper.</summary>
        static void Teeth(MB mb, Vector3 centre, float rx, float rz, float up, float s)
        {
            for (int i = 0; i < 14; i++)
            {
                float a = Mathf.Lerp(-1.15f, 1.15f, i / 13f);
                Vector3 outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 p = centre + new Vector3(Mathf.Sin(a) * rx, 0f, Mathf.Cos(a) * rz);
                float back = Mathf.Abs(a) / 1.15f;           // molars at the back are wider
                var r = new Vector3(Mathf.Lerp(0.0028f, 0.0042f, back), 0.0055f, Mathf.Lerp(0.0022f, 0.004f, back)) * s;
                mb.Blob(p + Vector3.up * up * 0.002f * s, r, Quaternion.LookRotation(outward, Vector3.up), ToothC, 8, 6);
            }
        }

        /// <summary>Ribs from the spine round to the front, with the costal cartilage joining them to the sternum.</summary>
        public static Mesh Ribcage() => Cached("ribcage", () =>
        {
            var mb = new MB();
            var ends = new Vector3[9, 2];
            for (int k = 0; k < 9; k++)
            {
                float y = 0.11f - k * 0.025f;
                float rx = 0.172f * Mathf.Sqrt(Mathf.Max(0.2f, 1f - (y / 0.16f) * (y / 0.16f) * 0.55f));
                for (int s = -1; s <= 1; s += 2)
                {
                    var pts = new List<Vector3>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float t = i / 10f;
                        float phi = Mathf.Lerp(0.1f, 0.86f, t) * Mathf.PI;
                        pts.Add(new Vector3(s * rx * Mathf.Sin(phi), y - 0.035f * t, -0.105f * Mathf.Cos(phi)));
                    }
                    // ribs are flatter and wider towards the front
                    mb.Tube(pts, i => Mathf.Lerp(0.0045f, 0.0062f, i / 10f), i => BoneC, 6);
                    ends[k, s < 0 ? 0 : 1] = pts[pts.Count - 1];
                }
            }
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 9; k++)
                {
                    Vector3 e = ends[k, s < 0 ? 0 : 1];
                    // true ribs reach the sternum; the lower ones join the cartilage above
                    Vector3 to = k < 7 ? new Vector3(s * 0.014f, e.y + 0.012f + k * 0.002f, 0.108f) : ends[6, s < 0 ? 0 : 1] + new Vector3(-s * 0.012f, -0.01f, 0.003f);
                    Vector3 mid = Vector3.Lerp(e, to, 0.5f) + new Vector3(0f, -0.006f, 0.006f);
                    mb.Tube(new[] { e, mid, to }, 0.0042f, CartilageC, 5);
                }
            return mb.Build("Ribcage");
        });
    }
}
