using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class RaceSplinePath : MonoBehaviour
{
    [Tooltip("Segments ordered from the start line to the finish line.")]
    public RaceSplineSegment[] segments;

    public float TotalLength { get; private set; }
    float[] segmentLateralSigns;
    Vector3[] chicaneLaneDirections;

    void Awake()
    {
        CollectSegmentsIfNeeded();
        RecalculateLength();
        BuildLateralSigns();
        if (segments.Length == 0)
            Debug.LogError($"{name}: no RaceSplineSegment children were found.", this);
    }

    void OnValidate()
    {
        CollectSegmentsIfNeeded();
        RecalculateLength();
        BuildLateralSigns();
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

    void BuildLateralSigns()
    {
        segmentLateralSigns = segments == null
            ? null
            : new float[segments.Length];
        chicaneLaneDirections = segments == null
            ? null
            : new Vector3[segments.Length];
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            RaceSplineSegment segment = segments[i];
            if (segment == null) continue;

            SplineContainer container = segment.Spline;
            if (container == null)
                container = segment.GetComponent<SplineContainer>();
            if (container == null) continue;

            Vector3 tangent = GetSplineTangent(segment, 0f);
            Vector3 splineRight = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 physicalRight = Vector3.ProjectOnPlane(
                container.transform.right,
                Vector3.up).normalized;
            segmentLateralSigns[i] =
                physicalRight.sqrMagnitude > 0.001f &&
                Vector3.Dot(splineRight, physicalRight) < 0f
                    ? -1f
                    : 1f;
            if (segment.type == RaceSplineSegment.SegmentType.Chicane)
            {
                Vector3 start = container.transform.TransformPoint(
                    SplineUtility.EvaluatePosition(container.Spline, 0f));
                Vector3 end = container.transform.TransformPoint(
                    SplineUtility.EvaluatePosition(container.Spline, 1f));
                Vector3 flow = end - start;
                flow.y = 0f;

                if (flow.sqrMagnitude > 0.001f)
                {
                    Vector3 fixedRight = Vector3.Cross(
                        Vector3.up,
                        flow.normalized);
                    Vector3 segmentRight = container.transform.right;
                    segmentRight.y = 0f;
                    if (segmentRight.sqrMagnitude > 0.001f &&
                        Vector3.Dot(fixedRight, segmentRight) < 0f)
                    {
                        fixedRight = -fixedRight;
                    }

                    chicaneLaneDirections[i] = fixedRight.normalized;
                }
            }
        }
    }

    static Vector3 GetSplineUp(RaceSplineSegment segment, float t)
    {
        SplineContainer container = segment.Spline;
        if (container == null)
            container = segment.GetComponent<SplineContainer>();
        if (container == null) return Vector3.up;

        return container.transform.TransformDirection(
            SplineUtility.EvaluateUpVector(container.Spline, t)).normalized;
    }

    static Vector3 GetSplineTangent(RaceSplineSegment segment, float t)
    {
        SplineContainer container = segment.Spline;
        if (container == null)
            container = segment.GetComponent<SplineContainer>();
        if (container == null) return Vector3.forward;

        return container.transform.TransformDirection(
            SplineUtility.EvaluateTangent(container.Spline, t)).normalized;
    }

    Vector3 GetLaneRight(
        RaceSplineSegment segment,
        int segmentIndex,
        float t)
    {
        Vector3 currentTangent = GetSplineTangent(segment, t);
        if (segment.type == RaceSplineSegment.SegmentType.Chicane &&
            chicaneLaneDirections != null &&
            segmentIndex < chicaneLaneDirections.Length &&
            chicaneLaneDirections[segmentIndex].sqrMagnitude > 0.001f)
        {
            return chicaneLaneDirections[segmentIndex];
        }

        Vector3 right = Vector3.Cross(Vector3.up, currentTangent).normalized;
        if (segmentLateralSigns != null &&
            segmentIndex < segmentLateralSigns.Length)
            right *= segmentLateralSigns[segmentIndex];
        return right;
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

        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            RaceSplineSegment candidate = segments[segmentIndex];
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
            Vector3 right = GetLaneRight(candidate, segmentIndex, t);

            position = worldPosition + right * lateralOffset;
            forward = worldForward;
            segment = candidate;
            return true;
        }

        return false;
    }

    public float GetClosestDistance(Vector3 worldPosition)
    {
        if (segments == null || segments.Length == 0 || TotalLength <= 0f)
            return 0f;

        const int samplesPerSegment = 64;
        float distanceBeforeSegment = 0f;
        float closestDistance = 0f;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (RaceSplineSegment segment in segments)
        {
            if (segment == null) continue;

            SplineContainer container = segment.Spline;
            if (container == null)
                container = segment.GetComponent<SplineContainer>();
            if (container == null) continue;

            float length = container.Spline.GetLength();
            if (length <= 0f) continue;

            for (int sample = 0; sample <= samplesPerSegment; sample++)
            {
                float t = sample / (float)samplesPerSegment;
                Vector3 point = container.transform.TransformPoint(
                    SplineUtility.EvaluatePosition(container.Spline, t));
                float sqrDistance = (point - worldPosition).sqrMagnitude;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestDistance = distanceBeforeSegment + t * length;
                }
            }

            distanceBeforeSegment += length;
        }

        return closestDistance;
    }

    public float GetSegmentProgress(
        float distance,
        RaceSplineSegment target)
    {
        float passed = 0f;
        float wrappedDistance = Mathf.Repeat(distance, TotalLength);

        foreach (RaceSplineSegment segment in segments)
        {
            if (segment == null) continue;
            SplineContainer container = segment.Spline;
            if (container == null)
                container = segment.GetComponent<SplineContainer>();
            if (container == null) continue;

            float length = container.Spline.GetLength();
            if (segment == target)
                return length > 0f
                    ? Mathf.Clamp01((wrappedDistance - passed) / length)
                    : 0f;
            passed += length;
        }

        return 0.5f;
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
