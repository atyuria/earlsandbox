using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Carl
{
    /// <summary>A batch of surface quads (bullet holes, scorch marks) drawn as one mesh.</summary>
    public class StainBatch
    {
        public struct Stain
        {
            public Vector3 pos, normal, right;
            public float w, h;
            public int cell;
            public float alpha;
        }

        public readonly List<Stain> stains = new List<Stain>();
        readonly int max;
        readonly Material mat;
        readonly Mesh mesh;
        bool dirty;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> nrm = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color> col = new List<Color>();
        readonly List<int> idx = new List<int>();

        public StainBatch(Material m, int maxStains)
        {
            mat = m;
            max = maxStains;
            mesh = new Mesh { name = "Marks", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
        }

        public void Add(Stain s)
        {
            if (stains.Count >= max) stains.RemoveAt(0);
            stains.Add(s);
            dirty = true;
        }

        public void Clear() { stains.Clear(); dirty = true; }

        public void Render()
        {
            if (dirty) Rebuild();
            if (stains.Count == 0) return;
            var rp = new RenderParams(mat) { worldBounds = new Bounds(Vector3.zero, Vector3.one * 5000f), shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true };
            Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.identity);
        }

        void Rebuild()
        {
            dirty = false;
            v.Clear(); nrm.Clear(); uv.Clear(); col.Clear(); idx.Clear();
            for (int i = 0; i < stains.Count; i++)
            {
                var s = stains[i];
                Vector3 up = Vector3.Cross(s.normal, s.right).normalized;
                Vector3 r = s.right * (s.w * 0.5f), u = up * (s.h * 0.5f);
                Vector3 c = s.pos + s.normal * 0.002f;
                int b = v.Count;
                v.Add(c - r - u); v.Add(c + r - u); v.Add(c + r + u); v.Add(c - r + u);
                for (int k = 0; k < 4; k++) nrm.Add(s.normal);
                float cu = (s.cell % 2) * 0.5f, cv = (s.cell / 2) * 0.5f;
                uv.Add(new Vector2(cu, cv)); uv.Add(new Vector2(cu + 0.5f, cv)); uv.Add(new Vector2(cu + 0.5f, cv + 0.5f)); uv.Add(new Vector2(cu, cv + 0.5f));
                var cc = new Color(1f, 1f, 0f, s.alpha);
                for (int k = 0; k < 4; k++) col.Add(cc);
                idx.Add(b); idx.Add(b + 2); idx.Add(b + 1);
                idx.Add(b); idx.Add(b + 3); idx.Add(b + 2);
            }
            mesh.Clear();
            mesh.SetVertices(v); mesh.SetNormals(nrm); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(idx, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 5000f);
        }
    }

    /// <summary>
    /// Real-time blood. Droplets are simulated in flight; when they land they stamp unique splats into
    /// GPU blood fields on the surface (world tiles) or the body part's skin, where blood keeps flowing:
    /// it runs down walls and limbs, pools and spreads, gets smeared by sliding bodies and dries over time.
    /// </summary>
    public class BloodSystem : MonoBehaviour
    {
        public static BloodSystem Instance;

        struct Drop
        {
            public Vector3 pos, vel;
            public float size, vol, age, drag;
            public HumanBody source;
            public bool mist;
        }

        class Tile
        {
            public BloodCanvas canvas;
            public GameObject go;
            public MeshRenderer mr;
            public MaterialPropertyBlock mpb;
            public Vector3 corner, u, v, n;
            public Vector4 grav;
            public float lastUse;
            public (int, int, int, int, int, int) key;
            public readonly float[] amt = new float[Grid * Grid];
        }

        const int MaxDrops = 4000;
        const float TileSize = 1f;
        static int TileRes = 256;   // pixels per metre (blood quality setting)
        const int MaxTiles = 420;

        readonly Drop[] drops = new Drop[MaxDrops];
        int dropCount;
        ParticleSystem ps;
        ParticleSystem.Particle[] pbuf = new ParticleSystem.Particle[MaxDrops];
        public StainBatch marks;
        Material surfaceMat;
        readonly Dictionary<(int, int, int, int, int, int), Tile> tiles = new Dictionary<(int, int, int, int, int, int), Tile>();
        readonly List<BodyPart> bloodyParts = new List<BodyPart>();
        readonly Dictionary<HumanBody, float> heartPhase = new Dictionary<HumanBody, float>();
        readonly Dictionary<HumanBody, float> coughTimer = new Dictionary<HumanBody, float>();
        Mesh tileMesh;

        public static BloodSystem Create()
        {
            var go = new GameObject("BloodSystem");
            var bs = go.AddComponent<BloodSystem>();
            Instance = bs;
            bs.Setup();
            return bs;
        }

        void Setup()
        {
            var atlas = TexGen.BloodAtlas();
            var markMat = new Material(Mats.Find("Carl/BloodStain"));
            markMat.SetTexture("_MainTex", atlas);
            markMat.SetColor("_Fresh", new Color(0.05f, 0.05f, 0.05f));
            markMat.SetColor("_Dry", new Color(0.07f, 0.07f, 0.07f));
            markMat.renderQueue = 2960;
            marks = new StainBatch(markMat, 600);
            surfaceMat = new Material(Mats.Find("Carl/BloodSurface"));

            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = MaxDrops;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.startLifetime = 100f;
            main.simulationSpeed = 0.0001f;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.03f;
            r.lengthScale = 1.4f;
            r.sharedMaterial = Mats.SoftParticle();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.minParticleSize = 0f;
            ps.Play();

            tileMesh = new Mesh { name = "BloodTile" };
            tileMesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) };
            tileMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            tileMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            tileMesh.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
            tileMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            tileMesh.bounds = new Bounds(new Vector3(0.5f, 0.5f, 0), new Vector3(1, 1, 0.1f));
        }

        public void ClearAll()
        {
            dropCount = 0;
            runners.Clear();
            foreach (var t in tiles.Values) { t.canvas.Release(); Destroy(t.go); }
            tiles.Clear();
            bloodyParts.Clear();
            marks.Clear();
        }

        // ============================================================ surface tiles

        // surfaces are told apart to half a centimetre: a rug, a step or a mat lying on the floor gets its own blood
        // layer exactly on its top (rounding to 2.5 cm put blood under the rug, popping in and out with distance)
        const float PlaneSteps = 200f;

        Tile GetTile(Vector3 p, Vector3 normal, out Vector2 uv)
        {
            Vector3 nq = new Vector3(Mathf.Round(normal.x * 20f), Mathf.Round(normal.y * 20f), Mathf.Round(normal.z * 20f));
            var nKey = ((int)nq.x, (int)nq.y, (int)nq.z);
            nq = nq.normalized;
            float d = Mathf.Round(Vector3.Dot(nq, p) * PlaneSteps) / PlaneSteps;
            Vector3 u, v;
            if (Mathf.Abs(nq.y) > 0.9f)
            {
                u = Vector3.ProjectOnPlane(Vector3.right, nq).normalized;
                v = Vector3.Cross(nq, u).normalized;
            }
            else
            {
                v = Vector3.ProjectOnPlane(Vector3.up, nq).normalized;
                u = Vector3.Cross(v, nq).normalized;
            }
            Vector3 o = nq * d;
            float pu = Vector3.Dot(p - o, u), pv = Vector3.Dot(p - o, v);
            int i = Mathf.FloorToInt(pu / TileSize), j = Mathf.FloorToInt(pv / TileSize);
            uv = new Vector2(pu / TileSize - i, pv / TileSize - j);
            var key = (nKey.Item1, nKey.Item2, nKey.Item3, Mathf.RoundToInt(d * PlaneSteps), i, j);
            if (tiles.TryGetValue(key, out var t)) { t.lastUse = Time.time; return t; }

            if (tiles.Count >= MaxTiles) EvictOldest();
            mNewTile.Begin();
            t = new Tile { canvas = new BloodCanvas(TileRes, false), u = u, v = v, n = nq, lastUse = Time.time, mpb = new MaterialPropertyBlock() };
            t.corner = o + u * (i * TileSize) + v * (j * TileSize);
            Vector3 down = Vector3.down;
            Vector2 g = new Vector2(Vector3.Dot(down, u), Vector3.Dot(down, v));
            t.grav = new Vector4(g.sqrMagnitude > 1e-6f ? g.normalized.x : 0f, g.sqrMagnitude > 1e-6f ? g.normalized.y : 0f, g.magnitude, 0f);
            t.go = new GameObject("BloodTile");
            t.go.transform.SetParent(transform, false);
            // object space: x along u, y along v, -z along the normal (quad faces -Z in object space)
            t.go.transform.position = t.corner + nq * 0.003f;
            t.go.transform.rotation = Quaternion.LookRotation(-nq, v);
            if (Vector3.Dot(t.go.transform.right, u) < 0f)
            {
                // mirrored basis: flip so object x lines up with u
                t.go.transform.localScale = new Vector3(-TileSize, TileSize, 1f);
            }
            else t.go.transform.localScale = new Vector3(TileSize, TileSize, 1f);
            t.go.AddComponent<MeshFilter>().sharedMesh = tileMesh;
            t.mr = t.go.AddComponent<MeshRenderer>();
            t.mr.sharedMaterial = surfaceMat;
            t.mr.shadowCastingMode = ShadowCastingMode.Off;
            t.key = key;
            t.canvas.mask = BuildMask(t);
            t.mpb.SetTexture("_Mask", t.canvas.mask);
            tiles[key] = t;
            mNewTile.End();
            return t;
        }

        /// <summary>Raycasts a grid over the tile to find where the surface really is (walls end, corners, edges).</summary>
        const int MaskN = 48, CoarseN = 12;
        /// <summary>Tiles over an edge whose fine mask is still being worked out, a few rows a frame.</summary>
        readonly List<(Tile t, Color32[] px)> maskQueue = new List<(Tile, Color32[])>();
        int maskRow;

        bool OnSurface(Tile t, float fx, float fy)
        {
            Vector3 p = t.corner + t.u * (fx * TileSize) + t.v * (fy * TileSize);
            return Physics.Raycast(p + t.n * 0.04f, -t.n, out var h, 0.08f, ~0, QueryTriggerInteraction.Ignore)
                   && Vector3.Dot(h.normal, t.n) > 0.95f && h.rigidbody == null;
        }

        /// <summary>
        /// Where on the tile the surface really is. A coarse look first (12 x 12): a tile wholly on the floor or a
        /// wall (most of them) is done at once; one over an edge gets the coarse mask now and the full 48 x 48 a
        /// few rows a frame after (RefineMasks) - the 2300 raycasts all in one frame were a hitch on every new splash.
        /// </summary>
        Texture2D BuildMask(Tile t)
        {
            var tex = new Texture2D(MaskN, MaskN, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[MaskN * MaskN];
            var coarse = new bool[CoarseN * CoarseN];
            bool all = true;
            for (int y = 0; y < CoarseN; y++)
                for (int x = 0; x < CoarseN; x++)
                    all &= coarse[y * CoarseN + x] = OnSurface(t, (x + 0.5f) / CoarseN, (y + 0.5f) / CoarseN);
            const int k = MaskN / CoarseN;
            for (int y = 0; y < MaskN; y++)
                for (int x = 0; x < MaskN; x++)
                    px[y * MaskN + x] = coarse[(y / k) * CoarseN + x / k] ? new Color32(255, 0, 0, 255) : new Color32(0, 0, 0, 255);
            tex.SetPixels32(px);
            tex.Apply(false);
            if (!all) maskQueue.Add((t, px));
            return tex;
        }

        /// <summary>The fine masks of edge tiles, at most so many raycasts a frame.</summary>
        void RefineMasks()
        {
            int budget = 600;
            while (budget > 0 && maskQueue.Count > 0)
            {
                var (t, px) = maskQueue[0];
                if (t == null || t.go == null || t.canvas.mask == null) { maskQueue.RemoveAt(0); maskRow = 0; continue; }
                for (int x = 0; x < MaskN; x++)
                    px[maskRow * MaskN + x] = OnSurface(t, (x + 0.5f) / MaskN, (maskRow + 0.5f) / MaskN) ? new Color32(255, 0, 0, 255) : new Color32(0, 0, 0, 255);
                budget -= MaskN;
                if (++maskRow >= MaskN)
                {
                    var mt = (Texture2D)t.canvas.mask; mt.SetPixels32(px);
                    mt.Apply(false);
                    maskQueue.RemoveAt(0);
                    maskRow = 0;
                }
            }
        }

        void EvictOldest()
        {
            (int, int, int, int, int, int) oldest = default; float best = float.MaxValue;
            foreach (var kv in tiles) if (kv.Value.lastUse < best) { best = kv.Value.lastUse; oldest = kv.Key; }
            if (tiles.TryGetValue(oldest, out var t)) { t.canvas.Release(); if (t.canvas.mask) Destroy(t.canvas.mask); Destroy(t.go); tiles.Remove(oldest); }
        }

        const int Grid = 32;

        // CPU estimate of how much blood is lying where (so bodies can pick it up / wipe it)
        static void AddAmount(Tile t, Vector2 uv, float r, float amount)
        {
            int r0 = Mathf.Max(1, Mathf.CeilToInt(r * Grid));
            int cx = Mathf.FloorToInt(uv.x * Grid), cy = Mathf.FloorToInt(uv.y * Grid);
            for (int y = cy - r0; y <= cy + r0; y++)
                for (int x = cx - r0; x <= cx + r0; x++)
                {
                    if (x < 0 || y < 0 || x >= Grid || y >= Grid) continue;
                    float d = new Vector2(x - cx, y - cy).magnitude / (r0 + 0.5f);
                    if (d > 1f) continue;
                    t.amt[y * Grid + x] = Mathf.Max(0f, t.amt[y * Grid + x] + amount * (1f - d * 0.5f));
                }
        }

        /// <summary>Approximate blood thickness lying on the surface at p (0 = dry).</summary>
        public float BloodOnSurface(Vector3 p, Vector3 n)
        {
            p = SnapToSurface(p, n);
            var t = FindTile(p, n, out var uv);
            if (t == null) return 0f;
            int x = Mathf.Clamp((int)(uv.x * Grid), 0, Grid - 1), y = Mathf.Clamp((int)(uv.y * Grid), 0, Grid - 1);
            return t.amt[y * Grid + x];
        }

        Tile FindTile(Vector3 p, Vector3 n, out Vector2 uv)
        {
            // same key math as GetTile, without creating
            Vector3 nq = new Vector3(Mathf.Round(n.x * 20f), Mathf.Round(n.y * 20f), Mathf.Round(n.z * 20f));
            var nKey = ((int)nq.x, (int)nq.y, (int)nq.z);
            nq = nq.normalized;
            float d = Mathf.Round(Vector3.Dot(nq, p) * PlaneSteps) / PlaneSteps;
            Vector3 u, v;
            if (Mathf.Abs(nq.y) > 0.9f) { u = Vector3.ProjectOnPlane(Vector3.right, nq).normalized; v = Vector3.Cross(nq, u).normalized; }
            else { v = Vector3.ProjectOnPlane(Vector3.up, nq).normalized; u = Vector3.Cross(v, nq).normalized; }
            Vector3 o = nq * d;
            float pu = Vector3.Dot(p - o, u), pv = Vector3.Dot(p - o, v);
            int i = Mathf.FloorToInt(pu / TileSize), j = Mathf.FloorToInt(pv / TileSize);
            uv = new Vector2(pu / TileSize - i, pv / TileSize - j);
            tiles.TryGetValue((nKey.Item1, nKey.Item2, nKey.Item3, Mathf.RoundToInt(d * PlaneSteps), i, j), out var t);
            return t;
        }

        static readonly RaycastHit[] snapHits = new RaycastHit[8];

        /// <summary>Puts a point exactly on the solid surface it belongs to (contacts can sit a few mm inside it).</summary>
        static Vector3 SnapToSurface(Vector3 p, Vector3 n)
        {
            int c = Physics.RaycastNonAlloc(p + n * 0.06f, -n, snapHits, 0.12f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 q = p;
            for (int i = 0; i < c; i++)
            {
                var h = snapHits[i];
                if (h.collider.attachedRigidbody != null) continue;          // bodies and loose things are not the surface
                if (h.distance < best) { best = h.distance; q = h.point; }
            }
            return q;
        }

        void StampSurface(Vector3 p, Vector3 n, float radius, float amount, Vector3 travel, int kind, float spatter, float stretch = -1f, float seed = -1f)
        {
            p = SnapToSurface(p, n);
            var t = GetTile(p, n, out var uv);
            Vector3 tang = Vector3.ProjectOnPlane(travel, t.n);
            Vector2 dir = new Vector2(Vector3.Dot(tang, t.u), Vector3.Dot(tang, t.v));
            if (stretch < 0f) stretch = 1f + Mathf.Clamp(tang.magnitude * 0.35f, 0f, 3.5f);
            float r = radius / TileSize;
            t.canvas.AddStamp(uv, new Vector2(r, r), amount, dir, stretch, kind, spatter, seed);
            AddAmount(t, uv, r * (kind == 1 ? Mathf.Max(1f, stretch * 0.6f) : 1f), amount * (kind == 1 ? 0.12f : 1f)); // a thin smear is not a pool to pick back up
            // carry the stamp over tile borders so splats are not clipped
            float reach = r * (kind == 1 ? stretch * 1.2f : 1.5f + spatter * 3f);
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    if (di == 0 && dj == 0) continue;
                    if (di < 0 && uv.x > reach || di > 0 && uv.x < 1f - reach) continue;
                    if (dj < 0 && uv.y > reach || dj > 0 && uv.y < 1f - reach) continue;
                    var nt = GetTile(p + t.u * (di * TileSize) + t.v * (dj * TileSize), n, out _);
                    if (nt != t) nt.canvas.AddStamp(uv - new Vector2(di, dj), new Vector2(r, r), amount, dir, stretch, kind, spatter, seed);
                }
        }

        // ============================================================ emitters

        public void Spray(Vector3 pos, Vector3 dir, int count, float speedMin, float speedMax, float spread, float size, BodyPart source)
        {
            dir.Normalize();
            count = Mathf.RoundToInt(count * GameSettings.BloodMult + (GameSettings.NoBlood ? 0f : Random.value * 0.6f));
            for (int i = 0; i < count; i++)
            {
                Vector3 d = (dir + Random.insideUnitSphere * spread).normalized;
                Add(pos, d * Random.Range(speedMin, speedMax), size * Random.Range(0.5f, 1.6f), Random.Range(0.2f, 0.8f), source ? source.body : null, false);
            }
            SpraySound(pos, count);
        }

        /// <summary>A wet spatter / hiss when blood sprays out (not every spray, and never loud).</summary>
        void SpraySound(Vector3 pos, int count)
        {
            if (count < 6 || Time.time < nextSpraySound) return;
            nextSpraySound = Time.time + Random.Range(0.18f, 0.35f);
            Sfx.Play("blood_spray", pos, Mathf.Clamp(0.08f + count * 0.004f, 0.08f, 0.22f), 1f, 0.8f, 0.12f, 14f);
        }

        public void Mist(Vector3 pos, Vector3 dir, int count)
        {
            count = Mathf.RoundToInt(count * GameSettings.BloodMult);
            for (int i = 0; i < count; i++)
            {
                Vector3 d = (dir + Random.insideUnitSphere * 0.9f).normalized;
                Add(pos, d * Random.Range(2f, 9f), Random.Range(0.002f, 0.006f), 0.05f, null, true);
            }
        }

        /// <summary>A bloody body part dragging along a surface.</summary>
        public void Smear(Vector3 pos, Vector3 normal, Vector3 along, float length, float amount)
        {
            SmearSegment(pos - along * length, pos, normal, 0.07f, amount, Random.value * 97f);
        }

        /// <summary>
        /// One piece of a drag mark from a to b. Called every few centimetres along the real contact path,
        /// so drags follow curves; thickness = how much blood the dragging part is carrying.
        /// </summary>
        public void SmearSegment(Vector3 a, Vector3 b, Vector3 normal, float width, float carried, float seed)
        {
            Vector3 seg = Vector3.ProjectOnPlane(b - a, normal);
            float len = seg.magnitude;
            if (len < 0.005f || carried <= 0.005f) return;
            float thickness = Mathf.Clamp(carried * 0.35f, 0.02f, 0.9f);
            StampSurface((a + b) * 0.5f, normal, width * 0.5f, thickness, seg, 1, 0f, Mathf.Max(1f, (len + width * 1.2f) / width), seed);
        }

        /// <summary>Blood seeping from a resting contact onto the surface beneath it.</summary>
        public void Ooze(Vector3 p, Vector3 normal, float radius, float amount)
        {
            StampSurface(p, normal, radius, amount, Vector3.zero, 2, 0f);
        }

        /// <summary>Pull blood off a surface (a body sliding through a pool). Returns how much was picked up.</summary>
        public float Wipe(Vector3 p, Vector3 normal, float radius, float strength)
        {
            float there = BloodOnSurface(p, normal);
            if (there <= 0.6f) return 0f; // only real pools soak a dragged body
            float take = Mathf.Min(there, strength) * 0.5f;
            StampSurface(p, normal, radius, -take, Vector3.zero, 2, 0f);
            return take;
        }

        public void Mark(Vector3 pos, Vector3 normal, float size, float alpha = 0.85f)
        {
            Vector3 right = Vector3.Cross(normal, Random.onUnitSphere).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(normal, Vector3.up).normalized;
            marks.Add(new StainBatch.Stain { pos = pos, normal = normal, right = right, w = size, h = size, cell = Random.Range(0, 3), alpha = alpha });
        }

        public void RegisterBloody(BodyPart p)
        {
            if (!bloodyParts.Contains(p)) bloodyParts.Add(p);
        }

        void Add(Vector3 pos, Vector3 vel, float size, float vol, HumanBody src, bool mist)
        {
            if (dropCount >= MaxDrops || GameSettings.NoBlood) return;
            drops[dropCount++] = new Drop { pos = pos, vel = vel, size = size, vol = vol, source = src, mist = mist, drag = mist ? 3.5f : 0.2f };
        }

        static readonly RaycastHit[] floorHits = new RaycastHit[8];

        /// <summary>The ground (not a body) straight under a point.</summary>
        static bool FloorUnder(Vector3 p, out RaycastHit hit)
        {
            hit = default;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.05f, Vector3.down, floorHits, 0.6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = floorHits[i].collider;
                if (c.attachedRigidbody != null) continue;   // bodies, loose objects
                if (floorHits[i].distance < best) { best = floorHits[i].distance; hit = floorHits[i]; }
            }
            if (best == float.MaxValue) return false;
            hit.distance -= 0.05f;
            return true;
        }

        void EmitWounds(float dt)
        {
            foreach (var h in HumanBody.All)
            {
                if (h == null || h.physio == null) continue;
                var ph = h.physio;
                heartPhase.TryGetValue(h, out float phase);
                phase += dt * Mathf.Max(ph.heartRate, 0f) / 60f * Mathf.PI * 2f;
                heartPhase[h] = phase;
                float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase)), 3f);
                float pressure = Mathf.Clamp01(ph.systolic / 120f);
                if (ph.cardiacArrest) pressure *= 0.05f;
                float volF = Mathf.Clamp01(ph.blood / Physiology.NormalBlood);
                if (GameSettings.NoBlood) continue;
                if (GameSettings.UnlimitedBlood) volF = 1f;   // wounds never run dry

                bool own = h == PlayerAvatar.Body;
                foreach (var bp in h.partList)
                {
                    // blood carried on the skin runs to the lowest point and drips off
                    if (bp.bloodiness > 0.3f && Random.value < dt * bp.bloodiness * (own ? 0.4f : 1.2f))
                    {
                        Vector3 low = bp.col.ClosestPoint(bp.transform.position + Vector3.down * 2f);
                        Add(low + Vector3.down * 0.005f, bp.rb.GetPointVelocity(low) + Random.insideUnitSphere * 0.05f, Random.Range(0.006f, 0.011f), 0.25f, h, false);
                        bp.bloodiness *= 0.97f;
                    }
                    foreach (var w in bp.wounds)
                    {
                        if (w.type == WoundType.Internal || w.extRate < 0.03f) continue;
                        float rate = w.extRate * (w.arterial ? pressure : Mathf.Lerp(0.35f, 1f, volF)) * GameSettings.BloodMult;
                        rate *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.3f, volF));
                        if (bp.severed) rate *= Mathf.Clamp01(1f - w.age / 40f);
                        if (rate < 0.02f) continue;
                        Vector3 wp = bp.transform.TransformPoint(w.localPos);
                        Vector3 wn = bp.transform.TransformDirection(w.localNormal);
                        Vector3 bodyVel = bp.rb.GetPointVelocity(wp);

                        // lying in it: the blood runs straight out onto the floor and spreads into a pool that grows
                        w.poolTimer -= dt;
                        if (w.poolTimer <= 0f && bodyVel.sqrMagnitude < 0.25f)
                        {
                            w.poolTimer = 0.3f;
                            if (FloorUnder(wp, out RaycastHit fh) && fh.distance < 0.32f)
                            {
                                w.pooled += rate * 0.3f;
                                float pr = Mathf.Min(0.38f, 0.035f + Mathf.Sqrt(w.pooled) * 0.013f);
                                Vector3 spot = fh.point + Random.insideUnitSphere * pr * 0.25f;
                                Ooze(spot, fh.normal, pr * Random.Range(0.7f, 1f), Mathf.Min(0.6f, 0.06f + rate * 0.02f));
                            }
                        }

                        // blood welling out of the wound onto the skin; the skin sim carries it down the body
                        // (a fresh wound keeps welling enough to run in visible streams, even if the loss is small)
                        float well = rate * 1.1f + 0.7f * Mathf.Exp(-w.age / 30f);
                        bp.PaintBloodLocal(w.localPos, 0.01f + Mathf.Min(0.015f, rate * 0.0015f), Mathf.Min(0.35f, well * dt * 1.2f), 2);

                        float perSec, speed, spread, size;
                        if (w.arterial && rate > 2f)
                        {
                            float pulse = 0.1f + beat * 2.4f;
                            perSec = Mathf.Min(70f, rate * 2.5f) * pulse;
                            speed = (0.5f + 4.5f * pressure * beat) * Mathf.Lerp(0.6f, 1.2f, Mathf.Clamp01(rate / 30f));
                            spread = 0.1f; size = 0.007f;
                        }
                        else if (rate > 4f)
                        {
                            perSec = Mathf.Min(40f, rate * 2f);
                            speed = 0.2f + rate * 0.02f; spread = 0.3f; size = 0.009f;
                        }
                        else
                        {
                            // slow wounds drip off the lowest point of the part instead of spraying
                            perSec = Mathf.Max(0.25f, rate * 2f);
                            speed = 0.02f; spread = 0.5f; size = 0.008f;
                            Vector3 low = bp.col.ClosestPoint(bp.transform.position + Vector3.down * 2f);
                            if (Random.value < 0.6f) { wp = low; wn = Vector3.down; }
                        }
                        // your own body: a third of the drops (you rarely see them, and each one is simulated)
                        if (own) perSec *= 0.35f;
                        w.emitAcc += perSec * dt;
                        int n = (int)w.emitAcc;
                        w.emitAcc -= n;
                        float vol = n > 0 ? rate * dt / n * 1.5f : 0f;
                        if (w.arterial && n >= 3) SpraySound(wp, n * 3);     // the spurt of a cut artery
                        for (int i = 0; i < n; i++)
                        {
                            Vector3 d = (wn + Random.insideUnitSphere * spread).normalized;
                            Add(wp + wn * 0.004f, bodyVel + d * speed * Random.Range(0.7f, 1.2f), size * Random.Range(0.6f, 1.5f), vol, h, false);
                        }
                    }
                }

                if (!ph.dead && ph.consciousness > 0.2f && ph.airwayBlood > 0.15f)
                {
                    coughTimer.TryGetValue(h, out float ct);
                    ct -= dt;
                    if (ct <= 0f)
                    {
                        ct = Random.Range(1.5f, 5f) / (0.5f + ph.airwayBlood);
                        var head = h.Head.transform;
                        Vector3 mouth = head.TransformPoint(new Vector3(0, -0.06f, 0.1f));
                        Spray(mouth, head.forward + Vector3.down * 0.3f, 6 + (int)(ph.airwayBlood * 18f), 1f, 3f, 0.35f, 0.005f, h.Head);
                        h.Head.PaintBloodLocal(new Vector3(0, -0.07f, 0.09f), 0.03f, 0.8f, 2);
                    }
                    coughTimer[h] = ct;
                }
            }
        }

        // ============================================================ simulation

        static readonly Unity.Profiling.ProfilerMarker mUpd1 = new Unity.Profiling.ProfilerMarker("Carl.Blood.EmitDropsRunners"), mTiles = new Unity.Profiling.ProfilerMarker("Carl.Blood.WorldTiles"), mParts = new Unity.Profiling.ProfilerMarker("Carl.Blood.BodyParts"), mNewTile = new Unity.Profiling.ProfilerMarker("Carl.Blood.NewTile");

        static readonly Unity.Profiling.ProfilerMarker PM_Update = new Unity.Profiling.ProfilerMarker("Carl.BloodSystem.Update");

        void Update()
        {

            using var pmScope = PM_Update.Auto();
            float dt = WorldFreeze.On ? 0f : Time.deltaTime;   // frozen world (spawn menu): the blood stops too
            // blood quality changed: start the world blood again at the new resolution
            if (TileRes != GameSettings.WorldBloodRes)
            {
                foreach (var t in tiles.Values) { t.canvas.Release(); Destroy(t.go); }
                tiles.Clear();
                runners.Clear();
                TileRes = GameSettings.WorldBloodRes;
            }
            using (mUpd1.Auto())
            if (dt > 0f)
            {
                EmitWounds(dt);
                SimulateDrops(dt);
                UpdateRunners(dt);
            }

            mTiles.Begin();
            foreach (var t in tiles.Values)
            {
                var k = t.key;
                tiles.TryGetValue((k.Item1, k.Item2, k.Item3, k.Item4, k.Item5 - 1, k.Item6), out var nl);
                tiles.TryGetValue((k.Item1, k.Item2, k.Item3, k.Item4, k.Item5 + 1, k.Item6), out var nr);
                tiles.TryGetValue((k.Item1, k.Item2, k.Item3, k.Item4, k.Item5, k.Item6 - 1), out var nd);
                tiles.TryGetValue((k.Item1, k.Item2, k.Item3, k.Item4, k.Item5, k.Item6 + 1), out var nu);
                // a flowing tile keeps its neighbours simulating so blood can cross into them
                if (Time.time < t.canvas.activeUntil)
                {
                    float until = t.canvas.activeUntil;
                    if (nl != null) nl.canvas.activeUntil = Mathf.Max(nl.canvas.activeUntil, until - 1f);
                    if (nr != null) nr.canvas.activeUntil = Mathf.Max(nr.canvas.activeUntil, until - 1f);
                    if (nd != null) nd.canvas.activeUntil = Mathf.Max(nd.canvas.activeUntil, until - 1f);
                    if (nu != null) nu.canvas.activeUntil = Mathf.Max(nu.canvas.activeUntil, until - 1f);
                }
                t.canvas.Step(dt, t.grav, Vector3.zero, t.grav.z > 0.5f ? 0.12f : 1f, nl?.canvas.Texture, nr?.canvas.Texture, nd?.canvas.Texture, nu?.canvas.Texture,
                    new Vector2(k.Item5 * TileRes, k.Item6 * TileRes), false, 7.3f);
                t.mpb.SetTexture("_BloodTex", t.canvas.Texture);
                t.mr.SetPropertyBlock(t.mpb);
            }
            mTiles.End(); mParts.Begin();
            var cam = Camera.main != null ? Camera.main.transform : null;
            for (int i = bloodyParts.Count - 1; i >= 0; i--)
            {
                var p = bloodyParts[i];
                if (p == null) { bloodyParts.RemoveAt(i); continue; }
                // the blood running on your own skin: a third as often (it's mostly out of view)
                if (p.body != null && p.body == PlayerAvatar.Body)
                {
                    if ((Time.frameCount + i) % 3 != 0) continue;
                    p.StepBlood(Mathf.Min(0.1f, dt * 3f));
                }
                // an Earl: every other frame (far off, every fourth), with the step to match - it flows the same,
                // just updated less often; the parts take turns so it's spread over the frames
                else
                {
                    int every = cam != null && (p.transform.position - cam.position).sqrMagnitude > 15f * 15f ? 4 : 2;
                    if ((Time.frameCount + i) % every == 0) p.StepBlood(Mathf.Min(0.1f, dt * every));
                }
            }
            mParts.End();
            RefineMasks();
            marks.Render();
            RenderDrops();
        }

        void SimulateDrops(float dt)
        {
            Vector3 g = Physics.gravity;
            int i = 0;
            while (i < dropCount)
            {
                ref var d = ref drops[i];
                d.age += dt;
                d.vel += g * dt;
                d.vel *= 1f - Mathf.Min(0.9f, d.drag * dt);
                Vector3 step = d.vel * dt;
                float dist = step.magnitude;
                bool kill = d.age > 6f || d.pos.y < -50f;
                if (!kill && dist > 1e-5f)
                {
                    if (Physics.Raycast(d.pos, step / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider is CharacterController) d.pos += step;
                        else
                        {
                            var bp = hit.collider.GetComponent<BodyPart>();
                            if (bp != null)
                            {
                                if (bp.body == d.source && d.age < 0.08f) d.pos += step;
                                else
                                {
                                    bp.PaintBlood(hit.point, 0.01f + d.size, d.mist ? 0.12f : 0.35f, 0);
                                    kill = true;
                                }
                            }
                            else
                            {
                                Land(hit, d);
                                kill = true;
                            }
                        }
                    }
                    else d.pos += step;
                }
                if (kill) { drops[i] = drops[--dropCount]; continue; }
                i++;
            }
        }

        // ------------------------------------------------------------ wall run-off reaching the floor

        struct Runner { public Vector3 pos, normal; public float vol, floorY; }
        readonly List<Runner> runners = new List<Runner>();
        const float RunSpeed = 0.12f; // matches the GPU flow front speed on a vertical wall

        void OnWallBlood(Vector3 p, Vector3 n, float amount)
        {
            p = SnapToSurface(p, n);
            float floorY = p.y - 6f;
            if (Physics.Raycast(p + n * 0.05f, Vector3.down, out var fh, 12f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
            // make sure the wall has canvases all the way down so the drip is never cut off
            var t = GetTile(p, n, out _);
            for (int k = 1; k < 8; k++)
            {
                Vector3 q = p - t.v * (k * TileSize);
                if (q.y < floorY - TileSize * 0.5f) break;
                GetTile(q, n, out _);
            }
            float excess = amount - 0.15f;
            if (excess < 0.1f) return;
            for (int i = 0; i < runners.Count; i++)
            {
                var r = runners[i];
                if (Vector2.Distance(new Vector2(r.pos.x, r.pos.z), new Vector2(p.x, p.z)) < 0.06f && Mathf.Abs(r.pos.y - p.y) < 0.3f)
                {
                    r.vol += excess; runners[i] = r; return;
                }
            }
            if (runners.Count < 250) runners.Add(new Runner { pos = p, normal = n, vol = excess, floorY = floorY });
        }

        void UpdateRunners(float dt)
        {
            for (int i = runners.Count - 1; i >= 0; i--)
            {
                var r = runners[i];
                if (r.pos.y > r.floorY + 0.01f)
                {
                    r.pos.y -= RunSpeed * dt;
                    r.vol -= dt * 0.025f; // left behind as the trail
                }
                else
                {
                    // the drip has reached the floor: feed a pool at the base of the wall
                    float give = Mathf.Min(r.vol, dt * 0.35f);
                    r.vol -= give;
                    if (give > 0f)
                    {
                        Vector3 at = new Vector3(r.pos.x, r.floorY, r.pos.z) + r.normal * 0.03f;
                        StampSurface(at, Vector3.up, 0.035f + Random.value * 0.02f, give * 6f, r.normal, 2, 0f);
                    }
                }
                if (r.vol <= 0f) { runners.RemoveAt(i); continue; }
                runners[i] = r;
            }
        }

        void Land(RaycastHit hit, Drop d)
        {
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) return;
            float speed = d.vel.magnitude;
            float radius = d.mist ? Random.Range(0.003f, 0.008f) : Mathf.Clamp(d.size * 1.8f + speed * 0.003f + d.vol * 0.01f, 0.006f, 0.05f);
            float amount = d.mist ? 0.2f : Mathf.Clamp(0.45f + d.vol * 0.8f, 0.4f, 2f) * Random.Range(0.65f, 1.35f);
            float spatter = d.mist ? 0f : Mathf.Clamp01((speed - 2f) / 6f);
            StampSurface(hit.point, hit.normal, radius, amount, d.vel, 0, spatter);
            if (!d.mist && hit.normal.y < 0.7f && hit.normal.y > -0.3f) OnWallBlood(hit.point, hit.normal, amount);
            // a proper drop hitting the floor: a soft plip (a few a second at most, quiet)
            if (!d.mist && d.size > 0.006f && Time.time > nextDripSound)
            {
                nextDripSound = Time.time + Random.Range(0.09f, 0.22f);
                Sfx.Play("blood_drip", hit.point, Mathf.Clamp(0.08f + d.vol * 0.1f + speed * 0.01f, 0.08f, 0.2f), 1f, 0.6f, 0.15f, 10f);
            }
        }
        float nextDripSound, nextSpraySound;

        void RenderDrops()
        {
            if (pbuf.Length < dropCount) pbuf = new ParticleSystem.Particle[MaxDrops];
            for (int i = 0; i < dropCount; i++)
            {
                ref var d = ref drops[i];
                pbuf[i].position = d.pos;
                pbuf[i].velocity = d.vel;
                pbuf[i].startSize = d.size;
                pbuf[i].startColor = d.mist ? new Color(0.55f, 0.05f, 0.06f, 0.5f) : new Color(0.5f, 0.0f, 0.02f, 1f);
                pbuf[i].remainingLifetime = 50f;
                pbuf[i].startLifetime = 100f;
            }
            ps.SetParticles(pbuf, dropCount);
        }
    }
}
