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
        [Tooltip("Transmission ratio for this gear. Reserved for the detailed RPM model.")]
        public float gearRatio = 1f;
        [Tooltip("RPM increase while this gear is engaged. Reserved for the detailed RPM model.")]
        public float rpmIncrease = 400f;
        [Tooltip("RPM decrease while this gear is engaged. Reserved for the detailed RPM model.")]
        public float rpmDecrease = 400f;
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

    // --- H-pattern stick position ---
    public int StickColumn { get; private set; }   // 0..Columns-1
    public int StickRow { get; private set; } = 1; // +1 = top, 0 = neutral, -1 = bottom
    public bool StickInNeutral => StickRow == 0;
    public int Columns => Mathf.Max(1, (GearCount + 1) / 2);

    public event System.Action<ShiftZone> Shifted;
    public event System.Action ClutchPressed;     // clutch pressed
    public event System.Action<int> GearPicked;   // stick landed in a different gear: +1 = up, -1 = down
    public event System.Action NeutralMoved;      // stick moved sideways between neutrals

    int targetGear = 1;
    int shiftDir;
    GearSetting[] defaultGears;

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
    public float PreviewZoneCenter
    {
        get
        {
            bool up;
            if (CurrentGear <= 1) up = true;
            else if (CurrentGear >= GearCount) up = false;
            else up = Rpm >= (shiftPromptRpm + downshiftPromptRpm) * 0.5f;

            return up
                ? Mathf.Clamp(Rpm - zoneLead,
                    zoneMinRpm + yellowHalfWidth, zoneMaxRpm - yellowHalfWidth)
                : Mathf.Clamp(Rpm + zoneLead,
                    downZoneMinRpm + yellowHalfWidth, downZoneMaxRpm - yellowHalfWidth);
        }
    }
    void Awake() => defaultGears = CloneGears(gears);

    // --- Per-car gear sets ---
    static GearSetting[] CloneGears(GearSetting[] src)
    {
        var copy = new GearSetting[src.Length];
        for (int i = 0; i < src.Length; i++)
            copy[i] = new GearSetting
            {
                name = src[i].name,
                maxSpeed = src[i].maxSpeed,
                acceleration = src[i].acceleration,
                gearRatio = src[i].gearRatio,
                rpmIncrease = src[i].rpmIncrease,
                rpmDecrease = src[i].rpmDecrease
            };
        return copy;
    }

    // Swap the gear set (null or empty = defaults)
    public void Configure(GearSetting[] newGears)
    {
        if (newGears == null || newGears.Length == 0) { ResetToDefault(); return; }
        gears = CloneGears(newGears);
        ResetState();
    }

    public void ResetToDefault()
    {
        if (defaultGears != null) gears = CloneGears(defaultGears);
        ResetState();
    }

    void ResetState()
    {
        CurrentGear = 1;
        InClutch = false;
        GearSelected = false;
        targetGear = 1;
        StickColumn = 0;
        StickRow = 1;
    }

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

    // Gear (1-based) at a stick slot, 0 for neutral, -1 if the slot doesn't exist
    int GearAt(int column, int row)
    {
        if (row == 0) return 0;
        int g = column * 2 + (row > 0 ? 1 : 2);
        return g <= GearCount ? g : -1;
    }

    // --- Clutch flow ---
    public void PressClutch()
    {
        InClutch = true;
        GearSelected = false;

        // stick starts in the slot of the current gear
        StickColumn = (CurrentGear - 1) / 2;
        StickRow = CurrentGear % 2 == 1 ? 1 : -1;
        targetGear = CurrentGear;

        ClutchPressed?.Invoke();
    }

    // dx: -1 left / +1 right (only works from neutral)
    // dy: +1 up / -1 down
    public void MoveStick(int dx, int dy)
    {
        if (!InClutch) return;

        if (dy != 0)
        {
            int newRow = Mathf.Clamp(StickRow + dy, -1, 1);
            if (newRow == StickRow) return;
            if (GearAt(StickColumn, newRow) < 0) return;   // no gear in that slot

            StickRow = newRow;
            OnStickRowChanged();
        }
        else if (dx != 0)
        {
            if (!StickInNeutral) return;                   // must pass through neutral
            int newCol = Mathf.Clamp(StickColumn + dx, 0, Columns - 1);
            if (newCol == StickColumn) return;

            StickColumn = newCol;
            NeutralMoved?.Invoke();
        }
    }

    void OnStickRowChanged()
    {
        int g = GearAt(StickColumn, StickRow);

        // neutral, or back in the gear we already have: nothing selected
        if (g <= 0 || g == CurrentGear)
        {
            GearSelected = false;
            targetGear = CurrentGear;
            return;
        }

        targetGear = g;
        shiftDir = g > CurrentGear ? 1 : -1;
        GearSelected = true;

        if (shiftDir > 0)
            ZoneCenter = Mathf.Clamp(Rpm - zoneLead,
                zoneMinRpm + yellowHalfWidth, zoneMaxRpm - yellowHalfWidth);
        else
            ZoneCenter = Mathf.Clamp(Rpm + zoneLead,
                downZoneMinRpm + yellowHalfWidth, downZoneMaxRpm - yellowHalfWidth);

        GearPicked?.Invoke(shiftDir);
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

        // released in neutral or in the same gear: no shift
        if (!GearSelected) { Shifted?.Invoke(ShiftZone.None); return; }

        ShiftZone zone = ZoneFor(Rpm);
        CurrentGear = targetGear;
        LastShiftWasUp = shiftDir > 0;
        GearSelected = false;
        Shifted?.Invoke(zone);
    }

    // TEMP test controls: Space = clutch, arrows = stick
    void Update()
    {
        bool press, release, up, down, left, right;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        press = kb.spaceKey.wasPressedThisFrame;
        release = kb.spaceKey.wasReleasedThisFrame;
        up = kb.upArrowKey.wasPressedThisFrame;
        down = kb.downArrowKey.wasPressedThisFrame;
        left = kb.leftArrowKey.wasPressedThisFrame;
        right = kb.rightArrowKey.wasPressedThisFrame;
#else
        press   = Input.GetKeyDown(KeyCode.Space);
        release = Input.GetKeyUp(KeyCode.Space);
        up      = Input.GetKeyDown(KeyCode.UpArrow);
        down    = Input.GetKeyDown(KeyCode.DownArrow);
        left    = Input.GetKeyDown(KeyCode.LeftArrow);
        right   = Input.GetKeyDown(KeyCode.RightArrow);
#endif
        if (press) PressClutch();
        if (up) MoveStick(0, +1);
        if (down) MoveStick(0, -1);
        if (left) MoveStick(-1, 0);
        if (right) MoveStick(+1, 0);
        if (release) ReleaseClutch();
    }
}