using UnityEngine;

[System.Serializable]
public class GearStats
{
    public string name = "Gear";
    [Min(0f)]
    public float maximumSpeed = 5f;
    [Min(0f)]
    public float maximumTorque = 1000f;
}

public class PlayerStats : MonoBehaviour
{
    [Header("Default vehicle stats")]
    [Range(0f, 1f)]
    [SerializeField] private float handling = 0.5f;
    [Min(0f)]
    [SerializeField] private float acceleration = 4f;
    [Min(0f)]
    [SerializeField] private float baseSpeed = 3f;

    [Header("Transmission")]
    [Min(0)]
    [SerializeField] private int currentGear = 1;
    [SerializeField] private GearStats[] gears =
    {
        new GearStats { name = "1st", maximumSpeed = 3f, maximumTorque = 1000f },
        new GearStats { name = "2nd", maximumSpeed = 6f, maximumTorque = 700f },
        new GearStats { name = "3rd", maximumSpeed = 9f, maximumTorque = 450f }
    };

    public float Handling => handling;
    public float Acceleration => acceleration;
    public float BaseSpeed => baseSpeed;

    public GearStats CurrentGear
    {
        get
        {
            if (gears == null || gears.Length == 0)
                return null;

            int index = Mathf.Clamp(currentGear - 1, 0, gears.Length - 1);
            return gears[index];
        }
    }
}
