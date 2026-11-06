using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour, ISpeedSource
{
    public Gearbox gearbox;
    public CornerBraking corners;
    public RaceSplineFollower splineFollower;
    public Map_Gen map;
    
    public float heightOffset = 0.5f;
    public float lateralOffset = 1.5f;

    public float clutchDrag = 0.8f;
    [Min(1f)]
    public float accelerationMultiplier = 1.3f;
    [Min(0f)]
    public float engineBogStrength = 2f;
    [Range(0.05f, 0.5f)]
    public float engineBogSpeedFraction = 0.2f;
    public float greenBonus = 1.5f;        // m/s added on a green UPSHIFT
    public float redPenalty = 0.9f;        // speed multiplier on a red shift
    public float engineBrake = 5f;

    [Header("Power-up")]
    [Min(1)]
    public int chargesToActivate = 5;
    [Min(0.1f)]
    public float turboDuration = 2f;
    [Min(1f)]
    public float turboSpeedMultiplier = 1.15f;
    [Min(0f)]
    public float turboAcceleration = 20f;
    public KeyCode powerUpKey = KeyCode.RightShift;


    public float CurrentSpeed { get; private set; }   // m/s
    public int TileIndex { get; private set; }
    public int PowerUpCharges { get; private set; }
    public bool TurboActive => turboTimeRemaining > 0f;

    float t;   // progress along the current tile, 0..1
    float turboTimeRemaining;

    void Awake()
    {
        if (gearbox == null)
            gearbox = GetComponent<Gearbox>()
                ?? GetComponentInChildren<Gearbox>()
                ?? GetComponentInParent<Gearbox>();
        if (splineFollower == null)
            splineFollower = GetComponent<RaceSplineFollower>()
                ?? GetComponentInChildren<RaceSplineFollower>()
                ?? GetComponentInParent<RaceSplineFollower>();
        if (map == null) map = FindFirstObjectByType<Map_Gen>();
    }

    void Start()
    {
        if (splineFollower != null)
            return;

        TileIndex = 0;
        t = 0f;
        Place();
    }

    void OnEnable()
    {
        if (gearbox != null)
            gearbox.Shifted += OnShifted;
    }

    void OnDisable()
    {
        if (gearbox != null)
            gearbox.Shifted -= OnShifted;
    }

    void OnShifted(ShiftZone zone)
    {
        if (zone == ShiftZone.Green && gearbox.LastShiftWasUp)
        {
            CurrentSpeed += greenBonus;
            PowerUpCharges = Mathf.Min(
                PowerUpCharges + 1,
                MaxPowerUpCharges);
        }
        if (zone == ShiftZone.Red) CurrentSpeed *= redPenalty;
    }

    void Update()
    {
        TryActivatePowerUp();
        if (turboTimeRemaining > 0f)
            turboTimeRemaining = Mathf.Max(
                0f,
                turboTimeRemaining - Time.deltaTime);

        UpdateSpeed(Time.deltaTime);
        if (splineFollower == null)
            FollowRoad(CurrentSpeed * Time.deltaTime);
    }

    void TryActivatePowerUp()
    {
        if (!PowerUpButtonPressed() ||
            turboTimeRemaining > 0f ||
            PowerUpCharges < Mathf.Max(1, chargesToActivate))
            return;

        PowerUpCharges -= Mathf.Max(1, chargesToActivate);
        turboTimeRemaining = turboDuration;
    }

    bool PowerUpButtonPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            powerUpKey == KeyCode.RightShift &&
            Keyboard.current.rightShiftKey.wasPressedThisFrame)
            return true;
#else
        return Input.GetKeyDown(powerUpKey);
#endif
        return false;
    }

    int MaxPowerUpCharges => Mathf.Max(1, chargesToActivate);

    void UpdateSpeed(float dt)
    {
        if (gearbox == null)
        {
            Debug.LogError($"{name}: PlayerController requires a Gearbox reference.", this);
            enabled = false;
            return;
        }

        if (TurboActive)
        {
            float turboCap = GetTurboSpeedCap();
            CurrentSpeed = Mathf.MoveTowards(
                CurrentSpeed,
                turboCap,
                turboAcceleration * dt);
            gearbox.UpdateRpm(CurrentSpeed);
            return;
        }

        float cornerLimit = splineFollower != null
            ? splineFollower.CurrentSpeedLimitMs
            : corners != null
                ? corners.SpeedLimit
                : float.PositiveInfinity;
        float cornerBrake = corners != null ? corners.BrakeDecel : 0f;
        float gearMax = gearbox.MaxSpeed;

        float cap = Mathf.Min(gearMax, cornerLimit);
        float splineCornerBrake = splineFollower != null
            ? splineFollower.CurrentCornerBrakeDeceleration
            : 0f;
        if (splineFollower != null &&
            splineFollower.CurrentSegment != null &&
            splineFollower.CurrentSegment.IsCorner &&
            CurrentSpeed > cornerLimit)
        {
            float targetSpeed = Mathf.Max(0.1f, cornerLimit);
            float excessFactor = Mathf.Max(1f, CurrentSpeed / targetSpeed);
            splineCornerBrake *= excessFactor;
        }
        float brakeRate = cornerLimit < gearMax
            ? Mathf.Max(cornerBrake, splineCornerBrake)
            : engineBrake;

        if (gearbox.InClutch)
        {
            gearbox.TickClutch(dt);
            CurrentSpeed = Mathf.Max(0f, CurrentSpeed - clutchDrag * dt);
            if (CurrentSpeed > cornerLimit)   // corners still slow you down on the clutch
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, cornerLimit, cornerBrake * dt);
        }
        else
        {
            if (gearbox.ShouldStall(CurrentSpeed))
            {
                gearbox.Stall();
                CurrentSpeed = 0f;
                return;
            }

            bool engineBogging = gearbox.CurrentGear > 1 &&
                CurrentSpeed < gearbox.MaxSpeed * engineBogSpeedFraction;
            if (engineBogging)
            {
                CurrentSpeed = Mathf.Max(
                    0f,
                    CurrentSpeed - engineBogStrength * dt);
            }
            else if (CurrentSpeed > cap)     // above the corner limit or the gear's top speed
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, cap, brakeRate * dt);
            else
            {
                float acceleration = gearbox.Acceleration * accelerationMultiplier;
                CurrentSpeed = Mathf.Min(
                    CurrentSpeed + acceleration * dt,
                    cap);
            }

            gearbox.UpdateRpm(CurrentSpeed);
        }
    }

    float GetTurboSpeedCap()
    {
        float normalTopSpeed = gearbox.gears != null &&
            gearbox.gears.Length > 0
            ? gearbox.gears[gearbox.gears.Length - 1].maxSpeed
            : gearbox.MaxSpeed;
        return normalTopSpeed * turboSpeedMultiplier;
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