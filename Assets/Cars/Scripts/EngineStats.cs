using UnityEngine;

[CreateAssetMenu(
    fileName = "EngineStats",
    menuName = "Gear4Speed/Cars/Engine Stats"
)]
public class EngineStats : ScriptableObject
{
    [Header("Engine")]
    [Min(0f)]
    public float maximumTorque = 1000f;
    [Min(0f)]
    public float idleRpm = 800f;
    [Min(1f)]
    public float limiterRpm = 3000f;

    [Header("Torque curve")]
    [Tooltip("X = RPM normalizado entre idle e limiter, Y = percentagem do torque maximo.")]
    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f, 0.65f),
        new Keyframe(0.25f, 1f),
        new Keyframe(0.65f, 0.9f),
        new Keyframe(1f, 0.7f)
    );

    public float GetTorque(float rpm)
    {
        if (maximumTorque <= 0f)
            return 0f;

        float normalizedRpm = Mathf.InverseLerp(
            idleRpm,
            limiterRpm,
            rpm
        );
        return maximumTorque * Mathf.Max(0f, torqueCurve.Evaluate(normalizedRpm));
    }
}
