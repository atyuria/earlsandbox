using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>Runtime material factory. Everything in the sandbox is built from code, so materials live here.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader urpLit;

        public static Shader Find(string name)
        {
            var s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogError($"[Carl] Shader not found: {name}");
                s = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return s;
        }

        public static Material Lit(Color c, float smoothness = 0.2f, float metallic = 0f)
        {
            string key = $"lit{c}{smoothness}{metallic}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            urpLit ??= Find("Universal Render Pipeline/Lit");
            m = new Material(urpLit);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            cache[key] = m;
            return m;
        }

        public static Material LitTextured(Texture tex, Vector2 tiling, Color tint, float smoothness = 0.2f)
        {
            urpLit ??= Find("Universal Render Pipeline/Lit");
            var m = new Material(urpLit);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        public static Material Emissive(Color c, float intensity)
        {
            string key = $"emi{c}{intensity}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(Lit(c, 0.5f));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            cache[key] = m;
            return m;
        }

        public static Material Unlit(Color c, bool additive = false)
        {
            string key = $"unlit{c}{additive}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(Find("Carl/UnlitColor"));
            m.SetColor("_Color", c);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.renderQueue = 3000;
            cache[key] = m;
            return m;
        }

        public static Material SoftParticle(bool additive = false)
        {
            string key = $"soft{additive}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(Find("Carl/SoftParticle"));
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.renderQueue = 3000;
            cache[key] = m;
            return m;
        }
    }

    /// <summary>Procedural meshes used for bodies, organs and props.</summary>
    public static class ProcMesh
    {
        /// <summary>Builds a closed lathe: rings stacked along +Y (x = radius X, y = height, z = radius Z) with pointed tips.</summary>
        static Mesh Lathe(List<Vector3> rings, float bottomTipY, float topTipY, int seg, string name)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            verts.Add(new Vector3(0, bottomTipY, 0));
            for (int r = 0; r < rings.Count; r++)
            {
                for (int s = 0; s < seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * rings[r].x, rings[r].y, Mathf.Sin(a) * rings[r].z));
                }
            }
            int top = verts.Count;
            verts.Add(new Vector3(0, topTipY, 0));

            for (int s = 0; s < seg; s++)
            {
                int n = (s + 1) % seg;
                tris.Add(0); tris.Add(1 + s); tris.Add(1 + n);
            }
            for (int r = 0; r < rings.Count - 1; r++)
            {
                int a0 = 1 + r * seg, b0 = 1 + (r + 1) * seg;
                for (int s = 0; s < seg; s++)
                {
                    int n = (s + 1) % seg;
                    tris.Add(a0 + s); tris.Add(b0 + s); tris.Add(b0 + n);
                    tris.Add(a0 + s); tris.Add(b0 + n); tris.Add(a0 + n);
                }
            }
            int last = 1 + (rings.Count - 1) * seg;
            for (int s = 0; s < seg; s++)
            {
                int n = (s + 1) % seg;
                tris.Add(top); tris.Add(last + n); tris.Add(last + s);
            }

            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Capsule along local Y with an elliptical cross-section. halfHeight is centre-to-tip.</summary>
        public static Mesh Capsule(float rx, float rz, float halfHeight, int seg = 16, int capRings = 5, int bodyRings = 6)
        {
            float c = Mathf.Min(rx, rz);
            c = Mathf.Min(c, halfHeight);
            float cyl = Mathf.Max(0f, halfHeight - c);
            var rings = new List<Vector3>();
            for (int i = 1; i <= capRings; i++)
            {
                float phi = -90f + 90f * i / capRings;
                float cp = Mathf.Cos(phi * Mathf.Deg2Rad), sp = Mathf.Sin(phi * Mathf.Deg2Rad);
                rings.Add(new Vector3(rx * cp, -cyl + c * sp, rz * cp));
            }
            for (int i = 1; i < bodyRings; i++)
            {
                float y = Mathf.Lerp(-cyl, cyl, i / (float)bodyRings);
                rings.Add(new Vector3(rx, y, rz));
            }
            for (int i = 0; i < capRings; i++)
            {
                float phi = 90f * i / capRings;
                float cp = Mathf.Cos(phi * Mathf.Deg2Rad), sp = Mathf.Sin(phi * Mathf.Deg2Rad);
                rings.Add(new Vector3(rx * cp, cyl + c * sp, rz * cp));
            }
            return Lathe(rings, -halfHeight, halfHeight, seg, "Capsule");
        }

        public static Mesh Ellipsoid(Vector3 radii, int seg = 16, int rings = 12)
        {
            var list = new List<Vector3>();
            for (int i = 1; i < rings; i++)
            {
                float phi = -90f + 180f * i / rings;
                float cp = Mathf.Cos(phi * Mathf.Deg2Rad), sp = Mathf.Sin(phi * Mathf.Deg2Rad);
                list.Add(new Vector3(radii.x * cp, radii.y * sp, radii.z * cp));
            }
            return Lathe(list, -radii.y, radii.y, seg, "Ellipsoid");
        }

        /// <summary>Tube along a polyline (local space), used for vessels, ribs and the spinal cord.</summary>
        public static Mesh Tube(IList<Vector3> pts, float radius, int sides = 6)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 prevN = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 t = i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1];
                t.Normalize();
                Vector3 n;
                if (i == 0)
                {
                    n = Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                }
                else
                {
                    n = Vector3.ProjectOnPlane(prevN, t).normalized;
                    if (n.sqrMagnitude < 0.001f) n = Vector3.Cross(t, Vector3.right).normalized;
                }
                prevN = n;
                Vector3 b = Vector3.Cross(t, n);
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    verts.Add(pts[i] + (n * Mathf.Cos(a) + b * Mathf.Sin(a)) * radius);
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int a0 = i * sides, b0 = (i + 1) * sides;
                for (int s = 0; s < sides; s++)
                {
                    int n = (s + 1) % sides;
                    tris.Add(a0 + s); tris.Add(b0 + n); tris.Add(b0 + s);
                    tris.Add(a0 + s); tris.Add(a0 + n); tris.Add(b0 + n);
                }
            }
            int c0 = verts.Count; verts.Add(pts[0]);
            int c1 = verts.Count; verts.Add(pts[pts.Count - 1]);
            int lastRing = (pts.Count - 1) * sides;
            for (int s = 0; s < sides; s++)
            {
                int n = (s + 1) % sides;
                tris.Add(c0); tris.Add(n); tris.Add(s);
                tris.Add(c1); tris.Add(lastRing + s); tris.Add(lastRing + n);
            }
            var m = new Mesh { name = "Tube" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh quad;
        public static Mesh Quad
        {
            get
            {
                if (quad) return quad;
                quad = new Mesh { name = "CarlQuad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateNormals();
                return quad;
            }
        }
    }

    /// <summary>Procedural textures (grid floor, blood atlas).</summary>
    public static class TexGen
    {
        public static Texture2D Grid(Color bg, Color line, int size = 256, int lineWidth = 3)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool l = x < lineWidth || y < lineWidth;
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.03f;
                    px[y * size + x] = l ? line : bg * (0.985f + n);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>2x2 atlas: cells 0-2 are splats, cell 3 is a smear/streak.</summary>
        public static Texture2D BloodAtlas(int cell = 256)
        {
            int size = cell * 2;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            var rng = new System.Random(1337);
            for (int c = 0; c < 4; c++)
            {
                int ox = (c % 2) * cell, oy = (c / 2) * cell;
                // satellites for splats
                var sats = new List<Vector3>();
                if (c < 3)
                {
                    int count = 6 + c * 5;
                    for (int i = 0; i < count; i++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float d = 0.25f + (float)rng.NextDouble() * 0.22f;
                        sats.Add(new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, 0.012f + (float)rng.NextDouble() * 0.035f));
                    }
                }
                float seed = c * 17.3f;
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                    {
                        float u = x / (float)cell, v = y / (float)cell;
                        float a;
                        if (c < 3)
                        {
                            float dx = u - 0.5f, dy = v - 0.5f;
                            float ang = Mathf.Atan2(dy, dx);
                            float r = Mathf.Sqrt(dx * dx + dy * dy);
                            float edge = 0.2f + 0.07f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + seed, Mathf.Sin(ang) * 2f + seed)
                                         + 0.04f * Mathf.PerlinNoise(Mathf.Cos(ang) * 7f + seed, Mathf.Sin(ang) * 7f);
                            a = Mathf.Clamp01((edge - r) * 60f);
                            foreach (var s in sats)
                            {
                                float sd = Vector2.Distance(new Vector2(u, v), new Vector2(s.x, s.y));
                                a = Mathf.Max(a, Mathf.Clamp01((s.z - sd) * 220f));
                            }
                        }
                        else
                        {
                            // streak: long smeared band with drag lines
                            float dy = Mathf.Abs(v - 0.5f);
                            float width = 0.28f + 0.06f * Mathf.PerlinNoise(u * 5f, 3.1f);
                            float lines = 0.55f + 0.45f * Mathf.PerlinNoise(v * 40f, 1.7f);
                            float ends = Mathf.Clamp01(u * 8f) * Mathf.Clamp01((1f - u) * 3f);
                            a = Mathf.Clamp01((width - dy) * 20f) * lines * ends;
                        }
                        float shade = 0.75f + 0.25f * Mathf.PerlinNoise(u * 12f + seed, v * 12f);
                        px[(oy + y) * size + ox + x] = new Color(shade, shade, shade, a);
                    }
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }
    }

    public static class CarlMath
    {
        /// <summary>Segment vs axis-aligned ellipsoid. Returns false if no hit; t0/t1 are segment parameters (0..1).</summary>
        public static bool SegmentEllipsoid(Vector3 p0, Vector3 p1, Vector3 center, Vector3 radii, out float t0, out float t1)
        {
            t0 = t1 = 0;
            Vector3 a = Div(p0 - center, radii);
            Vector3 d = Div(p1 - p0, radii);
            float A = Vector3.Dot(d, d);
            if (A < 1e-10f) return false;
            float B = 2f * Vector3.Dot(a, d);
            float C = Vector3.Dot(a, a) - 1f;
            float disc = B * B - 4f * A * C;
            if (disc < 0) return false;
            float sq = Mathf.Sqrt(disc);
            t0 = (-B - sq) / (2f * A);
            t1 = (-B + sq) / (2f * A);
            if (t1 < 0 || t0 > 1) return false;
            t0 = Mathf.Max(0, t0);
            t1 = Mathf.Min(1, t1);
            return t1 > t0;
        }

        /// <summary>Segment vs capsule (a-b, radius). Approximates the entry/exit parameters on the segment.</summary>
        public static bool SegmentCapsule(Vector3 p0, Vector3 p1, Vector3 a, Vector3 b, float radius, out float t0, out float t1)
        {
            t0 = t1 = 0;
            ClosestSegmentSegment(p0, p1, a, b, out float s, out float u, out Vector3 c1, out Vector3 c2);
            float d = Vector3.Distance(c1, c2);
            if (d > radius) return false;
            float segLen = Vector3.Distance(p0, p1);
            if (segLen < 1e-6f) return false;
            Vector3 dir = (p1 - p0) / segLen;
            Vector3 axis = (b - a).sqrMagnitude > 1e-8f ? (b - a).normalized : Vector3.up;
            float sin = Mathf.Max(0.2f, Vector3.Cross(dir, axis).magnitude);
            float half = Mathf.Sqrt(radius * radius - d * d) / sin;
            half = Mathf.Min(half, Vector3.Distance(a, b) * 0.5f + radius);
            t0 = Mathf.Clamp01(s - half / segLen);
            t1 = Mathf.Clamp01(s + half / segLen);
            return t1 > t0;
        }

        public static void ClosestSegmentSegment(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            if (a <= 1e-9f && e <= 1e-9f) { s = t = 0; c1 = p1; c2 = p2; return; }
            if (a <= 1e-9f) { s = 0; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-9f) { t = 0; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0 ? Mathf.Clamp01((b * f - c * e) / denom) : 0;
                    t = (b * s + f) / e;
                    if (t < 0) { t = 0; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1) { t = 1; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }

        public static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

        public static float Gauss(System.Random r)
        {
            double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
        }
    }
}
