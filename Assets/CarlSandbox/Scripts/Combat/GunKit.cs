using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Attachments for a gun model that isn't in your hands (an Earl's rifle): where its rail and handguard take
    /// them, found from the model's own shape exactly as for your guns (WeaponController), and fitting a sight,
    /// a foregrip, a suppressor and a light to it. Also rolls a random setup.
    /// </summary>
    public static class GunKit
    {
        public struct Mounts { public Vector3 top, under, muzzle, grip; }
        static readonly Dictionary<string, Mounts> cache = new Dictionary<string, Mounts>();

        /// <summary>Rail top, the underside of the handguard and the muzzle, in the model's space.</summary>
        public static Mounts For(string model)
        {
            if (cache.TryGetValue(model, out var m)) return m;
            var md = ModelLibrary.Get(model);
            m = new Mounts { top = new Vector3(0f, 0.05f, 0f), under = new Vector3(0f, -0.03f, 0.2f), muzzle = new Vector3(0f, 0f, 0.4f) };
            if (md == null) return m;
            Vector3 grip = md.points.TryGetValue("Grip", out var g) ? g : Vector3.zero;
            Vector3 muzzle = md.points.TryGetValue("Muzzle", out var mz) ? mz : new Vector3(0, 0, 0.3f);
            float zA = Mathf.Lerp(grip.z, muzzle.z, 0.05f), zB = Mathf.Lerp(grip.z, muzzle.z, 0.35f), zC = Mathf.Lerp(grip.z, muzzle.z, 0.5f), zD = Mathf.Lerp(grip.z, muzzle.z, 0.68f);
            float top = float.MinValue, under = float.MaxValue;
            foreach (var v in md.mesh.vertices)
            {
                if (v.z > zA && v.z < zB) top = Mathf.Max(top, v.y);
                if (v.z > zC && v.z < zD && Mathf.Abs(v.x) < 0.03f) under = Mathf.Min(under, v.y);
            }
            m.top = new Vector3(0f, top == float.MinValue ? 0.05f : top, (zA + zB) * 0.5f);
            m.under = new Vector3(0f, under == float.MaxValue ? -0.03f : under, (zC + zD) * 0.5f);
            m.muzzle = muzzle; m.grip = grip;
            cache[model] = m;
            return m;
        }

        /// <summary>A setup an Earl might have put together for a rifle: mostly irons or a red dot, sometimes more.</summary>
        public static WeaponLoadout Random(bool rifle)
        {
            var L = new WeaponLoadout();
            float r = UnityEngine.Random.value;
            L.sight = !rifle ? SightKind.Iron : r < 0.35f ? SightKind.Iron : r < 0.65f ? SightKind.RedDot : r < 0.88f ? SightKind.Holo : SightKind.Scope4x;
            if (rifle)
            {
                float g = UnityEngine.Random.value;
                L.grip = g < 0.5f ? GripKind.None : g < 0.8f ? GripKind.Vertical : GripKind.Angled;
                L.light = UnityEngine.Random.value < 0.3f;
            }
            L.suppressor = UnityEngine.Random.value < 0.35f;
            Attachments.Randomise(L, Nations.Active ? -1 : -1);
            return L;
        }

        /// <summary>
        /// Puts the attachments on a gun model (children of root, which is in the model's space). Returns the
        /// muzzle in model space (further out with a suppressor on).
        /// </summary>
        public static Vector3 Fit(Transform root, string model, WeaponLoadout L, int layer)
        {
            var m = For(model);
            Vector3 muzzle = m.muzzle;
            void Put(string name, Vector3 at, Vector3 euler = default)
            {
                var g = ModelLibrary.Spawn(name, root, ModelLibrary.GunMaterial, true);
                if (g == null) return;
                g.transform.localPosition = at; g.transform.localRotation = Quaternion.Euler(euler);
                g.layer = layer;
            }
            string sight = Attachments.OpticOf(L)?.model;
            if (sight != null) Put(sight, m.top);
            if (L.grip != GripKind.None) Put(Attachments.GripOf(L)?.model ?? "Att_GripVertical", m.under);
            if (L.light) Put(Attachments.LightOf(L)?.model ?? "Att_Light", new Vector3(0.026f, -0.004f, Mathf.Lerp(m.grip.z, m.muzzle.z, 0.5f)), new Vector3(0f, 0f, 90f));
            if (L.suppressor && !model.StartsWith("Build_"))
            {
                bool pistol = model == "Gun_Glock" || model == "Gun_Beretta";
                string can = pistol ? "Att_SuppPistol" : "Att_SuppRifle";
                Put(can, m.muzzle);
                var cm = ModelLibrary.Get(can);
                muzzle = m.muzzle + (cm != null && cm.points.TryGetValue("Front", out var f) ? f : new Vector3(0f, 0f, 0.18f));
            }
            return muzzle;
        }
    }
}
