using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// You, as a body that can be hurt the same way an Earl can: where a round lands decides what it destroys.
    /// Eyes (blind on that side of the screen), brain regions (each one feels different), the cervical / thoracic /
    /// lumbar spine (paralysed below the lesion), carotids, heart, lungs, liver, femoral arteries, arms and legs
    /// (shot off: one-handed shooting, falling over). Blood loss darkens the screen until you pass out.
    /// Hits are mapped from where they land on the player's capsule.
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        public static PlayerBody I;
        public const float NormalBlood = 5000f;

        public float blood = NormalBlood, pain, bleed;
        public bool dead, passedOut;
        public string cause = "";
        // eyes and brain (0..1 damage)
        public bool blindL, blindR;
        public float frontal, occipital, temporal, cerebellum, motorL, motorR;
        // limbs: function 0..1, lost = shot / blown off
        public float armL = 1f, armR = 1f, legL = 1f, legR = 1f;
        public bool armLostL, armLostR, legLostL, legLostR;
        /// <summary>0 none, 1 cervical (nothing below the neck works), 2 thoracic (legs), 3 lumbar (legs weak).</summary>
        public int spine;
        public bool lungHit;

        readonly List<string> injuries = new List<string>();
        float hitFlash, whiteFlash, nextFlash, deadTime;
        AudioSource ringing;
        Texture2D vignette, softEdge, blob, white;
        CharacterController cc;

        public static PlayerBody Attach(GameObject player)
        {
            I = player.AddComponent<PlayerBody>();
            I.cc = player.GetComponent<CharacterController>();
            return I;
        }

        // ------------------------------------------------------------ what still works

        /// <summary>Is this arm (-1 left, +1 right) still able to hold a gun? The motor cortex on the other side drives it.</summary>
        public bool ArmUsable(int side)
        {
            if (dead || passedOut || spine == 1) return false;
            return side < 0 ? !armLostL && armL > 0.25f && motorR < 0.6f : !armLostR && armR > 0.25f && motorL < 0.6f;
        }
        public int UsableArms => (ArmUsable(-1) ? 1 : 0) + (ArmUsable(1) ? 1 : 0);
        bool LegUsable(int side) => side < 0 ? !legLostL && legL > 0.3f : !legLostR && legR > 0.3f;
        /// <summary>Can you get up and stay up?</summary>
        public bool CanStand => !dead && !passedOut && spine != 1 && spine != 2 && LegUsable(-1) && LegUsable(1) && Consciousness > 0.4f;
        public float Consciousness => dead ? 0f : passedOut ? 0f : Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.75f, blood / NormalBlood) - frontal * 0.2f);
        /// <summary>How much slower you move (legs hurt, blood loss).</summary>
        public float MoveScale => Mathf.Clamp01(Mathf.Min(legL, legR) * 0.7f + 0.3f) * (spine == 3 ? 0.5f : 1f) * Mathf.Lerp(0.5f, 1f, Consciousness);
        /// <summary>Shaking hands: injured arms, pain, blood loss, a damaged cerebellum.</summary>
        public float Tremor => Mathf.Clamp01((2f - armL - armR) * 0.35f + pain * 0.25f + (1f - blood / NormalBlood) * 1.2f + cerebellum * 0.8f);
        /// <summary>The world swims: cerebellum damage and blood loss.</summary>
        public float Wobble => Mathf.Clamp01(cerebellum + Mathf.InverseLerp(0.8f, 0.5f, blood / NormalBlood) * 0.6f);
        public bool Paralysed => spine == 1;

        // ------------------------------------------------------------ getting hurt

        /// <summary>A round, fragment or blade reaches you. point/dir world space; energy J.</summary>
        public void Hit(Vector3 point, Vector3 dir, float energy, HitKind kind, float diameter = 0.008f)
        {
            if (dead) return;
            float scale = cc != null ? 1.8f / Mathf.Max(0.4f, cc.height) : 1f;
            Vector3 l = transform.InverseTransformPoint(point);
            float h = l.y * scale, x = l.x;
            bool fromBack = Vector3.Dot(dir, transform.forward) > 0.2f;
            bool fromSide = Mathf.Abs(Vector3.Dot(dir, transform.right)) > 0.7f;
            bool centre = Mathf.Abs(x) < 0.06f;
            float e = Mathf.Clamp(energy / 500f, 0.05f, 12f);            // a 9 mm round ~ 1
            if (kind == HitKind.Blunt) e *= 0.4f;
            int side = x < 0f ? -1 : 1;
            string sn = side < 0 ? "LEFT " : "RIGHT ";
            hitFlash = Mathf.Min(1f, hitFlash + 0.35f + e * 0.1f);
            pain = Mathf.Min(1f, pain + 0.2f + e * 0.12f);
            Sfx.Play("impact_flesh", point, 0.9f, 0f);
            BloodSystem.Instance?.Spray(point, -dir + Random.insideUnitSphere * 0.3f, Mathf.RoundToInt(6 + e * 4), 1.5f, 4f, 0.6f, 0.006f, null);

            if (h > 1.58f)
            {
                // ---- head
                if (!fromBack && !fromSide && h > 1.6f && h < 1.7f && Mathf.Abs(x) > 0.015f && Mathf.Abs(x) < 0.06f)
                {
                    if (side < 0 && !blindL) { blindL = true; Note("LEFT EYE DESTROYED - BLIND ON THE LEFT"); }
                    else if (side > 0 && !blindR) { blindR = true; Note("RIGHT EYE DESTROYED - BLIND ON THE RIGHT"); }
                    frontal += 0.15f * e;
                }
                else if (fromBack && h < 1.66f && centre) { if (e > 0.6f) Die("BRAINSTEM DESTROYED"); else cerebellum += 0.5f * e; Note("CEREBELLUM / BRAINSTEM"); }
                else if (fromBack) { occipital += 0.45f * e; Note("OCCIPITAL LOBE - VISION FAILING"); }
                else if (fromSide) { temporal += 0.45f * e; if (side < 0) motorL += 0.35f * e; else motorR += 0.35f * e; Note(sn + "TEMPORAL / MOTOR CORTEX"); }
                else { frontal += 0.45f * e; Note("FRONTAL LOBE"); }
                bleed += 2f * e;
                // a big round through the head is the end of it
                if (e > 2.5f || (frontal + occipital + temporal + cerebellum) > 1.6f) Die("MASSIVE HEAD WOUND");
            }
            else if (h > 1.45f)
            {
                // ---- neck
                if (fromBack && centre || e > 3f && centre) { spine = 1; Note("CERVICAL SPINE - PARALYSED FROM THE NECK DOWN"); }
                else if (!centre) { bleed += 22f * Mathf.Min(1f, e); Note(sn + "CAROTID ARTERY - SPURTING"); }
                else { bleed += 4f * e; Note("THROAT"); }
            }
            else if (h > 0.98f)
            {
                if (Mathf.Abs(x) > 0.19f) { Arm(side, e, kind); }
                else if (fromBack && centre) { if (spine != 1) spine = 2; Note("THORACIC SPINE - LEGS PARALYSED"); }
                else if (x < 0f && x > -0.08f && !fromBack) { bleed += 50f * Mathf.Min(1f, e); Note("HEART"); if (e > 1.5f) Die("HEART DESTROYED"); }
                else { bleed += 6f * e; lungHit = true; Note(sn + "LUNG - COUGHING BLOOD"); }
            }
            else if (h > 0.8f)
            {
                if (fromBack && centre) { if (spine == 0) spine = 3; Note("LUMBAR SPINE - LEGS WEAK"); }
                else { bleed += (x > 0f ? 10f : 7f) * e; Note(x > 0f ? "LIVER" : "SPLEEN / BOWEL"); }
            }
            else Leg(side, e, kind, h);
            // overall shock from the blow
            blood -= 15f * e;
        }

        void Arm(int s, float e, HitKind kind)
        {
            string sn = s < 0 ? "LEFT " : "RIGHT ";
            if (s < 0) armL = Mathf.Max(0f, armL - 0.35f * e); else armR = Mathf.Max(0f, armR - 0.35f * e);
            bleed += 3f * e;
            if (Random.value < 0.25f * e) { bleed += 8f; Note(sn + "BRACHIAL ARTERY"); }
            bool off = e > 4f || (kind == HitKind.Slash && e > 1.6f) || (s < 0 ? armL : armR) <= 0f && e > 1.2f;
            if (off) { if (s < 0) armLostL = true; else armLostR = true; bleed += 10f; Note(sn + "ARM TORN OFF - SHOOTING ONE-HANDED"); }
            else Note(sn + "ARM HIT");
        }

        void Leg(int s, float e, HitKind kind, float h)
        {
            string sn = s < 0 ? "LEFT " : "RIGHT ";
            if (s < 0) legL = Mathf.Max(0f, legL - 0.4f * e); else legR = Mathf.Max(0f, legR - 0.4f * e);
            bleed += 3f * e;
            if (h > 0.45f && Random.value < 0.3f * e) { bleed += 20f; Note(sn + "FEMORAL ARTERY"); }
            bool off = e > 4f || (kind == HitKind.Slash && e > 1.8f);
            if (off) { if (s < 0) legLostL = true; else legLostR = true; bleed += 15f; Note(sn + "LEG GONE"); }
            else Note(sn + (h > 0.45f ? "THIGH" : "SHIN") + " HIT");
        }

        /// <summary>A grenade going off near you.</summary>
        public void Blast(Vector3 at, float intensity)
        {
            if (dead || intensity <= 0.02f) return;
            hitFlash = 1f;
            whiteFlash = Mathf.Max(whiteFlash, intensity);
            temporal = Mathf.Min(1f, temporal + intensity * 0.3f);       // ears
            pain = Mathf.Min(1f, pain + intensity);
            Vector3 l = transform.InverseTransformPoint(at);
            int side = l.x < 0f ? -1 : 1;
            if (intensity > 0.45f)
            {
                // the limb nearest to it
                if (l.y < 0.6f) Leg(side, intensity * 5f, HitKind.Blast, 0.3f); else Arm(side, intensity * 4f, HitKind.Blast);
                bleed += 20f * intensity;
            }
            if (intensity > 0.85f) Die("BLOWN APART");
        }

        void Note(string s) { injuries.Remove(s); injuries.Add(s); if (injuries.Count > 7) injuries.RemoveAt(0); }

        public void Die(string why)
        {
            if (dead) return;
            dead = true; cause = why; deadTime = Time.time;
            Note(why);
        }

        public void Revive()
        {
            blood = NormalBlood; pain = bleed = 0f; dead = passedOut = false; cause = "";
            blindL = blindR = false; frontal = occipital = temporal = cerebellum = motorL = motorR = 0f;
            armL = armR = legL = legR = 1f; armLostL = armLostR = legLostL = legLostR = false; spine = 0; lungHit = false;
            injuries.Clear(); hitFlash = whiteFlash = 0f;
            AudioListener.volume = 1f;
        }

        // ------------------------------------------------------------ over time

        string lastHitSeen = "";

        /// <summary>
        /// With an Earl body (PlayerAvatar) everything comes from it: its blood, its eyes, which parts of its brain
        /// are damaged, where its spine is cut, which limbs it still has. This class just shows what that is like.
        /// </summary>
        void SyncFromBody(HumanBody h)
        {
            var p = h.physio;
            blood = p.blood / Physiology.NormalBlood * NormalBlood;
            bleed = p.bleedOut + p.bleedIn;
            pain = p.EffectivePain;
            if (p.dead && !dead) Die(string.IsNullOrEmpty(p.causeOfDeath) ? "DEAD" : p.causeOfDeath);
            passedOut = !dead && p.consciousness < 0.25f;
            blindL = p.blindL; blindR = p.blindR;
            frontal = occipital = temporal = cerebellum = motorL = motorR = 0f;
            foreach (var o in h.Head.organs)
            {
                float d = o.Damage;
                switch (o.def.brain)
                {
                    case BrainRegion.Frontal: frontal = d; break;
                    case BrainRegion.Occipital: occipital = d; break;
                    case BrainRegion.TemporalLeft: case BrainRegion.TemporalRight: temporal = Mathf.Max(temporal, d); break;
                    case BrainRegion.MotorLeft: motorL = d; break;
                    case BrainRegion.MotorRight: motorR = d; break;
                    case BrainRegion.Cerebellum: cerebellum = d; break;
                }
            }
            frontal = Mathf.Max(frontal, p.concussion * 0.5f);
            spine = p.lesion == SpineLevel.HighCervical || p.lesion == SpineLevel.LowCervical ? 1 : p.lesion == SpineLevel.Thoracic ? 2 : p.lesion != SpineLevel.None ? 3 : 0;
            bool Lost(PartId a, PartId b, PartId c) => h[a] == null || h[a].severed || h[b] == null || h[b].severed || h[c] == null || h[c].severed;
            armLostL = Lost(PartId.UpperArmL, PartId.ForearmL, PartId.HandL);
            armLostR = Lost(PartId.UpperArmR, PartId.ForearmR, PartId.HandR);
            legLostL = Lost(PartId.ThighL, PartId.ShinL, PartId.FootL);
            legLostR = Lost(PartId.ThighR, PartId.ShinR, PartId.FootR);
            armL = p.ArmUsable(-1) ? 1f - Mathf.Max(h[PartId.UpperArmL]?.tissueDamage ?? 1f, h[PartId.ForearmL]?.tissueDamage ?? 1f) * 0.8f : 0f;
            armR = p.ArmUsable(1) ? 1f - Mathf.Max(h[PartId.UpperArmR]?.tissueDamage ?? 1f, h[PartId.ForearmR]?.tissueDamage ?? 1f) * 0.8f : 0f;
            legL = p.LegSupport(-1); legR = p.LegSupport(1);
            if (h.lastHit != lastHitSeen && !string.IsNullOrEmpty(h.lastHit)) { lastHitSeen = h.lastHit; Note(h.lastHit.Length > 80 ? h.lastHit.Substring(0, 80) : h.lastHit); hitFlash = 1f; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (PlayerAvatar.Body != null) SyncFromBody(PlayerAvatar.Body);
            else if (!dead)
            {
                blood -= bleed * GameSettings.BloodMult * 0.5f * dt;
                // small bleeds clot, arteries don't
                bleed = Mathf.Max(0f, bleed - dt * (bleed < 12f ? 0.25f : 0.05f));
                if (bleed < 0.5f) blood = Mathf.Min(NormalBlood, blood + dt * 2f);
                pain = Mathf.Max(0f, pain - dt * 0.05f);
                if (lungHit && Random.value < dt * 0.3f) Sfx.Play("cough", transform.position + Vector3.up * 1.6f, 0.4f, 0f);
                float frac = blood / NormalBlood;
                passedOut = frac < 0.45f;
                if (frac < 0.3f) Die("BLED OUT");
            }
            hitFlash = Mathf.Max(0f, hitFlash - dt * 1.8f);
            whiteFlash = Mathf.Max(0f, whiteFlash - dt * 1.2f);
            // frontal lobe: sudden white-outs every so often
            if (frontal > 0.15f && Time.time > nextFlash) { whiteFlash = Mathf.Max(whiteFlash, Mathf.Min(1f, frontal)); nextFlash = Time.time + Random.Range(2f, 8f) / Mathf.Max(0.3f, frontal); }
            // temporal lobe and the ears: muffled, ringing
            AudioListener.volume = GameSettings.Volume <= 0f ? 0f : Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(temporal));
            Ring(Mathf.Clamp01(frontal * 0.6f + temporal * 0.8f + whiteFlash * 0.5f));
            if (dead && Time.time - deadTime > 1.5f && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
            {
                Revive();
                var pc = SandboxGame.Instance != null ? SandboxGame.Instance.player : null;
                if (pc != null)
                {
                    pc.down = false;          // dying left you lying down: start again on your feet
                    pc.Teleport(SandboxGame.Instance.SpawnPoint, 0f);
                }
            }
        }

        /// <summary>A high whine in the ears, as loud as the damage.</summary>
        void Ring(float amount)
        {
            if (ringing == null)
            {
                var src = new GameObject("Tinnitus").AddComponent<AudioSource>();
                src.transform.SetParent(transform, false);
                const int rate = 44100;
                var clip = AudioClip.Create("tinnitus", rate, 1, rate, false);
                var d = new float[rate];
                for (int i = 0; i < rate; i++) d[i] = Mathf.Sin(i * 2f * Mathf.PI * 4200f / rate) * 0.25f + Mathf.Sin(i * 2f * Mathf.PI * 4237f / rate) * 0.15f;
                clip.SetData(d, 0);
                src.clip = clip; src.loop = true; src.spatialBlend = 0f; src.volume = 0f; src.ignoreListenerVolume = true;
                src.Play();
                ringing = src;
            }
            ringing.volume = amount * 0.35f * GameSettings.Volume;
        }

        // ------------------------------------------------------------ what you see

        Texture2D Make(int size, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = new Color(1, 1, 1, alpha(x / (size - 1f), y / (size - 1f)));
            t.SetPixels(px); t.Apply();
            return t;
        }

        void Fill(Rect r, Texture t, Color c) { GUI.color = c; GUI.DrawTexture(r, t, ScaleMode.StretchToFill); }

        void OnGUI()
        {
            if (vignette == null)
            {
                vignette = Make(128, (u, v) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((new Vector2(u - 0.5f, v - 0.5f).magnitude - 0.25f) / 0.45f)));
                softEdge = Make(64, (u, v) => Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.75f, 1f, u)));
                blob = Make(64, (u, v) => Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f)));
                white = Texture2D.whiteTexture;
            }
            GUI.depth = -50;
            float W = Screen.width, H = Screen.height;
            var all = new Rect(0, 0, W, H);
            float frac = blood / NormalBlood;
            // blood loss: the world darkens and closes in, grey and cold
            float loss = Mathf.Clamp01(Mathf.InverseLerp(0.95f, 0.45f, frac));
            Fill(all, white, new Color(0.35f, 0.35f, 0.38f, loss * 0.35f));
            Fill(new Rect(-W * loss * 0.3f, -H * loss * 0.3f, W * (1f + loss * 0.6f), H * (1f + loss * 0.6f)), vignette, new Color(0, 0, 0, loss));
            // pain: red edges when hit
            Fill(all, vignette, new Color(0.6f, 0f, 0f, hitFlash * 0.7f + pain * 0.15f));
            // occipital: tunnel vision and dark blind spots
            if (occipital > 0.02f)
            {
                float o = Mathf.Clamp01(occipital);
                Fill(new Rect(W * 0.1f * o - W * 0.2f, H * 0.1f * o - H * 0.2f, W * (1.4f - 0.2f * o), H * (1.4f - 0.2f * o)), vignette, new Color(0, 0, 0, o));
                for (int k = 0; k < 5; k++)
                {
                    float sx = Mathf.PerlinNoise(k * 3.1f, 0.5f) * W, sy = Mathf.PerlinNoise(0.5f, k * 2.7f) * H, sr = H * 0.35f * o;
                    Fill(new Rect(sx - sr, sy - sr, sr * 2f, sr * 2f), blob, new Color(0, 0, 0, Mathf.Clamp01(o * 1.2f)));
                }
            }
            // a destroyed eye: that half of the world is gone
            if (blindL) Fill(new Rect(0, 0, W * 0.55f, H), softEdge, Color.black);
            if (blindR) { GUIUtility.RotateAroundPivot(180f, new Vector2(W * 0.5f, H * 0.5f)); Fill(new Rect(0, 0, W * 0.55f, H), softEdge, Color.black); GUI.matrix = Matrix4x4.identity; }
            // frontal lobe: colours slide about, everything flares white
            if (frontal > 0.02f)
            {
                float f = Mathf.Clamp01(frontal);
                var tint = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.15f, 1f), 0.8f, 1f); tint.a = 0.16f * f;
                Fill(all, white, tint);
            }
            Fill(all, white, new Color(1f, 1f, 0.97f, whiteFlash * 0.85f));
            // passing out / dead
            if (passedOut || dead) Fill(all, white, new Color(0, 0, 0, dead ? Mathf.Clamp01((Time.time - deadTime) * 1.5f) * 0.92f : 0.97f));
            GUI.color = Color.white;
            var st = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.35f, 0.3f) } };
            // the health panel (H) lists them properly and sits where these would be
            bool panelOpen = PlayerAvatar.Body != null && SandboxGame.Instance != null && SandboxGame.Instance.medical != null && SandboxGame.Instance.medical.selected == PlayerAvatar.Body;
            for (int i = 0; i < injuries.Count && !panelOpen; i++) GUI.Label(new Rect(18, H - 120 - (injuries.Count - i) * 18, 600, 20), injuries[i], st);
            if (bleed > 1f) GUI.Label(new Rect(18, H - 116, 600, 20), $"BLEEDING {bleed:0} ML/S  ·  BLOOD {frac * 100f:0}%", st);
            if (dead || passedOut)
            {
                var big = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.1f, 0.1f) } };
                GUI.Label(new Rect(0, H * 0.4f, W, 50), dead ? "YOU DIED" : "UNCONSCIOUS", big);
                var sm = new GUIStyle(big) { fontSize = 15, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
                GUI.Label(new Rect(0, H * 0.4f + 46, W, 30), dead ? $"{cause}  ·  ENTER to start again" : "you've lost too much blood", sm);
            }
        }
    }
}
