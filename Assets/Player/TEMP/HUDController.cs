using UnityEngine;
using TMPro;

public class HudController : MonoBehaviour
{
    public PlayerController car;
    public Gearbox gearbox;
    public TMP_Text speedText, rpmText, gearText, shiftText, zoneText, resultText;

    float resultTimer;

    void OnEnable()  => gearbox.Shifted += OnShifted;
    void OnDisable() => gearbox.Shifted -= OnShifted;

    void OnShifted(ShiftZone zone)
    {
        resultText.text = zone == ShiftZone.None ? "NO SHIFT" : zone.ToString().ToUpper();
        resultText.color = zone == ShiftZone.Green ? Color.green
            : zone == ShiftZone.Yellow ? Color.yellow
            : zone == ShiftZone.Red ? Color.red : Color.white;
        resultTimer = 1.2f;
    }

    void Update()
    {
        speedText.text = $"{car.CurrentSpeed * 3.6f:0} km/h";
        rpmText.text   = $"{gearbox.Rpm:0} RPM";
        gearText.text  = gearbox.CurrentGear.ToString();

        shiftText.gameObject.SetActive(gearbox.ShiftPromptActive);
        shiftText.text = $"SHIFT TO {gearbox.NextGear}";

        // Debug view of the zone until the real gauge exists
        zoneText.gameObject.SetActive(gearbox.GearSelected);
        zoneText.text = $"GREEN {gearbox.GreenMin:0}-{gearbox.GreenMax:0}   " +
                        $"YELLOW {gearbox.YellowMin:0}-{gearbox.YellowMax:0}";

        resultTimer -= Time.deltaTime;
        resultText.gameObject.SetActive(resultTimer > 0f);
    }
}