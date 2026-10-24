using UnityEngine;

namespace Carl
{
    /// <summary>
    /// An Earl with an FPV drone (about one in five in the trenches and in WAR, or given one in the spawn menu).
    /// When nobody is shooting at him and he is on his feet, he goes somewhere covered from the other side - lower
    /// ground, a trench bay, a dugout, behind a wall - and flies it from there: standing still, goggles over his
    /// eyes, the controller in both hands, his rifle slung. The drone (FpvDrone.LaunchAI) hunts the other side on
    /// its own. If he's hit, knocked down or shot at hard, the goggles come off and the drone drops out of the sky.
    /// </summary>
    public partial class EarlGunner
    {
        /// <summary>The drone he has to fly (null: none, or already flown).</summary>
        public FpvDrone.Kind? droneKit;
        FpvDrone flyingDrone;
        Vector3? pilotSpot;
        float pilotSpotT, pilotNotBefore = -1f;
        int woundsAtLaunch;
        GameObject goggles, controller;
        Renderer[] slung;
        /// <summary>For the playtests: drones launched by Earls.</summary>
        public static int DronesLaunched;
        public bool Piloting => flyingDrone != null && !flyingDrone.Ended;
        /// <summary>Playtests: where he is with his drone.</summary>
        public string PilotDebug => $"kit {droneKit} fighting {Fighting} down {body.ragdoll.IsDown} sup {suppression:0.00} sees {sees}{(enemy != null ? " " + Vector3.Distance(enemy.Center, body.Center).ToString("0") + "m" : "")} wait {pilotNotBefore - Time.time:0} status '{Status}'";

        int WoundCount() { int n = 0; foreach (var bp in body.partList) if (bp != null) n += bp.wounds.Count; return n; }

        /// <summary>The nearest man of the other side he knows of (to keep a wall between him and them).</summary>
        Vector3? ThreatFrom()
        {
            if (lastSeen > 0f && Time.time - lastSeen < 60f) return lastSeenPos;
            HumanBody best = null; float bd = 200f;
            foreach (var h in HumanBody.All)
            {
                if (h == null || h.Dead || !Teams.Hostile(team, Teams.Of(h))) continue;
                float d = Vector3.Distance(h.Center, body.Center);
                if (d < bd) { bd = d; best = h; }
            }
            return best != null ? best.Center : (Vector3?)null;
        }

        /// <summary>Somewhere near out of the other side's sight, and low if he can (a dugout, a trench bay).</summary>
        Vector3? FindPilotSpot()
        {
            Vector3 me = body.Pelvis.transform.position;
            var threat = ThreatFrom();
            Vector3? best = null; float bs = float.MinValue;
            for (int k = 0; k < 16; k++)
            {
                float a = k * 22.5f * Mathf.Deg2Rad, r = 3f + (k % 4) * 3.5f;
                Vector3 c = me + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                if (!Physics.Raycast(c + Vector3.up * 2.5f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore) || g.collider.attachedRigidbody != null) continue;
                Vector3 foot = g.point;
                // he must be able to walk there in a straight line at chest height
                if (!Clear(me + Vector3.up * 0.4f, foot + Vector3.up * 1.2f, body)) continue;
                float score = -r * 0.25f - (foot.y - me.y) * 3f;
                if (threat.HasValue && !Clear(threat.Value + Vector3.up * 0.4f, foot + Vector3.up * 1.6f, body)) score += 10f;
                if (score > bs) { bs = score; best = foot; }
            }
            return best;
        }

        void GogglesOn()
        {
            var head = body.Head; var hand = body[PartId.HandR];
            if (head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(body.Chest.transform.forward, Vector3.up).normalized;
            Color dark = new Color(0.08f, 0.08f, 0.09f);
            goggles = new GameObject("FPV Goggles");
            goggles.transform.position = head.transform.position + fwd * 0.088f - Vector3.up * 0.005f;
            goggles.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            Box(goggles.transform, Vector3.zero, new Vector3(0.18f, 0.075f, 0.09f), dark);
            Box(goggles.transform, new Vector3(0f, 0f, 0.047f), new Vector3(0.13f, 0.045f, 0.006f), new Color(0.15f, 0.16f, 0.2f));   // the front panel
            Box(goggles.transform, new Vector3(0.05f, 0.05f, -0.01f), new Vector3(0.008f, 0.05f, 0.008f), dark);                     // antenna
            Box(goggles.transform, new Vector3(0f, 0.01f, -0.1f), new Vector3(0.2f, 0.025f, 0.11f), dark);                            // the strap round the back
            goggles.transform.SetParent(head.transform, true);
            if (hand != null)
            {
                controller = new GameObject("Controller");
                controller.transform.position = hand.transform.position + fwd * 0.05f + Vector3.up * 0.02f;
                controller.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(-35f, 0f, 0f);
                Box(controller.transform, Vector3.zero, new Vector3(0.17f, 0.045f, 0.1f), dark);
                foreach (float sx in new[] { -0.045f, 0.045f }) Box(controller.transform, new Vector3(sx, 0.03f, 0.01f), new Vector3(0.012f, 0.025f, 0.012f), new Color(0.3f, 0.3f, 0.32f));   // the sticks
                Box(controller.transform, new Vector3(0.07f, 0.05f, -0.04f), new Vector3(0.006f, 0.07f, 0.006f), dark);
                controller.transform.SetParent(hand.transform, true);
            }
            // the rifle slung: out of his hands while he flies
            if (weapon != null) { slung = weapon.GetComponentsInChildren<Renderer>(); foreach (var r in slung) r.enabled = false; }
        }

        static void Box(Transform parent, Vector3 at, Vector3 size, Color c)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at; g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(c, 0.35f);
        }

        void StopPiloting()
        {
            if (flyingDrone != null && !flyingDrone.Ended) flyingDrone.End("SIGNAL LOST");
            flyingDrone = null;
            if (goggles) Object.Destroy(goggles);
            if (controller) Object.Destroy(controller);
            if (slung != null) foreach (var r in slung) if (r) r.enabled = true;
            slung = null;
        }

        /// <summary>Flying his drone, or getting somewhere to fly it from. True if that's what he's doing this frame.</summary>
        bool PilotBehaviour(float legs)
        {
            var rag = body.ragdoll;
            if (flyingDrone != null)
            {
                // hit, knocked down, or rounds coming close: the goggles come off
                if (flyingDrone.Ended || rag.IsDown || suppression > 0.55f || WoundCount() > woundsAtLaunch) { StopPiloting(); pilotNotBefore = Time.time + 6f; return false; }
                rag.moveSpeed = 0f; rag.lean = 0f; rag.crouch = 0.15f;
                Vector3 to = Vector3.ProjectOnPlane(flyingDrone.transform.position - body.Chest.transform.position, Vector3.up);
                if (to.sqrMagnitude > 1f) rag.faceDir = to.normalized;
                // both hands up in front of his chest on the controller (the arms brought up as for a rifle held low)
                rag.aimHold = 2;
                rag.aimDir = (rag.faceDir + Vector3.down * 1.1f).normalized;
                rag.lookAt = body.Head.transform.position + rag.faceDir * 2f - Vector3.up * 0.3f;
                Status = "FLYING A DRONE";
                return true;
            }
            if (droneKit == null) return false;
            if (pilotNotBefore < 0f) pilotNotBefore = Time.time + Random.Range(5f, 15f);
            // not with a man right in front of him, not while they're shooting at him, not on the floor
            if (rag.IsDown || suppression > 0.4f || Time.time < pilotNotBefore) { pilotSpot = null; return false; }
            if (sees && enemy != null && Vector3.Distance(enemy.Center, body.Center) < 25f) { pilotSpot = null; return false; }
            if (pilotSpot == null) { pilotSpot = FindPilotSpot() ?? body.Pelvis.transform.position; pilotSpotT = Time.time; }
            Vector3 me = body.Pelvis.transform.position;
            Vector3 go = Vector3.ProjectOnPlane(pilotSpot.Value - me, Vector3.up);
            if (go.magnitude > 1f && Time.time - pilotSpotT < 12f)
            {
                rag.moveDir = go.normalized; rag.moveSpeed = 2.4f * Mathf.Max(0.4f, legs); rag.faceDir = go.normalized;
                rag.aimDir = null;
                Status = "GOING TO FLY A DRONE";
                return true;
            }
            // there: goggles on, up it goes
            var k = droneKit.Value;
            droneKit = null;
            flyingDrone = FpvDrone.LaunchAI(this, k);
            if (flyingDrone == null) return false;
            DronesLaunched++;
            woundsAtLaunch = WoundCount();
            GogglesOn();
            return true;
        }
    }
}
