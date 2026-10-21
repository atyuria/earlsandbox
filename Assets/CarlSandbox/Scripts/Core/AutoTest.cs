using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Debug playtest: shoots, stabs and drags Carls, then photographs the results close up with its own
    /// camera into Screenshots/autotest. Started from the editor (AutoTest.Run()) or with Shift+F4.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string dir;
        Camera shot;
        RenderTexture rt;

        /// <summary>One picture through a throwaway camera (all layers, or just one body if given). For quick checks.</summary>
        public static void Photo(string name, Vector3 at, Vector3 from, HumanBody only = null, float fov = 40f)
        {
            string d = Path.Combine(Application.dataPath, "..", "Screenshots", "autotest");
            Directory.CreateDirectory(d);
            var go = new GameObject("PhotoCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = fov; cam.nearClipPlane = 0.02f;
            var rt = new RenderTexture(900, 700, 24); cam.targetTexture = rt;
            var moved = new List<(GameObject, int)>();
            if (only != null)
            {
                foreach (var t in only.GetComponentsInChildren<Transform>()) { moved.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = 31; }
                cam.cullingMask = 1 << 31; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            }
            else cam.cullingMask = SandboxGame.Instance.cam.cullingMask;
            go.transform.position = from; go.transform.LookAt(at); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(900, 700, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(d, name + ".png"), tex.EncodeToPNG());
            foreach (var (g, l) in moved) g.layer = l;
            cam.targetTexture = null; rt.Release(); Destroy(tex); DestroyImmediate(go);
        }

        public static void Run()
        {
            var old = FindFirstObjectByType<AutoTest>();
            if (old != null) Destroy(old.gameObject);
            new GameObject("AutoTest").AddComponent<AutoTest>();
        }

        void Start()
        {
            dir = Path.Combine(Application.dataPath, "..", "Screenshots", "autotest");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            rt = new RenderTexture(900, 700, 24);
            shot = new GameObject("AutoTestCam").AddComponent<Camera>();
            shot.transform.SetParent(transform);
            shot.fieldOfView = 40f; shot.nearClipPlane = 0.02f; shot.targetTexture = rt; shot.enabled = false;
            var main = SandboxGame.Instance.cam;
            shot.cullingMask = main.cullingMask;
            StartCoroutine(Sequence());
        }

        void OnDestroy() { if (rt != null) rt.Release(); }

        void Snap(string name, Vector3 target, Vector3 from)
        {
            shot.transform.position = from;
            shot.transform.LookAt(target);
            shot.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        /// <summary>Photographs a part's biggest wound on its own (nothing else rendered), looking into it.</summary>
        void SnapWound(string name, BodyPart bp, float dist = 0.18f, int which = -1)
        {
            if (bp == null || bp.holes.Count == 0) { Debug.Log("[AutoTest] no wound on " + name); return; }
            int best = which;
            if (best < 0) { best = 0; for (int i = 1; i < bp.holes.Count; i++) if (bp.holes[i].w > bp.holes[best].w) best = i; }
            Vector3 c = bp.transform.TransformPoint(bp.holes[best]);
            Vector4 ax = bp.holeAxes[best];
            Vector3 outward = -bp.transform.TransformDirection(new Vector3(ax.x, ax.y, ax.z)).normalized;
            var layers = new System.Collections.Generic.List<(GameObject, int)>();
            foreach (var t in bp.GetComponentsInChildren<Transform>()) { layers.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = 31; }
            int mask = shot.cullingMask; var flags = shot.clearFlags; var bg = shot.backgroundColor;
            shot.cullingMask = 1 << 31; shot.clearFlags = CameraClearFlags.SolidColor; shot.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            Vector3 side = Vector3.Cross(outward, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(outward, Vector3.up).normalized : Vector3.right;
            Snap(name + "_straight", c, c + outward * dist);
            Snap(name + "_angled", c, c + (outward * 0.8f + side * 0.6f).normalized * dist);
            var wc = bp.GetComponentInChildren<WoundChannels>();
            if (wc != null) { var wr = wc.GetComponent<MeshRenderer>(); wr.enabled = false; Snap(name + "_nochannel", c, c + outward * dist); wr.enabled = true; }
            shot.cullingMask = mask; shot.clearFlags = flags; shot.backgroundColor = bg;
            foreach (var (go, l) in layers) go.layer = l;
            Debug.Log($"[AutoTest] {name}: hole r={bp.holes[best].w:F3} depth={ax.w:F3} of {bp.holes.Count}");
        }

        /// <summary>Renders through the real player camera (its renderer settings), from a chosen spot.</summary>
        void SnapMain(string name, Vector3 target, Vector3 from)
        {
            var cam = SandboxGame.Instance.cam;
            var t = cam.transform; Vector3 p = t.position; Quaternion r = t.rotation; var tt = cam.targetTexture;
            t.position = from; t.LookAt(target);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = tt;
            t.position = p; t.rotation = r;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        void SnapPartOnly(string name, BodyPart bp, Vector3 target, Vector3 from)
        {
            var ts = bp.GetComponentsInChildren<Transform>(); var layers = new int[ts.Length];
            for (int i = 0; i < ts.Length; i++) { layers[i] = ts[i].gameObject.layer; ts[i].gameObject.layer = 31; }
            int mask = shot.cullingMask; var flags = shot.clearFlags;
            shot.cullingMask = 1 << 31; shot.clearFlags = CameraClearFlags.SolidColor; shot.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            Snap(name, target, from);
            shot.cullingMask = mask; shot.clearFlags = flags;
            for (int i = 0; i < ts.Length; i++) ts[i].gameObject.layer = layers[i];
        }

        void Around(string name, Vector3 target, float dist)
        {
            Snap(name + "_front", target, target + new Vector3(0, 0.15f, dist));
            Snap(name + "_back", target, target + new Vector3(0, 0.15f, -dist));
            Snap(name + "_left", target, target + new Vector3(-dist, 0.15f, 0));
            Snap(name + "_right", target, target + new Vector3(dist, 0.15f, 0));
            Snap(name + "_top", target, target + new Vector3(0.05f, dist, 0));
        }

        /// <summary>AK rounds into a thigh, then the same wound through the player camera from near to far.</summary>
        IEnumerator DistanceTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(9f, 0f, 9f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            e.ragdoll.Fall("test");
            yield return new WaitForSeconds(3f);
            foreach (var id in new[] { PartId.ForearmL, PartId.HandL, PartId.ThighR, PartId.Chest })
            {
                var bp = e[id];
                for (int i = 0; i < 3; i++)
                {
                    Vector3 c = bp.transform.position;
                    Vector3 from = c + new Vector3(0.015f * i, 1.2f, 0.05f);
                    Ballistics.Instance.Fire(from, (c - from).normalized, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                    yield return new WaitForSeconds(0.25f);
                }
            }
            yield return new WaitForSeconds(5f);
            var sb = new System.Text.StringBuilder();
            foreach (var bp in e.parts)
            {
                if (bp == null || bp.holes.Count == 0) continue;
                var wc = bp.GetComponentInChildren<WoundChannels>();
                var m = wc != null ? wc.GetComponent<MeshFilter>().sharedMesh : null;
                Color avg = Color.clear; if (m != null) { var cs = m.colors; foreach (var c in cs) avg += c; if (cs.Length > 0) avg /= cs.Length; }
                sb.AppendLine($"{bp.id}: holes {bp.holes.Count} maxR {bp.holes[0].w:F3} uvR {bp.uvRadius:F3} channelVerts {(m != null ? m.vertexCount : -1)} avgCol {avg} shader {(wc != null ? wc.GetComponent<MeshRenderer>().sharedMaterial.shader.name : "-")}");
            }
            Debug.Log("[AutoTest] " + sb);
            Vector3 w = e[PartId.ForearmL].transform.position;
            foreach (float dist in new[] { 0.35f, 0.8f, 1.6f, 3f })
            {
                w = e[PartId.ForearmL].transform.position;
                SnapMain($"5_dist_{dist:0.0}", w, w + new Vector3(0.15f, 1f, -0.35f).normalized * dist);
                yield return null;
            }
            Debug.Log("[AutoTest] distance test done");
        }

        /// <summary>Digging into the gut, a bone-deep forearm hit, a hit on the edge of a wound, and variety.</summary>
        IEnumerator OrganTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var carls = new HumanBody[2];
            for (int n = 0; n < 2; n++)
            {
                carls[n] = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(6f + n * 2.5f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            }
            yield return new WaitForSeconds(1.2f);
            foreach (var c in carls) c.ragdoll.Fall("test");
            yield return new WaitForSeconds(3f);
            for (int n = 0; n < 2; n++)
            {
                var e = carls[n];
                var ab = e[PartId.Abdomen];
                Vector3 hitPt = ab.transform.position;
                for (int i = 0; i < 3; i++)          // three rounds into the same spot: dig into the gut
                {
                    Vector3 from = hitPt + Vector3.up * 1.2f;
                    Ballistics.Instance.Fire(from, (hitPt - from).normalized, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                    yield return new WaitForSeconds(0.04f);
                }
                var fa = e[PartId.ForearmR];
                Vector3 fp = fa.transform.position;
                Ballistics.Instance.Fire(fp + Vector3.up * 1.2f, Vector3.down, 375f, 0.0075f, 0.009f, HitKind.Bullet, false);
                yield return new WaitForSeconds(0.04f);
                var th = e[PartId.ThighL];
                Vector3 tp = th.transform.position;
                Ballistics.Instance.Fire(tp + Vector3.up * 1.2f, Vector3.down, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                yield return new WaitForSeconds(0.04f);
                Vector3 side = th.transform.TransformDirection(Vector3.right) * 0.022f;   // the edge of that wound
                Ballistics.Instance.Fire(tp + side + Vector3.up * 1.2f, Vector3.down, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(2f);
            for (int n = 0; n < 2; n++)
            {
                var e = carls[n];
                SnapWound($"6_gut_c{n}", e[PartId.Abdomen], 0.16f);
                SnapWound($"6_forearm_c{n}", e[PartId.ForearmR], 0.12f);
                SnapWound($"6_thigh_edge_c{n}", e[PartId.ThighL], 0.16f);
                var sb = new System.Text.StringBuilder();
                foreach (var id in new[] { PartId.Abdomen, PartId.ForearmR, PartId.ThighL })
                {
                    var bp = e[id]; sb.Append(id + ":");
                    for (int i = 0; i < bp.holes.Count; i++) sb.Append($" r{bp.holes[i].w:F3}/d{bp.holeAxes[i].w:F3}");
                    sb.Append(" | ");
                }
                Debug.Log("[AutoTest] carl " + n + " " + sb);
            }
            // open the belly: a tight group of rounds into the abdomen
            var f = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(3f, 0f, 11f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            f.ragdoll.Fall("test");
            yield return new WaitForSeconds(3f);
            var belly = f[PartId.Abdomen];
            Vector3 bc = belly.transform.position;
            for (int i = 0; i < 12; i++)
            {
                Vector3 off = new Vector3((i % 4 - 1.5f) * 0.025f, 0f, (i / 4 - 1f) * 0.025f);
                Ballistics.Instance.Fire(bc + off + Vector3.up * 1.2f, Vector3.down, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            // look at the densest cluster of wounds, from outside along their mean direction
            int bestH = 0, bestN = -1;
            for (int i = 0; i < belly.holes.Count; i++)
            {
                int nb = 0; for (int j = 0; j < belly.holes.Count; j++) if (Vector3.Distance(belly.holes[i], belly.holes[j]) < 0.06f) nb++;
                if (nb > bestN) { bestN = nb; bestH = i; }
            }
            Vector3 hc = Vector3.zero, ho = Vector3.zero; int cnt = 0;
            for (int j = 0; j < belly.holes.Count; j++)
                if (Vector3.Distance(belly.holes[bestH], belly.holes[j]) < 0.06f)
                { hc += belly.transform.TransformPoint(belly.holes[j]); ho -= belly.transform.TransformDirection((Vector3)belly.holeAxes[j]); cnt++; }
            hc /= cnt; ho.Normalize();
            SnapPartOnly("6_belly_top", belly, hc, hc + ho * 0.3f);
            SnapPartOnly("6_belly_close", belly, hc, hc + (ho + Vector3.Cross(ho, Vector3.up).normalized * 0.4f).normalized * 0.17f);
            Debug.Log("[AutoTest] belly holes " + belly.holes.Count);
            Debug.Log("[AutoTest] organ test done");
        }

        /// <summary>.50 rounds spread over the chest, seen close through the player camera (ribs must not poke out).</summary>
        IEnumerator ChestTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(5f, 0f, 9f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            e.ragdoll.Fall("test");
            yield return new WaitForSeconds(3f);
            var ch = e.Chest;
            for (int i = 0; i < 10; i++)
            {
                Vector3 c = ch.transform.position + new Vector3(Random.Range(-0.08f, 0.08f), 0f, Random.Range(-0.08f, 0.08f));
                Ballistics.Instance.Fire(c + Vector3.up * 1.5f, Vector3.down, 890f, 0.0421f, 0.0127f, HitKind.Bullet, false);
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitForSeconds(3f);
            // look at the side of the chest facing up, from close and further away
            Vector3 cc = ch.transform.position;
            RaycastHit hit;
            Vector3 top = Physics.Raycast(cc + Vector3.up * 1f, Vector3.down, out hit, 2f) ? hit.point : cc + Vector3.up * 0.12f;
            SnapMain("8_chest_close", top, top + new Vector3(0.03f, 0.35f, -0.12f));
            SnapMain("8_chest_mid", top, top + new Vector3(0.1f, 0.7f, -0.3f));
            SnapMain("8_chest_side", top, top + new Vector3(0.45f, 0.25f, -0.1f));
            Debug.Log("[AutoTest] chest holes " + ch.holes.Count + ", chest test done");
        }

        static void Buckshot(Vector3 from, Vector3 at)
        {
            Vector3 fwd = (at - from).normalized, right = Vector3.Cross(fwd, Vector3.up).normalized;
            for (int p = 0; p < 9; p++)
            {
                Vector3 d = Quaternion.AngleAxis(Random.Range(0f, 360f), fwd) * Quaternion.AngleAxis(Mathf.Sqrt(Random.value) * 2.4f, right) * fwd;
                Ballistics.Instance.Fire(from, d, 400f, 0.0035f, 0.0084f, HitKind.Bullet, false);
            }
        }

        static string Severed(HumanBody h)
        {
            var sb = new System.Text.StringBuilder();
            // where each limb came off (the segment whose joint broke)
            foreach (var bp in h.parts) if (bp != null && bp.severed && bp.joint == null && bp.id != PartId.Pelvis) sb.Append(bp.id + " ");
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        /// <summary>Photographs a part with its skin hidden, so the bones themselves show.</summary>
        void SnapBones(string name, BodyPart bp, float dist)
        {
            bp.skin.enabled = false;
            var wc = bp.GetComponentInChildren<WoundChannels>();
            var wr = wc != null ? wc.GetComponent<MeshRenderer>() : null;
            if (wr) wr.enabled = false;
            // show all its anatomy, not just what is under the wounds
            var mat = bp.revealMaterial; float hc = mat.GetFloat("_HoleCount");
            var ext = mat.GetVector("_BodyExtents");
            var swap = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            swap.color = new Color(0.9f, 0.86f, 0.76f);
            var rs = bp.revealRoot.GetComponentsInChildren<MeshRenderer>(); var old = new Material[rs.Length];
            for (int i = 0; i < rs.Length; i++) { old[i] = rs[i].sharedMaterial; rs[i].sharedMaterial = swap; }
            Vector3 c = bp.transform.position;
            SnapPartOnly(name, bp, c, c + (Vector3.up + bp.transform.right * 0.3f).normalized * dist);
            for (int i = 0; i < rs.Length; i++) rs[i].sharedMaterial = old[i];
            bp.skin.enabled = true; if (wr) wr.enabled = true;
            Destroy(swap);
        }

        /// <summary>Grenades held against the head and chest; each layer photographed alone to find glitches.</summary>
        IEnumerator LayerTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            e.ragdoll.Fall("test");
            yield return new WaitForSeconds(3.5f);
            foreach (var id in new[] { PartId.Head, PartId.Chest })
            {
                var bp = e[id];
                Vector3 at = bp.col.ClosestPoint(bp.transform.position + Vector3.up * 0.6f);
                Grenade.Throw(at + Vector3.up * 0.03f, Vector3.zero).fuse = 0.05f;
                yield return new WaitForSeconds(1.5f);
            }
            // shotgun: point blank into the belly of a second Carl, and from 4 m into his chest
            var f = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(3f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            f.ragdoll.Fall("test");
            yield return new WaitForSeconds(3.5f);
            Vector3 fb = f[PartId.Abdomen].transform.position;
            Buckshot(fb + Vector3.up * 0.45f, fb);
            Vector3 fc = f.Chest.transform.position;
            Buckshot(fc + Vector3.up * 4f, fc);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[AutoTest] shotgun belly 0.4m: holes {f[PartId.Abdomen].holes.Count}; chest 4m: holes {f.Chest.holes.Count}");
            SnapWound("15_shotgun_belly", f[PartId.Abdomen], 0.3f);
            SnapWound("15_shotgun_chest4m", f.Chest, 0.4f);
            foreach (var id in new[] { PartId.Head, PartId.Chest })
            {
                var bp = e[id];
                if (bp.holes.Count == 0) { Debug.Log("[AutoTest] no holes " + id); continue; }
                int best = 0; for (int i = 1; i < bp.holes.Count; i++) if (bp.holes[i].w > bp.holes[best].w) best = i;
                Vector3 c = bp.transform.TransformPoint(bp.holes[best]);
                Vector3 o = -bp.transform.TransformDirection((Vector3)bp.holeAxes[best]).normalized;
                Vector3 from = c + (o + Vector3.Cross(o, Vector3.up).normalized * 0.5f).normalized * 0.3f;
                var wc = bp.GetComponentInChildren<WoundChannels>();
                var wr = wc != null ? wc.GetComponent<MeshRenderer>() : null;
                SnapPartOnly($"15_{id}_all", bp, c, from);
                if (wr) { wr.enabled = false; SnapPartOnly($"15_{id}_nochannel", bp, c, from); wr.enabled = true; }
                bp.skin.enabled = false; SnapPartOnly($"15_{id}_noskin", bp, c, from);
                bp.revealRoot.gameObject.SetActive(false); SnapPartOnly($"15_{id}_channelonly", bp, c, from);
                bp.revealRoot.gameObject.SetActive(true); bp.skin.enabled = true;
                Debug.Log($"[AutoTest] {id}: holes {bp.holes.Count}, biggest r {bp.holes[best].w:F3} d {bp.holeAxes[best].w:F3}, uvR {bp.uvRadius:F3}, verts {(wc ? wc.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0)}");
            }
            Debug.Log("[AutoTest] layer test done");
        }

        /// <summary>The clip: a grenade at the head/neck, head comes off. Every part near it, every layer.</summary>
        /// <summary>Injury levels: one cut, four cuts in one place, pistol, AK, .50 - each should go deeper.</summary>
        /// <summary>Wounds right at the joints (belly over the pelvis, chest over the belly): no foreign skin inside.</summary>
        /// <summary>Side-on frame strips of walking, running and limping.</summary>
        IEnumerator GaitTest(SandboxGame g)
        {
            ActiveRagdoll.DebugFalls = true;
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            // walk: calm wander
            var w = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-6f, 0f, 10f)) + Vector3.up * 0.02f, 90f);
            yield return new WaitForSeconds(2.5f);
            yield return Film("24_walk", w, 16, 0.1f);
            // run: frightened
            var r = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 8f)) + Vector3.up * 0.02f, 0f);
            yield return new WaitForSeconds(1.5f);
            HumanBrain.BroadcastNoise(r.Center + Vector3.back * 2f, 10f, 1f);
            r.brain.threatPos = basePos; r.brain.lastThreatTime = Time.time; r.brain.fear = 1f;
            yield return new WaitForSeconds(1f);
            yield return Film("25_run", r, 16, 0.07f);
            // limp: shot through the thigh muscle, then scared
            var l = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(6f, 0f, 8f)) + Vector3.up * 0.02f, 0f);
            yield return new WaitForSeconds(1.5f);
            { var th = l[PartId.ThighR]; Vector3 c = th.transform.position + th.transform.right * 0.05f; Vector3 from = c + (basePos + Vector3.up * c.y - c).normalized * 4f; Ballistics.Instance.Fire(from, (c - from).normalized, 375f, 0.0075f, 0.009f, HitKind.Bullet, false); }
            yield return new WaitForSeconds(2f);
            l.brain.threatPos = basePos; l.brain.lastThreatTime = Time.time; l.brain.fear = 0.9f;
            yield return new WaitForSeconds(1f);
            yield return Film("26_limp", l, 16, 0.1f);
            Vector3 lc = l[PartId.ThighR].transform.position;
            SnapMain("26_limp_blood", lc, lc + (basePos + Vector3.up * lc.y - lc).normalized * 0.6f);
            Debug.Log($"[AutoTest] gait done: walk {State(w)} | run {State(r)} | limp {State(l)}");
        }

        /// <summary>Four frightened Carls running away, each shot somewhere different.</summary>
        IEnumerator HurtRunTest(SandboxGame g)
        {
            ActiveRagdoll.DebugFalls = true;
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var cs = new HumanBody[4];
            for (int i = 0; i < 4; i++)
                cs[i] = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-6f + i * 4f, 0f, 6f)) + Vector3.up * 0.02f, 0f);
            yield return new WaitForSeconds(1.5f);
            foreach (var c in cs) { c.brain.threatPos = basePos; c.brain.lastThreatTime = Time.time; c.brain.fear = 1f; }
            yield return new WaitForSeconds(1.2f);
            Vector3 eye = basePos + Vector3.up * 1.6f;
            void Shoot(HumanBody h, Vector3 at, float v, float m, float d) => Ballistics.Instance.Fire(eye, (at - eye).normalized, v, m, d, HitKind.Bullet, false);
            Shoot(cs[0], cs[0][PartId.ThighR].transform.position + cs[0][PartId.ThighR].transform.right * 0.05f, 375f, 0.0075f, 0.009f);   // thigh muscle
            Shoot(cs[1], cs[1].Chest.transform.position + cs[1].Chest.transform.right * 0.11f, 715f, 0.0079f, 0.0078f);                     // side of chest
            Shoot(cs[2], cs[2].Chest.transform.position + cs[2].Chest.transform.right * -0.03f, 715f, 0.0079f, 0.0078f);                    // heart
            Shoot(cs[3], cs[3][PartId.ThighL].transform.position, 715f, 0.0079f, 0.0078f);                                                   // femur
            StartCoroutine(Film("28_thigh", cs[0], 14, 0.15f));
            for (float t = 0f; t < 9f; t += 0.5f)
            {
                for (int i = 0; i < 4; i++) Debug.Log($"[AutoTest] R{i} t{t,4:0.0} {State(cs[i])} struggle {cs[i].ragdoll.struggle:0.00} crawl {cs[i].ragdoll.IsCrawling}");
                if (Mathf.Approximately(t, 3f)) StartCoroutine(Film("29_femur", cs[3], 14, 0.3f));
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log("[AutoTest] hurt-run done");
        }

        IEnumerator GetUpTest(SandboxGame g)
        {
            ActiveRagdoll.DebugFalls = true;
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var a = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-1.5f, 0f, 4f)) + Vector3.up * 0.02f, 90f);
            var b = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(1.8f, 0f, 4f)) + Vector3.up * 0.02f, 90f);
            yield return new WaitForSeconds(1.5f);
            a.ragdoll.Fall("test push");
            a.Chest.rb.AddForce(Vector3.right * 120f, ForceMode.Impulse);
            b.physio.AddPain(0.9f, PartId.Abdomen);
            b.ragdoll.Fall("test pain");
            StartCoroutine(Film("31_getup", a, 24, 0.25f));
            for (float t = 0f; t < 8f; t += 0.5f)
            {
                Debug.Log($"[AutoTest] GU t{t,4:0.0} {State(a)} | pain one {State(b)}");
                yield return new WaitForSeconds(0.5f);
            }
            yield return Film("32_writhe", b, 12, 0.3f);
            Debug.Log("[AutoTest] getup test done");
        }

        IEnumerator ArmourTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var loads = new[]
            {
                new ArmourLoadout { vest = VestKind.Russian6B45, plateClass = 4, helmet = HelmetKind.Spetsnaz },
                new ArmourLoadout { vest = VestKind.KnightCuirass, helmet = HelmetKind.Knight, pauldrons = true, vambraces = true, gauntlets = true, cuisses = true, greaves = true, sabatons = true, tassets = true },
                new ArmourLoadout { vest = VestKind.Russian6B45, plateClass = 2, sideArmour = false, groinFlap = false, collar = false, helmet = HelmetKind.Spetsnaz, visorDown = false },
            };
            var es = new HumanBody[3];
            for (int i = 0; i < 3; i++)
            {
                es[i] = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-2.5f + i * 2.5f, 0f, 4f)) + Vector3.up * 0.02f, 180f);
                es[i].brain.enabled = false;
                ArmourSystem.Equip(es[i], loads[i]);
            }
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = es[i].Chest.transform.position;
                SnapMain($"30_armour{i}_front", c + Vector3.down * 0.15f, c + new Vector3(0.3f, 0.2f, -1.9f));
                SnapMain($"30_armour{i}_side", c + Vector3.down * 0.15f, c + new Vector3(1.9f, 0.3f, -0.2f));
                SnapMain($"30_armour{i}_head", es[i].Head.transform.position, es[i].Head.transform.position + new Vector3(0.35f, 0.1f, -0.6f));
            }
            // shoot the Br4 vest: PS, BP, .50
            var ammoAK = AmmoTable.For("AK-47");
            Vector3 eye = basePos + Vector3.up * 1.5f;
            Vector3 tgt = es[0].Chest.transform.position;
            int before = es[0].Chest.wounds.Count;
            for (int i = 0; i < 5; i++) { Ballistics.Instance.Fire(eye, (tgt + Random.insideUnitSphere * 0.04f - eye).normalized, ammoAK[0].velocity, ammoAK[0].mass, ammoAK[0].diameter, HitKind.Bullet, false, null, ammoAK[0]); yield return new WaitForSeconds(0.15f); }
            Debug.Log($"[AutoTest] Br4 vs 5x PS: chest wounds {es[0].Chest.wounds.Count - before}");
            before = es[0].Chest.wounds.Count;
            for (int i = 0; i < 3; i++) { Ballistics.Instance.Fire(eye, (tgt + Random.insideUnitSphere * 0.04f - eye).normalized, ammoAK[2].velocity, ammoAK[2].mass, ammoAK[2].diameter, HitKind.Bullet, false, null, ammoAK[2]); yield return new WaitForSeconds(0.15f); }
            Debug.Log($"[AutoTest] Br4 vs 3x BP: chest wounds {es[0].Chest.wounds.Count - before}");
            // knight with pistol
            var g17 = AmmoTable.For("GLOCK 17");
            tgt = es[1].Chest.transform.position;
            before = es[1].Chest.wounds.Count;
            for (int i = 0; i < 4; i++) { Ballistics.Instance.Fire(eye, (tgt - eye).normalized, g17[0].velocity, g17[0].mass, g17[0].diameter, HitKind.Bullet, false, null, g17[0]); yield return new WaitForSeconds(0.15f); }
            Debug.Log($"[AutoTest] knight vs 4x 9mm: chest wounds {es[1].Chest.wounds.Count - before}");
            yield return new WaitForSeconds(1f);
            Vector3 c0 = es[0].Chest.transform.position;
            SnapMain("30_armour0_shot", c0, c0 + (eye - c0).normalized * 0.8f);
            Debug.Log("[AutoTest] armour test done");
        }

        IEnumerator Film(string name, HumanBody h, int n, float gap)
        {
            for (int i = 0; i < n && h != null; i++)
            {
                Vector3 c = h.Pelvis.transform.position;
                Vector3 v = Vector3.ProjectOnPlane(h.Pelvis.rb.linearVelocity, Vector3.up);
                Vector3 side = v.sqrMagnitude > 0.05f ? Vector3.Cross(Vector3.up, v.normalized) : h.Pelvis.transform.right;
                SnapMain($"{name}_{i:00}", c - Vector3.up * 0.25f, c + side * 3.2f + Vector3.up * 0.1f);
                yield return new WaitForSeconds(gap);
            }
        }

        static string State(HumanBody h)
        {
            var r = h.ragdoll;
            float v = Vector3.ProjectOnPlane(h.Pelvis.rb.linearVelocity, Vector3.up).magnitude;
            return $"{h.brain.activity,-12} {r.mode,-9} sink {(r.IsSinking ? 1 : 0)} wall {(r.BracingOnWall ? 1 : 0)} bal {r.balance,5:0.00} steady {r.Steadiness:0.00} spd {v:0.0} pain {h.physio.EffectivePain:0.00} loss {h.physio.LossFrac:0.00}";
        }

        IEnumerator Watch(string tag, HumanBody h, float seconds)
        {
            for (float t = 0f; t < seconds && h != null; t += 0.25f)
            {
                Debug.Log($"[AutoTest] {tag} t{t,4:0.0} {State(h)}");
                yield return new WaitForSeconds(0.25f);
            }
        }

        /// <summary>Injury, balance and falling over time: chest shots, a thigh shot, a trip, a wall.</summary>
        IEnumerator BehaviourTest(SandboxGame g)
        {
            ActiveRagdoll.DebugFalls = true;
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;

            // B: one pistol round to the thigh - should limp and slow, not fall
            var b = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-4f, 0f, 7f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            {
                // through the outer thigh muscle, missing the femur
                var th = b[PartId.ThighR];
                Vector3 c = th.transform.position + th.transform.right * 0.055f;
                Vector3 from = c + (basePos + Vector3.up * c.y - c).normalized * 4f;
                Ballistics.Instance.Fire(from, (c - from).normalized, 375f, 0.0075f, 0.009f, HitKind.Bullet, false);
            }
            yield return Watch("B-thigh", b, 5f);

            // A: AK rounds to the chest, one every 1.5 s, until they go down (or 6)
            var a = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 7f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < 6 && a != null && a.ragdoll.mode != BodyMode.Down; i++)
            {
                // the side of the chest (lung, ribs) - not straight through the heart and spine
                Vector3 c = a.Chest.transform.position + a.Chest.transform.right * 0.11f + Vector3.up * 0.04f;
                Vector3 from = basePos + Vector3.up * 1.5f;
                Ballistics.Instance.Fire(from, (c - from).normalized, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                Debug.Log($"[AutoTest] A shot {i + 1}");
                if (i == 1) StartCoroutine(BurstShots("21_fall", a, 14, 0.2f));
                yield return Watch("A-chest", a, 1.5f);
            }
            yield return Watch("A-after", a, 6f);

            // C: unhurt Carl knocked off balance - should catch themselves or get straight back up
            var c2 = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(4f, 0f, 7f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            c2.ragdoll.balance = -0.4f; c2.ragdoll.Topple("test trip", 0.3f);
            yield return Watch("C-trip", c2, 7f);

            // D: hurt Carl next to a wall staggers - should put a hand on it
            Vector3 wallSpot = basePos + new Vector3(0f, 0f, 3f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = SandboxGame.GroundAt(wallSpot + new Vector3(0.6f, 0f, 0f)) + Vector3.up * 1.2f;
            wall.transform.localScale = new Vector3(0.2f, 2.4f, 4f);
            var d = g.SpawnCarl(SandboxGame.GroundAt(wallSpot) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            d.physio.AddPain(0.85f, PartId.Abdomen);
            d.ragdoll.balance = 0.1f;
            d.ragdoll.Topple("test stagger", 0f);
            StartCoroutine(BurstShots("22_wall", d, 8, 0.15f));
            yield return Watch("D-wall", d, 6f);
            Destroy(wall);
            Debug.Log("[AutoTest] behaviour test done");
        }

        IEnumerator BurstShots(string name, HumanBody h, int n, float gap)
        {
            for (int i = 0; i < n && h != null; i++)
            {
                Vector3 c = h.Pelvis.transform.position;
                Vector3 side = Vector3.Cross(Vector3.up, (SandboxGame.Instance.cam.transform.position - c).normalized);
                SnapMain($"{name}_{i:00}", c + Vector3.up * 0.3f, c + side * 2.6f + Vector3.up * 0.9f);
                yield return new WaitForSeconds(gap);
            }
        }

        /// <summary>The clip: a standing Carl stabbed again and again around the shoulder.</summary>
        IEnumerator StabTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 3f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            foreach (var id in new[] { PartId.UpperArmR, PartId.Chest, PartId.Neck, PartId.UpperArmR, PartId.Chest, PartId.Abdomen, PartId.UpperArmR, PartId.Chest })
            {
                var bp = e[id];
                Vector3 toPlayer = basePos + Vector3.up * bp.transform.position.y - bp.transform.position; toPlayer.y = 0f; toPlayer.Normalize();
                Vector3 aim = bp.transform.position + bp.transform.up * bp.halfLength * 0.5f + Random.insideUnitSphere * 0.03f;
                if (SurfacePoint(bp, aim + toPlayer * 0.8f, out var hit))
                    e.damage.ApplyStab(bp, hit.point, -hit.normal, Random.Range(0.12f, 0.18f));
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(1f);
            Vector3 c = e.Chest.transform.position + Vector3.up * 0.1f;
            Vector3 tp = basePos + Vector3.up * c.y - c; tp.y = 0f; tp.Normalize();
            Vector3 side = Vector3.Cross(tp, Vector3.up);
            SnapMain("19_front", c, c + tp * 0.9f);
            SnapMain("19_left", c, c + (tp + side).normalized * 0.8f);
            SnapMain("19_right", c, c + (tp - side).normalized * 0.8f);
            SnapMain("19_back", c, c - tp * 0.9f + Vector3.up * 0.2f);
            SnapMain("19_close", c, c + (tp + Vector3.up * 0.3f).normalized * 0.45f);
            SnapMain("19_shoulder", e[PartId.UpperArmR].transform.position, e[PartId.UpperArmR].transform.position + (tp - side * 0.5f + Vector3.up * 0.3f).normalized * 0.4f);
            string hs = ""; foreach (var bp in e.partList) if (bp.holes.Count > 0) hs += bp.id + ":" + bp.holes.Count + " ";
            Debug.Log("[AutoTest] stab test: " + hs);
        }

        IEnumerator NbrTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 3f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            int n = 0;
            foreach (var (id, toward) in new[] { (PartId.Abdomen, -1f), (PartId.Chest, -1f), (PartId.ThighR, 1f) })
            {
                var bp = e[id];
                Vector3 toPlayer = basePos + Vector3.up * bp.transform.position.y - bp.transform.position; toPlayer.y = 0f; toPlayer.Normalize();
                // the front, at the end of the part where its neighbour overlaps
                Vector3 c = bp.transform.position + bp.transform.up * toward * bp.halfLength * 0.7f;
                if (!SurfacePoint(bp, c + toPlayer * 0.8f, out var hit)) { Debug.Log("[AutoTest] nbr: no surface " + id); continue; }
                bp.AddHole(bp.transform.InverseTransformPoint(hit.point), 0.035f, 0.07f);
                yield return null; yield return null;
                Vector3 w = bp.transform.TransformPoint(bp.holes[bp.holes.Count - 1]);
                SnapMain($"18_{id}_straight", w, w + toPlayer * 0.3f);
                SnapMain($"18_{id}_tilt1", w, w + (toPlayer + Vector3.up * 0.6f).normalized * 0.3f);
                SnapMain($"18_{id}_tilt2", w, w + (toPlayer - Vector3.up * 0.6f).normalized * 0.3f);
                n++;
            }
            Debug.Log("[AutoTest] nbr test done " + n);
        }

        IEnumerator DepthTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            e.ragdoll.Fall("test");
            yield return new WaitForSeconds(3.5f);
            void Cut(BodyPart bp)
            {
                Vector3 c = bp.transform.position;
                if (SurfacePoint(bp, c + Vector3.up * 0.8f, out var hit)) e.damage.ApplySlash(bp, hit.point, hit.normal, bp.transform.up);
            }
            Cut(e[PartId.ThighL]);
            for (int i = 0; i < 4; i++) { Cut(e[PartId.ThighR]); yield return new WaitForSeconds(0.25f); }
            { var bp = e[PartId.ForearmL]; if (SurfacePoint(bp, bp.transform.position + Vector3.up * 0.8f, out var hit)) Ballistics.Instance.Fire(hit.point + hit.normal * 0.6f, -hit.normal, 375f, 0.0075f, 0.009f, HitKind.Bullet, false); }
            {
                // three AK rounds into the lower belly, close to where the pelvis overlaps it
                var bp = e[PartId.Abdomen];
                for (int i = 0; i < 3; i++)
                {
                    Vector3 c = bp.transform.position - bp.transform.up * 0.06f + bp.transform.right * (i - 1) * 0.03f;
                    if (SurfacePoint(bp, c + Vector3.up * 0.8f, out var hit))
                        Ballistics.Instance.Fire(hit.point + hit.normal * 0.6f, -hit.normal, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                    yield return null;
                }
            }
            { var bp = e.Chest; Vector3 c = bp.transform.position; Ballistics.Instance.Fire(c + Vector3.up * 1f, Vector3.down, 890f, 0.0421f, 0.0127f, HitKind.Bullet, false); }
            yield return new WaitForSeconds(1.5f);
            foreach (var (n, id) in new[] { ("1cut", PartId.ThighL), ("4cuts", PartId.ThighR), ("pistol", PartId.ForearmL), ("ak", PartId.Abdomen), ("50cal", PartId.Chest) })
            {
                var bp = e[id];
                float md = 0f, mr = 0f; foreach (var h in bp.holeAxes) md = Mathf.Max(md, h.w); foreach (var h in bp.holes) mr = Mathf.Max(mr, h.w);
                Debug.Log($"[AutoTest] {n}: holes {bp.holes.Count} maxR {mr:F4} maxDepth {md:F4}");
                if (bp.holes.Count == 0) continue;
                // look at the widest hole
                int best = 0; for (int i = 1; i < bp.holes.Count; i++) if (bp.holes[i].w > bp.holes[best].w) best = i;
                Vector3 c = bp.transform.TransformPoint(bp.holes[best]);
                Vector3 o = -bp.transform.TransformDirection((Vector3)bp.holeAxes[best]).normalized;
                Vector3 side = Vector3.Cross(o, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(o, Vector3.up).normalized : Vector3.right;
                float dist = n.Contains("cut") ? 0.11f : 0.16f;
                SnapPartOnly($"17_{n}_a", bp, c, c + o * dist);
                SnapPartOnly($"17_{n}_b", bp, c, c + (o + side * 0.7f).normalized * dist);
                SnapMain($"17_{n}_c_scene", c, c + o * dist);
            }
            Debug.Log("[AutoTest] depth test done");
        }

        IEnumerator NeckTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-1.5f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            var f = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(1.5f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            var h = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(4.5f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            e.ragdoll.Fall("test"); f.ragdoll.Fall("test"); h.ragdoll.Fall("test");
            yield return new WaitForSeconds(3.5f);
            // A: a real grenade against the head
            var hd = e.Head;
            Vector3 at = hd.col.ClosestPoint(hd.transform.position + Vector3.up * 0.6f);
            Grenade.Throw(at + Vector3.up * 0.03f, Vector3.zero).fuse = 0.05f;
            // B: blast right on the neck (head comes off)
            var nk = f[PartId.Neck];
            Vector3 nat = nk.col.ClosestPoint(nk.transform.position + Vector3.up * 0.6f);
            Fx.Explosion(nat);
            foreach (var bp in f.partList)
            {
                float ds = Vector3.Distance(bp.col.ClosestPoint(nat), nat);
                if (ds < 0.5f) f.damage.ApplyBlast(bp, nat + Vector3.up * 0.02f, Mathf.Pow(Mathf.Clamp01(1f - ds / 2f), 2f), ds, bp == nk);
            }
            // C: point-blank shotgun to the belly and the chest
            Vector3 hb = h[PartId.Abdomen].transform.position; Buckshot(hb + Vector3.up * 0.45f, hb);
            Vector3 hc = h.Chest.transform.position; Buckshot(hc + Vector3.up * 0.5f, hc);
            // D: shotgun into the head from 1 m (the clip)
            Vector3 hh = h.Head.transform.position; Buckshot(hh + Vector3.up * 1f, hh);
            yield return new WaitForSeconds(3f);
            Debug.Log($"[AutoTest] head grenade severed: {Severed(e)} | neck blast severed: {Severed(f)}");
            var shots = new List<(string, HumanBody, PartId)> { ("A", e, PartId.Head), ("A", e, PartId.Neck), ("A", e, PartId.Chest),
                ("B", f, PartId.Neck), ("B", f, PartId.Head), ("B", f, PartId.Chest), ("C", h, PartId.Abdomen), ("C", h, PartId.Chest), ("D", h, PartId.Head) };
            foreach (var (tag, body, id) in shots)
            {
                var bp = body[id];
                if (bp.holes.Count == 0) continue;
                int best = 0; for (int i = 1; i < bp.holes.Count; i++) if (bp.holes[i].w > bp.holes[best].w) best = i;
                Vector3 c = bp.transform.TransformPoint(bp.holes[best]);
                Vector3 o = -bp.transform.TransformDirection((Vector3)bp.holeAxes[best]).normalized;
                Vector3 side = Vector3.Cross(o, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(o, Vector3.up).normalized : Vector3.right;
                SnapPartOnly($"16_{tag}_{id}_1straight", bp, c, c + o * 0.28f);
                SnapPartOnly($"16_{tag}_{id}_2angled", bp, c, c + (o + side * 0.9f).normalized * 0.28f);
                SnapMain($"16_{tag}_{id}_3game", c, c + (o * 0.6f + Vector3.up * 0.6f - side * 0.3f).normalized * 0.45f);
                if (tag == "D" || tag == "C")
                    for (int k = 0; k < 4; k++)   // sweep the camera around: the wound must stay put
                    {
                        float ang = (k - 1.5f) * 25f;
                        Vector3 dir2 = Quaternion.AngleAxis(ang, Vector3.up) * (o + Vector3.up * 0.4f).normalized;
                        SnapMain($"16_{tag}_{id}_4sweep{k}", c, c + dir2 * 0.5f);
                    }
                Debug.Log($"[AutoTest] {tag} {id}: holes {bp.holes.Count} maxR {bp.holes[best].w:F3} uvR {bp.uvRadius:F3}");
            }
            Debug.Log("[AutoTest] neck test done");
        }

        IEnumerator BoneTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(2f, 0f, 9f)) + Vector3.up * 0.02f, 180f);
            var f = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-2f, 0f, 9f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            e.ragdoll.Fall("test"); f.ragdoll.Fall("test");
            yield return new WaitForSeconds(3.5f);
            // AK through the forearm, twice at the same spot
            var fa = e[PartId.ForearmR];
            for (int i = 0; i < 2; i++) { Vector3 c = fa.transform.position; Ballistics.Instance.Fire(c + Vector3.up * 1.2f, Vector3.down, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false); yield return new WaitForSeconds(0.1f); }
            // .50 through the shin
            var sh = e[PartId.ShinL];
            { Vector3 c = sh.transform.position; Ballistics.Instance.Fire(c + Vector3.up * 1.2f, Vector3.down, 890f, 0.0421f, 0.0127f, HitKind.Bullet, false); }
            yield return new WaitForSeconds(0.1f);
            // shotgun into the thigh
            var th = e[PartId.ThighR];
            Buckshot(th.transform.position + Vector3.up * 1.2f, th.transform.position);
            yield return new WaitForSeconds(0.1f);
            // grenade against the upper arm of the other Carl
            var ua = f[PartId.ThighL];
            Vector3 up = ua.col.ClosestPoint(ua.transform.position + Vector3.up * 0.6f);
            Fx.Explosion(up);
            f.damage.ApplyBlast(ua, up + Vector3.up * 0.03f, 1f, 0.03f, true);   // point-blank against the thigh
            yield return new WaitForSeconds(3f);
            foreach (var (n, bp) in new[] { ("14_forearm", fa), ("14_shin", sh), ("14_thigh", th), ("14_blast_thigh", ua) })
            {
                string br = ""; foreach (var o in bp.organs) if (o.breaks.Count > 0) br += $"{o.def.name}:{o.breaks.Count}({o.breaks[0].y:F2}) ";
                Debug.Log($"[AutoTest] {n}: breaks {br} severed {bp.severed}");
                SnapWound(n + "_w", bp, 0.16f);
                SnapBones(n + "_bones", bp, 0.45f);
            }
            Debug.Log("[AutoTest] bone test done");
        }

        /// <summary>Point-blank grenades (belly, shin) and the shotgun.</summary>
        IEnumerator BoomTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var a = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-4f, 0f, 9f)) + Vector3.up * 0.02f, 180f);
            var b = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(4f, 0f, 9f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            a.ragdoll.Fall("test"); b.ragdoll.Fall("test");
            yield return new WaitForSeconds(3.5f);

            // grenade resting on the belly
            var belly = a[PartId.Abdomen];
            Vector3 top = belly.col.ClosestPoint(belly.transform.position + Vector3.up * 0.6f);
            Grenade.Throw(top + Vector3.up * 0.04f, Vector3.zero).fuse = 0.15f;
            RaycastHit hit;
            // grenade against a shin
            var shin = b[PartId.ShinL];
            Vector3 sp = shin.col.ClosestPoint(shin.transform.position + Vector3.up * 0.6f);
            var sg = Grenade.Throw(sp + Vector3.up * 0.04f, Vector3.zero);
            sg.fuse = 0.15f;
            yield return new WaitForSeconds(0.13f);
            if (sg != null)
            {
                var sbd = new System.Text.StringBuilder();
                foreach (var id in new[] { PartId.ThighL, PartId.ShinL, PartId.FootL, PartId.ThighR, PartId.ShinR })
                {
                    var lp2 = b[id];
                    sbd.Append($"{id}:{Vector3.Distance(lp2.col.ClosestPoint(sg.transform.position), sg.transform.position):F2}/{Vector3.Distance(lp2.transform.position, sg.transform.position):F2} ");
                }
                Debug.Log("[AutoTest] shin grenade distances (surface/centre) " + sbd);
            }
            yield return new WaitForSeconds(2.9f);
            Debug.Log($"[AutoTest] belly grenade: severed {Severed(a)}; abdomen holes {belly.holes.Count}, worst {belly.WorstHoleFraction:F2}");
            Debug.Log($"[AutoTest] shin grenade: severed {Severed(b)}");
            Vector3 ac = a[PartId.Abdomen].transform.position;
            SnapMain("10_belly_over", ac, ac + new Vector3(0.3f, 1.0f, -0.6f));
            SnapWound("10w_belly", belly, 0.32f);
            Vector3 bc = b.Pelvis.transform.position;
            SnapMain("10_shin_over", bc, bc + new Vector3(0.4f, 1.6f, -1.2f));

            // grenade at the neck / head (the messy case)
            var n = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 7f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.2f);
            n.ragdoll.Fall("test");
            yield return new WaitForSeconds(3.5f);
            var neck = n[PartId.Neck];
            Vector3 np = neck.col.ClosestPoint(neck.transform.position + Vector3.up * 0.6f);
            Grenade.Throw(np + Vector3.up * 0.04f, Vector3.zero).fuse = 0.1f;
            yield return new WaitForSeconds(3f);
            Debug.Log($"[AutoTest] neck grenade: severed {Severed(n)}");
            Vector3 nc = n.Chest.transform.position;
            SnapMain("13_neck_close", nc, nc + new Vector3(0.25f, 0.55f, -0.35f));
            SnapMain("13_neck_side", nc, nc + new Vector3(-0.5f, 0.35f, 0.2f));
            Vector3 ab2 = a[PartId.Abdomen].transform.position;
            SnapMain("10_belly_close", ab2, ab2 + new Vector3(0.2f, 0.5f, -0.3f));

            // shotgun: one blast to the chest from 4 m, then three into one thigh from 1.5 m
            var c = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 11f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            Vector3 cc = c.Chest.transform.position;
            Buckshot(cc + new Vector3(0, 0.1f, -4f), cc);
            yield return new WaitForSeconds(0.25f);
            SnapWound("11w_shotgun_chest", c.Chest, 0.35f);
            yield return new WaitForSeconds(2.5f);
            var th = c[PartId.ThighR];
            for (int i = 0; i < 3; i++)
            {
                Vector3 tp = th.transform.position;
                Buckshot(tp + Vector3.up * 1.5f, tp);
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[AutoTest] shotgun thigh x3: severed {Severed(c)}; thigh holes {th.holes.Count}, worst {th.WorstHoleFraction:F2}");
            Vector3 thp = th.transform.position;
            SnapMain("11_shotgun_thigh", thp, thp + new Vector3(0.2f, 0.9f, -0.5f));

            // the viewmodel
            var wc = FindAnyObjectByType<WeaponController>();
            if (wc != null)
            {
                wc.Select(4);
                yield return new WaitForSeconds(0.6f);
                var cam = SandboxGame.Instance.cam.transform;
                SnapMain("12_shotgun_view", cam.position + cam.forward * 5f, cam.position);
            }
            Debug.Log("[AutoTest] boom test done");
        }

        IEnumerator GrenadeTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(4f, 0f, 9f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            Grenade.Throw(e.Pelvis.transform.position + Vector3.right * 0.7f + Vector3.up * 0.2f, Vector3.zero).fuse = 0.4f;
            yield return new WaitForSeconds(5f);
            Vector3 c = e.Chest.transform.position;
            SnapMain("7_grenade_over", c, c + new Vector3(0.6f, 1.4f, -0.8f));
            SnapMain("7_grenade_close", c, c + new Vector3(0.5f, 0.6f, -0.3f));
            int severed = 0; foreach (var bp in e.parts) if (bp != null && bp.severed) severed++;
            var sb = new System.Text.StringBuilder();
            foreach (var bp in e.parts) if (bp != null && bp.holes.Count > 0) sb.Append($"{bp.id}:{bp.holes.Count} ");
            Debug.Log($"[AutoTest] grenade: severed {severed}, holes {sb}");
            foreach (var id in new[] { PartId.Abdomen, PartId.Chest, PartId.ThighR, PartId.UpperArmR })
                SnapWound("7w_" + id, e[id], 0.2f);
            Debug.Log("[AutoTest] grenade test done");
        }

        /// <summary>Slug / birdshot, a wallbang, a stump, an explosion frame, the x-ray and writhing.</summary>
        IEnumerator NewTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var sg = AmmoTable.For("MOSSBERG 590");
            AmmoType Find(AmmoType[] t, string n) { foreach (var a in t) if (a.name.Contains(n)) return a; return t[0]; }
            void FireAmmo(AmmoType a, Vector3 from, Vector3 at)
            {
                for (int i = 0; i < a.pellets; i++)
                {
                    Vector3 d = Quaternion.Euler(Random.Range(-a.spread, a.spread) * 0.5f, Random.Range(-a.spread, a.spread) * 0.5f, 0f) * (at - from).normalized;
                    Ballistics.Instance.Fire(from, d, a.velocity, a.mass, a.diameter, HitKind.Bullet, false, null, a);
                }
            }
            // ---- slug vs birdshot at 5 m
            var e1 = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(-3f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            var e2 = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            Vector3 c1 = e1.Chest.transform.position, c2 = e2.Chest.transform.position;
            FireAmmo(Find(sg, "Slug"), c1 + new Vector3(0, 0.05f, -5f), c1);
            FireAmmo(Find(sg, "Bird"), c2 + new Vector3(0, 0.05f, -5f), c2);
            yield return new WaitForSeconds(0.3f);
            SnapMain("30_slug", e1.Chest.transform.position, e1.Chest.transform.position + new Vector3(0.1f, 0.15f, -0.7f));
            SnapMain("30_birdshot", e2.Chest.transform.position, e2.Chest.transform.position + new Vector3(0.1f, 0.15f, -0.7f));
            Debug.Log($"[AutoTest] slug holes {e1.Chest.holes.Count} wounds {e1.Chest.wounds.Count}; birdshot holes {e2.Chest.holes.Count} wounds {e2.Chest.wounds.Count}");

            // ---- wallbang: 10 cm wall in front of an Earl, AK PS and a pistol FMJ
            var e3 = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(3f, 0f, 8f)) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            Vector3 c3 = e3.Chest.transform.position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = c3 + new Vector3(0, 0, -1f); wall.transform.localScale = new Vector3(1.2f, 2.5f, 0.1f);
            int before = 0; foreach (var bp in e3.parts) before += bp.wounds.Count;
            var ak = AmmoTable.For("AK-47")[0];
            FireAmmo(ak, c3 + new Vector3(0, 0, -4f), c3);
            yield return new WaitForSeconds(0.3f);
            int afterAk = 0; foreach (var bp in e3.parts) afterAk += bp.wounds.Count;
            FireAmmo(AmmoTable.For("GLOCK 17")[0], c3 + new Vector3(0.05f, -0.1f, -4f), c3 + new Vector3(0.05f, -0.1f, 0));
            yield return new WaitForSeconds(0.3f);
            int afterG = 0; foreach (var bp in e3.parts) afterG += bp.wounds.Count;
            Debug.Log($"[AutoTest] wallbang 10cm concrete: AK wounds +{afterAk - before}, Glock +{afterG - afterAk}");
            // same wall as a house wall (drywall)
            var house = new GameObject("Bungalow"); wall.transform.SetParent(house.transform, true); wall.name = "Wall";
            FireAmmo(ak, c3 + new Vector3(-0.05f, 0.05f, -4f), c3 + new Vector3(-0.05f, 0.05f, 0));
            yield return new WaitForSeconds(0.3f);
            int afterH = 0; foreach (var bp in e3.parts) afterH += bp.wounds.Count;
            FireAmmo(AmmoTable.For("GLOCK 17")[0], c3 + new Vector3(0.08f, -0.15f, -4f), c3 + new Vector3(0.08f, -0.15f, 0));
            yield return new WaitForSeconds(0.3f);
            int afterH2 = 0; foreach (var bp in e3.parts) afterH2 += bp.wounds.Count;
            Debug.Log($"[AutoTest] wallbang house wall: AK wounds +{afterH - afterG}, Glock +{afterH2 - afterH}");
            Destroy(house);

            // ---- stump cap
            e3.damage.Sever(e3[PartId.ForearmR], Vector3.right, 2f);
            yield return new WaitForSeconds(1.2f);
            var up = e3[PartId.UpperArmR];
            Vector3 ue = up.transform.position;
            SnapPartOnly("31_stump_upper", up, ue, ue + up.transform.up * -0.35f + Vector3.up * 0.1f);
            var fa = e3[PartId.ForearmR];
            if (fa != null) SnapPartOnly("31_stump_fore", fa, fa.transform.position, fa.transform.position + fa.transform.up * 0.35f + Vector3.up * 0.1f);

            // ---- explosion frame
            var gr = Grenade.Throw(basePos + new Vector3(0f, 0.2f, 14f), Vector3.zero); gr.fuse = 0.3f;
            yield return new WaitForSeconds(0.42f);
            SnapMain("32_explosion_early", basePos + new Vector3(0f, 0.8f, 14f), basePos + new Vector3(0f, 1.7f, 7f));
            yield return new WaitForSeconds(0.5f);
            SnapMain("32_explosion_late", basePos + new Vector3(0f, 1.5f, 14f), basePos + new Vector3(0f, 1.7f, 7f));

            // ---- writhing: gut shot, watch
            Ballistics.Instance.Fire(e2[PartId.Abdomen].transform.position + new Vector3(0, 0, -2f), Vector3.forward, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
            yield return new WaitForSeconds(4f);
            SnapMain("33_writhe_a", e2.Chest.transform.position, e2.Chest.transform.position + new Vector3(1.2f, 1.2f, -1.2f));
            yield return new WaitForSeconds(1.5f);
            SnapMain("33_writhe_b", e2.Chest.transform.position, e2.Chest.transform.position + new Vector3(1.2f, 1.2f, -1.2f));

            // ---- x-ray
            g.xray.Toggle();
            yield return null; yield return null;
            SnapMain("34_xray", e1.Chest.transform.position, e1.Chest.transform.position + new Vector3(0.3f, 0.6f, -1.6f));
            g.xray.Toggle();
            Debug.Log("[AutoTest] new test done");
        }

        /// <summary>Armour damage: dents and rips that build up and crack a plate, a hole straight through, straps.</summary>
        IEnumerator ArmDmgTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var L = new ArmourLoadout { vest = VestKind.Russian6B45, plateClass = 4, helmet = HelmetKind.Spetsnaz, visor = true, visorDown = true };
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 6f)) + Vector3.up * 0.02f, 0f);
            ArmourSystem.Equip(e, L.Clone());
            e.brain.aiEnabled = false;
            yield return new WaitForSeconds(1.2f);
            var ak = AmmoTable.For("AK-47");
            Vector3 c = e.Chest.transform.position;
            Vector3 fwd = -e.Chest.transform.forward; // toward the shooter if the Earl faces us
            fwd = (basePos + Vector3.up * 1.4f - c); fwd.y = 0; fwd.Normalize();
            Vector3 spot = c + Vector3.up * 0.02f + Vector3.Cross(Vector3.up, fwd) * 0.05f;
            Vector3 from = spot + fwd * 3f;
            for (int i = 0; i < 6; i++)
            {
                Ballistics.Instance.Fire(from, (spot - from).normalized, ak[0].velocity, ak[0].mass, ak[0].diameter, HitKind.Bullet, false, null, ak[0]);
                yield return new WaitForSeconds(0.25f);
                if (i == 1) SnapMain("50_vest_2hits", e.Chest.transform.position, e.Chest.transform.position + fwd * 0.55f + Vector3.up * 0.05f);
            }
            Vector3 spot2 = c - Vector3.up * 0.12f - Vector3.Cross(Vector3.up, fwd) * 0.06f;
            Ballistics.Instance.Fire(spot2 + fwd * 3f, -fwd, ak[2].velocity, ak[2].mass, ak[2].diameter, HitKind.Bullet, false, null, ak[2]);
            yield return new WaitForSeconds(0.3f);
            var front = e.Chest.GetComponentInChildren<ArmourPiece>();
            string info = "";
            foreach (var ap in e.Chest.GetComponentsInChildren<ArmourPiece>()) info += $"{ap.label}: dents {ap.dents.Count} breaks {ap.breaks.Count} straps {ap.strapHealth:0}; ";
            Debug.Log("[AutoTest] armour " + info);
            SnapMain("51_vest_after", e.Chest.transform.position, e.Chest.transform.position + fwd * 0.55f + Vector3.up * 0.05f);
            SnapMain("52_vest_side", e.Chest.transform.position, e.Chest.transform.position + (fwd + Vector3.Cross(Vector3.up, fwd)).normalized * 0.5f + Vector3.up * 0.1f);
            Vector3 h = e.Head.transform.position;
            SnapMain("53_visor", h, h + fwd * 0.35f + Vector3.up * 0.03f);
            SnapMain("53_visor_side", h, h + (fwd + Vector3.Cross(Vector3.up, fwd) * 0.8f).normalized * 0.35f);
            // grenade at their feet: straps
            Grenade.Throw(e.Pelvis.transform.position + fwd * 0.6f, Vector3.zero).fuse = 0.2f;
            yield return new WaitForSeconds(3f);
            int dropped = 0; foreach (var go in GameObject.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)) if (go.name.StartsWith("Dropped")) dropped++;
            Debug.Log("[AutoTest] dropped kit after grenade: " + dropped);
            SnapMain("54_after_blast", e.Chest.transform.position, e.Chest.transform.position + new Vector3(0.8f, 1.2f, 0.8f));
            Debug.Log("[AutoTest] armour damage test done");
        }

        /// <summary>The same spot shot again and again (one round at a time), then photographed close up.</summary>
        IEnumerator RepeatTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 5f)) + Vector3.up * 0.02f, 180f);
            e.brain.aiEnabled = false;
            yield return new WaitForSeconds(1.2f);
            e.ragdoll.Fall("test");
            yield return new WaitForSeconds(2.5f);
            foreach (var id in new[] { PartId.Abdomen, PartId.Chest, PartId.ThighR })
            {
                var bp = e[id];
                Vector3 c = bp.transform.position;
                Vector3 from = c + Vector3.up * 1.5f + Vector3.forward * 0.2f;
                for (int i = 0; i < 12; i++)
                {
                    Vector3 at = bp.transform.position + Random.insideUnitSphere * 0.012f;
                    Ballistics.Instance.Fire(from, (at - from).normalized, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                    yield return new WaitForSeconds(0.15f);
                }
                yield return new WaitForSeconds(0.5f);
                int best = 0; for (int i = 1; i < bp.holes.Count; i++) if (bp.holes[i].w > bp.holes[best].w) best = i;
                Debug.Log($"[AutoTest] repeat {id}: {bp.holes.Count} holes, biggest r {bp.holes[best].w:0.000}");
                Vector3 w = bp.transform.TransformPoint(bp.holes[best]);
                SnapMain($"62_rep_{id}_close", w, w + (from - w).normalized * 0.2f);
                SnapMain($"62_rep_{id}_angle", w, w + ((from - w).normalized + Vector3.right * 0.7f).normalized * 0.25f);
            }
            Debug.Log("[AutoTest] repeat test done");
        }

        /// <summary>A thigh shot through in one place over and over (hangs, then comes off there); a .50 through a forearm.</summary>
        IEnumerator LimbTest(SandboxGame g)
        {
            Vector3 basePos = g.player.transform.position; basePos.y = 0f;
            var e = g.SpawnCarl(SandboxGame.GroundAt(basePos + new Vector3(0f, 0f, 5f)) + Vector3.up * 0.02f, 180f);
            e.brain.aiEnabled = false;
            yield return new WaitForSeconds(1.2f);
            e.ragdoll.Fall("test");
            yield return new WaitForSeconds(2.5f);
            var th = e[PartId.ThighR];
            var li = LimbIntegrity.Of(th);
            for (int i = 0; i < 14 && li != null && !(li.cut && li.lower != null && th.body != null && li.Status() == "SEVERED"); i++)
            {
                Vector3 at = th.transform.position + Random.insideUnitSphere * 0.01f;
                Vector3 from = at + Vector3.up * 1.4f + Vector3.forward * 0.15f;
                var ak = AmmoTable.For("AK-47")[0];
                Ballistics.Instance.Fire(from, (at - from).normalized, ak.velocity, ak.mass, ak.diameter, HitKind.Bullet, false, null, ak);
                yield return new WaitForSeconds(0.25f);
                Debug.Log($"[AutoTest] thigh shot {i}: {li.Status()}");
                if (li.Status() == "HANGING BY SKIN") { Vector3 c = th.transform.position; Photo("80_hanging", c, c + new Vector3(0.6f, 0.9f, -0.6f)); }
            }
            yield return new WaitForSeconds(1.5f);
            Vector3 c2 = th.transform.position;
            Photo("81_thigh_after", c2, c2 + new Vector3(0.7f, 1.1f, -0.7f));
            Photo("81_thigh_close", c2, c2 + new Vector3(0.35f, 0.4f, -0.3f));
            // .50 through the forearm
            var fa = e[PartId.ForearmL];
            Vector3 fat = fa.transform.position;
            var b50 = AmmoTable.For(".50 CAL")[0];
            Ballistics.Instance.Fire(fat + Vector3.up * 2f, Vector3.down, b50.velocity, b50.mass, b50.diameter, HitKind.Bullet, false, null, b50);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[AutoTest] forearm after .50: {LimbIntegrity.Of(fa)?.Status()}");
            Vector3 c3 = fa.transform.position;
            Photo("82_forearm", c3, c3 + new Vector3(0.5f, 0.8f, -0.5f));
            Debug.Log("[AutoTest] limb test done");
        }

        static void Shoot(Vector3 from, Vector3 at)
        {
            Ballistics.Instance.Fire(from, (at - from).normalized, 890f, 0.0421f, 0.0127f, HitKind.Bullet, false);
        }

        static bool SurfacePoint(BodyPart bp, Vector3 from, out RaycastHit hit)
        {
            Vector3 c = bp.transform.position;
            foreach (var h in Physics.RaycastAll(from, (c - from).normalized, 10f))
                if (h.collider.GetComponentInParent<BodyPart>() == bp) { hit = h; return true; }
            hit = default; return false;
        }

        public static bool distanceOnly, organOnly, grenadeOnly, chestOnly, boomOnly, boneOnly, layerOnly, neckOnly, depthOnly, nbrOnly, stabOnly, behaviourOnly, gaitOnly, hurtRunOnly, armourOnly, getUpOnly, newOnly, armDmgOnly, repeatOnly, limbOnly;

        IEnumerator Sequence()
        {
            var g = SandboxGame.Instance;
            if (distanceOnly) { yield return DistanceTest(g); Destroy(gameObject); yield break; }
            if (organOnly) { yield return OrganTest(g); Destroy(gameObject); yield break; }
            if (grenadeOnly) { yield return GrenadeTest(g); Destroy(gameObject); yield break; }
            if (chestOnly) { yield return ChestTest(g); Destroy(gameObject); yield break; }
            if (boomOnly) { yield return BoomTest(g); Destroy(gameObject); yield break; }
            if (boneOnly) { yield return BoneTest(g); Destroy(gameObject); yield break; }
            if (layerOnly) { yield return LayerTest(g); Destroy(gameObject); yield break; }
            if (neckOnly) { yield return NeckTest(g); Destroy(gameObject); yield break; }
            if (depthOnly) { yield return DepthTest(g); Destroy(gameObject); yield break; }
            if (nbrOnly) { yield return NbrTest(g); Destroy(gameObject); yield break; }
            if (stabOnly) { yield return StabTest(g); Destroy(gameObject); yield break; }
            if (behaviourOnly) { yield return BehaviourTest(g); Destroy(gameObject); yield break; }
            if (gaitOnly) { yield return GaitTest(g); Destroy(gameObject); yield break; }
            if (hurtRunOnly) { yield return HurtRunTest(g); Destroy(gameObject); yield break; }
            if (armourOnly) { yield return ArmourTest(g); Destroy(gameObject); yield break; }
            if (getUpOnly) { yield return GetUpTest(g); Destroy(gameObject); yield break; }
            if (newOnly) { yield return NewTest(g); Destroy(gameObject); yield break; }
            if (armDmgOnly) { yield return ArmDmgTest(g); Destroy(gameObject); yield break; }
            if (repeatOnly) { yield return RepeatTest(g); Destroy(gameObject); yield break; }
            if (limbOnly) { yield return LimbTest(g); Destroy(gameObject); yield break; }
            Vector3 basePos = g.player.transform.position;
            basePos.y = 0f;
            Vector3 fwd = Vector3.forward;

            // ---- 1. ten .50 cal shots to the head
            var a = g.SpawnCarl(SandboxGame.GroundAt(basePos + fwd * 4f) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < 10 && a != null; i++)
            {
                Vector3 head = a.Head.transform.position;
                Vector3 from = head + new Vector3(Random.Range(-0.3f, 0.3f), 0.05f, -3f);
                Shoot(from, head + Random.insideUnitSphere * 0.03f);
                Debug.Log($"[AutoTest] shot {i} at head {head}");
                if (i == 0)
                {
                    yield return new WaitForSeconds(0.25f);
                    Vector3 hp = a.Head.transform.position;
                    Snap("1a_head_oneshot_entry", hp, hp + (from - hp).normalized * 0.45f);
                    Snap("1a_head_oneshot_exit", hp, hp - (from - hp).normalized * 0.45f);
                }
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(3f);
            if (a != null)
            {
                Debug.Log($"[AutoTest] head holes={a.Head.holes.Count}");
                Around("1_head", a.Head.transform.position, 0.55f);
                SnapWound("1w_head", a.Head, 0.3f);
            }

            // ---- 2. thigh shots, stabs to the gut, slash on the arm
            var b = g.SpawnCarl(SandboxGame.GroundAt(basePos + fwd * 4f + Vector3.right * 3f) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            var thigh = b[PartId.ThighR];
            Shoot(thigh.transform.position + new Vector3(0, 0.05f, -3f), thigh.transform.position);
            yield return new WaitForSeconds(0.2f);
            if (SurfacePoint(thigh, thigh.transform.position + new Vector3(0, 0.05f, -1f), out var th))
                Snap("2_thigh_entry_closeup", th.point, th.point + th.normal * 0.25f + Vector3.up * 0.03f);
            yield return new WaitForSeconds(0.2f);
            Shoot(thigh.transform.position + new Vector3(0.2f, 0.1f, -3f), thigh.transform.position + Vector3.up * 0.06f);
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < 3; i++)
            {
                var ab = b[PartId.Abdomen];
                Vector3 from = ab.transform.position + new Vector3(0.05f * i - 0.05f, 0.03f * i, -1f);
                if (SurfacePoint(ab, from, out var hit))
                {
                    b.damage.ApplyStab(ab, hit.point, (hit.point - from).normalized, 0.16f);
                    if (i == 2) Snap("2_stab_closeup", hit.point, hit.point + hit.normal * 0.25f + Vector3.up * 0.03f);
                }
                yield return new WaitForSeconds(0.25f);
            }
            var arm = b[PartId.ForearmL];
            if (SurfacePoint(arm, arm.transform.position + new Vector3(0, 0, -1f), out var ah))
            {
                b.damage.ApplySlash(arm, ah.point, ah.normal, Vector3.right);
                Snap("2_slash_closeup", ah.point, ah.point + ah.normal * 0.22f + Vector3.up * 0.03f);
            }
            yield return new WaitForSeconds(0.3f);
            SnapWound("2w_stab", b[PartId.Abdomen]);
            SnapWound("2w_slash", arm, 0.14f);
            SnapWound("2w_thigh", thigh);
            Around("2_abdomen", b[PartId.Abdomen].transform.position, 0.6f);
            Around("2_thigh", thigh.transform.position, 0.6f);
            Around("2_arm", arm.transform.position, 0.5f);
            yield return new WaitForSeconds(3f);
            Around("2b_abdomen", b[PartId.Abdomen].transform.position, 0.6f);
            Around("2b_thigh", thigh.transform.position, 0.6f);

            // ---- 3. drag: does the trail run out?
            var c = g.SpawnCarl(SandboxGame.GroundAt(basePos + fwd * 4f - Vector3.right * 3f) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            Shoot(c.Chest.transform.position + new Vector3(0, 0, -3f), c.Chest.transform.position);
            yield return new WaitForSeconds(4f);
            Vector3 c0 = c.Chest.transform.position;
            g.StartCoroutine(g.DragTestRoutine(c));
            yield return new WaitForSeconds(3.5f);
            Vector3 mid = c0 + Vector3.right * 1.6f; mid.y = 0f;
            Snap("3_drag_top_t0", mid, mid + new Vector3(0, 4.5f, -0.6f));
            yield return new WaitForSeconds(6f);
            Snap("3_drag_top_t6", mid, mid + new Vector3(0, 4.5f, -0.6f));
            Snap("3_drag_low", mid, mid + new Vector3(0, 1.1f, -2.6f));

            // ---- 4. a full AK magazine into one Carl: wounds everywhere, but limbs should stay on
            var d = g.SpawnCarl(SandboxGame.GroundAt(basePos + fwd * 7f) + Vector3.up * 0.02f, 180f);
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < 30 && d != null; i++)
            {
                var bp = d.parts[Random.Range(0, d.parts.Length)];
                if (bp == null) continue;
                Vector3 t = bp.transform.position;
                Vector3 from = t + new Vector3(Random.Range(-0.5f, 0.5f), 0.4f, -4f);
                Ballistics.Instance.Fire(from, (t - from).normalized, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                yield return new WaitForSeconds(0.1f);
            }
            yield return new WaitForSeconds(2f);
            int severed = 0; foreach (var bp in d.parts) if (bp != null && bp.severed) severed++;
            Debug.Log($"[AutoTest] AK magazine: severed parts = {severed}");
            Vector3 dc = d.Chest.transform.position;
            Snap("4_ak_top", dc, dc + new Vector3(0.1f, 1.6f, -0.4f));
            Snap("4_ak_close", dc, dc + new Vector3(0.2f, 0.7f, -0.3f));
            SnapWound("4w_chest", d.Chest);
            Debug.Log("[AutoTest] done -> " + dir);
            Destroy(gameObject);
        }
    }
}
