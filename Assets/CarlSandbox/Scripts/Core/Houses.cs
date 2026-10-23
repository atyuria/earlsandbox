using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The HOUSES map: a street of homes to fight through. Bungalows and two-storey cottages with real doorways,
    /// stairs you can walk up and down (a stairwell cut in the floor above, a railing round it), furniture kept out
    /// of the way, see-through windows that shatter, pitched roofs, gardens, fences and a road.
    /// Walls are thin plaster and timber, so rifle rounds can go through them.
    /// </summary>
    public static class Houses
    {
        struct Opening { public float at, width, bottom, top; public Opening(float a, float w, float b, float t) { at = a; width = w; bottom = b; top = t; } }

        static Material plaster, plaster2, plaster3, plaster4, wood, floorWood, tile, roofTile, roofTile2, glassM, fabric, fabric2, white, dark, metal, grass, asphalt, fence, frame, brick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { plaster = null; }

        static void InitMats()
        {
            if (plaster != null) return;
            plaster = Mats.Lit(new Color(0.88f, 0.84f, 0.76f), 0.1f);
            plaster2 = Mats.Lit(new Color(0.74f, 0.82f, 0.86f), 0.1f);
            plaster3 = Mats.Lit(new Color(0.86f, 0.76f, 0.66f), 0.1f);
            plaster4 = Mats.Lit(new Color(0.8f, 0.85f, 0.74f), 0.1f);
            brick = Mats.LitTextured(TexGen.Grid(new Color(0.62f, 0.32f, 0.24f), new Color(0.75f, 0.72f, 0.66f), 128, 8), new Vector2(6, 3), Color.white, 0.15f);
            wood = Mats.Lit(new Color(0.45f, 0.3f, 0.18f), 0.3f);
            frame = Mats.Lit(new Color(0.95f, 0.95f, 0.93f), 0.35f);
            floorWood = Mats.LitTextured(TexGen.Grid(new Color(0.55f, 0.38f, 0.22f), new Color(0.46f, 0.31f, 0.18f), 128, 8), new Vector2(4, 4), Color.white, 0.35f);
            tile = Mats.LitTextured(TexGen.Grid(new Color(0.9f, 0.9f, 0.88f), new Color(0.7f, 0.72f, 0.72f), 128, 6), new Vector2(3, 3), Color.white, 0.5f);
            roofTile = Mats.Lit(new Color(0.48f, 0.2f, 0.15f), 0.2f);
            roofTile2 = Mats.Lit(new Color(0.26f, 0.27f, 0.3f), 0.25f);
            glassM = new Material(Mats.Find("Carl/Glass"));
            glassM.SetColor("_Color", new Color(0.6f, 0.75f, 0.8f, 0.18f));
            fabric = Mats.Lit(new Color(0.3f, 0.36f, 0.5f), 0.05f);
            fabric2 = Mats.Lit(new Color(0.55f, 0.3f, 0.25f), 0.05f);
            white = Mats.Lit(new Color(0.92f, 0.92f, 0.9f), 0.4f);
            dark = Mats.Lit(new Color(0.15f, 0.15f, 0.16f), 0.3f);
            metal = Mats.Lit(new Color(0.55f, 0.56f, 0.58f), 0.6f, 0.6f);
            grass = Mats.LitTextured(TexGen.Grid(new Color(0.36f, 0.5f, 0.25f), new Color(0.33f, 0.46f, 0.23f), 128, 4), new Vector2(20, 20), Color.white, 0.05f);
            asphalt = Mats.Lit(new Color(0.22f, 0.22f, 0.23f), 0.15f);
            fence = Mats.Lit(new Color(0.62f, 0.52f, 0.38f), 0.2f);
        }

        static GameObject Box(Transform r, string name, Vector3 c, Vector3 size, Material m, float rotY = 0f, bool collide = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(r, false);
            g.transform.localPosition = c;
            g.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collide) { var dc = g.GetComponent<Collider>(); dc.enabled = false; Object.Destroy(dc); }
            g.isStatic = true;
            return g;
        }

        /// <summary>An invisible slope to walk on (stairs: smooth both ways, no catching on step edges).</summary>
        static void Ramp(Transform r, Vector3 from, Vector3 to, float width)
        {
            var g = new GameObject("StairRamp");
            g.transform.SetParent(r, false);
            Vector3 d = to - from;
            g.transform.localPosition = (from + to) * 0.5f;
            g.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            var bc = g.AddComponent<BoxCollider>();
            bc.size = new Vector3(width, 0.04f, d.magnitude);
            bc.center = new Vector3(0f, -0.02f, 0f);
        }

        /// <summary>A straight wall from a to b (local, y = floor height) with doors/windows cut out of it.</summary>
        static void Wall(Transform r, Vector3 a, Vector3 b, float h, float t, Material m, params Opening[] holes)
        {
            Vector3 d = b - a; float len = new Vector2(d.x, d.z).magnitude;
            Vector3 dir = new Vector3(d.x, 0, d.z) / len;
            float rot = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - 90f;
            var list = new List<Opening>(holes); list.Sort((x, y) => x.at.CompareTo(y.at));
            float cursor = 0f;
            void Seg(float s0, float s1, float y0, float y1)
            {
                if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
                Vector3 c = a + dir * ((s0 + s1) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f);
                Box(r, "Wall", c, new Vector3(s1 - s0, y1 - y0, t), m, rot);
            }
            foreach (var o in list)
            {
                float s0 = o.at - o.width / 2f, s1 = o.at + o.width / 2f;
                Seg(cursor, s0, 0f, h);
                Seg(s0, s1, 0f, o.bottom);
                Seg(s0, s1, o.top, h);
                Vector3 mid = a + dir * o.at;
                if (o.bottom > 0.2f)
                {
                    // window: a pane of real glass in a white frame, and a sill
                    var pane = Box(r, "WindowGlass", mid + Vector3.up * ((o.bottom + o.top) * 0.5f), new Vector3(o.width - 0.04f, o.top - o.bottom - 0.04f, 0.012f), glassM, rot);
                    pane.isStatic = false;
                    pane.AddComponent<WindowGlass>();
                    pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Box(r, "Frame", mid + Vector3.up * o.top, new Vector3(o.width, 0.05f, t + 0.02f), frame, rot, false);
                    Box(r, "Frame", mid + Vector3.up * ((o.bottom + o.top) * 0.5f), new Vector3(0.04f, o.top - o.bottom, 0.05f), frame, rot, false);
                    Box(r, "Sill", mid + Vector3.up * o.bottom, new Vector3(o.width + 0.12f, 0.05f, t + 0.1f), frame, rot);
                }
                else
                {
                    // door frame (trim only - the doorway itself is open)
                    Box(r, "DoorFrame", mid + Vector3.up * o.top, new Vector3(o.width + 0.1f, 0.08f, t + 0.03f), frame, rot, false);
                    for (int s = -1; s <= 1; s += 2)
                        Box(r, "DoorFrame", mid + dir * (s * (o.width / 2f + 0.02f)) + Vector3.up * (o.top * 0.5f), new Vector3(0.05f, o.top, t + 0.03f), frame, rot, false);
                }
                cursor = s1;
            }
            Seg(cursor, len, 0f, h);
        }

        static Opening Door(float at) => new Opening(at, 1.0f, 0f, 2.15f);
        static Opening Window(float at, float w = 1.2f) => new Opening(at, w, 0.9f, 2.0f);

        static void PitchedRoof(Transform r, float w, float d, float baseY, float pitchH, Material m, Material gable)
        {
            float half = w / 2f + 0.4f;
            float slope = Mathf.Sqrt(half * half + pitchH * pitchH);
            float ang = Mathf.Atan2(pitchH, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
            {
                var g = Box(r, "Roof", new Vector3(s * half / 2f, baseY + pitchH / 2f, 0f), new Vector3(slope, 0.14f, d + 0.8f), m);
                g.transform.localRotation = Quaternion.Euler(0, 0, -s * ang);
            }
            for (int e = -1; e <= 1; e += 2)
                for (int k = 0; k < 8; k++)
                {
                    float f = (k + 0.5f) / 8f;
                    Box(r, "Gable", new Vector3(0, baseY + pitchH * f * 0.95f, e * d / 2f), new Vector3(w * (1f - f), pitchH / 8f + 0.01f, 0.14f), gable);
                }
            Box(r, "Ridge", new Vector3(0, baseY + pitchH + 0.04f, 0), new Vector3(0.25f, 0.12f, d + 0.9f), m);
        }

        // ---- furniture
        static Transform Group(Transform r, string name, Vector3 p, float rot) { var t = new GameObject(name).transform; t.SetParent(r, false); t.localPosition = p; t.localRotation = Quaternion.Euler(0, rot, 0); return t; }
        static void Table(Transform r, Vector3 p, float rot = 0f) { var t = Group(r, "Table", p, rot); Box(t, "Top", new Vector3(0, 0.75f, 0), new Vector3(1.4f, 0.05f, 0.8f), wood); for (int i = 0; i < 4; i++) Box(t, "Leg", new Vector3((i % 2 == 0 ? -0.62f : 0.62f), 0.37f, (i < 2 ? -0.32f : 0.32f)), new Vector3(0.06f, 0.74f, 0.06f), wood); }
        static void Chair(Transform r, Vector3 p, float rot) { var t = Group(r, "Chair", p, rot); Box(t, "Seat", new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), wood); Box(t, "Back", new Vector3(0, 0.75f, -0.2f), new Vector3(0.45f, 0.55f, 0.05f), wood); for (int i = 0; i < 4; i++) Box(t, "Leg", new Vector3((i % 2 == 0 ? -0.19f : 0.19f), 0.22f, (i < 2 ? -0.19f : 0.19f)), new Vector3(0.04f, 0.44f, 0.04f), wood); }
        static void Sofa(Transform r, Vector3 p, float rot, Material m) { var t = Group(r, "Sofa", p, rot); Box(t, "Base", new Vector3(0, 0.22f, 0), new Vector3(2f, 0.44f, 0.85f), m); Box(t, "Back", new Vector3(0, 0.62f, -0.34f), new Vector3(2f, 0.5f, 0.2f), m); Box(t, "ArmL", new Vector3(-0.92f, 0.52f, 0), new Vector3(0.18f, 0.3f, 0.85f), m); Box(t, "ArmR", new Vector3(0.92f, 0.52f, 0), new Vector3(0.18f, 0.3f, 0.85f), m); }
        static void Bed(Transform r, Vector3 p, float rot) { var t = Group(r, "Bed", p, rot); Box(t, "Frame", new Vector3(0, 0.2f, 0), new Vector3(1.5f, 0.4f, 2.1f), wood); Box(t, "Mattress", new Vector3(0, 0.48f, 0.05f), new Vector3(1.42f, 0.18f, 1.95f), white); Box(t, "Blanket", new Vector3(0, 0.58f, 0.35f), new Vector3(1.46f, 0.04f, 1.3f), fabric); Box(t, "Head", new Vector3(0, 0.6f, -1.03f), new Vector3(1.5f, 1.2f, 0.06f), wood); Box(t, "Pillow", new Vector3(0, 0.62f, -0.75f), new Vector3(0.9f, 0.12f, 0.35f), white); }
        static void Counter(Transform r, Vector3 p, float len, float rot) { var t = Group(r, "Kitchen", p, rot); Box(t, "Cabinets", new Vector3(0, 0.44f, 0), new Vector3(len, 0.88f, 0.6f), white); Box(t, "Worktop", new Vector3(0, 0.9f, 0), new Vector3(len + 0.04f, 0.04f, 0.64f), dark); Box(t, "Upper", new Vector3(0, 1.85f, -0.15f), new Vector3(len, 0.7f, 0.35f), white); Box(t, "Fridge", new Vector3(len / 2f + 0.35f, 0.95f, 0), new Vector3(0.7f, 1.9f, 0.65f), metal); }
        static void Shelf(Transform r, Vector3 p, float rot) { var t = Group(r, "Shelf", p, rot); Box(t, "Side", new Vector3(-0.45f, 0.95f, 0), new Vector3(0.04f, 1.9f, 0.35f), wood); Box(t, "Side", new Vector3(0.45f, 0.95f, 0), new Vector3(0.04f, 1.9f, 0.35f), wood); for (int i = 0; i < 5; i++) Box(t, "Board", new Vector3(0, 0.05f + i * 0.45f, 0), new Vector3(0.9f, 0.03f, 0.35f), wood); }
        static void Tv(Transform r, Vector3 p, float rot) { var t = Group(r, "Tv", p, rot); Box(t, "Stand", new Vector3(0, 0.25f, 0), new Vector3(1.4f, 0.5f, 0.4f), wood); Box(t, "Screen", new Vector3(0, 0.85f, 0), new Vector3(1.2f, 0.7f, 0.06f), dark); }

        /// <summary>A single-storey bungalow: living room, kitchen, bedroom and hall, pitched roof.</summary>
        public static void Bungalow(Transform root, Vector3 o, float rotY, Material wallMat, List<Vector3> spawns)
        {
            InitMats();
            var r = new GameObject("Bungalow").transform; r.SetParent(root, false); r.localPosition = o; r.localRotation = Quaternion.Euler(0, rotY, 0);
            float W = 11f, D = 8f, H = 2.8f, t = 0.15f;
            Box(r, "Floor", new Vector3(0, 0.05f, 0), new Vector3(W, 0.1f, D), floorWood);
            Box(r, "Step", new Vector3(0f, 0.04f, -D / 2 - 0.5f), new Vector3(1.6f, 0.08f, 1f), white);
            float y = 0.1f;
            // outer walls: front door into the hall (x 0), a side door into the bedroom
            Wall(r, new Vector3(-W / 2, y, -D / 2), new Vector3(W / 2, y, -D / 2), H, t, wallMat, Window(2f), Door(5.5f), Window(8.5f));
            Wall(r, new Vector3(-W / 2, y, D / 2), new Vector3(W / 2, y, D / 2), H, t, wallMat, Window(2.5f), Window(8f, 1.4f));
            Wall(r, new Vector3(-W / 2, y, -D / 2), new Vector3(-W / 2, y, D / 2), H, t, wallMat, Window(5.5f));
            Wall(r, new Vector3(W / 2, y, -D / 2), new Vector3(W / 2, y, D / 2), H, t, wallMat, Door(2f), Window(5.5f));
            // inside: living | hall | bedroom, kitchen behind the living room
            Wall(r, new Vector3(-1.5f, y, -D / 2), new Vector3(-1.5f, y, D / 2), H, 0.1f, plaster, Door(2f), Door(6f));
            Wall(r, new Vector3(1.8f, y, -D / 2), new Vector3(1.8f, y, D / 2), H, 0.1f, plaster, Door(2f), Door(6f));
            Wall(r, new Vector3(-W / 2, y, 1f), new Vector3(-1.5f, y, 1f), H, 0.1f, plaster, Door(2f));
            Box(r, "Ceiling", new Vector3(0, y + H + 0.05f, 0), new Vector3(W, 0.1f, D), white);
            PitchedRoof(r, W, D, y + H + 0.1f, 2.2f, Mathf.Abs(o.z) % 56f < 20f ? roofTile : roofTile2, wallMat);
            // furniture, all against the walls and away from the doorways
            Sofa(r, new Vector3(-3.3f, y, -3.45f), 0f, fabric); Tv(r, new Vector3(-3.3f, y, 0.55f), 180f);
            Box(r, "Rug", new Vector3(-3.3f, y + 0.005f, -1.6f), new Vector3(2.4f, 0.01f, 1.8f), fabric2, 0f, false);
            Counter(r, new Vector3(-2.9f, y, 3.6f), 1.6f, 180f);
            Table(r, new Vector3(-4.75f, y, 2.5f), 90f); Chair(r, new Vector3(-4.1f, y, 2.5f), -90f);
            Bed(r, new Vector3(3.6f, y, 2.8f), 180f); Shelf(r, new Vector3(5.2f, y, 0.2f), -90f);
            spawns.Add(r.TransformPoint(new Vector3(0.15f, y, 0f)));
            spawns.Add(r.TransformPoint(new Vector3(-3.3f, y, -1.5f)));
            spawns.Add(r.TransformPoint(new Vector3(3.5f, y, -1.5f)));
        }

        /// <summary>
        /// A two-storey cottage. Ground: living room, kitchen, a hall with a straight staircase against the end
        /// wall. Upstairs: a bedroom and a landing round the stairwell, with a railing. The stairs have a smooth
        /// walking ramp so you go up and down them without catching.
        /// </summary>
        public static void Cottage(Transform root, Vector3 o, float rotY, Material wallMat, List<Vector3> spawns)
        {
            InitMats();
            var r = new GameObject("Cottage").transform; r.SetParent(root, false); r.localPosition = o; r.localRotation = Quaternion.Euler(0, rotY, 0);
            float W = 10f, D = 8f, H = 2.8f, t = 0.15f, slab = 0.2f;
            float up = H + slab;                                   // floor to floor
            Box(r, "Floor", new Vector3(0, 0.05f, 0), new Vector3(W, 0.1f, D), floorWood);
            Box(r, "Step", new Vector3(2.5f, 0.04f, -D / 2 - 0.5f), new Vector3(1.6f, 0.08f, 1f), white);
            float y0 = 0.1f, y1 = y0 + up;
            // stairs: against the right-hand end wall, climbing towards the back
            const int n = 15;
            float rise = up / n, tread = 0.27f, sw = 1.05f;
            float sx = W / 2 - t / 2 - sw / 2 - 0.02f, sz0 = -1.6f;
            var st = new GameObject("Stairs").transform; st.SetParent(r, false);
            for (int i = 0; i < n; i++)
            {
                float h = rise * (i + 1);
                Box(st, "Step", new Vector3(sx, y0 + h / 2f, sz0 + tread * (i + 0.5f)), new Vector3(sw, h, tread), wood);
            }
            Ramp(st, new Vector3(sx, y0 + 0.02f, sz0 - 0.15f), new Vector3(sx, y1 + 0.02f, sz0 + tread * n + 0.05f), sw - 0.05f);
            float sz1 = sz0 + tread * n;                           // top of the stairs
            // ---- ground floor
            Wall(r, new Vector3(-W / 2, y0, -D / 2), new Vector3(W / 2, y0, -D / 2), H, t, wallMat, Window(2.2f, 1.6f), Door(7.4f));
            Wall(r, new Vector3(-W / 2, y0, D / 2), new Vector3(W / 2, y0, D / 2), H, t, wallMat, Window(2.5f), Door(6.6f));
            Wall(r, new Vector3(-W / 2, y0, -D / 2), new Vector3(-W / 2, y0, D / 2), H, t, wallMat, Window(2f), Window(6f));
            Wall(r, new Vector3(W / 2, y0, -D / 2), new Vector3(W / 2, y0, D / 2), H, t, wallMat, Window(1.2f, 0.9f));
            Wall(r, new Vector3(1f, y0, -D / 2), new Vector3(1f, y0, D / 2), H, 0.1f, plaster, Door(1.5f), Door(6.4f));
            Wall(r, new Vector3(-W / 2, y0, 1f), new Vector3(1f, y0, 1f), H, 0.1f, plaster, Door(3f));
            Sofa(r, new Vector3(-2.2f, y0, -3.45f), 0f, fabric2); Tv(r, new Vector3(-2.2f, y0, 0.55f), 180f);
            Box(r, "Rug", new Vector3(-2.2f, y0 + 0.005f, -1.5f), new Vector3(2.4f, 0.01f, 1.8f), fabric, 0f, false);
            Counter(r, new Vector3(-3.6f, y0, 3.6f), 1.6f, 180f);
            Table(r, new Vector3(-0.6f, y0, 2.6f), 90f); Chair(r, new Vector3(-1.25f, y0, 2.6f), 90f);
            Shelf(r, new Vector3(1.3f, y0, 3.7f), 180f);
            // ---- upper floor slab with the stairwell left open over the stairs
            float hx0 = sx - sw / 2 - 0.05f, hz0 = sz0 - 0.05f, hz1 = sz1 + 0.05f;      // the hole
            float sy = y1 - slab / 2f;
            Box(r, "Slab", new Vector3((-W / 2 + hx0) / 2f, sy, 0f), new Vector3(hx0 + W / 2, slab, D), white);
            Box(r, "Slab", new Vector3((hx0 + W / 2) / 2f, sy, (-D / 2 + hz0) / 2f), new Vector3(W / 2 - hx0, slab, hz0 + D / 2), white);
            Box(r, "Slab", new Vector3((hx0 + W / 2) / 2f, sy, (hz1 + D / 2) / 2f), new Vector3(W / 2 - hx0, slab, D / 2 - hz1), white);
            Box(r, "UpperFloor", new Vector3((-W / 2 + hx0) / 2f, y1 + 0.005f, 0f), new Vector3(hx0 + W / 2, 0.01f, D), floorWood, 0f, false);
            // railing along the open side of the stairwell and its low end
            Box(r, "Rail", new Vector3(hx0 - 0.02f, y1 + 0.5f, (hz0 + hz1) / 2f), new Vector3(0.05f, 1f, hz1 - hz0), metal);
            Box(r, "Rail", new Vector3((hx0 + W / 2) / 2f, y1 + 0.5f, hz0 - 0.02f), new Vector3(W / 2 - hx0, 1f, 0.05f), metal);
            // ---- upper floor walls
            Wall(r, new Vector3(-W / 2, y1, -D / 2), new Vector3(W / 2, y1, -D / 2), H, t, wallMat, Window(2.2f), Window(7.2f));
            Wall(r, new Vector3(-W / 2, y1, D / 2), new Vector3(W / 2, y1, D / 2), H, t, wallMat, Window(2.5f), Window(6.6f, 0.9f));
            Wall(r, new Vector3(-W / 2, y1, -D / 2), new Vector3(-W / 2, y1, D / 2), H, t, wallMat, Window(4f));
            Wall(r, new Vector3(W / 2, y1, -D / 2), new Vector3(W / 2, y1, D / 2), H, t, wallMat, Window(2f, 0.9f));
            Wall(r, new Vector3(1f, y1, -D / 2), new Vector3(1f, y1, D / 2), H, 0.1f, plaster, Door(6.6f));
            Bed(r, new Vector3(-2f, y1, -2.7f), 0f); Shelf(r, new Vector3(-4.75f, y1, 1.5f), 90f); Bed(r, new Vector3(-2.6f, y1, 2.8f), 180f);
            Box(r, "Ceiling", new Vector3(0, y1 + H + 0.05f, 0), new Vector3(W, 0.1f, D), white);
            PitchedRoof(r, W, D, y1 + H + 0.1f, 2.4f, Mathf.Abs(o.z) % 56f < 20f ? roofTile2 : roofTile, wallMat);
            spawns.Add(r.TransformPoint(new Vector3(-2.2f, y0, -1.4f)));
            spawns.Add(r.TransformPoint(new Vector3(2.6f, y0, -2.5f)));
            spawns.Add(r.TransformPoint(new Vector3(-0.6f, y1, 0f)));
            spawns.Add(r.TransformPoint(new Vector3(2.4f, y1, 2.8f)));
        }

        /// <summary>The whole HOUSES map: a road with gardens and homes on both sides.</summary>
        public static void Neighbourhood(Transform r, out Vector3 spawn, out float yaw, out Vector3[] carls)
        {
            InitMats();
            var spawns = new List<Vector3>();
            Box(r, "Ground", new Vector3(0, -0.25f, 0), new Vector3(140f, 0.5f, 140f), grass);
            Box(r, "Road", new Vector3(0, 0.01f, 0), new Vector3(8f, 0.02f, 120f), asphalt);
            for (int i = -5; i <= 5; i++) Box(r, "Line", new Vector3(0, 0.025f, i * 11f), new Vector3(0.15f, 0.01f, 5f), white, 0, false);
            var pave = Mats.Lit(new Color(0.62f, 0.62f, 0.6f), 0.1f);
            Material[] walls = { plaster, plaster2, brick, plaster3, plaster4, brick };
            for (int s = -1; s <= 1; s += 2)
            {
                Box(r, "Pavement", new Vector3(s * 5f, 0.07f, 0), new Vector3(2f, 0.14f, 120f), pave);
                for (int k = 0; k < 4; k++)
                {
                    float z = -42f + k * 28f;
                    int idx = (k + (s > 0 ? 1 : 0)) % 2;
                    // front doors face the road
                    float face = s > 0 ? 90f : -90f;
                    if (idx == 0) Bungalow(r, new Vector3(s * 15.5f, 0f, z), face, walls[(k * 2 + (s > 0 ? 1 : 0)) % walls.Length], spawns);
                    else Cottage(r, new Vector3(s * 15f, 0f, z), face, walls[(k * 2 + (s > 0 ? 1 : 0) + 1) % walls.Length], spawns);
                    // garden path from the pavement to the front door, and a fence with a gate gap
                    Box(r, "Path", new Vector3(s * 8.3f, 0.02f, z + (idx == 0 ? 0f : -s * 2.4f)), new Vector3(5.2f, 0.03f, 1.3f), pave, 0f, false);
                    Box(r, "Fence", new Vector3(s * 6.6f, 0.5f, z - 7.5f), new Vector3(0.08f, 1f, 12f), fence);
                    Box(r, "Fence", new Vector3(s * 6.6f, 0.5f, z + 8.5f), new Vector3(0.08f, 1f, 10f), fence);
                    Box(r, "Fence", new Vector3(s * 14f, 0.5f, z + 13.5f), new Vector3(15f, 1f, 0.08f), fence);
                }
                for (int k = 0; k < 6; k++)
                {
                    Box(r, "Lamp", new Vector3(s * 4.3f, 2.2f, -50f + k * 20f), new Vector3(0.12f, 4.4f, 0.12f), dark);
                    Box(r, "LampHead", new Vector3(s * 3.9f, 4.4f, -50f + k * 20f), new Vector3(0.9f, 0.1f, 0.25f), dark);
                }
            }
            // parked cars
            for (int c = 0; c < 3; c++)
            {
                var car = Group(r, "Car", new Vector3(c % 2 == 0 ? 2.4f : -2.4f, 0f, -30f + c * 26f), c % 2 == 0 ? 0f : 180f);
                Color[] cols = { new Color(0.25f, 0.32f, 0.5f), new Color(0.55f, 0.12f, 0.1f), new Color(0.85f, 0.85f, 0.82f) };
                Box(car, "Body", new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.7f, 4.2f), Mats.Lit(cols[c], 0.6f, 0.3f));
                var cab = Box(car, "Cabin", new Vector3(0, 1.2f, -0.2f), new Vector3(1.6f, 0.55f, 2.2f), glassM);
                cab.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Box(car, "Seats", new Vector3(0, 0.95f, -0.2f), new Vector3(1.5f, 0.15f, 1.9f), dark, 0f, false);
                for (int i = 0; i < 4; i++) Box(car, "Wheel", new Vector3(i % 2 == 0 ? -0.85f : 0.85f, 0.32f, i < 2 ? -1.35f : 1.35f), new Vector3(0.25f, 0.64f, 0.64f), dark);
            }
            spawn = new Vector3(0f, 0.1f, -56f);
            yaw = 0f;
            carls = spawns.ToArray();
        }
    }

    /// <summary>A window pane: see-through, and it shatters when shot, hit hard or blasted.</summary>
    public class WindowGlass : MonoBehaviour
    {
        public static readonly List<WindowGlass> All = new List<WindowGlass>();
        public bool broken;
        static int liveShards;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void Shatter(Vector3 point, Vector3 dir, float force)
        {
            if (broken) return;
            broken = true;
            GetComponent<Collider>().enabled = false;
            var mr = GetComponent<MeshRenderer>();
            mr.enabled = false;
            Sfx.Play("glass", point, 1f, 1f, 3f, 0.08f);
            // the pane falls apart into shards, most of them going the way it was hit
            Vector3 s = transform.lossyScale;
            int count = Mathf.Clamp(Mathf.RoundToInt(s.x * s.y * 14f), 8, 26);
            for (int i = 0; i < count && liveShards < 220; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = "Shard";
                g.transform.position = transform.TransformPoint(new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.45f, 0.45f), 0f));
                g.transform.rotation = transform.rotation * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                float a = Random.Range(0.04f, 0.16f);
                g.transform.localScale = new Vector3(a, a * Random.Range(0.3f, 1.2f), 0.006f);
                g.GetComponent<MeshRenderer>().sharedMaterial = mr.sharedMaterial;
                var rb = g.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                float near = 1f / (1f + Vector3.Distance(g.transform.position, point) * 4f);
                rb.linearVelocity = dir.normalized * force * Random.Range(0.3f, 1.2f) * (0.4f + near) + Random.insideUnitSphere * 0.6f;
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                liveShards++;
                Destroy(g, Random.Range(6f, 10f));
                g.AddComponent<ShardCount>();
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (broken || c.rigidbody == null) return;
            float hit = c.relativeVelocity.magnitude * Mathf.Min(c.rigidbody.mass, 30f);
            if (hit > 25f) Shatter(c.GetContact(0).point, -c.GetContact(0).normal, c.relativeVelocity.magnitude * 0.6f);
        }

        /// <summary>Blast: windows near it blow in.</summary>
        public static void Blast(Vector3 pos, float radius)
        {
            foreach (var w in All.ToArray())
            {
                if (w == null || w.broken) continue;
                Vector3 p = w.transform.position;
                float d = Vector3.Distance(p, pos);
                if (d < radius) w.Shatter(p, (p - pos).normalized, Mathf.Lerp(9f, 2f, d / radius));
            }
        }

        class ShardCount : MonoBehaviour { void OnDestroy() { liveShards = Mathf.Max(0, liveShards - 1); } }
    }
}
