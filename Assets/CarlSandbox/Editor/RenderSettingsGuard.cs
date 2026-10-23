#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Carl.EditorTools
{
    /// <summary>
    /// The graphics setting (GameSettings.ApplyGraphics) changes the render pipeline asset and its renderer while the
    /// game runs. In the editor those are the project's own asset files, and changes made in play mode stick: a
    /// LOW setting tried in play mode was left in the files, and the next build left out the shaders for what LOW
    /// turns off (ambient occlusion, lamp shadows, soft shadows) - the built game's renderer then failed to start
    /// at all (a black window). So: what the pipeline asset, its renderers and their features look like is noted
    /// on the way into play mode and put back on the way out. The files keep the full (HIGH) set-up; builds carry
    /// every shader; the game turns things down at run time.
    /// </summary>
    [InitializeOnLoad]
    public static class RenderSettingsGuard
    {
        const string Key = "carl.renderSnapshot";

        static RenderSettingsGuard()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingEditMode) Snapshot();
                else if (s == PlayModeStateChange.EnteredEditMode) Restore();
            };
        }

        static List<Object> Targets()
        {
            var list = new List<Object>();
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset q) urp = q;
            if (urp == null) return list;
            list.Add(urp);
            var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f?.GetValue(urp) is ScriptableRendererData[] datas)
                foreach (var d in datas)
                {
                    if (d == null) continue;
                    list.Add(d);
                    foreach (var feat in d.rendererFeatures) if (feat != null) list.Add(feat);
                }
            return list;
        }

        static void Snapshot()
        {
            var parts = new List<string>();
            foreach (var o in Targets()) parts.Add(GlobalObjectId.GetGlobalObjectIdSlow(o) + "\u0001" + EditorJsonUtility.ToJson(o));
            SessionState.SetString(Key, string.Join("\u0002", parts));
        }

        static void Restore()
        {
            string all = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(all)) return;
            foreach (var part in all.Split('\u0002'))
            {
                int i = part.IndexOf('\u0001');
                if (i < 0 || !GlobalObjectId.TryParse(part.Substring(0, i), out var id)) continue;
                var o = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                if (o != null) EditorJsonUtility.FromJsonOverwrite(part.Substring(i + 1), o);
            }
            SessionState.EraseString(Key);
        }

        /// <summary>What the pipeline is set to right now (for checking from outside).</summary>
        public static string Describe()
        {
            var t = Targets();
            if (t.Count == 0) return "no URP asset";
            var urp = (UniversalRenderPipelineAsset)t[0];
            var sb = new System.Text.StringBuilder($"cascades {urp.shadowCascadeCount}, shadow res {urp.mainLightShadowmapResolution}, distance {urp.shadowDistance}, scale {urp.renderScale}, HDR {urp.supportsHDR}, opaque tex {urp.supportsCameraOpaqueTexture}");
            foreach (var o in t) if (o is ScriptableRendererFeature f) sb.Append($", {f.name} active {f.isActive}");
            return sb.ToString();
        }
    }
}
#endif
