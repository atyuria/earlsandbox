using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// What you see through your own helmet's visor when it is down. The Altyn's face shield is a slab of armoured
    /// glass in a thick titanium frame: the frame closes in round the edges of the view (heavy at the top and
    /// bottom, where it meets the helmet and the chin), the glass is tinted, smudged and scuffed, and every round
    /// it stops leaves a crack star in it. A knight's visor is steel with a slit to look out of. The frame swings
    /// up out of view as the visor is lifted (N).
    /// </summary>
    public class VisorView : MonoBehaviour
    {
        Texture2D altynFrame, altynGlass, knightPlate, crackTex, crazeTex;
        GUIStyle hint;

        /// <summary>How smashed the glass is: 0 clear .. 1 a white web you can't see through.</summary>
        public static float Frost;
        readonly List<Vector2> crackSpots = new List<Vector2>();
        readonly List<float> crackSize = new List<float>();
        int cracksSeen;
        ArmourPiece lastGlass;

        /// <summary>0 nothing in the way .. 1 the visor fully down (how much of the view it takes, for others to ask).</summary>
        public static float Down;

        static Texture2D Tex(int w, int h, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            t.SetPixels(px); t.Apply();
            return t;
        }

        static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }

        /// <summary>Signed distance (in uv units) to a rounded rectangle centred at c with half size e.</summary>
        static float RoundRect(Vector2 p, Vector2 c, Vector2 e, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - e + new Vector2(r, r);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        void Build()
        {
            // (uv: x 0..1 across, y 0..1 bottom to top; the window is drawn on a 2:1 sheet stretched to the screen)
            const int W = 1024, H = 512;
            Vector2 c = new Vector2(0.5f, 0.53f), e = new Vector2(0.41f, 0.31f);
            altynFrame = Tex(W, H, (u, v) =>
            {
                Vector2 p = new Vector2(u, v);
                // the window: wider at the eyes, the top edge dipping in the middle where the brow of the frame is
                float brow = 0.018f * Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.12f, 2f));
                float d = RoundRect(p, c, e, 0.11f) + (v > c.y ? brow : 0f);
                float inside = Mathf.Clamp01(-d / 0.006f);
                if (inside >= 1f) return new Color(0, 0, 0, 0);
                // the frame: dark olive titanium, lit a little along its inner bevel, scuffed
                float bevel = Mathf.Exp(-Mathf.Max(0f, d) / 0.012f);
                float grain = 0.85f + 0.15f * Hash(Mathf.Floor(u * 300f), Mathf.Floor(v * 150f));
                float shade = Mathf.Lerp(0.05f, 0.16f, bevel) * grain * Mathf.Lerp(0.6f, 1f, v);
                var col = new Color(shade * 0.92f, shade, shade * 0.72f, 1f);
                // the out-of-focus blur of something 3 cm from the eye: soft inner edge
                col.a = 1f - inside;
                return col;
            });
            altynGlass = Tex(W / 2, H / 2, (u, v) =>
            {
                // tint, a slightly darker ring at the edge of the glass, smudges and fine scratches
                Vector2 p = new Vector2(u, v);
                float d = RoundRect(p, c, e, 0.11f);
                float edge = Mathf.Clamp01(1f + d / 0.05f);
                float smudge = Mathf.PerlinNoise(u * 6f + 3.1f, v * 4f + 7.7f);
                smudge = Mathf.Clamp01((smudge - 0.55f) * 3f) * 0.12f;
                float scratch = 0f;
                for (int k = 0; k < 9; k++)
                {
                    float a = Hash(k, 1.3f) * Mathf.PI, ox = Hash(k, 2.7f), oy = Hash(k, 5.1f);
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 rel = p - new Vector2(ox, oy);
                    float along = Vector2.Dot(rel, dir), across = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
                    if (Mathf.Abs(along) < 0.025f + 0.03f * Hash(k, 9.2f) && across < 0.0008f) scratch = Mathf.Max(scratch, 0.12f);
                }
                float a2 = 0.13f + edge * 0.18f + smudge;
                return new Color(0.42f + scratch, 0.45f + scratch, 0.34f + scratch, Mathf.Clamp01(a2 + scratch * 0.5f));
            });
            knightPlate = Tex(W, H, (u, v) =>
            {
                // the eye slit across the middle, a little high; a cross bar of the frame in the middle of it
                float slit = Mathf.Abs(v - 0.56f) - 0.05f;
                float sideFade = Mathf.Abs(u - 0.5f) - 0.4f;
                float d = Mathf.Max(slit, sideFade * 0.25f);
                if (Mathf.Abs(u - 0.5f) < 0.012f) d = 1f;                       // the nose bar
                float inside = Mathf.Clamp01(-d / 0.008f);
                float bevel = Mathf.Exp(-Mathf.Max(0f, d) / 0.01f);
                float shade = Mathf.Lerp(0.04f, 0.2f, bevel) * (0.9f + 0.1f * Hash(Mathf.Floor(u * 200f), Mathf.Floor(v * 100f)));
                return new Color(shade, shade, shade * 1.05f, 1f - inside);
            });
            // the whole pane crazed: a fine web of cracks over milky, crushed glass
            crazeTex = Tex(512, 256, (u, v) =>
            {
                // cells of a broken pane: the crack lines are where two cells meet
                Vector2 p = new Vector2(u * 18f, v * 9f);
                float d1 = 9f, d2 = 9f;
                int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y);
                for (int j = -1; j <= 1; j++)
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 c = new Vector2(cx + i + Hash(cx + i, cy + j), cy + j + Hash(cy + j + 7.3f, cx + i + 1.9f));
                        float d = (p - c).magnitude;
                        if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                    }
                float line = Mathf.Clamp01(1f - (d2 - d1) / 0.06f);
                float haze = 0.55f + 0.25f * Mathf.PerlinNoise(u * 9f, v * 5f);
                float a = Mathf.Max(line * 0.95f, haze);
                return new Color(0.9f, 0.92f, 0.9f, a);
            });
            crackTex = Tex(256, 256, (u, v) =>
            {
                // a star of cracks from the impact, a crushed white spot in the middle and a ring or two
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
                float r = p.magnitude, ang = Mathf.Atan2(p.y, p.x);
                float a = 0f;
                for (int k = 0; k < 11; k++)
                {
                    float ka = Hash(k, 4.4f) * Mathf.PI * 2f, len = 0.25f + 0.25f * Hash(k, 8.8f);
                    float wig = ka + Mathf.Sin(r * 40f + k) * 0.05f;
                    float dAng = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, wig * Mathf.Rad2Deg)) * Mathf.Deg2Rad * r;
                    if (r < len && dAng < 0.004f + 0.002f * (1f - r / len)) a = Mathf.Max(a, 0.75f * (1f - r / len) + 0.2f);
                }
                foreach (float ring in new[] { 0.09f, 0.17f })
                    if (Mathf.Abs(r - ring - 0.01f * Mathf.Sin(ang * 7f)) < 0.003f) a = Mathf.Max(a, 0.45f);
                a = Mathf.Max(a, Mathf.Clamp01(1f - r / 0.035f) * 0.85f);
                return new Color(0.92f, 0.94f, 0.9f, a);
            });
        }

        void LateUpdate()
        {
            // a visor down over your face covers your ears: everything you hear is duller and quieter
            var me = PlayerAvatar.Body;
            var vis = me != null && !me.Dead ? ArmourSystem.VisorOf(me, out _) : null;
            Sfx.EarCover = vis != null ? 1f - vis.VisorOpen : 0f;
        }

        void OnGUI()
        {
            Down = 0f; Frost = 0f;
            var me = PlayerAvatar.Body;
            var game = SandboxGame.Instance;
            if (me == null || me.Dead || game == null || game.player == null || game.player.flying) return;
            var vis = ArmourSystem.VisorOf(me, out var glass);
            if (vis == null) return;
            float open = vis.VisorOpen;
            if (open >= 0.98f) return;
            if (altynFrame == null) Build();
            bool knight = vis.label == "VISOR";
            Down = 1f - open;
            GUI.depth = 20;   // under the HUD and the menus
            float W = Screen.width, H = Screen.height;
            // the visor is hinged at the top: lifting it swings it up out of view
            float lift = Mathf.SmoothStep(0f, 1f, open) * H * 1.15f;
            var r = new Rect(-W * 0.02f, -lift - H * 0.02f, W * 1.04f, H * 1.04f);
            if (knight)
            {
                GUI.DrawTexture(r, knightPlate, ScaleMode.StretchToFill);
                return;
            }
            GUI.DrawTexture(r, altynGlass, ScaleMode.StretchToFill);
            // cracks where rounds have hit the glass (the newest stay where they are)
            if (glass != lastGlass) { lastGlass = glass; crackSpots.Clear(); crackSize.Clear(); cracksSeen = 0; }
            int hits = glass != null ? glass.breaks.Count + glass.dents.Count : 0;
            while (cracksSeen < hits)
            {
                int k = cracksSeen++;
                crackSpots.Add(new Vector2(0.28f + 0.44f * Hash(k, 0.37f), 0.3f + 0.4f * Hash(k, 1.91f)));
                crackSize.Add(k < (glass != null ? glass.breaks.Count : 0) ? 0.55f : 0.32f);
            }
            for (int k = 0; k < crackSpots.Count; k++)
            {
                float s = crackSize[k] * H;
                GUI.DrawTexture(new Rect(r.x + crackSpots[k].x * r.width - s / 2f, r.y + (1f - crackSpots[k].y) * r.height - s / 2f, s, s), crackTex, ScaleMode.StretchToFill);
            }
            // every hit crazes the pane further: after a few it is a white web you can't see through
            int dmg = glass != null ? glass.dents.Count + glass.breaks.Count * 2 : 0;
            Frost = Mathf.Clamp01((dmg - 1) / 6f);
            if (Frost > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Frost) * (1f - open));
                GUI.DrawTexture(r, crazeTex, ScaleMode.StretchToFill);
                GUI.color = Color.white;
            }
            GUI.DrawTexture(r, altynFrame, ScaleMode.StretchToFill);
            if (Frost > 0.6f && open < 0.05f)
            {
                if (hint == null) hint = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.6f) } };
                GUI.Label(new Rect(0, H * 0.86f, W, 24), "VISOR GLASS SMASHED  ·  N  LIFT IT  ·  SHIFT+N  TEAR IT OFF", hint);
            }
        }
    }
}
