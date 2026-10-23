using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>Runtime state of one anatomical structure.</summary>
    public class Organ
    {
        public OrganDef def;
        public BodyPart part;
        public float health = 1f;
        public bool severed;              // vessel transected / cord cut
        public Renderer xray;
        public Renderer interior;
        public readonly List<Renderer> xrayExtra = new List<Renderer>();
        public bool split;            // bone visibly snapped in two
        public Renderer reveal;       // the real bone/organ seen through wounds
        /// <summary>Bone breaks seen through wounds: x = position along the bone (0..1), y = severity, z = seed.</summary>
        public readonly List<Vector4> breaks = new List<Vector4>();
        public MaterialPropertyBlock mpb;

        public bool IsBone => def.kind == TissueKind.Bone || def.kind == TissueKind.Spine;
        public bool Fractured => IsBone && health < 0.6f;
        public bool BadlyBroken => IsBone && health < 0.25f;
        public bool Destroyed => health <= 0.05f;
        public float Damage => 1f - health;

        public string StatusText()
        {
            if (def.kind == TissueKind.Spine)
            {
                if (severed) return "CORD SEVERED";
                if (health < 0.6f) return "VERTEBRA FRACTURED";
                return health < 1f ? "BRUISED" : "INTACT";
            }
            if (IsBone)
            {
                if (BadlyBroken) return "BADLY BROKEN";
                if (Fractured) return "FRACTURED";
                return health < 1f ? "CHIPPED" : "INTACT";
            }
            if (def.kind == TissueKind.Artery || def.kind == TissueKind.Vein)
                return severed ? "SEVERED" : health < 1f ? "NICKED" : "INTACT";
            if (Destroyed) return "DESTROYED";
            if (health < 0.4f) return "SEVERELY DAMAGED";
            if (health < 0.8f) return "DAMAGED";
            return health < 1f ? "BRUISED" : "OK";
        }
    }

    public enum WoundType { Entry, Exit, Stab, Laceration, Abrasion, Bruise, Amputation, Blast, Internal }

    public class Wound
    {
        public BodyPart part;
        public Vector3 localPos;
        public Vector3 localNormal;
        public WoundType type;
        public float extRate;        // ml/s leaving the body
        public float intRate;        // ml/s into body cavities
        public bool arterial;
        public bool venous;
        public float age;
        public float clotRate = 1f / 90f; // fraction of bleeding stopped per second
        public string desc;
        public bool bandaged, sutured;   // treated (MedKit)
        public float emitAcc;
        public float pooled, poolTimer;   // blood run onto the floor under this wound while lying in it

        public string Severity()
        {
            float r = extRate + intRate;
            if (arterial && r > 4f) return "ARTERIAL BLEED";
            if (r > 20f) return "SEVERE BLEED";
            if (r > 8f) return "HEAVY BLEED";
            if (r > 2f) return "MODERATE BLEED";
            if (r > 0.05f) return "LIGHT BLEED";
            return "NOT BLEEDING";
        }
    }

    /// <summary>One rigid segment of a human ragdoll. Owns its anatomy, wounds and flesh mesh.</summary>
    public class BodyPart : MonoBehaviour
    {
        public PartId id;
        public HumanBody body;
        public Rigidbody rb;
        public ConfigurableJoint joint;
        public BodyPart parent;
        public Collider col;
        public FleshDeformer flesh;
        public MeshRenderer skin;
        public Material skinMaterial;
        public Transform xrayRoot;
        public readonly List<Organ> organs = new List<Organ>();
        public readonly List<Wound> wounds = new List<Wound>();
        public float tissueDamage;        // soft tissue 0..1
        public float bloodiness;          // how much blood is on the surface (for smears)
        public bool severed;              // detached from the body
        public float halfLength;
        public Vector3 jointLocal;        // joint anchor in this part's local space

        Vector3 lastSmearPos;
        float lastImpactTime;
        /// <summary>When this body was made: for its first moments it is still being set down and settled (spawned
        /// against a wall, the boards of a trench), and the world can't hurt it.</summary>
        float born;
        void Awake() { born = Time.time; }

        public string Name => Anatomy.PartName(id);
        public float OutRate { get { float r = 0; foreach (var w in wounds) r += w.extRate; return r; } }
        public float InRate { get { float r = 0; foreach (var w in wounds) r += w.intRate; return r; } }

        public Organ Find(string name)
        {
            foreach (var o in organs) if (o.def.name == name) return o;
            return null;
        }

        /// <summary>
        /// The change of speed (m/s) a knock must cause before it does anything at all: about a 1.5 m fall. Slight
        /// drops, stumbles and bumps are below it.
        /// </summary>
        public const float ImpactHurtSpeed = 5.5f;

        void OnCollisionEnter(Collision c)
        {
            if (body == null || body.Dead && body.TimeSinceDeath > 8f) return;
            var other = c.rigidbody ? c.rigidbody.GetComponent<BodyPart>() : null;
            if (other != null && other.body == body) return;
            float speed = c.relativeVelocity.magnitude;
            if (rb != null)
            {
                // every body is driven hard by its muscles: a foot planted mid-stride, a hand brushing a wall, a
                // knee bumping a crate all show big contact speeds without anything much happening. What hurts is
                // how much the knock actually changed this part's speed - the impulse it took - so only real falls,
                // throws and hits count, and hands and feet (built to take knocks) need a harder one.
                speed = Mathf.Min(speed, c.impulse.magnitude / rb.mass);
                bool extremity = id == PartId.FootL || id == PartId.FootR || id == PartId.HandL || id == PartId.HandR || id == PartId.ShinL || id == PartId.ShinR || id == PartId.ForearmL || id == PartId.ForearmR;
                if (other == null && extremity) speed -= 3f;
                // walking or standing into a wall is a bump, not an injury: only a body that is falling (or thrown)
                // gets hurt by the world
                if (other == null && body.ragdoll != null && body.ragdoll.mode != BodyMode.Down && speed < 7f) return;
            }
            if (other == null && Time.time - born < 2f) return;
            if (speed < ImpactHurtSpeed || Time.time - lastImpactTime < 0.15f) return;
            lastImpactTime = Time.time;
            var cp = c.GetContact(0);
            body.damage.ApplyImpact(this, cp.point, cp.normal, speed, other != null);
        }

        float lastContactTime = -1f;
        float smearSeed;
        Vector3 smearDir;          // direction of the drag so far (a wobble back and forth is not a drag)
        float smearRun;            // metres dragged consistently in that direction
        float printTimer;

        /// <summary>Contact width of this part when dragged flat along a surface.</summary>
        float DragWidth
        {
            get
            {
                switch (id)
                {
                    case PartId.Chest: return 0.24f;
                    case PartId.Pelvis: case PartId.Abdomen: return 0.2f;
                    case PartId.Head: return 0.12f;
                    case PartId.ThighL: case PartId.ThighR: return 0.11f;
                    case PartId.ShinL: case PartId.ShinR: case PartId.UpperArmL: case PartId.UpperArmR: return 0.08f;
                    case PartId.Neck: case PartId.ForearmL: case PartId.ForearmR: return 0.065f;
                    default: return 0.05f;
                }
            }
        }

        void OnCollisionStay(Collision c)
        {
            if (body == null || BloodSystem.Instance == null) return;
            if (c.rigidbody && c.rigidbody.GetComponent<BodyPart>()) return;
            var bs = BloodSystem.Instance;
            var cp = c.GetContact(0);
            Vector3 v = rb.GetPointVelocity(cp.point);
            Vector3 slide = Vector3.ProjectOnPlane(v, cp.normal);

            // a new drag starts when contact was lost for a moment
            if (Time.time - lastContactTime > 0.3f) { lastSmearPos = cp.point; smearSeed = Random.value * 97f; }
            lastContactTime = Time.time;
            // Only a real drag (the whole body being pulled or sliding along) leaves a drag mark. A body twitching,
            // writhing or rolling in place just presses blood into the floor: blotchy prints and a growing pool.
            Vector3 bodyV = body.Pelvis != null && body.Pelvis.rb != null ? Vector3.ProjectOnPlane(body.Pelvis.rb.linearVelocity, cp.normal) : slide;
            bool dragged = bodyV.magnitude > 0.45f && slide.magnitude > 0.3f && Vector3.Dot(slide.normalized, bodyV.normalized) > 0.6f;
            if (!dragged)
            {
                printTimer -= Time.fixedDeltaTime;
                if (bloodiness > 0.25f && printTimer <= 0f)
                {
                    // blood transferred where the bloody part presses: a soft blot, not a streak
                    bs.Ooze(cp.point, cp.normal, DragWidth * Random.Range(0.3f, 0.45f), Mathf.Min(0.3f, bloodiness * 0.18f));
                    bloodiness *= 0.95f;
                    printTimer = Random.Range(0.25f, 0.6f);
                }
                lastSmearPos = cp.point; smearRun = 0f; smearDir = Vector3.zero;
                return;
            }

            float step = Vector3.Distance(cp.point, lastSmearPos);
            if (step < 0.03f) return;
            if (step > 0.5f) { lastSmearPos = cp.point; smearRun = 0f; return; }
            Vector3 dirNow = Vector3.ProjectOnPlane(cp.point - lastSmearPos, cp.normal).normalized;
            if (smearDir != Vector3.zero && Vector3.Dot(dirNow, smearDir) < 0.5f) { lastSmearPos = cp.point; smearRun = 0f; smearDir = dirNow; return; }
            smearDir = smearDir == Vector3.zero ? dirNow : Vector3.Slerp(smearDir, dirNow, 0.4f);
            smearRun += step;

            // sliding through blood on the ground soaks the part and drags the pool along
            float width = DragWidth * Random.Range(0.9f, 1.1f);
            float picked = bs.Wipe(cp.point, cp.normal, width * 0.45f, 0.25f + step);
            if (picked > 0f)
            {
                bloodiness = Mathf.Min(1.2f, bloodiness + picked * 1.5f);
                PaintBlood(cp.point, width * 0.5f, Mathf.Min(0.8f, picked * 2f), 0);
            }

            // lay the drag mark along the actual path travelled once it is clearly a drag; thickness follows what
            // is being carried, the start fades in and the end feathers off as the part runs dry
            if (bloodiness > 0.04f && smearRun > 0.08f)
            {
                float fadeIn = Mathf.Clamp01((smearRun - 0.08f) / 0.15f);
                bs.SmearSegment(lastSmearPos, cp.point, cp.normal, width, bloodiness * fadeIn, smearSeed);
                bloodiness = Mathf.Max(0f, bloodiness * Mathf.Clamp01(1f - step * 1.4f) - step * 0.25f);
            }
            lastSmearPos = cp.point;
        }

        // ------------------------------------------------------------ open wounds

        public const int MaxHoles = 64;
        public readonly List<Vector4> holes = new List<Vector4>();
        /// <summary>Per hole: inward direction (xyz, local) and how deep the wound channel goes (w, metres).</summary>
        public readonly List<Vector4> holeAxes = new List<Vector4>();
        /// <summary>Per hole: stretch direction (xyz, length = how elongated, 1 = round) and a random seed (w).</summary>
        public readonly List<Vector4> holeShapes = new List<Vector4>();
        /// <summary>Per hole: when it was last torn open (damage landing together merges into one hole).</summary>
        public readonly List<float> holeTimes = new List<float>();
        const float SameEventWindow = 0.02f;   // one shotgun shell or one blast; not consecutive rifle rounds
        WoundChannels channels;
        float MaxHoleRadius => Mathf.Max(0.02f, uvRadius * 0.9f);
        float MaxHoleDepth => Mathf.Max(0.04f, uvRadius * 1.3f);
        public Material interiorMaterial;
        /// <summary>Real organs/bones shown through wounds (built by HumanBody), sharing this part's hole list.</summary>
        public Transform revealRoot;
        public Material revealMaterial;
        /// <summary>Layers of muscle under the skin, carved by wounds (built by HumanBody).</summary>
        public Transform fleshRoot;
        public readonly List<Material> fleshMaterials = new List<Material>();
        public Transform interiorRoot;
        // the hole list goes to the shader as a small per-part texture (row 0 centre+radius, row 1 axis+depth):
        // textures are reliably bound per material, per-material arrays were getting mixed up between parts
        Texture2D holeTex;
        static readonly Color[] holePixels = new Color[MaxHoles * 3];
        static readonly int HoleTexId = Shader.PropertyToID("_HoleTex"), HoleCountId = Shader.PropertyToID("_HoleCount");

        /// <summary>
        /// Cuts an open wound (radius in metres) at a local point on the skin. A 3D channel of torn tissue runs
        /// inward from it (depth in metres). stretch (local, along the skin) elongates it, e.g. a bullet arriving at
        /// an angle. A hit on the edge of an existing wound tears that side open deeper; the rest is left alone.
        /// </summary>
        public void AddHole(Vector3 local, float radius, float depth = -1f, bool merge = true, Vector3 stretch = default, Vector3 path = default)
        {
            if (radius <= 0.001f) return;
            // always on the visible skin (collider points sit centimetres off it)
            if (flesh != null) local = flesh.SnapToSkin(local, flesh.RestNormalAt(local));
            radius = Mathf.Min(radius, MaxHoleRadius);
            if (depth < 0f) depth = radius * 2.2f;
            Vector3 inward = flesh != null ? -flesh.RestNormalAt(local) : -local.normalized;
            // never let a channel point out of the limb: it must head towards the limb's centre line
            Vector3 radial = uvAlongZ ? new Vector3(local.x, local.y, 0f) : new Vector3(local.x, 0f, local.z);
            if (radial.sqrMagnitude > 1e-6f && Vector3.Dot(inward, radial) > -0.2f * radial.magnitude)
                inward = (inward - radial.normalized * (Vector3.Dot(inward, radial.normalized) + 0.5f)).normalized;
            // a bullet's channel follows the path it actually took through the body
            if (path.sqrMagnitude > 1e-6f)
            {
                // mostly straight in: a channel that follows a slanted path would come out through the skin
                Vector3 pd = path.normalized;
                if (Vector3.Dot(pd, inward) > 0f) inward = (inward * 1.6f + pd).normalized;
            }

            // damage landing at the same moment and overlapping (one blast, one shotgun shell at close range)
            // tears ONE big hole: the openings join and it goes deeper - insides become outsides
            if (merge)
            {
                int join = -1; float jd = float.MaxValue;
                for (int i = 0; i < holes.Count; i++)
                {
                    if (Time.time - holeTimes[i] > SameEventWindow) continue;
                    float d = Vector3.Distance(holes[i], local);
                    if (d < holes[i].w + radius * 1.5f + 0.01f && d < jd && Vector3.Dot((Vector3)holeAxes[i], inward) > 0f) { jd = d; join = i; }
                }
                if (join >= 0)
                {
                    Vector4 h = holes[join];
                    Vector3 c0 = h;
                    float r0 = h.w;
                    // the joined opening covers both, torn a little wider
                    float nr = Mathf.Min(MaxHoleRadius, Mathf.Max(r0, (jd + r0 + radius) * 0.5f, Mathf.Sqrt(r0 * r0 + radius * radius) * 1.12f));
                    Vector3 nc = Vector3.Lerp(c0, local, Mathf.Clamp01((nr - r0) / (jd + 1e-4f)) * 0.5f);
                    if (flesh != null) nc = flesh.SnapToSkin(nc, (Vector3)holeAxes[join]);
                    holes[join] = new Vector4(nc.x, nc.y, nc.z, nr);
                    Vector4 ax = holeAxes[join];
                    ax.w = Mathf.Min(MaxHoleDepth, Mathf.Max(ax.w, depth) + 0.25f * Mathf.Min(ax.w, depth));
                    holeAxes[join] = ax;
                    // a big joined hole is roughly round
                    Vector4 sh = holeShapes[join];
                    Vector3 sv = sh; if (sv.magnitude > 1.3f) sv = sv.normalized * 1.3f;
                    holeShapes[join] = new Vector4(sv.x, sv.y, sv.z, sh.w);
                    holeTimes[join] = Time.time;
                    if (flesh != null) flesh.Dent(nc, (Vector3)ax, nr * 1.6f, nr * 0.3f, 0.3f);
                    { using (Prof.Holes.Auto()) PushHoles(); }
                    return;
                }
            }

            // overlapping an older wound: that spot is already open, so this hit digs further in right here
            if (merge)
            {
                float extra = 0f;
                for (int i = 0; i < holes.Count; i++)
                {
                    float d = Vector3.Distance(holes[i], local), r = holes[i].w;
                    if (d < r + radius && Vector3.Dot((Vector3)holeAxes[i], inward) > 0.2f)
                        extra = Mathf.Max(extra, holeAxes[i].w * 0.7f * (1f - d / (r + radius)));
                }
                if (extra > 0f)
                {
                    depth += extra;
                    if (flesh != null) flesh.Dent(local, inward, radius * 2.2f, radius * 0.12f, 0.25f);
                }
            }

            if (holes.Count >= MaxHoles)
            {
                // out of slots: fold the new damage into the nearest wound instead
                int n = 0; float bd = float.MaxValue;
                for (int i = 0; i < holes.Count; i++) { float d = Vector3.Distance(holes[i], local); if (d < bd) { bd = d; n = i; } }
                Vector4 h = holes[n];
                holes[n] = new Vector4(h.x, h.y, h.z, Mathf.Min(MaxHoleRadius, Mathf.Max(h.w, radius + bd * 0.5f)));
                Vector4 ax = holeAxes[n]; ax.w = Mathf.Min(MaxHoleDepth, Mathf.Max(ax.w, depth));
                holeAxes[n] = ax;
                holeTimes[n] = Time.time;
                { using (Prof.Holes.Auto()) PushHoles(); }
                return;
            }

            // every wound is torn a little differently
            Vector3 sdir = Vector3.ProjectOnPlane(stretch.sqrMagnitude > 1e-6f ? stretch : Random.onUnitSphere, inward);
            if (sdir.sqrMagnitude < 1e-6f) sdir = Vector3.Cross(inward, Vector3.up);
            float aspect = Mathf.Max(1f, stretch.magnitude) * Random.Range(1f, 1.35f);
            sdir = sdir.normalized * aspect;
            holes.Add(new Vector4(local.x, local.y, local.z, radius * Random.Range(0.85f, 1.15f)));
            holeAxes.Add(new Vector4(inward.x, inward.y, inward.z, Mathf.Min(MaxHoleDepth, depth)));
            holeShapes.Add(new Vector4(sdir.x, sdir.y, sdir.z, Random.Range(0f, 97f)));
            holeTimes.Add(Time.time);
            body.EnsureInterior(this);
            { using (Prof.Holes.Auto()) PushHoles(); }
        }

        /// <summary>
        /// A knife cut along an existing slit: that slit is cut deeper and wider instead of adding another one.
        /// </summary>
        public bool DeepenSlit(Vector3 local, Vector3 dir, float width, float depth)
        {
            for (int i = 0; i < holes.Count; i++)
            {
                Vector3 sv = holeShapes[i];
                if (sv.magnitude < 2.5f) continue;                                    // not a slit
                if (Mathf.Abs(Vector3.Dot(sv.normalized, dir.normalized)) < 0.8f) continue;
                Vector3 off = local - (Vector3)holes[i];
                float across = (off - sv.normalized * Vector3.Dot(off, sv.normalized)).magnitude;
                if (across > holes[i].w * 2.5f + 0.004f || Mathf.Abs(Vector3.Dot(off, sv.normalized)) > holes[i].w * sv.magnitude) continue;
                float len = holes[i].w * sv.magnitude;                                  // keep its length
                float nw = Mathf.Max(holes[i].w, width);
                holes[i] = new Vector4(holes[i].x, holes[i].y, holes[i].z, nw);
                holeShapes[i] = new Vector4(sv.normalized.x, sv.normalized.y, sv.normalized.z, 0f) * (len / nw) + new Vector4(0, 0, 0, holeShapes[i].w);
                Vector4 ax = holeAxes[i]; ax.w = Mathf.Min(MaxHoleDepth, Mathf.Max(ax.w, depth)); holeAxes[i] = ax;
                holeTimes[i] = Time.time;
                { using (Prof.Holes.Auto()) PushHoles(); }
                return true;
            }
            return false;
        }

        // ---- wounds of the neighbouring parts (they overlap this part's skin at the joints)
        const int MaxNbr = 24;
        Texture2D nbrTex;
        static readonly Color[] nbrPixels = new Color[MaxNbr * 7];
        static readonly int NbrTexId = Shader.PropertyToID("_NbrTex"), NbrCountId = Shader.PropertyToID("_NbrCount");
        int nbrCount;

        /// <summary>Skin of this part inside a neighbour's wound is drawn as meat. Holes given in world space.</summary>
        public void SetNeighbourHoles(List<BodyPart> nbrs)
        {
            int n = 0;
            if (nbrs != null)
                foreach (var nb in nbrs)
                {
                    if (nb == null || nb.severed != severed) continue;      // a severed limb no longer touches this skin
                    // the wounded part's body as an oriented box (world space): only skin inside it is cut away
                    var bb = nb.skin.GetComponent<MeshFilter>().sharedMesh.bounds;
                    Vector3 bc = nb.transform.TransformPoint(bb.center);
                    Vector3 ex = nb.transform.TransformVector(new Vector3(bb.extents.x * 0.93f, 0, 0));
                    Vector3 ey = nb.transform.TransformVector(new Vector3(0, bb.extents.y * 0.93f, 0));
                    Vector3 ez = nb.transform.TransformVector(new Vector3(0, 0, bb.extents.z * 0.93f));
                    for (int i = 0; i < nb.holes.Count && n < MaxNbr; i++)
                    {
                        Vector3 c = nb.transform.TransformPoint(nb.holes[i]);
                        Vector3 a = nb.transform.TransformDirection((Vector3)nb.holeAxes[i]).normalized;
                        Vector3 sv = nb.transform.TransformDirection((Vector3)nb.holeShapes[i]);
                        nbrPixels[n] = new Color(c.x, c.y, c.z, nb.holes[i].w);
                        nbrPixels[MaxNbr + n] = new Color(a.x, a.y, a.z, nb.holeAxes[i].w);
                        nbrPixels[MaxNbr * 2 + n] = new Color(sv.x, sv.y, sv.z, 0f);
                        nbrPixels[MaxNbr * 3 + n] = new Color(bc.x, bc.y, bc.z, 0f);
                        nbrPixels[MaxNbr * 4 + n] = new Color(ex.x, ex.y, ex.z, 0f);
                        nbrPixels[MaxNbr * 5 + n] = new Color(ey.x, ey.y, ey.z, 0f);
                        nbrPixels[MaxNbr * 6 + n] = new Color(ez.x, ez.y, ez.z, 0f);
                        n++;
                    }
                }
            if (n == 0 && nbrCount == 0) return;
            if (nbrTex == null) nbrTex = new Texture2D(MaxNbr, 7, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (n > 0) { nbrTex.SetPixels(nbrPixels); nbrTex.Apply(false); }
            nbrCount = n;
            if (skinMaterial) { skinMaterial.SetTexture(NbrTexId, nbrTex); skinMaterial.SetFloat(NbrCountId, n); }
        }

        /// <summary>How deep the existing wounds already go at a point on the skin (0 = intact).</summary>
        public float CutDepthAt(Vector3 local)
        {
            float best = 0f;
            for (int i = 0; i < holes.Count; i++)
            {
                Vector3 sv = holeShapes[i];
                float a = Mathf.Max(1f, sv.magnitude);
                Vector3 off = local - (Vector3)holes[i];
                if (a > 1.001f) { Vector3 t = sv / a; off -= t * Vector3.Dot(off, t) * (1f - 1f / a); }
                if (off.magnitude < holes[i].w * 1.6f + 0.004f) best = Mathf.Max(best, holeAxes[i].w);
            }
            return best;
        }

        /// <summary>
        /// How much of the limb's width is torn open in its worst spot: neighbouring wounds add up
        /// (1 = as wide as the limb). Used for limb loss.
        /// </summary>
        public float WorstHoleFraction
        {
            get
            {
                float best = 0f, reach = Mathf.Max(0.02f, uvRadius) * 0.9f;
                for (int i = 0; i < holes.Count; i++)
                {
                    float a = 0f;
                    for (int j = 0; j < holes.Count; j++)
                        if (Vector3.Distance(holes[i], holes[j]) < reach) a += holes[j].w * holes[j].w;
                    best = Mathf.Max(best, Mathf.Sqrt(a));
                }
                return best / Mathf.Max(0.02f, uvRadius);
            }
        }

        void PushHoles()
        {
            if (holeTex == null)
                holeTex = new Texture2D(MaxHoles, 3, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name + "_Holes" };
            for (int i = 0; i < MaxHoles; i++)
            {
                Vector4 h = i < holes.Count ? holes[i] : Vector4.zero, a = i < holeAxes.Count ? holeAxes[i] : Vector4.zero;
                holePixels[i] = new Color(h.x, h.y, h.z, h.w);
                holePixels[MaxHoles + i] = new Color(a.x, a.y, a.z, a.w);
                Vector4 sh = i < holeShapes.Count ? holeShapes[i] : new Vector4(1f, 0f, 0f, i * 7.3f);
                holePixels[MaxHoles * 2 + i] = new Color(sh.x, sh.y, sh.z, sh.w);
            }
            holeTex.SetPixels(holePixels);
            holeTex.Apply(false);
            if (skinMaterial) { skinMaterial.SetTexture(HoleTexId, holeTex); skinMaterial.SetFloat(HoleCountId, holes.Count); }
            foreach (var fm in fleshMaterials) { fm.SetTexture(HoleTexId, holeTex); fm.SetFloat(HoleCountId, holes.Count); }
            if (fleshRoot) fleshRoot.gameObject.SetActive(holes.Count > 0 && !body.XrayVisible);
            // the organs inside are only ever seen through a hole: until there is one, they aren't drawn at all
            if (revealRoot) revealRoot.gameObject.SetActive((holes.Count > 0 || severed) && !body.XrayVisible);
            if (revealMaterial) { revealMaterial.SetTexture(HoleTexId, holeTex); revealMaterial.SetFloat(HoleCountId, holes.Count); revealMaterial.SetMatrix("_PartW2L", transform.worldToLocalMatrix); }
            if (channels == null && flesh != null) channels = WoundChannels.Create(this);
            if (channels != null) channels.MarkDirty();
        }

        /// <summary>Total open-wound area relative to the part's cross-section (used for dismemberment).</summary>
        public float OpenFraction
        {
            get
            {
                float a = 0f;
                foreach (var h in holes) a += h.w * h.w;
                float r = Mathf.Max(0.03f, uvRadius);
                return a / (r * r);
            }
        }

        public void AddBloodiness(Vector3 worldPoint, float amount)
        {
            PaintBlood(worldPoint, 0.03f, amount, 0);
        }

        // ------------------------------------------------------------ simulated blood on the skin

        // cylindrical UV mapping parameters (set by HumanBody when the mesh is built)
        [HideInInspector] public bool uvAlongZ;
        [HideInInspector] public float uvMin, uvMax, uvRadius = 0.05f;
        BloodCanvas blood;
        MaterialPropertyBlock mpb;

        public Vector2 LocalToUV(Vector3 p)
        {
            float a, b, t;
            if (uvAlongZ) { a = p.x; b = p.y; t = p.z; } else { a = p.x; b = p.z; t = p.y; }
            float u = Mathf.Atan2(b, a) / (Mathf.PI * 2f) + 0.5f;
            float v = Mathf.InverseLerp(uvMin, uvMax, t);
            return new Vector2(u, v);
        }

        Vector2 UVRadius(float worldRadius, Vector3 local)
        {
            // use the real girth at this point so stamps stay round (narrow ends, wide torso)
            float girth = uvAlongZ ? new Vector2(local.x, local.y).magnitude : new Vector2(local.x, local.z).magnitude;
            float circ = Mathf.PI * 2f * Mathf.Max(0.015f, girth);
            float h = Mathf.Max(0.05f, uvMax - uvMin);
            return new Vector2(worldRadius / circ, worldRadius / h);
        }

        /// <summary>kind: 0 splat, 1 smear, 2 ooze.</summary>
        public void PaintBlood(Vector3 worldPoint, float radius, float amount, int kind)
        {
            PaintBloodLocal(transform.InverseTransformPoint(worldPoint), radius, amount, kind);
        }

        public void PaintBloodLocal(Vector3 local, float radius, float amount, int kind)
        {
            if (amount <= 0f || BloodSystem.Instance == null || GameSettings.NoBlood) return;
            if (blood == null)
            {
                blood = new BloodCanvas(GameSettings.BodyBloodRes, true);
                mpb = new MaterialPropertyBlock();
                BloodSystem.Instance.RegisterBloody(this);
            }
            Vector3 down = transform.InverseTransformDirection(Vector3.down);
            Vector2 dir = uvAlongZ ? Vector2.right : new Vector2(0f, down.y < 0f ? -1f : 1f);
            blood.AddStamp(LocalToUV(local), UVRadius(radius, local), amount, dir, 1f, kind, kind == 0 ? 0.3f : 0f);
            // wound ooze trickles in slowly; splashes and pools soak the skin faster
            bloodiness = Mathf.Clamp(bloodiness + amount * radius * (kind == 2 ? 0.8f : 3f), 0f, 1.2f);
        }

        public void StepBlood(float dt)
        {
            if (blood == null) return;
            float circ = Mathf.PI * 2f * Mathf.Max(0.02f, uvRadius);
            float h = Mathf.Max(0.05f, uvMax - uvMin);
            Vector3 gl = uvAlongZ ? Vector3.zero : transform.InverseTransformDirection(Vector3.down);
            blood.Step(dt, new Vector4(1f / circ, 1f / h, 1f, 1f), gl, 0.08f, body: true);
            if (skinMaterial) skinMaterial.SetTexture("_BloodTex", blood.Texture);
        }

        void OnDestroy()
        {
            blood?.Release();
        }
    }
}
