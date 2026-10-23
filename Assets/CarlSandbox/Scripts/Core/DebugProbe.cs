using UnityEngine;

namespace Carl
{
    /// <summary>Editor-side pokes at the running game (called through the editor bridge while testing).</summary>
    public static class DebugProbe
    {
        /// <summary>Every body: where, standing or down, side, what it's doing.</summary>
        public static string Bodies()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var h in HumanBody.All)
                if (h != null && h.Pelvis != null)
                    sb.AppendLine($"{h.DisplayName} at {h.Pelvis.transform.position} down {h.ragdoll.IsDown} dead {h.Dead} side {Teams.Of(h)} {(h.gunner != null ? h.gunner.Status + " " + h.gunner.NavDebug : "")}");
            return sb.ToString();
        }

        /// <summary>Every collider within r of a point, and what it belongs to.</summary>
        public static string Colliders(float x, float y, float z, float r)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in Physics.OverlapSphere(new Vector3(x, y, z), r, ~0, QueryTriggerInteraction.Collide))
            {
                var hb = c.GetComponentInParent<HumanBody>();
                sb.AppendLine($"{c.name} ({c.GetType().Name}{(c.isTrigger ? ", trigger" : "")}) parent {(c.transform.parent ? c.transform.parent.name : "-")} {(hb != null ? "body " + hb.DisplayName : "")} at {c.bounds.center} size {c.bounds.size}");
            }
            return sb.ToString();
        }
    }
}
