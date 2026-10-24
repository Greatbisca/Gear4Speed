using UnityEngine;

public class AIController : MonoBehaviour
{
    [Header("References")]
    public Map_Gen map;
    public PlayerController player;      // used only to stay inside the generated tiles

    [Header("Speed (m/s)")]
    public float cruiseSpeed = 12f;
    public bool slowInCorners = true;
    public float cornerSpeed = 8f;
    public float accel = 4f;
    public float brake = 8f;

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
    }

    void Start()
    {
        TileIndex = startTileOffset;
        t = 0f;
        Place();
    }

    void Update()
    {
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