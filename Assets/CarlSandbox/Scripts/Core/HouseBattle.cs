using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The Houses map as a fight instead of a quiet street.
    ///   HOOD: a gang war - Crips (blue) against Bloods (red), you in one of them. Pistols mostly, some with
    ///         extended magazines, a few Glocks switched to full auto, the odd AK.
    ///   WAR:  Ukraine against Russia on a shelled, half-ruined street. ATTACK: they hold the houses up the street
    ///         and you push up with your squad. DEFEND: you and your squad hold the houses at your end and they
    ///         come down the street at you in waves.
    /// </summary>
    public class HouseBattle : MonoBehaviour
    {
        public static HouseBattle I;
        public const int Normal = 0, Hood = 1, War = 2;
        public const int Crips = 0, Bloods = 1;
        bool defending;
        float nextWave;
        int waves;

        /// <summary>The houses map's mode right now (Normal anywhere else).</summary>
        public static int Mode;

        public static void Stop() { if (I != null) Destroy(I.gameObject); I = null; Mode = Normal; }

        /// <summary>Sets up the fight on the houses just built: who is where, dressed and armed how.</summary>
        public static void Begin(Vector3 spawn, Vector3[] houseSpots, int mode)
        {
            Stop();
            Mode = mode;
            if (mode == Normal) return;
            I = new GameObject("HouseBattle").AddComponent<HouseBattle>();
            var g = SandboxGame.Instance;
            // the two ends of the street: your end (south, where you come in) and theirs (north)
            var mine = new List<Vector3>(); var theirs = new List<Vector3>();
            foreach (var p in houseSpots) (p.z < -14f ? mine : theirs).Add(p);
            if (mode == Hood)
            {
                // your gang round you on the street, theirs in and round the houses further up
                for (int k = 0; k < 5; k++) Gangster(spawn + new Vector3(Random.Range(-3f, 3f), 0f, 3f + k * 1.6f), Teams.Friendly);
                int n = 0;
                foreach (var p in theirs) { if (n++ >= 9) break; Gangster(p, Teams.Enemy); }
                for (int k = 0; k < 3; k++) Gangster(new Vector3(Random.Range(-3f, 3f), 0.05f, Random.Range(20f, 45f)), Teams.Enemy);
                g.Toast($"HOOD: {(GameSettings.Gang == Crips ? "CRIPS" : "BLOODS")} VS {(GameSettings.Gang == Crips ? "BLOODS" : "CRIPS")}");
                return;
            }
            I.defending = !GameSettings.HouseAttack;
            if (I.defending)
            {
                // you hold the houses at your end: your squad in them with you; they come down the street
                foreach (var p in mine) Soldier(p, Teams.Friendly, true);
                I.nextWave = Time.time + 12f;
                g.Toast("WAR - DEFEND: HOLD THE HOUSES  ·  THEY ARE COMING DOWN THE STREET");
            }
            else
            {
                // they hold the houses up the street; you go in with your squad
                for (int k = 0; k < 8; k++) Soldier(spawn + new Vector3(Random.Range(-3f, 3f), 0f, 2f + k * 1.5f), Teams.Friendly, false);
                int n = 0;
                foreach (var p in theirs) { if (n++ >= 10) break; Soldier(p, Teams.Enemy, true); }
                g.Toast("WAR - ATTACK: CLEAR THE HOUSES UP THE STREET");
            }
        }

        static HumanBody Body(Vector3 at, float yaw)
        {
            var h = SandboxGame.Instance.SpawnCarl(at, yaw);
            if (h != null) h.brain.aiEnabled = true;
            return h;
        }

        /// <summary>A gang member: his gang's colour of T-shirt, jeans, sneakers, and a pistol (mostly).</summary>
        static void Gangster(Vector3 at, int team)
        {
            var h = Body(at, Random.Range(0f, 360f));
            if (h == null) return;
            int gang = team == Teams.Friendly ? GameSettings.Gang : 1 - GameSettings.Gang;
            h.SetOutfit(gang == Crips ? 4 : 5);
            var kit = new WeaponLoadout();
            EarlWeapon w;
            float r = Random.value;
            if (r < 0.5f) { w = EarlWeapon.Pistol; kit.extMag = Random.value < 0.35f; }
            else if (r < 0.72f) w = EarlWeapon.Beretta;
            else if (r < 0.9f) { w = EarlWeapon.Pistol; kit.autoSwitch = true; kit.extMag = Random.value < 0.6f; }
            else w = EarlWeapon.AK47;
            var gn = EarlGunner.Arm(h, w, kit, team);
            if (gn == null) return;
            Teams.RemoveArmband(h);
            gn.grenades = 0;
            gn.leans = Random.value < 0.5f;
        }

        /// <summary>A soldier of either army (uniform, tape and rifle as in the trenches).</summary>
        static void Soldier(Vector3 at, int team, bool hold)
        {
            var h = Body(at, team == Teams.Friendly ? 0f : 180f);
            if (h == null) return;
            int nation = Nations.Of(team);
            ArmourSystem.Equip(h, TrenchSquad.RandomKit(nation));
            var w = Nations.RandomWeapon(nation);
            var gn = EarlGunner.Arm(h, w, GunInfo.Lmg(GunInfo.Of(w)?.name) ? null : GunKit.Random(true), team);
            if (gn == null) return;
            gn.grenades = Random.value < 0.8f ? Random.Range(1, 3) : 0;
            gn.leans = Random.value < 0.6f;
            if (hold) { gn.defend = true; gn.holdPoint = at; }
        }

        void Update()
        {
            if (!defending || Time.time < nextWave || waves >= 8) return;
            // the next wave down the street: four to six of them, coming for the houses (and you)
            waves++;
            nextWave = Time.time + Random.Range(35f, 50f);
            int n = Random.Range(4, 7);
            for (int k = 0; k < n; k++) Soldier(new Vector3(Random.Range(-4f, 4f), 0.05f, 52f + k * 1.5f), Teams.Enemy, false);
            SandboxGame.Instance?.Toast($"WAVE {waves}: {n} COMING DOWN THE STREET");
        }
    }
}
