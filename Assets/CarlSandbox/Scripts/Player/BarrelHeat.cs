using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Your guns heat up as you fire them and cool when you stop. Hot, the barrel glows (dark red, then orange)
    /// and smokes, rounds spread wider, and the gun starts to jam - rarely when it's cool, more and more as it
    /// nears its limit. A jammed gun only clicks: press J to clear it. What's wrong decides how much work that is:
    ///   STOVEPIPE         a spent case caught in the ejection port - one rack (J)
    ///   FAILURE TO FEED   the round didn't make it into the chamber - one rack, the round is lost
    ///   DOUBLE FEED       two rounds fighting for the chamber - strip the mag, rack, reseat (J three times)
    ///   STUCK CASE        a case swollen in a hot chamber - work it out (J four times); only when very hot
    /// The HEAT % next to your ammo is there so you can choose to stop spraying and make the shots count.
    /// </summary>
    public class BarrelHeat : MonoBehaviour
    {
        public static BarrelHeat I;
        public static void Attach(GameObject g) => I = g.AddComponent<BarrelHeat>();

        enum Jam { None, Stovepipe, FailureToFeed, DoubleFeed, StuckCase }

        readonly float[] heat = new float[16];
        readonly float[] lastShot = new float[16];
        readonly Jam[] jam = new Jam[16];
        readonly int[] stepsLeft = new int[16];
        float stepBusyUntil, lastClick, smokeAt;
        GameObject glow;
        Material glowMat;
        Transform glowOn;
        GUIStyle big, small;

        static WeaponController W => SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;

        // ------------------------------------------------------------ the numbers

        /// <summary>How much one round heats the barrel (1 = as hot as it gets).</summary>
        static float PerShot(WeaponDef d)
        {
            switch (d.name)
            {
                case "AK-47": return 0.022f;
                case "AR-15": return 0.018f;
                case "GLOCK 17": return 0.016f;
                case "BERETTA M9": return 0.016f;
                case "MOSSBERG 590": return 0.05f;
                case "AWP": return 0.045f;
                case ".50 CAL": return 0.06f;
                default: return d.name == "TASER" ? 0f : 0.02f;
            }
        }

        /// <summary>Chance a round jams, by how hot the barrel is: next to nothing cool, likely at the limit.</summary>
        public static float JamChance(float h) => 0.0004f + 0.012f * h * h * h + (h > 0.92f ? 0.06f : 0f);

        public float Heat(int idx) => idx >= 0 && idx < heat.Length ? heat[idx] : 0f;
        public bool Jammed(int idx) => idx >= 0 && idx < jam.Length && jam[idx] != Jam.None;
        public string JamName(int idx) => Jammed(idx) ? jam[idx].ToString() : "";
        public bool Glowing => glow != null && glow.activeInHierarchy;

        /// <summary>For the playtests: jams, dead clicks, and a jam of a given kind (1-4) or a set heat.</summary>
        public int Jams, DeadClicks;
        public void ForceJam(int idx, int kind) { jam[idx] = (Jam)kind; stepsLeft[idx] = jam[idx] == Jam.StuckCase ? 4 : jam[idx] == Jam.DoubleFeed ? 3 : 1; }
        /// <summary>A new map: every gun cool and clear.</summary>
        public void Clear() { for (int i = 0; i < heat.Length; i++) { heat[i] = 0f; jam[i] = Jam.None; stepsLeft[i] = 0; } }
        public void SetHeat(int idx, float h) { heat[idx] = h; lastShot[idx] = Time.time; }

        /// <summary>A hot barrel throws rounds wider.</summary>
        public float SpreadMul(int idx) => 1f + Heat(idx) * 0.8f;

        // ------------------------------------------------------------ firing, clearing

        /// <summary>A round has gone: the barrel takes the heat, and may jam on the next.</summary>
        public void Shot(int idx, WeaponDef d)
        {
            if (idx < 0 || idx >= heat.Length) return;
            float add = PerShot(d);
            if (add <= 0f) return;
            heat[idx] = Mathf.Min(1f, heat[idx] + add);
            lastShot[idx] = Time.time;
            if (Random.value >= JamChance(heat[idx])) return;
            // what went wrong: cool, it's nearly always a case or a round that didn't make it; hot, the bad ones
            float h = heat[idx], r = Random.value;
            Jam j = h > 0.8f && r < 0.3f ? Jam.StuckCase : h > 0.5f && r < 0.55f ? Jam.DoubleFeed : r < 0.6f ? Jam.Stovepipe : Jam.FailureToFeed;
            jam[idx] = j;
            Jams++;
            stepsLeft[idx] = j == Jam.StuckCase ? 4 : j == Jam.DoubleFeed ? 3 : 1;
            Sfx.Play("click_empty", Ears(), 0.7f, 0f, 1f, 0.05f);
        }

        /// <summary>Pulling the trigger on a jammed gun: a dead click.</summary>
        public void DeadClick()
        {
            if (Time.unscaledTime - lastClick < 0.18f) return;
            lastClick = Time.unscaledTime;
            DeadClicks++;
            Sfx.Play("click_empty", Ears(), 0.6f, 0f, 1f, 0.08f);
        }

        /// <summary>J: one step of clearing it (a rack, stripping the mag, seating it again).</summary>
        public void ClearStep(int idx, WeaponController w)
        {
            if (!Jammed(idx) || Time.unscaledTime < stepBusyUntil) return;
            stepBusyUntil = Time.unscaledTime + 0.35f;
            stepsLeft[idx]--;
            // the mag out, the mag back in, and the bolt racked to finish
            Sfx.Play(stepsLeft[idx] == 0 ? "reload_bolt" : stepsLeft[idx] % 2 == 0 ? "reload_magout" : "reload_magin", Ears(), 0.7f, 0f, 1f, 0.05f);
            if (stepsLeft[idx] > 0) return;
            // cleared: what was in the way is gone (a failure to feed throws out the round, a double feed two)
            int lost = jam[idx] == Jam.FailureToFeed ? 1 : jam[idx] == Jam.DoubleFeed ? 2 : 0;
            if (lost > 0 && w != null) w.LoseRounds(idx, lost);
            jam[idx] = Jam.None;
            SandboxGame.Instance?.Toast("CLEARED");
        }

        static Vector3 Ears() => Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        // ------------------------------------------------------------ cooling, the glow

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < heat.Length; i++)
            {
                if (heat[i] <= 0f) continue;
                // it cools all the time, much faster once you stop firing and the hotter it is
                bool resting = Time.time - lastShot[i] > 0.4f;
                heat[i] = Mathf.Max(0f, heat[i] - dt * (resting ? 0.03f + 0.11f * heat[i] : 0.015f));
            }
            var w = W;
            float h = w != null && w.Armed && w.Current.type == WeaponType.Gun ? Heat(w.current) : 0f;
            // a hot barrel smokes
            if (h > 0.45f && glowOn != null && Time.time > smokeAt && FpvDrone.Piloting == null)
            {
                smokeAt = Time.time + Mathf.Lerp(0.5f, 0.12f, (h - 0.45f) / 0.55f);
                Haze.Puff(glowOn.position + glowOn.forward * 0.02f, Vector3.up * 0.35f + Random.insideUnitSphere * 0.05f, 0.04f + 0.05f * h, 0.12f + 0.15f * h, new Color(0.7f, 0.7f, 0.68f), true);
            }
        }

        /// <summary>Called by the weapon each frame with the barrel's muzzle (null when there's no gun out).</summary>
        public void UpdateGlow(Transform muzzle, int idx, bool pistol)
        {
            float h = muzzle != null ? Heat(idx) : 0f;
            if (glow == null)
            {
                glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                glow.name = "BarrelGlow";
                var c = glow.GetComponent<Collider>(); c.enabled = false; Destroy(c);
                glowMat = new Material(Mats.Unlit(new Color(1f, 0.25f, 0.05f, 1f), true));
                var mr = glow.GetComponent<MeshRenderer>();
                mr.sharedMaterial = glowMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            // dull red from about a third hot, brighter and more orange toward the limit
            float k = Mathf.InverseLerp(0.3f, 1f, h);
            bool on = muzzle != null && k > 0f;
            glow.SetActive(on);
            if (!on) { glowOn = muzzle; return; }
            if (glow.transform.parent != muzzle)
            {
                glow.transform.SetParent(muzzle, false);
                glow.layer = muzzle.gameObject.layer;
                float len = pistol ? 0.07f : 0.3f, rad = pistol ? 0.012f : 0.0135f;
                glow.transform.localPosition = new Vector3(0f, 0f, -len * 0.5f - 0.005f);
                glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                glow.transform.localScale = new Vector3(rad * 2f, len * 0.5f, rad * 2f);
            }
            glowOn = muzzle;
            var col = Color.Lerp(new Color(0.35f, 0.02f, 0f), new Color(1f, 0.45f, 0.08f), k * k);
            col.a = Mathf.Lerp(0.15f, 0.85f, k);
            glowMat.SetColor("_Color", col);
        }

        // ------------------------------------------------------------ what you see

        void OnGUI()
        {
            var w = W;
            if (w == null || !w.Armed || w.Current.type != WeaponType.Gun || FpvDrone.Piloting != null) return;
            if (Menus.Blocking && !SquadCommand.Open) return;
            if (big == null)
            {
                big = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
                small = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            }
            GUI.depth = -40;
            float SW = Screen.width, H = Screen.height;
            int idx = w.current;
            float h = Heat(idx);
            if (h > 0.01f)
            {
                // the number: white when cool, orange, then red as it nears the limit
                var c = h < 0.5f ? Color.Lerp(new Color(0.85f, 0.87f, 0.9f), new Color(1f, 0.6f, 0.15f), h * 2f) : Color.Lerp(new Color(1f, 0.6f, 0.15f), new Color(1f, 0.15f, 0.1f), (h - 0.5f) * 2f);
                small.normal.textColor = c;
                GUI.Label(new Rect(SW - 330, H - 124, 300, 22), $"HEAT {h * 100f:0}%" + (h > 0.85f ? "  ·  JAM RISK HIGH" : h > 0.6f ? "  ·  EASE OFF" : ""), small);
            }
            if (Jammed(idx))
            {
                string what = jam[idx] == Jam.Stovepipe ? "STOVEPIPE" : jam[idx] == Jam.FailureToFeed ? "FAILURE TO FEED" : jam[idx] == Jam.DoubleFeed ? "DOUBLE FEED" : "STUCK CASE";
                int total = jam[idx] == Jam.StuckCase ? 4 : jam[idx] == Jam.DoubleFeed ? 3 : 1;
                float flash = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);
                big.normal.textColor = new Color(1f, 0.25f, 0.15f, flash);
                string steps = total > 1 ? $"  ({total - stepsLeft[idx] + 1}/{total})" : "";
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(SW / 2 - 330, H * 0.64f - 6, 660, 40), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(SW / 2 - 330, H * 0.64f - 6, 660, 40), $"GUN JAMMED: {what}  ·  PRESS <b>J</b> TO CLEAR{steps}", big);
            }
        }
    }
}
