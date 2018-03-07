using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class AssetReferenceTool
{
    public class ReplaceData
    {
        public List<Object> oldRefs;
        public Object newRef;

        public ReplaceData()
        {
            
        }

        public ReplaceData(List<Object> oldObjs, Object newObj)
        {
            this.oldRefs = oldObjs;
            this.newRef = newObj;
        }

        public ReplaceData(Object oldObj, Object newObj)
        {
            this.oldRefs = new List<Object>() {oldObj};
            this.newRef = newObj;
        }
    }

    public static List<Object> dealedObjects = new List<Object>();

    [MenuItem("Assets/Find References In Project", true, 25)]
    public static bool IsSelectFile()
    {
        return Selection.activeObject != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }

    [MenuItem("Assets/Find References In Project", false, 25)]
    public static void SelectReferencesInProject()
    {
        Selection.objects = FindReferencesInProject(Selection.activeObject).ToArray();
    }

    public static List<Object> FindReferencesInProject(Object obj)
    {
        return FindReferences(obj,FindObject, AssetDatabase.GetAllAssetPaths());
    }

    public static List<Object> FindReferencesInProject(List<Object> obj)
    {
        return FindReferences(obj, FindMultiObject, AssetDatabase.GetAllAssetPaths());
    }

    public static List<Object> FindReferences(Object obj, IList<string> paths)
    {
        return FindReferences(obj, FindObject, paths);
    }

    public static List<Object> FindReferences(List<Object> obj, IList<string> paths)
    {
        return FindReferences(obj, FindMultiObject, paths);
    }

    public static List<Object> FindReferencesInBuild(Object obj)
    {
        return FindReferences(obj, FindObject, GetBuildAssets());
    }

    public static List<Object> FindReferencesInBuild(List<Object> obj)
    {
        return FindReferences(obj, FindMultiObject, GetBuildAssets());
    }

    public static List<Object> FindReferences<T>(T objs, Func<SerializedProperty, T, string, bool> searchFunc,IList<string> paths)
    {
        List<Object> references = new List<Object>();

        var openScenes = EditorSceneManager.GetSceneManagerSetup();
        int assetCount = paths.Count;
        for(int i = 0;i < assetCount;i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Hold On", i + "/" + assetCount + "\t" + paths[i], (float) i/assetCount))
            {
                break;
            }
            Object asset = AssetDatabase.LoadMainAssetAtPath(paths[i]);
            if (asset == null)
            {
                continue;
            }
            dealedObjects.Clear();
            if (asset is SceneAsset)
            {
                Scene scene = EditorSceneManager.OpenScene(paths[i],OpenSceneMode.Additive);
                var roots = scene.GetRootGameObjects();
                foreach (var gameObject in roots)
                {
                    if (SearchSerializedObject(new SerializedObject(gameObject), objs, paths[i], searchFunc))
                    {
                        references.Add(asset);
                        break;
                    }

                }
                if (openScenes.All(s => s.path != paths[i]))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            else
            {
                if (SearchSerializedObject(new SerializedObject(asset), objs, paths[i], searchFunc))
                {
                    references.Add(asset);
                }
            }
        }
        LogPaths(references.Select(obj=>AssetDatabase.GetAssetPath(obj)));
        EditorUtility.ClearProgressBar();
        return references;
    }

    public static bool SearchSerializedObject<T>(SerializedObject so,T targets,string assetPath,Func<SerializedProperty,T,string,bool> handleSerializedProperty)
    {
        dealedObjects.Add(so.targetObject);
        SerializedProperty sp = so.GetIterator();
        bool processed = false;
        while (sp.Next(true))
        {
            if (sp.propertyType == SerializedPropertyType.String)
            {
                if (!sp.Next(false))
                {
                    break;
                }
            }
            if (sp.isArray)
            {
                if (sp.arraySize > 2)
                {
                    SerializedProperty element0 = sp.GetArrayElementAtIndex(0);
                    SerializedProperty element1 = sp.GetArrayElementAtIndex(1);
                    element0 = GetObjectProperty(element0, element1);
                    if (element0.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        sp = element0;
                    }
                    else
                    {
                        if (!sp.Next(false))
                        {
                            break;
                        }
                    }
                }
            }
            if (sp.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (SearchSerializedProperty(sp, targets, assetPath, handleSerializedProperty))
                {
                    processed = true;
                }
            }
        }
        return processed;
    }

    public static SerializedProperty GetObjectProperty(SerializedProperty startProperty, SerializedProperty endProperty)
    {
        while (startProperty.propertyType != SerializedPropertyType.ObjectReference && !SerializedProperty.EqualContents(startProperty, endProperty))
        {
            startProperty.Next(true);
        }
        return startProperty;
    }

    public static bool SearchSerializedProperty<T>(SerializedProperty sp,T targets,string assetPath, Func<SerializedProperty, T, string, bool> handleSerializedProperty)
    {
        bool handled = handleSerializedProperty(sp, targets, assetPath);
            
        if(sp.objectReferenceValue != null && !dealedObjects.Contains(sp.objectReferenceValue))
        {
            string path = AssetDatabase.GetAssetPath(sp.objectReferenceValue);
            if (string.IsNullOrEmpty(path) || path == assetPath)
            {
                if (SearchSerializedObject(new SerializedObject(sp.objectReferenceValue), targets, assetPath,
                    handleSerializedProperty))
                {
                    handled = true;
                }
            }
        }

        return handled;
    }

    public static List<string> GetBuildAssets()
    {
        HashSet<string> paths = new HashSet<string>();
        EditorUtility.DisplayProgressBar("Get Assets in Resources", "", 0);
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        foreach (var asset in allAssets)
        {
            if (Path.GetDirectoryName(asset).ToLower().Contains("resources"))
            {
                paths.Add(asset);
            }
        }
        EditorUtility.DisplayProgressBar("Get Resources Depend Assets", "", 0.5f);
        paths.UnionWith(AssetDatabase.GetDependencies(paths.ToArray(), true));

        EditorUtility.DisplayProgressBar("Get Build Scene Depend Assets", "", 0.8f);
        foreach (var editorBuildSettingsScene in EditorBuildSettings.scenes)
        {
            if (editorBuildSettingsScene.enabled)
            {
                paths.UnionWith(AssetDatabase.GetDependencies(editorBuildSettingsScene.path, true));
            }
        }

        return paths.ToList();
    }

    public static bool FindObject(SerializedProperty sp, Object target, string assetPath)
    {
        if (sp.objectReferenceValue == target)
        {
            return true;
        }
        return false;
    }

    public static bool FindMultiObject(SerializedProperty sp, List<Object> targets, string assetPath)
    {
        foreach (var target in targets)
        {
            if (sp.objectReferenceValue == target)
            {
                return true;
            }
        }
        return false;
    }

    public static List<Object> ReplaceReferenceInProject(ReplaceData data)
    {
        return ReplaceReferenceInProject(new List<ReplaceData>() { data });
    }

    public static List<Object> ReplaceReferenceInProject(List<ReplaceData> datas)
    {
        var paths = AssetDatabase.GetAllAssetPaths();
        return ReplaceReference(datas,paths);
    }

    public static List<Object> ReplaceReference(ReplaceData data, string[] paths)
    {
        return ReplaceReference(new List<ReplaceData>() { data }, paths);
    }

    public static List<Object> ReplaceReference(List<ReplaceData> datas, string[] paths)
    {
        List<Object> replacedObjects = new List<Object>();
        
        var openScenes = EditorSceneManager.GetSceneManagerSetup();
        int assetCount = paths.Length;
        for (int i = 0; i < assetCount; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Hold On", i + "/" + assetCount + "\t" + paths[i], (float) i/assetCount))
            {
                break;
            }
            Object asset = AssetDatabase.LoadMainAssetAtPath(paths[i]);
            if (asset == null)
            {
                continue;
            }
            bool replaced = false;
            dealedObjects.Clear();
            if (asset is SceneAsset)
            {
                Scene scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Additive);
                var roots = scene.GetRootGameObjects();
                foreach (var gameObject in roots)
                {
                    if (SearchSerializedObject(new SerializedObject(gameObject), datas, paths[i],ReplaceInSerializedProperty))
                    {
                        replaced = true;
                    }
                }

                if (replaced)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                if (openScenes.All(s => s.path != paths[i]))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            else
            {
                if (SearchSerializedObject(new SerializedObject(asset), datas,paths[i], ReplaceInSerializedProperty))
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                    replaced = true;
                }
            }
            if (replaced)
            {
                replacedObjects.Add(asset);
            }
        }
        LogPaths(replacedObjects.Select(obj=>AssetDatabase.GetAssetPath(obj)));
        EditorUtility.ClearProgressBar();
        return replacedObjects;
    }


    public static bool ReplaceInSerializedProperty(SerializedProperty sp, List<ReplaceData> datas,string assetPath)
    {
        bool replaced = false;

        foreach (var data in datas)
        {
            foreach (var oldRef in data.oldRefs)
            {
                if (sp.objectReferenceValue == oldRef)
                {
                    sp.objectReferenceValue = data.newRef;
                    replaced = true;
                    break;
                }
            }
            if (replaced)
            {
                sp.serializedObject.ApplyModifiedProperties();
                break;
            }
        }

        return replaced;
    }

    public static void LogPaths(IEnumerable<string> paths)
    {
        StringBuilder output = new StringBuilder();
        output.AppendLine();

        int i = 0;
        foreach (var p in paths)
        {
            output.AppendLine(p);
            i++;
        }
        output.Insert(0, i);
        output.Insert(0, "Count:");
        Debug.Log(output);
    }
}
