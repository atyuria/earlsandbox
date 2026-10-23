#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Carl.EditorTools
{
    /// <summary>Puts a shader on the Always Included list, so Shader.Find still finds it in a built game.</summary>
    public static class ShaderIncluder
    {
        public static string Include(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) return "no shader " + shaderName;
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return "already included";
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return "included " + shaderName;
        }
    }
}
#endif
