using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    public enum MenuKind { None, Settings, Spawn, Gun }

    /// <summary>
    /// Esc settings, F spawn menu (with a ghost outline where the Earl will appear and a turning showcase of the
    /// loadout) and G gun menu (showcase of the weapon as it is set up). While a menu is open the mouse is free;
    /// in the spawn menu hold right mouse to look around.
    /// </summary>
    public class Menus : MonoBehaviour
    {
        public MenuKind open;
        public bool IsOpen => open != MenuKind.None;
        public static int LastMenuFrame;
        /// <summary>True while a menu is open or was just closed (the closing click must not fire the gun).</summary>
        public static bool Blocking => Time.frameCount - LastMenuFrame < 3;
        SandboxGame game;

        // spawn options
        public readonly ArmourLoadout spawnArmour = new ArmourLoadout();
        public bool spawnAI = true;
        public int spawnCount = 1;
        GameObject ghost;
        Vector3 ghostPos; float ghostYaw; bool ghostValid;
        Vector2 scroll;
        Rect panel, panel2;
        int gunTab;
        float baseSens = -1f;
        PreviewStage stage;
        string shownLoadout = "", shownGun = "";

        public void Init(SandboxGame g) { game = g; GameSettings.Load(); }

        public void Toggle(MenuKind k)
        {
            open = open == k ? MenuKind.None : k;
            LastMenuFrame = Time.frameCount;
            if (k == MenuKind.Gun) gunTab = game.weapons.current;
            ApplyCursor();
            if (open != MenuKind.Spawn && ghost) ghost.SetActive(false);
            if (open == MenuKind.None) GameSettings.Save();
            shownLoadout = shownGun = "";
            if (stage != null) stage.SetActive(open == MenuKind.Spawn || open == MenuKind.Gun);
        }

        void ApplyCursor()
        {
            bool free = IsOpen && !(open == MenuKind.Spawn && Mouse.current != null && Mouse.current.rightButton.isPressed);
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        void Update()
        {
            if (IsOpen) LastMenuFrame = Time.frameCount;
            if (game == null || game.player == null) return;
            if (baseSens < 0f) baseSens = game.player.sensitivity;
            game.player.sensitivity = baseSens * GameSettings.MouseSensitivity;
            if (!IsOpen) return;
            var mouse = Mouse.current;
            if (open == MenuKind.Spawn || open == MenuKind.Gun)
            {
                if (stage == null) stage = new PreviewStage(game);
                stage.SetActive(true);
                if (open == MenuKind.Spawn)
                {
                    string key = JsonUtility.ToJson(spawnArmour);
                    if (key != shownLoadout) { shownLoadout = key; stage.ShowEarl(spawnArmour); }
                }
                else
                {
                    var L = game.weapons.loadouts[gunTab];
                    string key = gunTab + "|" + L.sight + "|" + L.grip;
                    if (key != shownGun) { shownGun = key; stage.ShowGun(game.weapons, gunTab); }
                }
                stage.Spin(Time.unscaledDeltaTime);
            }
            if (open == MenuKind.Spawn)
            {
                ApplyCursor();
                UpdateGhost(mouse);
                Vector2 mp = mouse.position.ReadValue();
                Vector2 gp = new Vector2(mp.x, Screen.height - mp.y);
                if (mouse.leftButton.wasPressedThisFrame && !panel.Contains(gp) && !panel2.Contains(gp) && ghostValid)
                {
                    for (int i = 0; i < spawnCount; i++)
                    {
                        Vector3 off = i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 72f, 0f) * Vector3.forward * 0.9f;
                        var h = game.SpawnCarl(ghostPos + off + Vector3.up * 0.02f, ghostYaw);
                        if (h == null) break;
                        ArmourSystem.Equip(h, spawnArmour.Clone());
                        h.brain.aiEnabled = spawnAI;
                    }
                    game.Toast(spawnCount > 1 ? $"{spawnCount} EARLS SPAWNED" : "EARL SPAWNED");
                }
            }
        }

        void UpdateGhost(Mouse mouse)
        {
            if (ghost == null) BuildGhost();
            var cam = game.cam;
            Ray r = Cursor.lockState == CursorLockMode.Locked ? new Ray(cam.transform.position, cam.transform.forward) : cam.ScreenPointToRay(mouse.position.ReadValue());
            ghostValid = false;
            float best = 250f; RaycastHit hit = default;
            foreach (var h in Physics.RaycastAll(r, 250f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is CharacterController || h.collider.GetComponentInParent<BodyPart>() != null) continue;
                if (h.collider.gameObject.layer == PreviewStage.Layer) continue;
                if (h.distance < best) { best = h.distance; hit = h; ghostValid = true; }
            }
            if (!ghostValid) { ghost.SetActive(false); return; }
            Vector3 p = hit.normal.y > 0.6f ? hit.point : hit.point + hit.normal * 0.6f;
            ghostPos = SandboxGame.ClearSpot(p);
            Vector3 face = cam.transform.position - ghostPos; face.y = 0f;
            ghostYaw = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face).eulerAngles.y : 0f;
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(ghostPos, Quaternion.Euler(0f, ghostYaw, 0f));
        }

        void BuildGhost()
        {
            ghost = new GameObject("SpawnGhost");
            var mat = Mats.Unlit(new Color(0.35f, 0.85f, 1f, 0.28f), false);
            void Piece(PrimitiveType t, Vector3 pos, Vector3 scale, float rotZ = 0f)
            {
                var g = GameObject.CreatePrimitive(t);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(ghost.transform, false);
                g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
                var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Piece(PrimitiveType.Sphere, new Vector3(0, 1.67f, 0.02f), new Vector3(0.19f, 0.24f, 0.21f));
            Piece(PrimitiveType.Capsule, new Vector3(0, 1.3f, 0), new Vector3(0.36f, 0.32f, 0.24f));
            Piece(PrimitiveType.Capsule, new Vector3(0, 1.0f, 0), new Vector3(0.32f, 0.2f, 0.22f));
            for (int s = -1; s <= 1; s += 2)
            {
                Piece(PrimitiveType.Capsule, new Vector3(s * 0.1f, 0.48f, 0), new Vector3(0.14f, 0.46f, 0.14f));
                Piece(PrimitiveType.Capsule, new Vector3(s * 0.25f, 1.15f, 0), new Vector3(0.1f, 0.34f, 0.1f), s * 8f);
            }
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(ghost.transform, false);
            ring.transform.localScale = new Vector3(0.7f, 0.004f, 0.7f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = Mats.Unlit(new Color(0.35f, 0.85f, 1f, 0.35f), true);
        }

        // ============================================================ look

        static readonly Color Accent = new Color(0.96f, 0.56f, 0.16f);
        static readonly Color Ink = new Color(0.9f, 0.91f, 0.93f), Dim = new Color(0.58f, 0.6f, 0.65f);
        GUIStyle card, cardDark, title, sub, section, small, chip, chipOn, big, bigOn, statLabel, sliderBg, thumb;
        Texture2D white, accentTex;

        /// <summary>A rounded rectangle texture for 9-slicing.</summary>
        public static Texture2D Rounded(Color fill, Color edge, int r = 10)
        {
            int n = r * 2 + 4;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x, x - (n - 1 - r)) - 0.5f), dy = Mathf.Max(0f, Mathf.Max(r - y, y - (n - 1 - r)) - 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    float e = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 1f)));       // 1px outline
                    var c = Color.Lerp(fill, edge, e * edge.a);
                    c.a = fill.a * a + (1f - fill.a) * e * edge.a * a;
                    px[y * n + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        void Styles()
        {
            if (card != null) return;
            white = Texture2D.whiteTexture;
            accentTex = new Texture2D(1, 1); accentTex.SetPixel(0, 0, Accent); accentTex.Apply();
            var cardT = Rounded(new Color(0.07f, 0.08f, 0.095f, 0.94f), new Color(1f, 1f, 1f, 0.08f), 12);
            var darkT = Rounded(new Color(0.035f, 0.04f, 0.05f, 0.96f), new Color(1f, 1f, 1f, 0.06f), 10);
            var chipT = Rounded(new Color(0.16f, 0.17f, 0.19f, 1f), new Color(1f, 1f, 1f, 0.05f), 7);
            var chipH = Rounded(new Color(0.22f, 0.23f, 0.26f, 1f), new Color(1f, 1f, 1f, 0.12f), 7);
            var chipOnT = Rounded(Accent, new Color(1f, 0.85f, 0.6f, 0.6f), 7);
            var bar = Rounded(new Color(0.18f, 0.19f, 0.21f, 1f), new Color(0, 0, 0, 0), 4);
            var knob = Rounded(Accent, new Color(1f, 1f, 1f, 0.5f), 7);
            RectOffset B(int b) => new RectOffset(b, b, b, b);
            card = new GUIStyle { normal = { background = cardT }, border = B(12), padding = new RectOffset(18, 18, 16, 16) };
            cardDark = new GUIStyle { normal = { background = darkT }, border = B(10), padding = B(10) };
            title = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, margin = new RectOffset(0, 0, 0, 2) };
            sub = new GUIStyle { fontSize = 11, normal = { textColor = Dim }, wordWrap = true, margin = new RectOffset(0, 0, 0, 10) };
            section = new GUIStyle { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = Accent }, margin = new RectOffset(0, 0, 12, 5) };
            small = new GUIStyle { fontSize = 11, wordWrap = true, normal = { textColor = Dim }, margin = new RectOffset(2, 2, 2, 4) };
            chip = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { background = chipT, textColor = Ink }, hover = { background = chipH, textColor = Color.white }, active = { background = chipOnT, textColor = Color.white }, border = B(7), padding = new RectOffset(8, 8, 6, 6), margin = new RectOffset(3, 3, 3, 3) };
            chipOn = new GUIStyle(chip) { normal = { background = chipOnT, textColor = new Color(0.08f, 0.06f, 0.04f) }, hover = { background = chipOnT, textColor = new Color(0.08f, 0.06f, 0.04f) } };
            big = new GUIStyle(chip) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 10, 8, 8) };
            bigOn = new GUIStyle(chipOn) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 10, 8, 8) };
            statLabel = new GUIStyle { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = Ink } };
            sliderBg = new GUIStyle { normal = { background = bar }, border = B(4), fixedHeight = 8, margin = new RectOffset(4, 4, 8, 8) };
            thumb = new GUIStyle { normal = { background = knob }, hover = { background = knob }, active = { background = knob }, border = B(7), fixedWidth = 16, fixedHeight = 16, margin = new RectOffset(0, 0, -4, 0) };
        }

        bool Chip(string text, bool on, float w = 0f, float h = 30f)
        {
            var opts = w > 0f ? new[] { GUILayout.Width(w), GUILayout.Height(h) } : new[] { GUILayout.Height(h) };
            return GUILayout.Button(text, on ? chipOn : chip, opts);
        }

        bool Check(string text, ref bool v)
        {
            if (Chip((v ? "■  " : "□  ") + text, v)) { v = !v; return true; }
            return false;
        }

        void Section(string name)
        {
            GUILayout.Label(name, section);
        }

        float Slider(string name, string val, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, statLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(val, new GUIStyle(statLabel) { normal = { textColor = Accent } });
            GUILayout.EndHorizontal();
            return GUILayout.HorizontalSlider(v, min, max, sliderBg, thumb);
        }

        void Stat(string name, float v01, string val)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(20));
            GUILayout.Label(name, statLabel, GUILayout.Width(100), GUILayout.Height(20));
            var r = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true));
            r.y += 6; r.height = 7;
            GUI.color = new Color(1f, 1f, 1f, 0.08f); GUI.DrawTexture(r, white);
            GUI.color = Accent; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v01), r.height), white);
            GUI.color = Color.white;
            GUILayout.Label(val, small, GUILayout.Width(80), GUILayout.Height(20));
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        /// <summary>The showcase picture with a caption under it.</summary>
        void Showcase(Rect r, string caption, string detail)
        {
            GUI.Box(r, GUIContent.none, card);
            var inner = new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 70);
            if (stage != null && stage.Texture != null) { stage.Fit(inner.width / Mathf.Max(1f, inner.height)); GUI.DrawTexture(inner, stage.Texture, ScaleMode.StretchToFill); }
            GUI.Label(new Rect(r.x + 18, r.yMax - 56, r.width - 36, 24), caption, new GUIStyle(title) { fontSize = 16 });
            GUI.Label(new Rect(r.x + 18, r.yMax - 32, r.width - 36, 22), detail, small);
        }

        public void Draw()
        {
            if (!IsOpen) return;
            Styles();
            // dim the world behind a menu a little
            GUI.color = new Color(0f, 0f, 0f, open == MenuKind.Spawn ? 0.12f : 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = Color.white;
            switch (open)
            {
                case MenuKind.Settings: DrawSettings(); break;
                case MenuKind.Spawn: DrawSpawn(); break;
                case MenuKind.Gun: DrawGun(); break;
            }
        }

        void DrawSettings()
        {
            panel = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 300, 500, 600);
            panel2 = default;
            GUILayout.BeginArea(panel, card);
            GUILayout.Label("PAUSED", title);
            GUILayout.Label("Settings are saved when you close this.", sub);
            Section("BLOOD");
            GameSettings.BloodQuality = Slider("QUALITY", GameSettings.QualityName, GameSettings.BloodQuality, 0f, 1f);
            GUILayout.Label("Resolution of blood on skin, floors and walls. Changing it clears the blood on the map.", small);
            GameSettings.BloodAmount = Slider("AMOUNT", GameSettings.AmountName, GameSettings.BloodAmount, 0f, 1f);
            GUILayout.Label("None ... realistic (middle) ... unlimited: wounds never run dry.", small);
            Section("GAME");
            GameSettings.MouseSensitivity = Slider("MOUSE SENSITIVITY", GameSettings.MouseSensitivity.ToString("0.00"), GameSettings.MouseSensitivity, 0.2f, 3f);
            GameSettings.Volume = Slider("VOLUME", Mathf.RoundToInt(GameSettings.Volume * 100f) + "%", GameSettings.Volume, 0f, 1f);
            GUILayout.Space(4);
            Check("INFINITE AMMO", ref GameSettings.InfiniteAmmo);
            Section("MAP");
            GUILayout.BeginHorizontal();
            for (int m = 0; m < MapBuilder.Names.Length; m++) if (Chip(MapBuilder.Names[m], game.mapIndex == m)) game.LoadMap(m);
            GUILayout.EndHorizontal();
            if (Chip("CLEAR ALL EARLS AND BLOOD", false)) game.ClearAllPublic();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (Chip("RESUME", true, 0f, 38f)) Toggle(MenuKind.Settings);
            if (Chip("QUIT GAME", false, 140f, 38f)) Application.Quit();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static string LoadoutName(ArmourLoadout L)
        {
            var parts = new List<string>();
            if (L.vest == VestKind.Russian6B45) parts.Add($"6B45 · Br{L.plateClass}");
            else if (L.vest == VestKind.KnightCuirass) parts.Add("CUIRASS");
            if (L.helmet == HelmetKind.Spetsnaz) parts.Add("ALTYN");
            else if (L.helmet == HelmetKind.Ratnik) parts.Add("6B47");
            else if (L.helmet == HelmetKind.Knight) parts.Add("BASCINET");
            if (L.pauldrons || L.vambraces || L.gauntlets || L.cuisses || L.greaves || L.sabatons || L.tassets) parts.Add("PLATE LIMBS");
            return parts.Count == 0 ? "NO ARMOUR" : string.Join("  ·  ", parts);
        }

        void DrawSpawn()
        {
            panel = new Rect(16, 16, 380, Screen.height - 32);
            float pw = Mathf.Min(340f, Screen.width - panel.xMax - 32f);
            panel2 = new Rect(panel.xMax + 14, 16, pw, Mathf.Min(520f, Screen.height - 32));
            Showcase(panel2, "EARL", LoadoutName(spawnArmour) + (spawnAI ? "  ·  AI ON" : "  ·  DUMMY"));

            GUILayout.BeginArea(panel, card);
            GUILayout.Label("SPAWN", title);
            GUILayout.Label("Click in the world to drop them at the outline.  Hold RIGHT MOUSE to look.  F closes.", sub);
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            var L = spawnArmour;

            Section("BRAIN");
            GUILayout.BeginHorizontal();
            if (Chip("AI ON", spawnAI)) spawnAI = true;
            if (Chip("DUMMY", !spawnAI)) spawnAI = false;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (int n in new[] { 1, 3, 5 }) if (Chip($"x{n}", spawnCount == n)) spawnCount = n;
            GUILayout.EndHorizontal();

            Section("BODY ARMOUR");
            GUILayout.BeginHorizontal();
            if (Chip("NONE", L.vest == VestKind.None)) L.vest = VestKind.None;
            if (Chip("6B45", L.vest == VestKind.Russian6B45)) L.vest = VestKind.Russian6B45;
            if (Chip("CUIRASS", L.vest == VestKind.KnightCuirass)) L.vest = VestKind.KnightCuirass;
            GUILayout.EndHorizontal();
            if (L.vest == VestKind.Russian6B45)
            {
                GUILayout.Label("PLATE CLASS (GOST)", small);
                GUILayout.BeginHorizontal();
                for (int c = 2; c <= 6; c++) if (Chip($"Br{c}", L.plateClass == c)) L.plateClass = c;
                GUILayout.EndHorizontal();
                GUILayout.Label(L.plateClass <= 2 ? "Stops pistol rounds." : L.plateClass == 3 ? "Stops pistols, some buckshot." : L.plateClass == 4 ? "Stops 7.62x39 PS. AP and magnums go through." : L.plateClass == 5 ? "Stops rifle AP. .338 and .50 go through." : "Stops almost everything once. Wears out.", small);
                GUILayout.BeginHorizontal(); Check("SIDES", ref L.sideArmour); Check("GROIN FLAP", ref L.groinFlap); GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(); Check("COLLAR", ref L.collar); Check("POUCHES", ref L.pouches); GUILayout.EndHorizontal();
            }

            Section("HELMET");
            GUILayout.BeginHorizontal();
            if (Chip("NONE", L.helmet == HelmetKind.None)) L.helmet = HelmetKind.None;
            if (Chip("6B47", L.helmet == HelmetKind.Ratnik)) L.helmet = HelmetKind.Ratnik;
            if (Chip("ALTYN", L.helmet == HelmetKind.Spetsnaz)) L.helmet = HelmetKind.Spetsnaz;
            if (Chip("KNIGHT", L.helmet == HelmetKind.Knight)) L.helmet = HelmetKind.Knight;
            GUILayout.EndHorizontal();
            if (L.helmet == HelmetKind.Spetsnaz || L.helmet == HelmetKind.Knight)
            {
                GUILayout.BeginHorizontal(); Check("VISOR", ref L.visor); if (L.visor) Check("VISOR DOWN", ref L.visorDown); GUILayout.EndHorizontal();
                if (L.visor) GUILayout.Label("Look at a visor and press E to flip it up or down.", small);
                if (L.helmet == HelmetKind.Knight) { GUILayout.BeginHorizontal(); Check("AVENTAIL", ref L.aventail); Check("PLUME", ref L.plume); GUILayout.EndHorizontal(); }
            }

            Section("PLATE LIMBS");
            GUILayout.BeginHorizontal(); Check("PAULDRONS", ref L.pauldrons); Check("VAMBRACES", ref L.vambraces); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); Check("GAUNTLETS", ref L.gauntlets); Check("TASSETS", ref L.tassets); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); Check("CUISSES", ref L.cuisses); Check("GREAVES", ref L.greaves); GUILayout.EndHorizontal();
            Check("SABATONS", ref L.sabatons);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (Chip("FULL KNIGHT", false)) { L.vest = VestKind.KnightCuirass; L.helmet = HelmetKind.Knight; L.pauldrons = L.vambraces = L.gauntlets = L.tassets = L.cuisses = L.greaves = L.sabatons = true; }
            if (Chip("OPERATOR", false)) { L.vest = VestKind.Russian6B45; L.plateClass = 4; L.helmet = HelmetKind.Ratnik; L.sideArmour = L.groinFlap = L.collar = L.pouches = true; }
            if (Chip("STRIP", false)) { L.vest = VestKind.None; L.helmet = HelmetKind.None; L.pauldrons = L.vambraces = L.gauntlets = L.tassets = L.cuisses = L.greaves = L.sabatons = false; }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string SightName(SightKind s) => s == SightKind.RedDot ? "RED DOT" : s == SightKind.Holo ? "HOLO" : s == SightKind.Scope4x ? "4x SCOPE" : s == SightKind.Scope8x ? "8x SCOPE" : "IRONS";

        void DrawGun()
        {
            var w = game.weapons;
            float W = Mathf.Min(980f, Screen.width - 40f), H = Mathf.Min(620f, Screen.height - 40f);
            panel = new Rect((Screen.width - W) / 2f, (Screen.height - H) / 2f, W, H);
            panel2 = default;
            GUI.Box(panel, GUIContent.none, card);

            // weapon list down the left
            var listR = new Rect(panel.x + 16, panel.y + 16, 200, panel.height - 32);
            GUILayout.BeginArea(listR);
            GUILayout.Label("ARSENAL", title);
            GUILayout.Label("G closes", sub);
            for (int i = 0; i < w.defs.Count; i++)
                if (GUILayout.Button((i + 1) + "   " + w.defs[i].name, gunTab == i ? bigOn : big, GUILayout.Height(40))) { gunTab = i; scroll = Vector2.zero; }
            GUILayout.EndArea();

            var d = w.defs[gunTab];
            var L = w.loadouts[gunTab];
            // showcase
            var showR = new Rect(listR.xMax + 14, panel.y + 16, panel.xMax - listR.xMax - 30, Mathf.Min(260f, panel.height * 0.42f));
            string detail = d.type == WeaponType.Gun ? $"{SightName(L.sight)}  ·  {(L.grip == GripKind.None ? "NO GRIP" : L.grip == GripKind.Vertical ? "VERTICAL GRIP" : "ANGLED GRIP")}  ·  {d.magazine} RDS" : d.type == WeaponType.Knife ? "STAB · SLASH" : $"{(L.fuse == 0 ? "IMPACT" : (L.fuse + 1) + " SEC")} FUSE  ·  {L.shrapnel} FRAGMENTS";
            Showcase(showR, d.name, detail);

            var optR = new Rect(showR.x, showR.yMax + 10, showR.width, panel.yMax - showR.yMax - 26);
            GUILayout.BeginArea(optR);
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (d.type == WeaponType.Gun)
            {
                var am = w.loadouts[gunTab].ammo >= 0 && AmmoTable.For(d.name) != null ? AmmoTable.For(d.name)[Mathf.Clamp(L.ammo, 0, AmmoTable.For(d.name).Length - 1)] : null;
                float energy = am != null ? am.Energy * am.pellets : d.Energy;
                Stat("DAMAGE", Mathf.Sqrt(energy / 18000f), $"{energy:0} J");
                Stat("PENETRATION", (am != null ? am.pen : 3f) / 9f, $"{(am != null ? am.pen : 3f):0.#}");
                Stat("RECOIL", d.recoil / 8f, $"{d.recoil:0.#}");
                Stat("FIRE RATE", d.rpm / 700f, $"{d.rpm:0} RPM");
            }
            var ammo = AmmoTable.For(d.name);
            if (ammo != null)
            {
                Section("AMMUNITION");
                for (int i = 0; i < ammo.Length; i++)
                {
                    var a = ammo[i];
                    if (GUILayout.Button($"{a.name}      PEN {a.pen:0.#}  ·  {a.Energy:0} J{(a.pellets > 1 ? $"  ·  {a.pellets} PELLETS" : "")}", L.ammo == i ? bigOn : big, GUILayout.Height(32))) L.ammo = i;
                    GUILayout.Label(a.desc, small);
                }
            }
            var sights = WeaponController.SightsFor(d);
            if (sights.Length > 0)
            {
                Section("OPTIC");
                GUILayout.BeginHorizontal();
                foreach (var s in sights) if (Chip(SightName(s), L.sight == s)) { L.sight = s; w.RefreshAttachments(); }
                GUILayout.EndHorizontal();
            }
            if (WeaponController.GripsFor(d))
            {
                Section("FOREGRIP");
                GUILayout.BeginHorizontal();
                if (Chip("NONE", L.grip == GripKind.None)) { L.grip = GripKind.None; w.RefreshAttachments(); }
                if (Chip("VERTICAL  -30% RECOIL", L.grip == GripKind.Vertical)) { L.grip = GripKind.Vertical; w.RefreshAttachments(); }
                if (Chip("ANGLED  FASTER AIM", L.grip == GripKind.Angled)) { L.grip = GripKind.Angled; w.RefreshAttachments(); }
                GUILayout.EndHorizontal();
            }
            if (d.type == WeaponType.Grenade)
            {
                Section("FUSE");
                GUILayout.BeginHorizontal();
                for (int f = 0; f <= 4; f++) if (Chip(f == 0 ? "IMPACT" : $"{f + 1} SEC", L.fuse == f)) L.fuse = f;
                GUILayout.EndHorizontal();
                L.shrapnel = Mathf.RoundToInt(Slider("SHRAPNEL", $"{L.shrapnel} fragments", L.shrapnel, 80f, 700f) / 10f) * 10;
                GUILayout.Label("More fragments: deadlier, but a heavier grenade you can't throw as far.", small);
            }
            if (d.type == WeaponType.Knife) GUILayout.Label("LEFT CLICK stab  ·  RIGHT CLICK slash.  Blades skid off plate armour.  Hack at the same spot to cut deeper.", small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }

    /// <summary>
    /// A little photo studio far under the map: a turning Earl in the chosen kit, or the chosen gun, lit and
    /// filmed into a texture for the menus.
    /// </summary>
    public class PreviewStage
    {
        public const int Layer = 30;
        public RenderTexture Texture;
        readonly SandboxGame game;
        readonly GameObject root;
        readonly Transform turntable;
        readonly Camera cam;
        HumanBody earl;
        GameObject gun;
        float angle;
        static readonly Vector3 At = new Vector3(0f, -600f, 0f);

        public PreviewStage(SandboxGame g)
        {
            game = g;
            root = new GameObject("PreviewStage");
            root.transform.position = At;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(root.transform, false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Plinth";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.12f, 0.13f, 0.15f), 0.4f);
            Texture = new RenderTexture(640, 760, 24) { antiAliasing = 4 };
            cam = new GameObject("PreviewCam").AddComponent<Camera>();
            cam.transform.SetParent(root.transform, false);
            cam.targetTexture = Texture;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.11f, 0.13f, 1f);
            cam.fieldOfView = 26f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 30f;
            foreach (var (pos, col, inten) in new[] { (new Vector3(1.5f, 2.6f, 2f), new Color(1f, 0.95f, 0.88f), 6f), (new Vector3(-2f, 1.5f, 1f), new Color(0.6f, 0.75f, 1f), 3f), (new Vector3(0f, 2.2f, -2.2f), new Color(1f, 0.7f, 0.45f), 4f) })
            {
                var l = new GameObject("Light").AddComponent<Light>();
                l.transform.SetParent(root.transform, false);
                l.transform.localPosition = pos;
                l.type = LightType.Point; l.range = 8f; l.intensity = inten; l.color = col;
                l.cullingMask = 1 << Layer; l.shadows = LightShadows.None;
            }
            SetLayer(root, Layer);
            // the game camera must not see the studio
            game.cam.cullingMask &= ~(1 << Layer);
            root.SetActive(false);
        }

        static void SetLayer(GameObject g, int l) { foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = l; }

        public void SetActive(bool on) { if (root) root.SetActive(on); }

        /// <summary>Match the render to the shape of the box it is shown in.</summary>
        public void Fit(float aspect)
        {
            int h = 600, w = Mathf.Clamp(Mathf.RoundToInt(h * aspect), 200, 1600);
            if (Texture != null && Texture.width == w) return;
            if (Texture != null) Texture.Release();
            Texture = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = Texture;
            cam.aspect = w / (float)h;
        }

        void Clear()
        {
            if (gun) Object.Destroy(gun);
            gun = null;
        }

        public void ShowEarl(ArmourLoadout L)
        {
            Clear();
            if (earl == null)
            {
                earl = HumanBody.Spawn(At + Vector3.up * 0.02f, 180f);
                if (earl == null) return;
                HumanBody.All.Remove(earl);                         // not a real Earl: doesn't count, isn't simulated
                foreach (var bp in earl.partList) { bp.rb.isKinematic = true; bp.rb.detectCollisions = false; }
                if (earl.brain) earl.brain.enabled = false;
                if (earl.ragdoll) earl.ragdoll.enabled = false;
                earl.transform.SetParent(turntable, true);
                foreach (var bp in earl.partList) bp.transform.SetParent(turntable, true);
            }
            // take the old kit off and put the new one on
            foreach (var bp in earl.partList)
            {
                var kill = new List<GameObject>();
                foreach (Transform t in bp.transform) if (t.GetComponent<ArmourPiece>() != null || t.name == "Trim" || t.name == "Plume") kill.Add(t.gameObject);
                foreach (var k in kill) { var ap = k.GetComponent<ArmourPiece>(); if (ap) ArmourSystem.All.Remove(ap); Object.DestroyImmediate(k); }
            }
            ArmourSystem.Equip(earl, L.Clone());
            foreach (var bp in earl.partList) foreach (var ap in bp.GetComponentsInChildren<ArmourPiece>()) ArmourSystem.All.Remove(ap);
            foreach (var bp in earl.partList) SetLayer(bp.gameObject, Layer);
            foreach (var bp in earl.partList) bp.gameObject.SetActive(true);
            cam.transform.position = At + new Vector3(0f, 1.1f, 3.6f);
            cam.transform.LookAt(At + Vector3.up * 0.98f);
            cam.fieldOfView = 26f;
        }

        public void ShowGun(WeaponController w, int i)
        {
            Clear();
            if (earl != null) foreach (var bp in earl.partList) bp.gameObject.SetActive(false);
            gun = w.PreviewModel(i);
            if (gun == null) return;
            gun.transform.SetParent(turntable, false);
            gun.transform.localScale = Vector3.one;
            gun.transform.localRotation = Quaternion.identity;
            var b = new Bounds(gun.transform.position, Vector3.zero);
            foreach (var r in gun.GetComponentsInChildren<MeshRenderer>()) if (r.gameObject.activeInHierarchy) b.Encapsulate(r.bounds);
            gun.transform.position += (At + Vector3.up * 0.9f) - b.center;
            SetLayer(gun, Layer);
            float size = Mathf.Max(0.25f, b.size.magnitude);
            cam.fieldOfView = 22f;
            cam.transform.position = At + Vector3.up * 0.9f + new Vector3(0.0f, size * 0.45f, size * 2.6f);
            cam.transform.LookAt(At + Vector3.up * 0.9f);
        }

        public void Spin(float dt)
        {
            angle += dt * 28f;
            turntable.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (gun != null)
            {
                // the gun turns about its own middle
                gun.transform.RotateAround(At + Vector3.up * 0.9f, Vector3.up, 0f);
            }
        }
    }
}
