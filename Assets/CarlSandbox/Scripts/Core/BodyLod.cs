using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Detail by distance for the bodies, so a crowd (or a heap of dead) costs what you can see of it:
    /// past the graphics setting's shadow range a body stops casting shadows (its forty-odd pieces would be drawn
    /// again for every shadow cascade), and the farther a living Earl is the less exactly the physics solves his
    /// joints (nobody can see a far man's elbow settle a millimetre off).
    /// Ticked by SandboxGame; a few bodies each frame.
    /// </summary>
    public static class BodyLod
    {
        class State
        {
            public readonly List<Renderer> casters = new List<Renderer>();
            public bool shadowsOn = true;
            public float refreshAt;
            public int solverTier = -1;
        }

        static readonly Dictionary<HumanBody, State> states = new Dictionary<HumanBody, State>();
        static int cursor;
        static readonly List<HumanBody> gone = new List<HumanBody>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { states.Clear(); cursor = 0; }

        public static void Tick()
        {
            var cam = Camera.main;
            if (cam == null || HumanBody.All.Count == 0) return;
            Vector3 eye = cam.transform.position;
            float range = GameSettings.BodyShadowRange;
            // a handful per frame: every body is looked at a few times a second
            int n = Mathf.Min(HumanBody.All.Count, 6);
            for (int k = 0; k < n; k++)
            {
                cursor = (cursor + 1) % HumanBody.All.Count;
                var h = HumanBody.All[cursor];
                if (h == null || h.Pelvis == null || h == PlayerAvatar.Body) continue;
                if (!states.TryGetValue(h, out var st)) states[h] = st = new State();
                float d = Vector3.Distance(eye, h.Pelvis.transform.position);

                // ---- shadows
                if (Time.time > st.refreshAt)
                {
                    // (what it wears and what has come off it changes: look again now and then)
                    st.refreshAt = Time.time + 4f;
                    var keep = new HashSet<Renderer>(st.casters);
                    st.casters.Clear();
                    foreach (var r in h.GetComponentsInChildren<Renderer>())
                        if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off || keep.Contains(r)) st.casters.Add(r);
                    if (!st.shadowsOn) SetShadows(st, false);
                }
                bool want = d < range;
                if (want != st.shadowsOn) { st.shadowsOn = want; SetShadows(st, want); }

                // ---- how exactly the living are solved (the dead look after themselves: ActiveRagdoll)
                if (h.Dead || h.ragdoll == null || h.ragdoll.IsCorpse) continue;
                int tier = d < 20f ? 0 : d < 45f ? 1 : 2;
                if (tier == st.solverTier) continue;
                st.solverTier = tier;
                int pos = tier == 0 ? (GameSettings.Graphics == 0 ? 10 : 14) : tier == 1 ? 8 : 6;
                int vel = tier == 0 ? (GameSettings.Graphics == 0 ? 4 : 6) : tier == 1 ? 3 : 2;
                foreach (var bp in h.partList)
                    if (bp != null && bp.rb != null) { bp.rb.solverIterations = pos; bp.rb.solverVelocityIterations = vel; }
            }
            // forget the ones that are gone
            if (Time.frameCount % 120 == 0)
            {
                gone.Clear();
                foreach (var kv in states) if (kv.Key == null) gone.Add(kv.Key);
                foreach (var g in gone) states.Remove(g);
            }
        }

        static void SetShadows(State st, bool on)
        {
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var r in st.casters) if (r != null) r.shadowCastingMode = mode;
        }

        /// <summary>The graphics setting changed: look at every body again.</summary>
        public static void Reset()
        {
            foreach (var kv in states) { kv.Value.solverTier = -1; kv.Value.refreshAt = 0f; }
        }
    }
}
