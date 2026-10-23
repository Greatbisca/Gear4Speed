using UnityEngine;

[CreateAssetMenu(
    fileName = "CarStats",
    menuName = "Gear4Speed/Cars/Car Stats"
)]
public class CarStats : ScriptableObject
{
    [Header("Car")]
    [Range(0f, 1f)]
    public float handling = 0.5f;
    [Min(1)]
    public int gearCount = 4;
    [Min(0f)]
    [Tooltip("Velocidade maxima do carro, em km/h.")]
    public float maximumVelocity = 180f;
    [Min(0f)]
    [Tooltip("Aceleracao de referencia do carro, em m/s2.")]
    public float acceleration = 4f;

    [Header("Gear ratios")]
    [Min(0.01f)]
    public float[] gearRatios = { 1000f, 2.1f, 1.4f, 1f };
    [Min(0.01f)]
    public float finalDriveRatio = 3.4f;

    private void OnValidate()
    {
        gearCount = Mathf.Max(1, gearCount);
        maximumVelocity = Mathf.Max(0f, maximumVelocity);
        acceleration = Mathf.Max(0f, acceleration);
        finalDriveRatio = Mathf.Max(0.01f, finalDriveRatio);

        if (gearRatios == null || gearRatios.Length != gearCount)
        {
            float[] previousRatios = gearRatios;
            gearRatios = new float[gearCount];

            for (int i = 0; i < gearRatios.Length; i++)
            {
                gearRatios[i] = previousRatios != null &&
                                i < previousRatios.Length
                    ? previousRatios[i]
                    : Mathf.Max(1f, 3.5f - i * 0.8f);
            }
        }

        for (int i = 0; i < gearRatios.Length; i++)
            gearRatios[i] = Mathf.Max(0.01f, gearRatios[i]);
    }
}
