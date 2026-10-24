#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Carl
{
    /// <summary>For the bridge: does a shader compile, and if not, what does Unity say.</summary>
    public static class ShaderCheck
    {
        public static string Errors(string name)
        {
            var sh = Shader.Find(name);
            if (sh == null) return "no shader " + name;
            var msgs = ShaderUtil.GetShaderMessages(sh);
            var sb = new System.Text.StringBuilder($"{name}: hasError {ShaderUtil.ShaderHasError(sh)}, {msgs.Length} messages\n");
            foreach (var m in msgs) sb.AppendLine($"{m.severity} line {m.line}: {m.message} {m.messageDetails}");
            return sb.ToString();
        }
    }
}
#endif
