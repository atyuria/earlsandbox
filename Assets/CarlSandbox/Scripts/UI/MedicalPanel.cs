using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Middle-click a person to open their medical monitor: live 3D x-ray portrait, vitals,
    /// mental state, neurological status, fractures and a per-region wound table.
    /// </summary>
    public class MedicalPanel : MonoBehaviour
    {
        public HumanBody selected;
        Camera portraitCam;
        RenderTexture rt;
        Vector2 scroll;
        GUIStyle panel, card, tile, pill, partBox, header, title, text, dim, big, small, tiny, value, unit, rowL, rowR;
        Texture2D white;

        static readonly Color Orange = new Color(1f, 0.45f, 0.15f);
        static readonly Color Green = new Color(0.4f, 0.92f, 0.5f);
        static readonly Color Amber = new Color(1f, 0.72f, 0.2f);
        static readonly Color Red = new Color(1f, 0.25f, 0.2f);
        static readonly Color Blue = new Color(0.35f, 0.75f, 1f);
        static readonly Color Violet = new Color(0.7f, 0.55f, 1f);
        static readonly Color Ink = new Color(0.93f, 0.95f, 0.97f);
        static readonly Color Muted = new Color(0.58f, 0.63f, 0.69f);
        static readonly Color ArmourBlue = new Color(0.55f, 0.75f, 1f);

        public void Select(HumanBody h)
        {
            if (selected != null) selected.forceXray = false;
            selected = h == selected ? null : h;
            // not your own: the camera is inside it, and an x-rayed head would fill the screen
            if (selected != null && selected != PlayerAvatar.Body) selected.forceXray = true;
            EnsureCamera();
            portraitCam.enabled = selected != null;
        }

        void EnsureCamera()
        {
            if (portraitCam != null) return;
            rt = new RenderTexture(260, 440, 24) { name = "MedicalPortrait" };
            var go = new GameObject("PortraitCamera");
            go.transform.SetParent(transform, false);
            portraitCam = go.AddComponent<Camera>();
            portraitCam.targetTexture = rt;
            portraitCam.clearFlags = CameraClearFlags.SolidColor;
            portraitCam.backgroundColor = new Color(0.03f, 0.04f, 0.05f, 1f);
            portraitCam.fieldOfView = 30f;
            portraitCam.nearClipPlane = 0.2f;
            portraitCam.farClipPlane = 12f;
            portraitCam.depth = -10;
            portraitCam.enabled = false;
        }

        static readonly Unity.Profiling.ProfilerMarker PM_LateUpdate = new Unity.Profiling.ProfilerMarker("Carl.MedicalPanel.LateUpdate");

        void LateUpdate()
        {

            using var pmScope = PM_LateUpdate.Auto();
            if (selected == null || selected.gameObject == null) { if (selected == null && portraitCam) portraitCam.enabled = false; return; }
            var pel = selected.Pelvis.transform;
            var chest = selected.Chest.transform;
            Vector3 center = (pel.position + chest.position) * 0.5f;
            bool upright = Vector3.Dot(chest.up, Vector3.up) > 0.6f;
            Vector3 pos;
            Quaternion rot;
            if (upright)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(chest.forward, Vector3.up).normalized;
                center.y = pel.position.y + 0.05f;
                pos = center + fwd * 3.6f;
                rot = Quaternion.LookRotation(center - pos, Vector3.up);
            }
            else
            {
                Vector3 along = Vector3.ProjectOnPlane(chest.up, Vector3.up);
                if (along.sqrMagnitude < 0.01f) along = Vector3.forward;
                pos = center + Vector3.up * 3.7f;
                rot = Quaternion.LookRotation(Vector3.down, along.normalized);
            }
            portraitCam.transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>A rounded rectangle, drawn nine-sliced so the corners stay round at any size.</summary>
        static Texture2D Rounded(Color c, int radius)
        {
            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    var k = c; k.a *= Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = k;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static RectOffset R(int a) => new RectOffset(a, a, a, a);

        void InitStyles()
        {
            if (panel != null) return;
            white = Texture2D.whiteTexture;
            panel = new GUIStyle { normal = { background = Rounded(new Color(0.04f, 0.047f, 0.057f, 0.94f), 12) }, border = R(12), padding = new RectOffset(14, 14, 12, 12) };
            card = new GUIStyle { normal = { background = Rounded(new Color(0.085f, 0.097f, 0.113f, 1f), 8) }, border = R(8), padding = new RectOffset(11, 11, 8, 9), margin = new RectOffset(0, 0, 0, 6) };
            tile = new GUIStyle { normal = { background = Rounded(new Color(0.055f, 0.065f, 0.077f, 1f), 6) }, border = R(6), padding = new RectOffset(9, 9, 6, 6), margin = new RectOffset(0, 0, 0, 0) };
            pill = new GUIStyle { normal = { background = Rounded(Color.white, 9), textColor = new Color(0.05f, 0.06f, 0.07f) }, border = R(9), padding = new RectOffset(9, 9, 3, 3), margin = new RectOffset(0, 5, 2, 2), fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            partBox = new GUIStyle { normal = { background = Rounded(Color.white, 4) }, border = R(4) };
            header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = Orange }, margin = new RectOffset(0, 0, 0, 4), padding = new RectOffset(0, 0, 0, 0) };
            title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 2, 2) };
            text = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Ink }, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 1, 1), wordWrap = true };
            dim = new GUIStyle(text) { normal = { textColor = Muted } };
            big = new GUIStyle(text) { fontSize = 13, fontStyle = FontStyle.Bold };
            small = new GUIStyle(dim) { fontSize = 10 };
            tiny = new GUIStyle(dim) { fontSize = 9, fontStyle = FontStyle.Bold, wordWrap = false };
            value = new GUIStyle(text) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = false, margin = new RectOffset(0, 0, 0, 0) };
            unit = new GUIStyle(tiny) { alignment = TextAnchor.LowerLeft, padding = new RectOffset(3, 0, 0, 4) };
            rowL = new GUIStyle(text) { fontStyle = FontStyle.Bold, wordWrap = false };
            rowR = new GUIStyle(text) { alignment = TextAnchor.MiddleRight, wordWrap = false };
        }

        Texture2D ecg;
        Color32[] ecgPix;
        float ecgPhase, ecgX;
        const int EcgW = 300, EcgH = 54;

        /// <summary>Scrolling ECG trace drawn into a little texture each frame (flatline when the heart stops).</summary>
        void UpdateEcg(Physiology p)
        {
            if (ecg == null) { ecg = new Texture2D(EcgW, EcgH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear }; ecgPix = new Color32[EcgW * EcgH]; }
            float rate = p.dead || p.cardiacArrest ? 0f : p.heartRate;
            int steps = Mathf.Max(1, Mathf.RoundToInt(Time.deltaTime * 120f));
            for (int k = 0; k < steps; k++)
            {
                ecgPhase += rate / 60f / 120f;
                float ph = ecgPhase - Mathf.Floor(ecgPhase);
                // P wave, QRS spike, T wave
                float y = 0.06f * Mathf.Exp(-Mathf.Pow((ph - 0.15f) / 0.03f, 2f))
                        - 0.12f * Mathf.Exp(-Mathf.Pow((ph - 0.28f) / 0.008f, 2f))
                        + 0.85f * Mathf.Exp(-Mathf.Pow((ph - 0.3f) / 0.01f, 2f))
                        - 0.2f * Mathf.Exp(-Mathf.Pow((ph - 0.32f) / 0.01f, 2f))
                        + 0.16f * Mathf.Exp(-Mathf.Pow((ph - 0.5f) / 0.05f, 2f));
                if (rate <= 0f) y = (Random.value - 0.5f) * 0.02f;
                int x = (int)ecgX % EcgW;
                for (int r = 0; r < EcgH; r++) ecgPix[r * EcgW + x] = new Color32(0, 0, 0, 0);
                for (int dx = 1; dx < 6; dx++) for (int r = 0; r < EcgH; r++) ecgPix[r * EcgW + (x + dx) % EcgW] = new Color32(0, 0, 0, 0);
                int py = Mathf.Clamp(Mathf.RoundToInt(EcgH * 0.35f + y * EcgH * 0.55f), 1, EcgH - 2);
                var col = rate <= 0f ? new Color32(255, 70, 60, 255) : new Color32(90, 255, 120, 255);
                ecgPix[py * EcgW + x] = col; ecgPix[(py + 1) * EcgW + x] = col; ecgPix[(py - 1) * EcgW + x] = col;
                ecgX += 1f;
            }
            ecg.SetPixels32(ecgPix);
            ecg.Apply(false);
        }

        // ============================================================ building blocks

        void Fill(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

        void BeginCard(string name, Color accent)
        {
            GUILayout.BeginVertical(card);
            GUILayout.BeginHorizontal();
            var r = GUILayoutUtility.GetRect(3, 12, GUILayout.Width(3), GUILayout.Height(12));
            Fill(new Rect(r.x, r.y + 1, 3, 11), accent);
            GUILayout.Space(6);
            GUILayout.Label(name, new GUIStyle(header) { normal = { textColor = accent } });
            GUILayout.EndHorizontal();
        }

        void EndCard() => GUILayout.EndVertical();

        void Pill(string s, Color c)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = c;
            GUILayout.Label(s, pill, GUILayout.ExpandWidth(false));
            GUI.backgroundColor = old;
        }

        /// <summary>One vital sign: label, big coloured number with its unit, and how far through its range it sits.</summary>
        void Tile(string label, string val, string u, Color c, float fill01, float width)
        {
            GUILayout.BeginVertical(tile, GUILayout.Width(width));
            GUILayout.Label(label, tiny);
            GUILayout.BeginHorizontal();
            GUILayout.Label(val, new GUIStyle(value) { normal = { textColor = c } }, GUILayout.ExpandWidth(false));
            GUILayout.Label(u, unit, GUILayout.Height(28));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            var r = GUILayoutUtility.GetRect(10, 3, GUILayout.ExpandWidth(true));
            Fill(r, new Color(1f, 1f, 1f, 0.08f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill01), r.height), c);
            GUILayout.EndVertical();
        }

        /// <summary>A labelled bar: name on the left, value on the right, the bar between.</summary>
        void Meter(string label, float v01, string val, Color c)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(16));
            GUILayout.Label(label, tiny, GUILayout.Width(92), GUILayout.Height(16));
            var r = GUILayoutUtility.GetRect(60, 16, GUILayout.ExpandWidth(true));
            r.y += 5; r.height = 6;
            Fill(r, new Color(1f, 1f, 1f, 0.07f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v01), r.height), c);
            GUILayout.Label(val, new GUIStyle(tiny) { alignment = TextAnchor.MiddleRight, normal = { textColor = Ink } }, GUILayout.Width(88), GUILayout.Height(16));
            GUILayout.EndHorizontal();
        }

        void Line(string s, GUIStyle st = null) => GUILayout.Label(s, st ?? text);

        static Color Grade(float good01) => good01 > 0.66f ? Green : good01 > 0.33f ? Amber : Red;

        static Color DamageColor(float d) => d < 0.02f ? Green : Color.Lerp(Amber, Red, Mathf.InverseLerp(0.2f, 0.8f, d));

        /// <summary>Front-view body map: each region coloured by how damaged it is; armoured regions outlined.</summary>
        void BodyMap(HumanBody h)
        {
            var area = GUILayoutUtility.GetRect(150, 236, GUILayout.Width(150));
            Fill(area, new Color(0f, 0f, 0f, 0.18f));
            float cx = area.x + 75f, top = area.y + 8f, s = 1.1f;
            void Part(PartId id, float x, float y, float w, float hh)
            {
                var bp = h[id];
                float dmg = bp.tissueDamage;
                foreach (var o in bp.organs) dmg = Mathf.Max(dmg, o.Damage * 0.9f);
                if (bp.wounds.Count > 0) dmg = Mathf.Max(dmg, 0.25f);
                bool lost = bp.severed && bp.joint == null && bp.parent != null;
                Color c = lost ? new Color(0.16f, 0.17f, 0.19f, 1f) : Color.Lerp(new Color(0.28f, 0.72f, 0.4f), new Color(1f, 0.15f, 0.1f), dmg);
                var r = new Rect(cx + x * s - w * s / 2f, top + y * s, w * s, hh * s);
                bool armoured = false;
                foreach (Transform t in bp.transform) if (t.GetComponent<ArmourPiece>() != null) { armoured = true; break; }
                var old = GUI.backgroundColor;
                if (armoured) { GUI.backgroundColor = new Color(ArmourBlue.r, ArmourBlue.g, ArmourBlue.b, 0.95f); GUI.Box(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), GUIContent.none, partBox); }
                GUI.backgroundColor = c;
                GUI.Box(r, GUIContent.none, partBox);
                GUI.backgroundColor = old;
                // bleeding regions get a pulsing dot
                if (bp.OutRate + bp.InRate > 1f && !h.physio.dead)
                {
                    float a = 0.55f + 0.45f * Mathf.Sin(Time.time * 8f);
                    Fill(new Rect(r.center.x - 2, r.center.y - 2, 4, 4), new Color(1f, 0.95f, 0.9f, a));
                }
            }
            Part(PartId.Head, 0, 0, 22, 26); Part(PartId.Neck, 0, 27, 12, 8);
            Part(PartId.Chest, 0, 36, 46, 38); Part(PartId.Abdomen, 0, 75, 40, 26); Part(PartId.Pelvis, 0, 102, 42, 20);
            Part(PartId.UpperArmL, -30, 38, 11, 36); Part(PartId.ForearmL, -32, 75, 10, 34); Part(PartId.HandL, -33, 110, 10, 12);
            Part(PartId.UpperArmR, 30, 38, 11, 36); Part(PartId.ForearmR, 32, 75, 10, 34); Part(PartId.HandR, 33, 110, 10, 12);
            Part(PartId.ThighL, -11, 123, 16, 44); Part(PartId.ShinL, -11, 168, 13, 38); Part(PartId.FootL, -11, 207, 14, 8);
            Part(PartId.ThighR, 11, 123, 16, 44); Part(PartId.ShinR, 11, 168, 13, 38); Part(PartId.FootR, 11, 207, 14, 8);
        }

        void Legend(Color c, string s)
        {
            GUILayout.BeginHorizontal();
            var r = GUILayoutUtility.GetRect(8, 12, GUILayout.Width(8), GUILayout.Height(12));
            Fill(new Rect(r.x, r.y + 2, 8, 8), c);
            GUILayout.Space(4);
            GUILayout.Label(s, tiny);
            GUILayout.EndHorizontal();
        }

        /// <summary>The live x-ray picture, framed like a scanner screen.</summary>
        void Portrait(Rect pr)
        {
            Fill(pr, new Color(0.02f, 0.03f, 0.04f, 1f));
            if (rt != null) GUI.DrawTexture(pr, rt, ScaleMode.ScaleAndCrop);
            // a slow sweep line
            float sy = pr.y + Mathf.Repeat(Time.time * 60f, pr.height);
            Fill(new Rect(pr.x, sy, pr.width, 1), new Color(0.5f, 0.85f, 1f, 0.18f));
            // corner brackets
            var b = new Color(0.55f, 0.8f, 1f, 0.7f);
            const float L = 14f;
            Fill(new Rect(pr.x, pr.y, L, 2), b); Fill(new Rect(pr.x, pr.y, 2, L), b);
            Fill(new Rect(pr.xMax - L, pr.y, L, 2), b); Fill(new Rect(pr.xMax - 2, pr.y, 2, L), b);
            Fill(new Rect(pr.x, pr.yMax - 2, L, 2), b); Fill(new Rect(pr.x, pr.yMax - L, 2, L), b);
            Fill(new Rect(pr.xMax - L, pr.yMax - 2, L, 2), b); Fill(new Rect(pr.xMax - 2, pr.yMax - L, 2, L), b);
            GUI.Label(new Rect(pr.x + 7, pr.y + 5, 120, 14), "X-RAY  ·  LIVE", new GUIStyle(tiny) { normal = { textColor = b } });
        }

        void Ecg(Physiology p)
        {
            UpdateEcg(p);
            var er = GUILayoutUtility.GetRect(EcgW * 0.8f, EcgH + 6, GUILayout.ExpandWidth(true));
            bool flat = p.dead || p.cardiacArrest;
            Fill(er, new Color(0.01f, 0.06f, 0.03f, 0.95f));
            var grid = new Color(0.3f, 1f, 0.5f, 0.06f);
            for (float x = er.x; x < er.xMax; x += 12f) Fill(new Rect(x, er.y, 1, er.height), grid);
            for (float y = er.y; y < er.yMax; y += 12f) Fill(new Rect(er.x, y, er.width, 1), grid);
            if (ecg != null) GUI.DrawTexture(new Rect(er.x, er.y + 3, er.width, er.height - 6), ecg, ScaleMode.StretchToFill);
            GUI.Label(new Rect(er.x + 6, er.y + 3, 140, 14), flat ? "ECG  ·  ASYSTOLE" : "ECG  ·  LEAD II", new GUIStyle(tiny) { normal = { textColor = flat ? Red : new Color(0.45f, 1f, 0.55f, 0.8f) } });
        }

        // ============================================================ the panel

        void OnGUI()
        {
            if (selected == null) return;
            InitStyles();
            var h = selected;
            var p = h.physio;
            const float w = 500f;
            float hgt = Mathf.Min(Screen.height - 32f, 920f);
            GUILayout.BeginArea(new Rect(16, 16, w, hgt), panel);
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            // ---- header
            GUILayout.BeginHorizontal();
            GUILayout.Label("MEDICAL MONITOR", header);
            GUILayout.FlexibleSpace();
            GUILayout.Label("MIDDLE CLICK: NEXT / CLOSE", tiny);
            GUILayout.EndHorizontal();
            GUILayout.Label(h.DisplayName, title);

            int wounds = 0, regions = 0; bool arterial = false;
            foreach (var bp in h.partList)
            {
                if (bp.wounds.Count == 0) continue;
                regions++; wounds += bp.wounds.Count;
                foreach (var wd in bp.wounds) arterial |= wd.arterial && wd.extRate > 2f;
            }
            float totalBleed = p.bleedOut + p.bleedIn;
            string state = p.dead ? "DECEASED" : arterial ? "CRITICAL · ARTERIAL BLEED" : totalBleed > 15f ? "CRITICAL · MASSIVE HAEMORRHAGE" : totalBleed > 4f ? "SERIOUS · BLEEDING" : wounds > 0 ? "STABLE" : "UNINJURED";
            Color stateCol = p.dead ? Muted : arterial || totalBleed > 15f ? Red : totalBleed > 4f ? Amber : Green;
            string mode = h.ragdoll.mode == BodyMode.Standing ? (h.ragdoll.moveSpeed > 2f ? "RUNNING" : h.ragdoll.moveSpeed > 0.1f ? "WALKING" : "STANDING") : h.ragdoll.mode == BodyMode.GettingUp ? "GETTING UP" : "DOWN";

            GUILayout.BeginHorizontal();
            Pill(p.dead ? "DEAD" : p.cardiacArrest ? "CLINICALLY DEAD" : "ALIVE", p.dead || p.cardiacArrest ? Red : Green);
            Pill(p.ConsciousText(), p.consciousness > 0.6f ? new Color(0.75f, 0.82f, 0.9f) : p.consciousness > 0.25f ? Amber : new Color(0.6f, 0.6f, 0.65f));
            Pill(mode, new Color(0.7f, 0.74f, 0.8f));
            Pill(state, stateCol);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (p.dead) GUILayout.Label("CAUSE OF DEATH: " + p.causeOfDeath, new GUIStyle(dim) { normal = { textColor = new Color(1f, 0.6f, 0.55f) } });
            GUILayout.Space(8);

            // ---- portrait and vitals
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(card, GUILayout.Width(166));
            Portrait(GUILayoutUtility.GetRect(144, 292));
            GUILayout.EndVertical();
            GUILayout.Space(6);
            GUILayout.BeginVertical();
            float tw = 128f;
            float hr = p.cardiacArrest || p.dead ? 0f : p.heartRate;
            float bloodFrac = p.blood / Physiology.NormalBlood;
            GUILayout.BeginHorizontal();
            Tile("HEART RATE", hr.ToString("0"), "BPM", hr <= 0f ? Red : hr < 45f || hr > 140f ? Red : hr < 55f || hr > 110f ? Amber : Green, hr / 200f, tw);
            GUILayout.Space(5);
            Tile("BLOOD PRESSURE", $"{p.systolic:0}/{p.diastolic:0}", "", p.systolic < 70f ? Red : p.systolic < 95f ? Amber : Green, p.systolic / 160f, tw);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            Tile("OXYGEN SAT", (p.spo2 * 100f).ToString("0"), "%", p.spo2 > 0.93f ? Blue : p.spo2 > 0.85f ? Amber : Red, p.spo2, tw);
            GUILayout.Space(5);
            Tile("BLOOD VOLUME", (bloodFrac * 100f).ToString("0"), "%", Grade(Mathf.InverseLerp(0.55f, 0.9f, bloodFrac)), bloodFrac, tw);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            Ecg(p);
            GUILayout.Space(4);
            Line($"PULSE  {p.PulseText()}", small);
            Line($"BREATHING  {p.BreathingText()}", small);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // ---- circulation
            BeginCard("CIRCULATION", Red);
            Meter("BLOOD", bloodFrac, $"{p.blood / 1000f:0.00} / {Physiology.NormalBlood / 1000f:0.0} L", new Color(0.85f, 0.1f, 0.12f));
            Meter("BLEEDING OUT", totalBleed > 0f ? p.bleedOut / 30f : 0f, $"{p.bleedOut:0.0} mL/s", p.bleedOut > 10f ? Red : p.bleedOut > 2f ? Amber : Muted);
            Meter("BLEEDING INSIDE", p.bleedIn / 30f, $"{p.bleedIn:0.0} mL/s", p.bleedIn > 10f ? Red : p.bleedIn > 2f ? Amber : Muted);
            Meter("SHOCK", p.shock, $"{p.shock * 100f:0}%", p.shock > 0.6f ? Red : p.shock > 0.25f ? Amber : Green);
            Meter("PERFUSION", p.perfusion, $"{p.perfusion * 100f:0}%", Grade(p.perfusion));
            EndCard();

            // ---- respiration and status side by side
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(218));
            BeginCard("RESPIRATION", Blue);
            Meter("OXYGEN", p.spo2, $"{p.spo2 * 100f:0}%", Blue);
            Meter("BRAIN OXYGEN", p.brainO2, $"{p.brainO2 * 100f:0}%", Grade(p.brainO2));
            Line(p.BreathingText(), small);
            EndCard();
            GUILayout.EndVertical();
            GUILayout.Space(6);
            GUILayout.BeginVertical();
            BeginCard("STATE OF MIND", Violet);
            Meter("PAIN", p.EffectivePain, $"{p.EffectivePain * 100f:0}%", p.EffectivePain > 0.6f ? Red : p.EffectivePain > 0.25f ? Amber : Green);
            Meter("FEAR", h.brain.fear, $"{h.brain.fear * 100f:0}%", Violet);
            Meter("ADRENALINE", p.adrenaline, $"{p.adrenaline * 100f:0}%", Amber);
            EndCard();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            BeginCard("BEHAVIOUR", new Color(0.75f, 0.82f, 0.9f));
            Line($"FEELING  {h.brain.FeelingText()}", text);
            Line($"DOING  {h.brain.ActivityText()}", dim);
            Meter("LEFT LEG", p.LegSupport(-1), $"{p.LegSupport(-1) * 100f:0}%", Grade(p.LegSupport(-1)));
            Meter("RIGHT LEG", p.LegSupport(1), $"{p.LegSupport(1) * 100f:0}%", Grade(p.LegSupport(1)));
            Meter("EFFORT", p.Effort, $"{p.Effort * 100f:0}%", new Color(0.75f, 0.82f, 0.9f));
            EndCard();

            // ---- neurological
            BeginCard("NEUROLOGICAL", Amber);
            Line(p.ConsciousText() + (p.posture != PostureType.None ? $"  ·  {p.posture.ToString().ToUpper()} POSTURING" : ""), big);
            Line("BRAIN  " + BrainText(h), dim);
            Line("SPINAL CORD  " + p.SpineText(), dim);
            var skull = h.Head.Find("SKULL");
            if (skull != null) Meter("SKULL", skull.health, $"{skull.health * 100f:0}% INTACT", Grade(skull.health));
            if (p.concussion > 0.1f) Meter("CONCUSSION", p.concussion, $"{p.concussion * 100f:0}%", Amber);
            if (p.brainDamage > 0.02f) Meter("BRAIN DAMAGE", p.brainDamage, $"{p.brainDamage * 100f:0}%", Red);
            var extras = new List<string>();
            if (p.Blind) extras.Add("BLIND");
            else if (p.blindL || p.blindR) extras.Add(p.blindL ? "LEFT EYE BLIND" : "RIGHT EYE BLIND");
            if (p.hemiplegiaL > 0.2f) extras.Add($"LEFT SIDE WEAK {p.hemiplegiaL * 100f:0}%");
            if (p.hemiplegiaR > 0.2f) extras.Add($"RIGHT SIDE WEAK {p.hemiplegiaR * 100f:0}%");
            if (p.ataxia > 0.2f) extras.Add($"ATAXIA {p.ataxia * 100f:0}%");
            if (p.confusion > 0.2f) extras.Add($"CONFUSION {p.confusion * 100f:0}%");
            if (extras.Count > 0) Line(string.Join("  ·  ", extras), new GUIStyle(small) { normal = { textColor = Amber } });
            EndCard();

            // ---- body map and armour
            BeginCard("BODY MAP  ·  ARMOUR", ArmourBlue);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(150));
            BodyMap(h);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(); Legend(new Color(0.28f, 0.72f, 0.4f), "UNHURT"); Legend(new Color(1f, 0.15f, 0.1f), "DESTROYED"); GUILayout.EndVertical();
            GUILayout.BeginVertical(); Legend(ArmourBlue, "ARMOURED"); Legend(new Color(0.16f, 0.17f, 0.19f), "LOST"); GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            int pieces = 0;
            foreach (var bp in h.partList)
                foreach (var ap in bp.GetComponentsInChildren<ArmourPiece>())
                {
                    if (ap.rides) continue;      // pouches and flaps: part of the vest, not armour
                    float wear = 0f; foreach (var wv in ap.wear) wear += wv.w;
                    float cond = Mathf.Clamp01(1f - wear * 0.12f);
                    GUILayout.Label($"{ap.label}", new GUIStyle(tiny) { normal = { textColor = Ink } });
                    Meter($"CLASS {ap.cls:0.0}", cond, ap.wear.Count > 0 ? $"{cond * 100f:0}% · {ap.wear.Count} HIT{(ap.wear.Count == 1 ? "" : "S")}" : $"{cond * 100f:0}%", Grade(cond));
                    GUILayout.Space(2);
                    pieces++;
                }
            if (pieces == 0) Line("NO ARMOUR", dim);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            EndCard();

            // ---- skeleton
            var fx = new List<string>();
            foreach (var bp in h.partList)
            {
                if (bp.severed && bp.joint == null && bp.parent != null) fx.Add($"{bp.Name} — AMPUTATED");
                foreach (var o in bp.organs)
                    if (o.IsBone && (o.Fractured || o.severed)) fx.Add($"{o.def.name}  {o.StatusText()}");
            }
            BeginCard($"FRACTURES & AMPUTATIONS  ·  {fx.Count}", new Color(0.93f, 0.89f, 0.78f));
            if (fx.Count == 0) Line("NO FRACTURES", dim);
            else foreach (var f in fx) Line("•  " + f, text);
            EndCard();

            // ---- wounds
            BeginCard($"WOUNDS  ·  {wounds} IN {regions} REGION{(regions == 1 ? "" : "S")}", Red);
            GUILayout.BeginHorizontal();
            Pill(state, stateCol);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (wounds > 0)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label("REGION", tiny, GUILayout.Width(236));
                GUILayout.Label("WOUNDS", new GUIStyle(tiny) { alignment = TextAnchor.MiddleRight }, GUILayout.Width(46));
                GUILayout.Label("OUT mL/s", new GUIStyle(tiny) { alignment = TextAnchor.MiddleRight }, GUILayout.Width(58));
                GUILayout.Label("IN mL/s", new GUIStyle(tiny) { alignment = TextAnchor.MiddleRight }, GUILayout.Width(54));
                GUILayout.EndHorizontal();
                int row = 0;
                foreach (var bp in h.partList)
                {
                    if (bp.wounds.Count == 0) continue;
                    float dmg = bp.tissueDamage;
                    foreach (var o in bp.organs) dmg = Mathf.Max(dmg, o.Damage * 0.9f);
                    RowBegin(row++);
                    GUILayout.BeginHorizontal();
                    var sw = GUILayoutUtility.GetRect(4, 16, GUILayout.Width(4), GUILayout.Height(16));
                    Fill(new Rect(sw.x, sw.y + 1, 3, 14), DamageColor(dmg));
                    GUILayout.Space(4);
                    GUILayout.Label($"{bp.Name}  —  {DamageWord(dmg)} {dmg * 100f:0}%", rowL, GUILayout.Width(224));
                    GUILayout.Label(bp.wounds.Count.ToString(), rowR, GUILayout.Width(46));
                    GUILayout.Label(bp.OutRate.ToString("0.0"), new GUIStyle(rowR) { normal = { textColor = bp.OutRate > 5f ? Red : Ink } }, GUILayout.Width(58));
                    GUILayout.Label(bp.InRate.ToString("0.0"), new GUIStyle(rowR) { normal = { textColor = bp.InRate > 5f ? Red : Ink } }, GUILayout.Width(54));
                    GUILayout.EndHorizontal();
                    var sb = new StringBuilder();
                    foreach (var wd in bp.wounds)
                    {
                        if (sb.Length > 0) sb.Append("  ·  ");
                        sb.Append(wd.desc).Append(" (").Append(wd.Severity()).Append(')');
                    }
                    GUILayout.Label(sb.ToString(), new GUIStyle(small) { padding = new RectOffset(8, 0, 0, 2) });
                    GUILayout.EndVertical();
                }
            }
            EndCard();

            if (h.lastHitTime > 0f)
            {
                BeginCard($"LAST HIT  ·  {Time.time - h.lastHitTime:0} S AGO", Orange);
                Line(h.lastHit, dim);
                EndCard();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        GUIStyle zebra, plainRow;

        /// <summary>Starts a table row; every other row gets a faint band so the eye can follow it across.</summary>
        void RowBegin(int row)
        {
            if (zebra == null) zebra = new GUIStyle { normal = { background = Rounded(new Color(1f, 1f, 1f, 0.035f), 4) }, border = R(4), padding = new RectOffset(4, 4, 3, 3) };
            if (plainRow == null) plainRow = new GUIStyle { padding = new RectOffset(4, 4, 3, 3) };
            GUILayout.BeginVertical(row % 2 == 0 ? zebra : plainRow);
        }

        static string DamageWord(float d)
        {
            if (d > 0.85f) return "DESTROYED";
            if (d > 0.6f) return "SEVERELY DAMAGED";
            if (d > 0.25f) return "DAMAGED";
            return "INJURED";
        }

        static string BrainText(HumanBody h)
        {
            var list = new List<string>();
            foreach (var o in h.Head.organs)
                if (o.def.kind == TissueKind.Brain && o.Damage > 0.02f) list.Add($"{o.def.name} {o.Damage * 100f:0}%");
            return list.Count == 0 ? "NO INJURY" : string.Join(", ", list);
        }
    }
}
