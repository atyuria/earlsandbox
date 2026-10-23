using System.Collections;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Performance playtest of a gunfight: one armed Earl a little way off who shoots back, and you hosing him and
    /// the wall behind him with full auto (as real rounds, through the normal hit path) for a few seconds while
    /// PerfProbe averages where the frame goes. Temp/perf.txt.
    /// </summary>
    public class FirefightTest : MonoBehaviour
    {
        public static void Run(float seconds = 6f, int map = 0, bool probe = true) { var t = new GameObject("FirefightTest").AddComponent<FirefightTest>(); t.seconds = seconds; t.map = map; t.probe = probe; }
        bool probe;
        float seconds;
        int map;

        IEnumerator Start()
        {
            var g = SandboxGame.Instance;
            bool npcs = GameSettings.SpawnNpcs; GameSettings.SpawnNpcs = false;
            if (map == 3) GameSettings.SpawnNpcs = true;   // (the trenches with the whole battle going on round you)
            g.LoadMap(map);
            GameSettings.SpawnNpcs = npcs;
            yield return new WaitForSeconds(1f);
            var me = g.player.transform.position;
            var fw = g.player.transform.forward; var h = g.SpawnCarl(me + fw * (map == 3 ? 9f : 14f), Quaternion.LookRotation(-fw).eulerAngles.y);
            ArmourSystem.Equip(h, TrenchSquad.RandomKit());
            h.brain.aiEnabled = true;
            EarlGunner.Arm(h, EarlWeapon.AK47, null, Teams.Enemy);
            yield return new WaitForSeconds(2f);
            // your own AK, trigger held down (the real firing path: recoil, casings, flash, smoke, sound), at him
            var w = g.weapons; int ak = w.defs.FindIndex(x => x.name == "AK-47"); w.Give(ak, true); w.Select(ak);
            bool inf = GameSettings.InfiniteAmmo; GameSettings.InfiniteAmmo = true;
            yield return new WaitForSeconds(0.8f);
            if (probe) PerfProbe.Run(Mathf.RoundToInt(seconds * 60f));
            WeaponController.ForceFire = true;
            // (recoil walks your own rounds off him: these land, ten a second, like a burst that connects)
            float t0 = Time.time, next = 0f;
            while (Time.time - t0 < seconds + 1f)
            {
                if (Time.time >= next && h != null && h.Chest != null)
                {
                    next = Time.time + 0.1f;
                    Vector3 from = Camera.main.transform.position;
                    Vector3 at = h.Chest.transform.position + Random.insideUnitSphere * 0.3f;
                    Ballistics.Instance.Fire(from, (at - from).normalized, 715f, 0.0079f, 0.0079f, HitKind.Bullet, false);
                }
                yield return null;
            }
            WeaponController.ForceFire = false;
            GameSettings.InfiniteAmmo = inf;
            Destroy(gameObject);
        }
    }
}
