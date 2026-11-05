using UnityEngine;

[DefaultExecutionOrder(100)]
public class RaceSplineFollower : MonoBehaviour
{
    [Header("References")]
    public RaceSplinePath path;
    public PlayerController player;      // vazio = procura nos pais; senão usa ISpeedSource (IA)
    public LapCounter lapCounter;        // opcional

    [Header("Placement")]
    [Tooltip("Faixa: positivo = direita, negativo = esquerda (m).")]
    public float lateralOffset;
    public float heightOffset = 0.5f;
    public Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("Direção")]
    [Tooltip("Distância (m) à frente e atrás usada para medir a direção da curva.")]
    public float lookDistance = 1f;
    [Tooltip("Suavização da rotação. 0 = sem suavização.")]
    public float rotationSmoothing = 20f;

    public RaceSplineSegment CurrentSegment { get; private set; }
    public float CurrentSpeedLimitMs { get; private set; } = float.PositiveInfinity;
    public float CurrentCornerBrakeDeceleration { get; private set; }

    // Direção de movimento no plano horizontal (use isto em vez de eulerAngles,
    // que com -90 em X não dá o yaw)
    public Vector3 Heading { get; private set; } = Vector3.forward;
    public float Distance => distance;
    public float TotalDistance { get; private set; }

    float distance;
    ISpeedSource speedSource;
    Transform target;
    Quaternion currentYaw = Quaternion.identity;
    bool initialized;

    void Awake()
    {
        if (path == null)
            path = FindFirstObjectByType<RaceSplinePath>();

        if (player == null)
            player = GetComponentInParent<PlayerController>();

        speedSource = player != null
            ? (ISpeedSource)player
            : GetComponentInParent<ISpeedSource>();

        var comp = speedSource as Component;
        target = comp != null ? comp.transform : transform;

        if (lapCounter == null)
            lapCounter = GetComponentInParent<LapCounter>();
    }

    void Start()
    {
        if (path == null)
        {
            Debug.LogError($"{name}: RaceSplinePath is missing.", this);
            enabled = false;
            return;
        }

        if (path.TotalLength <= 0f)
        {
            Debug.LogError($"{name}: RaceSplinePath has no length.", this);
            enabled = false;
            return;
        }

        InitializeFromCurrentPosition();
    }

    public void InitializeFromCurrentPosition()
    {
        if (path == null || target == null || path.TotalLength <= 0f)
            return;

        distance = path.GetClosestDistance(target.position);
        TotalDistance = distance;
        initialized = true;
        ApplySplineTransform(0f, snap: true);
    }

    void LateUpdate()
    {
        if (!initialized || target == null || path.TotalLength <= 0f)
            return;

        float speed = speedSource != null ? Mathf.Max(0f, speedSource.CurrentSpeed) : 0f;
        float step = speed * Time.deltaTime;

        float previous = distance;
        distance = Mathf.Repeat(distance + step, path.TotalLength);
        TotalDistance += step;

        lapCounter?.NotifyDistanceAdvanced(previous, distance, path.TotalLength);

        ApplySplineTransform(Time.deltaTime, snap: false);
    }

    void ApplySplineTransform(float dt, bool snap)
    {
        float len = path.TotalLength;

        // Centro da pista (sem desvio lateral)
        if (!path.Evaluate(distance, 0f, out Vector3 center, out Vector3 tangent,
                           out RaceSplineSegment segment))
            return;
        CurrentSegment = segment;

        // Direção real do movimento: ponto atrás -> ponto à frente (atravessa as juntas)
        bool okBack = path.Evaluate(Mathf.Repeat(distance - lookDistance, len), 0f,
                                    out Vector3 back, out _, out _);
        bool okFront = path.Evaluate(Mathf.Repeat(distance + lookDistance, len), 0f,
                                     out Vector3 front, out _, out _);

        Vector3 dir = (okBack && okFront) ? front - back : tangent;
        Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
        if (flat.sqrMagnitude < 1e-6f)
            flat = Vector3.ProjectOnPlane(tangent, Vector3.up);
        if (flat.sqrMagnitude < 1e-6f)
            return;
        flat.Normalize();
        Heading = flat;

        // Faixa: perpendicular à direção real
        Vector3 right = Vector3.Cross(Vector3.up, flat);
        Vector3 position = center + right * lateralOffset + Vector3.up * heightOffset;

        // Rotação (yaw suavizado) + offset do modelo
        Quaternion desired = Quaternion.LookRotation(flat, Vector3.up);
        if (snap || rotationSmoothing <= 0f)
            currentYaw = desired;
        else
            currentYaw = Quaternion.Slerp(currentYaw, desired,
                                          1f - Mathf.Exp(-rotationSmoothing * dt));

        target.SetPositionAndRotation(
            position,
            currentYaw * Quaternion.Euler(rotationOffset));
    }
}