using UnityEngine;

public class AIController : MonoBehaviour, ISpeedSource
{
    [Header("References")]
    public Map_Gen map;
    public PlayerController player;      // used only to stay inside the generated tiles
    public RaceSplineFollower splineFollower;
    public CarSpawner spawner;

    [Header("Speed (m/s) - modo tiles / sem stats")]
    public float cruiseSpeed = 12f;
    public bool slowInCorners = true;
    public float cornerSpeed = 8f;
    public float accel = 4f;
    public float brake = 8f;

    [Header("Stats do carro (modo spline)")]
    [Range(0.5f, 1.2f)]
    [Tooltip("Fração da velocidade máxima do carro que a IA usa. <1 deixa o jogador ganhar a direito.")]
    public float speedFactor = 0.95f;
    [Tooltip("m/s² por cada ponto do stat 'acceleration'. O jogador acelera mais na 1ª e menos nas altas.")]
    public float accelPerStat = 0.55f;

    [Header("Road")]
    public float heightOffset = 0.5f;
    public float lateralOffset = 1.5f;   // shifts the AI sideways so it doesn't overlap the player
    public int startTileOffset = 1;      // start this many tiles ahead of tile 0

    public float CurrentSpeed { get; private set; }
    public int TileIndex { get; private set; }

    float t;

    void Awake()
    {
        if (map == null) map = FindFirstObjectByType<Map_Gen>();
        if (player == null) player = FindFirstObjectByType<PlayerController>();

        if (splineFollower == null) splineFollower = GetComponent<RaceSplineFollower>();
        if (splineFollower == null) splineFollower = GetComponentInChildren<RaceSplineFollower>();
        if (splineFollower == null) splineFollower = GetComponentInParent<RaceSplineFollower>();

        if (spawner == null) spawner = GetComponent<CarSpawner>();
        if (spawner == null) spawner = GetComponentInChildren<CarSpawner>();
        if (spawner == null) spawner = GetComponentInParent<CarSpawner>();
    }

    void Start()
    {
        if (splineFollower != null)
            return;

        TileIndex = startTileOffset;
        t = 0f;
        Place();
    }

    void Update()
    {
        if (splineFollower != null)
        {
            UpdateSplineSpeed();
            return;
        }

        if (map == null) return;

        // Our tile was deleted (AI too far behind the player): jump ahead of the player
        if (player != null && TileIndex < player.TileIndex - 1)
        {
            TileIndex = player.TileIndex + 3;
            t = 0f;
        }

        float target = cruiseSpeed;
        if (slowInCorners && CornerAhead()) target = Mathf.Min(target, cornerSpeed);

        float rate = CurrentSpeed > target ? brake : accel;
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, target, rate * Time.deltaTime);

        FollowRoad(CurrentSpeed * Time.deltaTime);
    }

    void UpdateSplineSpeed()
    {
        CarStats stats = spawner != null ? spawner.CurrentStats : null;

        float topSpeed = stats != null && stats.maxSpeed > 0f
            ? stats.maxSpeed / 3.6f * speedFactor
            : cruiseSpeed;
        float acceleration = stats != null && stats.acceleration > 0f
            ? stats.acceleration * accelPerStat
            : accel;

        float target = topSpeed;
        if (splineFollower.CurrentSpeedLimitMs < float.PositiveInfinity)
            target = Mathf.Min(target, splineFollower.CurrentSpeedLimitMs);

        float rate = CurrentSpeed > target
            ? Mathf.Max(brake, splineFollower.CurrentCornerBrakeDeceleration)
            : acceleration;

        CurrentSpeed = Mathf.MoveTowards(
            CurrentSpeed,
            Mathf.Max(0f, target),
            rate * Time.deltaTime);
    }

    // Corner on this tile or the next one
    bool CornerAhead()
    {
        for (int i = 0; i <= 1; i++)
            if (map.TryGetTilePath(TileIndex + i, out RoadTilePath p) &&
                (p.type == RoadTileType.Left || p.type == RoadTileType.Right))
                return true;
        return false;
    }

    void FollowRoad(float distance)
    {
        while (true)
        {
            // No tile available (end of generated road): wait here
            if (!map.TryGetTilePath(TileIndex, out RoadTilePath p)) return;
            GetCurve(p, out _, out _, out _, out float len);

            float remaining = (1f - t) * len;
            if (distance < remaining) { t += distance / len; break; }

            distance -= remaining;
            TileIndex++;
            t = 0f;
        }
        Place();
    }

    void Place()
    {
        if (map == null || !map.TryGetTilePath(TileIndex, out RoadTilePath p)) return;
        GetCurve(p, out Vector3 s, out Vector3 c, out Vector3 e, out _);

        Vector3 pos = (1 - t) * (1 - t) * s + 2 * (1 - t) * t * c + t * t * e;
        Vector3 tangent = 2 * (1 - t) * (c - s) + 2 * t * (e - c);

        if (tangent.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
            transform.rotation = rot;
            pos += rot * Vector3.right * lateralOffset;
        }
        transform.position = pos + Vector3.up * heightOffset;
    }

    static void GetCurve(RoadTilePath p, out Vector3 s, out Vector3 c, out Vector3 e, out float len)
    {
        s = p.start;
        e = p.end;
        bool corner = p.type == RoadTileType.Left || p.type == RoadTileType.Right;

        c = corner
            ? s + p.entryDirection * (Vector3.Distance(s, e) / Mathf.Sqrt(2f))
            : (s + e) * 0.5f;

        len = (Vector3.Distance(s, e) + Vector3.Distance(s, c) + Vector3.Distance(c, e)) * 0.5f;
    }
}