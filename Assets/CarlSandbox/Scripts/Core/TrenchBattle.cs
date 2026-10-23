using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The TRENCHES battle, chosen in the main menu.
    ///   DEFEND: you and your side hold your front line (each man on his spot); they come across at you, some over
    ///           the top straight across no-man's-land, some by the middle, in waves until you stop playing.
    ///   ATTACK: you and your side go across and take their front line; they hold it.
    /// </summary>
    public class TrenchBattle : MonoBehaviour
    {
        static TrenchBattle I;
        public static bool Attacking { get; private set; }
        float nextWave;
        int waves;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; Attacking = false; }

        public static void Begin(bool attack)
        {
            if (I == null) I = new GameObject("TrenchBattle").AddComponent<TrenchBattle>();
            Attacking = attack;
            I.enabled = true;
            I.waves = 0;
            I.nextWave = Time.time + 45f;
            I.Assign();
        }

        public static void Stop() { if (I != null) I.enabled = false; }

        /// <summary>Gives every armed man on the map his job for the mode.</summary>
        void Assign()
        {
            // of the men going across, only one or two (per side) climb out and cross the open: the rest work
            // their way over along the communication trenches
            int[] overTop = new int[2];
            int[] allowed = { Random.Range(1, 3), Random.Range(1, 3) };
            foreach (var g in EarlGunner.All)
                if (g != null && g.assault && g.overTheTop && g.Fighting && g.team >= 0 && g.team < 2) overTop[g.team]++;
            foreach (var g in EarlGunner.All)
            {
                if (g == null) continue;
                bool attacker = Attacking ? g.team == Teams.Friendly : g.team == Teams.Enemy;
                bool wasAssault = g.assault;
                g.assault = attacker && !g.medic;
                g.defend = !attacker;
                if (g.assault && !wasAssault && g.team >= 0 && g.team < 2)
                {
                    g.overTheTop = overTop[g.team] < allowed[g.team];
                    if (g.overTheTop) overTop[g.team]++;
                }
                else if (!g.assault) g.overTheTop = false;
                if (g.defend && g.holdPoint == Vector3.zero && g.Body != null) g.holdPoint = g.Body.Pelvis.transform.position - Vector3.up * 0.95f;
            }
        }

        void Update()
        {
            if (Attacking || SandboxGame.Instance == null || SandboxGame.Instance.player == null) return;
            if (Time.time < nextWave) return;
            nextWave = Time.time + Random.Range(40f, 60f);
            // another wave comes across while there aren't too many of them already
            int alive = 0;
            foreach (var g in EarlGunner.All) if (g != null && g.team == Teams.Enemy && g.Fighting) alive++;
            if (alive >= 12) return;
            int n = TrenchSquad.AddSquad(SandboxGame.Instance.player.transform.position);
            if (n == 0) return;
            waves++;
            Assign();
            SandboxGame.Instance.Toast($"WAVE {waves + 1}: {n} MORE COMING ACROSS");
        }
    }
}
