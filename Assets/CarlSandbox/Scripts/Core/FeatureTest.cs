using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Scripted playtests for the newer features. Each scenario sets something up in the running game, measures
    /// what happens and writes a report (and pictures) to Screenshots/featuretest/[name].txt / .png. Start one
    /// from the editor while playing: FeatureTest.Run("blunt"). FeatureTest.Last holds the newest report.
    /// </summary>
    public class FeatureTest : MonoBehaviour
    {
        public static string Last = "";
        public static bool Busy;
        string scenario;
        readonly StringBuilder log = new StringBuilder();
        string dir;

        public static void Run(string name)
        {
            if (Busy) { Debug.LogWarning("[FeatureTest] already running"); return; }
            Busy = true;
            Last = "";
            var go = new GameObject("FeatureTest_" + name);
            go.AddComponent<FeatureTest>().scenario = name;
        }

        /// <summary>For poking at things from the editor: load a map now.</summary>
        public static void Map(int i) => SandboxGame.Instance?.LoadMap(i);

        void Start()
        {
            dir = Path.Combine(Application.dataPath, "..", "Screenshots", "featuretest");
            Directory.CreateDirectory(dir);
            StartCoroutine(Main());
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[FeatureTest] " + s); }
        void Check(bool ok, string what) => Line((ok ? "PASS  " : "FAIL  ") + what);

        IEnumerator Main()
        {
            Line($"== {scenario}  ({System.DateTime.Now:HH:mm:ss})");
            IEnumerator body = null;
            switch (scenario)
            {
                case "blunt": body = Blunt(); break;
                case "giveup": body = GiveUp(); break;
                case "armour": body = Armour(); break;
                case "trench": body = Trench(); break;
                case "handling": body = Handling(); break;
                case "medical": body = Medical(); break;
                case "optics": body = Optics(); break;
                case "audio": body = Audio(); break;
                case "pistol": body = Pistol(); break;
                case "adrenaline": body = Adrenaline(); break;
                case "adraim": body = AdrenalineAim(); break;
                case "recoil": body = RecoilTest(); break;
                case "ladder": body = LadderTest(); break;
                case "haze": body = HazeTest(); break;
                case "tactics": body = TacticsTest(); break;
                case "squad": body = SquadTest(); break;
                case "stuck": body = StuckTest(); break;
                case "battle": body = BattleTest(); break;
                case "drone": body = DroneTest(); break;
                case "antidrone": body = AntiDroneTest(); break;
                case "fire": body = FireTest(); break;
                case "sprint": body = SprintTest(); break;
                case "sprintkeys": body = SprintKeysTest(); break;
                case "underfire": body = UnderFireTest(); break;
                case "burnout": body = BurnOutTest(); break;
                case "wound": body = WoundTest(); break;
                case "gear": body = GearTest(); break;
                case "walk": body = WalkTest(); break;
                case "heat": body = HeatTest(); break;
                case "quality": body = QualityTest(); break;
                case "scopes": body = ScopesTest(); break;
                case "night": body = NightTest(); break;
                case "armies": body = ArmiesTest(); break;
                case "camo": body = CamoTest(); break;
                case "reload": body = ReloadTest(); break;
                case "newguns": body = NewGunsTest(); break;
                case "nightearls": body = NightEarls(); break;
                case "blind": body = BlindTest(); break;
                case "spacing": body = SpacingTest(); break;
                case "housemodes": body = HouseModesTest(); break;
                case "peek": body = PeekTest(); break;
                case "gunsmith": body = GunsmithTest(); break;
                case "opticlist": body = OpticsTest(); break;
                case "smithmenu": body = SmithMenuTest(); break;
                case "droneopts": body = DroneOptionsTest(); break;
                case "smithscreen": body = SmithScreenTest(); break;
                case "gmenu": body = GMenuTest(); break;
                case "smithguns": body = SmithGunsTest(); break;
                case "pilots": body = PilotsTest(); break;
                case "pilotlook": body = PilotLookTest(); break;
                case "mounts": body = MountsTest(); break;
                case "accuracy": body = AccuracyTest(); break;
                default: Line("unknown scenario"); break;
            }
            if (body != null) yield return body;
            Last = log.ToString();
            File.WriteAllText(Path.Combine(dir, scenario + ".txt"), Last);
            Busy = false;
            Destroy(gameObject);
        }

        // ------------------------------------------------------------ helpers

        static SandboxGame Game => SandboxGame.Instance;

        /// <summary>A dummy Earl (no AI) standing at a spot, settled.</summary>
        HumanBody Dummy(Vector3 feet, float yaw = 0f)
        {
            var h = Game.SpawnCarl(feet, yaw);
            if (h != null) h.brain.aiEnabled = false;
            return h;
        }

        static int Wounds(HumanBody h) { int n = 0; foreach (var bp in h.partList) if (bp != null) n += bp.wounds.Count; return n; }

        static void Shift(HumanBody h, Vector3 by, Vector3 vel)
        {
            foreach (var bp in h.partList)
            {
                if (bp == null || bp.rb == null) continue;
                bp.rb.position += by; bp.transform.position += by;
                bp.rb.linearVelocity = vel; bp.rb.angularVelocity = Vector3.zero;
            }
        }

        static GameObject Wall(Vector3 centre, Vector3 size)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "TestWall";
            w.transform.position = centre; w.transform.localScale = size;
            return w;
        }

        /// <summary>Somewhere open and flat on the current map, away from the player.</summary>
        Vector3 OpenSpot(Vector3 near)
        {
            for (int i = 0; i < 40; i++)
            {
                Vector3 p = near + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                Vector3 g = SandboxGame.GroundAt(p + Vector3.up * 2f);
                if (!Physics.CheckBox(g + Vector3.up * 1.2f, new Vector3(1.5f, 1f, 1.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return g;
            }
            return SandboxGame.GroundAt(near + Vector3.forward * 4f);
        }

        // ------------------------------------------------------------ blunt impacts

        IEnumerator Blunt()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1f);
            Vector3 basePos = OpenSpot(Game.player.transform.position + Vector3.forward * 10f);

            // 1. walking pace into a wall, three times
            var a = Dummy(basePos, 0f);
            yield return new WaitForSeconds(2f);
            var wall = Wall(basePos + new Vector3(0f, 1f, 0.9f), new Vector3(3f, 2f, 0.3f));
            float pain0 = a.physio.pain; int w0 = Wounds(a);
            float maxPain = 0f;
            for (int k = 0; k < 3; k++)
            {
                foreach (var bp in a.partList) if (bp && bp.rb) bp.rb.linearVelocity = new Vector3(0f, 0f, 2.2f);
                for (float t = 0; t < 1f; t += Time.deltaTime) { maxPain = Mathf.Max(maxPain, a.physio.pain); yield return null; }
            }
            Check(Wounds(a) == w0, $"walk into wall: no new wounds ({Wounds(a) - w0})");
            Check(maxPain - pain0 < 0.05f, $"walk into wall: pain stays low (+{maxPain - pain0:0.000})");
            Destroy(wall);

            // 2. an AI Earl wandering about bumping into things for a while
            var b = Game.SpawnCarl(basePos + new Vector3(3f, 0f, 0f), 0f);
            var box = Wall(basePos + new Vector3(3f, 1f, 1.2f), new Vector3(2f, 2f, 0.3f));
            yield return new WaitForSeconds(1f);
            float bp0 = b.physio.pain; float bMax = 0f;
            for (float t = 0; t < 6f; t += Time.deltaTime)
            {
                b.ragdoll.moveDir = Vector3.forward; b.ragdoll.moveSpeed = 1.6f; b.ragdoll.faceDir = Vector3.forward;
                bMax = Mathf.Max(bMax, b.physio.pain);
                yield return null;
            }
            Check(bMax - bp0 < 0.05f, $"Earl walking into a wall for 6 s: pain stays low (+{bMax - bp0:0.000}, wounds {Wounds(b)})");
            Destroy(box);

            // 3. a slight drop (0.8 m) while standing
            var c = Dummy(basePos + new Vector3(-3f, 0f, 0f), 0f);
            yield return new WaitForSeconds(2f);
            float cp0 = c.physio.pain; int cw0 = Wounds(c); float cMax = 0f;
            Shift(c, Vector3.up * 0.8f, Vector3.zero);
            for (float t = 0; t < 3f; t += Time.deltaTime) { cMax = Mathf.Max(cMax, c.physio.pain); yield return null; }
            Check(Wounds(c) == cw0 && cMax - cp0 < 0.05f, $"0.8 m drop: unhurt (pain +{cMax - cp0:0.000}, wounds +{Wounds(c) - cw0})");

            // 4. thrown down from 7 m: that must hurt
            var d = Dummy(basePos + new Vector3(-6f, 0f, 0f), 0f);
            yield return new WaitForSeconds(2f);
            d.ragdoll.Fall("test");
            Shift(d, Vector3.up * 7f, Vector3.zero);
            float dMax = 0f;
            for (float t = 0; t < 4f; t += Time.deltaTime) { dMax = Mathf.Max(dMax, d.physio.pain); yield return null; }
            Check(dMax > 0.2f || Wounds(d) > 0, $"7 m fall: hurt (pain {dMax:0.00}, wounds {Wounds(d)})");
            foreach (var h in new[] { a, b, c, d }) if (h) Destroy(h.gameObject);
        }

        /// <summary>The game view as you see it (HUD and overlays included), saved as a picture.</summary>
        IEnumerator GrabView(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null) { File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToJPG(85)); Destroy(tex); }
        }

        static List<string> Pieces(HumanBody h)
        {
            var l = new List<string>();
            foreach (var bp in h.partList) if (bp != null) foreach (var ap in bp.GetComponentsInChildren<ArmourPiece>()) if (!ap.fallen) l.Add(bp.id + ":" + ap.label);
            return l;
        }

        // ------------------------------------------------------------ armour, arm guards, the visor

        IEnumerator Armour()
        {
            Game.LoadMap(1);
            yield return new WaitForSeconds(1.5f);
            var L = new ArmourLoadout { vest = VestKind.Russian6B45, plateClass = 5, helmet = HelmetKind.Spetsnaz, visor = true, visorDown = true, armGuards = true };
            PlayerAvatar.Wear(L);
            yield return new WaitForSeconds(0.8f);
            var me = PlayerAvatar.Body;
            var pcs = Pieces(me);
            Line("on you: " + string.Join(", ", pcs));
            Check(pcs.Contains("UpperArmL:6B45 ARM GUARD") && pcs.Contains("UpperArmR:6B45 ARM GUARD"), "arm guards on both upper arms");
            Check(pcs.Exists(p => p.EndsWith("ALTYN VISOR")) && pcs.Exists(p => p.EndsWith("VISOR GLASS")), "Altyn visor and glass on");
            yield return new WaitForSeconds(0.5f);
            Check(VisorView.Down > 0.95f, $"visor down covers the view (Down={VisorView.Down:0.00})");
            yield return GrabView("armour_visor_down");
            // an Earl in the same kit, photographed from the side and the front
            var e = Dummy(Game.player.transform.position + Game.player.transform.forward * 3f, 180f);
            ArmourSystem.Equip(e, L.Clone());
            yield return new WaitForSeconds(1.5f);
            var c = e.Chest.transform.position;
            AutoTest.Photo("armour_earl_front", c, c + e.Chest.transform.forward * 1.6f + Vector3.up * 0.2f, e, 45f);
            AutoTest.Photo("armour_earl_side", c, c + e.Chest.transform.right * 1.6f + Vector3.up * 0.2f, e, 45f);
            var ep = Pieces(e);
            Check(ep.Contains("UpperArmL:6B45 ARM GUARD") && ep.Contains("UpperArmR:6B45 ARM GUARD"), "Earl gets arm guards too");
            // a pistol round into the arm guard: soft armour stops it
            var arm = e[PartId.UpperArmR];
            Vector3 at = arm.transform.position, from = at + e.Chest.transform.right * 2f;
            int Holes(BodyPart p) { int n = 0; foreach (var w in p.wounds) if (w.type == WoundType.Entry || w.type == WoundType.Exit) n++; return n; }
            int w0 = Holes(arm);
            var nine = AmmoTable.For("GLOCK 17")[0];
            for (int k = 0; k < 3; k++)
            {
                Ballistics.Instance.Fire(from + Vector3.up * (k * 0.02f), (at - from).normalized, nine.velocity, nine.mass, nine.diameter, HitKind.Bullet, false, null, nine);
                yield return new WaitForSeconds(0.3f);
            }
            var wt = new List<string>(); foreach (var w in arm.wounds) wt.Add(w.type + ":" + w.desc);
            Check(Holes(arm) == w0, $"9 mm into the arm guard (x3) is stopped (penetrating wounds {Holes(arm) - w0}; all: {string.Join(", ", wt)})");
            // flip your visor up with N's code path, then shoot your visor glass from the front
            var vis = ArmourSystem.VisorOf(me, out var glass);
            vis.ToggleVisor();
            yield return new WaitForSeconds(0.8f);
            Check(VisorView.Down < 0.05f, $"visor up clears the view (Down={VisorView.Down:0.00})");
            yield return GrabView("armour_visor_up");
            vis.ToggleVisor();
            yield return new WaitForSeconds(0.8f);
            // eight pistol rounds into the glass: it crazes over until you can't see through it
            for (int k = 0; k < 8; k++)
            {
                Vector3 pane = glass.transform.TransformPoint(glass.bounds.center) + Random.insideUnitSphere * 0.015f;
                Vector3 gunAt = pane + me.Head.transform.forward * 4f;
                Ballistics.Instance.Fire(gunAt, (pane - gunAt).normalized, nine.velocity, nine.mass, nine.diameter, HitKind.Bullet, false, null, nine);
                yield return new WaitForSeconds(0.25f);
                if (k == 2) yield return GrabView("armour_visor_hit3");
            }
            yield return new WaitForSeconds(0.3f);
            Check(glass != null && (glass.dents.Count + glass.breaks.Count) >= 4, $"rounds hit the visor glass (dents {glass?.dents.Count}, breaks {glass?.breaks.Count}), you alive: {!me.Dead}");
            Check(VisorView.Frost > 0.6f, $"smashed glass: you can't see through it (frost {VisorView.Frost:0.00})");
            yield return GrabView("armour_visor_smashed");
            // SHIFT+N: tear it off
            keysDown.Add(UnityEngine.InputSystem.Key.LeftShift); keysDown.Add(UnityEngine.InputSystem.Key.N);
            yield return Hold(0.15f);
            Release();
            yield return new WaitForSeconds(0.5f);
            Check(ArmourSystem.VisorOf(me, out _) == null && VisorView.Down == 0f, "SHIFT+N tears the visor off and the view is clear");
            yield return GrabView("armour_visor_torn_off");
            if (e) Destroy(e.gameObject);
            PlayerAvatar.Wear(new ArmourLoadout { vest = VestKind.None, helmet = HelmetKind.None });
        }

        // ------------------------------------------------------------ ladders and cover on the trench map

        void ClearEarls()
        {
            foreach (var h in HumanBody.All.ToArray()) if (h != null && h != PlayerAvatar.Body) { HumanBody.All.Remove(h); Destroy(h.gameObject); }
            TrenchSquad.Clear();
        }

        IEnumerator LadderTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Line($"ladders: {Ladder.All.Count}");
            Check(Ladder.All.Count >= 8, "ladders along the front lines");
            int walls = 0, wrecks = 0, hogs = 0, rims = 0, wire = 0, revet = 0, duck = 0;
            foreach (var t in FindObjectsByType<Transform>())
                switch (t.name)
                {
                    case "SandbagWall": walls++; break; case "Wreck": wrecks++; break; case "Hedgehog": hogs++; break;
                    case "CraterRim": rims++; break; case "Wire": wire++; break; case "Revetment": revet++; break; case "Duckboard": duck++; break;
                }
            Line($"cover: sandbag walls {walls}, wrecks {wrecks}, hedgehogs {hogs}, crater rim lumps {rims}, wire runs {wire}; revetment {revet}, duckboards {duck}");
            Check(walls >= 8 && wrecks >= 2 && hogs >= 6, "cover out on the field");
            // the ladder nearest your spawn: stand at its foot, climb
            var l = Ladder.Nearest(Game.SpawnPoint);
            Game.player.Teleport(l.foot + Vector3.up * 0.05f, Mathf.Atan2(l.outward.x, l.outward.z) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(2.5f);
            Game.player.LookAt(l.lip + Vector3.up * 0.5f);
            yield return GrabView("ladder_foot");
            var me = PlayerAvatar.Body;
            Check(LadderClimber.Begin(me, l, true) != null, "starts climbing");
            float t0 = Time.time;
            while (LadderClimber.Busy(me) && Time.time - t0 < 12f) yield return null;
            yield return new WaitForSeconds(1.5f);
            Vector3 pel = me.Pelvis.transform.position;
            Line($"after climbing ({Time.time - t0:0.0} s): pelvis {pel}, top {l.top}, down {me.ragdoll.IsDown}");
            Check(pel.y > l.top.y + 0.5f && !me.ragdoll.IsDown, "out on top, on his feet");
            Game.player.LookAt(pel + l.outward * 10f + Vector3.up * 0.8f);
            yield return GrabView("ladder_top");
            // and back down
            Check(LadderClimber.Begin(me, l, false) != null, "starts climbing down");
            t0 = Time.time;
            var trace = new StringBuilder();
            float nextT = 0f;
            while ((LadderClimber.Busy(me) || Time.time - t0 < 4f) && Time.time - t0 < 12f)
            {
                if (Time.time - t0 >= nextT) { nextT += 0.25f; trace.Append($"{Time.time - t0:0.0}:{me.Pelvis.transform.position.y:0.00}/{Vector3.Angle(me.Pelvis.transform.up, Vector3.up):0}{(LadderClimber.Busy(me) ? "L" : "")} "); }
                yield return null;
            }
            Line("down trace (t:y/tilt): " + trace);
            yield return new WaitForSeconds(1.5f);
            pel = me.Pelvis.transform.position;
            Check(pel.y < l.foot.y + 1.3f && !me.ragdoll.IsDown, $"back in the trench, standing (pelvis y {pel.y:0.00})");
            // a view along the field
            Game.player.Teleport(l.top + Vector3.up * 0.05f, Mathf.Atan2(l.outward.x, l.outward.z) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(2f);
            yield return GrabView("field");
        }

        IEnumerator HazeTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            yield return new WaitForSeconds(1.5f);
            var cam = Game.player.cam.transform;
            // a burst along the trench into the earth: muzzle smoke, dust off the walls, dirt where they land
            Vector3 muzzle = cam.position + cam.forward * 0.6f - Vector3.up * 0.1f;
            Vector3 aim = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 wallDir = (aim + Vector3.down * 0.15f).normalized;
            for (int i = 0; i < 10; i++)
            {
                Fx.Muzzle(muzzle, wallDir, 1f);
                Fx.BlastDust(muzzle, wallDir, 1f);
                Ballistics.Instance.Fire(muzzle, (wallDir + Random.insideUnitSphere * 0.03f).normalized, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                yield return new WaitForSeconds(0.1f);
            }
            yield return new WaitForSeconds(0.4f);
            Line($"after a 10-round burst: {Haze.Count} puffs, at camera {Haze.AtCamera:0.00}");
            yield return GrabView("haze_burst");
            // a look along the trench at a cloud a few metres off
            Haze.Clear();
            Vector3 along = Vector3.zero; float best = 0f;
            for (int k = 0; k < 16; k++)
            {
                Vector3 d = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                float dd = Physics.Raycast(me + Vector3.up * 1.2f, d, out var ah, 30f) ? ah.distance : 30f;
                if (dd > best) { best = dd; along = d; }
            }
            Game.player.LookAt(me + Vector3.up * 1.5f + along * 10f);
            for (int k = 0; k < 6; k++) Haze.DirtHit(me + along * Mathf.Min(best - 0.5f, 5f) + Vector3.up * Random.Range(0.4f, 1.6f), -along, 1.5f);
            yield return new WaitForSeconds(0.6f);
            yield return GrabView("haze_cloud");
            aim = along;
            // the grenade: 5 m along the trench
            Vector3 gp = me + aim * 5f + Vector3.up * 0.3f;
            if (Physics.Raycast(me + Vector3.up * 1f, aim, out var wh, 5f)) gp = me + aim * Mathf.Max(1.5f, wh.distance - 0.5f) + Vector3.up * 0.3f;
            var g = Grenade.Throw(gp, Vector3.zero); g.fuse = 0.2f; g.shrapnel = 0;
            yield return new WaitForSeconds(1.2f);
            float tr = Haze.Transmit(me + Vector3.up * 1.6f, gp + aim * 3f + Vector3.up * 1f);
            Line($"1 s after the grenade: {Haze.Count} puffs, sight through the cloud {tr:0.00}, at camera {Haze.AtCamera:0.00}");
            Check(tr < 0.35f, "the grenade's cloud blocks the view down the trench");
            yield return GrabView("haze_grenade");
            // wind: what is left, 20 s and 50 s on
            yield return new WaitForSeconds(20f);
            float tr20 = Haze.Transmit(me + Vector3.up * 1.6f, gp + aim * 3f + Vector3.up * 1f);
            Line($"20 s: {Haze.Count} puffs, sight {tr20:0.00}");
            yield return GrabView("haze_20s");
            yield return new WaitForSeconds(30f);
            float tr50 = Haze.Transmit(me + Vector3.up * 1.6f, gp + aim * 3f + Vector3.up * 1f);
            Line($"50 s: {Haze.Count} puffs, sight {tr50:0.00}");
            Check(tr20 > tr && tr50 > 0.75f, "it clears slowly");
            // up top it goes far quicker
            Haze.Clear();
            Vector3 field = me + Vector3.up * 6f;
            Haze.Puff(field, Vector3.zero, 1f, 1f, Color.gray);
            Haze.Puff(me + Vector3.up * 0.8f, Vector3.zero, 1f, 1f, Color.gray);
            yield return new WaitForSeconds(10f);
            float hi = Haze.Transmit(field - Vector3.right * 3f, field + Vector3.right * 3f), lo = Haze.Transmit(me + Vector3.up * 0.8f - Vector3.right * 3f, me + Vector3.up * 0.8f + Vector3.right * 3f);
            Line($"after 10 s: open air sight {hi:0.00}, in the trench {lo:0.00}");
            Check(hi > lo, "it lingers longer in the trench than over the top");
        }

        EarlGunner Gunner(Vector3 feet, int team, float yaw = 0f)
        {
            var h = Game.SpawnCarl(feet, yaw);
            if (h == null) return null;
            ArmourSystem.Equip(h, TrenchSquad.RandomKit());
            h.brain.aiEnabled = true;
            return EarlGunner.Arm(h, EarlWeapon.AK47, null, team);
        }

        IEnumerator TacticsTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Line($"nav: {TrenchNav.Nodes.Count} points");
            Vector3 theirs = Vector3.zero; float bz = -1f;
            foreach (var n in TrenchNav.Nodes) if (n.y < 0.5f && n.z > 20f && (bz < 0f || Mathf.Abs(n.x - me.x) < bz)) { bz = Mathf.Abs(n.x - me.x); theirs = n; }
            var trenchOnly = TrenchNav.Path(me, theirs, true, 50f);
            var overTop = TrenchNav.Path(me, theirs, true, 0.4f);
            int lad = 0; foreach (var st in overTop) if (st.kind != TrenchNav.Edge.Walk) lad++;
            float len(System.Collections.Generic.List<TrenchNav.Step> p) { float l = 0f; for (int i = 1; i < p.Count; i++) l += Vector3.Distance(p[i - 1].at, p[i].at); return l; }
            bool anyField = false; foreach (var st in trenchOnly) if (st.at.y > 1.4f) anyField = true;
            Line($"path to their line: by trench {trenchOnly.Count} points {len(trenchOnly):0} m (uses field: {anyField}); over the top {overTop.Count} points {len(overTop):0} m, {lad} ladder climbs");
            Check(trenchOnly.Count > 0 && overTop.Count > 0 && lad >= 2, "routes to their trench, along the trenches and over the top");

            // a lone enemy 12+ m off down the trench: hears a shot, works out roughly where
            Vector3 spot = me; float best = 0f;
            foreach (var n in TrenchNav.Nodes) { float d = Vector3.Distance(n, me); if (n.y < 0.5f && d > 12f && d < 20f && d > best) { best = d; spot = n; } }
            var e = Gunner(spot, Teams.Enemy);
            yield return new WaitForSeconds(2f);
            Intel.Gunshot(me + Vector3.up * 1.4f, Teams.Friendly, 60f, PlayerAvatar.Body, null);
            yield return new WaitForSeconds(0.5f);
            Line($"enemy heard the shot: suspects {Vector3.Distance(e.suspect, me):0.0} m from you (conf {e.SuspectConf:0.00}); stance {e.StanceName}");
            Check(e.SuspectConf > 0.3f && Vector3.Distance(e.suspect, me) < 4f, "a shot gives your position away (roughly)");
            float t0 = Time.time; string stances = "";
            while (Time.time - t0 < 6f) { if (!stances.EndsWith(e.StanceName)) stances += " " + e.StanceName; yield return null; }
            Line("what he did: " + stances);
            Destroy(e.gameObject); HumanBody.All.Remove(e.Body);
            yield return null;

            // grenade: he thinks someone is hiding round the corner and it's gone quiet
            e = Gunner(spot, Teams.Enemy);
            e.grenades = 2;
            yield return new WaitForSeconds(1.5f);
            e.GrenadeNow();
            Vector3 hide = me; best = 0f;
            foreach (var n in TrenchNav.Nodes) { float d = Vector3.Distance(n, spot); if (n.y < 0.5f && d > 9f && d < 16f && Vector3.Distance(n, me) > 10f && !EarlGunner.Clear(e.Body.Head.transform.position, n + Vector3.up, e.Body)) { hide = n; break; } }
            e.Learn(hide, 0.9f, false); e.suspectTime = Time.time - 5f;
            int before = Grenade.All.Count; float thrownAt = -1f;
            t0 = Time.time;
            while (Time.time - t0 < 10f) { if (Grenade.All.Count > before && thrownAt < 0f) thrownAt = Time.time - t0; yield return null; }
            Line($"grenade: {(thrownAt >= 0f ? $"thrown after {thrownAt:0.0} s" : "not thrown")}, {e.grenades} left");
            Check(thrownAt >= 0f, "throws a grenade into the corner you've gone quiet in");
            yield return new WaitForSeconds(3f);
            Destroy(e.gameObject); HumanBody.All.Remove(e.Body);

            // a grenade at his feet
            var f = Gunner(spot, Teams.Enemy);
            yield return new WaitForSeconds(2f);
            Vector3 gp = f.Body.Pelvis.transform.position + f.Body.ragdoll.faceDir * 1.2f; gp.y = spot.y + 0.2f;
            var g = Grenade.Throw(gp, Vector3.zero); g.fuse = 3.2f; g.shrapnel = 0; g.team = Teams.Friendly;
            float start = Vector3.Distance(f.Body.Pelvis.transform.position, gp);
            yield return new WaitForSeconds(3.1f);
            float end = Vector3.Distance(f.Body.Pelvis.transform.position, gp);
            Line($"grenade at his feet: {start:0.0} m -> {end:0.0} m when it went off, down {f.Body.ragdoll.IsDown}");
            Check(end > start + 2.5f || f.Body.ragdoll.IsDown, "runs from (or dives away from) a grenade");
            yield return new WaitForSeconds(1f);
            Destroy(f.gameObject); HumanBody.All.Remove(f.Body);

            // wandering, and yours pushing over to their trench
            var w = Gunner(spot, Teams.Enemy);
            var a = Gunner(me + Vector3.right * 1.5f, Teams.Friendly); a.overTheTop = false;
            var b = Gunner(me - Vector3.right * 1.5f, Teams.Friendly); b.overTheTop = true;
            Vector3 w0 = w.Body.Pelvis.transform.position, a0 = a.Body.Pelvis.transform.position, b0 = b.Body.Pelvis.transform.position;
            float wMax = 0f, bTop = 0f;
            t0 = Time.time; float nextLog = 2f;
            while (Time.time - t0 < 30f)
            {
                if (Time.time - t0 > nextLog && b != null) { nextLog += 5f; Line($"  over-the-top: sees {b.SeesEnemy} conf {b.SuspectConf:0.0} {b.StanceName} {b.NavDebug} {b.SpacingDebug} [{b.Status}] v {b.Body.Pelvis.rb.linearVelocity.magnitude:0.0} order {b.order}"); Line($"  trench: sees {a.SeesEnemy} conf {a.SuspectConf:0.0} {a.StanceName} {a.NavDebug} {a.SpacingDebug} [{a.Status}] v {a.Body.Pelvis.rb.linearVelocity.magnitude:0.0} order {a.order}"); }
                if (w != null && w.Body != null) wMax = Mathf.Max(wMax, Vector3.Distance(w.Body.Pelvis.transform.position, w0));
                if (b != null && b.Body != null) bTop = Mathf.Max(bTop, b.Body.Pelvis.transform.position.y);
                yield return null;
            }
            Line($"wanderer went up to {wMax:0} m from his start; trench friendly z {a0.z:0} -> {a.Body.Pelvis.transform.position.z:0}; over-the-top friendly z {b0.z:0} -> {b.Body.Pelvis.transform.position.z:0}, highest {bTop:0.0}");
            Check(wMax > 5f, "enemies wander their trench");
            Check(a.Body.Pelvis.transform.position.z > a0.z + 8f || b.Body.Pelvis.transform.position.z > b0.z + 8f, "friendlies push toward their trench");
            Check(bTop > 2.3f, "the over-the-top one climbed out");
            Game.player.LookAt(b.Body.Center);
            yield return GrabView("tactics_advance");
        }

        IEnumerator GearTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            var w = Game.weapons;
            int ak = w.defs.FindIndex(d => d.name == "AK-47");
            w.Give(ak, true);
            w.loadouts[ak].laser = true; w.loadouts[ak].light = true;
            w.Select(ak);
            w.RefreshAttachments();
            WeaponController.LaserOn = true; WeaponController.LightOn = true;
            // look along the trench
            Vector3 me = Game.SpawnPoint; Vector3 along = Vector3.zero; float best = 0f;
            for (int k = 0; k < 16; k++)
            {
                Vector3 d = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                float dd = Physics.Raycast(me + Vector3.up * 1.2f, d, out var ah, 30f) ? ah.distance : 30f;
                if (dd > best) { best = dd; along = d; }
            }
            Game.player.LookAt(me + Vector3.up * 1.3f + along * 10f);
            yield return new WaitForSeconds(2f);
            var beam = GameObject.Find("LaserBeam")?.GetComponent<LineRenderer>();
            Check(beam != null && beam.enabled, "AK with a laser: the beam is on");
            yield return GrabView("laser_clear");
            for (int k = 0; k < 6; k++) Haze.DirtHit(me + along * Mathf.Min(best - 0.5f, 5f) + Vector3.up * Random.Range(0.6f, 1.6f), -along, 1.5f);
            yield return new WaitForSeconds(0.5f);
            Line($"beam colour alpha in the dust {beam.startColor.a:0.00}");
            Check(beam.startColor.a > 0.3f, "the beam shows up in the dust");
            yield return GrabView("laser_dust");
            WeaponController.LaserOn = false;
            yield return null; yield return null;
            Check(!beam.enabled, "J turns it off");
            WeaponController.LaserOn = true;
            GameSettings.Crosshair = false;
            yield return new WaitForSeconds(0.3f);
            yield return GrabView("no_crosshair");
            GameSettings.Crosshair = true;
        }

        /// <summary>Hold W (as if pressed) for a while.</summary>
        IEnumerator HoldW(float secs)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            float t0 = Time.time;
            while (Time.time - t0 < secs)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
                yield return null;
            }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        }

        public static bool WalkNoBodies;

        IEnumerator WalkTest()
        {
            // the yard: flat ground, a row of dead Earls across your way, then you walk over them
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            // a way with nothing in it for 12 m
            Vector3 fwd = Vector3.forward;
            for (int k = 0; k < 16; k++)
            {
                Vector3 d = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                bool blocked = false;
                foreach (float hgt in new[] { 0.3f, 0.8f, 1.4f })
                    foreach (var hit in Physics.SphereCastAll(me + Vector3.up * hgt - d * 0.5f, 0.35f, d, 12f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.collider.GetComponentInParent<HumanBody>() == null && !(hit.collider is CharacterController)) blocked = true;
                if (!blocked) { fwd = d; break; }
            }
            Game.player.LookAt(me + Vector3.up * 1.4f + fwd * 10f);
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < 4 && !WalkNoBodies; i++)
            {
                var d = Dummy(me + fwd * (2.5f + i * 1.6f) + side * Random.Range(-0.4f, 0.4f), Random.Range(0f, 360f));
                d.physio.Kill("TEST", true);
            }
            yield return new WaitForSeconds(3f);
            var you = PlayerAvatar.Body;
            int falls = 0; bool wasDown = false;
            Vector3 p0 = you.Pelvis.transform.position;
            var hold = StartCoroutine(HoldW(7f));
            float t0 = Time.time;
            var tr = new StringBuilder(); float nt = 0f;
            while (Time.time - t0 < 7f)
            {
                if (Time.time - t0 >= nt) { nt += 0.5f; tr.Append($"{Time.time - t0:0.0}:{Vector3.Dot(you.Pelvis.transform.position - p0, fwd):0.0}/{you.Pelvis.transform.position.y:0.00}/{you.ragdoll.mode}/{you.ragdoll.moveSpeed:0.0} "); }
                if (you.ragdoll.IsDown && !wasDown) falls++;
                wasDown = you.ragdoll.IsDown;
                yield return null;
            }
            float went = Vector3.Dot(you.Pelvis.transform.position - p0, fwd);
            Line($"walked {went:0.0} m over 4 bodies: fell {falls} times");
            Line("trace (t:along/height/mode/speed): " + tr);
            Check(went > 7f && falls == 0, "you walk over bodies without tripping");
            yield return GrabView("walk_bodies");
            // knocked over, not hurt: hold space and up you get
            you.ragdoll.Fall("test");
            yield return new WaitForSeconds(0.3f);
            var kb = UnityEngine.InputSystem.Keyboard.current;
            float tu = Time.time;
            while (you.ragdoll.mode != BodyMode.Standing && Time.time - tu < 12f)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Space));
                yield return null;
            }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            float upT = Time.time - tu;
            Line($"knocked down unhurt: standing again after {upT:0.0} s");
            Check(upT < 5f, "you get back up quickly when you're not hurt");
        }

        IEnumerator WoundTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            var d = Dummy(me + fwd * 4f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            var d2 = Dummy(me + fwd * 4f + Vector3.Cross(Vector3.up, fwd) * 2f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            yield return new WaitForSeconds(2f);
            var nine = AmmoTable.For("GLOCK 17")[0];
            var chest = d.Chest;
            Vector3 at = chest.transform.position, from = at + fwd * -3f;
            Line($"before: holes {chest.holes.Count}, reveal built {chest.revealRoot != null}");
            Ballistics.Instance.Fire(from, (at - from).normalized, nine.velocity, nine.mass, nine.diameter, HitKind.Bullet, false, null, nine);
            yield return new WaitForSeconds(0.6f);
            int organsShown = chest.revealRoot != null ? chest.revealRoot.GetComponentsInChildren<MeshRenderer>().Length : 0;
            Line($"after a 9 mm into the chest: holes {chest.holes.Count}, wounds {chest.wounds.Count}, reveal built {chest.revealRoot != null} active {(chest.revealRoot != null && chest.revealRoot.gameObject.activeInHierarchy)}, organ pieces drawn {organsShown}, flesh layers {(chest.fleshRoot != null ? chest.fleshRoot.childCount : 0)}");
            Check(chest.holes.Count > 0 && chest.revealRoot != null && chest.revealRoot.gameObject.activeInHierarchy && organsShown > 0, "a hole opens and the insides show through it");
            Check(d2.Chest.revealRoot == null, "an Earl nobody has shot hasn't built any of that");
            Vector3 cam = at - fwd * 0.9f + Vector3.up * 0.1f;
            AutoTest.Photo("wound_chest", at, cam, null, 40f);
            // buckshot into the belly from close, and a forearm hit until it comes off
            Vector3 belly = d[PartId.Abdomen].transform.position;
            for (int i = 0; i < 9; i++) { Vector3 dd = Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0f) * (belly - (belly - fwd * 2f)).normalized; Ballistics.Instance.Fire(belly - fwd * 2f, dd, 400f, 0.0035f, 0.0084f, HitKind.Bullet, false); }
            yield return new WaitForSeconds(0.6f);
            AutoTest.Photo("wound_belly", belly, belly - fwd * 0.9f + Vector3.up * 0.15f, null, 40f);
            var arm = d2[PartId.ForearmR];
            for (int i = 0; i < 12 && !arm.severed; i++)
            {
                Vector3 a = arm.transform.position, f2 = a - fwd * 2f;
                Ballistics.Instance.Fire(f2, (a - f2).normalized, 715f, 0.0079f, 0.0078f, HitKind.Bullet, false);
                yield return new WaitForSeconds(0.15f);
            }
            yield return new WaitForSeconds(0.8f);
            Line($"forearm after up to 12 rifle rounds: severed {arm.severed}, holes {arm.holes.Count}, reveal built {arm.revealRoot != null}");
            // a burst into one thigh: many holes in one part (each tunnel is made once)
            var d3 = Dummy(me + fwd * 3f - Vector3.Cross(Vector3.up, fwd) * 2f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            yield return new WaitForSeconds(1.5f);
            var thigh = d3[PartId.ThighR];
            WoundChannels.Built = WoundChannels.Reused = 0; WoundChannels.BuildMs = 0;
            for (int i = 0; i < 15; i++)
            {
                Vector3 a = thigh.transform.position + Random.insideUnitSphere * 0.04f, f2 = a - fwd * 2f + Random.insideUnitSphere * 0.2f;
                Ballistics.Instance.Fire(f2, (a - f2).normalized, 375f, 0.0075f, 0.009f, HitKind.Bullet, false);
                yield return new WaitForSeconds(0.07f);
            }
            yield return new WaitForSeconds(0.8f);
            Line($"thigh after a 15-round burst: holes {thigh.holes.Count}; tunnels built {WoundChannels.Built} ({WoundChannels.BuildMs / Mathf.Max(1, WoundChannels.Built):0.00} ms each), reused {WoundChannels.Reused}");
            Vector3 tp = thigh.transform.position;
            AutoTest.Photo("wound_thigh_burst", tp, tp - fwd * 0.55f + Vector3.up * 0.1f, d3, 40f);
            Vector3 cp = d.Chest.transform.position;
            AutoTest.Photo("wound_chest_now", cp, cp - fwd * 0.6f + Vector3.up * 0.2f, d, 40f);
            AutoTest.Photo("wound_arm", d2[PartId.UpperArmR].transform.position, d2[PartId.UpperArmR].transform.position - fwd * 1.2f + Vector3.up * 0.3f, null, 40f);
        }

        IEnumerator DroneTest()
        {
            Game.LoadMap(1);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            var target = Dummy(me + fwd * 14f, 180f);
            var target2 = Dummy(me + fwd * 10f + Vector3.Cross(Vector3.up, fwd) * 6f, 180f);
            yield return new WaitForSeconds(1.5f);
            // the kamikaze: straight at him
            var d = FpvDrone.Launch(FpvDrone.Kind.Kamikaze);
            Check(d != null && FpvDrone.Piloting == d && !Game.player.cam.enabled, "kamikaze drone up, you're seeing through it");
            yield return new WaitForSeconds(0.6f);
            yield return GrabView("drone_fpv");
            float t0 = Time.time;
            while (d != null && !d.Ended && Time.time - t0 < 8f) { d.autopilot = (target.Center - d.transform.position).normalized * 14f; yield return null; }
            yield return new WaitForSeconds(0.5f);
            Line($"kamikaze: hit after {Time.time - t0:0.0} s; Earl dead {target.Dead}, wounds {Wounds(target)}; back in your eyes {Game.player.cam.enabled}");
            Check(Wounds(target) > 3 && Game.player.cam.enabled && FpvDrone.Piloting == null, "it blows up on him, you're back in your body");
            yield return GrabView("drone_after");
            // the dropper: over the other one, drop
            var b = FpvDrone.Launch(FpvDrone.Kind.Bomber);
            t0 = Time.time;
            Vector3 over = target2.Center + Vector3.up * 6f;
            while (Time.time - t0 < 10f && Vector3.Distance(b.transform.position, over) > 0.6f) { b.autopilot = Vector3.ClampMagnitude((over - b.transform.position) * 2f, 8f); yield return null; }
            b.autopilot = Vector3.zero;
            yield return new WaitForSeconds(0.6f);
            int before = Wounds(target2);
            b.Drop();
            yield return new WaitForSeconds(2.5f);
            Line($"dropper: dropped from {b.transform.position.y - target2.Center.y:0} m above; wounds {before} -> {Wounds(target2)}");
            Check(Wounds(target2) > before, "a dropped grenade hurts whoever it lands by");
            b.End("TEST OVER");
            yield return new WaitForSeconds(0.5f);
            Check(Game.player.cam.enabled && FpvDrone.Piloting == null, "E ends it, back in your body");
        }

        /// <summary>
        /// An Earl against your drones: one hanging still in front of him is shot down; one jinking about takes
        /// far longer to hit; a dropper high overhead goes mostly unseen.
        /// </summary>
        IEnumerator AntiDroneTest()
        {
            Game.LoadMap(1);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            Vector3 at = me + fwd * 32f;
            if (Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out var gh, 40f, ~0, QueryTriggerInteraction.Ignore)) at = gh.point;
            var g = Gunner(at, Teams.Enemy, Quaternion.LookRotation(-fwd).eulerAngles.y);
            yield return new WaitForSeconds(2f);
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            // hovering still, 16 m in front of him and a few metres up
            var d = FpvDrone.Launch(FpvDrone.Kind.Bomber);
            Vector3 hover = at - fwd * 16f + Vector3.up * 5f;
            float t0 = Time.time; int shots0 = EarlGunner.DroneShots;
            while (d != null && !d.Ended && Time.time - t0 < 20f) { d.autopilot = Vector3.ClampMagnitude((hover - d.transform.position) * 2f, 8f); yield return null; }
            float still = Time.time - t0;
            Line($"hovering: down after {still:0.0} s ({EarlGunner.DroneShots - shots0} rounds at it), ended {d == null || d.Ended}; mean gun-off {EarlGunner.DroneMissSum / Mathf.Max(1, EarlGunner.DroneShots - shots0):0.0} deg, aim-pt-off {EarlGunner.DroneAimErrSum / Mathf.Max(1, EarlGunner.DroneShots - shots0):0.0} deg");
            Check(d == null || d.Ended, "a drone hanging still in front of him gets shot down");
            if (d != null && !d.Ended) d.End("TEST OVER");
            yield return new WaitForSeconds(1.5f);
            // jinking: side to side and up and down, changing every half second or so
            var j = FpvDrone.Launch(FpvDrone.Kind.Bomber);
            t0 = Time.time; shots0 = EarlGunner.DroneShots;
            Vector3 vel = Vector3.zero; float change = 0f;
            while (j != null && !j.Ended && Time.time - t0 < 30f)
            {
                if (Time.time > change) { change = Time.time + Random.Range(0.35f, 0.7f); vel = side * Random.Range(-1f, 1f) * 9f + Vector3.up * Random.Range(-1f, 1f) * 4f; }
                Vector3 pull = (hover - j.transform.position) * 0.8f;
                j.autopilot = vel + pull;
                yield return null;
            }
            float moving = Time.time - t0;
            Line($"jinking: lasted {moving:0.0} s ({EarlGunner.DroneShots - shots0} rounds at it), ended {j == null || j.Ended}");
            Check(moving > still * 1.5f, "dodging about keeps it up much longer");
            if (j != null && !j.Ended) j.End("TEST OVER");
            yield return new WaitForSeconds(1.5f);
            // high overhead: a speck he rarely picks out
            var h = FpvDrone.Launch(FpvDrone.Kind.Bomber);
            Vector3 high = at - fwd * 6f + Vector3.up * 32f;
            // (straight up there, the same frame: launched in front of him at head height he'd have seen it go up)
            var hrb = h.GetComponent<Rigidbody>();
            hrb.position = high; h.transform.position = high; hrb.linearVelocity = Vector3.zero;
            h.autopilot = Vector3.zero;
            shots0 = EarlGunner.DroneShots; t0 = Time.time;
            while (h != null && !h.Ended && Time.time - t0 < 10f) { h.autopilot = Vector3.ClampMagnitude((high - h.transform.position) * 2f, 6f); yield return null; }
            Line($"high up (32 m): {EarlGunner.DroneShots - shots0} rounds at it in 10 s");
            Check(EarlGunner.DroneShots - shots0 < 15, "a dropper high up mostly goes unseen");
            if (h != null && !h.Ended) h.End("TEST OVER");
            yield return new WaitForSeconds(1.5f);
            // after a missed drop: the men it went off by know to look up; others further off don't
            ClearEarls();
            var near = new System.Collections.Generic.List<EarlGunner>();
            for (int k = 0; k < 4; k++) near.Add(Gunner(at + side * (k - 1.5f) * 2.5f, Teams.Enemy, Quaternion.LookRotation(-fwd).eulerAngles.y));
            for (int k = 0; k < 3; k++) Gunner(at + fwd * 40f + side * (k - 1f) * 3f, Teams.Enemy, Quaternion.LookRotation(-fwd).eulerAngles.y);
            yield return new WaitForSeconds(2f);
            var r = FpvDrone.Launch(FpvDrone.Kind.Bomber);
            Vector3 over = at + Vector3.up * 35f;
            var rrb = r.GetComponent<Rigidbody>();
            rrb.position = over; r.transform.position = over; rrb.linearVelocity = Vector3.zero;
            foreach (var e in near) if (e != null) e.droneAlertUntil = Time.time + 25f;
            r.RevealedUntil = Time.time + 45f;
            shots0 = EarlGunner.DroneShots; t0 = Time.time; int hits0 = FpvDrone.Hits;
            while (r != null && !r.Ended && Time.time - t0 < 20f) { r.autopilot = Vector3.ClampMagnitude((over - r.transform.position) * 2f, 6f); yield return null; }
            float lasted = Time.time - t0;
            Line($"  ({FpvDrone.Hits - hits0} hits; ended: {FpvDrone.LastEnd})");
            Line($"revealed, hovering 35 m up over 4 alerted men: lasted {lasted:0.0} s, {EarlGunner.DroneShots - shots0} rounds at it, down {r == null || r.Ended}");
            Check(lasted > 19f, "even once they know it's up there, a speck 35 m up is hard to hit");
            if (r != null && !r.Ended) r.End("TEST OVER");
        }

        /// <summary>You, sprinting with a rifle up through the trenches, turning hard: you should never go over.</summary>
        IEnumerator SprintTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(2f);
            ClearEarls();
            Wpn.Give(Idx("AK-47"), true);
            yield return new WaitForSeconds(0.5f);
            var me = PlayerAvatar.Body;
            int falls = 0; bool wasDown = false;
            float t0 = Time.time;
            Vector3 dir = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            float turnAt = 0f;
            while (Time.time - t0 < 25f)
            {
                // hard turns every second or so, back and forth along the trench
                if (Time.time > turnAt) { turnAt = Time.time + Random.Range(0.6f, 1.4f); dir = Quaternion.Euler(0f, Random.Range(-150f, 150f), 0f) * dir; }
                PlayerAvatar.TestRun = dir;
                bool down = me.ragdoll.IsDown;
                if (down && !wasDown) falls++;
                wasDown = down;
                yield return null;
            }
            PlayerAvatar.TestRun = null;
            Line($"25 s sprinting with a rifle, turning hard: {falls} falls");
            Check(falls == 0, "sprinting never puts you on the floor");
            // and going down on purpose, you're straight back up
            me.ragdoll.Fall("test");
            float t1 = Time.time;
            while (me.ragdoll.IsDown || me.ragdoll.mode != BodyMode.Standing) { if (Time.time - t1 > 8f) break; yield return null; }
            Line($"knocked down: back on your feet in {Time.time - t1:0.0} s");
            Check(Time.time - t1 < 3f, "you get back up quickly, on your own");
        }

        /// <summary>
        /// The real controls, as a player uses them: SHIFT + W held with a rifle out, the mouse swinging you round,
        /// on the trench map (lips, duckboards, ladders, bodies) and the yard. Every fall is logged with its reason,
        /// and how long getting back up took.
        /// </summary>
        IEnumerator SprintKeysTest()
        {
            // (the editor drops queued input while it isn't the focused window: take it anyway for this)
            var bg = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (int map in new[] { 3, 1 })
            {
                Game.LoadMap(map);
                Cursor.lockState = CursorLockMode.Locked;
                yield return new WaitForSeconds(2f);
                ClearEarls();
                Wpn.Give(Idx("AK-47"), true);
                yield return Hold(0.5f);
                var me = PlayerAvatar.Body;
                int falls = 0; bool wasDown = false; float downAt = 0f, worstUp = 0f;
                float t0 = Time.time, turn = 0f, turnUntil = 0f;
                var kb = UnityEngine.InputSystem.Keyboard.current; var mouse = UnityEngine.InputSystem.Mouse.current;
                while (Time.time - t0 < 30f)
                {
                    if (Time.time > turnUntil) { turnUntil = Time.time + Random.Range(0.5f, 1.5f); turn = Random.Range(-600f, 600f); }
                    bool shift = !(me != null && me.ragdoll.IsDown);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(shift ? new[] { UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift } : new[] { UnityEngine.InputSystem.Key.W }));
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { delta = new Vector2(turn * Time.deltaTime, 0f) });
                    bool down = me != null && me.ragdoll.IsDown;
                    if (down && !wasDown) { falls++; downAt = Time.time; Line($"  fell at {Time.time - t0:0.0} s: {me.ragdoll.LastFall}"); }
                    if (!down && wasDown) { worstUp = Mathf.Max(worstUp, Time.time - downAt); Line($"  back up after {Time.time - downAt:0.0} s"); }
                    if (down && Time.time - downAt > 6f && wasDown) { Line("  STILL DOWN after 6 s"); worstUp = 99f; downAt = Time.time; }
                    wasDown = down;
                    yield return null;
                }
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return GrabView("sprintkeys_" + map);
                Line($"{MapBuilder.Names[map]}: 30 s holding SHIFT + W with an AK, turning: {falls} falls, slowest back up {worstUp:0.0} s; moved {Vector3.Distance(Game.SpawnPoint, me.Center):0} m from spawn");
                Check(falls == 0, MapBuilder.Names[map] + ": sprinting doesn't put you on the floor");
            }
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = bg;
        }

        /// <summary>
        /// Sprinting with the real controls while two Earls shoot at you and shells land 7-10 m off every few seconds
        /// (INVINCIBLE on so you last): every fall and its reason, how long back up.
        /// </summary>
        IEnumerator UnderFireTest()
        {
            var bg = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            bool inv = GameSettings.Invincible; GameSettings.Invincible = true;
            Game.LoadMap(1);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(2f);
            ClearEarls();
            Wpn.Give(Idx("AK-47"), true);
            var me = PlayerAvatar.Body;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            Gunner(me.Center - Vector3.up * 0.9f + fwd * 25f + Vector3.Cross(Vector3.up, fwd) * 8f, Teams.Enemy, 180f);
            Gunner(me.Center - Vector3.up * 0.9f + fwd * 30f - Vector3.Cross(Vector3.up, fwd) * 8f, Teams.Enemy, 180f);
            yield return Hold(0.5f);
            int falls = 0; bool wasDown = false; float downAt = 0f, worstUp = 0f, nextShell = 0f;
            float t0 = Time.time, turn = 0f, turnUntil = 0f;
            var kb = UnityEngine.InputSystem.Keyboard.current; var mouse = UnityEngine.InputSystem.Mouse.current;
            while (Time.time - t0 < 30f)
            {
                if (Time.time > turnUntil) { turnUntil = Time.time + Random.Range(0.5f, 1.5f); turn = Random.Range(-400f, 400f); }
                if (Time.time > nextShell)
                {
                    nextShell = Time.time + Random.Range(2.5f, 4f);
                    Vector2 r = Random.insideUnitCircle.normalized * Random.Range(7f, 10f);
                    Vector3 at = me.Center + new Vector3(r.x, 0f, r.y);
                    if (Physics.Raycast(at + Vector3.up * 10f, Vector3.down, out var gh, 30f, ~0, QueryTriggerInteraction.Ignore)) Barrage.Fire(gh.point, 0.1f, 0f, 0.3f);
                }
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { delta = new Vector2(turn * Time.deltaTime, 0f) });
                bool down = me.ragdoll.IsDown;
                if (down && !wasDown) { falls++; downAt = Time.time; Line($"  fell at {Time.time - t0:0.0} s: {me.ragdoll.LastFall}"); }
                if (!down && wasDown) { worstUp = Mathf.Max(worstUp, Time.time - downAt); Line($"  back up after {Time.time - downAt:0.0} s"); }
                wasDown = down;
                yield return null;
            }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            Line($"30 s sprinting under fire, shells landing 7-10 m off: {falls} falls, slowest back up {worstUp:0.0} s; dead {me.Dead}");
            Check(falls == 0, "rounds and shells near you don't put you down");
            Check(!me.Dead, "INVINCIBLE: you don't die");
            GameSettings.Invincible = inv;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = bg;
        }

        /// <summary>Three dummies set alight all over: the fire must burn out, and each leaves remains of some kind.</summary>
        IEnumerator BurnOutTest()
        {
            Game.LoadMap(1);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            var bodies = new List<HumanBody>();
            for (int i = 0; i < 3; i++) bodies.Add(Dummy(me + fwd * 6f + side * (i - 1) * 2.5f, 0f));
            yield return new WaitForSeconds(1.5f);
            foreach (var h in bodies) foreach (var bp in h.partList) if (bp != null) Burning.Ignite(bp, bp.transform.position, 1f);
            yield return new WaitForSeconds(1f);
            float t0 = Time.time;
            while (Time.time - t0 < 45f)
            {
                bool any = false;
                foreach (var h in bodies) if (Burning.Intensity(h) > 0f) any = true;
                if (!any) break;
                yield return new WaitForSeconds(0.5f);
            }
            float took = Time.time - t0;
            int remains = 0; foreach (var h in bodies) if (h.GetComponent<BurntRemains>() != null) remains++;
            Line($"three set alight all over: out after {took:0} s; dead {bodies.FindAll(h => h.Dead).Count}; with remains {remains}");
            Check(took < 40f, "the fire burns out once there's nothing left to burn");
            Check(remains >= bodies.FindAll(h => h.Dead).Count && remains > 0, "the ones it burned to death leave burnt remains");
            // let a melted one sag down, then look
            yield return new WaitForSeconds(22f);
            var cg = new GameObject("TestCloseCam");
            var c = cg.AddComponent<Camera>();
            c.depth = 50f; c.fieldOfView = 50f; c.nearClipPlane = 0.05f;
            Vector3 mid = (bodies[0].Center + bodies[1].Center + bodies[2].Center) / 3f;
            cg.transform.position = mid + Vector3.up * 3.2f - fwd * 2.5f;
            cg.transform.LookAt(mid);
            yield return new WaitForSeconds(0.3f);
            yield return GrabView("burnout_remains");
            Destroy(cg);
        }

        /// <summary>The flamethrower on a dummy (he catches, burns, chars) and a grenade beside another (scorches).</summary>
        IEnumerator FireTest()
        {
            Game.LoadMap(1);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            var target = Dummy(me + fwd * 3.2f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            var other = Dummy(me + fwd * 5f + side * 2.5f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            yield return new WaitForSeconds(1.5f);
            // two seconds of flame onto him
            for (float t = 0f; t < 4f; t += 0.1f)
            {
                Vector3 from = Game.player.cam.transform.position - Vector3.up * 0.3f + fwd * 0.5f;
                Flamethrower.Spray(from, (target.Center - from).normalized, PlayerAvatar.Body);
                yield return new WaitForSeconds(0.1f);
            }
            // a grenade at the other one's feet
            var g = Grenade.Throw(other.Pelvis.transform.position - Vector3.up * 0.8f + side * 1.1f, Vector3.zero);
            g.fuse = 0.05f;
            yield return new WaitForSeconds(1f);
            float lit = Burning.Intensity(target);
            Line($"after the flame: alight {lit:0.00}");
            Check(lit > 0.1f, "the flamethrower sets him alight");
            yield return GrabView("fire_burning");
            yield return new WaitForSeconds(25f);
            yield return GrabView("fire_6s");
            yield return new WaitForSeconds(10f);
            float charAvg = 0f; int n = 0;
            foreach (var bp in target.partList) if (bp != null) { charAvg += bp.charLevel; n++; }
            charAvg /= Mathf.Max(1, n);
            int scorch = 0;
            foreach (var bp in other.partList) if (bp != null) scorch += bp.burns.Count;
            Line($"16 s on: char {charAvg:0.00}, alight {Burning.Intensity(target):0.00}, dead {target.Dead}; grenade scorches on the other: {scorch}");
            Check(charAvg > 0.15f, "he's charred where he burned");
            Check(scorch > 0, "the grenade left burn marks");
            // up close: over to him (wherever he ran), and the one the grenade went off beside
            foreach (var (who, tag) in new[] { (target, "fire_close"), (other, "fire_scorch") })
            {
                // a camera of its own, above and to the side, looking down on him
                Vector3 at = who.Center;
                var cg = new GameObject("TestCloseCam");
                var c = cg.AddComponent<Camera>();
                c.depth = 50f; c.fieldOfView = 45f; c.nearClipPlane = 0.05f;
                cg.transform.position = at + Vector3.up * 1.4f + side * 1.2f - fwd * 0.6f;
                cg.transform.LookAt(at);
                yield return new WaitForSeconds(0.3f);
                yield return GrabView(tag);
                Destroy(cg);
            }
        }

        IEnumerator BattleTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(2f);
            Vector3 me = Game.SpawnPoint;
            // you: well out of it, up in the air in the free camera's place (so the fight is between them)
            Teams.PlayerOutOfIt = true;
            int en = 0, fr = 0; foreach (var g in EarlGunner.All) if (g != null) { if (g.team == Teams.Enemy) en++; else fr++; }
            Line($"the map came up with {en} enemies and {fr} friendlies");
            Check(fr >= 4, "friendlies are there in your trench when the map loads");
            Intel.Shots[0] = Intel.Shots[1] = 0; Intel.OwnSideHits = 0; Intel.SkyShots = 0;
            int frSaw = 0, enSaw = 0, grenades = 0, overTop = 0;
            var saw = new HashSet<EarlGunner>(); var top = new HashSet<EarlGunner>();
            float t0 = Time.time; float nextLog = 15f; int gBefore = EarlGunner.Thrown[0] + EarlGunner.Thrown[1];
            int carry = 0, armed = 0; foreach (var g in EarlGunner.All) if (g != null && !g.medic) { armed++; if (g.grenades > 0) carry++; }
            Line($"{carry} of {armed} riflemen carry grenades");
            Check(armed > 0 && carry >= armed * 0.6f, "most soldiers carry grenades");
            int gLast = gBefore;
            while (Time.time - t0 < 150f)
            {
                if (EarlGunner.Thrown[0] + EarlGunner.Thrown[1] != gLast) { gLast = EarlGunner.Thrown[0] + EarlGunner.Thrown[1]; Line($"    grenade at {Time.time - t0:0} s (ours {EarlGunner.Thrown[0]}, theirs {EarlGunner.Thrown[1]})"); }
                foreach (var g in EarlGunner.All)
                {
                    if (g == null || g.Body == null) continue;
                    if (g.SeesEnemy && saw.Add(g)) { if (g.team == 0) frSaw++; else enSaw++; }
                    if (g.Body.Pelvis.transform.position.y > 2.2f && top.Add(g)) overTop++;
                }
                grenades = EarlGunner.Thrown[0] + EarlGunner.Thrown[1] - gBefore;
                if (Time.time - t0 > nextLog)
                {
                    nextLog += 15f;
                    int fa = 0, ea = 0; foreach (var g in EarlGunner.All) if (g != null && g.Fighting) { if (g.team == 0) fa++; else ea++; }
                    Line($"  {Time.time - t0:0} s: friendlies fighting {fa}/{fr}, enemies {ea}/{en}; shots {Intel.Shots[0]} / {Intel.Shots[1]}, own-side hits {Intel.OwnSideHits}");
                }
                yield return null;
            }
            Line($"battle: {frSaw} friendlies and {enSaw} enemies got eyes on someone; {grenades} grenades thrown; {overTop} went over the top; own-side hits {Intel.OwnSideHits}");
            Check(Intel.Shots[0] > 10 && Intel.Shots[1] > 10, "both sides fight");
            Line($"grenades by side: ours {EarlGunner.Thrown[0]}, theirs {EarlGunner.Thrown[1]}");
            Check(grenades >= 2 && grenades <= 16, "grenades get used, but no barrage (2 to 16 in 150 s)");
            Check(Intel.OwnSideHits <= 2, "(almost) no shooting their own");
            Line($"shots steeply up into the air: {Intel.SkyShots} of {Intel.Shots[0] + Intel.Shots[1]}");
            Check(Intel.SkyShots * 20 <= Intel.Shots[0] + Intel.Shots[1] + 1, "hardly anyone shooting at the sky");
            Teams.PlayerOutOfIt = false;
        }

        public static bool SkipGuns;
        public static int OnlyQ = -1;
        public static string SprintWith = "AK-47";

        IEnumerator QualityTest()
        {
            int was = GameSettings.Graphics;
            GameSettings.InfiniteAmmo = true;
            foreach (int q in new[] { 2, 1, 0 })
            {
                if (OnlyQ >= 0 && q != OnlyQ) continue;
                GameSettings.Graphics = q; GameSettings.ApplyGraphics();
                string Q = q == 2 ? "HIGH" : q == 1 ? "MEDIUM" : "LOW";
                Game.LoadMap(0);
                Cursor.lockState = CursorLockMode.Locked;
                yield return new WaitForSeconds(1.5f);
                ClearEarls();
                var you = PlayerAvatar.Body;
                // sprinting: a clear way
                Vector3 me = Game.SpawnPoint, fwd = Vector3.forward;
                float bestRun = 0f;
                for (int k = 0; k < 16; k++)
                {
                    Vector3 d = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                    float free = 40f;
                    foreach (float hgt in new[] { 0.3f, 0.8f, 1.4f })
                        foreach (var hit in Physics.SphereCastAll(me + Vector3.up * hgt, 0.35f, d, 40f, ~0, QueryTriggerInteraction.Ignore))
                            if (hit.collider.GetComponentInParent<HumanBody>() == null && !(hit.collider is CharacterController)) free = Mathf.Min(free, hit.distance);
                    if (free > bestRun) { bestRun = free; fwd = d; }
                }
                Wpn.Give(Idx(SprintWith), true);
                Game.player.LookAt(me + Vector3.up * 1.4f + fwd * 10f);
                yield return Hold(1f);
                float y0 = you.Pelvis.transform.position.y, eye0 = Camera.main.transform.position.y;
                int falls = 0; bool wasDown = false; float lowP = 99f;
                var tr = new StringBuilder();
                keysDown.Add(UnityEngine.InputSystem.Key.W); keysDown.Add(UnityEngine.InputSystem.Key.LeftShift);
                float t0 = Time.time, nt = 0f;
                Vector3 p0 = you.Pelvis.transform.position;
                while (Time.time - t0 < 8f)
                {
                    if (Time.time - t0 >= nt) { nt += 0.5f; tr.Append($"\n      {Time.time - t0:0.0}s pelvis {you.Pelvis.transform.position.y - y0:+0.00;-0.00} eye {Camera.main.transform.position.y - eye0:+0.00;-0.00} chest tilt {Vector3.Angle(you.Chest.transform.up, Vector3.up):0} pelvis tilt {Vector3.Angle(you.Pelvis.transform.up, Vector3.up):0} head over chest {you.Head.transform.position.y - you.Chest.transform.position.y:0.00} head tilt {Vector3.Angle(you.Head.transform.up, Vector3.up):0}"); }
                    if (you.ragdoll.IsDown && !wasDown) falls++;
                    wasDown = you.ragdoll.IsDown;
                    lowP = Mathf.Min(lowP, you.Pelvis.transform.position.y - y0);
                    yield return Hold(0.01f);
                }
                keysDown.Clear(); yield return Hold(1f);
                float ran = Vector3.Dot(you.Pelvis.transform.position - p0, fwd);
                Line($"{Q}: sprinted {ran:0.0} m in {Time.time - t0 - 1f:0.0} s ({bestRun:0} m clear), fell {falls}, lowest pelvis {lowP:+0.00;-0.00} m, eye at the end {Camera.main.transform.position.y - eye0:+0.00;-0.00}");
                Line("   " + tr);
                Check(falls == 0 && lowP > -0.25f, $"{Q}: sprinting doesn't sink you down or trip you");
                // every gun: held still, aimed, fired
                Game.player.Teleport(me, 0f);
                yield return Hold(1f);
                foreach (var d in Wpn.defs)
                {
                    if (d.type != WeaponType.Gun || d.name == "TASER" || SkipGuns) continue;
                    int i = Wpn.defs.IndexOf(d);
                    Wpn.Give(i, true);
                    yield return Hold(1.2f);
                    var cam = Camera.main.transform;
                    string rep = ""; bool ok = true;
                    foreach (var phase in new[] { "still", "aiming", "firing" })
                    {
                        rmb = phase != "still"; lmb = phase == "firing";
                        yield return Hold(0.4f);
                        int s0 = Wpn.GetHoldDebug().snaps, g0 = WeaponController.GripSnaps;
                        var hd = Wpn.GetHoldDebug();
                        Vector3 lastL = hd.gun != null ? cam.InverseTransformPoint(hd.gun.position) : Vector3.zero;
                        float jit = 0f; int n = 0;
                        yield return Hold(1.2f, () =>
                        {
                            var h = Wpn.GetHoldDebug();
                            if (h.gun == null) return;
                            Vector3 l = cam.InverseTransformPoint(h.gun.position);
                            jit += (l - lastL).magnitude; lastL = l; n++;
                        });
                        float j = n > 0 ? jit / n * 1000f : -1f;
                        int sn = Wpn.GetHoldDebug().snaps - s0, gs = WeaponController.GripSnaps - g0;
                        rep += $"{phase}: judder {j:0.00} mm, snaps {sn}, grip {gs}   ";
                        if (sn > 0 || gs > 2 || phase != "firing" && j > 0.8f) ok = false;
                    }
                    lmb = rmb = false; yield return Hold(0.4f);
                    Line($"   {Q} {d.name}: {rep}");
                    Check(ok, $"{Q} {d.name}: steady in the hands");
                }
            }
            GameSettings.Graphics = was; GameSettings.ApplyGraphics();
        }

        public static int NightMap = 1;
        IEnumerator NightEarls()
        {
            int was = GameSettings.TimeOfDay;
            GameSettings.TimeOfDay = 2;
            Game.LoadMap(NightMap);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(2f);
            ClearEarls();
            Teams.PlayerOutOfIt = true;
            // two of them a few metres down the trench in front of you
            Vector3 me = Game.SpawnPoint, eye = Game.cam.transform.position;
            Vector3 a = me, b = me; float ba = 0f;
            for (int i = 0; i < TrenchNav.Nodes.Count; i++)
            {
                var n = TrenchNav.Nodes[i]; float d = Vector3.Distance(n, me);
                if (TrenchNav.Field[i] || d < 5f || d > 9f) continue;
                if (!EarlGunner.Clear(eye, n + Vector3.up * 1.2f, null)) continue;
                if (d > ba) { ba = d; a = n; }
            }
            // (on the straight line to that point, so they are in view)
            Game.player.LookAt(a + Vector3.up * 1.1f);
            yield return Hold(0.5f);
            Vector3 fwd = Vector3.ProjectOnPlane(Game.cam.transform.forward, Vector3.up).normalized;
            a = PlayerAvatar.Body.Pelvis.transform.position - Vector3.up * 0.9f + fwd * 3.5f;
            b = a + fwd * 1.8f + Vector3.Cross(Vector3.up, fwd) * 0.5f;
            Line($"you at {PlayerAvatar.Body.Pelvis.transform.position}, Earls at {a} and {b}");
            Gunner(a, Teams.Enemy, Quaternion.LookRotation(Vector3.ProjectOnPlane(me - a, Vector3.up)).eulerAngles.y);
            Gunner(b, Teams.Enemy, Quaternion.LookRotation(Vector3.ProjectOnPlane(me - a, Vector3.up)).eulerAngles.y);
            Wpn.Give(Idx("AK-47"), true);
            Game.player.LookAt(a + Vector3.up * 1.1f);
            yield return Hold(4f);
            Game.player.LookAt(a + Vector3.up * 1.1f);
            WeaponController.PlayerLightOn = false;
            yield return Hold(0.3f);
            yield return GrabView("nightearls_dark");
            keysDown.Add(UnityEngine.InputSystem.Key.L); yield return Hold(0.1f); keysDown.Clear(); yield return Hold(0.4f);
            Game.player.LookAt(a + Vector3.up * 1.1f);
            yield return Hold(0.2f);
            yield return GrabView("nightearls_lit");
            keysDown.Add(UnityEngine.InputSystem.Key.L); yield return Hold(0.1f); keysDown.Clear();
            Line($"two Earls {ba:0.0} m off; see nightearls_dark / nightearls_lit");
            GameSettings.TimeOfDay = was;
            Teams.PlayerOutOfIt = false;
        }

        IEnumerator ReloadTest()
        {
            int wasT = GameSettings.TimeOfDay; GameSettings.TimeOfDay = 0;
            Game.LoadMap(0);
            GameSettings.TimeOfDay = wasT;
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            GameSettings.InfiniteAmmo = false;
            string gun = ReloadGun;
            int i = Idx(gun);
            Wpn.Give(i, true);
            yield return Hold(1.2f);
            lmb = true; yield return Hold(0.4f); lmb = false; yield return Hold(0.3f);
            BarrelHeat.I.Clear();
            float T = Wpn.defs[i].reloadTime;
            int loose0 = GameObject.FindObjectsByType<Rigidbody>().Length;
            keysDown.Add(UnityEngine.InputSystem.Key.R); yield return Hold(0.08f); keysDown.Clear();
            float t0 = Time.unscaledTime;
            foreach (float f in new[] { 0.2f, 0.36f, 0.5f, 0.66f, 0.84f })
            {
                while (Time.unscaledTime - t0 < T * f) yield return Hold(0.01f);
                yield return GrabView($"reload_{gun.Replace("-", "").ToLower()}_{Mathf.RoundToInt(f * 100)}");
                var hdb = Wpn.GetHoldDebug();
                Line($"   {f:0.00}: reloading {Wpn.reloading}, moving {Wpn.Moving}, gun roll {(hdb.gun != null ? Vector3.SignedAngle(Game.cam.transform.up, hdb.gun.up, Game.cam.transform.forward) : -1f):0} deg, mag shown {(Wpn.CurrentModel != null && Wpn.CurrentModel.transform.Find("Gun_AK74/Mag") != null ? Wpn.CurrentModel.transform.Find("Gun_AK74/Mag").gameObject.activeSelf.ToString() : "no mag")}, left hand at {(PlayerAvatar.Body[PartId.HandL].transform.position - Game.cam.transform.position):F2}");
            }
            while (Time.unscaledTime - t0 < T + 0.4f) yield return Hold(0.01f);
            var loose = GameObject.Find("LooseMag");
            Line($"{gun}: reloaded to {Wpn.AmmoText}; the old magazine {(loose != null ? $"lies at {loose.transform.position.y:0.00} m" : "isn't there")}");
            Check(loose != null, "the old magazine comes out and drops");
            Check(Wpn.AmmoText.StartsWith(Wpn.MagSize(i).ToString()), "full again");
            // a double feed cleared with J: strip, rack, seat
            BarrelHeat.I.ForceJam(i, 3);
            for (int k = 0; k < 3; k++)
            {
                keysDown.Add(UnityEngine.InputSystem.Key.J); yield return Hold(0.06f); keysDown.Clear();
                yield return Hold(0.15f);
                yield return GrabView($"unjam_{gun.Replace("-", "").ToLower()}_{k}");
                yield return Hold(0.25f);
            }
            Check(!BarrelHeat.I.Jammed(i), "the double feed is cleared");
            GameSettings.InfiniteAmmo = true;
        }
        public static string ReloadGun = "AK-74";

        /// <summary>Earls with FPV drones: they go somewhere covered, goggles on, and send them at the other side.</summary>
        IEnumerator PilotsTest()
        {
            Game.LoadMap(1);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            GameSettings.Invincible = true;
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            // your side: two riflemen standing in the open 45 m off
            var a = Gunner(me + fwd * 8f + side * 3f, Teams.Friendly, Quaternion.LookRotation(fwd).eulerAngles.y);
            var b = Gunner(me + fwd * 8f - side * 3f, Teams.Friendly, Quaternion.LookRotation(fwd).eulerAngles.y);
            // theirs: two pilots
            var k = Gunner(me + fwd * 52f + side * 4f, Teams.Enemy, Quaternion.LookRotation(-fwd).eulerAngles.y);
            var bo = Gunner(me + fwd * 52f - side * 4f, Teams.Enemy, Quaternion.LookRotation(-fwd).eulerAngles.y);
            k.droneKit = FpvDrone.Kind.Kamikaze; bo.droneKit = FpvDrone.Kind.Bomber;
            int l0 = EarlGunner.DronesLaunched, s0 = EarlGunner.DroneShots;
            int wa = Wounds(a.Body) + Wounds(b.Body);
            float t0 = Time.time; bool saw = false;
            while (Time.time - t0 < 50f)
            {
                if (!saw && (k.Piloting || bo.Piloting)) { saw = true; Line($"first drone up after {Time.time - t0:0.0} s (k {k.Status} / b {bo.Status})"); }
                if (Time.frameCount % 500 == 0) Line($"  t {Time.time - t0:0}: drones up {FpvDrone.Active.Count}, rounds at drones {EarlGunner.DroneShots - s0}  |  K {k.PilotDebug}  |  B {bo.PilotDebug}");
                yield return null;
                if (saw && Mathf.Abs(Time.time - t0 - 14f) < 0.02f) { }
            }
            Line($"launched {EarlGunner.DronesLaunched - l0}, your men fired {EarlGunner.DroneShots - s0} rounds at drones, wounds on your men {wa} -> {Wounds(a.Body) + Wounds(b.Body)}, last drone end: {FpvDrone.LastEnd}");
            Check(EarlGunner.DronesLaunched - l0 == 2, "both pilots got their drones up");
            Check(EarlGunner.DroneShots - s0 > 0, "your men shoot at the enemy drones");
            GameSettings.Invincible = false;
        }

        /// <summary>
        /// How well an Earl shoots you, standing in the open in front of him at 15, 35 and 60 m, at the enemy skill
        /// setting: rounds fired, rounds that hit you, and how long to the first hit.
        /// </summary>
        IEnumerator AccuracyTest()
        {
            Game.LoadMap(1);
            yield return new WaitForSeconds(1.5f);
            bool inv = GameSettings.Invincible; GameSettings.Invincible = true;
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            Line($"enemy skill {GameSettings.EnemySkill:0.00}");
            foreach (float dist in new[] { 15f, 35f, 60f })
            {
                ClearEarls();
                Vector3 at = me + fwd * dist;
                if (Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out var gh, 40f, ~0, QueryTriggerInteraction.Ignore)) at = gh.point;
                var g = Gunner(at, Teams.Enemy, Quaternion.LookRotation(-fwd).eulerAngles.y);
                g.defend = true; g.holdPoint = at;
                int s0 = EarlGunner.ShotsFired, h0 = Ballistics.PlayerHits;
                float t0 = Time.time, first = -1f;
                while (Time.time - t0 < 12f) { if (first < 0f && Ballistics.PlayerHits > h0) first = Time.time - t0; yield return null; }
                int shots = EarlGunner.ShotsFired - s0, hits = Ballistics.PlayerHits - h0;
                Line($"  {dist:0} m: {shots} rounds, {hits} hit you ({(shots > 0 ? 100f * hits / shots : 0f):0}%), first hit after {(first < 0 ? "-" : first.ToString("0.0") + " s")}");
            }
            GameSettings.Invincible = inv;
        }

        /// <summary>A light and a laser on each rail of an AK-74M in the gunsmith.</summary>
        IEnumerator MountsTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            int i = Idx("AK-74M");
            Wpn.Give(i, true);
            var L = Wpn.loadouts[i];
            L.build = new GunBuild { platform = "AK", parts = new[] { "415", "railed", "b10", "dtk1", "rk3", "zhukov", "545b" } };
            L.grip = GripKind.Vertical; L.foregrip = "rvg"; L.light = true; L.lightId = "m600"; L.laser = true; L.laserId = "perst";
            Wpn.ApplyBuild(i);
            Game.menus.ShowGunPage(i);
            yield return null;
            Game.menus.OpenGunsmith(i);
            Game.menus.SmithSelect("LIGHT");
            foreach (var (ls, zs) in new[] { (0, 1), (1, 0), (2, 2), (3, 3) })
            {
                L.lightSide = ls; L.laserSide = zs; Wpn.RefreshAttachments();
                yield return new WaitForSeconds(1.2f);
                yield return GrabView($"mount_light{WeaponController.SideNames[ls]}_laser{WeaponController.SideNames[zs]}");
            }
            Game.menus.Toggle(MenuKind.Gunsmith); Game.menus.Toggle(MenuKind.Gun);
        }

        /// <summary>A pilot with his goggles on, close up.</summary>
        IEnumerator PilotLookTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            var p = Gunner(me + fwd * 3f, Teams.Friendly, Quaternion.LookRotation(-fwd).eulerAngles.y);
            p.droneKit = FpvDrone.Kind.Bomber;
            var e = Gunner(me - fwd * 60f, Teams.Enemy, 0f); e.Body.brain.aiEnabled = false;
            float t0 = Time.time;
            while (!p.Piloting && Time.time - t0 < 25f) yield return null;
            yield return new WaitForSeconds(1.5f);
            Line($"piloting {p.Piloting} after {Time.time - t0:0.0} s, status {p.Status}");
            yield return GrabView("pilot_look");
            // and a camera in front of his face
            var head = p.Body.Head.transform;
            Vector3 face = Vector3.ProjectOnPlane(p.Body.Chest.transform.forward, Vector3.up).normalized;
            var cg = new GameObject("TestCam"); var c = cg.AddComponent<Camera>();
            c.transform.position = head.position + face * 1.1f + Vector3.up * 0.05f + Vector3.Cross(Vector3.up, face) * 0.35f;
            c.transform.LookAt(head.position - Vector3.up * 0.25f);
            c.fieldOfView = 40f;
            var rt = new RenderTexture(1280, 720, 24); c.targetTexture = rt; c.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, "pilot_face.png"), tex.EncodeToPNG());
            Destroy(cg); rt.Release();
        }

        IEnumerator SmithGunsTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            foreach (var (gun, slot) in new[] { ("AR-15", "B2"), ("GLOCK 17", "GMAG"), ("AK-47", "B6") })
            {
                Game.menus.ShowGunPage(Idx(gun));
                yield return null;
                Game.menus.OpenGunsmith(Idx(gun));
                Game.menus.SmithSelect(slot);
                yield return new WaitForSeconds(1.5f);
                yield return GrabView("smithgun_" + gun.Replace(" ", ""));
                Game.menus.Toggle(MenuKind.Gunsmith);
                Game.menus.Toggle(MenuKind.Gun);
                yield return null;
            }
        }

        /// <summary>The G menu opened and closed on a few pages (for GUI errors).</summary>
        IEnumerator GMenuTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            Debug.Log("[FeatureTest] step: open G");
            Game.menus.Toggle(MenuKind.Gun);
            yield return new WaitForSeconds(1f);
            Debug.Log("[FeatureTest] step: AK-74M page");
            Game.menus.ShowGunPage(Idx("AK-74M"));
            yield return new WaitForSeconds(1f);
            Debug.Log("[FeatureTest] step: drone page");
            Game.menus.ShowGunPage(Idx("KAMIKAZE DRONE"));
            yield return new WaitForSeconds(1f);
            Debug.Log("[FeatureTest] step: close");
            Game.menus.Toggle(MenuKind.Gun);
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[FeatureTest] step: settings");
            Game.menus.Toggle(MenuKind.Settings);
            yield return new WaitForSeconds(0.5f);
            Game.menus.ShowControls(0);
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[FeatureTest] step: close settings");
            Game.menus.Toggle(MenuKind.Settings);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The gunsmith screen: slots, parts with their pictures, presets saved and loaded; the controls page.</summary>
        IEnumerator SmithScreenTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            int i = Idx("AK-74M");
            Wpn.Give(i, true);
            var L = Wpn.loadouts[i];
            L.build = GunBuilder.Stock("AK-74M"); Wpn.ApplyBuild(i);
            Game.menus.ShowGunPage(i);
            yield return new WaitForSeconds(1f);
            yield return GrabView("smith_gpage");
            Game.menus.OpenGunsmith(i);
            yield return new WaitForSeconds(1.5f);
            yield return GrabView("smith_screen");
            Game.menus.SmithSelect("B5");
            yield return new WaitForSeconds(1.5f);
            yield return GrabView("smith_stock");
            Game.menus.SmithSelect("SIGHT");
            yield return new WaitForSeconds(1.5f);
            yield return GrabView("smith_sight");
            // a build, saved, the gun reset, the build loaded back
            L.build = new GunBuild { platform = "AK", parts = new[] { "415", "railed", "b10", "dtk1", "rk3", "zhukov", "545x60" } };
            L.sight = SightKind.Scope4x; L.optic = "acog"; L.grip = GripKind.Vertical; L.foregrip = "rk2"; L.light = true; L.lightId = "klesch";
            Wpn.ApplyBuild(i);
            GunPresets.Save(GunPreset.From("AK-74M", "TEST BUILD", L));
            L.build = GunBuilder.Stock("AK-74M"); L.sight = SightKind.Iron; L.grip = GripKind.None; L.light = false; Wpn.ApplyBuild(i);
            var p = GunPresets.For("AK-74M").Find(x => x.name == "TEST BUILD");
            p.Apply(L); Wpn.ApplyBuild(i); Wpn.RefreshAttachments();
            Check(L.build.Key == "AK_415.railed.b10.dtk1.rk3.zhukov.545x60" && L.optic == "acog" && L.light, "a saved preset loads back the same build");
            Game.menus.SmithSelect("PRESETS");
            yield return new WaitForSeconds(1.5f);
            yield return GrabView("smith_presets");
            Game.menus.SmithSelect("B3");
            yield return new WaitForSeconds(1.5f);
            yield return GrabView("smith_muzzle");
            GunPresets.Delete(p);
            Game.menus.Toggle(MenuKind.Gunsmith);
            yield return null;
            Check(Game.menus.open == MenuKind.Gun, "G / ESC out of the gunsmith goes back to the gun menu");
            Game.menus.Toggle(MenuKind.Gun);
            Game.menus.ShowControls(1);
            yield return new WaitForSeconds(0.5f);
            yield return GrabView("controls_combat");
            Game.menus.Toggle(MenuKind.Settings);
        }

        /// <summary>
        /// The drones: carried, set down on the floor (P), flown from there (P again); a big shaped-charge
        /// kamikaze against a big fragmentation one; the dropper's timed grenades.
        /// </summary>
        IEnumerator DroneOptionsTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            // ---- equip, set down, fly
            FpvDrone.WarheadSize = 1f; FpvDrone.ShapedCharge = true;
            // P with no drone down: nothing happens
            FpvDrone.PressP();
            yield return null;
            Check(FpvDrone.Piloting == null, "P with no drone down does nothing");
            int kd = Idx("KAMIKAZE DRONE");
            Wpn.Give(kd, true); Wpn.Select(kd);
            yield return new WaitForSeconds(1f);
            yield return GrabView("drone_inhand");
            bool held = Wpn.Armed && WeaponController.IsDrone(Wpn.Current);
            FpvDrone.SetDownHeld();
            yield return new WaitForSeconds(1.5f);
            Check(held && !Wpn.owned[kd], "held in the hand; LEFT CLICK puts it down and it leaves your hands");
            var parked = FpvDrone.Parked;
            bool onFloor = parked != null && FpvDrone.Piloting == null && parked.transform.position.y < me.y + 0.5f;
            Line($"set down: parked {parked != null}, flying {FpvDrone.Piloting != null}, height over feet {(parked != null ? parked.transform.position.y - me.y : -1f):0.00} m");
            yield return GrabView("drone_parked");
            Check(onFloor, "P sets the carried drone down on the floor");
            // a man to hit, 14 m off, and two more round him
            Vector3 at = me + fwd * 14f;
            var target = Dummy(at, 180f); var nearL = Dummy(at + side * 2.2f, 180f); var nearR = Dummy(at - side * 2.2f, 180f);
            yield return new WaitForSeconds(1f);
            int w0 = Wounds(target), l0 = Wounds(nearL) + Wounds(nearR);
            FpvDrone.PressP();
            yield return null;
            var d = FpvDrone.Piloting;
            Check(d != null && d == parked, "P again: you're flying it, from the floor");
            float t0 = Time.time;
            while (d != null && !d.Ended && Time.time - t0 < 12f)
            {
                Vector3 to = target.Chest.transform.position - d.transform.position;
                d.autopilot = to.normalized * 8f;
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            int shapedHit = Wounds(target) - w0, shapedSide = Wounds(nearL) + Wounds(nearR) - l0;
            Line($"big shaped charge: target {shapedHit} wounds, the men 2.2 m either side {shapedSide}");
            // ---- the same with a big fragmentation warhead
            ClearEarls();
            target = Dummy(at, 180f); nearL = Dummy(at + side * 2.2f, 180f); nearR = Dummy(at - side * 2.2f, 180f);
            yield return new WaitForSeconds(1f);
            w0 = Wounds(target); l0 = Wounds(nearL) + Wounds(nearR);
            FpvDrone.ShapedCharge = false;
            d = FpvDrone.Launch(FpvDrone.Kind.Kamikaze);
            t0 = Time.time;
            while (d != null && !d.Ended && Time.time - t0 < 12f)
            {
                Vector3 to = target.Chest.transform.position - d.transform.position;
                d.autopilot = to.normalized * 8f;
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            int fragHit = Wounds(target) - w0, fragSide = Wounds(nearL) + Wounds(nearR) - l0;
            Line($"big fragmentation: target {fragHit} wounds, the men either side {fragSide}");
            Check(fragSide > shapedSide, "fragmentation spreads out; the shaped charge stays on what it hits");
            // ---- speed: the biggest warhead is slow
            FpvDrone.WarheadSize = 0f;
            var light = FpvDrone.Launch(FpvDrone.Kind.Kamikaze);
            for (float t = 0f; t < 2.5f; t += Time.deltaTime) { light.autopilot = null; yield return null; }
            light.End("TEST OVER", true);
            yield return new WaitForSeconds(0.5f);
            // ---- the dropper's timed grenades
            ClearEarls();
            FpvDrone.TimedBombs = true; FpvDrone.WarheadSize = 0f;
            var b = FpvDrone.Launch(FpvDrone.Kind.Bomber);
            Vector3 over = me + fwd * 10f + Vector3.up * 12f;
            t0 = Time.time;
            while (Time.time - t0 < 4f) { b.autopilot = Vector3.ClampMagnitude((over - b.transform.position) * 2f, 6f); yield return null; }
            int g0 = Grenade.All.Count;
            b.Drop();
            yield return null;
            var g = Grenade.All.Count > g0 ? Grenade.All[Grenade.All.Count - 1] : null;
            Line($"timed drop: grenade {(g != null ? $"impact {g.impact}, fuse {g.fuse:0.0} s, power {g.power:0.0}, {g.shrapnel} fragments" : "none")}");
            Check(g != null && !g.impact && g.power > 1.5f, "timed grenades: a fuse and a bigger charge");
            yield return new WaitForSeconds(4f);
            b.End("TEST OVER");
            FpvDrone.TimedBombs = false;
        }

        /// <summary>The G menu's gunsmith with builds on the showcase.</summary>
        IEnumerator SmithMenuTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            int i = Idx("AK-74M"), ar = Idx("AR-15");
            Wpn.Give(i, true); Wpn.Give(ar, true);
            var shots = new (int gun, string plat, string[] p, string tag, int slot)[]
            {
                (i, "AK", new[] { "206", "hinged", "plum", "booster", "plum", "skel", "545p" }, "krinkov", 5),
                (i, "AK", new[] { "415", "railed", "zhukov", "pbs4", "moe", "zhukov", "545x60" }, "zhukov", 3),
                (i, "AK", new[] { "590", "railed", "b10", "dtk1", "rk3", "ak12", "545x45" }, "rpk", 2),
                (ar, "AR", new[] { "267", "raptor", "ras", "socom", "hogue", "moe", "d60" }, "arsbr", 6),
                (ar, "AR", new[] { "508", "milspec", "mlok13", "pa", "k2", "prs", "pmag40" }, "ardmr", 5),
                (ar, "AR", new[] { "368", "milspec", "m4", "sf3p", "moe", "m4", "sf60" }, "arm4", 2),
            };
            foreach (var (g, plat, p, tag, slot) in shots)
            {
                Wpn.loadouts[g].build = new GunBuild { platform = plat, parts = p };
                Wpn.loadouts[g].sight = tag == "zhukov" || tag == "ardmr" ? SightKind.Scope4x : tag == "rpk" ? SightKind.RedDot : SightKind.Iron;
                Wpn.loadouts[g].optic = tag == "zhukov" ? "pso1" : tag == "ardmr" ? "acog" : "kobra";
                Wpn.ApplyBuild(g);
                Game.menus.ShowGunPage(g, slot);
                yield return new WaitForSeconds(2.5f);
                yield return GrabView("smith_" + tag);
            }
            Game.menus.Toggle(MenuKind.Gun);
        }

        /// <summary>Every optic on an AK-74M with a railed cover, aimed through; with each light and laser on.</summary>
        IEnumerator OpticsTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            int i = Idx("AK-74M");
            Wpn.Give(i, true); Wpn.Select(i);
            yield return new WaitForSeconds(1f);
            var L = Wpn.loadouts[i];
            L.build = GunBuilder.Stock("AK-74M"); L.build[PartSlot.Cover] = "railed"; L.build[PartSlot.Handguard] = "zhukov";
            Wpn.ApplyBuild(i);
            int k = 0;
            foreach (var a in Attachments.Of(0))
            {
                L.sight = a.sight; L.optic = a.id;
                var li = Attachments.Of(2); var la = Attachments.Of(3); var fg = Attachments.Of(1);
                L.light = true; L.lightId = li[k % li.Count].id; L.laser = true; L.laserId = la[k % la.Count].id;
                L.grip = fg[k % fg.Count].grip; L.foregrip = fg[k % fg.Count].id;
                Wpn.RefreshAttachments();
                WeaponController.ForceAim = false;
                yield return new WaitForSeconds(0.5f);
                yield return GrabView("optic_" + a.id + "_hip");
                WeaponController.ForceAim = true;
                yield return new WaitForSeconds(1.2f);
                yield return GrabView("optic_" + a.id);
                Line($"{a.name}: light {L.lightId}, laser {L.laserId}, grip {L.foregrip}");
                k++;
            }
            WeaponController.ForceAim = false;
        }

        /// <summary>The gunsmith: AKs rebuilt from parts, in your hands, their numbers following the parts; Earls with theirs.</summary>
        IEnumerator GunsmithTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            int i = Idx("AK-74M");
            Wpn.Give(i, true); Wpn.Select(i);
            yield return new WaitForSeconds(1f);
            var L = Wpn.loadouts[i];
            var builds = new (string tag, string[] p)[]
            {
                ("stock", null),
                ("krinkov", new[] { "206", "hinged", "plum", "booster", "plum", "skel", "545p" }),
                ("zhukov_pbs", new[] { "415", "railed", "zhukov", "pbs4", "moe", "zhukov", "545x60" }),
                ("rpk_b10", new[] { "590", "railed", "b10", "dtk1", "rk3", "ak12", "545x45" }),
                ("nostock", new[] { "314", "ribbed", "poly", "thread", "poly", "none", "545b" }),
            };
            foreach (var (tag, p) in builds)
            {
                L.build = p == null ? GunBuilder.Stock("AK-74M") : new GunBuild { parts = p };
                Wpn.ApplyBuild(i);
                yield return new WaitForSeconds(0.6f);
                var d = Wpn.defs[i];
                Line($"{tag}: model {GunBuilder.Model("AK-74M", L.build)}  mag {Wpn.MagSize(i)}  recoil {d.recoil:0.00}  spread {d.spread:0.00}  v {d.muzzleVelocity:0}  loud {d.loudness:0}  reload {d.reloadTime:0.00}  suppressed {L.suppressor}");
                yield return GrabView("gunsmith_" + tag);
            }
            Check(Wpn.MagSize(i) == 30 && Wpn.defs[i].recoil > 1.5f, "no stock, bare muzzle: much more recoil");
            // the AR-15 too
            int ar = Idx("AR-15");
            Wpn.Give(ar, true); Wpn.Select(ar);
            yield return new WaitForSeconds(1f);
            var LA = Wpn.loadouts[ar];
            foreach (var (tag, p) in new (string, string[])[] { ("ar_sbr", new[] { "267", "raptor", "ras", "socom", "hogue", "moe", "d60" }), ("ar_m4", new[] { "368", "milspec", "m4", "a2", "a2", "m4", "stanag20" }), ("ar_dmr", new[] { "508", "milspec", "mlok13", "pa", "k2", "prs", "pmag40" }) })
            {
                LA.build = new GunBuild { platform = "AR", parts = p };
                GunBuilder.Fit("AR-15", LA.build, PartSlot.Barrel, p[0]);
                Wpn.ApplyBuild(ar);
                yield return new WaitForSeconds(0.6f);
                var d = Wpn.defs[ar];
                Line($"{tag}: {string.Join(".", LA.build.parts)}  mag {Wpn.MagSize(ar)}  recoil {d.recoil:0.00}  spread {d.spread:0.00}  v {d.muzzleVelocity:0}  loud {d.loudness:0}");
                yield return GrabView("gunsmith_" + tag);
            }
            Wpn.Select(i);
            // Earls in a battle: their own builds
            Nations.Active = true; Nations.NewBattle();
            int built = 0;
            Vector3 me = Game.SpawnPoint; Vector3 fwd = Vector3.ProjectOnPlane(Game.player.cam.transform.forward, Vector3.up).normalized; Vector3 side = Vector3.Cross(Vector3.up, fwd);
            for (int k = 0; k < 5; k++)
            {
                var h = Game.SpawnCarl(me + fwd * 2.6f + side * (k - 2) * 0.8f, Quaternion.LookRotation(side).eulerAngles.y);
                if (h == null) continue;
                h.brain.aiEnabled = false;
                var g = EarlGunner.Arm(h, k % 2 == 0 ? EarlWeapon.AK74M : EarlWeapon.AK47, GunKit.Random(true), Teams.Enemy);
                if (g != null && g.Body != null) { foreach (var mf in g.Body.GetComponentsInChildren<MeshFilter>()) if (mf.name.StartsWith("Build_")) { built++; break; } }
            }
            Wpn.Select(Idx("PHYSICS GUN"));
            yield return new WaitForSeconds(1.5f);
            yield return GrabView("gunsmith_earls");
            Line($"Earls with a rebuilt rifle: {built} of 5");
            Nations.Active = false;
        }

        /// <summary>
        /// An Earl right behind the edge of a wall, his man off to the side of it: from where he stands the wall is in
        /// the way, a lean out past the edge clears it. He should lean out and shoot, not wait for a clear line.
        /// </summary>
        IEnumerator PeekTest()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 o = Game.SpawnPoint + new Vector3(25f, 0f, 0f);
            if (Physics.Raycast(o + Vector3.up * 10f, Vector3.down, out var gh, 20f, ~0, QueryTriggerInteraction.Ignore)) o = gh.point;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = o + new Vector3(-1.1f, 1.2f, 0.75f); wall.transform.localScale = new Vector3(2.5f, 2.4f, 0.3f);
            int total = 0, fired = 0;
            foreach (float side in new[] { 2.5f, 4f })
            {
                ClearEarls();
                var tgt = Gunner(o + new Vector3(side, 0f, 20f), Teams.Friendly, 180f);
                tgt.Body.brain.aiEnabled = false;
                var g = Gunner(o, Teams.Enemy, 0f);
                g.defend = true; g.holdPoint = o;
                yield return new WaitForSeconds(1f);
                // the wall's right edge just left of his head, a stride in front of him
                Vector3 hd = g.Body.Head.transform.position;
                wall.transform.position = new Vector3(hd.x + 0.2f - 1.25f, o.y + 1.2f, hd.z + 0.7f);
                Physics.SyncTransforms();
                // (a firefight: he knows where his man is - he's been shot at from there)
                g.Learn(tgt.Body.Center, 1f, false);
                bool blocked = !EarlGunner.Clear(g.Body.Head.transform.position, tgt.Body.Chest.transform.position, g.Body);
                int s0 = EarlGunner.ShotsFired; float maxLean = 0f, t0 = Time.time, first = -1f;
                while (Time.time - t0 < 12f)
                {
                    if (g.Body != null) maxLean = Mathf.Max(maxLean, Mathf.Abs(g.Body.ragdoll.lean));
                    if (first < 0f && EarlGunner.ShotsFired > s0) first = Time.time - t0;
                    yield return null;
                }
                int n = EarlGunner.ShotsFired - s0;
                Line($"target {side} m off the wall's line: blocked from his standing head {blocked}; {n} rounds in 12 s, first after {first:0.0} s, max lean {maxLean:0.00}");
                Line($"  target dead {tgt.Body.Dead}, conscious {tgt.Body.physio.consciousness:0.00}; shooter status '{g.Status}', moved {Vector3.Distance(g.Body.Pelvis.transform.position, o + Vector3.up):0.0} m");
                total++; if (n > 0) fired++;
            }
            Destroy(wall);
            Check(fired == total, "half behind a corner, he leans out and shoots");
        }

        /// <summary>The houses map as HOOD (Crips v Bloods) and WAR (attack and defend, on a shelled street).</summary>
        IEnumerator HouseModesTest()
        {
            bool npcs = GameSettings.SpawnNpcs; int mode = GameSettings.HouseMode; bool att = GameSettings.HouseAttack;
            GameSettings.SpawnNpcs = true;
            string Count()
            {
                int f = 0, e = 0, fs = 0, es = 0;
                var statuses = new Dictionary<string, int>();
                foreach (var g in EarlGunner.All)
                {
                    if (g == null || g.Body == null) continue;
                    bool fr = g.team == Teams.Friendly;
                    if (g.Fighting) { if (fr) f++; else e++; } else { if (fr) fs++; else es++; }
                    if (g.Fighting) { statuses.TryGetValue(g.Status, out int n); statuses[g.Status] = n + 1; }
                }
                var st = new List<string>(); foreach (var kv in statuses) st.Add(kv.Key + " x" + kv.Value);
                return $"friendly {f} up / {fs} down, enemy {e} up / {es} down  [{string.Join(", ", st)}]";
            }
            int[] modes = { HouseBattle.Hood, HouseBattle.War, HouseBattle.War };
            bool[] attack = { true, true, false };
            string[] tag = { "hood", "war_attack", "war_defend" };
            for (int k = 0; k < 3; k++)
            {
                GameSettings.HouseMode = modes[k]; GameSettings.HouseAttack = attack[k];
                Game.LoadMap(2);
                yield return new WaitForSeconds(2f);
                int craters = 0, holes = 0, glass = Object.FindObjectsByType<WindowGlass>(FindObjectsSortMode.None).Length;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) { if (t.name == "Crater") craters++; if (t.name == "Rubble") holes++; }
                Line($"{tag[k]}: mode {HouseBattle.Mode}, wrecked {Houses.Wrecked}: {craters} craters, {holes} rubble, {glass} windows; {Count()}");
                yield return GrabView("house_" + tag[k] + "_start");
                yield return new WaitForSeconds(k == 2 ? 20f : 25f);
                Line($"  after {(k == 2 ? 20 : 25)} s: {Count()}");
                yield return GrabView("house_" + tag[k] + "_later");
                Check(EarlGunner.All.Count > 6, tag[k] + ": both sides spawned");
            }
            Check(HouseBattle.Mode == HouseBattle.War && Houses.Wrecked, "war street is wrecked");
            GameSettings.SpawnNpcs = npcs; GameSettings.HouseMode = mode; GameSettings.HouseAttack = att;
        }

        IEnumerator CamoTest()
        {
            int wasT = GameSettings.TimeOfDay; GameSettings.TimeOfDay = 0;
            Game.LoadMap(0);
            GameSettings.TimeOfDay = wasT;
            yield return new WaitForSeconds(1f);
            ClearEarls();
            Nations.Active = true; Nations.NewBattle();
            Vector3 me = PlayerAvatar.Body.Pelvis.transform.position - Vector3.up * 0.95f;
            Vector3 f = Vector3.ProjectOnPlane(Game.cam.transform.forward, Vector3.up).normalized, r = Vector3.Cross(Vector3.up, f);
            float yaw = Quaternion.LookRotation(-f).eulerAngles.y;
            foreach (int n in new[] { Nations.Ukraine, Nations.Russia })
            {
                var h = Game.SpawnCarl(me + f * 2.4f + r * (n == Nations.Ukraine ? -0.5f : 0.5f), yaw);
                ArmourSystem.Equip(h, TrenchSquad.RandomKit(n));
                h.SetUniform(Nations.Uniform(n));
                Teams.Armband(h, n == GameSettings.Side ? Teams.Friendly : Teams.Enemy);
                if (h.brain != null) h.brain.aiEnabled = false;
            }
            yield return new WaitForSeconds(25f);
            Game.player.LookAt(me + f * 2.4f + Vector3.up * 1.0f);
            yield return Hold(0.5f);
            yield return GrabView("camo_both");
            Nations.Active = false;
        }

                IEnumerator ArmiesTest()
        {
            int was = GameSettings.Side, wasT = GameSettings.TimeOfDay;
            GameSettings.TimeOfDay = 0;
            foreach (int side in new[] { Nations.Ukraine, Nations.Russia })
            {
                GameSettings.Side = side;
                Game.LoadMap(3);
                Cursor.lockState = CursorLockMode.Locked;
                yield return new WaitForSeconds(3f);
                var count = new Dictionary<string, int>[] { new Dictionary<string, int>(), new Dictionary<string, int>() };
                foreach (var g in EarlGunner.All)
                {
                    if (g == null) continue;
                    var c = count[g.team == Teams.Friendly ? 0 : 1];
                    string n = g.GunName;
                    c[n] = c.TryGetValue(n, out int v) ? v + 1 : 1;
                }
                string Fmt(Dictionary<string, int> c) { var sb = new StringBuilder(); foreach (var kv in c) sb.Append($"{kv.Key} x{kv.Value}  "); return sb.ToString(); }
                Line($"you: {Nations.Name(side)}  ·  you hold {Wpn.Current.name}");
                Line($"   your side ({Nations.Name(Nations.Of(Teams.Friendly))}): {Fmt(count[0])}");
                Line($"   the enemy ({Nations.Name(Nations.Of(Teams.Enemy))}): {Fmt(count[1])}");
                Check(Wpn.Current.name == Nations.IssueRifle(side), $"{Nations.Name(side)}: you start with the {Nations.IssueRifle(side)}");
                // a friendly up close
                EarlGunner near = null; float bd = 99f;
                foreach (var g in EarlGunner.All) if (g != null && g.team == Teams.Friendly) { float d = Vector3.Distance(g.Body.Center, PlayerAvatar.Body.Center); if (d < bd) { bd = d; near = g; } }
                if (near != null)
                {
                    Game.player.Teleport(near.Body.Pelvis.transform.position - Vector3.up * 0.95f + near.Body.Chest.transform.forward * 2.2f, 0f);
                    yield return Hold(0.6f);
                    Game.player.LookAt(near.Body.Chest.transform.position);
                    yield return Hold(0.4f);
                    Game.player.LookAt(near.Body.Chest.transform.position);
                    yield return GrabView("army_" + (side == 0 ? "ua" : "ru"));
                }
            }
            GameSettings.Side = was; GameSettings.TimeOfDay = wasT;
        }

        IEnumerator NewGunsTest()
        {
            int wasT = GameSettings.TimeOfDay; GameSettings.TimeOfDay = 0;
            Game.LoadMap(0);
            GameSettings.TimeOfDay = wasT;
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            GameSettings.InfiniteAmmo = false;
            foreach (var n in new[] { "AK-47", "AK-74", "AK-74M", "AK-105", "AKS-74U", "AK-12", "RPK-74", "PKM", "M249" })
            {
                int i = Idx(n);
                if (i < 0) { Check(false, n + " is in the game"); continue; }
                Wpn.Give(i, true);
                yield return Hold(1.2f);
                string tag = n.Replace("-", "").Replace(" ", "").ToLower();
                yield return GrabView("gun_" + tag + "_hip");
                rmb = true; yield return Hold(1.2f);
                yield return GrabView("gun_" + tag + "_ads");
                string before = Wpn.AmmoText;
                lmb = true; yield return Hold(0.5f); lmb = false; yield return Hold(0.3f);
                rmb = false; yield return Hold(0.3f);
                var am = AmmoTable.For(n);
                Line($"{n}: {before} -> {Wpn.AmmoText}, ammo {(am != null ? am[0].name : "none")}, modes {string.Join("/", Wpn.ModesFor(i))}");
                Check(Wpn.AmmoText != before && am != null, $"{n}: fires, its own ammo");
            }
            GameSettings.InfiniteAmmo = true;
        }

        IEnumerator NightTest()
        {
            int was = GameSettings.TimeOfDay;
            GameSettings.TimeOfDay = 2;
            Game.LoadMap(3);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(2f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            Vector3 eye = Game.cam.transform.position;
            // looking down the trench: the dark, then with the light on
            Wpn.Give(Idx("AK-47"), true);
            yield return Hold(1f);
            WeaponController.PlayerLightOn = false;
            yield return GrabView("night_dark");
            float dark = ScreenLuma();
            keysDown.Add(UnityEngine.InputSystem.Key.L); yield return Hold(0.1f); keysDown.Clear(); yield return Hold(0.5f);
            yield return GrabView("night_light");
            float lit = ScreenLuma();
            Line($"night, average brightness on screen: light off {dark:0.000}, light on {lit:0.000}");
            Check(dark < 0.06f, "night is dark (you need a light to see)");
            Check(lit > dark * 1.3f, "the flashlight lights things up");
            keysDown.Add(UnityEngine.InputSystem.Key.L); yield return Hold(0.1f); keysDown.Clear(); yield return Hold(0.5f);
            Check(!WeaponController.PlayerLightOn, "light back off");
            // out on the open field: you, and an enemy in plain line of sight 15-30 m off, in the dark
            Vector3 spot = me; float best = 0f;
            for (int a = 0; a < TrenchNav.Nodes.Count && best == 0f; a++)
            {
                if (!TrenchNav.Field[a]) continue;
                var pa = TrenchNav.Nodes[a];
                for (int i = 0; i < TrenchNav.Nodes.Count; i++)
                {
                    var n = TrenchNav.Nodes[i]; float d = Vector3.Distance(n, pa);
                    if (!TrenchNav.Field[i] || d < 15f || d > 30f) continue;
                    if (!EarlGunner.Clear(n + Vector3.up * 1.5f, pa + Vector3.up * 1f, null)) continue;
                    best = d; spot = n; me = pa; break;
                }
            }
            Game.player.Teleport(me + Vector3.up * 0.05f, 0f);
            yield return Hold(2f);
            Line($"enemy placed {best:0} m off, clear line of sight");
            var e = Gunner(spot, Teams.Enemy, Quaternion.LookRotation(Vector3.ProjectOnPlane(me - spot, Vector3.up)).eulerAngles.y);
            e.Body.ragdoll.faceDir = Vector3.ProjectOnPlane(me - spot, Vector3.up).normalized;
            int torch0 = EarlGunner.TorchUses, saw = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 4f) { if (e.SeesEnemy) saw++; yield return null; }
            Line($"light off, standing still: he saw you in {saw} frames, his torch on {EarlGunner.TorchUses - torch0} frames, suspects you {e.SuspectConf:0.00}");
            Check(saw == 0, "in the dark he can't see you without a light");
            Check(EarlGunner.TorchUses - torch0 == 0, "an Earl with nothing to hunt keeps his torch off");
            // your light on: he sees the light and knows roughly where you are
            int g0 = EarlGunner.LightGuesses;
            keysDown.Add(UnityEngine.InputSystem.Key.L); yield return Hold(0.1f); keysDown.Clear();
            yield return Hold(1.5f);
            float off = Vector3.Distance(Vector3.ProjectOnPlane(e.suspect - PlayerAvatar.Body.Center, Vector3.up), Vector3.zero);
            Line($"light on: guesses off it {EarlGunner.LightGuesses - g0}, sees you {e.SeesEnemy}, suspects you {e.SuspectConf:0.00} at {off:0.0} m off");
            Check(e.SeesEnemy || (EarlGunner.LightGuesses > g0 && off < 6f), "your light gives you away");
            keysDown.Add(UnityEngine.InputSystem.Key.L); yield return Hold(0.1f); keysDown.Clear(); yield return Hold(0.3f);
            // guesses by ear: near enough exact close up, a general area far off
            float near = 0f, far = 0f;
            for (int i = 0; i < 400; i++) { near += Intel.Guess(5f, 0.03f, 0.004f).magnitude; far += Intel.Guess(50f, 0.03f, 0.004f).magnitude; }
            Line($"a shot heard: guessed {near / 400f:0.00} m out at 5 m, {far / 400f:0.0} m out at 50 m");
            Check(near / 400f < 0.3f && far / 400f > 4f, "close shots placed exactly, far ones only roughly");
            Check(Sfx.Clip("gear_rattle") != null, "a vest rattles when you move");
            GameSettings.TimeOfDay = was;
            Teams.PlayerOutOfIt = false;
        }

        IEnumerator BlindTest()
        {
            Game.LoadMap(0);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            GameSettings.InfiniteAmmo = true;
            int ak = Idx("AK-47");
            Wpn.Give(ak, true); Wpn.loadouts[ak].fireMode = 0;
            yield return Hold(1f);
            float[] res = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                bool blind = pass == 1;
                keysDown.Clear();
                if (blind) { keysDown.Add(UnityEngine.InputSystem.Key.LeftAlt); keysDown.Add(UnityEngine.InputSystem.Key.D); }
                yield return Hold(1.2f);
                Vector3 d0 = Wpn.AimDirection; float worst = 0f;
                lmb = true;
                yield return Hold(1.2f, () => { worst = Mathf.Max(worst, Vector3.Angle(d0, Wpn.AimDirection)); });
                lmb = false; keysDown.Clear(); yield return Hold(1f);
                res[pass] = worst;
            }
            Line($"a 1.2 s burst: the barrel wandered up to {res[0]:0.0} deg shouldered, {res[1]:0.0} deg held out round a corner");
            Check(res[1] > res[0] * 1.6f, "round a corner it kicks all over the place");
        }

        float ScreenLuma()
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var px = tex.GetPixels32();
            double sum = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 37) { var c = px[i]; sum += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255.0; n++; }
            Destroy(tex);
            return (float)(sum / Mathf.Max(1, n));
        }

        IEnumerator ScopesTest()
        {
            Game.LoadMap(0);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            foreach (var n in new[] { ".50 CAL", "AWP" })
            {
                Wpn.Give(Idx(n), true);
                yield return Hold(1.2f);
                yield return GrabView("scope_hip_" + (n == "AWP" ? "awp" : "50"));
                rmb = true; yield return Hold(1.5f);
                Line($"{n} aiming: aimT {Wpn.aimT:0.00}, fov {Game.cam.fieldOfView:0.0}, scope view {(FindFirstObjectByType<ScopeView>() != null ? "on" : "none")}");
                yield return GrabView("scope_ads_" + (n == "AWP" ? "awp" : "50"));
                rmb = false; yield return Hold(0.8f);
            }
        }

        IEnumerator Tap(UnityEngine.InputSystem.Key k)
        {
            keysDown.Add(k); yield return Hold(0.08f); keysDown.Clear(); yield return Hold(0.42f);
        }

        IEnumerator HeatTest()
        {
            Game.LoadMap(0);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            var bh = BarrelHeat.I;
            Check(bh != null, "barrel heat is there");
            if (bh == null) yield break;
            int ak = Idx("AK-47");
            Line($"jam chance per round: cool {BarrelHeat.JamChance(0.1f) * 100f:0.00}%, half {BarrelHeat.JamChance(0.5f) * 100f:0.00}%, 80% {BarrelHeat.JamChance(0.8f) * 100f:0.0}%, at the limit {BarrelHeat.JamChance(1f) * 100f:0.0}%");
            Check(BarrelHeat.JamChance(0.1f) > 0.001f && BarrelHeat.JamChance(0.1f) < 0.003f && BarrelHeat.JamChance(1f) > 20f * BarrelHeat.JamChance(0.1f), "jams now and then cool, far more often at the limit");
            // full auto, trigger held, for a good while (clearing any jam that comes)
            GameSettings.InfiniteAmmo = true;
            Wpn.Give(ak, true); Wpn.loadouts[ak].fireMode = 0;
            yield return Hold(0.6f);
            bh.Jams = 0; bh.DeadClicks = 0;
            float t0 = Time.time, h2 = -1f; int clears = 0;
            lmb = true;
            while (Time.time - t0 < 16f)
            {
                if (h2 < 0f && Time.time - t0 > 2f) h2 = bh.Heat(ak);
                if (bh.Jammed(ak))
                {
                    if (clears == 0) { yield return Hold(0.4f); yield return GrabView("heat_jammed"); }
                    Line($"  jammed at {Time.time - t0:0.0} s, heat {bh.Heat(ak) * 100f:0}%: {bh.JamName(ak)}");
                    lmb = false;
                    while (bh.Jammed(ak)) { yield return Tap(UnityEngine.InputSystem.Key.J); clears++; if (clears > 40) break; }
                    lmb = true;
                }
                yield return Hold(0.05f);
            }
            float hot = bh.Heat(ak), spread = bh.SpreadMul(ak);
            bool glow = bh.Glowing;
            yield return GrabView("heat_hot");
            lmb = false;
            Line($"held 16 s: heat {h2 * 100f:0}% at 2 s, {hot * 100f:0}% at 16 s; spread x{spread:0.00}; glowing {glow}; jams {bh.Jams}, J presses {clears}, dead clicks {bh.DeadClicks}");
            Check(h2 > 0.2f && hot > h2 && hot > 0.6f, "the barrel heats up as you keep firing");
            Check(glow, "a hot barrel glows");
            Check(spread > 1.4f, "a hot barrel throws rounds wider");
            Check(bh.Jams >= 1, "keeping it at the limit jams it");
            // let it cool
            // (slowly: a cooked barrel takes a good while to come down, so spraying has a real cost)
            yield return Hold(8f);
            float warm = bh.Heat(ak);
            yield return Hold(10f);
            float cool = bh.Heat(ak);
            Line($"off the trigger: {hot * 100f:0}% -> {warm * 100f:0}% at 8 s -> {cool * 100f:0}% at 18 s, glowing {bh.Glowing}");
            Check(warm > hot * 0.45f, "it cools slowly");
            Check(cool < hot * 0.8f && cool > hot * 0.35f, "it does cool when you stop - over a minute or two, not seconds");
            // single aimed shots barely warm it
            bh.SetHeat(ak, 0f);
            Wpn.loadouts[ak].fireMode = 1; yield return Hold(0.3f);
            for (int i = 0; i < 10; i++) { lmb = true; yield return Hold(0.05f); lmb = false; yield return Hold(0.6f); }
            Line($"ten aimed shots, 0.65 s apart: heat {bh.Heat(ak) * 100f:0}%");
            Check(bh.Heat(ak) < 0.25f, "aimed shots don't cook the barrel");
            Wpn.loadouts[ak].fireMode = 0;
            // a jammed gun clicks and doesn't fire; J clears it, a step at a time
            GameSettings.InfiniteAmmo = false;
            Wpn.Give(ak, true); yield return Hold(0.6f);
            bh.ForceJam(ak, 3);   // double feed: three steps
            string before = Wpn.AmmoText; int clicks = bh.DeadClicks;
            lmb = true; yield return Hold(1f); lmb = false; yield return Hold(0.2f);
            Line($"jammed, trigger held 1 s: ammo {before} -> {Wpn.AmmoText}, dead clicks {bh.DeadClicks - clicks}");
            Check(Wpn.AmmoText == before && bh.DeadClicks > clicks, "jammed: the trigger only clicks");
            yield return Tap(UnityEngine.InputSystem.Key.J);
            bool still1 = bh.Jammed(ak);
            yield return Tap(UnityEngine.InputSystem.Key.J);
            yield return Tap(UnityEngine.InputSystem.Key.J);
            Line($"double feed: still jammed after one J {still1}, after three {bh.Jammed(ak)}; ammo {Wpn.AmmoText}");
            Check(still1 && !bh.Jammed(ak), "J clears it (a double feed takes three)");
            string b2 = Wpn.AmmoText;
            lmb = true; yield return Hold(0.3f); lmb = false; yield return Hold(0.2f);
            Check(Wpn.AmmoText != b2, "cleared: it fires again");
            // the laser moved to O
            Wpn.Give(ak, true); yield return Hold(0.3f);
            GameSettings.InfiniteAmmo = true;
        }

        IEnumerator SpacingTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Teams.PlayerOutOfIt = true;
            Vector3 me = Game.SpawnPoint;
            for (int round = 0; round < 2; round++)
            {
                int n = TrenchSquad.SpawnFriendlies(me, 5);
                yield return new WaitForSeconds(1f);
                var fr = new List<EarlGunner>();
                foreach (var g in EarlGunner.All) if (g != null && g.team == Teams.Friendly) fr.Add(g);
                // somewhere along the trench well off
                Vector3 far = me; float best = 0f;
                for (int i = 0; i < TrenchNav.Nodes.Count; i++) { var p = TrenchNav.Nodes[i]; float d = Vector3.Distance(p, me); if (p.y < 0.5f && !TrenchNav.Field[i] && d > 12f && d < 18f && d > best) { best = d; far = p; } }
                // round two: one stands in the way, holding an angle; the rest have to get by him
                EarlGunner holder = null;
                if (round == 1 && fr.Count > 0)
                {
                    holder = fr[0];
                    Vector3 mid = me; float bm = 99f;
                    foreach (var p in TrenchNav.Nodes) { float d = Mathf.Abs(Vector3.Distance(p, me) - best * 0.5f) + Mathf.Abs(Vector3.Distance(p, far) - best * 0.5f); if (p.y < 0.5f && d < bm) { bm = d; mid = p; } }
                    holder.inSquad = true; holder.order = SquadOrder.Move; holder.orderPos = mid;
                    foreach (var g in fr) if (g != holder) { g.inSquad = true; g.order = SquadOrder.Hold; g.orderPos = g.Body.Pelvis.transform.position - Vector3.up * 0.95f; }
                    float tw = Time.time;
                    while (Time.time - tw < 10f && Vector3.Distance(holder.Body.Pelvis.transform.position - Vector3.up * 0.95f, mid) > 1.2f) yield return null;
                    yield return new WaitForSeconds(1f);
                    Line($"  holder at the halfway point ({Vector3.Distance(holder.Body.Pelvis.transform.position - Vector3.up * 0.95f, mid):0.0} m off), {holder.Status}");
                }
                int k = 0;
                foreach (var g in fr)
                {
                    if (g == holder) continue;
                    g.inSquad = true; g.order = SquadOrder.Move;
                    g.orderPos = far + new Vector3((k % 3 - 1) * 0.6f, 0f, (k / 3) * 0.6f); k++;
                }
                EarlGunner.PassedMoving = EarlGunner.PassedStill = 0;
                var fell = new HashSet<EarlGunner>();
                float t0 = Time.time, minGap = 99f, tr = 2f;
                while (Time.time - t0 < 40f)
                {
                    foreach (var g in fr)
                    {
                        if (g == null || g.Body == null) continue;
                        if (g.Body.ragdoll.IsDown) fell.Add(g);
                        foreach (var o in fr) if (o != null && o != g && o.Body != null) minGap = Mathf.Min(minGap, Vector3.Distance(g.Body.Pelvis.transform.position, o.Body.Pelvis.transform.position));
                    }
                    if (Time.time - t0 > tr) { tr += 10f; foreach (var g in fr) if (g != null && g != holder && g.Body != null) Line($"     {Time.time - t0:0}s #{g.Body.number} at {g.Body.Pelvis.transform.position - Vector3.up * 0.95f} v {g.Body.Pelvis.rb.linearVelocity.magnitude:0.0} {g.Status} {g.SpacingDebug} down {g.Body.ragdoll.IsDown}"); }
                    if (Time.time - t0 > 6f && Time.time - t0 < 6.1f) { Game.player.Teleport(far + Vector3.up * 2.5f, 0f); Game.player.LookAt(me); yield return GrabView("spacing_" + round); }
                    yield return null;
                }
                int there = 0, movers = 0;
                foreach (var g in fr) if (g != null && g != holder) { movers++; if (Vector3.Distance(g.Body.Pelvis.transform.position - Vector3.up * 0.95f, far) < 3f) there++; }
                foreach (var g in fr) if (g != null && g != holder) Line($"   #{g.Body.number}: {Vector3.Distance(g.Body.Pelvis.transform.position - Vector3.up * 0.95f, far):0.0} m off, {g.Status}, {g.NavDebug}, queuing {g.queuing}");
                Line($"{(round == 0 ? "all walking to the same spot" : "past a man holding an angle")}: {n} spawned, {there}/{movers} got there ({best:0} m), fell {fell.Count}, passed men on the move {EarlGunner.PassedMoving}, passed men standing {EarlGunner.PassedStill}, closest {minGap:0.00} m");
                Check(there >= movers - 1, "they get there");
                Check(EarlGunner.PassedMoving <= 1, "nobody pushes past a man who's moving");
                Check(fell.Count <= 1, "no pile-ups (nobody knocked down)");
                if (round == 1) Check(EarlGunner.PassedStill >= 1 || there == movers, "they get past a man standing there");
                ClearEarls();
                Game.player.Teleport(me, 0f);
                yield return new WaitForSeconds(0.5f);
            }
            Teams.PlayerOutOfIt = false;
        }

        IEnumerator StuckTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            foreach (var w in new[] { EarlWeapon.Pistol, EarlWeapon.AK47 })
            {
                var h = Game.SpawnCarl(new Vector3(9.0f, 0.05f, -20.1f), 250f);
                h.brain.aiEnabled = true;
                var g = EarlGunner.Arm(h, w, null, Teams.Friendly);
                g.inSquad = true; g.order = SquadOrder.Move; g.orderPos = new Vector3(3.6f, 0f, -21.5f);
                Vector3 p0 = h.Pelvis.transform.position;
                Game.player.Teleport(new Vector3(5f, 0.05f, -21.2f), 70f);
                yield return new WaitForSeconds(1.5f);
                Game.player.LookAt(h.Center);
                yield return GrabView("stuck_" + w);
                yield return new WaitForSeconds(25f);
                Line($"{w}: spawned {p0}, now {h.Pelvis.transform.position}, {g.NavDebug}, order {g.order}");
                Destroy(h.gameObject); HumanBody.All.Remove(h);
                yield return null;
            }
        }

        IEnumerator SquadTest()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(1.5f);
            ClearEarls();
            Vector3 me = Game.SpawnPoint;
            int n = TrenchSquad.SpawnFriendlies(me, 5);
            yield return new WaitForSeconds(1f);
            var fr = new List<EarlGunner>();
            foreach (var g in EarlGunner.All) if (g != null && g.team == Teams.Friendly) fr.Add(g);
            int bands = 0, medics = 0;
            foreach (var g in fr) { foreach (var t in g.Body.GetComponentsInChildren<Transform>()) if (t.name == "Armband") bands++; if (g.medic) medics++; }
            Line($"friendlies: {n} spawned, {bands} armbands, {medics} medics");
            Check(n >= 4 && bands >= n * 2 && medics >= 1, "a friendly squad in your trench, green armbands, a medic");
            // recruit two (one the medic) and walk off
            var medic = fr.Find(g => g.medic);
            var rifle = fr.Find(g => !g.medic);
            medic.Recruit(true); rifle.Recruit(true);
            foreach (var g in fr) if (g != medic && g != rifle) Destroy(g.gameObject);
            yield return null;
            Vector3 far = me; float best = 0f;
            foreach (var p in TrenchNav.Nodes) { float d = Vector3.Distance(p, me); if (p.y < 0.5f && p.z < -15f && d > 12f && d < 16f && d > best) { best = d; far = p; } }
            Game.player.Teleport(far + Vector3.up * 0.05f, 0f);
            for (int i = 0; i < 4; i++) { yield return new WaitForSeconds(4f); foreach (var g in new[] { rifle, medic }) Line($"  {(g == medic ? "medic" : "rifle")} #{g.Body.number} at {g.Body.Pelvis.transform.position}: {g.Status} {g.SpacingDebug} going {g.goingSpeed:0.0} q {g.queuing} {g.NavDebug}"); }
            float dm = Vector3.Distance(medic.Body.Center, PlayerAvatar.Body.Center), dr = Vector3.Distance(rifle.Body.Center, PlayerAvatar.Body.Center);
            Line($"after you moved {best:0} m: squad {dr:0.0} m and {dm:0.0} m from you ({rifle.Status} / {medic.Status})");
            Check(dr < 7f && dm < 7f, "the squad follows you");
            // move order
            Vector3 there = me;
            rifle.order = SquadOrder.Move; rifle.orderPos = there;
            float t0 = Time.time;
            float nl = 4f;
            while (Time.time - t0 < 20f && Vector3.Distance(rifle.Body.Pelvis.transform.position - Vector3.up * 0.95f, there) > 1.6f) { if (Time.time - t0 > nl) { nl += 4f; Line($"  rifle: {rifle.Status} {rifle.NavDebug}"); } yield return null; }
            Line($"move order: {Vector3.Distance(rifle.Body.Pelvis.transform.position - Vector3.up * 0.95f, there):0.0} m from the spot after {Time.time - t0:0} s, now {rifle.order}");
            Check(Vector3.Distance(rifle.Body.Pelvis.transform.position - Vector3.up * 0.95f, there) < 2f, "goes where you send him");
            // the medic on you
            var you = PlayerAvatar.Body;
            var arm = you[PartId.ForearmL];
            you.damage.ApplyStab(arm, arm.transform.position + arm.transform.right * 0.04f, -arm.transform.right, 0.05f);
            int open() { int c = 0; foreach (var bp in you.partList) if (bp != null) foreach (var w in bp.wounds) if (MedKit.CanTreat(w, false)) c++; return c; }
            int before = open();
            medic.order = SquadOrder.Heal; medic.orderTarget = you;
            t0 = Time.time;
            nl = 4f;
            while (Time.time - t0 < 25f && open() > 0) { if (Time.time - t0 > nl) { nl += 4f; Line($"  medic: {medic.Status} order {medic.order} down {medic.Body.ragdoll.IsDown} mode {medic.Body.ragdoll.mode} canStand {medic.Body.physio.CanStand} conc {medic.Body.physio.consciousness:0.00} fear {medic.Body.brain.fear:0.00} wantStand {medic.Body.ragdoll.wantStand} pain {medic.Body.physio.EffectivePain:0.00} {medic.NavDebug}"); } yield return null; }
            Line($"medic: {before} open wounds -> {open()} after {Time.time - t0:0} s");
            Line(DebugProbe.Bodies());
            Game.player.LookAt(medic.Body.Center); yield return GrabView("medic_heal");
            Check(before > 0 && open() == 0, "your medic bandages you");
            // knocked down: a mate helps you up (you don't touch a key)
            medic.order = SquadOrder.Follow;
            you.ragdoll.Fall("test");
            yield return new WaitForSeconds(1.5f);
            t0 = Time.time;
            string who = "";
            while (Time.time - t0 < 20f && you.ragdoll.IsDown) { foreach (var g in new[] { medic, rifle }) if (g.Status.Contains("UP")) who = g.Body.DisplayName + " " + g.Status; yield return null; }
            Line($"knocked down: up again {!you.ragdoll.IsDown} after {Time.time - t0:0.0} s ({who})");
            Check(!you.ragdoll.IsDown, "a mate helps you up");
        }

        // ------------------------------------------------------------ the trenches: map, hostiles, squad movement

        IEnumerator Trench()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(2f);
            Vector3 me = Game.SpawnPoint;
            var gunners = new List<EarlGunner>(FindObjectsByType<EarlGunner>());
            Line($"hostiles: {gunners.Count} in {TrenchSquad.All.Count} squads");
            Check(gunners.Count >= 6, "at least six armed Earls in the trenches");
            float nearest = float.MaxValue; int seen = 0, suppressed = 0, optics = 0, altyn = 0, ratnik = 0, sides = 0, arms = 0, holders = 0, leaners = 0;
            Vector3 eye = me + Vector3.up * 1.6f;
            foreach (var g in gunners)
            {
                var h = g.GetComponent<HumanBody>();
                Vector3 p = h.Pelvis.transform.position;
                nearest = Mathf.Min(nearest, Vector3.Distance(p, me));
                if (!Physics.Raycast(eye, (h.Head.transform.position - eye).normalized, Vector3.Distance(eye, h.Head.transform.position) - 0.3f, ~0, QueryTriggerInteraction.Ignore)) seen++;
                if (g.Suppressed) suppressed++;
                if (g.kit != null && g.kit.sight != SightKind.Iron) optics++;
                var pcs = Pieces(h);
                if (pcs.Exists(x => x.EndsWith("ALTYN HELMET"))) altyn++;
                if (pcs.Exists(x => x.EndsWith("6B47 HELMET"))) ratnik++;
                if (pcs.Exists(x => x.EndsWith("6B45 SIDE"))) sides++;
                if (pcs.Exists(x => x.EndsWith("ARM GUARD"))) arms++;
                if (g.role == SquadRole.Hold) holders++;
                if (g.leans) leaners++;
            }
            Check(nearest > 34f, $"none spawn near you (nearest {nearest:0} m)");
            Check(seen == 0, $"none in your sight at the start ({seen} visible)");
            Line($"kit: suppressed {suppressed}, optics {optics}, Altyn {altyn}, 6B47 {ratnik}, side plates {sides}, arm guards {arms}; holders {holders}, can lean {leaners}");
            Check(ratnik > altyn, "mostly 6B47 helmets, Altyns rare");
            int crates = 0; foreach (var t in FindObjectsByType<Transform>()) if (t.name == "Crate") crates++;
            Check(crates == 0, $"no orange crates on the map ({crates})");

            // walkable trench floor from your trench to theirs, without climbing out (flood fill on a 0.5 m grid)
            var floor = new Dictionary<Vector2Int, bool>();
            bool Floor(Vector2Int c)
            {
                if (floor.TryGetValue(c, out var f)) return f;
                Vector3 p = new Vector3(c.x * 0.5f, 6f, c.y * 0.5f);
                f = Physics.Raycast(p, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y < 0.7f && hit.collider.GetComponentInParent<HumanBody>() == null;
                floor[c] = f; return f;
            }
            var start = new Vector2Int(Mathf.RoundToInt(me.x / 0.5f), Mathf.RoundToInt(me.z / 0.5f));
            var q = new Queue<Vector2Int>(); var seenC = new HashSet<Vector2Int> { start }; q.Enqueue(start);
            float far = me.z; int steps = 0;
            while (q.Count > 0 && steps < 60000)
            {
                var c = q.Dequeue(); steps++;
                far = Mathf.Max(far, c.y * 0.5f);
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var n = c + d;
                    if (seenC.Contains(n) || Mathf.Abs(n.x) > 110 || Mathf.Abs(n.y) > 125 || !Floor(n)) continue;
                    seenC.Add(n); q.Enqueue(n);
                }
                if ((steps & 2047) == 0) yield return null;
            }
            Check(far > 20f, $"trench floor reaches the enemy line from yours (furthest z {far:0.0}, enemy line at ~24)");
            AutoTest.Photo("trench_topdown", new Vector3(5f, 0f, 0f), new Vector3(5.01f, 95f, 0f), null, 70f);

            // 40 s of them hunting you: spacing, leaning, holders staying put
            float minSep = float.MaxValue; int leanFrames = 0, frames = 0; float holdDrift = 0f;
            var holdStart = new Dictionary<EarlGunner, Vector3>();
            foreach (var g in gunners) if (g.role == SquadRole.Hold) holdStart[g] = g.holdPoint;
            for (float t = 0; t < 40f; t += Time.deltaTime)
            {
                frames++;
                var live = gunners.FindAll(g => g != null && g.Fighting);
                int leaning = 0;
                for (int i = 0; i < live.Count; i++)
                {
                    var hi = live[i].GetComponent<HumanBody>();
                    if (Mathf.Abs(hi.ragdoll.lean) > 0.4f) leaning++;
                    for (int j = i + 1; j < live.Count; j++)
                    {
                        var hj = live[j].GetComponent<HumanBody>();
                        minSep = Mathf.Min(minSep, Vector3.Distance(hi.Pelvis.transform.position, hj.Pelvis.transform.position));
                    }
                    if (live[i].role == SquadRole.Hold && holdStart.TryGetValue(live[i], out var hp)) holdDrift = Mathf.Max(holdDrift, Vector3.Distance(hi.Pelvis.transform.position, hp));
                }
                if (live.Count > 0 && leaning == live.Count && live.Count > 1) leanFrames++;
                yield return null;
            }
            Check(minSep > 0.9f, $"never bunched on top of each other (closest pair {minSep:0.00} m)");
            Check(leanFrames < frames * 0.05f, $"not all leaning at once ({leanFrames}/{frames} frames)");
            Check(holdDrift < 2.5f || holders == 0, $"holders stay where they are (drift {holdDrift:0.0} m)");
            AutoTest.Photo("trench_squad", gunners.Count > 0 && gunners[0] ? gunners[0].transform.position : me, (gunners.Count > 0 && gunners[0] ? gunners[0].GetComponent<HumanBody>().Pelvis.transform.position : me) + new Vector3(4f, 6f, -4f), null, 50f);
        }

        // ------------------------------------------------------------ real keys and buttons (Input System state events)

        readonly HashSet<UnityEngine.InputSystem.Key> keysDown = new HashSet<UnityEngine.InputSystem.Key>();
        bool lmb, rmb;
        void Keys()
        {
            var ks = new List<UnityEngine.InputSystem.Key>(keysDown);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState(ks.ToArray()));
            var ms = new UnityEngine.InputSystem.LowLevel.MouseState();
            if (lmb) ms = ms.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);
            if (rmb) ms = ms.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, ms);
        }
        IEnumerator Hold(float sec, System.Action each = null)
        {
            for (float t = 0; t < sec; t += Time.deltaTime) { Keys(); each?.Invoke(); yield return null; }
        }
        void Release() { keysDown.Clear(); lmb = rmb = false; Keys(); }

        static WeaponController Wpn => Game.weapons;
        int Idx(string n) => Wpn.defs.FindIndex(d => d.name == n);

        // ------------------------------------------------------------ gun handling: lying, getting up, falling, arms

        IEnumerator Handling()
        {
            Game.LoadMap(0);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            var me = PlayerAvatar.Body;
            Wpn.Give(Idx("AK-47"), true);
            yield return Hold(1.5f);
            yield return GrabView("handling_1_standing");
            int badArm = 0, frames = 0, carryFrames = 0, farFromHand = 0, bracedFrames = 0;
            float worstHandGap = 0f;
            void Watch()
            {
                frames++;
                // an arm is never both drawn on the gun and shown as your own
                foreach (var (fa, drawn) in new[] { (PartId.ForearmL, Wpn.ArmDrawnL), (PartId.ForearmR, Wpn.ArmDrawnR) })
                {
                    var bp = me[fa];
                    if (bp != null && !bp.severed && drawn && bp.gameObject.layer != PlayerAvatar.OwnLayer) badArm++;
                }
                if (Wpn.Carrying)
                {
                    carryFrames++;
                    var hand = me[PartId.HandR];
                    var gun = Wpn.CurrentModel;
                    float gap = hand != null && gun != null ? Vector3.Distance(hand.transform.position, gun.transform.TransformPoint(GunKit.For("Gun_AK47").grip)) : 0f;
                    worstHandGap = Mathf.Max(worstHandGap, gap);
                    if (gap > 0.2f) farFromHand++;
                }
                if (me.ragdoll.Falling) bracedFrames++;
            }
            // drop (Z): going over
            keysDown.Add(UnityEngine.InputSystem.Key.Y);
            yield return Hold(0.1f, Watch);
            keysDown.Clear();
            yield return Hold(0.25f, Watch);
            yield return GrabView("handling_2_falling");
            yield return Hold(2.5f, Watch);
            Check(me.ragdoll.IsDown, "Z: you go down");
            yield return GrabView("handling_3_lying");
            Check(!Wpn.Carrying && Wpn.ArmDrawnR && Wpn.ArmDrawnL, $"lying there: both hands on the gun (R {Wpn.ArmDrawnR}, L {Wpn.ArmDrawnL})");
            // getting up (SPACE held)
            keysDown.Add(UnityEngine.InputSystem.Key.Space);
            bool sawCarry = false, sawFreeLeft = false; bool shot = false;
            yield return Hold(4.5f, () =>
            {
                Watch();
                if (Wpn.Carrying) { sawCarry = true; if (!Wpn.ArmDrawnL) sawFreeLeft = true; }
                if (Wpn.Carrying && !shot && me.ragdoll.mode == BodyMode.GettingUp) { shot = true; StartCoroutine(GrabView("handling_4_getting_up")); }
            });
            keysDown.Clear();
            yield return Hold(2.5f, Watch);
            Check(sawCarry && sawFreeLeft, $"getting up: gun in one hand, the other hand free for the floor (carried {sawCarry}, left free {sawFreeLeft})");
            Check(farFromHand == 0, $"carried gun stays in the hand (worst gap {worstHandGap:0.000} m over {carryFrames} frames)");
            Check(!me.ragdoll.IsDown && Wpn.ArmDrawnR && Wpn.ArmDrawnL, $"back up: both hands on it again (down {me.ragdoll.IsDown})");
            Check(badArm == 0, $"never a third arm (an arm both drawn and shown: {badArm} of {frames} frames)");
            Line($"falling frames with hands out: {bracedFrames}");
            yield return GrabView("handling_5_up_again");
            // the hands on a pistol and a grenade, close up
            foreach (var (n, file) in new[] { ("GLOCK 17", "hands_pistol"), ("GRENADE", "hands_grenade") })
            {
                Wpn.Give(Idx(n), true);
                yield return Hold(1.5f);
                yield return GrabView(file);
                var rh = GameObject.Find("YourRightHand");
                if (rh != null)
                {
                    Vector3 c = rh.transform.position;
                    AutoTest.Photo(file + "_close_side", c, c + Game.cam.transform.right * 0.35f + Vector3.up * 0.05f, null, 40f);
                    AutoTest.Photo(file + "_close_front", c, c + Game.cam.transform.forward * 0.35f + Vector3.up * 0.08f, null, 40f);
                }
            }
            Release();
        }

        // ------------------------------------------------------------ bandages, surgery, the little Earl

        IEnumerator Medical()
        {
            Game.LoadMap(1);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            var me = PlayerAvatar.Body;
            // a round through the thigh and one through the forearm
            var nine = AmmoTable.For("GLOCK 17")[0];
            foreach (var id in new[] { PartId.ThighL, PartId.ForearmR })
            {
                Vector3 at = me[id].transform.position, from = at + me.Chest.transform.right * (id == PartId.ThighL ? -3f : 3f);
                Ballistics.Instance.Fire(from, (at - from).normalized, nine.velocity, nine.mass, nine.diameter, HitKind.Bullet, false, null, nine);
                yield return new WaitForSeconds(0.4f);
            }
            var wounds = new List<Wound>();
            foreach (var bp in me.partList) if (bp != null) foreach (var w in bp.wounds) if (w.type != WoundType.Internal && w.type != WoundType.Bruise) wounds.Add(w);
            Line("wounds: " + string.Join(", ", wounds.ConvertAll(w => w.part.Name + " " + w.type + " " + w.extRate.ToString("0.0"))));
            Check(wounds.Count > 0, "you have wounds to treat");
            yield return new WaitForSeconds(0.5f);
            yield return GrabView("medical_1_mini_earl");
            // the bandage: click opens the list
            Wpn.Give(Idx("BANDAGE"), true);
            yield return Hold(0.6f);
            lmb = true; yield return Hold(0.1f); lmb = false; yield return Hold(0.3f);
            Check(MedKit.Busy, "left click with the bandage opens the wound list");
            yield return GrabView("medical_2_wound_list");
            rmb = true; yield return Hold(0.1f); rmb = false; yield return Hold(0.3f);
            Check(!MedKit.Busy, "right click closes it");
            Cursor.lockState = CursorLockMode.Locked;
            // bandage the worst one
            wounds.Sort((a, b) => b.extRate.CompareTo(a.extRate));
            var w1 = wounds[0]; float before = w1.extRate;
            MedKit.I.Begin(w1, false);
            yield return Hold(1f);
            yield return GrabView("medical_3_bandaging");
            yield return Hold(2.6f);
            Check(w1.bandaged && w1.extRate < before * 0.5f + 0.01f, $"bandaged: {w1.part.Name} {before:0.0} -> {w1.extRate:0.0} ml/s");
            // surgery on a deep one
            Wpn.Give(Idx("SURGERY KIT"), true);
            yield return Hold(0.6f);
            var deep = wounds.Find(w => MedKit.CanTreat(w, true));
            if (deep == null) { Line("no deep wound left to suture"); Release(); yield break; }
            MedKit.I.Begin(deep, true);
            yield return Hold(0.5f);
            Check(MedKit.Busy, "suturing under way");
            float waited = 0f;
            while (MedKit.Busy && waited < 25f) { waited += Time.deltaTime; Keys(); yield return null; }
            Check(deep.sutured && deep.extRate == 0f, $"sutured after {waited + 0.5f:0.0} s: {deep.part.Name} bleeding {deep.extRate:0.0}");
            yield return GrabView("medical_4_after");
            Release();
        }

        // ------------------------------------------------------------ optics

        IEnumerator Optics()
        {
            Game.LoadMap(1);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            int ak = Idx("AK-47");
            Wpn.Give(ak, true);
            foreach (var (s, file) in new[] { (SightKind.RedDot, "optic_reddot"), (SightKind.Holo, "optic_holo"), (SightKind.Scope4x, "optic_scope4x") })
            {
                Wpn.loadouts[ak].sight = s; Wpn.RefreshAttachments();
                yield return Hold(0.6f);
                rmb = true; yield return Hold(1.4f);
                yield return GrabView(file);
                var ep = GameObject.Find("ScopeEyepiece");
                // the picture in the glass must not be black: sample the scope's render texture
                float lum = -1f;
                if (ep != null)
                {
                    var rt = ep.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_MainTex") as RenderTexture;
                    if (rt != null)
                    {
                        var prev = RenderTexture.active; RenderTexture.active = rt;
                        var tx = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tx.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tx.Apply();
                        RenderTexture.active = prev;
                        lum = 0f; var px = tx.GetPixels(); foreach (var c in px) lum += c.grayscale; lum /= px.Length;
                        Destroy(tx);
                    }
                }
                Check(ep != null && lum > 0.05f, $"{s}: picture in picture in the glass, not black (brightness {lum:0.00})");
                if (ep != null) AutoTest.Photo(file + "_glass", ep.transform.position, ep.transform.position - ep.transform.forward * 0.12f, null, 30f);
                rmb = false; yield return Hold(0.6f);
            }
            int awp = Idx("AWP");
            Wpn.Give(awp, true);
            yield return Hold(0.8f);
            rmb = true; yield return Hold(1.6f);
            yield return GrabView("optic_scope8x");
            Check(GameObject.Find("ScopeEyepiece") != null, "the AWP's 8x scope has its eyepiece");
            Check(Game.cam.fieldOfView > 40f, $"the world round the scope stays nearly normal (fov {Game.cam.fieldOfView:0})");
            Release();
            // point aim: low and canted; with right mouse, canted at the eye
            Wpn.Give(ak, true);
            Wpn.loadouts[ak].sight = SightKind.Iron; Wpn.RefreshAttachments();
            yield return Hold(0.8f);
            keysDown.Add(UnityEngine.InputSystem.Key.B); yield return Hold(0.1f); keysDown.Clear();
            yield return Hold(1f);
            Check(Wpn.pointT > 0.95f && Wpn.AimAmount < 0.05f, "B: point aim, held low");
            yield return GrabView("pointaim_low");
            rmb = true; yield return Hold(1.2f);
            Check(Wpn.pointAimT > 0.95f && Wpn.AimAmount < 0.05f, "point aim + right mouse: canted aim at the eye");
            yield return GrabView("pointaim_canted_ads");
            rmb = false;
            keysDown.Add(UnityEngine.InputSystem.Key.B); yield return Hold(0.1f); keysDown.Clear();
            Release();
            Wpn.loadouts[ak].sight = SightKind.Iron; Wpn.RefreshAttachments();
        }

        // ------------------------------------------------------------ gun sounds

        static void SaveWav(AudioClip c, string path)
        {
            var d = new float[c.samples * c.channels]; c.GetData(d, 0);
            using (var f = new BinaryWriter(File.Create(path)))
            {
                int n = d.Length, rate = c.frequency;
                f.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); f.Write(36 + n * 2); f.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                f.Write(16); f.Write((short)1); f.Write((short)c.channels); f.Write(rate); f.Write(rate * 2 * c.channels); f.Write((short)(2 * c.channels)); f.Write((short)16);
                f.Write(System.Text.Encoding.ASCII.GetBytes("data")); f.Write(n * 2);
                foreach (var s in d) f.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
            }
        }

        static float HighShare(AudioClip c)
        {
            // share of the sound's energy above 3 kHz (the crack), from a one-pole split
            var d = new float[c.samples * c.channels]; c.GetData(d, 0);
            float a3 = Mathf.Exp(-2f * Mathf.PI * 3000f / c.frequency), lp = 0f;
            double all = 0, hi = 0;
            foreach (var s in d) { lp = (1f - a3) * s + a3 * lp; float h = s - lp; all += s * s; hi += h * h; }
            return (float)(hi / System.Math.Max(1e-9, all));
        }

        static float Rms(AudioClip c)
        {
            if (c == null) return -99f;
            var d = new float[c.samples * c.channels]; c.GetData(d, 0);
            int pk = 0; for (int i = 0; i < d.Length; i++) if (Mathf.Abs(d[i]) > Mathf.Abs(d[pk])) pk = i;
            int n = Mathf.Min(d.Length - pk, c.frequency / 10 * c.channels);
            double sum = 0; for (int i = 0; i < n; i++) sum += d[pk + i] * d[pk + i];
            return 20f * Mathf.Log10(Mathf.Sqrt((float)(sum / Mathf.Max(1, n))) + 1e-6f);
        }

        IEnumerator Audio()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(2f);
            string ad = Path.Combine(dir, "audio"); Directory.CreateDirectory(ad);
            foreach (var g in new[] { "gun_ak", "gun_ak_full", "gun_glock_full", "gun_50_full", "gun_ak_supp", "gun_ak_tail_open", "gun_ak_tail_tight", "gun_ak_far", "gun_glock_supp", "thump", "mech_rifle", "flyby_crack", "flyby_whiz", "casing", "casing_dirt", "ricochet" })
            {
                var c = Sfx.Clip(g);
                Check(c != null, $"sound '{g}' exists" + (c != null ? $" ({c.length:0.00} s)" : ""));
                if (c != null) SaveWav(c, Path.Combine(ad, g + ".wav"));
            }
            // loud like the real thing, and a suppressor only takes the edge off (measured: 6-25 dB, still 130-145 dB)
            float raw = Rms(Sfx.Clip("gun_ak")), full = Rms(Sfx.Clip("gun_ak_full")), supp = Rms(Sfx.Clip("gun_ak_supp"));
            Line($"AK report, first 100 ms: recording {raw:0.0} dBFS, as played {full:0.0} dBFS, suppressed {supp:0.0} dBFS ({full - supp:0.0} dB down)");
            Check(full > raw + 5f, "the report is much fuller than the bare recording");
            Check(full - supp > 3f && full - supp < 9f, "suppressed: quieter, but still a loud gunshot (3-9 dB down)");
            float hiFull = HighShare(Sfx.Clip("gun_ak_full")), hiSupp = HighShare(Sfx.Clip("gun_ak_supp"));
            Check(hiSupp > 0.05f, $"suppressed rifle keeps its crack (high-frequency share {hiSupp:0.000} vs {hiFull:0.000} unsuppressed)");
            // a full mag on full auto, right in your ears: loud, but the mix never clips
            Game.LoadMap(0);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            GameSettings.InfiniteAmmo = true;
            Wpn.Give(Idx("AK-47"), true); Wpn.loadouts[Idx("AK-47")].fireMode = 0;
            yield return Hold(0.6f);
            MasterLimiter.Reduction = 0f;
            lmb = true; yield return Hold(2.5f); lmb = false; yield return Hold(0.5f);
            Line($"full auto 2.5 s: the limiter pulled the mix down at most {20f * Mathf.Log10(Mathf.Max(1e-4f, 1f - MasterLimiter.Reduction)):0.0} dB");
            Check(MasterLimiter.Reduction < 0.7f, "full auto stays loud but under control (limiter under 10 dB)");
            Game.LoadMap(3);
            yield return new WaitForSeconds(2f);
            Check(Sfx.Enclosed(Game.SpawnPoint + Vector3.up * 0.3f), "in the trench: short slap echo");
            Check(!Sfx.Enclosed(new Vector3(0f, 2f, 0f)), "out in the field: long rolling echo");
            // a round passing a metre from your head
            int fb = Sfx.FlyBys;
            Vector3 ears = Sfx.Ears;
            var ak = AmmoTable.For("AK-47")[0];
            // (down from above: a level shot along a zig-zag trench would hit its wall before it got to you)
            Vector3 from = ears + Vector3.up * 30f + Game.cam.transform.right * 1f + Game.cam.transform.forward * 3f, to = ears + Game.cam.transform.right * 1f - Vector3.up * 2f;
            Ballistics.FiringBody = null;
            Ballistics.Instance.Fire(from, (to - from).normalized, ak.velocity, ak.mass, ak.diameter, HitKind.Bullet, true, null, ak);
            yield return new WaitForSeconds(0.5f);
            Check(Sfx.FlyBys > fb, "a round going past your head cracks");
            Line("listen: Screenshots/featuretest/audio/*.wav");
        }

        // ------------------------------------------------------------ the pistol grip and point aim

        IEnumerator Pistol()
        {
            Game.LoadMap(0);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            Wpn.Give(Idx("GLOCK 17"), true);
            yield return Hold(1.5f);
            yield return GrabView("pistol_hip");
            foreach (var n in new[] { "YourRightHand", "YourLeftHand" })
            {
                var h = GameObject.Find(n);
                if (h == null) continue;
                Vector3 c = Wpn.CurrentModel.transform.position;
                AutoTest.Photo("pistol_grip_left", c, c - Game.cam.transform.right * 0.32f + Game.cam.transform.forward * 0.05f, null, 40f);
                AutoTest.Photo("pistol_grip_right", c, c + Game.cam.transform.right * 0.32f + Game.cam.transform.forward * 0.05f, null, 40f);
                AutoTest.Photo("pistol_grip_below", c, c - Game.cam.transform.up * 0.3f + Game.cam.transform.forward * 0.12f, null, 40f);
                break;
            }
            rmb = true; yield return Hold(1.2f);
            yield return GrabView("pistol_ads");
            rmb = false; yield return Hold(0.6f);
            keysDown.Add(UnityEngine.InputSystem.Key.B); yield return Hold(0.1f); keysDown.Clear(); yield return Hold(1f);
            yield return GrabView("pistol_point_low");
            rmb = true; yield return Hold(1.2f);
            // raised point aim: the sights must stay below the eye (you look over the gun, not down the sights)
            var m = Wpn.CurrentModel.transform;
            Vector3 rear = m.TransformPoint(new Vector3(0f, 0.055f, -0.08f));
            float below = Vector3.Dot(Game.cam.transform.position - rear, Game.cam.transform.up);
            Check(Wpn.pointAimT > 0.95f && below > 0.03f, $"point aim raised: gun up but under the eye line ({below * 100f:0.0} cm below), not down the sights");
            yield return GrabView("pistol_point_raised");
            rmb = false;
            keysDown.Add(UnityEngine.InputSystem.Key.B); yield return Hold(0.1f);
            Release();
        }

        // ------------------------------------------------------------ adrenaline, helmet ring, blacking out

        IEnumerator Adrenaline()
        {
            Game.LoadMap(1);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            var me = PlayerAvatar.Body;
            float a0 = me.physio.adrenaline, t0 = PlayerBody.I.Tremor;
            // rounds cracking past your head
            var ak = AmmoTable.For("AK-47")[0];
            for (int k = 0; k < 6; k++)
            {
                Vector3 ears = Sfx.Ears;
                Vector3 from = ears + Game.cam.transform.forward * 30f + Game.cam.transform.right * 0.8f + Vector3.up * 0.3f, to = ears + Game.cam.transform.right * 0.8f - Game.cam.transform.forward * 4f;
                Ballistics.Instance.Fire(from, (to - from).normalized, ak.velocity, ak.mass, ak.diameter, HitKind.Bullet, true, null, ak);
                yield return new WaitForSeconds(0.25f);
            }
            Check(me.physio.adrenaline > a0 + 0.3f, $"rounds past your head: adrenaline {a0:0.00} -> {me.physio.adrenaline:0.00}");
            Check(PlayerBody.I.Tremor > t0 + 0.15f, $"hands shake with it: tremor {t0:0.00} -> {PlayerBody.I.Tremor:0.00}");
            yield return GrabView("adrenaline_bright");
            // a helmet with the visor down: ears covered; a round on it rings
            PlayerAvatar.Wear(new ArmourLoadout { vest = VestKind.None, helmet = HelmetKind.Spetsnaz, visor = true, visorDown = true });
            yield return new WaitForSeconds(0.8f);
            Check(Sfx.EarCover > 0.9f, $"visor down: hearing muffled (ear cover {Sfx.EarCover:0.00})");
            var helm = new List<ArmourPiece>(me.Head.GetComponentsInChildren<ArmourPiece>()).Find(p => p.label == "ALTYN HELMET");
            Vector3 top = helm.transform.TransformPoint(helm.bounds.center) + Vector3.up * 0.04f;
            var nine = AmmoTable.For("GLOCK 17")[0];
            Ballistics.Instance.Fire(top - me.Head.transform.right * 3f, me.Head.transform.right, nine.velocity, nine.mass, nine.diameter, HitKind.Bullet, false, null, nine);
            yield return new WaitForSeconds(0.3f);
            Check(PlayerBody.I.RingVolume > 0.08f, $"a round on the helmet: it rings (ring volume {PlayerBody.I.RingVolume:0.00})");
            PlayerAvatar.Wear(new ArmourLoadout { vest = VestKind.None, helmet = HelmetKind.None });
            yield return new WaitForSeconds(0.5f);
            Check(Sfx.EarCover < 0.05f, "helmet off: hearing back");
            // bleeding out slowly: the screen fades as you go
            float darkAtHalf = -1f;
            for (float t = 0; t < 25f && !PlayerBody.I.passedOut; t += Time.deltaTime)
            {
                me.physio.blood -= Physiology.NormalBlood * 0.022f * Time.deltaTime;
                if (darkAtHalf < 0f && me.physio.consciousness < 0.45f) darkAtHalf = PlayerBody.I.Dark;
                yield return null;
            }
            Check(darkAtHalf > 0.2f && darkAtHalf < 0.9f, $"slow bleed-out: half gone, the screen is fading ({darkAtHalf:0.00})");
            yield return null;
            Check(PlayerBody.I.passedOut && PlayerBody.I.Dark > 0.95f, $"passed out: fully black ({PlayerBody.I.Dark:0.00})");
            yield return new WaitForSeconds(0.3f);
            Check(PlayerBody.I.RingVolume < 0.01f, $"out cold: no ringing ({PlayerBody.I.RingVolume:0.00})");
            PlayerBody.I.Revive(); Game.player.Teleport(Game.SpawnPoint, 0f);
            yield return new WaitForSeconds(1.5f);
            Check(PlayerBody.I.Dark < 0.05f, "back up: the screen is clear again");
            Release();
        }

        /// <summary>Aiming down the sights: how far the muzzle wanders calm, and with adrenaline up.</summary>
        IEnumerator AdrenalineAim()
        {
            Game.LoadMap(0);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            var me = PlayerAvatar.Body;
            Wpn.Give(Idx("AK-47"), true);
            yield return Hold(1f);
            rmb = true; yield return Hold(1.2f);
            IEnumerator Measure(float adr, System.Action<float> result)
            {
                var dirs = new List<Vector3>();
                for (float t = 0; t < 2.5f; t += Time.deltaTime)
                {
                    me.physio.adrenaline = adr; me.physio.pain = 0f;
                    Keys();
                    if (t > 0.5f) dirs.Add(Quaternion.Inverse(Game.cam.transform.rotation) * Wpn.AimDirection);
                    yield return null;
                }
                Vector3 mean = Vector3.zero; foreach (var d in dirs) mean += d; mean.Normalize();
                float s = 0f; foreach (var d in dirs) s += Vector3.Angle(d, mean); result(s / Mathf.Max(1, dirs.Count));
            }
            float calm = 0f, rushed = 0f;
            yield return Measure(0.05f, v => calm = v);
            yield return Measure(0.95f, v => rushed = v);
            Check(rushed > calm * 3f && rushed > 0.1f, $"aiming down the sights: muzzle wander calm {calm:0.000} deg, with adrenaline {rushed:0.000} deg");
            rmb = false; Release();
            me.physio.adrenaline = 0.05f;
        }

        // ------------------------------------------------------------ recoil and fire modes

        /// <summary>Fire n rounds (held trigger) aiming down the sights; the path the barrel takes, in degrees
        /// right / up from where it pointed before the first shot.</summary>
        IEnumerator Burst(int gun, int n, List<Vector2> path)
        {
            Wpn.Give(gun, true);
            GameSettings.InfiniteAmmo = true;
            yield return Hold(0.8f);
            rmb = true; yield return Hold(1.2f);
            Vector3 f0 = Wpn.AimDirection, r0 = Game.cam.transform.right, u0 = Game.cam.transform.up;
            float tEnd = Time.unscaledTime + n * 60f / Wpn.Rpm(gun) + 0.05f;
            lmb = true;
            while (Time.unscaledTime < tEnd)
            {
                Keys();
                Vector3 d = Wpn.AimDirection;
                path.Add(new Vector2(Mathf.Asin(Mathf.Clamp(Vector3.Dot(d, r0), -1f, 1f)) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(Vector3.Dot(d, u0), -1f, 1f)) * Mathf.Rad2Deg));
                yield return null;
            }
            lmb = false; yield return Hold(0.6f);
            rmb = false; yield return Hold(0.8f);
        }

        static (float climb, float side) Spread(List<Vector2> p)
        {
            float up = 0f, lo = 0f, hi = 0f;
            foreach (var v in p) { up = Mathf.Max(up, v.y); lo = Mathf.Min(lo, v.x); hi = Mathf.Max(hi, v.x); }
            return (up, hi - lo);
        }

        IEnumerator RecoilTest()
        {
            Game.LoadMap(0);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(1.5f);
            int ak = Idx("AK-47"), ar = Idx("AR-15");
            // fire modes
            Wpn.Give(ak, true); Wpn.loadouts[ak].fireMode = 0;
            yield return Hold(0.6f);
            Check(Wpn.FireMode(ak) == "AUTO", "AK starts on AUTO");
            keysDown.Add(UnityEngine.InputSystem.Key.Z); yield return Hold(0.1f); keysDown.Clear(); yield return Hold(0.3f);
            Check(Wpn.FireMode(ak) == "SEMI", "Z: AK to SEMI");
            GameSettings.InfiniteAmmo = false;
            Wpn.Give(ak, true); yield return Hold(0.5f);
            string before = Wpn.AmmoText;
            lmb = true; yield return Hold(0.8f); lmb = false; yield return Hold(0.3f);
            Line($"SEMI, trigger held 0.8 s: {before} -> {Wpn.AmmoText}");
            Check(Wpn.AmmoText.StartsWith("29"), "SEMI: holding the trigger fires one round");
            keysDown.Add(UnityEngine.InputSystem.Key.Z); yield return Hold(0.1f); keysDown.Clear(); yield return Hold(0.3f);
            Check(Wpn.FireMode(ak) == "AUTO", "Z again: back to AUTO");
            // one aimed AK shot still kicks
            var one = new List<Vector2>();
            yield return Burst(ak, 1, one);
            float oneMax = 0f; foreach (var v in one) oneMax = Mathf.Max(oneMax, v.magnitude);
            Check(oneMax > 0.6f, $"one aimed AK shot: the muzzle jumps {oneMax:0.00} deg");
            // ten-round bursts, AK and AR-15
            var akA = new List<Vector2>(); var akB = new List<Vector2>(); var arA = new List<Vector2>();
            yield return Burst(ak, 10, akA);
            float slow = Wpn.FireSlow;
            yield return Burst(ak, 10, akB);
            yield return Burst(ar, 10, arA);
            var sa = Spread(akA); var sb = Spread(akB); var sr = Spread(arA);
            Line($"AK burst 1: climb {sa.climb:0.0} deg, side {sa.side:0.0} deg  ·  AK burst 2: climb {sb.climb:0.0}, side {sb.side:0.0}  ·  AR-15: climb {sr.climb:0.0}, side {sr.side:0.0}");
            Check(sa.climb > sr.climb * 1.3f && (sa.side + sb.side) * 0.5f > sr.side, "the AK kicks harder and dances more than the AR-15");
            float diff = 0f; int nn = Mathf.Min(akA.Count, akB.Count);
            for (int i = 0; i < nn; i++) diff += Mathf.Abs(akA[i].x - akB[i].x);
            Check(nn > 0 && diff / nn > 0.15f, $"two AK bursts don't follow the same track (mean side difference {diff / Mathf.Max(1, nn):0.00} deg)");
            Check(slow > 0.3f, $"firing the AK slows your walk ({slow:0.00})");
            // the shotgun: one shell, a big kick
            var sg = new List<Vector2>();
            yield return Burst(Idx("MOSSBERG 590"), 1, sg);
            float sgMax = 0f; foreach (var v in sg) sgMax = Mathf.Max(sgMax, v.magnitude);
            Check(sgMax > oneMax * 2.5f, $"a shotgun shell kicks far harder than an AK round ({sgMax:0.0} vs {oneMax:0.0} deg)");
            GameSettings.InfiniteAmmo = false;
            // a grenade throw: wind up, let go from the hand, follow through
            Wpn.Give(Idx("GRENADE"), true);
            yield return Hold(0.8f);
            int g0 = FindObjectsByType<Grenade>().Length;
            Vector3 handStart = Wpn.CurrentModel.transform.position;
            lmb = true; yield return Hold(0.05f); lmb = false;
            yield return Hold(0.3f);
            Check(Wpn.Throwing && FindObjectsByType<Grenade>().Length == g0, "throw: winding up, not let go yet");
            float back = Vector3.Dot(Wpn.CurrentModel.transform.position - handStart, -Game.cam.transform.forward);
            Check(back > 0.08f, $"the arm goes back for the throw ({back * 100f:0} cm)");
            yield return GrabView("grenade_windup");
            yield return Hold(0.3f);
            Check(FindObjectsByType<Grenade>().Length > g0, "let go: the grenade is in the air");
            yield return GrabView("grenade_release");
            yield return Hold(0.8f);
            Check(!Wpn.Throwing, "follow through done, ready for the next");
            Release();
        }

        // ------------------------------------------------------------ giving up while unconscious

        IEnumerator GiveUp()
        {
            Game.LoadMap(0);
            yield return new WaitForSeconds(1.5f);
            var me = PlayerAvatar.Body;
            if (me == null) { Line("no player body"); yield break; }
            // bleed out until out cold
            me.physio.blood = Physiology.NormalBlood * 0.42f;
            float t = 0f;
            while (t < 8f && (PlayerBody.I == null || !PlayerBody.I.passedOut)) { t += Time.deltaTime; me.physio.blood = Mathf.Min(me.physio.blood, Physiology.NormalBlood * 0.42f); yield return null; }
            Check(PlayerBody.I.passedOut, $"passed out after {t:0.0} s");
            PlayerBody.I.GiveUp();
            yield return null;
            Check(PlayerBody.I.dead && me.physio.dead, "ENTER (GiveUp) kills you: " + PlayerBody.I.cause);
            yield return new WaitForSeconds(2f);
            // and ENTER after that starts you again
            PlayerBody.I.Revive();
            Game.player.Teleport(Game.SpawnPoint, 0f);
            yield return new WaitForSeconds(1f);
            Check(PlayerAvatar.Body != null && !PlayerAvatar.Body.Dead, "respawned alive");
        }
    }
}
