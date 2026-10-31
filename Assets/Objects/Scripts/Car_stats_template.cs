using UnityEngine;

public class CarStats : MonoBehaviour
{
    [Header("Vehicle stats")]
    [Range(0f, 1f)] public float handling = 0.5f;
    [Min(0f)] public float acceleration = 4f;
    [Min(0f)] public float baseSpeed = 3f;

    [Header("Transmission (o número de mudanças define o H do stick)")]
    public Gearbox.GearSetting[] gears =
    {
        new Gearbox.GearSetting { name = "1st", maxSpeed = 12f,   acceleration = 4f   },
        new Gearbox.GearSetting { name = "2nd", maxSpeed = 18f,   acceleration = 3.5f },
        new Gearbox.GearSetting { name = "3rd", maxSpeed = 27f,   acceleration = 3f   },
        new Gearbox.GearSetting { name = "4th", maxSpeed = 40.5f, acceleration = 2.5f },
    };
}