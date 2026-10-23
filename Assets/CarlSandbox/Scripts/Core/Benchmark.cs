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
        readonly List<float>[] dts = { new List<float>(), new List<float>() };

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            while (SandboxGame.Instance == null) yield return null;
            yield return null;
            var g = SandboxGame.Instance;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
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
            HumanBody target = null;
            bool probe = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-probe") >= 0;
            PerfProbe pp = null;
            if (probe) { var _m = Prof.Hit; pp = PerfProbe.RunSplit(); }
            yield return null;
            for (int round = 0; round < 3; round++)
            {
                // not firing
                WeaponController.ForceFire = false; PerfProbe.Bucket = 0;
                yield return Record(0, 5f, null);
                // firing: an Earl in front of you (a fresh one if the last went down), trigger held, rounds landing
                if (target == null || target.Dead || target.ragdoll.IsDown) target = Target(g);
                WeaponController.ForceFire = true; PerfProbe.Bucket = 1;
                yield return Record(1, 5f, target);
            }
            WeaponController.ForceFire = false; PerfProbe.Bucket = -1;
            GameSettings.InfiniteAmmo = inf;
            Report("trench battle, not firing", dts[0]);
            Report("trench battle, firing full auto into an Earl", dts[1]);
            if (pp != null) pp.Finish();
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../benchmark.txt"), report.ToString());
            yield return null;
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

        IEnumerator Record(int bucket, float seconds, HumanBody target)
        {
            float t0 = Time.realtimeSinceStartup, next = 0f;
            yield return null;   // (the frame the switch happened in isn't counted)
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
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
