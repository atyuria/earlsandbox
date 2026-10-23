using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    public static partial class MapBuilder
    {
        static Material burlap, rust, burnt, wire;

        /// <summary>
        /// What the TRENCHES map has on top of the Blender ground: ladders out of the front lines, and things to hide
        /// behind up on the field between them - sandbag walls, wrecked armour, steel hedgehogs, log piles, wire, the
        /// thrown-up rims of the shell craters.
        /// </summary>
        static void TrenchExtras(Transform r, TrenchData data, MeshCollider ground, System.Random rng)
        {
            if (burlap == null)
            {
                burlap = Mats.Lit(new Color(0.42f, 0.37f, 0.27f), 0.05f);
                rust = Mats.Lit(new Color(0.23f, 0.17f, 0.13f), 0.25f, 0.4f);
                burnt = Mats.Lit(new Color(0.11f, 0.1f, 0.09f), 0.15f, 0.3f);
                wire = Mats.Lit(new Color(0.3f, 0.29f, 0.27f), 0.4f, 0.8f);
            }
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float H(float x, float z) => ground.Raycast(new Ray(new Vector3(x, 30f, z), Vector3.down), out var h, 60f) ? h.point.y : 0f;
            Vector3 G(float x, float z) => new Vector3(x, H(x, z), z);
            bool Field(float x, float z, float rad)
            {
                if (H(x, z) < 1.5f) return false;
                for (int k = 0; k < 6; k++) { float a = k * Mathf.PI / 3f; if (H(x + Mathf.Cos(a) * rad, z + Mathf.Sin(a) * rad) < 1.5f) return false; }
                return true;
            }
            var placed = new List<(Vector2 p, float r)>();
            bool Free(float x, float z, float rad)
            {
                foreach (var (p, pr) in placed) if (Vector2.Distance(p, new Vector2(x, z)) < rad + pr + 1f) return false;
                return true;
            }
            void Take(float x, float z, float rad) => placed.Add((new Vector2(x, z), rad));

            // ---- ladders: every dozen metres or so along both front lines, on alternate walls
            Ladder.All.Clear();
            foreach (var (kind, pts) in data.lines)
            {
                if (kind > 1) continue;
                float run = R(2f, 6f);
                int alt = 0;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    float len = Vector2.Distance(a, b);
                    for (float s = 0.6f; s < len - 0.6f; s += 0.5f)
                    {
                        run -= 0.5f;
                        if (run > 0f) continue;
                        var p2 = Vector2.Lerp(a, b, s / len);
                        Vector3 d3 = new Vector3(b.x - a.x, 0f, b.y - a.y) / len, n3 = new Vector3(-d3.z, 0f, d3.x);
                        // toward the enemy (the friendly line is at -z, the enemy's at +z), then the other way
                        float toward = kind == 0 ? 1f : -1f;
                        if (Mathf.Sign(n3.z) != Mathf.Sign(toward)) n3 = -n3;
                        if (alt++ % 3 == 2) n3 = -n3;
                        if (TryLadder(r, ground, G(p2.x, p2.y), n3)) run = R(9f, 15f);
                        else run = 1f;
                    }
                }
            }
            foreach (var l in Ladder.All) Take(l.top.x, l.top.z, 1.2f);

            // ---- crater rims: the earth thrown up round every shell hole, low enough to lie behind
            foreach (var (c, rad) in data.craters)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(rad * 4f), 7, 16);
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n + R(-0.15f, 0.15f);
                    float rr = rad + R(0.1f, 0.6f);
                    float x = c.x + Mathf.Cos(a) * rr, z = c.y + Mathf.Sin(a) * rr;
                    float y = H(x, z);
                    if (y < 1.4f) continue;
                    var g = Blob(r, "CraterRim", new Vector3(x, y - 0.08f, z), new Vector3(R(1f, 1.8f), R(0.3f, 0.55f), R(0.7f, 1.1f)), roofEarth, a * -Mathf.Rad2Deg + 90f, true);
                    g.AddComponent<Surface>().kind = SurfaceKind.Dirt;
                }
                Deco(r, "Scorch", new Vector3(c.x, H(c.x, c.y) + 0.02f, c.y), new Vector3(rad * 1.1f, 0.01f, rad * 1.1f), burnt, new Vector3(0f, R(0f, 180f), 0f));
                Take(c.x, c.y, rad + 0.5f);
            }

            // ---- sandbag walls: low fighting positions dug in out on the field
            for (int k = 0, tries = 0; k < 16 && tries < 400; tries++)
            {
                float x = R(-42f, 42f), z = R(-16f, 18f);
                if (!Field(x, z, 2f) || !Free(x, z, 2f)) continue;
                SandbagWall(r, G(x, z), R(-30f, 30f) + (rng.NextDouble() < 0.5 ? 0f : 90f), R(2.2f, 4f), rng.Next(3, 6), rng);
                Take(x, z, 2f); k++;
            }
            // ---- burnt-out armour: one out in the middle, one each side nearer the lines
            foreach (var zr in new[] { (-4f, 4f), (-14f, -8f), (10f, 16f) })
                for (int tries = 0; tries < 60; tries++)
                {
                    float x = R(-36f, 36f), z = R(zr.Item1, zr.Item2);
                    if (!Field(x, z, 3.6f) || !Free(x, z, 3.6f)) continue;
                    Wreck(r, G(x, z), R(0f, 360f), rng);
                    Take(x, z, 3.6f); break;
                }
            // ---- steel hedgehogs and concrete blocks
            for (int k = 0, tries = 0; k < 14 && tries < 300; tries++)
            {
                float x = R(-44f, 44f), z = R(-15f, 17f);
                if (!Field(x, z, 1f) || !Free(x, z, 1f)) continue;
                Hedgehog(r, G(x, z), R(0f, 90f));
                Take(x, z, 1f); k++;
            }
            for (int k = 0, tries = 0; k < 8 && tries < 200; tries++)
            {
                float x = R(-42f, 42f), z = R(-14f, 16f);
                if (!Field(x, z, 1.5f) || !Free(x, z, 1.5f)) continue;
                var y = R(0f, 360f);
                Prop(r, "concrete_road_barrier", G(x, z) - Vector3.up * 0.05f, y, 1f, true, new Vector3(R(-6f, 6f), 0f, R(-6f, 6f)));
                Take(x, z, 1.5f); k++;
            }
            // ---- log piles (the timber that never made it into a dugout roof)
            for (int k = 0, tries = 0; k < 8 && tries < 200; tries++)
            {
                float x = R(-44f, 44f), z = R(-20f, 22f);
                if (!Field(x, z, 1.8f) || !Free(x, z, 1.8f)) continue;
                var c = G(x, z); float yaw = R(0f, 180f);
                Vector3 d = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, sd = Vector3.Cross(Vector3.up, d);
                int rows = rng.Next(2, 4);
                for (int row = 0; row < rows; row++)
                    for (int j = 0; j < 4 - row; j++)
                    {
                        Vector3 o = c + sd * ((j - (3 - row) * 0.5f) * 0.26f) + Vector3.up * (0.12f + row * 0.22f);
                        float l = R(1.5f, 1.9f);
                        Pole(r, "Log", o - d * l + sd * R(-0.05f, 0.05f), o + d * l, R(0.11f, 0.14f), bark, true);
                    }
                Take(x, z, 1.8f); k++;
            }
            // ---- barbed wire in front of both lines, with gaps
            foreach (float zw in new[] { -17.5f, 19.5f })
            {
                float x = -44f;
                while (x < 44f)
                {
                    float runLen = R(6f, 14f);
                    WireRun(r, ground, new Vector2(x, zw + R(-1.5f, 1.5f)), new Vector2(Mathf.Min(44f, x + runLen), zw + R(-1.5f, 1.5f)), rng);
                    x += runLen + R(3f, 7f);
                }
            }
            // ---- smashed stumps where the shelling took the trees, and kit left lying about
            for (int k = 0, tries = 0; k < 18 && tries < 300; tries++)
            {
                float x = R(-46f, 46f), z = R(-30f, 32f);
                if (!Field(x, z, 0.4f)) continue;
                var p = G(x, z); float h = R(0.5f, 1.8f), rad = R(0.12f, 0.24f);
                Pole(r, "ShellStump", p - Vector3.up * 0.2f, p + new Vector3(R(-0.1f, 0.1f), h, R(-0.1f, 0.1f)), rad, bark, true);
                for (int j = 0; j < 3; j++)
                {
                    Vector3 tp = p + Vector3.up * h;
                    Pole(r, "Splinter", tp, tp + new Vector3(R(-0.15f, 0.15f), R(0.15f, 0.4f), R(-0.15f, 0.15f)), rad * 0.3f, bark, false);
                }
                k++;
            }
            string[] junk = { "ammo_box", "metal_jerrycan_green", "old_military_crate", "ammo_box", "barrel_03" };
            for (int k = 0, tries = 0; k < 16 && tries < 300; tries++)
            {
                float x = R(-44f, 44f), z = R(-17f, 19f);
                if (!Field(x, z, 0.5f) || !Free(x, z, 0.5f)) continue;
                Prop(r, junk[rng.Next(junk.Length)], G(x, z), R(0f, 360f), 1f, true, new Vector3(R(-8f, 8f), 0f, R(-25f, 25f)));
                k++;
            }
        }

        /// <summary>A ladder against the wall of the trench at this floor point, climbing out along <paramref name="n"/>.</summary>
        static bool TryLadder(Transform r, MeshCollider ground, Vector3 floor, Vector3 n)
        {
            float H(float x, float z) => ground.Raycast(new Ray(new Vector3(x, 30f, z), Vector3.down), out var h, 60f) ? h.point.y : 0f;
            if (floor.y > 0.5f) return false;
            // the wall: straight out from the floor at waist height
            if (!ground.Raycast(new Ray(floor + Vector3.up * 0.8f, n), out var wh, 1.6f)) return false;
            Vector3 wall = wh.point;
            // the lip, and somewhere to stand beyond it (open field, not another trench or a crater)
            Vector3 lip = Vector3.zero; bool found = false;
            for (float d = 0.05f; d < 1.6f; d += 0.08f)
            {
                Vector3 q = wall + n * d;
                float y = H(q.x, q.z);
                if (y > 1.35f) { lip = new Vector3(q.x, y, q.z); found = true; break; }
            }
            if (!found) return false;
            Vector3 top = lip + n * 1.0f;
            top.y = H(top.x, top.z);
            if (top.y < 1.35f || Mathf.Abs(top.y - lip.y) > 0.5f) return false;
            for (int k = 0; k < 5; k++)
            {
                Vector3 q = top + n * 0.4f + Quaternion.Euler(0f, k * 72f, 0f) * Vector3.forward * 0.6f;
                if (H(q.x, q.z) < 1.3f) return false;
            }
            foreach (var o in Ladder.All) if ((o.foot - floor).sqrMagnitude < 36f) return false;
            Vector3 foot = floor + n * Mathf.Max(0f, Vector3.Distance(new Vector3(floor.x, 0f, floor.z), new Vector3(wall.x, 0f, wall.z)) - 0.55f);
            foot.y = floor.y;
            Ladder.All.Add(new Ladder { foot = foot, lip = lip, top = top, outward = n });

            // the ladder itself: two poles from the floor near the wall to just above the lip, rungs between
            var go = new GameObject("Ladder");
            go.transform.SetParent(r, false);
            Vector3 side = Vector3.Cross(Vector3.up, n) * 0.22f;
            Vector3 b0 = new Vector3(wall.x, floor.y, wall.z) - n * 0.42f, t0 = lip + Vector3.up * 0.45f + n * 0.05f;
            for (int s = -1; s <= 1; s += 2)
                Pole(go.transform, "LadderRail", b0 + side * s - Vector3.up * 0.1f, t0 + side * s, 0.035f, logMat, false);
            float len = Vector3.Distance(b0, t0);
            for (float d = 0.3f; d < len - 0.1f; d += 0.32f)
            {
                Vector3 c = Vector3.Lerp(b0, t0, d / len);
                Pole(go.transform, "Rung", c - side * 1.05f, c + side * 1.05f, 0.022f, logMat, false);
            }
            return true;
        }

        static void SandbagWall(Transform r, Vector3 c, float yaw, float length, int layers, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var go = new GameObject("SandbagWall");
            go.transform.SetParent(r, false);
            go.transform.localPosition = c;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // gently curved, a few bags thick at the bottom
            int per = Mathf.CeilToInt(length / 0.5f);
            for (int l = 0; l < layers; l++)
            {
                float off = (l % 2) * 0.25f;
                for (int i = 0; i < per - (l > 2 ? 1 : 0); i++)
                {
                    float x = -length * 0.5f + off + i * 0.5f;
                    float bend = x * x * 0.05f;
                    Blob(go.transform, "Sandbag", new Vector3(x, 0.1f + l * 0.19f, bend + R(-0.03f, 0.03f)), new Vector3(0.55f, 0.22f, 0.34f), burlap, R(-8f, 8f), false);
                    if (l < 2) Blob(go.transform, "Sandbag", new Vector3(x + 0.12f, 0.1f + l * 0.19f, bend + 0.32f), new Vector3(0.55f, 0.22f, 0.34f), burlap, R(-8f, 8f), false);
                }
            }
            // one solid block for bullets and bodies (the bags are only looks): earth, so bullets throw dirt
            var box = new GameObject("SandbagCover");
            box.transform.SetParent(go.transform, false);
            float hgt = 0.1f + layers * 0.19f;
            box.transform.localPosition = new Vector3(0f, hgt * 0.5f, 0.12f);
            var bc = box.AddComponent<BoxCollider>();
            bc.size = new Vector3(length + 0.3f, hgt, 0.62f);
            box.AddComponent<Surface>().kind = SurfaceKind.Dirt;
            go.isStatic = true;
        }

        static void Wreck(Transform r, Vector3 c, float yaw, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var go = new GameObject("Wreck");
            go.transform.SetParent(r, false);
            go.transform.localPosition = c - Vector3.up * 0.25f;
            go.transform.localRotation = Quaternion.Euler(R(-4f, 4f), yaw, R(-6f, 6f));
            var t = go.transform;
            // a BMP-ish hull on its belly, tracks thrown, turret knocked askew, hatches open
            Box(t, "Hull", new Vector3(0f, 0.95f, 0f), new Vector3(2.9f, 1.1f, 6.4f), burnt);
            var glacis = Box(t, "Glacis", new Vector3(0f, 1.05f, 3.4f), new Vector3(2.9f, 0.9f, 1.3f), burnt);
            glacis.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            var tur = Box(t, "Turret", new Vector3(R(-0.3f, 0.3f), 1.75f, -0.2f), new Vector3(1.6f, 0.55f, 1.8f), burnt);
            tur.transform.localRotation = Quaternion.Euler(R(-5f, 5f), R(-40f, 40f), R(-8f, 8f));
            Pole(tur.transform, "Gun", new Vector3(0f, 0.1f, 0.5f), new Vector3(0f, 0.25f, 1.9f), 0.05f, burnt, true);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, "Track", new Vector3(s * 1.55f, 0.42f, 0f), new Vector3(0.4f, 0.75f, 6.2f), rust);
                for (int w = 0; w < 6; w++)
                    Pole(t, "Wheel", new Vector3(s * 1.4f, 0.4f, -2.4f + w * 0.95f), new Vector3(s * 1.75f, 0.4f, -2.4f + w * 0.95f), 0.33f, rust, false);
            }
            // a thrown track lying beside it
            var thrown = Box(t, "ThrownTrack", new Vector3(2.6f, 0.08f, R(-1f, 1f)), new Vector3(0.45f, 0.08f, 4.5f), rust);
            thrown.transform.localRotation = Quaternion.Euler(0f, R(-20f, 20f), 0f);
            go.isStatic = true;
        }

        static void Hedgehog(Transform r, Vector3 c, float yaw)
        {
            var go = new GameObject("Hedgehog");
            go.transform.SetParent(r, false);
            go.transform.localPosition = c + Vector3.up * 0.55f;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // three steel beams through one point, each along a different axis, tipped onto two ends
            foreach (var e in new[] { new Vector3(35f, 0f, 0f), new Vector3(-35f, 0f, 90f), new Vector3(0f, 90f, 35f) })
            {
                var b = Box(go.transform, "Beam", Vector3.zero, new Vector3(0.12f, 1.9f, 0.12f), rust);
                b.transform.localRotation = Quaternion.Euler(e);
            }
            go.isStatic = true;
        }

        static void WireRun(Transform r, MeshCollider ground, Vector2 a, Vector2 b, System.Random rng)
        {
            float R(float x, float y) => x + (float)rng.NextDouble() * (y - x);
            float H(float x, float z) => ground.Raycast(new Ray(new Vector3(x, 30f, z), Vector3.down), out var h, 60f) ? h.point.y : 0f;
            float len = Vector2.Distance(a, b);
            var go = new GameObject("Wire");
            go.transform.SetParent(r, false);
            Vector3 prev = default; bool have = false;
            for (float s = 0f; s <= len; s += 2.4f)
            {
                var p2 = Vector2.Lerp(a, b, s / len);
                float y = H(p2.x, p2.y);
                Vector3 post = new Vector3(p2.x, y, p2.y);
                if (y < 1.4f) { have = false; continue; }   // over a trench or into a crater: the wire's down there
                Pole(go.transform, "WirePost", post - Vector3.up * 0.2f, post + new Vector3(R(-0.08f, 0.08f), R(0.8f, 1.05f), R(-0.08f, 0.08f)), 0.035f, rust, false);
                if (have)
                    for (int k = 0; k < 3; k++)
                    {
                        float hy = 0.25f + k * 0.3f;
                        Vector3 sag = Vector3.up * -0.06f;
                        Vector3 m = (prev + post) * 0.5f + Vector3.up * hy + sag;
                        Pole(go.transform, "Strand", prev + Vector3.up * hy, m, 0.006f, wire, false);
                        Pole(go.transform, "Strand", m, post + Vector3.up * hy, 0.006f, wire, false);
                    }
                prev = post; have = true;
            }
        }
    }
}
