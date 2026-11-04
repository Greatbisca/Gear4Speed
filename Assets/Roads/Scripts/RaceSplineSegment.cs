using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
public class RaceSplineSegment : MonoBehaviour
{
    public enum SegmentType
    {
        Straight,
        Corner,
        Chicane,
        UTurn
    }

    [Header("Segment")]
    public SegmentType type = SegmentType.Straight;

    [Min(0f)]
    [Tooltip("Extra distance before this segment where the AI may start braking, in metres.")]
    public float brakingBuffer = 5f;

    public SplineContainer Spline { get; private set; }

    public bool IsCorner => type != SegmentType.Straight;

    public float GetSpeedLimitKmh(float carMaxSpeedKmh, float handling)
    {
        if (!IsCorner)
            return Mathf.Max(0f, carMaxSpeedKmh);

        float handlingFactor = Mathf.Clamp01(handling);
        float divisor = GetCornerDivisor(type);
        return Mathf.Max(0f, carMaxSpeedKmh * handlingFactor / divisor);
    }

    public float GetSpeedLimitMs(float carMaxSpeedKmh, float handling)
    {
        return GetSpeedLimitKmh(carMaxSpeedKmh, handling) / 3.6f;
    }

    static float GetCornerDivisor(SegmentType segmentType)
    {
        switch (segmentType)
        {
            case SegmentType.Chicane:
                return 1.5f;
            case SegmentType.UTurn:
                return 2f;
            case SegmentType.Corner:
                return 1f;
            default:
                return 1f;
        }
    }

    void Awake()
    {
        Spline = GetComponent<SplineContainer>();
    }

    void Reset()
    {
        Spline = GetComponent<SplineContainer>();
    }

    void OnValidate()
    {
        brakingBuffer = Mathf.Max(0f, brakingBuffer);
    }
}
