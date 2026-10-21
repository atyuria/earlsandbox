using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Getting up the way people do: roll onto the front, push up onto hands and knees, bring one foot through
    /// into a kneel, then stand up over it (with a hand on a wall when there is one). Every lift comes from a
    /// hand, knee or foot that is actually on the ground. Also: writhing in pain on the ground.
    /// </summary>
    public partial class ActiveRagdoll
    {
        int getUpPhase;          // 0 roll to front, 1 hands and knees, 2 kneel, 3 stand (balance code)
        float phaseT, phaseOk;
        Vector3 getUpHeading;
        float writheSeed = -1f;

        void BeginGetUp()
        {
            mode = BodyMode.GettingUp;
            getUpT = 0f;
            getUpPhase = 0;
            phaseT = 0f; phaseOk = 0f;
            var ct = body.Chest.transform;
            getUpHeading = Vector3.ProjectOnPlane(ct.up, Vector3.up);
            if (getUpHeading.sqrMagnitude < 0.01f) getUpHeading = Vector3.ProjectOnPlane(ct.forward, Vector3.up);
            getUpHeading.Normalize();
        }

        float HeightAboveGround(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.3f, Vector3.down, rayBuf, 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = 2f;
            for (int i = 0; i < n; i++)
            {
                var h = rayBuf[i];
                if (h.collider is CharacterController) continue;
                var bp = h.collider.GetComponent<BodyPart>();
                if (bp != null && bp.body == body) continue;
                best = Mathf.Min(best, h.distance - 0.3f);
            }
            return best;
        }

        /// <summary>Pose for the current get-up phase (called while building the frame's muscle targets).</summary>
        void GetUpPose()
        {
            float w = Mathf.Clamp01(phaseT / 0.6f);
            switch (getUpPhase)
            {
                case 0:
                    for (int s = -1; s <= 1; s += 2) { SetArm(s, Shoulder(s, 60f, 30f), 70f); SetLeg(s, 30f, 50f, 10f); }
                    break;
                case 1:
                    // hands and knees: arms straight under the shoulders, thighs vertical under the hips
                    for (int s = -1; s <= 1; s += 2)
                    {
                        SetArm(s, Shoulder(s, Mathf.Lerp(60f, 88f, w), 10f), Mathf.Lerp(70f, 8f, w), -20f);
                        SetLeg(s, Mathf.Lerp(40f, 92f, w), Mathf.Lerp(60f, 95f, w), 25f);
                    }
                    Set(PartId.Neck, AX(-25f)); Set(PartId.Head, AX(-10f));
                    break;
                case 2:
                    // kneel: one foot planted forward, the other knee on the ground, torso coming up
                    int f = fencingSide;
                    SetLeg(f, Mathf.Lerp(95f, 85f, w), 95f, -10f);
                    SetLeg(-f, Mathf.Lerp(80f, 5f, w), 90f, 30f);
                    for (int s = -1; s <= 1; s += 2) if (s != wallHand) SetArm(s, Shoulder(s, Mathf.Lerp(80f, 35f, w), 20f), 30f);
                    Set(PartId.Abdomen, Spine(Mathf.Lerp(30f, 10f, w))); Set(PartId.Chest, Spine(Mathf.Lerp(25f, 8f, w)));
                    break;
            }
        }

        /// <summary>Forces for the get-up phases before standing. Returns true while still handling it.</summary>
        bool UpdateGetUpPhases(float dt, Rigidbody pelvis, Rigidbody chest)
        {
            if (getUpPhase >= 3) return false;
            phaseT += dt;
            float M = body.totalMass;
            // weak, hurt people do it slowly and may not make it
            float strength = Mathf.Clamp01(physio.Effort + 0.25f) * Mathf.Lerp(0.55f, 1.05f, Steadiness) * Mathf.Lerp(1f, 1.3f, grit - 1f);
            var ct = chest.transform;
            float hChest = HeightAboveGround(chest.position), hPelvis = HeightAboveGround(pelvis.position);
            UpdateWallBrace(dt);

            switch (getUpPhase)
            {
                case 0:
                {
                    // roll onto the front (if lying on the back or side)
                    float faceDown = Vector3.Dot(ct.forward, Vector3.down);
                    if (faceDown > 0.45f || phaseT > 2.2f) { getUpPhase = 1; phaseT = 0f; break; }
                    var goal = Quaternion.LookRotation(Vector3.down, getUpHeading);
                    PD(chest, goal, 34f * strength, 7f, M * 0.05f);
                    PD(pelvis, goal, 24f * strength, 6f, M * 0.05f);
                    break;
                }
                case 1:
                {
                    // push up through the hands (chest) and knees (hips) - only what is touching the ground lifts
                    float handsDown = 0f;
                    for (int s = -1; s <= 1; s += 2)
                        if (physio.ArmUsable(s) && HeightAboveGround(body[s < 0 ? PartId.HandL : PartId.HandR].rb.position) < 0.12f) handsDown += 0.5f;
                    float kneesDown = 0f;
                    for (int s = -1; s <= 1; s += 2)
                        if (physio.LegSupport(s) > 0.2f && HeightAboveGround(body[s < 0 ? PartId.ShinL : PartId.ShinR].rb.position) < 0.2f) kneesDown += 0.5f;
                    float ramp = Mathf.Clamp01(phaseT / 0.9f);
                    float chestGoal = Mathf.Lerp(0.18f, 0.5f, ramp), pelvisGoal = Mathf.Lerp(0.18f, 0.52f, ramp);
                    chest.AddForce(Vector3.up * M * Mathf.Clamp(9.81f * 0.42f + (chestGoal - hChest) * 60f - chest.linearVelocity.y * 8f, 0f, 9.81f) * Mathf.Max(0.25f, handsDown) * strength);
                    pelvis.AddForce(Vector3.up * M * Mathf.Clamp(9.81f * 0.48f + (pelvisGoal - hPelvis) * 60f - pelvis.linearVelocity.y * 8f, 0f, 9.81f) * Mathf.Max(0.2f, kneesDown) * strength);
                    var flat = Quaternion.LookRotation(Vector3.down, getUpHeading);
                    PD(chest, flat, 40f * strength, 8f, M * 0.06f);
                    PD(pelvis, flat, 30f * strength, 7f, M * 0.06f);
                    phaseOk = hChest > 0.38f && hPelvis > 0.36f ? phaseOk + dt : 0f;
                    if (phaseOk > 0.25f) { getUpPhase = 2; phaseT = 0f; phaseOk = 0f; }
                    if (phaseT > 3.5f) { getUpPenalty += 1.2f; Fall("too weak to push up"); }
                    break;
                }
                case 2:
                {
                    // kneeling up: torso swings upright over the planted foot (a hand on a wall helps)
                    float ramp = Mathf.SmoothStep(0f, 1f, phaseT / 0.9f);
                    var up = Quaternion.LookRotation(getUpHeading, Vector3.up);
                    var lying = Quaternion.LookRotation(Vector3.down, getUpHeading);
                    var goal = Quaternion.Slerp(lying, up, ramp);
                    PD(chest, goal, 90f * strength, 14f, M * 0.08f);
                    PD(pelvis, Quaternion.Slerp(lying, up, Mathf.Clamp01(ramp * 1.2f)), 110f * strength, 16f, M * 0.1f);
                    float pelvisGoal = Mathf.Lerp(0.5f, 0.62f, ramp);
                    float feet = HeightAboveGround(body[fencingSide < 0 ? PartId.FootL : PartId.FootR].rb.position) < 0.15f ? 1f : 0.5f;
                    pelvis.AddForce(Vector3.up * M * Mathf.Clamp(9.81f * 0.7f + (pelvisGoal - hPelvis) * 70f - pelvis.linearVelocity.y * 10f, 0f, 14f) * feet * strength);
                    chest.AddForce(Vector3.up * M * 9.81f * 0.25f * strength * (BracingOnWall ? 1.4f : 1f));
                    if (phaseT > 1.0f && Vector3.Angle(ct.up, Vector3.up) < 35f)
                    {
                        // stand up out of the kneel with the normal balance code
                        getUpPhase = 3;
                        getUpT = 0.75f;
                        ResetGait();
                    }
                    if (phaseT > 3.2f) { getUpPenalty += 1.5f; Topple("couldn't stand up from kneeling", 0.4f); }
                    break;
                }
            }
            return true;
        }

        /// <summary>Lying in pain: curl around the wound, rock and roll from side to side, legs drawn up and pushing.</summary>
        void Writhe(float dt, Rigidbody pelvis, Rigidbody chest)
        {
            if (writheSeed < 0f) writheSeed = Random.Range(0f, 50f);
            float pain = physio.EffectivePain;
            // you keep control of yourself on the floor (to crawl, aim and shoot back)
            if (body.brain != null && body.brain.playerControlled) return;
            if (pain < 0.25f || physio.consciousness < 0.4f || physio.posture != PostureType.None || IsCrawling || struggle > 0.05f) return;
            float M = body.totalMass;
            float t = Time.time * Mathf.Lerp(0.5f, 0.9f, pain) + writheSeed;
            float k = Mathf.Lerp(0.2f, 0.75f, pain) * Mathf.Clamp01(physio.Effort + 0.3f);
            // rolling side to side about the body's long axis, now and then rolling right over
            Vector3 axis = (chest.position - pelvis.position).normalized;
            float roll = Mathf.Sin(t * 1.3f) + 0.6f * Mathf.Sin(t * 0.47f + 1.3f);
            chest.AddTorque(axis * roll * k * M * 0.55f, ForceMode.Force);
            pelvis.AddTorque(axis * roll * k * M * 0.35f, ForceMode.Force);
            // knees drawn up toward the belly and pushed away again
            float curl = 0.5f + 0.5f * Mathf.Sin(t * 0.8f + 0.7f);
            for (int s = -1; s <= 1; s += 2)
            {
                if (physio.LegSupport(s) < 0.15f) continue;   // broken / paralysed legs just lie there
                float leg = curl + 0.25f * Mathf.Sin(t * 1.1f + s);
                SetLeg(s, Mathf.Lerp(20f, 95f, leg * pain), Mathf.Lerp(30f, 115f, leg * pain), 15f, 8f * s);
            }
            // back arches and curls, head lifts and drops
            Set(PartId.Abdomen, Spine(Mathf.Lerp(-8f, 25f, curl) * pain));
            Set(PartId.Chest, Spine(Mathf.Lerp(-10f, 22f, curl) * pain, 8f * Mathf.Sin(t * 0.9f)));
            Set(PartId.Neck, AX(Mathf.Lerp(-25f, 30f, 0.5f + 0.5f * Mathf.Sin(t * 0.7f + 2f)) * pain));
        }
    }
}
