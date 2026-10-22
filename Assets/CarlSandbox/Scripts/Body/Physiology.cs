using UnityEngine;

namespace Carl
{
    public enum PostureType { None, Decerebrate, Decorticate, Fencing, Seizure, Curl, Tremor }

    /// <summary>
    /// Simulated physiology: blood volume, cardiac output, blood pressure, breathing, oxygen,
    /// brain perfusion, consciousness, pain, adrenaline, shock and neurological deficits.
    /// </summary>
    public class Physiology : MonoBehaviour
    {
        public const float NormalBlood = 5200f;

        HumanBody body;

        [Header("Circulation")]
        public float blood = NormalBlood;
        public float heartRate = 72f;
        public float systolic = 120f, diastolic = 80f;
        public float perfusion = 1f;
        public bool cardiacArrest;
        public float bleedOut, bleedIn;
        float arrestCountdown = -1f;

        [Header("Respiration")]
        public float breathing = 1f;
        public float spo2 = 0.98f;
        public float lungCollapseL, lungCollapseR;
        public float airwayBlood;

        [Header("Neuro")]
        public float brainO2 = 1f;
        public float brainDamage;        // total incl. hypoxic
        public float brainTrauma;        // direct trauma only
        public float consciousness = 1f;
        public float concussion;
        public float confusion;
        public float ataxia;
        public float hemiplegiaL, hemiplegiaR; // weakness of the LEFT / RIGHT side of the body
        public bool blindL, blindR;
        public SpineLevel lesion = SpineLevel.None;
        public float comaDepth;
        public PostureType posture = PostureType.None;
        public float postureTimer;
        public float faintTimer;

        [Header("Feeling")]
        public float pain;
        public float chronicPain;
        public float adrenaline = 0.05f;
        public float shock;
        public float fatigue;

        [Header("Death")]
        public bool dead;
        public float deathTime;
        public string causeOfDeath = "";
        public bool instantLimp;

        public float LossFrac => Mathf.Clamp01(1f - blood / NormalBlood);
        public float EffectivePain => Mathf.Clamp01(pain * (1f - 0.45f * adrenaline)) * Mathf.Clamp01(consciousness * 1.3f);
        public bool Blind => blindL && blindR || OrganDamage(BrainRegion.Occipital) > 0.45f;
        public bool Fainted => faintTimer > 0f;
        public bool Conscious => !dead && consciousness > 0.2f;
        public float Clumsiness => Mathf.Clamp01(Mathf.Max(ataxia, (1f - consciousness) * 0.8f, confusion * 0.5f, EffectivePain * 0.35f, LossFrac * 1.2f));

        public void Init(HumanBody b)
        {
            body = b;
            adrenaline = 0.05f;
        }

        public float OrganDamage(BrainRegion r)
        {
            float d = 0;
            foreach (var bp in body.partList)
            {
                if (bp.id != PartId.Head) continue;
                foreach (var o in bp.organs) if (o.def.brain == r) d = Mathf.Max(d, o.Damage);
            }
            return d;
        }

        float OrganHealth(OrganId id)
        {
            float h = 1f; int n = 0; float sum = 0;
            foreach (var o in body.organs)
            {
                if (o.def.organ != id) continue;
                sum += o.health; n++;
                h = Mathf.Min(h, o.health);
            }
            return n == 0 ? 1f : (id == OrganId.Lungs ? sum / n : h);
        }

        Organ FindOrgan(string name)
        {
            foreach (var o in body.organs) if (o.def.name == name) return o;
            return null;
        }

        // ---------------------------------------------------------------- events

        public void AddPain(float amount, PartId part)
        {
            if (dead) return;
            if (!HasSensation(part)) return;
            pain = Mathf.Clamp01(pain + amount);
            chronicPain = Mathf.Clamp(chronicPain + amount * 0.35f, 0f, 0.85f);
            adrenaline = Mathf.Clamp01(adrenaline + amount * 0.6f);
            // you, getting hit: the adrenaline floods in at once
            if (body != null && body == PlayerAvatar.Body) adrenaline = Mathf.Clamp01(adrenaline + 0.2f + amount);
            // vasovagal faint from sudden severe pain
            if (amount > 0.35f && EffectivePain > 0.8f && Random.value < 0.18f)
                faintTimer = Random.Range(3f, 9f);
        }

        public void AddStress(float amount)
        {
            adrenaline = Mathf.Clamp01(adrenaline + amount);
        }

        public void AddConcussion(float amount)
        {
            concussion = Mathf.Clamp01(concussion + amount);
            if (amount > 0.45f && Random.value < amount) faintTimer = Mathf.Max(faintTimer, Random.Range(4f, 15f));
        }

        public void StartPosture(PostureType p, float duration)
        {
            if (dead && p != PostureType.Decerebrate) return;
            posture = p;
            postureTimer = duration;
        }

        public void SetLesion(SpineLevel level)
        {
            if (level == SpineLevel.None) return;
            if (lesion == SpineLevel.None || (int)level < (int)lesion) lesion = level;
            if (lesion == SpineLevel.HighCervical) breathing = 0f;
        }

        public void Kill(string cause, bool limpInstantly)
        {
            if (dead) return;
            dead = true;
            deathTime = Time.time;
            causeOfDeath = cause;
            instantLimp = limpInstantly;
            cardiacArrest = true;
            consciousness = 0f;
            body.brain?.OnDeath();
        }

        /// <summary>Immediate neurological / cardiac consequences of damage to one structure.</summary>
        public void OnOrganDamaged(Organ o, float dmg, float energy)
        {
            if (dmg <= 0f) return;
            var d = o.def;
            switch (d.kind)
            {
                case TissueKind.Brain:
                    brainTrauma = Mathf.Clamp01(brainTrauma + dmg * 0.35f);
                    AddConcussion(0.3f + dmg * 0.5f);
                    switch (d.brain)
                    {
                        case BrainRegion.Medulla:
                            if (o.health < 0.6f) Kill("BRAINSTEM DESTROYED", true);
                            break;
                        case BrainRegion.Midbrain:
                            comaDepth = Mathf.Max(comaDepth, 0.9f);
                            StartPosture(PostureType.Decerebrate, Random.Range(4f, 9f));
                            if (o.health < 0.3f) arrestCountdown = Random.Range(8f, 20f);
                            break;
                        case BrainRegion.Deep:
                            comaDepth = Mathf.Max(comaDepth, 0.7f + dmg * 0.3f);
                            StartPosture(PostureType.Decorticate, Random.Range(4f, 8f));
                            break;
                        case BrainRegion.Cerebellum:
                            ataxia = Mathf.Clamp01(ataxia + dmg * 1.4f);
                            if (dmg > 0.5f) StartPosture(PostureType.Tremor, Random.Range(3f, 6f));
                            break;
                        case BrainRegion.MotorLeft:
                            hemiplegiaR = Mathf.Clamp01(hemiplegiaR + dmg * 1.6f); // left cortex drives the right body
                            if (dmg > 0.4f) StartPosture(PostureType.Fencing, Random.Range(2f, 5f));
                            break;
                        case BrainRegion.MotorRight:
                            hemiplegiaL = Mathf.Clamp01(hemiplegiaL + dmg * 1.6f);
                            if (dmg > 0.4f) StartPosture(PostureType.Fencing, Random.Range(2f, 5f));
                            break;
                        case BrainRegion.TemporalLeft:
                        case BrainRegion.TemporalRight:
                            confusion = Mathf.Clamp01(confusion + dmg);
                            StartPosture(PostureType.Seizure, Random.Range(6f, 13f));
                            break;
                        case BrainRegion.Frontal:
                            confusion = Mathf.Clamp01(confusion + dmg * 1.2f);
                            if (dmg > 0.55f) StartPosture(PostureType.Decorticate, Random.Range(3f, 6f));
                            break;
                        case BrainRegion.Occipital:
                            confusion = Mathf.Clamp01(confusion + dmg * 0.5f);
                            break;
                    }
                    if (brainTrauma > 0.85f) Kill("MASSIVE BRAIN TRAUMA", false);
                    break;

                case TissueKind.Spine:
                    if (o.health < 0.55f || energy > 1200f)
                    {
                        o.severed = true;
                        SetLesion(d.spine);
                    }
                    break;

                case TissueKind.Eye:
                    if (o.health < 0.5f) { if (d.side < 0) blindL = true; else blindR = true; }
                    break;

                case TissueKind.Airway:
                    airwayBlood = Mathf.Clamp01(airwayBlood + dmg);
                    break;

                case TissueKind.Organ:
                    if (d.organ == OrganId.Heart)
                    {
                        if (o.health < 0.35f) arrestCountdown = arrestCountdown < 0 ? Random.Range(1.5f, 6f) : Mathf.Min(arrestCountdown, 3f);
                    }
                    else if (d.organ == OrganId.Lungs)
                    {
                        if (d.side < 0) lungCollapseL = Mathf.Max(lungCollapseL, 0.05f);
                        else lungCollapseR = Mathf.Max(lungCollapseR, 0.05f);
                        airwayBlood = Mathf.Clamp01(airwayBlood + dmg * 0.4f);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- queries

        public bool HasSensation(PartId p)
        {
            return MotorFactorSpinal(p) > 0.05f || p == PartId.Head || p == PartId.Neck;
        }

        float MotorFactorSpinal(PartId p)
        {
            bool leg = Anatomy.IsLeg(p);
            bool arm = Anatomy.IsArm(p);
            switch (lesion)
            {
                case SpineLevel.HighCervical:
                    return p == PartId.Head || p == PartId.Neck ? 0.6f : 0f;
                case SpineLevel.LowCervical:
                    if (p == PartId.Head || p == PartId.Neck) return 1f;
                    if (p == PartId.UpperArmL || p == PartId.UpperArmR) return 0.3f;
                    if (arm) return 0.05f;
                    return p == PartId.Chest ? 0.2f : 0f;
                case SpineLevel.Thoracic:
                    if (leg || p == PartId.Pelvis) return 0f;
                    return p == PartId.Abdomen ? 0.4f : 1f;
                case SpineLevel.Lumbar:
                    if (p == PartId.ThighL || p == PartId.ThighR) return 0.15f;
                    return leg ? 0f : 1f;
                case SpineLevel.Sacral:
                    if (p == PartId.ShinL || p == PartId.ShinR || p == PartId.FootL || p == PartId.FootR) return 0.3f;
                    return leg ? 0.8f : 1f;
            }
            return 1f;
        }

        /// <summary>0..1 multiplier for the muscles of this part (paralysis, hemiplegia, unconsciousness excluded).</summary>
        public float MotorFactor(PartId p)
        {
            float f = MotorFactorSpinal(p);
            int side = Anatomy.Side(p);
            if (side < 0) f *= 1f - hemiplegiaL;
            else if (side > 0) f *= 1f - hemiplegiaR;
            var bp = body[p];
            if (bp != null && bp.severed) f = 0f;
            return f;
        }

        // Leg and arm state is asked for many times per body every physics step (balance, gait, getting up, the
        // gunner...). Worked out once per step (or frame) and reused: it can't change within one.
        float limbStamp = float.NaN;
        float legCacheL, legCacheR;
        bool armCacheL, armCacheR;
        static float Stamp => Time.inFixedTimeStep ? Time.fixedTime : -1f - Time.time;
        void RefreshLimbs()
        {
            float st = Stamp;
            if (st == limbStamp) return;
            limbStamp = st;
            legCacheL = ComputeLegSupport(-1); legCacheR = ComputeLegSupport(1);
            armCacheL = ComputeArmUsable(-1); armCacheR = ComputeArmUsable(1);
        }
        /// <summary>Forget the cached limb state (a limb was just hit, broken or cut off).</summary>
        public void LimbsChanged() => limbStamp = float.NaN;

        public float LegSupport(int side) { RefreshLimbs(); return side < 0 ? legCacheL : legCacheR; }
        public bool ArmUsable(int side) { RefreshLimbs(); return side < 0 ? armCacheL : armCacheR; }

        static readonly string[] femurN = { "LEFT FEMUR", "RIGHT FEMUR" }, tibiaN = { "LEFT TIBIA", "RIGHT TIBIA" }, fibulaN = { "LEFT FIBULA", "RIGHT FIBULA" },
            footN = { "LEFT FOOT BONES", "RIGHT FOOT BONES" }, humerusN = { "LEFT HUMERUS", "RIGHT HUMERUS" };

        float ComputeLegSupport(int side)
        {
            PartId th = side < 0 ? PartId.ThighL : PartId.ThighR;
            PartId sh = side < 0 ? PartId.ShinL : PartId.ShinR;
            PartId ft = side < 0 ? PartId.FootL : PartId.FootR;
            float s = Mathf.Min(MotorFactor(th), MotorFactor(sh));
            int si = side < 0 ? 0 : 1;
            var femur = body[th].Find(femurN[si]);
            var tibia = body[sh].Find(tibiaN[si]);
            var fib = body[sh].Find(fibulaN[si]);
            var foot = body[ft].Find(footN[si]);
            if (femur != null && femur.Fractured) s *= femur.BadlyBroken ? 0.02f : 0.12f;
            if (tibia != null && tibia.Fractured) s *= tibia.BadlyBroken ? 0.08f : 0.3f;
            if (fib != null && fib.Fractured) s *= 0.8f;
            if (foot != null && foot.Fractured) s *= 0.6f;
            s *= 1f - body[th].tissueDamage * 0.5f;
            s *= 1f - body[sh].tissueDamage * 0.4f;
            var pelvisBone = body.Pelvis.Find("PELVIC BONE");
            if (pelvisBone != null && pelvisBone.Fractured) s *= 0.25f;
            if (body[th].severed || body[sh].severed || body[ft].severed) s = 0f;
            return Mathf.Clamp01(s);
        }

        bool ComputeArmUsable(int side)
        {
            PartId ua = side < 0 ? PartId.UpperArmL : PartId.UpperArmR;
            PartId fa = side < 0 ? PartId.ForearmL : PartId.ForearmR;
            if (body[ua].severed || body[fa].severed) return false;
            if (MotorFactor(ua) < 0.3f || MotorFactor(fa) < 0.3f) return false;
            var hum = body[ua].Find(humerusN[side < 0 ? 0 : 1]);
            return hum == null || !hum.BadlyBroken;
        }

        public bool CanStand
        {
            get
            {
                if (dead || consciousness < 0.55f || Fainted) return false;
                if (posture != PostureType.None) return false;
                float l = LegSupport(-1), r = LegSupport(1);
                if (l + r < 1.05f || Mathf.Min(l, r) < 0.1f) return false;
                if (EffectivePain > 0.88f) return false;
                if (ataxia > 0.75f) return false;
                if (breathing < 0.3f) return false;
                return true;
            }
        }

        public float Effort => Mathf.Clamp01(0.35f + adrenaline * 0.5f + (body.brain ? body.brain.fear * 0.3f : 0f)) * consciousness;

        public string ConsciousText()
        {
            if (dead) return "DEAD";
            if (Fainted) return "FAINTED";
            if (cardiacArrest) return "CARDIAC ARREST";
            if (consciousness < 0.15f) return comaDepth > 0.5f ? "COMATOSE" : "UNCONSCIOUS";
            if (consciousness < 0.4f) return "BARELY CONSCIOUS";
            if (consciousness < 0.62f) return "DROWSY";
            if (confusion > 0.35f || consciousness < 0.8f) return "CONFUSED";
            return "CONSCIOUS";
        }

        public string PulseText()
        {
            if (cardiacArrest || dead) return "NO PULSE";
            if (perfusion < 0.35f) return "THREADY";
            if (perfusion < 0.7f) return "WEAK";
            return "STRONG";
        }

        public string BreathingText()
        {
            if (dead || breathing <= 0.01f) return "NOT BREATHING";
            if (airwayBlood > 0.4f) return "CHOKING ON BLOOD";
            if (breathing < 0.45f) return "GASPING";
            if (breathing < 0.8f) return "LABOURED";
            if (adrenaline > 0.5f) return "HYPERVENTILATING";
            return "BREATHING NORMALLY";
        }

        public string SpineText()
        {
            switch (lesion)
            {
                case SpineLevel.HighCervical: return "SEVERED C1-C4 — QUADRIPLEGIC, NO DIAPHRAGM";
                case SpineLevel.LowCervical: return "SEVERED C5-C7 — QUADRIPLEGIC";
                case SpineLevel.Thoracic: return "SEVERED THORACIC — PARAPLEGIC";
                case SpineLevel.Lumbar: return "SEVERED LUMBAR — LEGS PARALYSED";
                case SpineLevel.Sacral: return "CAUDA EQUINA — LOWER LEGS WEAK";
            }
            return "INTACT";
        }

        // ---------------------------------------------------------------- simulation

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || body == null) return;
            if (WorldFreeze.On && body != PlayerAvatar.Body) return;   // frozen world: no bleeding, no clock

            // --- bleeding
            float pressure = Mathf.Clamp01(systolic / 120f);
            float volF = Mathf.Clamp01(blood / NormalBlood);
            bleedOut = 0f; bleedIn = 0f;
            foreach (var bp in body.partList)
            {
                for (int i = bp.wounds.Count - 1; i >= 0; i--)
                {
                    var w = bp.wounds[i];
                    w.age += dt;
                    // clotting: small bleeds stop, arteries keep going while there is pressure
                    float clot = w.arterial ? (w.extRate < 6f ? 0.004f : 0f) : (w.extRate > 18f ? 0.002f : w.clotRate);
                    if (dead) clot += 0.02f;
                    w.extRate = Mathf.Max(0f, w.extRate * (1f - clot * dt));
                    w.intRate = Mathf.Max(0f, w.intRate * (1f - clot * 0.3f * dt));
                    if (bp.severed) continue;
                    float drive = w.arterial ? pressure : Mathf.Lerp(0.4f, 1f, volF);
                    drive *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.3f, volF)); // nothing left to bleed
                    if (cardiacArrest) drive *= w.arterial ? 0.06f : 0.35f;
                    bleedOut += w.extRate * drive;
                    bleedIn += w.intRate * drive;
                }
            }
            blood = Mathf.Max(0f, blood - (bleedOut + bleedIn) * dt);
            float loss = LossFrac;

            // --- heart
            if (arrestCountdown > 0f)
            {
                arrestCountdown -= dt;
                if (arrestCountdown <= 0f) cardiacArrest = true;
            }
            if (loss > 0.55f) cardiacArrest = true;
            float heartHealth = OrganHealth(OrganId.Heart);
            float cardiac = cardiacArrest ? 0f : Mathf.Lerp(0.2f, 1f, heartHealth);
            float volumeFactor = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.5f, loss)) * 0.85f;
            perfusion = Mathf.MoveTowards(perfusion, cardiac * volumeFactor, dt * 0.6f);
            float map = 93f * perfusion + (cardiacArrest ? 0f : adrenaline * 8f);
            systolic = map * 1.3f;
            diastolic = map * 0.85f;
            float targetHR = cardiacArrest ? 0f :
                70f + adrenaline * 50f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.4f, loss)) * 70f + EffectivePain * 15f - comaDepth * 20f;
            if (!cardiacArrest && loss > 0.45f) targetHR *= 0.75f; // decompensation
            heartRate = Mathf.MoveTowards(heartRate, targetHR, dt * 25f);

            // --- breathing
            float lungs = OrganHealth(OrganId.Lungs);
            lungCollapseL = lungCollapseL > 0f ? Mathf.Min(1f, lungCollapseL + dt / 40f) : 0f;
            lungCollapseR = lungCollapseR > 0f ? Mathf.Min(1f, lungCollapseR + dt / 40f) : 0f;
            float lungFunction = Mathf.Clamp01(lungs - (lungCollapseL + lungCollapseR) * 0.45f);
            var trachea = FindOrgan("TRACHEA");
            float airway = trachea != null && trachea.severed ? 0.35f : 1f;
            airway *= 1f - airwayBlood * 0.6f;
            float drive2 = 1f - OrganDamage(BrainRegion.Medulla);
            if (lesion == SpineLevel.HighCervical) drive2 = 0f;
            if (lesion == SpineLevel.LowCervical) drive2 *= 0.6f;
            if (dead || cardiacArrest) drive2 = 0f;
            breathing = Mathf.MoveTowards(breathing, Mathf.Clamp01(lungFunction * airway * drive2), dt * 0.5f);
            airwayBlood = Mathf.Max(0f, airwayBlood - dt * 0.01f);

            float spoTarget = 0.98f * Mathf.Sqrt(breathing);
            spo2 = Mathf.MoveTowards(spo2, spoTarget, dt * (spoTarget < spo2 ? 0.015f : 0.05f));

            // --- brain oxygen and damage
            float o2Target = spo2 / 0.98f * Mathf.Clamp01(perfusion * 1.1f);
            brainO2 += (o2Target - brainO2) * Mathf.Clamp01(dt / 5f);
            if (brainO2 < 0.55f) brainDamage += (0.55f - brainO2) * 0.06f * dt;
            brainDamage = Mathf.Max(brainDamage, brainTrauma);
            if (!dead && brainDamage >= 1f) Kill(cardiacArrest ? "CARDIAC ARREST — BRAIN DEATH" : "HYPOXIC BRAIN DEATH", false);
            if (!dead && cardiacArrest && brainO2 < 0.05f && Time.time - deathTime > 0f && brainDamage > 0.6f) Kill("CARDIAC ARREST", false);

            // --- neuro recovery
            concussion = Mathf.Max(0f, concussion - dt * 0.012f);
            confusion = Mathf.Max(brainTrauma * 0.6f, confusion - dt * 0.006f);
            if (faintTimer > 0f) faintTimer -= dt;
            if (postureTimer > 0f)
            {
                postureTimer -= dt;
                if (postureTimer <= 0f) posture = PostureType.None;
            }

            // --- consciousness
            float c = 1f;
            c = Mathf.Min(c, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, brainO2)));
            c -= brainTrauma * 0.9f;
            c -= concussion * 0.8f;
            c -= comaDepth;
            c -= Mathf.Max(0f, EffectivePain - 0.85f) * 2f;
            c -= Mathf.InverseLerp(0.25f, 0.42f, loss) * 0.5f;
            if (faintTimer > 0f) c = 0f;
            if (dead) c = 0f;
            c = Mathf.Clamp01(c);
            consciousness = c < consciousness ? Mathf.MoveTowards(consciousness, c, dt * 1.5f) : Mathf.MoveTowards(consciousness, c, dt * 0.06f);
            comaDepth = Mathf.Max(0f, comaDepth - dt * 0.002f);

            // --- pain / adrenaline / shock
            chronicPain = Mathf.Max(0f, chronicPain - dt * 0.002f);
            pain = Mathf.MoveTowards(pain, chronicPain, dt * 0.05f);
            if (dead) pain = 0f;
            adrenaline = Mathf.Max(0.05f, adrenaline - dt * 0.008f);
            shock = Mathf.Clamp01(Mathf.InverseLerp(0.05f, 0.45f, loss) * 0.85f + EffectivePain * 0.15f + (cardiacArrest ? 1f : 0f));
            ataxia = Mathf.Max(OrganDamage(BrainRegion.Cerebellum) * 1.2f, ataxia - dt * 0.002f);
        }
    }
}
