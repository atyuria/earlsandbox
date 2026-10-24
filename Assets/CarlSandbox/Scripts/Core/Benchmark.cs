using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Frame rate measured in a real build: start the game with -benchmark and it skips the menu, loads the
    /// TRENCHES battle (defend, daylight) once and then takes turns, three times over: five seconds with the
    /// battle going on round you, five seconds of you firing full auto into an Earl in front of you (rounds landing
    /// on him). Same battle for both, so what firing costs shows up clearly. Every frame time is recorded; it
    /// writes benchmark.txt next to the game (average fps, 1% lows, worst frame for each) and quits.
    /// With -probe as well (a development build), perf.txt says what firing adds, system by system.
    /// </summary>
    public class Benchmark : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Check()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-benchmark") < 0) return;
            new GameObject("Benchmark").AddComponent<Benchmark>();
        }

        readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
        readonly List<float>[] dts = { new List<float>(), new List<float>(), new List<float>(), new List<float>() };

        static readonly Dictionary<string, string> edArgs = new Dictionary<string, string>();

        /// <summary>The same run from inside the editor (play mode): graphics setting, report file name.</summary>
        public static void Run(int graphics, string outName)
        {
            edArgs.Clear(); edArgs["-graphics"] = graphics.ToString(); edArgs["-out"] = outName;
            new GameObject("Benchmark").AddComponent<Benchmark>();
        }

        static string Arg(string name)
        {
            if (edArgs.TryGetValue(name, out var v)) return v;
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            while (SandboxGame.Instance == null) yield return null;
            yield return null;
            var g = SandboxGame.Instance;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            // -graphics 0/1/2: run at that setting (low / medium / high)
            if (int.TryParse(Arg("-graphics") ?? "", out int gq)) { GameSettings.Graphics = gq; GameSettings.ApplyGraphics(); }
            Random.InitState(1234);
            report.AppendLine($"EARL SANDBOX BENCHMARK  {System.DateTime.Now}");
            report.AppendLine($"{SystemInfo.processorType}, {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}, graphics {GameSettings.Graphics}, blood quality {GameSettings.QualityName}");
            GameSettings.TimeOfDay = 0; GameSettings.TrenchAttack = false; GameSettings.SpawnNpcs = true;
            bool inf = GameSettings.InfiniteAmmo; GameSettings.InfiniteAmmo = true;
            var menus = FindFirstObjectByType<Menus>();
            if (menus != null) menus.open = MenuKind.None;
            g.LoadMap(3);
            yield return new WaitForSeconds(4f);
            var w = g.weapons; int ak = w.defs.FindIndex(x => x.name == "AK-47"); w.Give(ak, true); w.Select(ak);
            // a picture of what is on screen (proof it renders), next to the report
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../" + System.IO.Path.ChangeExtension(Arg("-out") ?? "benchmark.txt", ".png")));
            HumanBody target = null;
            bool probe = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-probe") >= 0;
            PerfProbe pp = null;
            if (probe) { var _m = Prof.Hit; pp = PerfProbe.RunSplit(); }
            bool spikeProbe = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-spike") >= 0;
            // -probepile: the split probe compares the heap with and without firing into it (instead of the battle)
            bool probePile = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-probepile") >= 0;
            PerfProbe spikeP = null;
            if (spikeProbe) { PerfProbe.OutName = "perf_spike.txt"; spikeP = PerfProbe.RunSpike(int.MaxValue); PerfProbe.OutName = "perf.txt"; }
            yield return null;
            for (int round = 0; round < 3; round++)
            {
                // not firing
                WeaponController.ForceFire = false; PerfProbe.Bucket = probePile ? -1 : 0; PerfProbe.Phase = "battle";
                yield return Record(0, 5f, null);
                // firing: an Earl in front of you (a fresh one if the last went down), trigger held, rounds landing
                PerfProbe.Phase = "spawning a target (not recorded)";
                if (target == null || target.Dead || target.ragdoll.IsDown) target = Target(g);
                WeaponController.ForceFire = true; PerfProbe.Bucket = probePile ? -1 : 1; PerfProbe.Phase = "battle, firing";
                yield return Record(1, 5f, target);
            }
            WeaponController.ForceFire = false; PerfProbe.Bucket = -1;
            PerfProbe.Phase = "heaping the dead (not recorded)";
            // after a wave: thirty dead heaped in front of you, the battle still going on
            var me = g.player.transform.position; var fw = g.player.transform.forward;
            var pile = new List<HumanBody>();
            for (int i = 0; i < 30; i++)
            {
                Vector3 p = me + fw * Random.Range(3f, 9f) + Vector3.Cross(Vector3.up, fw) * Random.Range(-3f, 3f) + Vector3.up * Random.Range(0.3f, 1.5f);
                var h = g.SpawnCarl(p, Random.Range(0f, 360f));
                if (h == null) break;
                pile.Add(h);
                if (i % 2 == 0) ArmourSystem.Equip(h, TrenchSquad.RandomKit());
                h.physio.Kill("BENCHMARK", false);
            }
            PerfProbe.Phase = "the pile settling (not recorded)";
            yield return new WaitForSeconds(6f);
            for (int round = 0; round < 2; round++)
            {
                WeaponController.ForceFire = false; PerfProbe.Phase = "pile"; if (probePile) PerfProbe.Bucket = 0;
                yield return Record(2, 5f, null);
                WeaponController.ForceFire = true; PerfProbe.Phase = "pile, firing"; if (probePile) PerfProbe.Bucket = 1;
                yield return Record(3, 5f, null, pile);
            }
            WeaponController.ForceFire = false;
            GameSettings.InfiniteAmmo = inf;
            Report("trench battle, not firing", dts[0]);
            Report("trench battle, firing full auto into an Earl", dts[1]);
            Report("thirty dead heaped in front of you, battle going on", dts[2]);
            Report("thirty dead heaped in front of you, firing into them", dts[3]);
            if (pp != null) pp.Finish();
            if (spikeP != null) spikeP.Write();
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../" + (Arg("-out") ?? "benchmark.txt")), report.ToString());
            yield return null;
            if (Application.isEditor) { edArgs.Clear(); Destroy(gameObject); }
            Application.Quit();
        }

        static HumanBody Target(SandboxGame g)
        {
            var me = g.player.transform.position; var fw = g.player.transform.forward;
            var h = g.SpawnCarl(me + fw * 9f, Quaternion.LookRotation(-fw).eulerAngles.y);
            if (h == null) return null;
            ArmourSystem.Equip(h, TrenchSquad.RandomKit());
            h.brain.aiEnabled = true;
            EarlGunner.Arm(h, EarlWeapon.AK47, null, Teams.Enemy);
            return h;
        }

        IEnumerator Record(int bucket, float seconds, HumanBody target, List<HumanBody> pile = null)
        {
            float t0 = Time.realtimeSinceStartup, next = 0f, switchAt = 0f;
            int pi = 0;
            yield return null;   // (the frame the switch happened in isn't counted)
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                // firing into the heap: on one body, then the next every half second
                if (pile != null && pile.Count > 0 && Time.time >= switchAt) { switchAt = Time.time + 0.5f; pi = (pi + 1) % pile.Count; target = pile[pi]; }
                // the gun stays on him: held down, the recoil would walk the rounds up into the sky and hit nothing
                // (aiming also settles the recoil, as you would pull it back down)
                if (target != null && target.Chest != null && WeaponController.ForceFire)
                    SandboxGame.Instance.player.LookAt(target.Chest.transform.position);
                if (target != null && target.Chest != null && Time.time >= next)
                {
                    // (recoil walks your own rounds off him: these land, ten a second, like a burst that connects)
                    next = Time.time + 0.1f;
                    Vector3 from = Camera.main.transform.position;
                    Vector3 at = target.Chest.transform.position + Random.insideUnitSphere * 0.3f;
                    Ballistics.Instance.Fire(from, (at - from).normalized, 715f, 0.0079f, 0.0079f, HitKind.Bullet, false);
                }
                yield return null;
                dts[bucket].Add(Time.unscaledDeltaTime);
            }
        }

        void Report(string what, List<float> d)
        {
            if (d.Count == 0) return;
            d.Sort();
            float sum = 0f; foreach (var x in d) sum += x;
            float avg = sum / d.Count;
            int n1 = Mathf.Max(1, d.Count / 100);
            float low = 0f; for (int i = d.Count - n1; i < d.Count; i++) low += d[i]; low /= n1;
            report.AppendLine($"{what}: {1f / avg:0} fps average, {1f / low:0} fps 1% low, worst frame {d[d.Count - 1] * 1000f:0} ms ({d.Count} frames, {HumanBody.All.Count} bodies)");
        }
    }
}
