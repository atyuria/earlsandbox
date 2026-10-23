using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The radio's map: the battlefield drawn as a hand-painted tactical map, from what is really there. The ground
    /// is measured from straight above (a ray every third of a metre) and every spot is sorted by how far it lies
    /// below or above the ground round it:
    ///   trenches    duckboard planks down the floor, a dark band where the wall drops away, and a rim of pale
    ///               stones and sandbags along both lips
    ///   shell holes darker churned earth, scorched in the middle
    ///   cover       sandbag walls, wrecks, hedgehogs, houses: drawn in their own colours with a drop shadow
    ///   wire        the wire runs as dark coils between their posts
    /// over brown earth with a little texture, a scatter of debris and the lie of the land shaded in. It is drawn
    /// once per map, a slice at a time over a second or two after the map loads (so nothing stutters).
    /// </summary>
    public static class MapPainter
    {
        public static Texture2D Tex { get; private set; }
        /// <summary>The ground it covers: x and z of its corner, and its size, in metres.</summary>
        public static Rect World { get; private set; }
        public static bool Ready { get; private set; }
        public static float Progress { get; private set; }
        static Transform paintedFor;
        static bool busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Tex = null; Ready = false; Progress = 0f; paintedFor = null; busy = false; }

        /// <summary>For looking at it outside the game: the map as drawn, to a PNG (false if it isn't drawn yet).</summary>
        public static bool SavePng(string path)
        {
            if (!Ready || Tex == null) return false;
            System.IO.File.WriteAllBytes(path, Tex.EncodeToPNG());
            return true;
        }

        /// <summary>Starts drawing the map now up, if it hasn't been (or is being) drawn already.</summary>
        public static void Ensure(MonoBehaviour host, Transform mapRoot)
        {
            if (mapRoot == null || host == null) return;
            if (paintedFor == mapRoot && (Ready || busy)) return;
            paintedFor = mapRoot;
            Ready = false; Progress = 0f;
            host.StartCoroutine(Paint(mapRoot));
        }

        // ------------------------------------------------------------ the palette

        static readonly Color Earth = new Color(0.49f, 0.41f, 0.33f), EarthDark = new Color(0.36f, 0.29f, 0.23f);
        static readonly Color Plank = new Color(0.47f, 0.41f, 0.35f), PlankGap = new Color(0.2f, 0.16f, 0.13f);
        static readonly Color Wall = new Color(0.22f, 0.18f, 0.15f);
        static readonly Color Stone = new Color(0.8f, 0.72f, 0.56f), StoneGap = new Color(0.33f, 0.28f, 0.22f);
        static readonly Color Crater = new Color(0.34f, 0.28f, 0.23f), Scorch = new Color(0.17f, 0.15f, 0.13f);
        static readonly Color Cover = new Color(0.56f, 0.5f, 0.4f), Roof = new Color(0.45f, 0.44f, 0.45f);
        static readonly Color WireC = new Color(0.16f, 0.15f, 0.14f);

        static float Hash(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / 4294967295f; }

        /// <summary>Distance to the nearest stone's edge in a field of stones (cells of about one size).</summary>
        static void Stones(float x, float y, out float edge, out float shade)
        {
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float d1 = 9f, d2 = 9f; int bx = 0, by = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float px = cx + i + 0.15f + 0.7f * Hash(cx + i, cy + j), py = cy + j + 0.15f + 0.7f * Hash(cy + j + 91, cx + i + 17);
                    float d = (x - px) * (x - px) + (y - py) * (y - py);
                    if (d < d1) { d2 = d1; d1 = d; bx = cx + i; by = cy + j; } else if (d < d2) d2 = d;
                }
            edge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
            shade = Hash(bx * 7 + 3, by * 13 + 5);
        }

        // ------------------------------------------------------------ drawing it

        static IEnumerator Paint(Transform root)
        {
            busy = true;
            yield return null;
            Physics.SyncTransforms();
            // what to cover: the map's solid things, but not some huge plane stretching off to the horizon
            Bounds b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
            foreach (var c in root.GetComponentsInChildren<Collider>())
            {
                if (c == null || c.isTrigger || c.bounds.size.x > 1500f || c.bounds.size.z > 1500f) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (!any) b = new Bounds(Vector3.zero, new Vector3(120f, 10f, 120f));
            float xMin = Mathf.Max(b.min.x, b.center.x - 170f) - 4f, xMax = Mathf.Min(b.max.x, b.center.x + 170f) + 4f;
            float zMin = Mathf.Max(b.min.z, b.center.z - 170f) - 4f, zMax = Mathf.Min(b.max.z, b.center.z + 170f) + 4f;
            float mpp = Mathf.Max(0.12f, Mathf.Max(xMax - xMin, zMax - zMin) / 1400f);
            int W = Mathf.CeilToInt((xMax - xMin) / mpp), H = Mathf.CeilToInt((zMax - zMin) / mpp);
            float top = b.max.y + 5f, reach = top - b.min.y + 20f;

            // ---- the heights, a slice at a time
            var h = new float[W * H];
            const int Chunk = 65536;
            var cmds = new NativeArray<RaycastCommand>(Chunk, Allocator.Persistent);
            var hits = new NativeArray<RaycastHit>(Chunk, Allocator.Persistent);
            var qp = new QueryParameters(~0, false, QueryTriggerInteraction.Ignore, false);
            try
            {
                for (int start = 0; start < W * H; start += Chunk)
                {
                    if (root == null) yield break;
                    int n = Mathf.Min(Chunk, W * H - start);
                    for (int k = 0; k < n; k++)
                    {
                        int idx = start + k, i = idx % W, j = idx / W;
                        cmds[k] = new RaycastCommand(new Vector3(xMin + (i + 0.5f) * mpp, top, zMin + (j + 0.5f) * mpp), Vector3.down, qp, reach);
                    }
                    for (int k = n; k < Chunk; k++) cmds[k] = new RaycastCommand(Vector3.zero, Vector3.down, qp, 0f);
                    RaycastCommand.ScheduleBatch(cmds, hits, 256, 1, default).Complete();
                    for (int k = 0; k < n; k++)
                    {
                        var hit = hits[k];
                        h[start + k] = hit.normal != Vector3.zero ? hit.point.y : float.NaN;
                    }
                    Progress = 0.6f * (start + n) / (float)(W * H);
                    yield return null;
                }
            }
            finally { cmds.Dispose(); hits.Dispose(); }
            float lowest = float.MaxValue;
            for (int k = 0; k < h.Length; k++) if (!float.IsNaN(h[k])) lowest = Mathf.Min(lowest, h[k]);
            if (lowest == float.MaxValue) lowest = 0f;
            for (int k = 0; k < h.Length; k++) if (float.IsNaN(h[k])) h[k] = lowest;

            // ---- the ground level round about: the commonest height in each 8 m square
            int cell = Mathf.Max(4, Mathf.RoundToInt(8f / mpp));
            int gw = (W + cell - 1) / cell, gh = (H + cell - 1) / cell;
            var gl = new float[gw * gh];
            var bins = new Dictionary<int, int>();
            for (int cy = 0; cy < gh; cy++)
                for (int cx = 0; cx < gw; cx++)
                {
                    bins.Clear(); int best = 0, bestN = -1;
                    for (int j = cy * cell; j < Mathf.Min(H, (cy + 1) * cell); j++)
                        for (int i = cx * cell; i < Mathf.Min(W, (cx + 1) * cell); i++)
                        {
                            int bin = Mathf.RoundToInt(h[j * W + i] * 5f);
                            bins.TryGetValue(bin, out int c); bins[bin] = ++c;
                            if (c > bestN) { bestN = c; best = bin; }
                        }
                    gl[cy * gw + cx] = best / 5f;
                }
            // a square that's mostly trench (where two cross) would take the floor for the ground: the field round
            // about is the ground there (but not a roof next door)
            var gl2 = new float[gl.Length];
            for (int cy = 0; cy < gh; cy++)
                for (int cx = 0; cx < gw; cx++)
                {
                    float own = gl[cy * gw + cx], g2 = own;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x2 = cx + dx, y2 = cy + dy;
                            if (x2 < 0 || y2 < 0 || x2 >= gw || y2 >= gh) continue;
                            float o = gl[y2 * gw + x2];
                            if (o > g2 && o > own + 0.9f && o < own + 3f) g2 = o;
                        }
                    gl2[cy * gw + cx] = g2;
                }
            gl = gl2;
            float Ground(int i, int j)
            {
                float fx = Mathf.Clamp((i + 0.5f) / cell - 0.5f, 0f, gw - 1), fy = Mathf.Clamp((j + 0.5f) / cell - 0.5f, 0f, gh - 1);
                int x0 = (int)fx, y0 = (int)fy, x1 = Mathf.Min(x0 + 1, gw - 1), y1 = Mathf.Min(y0 + 1, gh - 1);
                float tx = fx - x0, ty = fy - y0;
                // (the higher of neighbours where one is a trench-filled square: the field, not the floor)
                float a = gl[y0 * gw + x0], b2 = gl[y0 * gw + x1], c2 = gl[y1 * gw + x0], d = gl[y1 * gw + x1];
                return Mathf.Lerp(Mathf.Lerp(a, b2, tx), Mathf.Lerp(c2, d, tx), ty);
            }
            Progress = 0.65f;
            yield return null;

            // ---- what each spot is: 0 ground, 1 dip, 2 trench, 3 low cover, 4 tall (a building)
            var kind = new byte[W * H];
            var depth = new float[W * H];
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    int k = j * W + i;
                    float d = Ground(i, j) - h[k];
                    depth[k] = d;
                    kind[k] = (byte)(d > 0.9f ? 2 : d > 0.4f ? 1 : d < -2.2f ? 4 : d < -0.3f ? 3 : 0);
                }
            // the lips of the trenches: ground within ~0.7 m of a trench
            int rimPx = Mathf.Max(1, Mathf.RoundToInt(0.9f / mpp));
            var nearTrench = new byte[W * H];
            var rowRun = new byte[W * H];
            for (int j = 0; j < H; j++)
            {
                int last = -9999;
                for (int i = 0; i < W; i++) { if (kind[j * W + i] == 2) last = i; if (i - last <= rimPx) rowRun[j * W + i] = 1; }
                last = 99999;
                for (int i = W - 1; i >= 0; i--) { if (kind[j * W + i] == 2) last = i; if (last - i <= rimPx) rowRun[j * W + i] = 1; }
            }
            // (across the rows, then down the columns: within that distance either way)
            for (int i = 0; i < W; i++)
            {
                for (int j = 0; j < H; j++)
                    for (int q = -rimPx; q <= rimPx; q++)
                    {
                        int jj = j + q;
                        if (jj >= 0 && jj < H && rowRun[jj * W + i] == 1) { nearTrench[j * W + i] = 1; break; }
                    }
                if (i % 64 == 0) { Progress = 0.65f + 0.1f * i / W; yield return null; }
            }

            // ---- colouring it in
            var px = new Color32[W * H];
            float sunX = -0.6f, sunZ = 0.6f;                 // (light from the north-west, as maps are drawn)
            int shOff = Mathf.Max(1, Mathf.RoundToInt(0.5f / mpp));
            for (int j = 0; j < H; j++)
            {
                for (int i = 0; i < W; i++)
                {
                    int k = j * W + i;
                    float x = xMin + (i + 0.5f) * mpp, z = zMin + (j + 0.5f) * mpp;
                    // the lie of the land
                    float hl = h[j * W + Mathf.Max(0, i - 1)], hr = h[j * W + Mathf.Min(W - 1, i + 1)];
                    float hd = h[Mathf.Max(0, j - 1) * W + i], hu = h[Mathf.Min(H - 1, j + 1) * W + i];
                    float gx = Mathf.Clamp((hr - hl) / (2f * mpp), -2f, 2f), gz = Mathf.Clamp((hu - hd) / (2f * mpp), -2f, 2f);
                    float light = 1f + Mathf.Clamp(-(gx * sunX + gz * sunZ) * 0.35f, -0.35f, 0.25f);
                    float noise = Mathf.PerlinNoise(x * 0.09f, z * 0.09f) * 0.6f + Mathf.PerlinNoise(x * 0.5f, z * 0.5f) * 0.4f;
                    Color c;
                    byte kd = kind[k];
                    if (kd == 2)
                    {
                        // the trench: planks down the floor (drawn on later), a dark wall where it drops away
                        bool edge = i > 0 && kind[k - 1] != 2 || i < W - 1 && kind[k + 1] != 2 || j > 0 && kind[k - W] != 2 || j < H - 1 && kind[k + W] != 2;
                        c = edge ? Wall : Color.Lerp(Plank, Wall, Mathf.Clamp01((depth[k] - 0.9f) * 0.1f));
                    }
                    else if (kd == 1)
                    {
                        float t = Mathf.Clamp01((depth[k] - 0.22f) / 0.7f);
                        c = Color.Lerp(Crater, Scorch, t * 0.8f) * light;
                    }
                    else if (kd == 3 || kd == 4)
                    {
                        bool edge = i > 0 && kind[k - 1] < 3 || i < W - 1 && kind[k + 1] < 3 || j > 0 && kind[k - W] < 3 || j < H - 1 && kind[k + W] < 3;
                        c = (kd == 4 ? Roof : Cover) * (0.9f + 0.15f * noise);
                        if (edge) c *= 0.55f;
                    }
                    else if (nearTrench[k] == 1)
                    {
                        // the lip: pale stones and sandbags packed along it
                        Stones(x / 0.42f, z / 0.42f, out float e, out float s);
                        c = e < 0.12f ? StoneGap : Color.Lerp(Stone * 0.82f, Stone, s) * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(e * 3f));
                    }
                    else
                    {
                        c = Color.Lerp(EarthDark, Earth, 0.55f + noise * 0.5f) * light;
                        // the scatter: clods, splinters, bits of everything
                        float r = Hash(i, j);
                        if (r < 0.012f) c *= 0.62f;
                        else if (r > 0.994f) c = Color.Lerp(c, Stone, 0.55f);
                        // a drop shadow off anything standing up, away from the light
                        int si = i + shOff, sj = j - shOff;
                        if (si < W && sj >= 0 && kind[sj * W + si] >= 3) c *= 0.62f;
                    }
                    c.a = 1f;
                    px[k] = c;
                }
                if (j % 48 == 0) { Progress = 0.75f + 0.2f * j / H; yield return null; }
            }

            // ---- duckboards: slats across the trench floors, all the way along every trench
            void Put(float wx, float wz, Color c, bool onlyFloor)
            {
                int i = Mathf.FloorToInt((wx - xMin) / mpp), j = Mathf.FloorToInt((wz - zMin) / mpp);
                if (i < 0 || j < 0 || i >= W || j >= H) return;
                int k = j * W + i;
                if (onlyFloor && (kind[k] != 2 || px[k].Equals((Color32)Wall))) return;
                px[k] = c;
            }
            var lines = MapBuilder.TrenchLinesNow;
            if (lines != null)
                foreach (var (lk, pts) in lines)
                    for (int s = 0; s < pts.Length - 1; s++)
                    {
                        Vector2 a = pts[s], bb = pts[s + 1];
                        float len = Vector2.Distance(a, bb);
                        if (len < 0.01f) continue;
                        Vector2 dir = (bb - a) / len, side = new Vector2(-dir.y, dir.x);
                        for (float t = 0f; t < len; t += 0.34f)
                        {
                            Vector2 p = a + dir * t;
                            for (float w = -1.6f; w <= 1.6f; w += mpp * 0.5f) Put(p.x + side.x * w, p.y + side.y * w, PlankGap, true);
                        }
                        // the rails the slats sit on
                        for (float t = 0f; t < len; t += mpp * 0.5f)
                        {
                            Vector2 p = a + dir * t;
                            Put(p.x + side.x * 0.45f, p.y + side.y * 0.45f, PlankGap, true);
                            Put(p.x - side.x * 0.45f, p.y - side.y * 0.45f, PlankGap, true);
                        }
                    }

            // ---- wire: coils between the posts of every run
            var runs = new Dictionary<Transform, List<Vector3>>();
            if (root != null)
                foreach (var t in root.GetComponentsInChildren<Transform>())
                    if (t.name == "WirePost" && t.parent != null)
                    {
                        if (!runs.TryGetValue(t.parent, out var l)) runs[t.parent] = l = new List<Vector3>();
                        l.Add(t.position);
                    }
            foreach (var l in runs.Values)
            {
                if (l.Count < 2) continue;
                // in order along the run
                Vector3 a0 = l[0], far = l[0];
                foreach (var p in l) if ((p - a0).sqrMagnitude > (far - a0).sqrMagnitude) far = p;
                Vector3 axis = (far - a0).normalized;
                l.Sort((p, q) => Vector3.Dot(p - a0, axis).CompareTo(Vector3.Dot(q - a0, axis)));
                for (int s = 0; s < l.Count - 1; s++)
                {
                    Vector2 a = new Vector2(l[s].x, l[s].z), bb = new Vector2(l[s + 1].x, l[s + 1].z);
                    float len = Vector2.Distance(a, bb);
                    if (len < 0.01f || len > 12f) continue;
                    Vector2 dir = (bb - a) / len, side = new Vector2(-dir.y, dir.x);
                    for (float t = 0f; t < len; t += mpp * 0.4f)
                    {
                        float wob = Mathf.Sin(t / 0.45f * Mathf.PI * 2f) * 0.3f;
                        Vector2 p = a + dir * t;
                        Put(p.x + side.x * wob, p.y + side.y * wob, WireC, false);
                        Put(p.x - side.x * wob * 0.6f, p.y - side.y * wob * 0.6f, WireC, false);
                    }
                    // the post
                    for (float w = -0.25f; w <= 0.25f; w += mpp * 0.5f) { Put(a.x + w, a.y, WireC, false); Put(a.x, a.y + w, WireC, false); }
                }
            }

            if (root == null) { busy = false; yield break; }
            if (Tex != null) Object.Destroy(Tex);
            Tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 2, name = "RadioMap" };
            Tex.SetPixels32(px);
            Tex.Apply(true);
            World = new Rect(xMin, zMin, W * mpp, H * mpp);
            Progress = 1f;
            Ready = true;
            busy = false;
        }
    }
}
