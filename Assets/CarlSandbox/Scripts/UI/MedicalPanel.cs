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
        GUIStyle bg, header, title, text, dim, big, rowL, rowR, small;
        Texture2D white;

        static readonly Color Orange = new Color(1f, 0.45f, 0.15f);
        static readonly Color Green = new Color(0.45f, 0.95f, 0.5f);
        static readonly Color Red = new Color(1f, 0.25f, 0.2f);
        static readonly Color Blue = new Color(0.35f, 0.75f, 1f);

        public void Select(HumanBody h)
        {
            if (selected != null) selected.forceXray = false;
            selected = h == selected ? null : h;
            if (selected != null) selected.forceXray = true;
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
            portraitCam.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 1f);
            portraitCam.fieldOfView = 30f;
            portraitCam.nearClipPlane = 0.2f;
            portraitCam.farClipPlane = 12f;
            portraitCam.depth = -10;
            portraitCam.enabled = false;
        }

        void LateUpdate()
        {
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

        void InitStyles()
        {
            if (bg != null) return;
            white = Texture2D.whiteTexture;
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.07f, 0.085f, 0.1f, 0.9f));
            bgTex.Apply();
            bg = new GUIStyle { normal = { background = bgTex }, padding = new RectOffset(14, 14, 12, 12) };
            header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Orange }, margin = new RectOffset(0, 0, 8, 1), padding = new RectOffset(0, 0, 0, 0) };
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, padding = new RectOffset(0, 0, 0, 0) };
            text = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.92f, 0.94f, 0.96f) }, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 1, 1), wordWrap = true };
            dim = new GUIStyle(text) { normal = { textColor = new Color(0.6f, 0.65f, 0.7f) } };
            big = new GUIStyle(text) { fontSize = 13, fontStyle = FontStyle.Bold };
            small = new GUIStyle(dim) { fontSize = 10 };
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

        /// <summary>Front-view body map: each region coloured by how damaged it is; armour outlined.</summary>
        void BodyMap(HumanBody h)
        {
            var area = GUILayoutUtility.GetRect(150, 230, GUILayout.Width(150));
            float cx = area.x + 75f, top = area.y + 6f, s = 1.12f;
            void Part(PartId id, float x, float y, float w, float hh)
            {
                var bp = h[id];
                float dmg = bp.tissueDamage;
                foreach (var o in bp.organs) dmg = Mathf.Max(dmg, o.Damage * 0.9f);
                if (bp.wounds.Count > 0) dmg = Mathf.Max(dmg, 0.25f);
                Color c = bp.severed && bp.joint == null && bp.parent != null ? new Color(0.1f, 0.1f, 0.1f, 1f) : Color.Lerp(new Color(0.3f, 0.8f, 0.4f), new Color(1f, 0.15f, 0.1f), dmg);
                var r = new Rect(cx + x * s - w * s / 2f, top + y * s, w * s, hh * s);
                bool armoured = false;
                foreach (Transform t in bp.transform) if (t.GetComponent<ArmourPiece>() != null) { armoured = true; break; }
                if (armoured) { GUI.color = new Color(0.55f, 0.75f, 1f, 0.9f); GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), white); }
                GUI.color = c; GUI.DrawTexture(r, white); GUI.color = Color.white;
            }
            Part(PartId.Head, 0, 0, 22, 26); Part(PartId.Neck, 0, 27, 12, 8);
            Part(PartId.Chest, 0, 36, 46, 38); Part(PartId.Abdomen, 0, 75, 40, 26); Part(PartId.Pelvis, 0, 102, 42, 20);
            Part(PartId.UpperArmL, -30, 38, 11, 36); Part(PartId.ForearmL, -32, 75, 10, 34); Part(PartId.HandL, -33, 110, 10, 12);
            Part(PartId.UpperArmR, 30, 38, 11, 36); Part(PartId.ForearmR, 32, 75, 10, 34); Part(PartId.HandR, 33, 110, 10, 12);
            Part(PartId.ThighL, -11, 123, 16, 44); Part(PartId.ShinL, -11, 168, 13, 38); Part(PartId.FootL, -11, 207, 14, 8);
            Part(PartId.ThighR, 11, 123, 16, 44); Part(PartId.ShinR, 11, 168, 13, 38); Part(PartId.FootR, 11, 207, 14, 8);
        }

        void Bar(float v, Color c)
        {
            var r = GUILayoutUtility.GetRect(10, 5, GUILayout.ExpandWidth(true));
            GUI.color = new Color(1, 1, 1, 0.12f);
            GUI.DrawTexture(r, white);
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), white);
            GUI.color = Color.white;
        }

        void Line(string s, GUIStyle st = null) => GUILayout.Label(s, st ?? text);

        void Header(string s)
        {
            GUILayout.Label(s, header);
            var r = GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true));
            GUI.color = new Color(Orange.r, Orange.g, Orange.b, 0.5f);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (selected == null) return;
            InitStyles();
            var h = selected;
            var p = h.physio;
            float w = 470f;
            float hgt = Mathf.Min(Screen.height - 32f, 900f);
            GUILayout.BeginArea(new Rect(16, 16, w, hgt), bg);
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            GUILayout.BeginHorizontal();
            GUILayout.Label("MEDICAL MONITOR", header);
            GUILayout.FlexibleSpace();
            GUILayout.Label("MIDDLE CLICK — SELECT ANOTHER / CLOSE", small);
            GUILayout.EndHorizontal();
            GUILayout.Label(h.DisplayName, title);
            string life = p.dead ? "DEAD" : p.cardiacArrest ? "CLINICALLY DEAD" : "ALIVE";
            var lifeStyle = new GUIStyle(big) { normal = { textColor = p.dead || p.cardiacArrest ? Red : Green } };
            GUILayout.Label($"{life}  ·  {p.ConsciousText()}", lifeStyle);
            if (p.dead) GUILayout.Label("CAUSE: " + p.causeOfDeath, dim);

            GUILayout.BeginHorizontal();
            // portrait
            GUILayout.BeginVertical(GUILayout.Width(170));
            var pr = GUILayoutUtility.GetRect(170, 290);
            if (rt != null) GUI.DrawTexture(pr, rt, ScaleMode.ScaleAndCrop);
            GUI.color = new Color(1, 1, 1, 0.25f);
            GUI.DrawTexture(new Rect(pr.x, pr.y, pr.width, 1), white);
            GUI.DrawTexture(new Rect(pr.x, pr.yMax - 1, pr.width, 1), white);
            GUI.color = Color.white;
            GUILayout.EndVertical();
            GUILayout.Space(10);

            // vitals column
            GUILayout.BeginVertical();
            Header("STATUS");
            string mode = h.ragdoll.mode == BodyMode.Standing ? (h.ragdoll.moveSpeed > 2f ? "RUNNING" : h.ragdoll.moveSpeed > 0.1f ? "WALKING" : "STANDING") : h.ragdoll.mode == BodyMode.GettingUp ? "GETTING UP" : "DOWN";
            Line($"{p.ConsciousText()}  /  {mode}");
            Line($"FEELING: {h.brain.FeelingText()}", dim);
            Line($"DOING: {h.brain.ActivityText()}", dim);
            Line($"LEG SUPPORT  L {p.LegSupport(-1) * 100f:0}%  R {p.LegSupport(1) * 100f:0}%  ·  EFFORT {p.Effort * 100f:0}%", dim);
            Line($"PAIN {p.EffectivePain * 100f:0}%  ·  ADRENALINE {p.adrenaline * 100f:0}%  ·  FEAR {h.brain.fear * 100f:0}%", dim);

            UpdateEcg(p);
            var er = GUILayoutUtility.GetRect(EcgW * 0.85f, EcgH, GUILayout.ExpandWidth(true));
            GUI.color = new Color(0f, 0.08f, 0.03f, 0.9f); GUI.DrawTexture(er, white); GUI.color = Color.white;
            if (ecg != null) GUI.DrawTexture(er, ecg, ScaleMode.StretchToFill);
            Header("CIRCULATION");
            Bar(p.blood / Physiology.NormalBlood, new Color(0.85f, 0.1f, 0.12f));
            Line($"BLOOD {p.blood / 1000f:0.00} OF {Physiology.NormalBlood / 1000f:0.0} L  ({p.blood / Physiology.NormalBlood * 100f:0}%)", big);
            Line($"BLEEDING {p.bleedOut:0.0} ML/S OUT  ({p.bleedIn:0.0} INSIDE)", dim);
            Line($"PULSE {(p.cardiacArrest ? 0 : p.heartRate):0}/MIN  ·  {p.PulseText()}", dim);
            Line($"BP {p.systolic:0}/{p.diastolic:0}  ·  SHOCK {p.shock * 100f:0}%  ·  PERFUSION {p.perfusion * 100f:0}%", dim);

            Header("RESPIRATION");
            Bar(p.spo2, Blue);
            Line($"OXYGEN {p.spo2 * 100f:0}%  ·  {p.BreathingText()}", big);
            Line($"BRAIN OXYGEN {p.brainO2 * 100f:0}%", dim);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            Header("NEUROLOGICAL");
            Line(p.ConsciousText() + (p.posture != PostureType.None ? $"  ·  {p.posture.ToString().ToUpper()} POSTURING" : ""), big);
            Line("BRAIN: " + BrainText(h), dim);
            Line("SPINAL CORD: " + p.SpineText(), dim);
            var skull = h.Head.Find("SKULL");
            var extras = new List<string>();
            if (skull != null) extras.Add($"SKULL {skull.health * 100f:0}% INTACT");
            if (p.concussion > 0.1f) extras.Add($"CONCUSSION {p.concussion * 100f:0}%");
            if (p.Blind) extras.Add("BLIND");
            else if (p.blindL || p.blindR) extras.Add(p.blindL ? "LEFT EYE BLIND" : "RIGHT EYE BLIND");
            if (p.hemiplegiaL > 0.2f) extras.Add($"LEFT SIDE WEAK {p.hemiplegiaL * 100f:0}%");
            if (p.hemiplegiaR > 0.2f) extras.Add($"RIGHT SIDE WEAK {p.hemiplegiaR * 100f:0}%");
            if (p.ataxia > 0.2f) extras.Add($"ATAXIA {p.ataxia * 100f:0}%");
            if (p.confusion > 0.2f) extras.Add($"CONFUSION {p.confusion * 100f:0}%");
            if (p.brainDamage > 0.02f) extras.Add($"BRAIN DAMAGE {p.brainDamage * 100f:0}%");
            Line(string.Join("  ·  ", extras), dim);

            Header("BODY MAP  ·  ARMOUR");
            GUILayout.BeginHorizontal();
            BodyMap(h);
            GUILayout.BeginVertical();
            var arm = new List<string>();
            foreach (var bp in h.partList)
                foreach (var ap in bp.GetComponentsInChildren<ArmourPiece>())
                {
                    float wear = 0f; foreach (var wv in ap.wear) wear += wv.w;
                    float cond = Mathf.Clamp01(1f - wear * 0.12f);
                    arm.Add($"{ap.label}  ·  CLASS {ap.cls:0.0}  ·  {cond * 100f:0}%{(ap.wear.Count > 0 ? $"  ({ap.wear.Count} HITS)" : "")}");
                }
            Line(arm.Count == 0 ? "NO ARMOUR" : string.Join("\n", arm), arm.Count == 0 ? dim : small);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            Header("STRUCTURAL");
            var fx = new List<string>();
            foreach (var bp in h.partList)
            {
                if (bp.severed && bp.joint == null && bp.parent != null) fx.Add($"{bp.Name} — AMPUTATED");
                foreach (var o in bp.organs)
                    if (o.IsBone && (o.Fractured || o.severed)) fx.Add($"{o.def.name}  {o.StatusText()}");
            }
            Line(fx.Count == 0 ? "NO FRACTURES" : string.Join("\n", fx), fx.Count == 0 ? dim : text);

            // wounds by region
            int wounds = 0, regions = 0; bool arterial = false; float worst = 0f;
            foreach (var bp in h.partList)
            {
                if (bp.wounds.Count == 0) continue;
                regions++; wounds += bp.wounds.Count;
                foreach (var wd in bp.wounds) { arterial |= wd.arterial && wd.extRate > 2f; worst = Mathf.Max(worst, wd.extRate + wd.intRate); }
            }
            Header($"WOUNDS  ·  {wounds} IN {regions} REGION{(regions == 1 ? "" : "S")}");
            string state = p.dead ? "DECEASED" : arterial ? "CRITICAL — ARTERIAL BLEED" : p.bleedOut + p.bleedIn > 15f ? "CRITICAL — MASSIVE HAEMORRHAGE" : p.bleedOut + p.bleedIn > 4f ? "SERIOUS — BLEEDING" : wounds > 0 ? "STABLE" : "UNINJURED";
            Line($"{state}{(wounds > 0 ? $" — {wounds} WOUND{(wounds == 1 ? "" : "S")}" : "")}", dim);
            if (wounds > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("", rowL, GUILayout.Width(250));
                GUILayout.Label("WOUNDS", small, GUILayout.Width(52));
                GUILayout.Label("OUT mL/s", small, GUILayout.Width(62));
                GUILayout.Label("IN mL/s", small, GUILayout.Width(55));
                GUILayout.EndHorizontal();
                foreach (var bp in h.partList)
                {
                    if (bp.wounds.Count == 0) continue;
                    float dmg = bp.tissueDamage;
                    foreach (var o in bp.organs) dmg = Mathf.Max(dmg, o.Damage * 0.9f);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{bp.Name} — {DamageWord(dmg)} ({dmg * 100f:0}%)", rowL, GUILayout.Width(250));
                    GUILayout.Label(bp.wounds.Count.ToString(), rowR, GUILayout.Width(40));
                    GUILayout.Label(bp.OutRate.ToString("0.0"), rowR, GUILayout.Width(62));
                    GUILayout.Label(bp.InRate.ToString("0.0"), rowR, GUILayout.Width(55));
                    GUILayout.EndHorizontal();
                    var sb = new StringBuilder();
                    foreach (var wd in bp.wounds)
                    {
                        if (sb.Length > 0) sb.Append("  ·  ");
                        sb.Append(wd.desc).Append(" (").Append(wd.Severity()).Append(')');
                    }
                    GUILayout.Label(sb.ToString(), small);
                }
            }

            if (h.lastHitTime > 0f)
            {
                Header($"LAST HIT  ·  {Time.time - h.lastHitTime:0} S AGO");
                Line(h.lastHit, dim);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
