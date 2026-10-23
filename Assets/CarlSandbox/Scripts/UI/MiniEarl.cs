using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A little Earl in the bottom-left corner showing what has happened to you, at a glance: each part of him
    /// coloured by how hurt yours is (grey-green fine, yellow hurt, orange bad, red ruined), a gone limb just an
    /// outline, a red drop pulsing on a part that is bleeding (faster the worse it bleeds), a white strip where
    /// it's bandaged or stitched, a white tick for a broken bone. Blood and bleeding under him. H opens the full
    /// medical readout.
    /// </summary>
    public class MiniEarl : MonoBehaviour
    {
        void Awake() { useGUILayout = false; }   // (its OnGUI draws directly: no layout pass needed)
        Texture2D round, drop;
        GUIStyle tiny;

        // the figure, front view (your left on the right of the picture), in a 0..1 box
        static readonly (PartId id, Rect r)[] Layout =
        {
            (PartId.Head, new Rect(0.415f, 0.0f, 0.17f, 0.12f)),
            (PartId.Neck, new Rect(0.465f, 0.115f, 0.07f, 0.04f)),
            (PartId.Chest, new Rect(0.35f, 0.15f, 0.3f, 0.17f)),
            (PartId.Abdomen, new Rect(0.37f, 0.32f, 0.26f, 0.1f)),
            (PartId.Pelvis, new Rect(0.36f, 0.42f, 0.28f, 0.08f)),
            (PartId.UpperArmR, new Rect(0.22f, 0.16f, 0.12f, 0.17f)),
            (PartId.ForearmR, new Rect(0.18f, 0.335f, 0.11f, 0.15f)),
            (PartId.HandR, new Rect(0.16f, 0.49f, 0.1f, 0.065f)),
            (PartId.UpperArmL, new Rect(0.66f, 0.16f, 0.12f, 0.17f)),
            (PartId.ForearmL, new Rect(0.71f, 0.335f, 0.11f, 0.15f)),
            (PartId.HandL, new Rect(0.74f, 0.49f, 0.1f, 0.065f)),
            (PartId.ThighR, new Rect(0.37f, 0.505f, 0.125f, 0.2f)),
            (PartId.ShinR, new Rect(0.375f, 0.71f, 0.11f, 0.2f)),
            (PartId.FootR, new Rect(0.355f, 0.915f, 0.13f, 0.055f)),
            (PartId.ThighL, new Rect(0.505f, 0.505f, 0.125f, 0.2f)),
            (PartId.ShinL, new Rect(0.515f, 0.71f, 0.11f, 0.2f)),
            (PartId.FootL, new Rect(0.515f, 0.915f, 0.13f, 0.055f)),
        };

        void Init()
        {
            round = Menus.Rounded(Color.white, new Color(0, 0, 0, 0), 6);
            int s = 32;
            drop = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    // a teardrop: round at the bottom, pointed at the top
                    float r = Mathf.Sqrt(u * u + (v + 0.25f) * (v + 0.25f));
                    bool inside = r < 0.6f || (v > -0.25f && Mathf.Abs(u) < 0.6f * (1f - (v + 0.25f) / 1.2f));
                    drop.SetPixel(x, y, new Color(1, 1, 1, inside ? 1f : 0f));
                }
            drop.Apply();
            tiny = new GUIStyle { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.92f, 0.93f, 0.95f) } };
        }

        /// <summary>0 fine .. 1 ruined: soft tissue, the bones in it, its bleeding.</summary>
        static float Hurt(BodyPart bp, out bool broken, out float bleed, out bool treated)
        {
            broken = false; bleed = 0f; treated = false;
            if (bp == null) return 1f;
            float h = bp.tissueDamage;
            foreach (var o in bp.organs) if (o.IsBone && o.Fractured) { broken = true; h = Mathf.Max(h, 0.55f); }
            foreach (var w in bp.wounds) { bleed += w.extRate + w.intRate * 0.5f; if (w.bandaged || w.sutured) treated = true; if (w.type != WoundType.Bruise) h = Mathf.Max(h, 0.18f); }
            h = Mathf.Max(h, Mathf.Clamp01(bleed / 12f));
            return Mathf.Clamp01(h);
        }

        static Color Shade(float h)
        {
            var ok = new Color(0.42f, 0.55f, 0.42f, 0.9f);
            if (h < 0.33f) return Color.Lerp(ok, new Color(0.9f, 0.82f, 0.25f, 0.95f), h / 0.33f);
            if (h < 0.66f) return Color.Lerp(new Color(0.9f, 0.82f, 0.25f, 0.95f), new Color(0.95f, 0.45f, 0.12f, 0.95f), (h - 0.33f) / 0.33f);
            return Color.Lerp(new Color(0.95f, 0.45f, 0.12f, 0.95f), new Color(0.85f, 0.08f, 0.06f, 1f), (h - 0.66f) / 0.34f);
        }

        void OnGUI()
        {
            var me = PlayerAvatar.Body;
            var game = SandboxGame.Instance;
            if (me == null || game == null || game.medical == null) return;
            if (game.medical.selected == me) return;                 // the full readout (H) is open instead
            if (game.menus != null && game.menus.IsOpen) return;
            if (round == null) Init();
            GUI.depth = -40;
            float H = Screen.height;
            const float w = 92f, h = 196f;
            var box = new Rect(14f, H - 132f - h, w, h);
            float pulse = Time.unscaledTime;
            foreach (var (id, rr) in Layout)
            {
                var r = new Rect(box.x + rr.x * w, box.y + rr.y * h, rr.width * w, rr.height * h);
                var bp = me[id];
                bool gone = bp == null || bp.severed;
                if (gone)
                {
                    // just the outline where it was
                    GUI.color = new Color(0.85f, 0.15f, 0.12f, 0.6f);
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.x, r.y, 1.5f, r.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - 1.5f, r.y, 1.5f, r.height), Texture2D.whiteTexture);
                    continue;
                }
                float hurt = Hurt(bp, out bool broken, out float bleed, out bool treated);
                // a thin dark edge so he reads on any background (no box behind him)
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f), round, ScaleMode.StretchToFill);
                GUI.color = Shade(hurt);
                GUI.DrawTexture(r, round, ScaleMode.StretchToFill);
                if (treated) { GUI.color = new Color(1f, 1f, 1f, 0.85f); GUI.DrawTexture(new Rect(r.x + 1, r.center.y - 1.5f, r.width - 2, 3f), Texture2D.whiteTexture); }
                if (broken)
                {
                    GUI.color = Color.white;
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(-35f, r.center);
                    GUI.DrawTexture(new Rect(r.center.x - 5f, r.center.y - 1f, 10f, 2f), Texture2D.whiteTexture);
                    GUI.matrix = m;
                }
                if (bleed > 0.15f)
                {
                    // the drop pulses faster and brighter the harder it bleeds
                    float rate = Mathf.Lerp(1.2f, 4.5f, Mathf.Clamp01(bleed / 15f));
                    float a = 0.55f + 0.45f * Mathf.Sin(pulse * rate * Mathf.PI);
                    GUI.color = new Color(1f, 0.1f, 0.08f, a);
                    float s = Mathf.Lerp(7f, 12f, Mathf.Clamp01(bleed / 15f));
                    GUI.DrawTexture(new Rect(r.xMax - s * 0.6f, r.y - s * 0.3f, s, s), drop, ScaleMode.StretchToFill);
                }
            }
            GUI.color = Color.white;
            var p = me.physio;
            float blood = p.blood / Physiology.NormalBlood;
            float bleedTotal = p.bleedOut + p.bleedIn;
            var col = blood < 0.7f || bleedTotal > 5f ? new Color(1f, 0.45f, 0.4f) : new Color(0.92f, 0.93f, 0.95f);
            GUI.Label(new Rect(box.x - 2, box.yMax + 4, 160, 16), $"BLOOD {blood * 100f:0}%", new GUIStyle(tiny) { normal = { textColor = col } });
            GUI.Label(new Rect(box.x - 2, box.yMax + 18, 200, 16), bleedTotal > 0.2f ? $"BLEEDING {bleedTotal:0.0} ML/S" : "H  DETAILS", new GUIStyle(tiny) { fontSize = 10, normal = { textColor = bleedTotal > 0.2f ? new Color(1f, 0.45f, 0.4f) : new Color(0.62f, 0.64f, 0.68f) } });
        }
    }
}
