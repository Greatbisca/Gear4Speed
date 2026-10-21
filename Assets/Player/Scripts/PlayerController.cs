using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public Gearbox gearbox;
    public float clutchDrag = 0.8f;       // speed lost per second while clutch is held

    [Header("Shift results")]
    public float greenBonus = 1f;         // m/s added on a green release
    public float redPenalty = 0.85f;      // speed multiplier on a red release

    public float CurrentSpeed { get; private set; }

    void OnEnable()  => gearbox.Shifted += OnShifted;
    void OnDisable() => gearbox.Shifted -= OnShifted;

    void OnShifted(ShiftZone zone)
    {
        if (zone == ShiftZone.Green) CurrentSpeed += greenBonus;
        if (zone == ShiftZone.Red)   CurrentSpeed *= redPenalty;
    }

    void Update()
    {
        if (gearbox.InClutch)
        {
            gearbox.TickClutch(Time.deltaTime);
            CurrentSpeed = Mathf.Max(0f, CurrentSpeed - clutchDrag * Time.deltaTime);
        }
        else
        {
            CurrentSpeed = Mathf.Min(
                CurrentSpeed + gearbox.Acceleration * Time.deltaTime,
                gearbox.MaxSpeed);
            gearbox.UpdateRpm(CurrentSpeed);
        }

        transform.Translate(Vector3.forward * CurrentSpeed * Time.deltaTime);
    }
}
