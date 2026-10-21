using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// What is still holding a limb together, slice by slice along its length: bone, muscle and skin. A limb only
    /// comes apart where all of that has been shot, blown or cut away, and it comes apart right there (mid-thigh,
    /// mid-forearm), not at the nearest joint. With the bone gone and most of the meat gone it hangs on by what is
    /// left (strands of skin and muscle) and swings, until that is shot through or torn too.
    /// </summary>
    public class LimbIntegrity : MonoBehaviour
    {
        public BodyPart part;
        int n;
        float[] tissue, skin, bone;          // 1 = intact, 0 = gone
        Vector3 axisL;                       // part space, pointing away from the joint (distal)
        Vector3 axisOrigin;                  // a point on the limb's centre line
        float half, centerS;
        public bool cut;                     // in two pieces (maybe still hanging together)
        public int cutSlice = -1;
        public GameObject lower;
        ConfigurableJoint thread;
        SkinThread threadFx;

        public static bool Applies(PartId id) => Anatomy.IsArm(id) || Anatomy.IsLeg(id) || id == PartId.Neck;

        public static LimbIntegrity Of(BodyPart p)
        {
            if (p == null || !Applies(p.id) || p.skin == null) return null;
            var li = p.GetComponent<LimbIntegrity>();
            if (li == null) { li = p.gameObject.AddComponent<LimbIntegrity>(); li.Init(p); }
            return li;
        }

        void Init(BodyPart p)
        {
            part = p;
            Vector3 ax = p.uvAlongZ ? Vector3.forward : Vector3.up;
            var b = p.skin.GetComponent<MeshFilter>().sharedMesh.bounds;
            if (Vector3.Dot(p.jointLocal - b.center, ax) > 0f) ax = -ax;
            axisL = ax;
            Vector3 aa = new Vector3(Mathf.Abs(ax.x), Mathf.Abs(ax.y), Mathf.Abs(ax.z));
            half = Vector3.Dot(b.extents, aa);
            centerS = Vector3.Dot(b.center, axisL);
            axisOrigin = b.center - axisL * centerS;
            bool small = p.id == PartId.HandL || p.id == PartId.HandR || p.id == PartId.FootL || p.id == PartId.FootR || p.id == PartId.Neck;
            n = small ? 2 : 6;
            tissue = new float[n]; skin = new float[n]; bone = new float[n];
            for (int i = 0; i < n; i++) { tissue[i] = 1f; skin[i] = 1f; bone[i] = 1f; }
        }

        float S(Vector3 local) => Vector3.Dot(local, axisL);
        int Slice(Vector3 local) => Mathf.Clamp(Mathf.FloorToInt((S(local) - (centerS - half)) / (2f * half) * n), 0, n - 1);
        float SliceCenter(int i) => centerS - half + (i + 0.5f) * 2f * half / n;
        bool BoneGone(int k) => bone[k] <= 0.35f;

        /// <summary>
        /// Damage through a cross-section at a point (part space). tissueFrac / skinFrac: fraction of the limb's
        /// cross-section of meat / circumference of skin destroyed; boneDmg: how badly the bone there was hit.
        /// </summary>
        public void Damage(Vector3 local, float tissueFrac, float skinFrac, float boneDmg, Vector3 dirWorld)
        {
            if (tissue == null) return;
            int i = Slice(local);
            void Apply(int k, float w)
            {
                tissue[k] = Mathf.Max(0f, tissue[k] - tissueFrac * w);
                skin[k] = Mathf.Max(0f, skin[k] - skinFrac * w);
                bone[k] = Mathf.Max(0f, bone[k] - boneDmg * w);
            }
            Apply(i, 1f);
            if (i > 0) Apply(i - 1, 0.3f);
            if (i < n - 1) Apply(i + 1, 0.3f);
            Evaluate(dirWorld);
        }

        /// <summary>A hit on the hanging piece close to the cut tears at what is holding it.</summary>
        /// <summary>Is this world point at the tear (where the strands holding the hanging piece are)?</summary>
        public bool AtTear(Transform piece, Vector3 worldPoint)
        {
            if (!cut || cutSlice < 0 || piece == null) return false;
            Vector3 local = piece.InverseTransformPoint(worldPoint);
            Vector3 cutP = axisOrigin + axisL * SliceCenter(cutSlice);
            return Mathf.Abs(Vector3.Dot(local - cutP, axisL)) < 0.055f;
        }

        public void DamageThread(float tissueFrac, float skinFrac)
        {
            if (!cut || cutSlice < 0) return;
            tissue[cutSlice] = Mathf.Max(0f, tissue[cutSlice] - tissueFrac);
            skin[cutSlice] = Mathf.Max(0f, skin[cutSlice] - skinFrac);
            Evaluate(Vector3.down);
        }

        public string Status()
        {
            if (cut) return thread != null ? "HANGING BY SKIN" : "SEVERED";
            float worst = 1f;
            for (int k = 0; k < n; k++) worst = Mathf.Min(worst, (tissue[k] + skin[k] + bone[k]) / 3f);
            return worst > 0.9f ? "" : $"{Mathf.RoundToInt((1f - worst) * 100f)}% DESTROYED";
        }

        void Evaluate(Vector3 dir)
        {
            if (cut)
            {
                if (thread != null && tissue[cutSlice] <= 0.02f && skin[cutSlice] <= 0.06f) ThreadSnapped();   // nothing left holding it
                return;
            }
            // nothing left at all: it comes off right there
            for (int k = 0; k < n; k++) if (BoneGone(k) && tissue[k] <= 0.02f && skin[k] <= 0.06f) { Split(k, false, dir); return; }
            // bone gone and most of the meat: it hangs on by what is left
            for (int k = 0; k < n; k++) if (BoneGone(k) && tissue[k] <= 0.3f) { Split(k, true, dir); return; }
        }

        /// <summary>The last strands gave way (shot through or torn by the weight swinging on them).</summary>
        public void ThreadSnapped()
        {
            if (thread != null) Destroy(thread);
            thread = null;
            if (threadFx != null) Destroy(threadFx.gameObject);
            threadFx = null;
            if (part != null && part.body != null)
            {
                part.body.physio.AddPain(0.8f, part.id);
                var w = part.body.damage.AddWound(part, part.transform.TransformPoint(axisOrigin + axisL * SliceCenter(cutSlice)), part.transform.TransformDirection(axisL), WoundType.Amputation, 1f, 0f, false);
                w.desc = "TORN OFF";
            }
            BloodSystem.Instance?.Spray(part.transform.TransformPoint(axisOrigin + axisL * SliceCenter(cutSlice)), part.transform.TransformDirection(axisL), 12, 1f, 3f, 0.6f, 0.007f, part);
        }

        static float StumpBleed(PartId id)
        {
            switch (id)
            {
                case PartId.Neck: return 35f;
                case PartId.ThighL: case PartId.ThighR: return 18f;
                case PartId.UpperArmL: case PartId.UpperArmR: return 5f;
                case PartId.ShinL: case PartId.ShinR: return 5f;
                case PartId.ForearmL: case PartId.ForearmR: return 3f;
                default: return 1.2f;
            }
        }

        void Split(int k, bool hanging, Vector3 dirWorld)
        {
            cut = true; cutSlice = k;
            var body = part.body;
            float cs = SliceCenter(k);
            Vector3 cutP = axisOrigin + axisL * cs;                    // part space
            float frac = Mathf.InverseLerp(centerS - half, centerS + half, cs);

            lower = new GameObject(part.name + " (torn)");
            lower.layer = part.gameObject.layer;
            lower.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
            lower.transform.localScale = part.transform.lossyScale;
            var chunk = lower.AddComponent<LimbChunk>();
            chunk.from = part; chunk.li = this; chunk.bleed = hanging ? 6f : 10f;

            // the same visuals (skin, flesh layers, organs, wound tunnels), each half drawn only on its own side of the cut
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponentInParent<ArmourPiece>() != null) continue;
                if (InXray(r.transform)) continue;
                var mfSrc = r.GetComponent<MeshFilter>();
                if (mfSrc == null || mfSrc.sharedMesh == null) continue;
                Matrix4x4 M = part.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var go = new GameObject(r.name);
                go.layer = r.gameObject.layer;
                go.transform.SetParent(lower.transform, false);
                go.transform.localPosition = M.GetColumn(3);
                go.transform.localRotation = M.rotation;
                go.transform.localScale = M.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mfSrc.sharedMesh;
                var nr = go.AddComponent<MeshRenderer>();
                nr.sharedMaterials = r.sharedMaterials;
                nr.shadowCastingMode = r.shadowCastingMode;
                nr.receiveShadows = r.receiveShadows;
                nr.enabled = r.enabled;
                go.SetActive(r.gameObject.activeInHierarchy);
                bool organ = r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name == "Carl/RevealOrgan";
                SetPlane(r, M, cutP, axisL, organ);
                SetPlane(nr, M, cutP, -axisL, organ);
                if (organ) chunk.organs.Add(nr);
            }

            // physics: the part keeps its near end, the far end is its own body now
            var lrb = lower.AddComponent<Rigidbody>();
            lrb.mass = Mathf.Max(0.2f, part.rb.mass * (1f - frac));
            part.rb.mass = Mathf.Max(0.3f, part.rb.mass * Mathf.Max(0.2f, frac));
            lrb.linearDamping = 0.05f; lrb.angularDamping = 0.6f;
            lrb.interpolation = RigidbodyInterpolation.Interpolate;
            lrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            lrb.linearVelocity = part.rb.linearVelocity; lrb.angularVelocity = part.rb.angularVelocity;
            int dirIdx = Mathf.Abs(axisL.z) > 0.5f ? 2 : 1;
            float rad = Mathf.Max(0.015f, part.uvRadius * 0.85f);
            float distalEnd = centerS + half, proxEnd = centerS - half;
            var lc = lower.AddComponent<CapsuleCollider>();
            lc.direction = dirIdx; lc.radius = rad;
            lc.height = Mathf.Max(distalEnd - cs, rad * 2f + 0.01f);
            lc.center = axisOrigin + axisL * ((cs + distalEnd) * 0.5f);
            lc.sharedMaterial = part.col != null ? part.col.sharedMaterial : null;
            var uc = part.col as CapsuleCollider;
            if (uc == null)
            {
                if (part.col != null) part.col.enabled = false;
                uc = part.gameObject.AddComponent<CapsuleCollider>();
                uc.sharedMaterial = lc.sharedMaterial;
                part.col = uc;
            }
            uc.direction = dirIdx; uc.radius = rad;
            uc.height = Mathf.Max(cs - proxEnd, rad * 2f + 0.01f);
            uc.center = axisOrigin + axisL * ((cs + proxEnd) * 0.5f);
            Physics.IgnoreCollision(lc, uc, true);

            // whatever hangs further down now hangs off the torn piece, limp
            foreach (var c in body.partList)
                if (c.parent == part && c.joint != null)
                {
                    Vector3 wa = c.transform.TransformPoint(c.joint.anchor);
                    c.joint.autoConfigureConnectedAnchor = false;
                    c.joint.connectedBody = lrb;
                    c.joint.connectedAnchor = lower.transform.InverseTransformPoint(wa);
                    foreach (var col in c.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(lc, col, true);
                }
            foreach (var d in body.Descendants(part)) if (d != part) d.severed = true;

            // torn ends: meat, splintered bone, ragged skin - on both pieces
            var boneO = DamageModel.NearestBone(part, cutP);
            StumpCap.Build(part, cutP, axisL, part.uvRadius, boneO);
            var lowCap = StumpCap.Build(part, cutP, -axisL, part.uvRadius, boneO);
            if (lowCap != null) lowCap.transform.SetParent(lower.transform, false);

            Vector3 cutW = part.transform.TransformPoint(cutP);
            Vector3 axW = part.transform.TransformDirection(axisL);
            var w = body.damage.AddWound(part, cutW, axW, WoundType.Amputation, StumpBleed(part.id) * (hanging ? 0.7f : 1f), 0f, true);
            w.desc = hanging ? "LIMB HANGING BY SKIN" : "TRAUMATIC AMPUTATION";
            body.physio.AddPain(1f, part.id);
            body.physio.AddStress(1f);
            part.bloodiness = 1.2f;
            BloodSystem.Instance?.Spray(cutW, axW + Random.insideUnitSphere * 0.4f, 25, 1.5f, 4f, 0.7f, 0.008f, part);
            lrb.AddForce(dirWorld.normalized * (hanging ? 1.5f : 4f), ForceMode.Impulse);

            if (hanging)
            {
                // strands of skin and muscle: a short, stretchy tether that lets it swing and dangle
                thread = lower.AddComponent<ConfigurableJoint>();
                thread.connectedBody = part.rb;
                thread.autoConfigureConnectedAnchor = false;
                thread.anchor = cutP;
                thread.connectedAnchor = cutP;
                thread.xMotion = thread.yMotion = thread.zMotion = ConfigurableJointMotion.Limited;
                // the strands only give a few centimetres: pull on the limb and the body comes with it
                thread.linearLimit = new SoftJointLimit { limit = 0.02f + 0.03f * skin[k], contactDistance = 0.005f };
                thread.linearLimitSpring = new SoftJointLimitSpring { spring = 0f, damper = 0f };
                thread.projectionMode = JointProjectionMode.PositionAndRotation;
                thread.projectionDistance = 0.02f;
                thread.angularXMotion = thread.angularYMotion = thread.angularZMotion = ConfigurableJointMotion.Free;
                thread.enableCollision = false;
                thread.breakForce = float.PositiveInfinity;   // it only comes off when what holds it is destroyed, not from being swung about
                var fxGo = new GameObject("SkinThread");
                threadFx = fxGo.AddComponent<SkinThread>();
                threadFx.Init(part.transform, lower.transform, cutP, axisL, part.uvRadius, skin[k], tissue[k]);
            }
            Debug.Log($"[Limb] {part.id} {(hanging ? "hanging" : "severed")} at slice {k}/{n}");
        }

        static bool InXray(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "Xray") return true;
            return false;
        }

        static void SetPlane(MeshRenderer r, Matrix4x4 objToPart, Vector3 pointPart, Vector3 normalPart, bool partSpace)
        {
            Vector4 plane;
            if (partSpace) plane = new Vector4(normalPart.x, normalPart.y, normalPart.z, -Vector3.Dot(normalPart, pointPart));
            else
            {
                Vector3 q = objToPart.inverse.MultiplyPoint3x4(pointPart);
                Vector3 no = objToPart.transpose.MultiplyVector(normalPart).normalized;
                plane = new Vector4(no.x, no.y, no.z, -Vector3.Dot(no, q));
            }
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetVector("_ClipPlane", plane);
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>The torn-off far end of a limb: a loose, bleeding piece you can shoot, kick around and drag.</summary>
    public class LimbChunk : MonoBehaviour
    {
        public BodyPart from;
        public LimbIntegrity li;
        public float bleed = 10f;
        public readonly List<MeshRenderer> organs = new List<MeshRenderer>();
        MaterialPropertyBlock mpb;
        float dripAcc;

        void LateUpdate()
        {
            // it shares the body's materials: when the body is gone (cleared, despawned) so is this
            if (from == null) { Destroy(gameObject); return; }
            // organ shaders work in the limb's own frame: this piece moves on its own now
            if (organs.Count > 0)
            {
                mpb ??= new MaterialPropertyBlock();
                foreach (var r in organs)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetMatrix("_PartW2L", transform.worldToLocalMatrix);
                    r.SetPropertyBlock(mpb);
                }
            }
            // drains out of the torn end for a while
            if (bleed > 0f && BloodSystem.Instance != null && !GameSettings.NoBlood)
            {
                bleed -= Time.deltaTime * 0.6f;
                dripAcc += Time.deltaTime * Mathf.Max(0.5f, bleed * 0.8f);
                while (dripAcc > 1f)
                {
                    dripAcc -= 1f;
                    var b = GetComponent<Collider>().bounds;
                    Vector3 low = b.center - Vector3.up * b.extents.y * 0.9f + Random.insideUnitSphere * 0.02f;
                    BloodSystem.Instance.Spray(low, Vector3.down, 1, 0.05f, 0.3f, 0.4f, 0.007f, null);
                }
            }
        }

        void OnJointBreak(float force) { if (li != null) li.ThreadSnapped(); }

        /// <summary>Shot: blood, a shove, and if close to the tear it chews through what is still holding it on.</summary>
        public void Hit(Vector3 point, Vector3 dir, float energy)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.AddForceAtPosition(dir * (1f + energy * 0.004f), point, ForceMode.Impulse);
            BloodSystem.Instance?.Spray(point, -dir, 3, 1f, 3f, 0.6f, 0.006f, null);
            BloodSystem.Instance?.Spray(point, dir, 4, 2f, 4f, 0.4f, 0.006f, null);
            // only a hit right at the tear chews through what is still holding it on
            if (li != null && li.AtTear(transform, point)) li.DamageThread(Mathf.Clamp(energy / 6000f, 0.1f, 0.6f), Mathf.Clamp(energy / 9000f, 0.08f, 0.4f));
        }
    }

    /// <summary>
    /// The strands of skin and muscle still holding a torn limb on: round, sagging cords between the two torn
    /// ends (solid from every side - flat ribbons flickered as they turned edge-on).
    /// </summary>
    public class SkinThread : MonoBehaviour
    {
        Transform a, b;
        Vector3 c, ax;
        float r;
        int skinStrands, meatStrands;
        Mesh mesh;
        float[] angles, thick;
        static Material skinMat, meatMat;
        const int Len = 7, Ring = 6;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int> skinT = new List<int>(), meatT = new List<int>();

        public void Init(Transform upper, Transform lower, Vector3 cutLocal, Vector3 axisLocal, float radius, float skinLeft, float meatLeft)
        {
            a = upper; b = lower; c = cutLocal; ax = axisLocal; r = radius;
            skinStrands = Mathf.Clamp(Mathf.RoundToInt(skinLeft * 6f), 1, 5);
            meatStrands = Mathf.Clamp(Mathf.RoundToInt(meatLeft * 8f), 0, 3);
            int n = skinStrands + meatStrands;
            angles = new float[n]; thick = new float[n];
            float start = Random.value * 6.28f;
            for (int i = 0; i < n; i++)
            {
                bool isSkin = i < skinStrands;
                angles[i] = start + Random.Range(-0.4f, 0.4f) + (isSkin ? i * 0.55f : 3.1f + i * 0.4f);
                thick[i] = (isSkin ? 0.0045f : 0.006f) * Random.Range(0.8f, 1.3f);
            }
            if (skinMat == null) { skinMat = Mats.Lit(new Color(0.84f, 0.6f, 0.5f), 0.35f); meatMat = Mats.Lit(new Color(0.42f, 0.05f, 0.06f), 0.55f); }
            mesh = new Mesh { name = "Thread" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { skinMat, meatMat };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void LateUpdate()
        {
            if (a == null || b == null) { Destroy(gameObject); return; }
            verts.Clear(); skinT.Clear(); meatT.Clear();
            Vector3 u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(ax, u);
            for (int i = 0; i < angles.Length; i++)
            {
                bool isSkin = i < skinStrands;
                float rr = r * (isSkin ? 0.97f : 0.5f);
                Vector3 off = (u * Mathf.Cos(angles[i]) + v * Mathf.Sin(angles[i])) * rr;
                Vector3 p0 = a.TransformPoint(c + off), p1 = b.TransformPoint(c + off);
                float span = Vector3.Distance(p0, p1);
                // stretched strands get thinner
                float th = thick[i] * Mathf.Clamp(0.04f / Mathf.Max(0.01f, span), 0.45f, 1.2f);
                int baseIdx = verts.Count;
                for (int k = 0; k <= Len; k++)
                {
                    float t = k / (float)Len;
                    Vector3 p = Vector3.Lerp(p0, p1, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * Mathf.Min(0.015f, span * 0.3f);
                    Vector3 dir = (p1 - p0).sqrMagnitude > 1e-8f ? (p1 - p0).normalized : a.TransformDirection(ax);
                    Vector3 s1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized, s2 = Vector3.Cross(dir, s1);
                    float w = th * (0.75f + 0.25f * Mathf.Sin(t * Mathf.PI));      // thicker in the middle, torn thin at the ends
                    for (int j = 0; j < Ring; j++)
                    {
                        float ang = j / (float)Ring * Mathf.PI * 2f;
                        verts.Add(p + (s1 * Mathf.Cos(ang) + s2 * Mathf.Sin(ang)) * w);
                    }
                }
                var list = isSkin ? skinT : meatT;
                for (int k = 0; k < Len; k++)
                    for (int j = 0; j < Ring; j++)
                    {
                        int j1 = (j + 1) % Ring;
                        int i0 = baseIdx + k * Ring + j, i1 = baseIdx + k * Ring + j1, i2 = i0 + Ring, i3 = i1 + Ring;
                        list.Add(i0); list.Add(i2); list.Add(i1);
                        list.Add(i1); list.Add(i2); list.Add(i3);
                    }
            }
            mesh.Clear();
            mesh.subMeshCount = 2;
            mesh.SetVertices(verts);
            mesh.SetTriangles(skinT, 0);
            mesh.SetTriangles(meatT, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
