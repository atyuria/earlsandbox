using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    /// <summary>
    /// The gunsmith (G menu, a gun, GUNSMITH): the gun big in the middle of the screen, a point on each of its
    /// slots (barrel, dust cover, handguard, muzzle, grip, stock, magazine, optic, foregrip, light, laser); click
    /// one and what fits there comes up on the right with a picture of each part and what it does to the gun; its
    /// numbers along the bottom; presets saved and loaded on the left. RIGHT MOUSE drag turns the gun, the wheel
    /// zooms. G or ESC back to the gun menu.
    /// </summary>
    public partial class Menus
    {
        int smithGun = -1;
        string smithSel;
        bool presetsOpen;
        Vector2 smithScroll, slotScroll;
        float smithYaw, smithPitch, smithZoom = 1f, smithDragged;
        bool smithDragFrom;
        GunsmithStage smithStage;
        string smithShown = "", presetName = "";
        Texture2D dot, ring;
        GUIStyle tagStyle, optName, optSmall, optEffect;

        /// <summary>One of the gunsmith's numbers: its name, a bar, the figure.</summary>
        void SmithStat(string name, float v01, string val)
        {
            var r = GUILayoutUtility.GetRect(200, 26, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(r.x, r.y + 3, 170, 20), name, optName);
            var bar = new Rect(r.x + 172, r.y + 9, r.width - 172 - 150, 6);
            GUI.color = new Color(1f, 1f, 1f, 0.12f); GUI.DrawTexture(bar, white);
            GUI.color = new Color(0.96f, 0.56f, 0.16f); GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(v01), bar.height), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.xMax + 10, r.y + 4, 140, 20), val, optSmall);
        }

        /// <summary>Typing in a text box (a preset's name): the game's keys leave it alone.</summary>
        public bool Typing => open == MenuKind.Gunsmith && GUIUtility.keyboardControl != 0;

        /// <summary>Into the gunsmith with gun i.</summary>
        public void OpenGunsmith(int i)
        {
            smithGun = i; smithSel = null; presetsOpen = false; smithYaw = 0f; smithPitch = 0f; smithZoom = 1f; smithShown = "";
            presetName = game.weapons.defs[i].name + " BUILD";
            open = MenuKind.Gunsmith;
            LastMenuFrame = Time.frameCount;
            ApplyCursor();
            if (stage != null) stage.SetActive(false);
            if (smithStage == null) smithStage = new GunsmithStage(game);
            smithStage.SetActive(true);
        }

        /// <summary>Playtests: pick a slot (or the presets with "PRESETS").</summary>
        public void SmithSelect(string slot) { presetsOpen = slot == "PRESETS"; smithSel = presetsOpen ? null : slot; smithScroll = Vector2.zero; }
        /// <summary>Playtests: the controls page on one of its tabs.</summary>
        public void ShowControls(int tab) { if (open != MenuKind.Settings) Toggle(MenuKind.Settings); settingsTab = 1; controlsTab = tab; }

        /// <summary>Back out to the gun menu, on the same gun.</summary>
        void CloseGunsmith()
        {
            smithStage?.SetActive(false);
            open = MenuKind.Gun; gunTab = smithGun; gunPage = 0; armourPage = false;
            shownGun = shownLoadout = "";
            if (stage != null) stage.SetActive(true);
            ApplyCursor();
        }

        void UpdateGunsmith(Mouse mouse)
        {
            if (smithStage == null) return;
            var w = game.weapons; var L = w.loadouts[smithGun];
            string key = smithGun + "|" + L.sight + L.grip + L.extMag + L.autoSwitch + L.suppressor + L.light + L.laser + L.magnifier + "|" + L.build?.Key + "|" + L.optic + L.foregrip + L.lightId + L.laserId + L.lightSide + L.laserSide;
            if (key != smithShown) { smithShown = key; smithStage.Show(w.PreviewModel(smithGun)); }
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                bool overView = mp.x > 300f && mp.x < Screen.width - 420f && Screen.height - mp.y < Screen.height - 250f;
                if (mouse.leftButton.wasPressedThisFrame) { smithDragFrom = overView; smithDragged = 0f; }
                if (!mouse.leftButton.isPressed) smithDragFrom = false;
                Vector2 dlt = mouse.delta.ReadValue();
                bool turning = mouse.rightButton.isPressed;
                if (smithDragFrom && mouse.leftButton.isPressed) { smithDragged += dlt.magnitude; turning |= smithDragged > 4f; }
                if (turning) { smithYaw += dlt.x * 0.35f; smithPitch = Mathf.Clamp(smithPitch - dlt.y * 0.3f, -85f, 85f); }
                float sc = mouse.scroll.ReadValue().y;
                if (overView && Mathf.Abs(sc) > 0.01f) smithZoom = Mathf.Clamp(smithZoom * (sc > 0 ? 1.1f : 1f / 1.1f), 0.7f, 3.5f);
            }
            smithStage.View(smithYaw, smithPitch, smithZoom);
            smithStage.RenderIcons();
        }

        class SmithSlot { public string id, label, current; public Vector3? at; }
        class SmithOpt { public string name, desc, effect, icon; public bool on; public System.Action pick; }

        static readonly PartSlot[] builderSlots = { PartSlot.Barrel, PartSlot.Cover, PartSlot.Handguard, PartSlot.Muzzle, PartSlot.Grip, PartSlot.Stock, PartSlot.Mag };

        List<SmithSlot> SmithSlots(int i)
        {
            var w = game.weapons; var d = w.defs[i]; var L = w.loadouts[i];
            var pts = w.SmithPoints(i);
            var l = new List<SmithSlot>();
            Vector3? P(string k) => pts.TryGetValue(k, out var v) ? v : (Vector3?)null;
            if (GunBuilder.Supports(d.name))
            {
                var b = L.build ?? GunBuilder.Stock(d.name);
                string[] keys = { "BARREL", "COVER", "HANDGUARD", "MUZZLE", "GRIP", "STOCK", "MAG" };
                for (int s = 0; s < 7; s++)
                    l.Add(new SmithSlot { id = "B" + s, label = GunBuilder.SlotName(b, s), current = GunBuilder.Part(b, (PartSlot)s)?.name ?? "-", at = P(keys[s]) });
            }
            else
            {
                if (WeaponController.SuppressorFor(d)) l.Add(new SmithSlot { id = "SUPP", label = "MUZZLE", current = L.suppressor ? (WeaponController.IsPistol(d) ? "Omega 9K suppressor" : "QD suppressor") : "Factory", at = P("MUZZLE") });
                if (d.name == "GLOCK 17")
                {
                    l.Add(new SmithSlot { id = "GMAG", label = "MAGAZINE", current = L.extMag ? "Glock 33-round extended" : "Glock 17-round", at = P("MAG") });
                    l.Add(new SmithSlot { id = "SWITCH", label = "SLIDE BACK PLATE", current = L.autoSwitch ? "Auto switch (full auto)" : "Factory plate", at = P("SWITCH") });
                }
            }
            var sights = WeaponController.SightsFor(d);
            if (sights.Length > 0 && !d.scope) l.Add(new SmithSlot { id = "SIGHT", label = "OPTIC", current = Attachments.OpticOf(L)?.name ?? "Iron sights", at = P("SIGHT") });
            if (WeaponController.GripsFor(d) && (!GunBuilder.Supports(d.name) || GunBuilder.Railed(L.build ?? GunBuilder.Stock(d.name))))
                l.Add(new SmithSlot { id = "FOREGRIP", label = "FOREGRIP", current = Attachments.GripOf(L)?.name ?? "None", at = P("FOREGRIP") });
            if (WeaponController.LightFor(d)) l.Add(new SmithSlot { id = "LIGHT", label = "TACTICAL LIGHT", current = Attachments.LightOf(L) is AttInfo li ? li.name + (WeaponController.IsPistol(d) ? "" : "  ·  " + WeaponController.SideNames[L.lightSide >= 0 ? L.lightSide : 0]) : "None", at = P("LIGHT") });
            if (WeaponController.LaserFor(d)) l.Add(new SmithSlot { id = "LASER", label = "AIMING LASER", current = Attachments.LaserOf(L) is AttInfo la ? la.name + (WeaponController.IsPistol(d) ? "" : "  ·  " + WeaponController.SideNames[L.laserSide >= 0 ? L.laserSide : 1]) : "None", at = P("LASER") });
            if (WeaponController.MagnifierFor(d) && (L.sight == SightKind.RedDot || L.sight == SightKind.Holo)) l.Add(new SmithSlot { id = "MAGNIFIER", label = "MAGNIFIER", current = L.magnifier ? "EOTech G33 3x" : "None", at = P("MAGNIFIER") });
            if (AmmoTable.For(d.name) != null) l.Add(new SmithSlot { id = "AMMO", label = "AMMUNITION", current = AmmoTable.For(d.name)[Mathf.Clamp(L.ammo, 0, AmmoTable.For(d.name).Length - 1)].name, at = null });
            return l;
        }

        List<SmithOpt> SmithOptions(int i, string slot)
        {
            var w = game.weapons; var d = w.defs[i]; var L = w.loadouts[i];
            var o = new List<SmithOpt>();
            void Refresh() { w.RefreshAttachments(); }
            if (slot[0] == 'B' && slot.Length == 2)
            {
                var ps = builderSlots[slot[1] - '0'];
                var b = L.build ?? GunBuilder.Stock(d.name);
                foreach (var p in GunBuilder.Options(d.name, ps, b))
                {
                    var part = p;
                    o.Add(new SmithOpt { name = p.name, desc = p.desc, effect = GunBuilder.Effect(d.name, p), icon = GunBuilder.PartModel(b, p), on = b[ps] == p.id,
                        pick = () => { L.build = (L.build ?? GunBuilder.Stock(d.name)).Clone(); GunBuilder.Fit(d.name, L.build, ps, part.id); w.ApplyBuild(i); } });
                }
                return o;
            }
            switch (slot)
            {
                case "SUPP":
                    o.Add(new SmithOpt { name = "Factory muzzle", desc = "Nothing on the muzzle.", on = !L.suppressor, pick = () => { L.suppressor = false; Refresh(); } });
                    o.Add(new SmithOpt { name = WeaponController.IsPistol(d) ? "SilencerCo Omega 9K" : "QD suppressor", desc = "Shots carry about a third as far and barely flash.", effect = "NOISE -65%", icon = WeaponController.IsPistol(d) ? "Att_SuppPistol" : "Att_SuppRifle", on = L.suppressor, pick = () => { L.suppressor = true; Refresh(); } });
                    break;
                case "GMAG":
                    o.Add(new SmithOpt { name = "Glock 17-round", desc = "The factory magazine.", effect = "17 RDS", on = !L.extMag, pick = () => { L.extMag = false; Refresh(); } });
                    o.Add(new SmithOpt { name = "Glock 33-round extended", desc = "Sticks a long way out of the grip.", effect = "33 RDS", on = L.extMag, pick = () => { L.extMag = true; Refresh(); } });
                    break;
                case "SWITCH":
                    o.Add(new SmithOpt { name = "Factory slide back plate", desc = "Semi-automatic.", on = !L.autoSwitch, pick = () => { L.autoSwitch = false; Refresh(); } });
                    o.Add(new SmithOpt { name = "Auto switch", desc = "Full auto at about 1100 rounds a minute: an extended mag empties in under two seconds.", effect = "FULL AUTO", on = L.autoSwitch, pick = () => { L.autoSwitch = true; Refresh(); } });
                    break;
                case "SIGHT":
                {
                    var on = Attachments.OpticOf(L);
                    o.Add(new SmithOpt { name = "Iron sights", desc = "The gun's own sights.", on = on == null, pick = () => { L.sight = SightKind.Iron; Refresh(); } });
                    var sights = WeaponController.SightsFor(d);
                    foreach (var a in Attachments.Of(0))
                    {
                        if (System.Array.IndexOf(sights, a.sight) < 0) continue;
                        var att = a;
                        o.Add(new SmithOpt { name = a.name, desc = a.desc, effect = SightName(a.sight), icon = a.model, on = on != null && on.id == a.id, pick = () => { L.sight = att.sight; L.optic = att.id; Refresh(); } });
                    }
                    break;
                }
                case "FOREGRIP":
                {
                    var on = Attachments.GripOf(L);
                    o.Add(new SmithOpt { name = "None", desc = "The hand on the handguard.", on = on == null, pick = () => { L.grip = GripKind.None; Refresh(); } });
                    foreach (var a in Attachments.Of(1)) { var att = a; o.Add(new SmithOpt { name = a.name, desc = a.desc, effect = a.grip == GripKind.Vertical ? "RECOIL -30%" : "FASTER AIM", icon = a.model, on = on != null && on.id == a.id, pick = () => { L.grip = att.grip; L.foregrip = att.id; Refresh(); } }); }
                    break;
                }
                case "LIGHT":
                {
                    var on = Attachments.LightOf(L);
                    o.Add(new SmithOpt { name = "None", desc = "L is a head torch with no light fitted.", on = on == null, pick = () => { L.light = false; Refresh(); } });
                    foreach (var a in Attachments.Of(2)) { var att = a; o.Add(new SmithOpt { name = a.name, desc = a.desc, effect = "L ON / OFF", icon = a.model, on = on != null && on.id == a.id, pick = () => { L.light = true; L.lightId = att.id; Refresh(); } }); }
                    break;
                }
                case "LASER":
                {
                    var on = Attachments.LaserOf(L);
                    o.Add(new SmithOpt { name = "None", desc = "", on = on == null, pick = () => { L.laser = false; Refresh(); } });
                    foreach (var a in Attachments.Of(3)) { var att = a; o.Add(new SmithOpt { name = a.name, desc = a.desc, effect = "O ON / OFF", icon = a.model, on = on != null && on.id == a.id, pick = () => { L.laser = true; L.laserId = att.id; Refresh(); } }); }
                    break;
                }
                case "MAGNIFIER":
                    o.Add(new SmithOpt { name = "None", desc = "", on = !L.magnifier, pick = () => { L.magnifier = false; Refresh(); } });
                    o.Add(new SmithOpt { name = "EOTech G33 3x magnifier", desc = "Flips in behind the red dot or holo: 3x.", effect = "3X", icon = "Att_Magnifier", on = L.magnifier, pick = () => { L.magnifier = true; Refresh(); } });
                    break;
                case "AMMO":
                {
                    var ammo = AmmoTable.For(d.name);
                    for (int k = 0; k < ammo.Length; k++)
                    {
                        int kk = k; var a = ammo[k];
                        o.Add(new SmithOpt { name = a.name, desc = a.desc, effect = $"PEN {a.pen:0.#}  ·  {a.Energy:0} J" + (a.pellets > 1 ? $"  ·  {a.pellets} PELLETS" : ""), on = L.ammo == k, pick = () => L.ammo = kk });
                    }
                    break;
                }
            }
            return o;
        }

        Texture2D Disc(int size, bool hollow)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    float a = hollow ? Mathf.Clamp01(1.5f - Mathf.Abs(d - (r - 2.5f))) : Mathf.Clamp01(r - 0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        void DrawGunsmith()
        {
            var w = game.weapons; int i = smithGun;
            if (i < 0 || i >= w.defs.Count) { CloseGunsmith(); return; }
            var d = w.defs[i]; var L = w.loadouts[i];
            float W = Screen.width, H = Screen.height;
            if (dot == null) { dot = Disc(32, false); ring = Disc(48, true); }
            tagStyle ??= new GUIStyle(small) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            optName ??= new GUIStyle(small) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), normal = { textColor = Color.white } };
            optSmall ??= new GUIStyle(small) { fontSize = 12, wordWrap = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            optEffect ??= new GUIStyle(optSmall) { wordWrap = false, normal = { textColor = new Color(0.96f, 0.7f, 0.35f) } };
            // the room
            GUI.color = new Color(0.055f, 0.06f, 0.07f, 1f); GUI.DrawTexture(new Rect(0, 0, W, H), white); GUI.color = Color.white;
            var left = new Rect(16, 16, 280, H - 32);
            var right = new Rect(W - 416, 16, 400, H - 32);
            var bottom = new Rect(left.xMax + 12, H - 16 - 220, right.x - 12 - (left.xMax + 12), 220);
            var view = new Rect(bottom.x, 16, bottom.width, bottom.y - 12 - 16);
            if (smithStage != null && smithStage.Texture != null)
            {
                smithStage.Fit(view.width / Mathf.Max(1f, view.height));
                GUI.DrawTexture(view, smithStage.Texture, ScaleMode.StretchToFill);
            }
            GUI.Label(new Rect(view.x + 14, view.y + 10, 600, 30), d.name, new GUIStyle(title) { fontSize = 26 });
            GUI.Label(new Rect(view.x + 14, view.y + 44, 700, 20), "Click a point on the gun to change that part  ·  DRAG to turn it round, any way  ·  WHEEL zoom  ·  G or ESC back", optSmall);

            // ---- the points on the gun
            var slots = SmithSlots(i);
            var ev = Event.current;
            foreach (var s in slots)
            {
                if (!s.at.HasValue || smithStage == null) continue;
                var sp = smithStage.ToScreen(s.at.Value, view);
                if (!sp.HasValue || !view.Contains(sp.Value)) continue;
                var c = sp.Value;
                bool sel = smithSel == s.id;
                var hit = new Rect(c.x - 12, c.y - 12, 24, 24);
                bool hover = hit.Contains(ev.mousePosition);
                GUI.color = sel ? new Color(0.96f, 0.56f, 0.16f) : Color.white;
                GUI.DrawTexture(new Rect(c.x - 6, c.y - 6, 12, 12), dot);
                if (sel || hover) GUI.DrawTexture(new Rect(c.x - 13, c.y - 13, 26, 26), ring);
                GUI.color = Color.white;
                if (sel || hover)
                {
                    var tr = new Rect(c.x + 16, c.y - 26, 260, 40);
                    GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(new Rect(tr.x - 6, tr.y, 270, 38), white); GUI.color = Color.white;
                    GUI.Label(new Rect(tr.x, tr.y + 2, 260, 18), s.label, tagStyle);
                    GUI.Label(new Rect(tr.x, tr.y + 18, 260, 18), s.current, optSmall);
                }
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) { smithSel = sel ? null : s.id; presetsOpen = false; smithScroll = Vector2.zero; }
            }

            // ---- left: back, presets, every slot by name
            GUILayout.BeginArea(left, card);
            if (Chip("◂  BACK TO THE ARSENAL", false, 0f, 34f)) { CloseGunsmith(); GUILayout.EndArea(); return; }
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (Chip("PRESETS", presetsOpen, 0f, 32f)) { presetsOpen = !presetsOpen; smithSel = null; }
            if (Chip("FACTORY", false, 0f, 32f))
            {
                L.ammo = 0; L.grip = GripKind.None; L.extMag = false; L.autoSwitch = false; L.suppressor = L.light = L.laser = L.magnifier = false;
                L.sight = d.scope ? SightKind.Scope8x : SightKind.Iron;
                if (GunBuilder.Supports(d.name)) { L.build = GunBuilder.Stock(d.name); w.ApplyBuild(i); }
                w.RefreshAttachments();
            }
            GUILayout.EndHorizontal();
            if (!w.owned[i] && Chip("TAKE THIS WEAPON", true, 0f, 32f)) w.Give(i, true);
            Section("PARTS");
            slotScroll = GUILayout.BeginScrollView(slotScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            foreach (var s in slots)
            {
                bool sel = smithSel == s.id;
                if (GUILayout.Button(s.label + "\n" + s.current, sel ? bigOn : big, GUILayout.Height(46))) { smithSel = sel ? null : s.id; presetsOpen = false; smithScroll = Vector2.zero; }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // ---- right: what fits the chosen slot (or the presets)
            GUILayout.BeginArea(right, card);
            if (presetsOpen) DrawPresets(i);
            else if (smithSel == null)
            {
                GUILayout.Label("PARTS", title);
                GUILayout.Label("Pick a point on the gun, or a part on the left, to see what fits there.", small);
            }
            else
            {
                var cur = slots.Find(x => x.id == smithSel);
                GUILayout.Label(cur != null ? cur.label : "", title);
                GUILayout.Label("ON IT: " + (cur != null ? cur.current : ""), small);
                GUILayout.Space(6);
                if ((smithSel == "LIGHT" || smithSel == "LASER") && !WeaponController.IsPistol(d))
                {
                    bool las = smithSel == "LASER";
                    int cur2 = las ? (L.laserSide >= 0 ? L.laserSide : 1) : (L.lightSide >= 0 ? L.lightSide : 0);
                    GUILayout.Label("MOUNT IT ON", section);
                    GUILayout.BeginHorizontal();
                    for (int sd = 0; sd < 4; sd++)
                        if (Chip(WeaponController.SideNames[sd] + (sd == 3 ? " (AHEAD OF GRIP)" : ""), cur2 == sd, 0f, 30f))
                        { if (las) L.laserSide = sd; else L.lightSide = sd; w.RefreshAttachments(); }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(6);
                }
                smithScroll = GUILayout.BeginScrollView(smithScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
                foreach (var op in SmithOptions(i, smithSel))
                {
                    var r = GUILayoutUtility.GetRect(360, op.desc != null && op.desc.Length > 60 ? 96 : 84);
                    GUI.Box(r, GUIContent.none, big);
                    if (op.on)
                    {
                        // the part on it now: an orange outline and a bar down its side
                        GUI.color = new Color(0.96f, 0.56f, 0.16f);
                        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), white); GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), white);
                        GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), white); GUI.DrawTexture(new Rect(r.xMax - 2, r.y, 2, r.height), white);
                        GUI.color = Color.white;
                    }
                    var ic = new Rect(r.x + 6, r.y + 6, 112, r.height - 12);
                    GUI.color = new Color(0.12f, 0.13f, 0.15f, 1f); GUI.DrawTexture(ic, white); GUI.color = Color.white;
                    var tex = op.icon != null ? smithStage?.Icon(op.icon) : null;
                    if (tex != null) GUI.DrawTexture(ic, tex, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(ic.xMax + 10, r.y + 8, r.width - ic.width - 22, 20), op.name + (op.on ? "   ✓" : ""), optName);
                    if (!string.IsNullOrEmpty(op.effect)) GUI.Label(new Rect(ic.xMax + 10, r.y + 29, r.width - ic.width - 22, 18), op.effect, optEffect);
                    GUI.Label(new Rect(ic.xMax + 10, r.y + 48, r.width - ic.width - 22, r.height - 52), op.desc, optSmall);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !op.on) op.pick();
                    GUILayout.Space(4);
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();

            // ---- bottom: its numbers
            GUI.Box(bottom, GUIContent.none, card);
            GUILayout.BeginArea(new Rect(bottom.x + 14, bottom.y + 10, bottom.width - 28, bottom.height - 20));
            float weight = GunBuilder.Supports(d.name) ? GunBuilder.Weight(L.build ?? GunBuilder.Stock(d.name)) : w.Mass(i);
            if (L.sight != SightKind.Iron && !d.scope) weight += 0.25f;
            if (L.light) weight += 0.12f; if (L.laser) weight += 0.2f; if (L.grip != GripKind.None) weight += 0.08f;
            float ergo = Mathf.Clamp(95f - weight * 11f + (L.grip == GripKind.Angled ? 8f : L.grip == GripKind.Vertical ? 4f : 0f), 5f, 100f);
            var am = AmmoTable.For(d.name) is AmmoType[] at && at.Length > 0 ? at[Mathf.Clamp(L.ammo, 0, at.Length - 1)] : null;
            float vrec = d.recoil * (L.grip == GripKind.Vertical ? 0.7f : 1f) * 50f;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(bottom.width * 0.48f));
            SmithStat("WEIGHT", weight / 8f, $"{weight:0.00} KG");
            SmithStat("ERGONOMICS", ergo / 100f, $"{ergo:0}");
            SmithStat("VERTICAL RECOIL", vrec / 250f, $"{vrec:0}");
            SmithStat("HORIZONTAL RECOIL", vrec * 0.45f / 250f, $"{vrec * 0.45f:0}");
            SmithStat("MAGAZINE", w.MagSize(i) / 75f, $"{w.MagSize(i)} RDS");
            GUILayout.EndVertical();
            GUILayout.Space(20);
            GUILayout.BeginVertical();
            SmithStat("FIRE RATE", w.Rpm(i) / 1100f, $"{w.Rpm(i):0} RPM  {(w.IsAuto(i) ? "AUTO" : "SEMI")}");
            SmithStat("MUZZLE VELOCITY", d.muzzleVelocity / 1100f, $"{d.muzzleVelocity:0} M/S");
            SmithStat("ACCURACY", 1f - Mathf.Clamp01(d.spread / 1.5f), $"{d.spread:0.00}°");
            SmithStat("LOUDNESS", d.loudness / 120f, $"{d.loudness:0} M" + (L.suppressor ? "  SUPPRESSED" : ""));
            SmithStat("RELOAD", d.reloadTime / 6f, $"{d.reloadTime:0.0} S" + (am != null ? $"   ·   {am.name}" : ""));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawPresets(int i)
        {
            var w = game.weapons; var d = w.defs[i]; var L = w.loadouts[i];
            GUILayout.Label("PRESETS", title);
            GUILayout.Label("Save this build under a name, or put a saved one back on.", small);
            Section("SAVE THIS BUILD");
            presetName = GUILayout.TextField(presetName ?? "", 40, GUILayout.Height(30));
            if (Chip("SAVE PRESET", true, 0f, 34f) && !string.IsNullOrWhiteSpace(presetName))
            {
                GunPresets.Save(GunPreset.From(d.name, presetName.Trim(), L));
                game.Toast("SAVED  ·  " + presetName.Trim().ToUpper());
            }
            Section("LOAD");
            var list = GunPresets.For(d.name);
            if (list.Count == 0) GUILayout.Label("No saved builds for the " + d.name + " yet.", small);
            smithScroll = GUILayout.BeginScrollView(smithScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            foreach (var p in list)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name, big, GUILayout.Height(34)))
                {
                    p.Apply(L);
                    if (GunBuilder.Supports(d.name)) w.ApplyBuild(i);
                    w.RefreshAttachments();
                    game.Toast("LOADED  ·  " + p.name.ToUpper());
                }
                if (Chip("DELETE", false, 80f, 34f)) { GunPresets.Delete(p); GUILayout.EndHorizontal(); break; }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// The gunsmith's studio far under the map: the gun lit and filmed side on for the editor, turned and zoomed
    /// by the mouse, and a little photo booth beside it that takes a picture of each part for the lists.
    /// </summary>
    public class GunsmithStage
    {
        const int Layer = 30;
        static readonly Vector3 At = new Vector3(0f, -900f, 0f), IconAt = new Vector3(40f, -900f, 0f);
        public RenderTexture Texture;
        readonly GameObject root;
        readonly Transform pivot;
        readonly Camera cam, iconCam;
        GameObject gun;
        float size = 0.9f;
        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        RenderTexture iconRT;

        public GunsmithStage(SandboxGame g)
        {
            root = new GameObject("GunsmithStage");
            root.transform.position = At;
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            cam = new GameObject("SmithCam").AddComponent<Camera>();
            cam.transform.SetParent(root.transform, false);
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.075f, 0.08f, 0.09f, 1f);
            cam.fieldOfView = 20f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 30f;
            Texture = new RenderTexture(1400, 700, 24) { antiAliasing = 4 };
            cam.targetTexture = Texture;
            iconCam = new GameObject("IconCam").AddComponent<Camera>();
            iconCam.transform.SetParent(root.transform, false);
            iconCam.cullingMask = 1 << Layer; iconCam.clearFlags = CameraClearFlags.SolidColor;
            iconCam.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 1f);
            iconCam.orthographic = true; iconCam.nearClipPlane = 0.01f; iconCam.farClipPlane = 10f;
            iconCam.enabled = false;
            iconRT = new RenderTexture(224, 128, 24) { antiAliasing = 4 };
            iconCam.targetTexture = iconRT;
            // a key and two fills, on the studio layer only
            foreach (var (rot, col, inten) in new[] { (new Vector3(35f, 200f, 0f), new Color(1f, 0.96f, 0.9f), 2.6f), (new Vector3(15f, 140f, 0f), new Color(0.65f, 0.75f, 1f), 1.4f), (new Vector3(-20f, 250f, 0f), new Color(1f, 0.85f, 0.7f), 1.0f), (new Vector3(60f, 180f, 0f), Color.white, 0.8f) })
            {
                var l = new GameObject("Light").AddComponent<Light>();
                l.transform.SetParent(root.transform, false);
                l.transform.rotation = Quaternion.Euler(rot);
                l.type = LightType.Directional; l.intensity = inten; l.color = col;
                l.cullingMask = 1 << Layer; l.shadows = LightShadows.None;
            }
            g.cam.cullingMask &= ~(1 << Layer);
        }

        public void SetActive(bool on) { if (root) root.SetActive(on); }

        public void Fit(float aspect)
        {
            int h = 760, w = Mathf.Clamp(Mathf.RoundToInt(h * aspect), 300, 2400);
            if (Texture != null && Texture.width == w) return;
            if (Texture != null) Texture.Release();
            Texture = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = Texture;
            cam.aspect = w / (float)h;
        }

        static void SetLayer(GameObject g) { foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer; }

        static Bounds BoundsOf(GameObject g)
        {
            var b = new Bounds(g.transform.position, Vector3.zero); bool any = false;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) { if (!r.gameObject.activeInHierarchy) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        /// <summary>The gun on the stand, side on, centred.</summary>
        public void Show(GameObject g)
        {
            if (gun) Object.Destroy(gun);
            gun = g;
            if (g == null) return;
            pivot.localRotation = Quaternion.identity;
            g.transform.SetParent(pivot, false);
            g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.Euler(0f, -90f, 0f); g.transform.localScale = Vector3.one;
            SetLayer(g);
            foreach (var l in g.GetComponentsInChildren<Light>(true)) l.enabled = false;
            var b = BoundsOf(g);
            g.transform.position += pivot.position - b.center;
            size = Mathf.Max(0.2f, b.size.magnitude);
        }

        public void View(float yaw, float pitch, float zoom)
        {
            pivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            // the gun across about four fifths of the picture
            float hfov = 2f * Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect);
            float dist = size * 0.5f / 0.82f / Mathf.Tan(hfov * 0.5f) / zoom;
            cam.transform.position = At + new Vector3(0f, 0f, dist + 0.2f);
            cam.transform.LookAt(At);
        }

        /// <summary>Where a point on the gun (its own space) is on the screen, in the rectangle the picture is drawn in.</summary>
        public Vector2? ToScreen(Vector3 local, Rect r)
        {
            if (gun == null) return null;
            Vector3 vp = cam.WorldToViewportPoint(gun.transform.TransformPoint(local));
            if (vp.z < 0f) return null;
            return new Vector2(r.x + vp.x * r.width, r.y + (1f - vp.y) * r.height);
        }

        readonly Queue<string> wanted = new Queue<string>();

        /// <summary>A picture of a part, once it has been taken (asked for here, taken in RenderIcons).</summary>
        public Texture2D Icon(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            if (icons.TryGetValue(model, out var t)) return t;
            icons[model] = null;
            wanted.Enqueue(model);
            return null;
        }

        /// <summary>Take the pictures asked for (a few a frame, outside the GUI pass).</summary>
        public void RenderIcons()
        {
            for (int n = 0; n < 4 && wanted.Count > 0; n++) Take(wanted.Dequeue());
        }

        void Take(string model)
        {
            Texture2D t;
            var holder = new GameObject("IconHolder");
            holder.transform.SetParent(root.transform, false);
            holder.transform.position = IconAt;
            var g = ModelLibrary.Spawn(model, holder.transform, ModelLibrary.GunMaterial, false);
            if (g == null) { Object.Destroy(holder); return; }
            g.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            SetLayer(holder);
            var b = BoundsOf(holder);
            float aspect = iconRT.width / (float)iconRT.height;
            iconCam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect) * 1.18f + 0.002f;
            iconCam.transform.position = b.center + new Vector3(0.25f, 0.18f, 2f);
            iconCam.transform.LookAt(b.center);
            iconCam.aspect = aspect;
            iconCam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = iconRT;
            t = new Texture2D(iconRT.width, iconRT.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, iconRT.width, iconRT.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            Object.DestroyImmediate(holder);
            icons[model] = t;
        }
    }
}
