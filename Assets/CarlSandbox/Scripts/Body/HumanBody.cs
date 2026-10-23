using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A procedurally built human: 17 rigid parts joined by ConfigurableJoints (driven as muscles by ActiveRagdoll),
    /// with full anatomy inside each part. This is the hub other systems talk to.
    /// </summary>
    public class HumanBody : MonoBehaviour
    {
        public static readonly List<HumanBody> All = new List<HumanBody>();
        static int counter;

        // Play mode may run without a domain reload, so statics must be reset by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            counter = 0;
        }

        public int number;
        public string DisplayName => this == PlayerAvatar.Body ? "YOU" : $"EARL #{number:00}";
        public readonly BodyPart[] parts = new BodyPart[17];
        public readonly List<BodyPart> partList = new List<BodyPart>();
        public readonly List<Organ> organs = new List<Organ>();
        public readonly Dictionary<PartId, List<BodyPart>> children = new Dictionary<PartId, List<BodyPart>>();
        public Physiology physio;
        public DamageModel damage;
        public ActiveRagdoll ragdoll;
        public HumanBrain brain;
        /// <summary>His gun (and the side he fights for), if he has one.</summary>
        [HideInInspector] public EarlGunner gunner;
        public float totalMass;
        public string lastHit = "";
        public float lastHitTime = -999f;
        public bool forceXray;
        public float spawnTime;

        Material skinMat;
        bool xrayShown;

        public BodyPart this[PartId id] => parts[(int)id];
        public BodyPart Pelvis => parts[(int)PartId.Pelvis];
        public BodyPart Chest => parts[(int)PartId.Chest];
        public BodyPart Head => parts[(int)PartId.Head];
        public bool Dead => physio != null && physio.dead;
        public float TimeSinceDeath => physio != null && physio.dead ? Time.time - physio.deathTime : 0f;
        public Vector3 Center => Chest ? Chest.transform.position : transform.position;

        struct Spec
        {
            public PartId id, parent;
            public Vector3 c;          // rest centre (feet at y = 0, facing +Z)
            public float rx, rz, half; // capsule along local Y (or Z for feet)
            public bool ellipsoid, alongZ, capsuleCollider;
            public Vector3 eRadii;
            public float mass;
            public Vector3 joint;      // joint position (rest, world)
            public Vector2 xr;         // child rotation range about local X (deg, right-hand rule)
            public float yl, zl;
            public float spring;
        }

        static readonly List<Spec> specs = BuildSpecs();

        static List<Spec> BuildSpecs()
        {
            var L = new List<Spec>();
            L.Add(new Spec { id = PartId.Pelvis, parent = PartId.Pelvis, c = new Vector3(0, 0.98f, 0), rx = 0.17f, rz = 0.115f, half = 0.11f, mass = 12f });
            L.Add(new Spec { id = PartId.Abdomen, parent = PartId.Pelvis, c = new Vector3(0, 1.14f, 0), rx = 0.155f, rz = 0.105f, half = 0.1f, mass = 9f, joint = new Vector3(0, 1.06f, 0), xr = new Vector2(-30, 45), yl = 30, zl = 30, spring = 2600 });
            L.Add(new Spec { id = PartId.Chest, parent = PartId.Abdomen, c = new Vector3(0, 1.35f, 0), rx = 0.19f, rz = 0.12f, half = 0.15f, mass = 15f, joint = new Vector3(0, 1.22f, 0), xr = new Vector2(-20, 35), yl = 30, zl = 25, spring = 2600 });
            L.Add(new Spec { id = PartId.Neck, parent = PartId.Chest, c = new Vector3(0, 1.545f, -0.01f), rx = 0.055f, rz = 0.055f, half = 0.075f, mass = 1.5f, capsuleCollider = true, joint = new Vector3(0, 1.49f, -0.01f), xr = new Vector2(-40, 50), yl = 60, zl = 35, spring = 500 });
            L.Add(new Spec { id = PartId.Head, parent = PartId.Neck, c = new Vector3(0, 1.70f, 0.01f), ellipsoid = true, eRadii = new Vector3(0.09f, 0.115f, 0.105f), mass = 5f, joint = new Vector3(0, 1.60f, -0.005f), xr = new Vector2(-35, 40), yl = 50, zl = 25, spring = 350 });
            for (int s = -1; s <= 1; s += 2)
            {
                bool left = s < 0;
                PartId ua = left ? PartId.UpperArmL : PartId.UpperArmR, fa = left ? PartId.ForearmL : PartId.ForearmR, ha = left ? PartId.HandL : PartId.HandR;
                PartId th = left ? PartId.ThighL : PartId.ThighR, sh = left ? PartId.ShinL : PartId.ShinR, ft = left ? PartId.FootL : PartId.FootR;
                L.Add(new Spec { id = ua, parent = PartId.Chest, c = new Vector3(0.225f * s, 1.295f, 0), rx = 0.05f, rz = 0.05f, half = 0.17f, mass = 2.2f, capsuleCollider = true, joint = new Vector3(0.205f * s, 1.44f, 0), xr = new Vector2(-175, 60), yl = 80, zl = 110, spring = 700 });
                L.Add(new Spec { id = fa, parent = ua, c = new Vector3(0.24f * s, 1.005f, 0), rx = 0.042f, rz = 0.04f, half = 0.155f, mass = 1.4f, capsuleCollider = true, joint = new Vector3(0.235f * s, 1.15f, 0), xr = new Vector2(-150, 0), yl = 10, zl = 5, spring = 350 });
                L.Add(new Spec { id = ha, parent = fa, c = new Vector3(0.245f * s, 0.79f, 0.01f), rx = 0.022f, rz = 0.045f, half = 0.09f, mass = 0.5f, joint = new Vector3(0.245f * s, 0.865f, 0), xr = new Vector2(-70, 70), yl = 10, zl = 30, spring = 60 });
                L.Add(new Spec { id = th, parent = PartId.Pelvis, c = new Vector3(0.10f * s, 0.71f, 0), rx = 0.075f, rz = 0.08f, half = 0.235f, mass = 9f, capsuleCollider = true, joint = new Vector3(0.10f * s, 0.925f, 0), xr = new Vector2(-120, 30), yl = 30, zl = 45, spring = 3200 });
                L.Add(new Spec { id = sh, parent = th, c = new Vector3(0.10f * s, 0.29f, -0.005f), rx = 0.055f, rz = 0.06f, half = 0.23f, mass = 4f, capsuleCollider = true, joint = new Vector3(0.10f * s, 0.505f, 0), xr = new Vector2(0, 145), yl = 5, zl = 3, spring = 2600 });
                L.Add(new Spec { id = ft, parent = sh, c = new Vector3(0.10f * s, 0.045f, 0.055f), rx = 0.045f, rz = 0.04f, half = 0.13f, alongZ = true, mass = 1.2f, joint = new Vector3(0.10f * s, 0.075f, 0), xr = new Vector2(-40, 40), yl = 10, zl = 20, spring = 700 });
            }
            return L;
        }

        public static float SpringFor(PartId id)
        {
            foreach (var s in specs) if (s.id == id) return s.spring;
            return 500f;
        }

        public static HumanBody Spawn(Vector3 feetPos, float yaw)
        {
            var go = new GameObject($"Earl_{++counter:00}");
            var h = go.AddComponent<HumanBody>();
            h.number = counter;
            h.Build(feetPos, Quaternion.Euler(0, yaw, 0));
            return h;
        }

        void Build(Vector3 feet, Quaternion rot)
        {
            spawnTime = Time.time;
            var rng = new System.Random(number * 7919);
            float tone = (float)rng.NextDouble();
            var baseCol = Color.Lerp(new Color(0.88f, 0.74f, 0.61f), new Color(0.76f, 0.6f, 0.47f), tone * 0.7f);
            skinMat = new Material(Mats.Find("Carl/Skin"));
            skinMat.SetColor("_BaseColor", baseCol);

            var physMat = new PhysicsMaterial("Flesh") { dynamicFriction = 0.65f, staticFriction = 0.8f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Average };

            foreach (var s in specs)
            {
                var pgo = new GameObject(Anatomy.PartName(s.id));
                pgo.transform.SetParent(transform, false);
                pgo.transform.SetPositionAndRotation(feet + rot * s.c, rot);

                Mesh visual, coll = null;
                if (s.ellipsoid)
                {
                    visual = ProcMesh.Ellipsoid(s.eRadii, 22, 16);
                    coll = ProcMesh.Ellipsoid(s.eRadii, 10, 8);
                }
                else
                {
                    visual = ProcMesh.Capsule(s.rx, s.rz, s.half, 20, 6, 8);
                    if (!s.capsuleCollider) coll = ProcMesh.Capsule(s.rx, s.rz, s.half, 10, 3, 2);
                    if (s.alongZ)
                    {
                        RotateMesh(visual, Quaternion.Euler(90, 0, 0));
                        RotateMesh(coll, Quaternion.Euler(90, 0, 0));
                    }
                }
                // sculpted mannequin part from Blender (left limbs are mirrored), falls back to the procedural shape
                var model = ModelLibrary.Get(ModelName(s.id), Anatomy.Side(s.id) < 0);
                if (model != null) visual = model.mesh;
                visual = PrepareSkinMesh(visual, s.alongZ, out int[] weld, out float uvMin, out float uvMax, out float uvRadius);

                var mf = pgo.AddComponent<MeshFilter>();
                mf.sharedMesh = visual;
                var mr = pgo.AddComponent<MeshRenderer>();
                // each part gets its own skin material so its blood canvas can be bound without property blocks
                var partMat = new Material(skinMat);
                mr.sharedMaterial = partMat;

                var bp = pgo.AddComponent<BodyPart>();
                bp.id = s.id;
                bp.body = this;
                bp.skin = mr;
                bp.skinMaterial = partMat;
                bp.halfLength = s.ellipsoid ? s.eRadii.y : s.half;
                bp.uvAlongZ = s.alongZ;
                bp.uvMin = uvMin; bp.uvMax = uvMax; bp.uvRadius = uvRadius;
                bp.flesh = pgo.AddComponent<FleshDeformer>();
                // flesh can cave in at most ~a third of the way: deeper and the skin folds inside-out and bones end up outside it
                bp.flesh.Init(visual, Mathf.Min(s.rx > 0 ? s.rx : s.eRadii.x, s.rz > 0 ? s.rz : s.eRadii.z) * 0.32f, weld);

                if (s.capsuleCollider)
                {
                    var cc = pgo.AddComponent<CapsuleCollider>();
                    cc.direction = 1;
                    cc.radius = (s.rx + s.rz) * 0.5f;
                    cc.height = s.half * 2f;
                    bp.col = cc;
                }
                else
                {
                    var mc = pgo.AddComponent<MeshCollider>();
                    mc.sharedMesh = coll;
                    mc.convex = true;
                    bp.col = mc;
                }
                bp.col.sharedMaterial = physMat;

                var rb = pgo.AddComponent<Rigidbody>();
                rb.mass = s.mass;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.9f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations = 14;
                rb.solverVelocityIterations = 6;
                rb.maxAngularVelocity = 25f;
                bp.rb = rb;
                totalMass += s.mass;

                parts[(int)s.id] = bp;
                partList.Add(bp);
            }

            // joints
            foreach (var s in specs)
            {
                if (s.id == PartId.Pelvis) continue;
                var bp = parts[(int)s.id];
                var par = parts[(int)s.parent];
                bp.parent = par;
                if (!children.TryGetValue(s.parent, out var list)) children[s.parent] = list = new List<BodyPart>();
                list.Add(bp);

                var j = bp.gameObject.AddComponent<ConfigurableJoint>();
                j.connectedBody = par.rb;
                j.anchor = bp.transform.InverseTransformPoint(feet + rot * s.joint);
                bp.jointLocal = j.anchor;
                j.autoConfigureConnectedAnchor = true;
                j.axis = Vector3.right;
                j.secondaryAxis = Vector3.up;
                j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
                j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
                // Joint X angle runs opposite to the child's right-hand rotation about X.
                j.lowAngularXLimit = new SoftJointLimit { limit = -s.xr.y };
                j.highAngularXLimit = new SoftJointLimit { limit = -s.xr.x };
                j.angularYLimit = new SoftJointLimit { limit = s.yl };
                j.angularZLimit = new SoftJointLimit { limit = s.zl };
                j.rotationDriveMode = RotationDriveMode.Slerp;
                j.projectionMode = JointProjectionMode.PositionAndRotation;
                j.projectionDistance = 0.03f;
                j.projectionAngle = 8f;
                j.enablePreprocessing = false;
                j.enableCollision = false;
                bp.joint = j;
            }

            // keep torso segments from fighting each other through overlaps
            IgnorePair(PartId.Pelvis, PartId.Chest);
            IgnorePair(PartId.Abdomen, PartId.UpperArmL); IgnorePair(PartId.Abdomen, PartId.UpperArmR);
            IgnorePair(PartId.Pelvis, PartId.ThighL); IgnorePair(PartId.Pelvis, PartId.ThighR);
            IgnorePair(PartId.Neck, PartId.UpperArmL); IgnorePair(PartId.Neck, PartId.UpperArmR);
            IgnorePair(PartId.Head, PartId.Chest);

            BuildAnatomy();
            AddFace();

            physio = gameObject.AddComponent<Physiology>();
            damage = gameObject.AddComponent<DamageModel>();
            ragdoll = gameObject.AddComponent<ActiveRagdoll>();
            brain = gameObject.AddComponent<HumanBrain>();
            physio.Init(this);
            damage.Init(this);
            ragdoll.Init(this);
            brain.Init(this, rng);

            All.Add(this);
        }

        void IgnorePair(PartId a, PartId b) => Physics.IgnoreCollision(parts[(int)a].col, parts[(int)b].col, true);

        static string ModelName(PartId id)
        {
            switch (id)
            {
                case PartId.UpperArmL: case PartId.UpperArmR: return "Body_UpperArm";
                case PartId.ForearmL: case PartId.ForearmR: return "Body_Forearm";
                case PartId.HandL: case PartId.HandR: return "Body_Hand";
                case PartId.ThighL: case PartId.ThighR: return "Body_Thigh";
                case PartId.ShinL: case PartId.ShinR: return "Body_Shin";
                case PartId.FootL: case PartId.FootR: return "Body_Foot";
                default: return "Body_" + id;
            }
        }

        /// <summary>
        /// Copies a mesh for per-instance deformation and gives it cylindrical UVs for the blood canvas.
        /// Triangles that straddle the UV wrap get duplicated vertices (u + 1); weld maps them back for smooth normals.
        /// </summary>
        static Mesh PrepareSkinMesh(Mesh src, bool alongZ, out int[] weld, out float min, out float max, out float radius)
        {
            var v = new List<Vector3>(src.vertices);
            var tris = src.triangles;
            var uv = new List<Vector2>(v.Count);
            var w = new List<int>(v.Count);
            min = float.MaxValue; max = float.MinValue; radius = 0f;
            foreach (var p in v)
            {
                float t = alongZ ? p.z : p.y;
                min = Mathf.Min(min, t); max = Mathf.Max(max, t);
                radius += alongZ ? new Vector2(p.x, p.y).magnitude : new Vector2(p.x, p.z).magnitude;
            }
            radius /= Mathf.Max(1, v.Count);
            for (int i = 0; i < v.Count; i++)
            {
                var p = v[i];
                float a = p.x, b = alongZ ? p.y : p.z, t = alongZ ? p.z : p.y;
                uv.Add(new Vector2(Mathf.Atan2(b, a) / (Mathf.PI * 2f) + 0.5f, Mathf.InverseLerp(min, max, t)));
                w.Add(i);
            }
            var dup = new Dictionary<int, int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                float u0 = uv[tris[i]].x, u1 = uv[tris[i + 1]].x, u2 = uv[tris[i + 2]].x;
                if (Mathf.Max(u0, Mathf.Max(u1, u2)) - Mathf.Min(u0, Mathf.Min(u1, u2)) < 0.5f) continue;
                for (int k = 0; k < 3; k++)
                {
                    int vi = tris[i + k];
                    if (uv[vi].x >= 0.5f) continue;
                    if (!dup.TryGetValue(vi, out int ni))
                    {
                        ni = v.Count;
                        v.Add(v[vi]);
                        uv.Add(new Vector2(uv[vi].x + 1f, uv[vi].y));
                        w.Add(vi);
                        dup[vi] = ni;
                    }
                    tris[i + k] = ni;
                }
            }
            var m = new Mesh { name = src.name + "_skin", indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            weld = w.ToArray();
            // welded normals
            var n = new Vector3[v.Count];
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 fn = Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                n[weld[tris[i]]] += fn; n[weld[tris[i + 1]]] += fn; n[weld[tris[i + 2]]] += fn;
            }
            for (int i = 0; i < n.Length; i++) n[i] = n[weld[i]].normalized;
            m.normals = n;
            return m;
        }

        static void RotateMesh(Mesh m, Quaternion q)
        {
            if (m == null) return;
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = q * v[i];
            m.vertices = v;
            m.RecalculateNormals();
            m.RecalculateBounds();
        }

        void AddFace()
        {
            // plain mannequin head: no visor band
            Head.xrayRoot.gameObject.SetActive(false);
        }

        MeshRenderer visor;

        static Material xrayOpaque, xrayClear, ghostMat;

        void BuildAnatomy()
        {
            if (xrayOpaque == null)
            {
                xrayOpaque = new Material(Mats.Find("Carl/XrayOrgan")) { renderQueue = 3005 };
                xrayClear = new Material(Mats.Find("Carl/XrayOrgan")) { renderQueue = 3012 };
                xrayClear.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                xrayClear.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                xrayClear.SetFloat("_ZWrite", 0f);
                ghostMat = new Material(Mats.Find("Carl/XrayGhost")) { renderQueue = 3020 };
            }

            foreach (var bp in partList)
            {
                var root = new GameObject("Xray").transform;
                root.SetParent(bp.transform, false);
                bp.xrayRoot = root;
                foreach (var def in Anatomy.Build(bp.id))
                {
                    var o = new Organ { def = def, part = bp, mpb = new MaterialPropertyBlock() };
                    bp.organs.Add(o);
                    organs.Add(o);
                    o.xray = MakeOrganVisual(def, root);
                }
                root.gameObject.SetActive(false);
                BuildRevealAnatomy(bp);
            }
        }

        /// <summary>
        /// The same organs and bones as the x-ray, as real tissue that shows only through open wounds
        /// (open the belly and the gut is there; strip an arm and the bone is there).
        /// </summary>
        void BuildRevealAnatomy(BodyPart bp)
        {
            var root = new GameObject("Anatomy").transform;
            root.SetParent(bp.transform, false);
            bp.revealRoot = root;
            BuildFleshLayers(bp);
            bp.revealMaterial = new Material(Mats.Find("Carl/RevealOrgan")) { renderQueue = 2010 };
            var skinMesh = bp.skin != null ? bp.skin.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (skinMesh != null)
            {
                var bb = skinMesh.bounds;
                bp.revealMaterial.SetVector("_BodyCenter", bb.center);
                bp.revealMaterial.SetVector("_BodyExtents", bb.extents * 0.97f);
            }
            foreach (var o in bp.organs)
            {
                var r = MakeOrganVisual(o.def, root);
                r.sharedMaterial = bp.revealMaterial;
                o.reveal = r;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", InteriorColor(o.def));
                mpb.SetFloat("_Punchable", Punchable(o.def) ? 1f : 0f);
                float pattern = 0f;
                if (o.def.organ == OrganId.SmallIntestine || o.def.organ == OrganId.Colon) pattern = 1f;
                else if (o.def.kind == TissueKind.Bone || o.def.kind == TissueKind.Spine) pattern = 2f;
                else if (o.def.kind == TissueKind.Brain) pattern = 3f;
                mpb.SetFloat("_Pattern", pattern);
                r.SetPropertyBlock(mpb);
            }
        }

        Renderer MakeOrganVisual(OrganDef def, Transform root)
        {
            var go = new GameObject(def.name);
            go.transform.SetParent(root, false);
            Mesh mesh;
            if (def.name == "RIBCAGE")
            {
                mesh = AnatomyMeshes.Ribcage();
            }
            else if (def.capsule)
            {
                float len = Vector3.Distance(def.a, def.b);
                float r = def.radii.x;
                var rot = Quaternion.FromToRotation(Vector3.up, def.b - def.a);
                int seed = def.name.Length * 7 + def.side * 3;
                switch (def.kind)
                {
                    case TissueKind.Bone: mesh = AnatomyMeshes.LongBone(len * 0.5f, r); break;
                    // the body's front, in the capsule's own space, so the vertebrae face the right way
                    case TissueKind.Spine: mesh = AnatomyMeshes.Spine(len * 0.5f, r, Quaternion.Inverse(rot) * Vector3.forward); break;
                    case TissueKind.Artery: case TissueKind.Vein: mesh = AnatomyMeshes.Vessel(len * 0.5f, r, seed); break;
                    case TissueKind.Airway: mesh = AnatomyMeshes.Trachea(len * 0.5f, r); break;
                    default: mesh = ProcMesh.Capsule(r, r, len * 0.5f + r, 8, 2, 2); break;
                }
                go.transform.localPosition = (def.a + def.b) * 0.5f;
                go.transform.localRotation = rot;
            }
            else
            {
                var radii = def.shell ? def.radii * 0.95f : def.radii;
                int seed = Mathf.RoundToInt(def.name.Length * 3.7f);
                switch (def.organ)
                {
                    case OrganId.SmallIntestine: mesh = AnatomyMeshes.Intestine(radii); break;
                    case OrganId.Colon: mesh = AnatomyMeshes.Colon(radii); break;
                    case OrganId.Stomach: mesh = AnatomyMeshes.Stomach(radii); break;
                    case OrganId.Heart: mesh = AnatomyMeshes.Heart(radii); break;
                    case OrganId.Lungs: mesh = AnatomyMeshes.Lung(radii, def.side, seed); break;
                    case OrganId.Liver: mesh = AnatomyMeshes.Liver(radii); break;
                    case OrganId.Kidney: mesh = AnatomyMeshes.Kidney(radii, def.side); break;
                    case OrganId.Pancreas: mesh = AnatomyMeshes.Pancreas(radii); break;
                    default:
                        if (def.name == "SKULL") mesh = AnatomyMeshes.Skull(radii);
                        else if (def.name == "PELVIC BONE") mesh = AnatomyMeshes.Pelvis(radii);
                        else if (def.name == "JAW") mesh = AnatomyMeshes.Jaw(radii);
                        else if (def.name.EndsWith("HAND BONES")) mesh = AnatomyMeshes.Hand(radii);
                        else if (def.name.EndsWith("FOOT BONES")) mesh = AnatomyMeshes.Foot(radii, def.side);
                        else if (def.kind == TissueKind.Brain) mesh = AnatomyMeshes.Brain(radii, def.name.Contains("LOBE") || def.name.Contains("CORTEX"));
                        else if (def.kind == TissueKind.Organ && !def.shell) mesh = AnatomyMeshes.Organ(radii, def.name.Length * 3.7f);
                        else mesh = ProcMesh.Ellipsoid(radii, def.shell ? 24 : 14, def.shell ? 18 : 10);
                        break;
                }
                go.transform.localPosition = def.a;
            }
            AnatomyMeshes.EnsureColors(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = def.alpha < 1f ? xrayClear : xrayOpaque;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var c = def.color; c.a = def.alpha;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", c);
            mr.SetPropertyBlock(mpb);
            return mr;
        }

        public bool XrayVisible => xrayShown;

        public void SetXray(bool on)
        {
            on |= forceXray;
            if (on == xrayShown) return;
            xrayShown = on;
            foreach (var bp in partList)
            {
                bp.skin.sharedMaterial = on ? ghostMat : bp.skinMaterial;
                bp.skin.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                bp.xrayRoot.gameObject.SetActive(on);
                if (bp.revealRoot) bp.revealRoot.gameObject.SetActive(!on);
                if (bp.fleshRoot) bp.fleshRoot.gameObject.SetActive(!on && bp.holes.Count > 0);
            }
            if (visor) visor.enabled = !on;
        }

        void LateUpdate()
        {
            // interior shaders cut holes in part space: keep their transforms current
            foreach (var bp in partList)
            {
                if (bp.interiorMaterial) bp.interiorMaterial.SetMatrix(PartWorldToLocal, bp.transform.worldToLocalMatrix);
                if (bp.revealMaterial && bp.holes.Count > 0) bp.revealMaterial.SetMatrix(PartW2L, bp.transform.worldToLocalMatrix);
                // the parts next to this one (they overlap at the joints) - their wounds cut into this skin too
                nbrScratch.Clear();
                if (bp.parent != null && bp.parent != bp && bp.parent.holes.Count > 0) nbrScratch.Add(bp.parent);
                if (children.TryGetValue(bp.id, out var kids))
                    foreach (var k in kids) if (k != bp && k.holes.Count > 0) nbrScratch.Add(k);
                bp.SetNeighbourHoles(nbrScratch);
            }

            if (!xrayShown) return;
            foreach (var o in organs)
            {
                if (o.xray == null) continue;
                float dmg = o.Damage;
                bool bleeding = o.severed && (o.def.kind == TissueKind.Artery || o.def.kind == TissueKind.Vein) && !Dead;
                float flash = bleeding ? 1f : (o.Destroyed && !Dead ? 0.5f : 0f);
                if (o.def.kind == TissueKind.Bone && o.Fractured) { dmg = Mathf.Max(dmg, 0.7f); }
                o.mpb.SetColor("_Color", new Color(o.def.color.r, o.def.color.g, o.def.color.b, o.def.alpha));
                o.mpb.SetFloat("_Damage", Mathf.Clamp01(dmg));
                o.mpb.SetFloat("_Flash", flash);
                o.xray.SetPropertyBlock(o.mpb);
                foreach (var x in o.xrayExtra) if (x) x.SetPropertyBlock(o.mpb);
            }
        }

        readonly List<BodyPart> nbrScratch = new List<BodyPart>();
        static readonly int PartWorldToLocal = Shader.PropertyToID("_PartWorldToLocal"), PartW2L = Shader.PropertyToID("_PartW2L");
        static Shader interiorShader;
        public static bool ShowInterior = false;

        /// <summary>
        /// Solid flesh: copies of the skin pushed inward in a few layers (just the scalp on the head), slightly
        /// lumpy, that wounds carve craters through. Without them the body is a hollow bubble behind every hole.
        /// </summary>
        void BuildFleshLayers(BodyPart bp)
        {
            var skinMesh = bp.skin.GetComponent<MeshFilter>().sharedMesh;
            var sv = skinMesh.vertices; var sn = skinMesh.normals; var st = skinMesh.triangles;
            float R = Mathf.Max(0.02f, bp.uvRadius);
            // real depths under the skin (m): fat, then muscle in layers, as deep as the part allows;
            // the head is only scalp over the skull
            float[] all = bp.id == PartId.Head ? new[] { 0.004f } : new[] { 0.0035f, 0.009f, 0.018f, 0.03f, 0.045f, 0.065f };
            var list = new List<float>();
            foreach (float d in all) if (d < R * 0.62f) list.Add(d);
            float[] fr = list.ToArray();
            var root = new GameObject("Flesh").transform;
            root.SetParent(bp.transform, false);
            bp.fleshRoot = root;
            var shader = Mats.Find("Carl/FleshShell");
            for (int l = 0; l < fr.Length; l++)
            {
                float inset = fr[l];
                var v = new Vector3[sv.Length];
                for (int i = 0; i < sv.Length; i++)
                {
                    Vector3 p = sv[i];
                    // gently uneven (low frequency only: per-vertex lumps made the layers look crumpled and faceted)
                    float lump = 0.9f + 0.2f * Mathf.PerlinNoise(p.x * 9f + l * 3.1f + p.z * 5f, p.y * 9f + l * 1.7f);
                    v[i] = p - sn[i].normalized * inset * lump;
                }
                var m = new Mesh { name = bp.name + "_Flesh" + l };
                if (v.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.vertices = v; m.normals = sn; m.triangles = st;
                m.RecalculateBounds();
                var go = new GameObject("Layer" + l);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(shader) { renderQueue = 2005 };
                mat.SetFloat("_Inset", inset);
                mat.SetFloat("_Depth01", Mathf.Clamp01(inset / Mathf.Min(0.06f, R * 0.6f)));
                mat.SetFloat("_Tissue", bp.id != PartId.Head && l == 0 ? 0f : 1f);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                bp.fleshMaterials.Add(mat);
            }
            root.gameObject.SetActive(false);
        }

        /// <summary>Thin layers a wound goes straight through, showing what is under them (skull, ribs).</summary>
        public static bool Punchable(OrganDef d) => d.shell || d.name == "RIBCAGE";

        /// <summary>Realistic colour of a structure when seen through an open wound.</summary>
        public static Color InteriorColor(OrganDef d)
        {
            switch (d.kind)
            {
                case TissueKind.Bone: return new Color(0.88f, 0.84f, 0.74f);
                case TissueKind.Spine: return new Color(0.86f, 0.82f, 0.72f);
                case TissueKind.Artery: return new Color(0.45f, 0.03f, 0.05f);
                case TissueKind.Vein: return new Color(0.3f, 0.05f, 0.12f);
                case TissueKind.Brain: return new Color(0.8f, 0.56f, 0.55f);
                case TissueKind.Eye: return new Color(0.92f, 0.9f, 0.88f);
                case TissueKind.Airway: return new Color(0.82f, 0.7f, 0.68f);
            }
            switch (d.organ)
            {
                case OrganId.Heart: return new Color(0.48f, 0.06f, 0.08f);
                case OrganId.Lungs: return new Color(0.78f, 0.48f, 0.5f);
                case OrganId.Liver: return new Color(0.5f, 0.13f, 0.1f);
                case OrganId.Spleen: return new Color(0.34f, 0.07f, 0.14f);
                case OrganId.Kidney: return new Color(0.45f, 0.13f, 0.12f);
                case OrganId.Stomach: return new Color(0.72f, 0.38f, 0.36f);
                case OrganId.SmallIntestine: return new Color(0.8f, 0.42f, 0.4f);
                case OrganId.Colon: return new Color(0.66f, 0.38f, 0.32f);
                case OrganId.Pancreas: return new Color(0.85f, 0.7f, 0.55f);
                case OrganId.Bladder: return new Color(0.8f, 0.65f, 0.55f);
            }
            return new Color(0.55f, 0.15f, 0.15f);
        }

        /// <summary>Builds the lit organs/bones inside a part the first time it is opened by a wound.</summary>
        public void EnsureInterior(BodyPart bp)
        {
            // Wounds are drawn on the skin; interior meshes poked through the sculpted body, so they stay off.
            if (!ShowInterior || bp.interiorRoot != null) return;
            interiorShader ??= Mats.Find("Carl/Interior");
            bp.interiorMaterial = new Material(interiorShader);
            bp.interiorRoot = new GameObject("Interior").transform;
            bp.interiorRoot.SetParent(bp.transform, false);
            foreach (var o in bp.organs)
            {
                if (!o.split) { o.interior = MakeInteriorVisual(o, bp, null, Vector3.zero, Vector3.zero); continue; }
                // already-broken bone: show its two pieces
                foreach (var piece in o.xrayExtra)
                {
                    var mf = piece.GetComponent<MeshFilter>();
                    MakeInteriorVisual(o, bp, Instantiate(mf.sharedMesh), piece.transform.localPosition, piece.transform.localRotation.eulerAngles);
                }
            }
        }

        Renderer MakeInteriorVisual(Organ o, BodyPart bp, Mesh meshOverride, Vector3 pos, Vector3 euler)
        {
            if (o.xray == null || bp.interiorRoot == null) return null;
            var src = o.xray.GetComponent<MeshFilter>().sharedMesh;
            var mesh = meshOverride != null ? meshOverride : Instantiate(src);
            var col = InteriorColor(o.def);
            var cols = new Color[mesh.vertexCount];
            for (int i = 0; i < cols.Length; i++) cols[i] = col;
            mesh.colors = cols;
            var go = new GameObject(o.def.name);
            go.transform.SetParent(bp.interiorRoot, false);
            if (meshOverride != null) { go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(euler); }
            else { go.transform.localPosition = o.xray.transform.localPosition; go.transform.localRotation = o.xray.transform.localRotation; }
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = bp.interiorMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        /// <summary>Snaps a long bone in two at the impact point (both in x-ray and through open wounds).</summary>
        public void SplitBone(Organ o, Vector3 localPoint)
        {
            if (o.split || !o.def.capsule || o.xray == null) return;
            if (o.def.kind != TissueKind.Bone) return;
            o.split = true;
            Vector3 a = o.def.a, b = o.def.b;
            Vector3 ab = b - a;
            float s = Mathf.Clamp(Vector3.Dot(localPoint - a, ab) / ab.sqrMagnitude, 0.2f, 0.8f);
            Vector3 mid = a + ab * s;
            Vector3 axis = ab.normalized;
            float r = o.def.radii.x;
            float gap = r * 0.6f;
            // the far piece is knocked off-line at the break
            Vector3 bend = Vector3.Cross(axis, Random.onUnitSphere).normalized;
            Quaternion kink = Quaternion.AngleAxis(Random.Range(8f, 22f), bend);
            Vector3 aEnd = mid - axis * gap, bStart = mid + axis * gap;
            Vector3 bDir = kink * axis;
            Vector3 bEnd = bStart + bDir * Vector3.Distance(bStart, b) + Vector3.Cross(axis, bend) * r * 0.8f;

            var bp = o.part;
            var parent = o.xray.transform.parent;
            var mat = o.xray.sharedMaterial;
            o.xray.gameObject.SetActive(false);
            if (o.interior) o.interior.gameObject.SetActive(false);

            foreach (var (p0, p1) in new[] { (a, aEnd), (bStart, bEnd) })
            {
                float len = Vector3.Distance(p0, p1);
                var mesh = ProcMesh.Capsule(r, r, len * 0.5f + r, 8, 2, 2);
                AnatomyMeshes.EnsureColors(mesh);
                var go = new GameObject(o.def.name + " (broken)");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = (p0 + p1) * 0.5f;
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, p1 - p0);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                o.xrayExtra.Add(mr);
                if (bp.interiorRoot != null)
                    MakeInteriorVisual(o, bp, Instantiate(mesh), go.transform.localPosition, go.transform.localRotation.eulerAngles);
            }
        }

        public List<BodyPart> Descendants(BodyPart p)
        {
            var result = new List<BodyPart>();
            var stack = new Stack<BodyPart>();
            stack.Push(p);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                result.Add(c);
                if (children.TryGetValue(c.id, out var kids)) foreach (var k in kids) stack.Push(k);
            }
            return result;
        }

        public bool IsMine(Collider c)
        {
            var bp = c.GetComponent<BodyPart>();
            return bp != null && bp.body == this;
        }

        public void Despawn()
        {
            All.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (skinMat) Destroy(skinMat);
            foreach (var bp in partList) if (bp && bp.skinMaterial) Destroy(bp.skinMaterial);
        }
    }
}
