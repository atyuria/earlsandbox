using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// Where the frame goes: samples every timing marker for a number of frames and writes the most expensive to
    /// Temp/perf.txt (average ms per frame), for finding what to optimise. PerfProbe.Run() from the editor.
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        readonly List<ProfilerRecorder> recs = new List<ProfilerRecorder>();
        readonly List<string> names = new List<string>();
        double[] total;
        int frames, want;
        float dtSum;

        /// <summary>The file the next probe writes (perf.txt by default).</summary>
        public static string OutName = "perf.txt";
        public static void Run(int frames = 300)
        {
            var p = new GameObject("PerfProbe").AddComponent<PerfProbe>();
            p.want = frames;
        }

        /// <summary>Spikes instead of averages: over the frames, the single worst one, and what it spent its time on.</summary>
        public static void RunSpike(int frames = 300)
        {
            var p = new GameObject("PerfProbe").AddComponent<PerfProbe>();
            p.want = frames; p.spike = true;
        }
        bool spike;
        double[] worst;
        double worstLoop;
        int loopIdx = -1;

        void Start()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var h in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                recs.Add(ProfilerRecorder.StartNew(d.Category, d.Name, 1));
                names.Add(d.Category.Name + " / " + d.Name);
            }
            total = new double[recs.Count];
            worst = new double[recs.Count];
            loopIdx = names.IndexOf("PlayerLoop / PlayerLoop");
        }

        // ---- split mode: each frame counted under Bucket (0 not firing, 1 firing; -1 not counted), and the report is
        // what the second costs over the first, marker by marker (same battle, so the battle itself cancels out)
        public static int Bucket = -1;
        bool split;
        readonly double[][] bt = new double[2][];
        readonly int[] bf = new int[2];
        readonly float[] bdt = new float[2];

        public static PerfProbe RunSplit()
        {
            var p = new GameObject("PerfProbe").AddComponent<PerfProbe>();
            p.split = true; p.want = int.MaxValue;
            return p;
        }

        /// <summary>Split mode: write the comparison now and stop.</summary>
        public void Finish()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"not firing: {bf[0]} frames, {bdt[0] / Mathf.Max(1, bf[0]) * 1000f:0.00} ms/frame   firing: {bf[1]} frames, {bdt[1] / Mathf.Max(1, bf[1]) * 1000f:0.00} ms/frame");
            sb.AppendLine("extra ms per frame while firing (firing minus not firing), then not firing / firing:");
            var idx = new List<int>();
            for (int i = 0; i < recs.Count; i++) idx.Add(i);
            double Per(int b, int i) => bt[b] == null || bf[b] == 0 ? 0 : bt[b][i] / bf[b] / 1e6;
            idx.Sort((a, c) => (Per(1, c) - Per(0, c)).CompareTo(Per(1, a) - Per(0, a)));
            for (int k = 0; k < idx.Count && k < 120; k++)
            {
                int i = idx[k];
                sb.AppendLine($"{Per(1, i) - Per(0, i):+0.000;-0.000} ms   ({Per(0, i):0.000} / {Per(1, i):0.000})  {names[i]}");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, Application.isEditor ? "../Temp/" + OutName : "../" + OutName), sb.ToString());
            foreach (var r in recs) r.Dispose();
            Destroy(gameObject);
        }

        void LateUpdate()
        {
            if (total == null) return;
            if (split)
            {
                if (Bucket < 0 || Bucket > 1) return;
                bt[Bucket] ??= new double[recs.Count];
                bf[Bucket]++; bdt[Bucket] += Time.unscaledDeltaTime;
                for (int i = 0; i < recs.Count; i++) if (recs[i].Valid) bt[Bucket][i] += recs[i].LastValue;
                return;
            }
            frames++;
            dtSum += Time.unscaledDeltaTime;
            for (int i = 0; i < recs.Count; i++) if (recs[i].Valid) total[i] += recs[i].LastValue;
            // (spikes: the worst each marker ever took in a single frame)
            if (spike && frames > 3) for (int i = 0; i < recs.Count; i++) if (recs[i].Valid && recs[i].LastValue > worst[i]) worst[i] = recs[i].LastValue;
            if (frames < want) return;
            if (spike) { total = worst; frames = 1; }
            var idx = new List<int>();
            for (int i = 0; i < recs.Count; i++) if (total[i] > 0) idx.Add(i);
            idx.Sort((a, b) => total[b].CompareTo(total[a]));
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{frames} frames, {dtSum / frames * 1000f:0.0} ms/frame ({frames / dtSum:0} fps), {HumanBody.All.Count} bodies, {EarlGunner.All.Count} gunners");
            for (int k = 0; k < idx.Count && k < 90; k++) sb.AppendLine($"{total[idx[k]] / frames / 1e6:0.000} ms  {names[idx[k]]}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, Application.isEditor ? "../Temp/" + OutName : "../" + OutName), sb.ToString());
            foreach (var r in recs) r.Dispose();
            Destroy(gameObject);
        }
    }
}

namespace Carl
{
    /// <summary>Timing markers round the per-shot and per-hit work (seen by PerfProbe, in a development build too).</summary>
    public static class Prof
    {
        public static readonly Unity.Profiling.ProfilerMarker Hit = new Unity.Profiling.ProfilerMarker("Carl.Hit.Projectile"),
            Fire = new Unity.Profiling.ProfilerMarker("Carl.Shot.FireGun"), Casing = new Unity.Profiling.ProfilerMarker("Carl.Shot.Casing"),
            Channels = new Unity.Profiling.ProfilerMarker("Carl.Hit.WoundChannels"), Holes = new Unity.Profiling.ProfilerMarker("Carl.Hit.PushHoles"),
            Bullets = new Unity.Profiling.ProfilerMarker("Carl.Bullets.Fly");
    }
}
