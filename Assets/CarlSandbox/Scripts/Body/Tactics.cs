using UnityEngine;

namespace Carl
{
    /// <summary>Sides: 0 is yours (you and the friendly Earls, green armbands), 1 theirs (red armbands).</summary>
    public static class Teams
    {
        public const int Friendly = 0, Enemy = 1;

        /// <summary>Which side this body fights for: you are 0, an armed Earl his gunner's side, anyone else -1.</summary>
        /// <summary>Playtests: you're on nobody's side (so they fight each other, not you).</summary>
        public static bool PlayerOutOfIt;

        public static int Of(HumanBody h)
        {
            if (h == null) return -1;
            if (h == PlayerAvatar.Body) return PlayerOutOfIt ? -1 : Friendly;
            return h.gunner != null ? h.gunner.team : -1;
        }

        public static bool Hostile(int a, int b) => a >= 0 && b >= 0 && a != b;

        static Material green, red;

        /// <summary>Coloured tape round both upper arms: green ours, red theirs.</summary>
        static readonly System.Collections.Generic.Dictionary<Color, Material> tapes = new System.Collections.Generic.Dictionary<Color, Material>();
        /// <summary>Gaffer tape in that colour: a little sheen.</summary>
        static Material Tape(Color c)
        {
            if (!tapes.TryGetValue(c, out var m) || m == null) tapes[c] = m = Mats.Lit(c, 0.45f);
            return m;
        }

        public static void RemoveArmband(HumanBody h)
        {
            if (h == null) return;
            foreach (var id in new[] { PartId.UpperArmL, PartId.UpperArmR })
                if (h[id] != null && h[id].transform.Find("Armband") is Transform old) Object.Destroy(old.gameObject);
        }

        public static void Armband(HumanBody h, int team)
        {
            if (h == null) return;
            if (green == null) { green = Mats.Lit(new Color(0.12f, 0.62f, 0.16f), 0.15f); red = Mats.Lit(new Color(0.78f, 0.08f, 0.06f), 0.15f); }
            foreach (var id in new[] { PartId.UpperArmL, PartId.UpperArmR })
            {
                var bp = h[id];
                if (bp == null) continue;
                var old = bp.transform.Find("Armband");
                if (old != null) Object.Destroy(old.gameObject);
                var cap = bp.col as CapsuleCollider;
                Vector3 axis = Vector3.up, centre = Vector3.zero; float rad = 0.055f, len = 0.28f;
                if (cap != null)
                {
                    axis = cap.direction == 0 ? Vector3.right : cap.direction == 1 ? Vector3.up : Vector3.forward;
                    centre = cap.center; rad = cap.radius; len = cap.height;
                }
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = "Armband";
                Object.Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(bp.transform, false);
                // high on the arm, toward the shoulder (whichever end of the capsule is nearer the chest)
                Vector3 endA = bp.transform.TransformPoint(centre + axis * len * 0.22f), endB = bp.transform.TransformPoint(centre - axis * len * 0.22f);
                bool aNear = h.Chest == null || (endA - h.Chest.transform.position).sqrMagnitude < (endB - h.Chest.transform.position).sqrMagnitude;
                g.transform.localPosition = centre + axis * len * (aNear ? 0.22f : -0.22f);
                g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
                float d = rad * 2f * 1.18f;
                g.transform.localScale = new Vector3(d, 0.035f, d);
                g.GetComponent<MeshRenderer>().sharedMaterial = Nations.Active ? Tape(Nations.Band(Nations.Of(team))) : team == Friendly ? green : red;
                g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.layer = bp.gameObject.layer;
            }
        }
    }

    /// <summary>
    /// What the Earls learn without seeing you: shots heard (roughly where from), rounds cracking past them (the
    /// line they came down), a mate hit beside them (where it came from), footsteps; and a mate firing - they
    /// look where he is shooting.
    /// </summary>
    public static class Intel
    {
        static Vector3 Off(float d) { var r = Random.insideUnitCircle * d; return new Vector3(r.x, 0f, r.y); }

        /// <summary>
        /// How far off a guess from a sound (or a light) is, at a distance: right in front of him he knows near
        /// enough exactly; it grows faster than the distance, so far off it's only a general area.
        /// </summary>
        public static Vector3 Guess(float d, float lin, float sq) => Off(d * lin + d * d * sq);

        /// <summary>Counters for the playtests: shots fired by each side, and rounds that hit someone on the shooter's own side.</summary>
        public static readonly int[] Shots = new int[2];
        public static int OwnSideHits, SkyShots;

        /// <summary>A gunshot at <paramref name="pos"/> by someone on <paramref name="team"/>, heard out to <paramref name="radius"/>.</summary>
        public static void Gunshot(Vector3 pos, int team, float radius, HumanBody shooter, Vector3? shootingAt)
        {
            if (team >= 0 && team < 2) Shots[team]++;
            EarlGunner.Flashed(shooter);
            foreach (var g in EarlGunner.All)
            {
                if (g == null || !g.Fighting || g.Body == shooter) continue;
                float d = Vector3.Distance(g.Body.Center, pos);
                if (Teams.Hostile(g.team, team))
                {
                    if (d < radius) g.Learn(pos + Guess(d, 0.03f, 0.004f), 0.5f + 0.3f * (1f - d / radius), false);
                }
                else if (g.team == team && shootingAt.HasValue && d < 25f && !g.SeesEnemy)
                    g.Learn(shootingAt.Value, 0.5f, false);
            }
        }

        /// <summary>A round from <paramref name="origin"/> travelling from a along move: anyone it cracks past knows where it came from.</summary>
        public static void BulletPassed(Vector3 origin, int team, Vector3 a, Vector3 move)
        {
            if (team < 0) return;
            float m2 = move.sqrMagnitude;
            if (m2 < 1e-6f) return;
            foreach (var g in EarlGunner.All)
            {
                if (g == null || !Teams.Hostile(g.team, team) || !g.Fighting) continue;
                Vector3 c = g.Body.Center;
                float t = Mathf.Clamp01(Vector3.Dot(c - a, move) / m2);
                if ((a + move * t - c).sqrMagnitude > 6.25f) continue;
                float d = Vector3.Distance(c, origin);
                g.Learn(origin + Off(d * 0.06f), 0.75f, false);
                g.Suppress(0.35f);
            }
        }

        /// <summary>Somebody on a side was hit by a round from <paramref name="origin"/>: he and those near him know the angle.</summary>
        public static void Wounded(HumanBody victim, Vector3 origin, int shooterTeam)
        {
            int side = Teams.Of(victim);
            if (side >= 0 && side == shooterTeam) OwnSideHits++;
            if (side < 0 || !Teams.Hostile(side, shooterTeam)) return;
            Vector3 v = victim.Center;
            foreach (var g in EarlGunner.All)
            {
                if (g == null || g.team != side || !g.Fighting) continue;
                float d = Vector3.Distance(g.Body.Center, v);
                if (d > 30f) continue;
                if (g.Body != victim && d > 10f && !EarlGunner.Clear(g.Body.Head.transform.position, v, g.Body)) continue;
                float od = Vector3.Distance(g.Body.Center, origin);
                g.Learn(origin + Off(od * 0.05f), 0.8f, false);
                if (g.Body == victim) g.Suppress(0.6f);
            }
        }

        /// <summary>A noise someone on a side made (footsteps, a shout).</summary>
        public static void Noise(Vector3 pos, int team, float radius)
        {
            foreach (var g in EarlGunner.All)
            {
                if (g == null || !Teams.Hostile(g.team, team) || !g.Fighting) continue;
                float d = Vector3.Distance(g.Body.Center, pos);
                if (d < radius) g.Learn(pos + Guess(d, 0.06f, 0.012f), Mathf.Lerp(0.55f, 0.3f, d / radius), false);
            }
        }
    }
}
