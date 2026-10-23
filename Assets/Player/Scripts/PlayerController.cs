using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Map_Gen mapGenerator;
    [SerializeField] private Gearbox gearbox;

    [Header("Road position")]
    [Min(0f)]
    [SerializeField] private float straightRightOffset = 2.5f;
    [Min(0f)]
    [SerializeField] private float rightCurveInsideOffset = 2f;
    [Min(0f)]
    [SerializeField] private float leftCurveOutsideOffset = 2f;

    [Header("Road clearance")]
    [Min(0f)]
    [SerializeField] private float roadClearance = 0.5f;

    [Header("Movement")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float curveRadius = 5f;
    [Min(0f)]
    [SerializeField] private float clutchDrag = 0.8f;

    [Header("Shift results")]
    [SerializeField] private float greenBonus = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float redPenalty = 0.85f;

    public float CurrentSpeed { get; private set; }

    private float progress;
    private int segmentIndex;

    private void Awake()
    {
        if (mapGenerator == null)
            mapGenerator = FindFirstObjectByType<Map_Gen>();

        if (gearbox == null)
            gearbox = GetComponent<Gearbox>();
    }

    private void OnEnable()
    {
        if (gearbox != null)
            gearbox.Shifted += OnShifted;
    }

    private void OnDisable()
    {
        if (gearbox != null)
            gearbox.Shifted -= OnShifted;
    }

    private void OnValidate()
    {
        straightRightOffset = Mathf.Max(0f, straightRightOffset);
        rightCurveInsideOffset = Mathf.Max(0f, rightCurveInsideOffset);
        leftCurveOutsideOffset = Mathf.Max(0f, leftCurveOutsideOffset);
        roadClearance = Mathf.Max(0f, roadClearance);
        clutchDrag = Mathf.Max(0f, clutchDrag);
        greenBonus = Mathf.Max(0f, greenBonus);
        redPenalty = Mathf.Clamp01(redPenalty);
    }

    private void OnShifted(ShiftZone zone)
    {
        if (zone == ShiftZone.Green)
            CurrentSpeed += greenBonus;
        else if (zone == ShiftZone.Red)
            CurrentSpeed *= redPenalty;
    }

    private void Update()
    {
        if (mapGenerator == null || gearbox == null)
            return;

        mapGenerator.UpdateStreaming(segmentIndex);

        if (!mapGenerator.TryGetTilePath(
                segmentIndex,
                out RoadTilePath path))
        {
            return;
        }

        UpdateSpeed();

        float segmentLength = Vector3.Distance(path.start, path.end);
        if (segmentLength < 0.01f)
            return;

        AdvancePath(path, segmentLength);

        Vector3 target = EvaluatePosition(path, progress);
        target += Vector3.up * roadClearance;
        Vector3 tangent = EvaluateTangent(path, progress);

        if (tangent.sqrMagnitude < 0.001f)
            return;

        transform.position = target;

        Quaternion targetRotation = Quaternion.LookRotation(
            tangent.normalized,
            Vector3.up
        );
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void UpdateSpeed()
    {
        if (gearbox.InClutch)
        {
            gearbox.TickClutch(Time.deltaTime);
            CurrentSpeed = Mathf.Max(
                0f,
                CurrentSpeed - clutchDrag * Time.deltaTime
            );
            return;
        }

        CurrentSpeed = Mathf.Min(
            CurrentSpeed + gearbox.Acceleration * Time.deltaTime,
            gearbox.MaxSpeed
        );
        gearbox.UpdateRpm(CurrentSpeed);
    }

    private void AdvancePath(RoadTilePath path, float segmentLength)
    {
        float remainingDistance = CurrentSpeed * Time.deltaTime;

        while (remainingDistance > 0f)
        {
            float remainingSegment =
                (1f - progress) * segmentLength;

            if (remainingDistance < remainingSegment)
            {
                progress += remainingDistance / segmentLength;
                return;
            }

            remainingDistance -= remainingSegment;
            progress = 0f;
            segmentIndex++;
            mapGenerator.UpdateStreaming(segmentIndex);

            if (!mapGenerator.TryGetTilePath(
                    segmentIndex,
                    out path))
            {
                remainingDistance = 0f;
                return;
            }

            segmentLength = Vector3.Distance(path.start, path.end);
            if (segmentLength < 0.01f)
            {
                remainingDistance = 0f;
                return;
            }
        }
    }

    private Vector3 EvaluatePosition(RoadTilePath path, float value)
    {
        if (!IsCurve(path.type))
        {
            Vector3 start = GetOffsetPosition(
                path.start,
                path.entryDirection,
                straightRightOffset
            );
            Vector3 end = GetOffsetPosition(
                path.end,
                path.entryDirection,
                straightRightOffset
            );
            return Vector3.Lerp(start, end, value);
        }

        return EvaluateCurvePosition(path, value);
    }

    private Vector3 EvaluateTangent(RoadTilePath path, float value)
    {
        if (!IsCurve(path.type))
        {
            Vector3 start = GetOffsetPosition(
                path.start,
                path.entryDirection,
                straightRightOffset
            );
            Vector3 end = GetOffsetPosition(
                path.end,
                path.entryDirection,
                straightRightOffset
            );
            return end - start;
        }

        return EvaluateCurveTangent(path, value);
    }

    private Vector3 EvaluateCurvePosition(RoadTilePath path, float value)
    {
        Vector3 center = GetCurveCenter(path);
        float baseRadius = Vector3.Distance(center, path.start);
        float radius = GetCurveRadius(path, baseRadius);
        Vector3 radial = path.start - center;
        float angle = Mathf.Atan2(radial.z, radial.x);
        float angleChange = path.type == RoadTileType.Right
            ? -Mathf.PI * 0.5f
            : Mathf.PI * 0.5f;
        float currentAngle = angle + angleChange * value;
        Vector3 position = center + new Vector3(
            Mathf.Cos(currentAngle) * radius,
            0f,
            Mathf.Sin(currentAngle) * radius
        );
        position.y = Mathf.Lerp(path.start.y, path.end.y, value);
        return position;
    }

    private Vector3 EvaluateCurveTangent(RoadTilePath path, float value)
    {
        Vector3 center = GetCurveCenter(path);
        float baseRadius = Vector3.Distance(center, path.start);
        float radius = GetCurveRadius(path, baseRadius);
        Vector3 radial = path.start - center;
        float angle = Mathf.Atan2(radial.z, radial.x);
        float angleChange = path.type == RoadTileType.Right
            ? -Mathf.PI * 0.5f
            : Mathf.PI * 0.5f;
        float currentAngle = angle + angleChange * value;
        return new Vector3(
            -Mathf.Sin(currentAngle) * angleChange * radius,
            path.end.y - path.start.y,
            Mathf.Cos(currentAngle) * angleChange * radius
        );
    }

    private Vector3 GetCurveCenter(RoadTilePath path)
    {
        Vector3 turnSide = path.type == RoadTileType.Right
            ? GetRight(path.entryDirection)
            : -GetRight(path.entryDirection);
        return path.start + turnSide * curveRadius;
    }

    private float GetCurveRadius(RoadTilePath path, float baseRadius)
    {
        if (path.type == RoadTileType.Right)
        {
            return Mathf.Max(
                0.1f,
                baseRadius - rightCurveInsideOffset
            );
        }

        return baseRadius + leftCurveOutsideOffset;
    }

    private static Vector3 GetOffsetPosition(
        Vector3 position,
        Vector3 direction,
        float offset)
    {
        Vector3 right = Vector3.Cross(
            Vector3.up,
            direction
        ).normalized;
        return position + right * offset;
    }

    private static Vector3 GetRight(Vector3 direction)
    {
        return Vector3.Cross(Vector3.up, direction).normalized;
    }

    private static bool IsCurve(RoadTileType type)
    {
        return type == RoadTileType.Right ||
               type == RoadTileType.Left;
    }
}
