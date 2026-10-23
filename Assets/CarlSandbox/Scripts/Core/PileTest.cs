using System.Collections;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Performance playtest of holding a trench: the TRENCHES battle going on round you, a pile of dead Earls
    /// in front of you (as after a wave), and you firing into it. PerfProbe averages where the frame goes:
    /// Temp/perf.txt (or perf_pile.txt next to a build). PileTest.Run(bodies, seconds) from the editor.
    /// </summary>
    public class PileTest : MonoBehaviour
    {
        public static void Run(int bodies = 25, float seconds = 6f) { var t = new GameObject("PileTest").AddComponent<PileTest>(); t.bodies = bodies; t.seconds = seconds; }
        public static bool Done;
        /// <summary>Report the worst frame (and what it spent its time on) instead of averages.</summary>
        public static bool Spike;
        int bodies;
        float seconds;

        IEnumerator Start()
        {
            Done = false;
            var g = SandboxGame.Instance;
            bool npcs = GameSettings.SpawnNpcs; GameSettings.SpawnNpcs = true;
            g.LoadMap(3);
            GameSettings.SpawnNpcs = npcs;
            yield return new WaitForSeconds(1.5f);
            var me = g.player.transform.position; var fw = g.player.transform.forward;
            // the dead of a wave, heaped in front of you along the trench and on the lip
            for (int i = 0; i < bodies; i++)
            {
                Vector3 p = me + fw * Random.Range(3f, 9f) + Vector3.Cross(Vector3.up, fw) * Random.Range(-3f, 3f) + Vector3.up * Random.Range(0.3f, 1.5f);
                var h = g.SpawnCarl(p, Random.Range(0f, 360f));
                if (h == null) break;
                if (i % 2 == 0) ArmourSystem.Equip(h, TrenchSquad.RandomKit());
                h.physio.Kill("TEST", false);
            }
            yield return new WaitForSeconds(4f);
            var w = g.weapons; int ak = w.defs.FindIndex(x => x.name == "AK-47"); w.Give(ak, true); w.Select(ak);
            bool inf = GameSettings.InfiniteAmmo; GameSettings.InfiniteAmmo = true;
            yield return new WaitForSeconds(0.5f);
            PerfProbe.OutName = "perf_pile.txt";
            if (Spike) PerfProbe.RunSpike(Mathf.RoundToInt(seconds * 60f)); else PerfProbe.Run(Mathf.RoundToInt(seconds * 60f));
            WeaponController.ForceFire = true;
            float t0 = Time.time;
            while (Time.time - t0 < seconds * 4f && FindFirstObjectByType<PerfProbe>() != null) yield return null;
            WeaponController.ForceFire = false;
            GameSettings.InfiniteAmmo = inf;
            PerfProbe.OutName = "perf.txt";
            Done = true;
            Destroy(gameObject);
        }
    }
}
