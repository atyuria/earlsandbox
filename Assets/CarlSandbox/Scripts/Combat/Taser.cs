using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A Taser cartridge fired: two barbed probes fly out on thin insulated wires (the lower one angled 8 degrees
    /// down, so they spread about 13 cm per metre), and stick in whatever they hit. If both are in the same body,
    /// far enough apart, each trigger pull puts five seconds of current through it: the muscles between them
    /// lock and the man goes down stiff. One probe alone, or both close together, just hurts. Hard armour stops a
    /// probe. The darts stay in, each leaving a small puncture; the wires snap past their 7.6 m length.
    /// </summary>
    public class TaserWires : MonoBehaviour
    {
        public static TaserWires Current;
        const float WireLength = 7.6f, ProbeSpeed = 55f;

        class Probe
        {
            public Vector3 from, to;          // flight
            public float t, flight;
            public Transform stuck;           // the dart, parented to what it hit
            public BodyPart part;             // flesh it is in (null: missed, armour, a wall)
            public LineRenderer wire;
            public Transform bay;             // where its wire leaves the cartridge
        }

        readonly Probe[] probes = new Probe[2];
        float cycleUntil;
        AudioSource crackle;
        bool broken;
        static Material wireMat;

        public HumanBody Target => probes[0].part != null && probes[1].part != null && probes[0].part.body == probes[1].part.body && !broken ? probes[0].part.body : null;
        public bool Arrived => probes[0].t >= 1f && probes[1].t >= 1f;
        public bool Cycling => Time.time < cycleUntil;

        public static TaserWires Fire(Transform bay1, Transform bay2, Vector3 dir1, Vector3 dir2, HumanBody shooter)
        {
            if (Current != null) Destroy(Current.gameObject);
            var go = new GameObject("TaserWires");
            var w = go.AddComponent<TaserWires>();
            w.Launch(0, bay1, dir1, shooter);
            w.Launch(1, bay2, dir2, shooter);
            Current = w;
            return w;
        }

        void Launch(int i, Transform bay, Vector3 dir, HumanBody shooter)
        {
            var p = new Probe { bay = bay, from = bay.position };
            float dist = WireLength;
            Vector3 hitPoint = bay.position + dir * WireLength, normal = -dir;
            Collider hitCol = null;
            foreach (var h in Physics.RaycastAll(bay.position, dir, WireLength, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is CharacterController) continue;
                var hb = h.collider.GetComponentInParent<HumanBody>();
                if (hb != null && hb == shooter) continue;
                if (h.distance < dist) { dist = h.distance; hitPoint = h.point; normal = h.normal; hitCol = h.collider; }
            }
            // hard armour in the way catches the dart
            var arm = ArmourSystem.Raycast(bay.position, dir, dist + 0.05f, out float ad, out _, null, shooter);
            bool blocked = arm != null && ad <= dist + 0.03f && (arm.steel || arm.plateUnder || arm.cls > 2.5f);
            if (blocked) { hitPoint = bay.position + dir * ad; hitCol = arm.GetComponent<Collider>(); }
            p.to = hitPoint;
            p.flight = Mathf.Max(0.02f, Vector3.Distance(bay.position, hitPoint) / ProbeSpeed);
            p.part = blocked || hitCol == null ? null : hitCol.GetComponent<BodyPart>();

            // the dart: a steel barb with a little coloured tail
            var dart = new GameObject("Probe").transform;
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(tip.GetComponent<Collider>());
            tip.transform.SetParent(dart, false);
            tip.transform.localScale = new Vector3(0.0035f, 0.0055f, 0.0035f);
            tip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tip.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.GunMaterial("Steel");
            var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(tail.GetComponent<Collider>());
            tail.transform.SetParent(dart, false);
            tail.transform.localScale = new Vector3(0.006f, 0.006f, 0.008f);
            tail.transform.localPosition = new Vector3(0f, 0f, -0.009f);
            tail.GetComponent<MeshRenderer>().sharedMaterial = ModelLibrary.GunMaterial(i == 0 ? "Yellow" : "Tan");
            dart.position = p.from;
            dart.rotation = Quaternion.LookRotation(dir);
            p.stuck = dart;
            if (hitCol != null) dart.SetParent(hitCol.transform, true);

            if (wireMat == null) wireMat = Mats.Unlit(new Color(0.75f, 0.72f, 0.6f, 1f), false);
            var lr = new GameObject("Wire").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.positionCount = 14; lr.startWidth = lr.endWidth = 0.002f;
            lr.sharedMaterial = wireMat; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.wire = lr;
            probes[i] = p;
            pendingStab[i] = hitCol != null && p.part != null;
            stabDir[i] = dir;
        }

        readonly bool[] pendingStab = new bool[2];
        readonly Vector3[] stabDir = new Vector3[2];

        /// <summary>Another five seconds (a trigger pull with the wires still in him).</summary>
        public void Cycle()
        {
            if (broken || !Arrived) return;
            cycleUntil = Time.time + 5f;
            var target = Target;
            if (target != null)
            {
                float spread = Vector3.Distance(probes[0].stuck.position, probes[1].stuck.position);
                TaserShock.Apply(target, 5f, spread);
            }
            else
            {
                // one probe in, or both in one spot: a burn, no lock-up
                foreach (var p in probes) if (p.part != null) { p.part.body.physio.AddPain(0.35f, p.part.id); p.part.body.brain.fear = 1f; }
            }
            if (crackle == null)
            {
                crackle = gameObject.AddComponent<AudioSource>();
                crackle.clip = Sfx.Clip("taser"); crackle.loop = true; crackle.spatialBlend = 0.8f; crackle.minDistance = 1f; crackle.maxDistance = 30f;
            }
            if (crackle.clip != null) crackle.Play();
        }

        void Update()
        {
            for (int i = 0; i < 2; i++)
            {
                var p = probes[i];
                if (p == null) continue;
                if (p.t < 1f)
                {
                    p.t = Mathf.Min(1f, p.t + Time.deltaTime / p.flight);
                    if (p.stuck.parent == null || p.t < 1f) p.stuck.position = Vector3.Lerp(p.from, p.to, p.t) + Vector3.down * (0.03f * p.t * p.flight);
                    if (p.t >= 1f)
                    {
                        p.stuck.position = p.to;
                        // the barb goes in about 9 mm and leaves its mark
                        if (pendingStab[i] && p.part != null && p.part.body != null)
                        {
                            // a skin-deep puncture that oozes a little, and the burn ring round it (not a stab:
                            // a 9 mm barb goes nowhere near bone or organs)
                            p.part.body.damage.AddWound(p.part, p.to, -stabDir[i], WoundType.Stab, 0.12f, 0f, false);
                            p.part.body.damage.AddWound(p.part, p.to, -stabDir[i], WoundType.Abrasion, 0f, 0f, false);
                            p.part.body.lastHit = p.part.Name + " / TASER PROBE";
                            p.part.body.physio.AddPain(0.15f, p.part.id);
                            BloodSystem.Instance?.Mark(p.to, -stabDir[i], 0.008f);
                        }
                        pendingStab[i] = false;
                    }
                }
                // the wire hangs from the cartridge to the dart, sagging under its own weight
                Vector3 a = p.bay != null ? p.bay.position : p.from, b = p.stuck.position;
                float len = Vector3.Distance(a, b);
                if (len > WireLength + 0.3f) broken = true;
                p.wire.enabled = !broken || p.t < 1f;
                float sag = Mathf.Clamp(len * 0.04f, 0f, 0.35f) * (p.t >= 1f ? 1f : 0.2f);
                for (int k = 0; k < 14; k++)
                {
                    float u = k / 13f;
                    p.wire.SetPosition(k, Vector3.Lerp(a, b, u) + Vector3.down * (sag * 4f * u * (1f - u)));
                }
            }
            if (Arrived && cycleUntil == 0f) Cycle();      // the first cycle starts as the probes land
            if (crackle != null)
            {
                crackle.transform.position = probes[0].stuck.position;
                if (!Cycling && crackle.isPlaying) crackle.Stop();
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            // the darts stay where they are; only the wires go
        }
    }

    /// <summary>
    /// Current through the body: every muscle between the probes locks. The man goes rigid and down, shaking,
    /// can't do anything for the cycle (lets go of what he is holding), screams, and recovers once it stops.
    /// </summary>
    public class TaserShock : MonoBehaviour
    {
        HumanBody body;
        float until, strength;

        public static void Apply(HumanBody h, float seconds, float spread)
        {
            if (h == null || h.physio.dead) return;
            var s = h.GetComponent<TaserShock>();
            if (s == null) s = h.gameObject.AddComponent<TaserShock>();
            s.body = h;
            s.until = Time.time + seconds;
            // a wider spread catches more muscle
            s.strength = Mathf.Clamp01(Mathf.InverseLerp(0.05f, 0.3f, spread));
            if (s.strength > 0.2f) h.ragdoll.Fall("tased");
            h.brain.fear = 1f;
            h.physio.AddPain(0.6f * Mathf.Max(0.3f, s.strength), PartId.Chest);
            Sfx.Play("scream", h.Head != null ? h.Head.transform.position : h.Center, 0.9f, 1f, 3f);
        }

        void FixedUpdate()
        {
            if (body == null || Time.time > until || body.physio.dead) { Destroy(this); return; }
            // rigid and jerking at the pulse rate (19 a second)
            float pulse = Mathf.Sin(Time.time * Mathf.PI * 2f * 19f) > 0f ? 1f : -0.5f;
            foreach (var bp in body.partList)
            {
                if (bp == null || bp.rb == null || bp.severed) continue;
                bp.rb.AddTorque(Random.insideUnitSphere * bp.rb.mass * 3f * strength * pulse, ForceMode.Acceleration);
                bp.rb.angularVelocity *= 1f - 0.25f * strength;     // locked: the joints barely move
            }
            body.ragdoll.wantStand = false;
        }

        public static bool Active(HumanBody h) => h != null && h.GetComponent<TaserShock>() != null;
    }
}
