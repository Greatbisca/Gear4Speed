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
    [Min(0f)]
    [Tooltip("Track-specific speed multiplier applied after car max speed and handling.")]
    public float speedMultiplier = 1f;

    [Min(-1)]
    [Tooltip("Quantas mudanças o jogador tem de reduzir nesta curva. -1 = usa o valor do tipo + handling.")]
    public int gearDropOverride = -1;

    public SplineContainer Spline { get; private set; }

    public bool IsCorner => type != SegmentType.Straight;

    public float GetSpeedLimitKmh(
        float carMaxSpeedKmh,
        float handling,
        float normalizedProgress = 0.5f)
    {
        if (!IsCorner)
            return Mathf.Max(0f, carMaxSpeedKmh);

        float speed = carMaxSpeedKmh * Mathf.Max(0f, handling) * speedMultiplier;
        if (type == SegmentType.UTurn)
        {
            float distanceFromMiddle = Mathf.Abs(
                Mathf.Clamp01(normalizedProgress) * 2f - 1f);
            speed *= Mathf.Lerp(0.65f, 1f, distanceFromMiddle);
        }

        return Mathf.Max(0f, speed);
    }

    public float GetSpeedLimitMs(
        float carMaxSpeedKmh,
        float handling,
        float normalizedProgress = 0.5f)
    {
        return GetSpeedLimitKmh(
            carMaxSpeedKmh,
            handling,
            normalizedProgress) / 3.6f;
    }

    public float GetTargetSpeedMs(
        float carMaxSpeedKmh,
        float handling,
        float normalizedProgress = 0.5f)
    {
        return GetSpeedLimitMs(
            carMaxSpeedKmh,
            handling,
            normalizedProgress);
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
        speedMultiplier = Mathf.Max(0f, speedMultiplier);
    }
}
