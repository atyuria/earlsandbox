using System.Collections;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Development check for wounds, armour damage and dismemberment: stages the same few injuries every run and
    /// photographs them into Screenshots/autotest/gore_*.png. Started from the editor with GoreProbe.Run().
    /// </summary>
    public class GoreProbe : MonoBehaviour
    {
        public static void Run()
        {
            var old = FindFirstObjectByType<GoreProbe>();
            if (old != null) Destroy(old.gameObject);
            new GameObject("GoreProbe").AddComponent<GoreProbe>();
        }

        void Start() => StartCoroutine(Sequence());

        static void Shoot(Vector3 from, Vector3 at, string weapon, int ammo = 0)
        {
            var a = AmmoTable.For(weapon)[ammo];
            Ballistics.Instance.Fire(from, (at - from).normalized, a.velocity, a.mass, a.diameter, HitKind.Bullet, false, null, a);
        }

        static void Buckshot(Vector3 from, Vector3 at)
        {
            for (int i = 0; i < 9; i++)
            {
                Vector3 d = Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0f) * (at - from).normalized;
                Ballistics.Instance.Fire(from, d, 400f, 0.0035f, 0.0084f, HitKind.Bullet, false);
            }
        }

        static void Snap(string name, Vector3 at, Vector3 dir, float dist, HumanBody only = null)
            => AutoTest.Photo("gore_" + name, at, at + dir.normalized * dist, only, 40f);

        IEnumerator Sequence()
        {
            var g = SandboxGame.Instance;
            Vector3 b = g.player.transform.position; b.y = 0f;
            Vector3 fwd = Vector3.ProjectOnPlane(g.cam.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            HumanBody Spawn(float side, float ahead, ArmourLoadout L = null)
            {
                var h = g.SpawnCarl(SandboxGame.GroundAt(b + fwd * ahead + right * side) + Vector3.up * 0.02f, Quaternion.LookRotation(-fwd).eulerAngles.y);
                if (L != null) ArmourSystem.Equip(h, L);
                return h;
            }

            // 1: a grenade on the chest of an Earl in a vest, lying down
            var vest = new ArmourLoadout { vest = VestKind.Russian6B45, helmet = HelmetKind.None };
            var e1 = Spawn(-3f, 6f, vest);
            // 2: point-blank buckshot into the belly, and an AK through the forearm
            var e2 = Spawn(0f, 6f);
            // 3: AK into the vest pouches, AK at the Altyn visor (standing, dummy)
            var e3 = Spawn(3f, 6f, new ArmourLoadout { vest = VestKind.Russian6B45, helmet = HelmetKind.Spetsnaz });
            // 4: no armour, lying: a grenade against the belly, a .50 through the thigh, buckshot into the upper arm
            var e4 = Spawn(-6f, 6f);
            foreach (var h in new[] { e1, e2, e3, e4 }) h.brain.enabled = false;
            yield return new WaitForSeconds(1.2f);
            e1.ragdoll.Fall("probe"); e2.ragdoll.Fall("probe"); e4.ragdoll.Fall("probe");
            yield return new WaitForSeconds(3f);

            var th = e4[PartId.ThighR];
            Shoot(th.transform.position + Vector3.up * 1.5f, th.transform.position, ".50 CAL");
            var ua = e4[PartId.UpperArmL];
            for (int i = 0; i < 2; i++) Buckshot(ua.transform.position + Vector3.up * 0.8f, ua.transform.position);
            var ab = e4[PartId.Abdomen];
            Vector3 abAt = ab.col.ClosestPoint(ab.transform.position + Vector3.up * 0.6f);
            Grenade.Throw(abAt + Vector3.up * 0.03f, Vector3.zero).fuse = 0.05f;

            Vector3 c1 = e1.Chest.transform.position;
            Grenade.Throw(c1 + Vector3.up * 0.2f, Vector3.zero).fuse = 0.05f;

            Vector3 belly = e2[PartId.Abdomen].transform.position;
            Buckshot(belly + Vector3.up * 0.5f, belly);
            var fa = e2[PartId.ForearmL];
            Shoot(fa.transform.position + Vector3.up * 1f, fa.transform.position, "AK-47");

            Vector3 eye = e3.Head.transform.position + fwd * -0.0f;
            Vector3 from3 = e3.Chest.transform.position - Vector3.ProjectOnPlane(e3.Chest.transform.forward, Vector3.up) * -3f;
            var pouch = FindChild(e3, "MAG POUCH");
            Vector3 pouchAt = pouch != null ? pouch.GetComponent<Renderer>().bounds.center : e3[PartId.Abdomen].transform.position + e3.Chest.transform.forward * 0.12f;
            Vector3 front3 = e3.Chest.transform.forward;
            for (int i = 0; i < 3; i++) { Shoot(pouchAt + front3 * 3f + Random.insideUnitSphere * 0.01f, pouchAt, "AK-47"); yield return new WaitForSeconds(0.1f); }
            Vector3 face = e3.Head.transform.position + e3.Head.transform.forward * 0.05f;
            for (int i = 0; i < 2; i++) { Shoot(face + e3.Head.transform.forward * 3f, face + Random.insideUnitSphere * 0.02f, "GLOCK 17"); yield return new WaitForSeconds(0.1f); }
            yield return new WaitForSeconds(2f);

            c1 = e1.Chest.transform.position;
            Snap("blast_top", c1, Vector3.up + fwd * -0.3f, 0.75f);
            Snap("blast_close", c1, Vector3.up + right * 0.4f, 0.4f, e1);
            belly = e2[PartId.Abdomen].transform.position;
            Snap("buck_belly", belly, Vector3.up + right * 0.3f, 0.45f, e2);
            Snap("buck_belly_side", belly, Vector3.up * 0.4f + right, 0.4f, e2);
            Snap("forearm", fa.transform.position, Vector3.up + fwd * 0.2f, 0.35f, e2);
            Snap("forearm_side", fa.transform.position, right + Vector3.up * 0.3f, 0.35f, e2);
            Snap("pouches", pouchAt, front3 + right * 0.2f, 0.5f);
            Snap("visor", face, e3.Head.transform.forward + right * 0.3f, 0.35f);
            foreach (var bp in new[] { e4[PartId.Abdomen], e4[PartId.Chest], e4[PartId.ThighR], e4[PartId.UpperArmL], e4[PartId.Pelvis] })
            {
                if (bp == null) continue;
                Vector3 at = bp.transform.position;
                if (bp.holes.Count > 0)
                {
                    int best = 0; for (int i = 1; i < bp.holes.Count; i++) if (bp.holes[i].w > bp.holes[best].w) best = i;
                    at = bp.transform.TransformPoint(bp.holes[best]);
                }
                Snap($"e4_{bp.id}", at, Vector3.up + right * 0.35f, 0.42f, e4);
                Snap($"e4_{bp.id}_low", at, Vector3.up * 0.5f + fwd * -1f, 0.38f, e4);
            }
            Snap("e4_whole", e4.Pelvis.transform.position, Vector3.up + fwd * -0.4f, 1.6f, e4);
            // stump faces, looked at straight on
            int sn = 0;
            foreach (var bp in e4.partList)
                foreach (Transform t in bp.transform)
                    if (t.name == "StumpCap" && sn < 3)
                    {
                        // the cap faces along its mesh's average normal
                        var mf = t.GetComponent<MeshFilter>();
                        Vector3 nsum = Vector3.zero; foreach (var nn in mf.sharedMesh.normals) nsum += nn;
                        Vector3 outDir = t.TransformDirection(nsum).normalized;
                        Vector3 cc = t.GetComponent<Renderer>().bounds.center;
                        var layers = new System.Collections.Generic.List<(GameObject, int)>();
                        var all = new System.Collections.Generic.List<Transform>(bp.GetComponentsInChildren<Transform>());
                        foreach (var x in all) { layers.Add((x.gameObject, x.gameObject.layer)); x.gameObject.layer = 30; }
                        var cam = new GameObject("StumpCam").AddComponent<Camera>();
                        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
                        cam.fieldOfView = 40f; cam.nearClipPlane = 0.01f;
                        cam.transform.position = cc + (outDir + Vector3.up * 0.25f).normalized * 0.2f; cam.transform.LookAt(cc);
                        var rt = new RenderTexture(900, 700, 24); cam.targetTexture = rt; cam.Render();
                        var prev = RenderTexture.active; RenderTexture.active = rt;
                        var tex = new Texture2D(900, 700, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); tex.Apply();
                        RenderTexture.active = prev;
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Screenshots", "autotest", $"gore_stump{sn++}_{bp.id}.png"), tex.EncodeToPNG());
                        foreach (var (g2, l) in layers) g2.layer = l;
                        cam.targetTexture = null; rt.Release(); Destroy(cam.gameObject); Destroy(tex);
                    }
            Debug.Log($"[GoreProbe] e4 holes: belly {e4[PartId.Abdomen].holes.Count}, thigh {th.holes.Count}, arm {ua.holes.Count}; severed: {Severed(e4)}");
            Debug.Log($"[GoreProbe] done. e1 chest holes {e1.Chest.holes.Count}, belly holes {e2[PartId.Abdomen].holes.Count}, forearm holes {fa.holes.Count}, pouch {(pouch ? pouch.name : "none")}");
        }

        static string Severed(HumanBody h)
        {
            var s = "";
            foreach (var bp in h.partList) if (bp.severed) s += bp.id + " ";
            return s.Length == 0 ? "none" : s;
        }

        static Transform FindChild(HumanBody h, string contains)
        {
            foreach (var t in h.GetComponentsInChildren<Transform>()) if (t.name.Contains(contains) && t.GetComponent<Renderer>() != null) return t;
            return null;
        }
    }
}
