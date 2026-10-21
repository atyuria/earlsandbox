using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A GPU blood field (R = thickness, G = dryness) that is stamped by impacts and simulated every frame.
    /// Used for world surface tiles and for each body part's skin.
    /// </summary>
    public class BloodCanvas
    {
        static Material mat;
        static readonly Vector4[] b0 = new Vector4[16], b1 = new Vector4[16], b2 = new Vector4[16];
        static readonly int S0 = Shader.PropertyToID("_S0"), S1 = Shader.PropertyToID("_S1"), S2 = Shader.PropertyToID("_S2");
        static readonly int Count = Shader.PropertyToID("_Count"), Wrap = Shader.PropertyToID("_WrapU");
        static readonly int Grav = Shader.PropertyToID("_Grav"), GravLocal = Shader.PropertyToID("_GravLocal");
        static readonly int Dt = Shader.PropertyToID("_Dt"), Spread = Shader.PropertyToID("_Spread"), Seed = Shader.PropertyToID("_Seed");
        static readonly int NL = Shader.PropertyToID("_NL"), NR = Shader.PropertyToID("_NR"), ND = Shader.PropertyToID("_ND"), NU = Shader.PropertyToID("_NU");
        static readonly int HasN = Shader.PropertyToID("_HasN"), TileOffset = Shader.PropertyToID("_TileOffset"), Body = Shader.PropertyToID("_Body");

        struct Stamp { public Vector4 s0, s1, s2; }

        RenderTexture a, b;
        readonly List<Stamp> pending = new List<Stamp>();
        readonly bool wrapU;
        readonly float seed = Random.value * 100f;
        public float activeUntil;
        public float amount;           // rough CPU estimate of blood present
        public RenderTexture Texture => a;
        public Texture mask;            // where the canvas lies on a real surface (world tiles)
        static readonly int Mask = Shader.PropertyToID("_Mask"), UseMask = Shader.PropertyToID("_UseMask");

        public BloodCanvas(int res, bool wrapU)
        {
            this.wrapU = wrapU;
            if (mat == null) mat = new Material(Mats.Find("Hidden/Carl/BloodSim"));
            var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf) ? RenderTextureFormat.RGHalf : RenderTextureFormat.ARGBHalf;
            a = Make(res, fmt);
            b = Make(res, fmt);
        }

        RenderTexture Make(int res, RenderTextureFormat fmt)
        {
            var rt = new RenderTexture(res, res, 0, fmt, RenderTextureReadWrite.Linear)
            {
                wrapMode = wrapU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                name = "BloodCanvas"
            };
            rt.Create();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = prev;
            return rt;
        }

        /// <summary>kind: 0 splat, 1 smear, 2 ooze. radius in uv units per axis.</summary>
        public void AddStamp(Vector2 uv, Vector2 radius, float amt, Vector2 dir, float stretch, int kind, float spatter, float seed = -1f)
        {
            if (dir.sqrMagnitude < 1e-6f) dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            pending.Add(new Stamp
            {
                s0 = new Vector4(uv.x, uv.y, radius.x, radius.y),
                s1 = new Vector4(dir.normalized.x, dir.normalized.y, Mathf.Max(1f, stretch), seed >= 0f ? seed : Random.value * 97f),
                s2 = new Vector4(amt, kind, spatter, 0f)
            });
            amount = Mathf.Max(0f, amount + amt * radius.x * radius.y * 40f);
            activeUntil = Mathf.Max(activeUntil, Time.time + 30f);
        }

        /// <summary>Apply pending stamps and run one simulation step. grav/gravLocal as in the shader.</summary>
        public void Step(float dt, Vector4 grav, Vector3 gravLocal, float spread, Texture nl = null, Texture nr = null, Texture nd = null, Texture nu = null, Vector2 tileOffset = default, bool body = false, float sharedSeed = -1f)
        {
            if (a == null) return;
            mat.SetFloat(Wrap, wrapU ? 1f : 0f);
            mat.SetTexture(Mask, mask ? mask : Texture2D.whiteTexture);
            mat.SetFloat(UseMask, mask ? 1f : 0f);
            mat.SetVector(TileOffset, tileOffset);
            int i = 0;
            while (i < pending.Count)
            {
                int n = Mathf.Min(16, pending.Count - i);
                for (int k = 0; k < n; k++) { var s = pending[i + k]; b0[k] = s.s0; b1[k] = s.s1; b2[k] = s.s2; }
                mat.SetVectorArray(S0, b0);
                mat.SetVectorArray(S1, b1);
                mat.SetVectorArray(S2, b2);
                mat.SetInt(Count, n);
                Graphics.Blit(a, b, mat, 0);
                Swap();
                i += n;
            }
            pending.Clear();
            if (dt <= 0f || Time.time > activeUntil) return;
            mat.SetVector(Grav, grav);
            mat.SetVector(GravLocal, gravLocal);
            mat.SetFloat(Dt, Mathf.Min(dt, 0.05f));
            mat.SetFloat(Spread, spread);
            mat.SetFloat(Seed, sharedSeed >= 0f ? sharedSeed : seed);
            var black = Texture2D.blackTexture;
            mat.SetTexture(NL, nl ? nl : black); mat.SetTexture(NR, nr ? nr : black);
            mat.SetTexture(ND, nd ? nd : black); mat.SetTexture(NU, nu ? nu : black);
            mat.SetVector(HasN, new Vector4(nl ? 1 : 0, nr ? 1 : 0, nd ? 1 : 0, nu ? 1 : 0));
            mat.SetFloat(Body, body ? 1f : 0f);
            Graphics.Blit(a, b, mat, 1);
            Swap();
        }

        void Swap() { var t = a; a = b; b = t; }

        public void Release()
        {
            if (a) { a.Release(); Object.Destroy(a); }
            if (b) { b.Release(); Object.Destroy(b); }
            a = b = null;
        }
    }
}
