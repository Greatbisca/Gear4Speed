using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public static class ConfigureRoadSplineComponents
{
    const string PrefabFolder = "Assets/Roads/Prefabs";

    [MenuItem("Gear4Speed/Configure Road Spline Components")]
    public static void Configure()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        int configured = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);

            try
            {
                SplineContainer spline = prefab.GetComponentInChildren<SplineContainer>(true);
                if (spline == null)
                {
                    Debug.LogWarning($"No SplineContainer found in {path}.");
                    continue;
                }

                RaceSplineSegment segment =
                    spline.GetComponent<RaceSplineSegment>();
                if (segment == null)
                    segment = spline.gameObject.AddComponent<RaceSplineSegment>();

                segment.type = GetSegmentType(prefab.name);
                segment.brakingBuffer = GetBrakingBuffer(segment.type);

                EditorUtility.SetDirty(spline.gameObject);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                configured++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Configured RaceSplineSegment on {configured} road prefab(s).");
    }

    static RaceSplineSegment.SegmentType GetSegmentType(string prefabName)
    {
        string name = prefabName.ToLowerInvariant();

        if (name.Contains("u-turn"))
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
}
