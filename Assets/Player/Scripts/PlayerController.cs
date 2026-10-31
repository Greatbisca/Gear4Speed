using UnityEngine;

public class PlayerController : MonoBehaviour, ISpeedSource
{
    public Gearbox gearbox;
    public CornerBraking corners;
    public Map_Gen map;
    
    public float heightOffset = 0.5f;
    public float lateralOffset = 1.5f;

    public float clutchDrag = 0.8f;
    public float greenBonus = 1f;          // m/s added on a green UPSHIFT
    public float redPenalty = 0.85f;       // speed multiplier on a red shift
    public float engineBrake = 5f;


    public float CurrentSpeed { get; private set; }   // m/s
    public int TileIndex { get; private set; }

    float t;   // progress along the current tile, 0..1

    void Awake()
    {
        if (map == null) map = FindFirstObjectByType<Map_Gen>();
    }

    void Start()
    {
        TileIndex = 0;
        t = 0f;
        Place();
    }

    void OnEnable()  => gearbox.Shifted += OnShifted;
    void OnDisable() => gearbox.Shifted -= OnShifted;

    void OnShifted(ShiftZone zone)
    {
        if (zone == ShiftZone.Green && gearbox.LastShiftWasUp) CurrentSpeed += greenBonus;
        if (zone == ShiftZone.Red) CurrentSpeed *= redPenalty;
    }

    void Update()
    {
        UpdateSpeed(Time.deltaTime);
        FollowRoad(CurrentSpeed * Time.deltaTime);
    }

    void UpdateSpeed(float dt)
    {
        float cornerLimit = corners != null ? corners.SpeedLimit : float.PositiveInfinity;
        float cornerBrake = corners != null ? corners.BrakeDecel : 0f;
        float gearMax = gearbox.MaxSpeed;

        float cap = Mathf.Min(gearMax, cornerLimit);
        float brakeRate = cornerLimit < gearMax ? cornerBrake : engineBrake;

        if (gearbox.InClutch)
        {
            gearbox.TickClutch(dt);
            CurrentSpeed = Mathf.Max(0f, CurrentSpeed - clutchDrag * dt);
            if (CurrentSpeed > cornerLimit)   // corners still slow you down on the clutch
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, cornerLimit, cornerBrake * dt);
        }
        else
        {
            if (CurrentSpeed > cap)     // above the corner limit or the gear's top speed
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, cap, brakeRate * dt);
            else
                CurrentSpeed = Mathf.Min(CurrentSpeed + gearbox.Acceleration * dt, cap);

            gearbox.UpdateRpm(CurrentSpeed);
        }
    }

    // ---------- Road following ----------
    void FollowRoad(float distance)
    {
        if (map == null) return;

        while (true)
        {
            if (!map.TryGetTilePath(TileIndex, out RoadTilePath p)) return;
            GetCurve(p, out _, out _, out _, out float len);

            float remaining = (1f - t) * len;
            if (distance < remaining) { t += distance / len; break; }

            distance -= remaining;
            TileIndex++;
            t = 0f;
        }

        map.UpdateStreaming(TileIndex);
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
            pos += rot * Vector3.right * lateralOffset;   // shift into the lane
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