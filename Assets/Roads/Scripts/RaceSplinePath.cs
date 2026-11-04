using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class RaceSplinePath : MonoBehaviour
{
    [Tooltip("Segments ordered from the start line to the finish line.")]
    public RaceSplineSegment[] segments;

    public float TotalLength { get; private set; }

    void Awake()
    {
        CollectSegmentsIfNeeded();
        RecalculateLength();
        if (segments.Length == 0)
            Debug.LogError($"{name}: no RaceSplineSegment children were found.", this);
    }

    void OnValidate()
    {
        CollectSegmentsIfNeeded();
        RecalculateLength();
    }

    public void CollectSegmentsIfNeeded()
    {
        RaceSplineSegment[] found =
            GetComponentsInChildren<RaceSplineSegment>(true);

        var unique = new List<RaceSplineSegment>(found.Length);
        foreach (RaceSplineSegment segment in found)
        {
            if (segment != null && !unique.Contains(segment))
                unique.Add(segment);
        }

        segments = unique.ToArray();
    }

    public void RecalculateLength()
    {
        TotalLength = 0f;
        if (segments == null) return;

        foreach (RaceSplineSegment segment in segments)
        {
            if (segment == null) continue;
            SplineContainer container = segment.Spline;
            if (container == null)
                container = segment.GetComponent<SplineContainer>();
            if (container != null)
                TotalLength += container.Spline.GetLength();
        }
    }

    public bool Evaluate(
        float distance,
        float lateralOffset,
        out Vector3 position,
        out Vector3 forward,
        out RaceSplineSegment segment)
    {
        position = default;
        forward = Vector3.forward;
        segment = null;

        if (segments == null || segments.Length == 0) return false;

        float wrappedDistance = Mathf.Repeat(distance, TotalLength);
        float passed = 0f;

        foreach (RaceSplineSegment candidate in segments)
        {
            if (candidate == null) continue;
            SplineContainer container = candidate.Spline;
            if (container == null)
                container = candidate.GetComponent<SplineContainer>();
            if (container == null) continue;

            float length = container.Spline.GetLength();
            if (length <= 0f) continue;
            if (wrappedDistance > passed + length)
            {
                passed += length;
                continue;
            }

            float t = Mathf.Clamp01((wrappedDistance - passed) / length);
            Vector3 localPosition = SplineUtility.EvaluatePosition(container.Spline, t);
            Vector3 localTangent = SplineUtility.EvaluateTangent(container.Spline, t);
            Vector3 localUp = SplineUtility.EvaluateUpVector(container.Spline, t);

            Vector3 worldPosition = container.transform.TransformPoint(localPosition);
            Vector3 worldForward = container.transform.TransformDirection(localTangent).normalized;
            Vector3 worldUp = container.transform.TransformDirection(localUp).normalized;
            Vector3 right = Vector3.Cross(worldUp, worldForward).normalized;

            position = worldPosition + right * lateralOffset;
            forward = worldForward;
            segment = candidate;
            return true;
        }

        return false;
    }

    public float GetDistanceToSegmentStart(float distance, RaceSplineSegment target)
    {
        float passed = 0f;
        float wrappedDistance = Mathf.Repeat(distance, TotalLength);

        foreach (RaceSplineSegment segment in segments)
        {
            if (segment == target)
                return Mathf.Repeat(passed - wrappedDistance, TotalLength);

            SplineContainer container = segment.Spline;
            if (container != null)
                passed += container.Spline.GetLength();
        }

        return float.PositiveInfinity;
    }
}
