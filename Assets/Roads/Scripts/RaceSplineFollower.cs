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

    [Header("Curvas: quantas mudanças reduzir")]
    public int cornerGearDrop = 1;
    public int chicaneGearDrop = 1;
    public int uTurnGearDrop = 2;
    [Tooltip("Efeito do handling no nº de mudanças: handling 0 soma metade disto, handling 1 subtrai metade. 2 = ±1 mudança.")]
    public float handlingGearSwing = 2f;
    public int maxGearDrop = 2;
    [Range(0f, 1f)] public float defaultHandling = 0.5f;

    [Header("Travagem")]
    [Tooltip("Tempo (s) que o jogador deve ter para trocar de mudança dentro da janela de redução.")]
    public float shiftTimeBudget = 1.5f;
    public float minBrakeDecel = 2.5f;
    public float maxBrakeDecel = 8f;
    [Range(0.5f, 1f)]
    [Tooltip("<1: o limite desce um pouco mais cedo do que o estritamente necessário.")]
    public float planningMargin = 0.9f;
    [Tooltip("Até onde olha à procura de curvas (m). Tem de cobrir a distância de travagem.")]
    public float maxLookAhead = 300f;
    public float minCornerSpeedMs = 3f;

    [Header("Sem Gearbox (IA)")]
    public float fallbackMaxSpeedKmh = 120f;
    public float fallbackBrakeDecel = 5f;

    public RaceSplineSegment CurrentSegment { get; private set; }
    public float CurrentSpeedLimitMs { get; private set; } = float.PositiveInfinity;
    public float CurrentCornerBrakeDeceleration { get; private set; }

    // Para UI/avisos
    public RaceSplineSegment UpcomingCorner { get; private set; }
    public float DistanceToUpcomingCorner { get; private set; } = float.PositiveInfinity;
    public int UpcomingRequiredGear { get; private set; }     // mudança máxima permitida na próxima curva (0 = n/a)
    public float UpcomingTargetSpeedMs { get; private set; }

    // Direção de movimento no plano horizontal (com -90 em X o eulerAngles não dá o yaw)
    public Vector3 Heading { get; private set; } = Vector3.forward;
    public float Distance => distance;
    public float TotalDistance { get; private set; }

    float distance;
    ISpeedSource speedSource;
    Gearbox gearbox;
    CarSpawner spawner;
    Transform target;
    Quaternion currentYaw = Quaternion.identity;
    bool initialized;
    bool warnedWindow;

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

        spawner = GetComponentInParent<CarSpawner>();

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
        UpdateCornerLimit();
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
        UpdateCornerLimit();
    }

    // ---------- Gearbox / stats ----------

    // Lazy: o PlayerController pode só preencher o gearbox no seu Awake
    Gearbox Gb
    {
        get
        {
            if (gearbox == null)
                gearbox = player != null ? player.gearbox : GetComponentInParent<Gearbox>();
            return gearbox;
        }
    }

    float BogFraction => player != null ? player.engineBogSpeedFraction : 0.2f;

    float GetHandling()
    {
        CarStats stats = spawner != null ? spawner.CurrentStats : null;
        return stats != null ? Mathf.Clamp01(stats.handling) : defaultHandling;
    }

    // Velocidade abaixo da qual a mudança g (1-based) pede redução
    float PromptSpeed(Gearbox gb, int gear) =>
        gb.gears[gear - 1].maxSpeed * gb.downshiftPromptRpm / gb.limiterRpm;

    // Velocidade abaixo da qual a mudança g engasga
    float BogSpeed(Gearbox gb, int gear) =>
        gb.gears[gear - 1].maxSpeed * BogFraction;

    // ---------- Plano de uma curva ----------

    int GetGearDrop(RaceSplineSegment seg, float handling)
    {
        if (!seg.IsCorner) return 0;
        if (seg.gearDropOverride >= 0)
            return Mathf.Min(seg.gearDropOverride, maxGearDrop);

        int baseDrop;
        switch (seg.type)
        {
            case RaceSplineSegment.SegmentType.Corner:  baseDrop = cornerGearDrop; break;
            case RaceSplineSegment.SegmentType.Chicane: baseDrop = chicaneGearDrop; break;
            case RaceSplineSegment.SegmentType.UTurn:   baseDrop = uTurnGearDrop; break;
            default: baseDrop = 0; break;
        }

        float d = baseDrop + (0.5f - handling) * handlingGearSwing;
        return Mathf.Clamp(Mathf.FloorToInt(d + 0.5f), 0, maxGearDrop);
    }

    // Velocidade alvo, travagem e mudança final para uma curva que exige 'drop' reduções
    bool PlanForDrop(int drop, float handling, float speedMultiplier,
                     out float targetSpeed, out float decel, out int forcedGear)
    {
        targetSpeed = float.PositiveInfinity;
        decel = minBrakeDecel;
        forcedGear = 0;
        if (drop <= 0) return false;

        Gearbox gb = Gb;
        if (gb == null || gb.GearCount < 2)
        {
            // Sem caixa de velocidades (IA): velocidade proporcional ao handling
            float maxMs = fallbackMaxSpeedKmh / 3.6f;
            targetSpeed = Mathf.Max(minCornerSpeedMs,
                maxMs * Mathf.Lerp(0.2f, 0.45f, handling) * speedMultiplier);
            decel = fallbackBrakeDecel;
            return true;
        }

        int top = gb.GearCount;
        int f = Mathf.Clamp(top - drop, 1, top - 1);   // mudança máxima permitida na curva
        forcedGear = f;

        // Janela: confortável na mudança f (lower) mas já a engasgar na f+1 (upper)
        float lower = f > 1 ? PromptSpeed(gb, f) : gb.gears[0].maxSpeed * 0.2f;
        float upper = BogSpeed(gb, f + 1) * 0.95f;

        if (upper <= lower && !warnedWindow)
        {
            warnedWindow = true;
            float needed = lower * 1.2f / gb.gears[f].maxSpeed;
            float promptFraction = gb.downshiftPromptRpm / gb.limiterRpm;
            Debug.LogWarning(
                $"{name}: não há janela entre a mudança {f} e a {f + 1} (a {f + 1}ª engasga a " +
                $"{BogSpeed(gb, f + 1):F1} m/s, a {f}ª só está confortável a partir de {lower:F1} m/s). " +
                $"Põe engineBogSpeedFraction entre {needed:F2} e {promptFraction:F2} no PlayerController, " +
                $"ou afasta mais as velocidades máximas das mudanças.", this);
        }

        float pos = Mathf.Lerp(0.3f, 0.9f, handling);   // mais handling = mais perto do limite de cima
        targetSpeed = upper > lower ? Mathf.Lerp(lower, upper, pos) : upper;
        targetSpeed = Mathf.Max(minCornerSpeedMs, targetSpeed * speedMultiplier);

        // Travagem: a largura da janela de redução da mudança f dividida pelo tempo para trocar
        float windowRpm = Mathf.Max(100f, gb.downZoneMaxRpm - gb.downZoneMinRpm);
        float windowSpeed = gb.gears[f - 1].maxSpeed * windowRpm / gb.limiterRpm;
        decel = Mathf.Clamp(windowSpeed / Mathf.Max(0.1f, shiftTimeBudget), minBrakeDecel, maxBrakeDecel);
        return true;
    }

    bool PlanForSegment(RaceSplineSegment seg, float handling,
                        out float targetSpeed, out float decel, out int forcedGear)
    {
        return PlanForDrop(GetGearDrop(seg, handling), handling, seg.speedMultiplier,
                           out targetSpeed, out decel, out forcedGear);
    }

    // ---------- Limite de velocidade nas curvas ----------

    void UpdateCornerLimit()
    {
        float handling = GetHandling();

        float limit = float.PositiveInfinity;
        float bindingDecel = minBrakeDecel;

        UpcomingCorner = null;
        DistanceToUpcomingCorner = float.PositiveInfinity;
        UpcomingRequiredGear = 0;
        UpcomingTargetSpeedMs = 0f;

        // 1) Dentro de uma curva: o limite é a velocidade alvo
        if (CurrentSegment != null && CurrentSegment.IsCorner &&
            PlanForSegment(CurrentSegment, handling, out float curTarget, out float curDecel, out _))
        {
            limit = curTarget;
            bindingDecel = curDecel;
        }

        // 2) Curvas à frente: o limite desce de forma constante até lá chegar já à velocidade alvo
        if (path.segments != null)
        {
            foreach (RaceSplineSegment seg in path.segments)
            {
                if (seg == null || seg == CurrentSegment || !seg.IsCorner) continue;

                float d = path.GetDistanceToSegmentStart(distance, seg);
                if (d > maxLookAhead) continue;

                if (!PlanForSegment(seg, handling, out float t, out float decel, out int forced))
                    continue;

                float brakeDistance = Mathf.Max(0f, d - seg.brakingBuffer);
                float allowed = Mathf.Sqrt(t * t + 2f * decel * planningMargin * brakeDistance);

                if (allowed < limit)
                {
                    limit = allowed;
                    bindingDecel = decel;
                }

                if (d < DistanceToUpcomingCorner)
                {
                    DistanceToUpcomingCorner = d;
                    UpcomingCorner = seg;
                    UpcomingRequiredGear = forced;
                    UpcomingTargetSpeedMs = t;
                }
            }
        }

        CurrentSpeedLimitMs = limit;
        CurrentCornerBrakeDeceleration = bindingDecel;
    }

    // ---------- Diagnóstico ----------

    [ContextMenu("Print Gear Table")]
    void PrintGearTable()
    {
        PlayerController pc = player != null ? player : GetComponentInParent<PlayerController>();
        Gearbox gb = pc != null ? pc.gearbox : GetComponentInParent<Gearbox>();
        if (gb == null) { Debug.Log("Sem Gearbox."); return; }

        player = pc;   // para o BogFraction
        var sb = new System.Text.StringBuilder("Tabela de mudanças (m/s)\n");
        for (int g = 1; g <= gb.GearCount; g++)
            sb.AppendLine($"  {g}ª: máx {gb.gears[g - 1].maxSpeed:F1} | pede redução < {PromptSpeed(gb, g):F1} | engasga < {BogSpeed(gb, g):F1}");

        float h = Application.isPlaying ? GetHandling() : defaultHandling;
        sb.AppendLine($"Curvas com handling {h:F2}:");
        for (int drop = 1; drop <= maxGearDrop; drop++)
        {
            if (PlanForDrop(drop, h, 1f, out float t, out float decel, out int f))
            {
                float topMs = gb.gears[gb.GearCount - 1].maxSpeed;
                float dist = Mathf.Max(0f, (topMs * topMs - t * t) / (2f * decel * planningMargin));
                sb.AppendLine($"  reduzir {drop}: acaba na {f}ª | alvo {t:F1} m/s ({t * 3.6f:F0} km/h) | travagem {decel:F1} m/s² | começa a ~{dist:F0} m da curva (a velocidade máxima)");
            }
        }
        Debug.Log(sb.ToString(), this);
    }

    // ---------- Colocação no circuito ----------

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