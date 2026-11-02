using UnityEngine;

[CreateAssetMenu(fileName = "CarCatalog", menuName = "Gear4Speed/Car Catalog")]
public class CarCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string displayName = "Car";
        public GameObject prefab;

        [Header("Balance")]
        public float topSpeed = 180f;
        public float acceleration = 4f;
        [Range(0f, 1f)] public float handling = 0.5f;
        public CarPowerUp powerUp = CarPowerUp.None;
        [Min(0f)] public float powerUpChargeRequired = 100f;
        public AudioClip engineSound;
        [Min(1f)] public float maxRpm = 4000f;
        [Min(0f)] public float rpmIncrease = 400f;
        [Min(0f)] public float rpmDecrease = 400f;

        [Header("Transmission")]
        public Gearbox.GearSetting[] gears =
        {
            new Gearbox.GearSetting { name = "1st", maxSpeed = 12f, acceleration = 4f, gearRatio = 3.5f },
            new Gearbox.GearSetting { name = "2nd", maxSpeed = 18f, acceleration = 3.5f, gearRatio = 2.1f },
            new Gearbox.GearSetting { name = "3rd", maxSpeed = 27f, acceleration = 3f, gearRatio = 1.4f },
            new Gearbox.GearSetting { name = "4th", maxSpeed = 40.5f, acceleration = 2.5f, gearRatio = 1f }
        };

        public float TopSpeed => topSpeed;
        public float Acceleration => acceleration;
        public float Handling => handling;
    }

    public Entry[] cars;

    void OnValidate()
    {
        if (cars == null) return;
        foreach (var car in cars)
        {
            if (car != null && (car.gears == null || car.gears.Length == 0))
                car.gears = CreateDefaultGears();
        }
    }

    static Gearbox.GearSetting[] CreateDefaultGears()
    {
        return new[]
        {
            new Gearbox.GearSetting { name = "1st", maxSpeed = 12f, acceleration = 4f, gearRatio = 3.5f },
            new Gearbox.GearSetting { name = "2nd", maxSpeed = 18f, acceleration = 3.5f, gearRatio = 2.1f },
            new Gearbox.GearSetting { name = "3rd", maxSpeed = 27f, acceleration = 3f, gearRatio = 1.4f },
            new Gearbox.GearSetting { name = "4th", maxSpeed = 40.5f, acceleration = 2.5f, gearRatio = 1f }
        };
    }
}

public static class GameSelection
{
    public static int CarIndex;   // survives scene loads
    public static GameObject SelectedPrefab;
    public static CarCatalog.Entry SelectedCar;
}