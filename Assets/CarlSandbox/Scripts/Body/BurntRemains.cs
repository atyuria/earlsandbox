using UnityEngine;

namespace Carl
{
    /// <summary>
    /// What's left once a body has burned out: no two the same. Each one comes out as one of
    ///   CRUST   black-brown crust split open in patches of dark raw red
    ///   ASHEN   dried right down, thin limbs under a grey-white ash
    ///   MELTED  sagged down into the floor and spread over it where it lies, a dark burnt stain round it
    /// (all drawn by the skin shader on top of the char - BodyPart.SetBurnStyle).
    /// </summary>
    public class BurntRemains : MonoBehaviour
    {
        enum Kind { Crust, Ashen, Melted }
        HumanBody body;
        Kind kind;
        Vector4 style;
        float t;
        Vector3 floor;
        bool stained;

        /// <summary>A burned-out body takes on its look (once).</summary>
        public static void Begin(HumanBody h)
        {
            if (h == null || h.GetComponent<BurntRemains>() != null) return;
            var r = h.gameObject.AddComponent<BurntRemains>();
            r.body = h;
            float v = Random.value;
            r.kind = v < 0.35f ? Kind.Melted : v < 0.65f ? Kind.Ashen : Kind.Crust;
            r.style = r.kind == Kind.Ashen ? new Vector4(1f, 0.2f, 0f, 0f) : r.kind == Kind.Crust ? new Vector4(0f, 1f, 0f, 0f) : new Vector4(0.2f, 0.6f, 0f, 0f);
            r.Apply();
        }

        void Apply()
        {
            Vector3 c = body.Center;
            foreach (var bp in body.partList) if (bp != null) bp.SetBurnStyle(style, c);
        }

        void Update()
        {
            if (body == null) { Destroy(this); return; }
            if (kind != Kind.Melted) { enabled = false; return; }
            // melting down: only once it's lying still on the floor, over about twenty seconds
            if (!body.Dead || !body.ragdoll.IsDown || body.Pelvis == null || body.Pelvis.rb == null || body.Pelvis.rb.linearVelocity.sqrMagnitude > 0.05f) return;
            if (!stained)
            {
                stained = true;
                Vector3 p = body.Pelvis.transform.position;
                floor = p - Vector3.up * 0.3f;
                foreach (var hit in Physics.RaycastAll(p + Vector3.up * 0.3f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.GetComponentInParent<HumanBody>() == null && (hit.rigidbody == null || hit.rigidbody.isKinematic)) { floor = hit.point; break; }
                // the dark, greasy burn it leaves on the floor under and round it
                BloodSystem.Instance?.Mark(floor, Vector3.up, 1.3f, 0.9f);
                foreach (var bp in body.partList)
                    if (bp != null && Random.value < 0.6f)
                        BloodSystem.Instance?.Mark(new Vector3(bp.transform.position.x, floor.y, bp.transform.position.z) + Random.insideUnitSphere * 0.15f, Vector3.up, Random.Range(0.35f, 0.7f), 0.8f);
            }
            t += Time.deltaTime;
            style.z = Mathf.SmoothStep(0f, 0.85f, t / 20f);
            style.w = floor.y;
            Apply();
            if (t > 20f) enabled = false;
        }
    }
}
