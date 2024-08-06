// jave.lin : 2023/03/08 清理材质工具
// refer to : Unity材质冗余序列化数据清理 https://zhuanlan.zhihu.com/p/366636732，但是这个参考的工具有 BUG，仅供参考

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public struct EnabledRegion : IDisposable
{
    private bool srcVale;
    public EnabledRegion(bool val)
    {
        srcVale = GUI.enabled;
        GUI.enabled = val;
    }
    public void Dispose()
    {
        GUI.enabled = srcVale;
    }
}

public struct HorizontalRegion : IDisposable
{
    public HorizontalRegion(int test)
    {
        EditorGUILayout.BeginHorizontal();
    }

    public void Dispose()
    {
        EditorGUILayout.EndHorizontal();
    }
}

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
    [MenuItem("Tools/Materials/MaterialCleaner...")]
    private static void _Show()
    {
        var win = EditorWindow.GetWindow<ClearUpMatPropKWTools>();
        win.titleContent = new GUIContent("MaterialCleaner");
        win.Show();
    }

    private static string _RichTextWrapper(string msg, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{msg}</color>";
    }
    
    private void OnGUI()
    {
        { // clear path
            var assetRootPath = string.Empty;
            using (var hr = new HorizontalRegion(1))
            {
                EditorGUILayout.LabelField("AssetRootPath(Default : Assets)", GUILayout.Width(190));
                var new_assetRootPathObj =
                    EditorGUILayout.ObjectField(_s_pAssetRootPathObj, typeof(UnityEngine.Object), false);
                if (_s_pAssetRootPathObj != new_assetRootPathObj)
                {
                    _s_pAssetRootPathObj = new_assetRootPathObj;
                    //Debug.Log($"Role Folder Name : {assetRootPathObj.name}");
                }
            }

            if (_s_pAssetRootPathObj != null)
            {
                assetRootPath = AssetDatabase.GetAssetPath(_s_pAssetRootPathObj);
            }

            var temp_assetRootPath = string.IsNullOrEmpty(assetRootPath) ? "Assets" : assetRootPath;
            if (GUILayout.Button("Clear mats of the Path"))
            {
                ClearUpByPath(temp_assetRootPath, All_Shader_Filter);
            }

            var color = Color.green;
            ColorUtility.TryParseHtmlString("0xAAAAAA", out var content_coolor);
            var clear_rang_str = temp_assetRootPath;
            GUILayout.Label(
                _RichTextWrapper($"You Will Clear {_RichTextWrapper(clear_rang_str, content_coolor)} path.", color),
                new GUIStyle { richText = true });
        }

        { // clear materials of the scene obj
            GameObject new_assetRootPathObj = null;
            using (var hr = new HorizontalRegion(1))
            {
                EditorGUILayout.LabelField("GameObject(project asset, or scene object):", GUILayout.Width(250));
                new_assetRootPathObj = EditorGUILayout.ObjectField(_s_pAssetOrSceneObj, typeof(UnityEngine.GameObject), true) as GameObject;
                if (_s_pAssetOrSceneObj != new_assetRootPathObj)
                {
                    _s_pAssetOrSceneObj = new_assetRootPathObj;
                    //Debug.Log($"Role Folder Name : {projectAssetOrSceneObj.name}");
                }
            }

            using (var enabled = new EnabledRegion(new_assetRootPathObj != null))
            {
                if (GUILayout.Button("Clear mats of the GO"))
                {
                    ClearUpByGO(new_assetRootPathObj, All_Shader_Filter);
                }
            }

            var color = Color.green;
            ColorUtility.TryParseHtmlString("0xAAAAAA", out var content_coolor);
            var goName = new_assetRootPathObj != null ? new_assetRootPathObj.name : "null";
            GUILayout.Label(
                _RichTextWrapper($"You Will Clear {_RichTextWrapper($"GO:{goName}", content_coolor)} path.", color),
                new GUIStyle { richText = true });
        }
    }

    // 这个可以在 发布打包资源前，调用一下
    public static void ClearAllMat()
    {
        ClearUpByPath("Assets", All_Shader_Filter);
    }

    private static System.Reflection.BindingFlags _s_vBindFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    private static HashSet<string> _s_pHashSetValidatedKeywordhelper = new HashSet<string>();
    private static List<string> _s_pListKeywordsTempHelper = new List<string>();
    private static List<Renderer> _s_pListRenderHelper = new List<Renderer>();
    
    private static List<string> _s_pListExcludePathList = new List<string>()
    {
        // "Assets/CasualGame",
        // "Assets/Ultimate Game Tools",
        // "Assets/StreamingAssets",
    };
    
    private static UnityEngine.Object _s_pAssetRootPathObj;
    private static UnityEngine.GameObject _s_pAssetOrSceneObj;

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
    
    private static TResult Inovke<TResult>(Type type, string methodName, BindingFlags bindFlags, params object[] param)
    {
        if (type == null)
            return default(TResult);
        MethodInfo method = type.GetMethod(methodName, bindFlags);
        if (method == null)
            return default(TResult);
        return (TResult)method.Invoke(null, param);
    }

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
        var globalKeywords = Inovke<string[]>(typeof(ShaderUtil), "GetShaderGlobalKeywords", _s_vBindFlags, mat.shader);
        var localKeywords = Inovke<string[]>(typeof(ShaderUtil), "GetShaderLocalKeywords", _s_vBindFlags, mat.shader);

        if (globalKeywords == null || localKeywords == null)
        {
            if (sbLog != null)
                sbLog.AppendLine("globalKeywords == null || localKeywords == null");
            return false;
        }

        _s_pHashSetValidatedKeywordhelper.Clear();
        _s_pHashSetValidatedKeywordhelper.AddRange(globalKeywords);
        _s_pHashSetValidatedKeywordhelper.AddRange(localKeywords);

        if (sbLog != null)
            sbLog.AppendLine($"validated kw : {string.Join(",", _s_pHashSetValidatedKeywordhelper.ToArray())}");

        // 删除材质中存在，但是Shader中却不存在的Keyword
        var usingKeyworldList = _s_pListKeywordsTempHelper;
        usingKeyworldList.Clear();

        // jave.lin : prechecking invalidate keywords
        var precheckProp = sObj.FindProperty("m_InvalidKeywords");
        if (precheckProp != null)
        {
            changed = precheckProp.arraySize != 0;
        }

        // 提取 keywords
        usingKeyworldList.AddRange(mat.shaderKeywords.Distinct());
        if (!changed && usingKeyworldList.Count != mat.shaderKeywords.Length)
        {
            changed = true;
        }

        if (sbLog != null)
            sbLog.Append("delete key words: ");

        for (int i = usingKeyworldList.Count - 1; i > -1; i--)
        {
            var keyword = usingKeyworldList[i];
            if (_s_pHashSetValidatedKeywordhelper.Contains(keyword))
                continue;
            if (sbLog != null)
                sbLog.Append(keyword + ",");
            //mat.DisableKeyword(keyword);
            usingKeyworldList.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
            // jave.lin : 兼容一下低版本，和 高版本的字段区别
            var m_ShaderKeywordsProps = sObj.FindProperty("m_ShaderKeywords");
            if (m_ShaderKeywordsProps != null)
            {
                //Debug.Log(m_ShaderKeywordsProps.stringValue);
                m_ShaderKeywordsProps.stringValue = string.Join(" ", usingKeyworldList);
            }
            // jave.lin : 高版本中，这块的属性名字都变了
            else
            {
                // clearing invalidate keywords
                var invalidKeywords = sObj.FindProperty("m_InvalidKeywords");
                if (invalidKeywords != null)
                {
                    invalidKeywords.ClearArray();
                }

                // adding validated keywords
                var validKeywords = sObj.FindProperty("m_ValidKeywords");
                if (validKeywords != null)
                {
                    validKeywords.ClearArray();

                    foreach (var keyword in usingKeyworldList)
                    {
                        validKeywords.InsertArrayElementAtIndex(validKeywords.arraySize);
                        validKeywords.GetArrayElementAtIndex(validKeywords.arraySize - 1).stringValue = keyword;
                    }
                }
            }
        }

        return changed;
    }

    private static void ClearUpByGO(GameObject go, Func<Material, bool> filter = null)
    {
        var matPathList = new List<string>();
        FindMatPaths(go, matPathList);
        ClearUpMatList(matPathList, out var ret, out var error, filter);
    }
    
    private static void ClearUpByPath(string matRootPath, Func<Material, bool> filter = null)
    {
        var matPathList = new List<string>();
        FindMatPaths(matRootPath, matPathList);
        ClearUpMatList(matPathList, out var ret, out var error, filter);
    }

    private static void ClearUpMatList(List<string> matPathList, out bool ret, out string error, Func<Material, bool> filter = null)
    {
        ret = false;
        error = string.Empty;
        try
        {
            var matList = new List<Material>();

            var title = $"{nameof(ClearUpMatPropKWTools)}.{nameof(ClearUpByPath)} [Analysing]";
            for (int i = 0; i < matPathList.Count; i++)
            {
                EditorUtility.DisplayProgressBar(title, $"{i + 1}/{matPathList.Count}",
                    (float)(i + 1) / matPathList.Count);
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

            title = $"{nameof(ClearUpMatPropKWTools)}.{nameof(ClearUpByPath)} [Clearing]";
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
                Debug.Log($"{nameof(ClearUpMatPropKWTools)}.{nameof(ClearUpByPath)} Successfully!");
            }
            else
            {
                Debug.LogError($"{nameof(ClearUpMatPropKWTools)}.{nameof(ClearUpByPath)} Failured, Error : {error}");
            }
        }
    }

    private static void FindMatPaths(GameObject go, List<string> pathList)
    {
        _s_pListRenderHelper.Clear();
        go.GetComponentsInChildren<Renderer>(true, _s_pListRenderHelper);
        foreach (var item in _s_pListRenderHelper)
        {
            var mats = item.sharedMaterials;
            foreach (var mat in mats)
            {
                var path = AssetDatabase.GetAssetPath(mat);
                var ext = System.IO.Path.GetExtension(path).ToLower();
                if (ext == ".fbx" || ext == ".obj") continue;
                var isExcludePath = IsExcludePath(path);

                if (!isExcludePath)
                {
                    pathList.Add(path);
                }
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
            var isExcludePath = IsExcludePath(path);

            if (!isExcludePath)
            {
                pathList.Add(path);
            }
        }
    }

    private static bool IsExcludePath(string path)
    {
        var isExcludePath = false;
        foreach (var excludePath in _s_pListExcludePathList)
        {
            if (path.StartsWith(excludePath))
            {
                isExcludePath = true;
                break;
            }
        }

        return isExcludePath;
    }
}
