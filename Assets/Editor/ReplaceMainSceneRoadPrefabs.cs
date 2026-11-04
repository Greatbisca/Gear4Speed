using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReplaceMainSceneRoadPrefabs
{
    const string NewPrefabFolder = "Assets/Roads/Prefabs";
    const string OldPrefabFolder = "Assets/Roads/Modular Racing Circuit Lowpoly Free/Prefabs";

    [MenuItem("Gear4Speed/Replace Main Scene Road Prefabs")]
    public static void Replace()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("Open Main Scene before replacing road prefabs.");
            return;
        }
        if (scene.path != "Assets/Scenes/Main Scene.unity")
        {
            Debug.LogError("The active scene is not Assets/Scenes/Main Scene.unity.");
            return;
        }

        Dictionary<string, GameObject> replacements = LoadReplacements();
        List<GameObject> instances = FindRoadInstances(scene, replacements);

        if (instances.Count == 0)
        {
            Debug.Log("No old road prefab instances found in the active scene.");
            return;
        }

        int replaced = 0;
        foreach (GameObject oldInstance in instances)
        {
            GameObject oldPrefab =
                PrefabUtility.GetCorrespondingObjectFromSource(oldInstance);

            if (oldPrefab == null ||
                !replacements.TryGetValue(
                    AssetDatabase.GetAssetPath(oldPrefab),
                    out GameObject newPrefab))
                continue;

            ReplaceInstance(oldInstance, newPrefab, scene);
            replaced++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Replaced {replaced} road prefab instance(s) with spline prefabs.");
    }

    static Dictionary<string, GameObject> LoadReplacements()
    {
        var result = new Dictionary<string, GameObject>();
        string[] oldGuids = AssetDatabase.FindAssets(
            "t:Prefab", new[] { OldPrefabFolder });

        foreach (string guid in oldGuids)
        {
            string oldPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileName(oldPath);
            string newPath = $"{NewPrefabFolder}/{fileName}";
            if (fileName == "Lite medium shicane Turn Left curb.prefab")
                newPath = $"{NewPrefabFolder}/Lite medium shicane Turn Left curb 1.prefab";
            GameObject newPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(newPath);

            if (newPrefab != null)
                result[oldPath] = newPrefab;
        }

        return result;
    }

    static List<GameObject> FindRoadInstances(
        Scene scene,
        Dictionary<string, GameObject> replacements)
    {
        var result = new List<GameObject>();
        GameObject[] objects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (GameObject instance in objects)
        {
            if (instance.scene != scene ||
                PrefabUtility.GetNearestPrefabInstanceRoot(instance) != instance)
                continue;

            GameObject source =
                PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (source == null)
                continue;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (replacements.ContainsKey(sourcePath))
                result.Add(instance);
        }

        return result;
    }

    static void ReplaceInstance(
        GameObject oldInstance,
        GameObject newPrefab,
        Scene scene)
    {
        Transform oldTransform = oldInstance.transform;
        Transform parent = oldTransform.parent;
        int siblingIndex = oldTransform.GetSiblingIndex();
        Vector3 localPosition = oldTransform.localPosition;
        Quaternion localRotation = oldTransform.localRotation;
        Vector3 localScale = oldTransform.localScale;
        bool active = oldInstance.activeSelf;
        string objectName = oldInstance.name;

        GameObject replacement =
            (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, scene);
        Undo.RegisterCreatedObjectUndo(replacement, "Replace road prefab");

        Transform replacementTransform = replacement.transform;
        replacementTransform.SetParent(parent, false);
        replacementTransform.localPosition = localPosition;
        replacementTransform.localRotation = localRotation;
        replacementTransform.localScale = localScale;
        replacementTransform.SetSiblingIndex(siblingIndex);
        replacement.name = objectName;
        replacement.SetActive(active);

        Undo.DestroyObjectImmediate(oldInstance);
    }
}
