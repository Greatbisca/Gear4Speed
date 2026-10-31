using UnityEngine;

[CreateAssetMenu(fileName = "CarCatalog", menuName = "Gear4Speed/Car Catalog")]
public class CarCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string displayName = "Car";
        public GameObject prefab;

        [Header("Showroom stats (display only for now)")]
        [Tooltip("km/h")]
        public float topSpeed = 180f;
        [Tooltip("m/s2")]
        public float acceleration = 4f;
        [Range(0f, 1f)]
        public float handling = 0.5f;
    }

    public Entry[] cars;
}

public static class GameSelection
{
    public static int CarIndex;   // survives scene loads
}