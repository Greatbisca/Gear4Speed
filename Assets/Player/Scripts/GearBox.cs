using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum ShiftZone { None, Red, Yellow, Green }

public class Gearbox : MonoBehaviour
{
    [System.Serializable]
    public class GearSetting
    {
        public string name = "Gear";
        [Tooltip("Top speed in this gear, in m/s (reached at the RPM limiter).")]
        public float maxSpeed = 12f;
        [Tooltip("Acceleration in this gear, in m/s2.")]
        public float acceleration = 4f;
    }

    public GearSetting[] gears =
    {
        new GearSetting { name = "1st", maxSpeed = 12f,   acceleration = 4f   },
        new GearSetting { name = "2nd", maxSpeed = 18f,   acceleration = 3.5f },
        new GearSetting { name = "3rd", maxSpeed = 27f,   acceleration = 3f   },
        new GearSetting { name = "4th", maxSpeed = 40.5f, acceleration = 2.5f },
    };

    public float idleRpm = 800f;
    public float shiftPromptRpm = 3000f;     // upshift prompt
    public float downshiftPromptRpm = 1200f; // downshift prompt
    public float limiterRpm = 4000f;

    [Header("Lugging (low RPM = weak acceleration)")]
    public float powerRpm = 1500f;           // full power from here upwards
    [Range(0f, 1f)] public float lugFactor = 0.25f;

    [Header("Counter-rotation: upshift (needle falls)")]
    public float zoneMinRpm = 2000f;
    public float zoneMaxRpm = 3000f;

    [Header("Counter-rotation: downshift (needle rises)")]
    public float downZoneMinRpm = 1800f;
    public float downZoneMaxRpm = 3000f;

    [Header("Counter-rotation: shared")]
    public float zoneLead = 500f;            // how far ahead of the needle the zone appears
    public float greenHalfWidth = 100f;
    public float yellowHalfWidth = 250f;
    public float fallRate = 400f;            // RPM per second while the needle moves

    public int CurrentGear { get; private set; } = 1;
    public float Rpm { get; private set; }
    public bool InClutch { get; private set; }
    public bool GearSelected { get; private set; }
    public float ZoneCenter { get; private set; }
    public bool LastShiftWasUp { get; private set; }

    public event System.Action<ShiftZone> Shifted;
    public event System.Action ClutchPressed;     // clutch pressed
    public event System.Action<int> GearPicked;   // stick moved: +1 = up, -1 = down

    int targetGear;
    int shiftDir;

    GearSetting Current => gears[CurrentGear - 1];
    public int GearCount => gears.Length;
    float SpeedPerRpm => Current.maxSpeed / limiterRpm;

    public float MaxSpeed => Current.maxSpeed;
    public float Acceleration =>
        Current.acceleration * Mathf.Lerp(lugFactor, 1f, Mathf.InverseLerp(idleRpm, powerRpm, Rpm));

    // 0 = no prompt, otherwise the gear the player should shift to
    public int PromptGear
    {
        get
        {
            if (InClutch) return 0;
            if (CurrentGear < GearCount && Rpm >= shiftPromptRpm) return CurrentGear + 1;
            if (CurrentGear > 1 && Rpm <= downshiftPromptRpm) return CurrentGear - 1;
            return 0;
        }
    }
    public bool ShiftPromptActive => PromptGear != 0;
    public int NextGear => PromptGear;

    public void UpdateRpm(float speed) =>
        Rpm = Mathf.Clamp(speed / SpeedPerRpm, idleRpm, limiterRpm);

    public float GreenMin => ZoneCenter - greenHalfWidth;
    public float GreenMax => ZoneCenter + greenHalfWidth;
    public float YellowMin => ZoneCenter - yellowHalfWidth;
    public float YellowMax => ZoneCenter + yellowHalfWidth;

    public ShiftZone ZoneFor(float rpm)
    {
        float d = Mathf.Abs(rpm - ZoneCenter);
        if (d <= greenHalfWidth) return ShiftZone.Green;
        if (d <= yellowHalfWidth) return ShiftZone.Yellow;
        return ShiftZone.Red;
    }

    // --- Clutch flow ---
    public void PressClutch()
    {
        InClutch = true;
        GearSelected = false;
        ClutchPressed?.Invoke();
    }

    // dir = +1 (up) or -1 (down): the stick landed in the next/previous gear
    public void SelectGear(int dir)
    {
        if (!InClutch || GearSelected) return;
        int g = CurrentGear + dir;
        if (g < 1 || g > GearCount) return;

        targetGear = g;
        shiftDir = dir;
        GearSelected = true;

        if (dir > 0)
            ZoneCenter = Mathf.Clamp(Rpm - zoneLead,
                zoneMinRpm + yellowHalfWidth, zoneMaxRpm - yellowHalfWidth);
        else
            ZoneCenter = Mathf.Clamp(Rpm + zoneLead,
                downZoneMinRpm + yellowHalfWidth, downZoneMaxRpm - yellowHalfWidth);

        GearPicked?.Invoke(dir);
    }

    // Called every frame while the clutch is held
    public void TickClutch(float dt)
    {
        if (GearSelected)
            Rpm = Mathf.Clamp(Rpm - shiftDir * fallRate * dt, idleRpm, limiterRpm);
    }

    public void ReleaseClutch()
    {
        if (!InClutch) return;
        InClutch = false;

        if (!GearSelected) { Shifted?.Invoke(ShiftZone.None); return; }

        ShiftZone zone = ZoneFor(Rpm);
        CurrentGear = targetGear;
        LastShiftWasUp = shiftDir > 0;
        GearSelected = false;
        Shifted?.Invoke(zone);
    }

    // TEMP test controls: Space = clutch, Up/Down arrow = stick into next/previous gear
    void Update()
    {
        bool press, release, up, down;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        press = kb.spaceKey.wasPressedThisFrame;
        release = kb.spaceKey.wasReleasedThisFrame;
        up = kb.upArrowKey.wasPressedThisFrame;
        down = kb.downArrowKey.wasPressedThisFrame;
#else
        press   = Input.GetKeyDown(KeyCode.Space);
        release = Input.GetKeyUp(KeyCode.Space);
        up      = Input.GetKeyDown(KeyCode.UpArrow);
        down    = Input.GetKeyDown(KeyCode.DownArrow);
#endif
        if (press) PressClutch();
        if (up) SelectGear(+1);
        if (down) SelectGear(-1);
        if (release) ReleaseClutch();
    }
}