using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

public class AssetReference : EditorWindow
{
    private static readonly string[] mode = {"Find", "Replace"};
    Object findObj;
    private Object replaceObj;
    private int select = 0;
    private bool option = true;
    private bool includeSubAsset = false;
    private bool inSelection = false;
    Vector2 scrollPos = Vector2.zero;

    List<Object> referenceObjs = new List<Object>();
    List<Object> replacedObjs = new List<Object>();

    [MenuItem("Tools/Window/AssetReference")]
	static void Init()
    {
        GetWindow<AssetReference>();
    }

    void OnGUI()
    {
        select = GUILayout.Toolbar(select, mode);

        findObj = EditorGUILayout.ObjectField("Find target",findObj, typeof(Object),false);
        if (select == 1)
        {
            replaceObj = EditorGUILayout.ObjectField("Replace with", replaceObj, typeof (Object), false);
        }

        option = EditorGUILayout.Foldout(option, "Options");
        if (option)
        {
            EditorGUI.indentLevel++;
            includeSubAsset = EditorGUILayout.ToggleLeft("Include subAsset", includeSubAsset);
            inSelection = EditorGUILayout.ToggleLeft("In Selection", inSelection);
            inSelection = !EditorGUILayout.ToggleLeft("In Project", !inSelection);

            EditorGUI.indentLevel--;
        }
        
        if(GUILayout.Button(mode[select]))
        {
            if(findObj == null)
            {
                ShowNotification(new GUIContent("obj is null"));
            }
            else
            {
                string[] paths;
                if (inSelection)
                {
                    paths = GetAllAssetsPathInSelection();
                    AssetReferenceTool.LogPaths(paths);
                }
                else
                {
                    paths = AssetDatabase.GetAllAssetPaths();
                }
                if (includeSubAsset)
                {
                    List<Object> findObjs = GetObjectWithSubAssets(findObj);
                    if (select == 0)
                    {
                        referenceObjs = AssetReferenceTool.FindReferences(findObjs, paths);
                    }
                    else
                    {
                        if (replaceObj == null)
                        {
                            if (EditorUtility.DisplayDialog("", "Replace target with null ?", "ok", "cancel"))
                            {
                                List<AssetReferenceTool.ReplaceData> replaceDatas = new List<AssetReferenceTool.ReplaceData>();
                                foreach (var obj in findObjs)
                                {
                                    replaceDatas.Add(new AssetReferenceTool.ReplaceData(obj,null));
                                }
                                replacedObjs = AssetReferenceTool.ReplaceReference(replaceDatas, paths);
                            }
                        }
                        else
                        {
                            List<Object> replaceObjs = GetObjectWithSubAssets(replaceObj);
                            if (CheckAssetMatch(findObjs, replaceObjs))
                            {
                                List<AssetReferenceTool.ReplaceData> replaceDatas = new List<AssetReferenceTool.ReplaceData>();
                                for (int i = 0; i < findObjs.Count; i++) 
                                {
                                    replaceDatas.Add(new AssetReferenceTool.ReplaceData(findObjs[i], replaceObjs[i]));
                                }
                                replacedObjs = AssetReferenceTool.ReplaceReference(replaceDatas, paths);
                            }
                            else
                            {
                                if (EditorUtility.DisplayDialog("", "SubAsset do not match,do you still want to replace ?", "Yes", "No"))
                                {
                                    List<AssetReferenceTool.ReplaceData> replaceDatas = new List<AssetReferenceTool.ReplaceData>();
                                    for (int i = 0; i < findObjs.Count; i++)
                                    {
                                        if (i < replaceObjs.Count)
                                        {
                                            replaceDatas.Add(new AssetReferenceTool.ReplaceData(findObjs[i], replaceObjs[i]));
                                        }
                                        else
                                        {
                                            replaceDatas.Add(new AssetReferenceTool.ReplaceData(findObjs[i], null));
                                        }
                                        
                                    }
                                    replacedObjs = AssetReferenceTool.ReplaceReference(replaceDatas, paths);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (select == 0)
                    {
                        referenceObjs = AssetReferenceTool.FindReferences(findObj, paths);
                    }
                    else
                    {
                        replacedObjs = AssetReferenceTool.ReplaceReference(new AssetReferenceTool.ReplaceData(findObj,replaceObj),paths );
                    }
                }
                
            }

        }

        if (GUILayout.Button("Clear Result"))
        {
            referenceObjs.Clear();
            replacedObjs.Clear();
        }
        if (select == 0)
        {
            EditorGUILayout.LabelField("Referenced By:");
        }
        else
        {
            EditorGUILayout.LabelField("Replaced objs:");
        }
        

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        if (select == 0)
        {
            foreach (var o in referenceObjs)
            {
                EditorGUILayout.ObjectField(o, typeof (Object));
            }
        }
        else
        {
            foreach (var o in replacedObjs)
            {
                EditorGUILayout.ObjectField(o, typeof(Object));
            }
        }
        EditorGUILayout.EndScrollView();

    }

    private string[] GetAllAssetsPathInSelection()
    {
        return Selection.assetGUIDs.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).SelectMany(path =>
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return AssetDatabase.FindAssets("t:Object", new string[] {path}).Select(guid=>AssetDatabase.GUIDToAssetPath(guid));
            }
            else
            {
                return new string[]{path};
            }
        }).Distinct().Where(path=>!AssetDatabase.IsValidFolder(path)).ToArray();
    }

    List<Object> GetObjectWithSubAssets(Object obj)
    {
        List<Object> objs = new List<Object>();
        if (obj == null)
        {
            return objs;
        }
        objs.Add(obj);
        string path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path))
        {
            objs.AddRange(AssetDatabase.LoadAllAssetRepresentationsAtPath(path));
        }
        return objs;
    } 

    void OnSelectionChange()
    {
    }

    bool CheckAssetMatch(List<Object> asset1, List<Object> asset2)
    {
        if (asset1.Count != asset2.Count)
        {
            return false;
        }
        for (int i = 0; i < asset1.Count; i++)
        {
            if (Unsupported.GetLocalIdentifierInFile(asset1[i].GetInstanceID()) !=
                Unsupported.GetLocalIdentifierInFile(asset2[i].GetInstanceID()))
            {
                return false;
            }
        }
        return true;
    }
}
