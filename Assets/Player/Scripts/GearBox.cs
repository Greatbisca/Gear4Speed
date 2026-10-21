using UnityEngine;
using UnityEngine.InputSystem;

public enum ShiftZone { None, Red, Yellow, Green }

public class Gearbox : MonoBehaviour
{
    public int gearCount = 4;
    public float firstGearSpeedPerRpm = 0.005f;
    public float[] accelerations = { 3f, 2.5f, 2f, 1.5f };
    
    public float idleRpm = 800f;
    public float shiftPromptRpm = 3000f;
    public float limiterRpm = 4000f;
    
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

    float[] speedPerRpm;

    void Awake()
    {
        float step = shiftPromptRpm / 2000f;   // 1.5: shift at 3k lands near 2k
        speedPerRpm = new float[gearCount];
        for (int i = 0; i < gearCount; i++)
            speedPerRpm[i] = firstGearSpeedPerRpm * Mathf.Pow(step, i);
    }

    float Ratio => speedPerRpm[CurrentGear - 1];
    public float Acceleration => accelerations[CurrentGear - 1];
    public float MaxSpeed => limiterRpm * Ratio;
    public bool ShiftPromptActive => !InClutch && CurrentGear < gearCount && Rpm >= shiftPromptRpm;
    public int NextGear => CurrentGear + 1;

    public void UpdateRpm(float speed) => Rpm = Mathf.Max(idleRpm, speed / Ratio);

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
        if (CurrentGear >= gearCount) return;
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
        if (GearSelected) Rpm = Mathf.Max(idleRpm, Rpm - fallRate * dt);
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