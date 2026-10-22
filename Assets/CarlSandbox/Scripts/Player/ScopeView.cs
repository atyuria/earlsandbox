using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Every optic as picture-in-picture, the way Tarkov does it: aiming, you still see the gun, your hands and the
    /// world round the sight at nearly normal zoom, and the view down the optic is only in its glass - rendered by
    /// a second camera at the front of the optic looking down its line, at a field of view worked out from how big
    /// the glass looks from your eye and the magnification (so a 1x red dot lines up exactly with the world round
    /// it, and a 4x or 8x scope magnifies just what is in the glass). The reticle is drawn on that picture, and a
    /// magnified scope has its scope shadow (Carl/ScopeLens). The glass is found on each optic model.
    /// </summary>
    [DefaultExecutionOrder(120)]   // after WeaponController has put the gun where it is held this frame
    public class ScopeView : MonoBehaviour
    {
        WeaponController w;
        Camera scopeCam;
        RenderTexture rt;
        GameObject lens;
        Material lensMat;
        string builtFor = "";
        Transform optic;
        Vector3 ocular, objective;
        float lensW = 0.016f, lensH = 0.016f;

        public static ScopeView Attach(WeaponController w)
        {
            var s = w.gameObject.AddComponent<ScopeView>();
            s.w = w;
            return s;
        }

        /// <summary>How many times the optic magnifies (0: iron sights, nothing to look through).</summary>
        public static float Magnification(WeaponController w)
        {
            if (!w.Armed || w.Current.type != WeaponType.Gun) return 0f;
            switch (w.Loadout.sight)
            {
                case SightKind.Scope8x: return 8f;
                case SightKind.Scope4x: return 4f;
                case SightKind.RedDot: case SightKind.Holo: return w.Loadout.magnifier && WeaponController.MagnifierFor(w.Current) ? 3f : 1f;
                default: return 0f;
            }
        }

        void EnsureCam()
        {
            if (scopeCam != null) return;
            rt = new RenderTexture(768, 768, 24) { antiAliasing = 2, name = "ScopeRT" };
            var go = new GameObject("ScopeCam");
            scopeCam = go.AddComponent<Camera>();
            scopeCam.targetTexture = rt;
            scopeCam.nearClipPlane = 0.03f; scopeCam.farClipPlane = 1500f;
            scopeCam.enabled = false;
            lensMat = new Material(Mats.Find("Carl/ScopeLens"));
            lensMat.SetTexture("_MainTex", rt);
        }

        string OpticModel()
        {
            var s = w.Loadout.sight;
            if (w.Current.scope) return "Gun_";
            return s == SightKind.RedDot ? "Att_RedDot" : s == SightKind.Holo ? "Att_Holo" : "Att_Scope";
        }

        /// <summary>Find the optic's glass on the model and put the picture there.</summary>
        void Build()
        {
            if (lens != null) Destroy(lens);
            lens = null; optic = null;
            var root = w.CurrentModel;
            if (root == null) return;
            string want = OpticModel();
            MeshFilter mf = null;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) if (f.name.StartsWith(want)) { mf = f; break; }
            if (mf == null || mf.sharedMesh == null) return;
            var mr = mf.GetComponent<MeshRenderer>();
            var mesh = mf.sharedMesh; var v = mesh.vertices;
            int glassSub = -1;
            for (int s = 0; s < mesh.subMeshCount && mr != null && s < mr.sharedMaterials.Length; s++)
                if (mr.sharedMaterials[s] != null && mr.sharedMaterials[s].shader != null && mr.sharedMaterials[s].shader.name == "Carl/Glass") glassSub = s;
            var tri = glassSub >= 0 ? mesh.GetTriangles(glassSub) : null;
            if (tri == null || tri.Length == 0) return;
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (int i in tri) { zMin = Mathf.Min(zMin, v[i].z); zMax = Mathf.Max(zMax, v[i].z); }
            Vector3 front = Vector3.zero; int nf = 0;
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (int i in tri)
            {
                if (v[i].z < zMin + 0.005f) { x0 = Mathf.Min(x0, v[i].x); x1 = Mathf.Max(x1, v[i].x); y0 = Mathf.Min(y0, v[i].y); y1 = Mathf.Max(y1, v[i].y); }
                if (v[i].z > zMax - 0.005f) { front += v[i]; nf++; }
            }
            if (nf == 0 || x0 > x1) return;
            optic = mf.transform;
            ocular = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, zMin);
            objective = front / nf;
            lensW = Mathf.Clamp((x1 - x0) * 0.5f, 0.006f, 0.035f);
            lensH = Mathf.Clamp((y1 - y0) * 0.5f, 0.006f, 0.035f);
            bool rect = w.Loadout.sight == SightKind.Holo && !w.Current.scope;
            if (!rect) lensW = lensH = Mathf.Min(lensW, lensH);
            lens = new GameObject("ScopeEyepiece");
            lens.transform.SetParent(optic, false);
            lens.transform.localPosition = ocular - Vector3.forward * 0.0015f;
            lens.transform.localRotation = Quaternion.identity;
            lens.layer = optic.gameObject.layer;
            var q = new Mesh { name = "Eyepiece" };
            q.vertices = new[] { new Vector3(-lensW, -lensH, 0), new Vector3(lensW, -lensH, 0), new Vector3(lensW, lensH, 0), new Vector3(-lensW, lensH, 0) };
            q.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            q.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            q.RecalculateBounds();
            lens.AddComponent<MeshFilter>().sharedMesh = q;
            var lr = lens.AddComponent<MeshRenderer>();
            lr.sharedMaterial = lensMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lensMat.SetFloat("_Mask", rect ? 1f : 0f);
            lensMat.SetFloat("_Aspect", lensW / lensH);
        }

        void LateUpdate()
        {
            if (w == null) return;
            float mag = Magnification(w);
            bool want = mag > 0f && w.AimAmount > 0.02f;
            if (!want)
            {
                if (scopeCam != null && scopeCam.enabled) scopeCam.enabled = false;
                if (lens != null && lens.activeSelf) lens.SetActive(false);
                return;
            }
            EnsureCam();
            string key = w.current + "|" + w.Loadout.sight + "|" + w.Loadout.magnifier + "|" + (w.CurrentModel != null ? w.CurrentModel.transform.childCount : -1);
            if (key != builtFor || lens == null) { builtFor = key; Build(); }
            if (optic == null || lens == null) return;
            if (!lens.activeSelf) lens.SetActive(true);
            var main = w.MainCam;
            // how big the glass looks from your eye: the optic shows that much of the world, divided by its power
            Vector3 lensC = lens.transform.position;
            float dist = Mathf.Max(0.03f, Vector3.Distance(main.transform.position, lensC));
            float halfAng = Mathf.Atan(lensH / dist) * Mathf.Rad2Deg;
            float shownHalf = Mathf.Clamp(halfAng / mag, 0.15f, 40f);
            scopeCam.transform.SetPositionAndRotation(optic.TransformPoint(objective + Vector3.forward * 0.01f), optic.rotation);
            scopeCam.fieldOfView = shownHalf * 2f;
            scopeCam.aspect = lensW / lensH;
            scopeCam.cullingMask = main.cullingMask & ~(1 << PlayerAvatar.OwnLayer);
            scopeCam.clearFlags = main.clearFlags; scopeCam.backgroundColor = main.backgroundColor;
            var s = w.Loadout.sight;
            float ret = w.Current.scope || s == SightKind.Scope8x ? 1f : s == SightKind.Scope4x ? 0f : s == SightKind.Holo ? 3f : 2f;
            lensMat.SetFloat("_Reticle", ret);
            lensMat.SetFloat("_MOA", shownHalf * 60f);
            bool magnified = mag >= 3.5f;
            lensMat.SetFloat("_Shadow", magnified ? 1f : 0f);
            lensMat.SetFloat("_Pupil", mag >= 6f ? 0.016f : 0.022f);
            lensMat.SetFloat("_Relief", Mathf.Max(0.1f, dist));     // the eye relief of how it is held (cheek on the stock)
            scopeCam.Render();
        }

        void OnDestroy() { if (rt != null) rt.Release(); }
    }
}
