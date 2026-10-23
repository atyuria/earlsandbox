using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    /// <summary>
    /// Treating yourself. Hold a BANDAGE or the SURGERY KIT and left click: a list of your wounds comes up (worst
    /// first) and you pick the one to treat. A bandage is quick and slows the bleeding a lot, but a deep hole or an
    /// artery keeps seeping through it. The surgery kit takes much longer and only does deep wounds (gunshot holes,
    /// stab wounds, cuts, stumps), but sews them shut. Neither reaches bleeding inside the body. Treatment stops if
    /// you pass out or let go of it.
    /// </summary>
    public class MedKit : MonoBehaviour
    {
        public static MedKit I;
        public static bool Busy => I != null && (I.menuOpen || I.treating != null);
        bool menuOpen;
        Wound treating;
        bool surgery;
        float t, dur;
        Vector2 scroll;
        GUIStyle card, title, row, rowOff, small, bar;

        public static MedKit Attach(GameObject g) { I = g.AddComponent<MedKit>(); return I; }

        static WeaponController W => SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;
        static bool Holding(out bool kit)
        {
            kit = false;
            var w = W;
            if (w == null || !w.Armed || w.Current.type != WeaponType.Medical || WeaponController.IsRadio(w.Current)) return false;
            kit = w.Current.name == "SURGERY KIT";
            return true;
        }

        /// <summary>Deep enough to need stitches.</summary>
        public static bool Deep(Wound w) => w.type == WoundType.Entry || w.type == WoundType.Exit || w.type == WoundType.Stab || w.type == WoundType.Laceration || w.type == WoundType.Amputation || w.type == WoundType.Blast;

        public static bool CanTreat(Wound w, bool kit) =>
            w != null && w.type != WoundType.Internal && w.type != WoundType.Bruise && !w.sutured && (kit ? Deep(w) : !w.bandaged);

        static List<Wound> Wounds(bool kit)
        {
            var l = new List<Wound>();
            var me = PlayerAvatar.Body;
            if (me == null) return l;
            foreach (var bp in me.partList)
                if (bp != null) foreach (var w in bp.wounds) if (w.type != WoundType.Internal && w.type != WoundType.Bruise) l.Add(w);
            l.Sort((a, b) => (b.extRate + (b.arterial ? 50f : 0f)).CompareTo(a.extRate + (a.arterial ? 50f : 0f)));
            return l;
        }

        void Update()
        {
            var me = PlayerAvatar.Body;
            bool held = Holding(out bool kit);
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (me == null || me.Dead || PlayerBody.I != null && PlayerBody.I.passedOut || !held)
            {
                if (menuOpen) Close();
                treating = null;
                return;
            }
            if (treating != null)
            {
                t += Time.deltaTime;
                // let go (weapon switched) or the wound is gone: stop
                if (kit != surgery || treating.part == null) { treating = null; SandboxGame.Instance?.Toast("TREATMENT STOPPED"); return; }
                if (t >= dur) Finish();
                return;
            }
            if (menuOpen)
            {
                if (kb != null && kb.escapeKey.wasPressedThisFrame || mouse != null && mouse.rightButton.wasPressedThisFrame) Close();
                Menus.LastMenuFrame = Time.frameCount;     // (the click on the list must not do anything else)
                return;
            }
            bool locked = Cursor.lockState == CursorLockMode.Locked && !Menus.Blocking;
            if (locked && mouse != null && mouse.leftButton.wasPressedThisFrame) Open();
        }

        void Open()
        {
            menuOpen = true; scroll = Vector2.zero;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Menus.LastMenuFrame = Time.frameCount;
        }

        void Close()
        {
            menuOpen = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Menus.LastMenuFrame = Time.frameCount;
        }

        /// <summary>Start treating this wound (from the list, or a playtest).</summary>
        public void Begin(Wound w, bool kit)
        {
            if (!CanTreat(w, kit)) return;
            treating = w; surgery = kit; t = 0f;
            // a bigger wound takes a little longer to pack or to sew
            dur = kit ? 12f + Mathf.Clamp(w.extRate, 0f, 20f) * 0.2f : 3f;
            if (menuOpen) Close();
            Sfx.Play(kit ? "knife" : "casing_dirt", Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.4f, 0f, 1f, 0.1f);
        }

        void Finish()
        {
            var w = treating; treating = null;
            if (w == null) return;
            Treat(PlayerAvatar.Body, w, surgery);
            SandboxGame.Instance?.Toast((surgery ? "SUTURED: " : "BANDAGED: ") + (w.part != null ? w.part.Name : ""));
        }

        /// <summary>A wound packed and wrapped, or sewn shut - by you, or by a medic (on you or on an Earl).</summary>
        public static void Treat(HumanBody patient, Wound w, bool surgery)
        {
            if (w == null) return;
            if (surgery)
            {
                // sewn shut: it doesn't bleed outside any more, and a sewn artery is tied off
                w.extRate = 0f;
                w.arterial = false;
                w.sutured = true;
                w.intRate *= 0.5f;
                w.desc = (w.desc ?? w.type.ToString().ToUpper()) + " (SUTURED)";
            }
            else
            {
                // packed and wrapped: most of the bleeding stopped, it clots on faster; deep holes and arteries
                // keep seeping through
                w.extRate *= w.arterial ? 0.45f : Deep(w) ? 0.3f : 0.1f;
                w.clotRate = Mathf.Max(w.clotRate, 1f / 25f);
                w.bandaged = true;
                w.desc = (w.desc ?? w.type.ToString().ToUpper()) + " (BANDAGED)";
            }
            patient?.physio.AddPain(surgery ? 0.15f : 0.04f, w.part != null ? w.part.id : PartId.Chest);
        }

        /// <summary>What a medic would do next for this body: the worst wound a bandage helps, then the worst to sew.</summary>
        public static Wound NextFor(HumanBody h, out bool surgery)
        {
            surgery = false;
            Wound best = null; float bs = 0.04f;
            foreach (var bp in h.partList)
                if (bp != null) foreach (var w in bp.wounds)
                    {
                        if (!CanTreat(w, false)) continue;
                        float s = w.extRate + (w.arterial ? 50f : 0f) + (Deep(w) ? 0.5f : 0f);
                        if (s > bs) { bs = s; best = w; }
                    }
            if (best != null) return best;
            foreach (var bp in h.partList)
                if (bp != null) foreach (var w in bp.wounds)
                        if (CanTreat(w, true) && (best == null || w.extRate > best.extRate)) best = w;
            surgery = best != null;
            return best;
        }

        // ------------------------------------------------------------ the list and the progress bar

        void Styles()
        {
            if (card != null) return;
            RectOffset B(int b) => new RectOffset(b, b, b, b);
            card = new GUIStyle { normal = { background = Menus.Rounded(new Color(0.07f, 0.08f, 0.095f, 0.95f), new Color(1f, 1f, 1f, 0.08f), 12) }, border = B(12), padding = new RectOffset(16, 16, 14, 14) };
            title = new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, margin = new RectOffset(0, 0, 0, 6) };
            small = new GUIStyle { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.62f, 0.64f, 0.68f) }, margin = new RectOffset(0, 0, 0, 8) };
            var chip = Menus.Rounded(new Color(0.16f, 0.17f, 0.19f, 1f), new Color(1f, 1f, 1f, 0.05f), 7);
            var hov = Menus.Rounded(new Color(0.96f, 0.56f, 0.16f, 1f), new Color(1f, 0.85f, 0.6f, 0.6f), 7);
            row = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleLeft, richText = true, normal = { background = chip, textColor = new Color(0.92f, 0.93f, 0.95f) }, hover = { background = hov, textColor = new Color(0.08f, 0.06f, 0.04f) }, border = B(7), padding = new RectOffset(12, 10, 6, 6), margin = new RectOffset(0, 0, 3, 3) };
            rowOff = new GUIStyle(row) { normal = { background = chip, textColor = new Color(0.45f, 0.46f, 0.5f) }, hover = { background = chip, textColor = new Color(0.45f, 0.46f, 0.5f) } };
            bar = new GUIStyle { normal = { background = Texture2D.whiteTexture } };
        }

        static string Describe(Wound w)
        {
            string what = string.IsNullOrEmpty(w.desc) ? w.type.ToString().ToUpper() : w.desc;
            return $"<b>{(w.part != null ? w.part.Name : "?")}</b>  ·  {what}  ·  {w.Severity()}{(w.extRate > 0.05f ? $" ({w.extRate:0.0} ML/S)" : "")}";
        }

        void OnGUI()
        {
            GUI.depth = -60;
            if (!Holding(out bool kit)) return;
            Styles();
            float W = Screen.width, H = Screen.height;
            if (treating != null)
            {
                // the progress bar in the middle of the screen
                var r = new Rect(W / 2 - 180, H * 0.62f, 360, 56);
                GUI.Box(r, GUIContent.none, card);
                GUI.Label(new Rect(r.x + 16, r.y + 8, 330, 20), (surgery ? "SUTURING  " : "BANDAGING  ") + (treating.part != null ? treating.part.Name : ""), new GUIStyle(small) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } });
                GUI.color = new Color(1f, 1f, 1f, 0.1f); GUI.DrawTexture(new Rect(r.x + 16, r.y + 34, 328, 8), Texture2D.whiteTexture);
                GUI.color = new Color(0.96f, 0.56f, 0.16f); GUI.DrawTexture(new Rect(r.x + 16, r.y + 34, 328 * Mathf.Clamp01(t / dur), 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }
            if (!menuOpen) return;
            var list = Wounds(kit);
            var pr = new Rect(W / 2 - 260, H / 2 - 220, 520, 440);
            GUILayout.BeginArea(pr, card);
            GUILayout.Label(kit ? "SURGERY KIT: WHICH WOUND?" : "BANDAGE: WHICH WOUND?", title);
            GUILayout.Label(kit ? "Sews a deep wound shut (12 s or more). Grazes and scrapes don't need it; bleeding inside can't be reached." : "Packs and wraps a wound (3 s). Slows bleeding a lot; deep holes and arteries keep seeping. RIGHT CLICK / ESC closes.", small);
            scroll = GUILayout.BeginScrollView(scroll);
            if (list.Count == 0) GUILayout.Label("No wounds to treat.", small);
            foreach (var w in list)
            {
                bool ok = CanTreat(w, kit);
                string tag = w.sutured ? "  ·  SUTURED" : w.bandaged ? "  ·  BANDAGED" : kit && !Deep(w) ? "  ·  NOT DEEP" : "";
                if (GUILayout.Button(Describe(w) + tag, ok ? row : rowOff, GUILayout.Height(34)) && ok) Begin(w, kit);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
