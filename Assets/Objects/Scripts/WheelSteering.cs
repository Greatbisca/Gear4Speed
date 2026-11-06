using UnityEngine;

[RequireComponent(typeof(CarWheels))]
public class WheelSteering : MonoBehaviour
{
    public float maxSteerAngle = 35f;
    public float smoothing = 12f;      // maior = resposta mais rápida
    public float minSpeed = 0.5f;      // abaixo disto não vira (evita tremer parado)

    [Header("Opcional: input direto do jogador")]
    public bool usePlayerInput = false;

    CarWheels car;
    RaceSplineFollower follower;
    Transform src;
    float lastYaw;
    float current;

    void Start()
    {
        car = GetComponent<CarWheels>();
        src = car.motionSource != null ? car.motionSource : transform;
        follower = GetComponentInParent<RaceSplineFollower>();
        lastYaw = CurrentYaw();
    }

    // Direção da estrada em graus (0..360), sem depender da rotação -90 do player
    float CurrentYaw()
    {
        if (follower != null && follower.Heading.sqrMagnitude > 0.01f)
            return Mathf.Atan2(follower.Heading.x, follower.Heading.z) * Mathf.Rad2Deg;
        return src.eulerAngles.y;   // sem follower (mapa antigo)
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
            float yaw = CurrentYaw();
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;   // graus/s
            lastYaw = yaw;

            float speed = car.SignedSpeed;
            if (Mathf.Abs(speed) < minSpeed)
            {
                target = 0f;
            }
            else
            {
                // modelo de bicicleta: tan(ângulo) = distância entre eixos * yawRate / velocidade
                float yawRad = yawRate * Mathf.Deg2Rad;
                target = Mathf.Atan(car.WheelBase * yawRad / speed) * Mathf.Rad2Deg;
                target = Mathf.Clamp(target, -maxSteerAngle, maxSteerAngle);
            }
        }

        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * dt));
        car.steerAngle = current;
    }
}