using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A body on fire (the flamethrower, burning ground). Each part burns on its own: it catches, burns hotter for
    /// a while on what it has to burn, then dies down. Fire creeps to the parts touching a burning one. While it
    /// burns, the skin goes through soot, blistered raw patches and then char (BodyPart.SetBurning / AddBurn - the
    /// skin shader draws it, shrinking and buckling the worst of it), flames and black smoke pour off it, and it
    /// hurts: pain, damaged tissue, shock. Burned over enough of the body, he dies of it.
    ///
    /// Someone who's conscious doesn't stand there: he screams (Sfx), drops whatever he was holding and runs blind,
    /// flailing, changing direction; some throw themselves down and roll, which puts the fire out faster.
    /// </summary>
    [DefaultExecutionOrder(200)]   // (after the brain and the gunner have had their say: on fire, this wins)
    public class Burning : MonoBehaviour
    {
        HumanBody body;
        float[] fire, fuel, char_, spent;
        /// <summary>How long a part can burn in all, ever: once it has, or is charred through, nothing relights it.</summary>
        const float Budget = 16f;
        bool Spent(int i) => spent[i] >= Budget || char_[i] >= 0.97f;
        float burnT, nextTurn, rollFrom = -1f, nextMark, alight;
        Vector3 runDir;
        bool rolls;
        Light glow;
        AudioSource crackle;
        static readonly Dictionary<HumanBody, Burning> map = new Dictionary<HumanBody, Burning>();
        static AudioClip crackleClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { map.Clear(); }

        /// <summary>How much of him is on fire right now (0 not at all .. 1 all over).</summary>
        public static float Intensity(HumanBody h) => h != null && map.TryGetValue(h, out var b) && b != null ? b.alight : 0f;

        /// <summary>Sets a body part alight at a point (more <paramref name="amount"/>: catches harder).</summary>
        public static void Ignite(BodyPart bp, Vector3 at, float amount)
        {
            if (bp == null || bp.body == null || bp.severed) return;
            if (GameSettings.Invincible && bp.body == PlayerAvatar.Body) return;     // (INVINCIBLE: fire does nothing to you)
            var h = bp.body;
            if (!map.TryGetValue(h, out var b) || b == null)
            {
                b = h.gameObject.AddComponent<Burning>();
                b.Init(h);
                map[h] = b;
            }
            b.Catch(bp, at, amount);
        }

        void Init(HumanBody h)
        {
            body = h;
            int n = h.partList.Count;
            fire = new float[n]; fuel = new float[n]; char_ = new float[n]; spent = new float[n];
            // (burned before: what it already burned off is gone - a charred part won't catch again)
            for (int i = 0; i < n; i++) if (h.partList[i] != null) { char_[i] = h.partList[i].charLevel; spent[i] = char_[i] * Budget * 1.3f; }
            rolls = Random.value < 0.45f;
            glow = new GameObject("BurnGlow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.type = LightType.Point; glow.range = 6f; glow.color = new Color(1f, 0.55f, 0.2f); glow.shadows = LightShadows.None; glow.intensity = 0f;
            crackle = gameObject.AddComponent<AudioSource>();
            crackle.clip = Crackle(); crackle.loop = true; crackle.spatialBlend = 1f; crackle.minDistance = 2f; crackle.maxDistance = 30f; crackle.volume = 0f;
            crackle.Play();
        }

        void Catch(BodyPart bp, Vector3 at, float amount)
        {
            int i = body.partList.IndexOf(bp);
            if (i < 0) return;
            if (Spent(i)) return;     // (nothing left there to burn)
            fire[i] = Mathf.Min(1f, fire[i] + amount);
            fuel[i] = Mathf.Min(Mathf.Max(fuel[i], Random.Range(9f, 15f)), Budget - spent[i]);
            // where it touched: blistered first, charring as it keeps on
            if (Random.value < 0.5f) bp.AddBurn(at, Random.Range(0.03f, 0.06f), Mathf.Clamp01(fire[i] * 0.6f), 0.9f);
        }

        void Update()
        {
            if (body == null) { Destroy(this); return; }
            if (WorldFreeze.On) return;
            float dt = Time.deltaTime;
            var parts = body.partList;
            var p = body.physio;
            bool rolling = rollFrom > 0f && body.ragdoll.IsDown;
            float sum = 0f, burnedArea = 0f; int lit = 0;
            Vector3 hottest = body.Center; float hottestF = 0f;
            for (int i = 0; i < parts.Count; i++)
            {
                var bp = parts[i];
                if (bp == null) continue;
                if (fire[i] > 0f)
                {
                    // burning on what it has: hotter while it lasts, dying down when it runs out (faster rolling)
                    fuel[i] -= dt;
                    spent[i] += dt;
                    if (Spent(i)) fuel[i] = Mathf.Min(fuel[i], 0f);
                    if (fuel[i] > 0f && !rolling) fire[i] = Mathf.Min(1f, fire[i] + dt * 0.08f);
                    else fire[i] = Mathf.Max(0f, fire[i] - dt * (rolling ? 0.35f : 0.12f));
                    if (bp.severed && bp.skin == null) fire[i] = 0f;
                    char_[i] = Mathf.Min(1f, char_[i] + dt * fire[i] * 0.075f);
                    bp.SetBurning(char_[i], fire[i]);
                    if (!p.dead)
                    {
                        bp.tissueDamage = Mathf.Min(1f, bp.tissueDamage + dt * fire[i] * 0.025f);
                        p.AddPain(dt * fire[i] * 0.5f, bp.id);
                        p.shock = Mathf.Min(1f, p.shock + dt * fire[i] * 0.003f);
                    }
                    // flames off it, and now and then a fresh burn on the skin where they lick
                    var b = bp.col != null ? bp.col.bounds : new Bounds(bp.transform.position, Vector3.one * 0.15f);
                    Fx.BurnFlames(b.center, fire[i], Mathf.Min(b.extents.x, Mathf.Min(b.extents.y, b.extents.z)) + 0.03f);
                    if (fire[i] > 0.3f && Random.value < dt * 1.2f && bp.col != null)
                        bp.AddBurn(bp.col.ClosestPoint(b.center + Random.onUnitSphere), Random.Range(0.03f, 0.07f), Random.Range(0.4f, 0.9f), 1f);
                    // it creeps to the parts touching it
                    for (int j = 0; j < parts.Count; j++)
                    {
                        if (j == i || parts[j] == null || fire[j] > 0.05f || parts[j].severed) continue;
                        if (Vector3.Distance(parts[j].transform.position, bp.transform.position) > 0.34f) continue;
                        if (Spent(j)) continue;
                        if (Random.value < dt * 0.3f * fire[i]) { fire[j] = 0.2f; fuel[j] = Mathf.Min(Random.Range(8f, 14f), Budget - spent[j]); }
                    }
                    sum += fire[i]; lit++;
                    if (fire[i] > hottestF) { hottestF = fire[i]; hottest = b.center; }
                }
                else if (char_[i] > 0f) bp.SetBurning(char_[i], 0f);
                burnedArea += char_[i];
            }
            alight = parts.Count > 0 ? Mathf.Clamp01(sum / Mathf.Max(4f, parts.Count * 0.5f)) : 0f;
            if (lit > 0) burnT += dt;
            // the glow and the crackle
            glow.transform.position = hottest + Vector3.up * 0.2f;
            glow.intensity = alight * 3f * (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, 0.3f));
            crackle.volume = Mathf.Clamp01(alight * 1.4f) * 0.55f * GameSettings.Volume;
            // burned over most of him: that kills
            if (!p.dead && burnedArea > parts.Count * 0.55f) p.Kill("burned to death", false);
            if (lit == 0)
            {
                // out: the char stays, the fire and its noise go; burned badly enough, what's left takes its look
                if (burnedArea > parts.Count * 0.45f) BurntRemains.Begin(body);
                Destroy(glow.gameObject);
                Destroy(crackle);
                map.Remove(body);
                Destroy(this);
            }
        }

        void LateUpdate()
        {
            if (body == null || WorldFreeze.On || alight < 0.12f) return;
            var p = body.physio; var rag = body.ragdoll;
            if (p.dead || p.consciousness < 0.3f || body == PlayerAvatar.Body) return;
            body.brain.fear = 1f;
            rag.panic = 1f;
            rag.aimDir = null;
            rag.lean = 0f;
            rag.crouch = 0f;
            if (rag.IsDown)
            {
                // rolling: over and over to smother it
                if (rollFrom > 0f && body.Pelvis != null && body.Pelvis.rb != null)
                {
                    float s = Mathf.Sign(Mathf.Sin((Time.time - rollFrom) * 2.2f));
                    body.Pelvis.rb.AddTorque(runDir * s * 18f, ForceMode.Force);
                    if (body.Chest != null && body.Chest.rb != null) body.Chest.rb.AddTorque(runDir * s * 14f, ForceMode.Force);
                }
                rag.wantStand = false;
                return;
            }
            // running blind, flailing, turning this way and that
            if (Time.time > nextTurn)
            {
                nextTurn = Time.time + Random.Range(0.6f, 1.5f);
                Vector2 r = Random.insideUnitCircle.normalized;
                runDir = new Vector3(r.x, 0f, r.y);
            }
            float legs = Mathf.Clamp01(p.LegSupport(-1) + p.LegSupport(1) - 0.6f);
            rag.moveDir = runDir; rag.faceDir = runDir;
            rag.moveSpeed = 3.6f * Mathf.Max(0.3f, legs);
            rag.lookAt = null;
            // some throw themselves down and roll after a few seconds of it
            if (rolls && burnT > 3.5f && rollFrom < 0f) { rollFrom = Time.time; rag.Fall("threw himself down to roll out the flames"); }
        }

        void OnDestroy() { if (body != null && map.TryGetValue(body, out var b) && b == this) map.Remove(body); }

        /// <summary>Fire: a soft roar with pops and crackles.</summary>
        static AudioClip Crackle()
        {
            if (crackleClip != null) return crackleClip;
            int rate = 22050, n = rate * 2;
            var d = new float[n];
            var rnd = new System.Random(4);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = (float)rnd.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, w, 0.08f);
                float pop = rnd.NextDouble() < 0.0015 ? ((float)rnd.NextDouble() * 2f - 1f) * 0.9f : 0f;
                d[i] = lp * 1.4f + pop;
            }
            for (int i = 1; i < n; i++) if (d[i] == 0f) d[i] = d[i - 1] * 0.6f;
            for (int i = 0; i < 1500; i++) { float k = i / 1500f; d[n - 1500 + i] = Mathf.Lerp(d[n - 1500 + i], d[i], k); }
            crackleClip = AudioClip.Create("FireCrackle", n, 1, rate, false);
            crackleClip.SetData(d, 0);
            return crackleClip;
        }
    }
}
