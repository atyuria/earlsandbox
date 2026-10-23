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
            SplitIntoGroups();
        }

        static int nextGroup;

        /// <summary>
        /// The men going across who haven't been told where yet are split into little groups of two to four, each
        /// sent at a different stretch of the other side's line (and so down a different trench to get there), so
        /// the attack comes in at several places at once. A group goes over the top together, or not at all.
        /// </summary>
        void SplitIntoGroups()
        {
            for (int team = 0; team < 2; team++)
            {
                var fresh = new System.Collections.Generic.List<EarlGunner>();
                foreach (var g in EarlGunner.All)
                    if (g != null && g.team == team && g.assault && g.group < 0 && g.Body != null && g.Fighting) fresh.Add(g);
                if (fresh.Count == 0) continue;
                // how wide their line is
                float minX = float.MaxValue, maxX = float.MinValue;
                if (TrenchNav.Ready)
                    for (int i = 0; i < TrenchNav.Nodes.Count; i++)
                    {
                        var n = TrenchNav.Nodes[i];
                        if (TrenchNav.Field[i] || !EarlGunner.EnemyLineNode(team, n)) continue;
                        minX = Mathf.Min(minX, n.x); maxX = Mathf.Max(maxX, n.x);
                    }
                if (minX > maxX) { minX = -30f; maxX = 30f; }
                // neighbours together: ordered along the line, then cut into groups
                fresh.Sort((a, b) => a.Body.Center.x.CompareTo(b.Body.Center.x));
                var lanes = new System.Collections.Generic.List<float>();
                int groups = Mathf.Max(1, Mathf.CeilToInt(fresh.Count / 3f));
                int laneCount = Mathf.Max(groups, 3);
                for (int l = 0; l < laneCount; l++) lanes.Add(Mathf.Lerp(minX + 4f, maxX - 4f, (l + 0.5f) / laneCount));
                // a random start so one wave after another doesn't always pick the same stretches
                int start = Random.Range(0, laneCount);
                bool topTaken = false;
                int idx = 0;
                for (int k = 0; k < groups; k++)
                {
                    int size = k == groups - 1 ? fresh.Count - idx : Mathf.Clamp(Random.Range(2, 5), 1, fresh.Count - idx);
                    if (size <= 0) break;
                    float lane = lanes[(start + k * Mathf.Max(1, laneCount / groups)) % laneCount] + Random.Range(-3f, 3f);
                    // one group in a while goes straight across the open; the rest by the trenches
                    bool top = !topTaken && groups > 1 && Random.value < 0.45f;
                    if (top) topTaken = true;
                    int id = nextGroup++;
                    for (int m = 0; m < size && idx < fresh.Count; m++, idx++)
                    {
                        var g = fresh[idx];
                        g.group = id;
                        g.laneX = lane;
                        g.overTheTop = top;
                    }
                }
            }
        }

        static readonly Unity.Profiling.ProfilerMarker PM_Update = new Unity.Profiling.ProfilerMarker("Carl.TrenchBattle.Update");

        void Update()
        {

            using var pmScope = PM_Update.Auto();
            if (Attacking || SandboxGame.Instance == null || SandboxGame.Instance.player == null) return;
            if (Time.time < nextWave) return;
            nextWave = Time.time + Random.Range(40f, 60f);
            // another wave comes across while there aren't too many of them already
            int alive = 0;
            foreach (var g in EarlGunner.All) if (g != null && g.team == Teams.Enemy && g.Fighting) alive++;
            if (alive >= 12) return;
            TrenchSquad.AddSquadSpread(SandboxGame.Instance.player.transform.position, n =>
            {
                if (n == 0 || !enabled) return;
                waves++;
                Assign();
                SandboxGame.Instance.Toast($"WAVE {waves + 1}: {n} MORE COMING ACROSS");
            });
        }
    }
}
