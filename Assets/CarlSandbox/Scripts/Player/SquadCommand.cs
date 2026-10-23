using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    /// <summary>
    /// Your squad. Look at a friendly Earl (green armband) and HOLD R to take him into your squad (or let him go);
    /// a tap of R still reloads. HOLD MIDDLE MOUSE for the squad list - who's in it, how they are, what they're
    /// doing - and, while it's up: 1-9 picks one of them (0 / ` all of them, the wheel steps through),
    /// LEFT CLICK sends them where you're looking, RIGHT CLICK has them follow you, H holds them where they are,
    /// E sends a medic to whoever you're looking at (or you), U has someone help them (or you) up.
    /// A tap of the middle mouse still opens the medical panel.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SquadCommand : MonoBehaviour
    {
        public static SquadCommand I;
        /// <summary>The frame a short tap of R happened (reload), and of the middle mouse (medical panel).</summary>
        public static int ReloadFrame = -1, MiddleTapFrame = -1;
        public static bool Open => I != null && I.open;

        bool open;
        float rDown = -1f, mDown = -1f;
        EarlGunner recruiting;
        int selected;          // 0 = all
        readonly List<EarlGunner> list = new List<EarlGunner>();
        GUIStyle card, title, row, small, hint;

        public static void Attach(GameObject g) => I = g.AddComponent<SquadCommand>();

        static Camera Cam => SandboxGame.Instance != null ? SandboxGame.Instance.cam : Camera.main;

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            bool locked = Cursor.lockState == CursorLockMode.Locked;

            // ---- R: tap reloads, hold on a friendly recruits (or dismisses)
            if (kb.rKey.wasPressedThisFrame)
            {
                recruiting = locked ? FriendlyInSight(5f) : null;
                if (recruiting == null) ReloadFrame = Time.frameCount;
                else rDown = Time.unscaledTime;
            }
            if (recruiting != null)
            {
                if (!kb.rKey.isPressed)
                {
                    // let go early: just a reload after all
                    if (Time.unscaledTime - rDown < 0.5f) ReloadFrame = Time.frameCount;
                    recruiting = null;
                }
                else if (Time.unscaledTime - rDown >= 0.5f)
                {
                    bool join = !recruiting.inSquad;
                    int n = 0; foreach (var _ in EarlGunner.Squad) n++;
                    if (join && n >= 8) SandboxGame.Instance?.Toast("SQUAD FULL (8)");
                    else
                    {
                        recruiting.Recruit(join);
                        SandboxGame.Instance?.Toast(join ? $"{recruiting.Body.DisplayName}{(recruiting.medic ? " (MEDIC)" : "")} JOINED YOUR SQUAD" : $"{recruiting.Body.DisplayName} LEFT YOUR SQUAD");
                        Sfx.Play("click_empty", Cam.transform.position, 0.3f, 0f, 1f, 0.05f);
                    }
                    recruiting = null;
                }
            }

            // ---- the middle mouse: tap = medical panel, hold = the squad list
            if (mouse.middleButton.wasPressedThisFrame) mDown = Time.unscaledTime;
            if (mDown >= 0f && mouse.middleButton.isPressed && !open && Time.unscaledTime - mDown > 0.22f && locked) { open = true; Refresh(); }
            if (mDown >= 0f && !mouse.middleButton.isPressed)
            {
                if (!open) MiddleTapFrame = Time.frameCount;
                open = false; mDown = -1f;
            }
            if (!open) return;
            Menus.LastMenuFrame = Time.frameCount;     // (keeps the gun's keys and buttons off while it's up)
            Refresh();
            for (int k = 0; k <= 9; k++)
            {
                var key = k == 0 ? kb.digit0Key : (KeyControl(kb, k));
                if (key.wasPressedThisFrame) selected = Mathf.Min(k, list.Count);
            }
            if (kb.backquoteKey.wasPressedThisFrame) selected = 0;
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0.1f) selected = (selected + list.Count) % (list.Count + 1);
            if (wheel < -0.1f) selected = (selected + 1) % (list.Count + 1);
            if (selected > list.Count) selected = 0;
            if (list.Count == 0) return;

            var who = Chosen();
            if (mouse.leftButton.wasPressedThisFrame && Aim(out Vector3 at, out _))
            {
                // spread out round the spot, a pace and a half apart
                Vector3 side = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(Cam.transform.forward, Vector3.up).normalized);
                for (int i = 0; i < who.Count; i++)
                {
                    who[i].order = SquadOrder.Move;
                    who[i].orderPos = at + side * ((i - (who.Count - 1) * 0.5f) * 1.5f);
                }
                Say($"{Name(who)}: MOVING TO {Vector3.Distance(Cam.transform.position, at):0} M");
            }
            if (mouse.rightButton.wasPressedThisFrame) { foreach (var g in who) g.order = SquadOrder.Follow; Say($"{Name(who)}: FOLLOWING YOU"); }
            if (kb.hKey.wasPressedThisFrame) { foreach (var g in who) { g.order = SquadOrder.Hold; g.orderPos = g.Body.Pelvis.transform.position - Vector3.up * 0.95f; } Say($"{Name(who)}: HOLDING"); }
            if (kb.eKey.wasPressedThisFrame)
            {
                Aim(out _, out var looked);
                var patient = looked != null && !looked.Dead ? looked : PlayerAvatar.Body;
                EarlGunner medic = null;
                if (who.Count == 1 && who[0].medic) medic = who[0];
                else
                {
                    float bd = float.MaxValue;
                    foreach (var g in list) if (g.medic && Vector3.Distance(g.Body.Center, patient.Center) < bd) { bd = Vector3.Distance(g.Body.Center, patient.Center); medic = g; }
                }
                if (medic == null) Say("NO MEDIC IN YOUR SQUAD");
                else { medic.order = SquadOrder.Heal; medic.orderTarget = patient; Say($"{medic.Body.DisplayName}: GOING TO HEAL {(patient == PlayerAvatar.Body ? "YOU" : patient.DisplayName)}"); }
            }
            if (kb.uKey.wasPressedThisFrame)
            {
                Aim(out _, out var looked);
                var down = looked != null && looked.ragdoll.IsDown ? looked : PlayerAvatar.Body;
                if (down == null || !down.ragdoll.IsDown) Say("NOBODY DOWN THERE");
                else
                {
                    EarlGunner h = null; float bd = float.MaxValue;
                    foreach (var g in who) if (g.Body != down && Vector3.Distance(g.Body.Center, down.Center) < bd) { bd = Vector3.Distance(g.Body.Center, down.Center); h = g; }
                    if (h != null) { h.order = SquadOrder.HelpUp; h.orderTarget = down; Say($"{h.Body.DisplayName}: HELPING {(down == PlayerAvatar.Body ? "YOU" : down.DisplayName)} UP"); }
                }
            }
        }

        static UnityEngine.InputSystem.Controls.KeyControl KeyControl(Keyboard kb, int k)
        {
            switch (k)
            {
                case 1: return kb.digit1Key; case 2: return kb.digit2Key; case 3: return kb.digit3Key; case 4: return kb.digit4Key;
                case 5: return kb.digit5Key; case 6: return kb.digit6Key; case 7: return kb.digit7Key; case 8: return kb.digit8Key;
                default: return kb.digit9Key;
            }
        }

        void Refresh()
        {
            list.Clear();
            foreach (var g in EarlGunner.Squad) list.Add(g);
            list.Sort((a, b) => a.Body.number.CompareTo(b.Body.number));
        }

        List<EarlGunner> Chosen()
        {
            if (selected <= 0 || selected > list.Count) return new List<EarlGunner>(list);
            return new List<EarlGunner> { list[selected - 1] };
        }

        static string Name(List<EarlGunner> who) => who.Count == 1 ? who[0].Body.DisplayName : "SQUAD";

        static void Say(string s)
        {
            SandboxGame.Instance?.Toast(s);
            Sfx.Play("click_empty", Cam.transform.position, 0.25f, 0f, 1f, 0.05f);
        }

        /// <summary>Where you're looking: the spot on the ground (or wall), and anyone there.</summary>
        static bool Aim(out Vector3 at, out HumanBody body)
        {
            at = Vector3.zero; body = null;
            var cam = Cam; if (cam == null) return false;
            float best = 300f; bool got = false;
            foreach (var hit in Physics.RaycastAll(cam.transform.position, cam.transform.forward, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is CharacterController || hit.distance >= best) continue;
                var hb = hit.collider.GetComponentInParent<HumanBody>();
                if (hb != null && hb == PlayerAvatar.Body) continue;
                best = hit.distance; at = hit.point; body = hb; got = true;
            }
            // a spot to stand: the floor under where you pointed (not halfway up a wall)
            if (got && Physics.Raycast(at + Vector3.up * 0.3f - cam.transform.forward * 0.4f, Vector3.down, out var g, 4f, ~0, QueryTriggerInteraction.Ignore)) at = g.point;
            return got;
        }

        /// <summary>The friendly Earl you're looking at, if he's close.</summary>
        static EarlGunner FriendlyInSight(float range)
        {
            var cam = Cam; if (cam == null) return null;
            HumanBody h = null; float best = range;
            foreach (var hit in Physics.SphereCastAll(cam.transform.position, 0.15f, cam.transform.forward, range, ~0, QueryTriggerInteraction.Ignore))
            {
                var hb = hit.collider.GetComponentInParent<HumanBody>();
                if (hb == null || hb == PlayerAvatar.Body || hit.distance >= best) continue;
                best = hit.distance; h = hb;
            }
            return h != null && h.gunner != null && h.gunner.team == Teams.Friendly && h.gunner.Fighting ? h.gunner : null;
        }

        // ------------------------------------------------------------ the list

        void Styles()
        {
            if (card != null) return;
            RectOffset B(int b) => new RectOffset(b, b, b, b);
            card = new GUIStyle { normal = { background = Menus.Rounded(new Color(0.07f, 0.08f, 0.095f, 0.9f), new Color(1f, 1f, 1f, 0.08f), 12) }, border = B(12), padding = new RectOffset(14, 14, 12, 12) };
            title = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, margin = new RectOffset(0, 0, 0, 6) };
            row = new GUIStyle { fontSize = 12, richText = true, normal = { textColor = new Color(0.9f, 0.92f, 0.94f) }, padding = new RectOffset(8, 8, 4, 4) };
            small = new GUIStyle { fontSize = 11, wordWrap = true, richText = true, normal = { textColor = new Color(0.62f, 0.64f, 0.68f) }, margin = new RectOffset(0, 0, 6, 0) };
            hint = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.55f, 0.95f, 0.5f) } };
        }

        void OnGUI()
        {
            GUI.depth = -55;
            Styles();
            float W = Screen.width, H = Screen.height;
            if (recruiting != null)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - rDown) / 0.5f);
                GUI.Label(new Rect(W / 2 - 200, H / 2 + 40, 400, 24), (recruiting.inSquad ? "DISMISSING " : "RECRUITING ") + recruiting.Body.DisplayName + "  " + new string('|', Mathf.RoundToInt(k * 20f)), hint);
            }
            else if (Cursor.lockState == CursorLockMode.Locked && !open && Time.frameCount % 10 == 0) lookingAt = FriendlyInSight(5f);
            if (!open && recruiting == null && lookingAt != null && Cursor.lockState == CursorLockMode.Locked)
                GUI.Label(new Rect(W / 2 - 200, H / 2 + 40, 400, 24), $"{lookingAt.Body.DisplayName}{(lookingAt.medic ? " · MEDIC" : "")} · HOLD R TO {(lookingAt.inSquad ? "DISMISS" : "RECRUIT")}", hint);
            if (!open) return;
            var r = new Rect(W - 470, 70, 450, 92 + 24 * (list.Count + 1) + 90);
            GUILayout.BeginArea(r, card);
            GUILayout.Label($"SQUAD  ·  {list.Count}", title);
            Row(0, "<b>0  ALL</b>", "");
            for (int i = 0; i < list.Count; i++)
            {
                var g = list[i]; var h = g.Body; var p = h.physio;
                string state = h.Dead ? "<color=#ff5040>DEAD</color>" : h.ragdoll.IsDown ? "<color=#ffb040>DOWN</color>" : g.SeesEnemy ? "<color=#ff8060>ENGAGING</color>" : (g.Status == "" ? g.order.ToString().ToUpper() : g.Status);
                float blood = Mathf.Clamp01(1f - p.LossFrac) * 100f;
                bool bleeding = false; foreach (var bp in h.partList) if (bp != null) foreach (var w in bp.wounds) if (w.extRate > 0.5f) bleeding = true;
                Row(i + 1, $"<b>{i + 1}  {h.DisplayName}</b>{(g.medic ? "  <color=#7fd0ff>MEDIC</color>" : "")}",
                    $"BLOOD {blood:0}%{(bleeding ? " <color=#ff5040>BLEEDING</color>" : "")}   {state}   {Vector3.Distance(h.Center, Cam.transform.position):0} M");
            }
            GUILayout.Label("<b>1-9</b> pick · <b>0</b> all · <b>LEFT CLICK</b> go where you look · <b>RIGHT CLICK</b> follow me · <b>H</b> hold there · <b>E</b> medic: heal who you look at (or you) · <b>U</b> help them (or you) up", small);
            GUILayout.EndArea();
            // where they'd go
            if (Aim(out var at, out var who))
            {
                string s = who != null && who != PlayerAvatar.Body ? who.DisplayName + (who.ragdoll.IsDown ? " (DOWN)" : "") : $"GO HERE · {Vector3.Distance(Cam.transform.position, at):0} M";
                GUI.Label(new Rect(W / 2 - 200, H / 2 + 28, 400, 24), s, hint);
            }
        }

        EarlGunner lookingAt;

        void Row(int i, string left, string right)
        {
            var rect = GUILayoutUtility.GetRect(400, 24);
            if (i == selected) { GUI.color = new Color(0.96f, 0.56f, 0.16f, 0.35f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
            GUI.Label(new Rect(rect.x, rect.y, 200, rect.height), left, row);
            GUI.Label(new Rect(rect.x + 150, rect.y, rect.width - 150, rect.height), right, new GUIStyle(row) { alignment = TextAnchor.MiddleRight });
        }
    }
}
