using UnityEngine;
using UnityEngine.InputSystem;

public enum ShiftZone { None, Red, Yellow, Green }

public class Gearbox : MonoBehaviour
{
    [SerializeField] private CarStats carStats;
    [SerializeField] private EngineStats engineStats;

    public float shiftPromptRpm = 3000f;
    public float zoneMinRpm = 2000f;     // zone always stays inside this range...
    public float zoneMaxRpm = 3000f;     // ...and this one
    public float zoneLead = 500f;        // how far below the needle the zone appears
    public float greenHalfWidth = 100f;
    public float yellowHalfWidth = 250f;
    public float fallRate = 400f;        // RPM per second while the needle falls

    public int CurrentGear { get; private set; } = 1;
    public float Rpm { get; private set; }
    public bool InClutch { get; private set; }
    public bool GearSelected { get; private set; }  // true = zone visible, needle falling
    public float ZoneCenter { get; private set; }

    public event System.Action<ShiftZone> Shifted;

    private void Awake()
    {
        if (carStats == null)
        {
            carStats = ScriptableObject.CreateInstance<CarStats>();
            Debug.LogWarning(
                "Gearbox nao tem um CarStats atribuido. " +
                "Foi criado um perfil temporario com valores default.",
                this
            );
        }

        if (engineStats == null)
        {
            engineStats = ScriptableObject.CreateInstance<EngineStats>();
            Debug.LogWarning(
                "Gearbox nao tem um EngineStats atribuido. " +
                "Foi criado um perfil temporario com valores default.",
                this
            );
        }
    }

    private int GearCount =>
        carStats == null ? 1 : Mathf.Max(1, carStats.gearCount);

    private float CurrentGearRatio
    {
        get
        {
            if (carStats == null ||
                carStats.gearRatios == null ||
                carStats.gearRatios.Length == 0)
                return 1f;

            int index = Mathf.Clamp(
                CurrentGear - 1,
                0,
                carStats.gearRatios.Length - 1
            );
            return carStats.gearRatios[index] *
                   carStats.finalDriveRatio;
        }
    }

    private float TopGearRatio
    {
        get
        {
            if (carStats == null ||
                carStats.gearRatios == null ||
                carStats.gearRatios.Length == 0)
                return 1f;

            return carStats.gearRatios[
                carStats.gearRatios.Length - 1
            ] * carStats.finalDriveRatio;
        }
    }

    public float Acceleration
    {
        get
        {
            if (carStats == null || engineStats == null)
                return 0f;

            float torqueFactor = engineStats.GetTorque(Rpm) /
                                 Mathf.Max(0.01f, engineStats.maximumTorque);
            float gearFactor = CurrentGearRatio /
                               Mathf.Max(0.01f, TopGearRatio);
            return carStats.acceleration * torqueFactor * gearFactor;
        }
    }

    public float MaxSpeed
    {
        get
        {
            if (carStats == null || engineStats == null)
                return 0f;

            float topSpeed = carStats.maximumVelocity / 3.6f;
            return topSpeed * TopGearRatio /
                   Mathf.Max(0.01f, CurrentGearRatio);
        }
    }

    public float CurrentTorque =>
        engineStats == null ? 0f : engineStats.GetTorque(Rpm);

    public bool ShiftPromptActive =>
        !InClutch && CurrentGear < GearCount && Rpm >= shiftPromptRpm;
    public int NextGear => Mathf.Min(CurrentGear + 1, GearCount);

    public void UpdateRpm(float speed)
    {
        if (engineStats == null)
            return;

        Rpm = Mathf.Clamp(
            speed / Mathf.Max(0.01f, MaxSpeed) * engineStats.limiterRpm,
            engineStats.idleRpm,
            engineStats.limiterRpm
        );
    }

    public float GreenMin => ZoneCenter - greenHalfWidth;
    public float GreenMax => ZoneCenter + greenHalfWidth;
    public float YellowMin => ZoneCenter - yellowHalfWidth;
    public float YellowMax => ZoneCenter + yellowHalfWidth;

    public ShiftZone ZoneFor(float rpm)
    {
        float d = Mathf.Abs(rpm - ZoneCenter);
        if (d <= greenHalfWidth)  return ShiftZone.Green;
        if (d <= yellowHalfWidth) return ShiftZone.Yellow;
        return ShiftZone.Red;
    }

    // --- Clutch flow ---
    public void PressClutch()
    {
        if (CurrentGear >= GearCount) return;
        InClutch = true;
        GearSelected = false;
    }

    // Called when the stick lands in the next gear
    public void SelectGear()
    {
        if (!InClutch || GearSelected) return;
        GearSelected = true;
        ZoneCenter = Mathf.Clamp(Rpm - zoneLead,
                                 zoneMinRpm + yellowHalfWidth,
                                 zoneMaxRpm - yellowHalfWidth);
    }

    // Called every frame by the car while the clutch is held
    public void TickClutch(float dt)
    {
        if (GearSelected && engineStats != null)
        {
            Rpm = Mathf.Max(
                engineStats.idleRpm,
                Rpm - fallRate * dt
            );
        }
    }

    public void ReleaseClutch()
    {
        if (!InClutch) return;
        InClutch = false;

        if (!GearSelected) { Shifted?.Invoke(ShiftZone.None); return; }

        ShiftZone zone = ZoneFor(Rpm);
        CurrentGear++;
        GearSelected = false;
        Shifted?.Invoke(zone);
    }

    // TEMP test controls: Space = clutch, Up Arrow = "stick landed in gear"
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame)   PressClutch();
        if (kb.upArrowKey.wasPressedThisFrame) SelectGear();
        if (kb.spaceKey.wasReleasedThisFrame)  ReleaseClutch();
    }
}