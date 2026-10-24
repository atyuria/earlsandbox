using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// How an armed Earl fights when he can't see anyone: what he knows (seen, heard, shot at from), and what he
    /// does about it - peek the angle or hold it, prefire where you should be, push in and clear it, or put a
    /// grenade into the corner you've gone quiet in. And what he does about a grenade at his own feet.
    /// Around the trenches he walks the TrenchNav paths (ladders and all); with nothing going on he wanders his
    /// own lines, or (yours) pushes over to theirs.
    /// </summary>
    public partial class EarlGunner
    {
        public static readonly List<EarlGunner> All = new List<EarlGunner>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { All.Clear(); teamGrenadeAt[0] = teamGrenadeAt[1] = anyGrenadeAt = 0f; Thrown[0] = Thrown[1] = 0; }

        /// <summary>0 your side, 1 theirs.</summary>
        public int team = Teams.Enemy;
        public int grenades;
        /// <summary>Goes over the top to get at their trench rather than along the trenches.</summary>
        public bool overTheTop;
        float aggression = 0.6f, suppression;
        public HumanBody Body => body;
        public string StanceName => stance.ToString();
        public string NavDebug => path == null ? "no path" : $"step {pathI}/{path.Count}" + (pathI < path.Count ? $" next {path[pathI].kind} at {path[pathI].at}" : "") + $" feet {body.Pelvis.transform.position - Vector3.up * 0.95f} speed {body.ragdoll.moveSpeed:0.0} obj {objective}";
        public bool SeesEnemy => sees;
        public HumanBody Enemy => enemy;

        // ------------------------------------------------------------ what he knows

        HumanBody enemy, lastEnemy;
        Vector3 enemyAim;
        /// <summary>Where he thinks an enemy is, and since when, and how sure he was then.</summary>
        public Vector3 suspect;
        public float suspectTime = -99f;
        float suspectConf;
        bool newIntel;
        readonly List<(float d, HumanBody h)> cand = new List<(float, HumanBody)>();

        public float SuspectConf => suspectTime < 0f ? 0f : suspectConf * Mathf.Exp(-(Time.time - suspectTime) / 30f);

        /// <summary>Something tells him an enemy is (or was) at pos.</summary>
        public void Learn(Vector3 pos, float conf, bool seen)
        {
            if (conf >= SuspectConf * 0.8f || Time.time - suspectTime > 3f)
            {
                // kept as where they stand (a muzzle, a chest, a head is a metre or more up: everything that
                // aims at "where he is" adds the height of a man to this)
                pos = Feet(pos);
                if (!seen && Vector3.Distance(pos, suspect) > 3f) newIntel = true;
                suspect = pos; suspectTime = Time.time; suspectConf = conf;
            }
        }

        static readonly RaycastHit[] feetBuf = new RaycastHit[12];

        /// <summary>The floor under a point (through any bodies, a couple of metres down at most).</summary>
        static Vector3 Feet(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.2f, Vector3.down, feetBuf, 2.6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 at = p - Vector3.up * 1.3f;
            for (int i = 0; i < n; i++)
            {
                var h = feetBuf[i];
                if (h.collider is CharacterController || h.collider.GetComponentInParent<HumanBody>() != null) continue;
                if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;
                if (h.distance < best) { best = h.distance; at = h.point; }
            }
            return at;
        }

        /// <summary>Rounds coming close: he keeps his head down more.</summary>
        public void Suppress(float k) => suppression = Mathf.Min(1.5f, suppression + k);

        void Sense()
        {
            if (enemy != null && (enemy.Dead || enemy.Head == null)) { enemy = null; sees = false; }
            if (Time.time < sightCheck)
            {
                if (sees && enemy != null) enemyAim = enemy.Center + Vector3.up * 0.05f;
                return;
            }
            sightCheck = Time.time + 0.22f + Random.value * 0.06f;
            sees = false; enemy = null;
            if (body.Head == null) return;
            Vector3 eye = body.Head.transform.position;
            Vector3 face = body.ragdoll.faceDir;
            cand.Clear();
            foreach (var h in HumanBody.All)
            {
                if (h == null || h == body || h.Dead || h.Head == null) continue;
                if (!Teams.Hostile(team, Teams.Of(h))) continue;
                // a man who has dropped his gun and is lying there is out of it
                if (h != PlayerAvatar.Body && h.gunner != null && !h.gunner.Fighting && h.ragdoll.IsDown) continue;
                float d = (h.Center - eye).sqrMagnitude;
                if (d > 75f * 75f) continue;
                cand.Add((d, h));
            }
            cand.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < cand.Count && i < 5; i++)
            {
                var h = cand[i].h;
                Vector3 c = h.Center, hd = h.Head.transform.position;
                // not with eyes in the back of his head (unless it's right behind him, or the one he was fighting)
                Vector3 fl = Vector3.ProjectOnPlane(c - eye, Vector3.up);
                if (face.sqrMagnitude > 0.01f && Vector3.Angle(face, fl) > 115f && cand[i].d > 25f && h != lastEnemy) continue;
                bool torso = Clear(eye, c, body), head = !torso && Clear(eye, hd, body);
                Vector3 at = torso ? c + Vector3.up * 0.05f : hd;
                if (!torso && !head)
                {
                    // round a corner, behind a lip: the middle of him hidden but a shoulder, an arm, a hip or a leg
                    // showing is still a man to shoot at (the nearer ones only, within sensible range)
                    if (i >= 3 || cand[i].d > 70f * 70f || !SomePartShows(h, eye, out at)) continue;
                }
                if (Haze.Transmit(eye, at) < 0.3f) continue;     // lost in the dust and smoke
                if (!Visible(h, eye, cand[i].d)) continue;          // at night: nothing gives him away
                enemy = h; enemyAim = at; sees = true; headOnly = !torso;
                break;
            }
            // (a man not seen for a moment: when he shows again, the reaction time starts over)
            if (sees && Time.time - lastSeen > 1.5f) acquiredAt = Time.time;
            if (sees) { lastSeen = Time.time; lastSeenPos = enemyAim; lastEnemy = enemy; Learn(enemyAim, 1f, true); }
            else if (SandboxGame.Night) SpotLights(eye);
        }

        static readonly PartId[] showParts = { PartId.UpperArmL, PartId.UpperArmR, PartId.Pelvis, PartId.ThighL, PartId.ThighR, PartId.ForearmL, PartId.ForearmR };
        /// <summary>Any of these parts of him in plain sight from the eye (and where).</summary>
        bool SomePartShows(HumanBody h, Vector3 eye, out Vector3 at)
        {
            at = default;
            foreach (var id in showParts)
            {
                var bp = h[id];
                if (bp == null || bp.severed) continue;
                Vector3 p = bp.transform.position;
                if (Clear(eye, p, body)) { at = p; return true; }
            }
            return false;
        }

        // ------------------------------------------------------------ getting about

        List<TrenchNav.Step> path;
        int pathI;
        Vector3 pathGoal, stuckFrom;
        float pathAt, stuckT, skipCheck, relevelAt;
        int stuckCount;

        /// <summary>
        /// Walk (along the trench paths if there are any) toward a spot. True once there. Climbs ladders on the
        /// way. <paramref name="fieldCost"/> above 1 keeps him in the trenches, below 1 sends him over the top.
        /// </summary>
        bool MoveTo(Vector3 goal, float speed, float fieldCost = 1f)
        {
            var rag = body.ragdoll;
            Vector3 me = body.Pelvis.transform.position;
            Vector3 feet = me - Vector3.up * 0.95f;
            if (!TrenchNav.Ready)
            {
                Vector3 g = Vector3.ProjectOnPlane(goal - me, Vector3.up);
                rag.moveDir = g.sqrMagnitude > 1e-4f ? g.normalized : rag.faceDir;
                rag.moveSpeed = g.magnitude > 0.7f ? speed : 0f;
                return g.magnitude <= 0.7f;
            }
            if (path == null || Vector3.Distance(goal, pathGoal) > 2f || Time.time > pathAt + 6f || pathI >= path.Count)
            {
                if (path != null && pathI >= path.Count && Vector3.Distance(goal, pathGoal) <= 2f && FlatDist(feet, goal) < 1.2f) { rag.moveSpeed = 0f; return true; }
                path = TrenchNav.Path(feet, goal, true, fieldCost);
                // (start from the first point if the second can't be walked to straight from here)
                pathI = path.Count > 1 && FlatDist(path[0].at, feet) < 1.5f && TrenchNav.Walkable(feet + Vector3.up * 0.1f, path[1].at) ? 1 : 0;
                pathGoal = goal; pathAt = Time.time;
                if (path.Count == 0) { path = null; Vector3 g = Vector3.ProjectOnPlane(goal - me, Vector3.up); rag.moveDir = g.normalized; rag.moveSpeed = g.magnitude > 0.7f ? speed : 0f; return g.magnitude <= 0.7f; }
            }
            // the next point is on another level from him (he slid back down into the trench, or got up out of it
            // where the wall has caved in): a new way from where he actually is
            if (path[pathI].kind == TrenchNav.Edge.Walk && Mathf.Abs(path[pathI].at.y - feet.y) > 1.3f && Time.time > relevelAt)
            {
                relevelAt = Time.time + 1f;
                path = null;
                return MoveTo(goal, speed, fieldCost);
            }
            var step = path[pathI];
            if (step.kind != TrenchNav.Edge.Walk && step.ladder != null)
            {
                // the ladder: get to its foot (or top), then climb
                Vector3 from = step.kind == TrenchNav.Edge.LadderUp ? step.ladder.foot : step.ladder.top;
                if (FlatDist(feet, from) < 1.0f && Mathf.Abs(feet.y - from.y) < 0.9f)
                {
                    rag.moveSpeed = 0f;
                    if (LadderClimber.Begin(body, step.ladder, step.kind == TrenchNav.Edge.LadderUp) != null) pathI++;
                    return false;
                }
                Steer(from, speed);
                return false;
            }
            // cut corners when the next point can be walked to straight
            if (Time.time > skipCheck && pathI + 1 < path.Count && path[pathI + 1].kind == TrenchNav.Edge.Walk)
            {
                skipCheck = Time.time + 0.5f;
                if (Mathf.Abs(path[pathI + 1].at.y - feet.y) < 0.8f && TrenchNav.Walkable(feet + Vector3.up * 0.1f, path[pathI + 1].at)) pathI++;
                step = path[pathI];
            }
            if (FlatDist(feet, step.at) < 0.8f && Mathf.Abs(feet.y - step.at.y) < 1f)
            {
                pathI++;
                if (pathI >= path.Count) { rag.moveSpeed = 0f; return FlatDist(feet, goal) < 1.5f; }
                step = path[pathI];
                if (step.kind != TrenchNav.Edge.Walk) return false;
            }
            Steer(step.at, speed);
            // stuck on something: try again (a few times, then give it up)
            if ((me - stuckFrom).sqrMagnitude > 0.25f) { stuckFrom = me; stuckT = 0f; }
            else if (rag.moveSpeed > 0.3f && (stuckT += Time.deltaTime) > 1.2f && pathI < path.Count - 1 && FlatDist(feet, path[pathI].at) < 1.8f && path[pathI + 1].kind == TrenchNav.Edge.Walk)
            {
                // wedged against the corner by a point: count it reached and make for the next
                pathI++; stuckT = 0f;
            }
            else if (rag.moveSpeed > 0.3f && stuckT > 2.5f)
            {
                stuckT = 0f; path = null; stuckCount++;
                rag.moveDir = Quaternion.Euler(0f, Random.Range(-100f, 100f), 0f) * rag.moveDir;
                if (stuckCount > 3) { stuckCount = 0; return true; }
            }
            return false;
        }

        void Steer(Vector3 to, float speed)
        {
            var rag = body.ragdoll;
            Vector3 g = Vector3.ProjectOnPlane(to - body.Pelvis.transform.position, Vector3.up);
            rag.moveDir = g.sqrMagnitude > 1e-4f ? g.normalized : rag.faceDir;
            rag.moveSpeed = speed;
        }

        static float FlatDist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Gun up on a point (or at the low ready on it), head with it.</summary>
        void AimAt(Vector3 p, bool up)
        {
            var rag = body.ragdoll;
            Vector3 chest = body.Chest.transform.position;
            Vector3 d = (p - (chest + Vector3.up * 0.15f)).normalized;
            rag.faceDir = Vector3.ProjectOnPlane(p - chest, Vector3.up).normalized;
            rag.aimDir = up ? d : (d + Vector3.down * 0.35f).normalized;
            rag.lookAt = p;
        }

        float lookCheck; bool lookClear;

        /// <summary>Gun up on a spot while walking: on it if he can see it, otherwise the way he's going (round the corner first).</summary>
        void AimMoving(Vector3 look)
        {
            var rag = body.ragdoll;
            if (Time.time > lookCheck) { lookCheck = Time.time + 0.25f; lookClear = body.Head != null && Clear(body.Head.transform.position, look, body); }
            if (rag.moveSpeed > 0.2f && !lookClear) AimAt(body.Head.transform.position + rag.moveDir * 8f - Vector3.up * 0.3f, true);
            else AimAt(look, true);
        }

        // ------------------------------------------------------------ he knows (roughly) where you are

        enum Stance { None, Peek, Hold, Push, Search }
        Stance stance;
        float stanceUntil, peekWait, cornerCheck, prefiredFor = -1f, grenadeReady, grenadeLook;
        bool headOnly;
        Vector3? peekSpot;
        Vector3 corner;
        int searchLeft;
        Vector3? searchSpot;

        void Investigate(float legs)
        {
            var rag = body.ragdoll;
            Vector3 me = body.Pelvis.transform.position;
            float age = Time.time - suspectTime;
            bool fresh = newIntel; newIntel = false;
            if (stance == Stance.None || Time.time > stanceUntil || (fresh && stance != Stance.Push && stance != Stance.Search)) Decide(age, Vector3.Distance(me, suspect));
            Vector3 look = suspect + Vector3.up * 1.1f;
            if (age > 1.5f && SuspectConf > 0.3f && stance != Stance.Search) ConsiderGrenade(suspect, true);
            switch (stance)
            {
                case Stance.Hold:
                {
                    // on the spot, gun on the corner they'll come round
                    rag.moveSpeed = 0f;
                    if (Time.time > cornerCheck) { cornerCheck = Time.time + 1f; corner = CornerToward(suspect); }
                    AimAt(corner + Vector3.up * 1.2f, true);
                    if (suppression > 0.6f) { rag.crouch = 1f; rag.pose = PoseKind.Crouch; }
                    rag.lean = Mathf.MoveTowards(rag.lean, leans ? PeekSide(corner) : 0f, Time.deltaTime * 3f);
                    if (age < 3f) TryPrefire(0.5f);
                    break;
                }
                case Stance.Peek:
                {
                    // out to where the angle can be seen, gun up on it, and fire into it as it opens
                    bool there = peekSpot == null || MoveTo(peekSpot.Value, 1.6f * legs);
                    AimMoving(look);
                    rag.lean = Mathf.MoveTowards(rag.lean, leans ? PeekSide(suspect) : 0f, Time.deltaTime * 3f);
                    TryPrefire(1f);
                    if (there)
                    {
                        if (peekWait <= 0f) peekWait = Time.time + Random.Range(2f, 3.5f);
                        else if (Time.time > peekWait) { peekWait = 0f; stance = aggression > 0.45f ? Stance.Push : Stance.Hold; stanceUntil = Time.time + Random.Range(8f, 20f); }
                    }
                    break;
                }
                case Stance.Push:
                {
                    // in to where they were, carefully, leaning round each corner first
                    int side = leans ? PeekSide(suspect) : 0;
                    rag.lean = Mathf.MoveTowards(rag.lean, side, Time.deltaTime * 3f);
                    bool holding = Time.time < peekHold;
                    bool there = !holding && MoveTo(suspect, 1.5f * legs);
                    if (holding) rag.moveSpeed = 0f;
                    AimMoving(look);
                    TryPrefire(1f);
                    if (there || FlatDist(me, suspect) < 1.6f) { stance = Stance.Search; searchLeft = Random.Range(2, 4); searchSpot = null; stanceUntil = Time.time + 30f; }
                    break;
                }
                case Stance.Search:
                {
                    // not here: check round about, then give it up
                    if (searchSpot == null)
                    {
                        if (searchLeft-- <= 0) { suspectConf = 0f; stance = Stance.None; break; }
                        searchSpot = TrenchNav.Ready ? TrenchNav.RandomNear(suspect, 9f, i => TrenchNav.Field[i] == OnField) : NextSearchPoint(suspect);
                        if (searchSpot == null) { suspectConf = 0f; stance = Stance.None; break; }
                    }
                    bool there = MoveTo(searchSpot.Value, 1.4f * legs);
                    AimMoving(searchSpot.Value + Vector3.up * 1.2f);
                    rag.lean = Mathf.MoveTowards(rag.lean, leans ? PeekSide(searchSpot.Value) : 0f, Time.deltaTime * 3f);
                    if (there) searchSpot = null;
                    break;
                }
            }
        }

        bool OnField => body.Pelvis.transform.position.y > 2.2f;

        void Decide(float age, float dist)
        {
            float hurt = 1f - body.ragdoll.Steadiness;
            float bold = aggression - hurt * 0.6f - suppression * 0.4f + (team == Teams.Friendly ? 0.15f : 0f);
            peekWait = 0f;
            Vector3 head = body.Head.transform.position;
            // gone quiet in a corner he can't see into: a grenade
            // (deciding how to go at a corner someone has gone quiet in: a grenade first, more often than not)
            if (age > 1.5f && SuspectConf > 0.3f && ConsiderGrenade(suspect, true, 0.45f))
            {
                stance = Stance.Hold; stanceUntil = Time.time + 5f;
                return;
            }
            if (role == SquadRole.Hold && squad != null || inSquad || Random.value > bold)
            {
                stance = Stance.Hold; stanceUntil = Time.time + Random.Range(5f, 11f);
                cornerCheck = 0f;
                return;
            }
            peekSpot = FindPeekSpot(suspect);
            if (peekSpot != null) { stance = Stance.Peek; stanceUntil = Time.time + 14f; return; }
            stance = Stance.Push; stanceUntil = Time.time + 30f;
        }

        /// <summary>Somewhere a few steps off from which the spot can be seen standing (the angle to peek).</summary>
        Vector3? FindPeekSpot(Vector3 s)
        {
            Vector3 me = body.Pelvis.transform.position - Vector3.up * 0.95f;
            var pts = new List<Vector3>();
            if (TrenchNav.Ready) { foreach (int i in TrenchNav.Within(me, 11f)) if (Mathf.Abs(TrenchNav.Nodes[i].y - me.y) < 1f) pts.Add(TrenchNav.Nodes[i]); }
            else for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; pts.Add(me + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3f); }
            pts.Sort((a, b) => (a - me).sqrMagnitude.CompareTo((b - me).sqrMagnitude));
            int tests = 0;
            foreach (var p in pts)
            {
                if (tests++ > 14) break;
                if (Vector3.Distance(p, s) < 3.5f) continue;
                if (TrenchSquad.Taken(this, p, 1.2f)) continue;
                if (Clear(p + Vector3.up * 1.55f, s + Vector3.up * 1f, body)) return p;
            }
            return null;
        }

        /// <summary>The corner they'll come round: the farthest point on the way to them that he can see.</summary>
        Vector3 CornerToward(Vector3 s)
        {
            if (!TrenchNav.Ready || body.Head == null) return s;
            Vector3 head = body.Head.transform.position;
            var p = TrenchNav.Path(body.Pelvis.transform.position - Vector3.up * 0.95f, s, OnField || s.y > 1.4f, 2f);
            Vector3 best = s; bool any = false;
            for (int i = 1; i < p.Count; i++)
            {
                if (Clear(head, p[i].at + Vector3.up * 1.2f, body)) { best = p[i].at; any = true; }
                else if (any) break;
            }
            return any ? best : s;
        }

        // ------------------------------------------------------------ prefiring

        int prefireLeft;
        Vector3 prefireAt;

        /// <summary>Where they should be has just come into view, and it's fresh: put a few rounds into it.</summary>
        void TryPrefire(float chance)
        {
            if (prefiredFor == suspectTime || prefireLeft > 0 || body.Head == null) return;
            float age = Time.time - suspectTime;
            if (age > 6f || SuspectConf < 0.45f) return;
            Vector3 at = suspect + Vector3.up * 1.0f;
            Vector3 head = body.Head.transform.position;
            if (!Clear(head, at, body)) return;
            // (not up into the air over the lip of a trench: nobody's standing on nothing)
            Vector3 up = at - head;
            if (up.y > 0f && up.y / Mathf.Max(0.1f, new Vector2(up.x, up.z).magnitude) > 0.35f) return;
            prefiredFor = suspectTime;
            if (Random.value > chance * Mathf.Lerp(0.5f, 1f, aggression)) return;
            prefireAt = at + Random.insideUnitSphere * 0.3f;
            prefireLeft = Random.Range(2, 6);
        }

        void UpdatePrefire()
        {
            if (prefireLeft <= 0 || sees || !Gun) return;
            AimAt(prefireAt, true);
            int before = rounds;
            Shoot(prefireAt);
            if (rounds != before) prefireLeft--;
            if (Time.time < reloadUntil) prefireLeft = 0;
        }

        /// <summary>Would a round from here to there go through one of his own side?</summary>
        bool AllyInLine(Vector3 to)
        {
            Vector3 a = muzzle != null ? muzzle.position : body.Chest.transform.position;
            Vector3 d = to - a; float len2 = d.sqrMagnitude;
            if (len2 < 1e-4f) return false;
            foreach (var h in HumanBody.All)
            {
                if (h == null || h == body || h.Dead || Teams.Of(h) != team) continue;
                foreach (var c in new[] { h.Center, h.Head != null ? h.Head.transform.position : h.Center, h.Pelvis != null ? h.Pelvis.transform.position : h.Center })
                {
                    float t = Vector3.Dot(c - a, d) / len2;
                    if (t < 0.02f || t > 1.02f) continue;
                    if ((a + d * t - c).sqrMagnitude < 0.36f) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------ grenades

        /// <summary>Grenades thrown by each side (for the playtests).</summary>
        public static readonly int[] Thrown = new int[2];
        // a side lobs one every half minute or so, never two on top of each other: a grenade is for a man you
        // can't get at with the rifle (behind cover, dug in with his head showing, or a bunch together)
        static readonly float[] teamGrenadeAt = new float[2];
        static float anyGrenadeAt;

        /// <summary>For the playtests: this one may throw straight away.</summary>
        public void GrenadeNow() { grenadeReady = Time.time; grenadeLook = 0f; teamGrenadeAt[Mathf.Clamp(team, 0, 1)] = anyGrenadeAt = 0f; }

        /// <summary>
        /// Worth a grenade at <paramref name="spot"/>? Someone <paramref name="hidden"/> behind cover is the usual
        /// reason; a man out in the open gets the rifle instead, unless he's dug in or has mates bunched round him.
        /// </summary>
        bool ConsiderGrenade(Vector3 spot, bool hidden, float bonus = 0f)
        {
            if (grenades <= 0 || medic || Time.time < grenadeLook || body.ragdoll.IsDown || body.Head == null) return false;
            grenadeLook = Time.time + 1f;
            // not in the first moments of a fight: he gets his bearings first
            if (grenadeReady <= 0f) { grenadeReady = Time.time + Random.Range(5f, 15f); return false; }
            int t = Mathf.Clamp(team, 0, 1);
            if (Time.time < grenadeReady || Time.time < teamGrenadeAt[t] || Time.time < anyGrenadeAt) return false;
            float dist = Vector3.Distance(body.Pelvis.transform.position, spot);
            if (dist < 9f || dist > 32f) return false;
            int near = 0;
            foreach (var h in HumanBody.All)
                if (h != null && !h.Dead && Teams.Hostile(team, Teams.Of(h)) && Vector3.Distance(h.Center, spot) < 4.5f) near++;
            // a man down in a trench firing back: the rifle hardly gets at him, a grenade in beside him does
            bool dugIn = !hidden && TrenchNav.Ready && spot.y < 0.8f && dist > 12f;
            if (!hidden && near < 2 && !headOnly && !dugIn) return false;
            float chance = (hidden ? 0.3f : headOnly ? 0.15f : dugIn ? 0.12f : 0.1f) + 0.12f * Mathf.Max(0, near - 1) + bonus;
            if (Random.value > chance) return false;
            if (hidden && Clear(body.Head.transform.position, spot + Vector3.up * 0.8f, body)) return false;
            return TryGrenade(spot);
        }

        /// <summary>Lob one at a spot: low and flat if it can get there, up and over the top if not.</summary>
        bool TryGrenade(Vector3 spot)
        {
            if (grenades <= 0 || body.Head == null) return false;
            Vector3 me = body.Pelvis.transform.position;
            if (Vector3.Distance(me, spot) < 7f) return false;
            // never onto his own side
            foreach (var h in HumanBody.All)
                if (h != null && !h.Dead && h != body && Teams.Of(h) == team && Vector3.Distance(h.Center, spot) < 8f) return false;
            Vector3 flat = Vector3.ProjectOnPlane(spot - me, Vector3.up).normalized;
            Vector3 from = body.Head.transform.position + flat * 0.35f + Vector3.up * 0.25f;
            Vector3 aim = spot + Vector3.up * 0.15f;
            foreach (float v in new[] { 9f, 11.5f, 14f, 17f })
                foreach (bool high in new[] { false, true })
                {
                    if (!SolveArc(from, aim, v, high, out var vel, out float t)) continue;
                    if (!ArcClear(from, vel, aim, t)) continue;
                    var g = Grenade.Throw(from, vel);
                    g.team = team;
                    // cooked so it goes off a moment after it lands
                    g.fuse = Mathf.Clamp(t + 1.4f, 2f, 3.5f);
                    grenades--;
                    grenadeReady = Time.time + Random.Range(25f, 45f);
                    int side = Mathf.Clamp(team, 0, 1);
                    teamGrenadeAt[side] = Time.time + Random.Range(22f, 40f);
                    anyGrenadeAt = Time.time + 8f;
                    Thrown[side]++;
                    Sfx.Play("casing_dirt", from, 0.4f, 1f, 2f);
                    body.ragdoll.faceDir = flat;
                    return true;
                }
            return false;
        }

        static bool SolveArc(Vector3 from, Vector3 to, float v, bool high, out Vector3 vel, out float t)
        {
            vel = Vector3.zero; t = 0f;
            Vector3 d = to - from;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float x = flat.magnitude, y = d.y, g = 9.81f, v2 = v * v;
            if (x < 0.5f) return false;
            float disc = v2 * v2 - g * (g * x * x + 2f * y * v2);
            if (disc < 0f) return false;
            float sq = Mathf.Sqrt(disc);
            float ang = Mathf.Atan((v2 + (high ? sq : -sq)) / (g * x));
            if (high && ang < 0.6f) return false;
            vel = flat / x * v * Mathf.Cos(ang) + Vector3.up * v * Mathf.Sin(ang);
            t = x / (v * Mathf.Cos(ang));
            return t < 3f;
        }

        bool ArcClear(Vector3 from, Vector3 vel, Vector3 to, float t)
        {
            Vector3 p = from;
            const float dt = 0.04f;
            for (float s = 0f; s < t + 0.15f; s += dt)
            {
                Vector3 n = from + vel * (s + dt) + 0.5f * Physics.gravity * (s + dt) * (s + dt);
                Vector3 d = n - p;
                foreach (var hit in Physics.RaycastAll(p, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider is CharacterController || hit.collider.GetComponentInParent<HumanBody>() != null) continue;
                    if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
                    return Vector3.Distance(hit.point, to) < 1.8f;     // lands near enough
                }
                p = n;
            }
            return true;
        }

        // ------------------------------------------------------------ a grenade at his feet

        Grenade fleeFrom;
        Vector3? fleeTo;
        float fleeCheck;

        bool FleeGrenade(float legs)
        {
            var rag = body.ragdoll;
            Vector3 me = body.Pelvis.transform.position;
            if (Time.time > fleeCheck)
            {
                fleeCheck = Time.time + 0.15f;
                Grenade near = null; float nd = 7.5f;
                foreach (var g in Grenade.All)
                {
                    if (g == null) continue;
                    float d = Vector3.Distance(me, g.transform.position);
                    if (d >= nd) continue;
                    // seen it land (or heard it right by him)
                    if (d > 2.5f && (body.Head == null || !Clear(body.Head.transform.position, g.transform.position + Vector3.up * 0.1f, body))) continue;
                    // his own just thrown, still flying away from him: not a threat
                    var grb = g.GetComponent<Rigidbody>();
                    if (g.team == team && grb != null && Vector3.Dot(grb.linearVelocity, g.transform.position - me) > 1f && d > 2f) continue;
                    near = g; nd = d;
                }
                if (near != fleeFrom) { fleeFrom = near; fleeTo = near != null ? Refuge(near) : null; path = null; }
            }
            if (fleeFrom == null) return false;
            float left = fleeFrom.fuse;
            float dist = Vector3.Distance(me, fleeFrom.transform.position);
            body.brain.fear = Mathf.Max(body.brain.fear, 0.8f);
            // no time left to get away: throw himself flat
            if (dist < 3.5f && left < 0.9f) { if (!rag.IsDown) rag.Fall("dived away from a grenade"); return true; }
            rag.aimDir = null;
            rag.lean = 0f;
            if (fleeTo.HasValue) MoveTo(fleeTo.Value, 4.8f * Mathf.Max(0.4f, legs));
            else
            {
                Vector3 away = Vector3.ProjectOnPlane(me - fleeFrom.transform.position, Vector3.up);
                rag.moveDir = away.sqrMagnitude > 1e-4f ? away.normalized : -rag.faceDir;
                rag.moveSpeed = 4.8f * legs;
            }
            rag.faceDir = rag.moveDir;
            return true;
        }

        /// <summary>Round a corner or far enough off, whichever he can reach before it goes.</summary>
        Vector3? Refuge(Grenade g)
        {
            Vector3 me = body.Pelvis.transform.position - Vector3.up * 0.95f;
            Vector3 gp = g.transform.position + Vector3.up * 0.15f;
            float reach = Mathf.Max(2f, g.fuse * 4f);
            Vector3? best = null; float bs = float.MinValue;
            var pts = new List<Vector3>();
            if (TrenchNav.Ready) { foreach (int i in TrenchNav.Within(me, Mathf.Min(reach, 14f))) if (Mathf.Abs(TrenchNav.Nodes[i].y - me.y) < 1f) pts.Add(TrenchNav.Nodes[i]); }
            else for (int i = 0; i < 16; i++) { float a = i * Mathf.PI / 8f; Vector3 p = me + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Mathf.Min(reach, 8f); if (TrenchNav.Walkable(me, p)) pts.Add(p); }
            int tests = 0;
            foreach (var p in pts)
            {
                if (tests++ > 30) break;
                float fromG = Vector3.Distance(p, gp), fromMe = Vector3.Distance(p, me);
                if (fromMe > reach) continue;
                // running past it is no good
                if (Vector3.Dot(p - me, gp - me) > 0f && fromG < Vector3.Distance(me, gp)) continue;
                float score = Mathf.Min(fromG, 10f) - fromMe * 0.3f;
                if (!Clear(p + Vector3.up * 0.6f, gp, body)) score += 8f;
                if (score > bs) { bs = score; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------ nothing going on

        Vector3? wanderTo, objective;
        float wanderPause;

        void Idle(float legs)
        {
            var rag = body.ragdoll;
            stance = Stance.None;
            rag.lean = Mathf.MoveTowards(rag.lean, 0f, Time.deltaTime * 3f);
            if (assault) { Advance(legs); return; }
            if (defend) { HoldLine(legs); return; }
            if (team == Teams.Friendly) { Advance(legs); return; }
            // no paths to walk (not the trenches): they come looking for you, as they always have
            if (!TrenchNav.Ready)
            {
                if (PlayerAvatar.Body != null && !PlayerAvatar.Body.Dead && Time.time - lastSeen > 4f) Hunt(PlayerAvatar.Body.Center, legs);
                else rag.moveSpeed = 0f;
                return;
            }
            if (role == SquadRole.Follow && FollowBehaviour(suspect, legs)) return;
            if (role == SquadRole.Hold && squad != null)
            {
                // on his spot, watching the way you'd come
                Vector3 g = Vector3.ProjectOnPlane(holdPoint - body.Pelvis.transform.position, Vector3.up);
                if (g.magnitude > 1.2f) MoveTo(holdPoint, 1.3f * legs); else rag.moveSpeed = 0f;
                if (g.magnitude <= 1.2f) AimAt(holdPoint + new Vector3(0f, 1.3f, team == Teams.Enemy ? -8f : 8f), false);
                return;
            }
            Wander(legs, i => !TrenchNav.Field[i] && (team == Teams.Enemy ? TrenchNav.Nodes[i].z > 2f : TrenchNav.Nodes[i].z < -2f));
        }

        /// <summary>Walking the trench, now and then stopping to look about.</summary>
        void Wander(float legs, System.Func<int, bool> where)
        {
            var rag = body.ragdoll;
            if (wanderTo == null)
            {
                if (Time.time < wanderPause)
                {
                    rag.moveSpeed = 0f;
                    // looking about
                    float a = Mathf.PerlinNoise(Time.time * 0.25f, seed) * 300f;
                    rag.faceDir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    rag.aimDir = (rag.faceDir * 0.9f + Vector3.down * 0.35f).normalized;
                    return;
                }
                wanderTo = TrenchNav.RandomNear(body.Pelvis.transform.position, 28f, where);
                if (wanderTo == null) { wanderPause = Time.time + 3f; return; }
            }
            if (MoveTo(wanderTo.Value, 1.3f * legs)) { wanderTo = null; wanderPause = Time.time + Random.Range(3f, 9f); }
            rag.faceDir = rag.moveDir;
            rag.aimDir = (rag.moveDir * 0.9f + Vector3.down * 0.35f).normalized;
        }

        /// <summary>One of yours, on his own: over to their trench and in.</summary>
        void Advance(float legs)
        {
            var rag = body.ragdoll;
            if (!TrenchNav.Ready)
            {
                // nowhere to go: stay near you
                var me = PlayerAvatar.Body;
                if (me != null && FlatDist(me.Center, body.Center) > 6f) MoveTo(me.Center, 2f * legs); else rag.moveSpeed = 0f;
                return;
            }
            Vector3 here = body.Pelvis.transform.position;
            if (objective == null)
            {
                // a point along their front line: his group's stretch of it, or (on his own) roughly across from him
                Vector3? best = null; float bd = float.MaxValue;
                float aimX = laneX ?? here.x, jitter = laneX.HasValue ? 3f : 12f;
                for (int i = 0; i < TrenchNav.Nodes.Count; i++)
                {
                    var n = TrenchNav.Nodes[i];
                    if (TrenchNav.Field[i] || !TheirLineNode(n)) continue;
                    float d = Mathf.Abs(n.x - aimX + Random.Range(-jitter, jitter)) + Random.Range(0f, laneX.HasValue ? 2f : 6f);
                    if (d < bd) { bd = d; best = n; }
                }
                objective = best;
                if (objective == null) { rag.moveSpeed = 0f; return; }
            }
            bool inTheirs = InTheirLine(here) && here.y < 1.2f;
            if (inTheirs || FlatDist(here, objective.Value) < 2f)
            {
                // in: clear it, spot by spot
                Wander(legs, i => !TrenchNav.Field[i] && (team == Teams.Friendly ? TrenchNav.Nodes[i].z > 12f : TrenchNav.Nodes[i].z < -12f));
                return;
            }
            bool fast = here.y > 1.4f;      // out in the open: run
            MoveTo(objective.Value, (fast ? 3.4f : 2f) * legs, overTheTop ? 0.45f : 25f);
            AimAt(here + rag.moveDir * 8f + Vector3.up * 0.6f, false);
        }
    }
}
