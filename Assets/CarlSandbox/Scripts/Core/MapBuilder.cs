using UnityEngine;

namespace Carl
{
    /// <summary>Builds the two test maps out of primitives.</summary>
    public static class MapBuilder
    {
        public static readonly string[] Names = { "THE LAB", "THE YARD", "HOUSES", "TRENCHES" };

        static Material wall, wallDark, floorLab, floorYard, white, orange, concrete, metal, crate, sand, glass;
        static Font font;
        static Material textMat;

        static void InitMaterials()
        {
            if (wall != null) return;
            wall = Mats.Lit(new Color(0.66f, 0.68f, 0.71f), 0.15f);
            wallDark = Mats.Lit(new Color(0.36f, 0.38f, 0.41f), 0.2f);
            white = Mats.Lit(new Color(0.84f, 0.85f, 0.87f), 0.3f);
            orange = Mats.Emissive(new Color(1f, 0.38f, 0.12f), 1.2f);
            concrete = Mats.Lit(new Color(0.58f, 0.58f, 0.56f), 0.1f);
            metal = Mats.Lit(new Color(0.3f, 0.32f, 0.35f), 0.4f, 0.4f);
            crate = Mats.Lit(new Color(0.95f, 0.45f, 0.18f), 0.25f);
            sand = Mats.Lit(new Color(0.62f, 0.56f, 0.42f), 0.05f);
            glass = Mats.Lit(new Color(0.15f, 0.22f, 0.26f), 0.9f);
            var grid = TexGen.Grid(new Color(0.64f, 0.67f, 0.71f), new Color(0.88f, 0.9f, 0.93f), 256, 4);
            floorLab = Mats.LitTextured(grid, Vector2.one, Color.white, 0.25f);
            var grid2 = TexGen.Grid(new Color(0.6f, 0.63f, 0.6f), new Color(0.8f, 0.82f, 0.8f), 256, 3);
            floorYard = Mats.LitTextured(grid2, Vector2.one, Color.white, 0.1f);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static GameObject Box(Transform root, string name, Vector3 center, Vector3 size, Material m, float rotY = 0f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(root, false);
            g.transform.localPosition = center;
            g.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            g.isStatic = true;
            return g;
        }

        static GameObject Floor(Transform root, Vector3 center, Vector2 size, Material baseMat)
        {
            var g = Box(root, "Floor", center + Vector3.down * 1f, new Vector3(size.x, 2f, size.y), baseMat);
            var m = new Material(baseMat);
            m.SetTextureScale("_BaseMap", size);
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        static void Text(Transform root, string s, Vector3 pos, Quaternion rot, float height, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var g = new GameObject("Text " + s);
            g.transform.SetParent(root, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            var tm = g.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = s;
            tm.fontSize = 96;
            tm.characterSize = height * 10f / 96f;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            // depth-tested, so walls and bodies hide it
            if (textMat == null) { textMat = new Material(Mats.Find("Carl/WorldText")); textMat.mainTexture = font.material.mainTexture; }
            g.GetComponent<MeshRenderer>().sharedMaterial = textMat;
        }

        static void Stairs(Transform root, Vector3 bottom, float height, int steps, float width, float depthPerStep, float rotY, Material m)
        {
            var parent = new GameObject("Stairs").transform;
            parent.SetParent(root, false);
            parent.localPosition = bottom;
            parent.localRotation = Quaternion.Euler(0, rotY, 0);
            float stepH = height / steps;
            for (int i = 0; i < steps; i++)
            {
                float h = stepH * (i + 1);
                Box(parent, "Step", new Vector3(0, h * 0.5f, depthPerStep * (i + 0.5f)), new Vector3(width, h, depthPerStep), m);
            }
        }

        static void Crate(Transform root, Vector3 pos, float size, float mass)
        {
            var g = Box(root, "Crate", pos, Vector3.one * size, crate);
            g.isStatic = false;
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public static Transform Build(int index, out Vector3 playerSpawn, out float playerYaw, out Vector3[] carlSpawns)
        {
            InitMaterials();
            var root = new GameObject("Map_" + Names[index]).transform;
            if (index == 0) BuildLab(root, out playerSpawn, out playerYaw, out carlSpawns);
            else if (index == 1) BuildYard(root, out playerSpawn, out playerYaw, out carlSpawns);
            else if (index == 2) Houses.Neighbourhood(root, out playerSpawn, out playerYaw, out carlSpawns);
            else BuildTrenches(root, out playerSpawn, out playerYaw, out carlSpawns);
            // the trenches: an overcast grey haze
            RenderSettings.fog = index == 3;
            if (index == 3) { RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.014f; RenderSettings.fogColor = new Color(0.62f, 0.63f, 0.6f); }
            Batch(root);
            return root;
        }

        /// <summary>
        /// Everything in the map that never moves is merged into a few big meshes: far fewer draw calls, the same
        /// map (crates, glass, text and anything with a script stay separate).
        /// </summary>
        static void Batch(Transform root)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                var go = mr.gameObject;
                if (go.GetComponentInParent<Rigidbody>() != null || go.GetComponent<TextMesh>() != null || go.GetComponent<MeshFilter>() == null) continue;
                if (go.GetComponents<MonoBehaviour>().Length > 0) continue;
                list.Add(go);
            }
            if (list.Count > 1) StaticBatchingUtility.Combine(list.ToArray(), root.gameObject);
        }

        // ================================================================ TRENCHES
        //
        // Modern trench lines as dug in Ukraine: narrow slots a metre wide and deeper than a man, cut in zig-zags
        // through the tree lines that break up the fields, fighting positions pushed out toward the enemy, dugouts
        // roofed over with logs and earth, the dug earth heaped in spoil along both lips, leaf litter everywhere.
        // Two lines face each other across a cratered field. The ground itself is modelled in Blender
        // (BlenderTools/trench_build.py -> Resources/Models/Trench.bytes, textures painted by trench_textures.py);
        // this places what stands on it. Trench floors are at y = 0, the field about 1.8 m above.

        static Material bark, logMat, bulkBag, shrub, roofEarth, water;
        static Mesh blob;

        static GameObject Deco(Transform r, string name, Vector3 c, Vector3 size, Material m, Vector3 euler = default)
        {
            var g = Box(r, name, c, size, m);
            g.transform.localRotation = Quaternion.Euler(euler);
            Object.Destroy(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>A rounded lump (a shrub, a bag, earth on a roof): an ellipsoid, solid if asked.</summary>
        static GameObject Blob(Transform r, string name, Vector3 c, Vector3 size, Material m, float rotY, bool solid)
        {
            if (blob == null) blob = ProcMesh.Ellipsoid(Vector3.one * 0.5f, 12, 8);
            var g = new GameObject(name);
            g.transform.SetParent(r, false);
            g.transform.localPosition = c;
            g.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            g.transform.localScale = size;
            g.AddComponent<MeshFilter>().sharedMesh = blob;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (solid) { var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = blob; mc.convex = true; }
            g.isStatic = true;
            return g;
        }

        static GameObject Pole(Transform r, string name, Vector3 a, Vector3 b, float radius, Material m, bool solid)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(r, false);
            Vector3 d = b - a;
            g.transform.localPosition = (a + b) * 0.5f;
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            g.transform.localScale = new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f);
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!solid) Object.Destroy(g.GetComponent<Collider>());
            g.isStatic = true;
            return g;
        }

        class TrenchData
        {
            public Mesh mesh;
            public readonly System.Collections.Generic.List<(int kind, Vector2[] pts)> lines = new System.Collections.Generic.List<(int, Vector2[])>();
            public readonly System.Collections.Generic.List<(int kind, Vector2 c, float r)> cells = new System.Collections.Generic.List<(int, Vector2, float)>();
            public readonly System.Collections.Generic.List<(Vector2 c, float r)> craters = new System.Collections.Generic.List<(Vector2, float)>();
        }
        static TrenchData trenchData;

        /// <summary>
        /// The Blender terrain: three parts (cut earth walls, mud floors, leaf litter on top) and the layout it was
        /// dug from. Texture coordinates are made here: the walls side-on per triangle, the rest from above.
        /// </summary>
        static TrenchData LoadTrench()
        {
            // (a mesh made in play mode is destroyed when play stops: the static outlives it, so check the mesh)
            if (trenchData != null && trenchData.mesh != null) return trenchData;
            var ta = Resources.Load<TextAsset>("Models/Trench");
            if (ta == null) { Debug.LogError("[Carl] Models/Trench.bytes missing"); return null; }
            var d = new TrenchData();
            using (var br = new System.IO.BinaryReader(new System.IO.MemoryStream(ta.bytes)))
            {
                br.ReadBytes(4);
                int subs = br.ReadInt32();
                var verts = new System.Collections.Generic.List<Vector3>();
                var norms = new System.Collections.Generic.List<Vector3>();
                var uvs = new System.Collections.Generic.List<Vector2>();
                var tris = new int[subs][];
                for (int s = 0; s < subs; s++)
                {
                    int nv = br.ReadInt32(), b = verts.Count;
                    for (int i = 0; i < nv; i++) verts.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                    for (int i = 0; i < nv; i++) norms.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                    int nt = br.ReadInt32();
                    tris[s] = new int[nt * 3];
                    for (int i = 0; i < nt * 3; i++) tris[s][i] = b + br.ReadInt32();
                    for (int i = 0; i < nv; i++) { var p = verts[b + i]; uvs.Add(new Vector2(p.x, p.z) * (s == 1 ? 0.5f : 0.33f)); }
                    if (s == 0)
                        for (int i = 0; i < nt * 3; i += 3)
                        {
                            // each wall triangle has its own corners: map it on whichever side it faces most
                            Vector3 a = verts[tris[s][i]], e1 = verts[tris[s][i + 1]] - a, e2 = verts[tris[s][i + 2]] - a;
                            Vector3 n = Vector3.Cross(e1, e2);
                            bool alongZ = Mathf.Abs(n.x) > Mathf.Abs(n.z);
                            for (int k = 0; k < 3; k++) { var p = verts[tris[s][i + k]]; uvs[tris[s][i + k]] = new Vector2(alongZ ? p.z : p.x, p.y) * 0.5f; }
                        }
                }
                var m = new Mesh { name = "Trench", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs);
                m.subMeshCount = subs;
                for (int s = 0; s < subs; s++) m.SetTriangles(tris[s], s);
                m.RecalculateBounds(); m.RecalculateTangents();
                d.mesh = m;
                int nl = br.ReadInt32();
                for (int l = 0; l < nl; l++)
                {
                    int kind = br.ReadInt32(), n = br.ReadInt32();
                    var pts = new Vector2[n];
                    for (int i = 0; i < n; i++) pts[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    d.lines.Add((kind, pts));
                }
                int nc = br.ReadInt32();
                for (int c = 0; c < nc; c++) d.cells.Add((br.ReadInt32(), new Vector2(br.ReadSingle(), br.ReadSingle()), br.ReadSingle()));
                int ncr = br.ReadInt32();
                for (int c = 0; c < ncr; c++) d.craters.Add((new Vector2(br.ReadSingle(), br.ReadSingle()), br.ReadSingle()));
            }
            return trenchData = d;
        }

        static Material Tex(string name, float smooth, Color tint)
        {
            var t = Resources.Load<Texture2D>("Textures/" + name);
            if (t != null) { t.wrapMode = TextureWrapMode.Repeat; t.anisoLevel = 8; }
            return Mats.LitTextured(t, Vector2.one, tint, smooth);
        }

        static void BuildTrenches(Transform r, out Vector3 spawn, out float yaw, out Vector3[] carls)
        {
            if (bark == null)
            {
                bark = Mats.Lit(new Color(0.33f, 0.3f, 0.27f), 0.1f);
                logMat = Mats.Lit(new Color(0.4f, 0.32f, 0.22f), 0.1f);
                bulkBag = Mats.Lit(new Color(0.85f, 0.86f, 0.84f), 0.2f);
                shrub = Mats.Lit(new Color(0.22f, 0.18f, 0.12f), 0.02f);
                roofEarth = Mats.Lit(new Color(0.27f, 0.21f, 0.15f), 0.08f);
                water = Mats.Lit(new Color(0.2f, 0.17f, 0.13f), 0.93f, 0.1f);
            }
            var rng = new System.Random(2022);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var data = LoadTrench();
            spawn = new Vector3(0f, 2.2f, -30f); yaw = 0f; carls = new Vector3[0];
            if (data == null) return;

            // the ground
            var ground = new GameObject("TrenchGround");
            ground.transform.SetParent(r, false);
            ground.AddComponent<MeshFilter>().sharedMesh = data.mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterials = new[] { Tex("trench_earth", 0.06f, new Color(0.74f, 0.68f, 0.62f)), Tex("trench_mud", 0.22f, new Color(0.9f, 0.82f, 0.74f)), Tex("trench_litter", 0.04f, new Color(0.82f, 0.8f, 0.76f)) };
            var col = ground.AddComponent<MeshCollider>();
            col.sharedMesh = data.mesh;
            ground.AddComponent<Surface>().kind = SurfaceKind.Dirt;
            ground.isStatic = true;
            // solid earth under the trench floors: nothing that tunnels through the thin ground falls out of the world
            var under = Box(r, "Bedrock", new Vector3(0f, -1.25f, 0f), new Vector3(120f, 2f, 140f), roofEarth);
            under.GetComponent<MeshRenderer>().enabled = false;
            under.AddComponent<Surface>().kind = SurfaceKind.Dirt;
            Physics.SyncTransforms();
            float H(float x, float z) => col.Raycast(new Ray(new Vector3(x, 30f, z), Vector3.down), out var h, 60f) ? h.point.y : 0f;
            Vector3 G(float x, float z) => new Vector3(x, H(x, z), z);

            // tree lines along both fronts (the trenches are dug into them), thinner ones behind, a few lone trees
            void Tree(Vector3 p)
            {
                float h = Rand(6f, 13f), rad = Rand(0.08f, 0.2f);
                Vector3 lean = new Vector3(Rand(-0.15f, 0.15f), 1f, Rand(-0.15f, 0.15f)).normalized;
                Pole(r, "Trunk", p - Vector3.up * 0.3f, p + lean * h, rad, bark, true);
                int branches = rng.Next(4, 9);
                for (int b = 0; b < branches; b++)
                {
                    Vector3 from = p + lean * h * Rand(0.3f, 0.92f);
                    float a = Rand(0f, Mathf.PI * 2f);
                    Vector3 dir = new Vector3(Mathf.Cos(a), Rand(0.5f, 1.5f), Mathf.Sin(a)).normalized;
                    float len = Rand(1.2f, 3.4f);
                    Pole(r, "Branch", from, from + dir * len, rad * Rand(0.2f, 0.4f), bark, false);
                    // and twigs off the branch
                    if (rng.NextDouble() < 0.6)
                    {
                        Vector3 f2 = from + dir * len * Rand(0.4f, 0.8f);
                        Vector3 d2 = (dir + new Vector3(Rand(-0.8f, 0.8f), Rand(0f, 0.6f), Rand(-0.8f, 0.8f))).normalized;
                        Pole(r, "Twig", f2, f2 + d2 * Rand(0.6f, 1.4f), rad * 0.12f, bark, false);
                    }
                }
            }
            bool Open(float x, float z, float min = 1.5f) => H(x, z) > min;   // not in a trench or crater
            foreach (var (zc, w, count) in new[] { (-22f, 7f, 85), (24f, 7f, 85), (-44f, 5f, 30), (46f, 5f, 30) })
                for (int k = 0; k < count; k++)
                {
                    float x = Rand(-47f, 47f), z = zc + Rand(-w, w);
                    if (!Open(x, z) || !Open(x + 0.4f, z) || !Open(x - 0.4f, z) || !Open(x, z + 0.4f) || !Open(x, z - 0.4f)) continue;
                    Tree(G(x, z));
                    // (the round scrub blobs that used to go here looked like stray spheres: gone; the numbers are
                    // still drawn so the trees stay where they were)
                    if (rng.NextDouble() < 0.12) { Rand(-1.5f, 1.5f); Rand(-1.5f, 1.5f); Rand(0.8f, 1.8f); Rand(0.5f, 1f); Rand(0.8f, 1.8f); Rand(0f, 180f); }
                }
            for (int k = 0; k < 8; k++) { float x = Rand(-44f, 44f), z = Rand(-12f, 12f); if (Open(x, z)) Tree(G(x, z)); }

            Vector2 Point(Vector2[] pts) { int i = rng.Next(pts.Length - 1); return Vector2.Lerp(pts[i], pts[i + 1], Rand(0f, 1f)); }
            var mains = data.lines.FindAll(l => l.kind <= 2);
            // dugouts: logs laid across the hole, earth heaped on top
            foreach (var (kind, c, rad) in data.cells)
            {
                if (kind != 1) continue;
                float top = Mathf.Max(H(c.x + rad + 0.6f, c.y), H(c.x - rad - 0.6f, c.y));
                float y = Mathf.Min(top, 2f) - 0.05f;
                for (float zz = -rad - 0.2f; zz <= rad + 0.2f; zz += 0.23f)
                    Pole(r, "RoofLog", new Vector3(c.x - rad - 0.7f, y + Rand(-0.03f, 0.03f), c.y + zz), new Vector3(c.x + rad + 0.7f, y + Rand(-0.03f, 0.03f), c.y + zz + Rand(-0.1f, 0.1f)), Rand(0.09f, 0.12f), logMat, true);
                Blob(r, "RoofEarth", new Vector3(c.x, y + 0.12f, c.y), new Vector3(rad * 2f + 1f, 0.55f, rad * 2f + 0.6f), roofEarth, Rand(-10f, 10f), true);
                Rand(-0.4f, 0.4f); Rand(-0.4f, 0.4f);   // (where the crate used to go: keeps the rest of the layout the same)
            }
            // camouflage netting draped over most of the fighting positions, sagging into the hole
            var net = Tex("trench_net", 0.05f, Color.white);
            net.SetFloat("_AlphaClip", 1f); net.SetFloat("_Cutoff", 0.5f); net.EnableKeyword("_ALPHATEST_ON");
            net.SetFloat("_Cull", 0f); net.renderQueue = 2450;
            foreach (var (kind, c, rad) in data.cells)
            {
                if (kind != 0 || rng.NextDouble() < 0.3) continue;
                const int N = 12; float half = rad + 1.3f;
                var vs = new Vector3[N * N]; var uv = new Vector2[N * N]; var tri = new System.Collections.Generic.List<int>();
                float top = 0f;
                for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; top = Mathf.Max(top, H(c.x + Mathf.Cos(a) * half, c.y + Mathf.Sin(a) * half)); }
                float rot = Rand(0f, Mathf.PI);
                for (int i = 0; i < N; i++)
                    for (int j = 0; j < N; j++)
                    {
                        float u = i / (N - 1f) * 2f - 1f, v = j / (N - 1f) * 2f - 1f;
                        float x = (u * Mathf.Cos(rot) - v * Mathf.Sin(rot)) * half, z = (u * Mathf.Sin(rot) + v * Mathf.Cos(rot)) * half;
                        float q = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                        float y = Mathf.Max(H(c.x + x, c.y + z) + 0.06f, top + 0.15f - 0.55f * (1f - q * q) + Rand(-0.05f, 0.05f));
                        vs[i * N + j] = new Vector3(c.x + x, y, c.y + z);
                        uv[i * N + j] = new Vector2(u, v) * half * 0.5f;
                        if (i < N - 1 && j < N - 1) { int a = i * N + j; tri.AddRange(new[] { a, a + 1, a + N, a + 1, a + N + 1, a + N }); }
                    }
                var nm = new Mesh { name = "CamoNet", vertices = vs, uv = uv, triangles = tri.ToArray() };
                nm.RecalculateNormals();
                var go = new GameObject("CamoNet");
                go.transform.SetParent(r, false);
                go.AddComponent<MeshFilter>().sharedMesh = nm;
                go.AddComponent<MeshRenderer>().sharedMaterial = net;
                go.isStatic = true;
            }
            // branches fallen across the trenches, puddles in the low spots
            for (int k = 0; k < 45; k++)
            {
                var p = Point(mains[rng.Next(mains.Count)].pts);
                float a = Rand(0f, Mathf.PI);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Rand(1f, 2f);
                Vector3 c = new Vector3(p.x, 0f, p.y);
                Vector3 e0 = G(c.x - d.x, c.z - d.z) + Vector3.up * 0.05f, e1 = G(c.x + d.x, c.z + d.z) + Vector3.up * 0.05f;
                if (e0.y > 1.2f && e1.y > 1.2f) Pole(r, "FallenBranch", e0, e1, Rand(0.025f, 0.07f), bark, false);
            }
            for (int k = 0; k < 70; k++)
            {
                var p = Point(mains[rng.Next(mains.Count)].pts);
                float y = H(p.x, p.y);
                if (y < 0f) Deco(r, "Puddle", new Vector3(p.x, y + 0.015f, p.y), new Vector3(Rand(0.5f, 0.9f), 0.01f, Rand(0.6f, 1.4f)), water, new Vector3(0f, Rand(0f, 180f), 0f));
            }
            // white bulk bags dumped behind the positions
            for (int k = 0; k < 12; k++)
            {
                var line = data.lines[k % 2].pts;
                var p = Point(line);
                float z = p.y + (k % 2 == 0 ? -1f : 1f) * Rand(3f, 6f);
                // (the white bulk bags read as stray spheres all over the map: gone, numbers still drawn)
                if (Open(p.x, z)) { Rand(0.9f, 1.3f); Rand(0.6f, 0.9f); Rand(0.9f, 1.3f); Rand(0f, 180f); }
            }

            // you start in your own front line, the Earls in theirs and in their fighting positions
            var friendly = data.lines.Find(l => l.kind == 0).pts;
            var enemy = data.lines.Find(l => l.kind == 1).pts;
            Vector2 sp = Vector2.Lerp(friendly[friendly.Length / 2], friendly[friendly.Length / 2 + 1], 0.5f);
            spawn = new Vector3(sp.x, H(sp.x, sp.y) + 0.1f, sp.y);
            yaw = 0f;
            // the trench system is held by armed Earls (TrenchSquad): every spot along every trench floor, every
            // fighting position and dugout, is somewhere one of them could be
            TrenchSpots.Clear();
            foreach (var (kind, pts) in data.lines)
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    float len = Vector2.Distance(pts[i], pts[i + 1]);
                    for (float s = 0.5f; s < len; s += 2.5f)
                    {
                        var p = Vector2.Lerp(pts[i], pts[i + 1], s / len);
                        float y = H(p.x, p.y);
                        if (y < 0.5f) TrenchSpots.Add(new Vector3(p.x, y + 0.05f, p.y));
                    }
                }
            foreach (var (kind, c, rad) in data.cells) { float y = H(c.x, c.y); if (y < 0.5f) TrenchSpots.Add(new Vector3(c.x, y + 0.05f, c.y)); }
            carls = new Vector3[0];
        }

        /// <summary>Points on the trench floors of the TRENCHES map (filled when it is built).</summary>
        public static readonly System.Collections.Generic.List<Vector3> TrenchSpots = new System.Collections.Generic.List<Vector3>();

        // ================================================================ THE LAB

        static void BuildLab(Transform r, out Vector3 spawn, out float yaw, out Vector3[] carls)
        {
            float W = 60f, D = 44f, H = 9f;
            Floor(r, Vector3.zero, new Vector2(W, D), floorLab);
            // walls
            Box(r, "WallN", new Vector3(0, H / 2, D / 2), new Vector3(W, H, 0.5f), wall);
            Box(r, "WallS", new Vector3(0, H / 2, -D / 2), new Vector3(W, H, 0.5f), wall);
            Box(r, "WallE", new Vector3(W / 2, H / 2, 0), new Vector3(0.5f, H, D), wall);
            Box(r, "WallW", new Vector3(-W / 2, H / 2, 0), new Vector3(0.5f, H, D), wall);
            // dark lower panels + orange trim
            Box(r, "PanelN", new Vector3(0, 2.2f, D / 2 - 0.3f), new Vector3(W - 1, 4.4f, 0.1f), wallDark);
            Box(r, "PanelE", new Vector3(W / 2 - 0.3f, 2.2f, 0), new Vector3(0.1f, 4.4f, D - 1), wallDark);
            Box(r, "TrimN", new Vector3(0, 4.45f, D / 2 - 0.36f), new Vector3(W - 1, 0.06f, 0.05f), orange);
            Box(r, "TrimE", new Vector3(W / 2 - 0.36f, 4.45f, 0), new Vector3(0.05f, 0.06f, D - 1), orange);
            // roof (does not block the sun's light so the lab stays bright)
            var roof = Box(r, "Roof", new Vector3(0, H + 0.25f, 0), new Vector3(W + 1f, 0.5f, D + 1f), wall);
            roof.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // ceiling light strips
            for (int i = -3; i <= 3; i++)
                Box(r, "Light", new Vector3(i * 7f, H - 0.3f, 4f), new Vector3(0.5f, 0.15f, 22f), Mats.Emissive(Color.white, 2.5f), 15f);
            // sign
            Box(r, "Sign", new Vector3(0, 6.6f, D / 2 - 0.35f), new Vector3(9f, 2.2f, 0.12f), white);
            Text(r, "EXPERIMENTAL ASSISTANTS\nFOR RESEARCH AND LEARNING", new Vector3(0, 7.05f, D / 2 - 0.43f), Quaternion.identity, 0.32f, new Color(0.15f, 0.15f, 0.17f));
            Text(r, "THE LAB", new Vector3(0, 6.05f, D / 2 - 0.43f), Quaternion.identity, 0.55f, new Color(0.1f, 0.1f, 0.1f));
            // floor lanes and numbers
            for (int i = -2; i <= 2; i++)
            {
                Box(r, "Lane", new Vector3(i * 8f + 4f, 0.005f, 0), new Vector3(0.09f, 0.01f, D - 4), orange);
                Text(r, $"0{i + 3}", new Vector3(i * 8f, 0.02f, -6f), Quaternion.Euler(90, 0, 0), 1.2f, new Color(1, 1, 1, 0.9f));
            }
            Box(r, "Line", new Vector3(0, 0.005f, -12f), new Vector3(W - 4, 0.01f, 0.09f), orange);
            // dark floor pads
            Box(r, "Pad1", new Vector3(-8f, 0.004f, 6f), new Vector3(4f, 0.01f, 3f), wallDark);
            Box(r, "Pad2", new Vector3(10f, 0.004f, -2f), new Vector3(3f, 0.01f, 5f), wallDark);
            // cover walls
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(-14f + i * 14f, 1.25f, 9f + (i % 2) * 4f);
                Box(r, "Cover", pos, new Vector3(4.5f, 2.5f, 0.3f), white);
                Text(r, $"COVER / 0{i + 1}", pos + new Vector3(0, 0.6f, -0.17f), Quaternion.identity, 0.35f, new Color(0.3f, 0.3f, 0.32f));
            }
            // raised platform with stairs (east side)
            Box(r, "Platform", new Vector3(20f, 1.2f, 12f), new Vector3(12f, 2.4f, 10f), white);
            Box(r, "PlatformEdge", new Vector3(20f, 2.42f, 6.95f), new Vector3(12f, 0.04f, 0.1f), orange);
            Stairs(r, new Vector3(20f, 0f, -1.6f), 2.4f, 12, 4f, 0.72f, 0f, white);
            // white tables
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector3(-20f + i * 4f, 0f, -4f);
                Box(r, "Table", c + new Vector3(0, 0.85f, 0), new Vector3(2.2f, 0.08f, 1f), white);
                Box(r, "Leg", c + new Vector3(0, 0.42f, 0), new Vector3(1.8f, 0.84f, 0.1f), metal);
            }
            // ramp
            var ramp = Box(r, "Ramp", new Vector3(-22f, 0.9f, 14f), new Vector3(5f, 0.3f, 8f), white);
            ramp.transform.localRotation = Quaternion.Euler(-13f, 0, 0);
            // a hanging wall for splatter tests
            Box(r, "SplatterWall", new Vector3(0f, 2f, 18f), new Vector3(8f, 4f, 0.3f), white);
            // physics crates
            for (int i = 0; i < 6; i++) Crate(r, new Vector3(-26f + (i % 3) * 1.1f, 0.5f + (i / 3) * 1.01f, -16f), 1f, 25f);

            spawn = new Vector3(0, 0.1f, -18f);
            yaw = 0f;
            carls = new[] { new Vector3(-8f, 0, 0f), new Vector3(0f, 0, 3f), new Vector3(8f, 0, 0f), new Vector3(4f, 0, 10f) };
        }

        // ================================================================ THE YARD

        static void BuildYard(Transform r, out Vector3 spawn, out float yaw, out Vector3[] carls)
        {
            float W = 140f, D = 300f;
            Floor(r, new Vector3(0, 0, 90f), new Vector2(W, D), floorYard);
            // perimeter berms
            Box(r, "BermN", new Vector3(0, 4f, 240f), new Vector3(W, 8f, 4f), sand);
            Box(r, "BermE", new Vector3(W / 2, 2f, 90f), new Vector3(3f, 4f, D), sand);
            Box(r, "BermW", new Vector3(-W / 2, 2f, 90f), new Vector3(3f, 4f, D), sand);
            Box(r, "BermS", new Vector3(0, 2f, -60f), new Vector3(W, 4f, 3f), sand);

            // firing line
            Box(r, "Bench", new Vector3(0, 0.5f, -48f), new Vector3(20f, 1f, 0.8f), concrete);
            Box(r, "FiringLine", new Vector3(0, 0.005f, -47f), new Vector3(30f, 0.01f, 0.12f), orange);
            int[] marks = { 25, 50, 100, 150, 200 };
            foreach (var m in marks)
            {
                float z = -48f + m;
                Box(r, "Marker", new Vector3(-18f, 1.5f, z), new Vector3(3f, 2f, 0.15f), white);
                Text(r, $"{m} M", new Vector3(-18f, 1.5f, z - 0.09f), Quaternion.identity, 0.9f, new Color(0.9f, 0.3f, 0.1f));
                Box(r, "Dist", new Vector3(0, 0.005f, z), new Vector3(30f, 0.01f, 0.1f), orange);
            }
            // lane dividers
            for (int i = -2; i <= 2; i++) Box(r, "Divider", new Vector3(i * 6f + 3f, 0.4f, -20f), new Vector3(0.2f, 0.8f, 50f), concrete);

            // splatter wall
            Box(r, "SplatterWall", new Vector3(10f, 2f, -28f), new Vector3(8f, 4f, 0.3f), white);
            Text(r, "SPLATTER WALL", new Vector3(10f, 3.6f, -28.17f), Quaternion.identity, 0.35f, new Color(0.3f, 0.3f, 0.32f));

            // sniper tower
            Box(r, "TowerDeck", new Vector3(-14f, 6f, -45f), new Vector3(5f, 0.3f, 5f), concrete);
            for (int k = 0; k < 4; k++)
                Box(r, "TowerLeg", new Vector3(-14f + (k % 2 == 0 ? -2.2f : 2.2f), 3f, -45f + (k < 2 ? -2.2f : 2.2f)), new Vector3(0.3f, 6f, 0.3f), metal);
            Box(r, "TowerRail", new Vector3(-14f, 6.7f, -42.6f), new Vector3(5f, 1f, 0.15f), metal);
            Stairs(r, new Vector3(-14f, 0f, -56.7f), 6f, 24, 1.6f, 0.38f, 0f, concrete);
            Box(r, "TowerLanding", new Vector3(-14f, 5.9f, -47.5f), new Vector3(1.6f, 0.2f, 0.4f), concrete);

            // shipping containers
            Color[] cc = { new Color(0.7f, 0.2f, 0.15f), new Color(0.15f, 0.35f, 0.6f), new Color(0.2f, 0.45f, 0.25f) };
            Vector3[] cp = { new Vector3(-25f, 1.3f, 10f), new Vector3(-28f, 1.3f, 40f), new Vector3(12f, 1.3f, 70f), new Vector3(-12f, 1.3f, 110f), new Vector3(-12f, 3.9f, 110f) };
            for (int i = 0; i < cp.Length; i++)
                Box(r, "Container", cp[i], new Vector3(2.45f, 2.6f, 6.1f), Mats.Lit(cc[i % 3], 0.3f, 0.3f), i * 37f);
            // sandbags and barriers
            for (int i = 0; i < 6; i++)
                Box(r, "Sandbags", new Vector3(-6f + i * 2.4f, 0.45f, 20f + (i % 2) * 0.4f), new Vector3(2.3f, 0.9f, 0.7f), sand, (i % 2) * 6f);
            for (int i = 0; i < 4; i++)
                Box(r, "Barrier", new Vector3(18f, 0.6f, 0f + i * 3.5f), new Vector3(0.6f, 1.2f, 3f), concrete);
            for (int i = 0; i < 8; i++) Crate(r, new Vector3(5f + (i % 4) * 1.1f, 0.5f + (i / 4) * 1.01f, -38f), 1f, 25f);

            spawn = new Vector3(0, 0.1f, -52f);
            yaw = 0f;
            carls = new[] { new Vector3(-3f, 0, -30f), new Vector3(4f, 0, -22f), new Vector3(0f, 0, 2f), new Vector3(-6f, 0, 52f), new Vector3(3f, 0, 100f) };
        }

        static void BuildHouse(Transform root, Vector3 o)
        {
            var r = new GameObject("House").transform;
            r.SetParent(root, false);
            r.localPosition = o;
            float w = 12f, d = 10f, h = 3.2f, t = 0.25f;
            Box(r, "Slab", new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), concrete);
            for (int floor = 0; floor < 2; floor++)
            {
                float y = floor * h;
                // back wall
                Box(r, "Wall", new Vector3(0, y + h / 2, d / 2), new Vector3(w, h, t), concrete);
                // side walls with a window
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(r, "WallSide", new Vector3(s * w / 2, y + h / 2, -d / 4 - 1f), new Vector3(t, h, d / 2 - 2f), concrete);
                    Box(r, "WallSide", new Vector3(s * w / 2, y + h / 2, d / 4 + 1f), new Vector3(t, h, d / 2 - 2f), concrete);
                    Box(r, "Sill", new Vector3(s * w / 2, y + 0.5f, 0), new Vector3(t, 1f, 2f), concrete);
                    Box(r, "Lintel", new Vector3(s * w / 2, y + h - 0.35f, 0), new Vector3(t, 0.7f, 2f), concrete);
                }
                // front wall with door (ground) / window (upper)
                Box(r, "FrontL", new Vector3(-w / 4 - 0.75f, y + h / 2, -d / 2), new Vector3(w / 2 - 1.5f, h, t), concrete);
                Box(r, "FrontR", new Vector3(w / 4 + 0.75f, y + h / 2, -d / 2), new Vector3(w / 2 - 1.5f, h, t), concrete);
                Box(r, "FrontTop", new Vector3(0, y + h - 0.4f, -d / 2), new Vector3(3f, 0.8f, t), concrete);
                if (floor == 1) Box(r, "FrontSill", new Vector3(0, y + 0.5f, -d / 2), new Vector3(3f, 1f, t), concrete);
            }
            // upper floor with stairwell gap
            Box(r, "Upper", new Vector3(-1.5f, h, 0), new Vector3(w - 3f, 0.25f, d), concrete);
            Box(r, "UpperStrip", new Vector3(w / 2 - 1.5f, h, -d / 2 + 2.5f), new Vector3(3f, 0.25f, 5f), concrete);
            Stairs(r, new Vector3(w / 2 - 1.5f, 0f, 0.1f), h, 13, 2.6f, 0.36f, 0f, concrete);
            Box(r, "Roof", new Vector3(0, 2 * h, 0), new Vector3(w + 0.4f, 0.25f, d + 0.4f), concrete);
            Box(r, "Glass", new Vector3(-w / 2 - 0.02f, h + 1.6f, 0), new Vector3(0.02f, 1.2f, 1.9f), glass).GetComponent<Collider>().enabled = false;
        }
    }
}
