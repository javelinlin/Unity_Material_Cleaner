// jave.lin : 2023/03/08 清理材质工具
// refer to : Unity材质冗余序列化数据清理 https://zhuanlan.zhihu.com/p/366636732，但是这个参考的工具有 BUG，仅供参考

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ClearupMatPropKWTools_EXT
{
    public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> range)
    {
        foreach (T item in range)
        {
            hashSet.Add(item);
        }
    }
}

public class ClearUpMatPropKWTools : EditorWindow
{
    [MenuItem("实用工具/Materials/材质[Prop&KW]清理器...")]
    private static void _Show()
    {
        var win = EditorWindow.GetWindow<ClearUpMatPropKWTools>();
        win.titleContent = new GUIContent("材质[Prop&KW]清理器");
        win.Show();
    }

    private void OnGUI()
    {
        var assetRootPath = string.Empty;
        var new_assetRootPathObj = EditorGUILayout.ObjectField("AssetRootPath(Default : Assets)", assetRootPathObj, typeof(UnityEngine.Object), false);
        if (assetRootPathObj != new_assetRootPathObj)
        {
            assetRootPathObj = new_assetRootPathObj;
            //Debug.Log($"Role Folder Name : {assetRootPathObj.name}");
        }

        if (assetRootPathObj != null)
        {
            assetRootPath = AssetDatabase.GetAssetPath(assetRootPathObj);
        }

        var temp_assetRootPath = string.IsNullOrEmpty(assetRootPath) ? "Assets" : assetRootPath;

        {
            GUILayout.Label("Exclude Path:");
            var src_enabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.indentLevel++;
            foreach (var excludePath in excludePathList)
            {
                GUILayout.TextField(excludePath);
            }
            EditorGUI.indentLevel--;
            GUI.enabled = src_enabled;
        }

        if (GUILayout.Button("清理 所有 材质"))
        {
            ClearUp(temp_assetRootPath, All_Shader_Filter);
        }
        if (GUILayout.Button("清理 PBR 材质"))
        {
            ClearUp(temp_assetRootPath, PBR_Shader_Filter);
        }
    }

    [MenuItem("实用工具/Materials/执行清理所有材质")]
    public static void ClearAllMat()
    {
        ClearUp("Assets", All_Shader_Filter);
    }

    private static UnityEngine.Object assetRootPathObj;
    private static List<string> excludePathList = new List<string>() 
    {
        "Assets/CasualGame",
        "Assets/Ultimate Game Tools",
        "Assets/StreamingAssets",
    };

    private static bool PBR_Shader_Filter(Material mat)
    {
        return mat.shader.name.Contains("PBR");
    }

    private static bool All_Shader_Filter(Material mat)
    {
        return true;
    }

    private static bool ClearInvalidatedProps(Material mat, SerializedProperty sProp, StringBuilder sb = null)
    {
        var changed = false;
        var shader = mat.shader;
        for (int i = sProp.arraySize - 1; i > -1; i--)
        {
            var propName = sProp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
            // jave.lin : 如果 shader 定义的属性也不存在，那就删除
            if (shader.FindPropertyIndex(propName) == -1)
            {
                if (sb != null)
                    sb.Append($"{propName}, ");
                sProp.DeleteArrayElementAtIndex(i);
                changed = true;
            }
        }
        return changed;
    }

    private static bool ClearUpProps(Material mat, SerializedObject sObj, StringBuilder sbLog)
    {
        var changed = false;
        var shader = mat.shader;
        //var sObj = new SerializedObject(mat);
        var m_SavedPropertiesProp = sObj.FindProperty("m_SavedProperties");
        var m_TexEnvsProps = m_SavedPropertiesProp.FindPropertyRelative("m_TexEnvs");
        var m_FloatsEnvsProps = m_SavedPropertiesProp.FindPropertyRelative("m_Floats");
        var m_ColorsEnvsProps = m_SavedPropertiesProp.FindPropertyRelative("m_Colors");

        if (sbLog != null)
            sbLog.AppendLine($"ClearUpProps mat : {AssetDatabase.GetAssetPath(mat)}");

        //var shaderPath = AssetDatabase.GetAssetPath(shader);
        //var kws = Scavenger.GetShaderKeywords(shaderPath);
        //if (sbLog != null)
        //    sbLog.AppendLine($"ClearUpProps shaderPath : {shaderPath}, kws : {string.Join(",", kws.ToArray())}");

        if (sbLog != null)
            sbLog.Append("ClearUpProps remove invalidted propName :");

        if (ClearInvalidatedProps(mat, m_TexEnvsProps, sbLog)) changed = true;
        if (ClearInvalidatedProps(mat, m_FloatsEnvsProps, sbLog)) changed = true;
        if (ClearInvalidatedProps(mat, m_ColorsEnvsProps, sbLog)) changed = true;

        if (sbLog != null)
            sbLog.AppendLine();

        if (sbLog != null)
            Debug.Log(sbLog.ToString());

        return changed;
    }
    private static System.Reflection.BindingFlags BindFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    private static TResult Inovke<TResult>(Type type, string methodName, BindingFlags bindFlags, params object[] param)
    {
        if (type == null)
            return default(TResult);
        MethodInfo method = type.GetMethod(methodName, bindFlags);
        if (method == null)
            return default(TResult);
        return (TResult)method.Invoke(null, param);
    }

    private static HashSet<string> validatedKW_Hashset_helper = new HashSet<string>();
    private static List<string> keywords_temp_helper = new List<string>();

    private static bool ClearUpKeywords(Material mat, SerializedObject sObj, StringBuilder sbLog)
    {
        if (mat == null || mat.shader == null)
        {
            if (sbLog != null)
                sbLog.AppendLine("ClearUpKeywords mat == null || mat.shader == null");
            return false;
        }

        var changed = false;

        //var shaderPath = AssetDatabase.GetAssetPath(mat.shader);
        //var shader_kw = Scavenger.GetShaderKeywords(shaderPath);
        //if (sbLog != null)
        //    sbLog.AppendLine($"ClearUpKeywords shaderPath : {shaderPath}, shader_kw : {string.Join(",", shader_kw.ToArray())}");

        // 通过反射获取材质所使用的Shader的Keyword列表
        var globalKeywords = Inovke<string[]>(typeof(ShaderUtil), "GetShaderGlobalKeywords", BindFlags, mat.shader);
        var localKeywords = Inovke<string[]>(typeof(ShaderUtil), "GetShaderLocalKeywords", BindFlags, mat.shader);

        if (globalKeywords == null || localKeywords == null)
        {
            if (sbLog != null)
                sbLog.AppendLine("globalKeywords == null || localKeywords == null");
            return false;
        }

        validatedKW_Hashset_helper.Clear();
        validatedKW_Hashset_helper.AddRange(globalKeywords);
        validatedKW_Hashset_helper.AddRange(localKeywords);

        if (sbLog != null)
            sbLog.AppendLine($"validated kw : {string.Join(",", validatedKW_Hashset_helper.ToArray())}");

        // 删除材质中存在，但是Shader中却不存在的Keyword
        keywords_temp_helper.Clear();
        keywords_temp_helper.AddRange(mat.shaderKeywords);

        if (sbLog != null)
            sbLog.Append("delete key words: ");

        for (int i = keywords_temp_helper.Count - 1; i > -1; i--)
        {
            var keyword = keywords_temp_helper[i];
            if (validatedKW_Hashset_helper.Contains(keyword))
                continue;
            if (sbLog != null)
                sbLog.Append(keyword + ",");
            //mat.DisableKeyword(keyword);
            keywords_temp_helper.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
            var m_ShaderKeywordsProps = sObj.FindProperty("m_ShaderKeywords");
            //Debug.Log(m_ShaderKeywordsProps.stringValue);
            m_ShaderKeywordsProps.stringValue = string.Join(" ", keywords_temp_helper);
        }

        return changed;
    }

    private static void ClearUp(string matRootPath, Func<Material, bool> filter = null)
    {
        var ret = false;
        var error = string.Empty;
        try
        {
            var matList = new List<Material>();
            var matPathList = new List<string>();
            FindMatPaths(matRootPath, matPathList);
            var title = $"{nameof(ClearUpMatPropKWTools)}.{nameof(ClearUp)} [Analysing]";
            for (int i = 0; i < matPathList.Count; i++)
            {
                EditorUtility.DisplayProgressBar(title, $"{i + 1}/{matPathList.Count}", (float)(i + 1) / matPathList.Count);
                var assetPath = matPathList[i];
                Material mat = null;
                try
                {
                    mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                }
                catch (System.Exception er)
                {
                    Debug.Log($"Clearup LoadAssetAtPath error, assetPath : {assetPath}, error : {er}");
                }

                if (filter != null && !filter.Invoke(mat))
                    continue;

                //var shader = mat.shader;
                //if (!shader.name.StartsWith("PBR"))
                //    continue;

                matList.Add(mat);

                // TEST : jave.lin : 先测试一个
                //break;
            }

            //var sbLog = GetStrBuilder();
            StringBuilder sbLog = null;

            title = $"{nameof(ClearUpMatPropKWTools)}.{nameof(ClearUp)} [Clearing]";
            var anyChanged = false;
            for (int i = 0; i < matList.Count; i++)
            {
                var mat = matList[i];
                EditorUtility.DisplayProgressBar(title, $"{i + 1}/{matList.Count}", (float)(i + 1) / matList.Count);
                var changed = false;
                var sObj = new SerializedObject(mat);
                // jave.lin : 注意 props 和 keywords 的 field name 不同，如果有相同的話，那么 ClearUpProps 和 ClearUpKeywords 之后 apply modified props 要单独执行
                // jave.lin : Note that the field names for props and keywords are different. If they are the same, then apply modified props is executed separately after ClearUpProps and ClearUpKeywords
                // clears-up props
                if (ClearUpProps(mat, sObj, sbLog))
                {
                    changed = true;
                }
                // clears-up keywords
                if (ClearUpKeywords(mat, sObj, sbLog))
                {
                    changed = true;
                }
                if (changed)
                {
                    anyChanged = true;
                    sObj.ApplyModifiedProperties();
                    EditorUtility.SetDirty(mat);
                }
                if (sbLog != null)
                {
                    Debug.Log(sbLog.ToString());
                    sbLog.Clear();
                }
            }
            if (anyChanged)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            ret = true;
            error = string.Empty;
        }
        catch (System.Exception er)
        {
            ret = false;
            error = er.ToString();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (ret)
            {
                Debug.Log($"{nameof(ClearUpMatPropKWTools)}.{nameof(ClearUp)} Successfully!");
            }
            else
            {
                Debug.LogError($"{nameof(ClearUpMatPropKWTools)}.{nameof(ClearUp)} Failured, Error : {error}");
            }
        }
    }

    private static void FindMatPaths(string rootPath, List<string> pathList)
    {
        var guids = AssetDatabase.FindAssets("t:Material", new string[] { rootPath });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ext = System.IO.Path.GetExtension(path).ToLower();
            if (ext == ".fbx" || ext == ".obj") continue;
            var isExcludePath = false;
            foreach (var excludePath in excludePathList)
            {
                if (path.StartsWith(excludePath))
                {
                    isExcludePath = true;
                    break;
                }
            }
            if (!isExcludePath)
            {
                pathList.Add(path);
            }
        }
    }
}
