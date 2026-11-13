using UnityEngine;

// Alvo invisível da câmara: fica no carro e olha na direção da estrada.
// Corre DEPOIS do RaceSplineFollower (100) e ANTES do Cinemachine Brain (200).
[DefaultExecutionOrder(150)]
public class ChaseTarget : MonoBehaviour
{
    public Transform car;                              // arrasta o Player
    public RaceSplineFollower follower;                // vazio = procura no car

    [Tooltip("0 = preso à direção da estrada, maior = um pouco mais suave")]
    [Range(0f, 40f)] public float turnSmoothing = 20f;
    public float minMoveSpeed = 0.3f;                  // sem follower: abaixo disto mantém a direção
    [Tooltip("Só antes de o carro andar. Experimenta 90 / -90 / 180 se o arranque estiver errado.")]
    public float startYaw = 0f;

    Vector3 lastPos;
    bool hasHeading;

    void Start()
    {
        if (car == null) return;

        if (follower == null)
            follower = car.GetComponentInParent<RaceSplineFollower>()
                    ?? car.GetComponentInChildren<RaceSplineFollower>();

        lastPos = car.position;
        transform.position = car.position;
        transform.rotation = Quaternion.Euler(0f, startYaw, 0f);

        Vector3 f = follower != null
            ? follower.Heading
            : Vector3.ProjectOnPlane(car.forward, Vector3.up);
        if (f.sqrMagnitude > 0.05f)
        {
            transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            hasHeading = true;
        }
    }

    void Update()
    {
        if (car == null) return;

        float dt = Time.deltaTime;
        transform.position = car.position;

        Vector3 dir;
        if (follower != null && follower.Heading.sqrMagnitude > 0.01f)
        {
            // direção exata da estrada, sem ruído
            dir = follower.Heading;
        }
        else
        {
            // sem follower: deduz pelo deslocamento
            Vector3 move = car.position - lastPos;
            move.y = 0f;
            lastPos = car.position;
            if (dt <= 0f || move.magnitude / dt < minMoveSpeed) return;
            dir = move.normalized;
        }

        if (dt <= 0f) return;

        Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = (turnSmoothing <= 0f || !hasHeading)
            ? want
            : Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSmoothing * dt));
        hasHeading = true;
    }
}