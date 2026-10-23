using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Where an Earl can walk on the TRENCHES map: points along every trench floor joined up where the trenches
    /// meet, the fighting positions and dugouts off them, a grid over the open field, and the ladders between the
    /// two. Paths are found with A*; a ladder on the way is climbed (LadderClimber). Empty on the other maps
    /// (there the Earls just walk straight at things, as before).
    /// </summary>
    public static class TrenchNav
    {
        public enum Edge : byte { Walk, LadderUp, LadderDown }

        public struct Link { public int to; public float cost; public Edge kind; public Ladder ladder; }

        public static readonly List<Vector3> Nodes = new List<Vector3>();
        public static readonly List<bool> Field = new List<bool>();
        static readonly List<List<Link>> links = new List<List<Link>>();
        public static bool Ready => Nodes.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Clear(); }

        public static void Clear() { Nodes.Clear(); Field.Clear(); links.Clear(); ground = null; height = null; }

        static int Add(Vector3 p, bool field)
        {
            Nodes.Add(p); Field.Add(field); links.Add(new List<Link>());
            return Nodes.Count - 1;
        }

        static void Join(int a, int b, Edge ab = Edge.Walk, Ladder l = null, float extra = 0f)
        {
            float c = Vector3.Distance(Nodes[a], Nodes[b]) * (Field[a] || Field[b] ? 1.25f : 1f) + extra;
            foreach (var k in links[a]) if (k.to == b) return;
            links[a].Add(new Link { to = b, cost = c, kind = ab, ladder = l });
            if (ab == Edge.Walk) links[b].Add(new Link { to = a, cost = c, kind = Edge.Walk });
            else links[b].Add(new Link { to = a, cost = c, kind = ab == Edge.LadderUp ? Edge.LadderDown : Edge.LadderUp, ladder = l });
        }

        static Collider ground;
        static string why;

        /// <summary>The points near (x, z), their links, and why close ones aren't joined (for checking).</summary>
        public static string Around(float x, float z, float r)
        {
            var sb = new System.Text.StringBuilder();
            var c = new Vector3(x, 0f, z);
            var near = new List<int>();
            for (int i = 0; i < Nodes.Count; i++) if (FlatD(Nodes[i], c) < r) near.Add(i);
            foreach (int i in near) sb.AppendLine($"#{i} {Nodes[i]} {(Field[i] ? "F" : "T")} links {links[i].Count}");
            foreach (int a in near) foreach (int b in near)
                {
                    if (b <= a || Field[a] != Field[b] || Vector3.Distance(Nodes[a], Nodes[b]) > 3.5f) continue;
                    bool ok = Passable(Nodes[a], Nodes[b]);
                    bool linked = false; foreach (var k in links[a]) if (k.to == b) linked = true;
                    sb.AppendLine($"  {a}-{b} d {Vector3.Distance(Nodes[a], Nodes[b]):0.0} passable {ok} linked {linked} {(ok ? "" : why)}");
                }
            for (float dx = -r; dx <= r; dx += 1f) { sb.Append("  H"); for (float dz = -r; dz <= r; dz += 1f) sb.Append($" {height(x + dx, z + dz):0.0}"); sb.AppendLine(); }
            return sb.ToString();
        }
        static float FlatD(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        static System.Func<float, float, float> height;

        /// <summary>
        /// Following the lie of the ground (on the trench map): no step up or down too big to walk (a trench wall),
        /// and nothing standing in the way at knee or chest height. The ground's own bumps don't count.
        /// </summary>
        static bool Passable(Vector3 a, Vector3 b)
        {
            float dist = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
            int n = Mathf.Max(1, Mathf.CeilToInt(dist / 0.5f));
            Vector3 prev = new Vector3(a.x, height(a.x, a.z), a.z);
            for (int i = 1; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)n);
                p.y = height(p.x, p.z);
                if (Mathf.Abs(p.y - prev.y) > 0.5f) { why = $"step {prev.y:0.00}->{p.y:0.00} at {p.x:0.0},{p.z:0.0}"; return false; }
                foreach (float h in new[] { 0.5f, 1.1f })
                {
                    Vector3 u = prev + Vector3.up * h, v = p + Vector3.up * h;
                    foreach (var hit in Physics.RaycastAll(u, (v - u).normalized, Vector3.Distance(u, v), ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider == ground || hit.collider is CharacterController || hit.collider.GetComponentInParent<HumanBody>() != null) continue;
                        if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
                        why = $"{hit.collider.name} at {hit.point}";
                        return false;
                    }
                }
                prev = p;
            }
            return true;
        }

        /// <summary>Can you walk straight from a to b (nothing solid at knee and chest height)?</summary>
        public static bool Walkable(Vector3 a, Vector3 b)
        {
            if (ground != null && height != null) return Passable(a, b);
            foreach (float h in new[] { 0.45f, 1.1f })
            {
                Vector3 p = a + Vector3.up * h, q = b + Vector3.up * h;
                foreach (var hit in Physics.RaycastAll(p, (q - p).normalized, Vector3.Distance(p, q), ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider is CharacterController || hit.collider.GetComponentInParent<HumanBody>() != null) continue;
                    if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
                    return false;
                }
            }
            return true;
        }

        /// <summary>Built by MapBuilder once the trench map stands.</summary>
        public static void Build(List<(int kind, Vector2[] pts)> lines, List<(int kind, Vector2 c, float r)> cells, System.Func<float, float, float> H, Collider groundCol)
        {
            Clear();
            ground = groundCol; height = H;
            Physics.SyncTransforms();
            // ---- the trench floors
            var lineOf = new List<int>();
            int li = 0;
            foreach (var (kind, pts) in lines)
            {
                int prev = -1;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    float len = Vector2.Distance(pts[i], pts[i + 1]);
                    for (float s = 0f; s < len; s += 2f)
                    {
                        var p2 = Vector2.Lerp(pts[i], pts[i + 1], s / len);
                        float y = H(p2.x, p2.y);
                        // (a zig-zag's corner can cut across the wall: the floor is just beside it)
                        for (int k = 0; k < 8 && y > 0.6f; k++)
                        {
                            Vector2 o = p2 + new Vector2(Mathf.Cos(k * Mathf.PI / 4f), Mathf.Sin(k * Mathf.PI / 4f)) * 0.6f;
                            float yo = H(o.x, o.y);
                            if (yo < y) { y = yo; p2 = o; }
                        }
                        if (y > 0.6f) { prev = -1; continue; }
                        // the dug trench wanders a little off the line it was drawn from: the middle of the floor
                        // across it, so the point isn't at the foot of a wall
                        Vector2 dir2 = (pts[i + 1] - pts[i]).normalized, nrm = new Vector2(-dir2.y, dir2.x);
                        float sum = 0f; int cnt = 0;
                        for (float o = -1.1f; o <= 1.1f; o += 0.1f)
                        {
                            Vector2 q = p2 + nrm * o;
                            if (H(q.x, q.y) < y + 0.25f) { sum += o; cnt++; }
                        }
                        if (cnt > 0) { p2 += nrm * (sum / cnt); y = H(p2.x, p2.y); }
                        int n = Add(new Vector3(p2.x, y + 0.05f, p2.y), false);
                        lineOf.Add(li);
                        if (prev >= 0 && Passable(Nodes[prev], Nodes[n])) Join(prev, n);
                        prev = n;
                    }
                }
                li++;
            }
            int trench = Nodes.Count;
            // where the trenches meet (and gaps along one line where the corner cut a step out)
            for (int a = 0; a < trench; a++)
                for (int b = a + 1; b < trench; b++)
                {
                    float d = Vector3.Distance(Nodes[a], Nodes[b]);
                    if (d > 3.2f) continue;   // (lines that start where another runs share a point: joined too)
                    bool linked = false;
                    foreach (var k in links[a]) if (k.to == b) { linked = true; break; }
                    if (!linked && Passable(Nodes[a], Nodes[b])) Join(a, b);
                }
            // fighting positions and dugouts
            foreach (var (kind, c, r) in cells)
            {
                float y = H(c.x, c.y);
                if (y > 0.6f) continue;
                int n = Add(new Vector3(c.x, y + 0.05f, c.y), false);
                for (int b = 0; b < trench; b++)
                    if (Vector3.Distance(Nodes[b], Nodes[n]) < 4.5f && Passable(Nodes[b], Nodes[n])) Join(n, b);
            }
            // ---- the open field: a grid of points that aren't over a trench or in a crater
            var grid = new Dictionary<Vector2Int, int>();
            const float step = 3.5f;
            for (int gx = -13; gx <= 13; gx++)
                for (int gz = -13; gz <= 14; gz++)
                {
                    float x = gx * step, z = gz * step;
                    float y = H(x, z);
                    if (y < 1.5f) continue;
                    bool open = true;
                    for (int k = 0; k < 6 && open; k++) { float an = k * Mathf.PI / 3f; if (H(x + Mathf.Cos(an) * 1.1f, z + Mathf.Sin(an) * 1.1f) < 1.4f) open = false; }
                    if (!open) continue;
                    // nothing standing right there
                    bool blocked = false;
                    foreach (var c in Physics.OverlapCapsule(new Vector3(x, y + 0.5f, z), new Vector3(x, y + 1.5f, z), 0.3f, ~0, QueryTriggerInteraction.Ignore))
                        if (c != groundCol && c.GetComponentInParent<HumanBody>() == null && !(c is CharacterController)) { blocked = true; break; }
                    if (blocked) continue;
                    grid[new Vector2Int(gx, gz)] = Add(new Vector3(x, y + 0.05f, z), true);
                }
            foreach (var kv in grid)
                for (int dx = 0; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz <= 0) continue;
                        if (!grid.TryGetValue(kv.Key + new Vector2Int(dx, dz), out int o)) continue;
                        if (Passable(Nodes[kv.Value], Nodes[o])) Join(kv.Value, o);
                    }
            // ---- the ladders: foot on the trench floor, top on the field
            foreach (var l in Ladder.All)
            {
                int foot = Add(l.foot + Vector3.up * 0.05f, false);
                int top = Add(l.top + Vector3.up * 0.05f, true);
                Join(foot, top, Edge.LadderUp, l, 6f);
                for (int b = 0; b < trench; b++)
                    if (Vector3.Distance(Nodes[b], Nodes[foot]) < 3f && Passable(Nodes[b], Nodes[foot])) Join(foot, b);
                foreach (var kv in grid)
                    if (Vector3.Distance(Nodes[kv.Value], Nodes[top]) < 5.5f && Passable(Nodes[kv.Value], Nodes[top])) Join(top, kv.Value);
            }
            int ln = 0; foreach (var l in links) ln += l.Count;
            Debug.Log($"[Carl] TrenchNav: {Nodes.Count} points ({trench} trench, {grid.Count} field, {Ladder.All.Count} ladders), {ln / 2} links");
        }

        /// <summary>No trench or crater opening up between two field points.</summary>
        static bool GroundAlong(Vector3 a, Vector3 b, System.Func<float, float, float> H)
        {
            for (int i = 1; i < 6; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / 6f);
                if (H(p.x, p.z) < 1.3f) return false;
            }
            return true;
        }

        /// <summary>How the graph hangs together (for checking): sizes of the connected pieces, ladders joined.</summary>
        public static string Report()
        {
            var comp = new int[Nodes.Count];
            for (int i = 0; i < comp.Length; i++) comp[i] = -1;
            var sizes = new List<string>();
            int c = 0;
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (comp[i] >= 0) continue;
                int n = 0, field = 0; var q = new Queue<int>(); q.Enqueue(i); comp[i] = c;
                while (q.Count > 0) { int x = q.Dequeue(); n++; if (Field[x]) field++; foreach (var k in links[x]) if (comp[k.to] < 0) { comp[k.to] = c; q.Enqueue(k.to); } }
                if (n > 2) sizes.Add($"{n}({field} field, at {Nodes[i]})");
                c++;
            }
            int ladderLinks = 0; foreach (var l in links) foreach (var k in l) if (k.kind != Edge.Walk) ladderLinks++;
            return $"{c} pieces: " + string.Join(", ", sizes) + $"; ladder links {ladderLinks}";
        }

        /// <summary>Points and links to a text file (for plotting).</summary>
        public static string Dump(string file)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Nodes.Count; i++) sb.AppendLine($"n {Nodes[i].x:0.00} {Nodes[i].y:0.00} {Nodes[i].z:0.00} {(Field[i] ? 1 : 0)}");
            for (int i = 0; i < Nodes.Count; i++) foreach (var k in links[i]) if (k.to > i || k.kind != Edge.Walk) sb.AppendLine($"e {i} {k.to} {(int)k.kind}");
            foreach (var l in Ladder.All) sb.AppendLine($"l {l.foot.x:0.00} {l.foot.z:0.00} {l.top.x:0.00} {l.top.z:0.00}");
            System.IO.File.WriteAllText(file, sb.ToString());
            return "written " + Nodes.Count;
        }

        /// <summary>What blocks the walk between neighbouring points near z (for checking).</summary>
        public static string Blockers(float zMin, float zMax)
        {
            var hits = new Dictionary<string, int>();
            int pairs = 0, ok = 0;
            for (int a = 0; a < Nodes.Count; a++)
            {
                if (Field[a] || Nodes[a].z < zMin || Nodes[a].z > zMax) continue;
                for (int b = a + 1; b < Nodes.Count && b < a + 3; b++)
                {
                    if (Field[b] || Vector3.Distance(Nodes[a], Nodes[b]) > 2.6f) continue;
                    pairs++;
                    bool clear = true;
                    foreach (float h in new[] { 0.45f, 1.1f })
                    {
                        Vector3 p = Nodes[a] + Vector3.up * h, q = Nodes[b] + Vector3.up * h;
                        foreach (var hit in Physics.RaycastAll(p, (q - p).normalized, Vector3.Distance(p, q), ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (hit.collider is CharacterController || hit.collider.GetComponentInParent<HumanBody>() != null) continue;
                            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
                            string k = hit.collider.name + "@" + h; hits[k] = hits.TryGetValue(k, out int c) ? c + 1 : 1; clear = false;
                        }
                    }
                    if (clear) ok++;
                }
            }
            var sb = new System.Text.StringBuilder($"{pairs} pairs, {ok} clear; ");
            foreach (var kv in hits) sb.Append(kv.Key + " x" + kv.Value + ", ");
            return sb.ToString();
        }

        // ------------------------------------------------------------ finding a way

        /// <summary>The nearest point you could walk straight to from here (any, if none is in reach).</summary>
        public static int Nearest(Vector3 p, bool needWalk = true, bool allowField = true)
        {
            int best = -1, any = -1; float bd = float.MaxValue, ad = float.MaxValue;
            // a few nearest by distance, the first one walkable wins
            var cand = new List<(float d, int i)>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (!allowField && Field[i]) continue;
                float d = (Nodes[i] - p).sqrMagnitude;
                // (on a different level - trench vs field - counts as far)
                if (Mathf.Abs(Nodes[i].y - p.y) > 1.2f) d += 400f;
                if (d < ad) { ad = d; any = i; }
                if (d < 144f) cand.Add((d, i));
            }
            if (!needWalk) return any;
            cand.Sort((x, y) => x.d.CompareTo(y.d));
            for (int k = 0; k < cand.Count && k < 6; k++)
                if (Walkable(p, Nodes[cand[k].i])) { best = cand[k].i; bd = cand[k].d; break; }
            return best >= 0 ? best : any;
        }

        public struct Step { public Vector3 at; public Edge kind; public Ladder ladder; }

        static float[] gScore = new float[0];
        static int[] cameFrom = new int[0];
        static Link[] cameBy = new Link[0];
        static int[] stamp = new int[0];
        static int search;

        /// <summary>
        /// A way from here to there: the points to walk through, each with how you get to it (walking, or up or
        /// down a ladder). Empty if there is none. <paramref name="allowField"/> false keeps it below ground.
        /// </summary>
        public static List<Step> Path(Vector3 from, Vector3 to, bool allowField = true, float fieldPenalty = 1f)
        {
            var result = new List<Step>();
            if (!Ready) return result;
            int s = Nearest(from, true, true), g = Nearest(to, false, allowField);
            if (s < 0 || g < 0) return result;
            int n = Nodes.Count;
            if (gScore.Length < n) { gScore = new float[n]; cameFrom = new int[n]; cameBy = new Link[n]; stamp = new int[n]; }
            search++;
            var open = new SortedSet<(float f, int i)>(Comparer<(float f, int i)>.Create((a, b) => a.f != b.f ? a.f.CompareTo(b.f) : a.i.CompareTo(b.i)));
            stamp[s] = search; gScore[s] = 0f; cameFrom[s] = -1;
            open.Add((Vector3.Distance(Nodes[s], Nodes[g]), s));
            bool found = false;
            int guard = 0;
            while (open.Count > 0 && guard++ < 20000)
            {
                var cur = open.Min; open.Remove(cur);
                int c = cur.i;
                if (c == g) { found = true; break; }
                foreach (var k in links[c])
                {
                    if (!allowField && Field[k.to] && k.to != g) continue;
                    // (a penalty under 1 is a preference for the field: the trenches cost more instead)
                    float cost = k.cost * (Field[k.to] ? fieldPenalty : fieldPenalty < 1f ? 1f / fieldPenalty : 1f);
                    float ng = gScore[c] + cost;
                    if (stamp[k.to] == search && ng >= gScore[k.to]) continue;
                    if (stamp[k.to] == search) open.Remove((gScore[k.to] + Vector3.Distance(Nodes[k.to], Nodes[g]), k.to));
                    stamp[k.to] = search; gScore[k.to] = ng; cameFrom[k.to] = c; cameBy[k.to] = k;
                    open.Add((ng + Vector3.Distance(Nodes[k.to], Nodes[g]), k.to));
                }
            }
            if (!found) return result;
            for (int c = g; c != s && c >= 0; c = cameFrom[c])
                result.Add(new Step { at = Nodes[c], kind = cameBy[c].kind, ladder = cameBy[c].ladder });
            result.Reverse();
            result.Insert(0, new Step { at = Nodes[s], kind = Edge.Walk });
            // and on to the exact spot if it can be walked to
            if (Vector3.Distance(Nodes[g], to) > 0.5f && Walkable(Nodes[g], to)) result.Add(new Step { at = to, kind = Edge.Walk });
            return result;
        }

        /// <summary>A random walkable point within reach of p, meeting a test (somewhere to wander to).</summary>
        public static Vector3? RandomNear(Vector3 p, float r, System.Func<int, bool> ok = null)
        {
            var c = new List<int>();
            for (int i = 0; i < Nodes.Count; i++)
                if ((Nodes[i] - p).sqrMagnitude < r * r && (ok == null || ok(i))) c.Add(i);
            if (c.Count == 0) return null;
            return Nodes[c[Random.Range(0, c.Count)]];
        }

        public static IEnumerable<int> Within(Vector3 p, float r)
        {
            for (int i = 0; i < Nodes.Count; i++) if ((Nodes[i] - p).sqrMagnitude < r * r) yield return i;
        }
    }
}
