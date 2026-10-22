using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    public enum SquadRole { Point, Follow, Hold }

    /// <summary>
    /// The Earls holding the trenches: small squads of riflemen in mixed kit (6B45 vests with whatever parts of
    /// protection they have, 6B47 helmets, now and then an Altyn), AKs set up however each of them likes. They are
    /// placed well away from you and out of your sight, so you have to go and find them.
    ///
    /// A squad moves like people in a trench: the point man goes first, the rest follow in file a few metres
    /// apart (they wait behind someone rather than climb over him), and some stay where they are and let you come
    /// to them. Only some of them lean out round corners.
    /// </summary>
    public class TrenchSquad
    {
        public static readonly List<TrenchSquad> All = new List<TrenchSquad>();
        public readonly List<EarlGunner> members = new List<EarlGunner>();
        /// <summary>Cover spots taken (so two of them don't crowd into the same one).</summary>
        static readonly List<(EarlGunner who, Vector3 at)> claims = new List<(EarlGunner, Vector3)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); claims.Clear(); }

        public static void Clear() { All.Clear(); claims.Clear(); }

        // ------------------------------------------------------------ who goes where in the file

        /// <summary>The one a follower keeps behind: the next one up the file who is still in the fight.</summary>
        public EarlGunner Ahead(EarlGunner me)
        {
            EarlGunner prev = null;
            foreach (var m in members)
            {
                if (m == me) return prev;
                if (Active(m) && m.role != SquadRole.Hold) prev = m;
            }
            return prev;
        }

        public static bool Active(EarlGunner m) => m != null && m.Fighting;

        /// <summary>The point man is down: the next one in the file takes over.</summary>
        public void Promote()
        {
            foreach (var m in members) if (Active(m) && m.role == SquadRole.Point) return;
            foreach (var m in members) if (Active(m) && m.role == SquadRole.Follow) { m.role = SquadRole.Point; return; }
        }

        public static void Claim(EarlGunner who, Vector3? at)
        {
            claims.RemoveAll(c => c.who == who || c.who == null);
            if (at.HasValue) claims.Add((who, at.Value));
        }

        public static bool Taken(EarlGunner who, Vector3 at, float r = 1.8f)
        {
            foreach (var c in claims) if (c.who != who && c.who != null && Vector3.Distance(c.at, at) < r) return true;
            return false;
        }

        // ------------------------------------------------------------ manning the trenches

        /// <summary>Puts squads in the trenches (TRENCHES map), away from where you start and out of your sight.</summary>
        public static int Populate(Vector3 player, int squads = 3)
        {
            Clear();
            var spots = MapBuilder.TrenchSpots;
            if (spots.Count == 0 || SandboxGame.Instance == null) return 0;
            Vector3 eye = player + Vector3.up * 1.6f;
            bool Hidden(Vector3 p)
            {
                Vector3 head = p + Vector3.up * 1.55f, d = head - eye;
                return Physics.Raycast(eye, d.normalized, d.magnitude - 0.2f, ~0, QueryTriggerInteraction.Ignore);
            }
            var ok = spots.FindAll(p => Vector3.Distance(p, player) > 35f && Hidden(p));
            int placed = 0;
            var anchors = new List<Vector3>();
            for (int s = 0; s < squads && ok.Count > 0; s++)
            {
                // a squad's spot: well apart from the other squads
                Vector3 a = default; bool found = false;
                for (int tries = 0; tries < 40 && !found; tries++)
                {
                    a = ok[Random.Range(0, ok.Count)];
                    found = true;
                    foreach (var o in anchors) if (Vector3.Distance(o, a) < 18f) { found = false; break; }
                }
                if (!found) continue;
                anchors.Add(a);
                var sq = new TrenchSquad();
                All.Add(sq);
                // the rest of the squad on the trench floor round about, a few metres apart
                var near = ok.FindAll(p => Vector3.Distance(p, a) < 10f);
                near.Sort((x, y) => Vector3.Distance(x, a).CompareTo(Vector3.Distance(y, a)));
                int n = Random.Range(2, 5);
                var used = new List<Vector3>();
                foreach (var p in near)
                {
                    if (used.Count >= n) break;
                    bool clash = false;
                    foreach (var u in used) if (Vector3.Distance(u, p) < 2.6f) { clash = true; break; }
                    if (clash) continue;
                    used.Add(p);
                }
                for (int k = 0; k < used.Count; k++)
                {
                    Vector3 face = Vector3.ProjectOnPlane(player - used[k], Vector3.up);
                    float yaw = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face).eulerAngles.y : 0f;
                    var h = SandboxGame.Instance.SpawnCarl(used[k], yaw);
                    if (h == null) break;
                    ArmourSystem.Equip(h, RandomKit());
                    h.brain.aiEnabled = true;
                    var g = EarlGunner.Arm(h, EarlWeapon.AK47, GunKit.Random(true));
                    if (g == null) continue;
                    g.squad = sq;
                    // the first is the point man; of the rest about a third stay put and wait for you
                    g.role = k == 0 ? SquadRole.Point : Random.value < 0.35f ? SquadRole.Hold : SquadRole.Follow;
                    g.leans = Random.value < 0.55f;
                    g.holdPoint = used[k];
                    sq.members.Add(g);
                    placed++;
                }
            }
            return placed;
        }

        /// <summary>What a man in the trenches has on: a 6B45 with some of its parts, usually a 6B47, rarely an Altyn.</summary>
        public static ArmourLoadout RandomKit()
        {
            var L = new ArmourLoadout { vest = VestKind.Russian6B45 };
            L.plateClass = Random.value < 0.25f ? 3 : Random.value < 0.8f ? 4 : 5;
            L.sideArmour = Random.value < 0.55f;
            L.groinFlap = Random.value < 0.45f;
            L.collar = Random.value < 0.5f;
            L.pouches = Random.value < 0.8f;
            L.armGuards = Random.value < 0.25f;
            float r = Random.value;
            if (r < 0.12f) { L.helmet = HelmetKind.Spetsnaz; L.visor = Random.value < 0.8f; L.visorDown = Random.value < 0.6f; }
            else if (r < 0.92f) L.helmet = HelmetKind.Ratnik;
            else L.helmet = HelmetKind.None;
            return L;
        }
    }
}
