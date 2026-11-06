using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public static class ConfigureRoadSplineComponents
{
    const string RoadsFolder = "Assets/Roads";

    [MenuItem("Gear4Speed/Configure Road Spline Components")]
    public static void Configure()
    {
        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { RoadsFolder });
        int configuredPrefabs = 0;
        int configuredSegments = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (IsCurbPrefab(assetPath))
                    continue;

                GameObject prefab = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    int addedToPrefab = ConfigurePrefab(
                        prefab,
                        assetPath);
                    if (addedToPrefab == 0)
                        continue;

                    PrefabUtility.SaveAsPrefabAsset(prefab, assetPath);
                    configuredPrefabs++;
                    configuredSegments += addedToPrefab;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log(
            $"Configured {configuredSegments} RaceSplineSegment component(s) " +
            $"in {configuredPrefabs} road prefab(s).");
    }

    static int ConfigurePrefab(GameObject prefab, string assetPath)
    {
        RaceSplineSegment.SegmentType type =
            GetSegmentType(assetPath, prefab.name);
        SplineContainer[] splines =
            prefab.GetComponentsInChildren<SplineContainer>(true);
        int configured = 0;

        foreach (SplineContainer spline in splines)
        {
            if (spline == null || spline.Spline == null ||
                spline.Spline.GetLength() <= 0f)
            {
                continue;
            }

            RaceSplineSegment segment =
                spline.GetComponent<RaceSplineSegment>();
            if (segment == null)
                segment = spline.gameObject.AddComponent<RaceSplineSegment>();

            segment.type = type;
            segment.brakingBuffer = GetBrakingBuffer(type);
            segment.speedMultiplier = GetSpeedMultiplier(type);
            EditorUtility.SetDirty(segment);
            configured++;
        }

        return configured;
    }

    static bool IsCurbPrefab(string assetPath)
    {
        string fileName = System.IO.Path.GetFileNameWithoutExtension(
            assetPath);
        return fileName.IndexOf("curb", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static RaceSplineSegment.SegmentType GetSegmentType(
        string assetPath,
        string prefabName)
    {
        string name = (assetPath + "/" + prefabName).ToLowerInvariant()
            .Replace("-", "")
            .Replace("_", "")
            .Replace(" ", "");

        if (name.Contains("uturn") || name.Contains("u-turn"))
            return RaceSplineSegment.SegmentType.UTurn;
        if (name.Contains("chicane") || name.Contains("shicane"))
            return RaceSplineSegment.SegmentType.Chicane;
        if (name.Contains("turn") || name.Contains("turm"))
            return RaceSplineSegment.SegmentType.Corner;

        return RaceSplineSegment.SegmentType.Straight;
    }

    static float GetBrakingBuffer(RaceSplineSegment.SegmentType type)
    {
        switch (type)
        {
            case RaceSplineSegment.SegmentType.Corner:
                return 10f;
            case RaceSplineSegment.SegmentType.Chicane:
                return 14f;
            case RaceSplineSegment.SegmentType.UTurn:
                return 20f;
            default:
                return 0f;
        }
    }

    static float GetSpeedMultiplier(RaceSplineSegment.SegmentType type)
    {
        switch (type)
        {
            case RaceSplineSegment.SegmentType.Corner:
                return 0.8f;
            case RaceSplineSegment.SegmentType.Chicane:
                return 0.95f;
            case RaceSplineSegment.SegmentType.UTurn:
                return 0.55f;
            default:
                return 1f;
        }
    }
}
