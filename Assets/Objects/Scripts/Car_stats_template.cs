using UnityEngine;

// Stats de um carro. O CarSpawner preenche-os a partir do CarCatalog (ou usa os valores do prefab).
public class CarStats : MonoBehaviour
{
    [Header("Vehicle stats")]
    [Tooltip("Velocidade máxima em km/h.")]
    public float maxSpeed = 180f;
    [Min(0f)] public float acceleration = 4f;
    [Range(0f, 1f)] public float handling = 0.5f;

    [Header("Shift")]
    [Range(0.5f, 2.5f)]
    [Tooltip("Largura da zona verde/amarela da mudança perfeita. 1 = normal.")]
    public float shiftTolerance = 1f;

    [Header("Power-up")]
    public CarPowerUp powerUp = CarPowerUp.None;
    [Min(0f)] public float powerUpChargeRequired = 100f;

    [Header("Engine")]
    public AudioClip engineSound;
    [Min(1f)] public float maxRpm = 4000f;
    [Min(0f)] public float rpmIncrease = 400f;
    [Min(0f)] public float rpmDecrease = 400f;

    [Header("Transmission (o número de mudanças define o H do stick, máx. 6)")]
    public Gearbox.GearSetting[] gears =
    {
        new Gearbox.GearSetting { name = "1st", maxSpeed = 12f,   acceleration = 4f,   gearRatio = 3.5f },
        new Gearbox.GearSetting { name = "2nd", maxSpeed = 18f,   acceleration = 3.5f, gearRatio = 2.1f },
        new Gearbox.GearSetting { name = "3rd", maxSpeed = 27f,   acceleration = 3f,   gearRatio = 1.4f },
        new Gearbox.GearSetting { name = "4th", maxSpeed = 40.5f, acceleration = 2.5f, gearRatio = 1f   },
    };
}

public enum CarPowerUp
{
    None,
    Nitro,
    Shield,
    Repair,
    Grip
}