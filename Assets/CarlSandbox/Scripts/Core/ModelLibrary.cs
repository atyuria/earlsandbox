using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Loads meshes exported from Blender (Resources/Models/*.json: Unity-space vertices, triangles per material, named points).
    /// </summary>
    public static class ModelLibrary
    {
        [Serializable] class Sub { public string mat; public int[] tris; }
        [Serializable] class Data { public string name; public float[] verts; public Sub[] subs; }

        public class Model
        {
            public Mesh mesh;
            public string[] materials;
            public Dictionary<string, Vector3> points = new Dictionary<string, Vector3>();
        }

        static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();

        public static Model Get(string name, bool mirrorX = false)
        {
            string key = name + (mirrorX ? "_M" : "");
            if (cache.TryGetValue(key, out var m) && m.mesh != null) return m;
            var ta = Resources.Load<TextAsset>("Models/" + name);
            if (ta == null) { Debug.LogWarning($"[Carl] Model not found: {name}"); return null; }
            var d = JsonUtility.FromJson<Data>(ta.text);
            m = new Model();
            var v = new Vector3[d.verts.Length / 3];
            for (int i = 0; i < v.Length; i++)
                v[i] = new Vector3(d.verts[i * 3] * (mirrorX ? -1f : 1f), d.verts[i * 3 + 1], d.verts[i * 3 + 2]);
            var mesh = new Mesh { name = key, indexFormat = v.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.vertices = v;
            mesh.subMeshCount = d.subs.Length;
            m.materials = new string[d.subs.Length];
            for (int s = 0; s < d.subs.Length; s++)
            {
                var t = d.subs[s].tris;
                if (mirrorX)
                {
                    t = (int[])t.Clone();
                    for (int i = 0; i < t.Length; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
                }
                mesh.SetTriangles(t, s);
                m.materials[s] = d.subs[s].mat;
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            m.mesh = mesh;

            // "points":{"Muzzle":[x,y,z],...}
            var pm = Regex.Match(ta.text, "\"points\":\\{(.*?)\\}");
            if (pm.Success)
            {
                foreach (Match p in Regex.Matches(pm.Groups[1].Value, "\"(\\w+)\":\\[([-\\d.eE]+),([-\\d.eE]+),([-\\d.eE]+)\\]"))
                {
                    float x = float.Parse(p.Groups[2].Value, CultureInfo.InvariantCulture);
                    float y = float.Parse(p.Groups[3].Value, CultureInfo.InvariantCulture);
                    float z = float.Parse(p.Groups[4].Value, CultureInfo.InvariantCulture);
                    m.points[p.Groups[1].Value] = new Vector3(mirrorX ? -x : x, y, z);
                }
            }
            cache[key] = m;
            return m;
        }

        /// <summary>Instantiates a model as a child object with materials resolved by name.</summary>
        public static GameObject Spawn(string name, Transform parent, Func<string, Material> mat, bool shadows = true, bool mirrorX = false)
        {
            var m = Get(name, mirrorX);
            if (m == null) return null;
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = m.mesh;
            var mr = g.AddComponent<MeshRenderer>();
            var mats = new Material[m.materials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat(m.materials[i]);
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        static Material glass;

        public static Material GunMaterial(string name)
        {
            switch (name)
            {
                case "Wood": return Mats.Lit(new Color(0.58f, 0.3f, 0.13f), 0.35f);
                case "Polymer": return Mats.Lit(new Color(0.1f, 0.1f, 0.11f), 0.3f);
                case "Steel": return Mats.Lit(new Color(0.62f, 0.64f, 0.67f), 0.75f, 0.85f);
                case "Olive": return Mats.Lit(new Color(0.3f, 0.36f, 0.22f), 0.3f);
                case "Yellow": return Mats.Lit(new Color(0.95f, 0.78f, 0.08f), 0.45f);          // Taser body
                case "Tan": return Mats.Lit(new Color(0.62f, 0.52f, 0.38f), 0.3f);               // flat dark earth
                case "Rubber": return Mats.Lit(new Color(0.06f, 0.06f, 0.065f), 0.12f);          // grip panels, pads
                case "Brass": return Mats.Lit(new Color(0.78f, 0.6f, 0.26f), 0.7f, 0.9f);
                case "Lens": return Mats.Lit(new Color(0.75f, 0.8f, 0.85f), 0.95f, 0.3f);         // torch lens, laser window
                case "Anod": return Mats.Lit(new Color(0.11f, 0.11f, 0.12f), 0.55f, 0.7f);        // anodised aluminium
                case "Gauze": return Mats.Lit(new Color(0.93f, 0.92f, 0.88f), 0.05f);             // bandage
                case "Red": return Mats.Lit(new Color(0.75f, 0.07f, 0.06f), 0.25f);               // red cross
                case "Glass":
                    if (glass == null) { glass = new Material(Mats.Find("Carl/Glass")); }
                    return glass;
                default: return Mats.Lit(new Color(0.16f, 0.16f, 0.17f), 0.5f, 0.6f);
            }
        }
    }
}
