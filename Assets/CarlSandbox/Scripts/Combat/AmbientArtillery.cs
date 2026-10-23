using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The war going on round you (TRENCHES): every 10-30 seconds a shell comes in somewhere in the fighting - near
    /// men who are shooting at each other if anyone is, otherwise out in no-man's-land - with its scream on the way
    /// in. Never right on top of you (it's not aimed at you), but close enough to keep your head down.
    /// </summary>
    public class AmbientArtillery : MonoBehaviour
    {
        float next;

        public static void Attach(GameObject g) => g.AddComponent<AmbientArtillery>();

        void Update()
        {
            var g = SandboxGame.Instance;
            bool on = g != null && !g.InMenu && MapBuilder.TrenchLinesNow != null && GameSettings.SpawnNpcs && !WorldFreeze.On;
            if (!on) { next = Time.time + Random.Range(10f, 30f); return; }
            if (Time.time < next) return;
            next = Time.time + Random.Range(10f, 30f);
            if (TryPick(out var at)) Barrage.Fire(at, 0.1f, 0f, 0.3f);
        }

        /// <summary>Somewhere in the fighting: by someone in a firefight, or out in no-man's-land.</summary>
        static bool TryPick(out Vector3 at)
        {
            at = Vector3.zero;
            Vector3 you = PlayerAvatar.Body != null ? PlayerAvatar.Body.Center : SandboxGame.Instance.player.transform.position;
            for (int tries = 0; tries < 12; tries++)
            {
                Vector3 c;
                // a man in a firefight (either side), most of the time; else the open ground between the lines
                EarlGunner pick = null; int n = 0;
                foreach (var e in EarlGunner.All)
                    if (e != null && e.Fighting && e.SeesEnemy && Random.Range(0, ++n) == 0) pick = e;
                if (pick != null && Random.value < 0.7f) c = pick.Body.Center + new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f));
                else c = new Vector3(you.x + Random.Range(-45f, 45f), 0f, Random.Range(-14f, 14f));
                // not on top of you
                if (Vector3.Distance(new Vector3(c.x, you.y, c.z), you) < 12f) continue;
                if (!Physics.Raycast(new Vector3(c.x, 60f, c.z), Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<HumanBody>() != null) continue;
                at = hit.point;
                return true;
            }
            return false;
        }
    }
}
