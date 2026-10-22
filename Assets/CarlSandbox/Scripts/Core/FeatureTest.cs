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

        IEnumerator Audio()
        {
            Game.LoadMap(3);
            yield return new WaitForSeconds(2f);
            string ad = Path.Combine(dir, "audio"); Directory.CreateDirectory(ad);
            foreach (var g in new[] { "gun_ak", "gun_ak_supp", "gun_ak_tail_open", "gun_ak_tail_tight", "gun_ak_far", "gun_glock_supp", "thump", "mech_rifle", "flyby_crack", "flyby_whiz", "casing", "casing_dirt", "ricochet" })
            {
                var c = Sfx.Clip(g);
                Check(c != null, $"sound '{g}' exists" + (c != null ? $" ({c.length:0.00} s)" : ""));
                if (c != null) SaveWav(c, Path.Combine(ad, g + ".wav"));
            }
            float hiDry = HighShare(Sfx.Clip("gun_ak")), hiSupp = HighShare(Sfx.Clip("gun_ak_supp"));
            Check(hiSupp < hiDry * 0.7f, $"suppressed shot has the crack taken off (high-frequency share {hiSupp:0.000} vs {hiDry:0.000})");
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
