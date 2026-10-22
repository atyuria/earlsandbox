using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Carl
{
    /// <summary>
    /// Automated playtest of holding weapons: for each weapon it walks, sprints, backs off and strafes, sweeps the
    /// view hard left/right/up/down (also while walking), aims, fires, crouches and leans - through the real
    /// keyboard and mouse (state events into the Input System), so it runs exactly the code your hands do. Every
    /// frame it measures what must never happen and writes a report (Temp/ArmTest.txt) and a contact sheet per
    /// weapon of what you saw in each phase (Temp/ArmTest_[weapon].jpg):
    ///  - any part of the gun forward of the grip inside your torso; the gun left behind where it is held
    ///  - a bent-back wrist (angle between forearm and hand), an elbow up at/above the shoulder or across the body,
    ///    an arm through the torso
    ///  - anything of you or the gun closer than a few centimetres to the eye, in view
    ///  - hands or gun flung about; the gun snapping back to place; falling over; new injuries
    /// Start it with ArmTest.Run() (or Shift+F5).
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class ArmTest : MonoBehaviour
    {
        public static ArmTest Current;
        public static bool Running => Current != null;
        static readonly int[] AllWeapons = { 1, 10, 11, 9, 0, 4, 2, 3, 12, 5 };
        int[] Weapons = AllWeapons;
        string[] only; int repeats = 1;

        struct Phase { public string name; public float dur; public Key[] keys; public bool lmb, rmb; public System.Func<float, (float yaw, float pitch)> look; }
        List<Phase> phases;
        int wi, pi;
        float phaseT, yaw0;
        bool shot;
        readonly StringBuilder report = new StringBuilder();
        WeaponController wc; PlayerController pc;
        System.Reflection.FieldInfo yawF, pitchF;
        Texture2D sheet, sheetOut;
        int injuries0;
        // per phase worst values
        float gunIn, gunLag, wristMax, elbowUp, elbowCross, armIn, nearView, flingHand, flingGun; int frames, downFrames, snaps0;
        string worstNote;

        public static void Run() => Run(null, null, 1);

        /// <summary>A focused run: these weapons (indices), only the phases named, each repeated.</summary>
        public static void Run(int[] weapons, string[] phasesOnly, int repeat)
        {
            if (Current != null) return;
            var go = new GameObject("ArmTest");
            Current = go.AddComponent<ArmTest>();
            if (weapons != null) Current.Weapons = weapons;
            Current.only = phasesOnly; Current.repeats = Mathf.Max(1, repeat);
        }

        void Start()
        {
            wc = FindFirstObjectByType<WeaponController>();
            pc = FindFirstObjectByType<PlayerController>();
            yawF = typeof(PlayerController).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            pitchF = typeof(PlayerController).GetField("pitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            yaw0 = (float)yawF.GetValue(pc);
            foreach (var w in Weapons) wc.Give(w, true);
            // the open range, clear of Earls and walls
            if (SandboxGame.Instance != null) SandboxGame.Instance.LoadMap(1);
            Time.timeScale = 1f;
            phases = new List<Phase>
            {
                P("idle", 1.0f, null, (t) => (0f, 5f)),
                P("walk", 2.0f, new[] { Key.W }, (t) => (0f, 5f)),
                P("walk+sweep", 2.4f, new[] { Key.W }, (t) => (Mathf.Sin(t * 5f) * 70f, 5f + Mathf.Sin(t * 3.1f) * 25f)),
                P("sprint", 1.6f, new[] { Key.W, Key.LeftShift }, (t) => (0f, 5f)),
                P("back+strafe", 1.6f, new[] { Key.S, Key.A }, (t) => (0f, 5f)),
                P("walk+look down", 1.8f, new[] { Key.W }, (t) => (0f, Mathf.Lerp(20f, 85f, Mathf.Clamp01(t / 0.8f)))),
                P("look up", 1.2f, null, (t) => (0f, Mathf.Lerp(5f, -80f, Mathf.Clamp01(t / 0.6f)))),
                P("fast spin", 1.6f, new[] { Key.D }, (t) => (t * 420f, 10f + Mathf.Sin(t * 9f) * 30f)),
                P("ADS walk", 1.6f, new[] { Key.W }, (t) => (Mathf.Sin(t * 2f) * 20f, 5f), rmb: true),
                P("fire", 1.4f, null, (t) => (0f, 5f), lmb: true),
                P("ADS fire", 1.4f, null, (t) => (0f, 5f), lmb: true, rmb: true),
                P("crouch walk", 1.4f, new[] { Key.W, Key.LeftCtrl }, (t) => (0f, 15f)),
                P("lean L/R", 1.6f, null, (t) => (Mathf.Sin(t * 3f) * 15f, 5f)),
            };
            if (only != null) phases = phases.FindAll(p => System.Array.IndexOf(only, p.name) >= 0);
            if (repeats > 1) { var rep = new List<Phase>(); for (int r = 0; r < repeats; r++) rep.AddRange(phases); phases = rep; }
            report.AppendLine("ARM / HOLD PLAYTEST  " + System.DateTime.Now);
            // why you go down, if you do
            ActiveRagdoll.DebugFalls = true;
            Application.logMessageReceived += OnLog;
            StartWeapon();
        }

        static Phase P(string n, float d, Key[] k, System.Func<float, (float, float)> look, bool lmb = false, bool rmb = false)
            => new Phase { name = n, dur = d, keys = k ?? new Key[0], look = look, lmb = lmb, rmb = rmb };

        void StartWeapon()
        {
            if (wi >= Weapons.Length) { Finish(); return; }
            wc.Select(Weapons[wi]);
            pc.Teleport(new Vector3(0f, 0.1f, 10f), 0f);
            yaw0 = 0f;
            pi = 0; phaseT = -0.8f;   // a moment to raise it
            sheet = new Texture2D(4 * 320, 4 * 200, TextureFormat.RGB24, false);
            sheetOut = new Texture2D(4 * 320, 4 * 200, TextureFormat.RGB24, false);
            report.AppendLine().AppendLine("== " + wc.Current.name);
            ResetPhase();
        }

        void ResetPhase()
        {
            gunIn = gunLag = wristMax = elbowUp = elbowCross = armIn = nearView = flingHand = flingGun = 0f;
            frames = downFrames = 0; shot = false; worstNote = "";
            snaps0 = wc.GetHoldDebug().snaps;
            injuries0 = InjuryCount();
        }

        void Update()
        {
            if (wc == null || pc == null) return;
            Cursor.lockState = CursorLockMode.Locked;
            var ph = phases[Mathf.Min(pi, phases.Count - 1)];
            bool active = phaseT >= 0f;
            // the keys and buttons of this phase, held down through the Input System
            var keys = new List<Key>();
            if (active) keys.AddRange(ph.keys);
            if (active && ph.name == "lean L/R") keys.Add(phaseT < ph.dur * 0.5f ? Key.Q : Key.E);
            if (PlayerAvatar.Body != null && PlayerAvatar.Body.ragdoll.IsDown) keys.Add(Key.Space);   // knocked down: get up
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys.ToArray()));
            var ms = new MouseState();
            if (active && ph.lmb) ms = ms.WithButton(MouseButton.Left, wc.Current.type == WeaponType.Gun && !IsAuto() ? Mathf.Repeat(phaseT, 0.25f) < 0.08f : true);
            if (active && ph.rmb) ms = ms.WithButton(MouseButton.Right, true);
            InputSystem.QueueStateEvent(Mouse.current, ms);
            if (active)
            {
                // turned no faster than a hard mouse flick (720 deg/s), never jumped
                var (y, p) = ph.look(phaseT);
                float cy = (float)yawF.GetValue(pc), cp = (float)pitchF.GetValue(pc);
                yawF.SetValue(pc, Mathf.MoveTowards(cy, yaw0 + y, 720f * Time.deltaTime));
                pitchF.SetValue(pc, Mathf.MoveTowards(cp, p, 500f * Time.deltaTime));
            }
        }

        bool IsAuto() => wc.Current.name == "AR-15" || wc.Current.name == "AK-47";

        void LateUpdate()
        {
            if (wc == null) return;
            phaseT += Time.deltaTime;
            if (phaseT >= 0f) Measure();
            var ph = phases[pi];
            if (!shot && phaseT >= ph.dur * 0.55f) { Snapshot(pi); shot = true; }
            if (phaseT >= ph.dur)
            {
                Report(ph.name);
                pi++; phaseT = 0f;
                ResetPhase();
                if (pi >= phases.Count)
                {
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Temp/ArmTest_" + wc.Current.name.Replace(" ", "_").Replace(".", "") + ".jpg"), sheet.EncodeToJPG(80));
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Temp/ArmTestSide_" + wc.Current.name.Replace(" ", "_").Replace(".", "") + ".jpg"), sheetOut.EncodeToJPG(80));
                    wi++;
                    StartWeapon();
                }
            }
        }

        void Measure()
        {
            var camT = Camera.main != null ? Camera.main.transform : null;
            try { MeasureInner(); } finally { if (camT != null) lastCam = camT.position; }
        }
        Vector3 lastCam;

        void MeasureInner()
        {
            frames++;
            var dbg = wc.GetHoldDebug();
            var me = PlayerAvatar.Body;
            var cam = Camera.main;
            if (me == null || cam == null) return;
            if (me.ragdoll.IsDown) downFrames++;
            Vector3 up = cam.transform.up;
            // the torso: a capsule from the pelvis up through the chest
            Vector3 tA = me.Pelvis.transform.position, tB = me.Chest.transform.position + me.Chest.transform.up * 0.1f;
            const float torsoR = 0.13f;
            if (dbg.held && dbg.gun != null && dbg.gunMesh != null && dbg.gunMesh.sharedMesh != null)
            {
                var v = dbg.gunMesh.sharedMesh.vertices;
                var mt = dbg.gunMesh.transform;
                for (int i = 0; i < v.Length; i += 23)
                {
                    if (v[i].z < dbg.gripZ + 0.05f) continue;          // the stock and the grip hand belong against you
                    Vector3 w = mt.TransformPoint(v[i]);
                    float dd = DistSeg(w, tA, tB);
                    if (torsoR - dd > gunIn) { gunIn = torsoR - dd; worstNote = "gun vertex in torso"; }
                    Vector3 c = cam.transform.InverseTransformPoint(w);
                    if (c.z > 0f && c.z < 0.06f && InView(cam, w)) nearView = Mathf.Max(nearView, 0.06f - c.z);
                }
                gunLag = Mathf.Max(gunLag, dbg.offErr);
                Vector3 camVel = (cam.transform.position - lastCam) / Mathf.Max(Time.deltaTime, 1e-4f);
                if (frames > 2) flingGun = Mathf.Max(flingGun, (dbg.gunVel - camVel).magnitude);
            }
            for (int s = 0; s < 2; s++)
            {
                if (!dbg.armOn[s]) continue;
                wristMax = Mathf.Max(wristMax, dbg.wrist[s]);
                float hgt = Vector3.Dot(dbg.E[s] - dbg.S[s], up);
                elbowUp = Mathf.Max(elbowUp, hgt);
                float lat = Vector3.Dot(dbg.E[s] - dbg.S[s], cam.transform.right * (s == 1 ? 1f : -1f));
                elbowCross = Mathf.Max(elbowCross, -lat);
                for (int q = 1; q <= 8; q++)
                {
                    Vector3 pA = Vector3.Lerp(dbg.E[s], dbg.W[s], q / 8f);
                    armIn = Mathf.Max(armIn, 0.12f - DistSeg(pA, tA, tB));
                    if (q > 2) armIn = Mathf.Max(armIn, 0.11f - DistSeg(Vector3.Lerp(dbg.S[s], dbg.E[s], q / 8f), tA, tB));
                    foreach (var pt in new[] { pA, Vector3.Lerp(dbg.S[s], dbg.E[s], q / 8f) })
                    {
                        Vector3 c = cam.transform.InverseTransformPoint(pt);
                        if (c.z > 0f && c.z < 0.09f && InView(cam, pt)) nearView = Mathf.Max(nearView, 0.09f - c.z);
                    }
                }
            }
            // the physical hands: flung far off the gun, or thrown about
            foreach (var id in new[] { PartId.HandL, PartId.HandR })
            {
                var h = me[id];
                if (h == null || h.rb == null) continue;
                flingHand = Mathf.Max(flingHand, (h.rb.linearVelocity - me.Chest.rb.linearVelocity).magnitude);
            }
        }

        static bool InView(Camera cam, Vector3 w)
        {
            var vp = cam.WorldToViewportPoint(w);
            return vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f;
        }

        static float DistSeg(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        int InjuryCount()
        {
            var me = PlayerAvatar.Body;
            if (me == null) return 0;
            int n = 0; foreach (var bp in me.partList) n += bp.wounds.Count;
            return n;
        }

        void Report(string phase)
        {
            int snaps = wc.GetHoldDebug().snaps - snaps0, inj = InjuryCount() - injuries0;
            var bad = new List<string>();
            if (gunIn > 0.005f) bad.Add($"GUN IN TORSO {gunIn * 100f:0.0}cm");
            if (gunLag > 0.12f) bad.Add($"gun off its place {gunLag * 100f:0}cm");
            if (wristMax > 55f) bad.Add($"WRIST BENT {wristMax:0}deg");
            if (elbowUp > 0.03f) bad.Add($"ELBOW UP {elbowUp * 100f:0}cm over shoulder");
            if (elbowCross > 0.08f) bad.Add($"ELBOW ACROSS {elbowCross * 100f:0}cm");
            if (armIn > 0.01f) bad.Add($"ARM IN TORSO {armIn * 100f:0.0}cm");
            if (nearView > 0.005f) bad.Add($"IN YOUR FACE {nearView * 100f:0.0}cm");
            if (flingHand > 7f) bad.Add($"hand flung {flingHand:0.0}m/s");
            if (flingGun > 4f) bad.Add($"gun flung {flingGun:0.0}m/s");
            if (snaps > 0) bad.Add($"gun snapped back x{snaps}");
            if (downFrames > 0) bad.Add($"FELL ({downFrames} frames)");
            if (inj > 0) bad.Add($"{inj} new wounds");
            report.AppendLine($"  {phase,-16} wrist {wristMax,3:0}  lag {gunLag * 100f,4:0.0}cm  {(bad.Count == 0 ? "ok" : string.Join(", ", bad))}");
            foreach (var fl in falls) report.AppendLine("      ! " + fl);
            falls.Clear();
        }

        void Snapshot(int slot)
        {
            var cam = Camera.main;
            if (cam == null || slot >= 16) return;
            var rt = RenderTexture.GetTemporary(320, 200, 24);
            var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, 320, 200), (slot % 4) * 320, (3 - slot / 4) * 200);
            sheet.Apply();
            // and from the side: the whole stance (arms, gun, body)
            var dbg = wc.GetHoldDebug();
            var e = cam.transform;
            var go = new GameObject("SideCam"); var sc = go.AddComponent<Camera>(); sc.fieldOfView = 50f; sc.nearClipPlane = 0.02f; sc.cullingMask = cam.cullingMask;   // (what you see of yourself: not your hidden physical arms and head)
            Vector3 flat = Vector3.ProjectOnPlane(e.forward, Vector3.up).normalized, right = Vector3.Cross(Vector3.up, flat);
            Vector3 focus = e.position - Vector3.up * 0.35f + flat * 0.15f;
            go.transform.position = focus + right * 1.5f + flat * 0.4f + Vector3.up * 0.15f; go.transform.LookAt(focus);
            sc.targetTexture = rt; sc.Render(); sc.targetTexture = null;
            RenderTexture.active = rt;
            sheetOut.ReadPixels(new Rect(0, 0, 320, 200), (slot % 4) * 320, (3 - slot / 4) * 200);
            sheetOut.Apply();
            Destroy(go);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
        }

        readonly List<string> falls = new List<string>();
        void OnLog(string msg, string stack, LogType type)
        {
            if (msg.StartsWith("[Carl] YOU") && (msg.Contains("fell") || msg.Contains("going down") || msg.Contains("stagger"))) falls.Add(msg.Substring(11));   // ("[Carl] YOU " cut off)
        }

        void Finish()
        {
            ActiveRagdoll.DebugFalls = false;
            Application.logMessageReceived -= OnLog;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            var path = System.IO.Path.Combine(Application.dataPath, "../Temp/ArmTest.txt");
            System.IO.File.WriteAllText(path, report.ToString());
            Debug.Log("[ArmTest] done: " + path + "\n" + report);
            Current = null;
            Destroy(gameObject);
        }
    }
}
