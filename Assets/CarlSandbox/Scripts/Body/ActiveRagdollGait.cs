using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Footstep-driven locomotion. Feet are planted and pinned while in stance; each leg is solved with
    /// two-bone IK toward its foot target. Steps are placed where the body's momentum is carrying it
    /// (capture point), so walking, running, stumbling and recovering all come from where the feet land.
    /// Propulsion only acts through feet that are on the ground, scaled by how much load each leg can bear.
    /// </summary>
    public partial class ActiveRagdoll
    {
        class Leg
        {
            public int side;
            public bool swing;
            public float t, dur, stance;
            public Vector3 plant, from, to;
        }

        readonly Leg[] legs = { new Leg { side = -1 }, new Leg { side = 1 } };
        bool gaitReady;
        float gaitRun;
        const float L1 = 0.42f, L2 = 0.43f, AnkleHeight = 0.075f;

        PartId FootId(int s) => s < 0 ? PartId.FootL : PartId.FootR;
        PartId ThighId(int s) => s < 0 ? PartId.ThighL : PartId.ThighR;
        PartId ShinId(int s) => s < 0 ? PartId.ShinL : PartId.ShinR;

        Vector3 AnklePos(int s) { var f = body[FootId(s)]; return f.transform.TransformPoint(f.jointLocal); }
        Vector3 HipPos(int s) { var t = body[ThighId(s)]; return t.transform.TransformPoint(t.jointLocal); }
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        float LegLoad(int s) => physio.LegSupport(s) * (1f - legBuckle[s < 0 ? 0 : 1] * 0.8f);

        Vector3 GroundUnder(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 1.0f, Vector3.down, rayBuf, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 hitP = new Vector3(p.x, p.y - 0.0f, p.z);
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = rayBuf[i];
                if (h.collider is CharacterController) continue;
                if (Own(h.rigidbody)) continue;   // its own limbs
                // (you step down onto the floor through whoever lies on it: your legs pass over them)
                if (grit > 1f && h.rigidbody != null && h.collider.GetComponentInParent<HumanBody>() != null) continue;
                if (h.distance < best) { best = h.distance; hitP = h.point; found = true; }
            }
            if (!found) hitP.y = body.Pelvis.transform.position.y - GroundHeight;
            return hitP + Vector3.up * AnkleHeight;
        }

        void ResetGait() => gaitReady = false;

        float stepFailT;

        /// <summary>
        /// What the planted legs can actually hold up: the pelvis height a nearly straight stance leg reaches
        /// over its foot (so the body vaults and bobs over each step), and how much of the body's weight the
        /// planted feet carry (0 = no foot under the body).
        /// </summary>
        public float LegHeight(out float support)
        {
            support = 0f;
            if (!gaitReady) { support = 1f; return 99f; }
            float best = 0f;
            var pelvis = body.Pelvis.transform.position;
            foreach (var l in legs)
            {
                if (l.swing || body[FootId(l.side)].severed) continue;
                Vector3 hip = HipPos(l.side);
                float hipDrop = pelvis.y - hip.y;
                float d = Flat(hip - l.plant).magnitude;
                float reach = 0.97f * (L1 + L2);
                if (d >= reach) continue;
                float h = (l.plant.y - AnkleHeight) + AnkleHeight + Mathf.Sqrt(reach * reach - d * d) + hipDrop;
                float groundY = pelvis.y - GroundHeight;
                best = Mathf.Max(best, h - groundY);
                support += LegLoad(l.side) * Mathf.InverseLerp(reach, reach * 0.8f, d);
            }
            return best;
        }

        void UpdateGait(float dt, Rigidbody pelvis, Rigidbody chest)
        {
            var pt = pelvis.transform;
            if (!gaitReady)
            {
                foreach (var l in legs) { l.plant = AnklePos(l.side); l.swing = false; l.stance = 0.3f; }
                gaitReady = true;
            }

            Vector3 vCur = Flat(pelvis.linearVelocity);
            Vector3 vDes = moveDir * moveSpeed;
            if (stumbleTimer > 0f) vDes += stumbleVel;
            float spd = vDes.magnitude;
            gaitRun = Mathf.InverseLerp(1.8f, 4.5f, Mathf.Max(spd, vCur.magnitude));
            Vector3 fwd = Flat(faceDir).sqrMagnitude > 0.01f ? Flat(faceDir).normalized : Flat(pt.forward).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float swingDur = Mathf.Lerp(0.36f, 0.27f, gaitRun) / styleCadence * Mathf.Lerp(1f, 1.25f, Unsteady);

            // ---- advance swinging legs
            foreach (var l in legs)
            {
                if (l.swing)
                {
                    l.t += dt / l.dur;
                    if (l.t < 0.65f) l.to = StepTarget(l, vCur, vDes, right, l.dur);
                    if (l.t >= 1f)
                    {
                        l.swing = false;
                        l.plant = l.to;
                        l.stance = 0f;
                        OnFootPlant(l, vCur);
                    }
                }
                else l.stance += dt;
            }

            // ---- choose the next step
            Leg a = legs[0], b = legs[1];
            if (!a.swing && !b.swing)
            {
                Vector3 com = Flat((pelvis.position + chest.position) * 0.5f);
                Vector3 capture = com + vCur * 0.32f;
                Vector3 mid = Flat((a.plant + b.plant) * 0.5f);
                float balanceErr = Vector3.Distance(capture, mid);
                Vector3 feetLine = Flat(b.plant - a.plant);
                float yawErr = feetLine.sqrMagnitude > 0.001f ? Vector3.Angle(Vector3.Cross(feetLine, Vector3.up), fwd) : 0f;
                bool needStep = spd > 0.08f || balanceErr > 0.12f || yawErr > 35f;
                // the feet could not get back under the body in time: it goes over
                stepFailT = balanceErr > 0.65f ? stepFailT + dt : 0f;
                if (stepFailT > (grit > 1f ? 0.9f : 0.5f)) { stepFailT = 0f; LoseBalance(vCur, grit > 1f ? 0.2f : 0.4f, "couldn't get a foot under"); }

                Leg pick = null; float bestScore = 0.06f;
                foreach (var l in legs)
                {
                    var other = l == a ? b : a;
                    // can't hang around long on a bad leg: the good leg steps through quickly (limp / hop)
                    float minStance = Mathf.Lerp(0.14f, 0.04f, gaitRun);
                    if (LegLoad(other.side) < 0.7f) minStance *= 0.5f;
                    if (l.stance < minStance) continue;
                    if (LegLoad(other.side) < 0.15f) continue; // nothing to stand on while this foot is up
                    Vector3 want = StepTarget(l, vCur, vDes, right, swingDur);
                    float score = Vector3.Distance(Flat(l.plant), Flat(want));
                    if (LegLoad(l.side) < 0.7f && l.stance > 0.2f) score += 0.2f; // get off the hurt leg
                    if (score > bestScore) { bestScore = score; pick = l; }
                }
                if (pick != null && (needStep || bestScore > 0.15f))
                {
                    pick.swing = true;
                    pick.t = 0f;
                    pick.from = pick.plant;
                    // no two steps the same: timing wanders, more so when hurt or panicked
                    pick.dur = swingDur * (LegLoad(pick.side) < 0.8f ? 1.35f : 1f) * Random.Range(0.92f, 1.08f + Unsteady * 0.25f);
                    pick.to = StepTarget(pick, vCur, vDes, right, pick.dur);
                    OnStepStart(pick, vCur);
                }
            }

            // ---- weight shifts over the stance foot
            {
                Leg st = a.swing && !b.swing ? b : (b.swing && !a.swing ? a : null);
                if (st != null)
                {
                    float lat = Vector3.Dot(Flat(st.plant - pelvis.position), right);
                    pelvis.AddForce(right * Mathf.Clamp(lat, -0.15f, 0.15f) * body.totalMass * 18f);
                }
            }

            // ---- solve legs and pin planted feet
            foreach (var l in legs)
            {
                Vector3 target;
                if (l.swing)
                {
                    float s = Mathf.SmoothStep(0f, 1f, l.t);
                    float lift = Mathf.Lerp(0.07f, 0.16f, gaitRun) * (LegLoad(l.side) < 0.8f ? 0.5f : 1f);
                    target = Vector3.Lerp(l.from, l.to, s) + Vector3.up * lift * Mathf.Sin(l.t * Mathf.PI);
                }
                else target = l.plant;
                SolveLeg(l.side, target, pt, l.swing ? 0f : 1f);
                // heel and toe: toe pushes off as the foot leaves, heel strikes as it lands, heel lifts late in stance
                float ank;
                if (l.swing) ank = Mathf.Lerp(22f, -12f, Mathf.SmoothStep(0f, 1f, l.t));
                else ank = Mathf.Clamp(-Vector3.Dot(Flat(HipPos(l.side) - l.plant), fwd) * 70f, 0f, 18f);
                int fi = (int)FootId(l.side);
                target_mul(fi, Ankle(ank * Mathf.Clamp01(spd / 0.6f)));

                var foot = body[FootId(l.side)];
                if (foot.severed) continue;
                Vector3 an = AnklePos(l.side);
                Vector3 v = foot.rb.GetPointVelocity(an);
                if (!l.swing)
                {
                    Vector3 err = l.plant - an;
                    if (err.magnitude > 0.4f) { l.plant = GroundUnder(an); continue; }
                    float grip = 1500f * Mathf.Clamp01(LegLoad(l.side) + 0.2f);
                    foot.rb.AddForceAtPosition(Vector3.ClampMagnitude(err * grip - v * 50f, 320f), an);
                }
                else
                {
                    foot.rb.AddForceAtPosition(Vector3.ClampMagnitude((target - an) * 500f - v * 20f, 110f), an);
                }
            }

            // ---- arms swing against the feet, torso counter-rotates
            if (pose == PoseKind.Stand)
            {
                Vector3 feetMid = (AnklePos(-1) + AnklePos(1)) * 0.5f;
                foreach (var l in legs)
                {
                    float off = Vector3.Dot(AnklePos(l.side) - feetMid, fwd);
                    int arm = -l.side;
                    SetArm(arm, Shoulder(arm, Mathf.Clamp(-off * (38f + gaitRun * 55f) * styleArm, -30f, 35f) + gaitRun * 6f, 5f + gaitRun * 5f + Unsteady * 18f), 8f + gaitRun * 55f * styleArm);
                }
                float twist = Vector3.Dot(AnklePos(1) - AnklePos(-1), fwd) * 12f;
                float lean = (1f - LegLoad(-1)) * -10f + (1f - LegLoad(1)) * 10f;
                Set(PartId.Abdomen, Spine(2f + gaitRun * 5f + styleLean * 0.4f, 0f, -twist * 0.5f));
                Set(PartId.Chest, Spine(3f + gaitRun * 9f + styleLean, lean, twist));
            }
        }

        Vector3 StepTarget(Leg l, Vector3 vCur, Vector3 vDes, Vector3 right, float dur)
        {
            var pelvis = body.Pelvis.rb;
            // wider, irregular steps when unsteady
            Vector3 basePos = Flat(pelvis.position) + right * (l.side * (styleWidth + Unsteady * 0.08f));
            basePos += (right * (Mathf.PerlinNoise(Time.time * 1.7f, l.side * 13.1f + swaySeed) - 0.5f) + Vector3.ProjectOnPlane(body.Pelvis.transform.forward, Vector3.up) * (Mathf.PerlinNoise(swaySeed, Time.time * 1.3f + l.side) - 0.5f)) * Unsteady * 0.22f;
            // land ahead of where momentum is carrying the body; correct toward the commanded velocity
            Vector3 offset = vDes * dur * 0.4f + (vCur - vDes) * 0.25f + vCur * Mathf.Lerp(0.03f, 0.1f, gaitRun);
            float reach = Mathf.Lerp(0.3f, 0.56f, gaitRun) * styleStride * Mathf.Lerp(0.55f, 1f, LegLoad(l.side));
            offset = Vector3.ClampMagnitude(offset, reach);
            Vector3 p = basePos + offset;
            p.y = pelvis.position.y;
            return GroundUnder(p);
        }

        /// <summary>Two-bone IK in the pelvis frame: hip flex/abduction and knee bend to put the ankle on target.</summary>
        void SolveLeg(int s, Vector3 target, Transform pelvisT, float stanceStiff)
        {
            Vector3 d = pelvisT.InverseTransformDirection(target - HipPos(s));
            float r = Mathf.Clamp(d.magnitude, 0.25f, L1 + L2 - 0.008f);
            float cosK = Mathf.Clamp((L1 * L1 + L2 * L2 - r * r) / (2f * L1 * L2), -1f, 1f);
            float bend = 180f - Mathf.Acos(cosK) * Mathf.Rad2Deg;
            float alpha = Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg;
            float cosB = Mathf.Clamp((L1 * L1 + r * r - L2 * L2) / (2f * L1 * r), -1f, 1f);
            float flex = alpha + Mathf.Acos(cosB) * Mathf.Rad2Deg;
            float abd = Mathf.Atan2(s * d.x, new Vector2(d.y, d.z).magnitude) * Mathf.Rad2Deg;
            Set(ThighId(s), Hip(s, flex, abd));
            Set(ShinId(s), Knee(bend));
            Set(FootId(s), Ankle(flex - bend));
        }

        /// <summary>How far the pelvis sinks this frame: dropping onto a weak leg while the other is in the air.</summary>
        float GaitDip()
        {
            if (!gaitReady) return 0f;
            float dip = 0f;
            foreach (var l in legs)
            {
                var other = l == legs[0] ? legs[1] : legs[0];
                if (!l.swing && other.swing) dip = Mathf.Max(dip, (1f - LegLoad(l.side)) * 0.14f);
            }
            return dip;
        }

        /// <summary>Fraction of the commanded drive that can be delivered through grounded feet.</summary>
        float GaitTraction()
        {
            if (!gaitReady) return 1f;
            float t = 0f;
            foreach (var l in legs)
            {
                if (l.swing) continue;
                float load = LegLoad(l.side);
                // a foot stretched far behind the hip can't push any more: wait for the next step
                float stretch = Vector3.Distance(HipPos(l.side), l.plant);
                if (stretch > L1 + L2 - 0.02f) load *= 0.35f;
                t += load;
            }
            // only planted feet push the body along
            return Mathf.Clamp01(0.06f + t * 0.85f);
        }
    }
}
