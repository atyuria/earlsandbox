using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Simulated projectiles (bullets and shrapnel). Each one travels with gravity and drag, and on
    /// hitting a body it is traced through that body part's anatomy and continues if it has energy left.
    /// </summary>
    public class Ballistics : MonoBehaviour
    {
        public static Ballistics Instance;

        class Proj
        {
            public Vector3 pos, vel;
            public float mass, diam, drag, life;
            public HitKind kind;
            public BodyPart ignore;
            public LineRenderer tracer;
            public Vector3 tracerFrom;
            public float traveled;
            public bool alive = true;
            public int ricochets;
            public float pen = 3f, wall = 1f, expand = 1f;   // armour class it defeats, wall penetration, wound size
            public ArmourPiece ignoreArmour;
            public int walls;
        }

        readonly List<Proj> live = new List<Proj>();
        readonly Stack<LineRenderer> tracerPool = new Stack<LineRenderer>();
        readonly RaycastHit[] buf = new RaycastHit[16];
        Material tracerMat, fragMat;

        public static Ballistics Create()
        {
            var go = new GameObject("Ballistics");
            Instance = go.AddComponent<Ballistics>();
            Instance.tracerMat = Mats.Unlit(new Color(1f, 0.82f, 0.4f, 0.9f), true);
            Instance.fragMat = Mats.Unlit(new Color(1f, 0.6f, 0.3f, 0.5f), true);
            return Instance;
        }

        public void Fire(Vector3 origin, Vector3 dir, float speed, float mass, float diameter, HitKind kind, bool tracer, Vector3? tracerOrigin = null)
            => Fire(origin, dir, speed, mass, diameter, kind, tracer, tracerOrigin, null);

        public void Fire(Vector3 origin, Vector3 dir, float speed, float mass, float diameter, HitKind kind, bool tracer, Vector3? tracerOrigin, AmmoType ammo)
        {
            float e0 = 0.5f * mass * speed * speed;
            var p = new Proj
            {
                pos = origin, vel = dir.normalized * speed, mass = mass, diam = diameter, kind = kind,
                drag = kind == HitKind.Shrapnel ? 0.012f : (diameter < 0.005f ? 0.004f : 0.00012f), life = kind == HitKind.Shrapnel ? 1.2f : 4f,
                // without an ammo type: estimate from energy (fragments ~1-2, pistol ~2, rifle ~4)
                pen = ammo != null ? ammo.pen : kind == HitKind.Shrapnel ? Mathf.Clamp(e0 / 300f, 0.3f, 2.4f) : Mathf.Clamp(Mathf.Sqrt(e0) / 11f, 1f, 6.5f),
                wall = ammo != null ? ammo.wall : 1f,
                expand = ammo != null ? ammo.expand : 1f
            };
            if (tracer)
            {
                p.tracer = GetTracer(kind == HitKind.Shrapnel ? fragMat : tracerMat, kind == HitKind.Shrapnel ? 0.006f : 0.018f);
                p.tracerFrom = tracerOrigin ?? origin;
            }
            live.Add(p);
            Step(p, Time.deltaTime * 0.5f); // first half-step immediately so point-blank shots register this frame
        }

        LineRenderer GetTracer(Material m, float width)
        {
            LineRenderer lr = tracerPool.Count > 0 ? tracerPool.Pop() : null;
            if (lr == null)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
            }
            lr.sharedMaterial = m;
            lr.startWidth = width;
            lr.endWidth = width * 0.3f;
            lr.gameObject.SetActive(true);
            return lr;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                Step(p, dt);
                if (!p.alive)
                {
                    if (p.tracer) { p.tracer.gameObject.SetActive(false); tracerPool.Push(p.tracer); }
                    live.RemoveAt(i);
                }
            }
        }

        void Step(Proj p, float dt)
        {
            if (!p.alive) return;
            p.life -= dt;
            float speed = p.vel.magnitude;
            if (p.life <= 0f || speed < 25f) { p.alive = false; return; }
            p.vel += Physics.gravity * dt;
            p.vel -= p.vel * Mathf.Min(0.5f, p.drag * speed * dt);

            Vector3 move = p.vel * dt;
            float remaining = move.magnitude;
            Vector3 dir = move / remaining;
            int guard = 0;
            while (remaining > 0f && p.alive && guard++ < 10)
            {
                int n = Physics.RaycastNonAlloc(p.pos, dir, buf, remaining, ~0, QueryTriggerInteraction.Ignore);
                int bi = -1; float bd = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    if (buf[k].collider is CharacterController) continue;
                    if (p.ignore != null && buf[k].collider == p.ignore.col) continue;
                    if (buf[k].distance < bd) { bd = buf[k].distance; bi = k; }
                }
                if (bi < 0)
                {
                    // nothing solid hit - but armour (helmets, visors) can stick out past the body's colliders
                    var ap0 = ArmourSystem.Raycast(p.pos, dir, remaining, out float ad0, out Vector3 aL0);
                    if (ap0 != null && ap0 != p.ignoreArmour)
                    {
                        p.traveled += ad0; remaining -= ad0; p.pos += dir * ad0;
                        if (!HitArmour(p, ap0, aL0, dir)) break;
                        p.ignoreArmour = ap0;
                        continue;
                    }
                    p.pos += dir * remaining;
                    p.traveled += remaining;
                    remaining = 0f;
                    break;
                }
                var hit = buf[bi];

                // armour in front of whatever was hit?
                // (body colliders sit a few cm outside the skin, armour is on the skin: look a little past a body hit)
                bool bodyHit = hit.collider.GetComponent<BodyPart>() != null;
                var ap = ArmourSystem.Raycast(p.pos, dir, hit.distance + (bodyHit ? 0.14f : 0.02f), out float ad, out Vector3 aLocal);
                if (ap != null && ap != p.ignoreArmour)
                {
                    p.traveled += ad; remaining -= ad;
                    p.pos += dir * ad;
                    if (!HitArmour(p, ap, aLocal, dir)) break;
                    p.ignoreArmour = ap;
                    // through the armour and already inside this part's collider: wound it here
                    if (bodyHit && ad >= hit.distance - 0.002f)
                    {
                        var bpi = hit.collider.GetComponent<BodyPart>();
                        if (bpi.body != null && !HitBody(p, bpi, p.pos, dir, ref remaining)) break;
                    }
                    continue;
                }

                p.traveled += hit.distance;
                remaining -= hit.distance;
                p.pos = hit.point;
                float energy = 0.5f * p.mass * p.vel.sqrMagnitude;

                var bp = hit.collider.GetComponent<BodyPart>();
                if (bp != null && bp.body != null)
                {
                    if (!HitBody(p, bp, hit.point, dir, ref remaining)) break;
                    continue;
                }

                // a window: it shatters and the round carries on
                var glass = hit.collider.GetComponent<WindowGlass>();
                if (glass != null)
                {
                    glass.Shatter(hit.point, dir, 3f);
                    p.vel *= 0.95f;
                    p.pos = hit.point + dir * 0.03f;
                    remaining = Mathf.Max(0f, remaining - 0.03f);
                    continue;
                }

                // a torn-off piece of limb: flesh, the round goes on through
                var chunk = hit.collider.GetComponent<LimbChunk>();
                if (chunk != null)
                {
                    chunk.Hit(hit.point, dir, energy);
                    p.vel *= 0.75f;
                    p.pos = hit.point + dir * 0.12f;
                    remaining = Mathf.Max(0f, remaining - 0.12f);
                    continue;
                }

                // world impact
                p.ignore = null;
                float momentum = p.mass * p.vel.magnitude;
                if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(dir * momentum * 3f, hit.point, ForceMode.Impulse);
                float grazing = Vector3.Angle(-dir, hit.normal);
                // ricochet: glancing hits skip off hard surfaces, more likely the shallower the angle
                float fromSurface = 90f - grazing;
                float ricochetChance = Mathf.InverseLerp(38f, 6f, fromSurface) * (p.kind == HitKind.Bullet ? 0.95f : 0.6f);
                if (p.ricochets < 3 && Random.value < ricochetChance)
                {
                    p.ricochets++;
                    Fx.Sparks(hit.point, Vector3.Reflect(dir, hit.normal), 12);
                    Fx.Dust(hit.point, hit.normal, 3);
                    BloodSystem.Instance?.Mark(hit.point, hit.normal, 0.02f, 0.6f);
                    Vector3 refl = Vector3.Reflect(dir, hit.normal);
                    refl = (refl + Random.insideUnitSphere * 0.12f + hit.normal * 0.05f).normalized; // deformed bullets tumble off-line
                    p.vel = refl * p.vel.magnitude * Mathf.Lerp(0.45f, 0.75f, Random.value);
                    dir = refl;
                    p.pos = hit.point + hit.normal * 0.005f;
                    p.ignore = null;
                    if (p.tracer == null)
                    {
                        p.tracer = GetTracer(fragMat, 0.008f);
                        p.tracerFrom = hit.point;
                        p.traveled = 0f;
                    }
                    HumanBrain.BroadcastNoise(hit.point, 15f, 0.3f);
                    if (p.kind == HitKind.Bullet) Sfx.Play("ricochet", hit.point, 0.8f, 1f, 3f, 0.12f);
                    continue;
                }
                if (BloodSystem.Instance != null && hit.rigidbody == null)
                    BloodSystem.Instance.Mark(hit.point, hit.normal, p.kind == HitKind.Shrapnel ? 0.02f : Mathf.Clamp(p.diam * 4f, 0.03f, 0.08f));
                if (p.kind == HitKind.Bullet || Random.value < 0.08f) Sfx.Impact(hit.collider, hit.point);
                // wallbang: punch through if the round can get through this much material
                if (p.kind == HitKind.Bullet && p.walls < 3 && TryWallbang(p, hit, dir, energy)) { remaining = Mathf.Max(0f, remaining - 0.02f); continue; }
                if (p.kind == HitKind.Bullet) { Fx.Dust(hit.point, hit.normal, 8); Fx.Sparks(hit.point, hit.normal, 3); }
                else if (Random.value < 0.3f) Fx.Dust(hit.point, hit.normal, 2);
                p.alive = false;
            }

            if (p.tracer)
            {
                Vector3 tail = p.pos - p.vel.normalized * Mathf.Min(p.traveled, p.kind == HitKind.Shrapnel ? 1.5f : 6f);
                if (p.traveled < 6f) tail = Vector3.Lerp(p.tracerFrom, tail, p.traveled / 6f);
                p.tracer.SetPosition(0, tail);
                p.tracer.SetPosition(1, p.pos);
            }
        }

        bool HitBody(Proj p, BodyPart bp, Vector3 point, Vector3 dir, ref float remaining)
        {
            float energy = 0.5f * p.mass * p.vel.sqrMagnitude;
            if (p.kind == HitKind.Bullet || Random.value < 0.15f) Sfx.Play("impact_flesh", point, 0.9f, 1f, 2f);
            DamageModel.ProjectileExpand = p.expand;
            float left = bp.body.damage.ApplyProjectile(bp, point, dir, energy, p.diam, p.kind, out Vector3 exit, out bool exited);
            DamageModel.ProjectileExpand = 1f;
            if (!exited || left <= 5f) { p.alive = false; return false; }
            p.vel = dir * Mathf.Sqrt(2f * left / p.mass);
            p.pos = exit + dir * 0.003f;
            p.ignore = bp;
            remaining = Mathf.Max(0f, remaining - Vector3.Distance(point, exit));
            return true;
        }

        /// <summary>Bullet meets armour: stopped (blunt trauma behind it), or through with energy lost. Returns false when stopped.</summary>
        bool HitArmour(Proj p, ArmourPiece ap, Vector3 local, Vector3 dir)
        {
            float cls = ap.EffectiveClass(local);
            float energy = 0.5f * p.mass * p.vel.sqrMagnitude;
            float margin = p.pen - cls;
            bool through = margin >= 1f || (margin > -1f && Random.value < (margin + 1f) * 0.5f);
            // every hit wears the plate around the spot; stronger rounds wear it faster
            ap.AddWear(local, Mathf.Clamp(p.pen / Mathf.Max(1f, ap.cls), 0.2f, 1.6f) * (p.kind == HitKind.Shrapnel ? 0.25f : 0.6f));
            // what it looks like: ripped cloth and a dent over a plate that held, a torn hole where it didn't;
            // a plate dented in far enough cracks open (and the round that did it gets through)
            if (ap.Impact(local, dir, energy, p.diam, through) && !through) { through = true; margin = 0.5f; }
            Vector3 w = p.pos;
            Vector3 n = -dir;
            if (ap.steel) Fx.Sparks(w, n, through ? 5 : 14); else Fx.Dust(w, n, 4);
            if (p.kind == HitKind.Bullet || Random.value < 0.2f)
                Sfx.Play(ap.steel && !ap.plateUnder ? "impact_metal" : (ap.plateUnder && !through ? "impact_plate" : "impact_flesh"), w, 0.9f, 1f, 2f);
            if (ap.glass) Fx.Sparks(w, n, 3);
            HumanBrain.BroadcastNoise(w, 12f, 0.3f);
            if (!through)
            {
                // stopped: the energy still slams into the body behind the plate
                if (ap.part != null && ap.part.body != null) ap.part.body.damage.ApplyArmourTrauma(ap.part, w, dir, energy);
                if (p.kind == HitKind.Bullet && Random.value < 0.35f && ap.steel)
                {
                    // glancing off hard steel
                    Vector3 refl = (Vector3.Reflect(dir, (w - ap.transform.position).normalized) + Random.insideUnitSphere * 0.3f).normalized;
                    p.vel = refl * p.vel.magnitude * 0.3f;
                    p.pos = w + refl * 0.02f;
                    p.ignoreArmour = ap;
                    return true;
                }
                p.alive = false;
                return false;
            }
            // through: loses a good part of its energy, deforms (less wall/armour penetration after)
            float keep = Mathf.Lerp(0.45f, 0.85f, Mathf.Clamp01(margin / 3f));
            p.vel *= Mathf.Sqrt(keep);
            p.pen = Mathf.Max(0.5f, p.pen - cls * 0.35f);
            p.pos = w + dir * 0.01f;
            return true;
        }

        /// <summary>Finds how thick the wall is along the shot and, if the round can get through, carries it out the other side.</summary>
        /// <summary>
        /// How solid a wall is compared with concrete: house walls are timber studs and drywall (mostly air),
        /// furniture and fences are wood, the car is thin sheet metal. Arena blocks are solid concrete.
        /// </summary>
        static float WallDensity(Transform t)
        {
            string n = t.name;
            for (var a = t; a != null; a = a.parent)
            {
                string an = a.name;
                if (an == "Bungalow" || an == "Townhouse" || an == "Cottage")
                {
                    if (n.Contains("Roof") || n.Contains("Floor") || n.Contains("Stair")) return 0.5f;
                    return 0.15f;
                }
                if (an == "Table" || an == "Chair" || an == "Sofa" || an == "Bed" || an == "Shelf" || an == "Kitchen") return 0.3f;
                if (an == "Street") return n.Contains("Car") || n.Contains("Body") ? 0.35f : (n.Contains("Fence") ? 0.3f : 1f);
            }
            return 1f;
        }

        bool TryWallbang(Proj p, RaycastHit hit, Vector3 dir, float energy)
        {
            if (hit.collider.GetComponent<BodyPart>() != null) return false;
            // metres of building material this round can punch through (AK FMJ ~8 cm, .50 ~65 cm)
            float maxPen = energy * p.wall * 4e-5f;
            if (maxPen < 0.02f) return false;
            // probe back from well past the wall (a thick but hollow house wall can still be shot through)
            Vector3 far = hit.point + dir * 2f;
            if (!hit.collider.Raycast(new Ray(far, -dir), out var back, 2.02f)) return false;
            float thick = Vector3.Distance(hit.point, back.point) * WallDensity(hit.collider.transform);
            if (thick > maxPen || thick < 0.001f) return false;
            float frac = thick / maxPen;
            p.vel = (dir + Random.insideUnitSphere * 0.04f * frac).normalized * p.vel.magnitude * Mathf.Sqrt(1f - frac);
            p.pos = back.point + dir * 0.004f;
            p.walls++;
            p.pen *= Mathf.Lerp(1f, 0.6f, frac);
            Fx.Dust(back.point, dir, 6);
            BloodSystem.Instance?.Mark(back.point, back.normal, Mathf.Clamp(p.diam * 6f, 0.04f, 0.12f), 0.7f);
            return p.vel.magnitude > 30f;
        }

        public void ClearAll()
        {
            foreach (var p in live) if (p.tracer) { p.tracer.gameObject.SetActive(false); tracerPool.Push(p.tracer); }
            live.Clear();
        }
    }

    /// <summary>Lightweight particle effects and flashes.</summary>
    public class Fx : MonoBehaviour
    {
        static Fx I;
        ParticleSystem sparks, dust, smoke, fire, debris, flame;
        readonly List<(Transform t, Material m, float age, float dur, float size)> waves = new List<(Transform, Material, float, float, float)>();
        readonly List<(Light l, float t, float dur, float max)> flashes = new List<(Light, float, float, float)>();

        public static void Create()
        {
            var go = new GameObject("Fx");
            I = go.AddComponent<Fx>();
            I.sparks = I.MakeSystem("Sparks", Mats.SoftParticle(true), 0.25f, true, 1f);
            I.dust = I.MakeSystem("Dust", Mats.SoftParticle(false), 0.9f, false, 0.25f);
            I.smoke = I.MakeSystem("Smoke", Mats.SoftParticle(false), 4f, false, -0.03f);
            I.fire = I.MakeSystem("Fire", Mats.SoftParticle(true), 0.25f, false, -0.2f);
            I.debris = I.MakeSystem("Debris", Mats.SoftParticle(false), 1.2f, true, 1f);
            I.flame = I.MakeSystem("Flame", Mats.SoftParticle(true), 0.06f, false, 0f);
        }

        ParticleSystem MakeSystem(string name, Material m, float life, bool stretch, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = life;
            main.gravityModifier = gravity;
            main.maxParticles = 4000;
            main.playOnAwake = false;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = !stretch;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = 0.04f;
            r.lengthScale = 1f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, Color c, float life)
        {
            var ep = new ParticleSystem.EmitParams { position = pos, velocity = vel, startSize = size, startColor = c, startLifetime = life, applyShapeToPosition = false };
            ps.Emit(ep, 1);
        }

        public static void Sparks(Vector3 pos, Vector3 normal, int n)
        {
            if (I == null) return;
            for (int i = 0; i < n; i++)
                Emit(I.sparks, pos, (normal + Random.insideUnitSphere * 0.8f).normalized * Random.Range(3f, 9f), Random.Range(0.01f, 0.025f), new Color(1f, 0.7f, 0.3f), Random.Range(0.1f, 0.3f));
        }

        public static void Dust(Vector3 pos, Vector3 normal, int n)
        {
            if (I == null) return;
            for (int i = 0; i < n; i++)
                Emit(I.dust, pos, (normal + Random.insideUnitSphere * 0.6f).normalized * Random.Range(0.4f, 1.8f), Random.Range(0.05f, 0.16f), new Color(0.72f, 0.72f, 0.7f, 0.6f), Random.Range(0.4f, 1f));
        }

        public static void Explosion(Vector3 pos)
        {
            if (I == null) return;
            // white-hot core and an orange fireball that rolls up and out
            for (int i = 0; i < 12; i++)
                Emit(I.fire, pos + Random.insideUnitSphere * 0.15f, Random.insideUnitSphere * 2f, Random.Range(1.4f, 2.4f), new Color(1f, 0.95f, 0.8f, 1f), Random.Range(0.05f, 0.1f));
            for (int i = 0; i < 45; i++)
            {
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.8f + 0.2f;
                Emit(I.fire, pos + d * 0.2f, d * Random.Range(4f, 11f), Random.Range(0.5f, 1.4f), Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.82f, 0.4f), Random.value), Random.Range(0.12f, 0.35f));
            }
            // thick dark smoke rising in a column, lighter grey dust rolling along the ground
            for (int i = 0; i < 45; i++)
                Emit(I.smoke, pos + Random.insideUnitSphere * 0.6f + Vector3.up * 0.3f, Random.insideUnitSphere * 1.6f + Vector3.up * Random.Range(1.2f, 3.2f), Random.Range(1.4f, 3.4f), new Color(0.16f, 0.15f, 0.14f, 0.7f), Random.Range(3.5f, 7f));
            for (int i = 0; i < 36; i++)
            {
                float a = i / 36f * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0.04f, Mathf.Sin(a));
                Emit(I.smoke, pos + d * 0.3f, d * Random.Range(5f, 9f), Random.Range(0.8f, 1.8f), new Color(0.55f, 0.52f, 0.48f, 0.45f), Random.Range(1.5f, 3f));
            }
            // dirt and debris thrown up, hot fragments streaking out
            for (int i = 0; i < 70; i++)
            {
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y);
                Emit(I.debris, pos, d * Random.Range(4f, 14f), Random.Range(0.015f, 0.05f), new Color(0.2f, 0.18f, 0.16f, 1f), Random.Range(0.6f, 1.4f));
            }
            Sparks(pos, Vector3.up, 90);
            Flash(pos, 26f, 22f, 0.35f);
            Shockwave(pos);
        }

        /// <summary>A fast expanding, fading pressure ring.</summary>
        public static void Shockwave(Vector3 pos)
        {
            if (I == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            var m = new Material(Mats.Unlit(new Color(1f, 0.9f, 0.75f, 0.35f), true));
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            I.waves.Add((go.transform, m, 0f, 0.22f, 7f));
        }

        /// <summary>Muzzle flash: a short bright cone of burning gas, side jets, a few sparks and a puff of smoke.</summary>
        public static void Muzzle(Vector3 pos, Vector3 dir, float power)
        {
            if (I == null) return;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            Vector3 up2 = Vector3.Cross(side, dir);
            int n = Mathf.RoundToInt(6 + power * 8);
            for (int i = 0; i < n; i++)
                Emit(I.flame, pos + dir * Random.Range(0f, 0.08f * power), dir * Random.Range(2f, 8f) * power + Random.insideUnitSphere * 1.2f, Random.Range(0.04f, 0.12f) * power, new Color(1f, Random.Range(0.6f, 0.85f), 0.35f, 1f), Random.Range(0.025f, 0.06f));
            for (int k = 0; k < 4; k++)
            {
                Vector3 jet = Quaternion.AngleAxis(k * 90f + Random.Range(-20f, 20f), dir) * side;
                Emit(I.flame, pos + dir * 0.02f, (jet * 3f + dir) * power, 0.05f * power, new Color(1f, 0.7f, 0.4f, 0.9f), 0.035f);
            }
            for (int i = 0; i < 3; i++)
                Emit(I.smoke, pos + dir * 0.05f, dir * Random.Range(0.4f, 1.2f) + up2 * 0.3f + Random.insideUnitSphere * 0.2f, Random.Range(0.08f, 0.16f) * (0.6f + power * 0.5f), new Color(0.75f, 0.74f, 0.72f, 0.25f), Random.Range(0.6f, 1.2f));
            if (Random.value < 0.6f) Emit(I.sparks, pos, (dir + Random.insideUnitSphere * 0.3f).normalized * 6f, 0.008f, new Color(1f, 0.75f, 0.35f), 0.15f);
        }

        public static void Flash(Vector3 pos, float intensity, float range, float duration)
        {
            if (I == null) return;
            var go = new GameObject("Flash");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.45f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            I.flashes.Add((l, 0f, duration, intensity));
        }

        void Update()
        {
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                w.age += Time.deltaTime;
                if (w.t == null || w.age >= w.dur) { if (w.t) Destroy(w.t.gameObject); waves.RemoveAt(i); continue; }
                float k = w.age / w.dur;
                w.t.localScale = Vector3.one * Mathf.Lerp(0.3f, w.size, Mathf.Sqrt(k));
                var c = w.m.GetColor("_Color"); c.a = 0.35f * (1f - k); w.m.SetColor("_Color", c);
                waves[i] = w;
            }
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                f.t += Time.deltaTime;
                if (f.l == null || f.t >= f.dur)
                {
                    if (f.l) Destroy(f.l.gameObject);
                    flashes.RemoveAt(i);
                    continue;
                }
                f.l.intensity = f.max * (1f - f.t / f.dur);
                flashes[i] = f;
            }
        }
    }
}
