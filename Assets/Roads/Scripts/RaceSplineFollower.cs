using UnityEngine;

[DefaultExecutionOrder(100)]
public class RaceSplineFollower : MonoBehaviour
{
    public enum DriverType
    {
        Player,
        AI
    }

    [Header("Path")]
    public RaceSplinePath path;
    public DriverType driverType;
    public float lateralOffset = 1.5f;
    public float heightOffset = 0.5f;
    public Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("AI")]
    public float aiCruiseSpeedKmh = 160f;
    public float aiAcceleration = 8f;
    public float aiBrake = 12f;

    [Header("Player")]
    public PlayerController player;

    public float Distance { get; private set; }
    public float CurrentSpeed { get; private set; }
    public RaceSplineSegment CurrentSegment { get; private set; }
    public float CurrentSpeedLimitMs { get; private set; } = float.PositiveInfinity;

    CarStats stats;
    float aiSpeed;
    Transform movementTarget;

    void Awake()
    {
        if (path == null) path = FindFirstObjectByType<RaceSplinePath>();
        if (player == null) player = GetComponentInParent<PlayerController>();
        stats = GetComponentInParent<CarStats>();
        if (stats == null)
            stats = GetComponentInChildren<CarStats>();
        movementTarget = driverType == DriverType.Player && player != null
            ? player.transform
            : transform;
    }

    void Start()
    {
        if (driverType == DriverType.Player && player == null)
        {
            Debug.LogError(
                $"{name}: RaceSplineFollower is set to Player but no PlayerController was found on this object or its parents.",
                this);
        }
        Place();
    }

    void LateUpdate()
    {
        if (path == null || path.TotalLength <= 0f) return;

        CurrentSpeed = driverType == DriverType.Player
            ? player != null ? Mathf.Max(0f, player.CurrentSpeed) : 0f
            : UpdateAiSpeed();

        float previousDistance = Distance;
        Distance = Mathf.Repeat(Distance + CurrentSpeed * Time.deltaTime, path.TotalLength);
        Place();

        LapCounter counter = GetComponent<LapCounter>();
        if (counter != null)
            counter.NotifyDistanceAdvanced(previousDistance, Distance, path.TotalLength);
    }

    float UpdateAiSpeed()
    {
        float maxSpeedKmh = stats != null ? stats.maxSpeed : aiCruiseSpeedKmh;
        float handling = stats != null ? stats.handling : 0.5f;
        float target = Mathf.Min(aiCruiseSpeedKmh, maxSpeedKmh);

        if (path.Evaluate(Distance, 0f, out _, out _, out RaceSplineSegment segment) &&
            segment != null)
        {
            target = Mathf.Min(target, segment.GetSpeedLimitMs(maxSpeedKmh, handling));
        }

        float rate = aiSpeed > target ? aiBrake : aiAcceleration;
        aiSpeed = Mathf.MoveTowards(aiSpeed, target, rate * Time.deltaTime);
        return aiSpeed;
    }

    void Place()
    {
        if (!path.Evaluate(
                Distance,
                lateralOffset,
                out Vector3 position,
                out Vector3 forward,
                out RaceSplineSegment segment))
            return;

        CurrentSegment = segment;
        UpdateSpeedLimit(segment);
        movementTarget.position = position + Vector3.up * heightOffset;
        if (forward.sqrMagnitude > 0.001f)
        {
            Quaternion pathRotation = Quaternion.LookRotation(forward, Vector3.up);
            movementTarget.rotation = pathRotation * Quaternion.Euler(rotationOffset);
        }
    }

    void UpdateSpeedLimit(RaceSplineSegment segment)
    {
        if (segment == null)
        {
            CurrentSpeedLimitMs = float.PositiveInfinity;
            return;
        }

        float maxSpeedKmh = stats != null
            ? stats.maxSpeed
            : player != null && player.gearbox != null
                ? player.gearbox.MaxSpeed * 3.6f
                : aiCruiseSpeedKmh;
        float handling = stats != null ? stats.handling : 0.5f;
        CurrentSpeedLimitMs = segment.GetSpeedLimitMs(maxSpeedKmh, handling);
    }
}
