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
            // the trenches are fought in a cold brown haze
            RenderSettings.fog = index == 3;
            if (index == 3) { RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.012f; RenderSettings.fogColor = new Color(0.55f, 0.52f, 0.47f); }
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
        // Two zig-zag front lines facing each other across 50 m of no man's land, with support lines behind and
        // communication trenches joining them. The ground is solid earth 1.9 m above the trench floors (floor y = 0,
        // so everything that spawns on the ground spawns in a trench). Fire steps along the enemy side let you look
        // over the sandbagged parapet; duckboards on the floor, plank revetments on the walls, wire and craters out in
        // the open.

        static Material mud, dirt, wood, darkWood, bag, wireMat, craterMud;
        const float TC = 2f, TrenchDepth = 1.9f, TX0 = -36f, TZ0 = -50f;
        const int TNX = 36, TNZ = 50;

        static GameObject Deco(Transform r, string name, Vector3 c, Vector3 size, Material m, Vector3 euler = default)
        {
            var g = Box(r, name, c, size, m);
            g.transform.localRotation = Quaternion.Euler(euler);
            Object.Destroy(g.GetComponent<Collider>());
            return g;
        }

        static Vector3 CellPos(int i, int j, float y = 0f) => new Vector3(TX0 + (i + 0.5f) * TC, y, TZ0 + (j + 0.5f) * TC);

        static void BuildTrenches(Transform r, out Vector3 spawn, out float yaw, out Vector3[] carls)
        {
            if (mud == null)
            {
                mud = Mats.Lit(new Color(0.24f, 0.19f, 0.13f), 0.35f);
                dirt = Mats.Lit(new Color(0.35f, 0.28f, 0.19f), 0.05f);
                wood = Mats.Lit(new Color(0.42f, 0.31f, 0.19f), 0.1f);
                darkWood = Mats.Lit(new Color(0.27f, 0.2f, 0.13f), 0.1f);
                bag = Mats.Lit(new Color(0.57f, 0.51f, 0.37f), 0.05f);
                wireMat = Mats.Lit(new Color(0.22f, 0.2f, 0.19f), 0.4f, 0.6f);
                craterMud = Mats.Lit(new Color(0.2f, 0.16f, 0.11f), 0.45f);
            }
            var t = new bool[TNX, TNZ];
            int jA = 11, jE = 38;
            void Mark(int i, int j) { if (i >= 0 && i < TNX && j >= 0 && j < TNZ) t[i, j] = true; }
            // front lines: fire bays stepped back and forth (traverses stop a shell or a man clearing the whole line)
            for (int i = 2; i <= 33; i++)
            {
                int bayA = (i / 4) % 2, bayE = ((i + 2) / 4) % 2;
                Mark(i, jA + bayA); Mark(i, jE - bayE);
                if (i % 4 == 0) { Mark(i, jA); Mark(i, jA + 1); }
                if ((i + 2) % 4 == 0) { Mark(i, jE); Mark(i, jE - 1); }
            }
            // support lines and communication trenches (with doglegs), a sap pushed out into no man's land
            for (int i = 6; i <= 28; i++) Mark(i, 4);
            for (int i = 8; i <= 28; i++) Mark(i, 45);
            for (int j = 4; j <= jA; j++) Mark(26, j);
            for (int j = 7; j <= jA; j++) Mark(8, j);
            for (int i = 8; i <= 12; i++) Mark(i, 7);
            for (int j = 4; j <= 7; j++) Mark(12, j);
            for (int j = jE; j <= 45; j++) Mark(10, j);
            for (int j = jE; j <= 42; j++) Mark(24, j);
            for (int i = 20; i <= 24; i++) Mark(i, 42);
            for (int j = 42; j <= 45; j++) Mark(20, j);
            for (int j = jA + 1; j <= jA + 5; j++) Mark(18, j);
            for (int i = 17; i <= 19; i++) Mark(i, jA + 5);

            float W = TNX * TC, D = TNZ * TC;
            // trench floor (mud) under everything, earth on every cell that isn't trench, a high bank round the edge
            Box(r, "Mud", new Vector3(0f, -0.5f, 0f), new Vector3(W + 20f, 1f, D + 20f), mud);
            for (int j = 0; j < TNZ; j++)
            {
                int i = 0;
                while (i < TNX)
                {
                    if (t[i, j]) { i++; continue; }
                    int s = i;
                    while (i < TNX && !t[i, j]) i++;
                    float x0 = TX0 + s * TC, x1 = TX0 + i * TC, z = TZ0 + (j + 0.5f) * TC;
                    Box(r, "Earth", new Vector3((x0 + x1) * 0.5f, TrenchDepth * 0.5f, z), new Vector3(x1 - x0, TrenchDepth, TC), dirt);
                }
            }
            Box(r, "BankN", new Vector3(0f, 2.5f, D / 2 + 3f), new Vector3(W + 12f, 5f, 6f), dirt);
            Box(r, "BankS", new Vector3(0f, 2.5f, -D / 2 - 3f), new Vector3(W + 12f, 5f, 6f), dirt);
            Box(r, "BankE", new Vector3(W / 2 + 3f, 2.5f, 0f), new Vector3(6f, 5f, D), dirt);
            Box(r, "BankW", new Vector3(-W / 2 - 3f, 2.5f, 0f), new Vector3(6f, 5f, D), dirt);

            int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
            for (int i = 0; i < TNX; i++)
                for (int j = 0; j < TNZ; j++)
                {
                    if (!t[i, j]) continue;
                    Vector3 c = CellPos(i, j);
                    // duckboards: a walkway of slats on two runners
                    bool alongX = (i > 0 && t[i - 1, j]) || (i < TNX - 1 && t[i + 1, j]);
                    float ry = alongX ? 90f : 0f;
                    var duck = Box(r, "Duckboard", c + Vector3.up * 0.04f, new Vector3(0.9f, 0.08f, TC), darkWood, ry);
                    for (int k = 0; k < 5; k++)
                    {
                        float o = -0.8f + k * 0.4f;
                        Vector3 off = alongX ? new Vector3(o, 0f, 0f) : new Vector3(0f, 0f, o);
                        Deco(r, "Slat", c + off + Vector3.up * 0.09f, alongX ? new Vector3(0.12f, 0.03f, 1f) : new Vector3(1f, 0.03f, 0.12f), wood);
                    }
                    for (int n = 0; n < 4; n++)
                    {
                        int ni = i + di[n], nj = j + dj[n];
                        if (ni >= 0 && ni < TNX && nj >= 0 && nj < TNZ && t[ni, nj]) continue;
                        // revetment: planks against the earth wall, held by posts
                        Vector3 nrm = new Vector3(di[n], 0f, dj[n]);
                        Vector3 face = c + nrm * (TC * 0.5f - 0.04f);
                        Vector3 size = di[n] != 0 ? new Vector3(0.06f, TrenchDepth - 0.1f, TC) : new Vector3(TC, TrenchDepth - 0.1f, 0.06f);
                        Deco(r, "Revetment", face + Vector3.up * (TrenchDepth * 0.5f - 0.05f), size, wood);
                        Vector3 side = di[n] != 0 ? Vector3.forward : Vector3.right;
                        Deco(r, "Post", face - nrm * 0.05f + side * (TC * 0.5f - 0.08f) + Vector3.up * TrenchDepth * 0.5f, new Vector3(0.12f, TrenchDepth, 0.12f), darkWood);
                        // the enemy-facing wall of a front line: a fire step to stand on and a sandbag parapet with loopholes
                        bool allied = j < 25;
                        bool front = (allied && dj[n] == 1 && j >= jA && j <= jA + 5) || (!allied && dj[n] == -1 && j <= jE && j >= jE - 1);
                        if (!front) continue;
                        Box(r, "FireStep", c + nrm * (TC * 0.5f - 0.35f) + Vector3.up * 0.3f, new Vector3(TC, 0.6f, 0.6f), darkWood);
                        Vector3 top = c + nrm * (TC * 0.5f + 0.3f) + Vector3.up * TrenchDepth;
                        Box(r, "Sandbags", top + Vector3.up * 0.11f, new Vector3(TC, 0.22f, 0.45f), bag);
                        for (int k = 0; k < 3; k++)
                        {
                            if (k == 1 && (i + j) % 2 == 0) continue;   // a loophole every other bay
                            Box(r, "Sandbag", top + new Vector3((k - 1) * 0.66f, 0.33f, 0f), new Vector3(0.6f, 0.22f, 0.4f), bag, (k - 1) * 6f);
                        }
                    }
                }

            // no man's land: craters, wire and the stumps of trees
            var rng = new System.Random(1916);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int k = 0; k < 22; k++)
            {
                Vector3 c = new Vector3(Rand(-32f, 32f), TrenchDepth, Rand(-20f, 20f));
                float rad = Rand(1.2f, 3.2f);
                Deco(r, "CraterMud", c + Vector3.up * 0.01f, new Vector3(rad * 1.6f, 0.02f, rad * 1.6f), craterMud, new Vector3(0f, Rand(0f, 90f), 0f));
                for (int s = 0; s < 8; s++)
                {
                    float a = s / 8f * Mathf.PI * 2f;
                    Box(r, "CraterLip", c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad + Vector3.up * 0.15f, new Vector3(rad * 0.85f, 0.3f, 0.7f), dirt, -a * Mathf.Rad2Deg + 90f);
                }
            }
            foreach (float zLine in new[] { -20.5f, -17f, 17f, 20.5f })
            {
                for (float x = -33f; x <= 33f; x += 3f)
                {
                    Vector3 p = new Vector3(x + Rand(-0.4f, 0.4f), TrenchDepth, zLine + Rand(-0.5f, 0.5f));
                    Deco(r, "Picket", p + Vector3.up * 0.55f, new Vector3(0.08f, 1.2f, 0.08f), darkWood, new Vector3(25f, 0f, 0f));
                    Deco(r, "Picket", p + Vector3.up * 0.55f, new Vector3(0.08f, 1.2f, 0.08f), darkWood, new Vector3(-25f, 0f, 0f));
                    // wire strung across to the next picket, sagging, in a few strands
                    for (int s = 0; s < 3; s++)
                    {
                        float h = 0.3f + s * 0.3f;
                        Deco(r, "Wire", p + new Vector3(1.5f, h, 0f), new Vector3(3.1f, 0.01f, 0.01f), wireMat, new Vector3(0f, Rand(-12f, 12f), Rand(-6f, 6f)));
                    }
                    // a loose coil
                    if (rng.NextDouble() < 0.5)
                        for (int s = 0; s < 6; s++)
                            Deco(r, "Coil", p + new Vector3(0.8f, 0.35f, 0.3f), new Vector3(0.7f, 0.01f, 0.01f), wireMat, new Vector3(s * 30f, s * 31f, 0f));
                }
            }
            for (int k = 0; k < 9; k++)
            {
                Vector3 p = new Vector3(Rand(-30f, 30f), TrenchDepth, Rand(-14f, 14f));
                float h = Rand(2.5f, 6f);
                var trunk = Box(r, "DeadTree", p + Vector3.up * h * 0.5f, new Vector3(0.35f, h, 0.35f), darkWood, Rand(0f, 90f));
                trunk.transform.localRotation *= Quaternion.Euler(Rand(-6f, 6f), 0f, Rand(-6f, 6f));
                Deco(r, "Branch", p + Vector3.up * h * 0.75f, new Vector3(0.12f, h * 0.35f, 0.12f), darkWood, new Vector3(0f, Rand(0f, 360f), Rand(35f, 60f)));
            }
            // a dugout entrance in the support line, and ammunition boxes along the front
            Deco(r, "DugoutFrame", CellPos(16, 4) + new Vector3(0f, 0.95f, -0.97f), new Vector3(1.4f, 1.9f, 0.1f), darkWood);
            Deco(r, "DugoutDark", CellPos(16, 4) + new Vector3(0f, 0.85f, -0.95f), new Vector3(1.0f, 1.6f, 0.05f), Mats.Lit(new Color(0.03f, 0.03f, 0.03f), 0f));
            for (int k = 0; k < 6; k++) Crate(r, CellPos(4 + k * 5, jA + ((4 + k * 5) / 4) % 2, 0.35f) + new Vector3(0.4f, 0f, -0.5f), 0.5f, 18f);

            spawn = CellPos(16, jA + (16 / 4) % 2, 0.1f);
            yaw = 0f;
            carls = new Vector3[6];
            for (int k = 0; k < 6; k++)
            {
                int i = 5 + k * 5;
                carls[k] = CellPos(i, jE - ((i + 2) / 4) % 2, 0f);
            }
        }

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
