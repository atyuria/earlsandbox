using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Soft-body style deformation on a body part mesh. Impacts push vertices in (permanent dents, craters)
    /// and add velocity so the flesh wobbles back with a damped spring. Also paints injury channels into vertex colours.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class FleshDeformer : MonoBehaviour
    {
        Mesh mesh;
        Vector3[] rest;      // undeformed
        Vector3[] target;    // rest + permanent dents
        Vector3[] cur;
        Vector3[] vel;
        Color[] colors;
        bool active;
        bool colorsDirty;
        int normalFrame;
        float maxDent;

        readonly System.Collections.Generic.List<int> moving = new System.Collections.Generic.List<int>();
        bool[] isMoving;
        static int normalsFrame, normalsLeft;
        /// <summary>Slow motion on (T): only then does the flesh spring and wobble after a hit.</summary>
        static bool SlowMo => Time.timeScale > 0f && Time.timeScale < 0.9f;
        const int SimBudget = 10000;
        static int simFrame, simLeft;
        float owed;
        void Mark(int i) { if (isMoving == null || isMoving.Length != cur.Length) isMoving = new bool[cur.Length]; if (!isMoving[i]) { isMoving[i] = true; moving.Add(i); } }
        const float Stiffness = 420f;
        const float Damping = 14f;

        int[] weld;
        int[] tris;
        Vector3[] normals;
        Vector3[] restNormals;
        BodyPart part;

        // what each carved-in vertex has reached: rgb = tissue colour, a = how exposed (0 = intact skin)
        Vector4[] exposed;
        bool exposedDirty;
        readonly System.Collections.Generic.List<int> touched = new System.Collections.Generic.List<int>();
        Organ[] byPriority;
        static readonly Color Muscle = new Color(0.52f, 0.07f, 0.08f);
        const float SkinThickness = 0.003f;

        // what every Earl's copy of the same part starts from (worked out once; read-only after)
        class Template { public int[] tris; public Vector3[] rest, restNormals; }
        static readonly System.Collections.Generic.Dictionary<Mesh, Template> templates = new System.Collections.Generic.Dictionary<Mesh, Template>();

        /// <summary>
        /// Once per shared source shape (HumanBody's part cache): the starting state every copy's deformer needs,
        /// and the source itself given the blank colours and UV channels, so copies made of it already have them.
        /// </summary>
        public static void PrepareSource(Mesh src, int[] weldMap)
        {
            var t = new Template { tris = src.triangles, rest = src.vertices };
            t.restNormals = RestNormals(t.rest, t.tris, weldMap);
            src.colors = new Color[t.rest.Length];
            src.SetUVs(2, new Vector4[t.rest.Length]);
            src.SetUVs(3, new System.Collections.Generic.List<Vector3>(t.rest));
            templates[src] = t;
        }

        static Vector3[] RestNormals(Vector3[] rest, int[] tris, int[] weld)
        {
            var rn = new Vector3[rest.Length];
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                Vector3 n = Vector3.Cross(rest[b] - rest[a], rest[c] - rest[a]);
                if (weld != null) { a = weld[a]; b = weld[b]; c = weld[c]; }
                rn[a] += n; rn[b] += n; rn[c] += n;
            }
            for (int i = 0; i < rn.Length; i++) rn[i] = rn[weld != null ? weld[i] : i].normalized;
            return rn;
        }

        /// <summary>A copy of a prepared source shape: shares its starting state, only the moving parts are its own.</summary>
        public void Init(Mesh m, float maxDentDepth, int[] weldMap, Mesh source)
        {
            if (source == null || !templates.TryGetValue(source, out var t) || t.rest.Length != m.vertexCount) { Init(m, maxDentDepth, weldMap); return; }
            mesh = m;
            part = GetComponent<BodyPart>();
            tris = t.tris;
            weld = weldMap;
            normals = new Vector3[t.rest.Length];
            mesh.MarkDynamic();
            rest = t.rest;
            target = (Vector3[])rest.Clone();
            cur = (Vector3[])rest.Clone();
            vel = new Vector3[rest.Length];
            colors = new Color[rest.Length];
            maxDent = maxDentDepth;
            exposed = new Vector4[rest.Length];
            restNormals = t.restNormals;
        }

        public void Init(Mesh m, float maxDentDepth, int[] weldMap = null)
        {
            mesh = m;
            part = GetComponent<BodyPart>();
            tris = m.triangles;
            weld = weldMap;
            normals = new Vector3[m.vertexCount];
            mesh.MarkDynamic();
            rest = m.vertices;
            target = (Vector3[])rest.Clone();
            cur = (Vector3[])rest.Clone();
            vel = new Vector3[rest.Length];
            colors = new Color[rest.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0, 0, 0, 0);
            mesh.colors = colors;
            maxDent = maxDentDepth;
            exposed = new Vector4[rest.Length];
            mesh.SetUVs(2, exposed);
            // undeformed position, so wound openings stay pinned to the skin while the flesh dents
            mesh.SetUVs(3, new System.Collections.Generic.List<Vector3>(rest));
            // undeformed normals, averaged across UV-seam duplicates
            restNormals = new Vector3[rest.Length];
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                Vector3 n = Vector3.Cross(rest[b] - rest[a], rest[c] - rest[a]);
                if (weld != null) { a = weld[a]; b = weld[b]; c = weld[c]; }
                restNormals[a] += n; restNormals[b] += n; restNormals[c] += n;
            }
            for (int i = 0; i < restNormals.Length; i++) restNormals[i] = restNormals[weld != null ? weld[i] : i].normalized;
        }

        // spatial hash of the undeformed vertices, so "is this point inside the skin?" is cheap
        const float CellSize = 0.02f;
        System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<int>> grid;

        static long CellKey(int x, int y, int z) => ((long)(x + 4096) << 26) | ((long)(y + 4096) << 13) | (long)(z + 4096);

        void BuildGrid()
        {
            grid = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<int>>();
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i] / CellSize;
                long k = CellKey(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new System.Collections.Generic.List<int>();
                l.Add(i);
            }
        }

        /// <summary>Nearest undeformed vertex, searching outward through the grid (falls back to a full scan).</summary>
        int NearestFast(Vector3 p)
        {
            if (grid == null) BuildGrid();
            Vector3 g = p / CellSize;
            int cx = Mathf.FloorToInt(g.x), cy = Mathf.FloorToInt(g.y), cz = Mathf.FloorToInt(g.z);
            int best = -1; float bd = float.MaxValue;
            for (int ring = 1; ring <= 3 && best < 0; ring++)
                for (int x = -ring; x <= ring; x++)
                    for (int y = -ring; y <= ring; y++)
                        for (int z = -ring; z <= ring; z++)
                            if (grid.TryGetValue(CellKey(cx + x, cy + y, cz + z), out var l))
                                foreach (int i in l) { float d = (rest[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return best >= 0 ? best : Nearest(p);
        }

        /// <summary>
        /// Is a local point inside the (undeformed) skin? Also gives the nearest skin point and its outward normal,
        /// so anything that would poke out can be pulled back under the surface.
        /// </summary>
        public bool InsideSkin(Vector3 p, out Vector3 skin, out Vector3 normal)
        {
            int i = NearestFast(p);
            skin = rest[i];
            normal = restNormals != null ? restNormals[i] : p.normalized;
            return Vector3.Dot(p - skin, normal) < 0f;
        }

        int Nearest(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < rest.Length; i++) { float d = (rest[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return best;
        }

        /// <summary>Outward skin normal of the undeformed mesh nearest a local point.</summary>
        public Vector3 RestNormalAt(Vector3 p)
        {
            if (rest == null || restNormals == null || restNormals.Length != rest.Length) return p.normalized;
            int i = weld != null ? weld[Nearest(p)] : Nearest(p);
            Vector3 n = restNormals[i];
            return n.sqrMagnitude > 1e-6f ? n.normalized : p.normalized;
        }

        /// <summary>How far the flesh near a point has been permanently pushed in (local space).</summary>
        public Vector3 DentAt(Vector3 p)
        {
            if (rest == null) return Vector3.zero;
            int i = NearestFast(p);
            return target[i] - rest[i];
        }

        /// <summary>Tissue colour at a local point inside the part: bone, brain, organ, else muscle.</summary>
        public Color TissueAt(Vector3 p, out bool organ) => TissueAt(p, out organ, out _);

        /// <summary>As TissueAt; punchable = a thin layer a wound goes straight through (skull, ribs).</summary>
        public Color TissueAt(Vector3 p, out bool organ, out bool punchable)
        {
            organ = false; punchable = false;
            if (part == null) return Muscle;
            if (byPriority == null)
            {
                var list = new System.Collections.Generic.List<Organ>(part.organs);
                list.Sort((x, y) => Priority(x).CompareTo(Priority(y)));
                byPriority = list.ToArray();
            }
            foreach (var o in byPriority)
                if (o.def.kind != TissueKind.Artery && o.def.kind != TissueKind.Vein && o.def.Contains(p))
                {
                    organ = true; punchable = HumanBody.Punchable(o.def);
                    return HumanBody.InteriorColor(o.def);
                }
            return Muscle;
        }

        /// <summary>
        /// Moves a point found on the collider onto the visible skin along the hit line (local space).
        /// Colliders sit a few centimetres off the mesh, and a wound centred in the air never shows.
        /// </summary>
        public Vector3 SnapToSkin(Vector3 localPoint, Vector3 localDir)
        {
            if (rest == null) return localPoint;
            Vector3 d = localDir.sqrMagnitude > 1e-8f ? localDir.normalized : Vector3.forward;
            float best = float.MaxValue, bestAlong = 0f;
            // through the grid, outward from the point, shell by shell: a vertex D away scores at least D*D/2, so
            // once the box searched reaches far enough that nothing outside it could do better, that's the answer
            // (the same answer as looking at every vertex - wound rims did that 48 times a wound)
            if (grid == null) BuildGrid();
            Vector3 g = localPoint / CellSize;
            int cx = Mathf.FloorToInt(g.x), cy = Mathf.FloorToInt(g.y), cz = Mathf.FloorToInt(g.z);
            for (int ring = 0; ring <= 8; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                    for (int y = -ring; y <= ring; y++)
                        for (int z = -ring; z <= ring; z++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != ring) continue;
                            if (!grid.TryGetValue(CellKey(cx + x, cy + y, cz + z), out var l)) continue;
                            foreach (int i in l)
                            {
                                Vector3 v = rest[i] - localPoint;
                                float along = Vector3.Dot(v, d);
                                float score = (v - d * along).sqrMagnitude + 0.5f * along * along;
                                if (score < best) { best = score; bestAlong = along; }
                            }
                        }
                float reach = ring * CellSize;
                if (best < float.MaxValue && 0.5f * reach * reach >= best) return localPoint + d * bestAlong;
            }
            best = float.MaxValue; bestAlong = 0f;
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 v = rest[i] - localPoint;
                float along = Vector3.Dot(v, d);
                float score = (v - d * along).sqrMagnitude + 0.5f * along * along;   // prefer the near surface, never the far side
                if (score < best) { best = score; bestAlong = along; }
            }
            return localPoint + d * bestAlong;
        }

        /// <summary>Push flesh along dir (local space). depth is permanent, wobble is transient velocity.</summary>
        public void Dent(Vector3 localPoint, Vector3 localDir, float radius, float depth, float wobble)
        {
            if (rest == null) return;
            localDir.Normalize();
            float r2 = radius * radius;
            for (int i = 0; i < cur.Length; i++)
            {
                float d2 = (cur[i] - localPoint).sqrMagnitude;
                if (d2 > r2 * 4f) continue;
                float d = Mathf.Sqrt(d2);
                float f = Mathf.Clamp01(1f - d / radius);
                f *= f;
                if (f > 0)
                {
                    // flesh is pushed straight in (or pulled straight out) along the skin normal, never sheared
                    // sideways - a grazing hit must not drag the skin into long flaps
                    Vector3 n = restNormals != null ? restNormals[i] : localDir;
                    float sign = Vector3.Dot(localDir, -n) >= 0f ? 1f : -1f;
                    Vector3 push = sign > 0f ? -n : n * 0.35f;
                    // already pushed in: further hits push less and less (a smooth bowl that saturates, not a
                    // flat-bottomed plateau with a sharp crease where it hits the limit)
                    Vector3 had = target[i] - rest[i];
                    float room = Mathf.Clamp01(1f - had.magnitude / maxDent);
                    Vector3 off = had + push * depth * f * room * room;
                    if (off.magnitude > maxDent) off = off.normalized * maxDent;
                    target[i] = rest[i] + off;
                    touched.Add(i);
                    Mark(i);
                }
                // wider soft wobble ring
                float fw = Mathf.Clamp01(1f - d / (radius * 2f));
                if (SlowMo && fw > 0f) { vel[i] += localDir * wobble * fw * fw; Mark(i); }
            }
            active = true;
            UpdateExposure();
        }

        /// <summary>
        /// Carved vertices show what they have been carved down to: raw muscle under the skin, and the real
        /// bone / brain / organ where the crater reaches one (from the part's anatomy).
        /// </summary>
        void UpdateExposure()
        {
            if (touched.Count == 0 || part == null) return;
            if (byPriority == null)
            {
                var list = new System.Collections.Generic.List<Organ>(part.organs);
                list.Sort((x, y) => Priority(x).CompareTo(Priority(y)));
                byPriority = list.ToArray();
            }
            foreach (int i in touched)
            {
                float depth = Vector3.Distance(target[i], rest[i]);
                float w = Mathf.InverseLerp(SkinThickness * 0.5f, SkinThickness * 3f, depth);
                if (w <= 0f) continue;
                // dented flesh reads as muscle; the real organs and bones show through the openings instead
                Color c = Muscle;
                // deeper = darker cavity
                float shade = Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(0.01f, 0.05f, depth));
                exposed[i] = new Vector4(c.r * shade, c.g * shade, c.b * shade, Mathf.Clamp01(w));
            }
            touched.Clear();
            exposedDirty = true;
        }

        static int Priority(Organ o)
        {
            switch (o.def.kind)
            {
                case TissueKind.Bone: case TissueKind.Spine: return 0;
                case TissueKind.Brain: return 1;
                case TissueKind.Eye: return 2;
                case TissueKind.Organ: return 3;
                case TissueKind.Airway: return 4;
                default: return 5;
            }
        }

        /// <summary>Raised crater edge for exit wounds: pull outward then sink the centre.</summary>
        public void Crater(Vector3 localPoint, Vector3 outward, float radius, float depth, float wobble)
        {
            Dent(localPoint, outward, radius * 1.6f, depth * 0.6f, wobble);
            Dent(localPoint, -outward, radius * 0.6f, depth, 0f);
        }

        /// <summary>A long shallow groove (knife cut).</summary>
        public void Groove(Vector3 a, Vector3 b, Vector3 inward, float radius, float depth)
        {
            for (int k = 0; k <= 6; k++)
                Dent(Vector3.Lerp(a, b, k / 6f), inward, radius, depth, 0.15f);
        }

        /// <summary>Global squash (falls / blasts).</summary>
        public void Jiggle(Vector3 localDir, float amount)
        {
            if (rest == null) return;
            if (!SlowMo) return;   // (no wobble at full speed)
            for (int i = 0; i < vel.Length; i++) { vel[i] += localDir * amount * (0.6f + 0.4f * Mathf.Sin(i * 1.7f)); Mark(i); }
            active = true;
        }

        /// <summary>channel: 0 blood, 1 bruise, 2 scrape, 3 raw wound flesh. Adds paint with falloff.</summary>
        public void Paint(Vector3 localPoint, float radius, int channel, float amount)
        {
            if (channel == 0) { if (part) part.PaintBloodLocal(localPoint, radius * 0.6f, amount * 1.2f, 0); return; }
            if (colors == null) return;
            float r2 = radius * radius;
            for (int i = 0; i < cur.Length; i++)
            {
                float d2 = (cur[i] - localPoint).sqrMagnitude;
                if (d2 > r2) continue;
                float f = 1f - Mathf.Sqrt(d2) / radius;
                // noisy edge so stains are not perfect circles
                f *= 0.6f + 0.4f * Mathf.PerlinNoise(rest[i].x * 60f + channel, rest[i].y * 60f + rest[i].z * 30f);
                var c = colors[i];
                c[channel] = Mathf.Clamp01(c[channel] + amount * f);
                colors[i] = c;
            }
            colorsDirty = true;
        }

        /// <summary>Blood running down from a wound: paints a streak in the local direction of gravity.</summary>
        public void PaintRun(Vector3 localPoint, Vector3 localDown, float length, float width, float amount)
        {
            if (part) part.PaintBloodLocal(localPoint, width, amount, 2);
        }

        /// <summary>Smooth normals that ignore UV-seam vertex splits (duplicates share their canonical vertex).</summary>
        void UpdateNormals()
        {
            if (weld == null) { mesh.RecalculateNormals(); return; }
            System.Array.Clear(normals, 0, normals.Length);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = weld[tris[i]], b = weld[tris[i + 1]], c = weld[tris[i + 2]];
                Vector3 n = Vector3.Cross(cur[tris[i + 1]] - cur[tris[i]], cur[tris[i + 2]] - cur[tris[i]]);
                normals[a] += n; normals[b] += n; normals[c] += n;
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = normals[weld[i]].normalized;
            mesh.normals = normals;
        }

        public float BloodAt(Vector3 localPoint)
        {
            if (colors == null) return 0;
            float best = float.MaxValue; int bi = 0;
            for (int i = 0; i < cur.Length; i += 3)
            {
                float d = (cur[i] - localPoint).sqrMagnitude;
                if (d < best) { best = d; bi = i; }
            }
            return colors[bi].r;
        }

        static readonly Unity.Profiling.ProfilerMarker mSim = new Unity.Profiling.ProfilerMarker("Carl.Flesh.Sim"), mUp = new Unity.Profiling.ProfilerMarker("Carl.Flesh.Upload"), mNorm = new Unity.Profiling.ProfilerMarker("Carl.Flesh.Normals");

        static readonly Unity.Profiling.ProfilerMarker PM_LateUpdate = new Unity.Profiling.ProfilerMarker("Carl.FleshDeformer.LateUpdate");

        void LateUpdate()
        {

            using var pmScope = PM_LateUpdate.Auto();
            if (colorsDirty && mesh)
            {
                mesh.colors = colors;
                colorsDirty = false;
            }
            if (exposedDirty && mesh)
            {
                mesh.SetUVs(2, exposed);
                exposedDirty = false;
            }
            if (!active || rest == null) return;
            // at full speed there is no wobble: a dent goes straight to its shape. The springing flesh is only
            // simulated in slow motion, where you can actually see it (and the frame can afford it)
            if (!SlowMo)
            {
                for (int k = 0; k < moving.Count; k++) { int i = moving[k]; cur[i] = target[i]; vel[i] = Vector3.zero; isMoving[i] = false; }
                moving.Clear();
                mesh.vertices = cur;
                // the shading: a few parts a frame (the rest keep their old shading a frame or two longer)
                if (normalsFrame != Time.frameCount) { normalsFrame = Time.frameCount; normalsLeft = 4; }
                if (normalsLeft <= 0) return;
                normalsLeft--;
                UpdateNormals();
                mesh.RecalculateBounds();
                active = false;
                FollowLayers();
                return;
            }
            // a frame's worth of flesh to spring back is shared out: past it, a part waits a frame and catches up
            // with the time it missed (a blast sets every vertex on every body wobbling at once)
            if (simFrame != Time.frameCount) { simFrame = Time.frameCount; simLeft = SimBudget; }
            if (simLeft <= 0 && owed < 0.1f) { owed += Time.deltaTime; return; }
            simLeft -= moving.Count;
            float dt = Mathf.Min(Time.deltaTime + owed, 1f / 15f);
            owed = 0f;
            if (dt <= 0) return;
            float maxV = 0f;
            mSim.Begin();
            int steps = dt > 1f / 30f ? 4 : 2;
            float h = dt / steps;
            // only the vertices still springing back (the dented and wobbling ones), not the whole mesh
            for (int s = 0; s < steps; s++)
                for (int k = 0; k < moving.Count; k++)
                {
                    int i = moving[k];
                    Vector3 a = (target[i] - cur[i]) * Stiffness - vel[i] * Damping;
                    vel[i] += a * h;
                    cur[i] += vel[i] * h;
                }
            for (int k = moving.Count - 1; k >= 0; k--)
            {
                int i = moving[k];
                float v = vel[i].sqrMagnitude;
                if (v > maxV) maxV = v;
                // settled: put it exactly where it rests and stop working on it
                if (v < 1e-8f && (target[i] - cur[i]).sqrMagnitude < 1e-9f) { cur[i] = target[i]; vel[i] = Vector3.zero; isMoving[i] = false; moving[k] = moving[moving.Count - 1]; moving.RemoveAt(moving.Count - 1); }
            }
            mSim.End(); mUp.Begin();
            mesh.vertices = cur;
            mUp.End(); mNorm.Begin();
            // the shading follows a frame or two behind: only a few parts work out their normals each frame
            if (normalsFrame != Time.frameCount) { normalsFrame = Time.frameCount; normalsLeft = 4; }
            if (++normalFrame % 2 == 0 && normalsLeft > 0) { normalsLeft--; UpdateNormals(); }
            mNorm.End();
            if (maxV < 1e-6f)
            {
                UpdateNormals();
                mesh.RecalculateBounds();
                active = false;
                FollowLayers();
            }
        }

        readonly System.Collections.Generic.List<(Mesh m, Vector3[] rest, float inset)> layers = new System.Collections.Generic.List<(Mesh, Vector3[], float)>();
        Vector3[] layerWork;

        /// <summary>
        /// The fat / muscle layers under the skin are pushed in with it (a bit less the deeper they are), so a
        /// dented skin never sinks through them and leaves them standing as loose sheets in the wound.
        /// </summary>
        void FollowLayers()
        {
            if (part == null || part.fleshRoot == null || rest == null) return;
            if (layers.Count == 0)
                foreach (var mf in part.fleshRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh;
                    if (m == null || m.vertexCount != rest.Length) continue;
                    // (shared by every Earl until now: this one's own copy, then dent it)
                    if (HumanBody.SharedMeshes.Contains(m)) { m = Instantiate(m); m.MarkDynamic(); mf.sharedMesh = m; }
                    var mr = mf.GetComponent<MeshRenderer>();
                    float inset = mr && mr.sharedMaterial ? mr.sharedMaterial.GetFloat("_Inset") : 0.01f;
                    layers.Add((m, m.vertices, inset));
                }
            if (layerWork == null || layerWork.Length != rest.Length) layerWork = new Vector3[rest.Length];
            foreach (var (m, lr, inset) in layers)
            {
                float k = Mathf.Clamp01(1f - inset / 0.08f);
                for (int i = 0; i < lr.Length; i++) layerWork[i] = lr[i] + (target[i] - rest[i]) * k;
                m.vertices = layerWork;
                m.RecalculateBounds();
            }
        }
    }
}
