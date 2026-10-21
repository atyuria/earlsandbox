using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>Global x-ray toggle (X) and the label for whatever structure is under the crosshair.</summary>
    public class XrayController : MonoBehaviour
    {
        public bool on;
        public string hoverLabel;
        public Color hoverColor;
        readonly RaycastHit[] buf = new RaycastHit[16];

        public void Toggle() => on = !on;
        static Material lineMat;

        /// <summary>Wound tracks glow through the body on the x-ray: bright red for bleeding, dull for clotted.</summary>
        void OnRenderObject()
        {
            if (Camera.current == null) return;
            if (lineMat == null) { var ls = Shader.Find("Hidden/Carl/Lines"); if (ls == null) return; lineMat = new Material(ls); }
            lineMat.SetPass(0);
            GL.Begin(GL.LINES);
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 8f);
            foreach (var h in HumanBody.All)
            {
                if (h == null || !(on || h.forceXray)) continue;
                foreach (var bp in h.partList)
                    foreach (var w in bp.wounds)
                    {
                        if (w.type == WoundType.Internal) continue;
                        Vector3 p = bp.transform.TransformPoint(w.localPos);
                        Vector3 n = bp.transform.TransformDirection(w.localNormal);
                        float bleed = Mathf.Clamp01(w.extRate / 4f);
                        GL.Color(new Color(1f, 0.15f, 0.1f, (0.35f + 0.65f * bleed) * (w.arterial ? pulse : 1f)));
                        GL.Vertex(p + n * 0.01f); GL.Vertex(p - n * 0.09f);
                        float r = 0.012f + bleed * 0.01f;
                        Vector3 a = Vector3.Cross(n, Vector3.up).normalized * r, b = Vector3.Cross(n, a).normalized * r;
                        GL.Vertex(p - a); GL.Vertex(p + a); GL.Vertex(p - b); GL.Vertex(p + b);
                    }
            }
            GL.End();
        }

        // ------------------------------------------------------------ armour during the scan
        // Solid armour would hide the inside, so while scanning it is swapped for a see-through copy drawn over
        // the body: you see what they wear and what is under it. A copy rather than a material swap, because
        // armour makes its own damage material from whatever it is wearing when it is first hit.
        static Material armourGhostMat;
        readonly Dictionary<MeshRenderer, MeshRenderer> armourGhosts = new Dictionary<MeshRenderer, MeshRenderer>();
        readonly HashSet<MeshRenderer> ghostSet = new HashSet<MeshRenderer>();
        readonly List<MeshRenderer> deadGhosts = new List<MeshRenderer>();
        static readonly Color ScanTint = new Color(0.6f, 0.82f, 1f);

        readonly HashSet<MeshRenderer> visited = new HashSet<MeshRenderer>();

        void ScanArmour(MeshRenderer r, bool scanning)
        {
            if (r == null || ghostSet.Contains(r)) return;
            visited.Add(r);
            r.enabled = !scanning;
            armourGhosts.TryGetValue(r, out var g);
            if (!scanning) { if (g) g.enabled = false; return; }
            var srcMf = r.GetComponent<MeshFilter>();
            if (srcMf == null || srcMf.sharedMesh == null) return;
            if (g == null)
            {
                if (armourGhostMat == null) armourGhostMat = new Material(Mats.Find("Carl/XrayArmour")) { renderQueue = 3030 };
                var go = new GameObject("XrayArmourGhost");
                go.layer = r.gameObject.layer;
                go.transform.SetParent(r.transform, false);
                go.AddComponent<MeshFilter>();
                g = go.AddComponent<MeshRenderer>();
                g.sharedMaterial = armourGhostMat;
                g.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.receiveShadows = false;
                // the armour's own colour, pulled towards the scanner's blue so it reads as a layer, not as skin
                var src = r.sharedMaterial;
                Color c = src != null && src.HasProperty("_Color") ? src.GetColor("_Color") : new Color(0.5f, 0.55f, 0.5f);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", Color.Lerp(c, ScanTint, 0.35f));
                g.SetPropertyBlock(mpb);
                armourGhosts[r] = g;
                ghostSet.Add(g);
            }
            // dents and cuts replace the mesh: keep the copy in step
            var mf = g.GetComponent<MeshFilter>();
            if (mf.sharedMesh != srcMf.sharedMesh) mf.sharedMesh = srcMf.sharedMesh;
            g.enabled = true;
        }

        void LateUpdate()
        {
            foreach (var h in HumanBody.All) h.SetXray(on || h.forceXray);
            visited.Clear();
            foreach (var a in ArmourSystem.All)
                if (a != null && a.part != null && a.part.body != null)
                {
                    bool scanning = on || a.part.body.forceXray;
                    foreach (var r in a.GetComponentsInChildren<MeshRenderer>()) ScanArmour(r, scanning);
                    foreach (Transform t in a.part.transform)
                        if (t.GetComponent<ArmourPiece>() == null && (t.name == "Trim" || t.name == "Plume")) ScanArmour(t.GetComponent<MeshRenderer>(), scanning);
                }
            deadGhosts.Clear();
            foreach (var kv in armourGhosts)
            {
                if (kv.Key == null) { deadGhosts.Add(kv.Key); continue; }
                // shot off and lying on the floor: no longer worn, so back to solid
                if (!visited.Contains(kv.Key) && kv.Value != null && kv.Value.enabled) { kv.Value.enabled = false; kv.Key.enabled = true; }
            }
            foreach (var k in deadGhosts) { var g = armourGhosts[k]; armourGhosts.Remove(k); ghostSet.Remove(g); if (g) Destroy(g.gameObject); }
            hoverLabel = null;
            var game = SandboxGame.Instance;
            if (!on || game == null || game.cam == null) return;

            var cam = game.cam.transform;
            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, buf, 150f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; BodyPart part = null; Vector3 point = default;
            for (int i = 0; i < n; i++)
            {
                if (buf[i].collider is CharacterController) continue;
                if (PlayerAvatar.Body != null && buf[i].collider.GetComponentInParent<HumanBody>() == PlayerAvatar.Body) continue;
                if (buf[i].distance >= best) continue;
                best = buf[i].distance;
                part = buf[i].collider.GetComponent<BodyPart>();
                point = buf[i].point;
            }
            if (part == null) return;

            Vector3 p0 = part.transform.InverseTransformPoint(point);
            Vector3 p1 = part.transform.InverseTransformPoint(point + cam.forward * 1f);
            Organ pick = null, shell = null; float bt = float.MaxValue, st = float.MaxValue;
            foreach (var o in part.organs)
            {
                if (!o.def.Intersect(p0, p1, 0.004f, out float t0, out _)) continue;
                if (o.def.shell) { if (t0 < st) { st = t0; shell = o; } }
                else if (t0 < bt) { bt = t0; pick = o; }
            }
            var org = pick ?? shell;
            if (org != null)
            {
                hoverLabel = $"{org.def.name}  —  {org.StatusText()}";
                hoverColor = org.def.color;
            }
            else
            {
                hoverLabel = $"{part.Name}  —  SOFT TISSUE {(1f - part.tissueDamage) * 100f:0}%";
                hoverColor = new Color(0.86f, 0.71f, 0.58f);
            }
        }
    }
}
