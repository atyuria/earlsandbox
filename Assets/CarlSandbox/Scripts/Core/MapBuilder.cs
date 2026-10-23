using UnityEngine;

namespace Carl
{
    /// <summary>Builds the two test maps out of primitives.</summary>
    public static partial class MapBuilder
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

        // ------------------------------------------------------------ Poly Haven surfaces and props (CC0)
        // Surfaces: Resources/Textures/ph/<name>_diff / _nor (colour and OpenGL normal map, 1K). Props: Resources/Props
        // (FBX made from the glTFs by BlenderTools/ph_props.py), real-world size, pivot on the ground.
        static readonly System.Collections.Generic.Dictionary<string, Material> phMats = new System.Collections.Generic.Dictionary<string, Material>();

        /// <summary>A Poly Haven surface, one texture repeat every <paramref name="metres"/> on a 1 m UV (tile per object with Tiled).</summary>
        static Material PH(string name, float smooth = 0.15f, Color? tint = null, float bump = 1f)
        {
            string key = name + smooth + tint + bump;
            if (phMats.TryGetValue(key, out var m) && m != null) return m;
            var diff = Resources.Load<Texture2D>("Textures/ph/" + name + "_diff");
            var nor = Resources.Load<Texture2D>("Textures/ph/" + name + "_nor");
            if (diff != null) { diff.wrapMode = TextureWrapMode.Repeat; diff.anisoLevel = 8; }
            m = Mats.LitTextured(diff, Vector2.one, tint ?? Color.white, smooth);
            if (nor != null) { nor.wrapMode = TextureWrapMode.Repeat; m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            return phMats[key] = m;
        }

        /// <summary>This box's own copy of the material, repeating the texture every <paramref name="metres"/> along its faces.</summary>
        static GameObject Tiled(GameObject g, float metres)
        {
            var mr = g.GetComponent<MeshRenderer>();
            var s = g.transform.localScale;
            var m = new Material(mr.sharedMaterial);
            var t = new Vector2(Mathf.Max(s.x, s.z) / metres, Mathf.Max(s.y, Mathf.Min(s.x, s.z)) / metres);
            m.SetTextureScale("_BaseMap", t); m.SetTextureScale("_BumpMap", t);
            mr.sharedMaterial = m;
            return g;
        }

        static readonly System.Collections.Generic.Dictionary<string, GameObject> props = new System.Collections.Generic.Dictionary<string, GameObject>();

        /// <summary>How far a prop reaches out from its pivot across the ground (m, at scale 1).</summary>
        static float PropRadius(string id)
        {
            if (!props.TryGetValue(id, out var pf) || pf == null) props[id] = pf = Resources.Load<GameObject>("Props/" + id);
            if (pf == null) return 0.5f;
            float r = 0.2f;
            foreach (var mf in pf.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;   // (the prefab sits at the origin)
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z));
                    r = Mathf.Max(r, new Vector2(p.x, p.z).magnitude);
                }
            }
            return r;
        }

        // The props' textures: Resources/PropTex/<id>/ with <id>.json naming each material's colour and normal maps
        // and whether it is cut out (leaves, grass) and two-sided (BlenderTools/ph_proptex.js).
        [System.Serializable] class PropTexMat { public string name, diff, nor; public bool cut, two; }
        [System.Serializable] class PropTexList { public PropTexMat[] mats; }
        static readonly System.Collections.Generic.Dictionary<string, Material> propMats = new System.Collections.Generic.Dictionary<string, Material>();

        static Material[] PropMaterials(string id, Material[] src)
        {
            var ta = Resources.Load<TextAsset>("PropTex/" + id);
            if (ta == null) return src;
            var list = JsonUtility.FromJson<PropTexList>(ta.text);
            if (list?.mats == null || list.mats.Length == 0) return src;
            var res = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                string name = src[i] != null ? src[i].name : "";
                string key = id + "/" + name;
                if (propMats.TryGetValue(key, out var m) && m != null) { res[i] = m; continue; }
                var e = System.Array.Find(list.mats, x => x.name == name) ?? list.mats[0];
                var diff = string.IsNullOrEmpty(e.diff) ? null : Resources.Load<Texture2D>("PropTex/" + id + "/" + e.diff);
                m = Mats.LitTextured(diff, Vector2.one, Color.white, 0.25f);
                var nor = string.IsNullOrEmpty(e.nor) ? null : Resources.Load<Texture2D>("PropTex/" + id + "/" + e.nor);
                if (nor != null) { m.SetTexture("_BumpMap", nor); m.EnableKeyword("_NORMALMAP"); }
                if (e.cut) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; }
                if (e.two) m.SetFloat("_Cull", 0f);
                if (id == "cement_bag") m.SetColor("_BaseColor", new Color(0.62f, 0.6f, 0.45f));   // (printed paper sacks: tinted to read as hessian sandbags)
                res[i] = propMats[key] = m;
            }
            return res;
        }

        /// <summary>A Poly Haven prop stood at <paramref name="pos"/> (local to <paramref name="r"/>); solid ones get mesh colliders.</summary>
        static GameObject Prop(Transform r, string id, Vector3 pos, float yaw = 0f, float scale = 1f, bool solid = true, Vector3 tilt = default)
        {
            if (!props.TryGetValue(id, out var pf) || pf == null) props[id] = pf = Resources.Load<GameObject>("Props/" + id);
            if (pf == null) return null;
            var g = Object.Instantiate(pf, r, false);
            g.name = id;
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(tilt.x, yaw, tilt.z);
            g.transform.localScale = Vector3.one * scale;
            foreach (var mr in g.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterials = PropMaterials(id, mr.sharedMaterials);
            // one box round the whole prop for its collision (the scanned meshes run to 100k triangles, and the
            // bodies rolling about on them were most of the physics time); small bits and plants cast no shadow
            var w2l = g.transform.worldToLocalMatrix;
            Bounds box = default; bool any = false;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            {
                mf.gameObject.isStatic = true;
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds; var m = w2l * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z));
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; } else box.Encapsulate(p);
                }
            }
            if (solid && any) { var bc = g.AddComponent<BoxCollider>(); bc.center = box.center; bc.size = box.size; }
            if (!solid || box.size.magnitude * scale < 0.9f)
                foreach (var mr in g.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            Ladder.All.Clear(); TrenchSpots.Clear(); TrenchNav.Clear();
            var root = new GameObject("Map_" + Names[index]).transform;
            if (index == 0) BuildLab(root, out playerSpawn, out playerYaw, out carlSpawns);
            else if (index == 1) BuildYard(root, out playerSpawn, out playerYaw, out carlSpawns);
            else if (index == 2) Houses.Neighbourhood(root, out playerSpawn, out playerYaw, out carlSpawns);
            else BuildTrenches(root, out playerSpawn, out playerYaw, out carlSpawns);
            // the trenches: an overcast grey haze
            RenderSettings.fog = index == 3;
            if (index == 3) { RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.014f; RenderSettings.fogColor = new Color(0.62f, 0.63f, 0.6f); }
            // thin debris (twigs, wire strands, splinters) casts no shadow: hundreds of them, too fine to see
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
                if (mr.name == "Twig" || mr.name == "Strand" || mr.name == "Splinter") mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            { var dc = g.GetComponent<Collider>(); dc.enabled = false; Object.Destroy(dc); }   // (off now: Destroy only happens at the end of the frame)
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
            if (!solid) { var dc = g.GetComponent<Collider>(); dc.enabled = false; Object.Destroy(dc); }
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
                bark = new Material(PH("bark_brown_02", 0.05f, new Color(0.8f, 0.78f, 0.75f))); bark.SetTextureScale("_BaseMap", new Vector2(1f, 4f)); bark.SetTextureScale("_BumpMap", new Vector2(1f, 4f));
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
            ground.AddComponent<MeshRenderer>().sharedMaterials = new[] { PH("excavated_soil_wall", 0.08f, new Color(0.85f, 0.8f, 0.75f), 1.4f), PH("muddy_tracks", 0.35f, new Color(0.9f, 0.85f, 0.8f), 1.2f), PH("leaves_forest_ground", 0.05f, new Color(0.9f, 0.88f, 0.85f), 1.2f) };   // cut earth, wet mud, leaf litter (Poly Haven)
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

            // ---- dressing (Poly Haven): duckboards down the trench floors, plank revetment on posts holding the walls
            // of the front lines, sandbags on the parapets of the fighting positions, kit in the positions and dugouts,
            // and undergrowth, stumps, deadfall and rocks over the ground between
            var dr = new System.Random(77);
            float R2(float a, float b) => a + (float)dr.NextDouble() * (b - a);
            var planks = PH("old_planks_02", 0.1f, new Color(0.78f, 0.72f, 0.66f));
            foreach (var (kind, pts) in data.lines)
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    float len = Vector2.Distance(a, b);
                    if (len < 0.6f) continue;
                    float ya = H(a.x, a.y), yb = H(b.x, b.y);
                    if (ya > 0.5f || yb > 0.5f) continue;
                    Vector3 A3 = new Vector3(a.x, ya + 0.03f, a.y), B3 = new Vector3(b.x, yb + 0.03f, b.y);
                    // mostly bare dirt: boards only in stretches (where someone had the timber and the time)
                    Vector2 mid2 = (a + b) * 0.5f;
                    float timber = Mathf.PerlinNoise(mid2.x * 0.06f + 3.1f, mid2.y * 0.06f + 7.7f);
                    if (timber > 0.5f)
                    {
                        var duck = Box(r, "Duckboard", (A3 + B3) * 0.5f, new Vector3(0.5f, 0.05f, len - 0.1f), planks);
                        duck.transform.localRotation = Quaternion.LookRotation(B3 - A3);
                        Tiled(duck, 1.2f);
                    }
                    if (kind > 1 || timber < 0.66f) continue;
                    // the front lines: boards along both walls, held by posts driven in every couple of metres
                    Vector3 d3 = new Vector3(b.x - a.x, 0f, b.y - a.y) / len, n3 = new Vector3(-d3.z, 0f, d3.x);
                    float floor = Mathf.Min(ya, yb);
                    int oneSide = timber < 0.72f ? (Mathf.PerlinNoise(mid2.x * 0.3f, mid2.y * 0.3f) < 0.5f ? -1 : 1) : 0;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        if (oneSide != 0 && s != oneSide) continue;
                        // in board-lengths, against the wall as it was actually dug (it wanders off the line it was
                        // drawn from), and only where there is a wall: none across the mouth of a side trench or a
                        // fighting position
                        for (float t = 0f; t < len - 0.3f; t += 1.2f)
                        {
                            float piece = Mathf.Min(1.2f, len - t);
                            Vector2 at2 = a + (b - a) / len * (t + piece * 0.5f);
                            // the middle of the floor across here
                            Vector2 nrm2 = new Vector2(n3.x, n3.z);
                            float sum = 0f; int cnt = 0;
                            for (float o = -1.1f; o <= 1.1f; o += 0.1f) { Vector2 q = at2 + nrm2 * o; if (H(q.x, q.y) < floor + 0.3f) { sum += o; cnt++; } }
                            if (cnt == 0) continue;
                            at2 += nrm2 * (sum / cnt);
                            Vector3 from = new Vector3(at2.x, floor + 0.75f, at2.y);
                            if (!col.Raycast(new Ray(from, n3 * s), out var wallHit, 1.1f)) continue;
                            // not where the wall turns a corner (the board would stand out across the floor), nor
                            // anywhere it leaves less than a man's width between the walls
                            if (Mathf.Abs(Vector3.Dot(Vector3.ProjectOnPlane(wallHit.normal, Vector3.up).normalized, n3)) < 0.8f) continue;
                            float across = col.Raycast(new Ray(from, -n3 * s), out var otherHit, 2f) ? wallHit.distance + otherHit.distance : 3f;
                            if (across < 1.1f) continue;
                            Vector3 mid = wallHit.point - n3 * s * 0.03f;
                            var wallB = Box(r, "Revetment", mid, new Vector3(0.04f, 1.5f, piece), planks);
                            wallB.transform.localRotation = Quaternion.LookRotation(d3);
                            Tiled(wallB, 1.2f);
                            if (Mathf.Repeat(t, 2.4f) < 0.1f)
                            {
                                Vector3 pp = new Vector3(mid.x, floor, mid.z) - n3 * s * 0.05f;
                                Pole(r, "Post", pp - Vector3.up * 0.2f, pp + Vector3.up * R2(1.6f, 1.85f), 0.05f, logMat, true);
                            }
                        }
                    }
                }
            foreach (var (kind, c, rad) in data.cells)
            {
                float fy = H(c.x, c.y);
                if (kind == 0)
                {
                    // sandbags along the lip facing the enemy, two layers
                    Vector3 front = new Vector3(0f, 0f, c.y < 0f ? 1f : -1f), side = Vector3.right;
                    for (int k = -2; k <= 2; k++)
                    {
                        Vector3 p = new Vector3(c.x, 0f, c.y) + front * (rad + 0.45f) + side * k * 0.55f;
                        float y = H(p.x, p.z);
                        if (y < 1f) continue;
                        Prop(r, "cement_bag", new Vector3(p.x, y - 0.05f, p.z), R2(-8f, 8f));
                        if (k > -2 && k < 2) Prop(r, "cement_bag", new Vector3(p.x + 0.27f, y + 0.1f, p.z), R2(-8f, 8f));
                    }
                    if (dr.NextDouble() < 0.7) Prop(r, "ammo_box", new Vector3(c.x + R2(-0.4f, 0.4f), fy, c.y + R2(-0.4f, 0.4f)), R2(0f, 360f));
                    if (dr.NextDouble() < 0.35) Prop(r, "metal_jerrycan_green", new Vector3(c.x + R2(-0.4f, 0.4f), fy, c.y + R2(-0.4f, 0.4f)), R2(0f, 360f));
                }
                else
                {
                    Prop(r, dr.NextDouble() < 0.5 ? "old_military_crate" : "wooden_crate_01", new Vector3(c.x + R2(-0.5f, 0.5f), fy, c.y + R2(-0.5f, 0.5f)), R2(0f, 360f));
                    Prop(r, "Lantern_01", new Vector3(c.x + R2(-0.5f, 0.5f), fy, c.y + R2(-0.5f, 0.5f)), R2(0f, 360f), 1f, false);
                    if (dr.NextDouble() < 0.5) Prop(r, "ammo_box", new Vector3(c.x + R2(-0.5f, 0.5f), fy, c.y + R2(-0.5f, 0.5f)), R2(0f, 360f));
                }
            }
            void Scatter(string id, int n, float s0, float s1, bool solid, float sink = 0f, bool lying = false)
            {
                for (int k = 0, tries = 0; k < n && tries < n * 6; tries++)
                {
                    float x = R2(-48f, 48f), z = R2(-50f, 52f), sc = R2(s0, s1);
                    // its whole footprint on open, fairly level ground (a long clump of grass must not bridge a trench)
                    float rad = PropRadius(id) * sc + 0.4f, y0 = H(x, z);
                    bool fits = Open(x, z, 1.6f);
                    for (int q = 0; q < 12 && fits; q++)
                    {
                        float a = q * Mathf.PI / 6f, f = q < 6 ? rad : rad * 0.5f;
                        float px = x + Mathf.Cos(a) * f, pz = z + Mathf.Sin(a) * f;
                        fits = Open(px, pz, 1.6f) && Mathf.Abs(H(px, pz) - y0) < 0.3f;
                    }
                    if (!fits) continue;
                    Prop(r, id, G(x, z) - Vector3.up * sink, R2(0f, 360f), sc, solid, lying ? new Vector3(R2(-4f, 4f), 0f, R2(-4f, 4f)) : default);
                    k++;
                }
            }
            Scatter("grass_medium_01", 35, 0.8f, 1.3f, false);
            Scatter("fern_02", 45, 0.7f, 1.2f, false);
            Scatter("shrub_02", 10, 0.6f, 1f, false, 0.3f);
            Scatter("dry_branches_medium_01", 20, 0.8f, 1.3f, false);
            Scatter("tree_stump_01", 5, 0.8f, 1.2f, true, 0.05f); Scatter("tree_stump_02", 5, 0.8f, 1.2f, true, 0.05f);
            Scatter("dead_tree_trunk", 5, 0.9f, 1.1f, true, 0.1f, true); Scatter("dead_tree_trunk_02", 5, 0.9f, 1.1f, true, 0.1f, true);
            Scatter("boulder_01", 5, 0.5f, 0.9f, true, 0.2f); Scatter("rock_07", 12, 3f, 6f, true, 0.1f);

            TrenchExtras(r, data, col, dr);
            TrenchNav.Build(data.lines, data.cells, H, col);

            // you start in your own front line, the Earls in theirs and in their fighting positions
            var friendly = data.lines.Find(l => l.kind == 0).pts;
            var enemy = data.lines.Find(l => l.kind == 1).pts;
            Vector2 sp = Vector2.Lerp(friendly[friendly.Length / 2], friendly[friendly.Length / 2 + 1], 0.5f);
            spawn = new Vector3(sp.x, H(sp.x, sp.y) + 0.1f, sp.y);
            // facing along the trench, not into its wall a step away (the gun and arms rammed into the boards)
            Vector2 along = friendly[friendly.Length / 2 + 1] - friendly[friendly.Length / 2];
            yaw = along.sqrMagnitude > 0.01f ? Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg : 0f;
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
            // a sealed concrete hangar floor, block walls, steel plate wainscot (Poly Haven surfaces)
            Tiled(Box(r, "Floor", Vector3.down, new Vector3(W, 2f, D), PH("hangar_concrete_floor", 0.3f)), 4f);
            var blocks = PH("beige_wall_001", 0.1f, new Color(0.95f, 0.96f, 0.98f));
            Tiled(Box(r, "WallN", new Vector3(0, H / 2, D / 2), new Vector3(W, H, 0.5f), blocks), 2.4f);
            Tiled(Box(r, "WallS", new Vector3(0, H / 2, -D / 2), new Vector3(W, H, 0.5f), blocks), 2.4f);
            Tiled(Box(r, "WallE", new Vector3(W / 2, H / 2, 0), new Vector3(0.5f, H, D), blocks), 2.4f);
            Tiled(Box(r, "WallW", new Vector3(-W / 2, H / 2, 0), new Vector3(0.5f, H, D), blocks), 2.4f);
            // dark lower panels + orange trim
            Tiled(Box(r, "PanelN", new Vector3(0, 2.2f, D / 2 - 0.3f), new Vector3(W - 1, 4.4f, 0.1f), PH("metal_plate", 0.45f, new Color(0.85f, 0.87f, 0.9f))), 1.5f);
            Tiled(Box(r, "PanelE", new Vector3(W / 2 - 0.3f, 2.2f, 0), new Vector3(0.1f, 4.4f, D - 1), PH("metal_plate", 0.45f, new Color(0.85f, 0.87f, 0.9f))), 1.5f);
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
                Tiled(Box(r, "Cover", pos, new Vector3(4.5f, 2.5f, 0.3f), PH("beige_wall_002", 0.1f)), 2f);
                Text(r, $"COVER / 0{i + 1}", pos + new Vector3(0, 0.6f, -0.17f), Quaternion.identity, 0.35f, new Color(0.3f, 0.3f, 0.32f));
            }
            // raised platform with stairs (east side)
            Tiled(Box(r, "Platform", new Vector3(20f, 1.2f, 12f), new Vector3(12f, 2.4f, 10f), PH("metal_plate_02", 0.4f)), 2f);
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

            // the lab's kit (Poly Haven): stocked shelving down the west wall, the bench work on the tables, drums
            // and containers in the corners, a workshop corner, extinguishers on the walls
            for (int i = 0; i < 4; i++)
            {
                float z = -10f + i * 4.2f;
                Prop(r, "Shelf_01", new Vector3(-29.1f, 0f, z), 90f);
                Prop(r, "cardboard_box_01", new Vector3(-28.4f, 0f, z + 1.6f), 90f + i * 23f);
            }
            Prop(r, "drawer_cabinet", new Vector3(-29.2f, 0f, -18.5f), 90f);
            Prop(r, "chemistry_set", new Vector3(-20f, 0.89f, -4f), 180f, 1f, false);
            Prop(r, "medical_box", new Vector3(-18.6f, 0.89f, -3.9f), 160f, 1f, false);
            Prop(r, "industrial_microscope", new Vector3(-16f, 0.89f, -4f), 180f, 1f, false);
            Prop(r, "classic_laptop", new Vector3(-12.3f, 0.89f, -4.05f), 175f, 1f, false);
            Prop(r, "desk_lamp_arm_01", new Vector3(-11.3f, 0.89f, -3.75f), 200f, 1f, false);
            for (int i = 0; i < 3; i++) Prop(r, "SchoolChair_01", new Vector3(-20f + i * 4f, 0f, -5.2f), Random.Range(-15f, 15f));
            Prop(r, "Barrel_01", new Vector3(27.3f, 0f, 19.6f), 20f); Prop(r, "barrel_03", new Vector3(28.4f, 0f, 18.3f), 75f);
            Prop(r, "Barrel_01", new Vector3(28.5f, 0f, 20.4f), 140f);
            Prop(r, "industrial_pastic_container", new Vector3(26.5f, 0f, -20.2f), 0f);
            Prop(r, "industrial_storage_cart", new Vector3(24f, 0f, -15.5f), 30f);
            Prop(r, "hand_truck", new Vector3(27.6f, 0f, -12.5f), -60f);
            Prop(r, "drill_press_01", new Vector3(28.8f, 0f, -3f), -90f);
            Prop(r, "concrete_road_barrier", new Vector3(12f, 0f, -14f), 0f);
            Prop(r, "concrete_road_barrier", new Vector3(-25.5f, 0f, 4f), 90f);
            Prop(r, "WetFloorSign_01", new Vector3(3f, 0f, -9f), 35f);
            Prop(r, "wooden_military_crate", new Vector3(-27.6f, 0f, 20.2f), 5f);
            Prop(r, "plastic_crate_01", new Vector3(-22.5f, 0f, -19.5f), 12f);
            Prop(r, "plastic_crate_01", new Vector3(-22.4f, 0.3f, -19.4f), -8f);
            foreach (var (p, y) in new[] { (new Vector3(29.4f, 0f, -9f), -90f), (new Vector3(-29.4f, 0f, 9f), 90f), (new Vector3(5f, 0f, 21.5f), 180f), (new Vector3(-5f, 0f, -21.5f), 0f) })
                Prop(r, "korean_fire_extinguisher_01", p, y);

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
