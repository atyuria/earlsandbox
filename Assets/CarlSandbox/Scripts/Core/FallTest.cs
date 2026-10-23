using System.Collections;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Playtest of getting up: knocks you down, has you get up and walk straight off (as a player does, holding
    /// space then W), a number of times, and counts how often you go straight back down. Temp/falltest.txt.
    /// </summary>
    public class FallTest : MonoBehaviour
    {
        public static void Run(int cycles = 6) { var t = new GameObject("FallTest").AddComponent<FallTest>(); t.cycles = cycles; }
        int cycles;
        bool walk, getUp;
        Vector3 dir;
        readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

        void LateUpdate()
        {
            var me = PlayerAvatar.Body;
            if (me == null) return;
            var rag = me.ragdoll;
            if (getUp && rag.IsDown) rag.wantStand = true;
            if (walk && rag.mode == BodyMode.Standing) { rag.moveDir = dir; rag.moveSpeed = 2.3f * (rag.standGrace > 0f ? Mathf.Lerp(1f, 0.45f, rag.standGrace / 2.5f) : 1f); }
        }

        IEnumerator Start()
        {
            ActiveRagdoll.DebugFalls = true;
            int refalls = 0;
            for (int c = 0; c < cycles; c++)
            {
                var me = PlayerAvatar.Body;
                var rag = me.ragdoll;
                walk = false; getUp = false;
                rag.Fall("test: knocked down");
                yield return new WaitForSeconds(1.5f);
                getUp = true;
                float t0 = Time.time;
                while (rag.mode != BodyMode.Standing && Time.time - t0 < 15f) yield return null;
                float upAt = Time.time;
                bool stood = rag.mode == BodyMode.Standing;
                // straight off, a different way each time (along and across the trench)
                float a = c * 70f;
                dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                walk = true;
                bool fell = false;
                while (Time.time - upAt < 4f) { if (rag.mode != BodyMode.Standing || rag.IsSinking) { fell = true; break; } yield return null; }
                walk = false;
                if (fell) refalls++;
                log.AppendLine($"cycle {c}: got up {(stood ? $"in {upAt - t0:0.0}s" : "NO")}, walked off {(fell ? "and FELL" : "fine")}");
                yield return new WaitForSeconds(fell ? 6f : 0.5f);
                if (fell) { getUp = true; float t1 = Time.time; while (rag.mode != BodyMode.Standing && Time.time - t1 < 15f) yield return null; }
            }
            log.AppendLine($"re-falls {refalls}/{cycles}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/falltest.txt"), log.ToString());
            Destroy(gameObject);
        }
    }
}
