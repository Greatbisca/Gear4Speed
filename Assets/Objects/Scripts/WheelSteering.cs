using UnityEngine;

[RequireComponent(typeof(CarWheels))]
public class WheelSteering : MonoBehaviour
{
    public float maxSteerAngle = 35f;
    public float smoothing = 12f;
    public float minSpeed = 0.5f;

    [Header("Opcional: input direto do jogador")]
    public bool usePlayerInput = false;

    CarWheels car;
    Transform src;
    float lastYaw;
    float current;

    void Start()
    {
        car = GetComponent<CarWheels>();
        src = car.motionSource != null ? car.motionSource : transform;
        lastYaw = src.eulerAngles.y;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || car == null) return;

        float target;

        if (usePlayerInput)
        {
            target = Input.GetAxis("Horizontal") * maxSteerAngle;
        }
        else
        {
            float yaw = src.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;

            float speed = car.SignedSpeed;
            if (Mathf.Abs(speed) < minSpeed)
            {
                target = 0f;
            }
            else
            {
                float yawRad = yawRate * Mathf.Deg2Rad;
                target = Mathf.Atan(car.WheelBase * yawRad / speed) * Mathf.Rad2Deg;
                target = Mathf.Clamp(target, -maxSteerAngle, maxSteerAngle);
            }
        }

        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * dt));
        car.steerAngle = current;
    }
}