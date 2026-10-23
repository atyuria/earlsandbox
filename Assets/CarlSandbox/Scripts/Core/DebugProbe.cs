using UnityEngine;

namespace Carl
{
    /// <summary>Editor-side pokes at the running game (called through the editor bridge while testing).</summary>
    public static class DebugProbe
    {
        /// <summary>Spawn n armed, armoured Earls in one frame (in front of you) and time it: ms per Earl.</summary>
        public static string SpawnTime(int n)
        {
            var g = SandboxGame.Instance;
            var me = g.player.transform.position; var fw = g.player.transform.forward;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var lap = new System.Diagnostics.Stopwatch();
            double spawn = 0, armour = 0, gun = 0;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                lap.Restart();
                var h = g.SpawnCarl(me + fw * (4f + i * 0.8f) + Vector3.up * 0.02f, 180f);
                spawn += lap.Elapsed.TotalMilliseconds; lap.Restart();
                if (h == null) break;
                ArmourSystem.Equip(h, TrenchSquad.RandomKit());
                armour += lap.Elapsed.TotalMilliseconds; lap.Restart();
                EarlGunner.Arm(h, EarlWeapon.AK47, GunKit.Random(true), Teams.Enemy);
                gun += lap.Elapsed.TotalMilliseconds;
                made++;
            }
            sw.Stop();
            var sb = new System.Text.StringBuilder($"{made} Earls in {sw.Elapsed.TotalMilliseconds:0} ms = {sw.Elapsed.TotalMilliseconds / Mathf.Max(1, made):0.0} ms each. Per Earl: spawn {spawn / made:0.0} (");
            sb.Append($"), armour {armour / made:0.0}, gun {gun / made:0.0}");
            return sb.ToString();
        }

        /// <summary>Experiment: how long making a convex mesh collider takes, done different ways (ms each).</summary>
        public static string ColliderTest()
        {
            var mesh = ProcMesh.Ellipsoid(new Vector3(0.15f, 0.2f, 0.12f), 10, 8);
            var sb = new System.Text.StringBuilder();
            var opts = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;
            var list = new System.Collections.Generic.List<GameObject>();
            double Time(string what, System.Action<GameObject> make)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 40; i++) { var go = new GameObject("ct"); go.transform.position = new Vector3(0, -50 - i, 0); make(go); list.Add(go); }
                Physics.SyncTransforms();
                double ms = sw.Elapsed.TotalMilliseconds / 40;
                sb.Append($"{what}: {ms:0.000} ms; ");
                return ms;
            }
            Time("mesh then convex", go => { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true; });
            Time("convex then mesh", go => { var mc = go.AddComponent<MeshCollider>(); mc.convex = true; mc.sharedMesh = mesh; });
            var m2 = ProcMesh.Ellipsoid(new Vector3(0.15f, 0.2f, 0.12f), 10, 8);
            Physics.BakeMesh(m2.GetEntityId(), true, opts);
            Time("prebaked, convex then mesh", go => { var mc = go.AddComponent<MeshCollider>(); mc.convex = true; mc.cookingOptions = opts; mc.sharedMesh = m2; });
            var m3 = ProcMesh.Ellipsoid(new Vector3(0.15f, 0.2f, 0.12f), 10, 8);
            Physics.BakeMesh(m3.GetEntityId(), true);
            Time("prebaked default opts", go => { var mc = go.AddComponent<MeshCollider>(); mc.convex = true; mc.sharedMesh = m3; });
            var tmpl = new GameObject("tmpl"); var tmc = tmpl.AddComponent<MeshCollider>(); tmc.convex = true; tmc.sharedMesh = mesh;
            Time("Instantiate a template", go => { var c = Object.Instantiate(tmpl); c.transform.position = go.transform.position; list.Add(c); });
            Time("capsule collider", go => { go.AddComponent<CapsuleCollider>(); });
            var cached = HumanBody.AnyCached();
            if (cached.coll != null)
            {
                sb.Append($"[cached coll mesh: {cached.coll.vertexCount} verts, readable {cached.coll.isReadable}] ");
                Time("cached coll mesh", go => { var mc = go.AddComponent<MeshCollider>(); mc.convex = true; mc.cookingOptions = opts; mc.sharedMesh = cached.coll; });
                Time("cached coll + skin on object", go => { var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = Object.Instantiate(cached.skin); go.AddComponent<MeshRenderer>(); var mc = go.AddComponent<MeshCollider>(); mc.convex = true; mc.cookingOptions = opts; mc.sharedMesh = cached.coll; });
                Time("skin on object, collider no mesh set", go => { var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = Object.Instantiate(cached.skin); go.AddComponent<MeshRenderer>(); var mc = go.AddComponent<MeshCollider>(); });
            }
            foreach (var g in list) Object.Destroy(g);
            Object.Destroy(tmpl);
            return sb.ToString();
        }

        /// <summary>A picture of the game view (Screenshots/probe_name.png).</summary>
        public static string Snap(string name)
        {
            string p = System.IO.Path.Combine(Application.dataPath, "..", "Screenshots", "probe_" + name + ".png");
            ScreenCapture.CaptureScreenshot(p);
            return p;
        }

        /// <summary>Renderers on the first body that isn't you: how many, how many on, how many cast shadows, by name.</summary>
        public static string Renderers()
        {
            foreach (var h in HumanBody.All)
            {
                if (h == null || h == PlayerAvatar.Body) continue;
                var all = h.GetComponentsInChildren<Renderer>(true);
                int on = 0, shadow = 0; var names = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var r in all)
                {
                    if (r.enabled && r.gameObject.activeInHierarchy) { on++; if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadow++; string n = r.gameObject.name.Split('_')[0]; names[n] = names.TryGetValue(n, out int c) ? c + 1 : 1; }
                }
                var sb = new System.Text.StringBuilder($"{h.DisplayName}: {all.Length} renderers, {on} on, {shadow} casting shadows; on by name: ");
                foreach (var kv in names) sb.Append(kv.Key + " " + kv.Value + ", ");
                int cols = h.GetComponentsInChildren<Collider>(true).Length;
                sb.Append($"; colliders {cols}; scripts {h.GetComponentsInChildren<MonoBehaviour>(true).Length}");
                return sb.ToString();
            }
            return "no body";
        }

        /// <summary>Every body: where, standing or down, side, what it's doing.</summary>
        public static string Bodies()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var h in HumanBody.All)
                if (h != null && h.Pelvis != null)
                    sb.AppendLine($"{h.DisplayName} at {h.Pelvis.transform.position} down {h.ragdoll.IsDown} dead {h.Dead} side {Teams.Of(h)} {(h.gunner != null ? h.gunner.Status + " " + h.gunner.NavDebug : "")}");
            return sb.ToString();
        }

        /// <summary>Every collider within r of a point, and what it belongs to.</summary>
        public static string Colliders(float x, float y, float z, float r)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in Physics.OverlapSphere(new Vector3(x, y, z), r, ~0, QueryTriggerInteraction.Collide))
            {
                var hb = c.GetComponentInParent<HumanBody>();
                sb.AppendLine($"{c.name} ({c.GetType().Name}{(c.isTrigger ? ", trigger" : "")}) parent {(c.transform.parent ? c.transform.parent.name : "-")} {(hb != null ? "body " + hb.DisplayName : "")} at {c.bounds.center} size {c.bounds.size}");
            }
            return sb.ToString();
        }
    }
}
