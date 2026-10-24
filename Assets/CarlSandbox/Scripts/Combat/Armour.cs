using System;
using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    public enum VestKind { None, Russian6B45, KnightCuirass }
    public enum HelmetKind { None, Spetsnaz, Knight, Ratnik }

    /// <summary>What an Earl is wearing (chosen in the spawn menu).</summary>
    [Serializable]
    public class ArmourLoadout
    {
        public VestKind vest;
        public int camo;                     // the vest's cloth: 0 EMR camo, 1 coyote (Ukraine), 2 ranger green (Russia)
        public int plateClass = 4;           // GOST Br class of the 6B45 plates (2..6)
        public bool sideArmour = true, groinFlap = true, collar = true, pouches = true;
        public bool armGuards;               // 6B45 upper-arm protectors: soft armour down from the shoulder
        public HelmetKind helmet;
        public bool visor = true, visorDown = true;          // spetsnaz / knight visor
        public bool aventail = true, plume = true;           // knight helmet extras
        public bool pauldrons, vambraces, gauntlets, cuisses, greaves, sabatons, tassets;
        public ArmourLoadout Clone() => (ArmourLoadout)MemberwiseClone();
    }

    /// <summary>
    /// One piece of armour worn on a body part. Its mesh is built around the part's skin, a little off it, so it
    /// sits on the body rather than being painted on; it hangs from its straps and lags, bounces and swings as the
    /// body moves. Bullets are traced against it before they reach the body; hits rip the cloth, dent and crack the
    /// plates, punch holes, and a piece that has been shot to bits falls off.
    /// </summary>
    public class ArmourPiece : MonoBehaviour
    {
        public string label;
        public BodyPart part;
        public float cls;                    // armour class: ammo with pen >= cls+1 goes through, pen <= cls-1 stops
        public bool steel, glass, visor;     // what it is made of (sparks / cracks / fluff)
        public bool plateUnder;              // a cloth carrier with hard plates inside (6B45 front / back / side)
        public Mesh mesh;
        /// <summary>The mesh (and v) are shared with every other Earl's identical piece until this one is hit.</summary>
        public bool sharedMesh;
        public Vector3[] v;                  // local space (piece == part frame unless it is a hinged visor)
        public int[] t;
        public Bounds bounds;
        public float area;                   // outer surface area (m^2)
        public float swingHang;              // > 0: hangs from its top edge and swings (groin flap)
        public float looseness = 1f;         // how much it moves about on the body
        public readonly List<Vector4> wear = new List<Vector4>();   // xyz local hit, w amount
        public readonly List<Vector4> dents = new List<Vector4>();  // xyz local, w depth (m)
        public readonly List<Vector4> breaks = new List<Vector4>(); // xyz local, w radius of the cracked-open area
        public float strapHealth = 100f;     // cut / shot straps let it hang looser
        public bool rides;                   // pouches: sewn onto the piece they sit on, so they move with it
        public bool fallen;
        float destroyed;                     // area shot / cracked away (m^2)

        // hinged visor
        public bool isUp;
        public Vector3 hingePoint, hingeAxis;
        float angle, targetAngle;

        public float EffectiveClass(Vector3 local)
        {
            // where the plate has cracked open there is almost nothing left to stop a round
            foreach (var br in breaks) if (Vector3.Distance(local, br) < br.w * 1.1f) return cls * 0.12f;
            float w = 0f;
            foreach (var h in wear)
            {
                float d = Vector3.Distance(local, h);
                if (d < 0.07f) w += h.w * (1f - d / 0.07f);
            }
            return cls * Mathf.Clamp01(1f - w * 0.22f);
        }

        /// <summary>Its own copy of the shape before it gets scuffed or dented (shared until then).</summary>
        void Own()
        {
            if (!sharedMesh) return;
            sharedMesh = false;
            mesh = Instantiate(mesh);
            var mf = GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = mesh;
            v = (Vector3[])v.Clone();
        }

        public void AddWear(Vector3 local, float amount)
        {
            Own();
            wear.Add(new Vector4(local.x, local.y, local.z, amount));
            // light scuffing around the hit; the rip / hole itself is drawn by the shader
            var cols = mesh.colors;
            if (cols == null || cols.Length != v.Length) { cols = new Color[v.Length]; for (int i = 0; i < cols.Length; i++) cols[i] = Color.white; }
            for (int i = 0; i < v.Length; i++)
            {
                float d = Vector3.Distance(v[i], local);
                if (d < 0.02f) cols[i] *= Mathf.Lerp(steel ? 0.7f : 0.85f, 1f, d / 0.02f);
            }
            mesh.colors = cols;
        }

        // ------------------------------------------------------------ visible damage
        const int MaxDmg = 48;
        Texture2D dmgTex;
        int dmgCount;
        Material ownMat;

        void AddDamageMark(Vector3 local, float radius, bool through)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) return;
            if (ownMat == null) { ownMat = new Material(mr.sharedMaterial); mr.sharedMaterial = ownMat; ownMat.SetFloat("_PlateUnder", plateUnder ? 1f : 0f); }
            if (dmgTex == null) { dmgTex = new Texture2D(MaxDmg, 1, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; ownMat.SetTexture("_DmgTex", dmgTex); }
            int slot = dmgCount < MaxDmg ? dmgCount++ : UnityEngine.Random.Range(0, MaxDmg);
            dmgTex.SetPixel(slot, 0, new Color(local.x, local.y, local.z, through ? radius : -radius));
            dmgTex.Apply(false);
            ownMat.SetFloat("_DmgCount", dmgCount);
            if (through) destroyed += Mathf.PI * radius * radius;
        }

        float DentAt(Vector3 local)
        {
            float d = 0f;
            foreach (var dn in dents) { float r = Vector3.Distance(local, dn); if (r < 0.05f) d += dn.w * (1f - r / 0.05f); }
            return d;
        }

        bool IsBroken(Vector3 local) { foreach (var br in breaks) if (Vector3.Distance(local, br) < br.w) return true; return false; }

        /// <summary>
        /// A round hit this piece. Stopped: the cloth tears open over the plate (or the paint chips off steel)
        /// and the plate is pushed in; dents add up and a plate pushed in far enough cracks right open.
        /// Through: a torn hole. Returns true if this hit cracked the plate.
        /// </summary>
        public bool Impact(Vector3 local, Vector3 dirWorld, float energy, float diameter, bool through)
        {
            if (fallen) return false;
            // a round on the helmet on your own head: it rings in your skull
            if (part != null && part.body != null && part.body == PlayerAvatar.Body && part.id == PartId.Head) PlayerBody.I?.HelmetHit(energy, through);
            Vector3 dl = transform.InverseTransformDirection(dirWorld).normalized;
            bool hard = steel || plateUnder || glass;
            bool cracked = false;
            if (through)
                AddDamageMark(local, Mathf.Clamp(diameter * (steel ? 0.9f : 1.3f), 0.004f, 0.02f), true);
            else
            {
                AddDamageMark(local, Mathf.Clamp(0.004f + energy / 200000f, 0.006f, 0.025f), false);
                if (hard)
                {
                    float depth = Mathf.Clamp(energy / 260000f, 0.0012f, 0.02f) * (steel && !plateUnder ? 0.7f : 1f);
                    float radius = Mathf.Clamp(0.022f + energy / 220000f, 0.022f, 0.06f);
                    Dent(local, dl, radius, depth);
                    float limit = plateUnder ? 0.02f : (steel ? 0.015f : 0.018f);
                    if (DentAt(local) > limit && !IsBroken(local))
                    {
                        // the plate gives way: cracked open, a ragged hole through the cloth and the plate
                        float r = Mathf.Clamp(0.016f + energy / 300000f, 0.016f, 0.035f);
                        breaks.Add(new Vector4(local.x, local.y, local.z, r));
                        AddDamageMark(local, r * 0.75f, true);
                        cracked = true;
                    }
                }
            }
            // shot near the edges the straps / buckles get chewed and it sits looser
            var b = mesh.bounds;
            float edge = Mathf.Max(Mathf.InverseLerp(0.55f, 1f, Mathf.Abs((local.y - b.center.y) / Mathf.Max(1e-4f, b.extents.y))),
                                   Mathf.InverseLerp(0.65f, 1f, Mathf.Abs((local.x - b.center.x) / Mathf.Max(1e-4f, b.extents.x))));
            strapHealth = Mathf.Max(0f, strapHealth - energy / 90f * Mathf.Lerp(0.1f, 1f, edge));
            // shot to bits: nothing left holding it together, so what is left drops off
            if (area > 0f && destroyed > area * 0.3f) FallOff(dirWorld);
            return cracked;
        }

        void Dent(Vector3 local, Vector3 dirLocal, float radius, float depth)
        {
            Own();
            dents.Add(new Vector4(local.x, local.y, local.z, depth));
            bool moved = false;
            for (int i = 0; i < v.Length; i++)
            {
                float d = Vector3.Distance(v[i], local);
                if (d >= radius) continue;
                float f = 0.5f + 0.5f * Mathf.Cos(d / radius * Mathf.PI);   // smooth bowl
                v[i] += dirLocal * depth * f;
                moved = true;
            }
            if (!moved) return;
            mesh.vertices = v;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            bounds = mesh.bounds; bounds.Expand(0.01f);
        }

        /// <summary>Blast / shrapnel: shreds the cloth and loosens it, never strips it off in one go.</summary>
        public void Blasted(float power) { strapHealth = Mathf.Max(0f, strapHealth - power); }

        /// <summary>
        /// A blast right next to it: the side facing the explosion is peppered with fragment holes and scorched
        /// tears, plates under it are slammed in, and close enough the cloth is ripped wide open and what is left
        /// can be torn off the body. strength 0..1 (1 = against it).
        /// </summary>
        public void BlastDamage(Vector3 blastWorld, float strength)
        {
            if (fallen || strength <= 0.02f || v == null || v.Length == 0) return;
            Vector3 b = transform.InverseTransformPoint(blastWorld);
            var nrm = mesh.normals;
            bool hard = steel || plateUnder || glass;
            // the nearest point of the piece to the blast takes the worst of it
            int near = 0; float nd = float.MaxValue;
            for (int i = 0; i < v.Length; i++) { float d = (v[i] - b).sqrMagnitude; if (d < nd) { nd = d; near = i; } }
            float nearDist = Mathf.Sqrt(nd);
            // fragment holes and small tears scattered over the side facing it
            int tries = Mathf.RoundToInt(Mathf.Lerp(6f, 40f, strength));
            for (int k = 0; k < tries; k++)
            {
                int i = UnityEngine.Random.Range(0, v.Length);
                Vector3 to = b - v[i];
                float d = to.magnitude;
                if (nrm != null && i < nrm.Length && Vector3.Dot(nrm[i], to) <= 0f) continue;      // facing away
                float w = strength * Mathf.Clamp01(1f - (d - nearDist) / 0.6f);
                if (UnityEngine.Random.value > w) continue;
                bool through = !hard || UnityEngine.Random.value < w * 0.35f;
                AddDamageMark(v[i], Mathf.Lerp(0.004f, 0.016f, w * UnityEngine.Random.value), through);
            }
            if (hard)
            {
                // the plate takes the overpressure as one big push
                Vector3 inward = nrm != null && near < nrm.Length ? -nrm[near] : (v[near] - b).normalized;
                Dent(v[near], inward, Mathf.Lerp(0.04f, 0.09f, strength), Mathf.Lerp(0.002f, 0.012f, strength));
            }
            if (strength > 0.55f)
            {
                // against it: the cloth is ripped open in a ragged tear around the nearest point
                int rips = Mathf.RoundToInt(Mathf.Lerp(3f, 9f, (strength - 0.55f) / 0.45f));
                for (int k = 0; k < rips; k++)
                {
                    Vector3 off = UnityEngine.Random.insideUnitSphere * Mathf.Lerp(0.03f, 0.08f, strength);
                    AddDamageMark(v[near] + off, Mathf.Lerp(0.02f, 0.045f, strength) * UnityEngine.Random.Range(0.6f, 1f), true);
                }
                strapHealth = Mathf.Max(0f, strapHealth - 60f * strength);
                // torn to rags and nothing holding it on: blown off
                if (strength > 0.85f && (!hard || area > 0f && destroyed > area * 0.2f)) FallOff(v[near] - b == Vector3.zero ? Vector3.up : transform.TransformDirection(v[near] - b));
            }
            if (area > 0f && destroyed > area * 0.3f) FallOff(transform.TransformDirection(v[near] - b));
        }

        // ------------------------------------------------------------ worn, not glued on
        Vector3 restPos; Quaternion restRot; bool restSet;
        Vector3 lastPartVel, swayOff, swayVel, swayRot, swayRotVel, accSmooth;
        float swing, swingVel;
        Vector3 hangPivot;

        public void SetHang(Vector3 pivotLocal, float amount) { hangPivot = pivotLocal; swingHang = amount; }

        static readonly Unity.Profiling.ProfilerMarker PM_LateUpdate = new Unity.Profiling.ProfilerMarker("Carl.ArmourPiece.LateUpdate");

        void LateUpdate()
        {

            using var pmScope = PM_LateUpdate.Auto();
            if (fallen || visor || rides || part == null || part.rb == null) return;
            if (!restSet) { restPos = transform.localPosition; restRot = transform.localRotation; restSet = true; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // it hangs on straps: lags behind the body, bounces a little with each step and settles back
            Vector3 vel = part.rb.linearVelocity;
            Vector3 acc = (vel - lastPartVel) / dt;
            lastPartVel = vel;
            // physics steps and frames don't line up: smooth the felt acceleration
            accSmooth = Vector3.Lerp(accSmooth, Vector3.ClampMagnitude(acc, 40f), Mathf.Clamp01(dt * 18f));
            Vector3 accL = part.transform.InverseTransformDirection(accSmooth);
            float loose = looseness * (1f + Mathf.Clamp01(1f - strapHealth / 100f) * 1.5f);
            const float k = 70f, c = 7f;                         // soft, slow spring: it lags, overshoots a little and settles
            swayVel += (-accL * 0.22f * loose - swayOff * k - swayVel * c) * dt;
            swayOff += swayVel * dt;
            swayOff = Vector3.ClampMagnitude(swayOff, 0.02f * loose);
            swayRotVel += (new Vector3(-accL.z, 0f, accL.x) * 20f * loose - swayRot * 55f - swayRotVel * 6f) * dt;
            swayRot += swayRotVel * dt;
            swayRot = Vector3.ClampMagnitude(swayRot, 5f * loose);
            Vector3 pos = restPos + swayOff;
            Quaternion rot = restRot * Quaternion.Euler(swayRot);
            if (swingHang > 0f)
            {
                // a flap hanging from its top edge: swings with the walk and the legs pushing it
                float drive = -accL.z * 220f + accL.y * 40f + accL.x * 30f;
                swingVel += (drive * swingHang - swing * 38f - swingVel * 3.5f) * dt;
                swing = Mathf.Clamp(swing + swingVel * dt, -35f, 45f);
                Quaternion q = Quaternion.AngleAxis(swing, Vector3.right);
                pos += restRot * (hangPivot - q * hangPivot);
                rot = rot * q;
            }
            transform.localPosition = pos;
            transform.localRotation = rot;
        }

        /// <summary>Shot away: what is left of the piece drops off and is just an object on the floor.</summary>
        public void FallOff(Vector3 push)
        {
            if (fallen) return;
            fallen = true;
            ArmourSystem.All.Remove(this);
            // pouches and flaps sewn onto it come off with it
            foreach (var c in GetComponentsInChildren<ArmourPiece>())
                if (c != this && !c.fallen) { c.fallen = true; ArmourSystem.All.Remove(c); }
            var body = part != null ? part.body : null;
            transform.SetParent(null, true);
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = mesh.bounds.center;
            col.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.01f);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(area * 25f, 0.2f, 4f);
            if (part != null && part.rb != null) rb.linearVelocity = part.rb.linearVelocity;
            rb.AddForce(push.normalized * 0.6f, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 0.1f, ForceMode.Impulse);
            if (body != null)
                foreach (var bp in body.partList)
                    foreach (var c in bp.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c, true);
        }

        public bool Raycast(Vector3 originW, Vector3 dirW, float maxDist, out float dist, out Vector3 local)
        {
            dist = 0f; local = default;
            Vector3 o = transform.InverseTransformPoint(originW);
            Vector3 d = transform.InverseTransformDirection(dirW);
            float scale = d.magnitude; if (scale < 1e-6f) return false;
            d /= scale;
            if (!bounds.IntersectRay(new Ray(o, d), out float bd) || bd > maxDist * scale) return false;
            float best = float.MaxValue;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, pv);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1f / det;
                Vector3 tv = o - a;
                float u = Vector3.Dot(tv, pv) * inv; if (u < 0f || u > 1f) continue;
                Vector3 qv = Vector3.Cross(tv, e1);
                float w = Vector3.Dot(d, qv) * inv; if (w < 0f || u + w > 1f) continue;
                float tt = Vector3.Dot(e2, qv) * inv;
                if (tt > 0f && tt < best) best = tt;
            }
            if (best == float.MaxValue || best > maxDist * scale) return false;
            dist = best / scale;
            local = o + d * best;
            return true;
        }

        public void ToggleVisor()
        {
            if (!visor) return;
            isUp = !isUp;
            targetAngle = isUp ? -105f : 0f;
        }

        /// <summary>0 fully down over the face .. 1 fully up (it swings, so this moves smoothly).</summary>
        public float VisorOpen => visor ? Mathf.Clamp01(angle / -105f) : 1f;

        static readonly Unity.Profiling.ProfilerMarker PM_Update = new Unity.Profiling.ProfilerMarker("Carl.ArmourPiece.Update");

        void Update()
        {

            using var pmScope = PM_Update.Auto();
            if (!visor || Mathf.Approximately(angle, targetAngle) || transform.parent == null) return;
            float prev = angle;
            angle = Mathf.MoveTowards(angle, targetAngle, Time.deltaTime * 420f);
            transform.RotateAround(transform.parent.TransformPoint(hingePoint), transform.parent.TransformDirection(hingeAxis), angle - prev);
        }

        void OnEnable() { if (!fallen) ArmourSystem.All.Add(this); }
        void OnDisable() { ArmourSystem.All.Remove(this); }
    }

    public static class ArmourSystem
    {
        static readonly Dictionary<HumanBody, (bool vest, float at)> vestCache = new Dictionary<HumanBody, (bool, float)>();

        /// <summary>Wearing a vest (plates, webbing, pouches): it rattles and clinks when he moves.</summary>
        public static bool WearsVest(HumanBody b)
        {
            if (b == null || b.Chest == null) return false;
            if (vestCache.TryGetValue(b, out var c) && Time.time - c.at < 3f) return c.vest;
            bool v = false;
            foreach (var ap in b.Chest.GetComponentsInChildren<ArmourPiece>()) if (!ap.visor && !ap.glass) { v = true; break; }
            if (vestCache.Count > 400) vestCache.Clear();
            vestCache[b] = (v, Time.time);
            return v;
        }

        public static readonly List<ArmourPiece> All = new List<ArmourPiece>();
        static Material camoUA, vestUA, vestRU;
        /// <summary>The vest's cloth: in the trenches a plain colour of each army's own (coyote tan for Ukraine, ranger
        /// green for Russia) over the uniform's camo; elsewhere the EMR cover it always had.</summary>
        static Material CamoOf(ArmourLoadout L) => L == null ? camo : L.camo == 1 ? vestUA : L.camo == 2 ? vestRU : camo;
        static Material camo, camoCover, webbing, olive, steel, mail, glassMat, plumeMat, darkRubber, buckle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); camo = null; }

        /// <summary>Nearest armour piece along a ray (world space).</summary>
        public static ArmourPiece Raycast(Vector3 origin, Vector3 dir, float maxDist, out float dist, out Vector3 local, HumanBody only = null, HumanBody exclude = null)
        {
            ArmourPiece best = null; dist = maxDist; local = default;
            foreach (var p in All)
            {
                if (p == null || p.fallen || p.part == null) continue;
                if (only != null && p.part.body != only) continue;
                if (exclude != null && p.part.body == exclude) continue;
                // nowhere near the line (most pieces, most of the time): skip the exact test
                Vector3 c = p.transform.position, oc = c - origin;
                float along = Vector3.Dot(oc, dir);
                if (along < -0.6f || along > dist + 0.6f || (oc - dir * along).sqrMagnitude > 0.36f) continue;
                if (p.Raycast(origin, dir, dist, out float d, out Vector3 l) && d < dist) { dist = d; local = l; best = p; }
            }
            return best;
        }

        /// <summary>The hinged visor on a body's helmet (and its glass, if it has one), or null.</summary>
        public static ArmourPiece VisorOf(HumanBody b, out ArmourPiece glass)
        {
            glass = null;
            if (b == null || b.Head == null) return null;
            ArmourPiece vis = null;
            foreach (var ap in b.Head.GetComponentsInChildren<ArmourPiece>())
            {
                if (ap.fallen) continue;
                if (ap.visor && vis == null) vis = ap;
                if (ap.glass && glass == null) glass = ap;
            }
            return vis;
        }

        /// <summary>Takes everything off a body (armour, trim, pouches, plumes), so something else can be put on.</summary>
        public static void Remove(HumanBody b)
        {
            if (b == null) return;
            foreach (var bp in b.partList)
            {
                var gone = new List<GameObject>();
                foreach (Transform t in bp.transform)
                    if (t.GetComponent<ArmourPiece>() != null || t.name == "Trim" || t.name == "Plume") gone.Add(t.gameObject);
                foreach (var g in gone) { foreach (var ap in g.GetComponentsInChildren<ArmourPiece>()) All.Remove(ap); UnityEngine.Object.Destroy(g); }
            }
        }

        /// <summary>Blast: loosens straps, and shreds and rips open whatever is close to it.</summary>
        public static void Blast(Vector3 pos, float radius, float power)
        {
            foreach (var p in All.ToArray())
            {
                if (p == null || p.fallen) continue;
                // distance to the piece itself, not its pivot (a vest's pivot is the middle of the chest)
                Vector3 closest = p.transform.TransformPoint(p.bounds.ClosestPoint(p.transform.InverseTransformPoint(pos)));
                float d = Vector3.Distance(closest, pos);
                if (d >= radius) continue;
                p.Blasted(power * (1f - d / radius));
                p.BlastDamage(pos, Mathf.Pow(Mathf.Clamp01(1f - d / 1.2f), 1.5f));
            }
        }

        static void Mats_()
        {
            if (camo != null) return;
            var sh = Mats.Find("Carl/Armour");
            Material M(Color c, float metal, float gloss, float pattern)
            {
                var m = new Material(sh);
                m.SetColor("_Color", c); m.SetFloat("_Metal", metal); m.SetFloat("_Gloss", gloss); m.SetFloat("_Pattern", pattern);
                return m;
            }
            camo = M(Color.white, 0f, 0.12f, 1f);
            camoUA = M(Color.white, 0f, 0.12f, 5f);
            vestUA = M(new Color(0.55f, 0.46f, 0.33f), 0f, 0.1f, 3f);
            vestRU = M(new Color(0.28f, 0.32f, 0.24f), 0f, 0.1f, 3f);
            camoCover = M(new Color(0.36f, 0.38f, 0.28f), 0f, 0.08f, 4f);
            webbing = M(new Color(0.3f, 0.34f, 0.24f), 0f, 0.15f, 3f);
            olive = M(new Color(0.42f, 0.42f, 0.3f), 0.05f, 0.3f, 0f);
            steel = M(new Color(0.72f, 0.73f, 0.75f), 0.95f, 0.78f, 0f);
            mail = M(new Color(0.55f, 0.56f, 0.58f), 0.85f, 0.5f, 2f);
            plumeMat = M(new Color(0.6f, 0.06f, 0.06f), 0f, 0.15f, 0f);
            darkRubber = M(new Color(0.08f, 0.08f, 0.09f), 0f, 0.3f, 0f);
            buckle = M(new Color(0.06f, 0.06f, 0.06f), 0.1f, 0.5f, 0f);
            glassMat = new Material(Mats.Find("Carl/Glass"));
        }

        // ============================================================ equip

        public static void Equip(HumanBody b, ArmourLoadout L)
        {
            if (L == null) return;
            Mats_();
            EquipPieces(b, L);
            // cosmetic trim (webbing, straps, pouches, rim, plume) rides on the main piece of that part, so it moves
            // with it and falls with it
            foreach (var bp in b.partList)
            {
                ArmourPiece main = null;
                foreach (Transform t in bp.transform) { var ap = t.GetComponent<ArmourPiece>(); if (ap != null && !ap.visor && !ap.glass && !ap.rides && ap.swingHang <= 0f) { main = ap; break; } }
                if (main == null) continue;
                var trims = new List<Transform>();
                foreach (Transform t in bp.transform) { var tp = t.GetComponent<ArmourPiece>(); if (((t.name == "Trim" || t.name == "Plume") && tp == null) || (tp != null && tp.rides)) trims.Add(t); }
                foreach (var t in trims) t.SetParent(main.transform, true);
            }
        }

        /// <summary>Cordura with magazines in it: slows a round a little, stops nothing.</summary>
        const float PouchClass = 1.2f;

        /// <summary>A pouch is its own piece (so rounds rip it and can shoot it off) but rides on the vest.</summary>
        static ArmourPiece Pouch(ArmourPiece p)
        {
            if (p == null) return null;
            p.rides = true;
            p.looseness = 0f;
            return p;
        }

        static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        static void EquipPieces(HumanBody b, ArmourLoadout L)
        {
            float br = Mathf.Clamp(L.plateClass, 2, 6);
            // GOST Br class -> our armour class (pen scale): Br2 stops pistols, Br4 stops 7.62x39 PS, Br5 rifle AP, Br6 .338 / some .50
            float plate = br == 2 ? 2.6f : br == 3 ? 3.6f : br == 4 ? 4.8f : br == 5 ? 6f : 7f;
            const float soft = 2.2f, knight = 2.8f;

            if (L.vest == VestKind.Russian6B45)
            {
                // the carrier sits a couple of centimetres off the body; where the plates are it bulges out and is
                // thick, around them it is soft padded cloth; arm holes cut out under the shoulders
                foreach (var id in new[] { PartId.Chest, PartId.Abdomen })
                {
                    var p = b[id];
                    bool chest = id == PartId.Chest;
                    float top = chest ? 0.8f : 1.2f;
                    bool ArmHole(Vector3 u) => chest && Mathf.Abs(u.x) > 0.58f && u.y > 0.05f;
                    // plate bulge: thickest in the middle of the plate, easing off to the soft edge of the carrier
                    float Bulge(Vector3 u) => 0.016f * Smooth(0.95f, 0.55f, Mathf.Abs(u.x)) * Smooth(top, top - 0.35f, u.y) * (chest ? 1f : Smooth(-1.05f, -0.6f, u.y));
                    var f = Build(p, "6B45 FRONT", plate, CamoOf(L), 0.022f, 0.014f, (u, n) => n.z > 0.2f && u.y < top && Mathf.Abs(u.x) < 0.97f && !ArmHole(u) && (L.sideArmour || Mathf.Abs(n.x) < 0.75f), false, false, (u, n) => Bulge(u));
                    var k = Build(p, "6B45 BACK", plate, CamoOf(L), 0.022f, 0.014f, (u, n) => n.z < -0.2f && u.y < top + 0.05f && Mathf.Abs(u.x) < 0.97f && !ArmHole(u) && (L.sideArmour || Mathf.Abs(n.x) < 0.75f), false, false, (u, n) => Bulge(u));
                    if (f) f.plateUnder = true;
                    if (k) k.plateUnder = true;
                    if (L.sideArmour)
                    {
                        var s = Build(p, "6B45 SIDE", plate, CamoOf(L), 0.024f, 0.012f, (u, n) => Mathf.Abs(n.z) <= 0.25f && Mathf.Abs(n.x) > 0.5f && u.y < (chest ? 0.0f : top) && u.y > -0.95f);
                        if (s) s.plateUnder = true;
                    }
                    else
                        // the elastic cummerbund still wraps round the sides
                        Build(p, null, 0f, webbing, 0.026f, 0.01f, (u, n) => Mathf.Abs(n.z) <= 0.3f && Mathf.Abs(n.x) > 0.5f && u.y < (chest ? -0.2f : 0.6f) && u.y > (chest ? -1f : -0.3f));
                    // MOLLE webbing rows across front and back
                    Build(p, null, 0f, webbing, 0.04f, 0.006f, (u, n) => Mathf.Abs(n.z) > 0.45f && Mathf.Abs(u.x) < 0.7f && u.y < top - 0.12f && !ArmHole(u) && Frac((u.y + 1f) * 4.2f) < 0.3f, false, false, (u, n) => Bulge(u));
                    if (chest)
                    {
                        // padded shoulder straps over the top, joining front and back
                        Build(p, null, 0f, webbing, 0.03f, 0.016f, (u, n) => Mathf.Abs(u.x) > 0.28f && Mathf.Abs(u.x) < 0.6f && u.y > 0.62f);
                        // the adjustable buckles where the shoulder straps meet the front panel
                        Build(p, null, 0f, buckle, 0.045f, 0.012f, (u, n) => n.z > 0.3f && Mathf.Abs(Mathf.Abs(u.x) - 0.44f) < 0.07f && u.y > 0.5f && u.y < 0.66f);
                        if (L.pouches)
                            // admin pouch high on the chest
                            Pouch(Build(p, "ADMIN POUCH", PouchClass, CamoOf(L), 0.055f, 0.03f, (u, n) => n.z > 0.6f && Mathf.Abs(u.x) < 0.32f && u.y > -0.25f && u.y < 0.3f, false, false, (u, n) => Bulge(u)));
                    }
                    else
                    {
                        // cummerbund buckles at the sides
                        Build(p, null, 0f, buckle, 0.042f, 0.012f, (u, n) => Mathf.Abs(n.x) > 0.6f && Mathf.Abs(n.z) < 0.5f && u.y > -0.2f && u.y < 0.15f);
                        if (L.pouches)
                        {
                            // three mag pouches on the front, a flat pouch on the back
                            for (int i = -1; i <= 1; i++)
                            {
                                float cx = i * 0.33f;
                                var pouch = Pouch(Build(p, "MAG POUCH", PouchClass, CamoOf(L), 0.075f, 0.055f, (u, n) => n.z > 0.45f && Mathf.Abs(u.x - cx) < 0.13f && u.y > -0.62f && u.y < 0.12f, false, false, (u, n) => Bulge(u)));
                                // the flap over its top is sewn to the pouch: shot off, they go together
                                var flap = Pouch(Build(p, "POUCH FLAP", PouchClass * 0.5f, webbing, 0.082f, 0.01f, (u, n) => n.z > 0.45f && Mathf.Abs(u.x - cx) < 0.135f && u.y > 0.0f && u.y < 0.2f, false, false, (u, n) => Bulge(u)));
                                if (pouch != null && flap != null) flap.transform.SetParent(pouch.transform, true);
                            }
                            Pouch(Build(p, "BACK POUCH", PouchClass, CamoOf(L), 0.065f, 0.045f, (u, n) => n.z < -0.55f && Mathf.Abs(u.x) < 0.42f && u.y > -0.6f && u.y < 0.5f, false, false, (u, n) => Bulge(u)));
                        }
                    }
                }
                if (L.collar)
                {
                    var c = Build(b[PartId.Neck], "6B45 COLLAR", soft, CamoOf(L), 0.028f, 0.022f, (u, n) => u.y < 0.3f);
                    if (c) c.looseness = 1.4f;
                }
                if (L.groinFlap)
                {
                    var g = Build(b.Pelvis, "6B45 GROIN FLAP", soft, CamoOf(L), 0.03f, 0.02f, (u, n) => n.z > 0.25f && Mathf.Abs(u.x) < 0.42f && u.y < 0.45f);
                    if (g != null) g.SetHang(new Vector3(g.mesh.bounds.center.x, g.mesh.bounds.max.y, g.mesh.bounds.max.z - 0.01f), 1f);
                }
                if (L.armGuards)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        // the shoulder protectors that clip to the carrier: a padded aramid sleeve over the top of
                        // the shoulder and down the outside of the upper arm, open on the inside under the armpit
                        var arm = b[s < 0 ? PartId.UpperArmL : PartId.UpperArmR];
                        // (Br2 aramid: stops 9 mm ball and fragments; armour piercing and rifle rounds go through)
                        var ag = Build(arm, "6B45 ARM GUARD", 3.1f, CamoOf(L), 0.016f, 0.012f, (u, n) => u.y > -0.45f && Vector3.Dot(n, new Vector3(-s, 0f, 0f)) < 0.45f, true);
                        if (ag) ag.looseness = 1.15f;
                        // its strap round the arm
                        Build(arm, null, 0f, webbing, 0.03f, 0.008f, (u, n) => u.y > -0.52f && u.y < -0.4f, true);
                    }
            }
            else if (L.vest == VestKind.KnightCuirass)
            {
                var a = Build(b.Chest, "BREASTPLATE", knight, steel, 0.018f, 0.004f, (u, n) => u.y < 0.9f && !(Mathf.Abs(u.x) > 0.6f && u.y > 0.1f));
                var c = Build(b[PartId.Abdomen], "PLACKART", knight, steel, 0.018f, 0.004f, (u, n) => true);
                if (a) a.looseness = 0.4f;
                if (c) c.looseness = 0.4f;
            }

            if (L.tassets)
            {
                var t = Build(b.Pelvis, "TASSETS", knight, steel, 0.02f, 0.004f, (u, n) => n.z > -0.35f && u.y > -0.55f);
                if (t) t.looseness = 0.6f;
            }
            for (int s = -1; s <= 1; s += 2)
            {
                PartId Pick(PartId l, PartId r) => s < 0 ? l : r;
                if (L.pauldrons) Build(b[Pick(PartId.UpperArmL, PartId.UpperArmR)], "PAULDRON", knight, steel, 0.02f, 0.004f, (u, n) => u.y > -0.15f, true);
                if (L.vambraces) Build(b[Pick(PartId.ForearmL, PartId.ForearmR)], "VAMBRACE", knight, steel, 0.012f, 0.003f, (u, n) => Mathf.Abs(u.y) < 0.85f, true);
                if (L.gauntlets) Build(b[Pick(PartId.HandL, PartId.HandR)], "GAUNTLET", knight, steel, 0.009f, 0.003f, (u, n) => true, true);
                if (L.cuisses) Build(b[Pick(PartId.ThighL, PartId.ThighR)], "CUISSE", knight, steel, 0.014f, 0.004f, (u, n) => Mathf.Abs(u.y) < 0.85f, true);
                if (L.greaves) Build(b[Pick(PartId.ShinL, PartId.ShinR)], "GREAVE", knight, steel, 0.013f, 0.004f, (u, n) => Mathf.Abs(u.y) < 0.88f, true);
                if (L.sabatons) Build(b[Pick(PartId.FootL, PartId.FootR)], "SABATON", knight, steel, 0.01f, 0.003f, (u, n) => n.y > -0.4f, true);
            }
            foreach (var bp in b.partList)
                foreach (var ap in bp.GetComponentsInChildren<ArmourPiece>())
                    if (ap.steel && ap.label != "ALTYN HELMET" && ap.label != "6B47 HELMET") ap.looseness = Mathf.Min(ap.looseness, 0.5f);

            var head = b.Head;
            if (L.helmet == HelmetKind.Spetsnaz)
            {
                // Altyn: a thick, smooth olive titanium dome over the top, back and ears, with a rubber rim
                var hm = Build(head, "ALTYN HELMET", 3.6f, olive, 0.032f, 0.014f, (u, n) => u.y > -0.25f && !(n.z > 0.45f && u.y < 0.5f));
                if (hm) hm.looseness = 0.35f;
                Build(head, null, 0f, darkRubber, 0.036f, 0.012f, (u, n) => u.y > -0.32f && u.y < -0.18f && !(n.z > 0.45f));
                Chinstrap(head, darkRubber);
                if (L.visor)
                {
                    var vis = Build(head, "ALTYN VISOR", 3.6f, olive, 0.052f, 0.012f, (u, n) => n.z > 0.3f && u.y < 0.55f && u.y > -0.85f && !(Mathf.Abs(u.x) < 0.6f && u.y > -0.25f && u.y < 0.32f), false, true);
                    // the glass is cut bigger than the window and sits inside the frame's thickness, so it tucks under
                    // the frame all the way round (no gaps where the frame's and the glass's triangles don't meet)
                    var win = Build(head, "VISOR GLASS", 3.2f, glassMat, 0.046f, 0.004f, (u, n) => n.z > 0.22f && Mathf.Abs(u.x) < 0.74f && u.y > -0.36f && u.y < 0.43f, false, true);
                    if (win != null) { win.glass = true; win.transform.SetParent(vis != null ? vis.transform : head.transform, true); }
                    SetupHinge(head, vis, L.visorDown);
                    if (win != null && vis == null) SetupHinge(head, win, L.visorDown);
                }
            }
            else if (L.helmet == HelmetKind.Ratnik)
            {
                // 6B47: a thick aramid shell under a baggy cloth cover, a band round it, rails, a padded harness
                var hm = Build(head, "6B47 HELMET", 3.0f, camoCover, 0.036f, 0.02f, (u, n) => u.y > -0.32f && !(n.z > 0.5f && u.y < 0.38f), false, false, (u, n) => 0.004f * Mathf.PerlinNoise(u.x * 3f + 5f, u.y * 3f + u.z * 2f));
                if (hm) hm.looseness = 0.45f;
                Build(head, null, 0f, webbing, 0.041f, 0.008f, (u, n) => u.y > -0.05f && u.y < 0.12f && !(n.z > 0.5f && u.y < 0.38f));
                Build(head, null, 0f, buckle, 0.041f, 0.008f, (u, n) => Mathf.Abs(n.x) > 0.85f && u.y > -0.12f && u.y < 0.05f && Mathf.Abs(u.z) < 0.2f);
                Build(head, null, 0f, darkRubber, 0.04f, 0.016f, (u, n) => u.y > -0.36f && u.y < -0.26f && !(n.z > 0.5f));
                Chinstrap(head, webbing);
            }
            else if (L.helmet == HelmetKind.Knight)
            {
                var hm = Build(head, "BASCINET", knight, steel, 0.02f, 0.004f, (u, n) => u.y > -0.15f && !(n.z > 0.5f && u.y < 0.55f));
                if (hm) hm.looseness = 0.3f;
                if (L.visor)
                {
                    // face plate with an eye slit and breathing holes
                    var vis = Build(head, "VISOR", knight, steel, 0.04f, 0.004f, (u, n) => n.z > 0.25f && u.y < 0.62f && u.y > -0.9f && !(u.y > 0.12f && u.y < 0.24f && Mathf.Abs(u.x) < 0.75f), false, true);
                    SetupHinge(head, vis, L.visorDown);
                }
                if (L.aventail)
                {
                    var av = Build(b[PartId.Neck], "AVENTAIL", 2.2f, mail, 0.022f, 0.004f, (u, n) => true);
                    if (av) av.looseness = 1.3f;
                    Build(head, null, 0f, mail, 0.016f, 0.003f, (u, n) => u.y < -0.1f && u.y > -0.7f && n.z < 0.3f);
                }
                if (L.plume) BuildPlume(head);
            }
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        static void SetupHinge(BodyPart head, ArmourPiece vis, bool down)
        {
            if (vis == null) return;
            vis.visor = true;
            var bb = head.skin.GetComponent<MeshFilter>().sharedMesh.bounds;
            vis.hingePoint = new Vector3(0f, bb.center.y + bb.extents.y * 0.32f, bb.center.z);
            vis.hingeAxis = Vector3.right;
            if (!down) vis.ToggleVisor();
        }

        /// <summary>A Y-harness chin strap from above each ear, down the cheeks and under the chin.</summary>
        static void Chinstrap(BodyPart head, Material mat)
        {
            var bb = head.skin.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 c = bb.center, e = bb.extents;
            var verts = new List<Vector3>(); var tris = new List<int>();
            // a flat strap (a thin box section) that follows the face, visible from any side
            void Strip(Vector3[] pts, float w, float th)
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector3 a = pts[i], bq = pts[i + 1];
                    Vector3 fwd = (bq - a).normalized;
                    Vector3 outN = Vector3.Scale(a - c, new Vector3(1f / (e.x * e.x), 1f / (e.y * e.y), 1f / (e.z * e.z))).normalized;
                    Vector3 side = Vector3.Cross(fwd, outN).normalized * w * 0.5f;
                    Vector3 o = outN * th;
                    int b0 = verts.Count;
                    verts.AddRange(new[] { a - side, a + side, a + side + o, a - side + o, bq - side, bq + side, bq + side + o, bq - side + o });
                    int[] q = { 0, 1, 5, 0, 5, 4,  3, 7, 6, 3, 6, 2,  0, 4, 7, 0, 7, 3,  1, 2, 6, 1, 6, 5 };
                    foreach (int k in q) tris.Add(b0 + k);
                }
            }
            for (int s = -1; s <= 1; s += 2)
            {
                // from the helmet rim above the ear, along the cheek to under the chin, hugging the face
                Vector3 u0 = new Vector3(s * 1f, -0.25f, -0.05f).normalized, u1 = new Vector3(0f, -0.9f, 0.42f).normalized;
                var pts = new Vector3[10];
                for (int k = 0; k < pts.Length; k++)
                {
                    Vector3 u = Vector3.Slerp(u0, u1, k / (pts.Length - 1f));
                    pts[k] = c + Vector3.Scale(u, e) * 1.03f;
                }
                Strip(pts, 0.02f, 0.004f);
            }
            var go = new GameObject("Trim");
            go.transform.SetParent(head.transform, false);
            var m = new Mesh { name = "Chinstrap" }; m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var cols = new Color[verts.Count]; for (int i = 0; i < cols.Length; i++) cols[i] = Color.white; m.colors = cols;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void BuildPlume(BodyPart head)
        {
            var bb = head.skin.GetComponent<MeshFilter>().sharedMesh.bounds;
            var go = new GameObject("Plume");
            go.transform.SetParent(head.transform, false);
            var verts = new List<Vector3>(); var tris = new List<int>();
            // a sweep of feathers arching back from the crown
            for (int f = 0; f < 9; f++)
            {
                float side = (f - 4) * 0.008f;
                int b0 = verts.Count;
                for (int k = 0; k <= 8; k++)
                {
                    float t = k / 8f;
                    Vector3 c = new Vector3(side, bb.max.y + 0.03f + Mathf.Sin(t * 2.6f) * 0.12f, bb.center.z - t * 0.22f + 0.02f);
                    float w = 0.018f * Mathf.Sin(t * Mathf.PI) + 0.004f;
                    verts.Add(c + Vector3.left * w * 0.3f + Vector3.up * w); verts.Add(c - Vector3.left * w * 0.3f - Vector3.up * w * 0.2f);
                    if (k > 0) { int a = b0 + (k - 1) * 2; tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3, a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
                }
            }
            var m = new Mesh(); m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = plumeMat;
        }

        /// <summary>
        /// Builds a shell around the part's skin: every skin triangle whose vertices pass `select` (normalised
        /// position u in -1..1 of the part's bounds, outward normal n, both part-local) is lifted `offset` (+ bulge)
        /// off the skin with `thickness` (+ bulge) of material under it, and closed at its edges. limbAxis: u.y
        /// measured along the limb toward its joint.
        /// </summary>
        static ArmourPiece Build(BodyPart part, string label, float cls, Material mat, float offset, float thickness,
                                 Func<Vector3, Vector3, bool> select, bool limbAxis = false, bool own = false,
                                 Func<Vector3, Vector3, float> bulge = null)
        {
            if (part == null || part.skin == null) return null;
            var src = part.skin.GetComponent<MeshFilter>().sharedMesh;
            var sv = src.vertices; var sn = src.normals; var st = src.triangles;
            var bb = src.bounds;
            Vector3 c = bb.center, e = bb.extents;
            // limbs: measure "up" toward the joint (shoulder / hip / knee end)
            Vector3 up = Vector3.up;
            if (limbAxis && part.jointLocal.sqrMagnitude > 1e-6f)
            {
                Vector3 j = part.jointLocal - c;
                if (Mathf.Abs(j.z) > Mathf.Abs(j.y) && Mathf.Abs(j.z) > Mathf.Abs(j.x)) up = new Vector3(0, 0, Mathf.Sign(j.z));
                else up = new Vector3(0, Mathf.Sign(j.y), 0);
            }

            // weld by position so the shell's edges are real edges, not UV seams (worked out once per part: every
            // Earl's skin is the same shape when he puts it on)
            if (!weldCache.TryGetValue(part.id, out var wc) || wc.n != sv.Length)
            {
                var key = new Dictionary<Vector3Int, int>();
                var weld0 = new int[sv.Length];
                var wPos0 = new List<Vector3>(); var wNrm0 = new List<Vector3>();
                for (int i = 0; i < sv.Length; i++)
                {
                    var k = new Vector3Int(Mathf.RoundToInt(sv[i].x * 2000f), Mathf.RoundToInt(sv[i].y * 2000f), Mathf.RoundToInt(sv[i].z * 2000f));
                    if (!key.TryGetValue(k, out int w)) { w = wPos0.Count; key[k] = w; wPos0.Add(sv[i]); wNrm0.Add(Vector3.zero); }
                    weld0[i] = w;
                    wNrm0[w] += sn[i];
                }
                for (int i = 0; i < wNrm0.Count; i++) wNrm0[i] = wNrm0[i].normalized;
                weldCache[part.id] = wc = (sv.Length, weld0, wPos0, wNrm0);
            }
            var weld = wc.weld; var wPos = wc.pos; var wNrm = wc.nrm;
            var pass = new bool[wPos.Count];
            var lift = new float[wPos.Count];
            for (int i = 0; i < wPos.Count; i++)
            {
                Vector3 n = wNrm[i];
                Vector3 u = new Vector3((wPos[i].x - c.x) / Mathf.Max(1e-4f, e.x), (wPos[i].y - c.y) / Mathf.Max(1e-4f, e.y), (wPos[i].z - c.z) / Mathf.Max(1e-4f, e.z));
                if (up.z != 0f) u = new Vector3(u.x, u.z * up.z, -u.y * up.z);
                else if (up.y < 0f) u = new Vector3(u.x, -u.y, -u.z);
                pass[i] = select(u, n);
                lift[i] = bulge != null ? bulge(u, n) : 0f;
            }
            // the same piece cut the same way (same part, same options) has been made before: the same shape
            ulong hash = 1469598103934665603UL;
            void Mix(long x) { hash ^= (ulong)x; hash *= 1099511628211UL; }
            Mix((long)part.id); Mix((label ?? "").GetHashCode()); Mix(Mathf.RoundToInt(offset * 1e5f)); Mix(Mathf.RoundToInt(thickness * 1e5f)); Mix(bulge != null ? 1 : 0);
            Mix(sv.Length);
            for (int i = 0; i < pass.Length; i++) { Mix(pass[i] ? 1 : 0); if (pass[i] && bulge != null) Mix(Mathf.RoundToInt(lift[i] * 1e5f)); }
            if (shellCache.TryGetValue(hash, out var made) && made.mesh != null)
                return Finish(part, label, cls, mat, made.mesh, made.v, made.t, made.area, true);
            var faces = new List<int>();
            for (int i = 0; i < st.Length; i += 3)
            {
                int a = weld[st[i]], b2 = weld[st[i + 1]], c2 = weld[st[i + 2]];
                if (a == b2 || b2 == c2 || a == c2) continue;
                if (pass[a] && pass[b2] && pass[c2]) faces.Add(a); else continue;
                faces.Add(b2); faces.Add(c2);
            }
            if (faces.Count < 9) return null;

            // outer and inner surfaces, plus walls along every open edge
            var map = new Dictionary<int, int>();
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            var thick = new List<float>();
            int Get(int w)
            {
                if (!map.TryGetValue(w, out int i)) { i = verts.Count; map[w] = i; verts.Add(wPos[w] + wNrm[w] * (offset + lift[w])); norms.Add(wNrm[w]); thick.Add(thickness + lift[w]); }
                return i;
            }
            for (int i = 0; i < faces.Count; i++) tris.Add(Get(faces[i]));
            int nOuter = verts.Count;
            for (int i = 0; i < nOuter; i++) { verts.Add(verts[i] - norms[i] * thick[i]); norms.Add(-norms[i]); }
            int nf = tris.Count;
            float area = 0f;
            for (int i = 0; i < nf; i += 3) area += Vector3.Cross(verts[tris[i + 1]] - verts[tris[i]], verts[tris[i + 2]] - verts[tris[i]]).magnitude * 0.5f;
            for (int i = 0; i < nf; i += 3) { tris.Add(tris[i] + nOuter); tris.Add(tris[i + 2] + nOuter); tris.Add(tris[i + 1] + nOuter); }
            var edgeCount = new Dictionary<long, int>();
            long EK(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            for (int i = 0; i < nf; i += 3)
                for (int k = 0; k < 3; k++) { long ek = EK(tris[i + k], tris[i + (k + 1) % 3]); edgeCount.TryGetValue(ek, out int cnt); edgeCount[ek] = cnt + 1; }
            for (int i = 0; i < nf; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = tris[i + k], b = tris[i + (k + 1) % 3];
                    if (edgeCount[EK(a, b)] != 1) continue;
                    // separate vertices so the rim gets crisp normals
                    Vector3 rimN = Vector3.Cross(verts[b] - verts[a], norms[a]).normalized;
                    int r0 = verts.Count;
                    verts.Add(verts[a]); verts.Add(verts[b]); verts.Add(verts[b + nOuter]); verts.Add(verts[a + nOuter]);
                    norms.Add(rimN); norms.Add(rimN); norms.Add(rimN); norms.Add(rimN);
                    tris.AddRange(new[] { r0, r0 + 2, r0 + 1, r0, r0 + 3, r0 + 2 });
                }
            // smooth the shading of the bulged surfaces (the skin's normals no longer match them)
            var mesh = new Mesh { name = label ?? "Trim" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
            if (bulge != null)
            {
                mesh.RecalculateNormals();
                var nn = mesh.normals;
                // keep the crisp rim normals
                for (int i = nOuter * 2; i < nn.Length; i++) nn[i] = norms[i];
                mesh.normals = nn;
            }
            var cols = new Color[verts.Count]; for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            mesh.colors = cols;
            mesh.RecalculateBounds();
            var vArr = mesh.vertices; var tArr = tris.ToArray();
            shellCache[hash] = (mesh, vArr, tArr, area);
            return Finish(part, label, cls, mat, mesh, vArr, tArr, area, true);
        }

        // shapes worked out once and shared (a squad of Earls in the same kit used to have every plate, pouch and
        // strap built from scratch: ~37 ms each, spawning a wave stalled the game)
        static readonly Dictionary<PartId, (int n, int[] weld, List<Vector3> pos, List<Vector3> nrm)> weldCache =
            new Dictionary<PartId, (int, int[], List<Vector3>, List<Vector3>)>();
        static readonly Dictionary<ulong, (Mesh mesh, Vector3[] v, int[] t, float area)> shellCache =
            new Dictionary<ulong, (Mesh, Vector3[], int[], float)>();

        /// <summary>The piece itself, on the part, wearing the (shared) shape.</summary>
        static ArmourPiece Finish(BodyPart part, string label, float cls, Material mat, Mesh mesh, Vector3[] v, int[] t, float area, bool shared)
        {
            var go = new GameObject(label ?? "Trim");
            go.transform.SetParent(part.transform, false);
            go.layer = part.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (label == null) return null;      // cosmetic only

            var piece = go.AddComponent<ArmourPiece>();
            piece.label = label; piece.part = part; piece.cls = cls; piece.mesh = mesh; piece.sharedMesh = shared;
            piece.steel = mat == steel || mat == olive || mat == mail;
            piece.v = v; piece.t = t;
            piece.bounds = mesh.bounds; piece.bounds.Expand(0.01f);
            piece.area = area;
            return piece;
        }
    }
}
