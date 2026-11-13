using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [Header("Carro")]
    [Tooltip("Carro a instanciar. Se vazio e houver pool, escolhe um aleatório.")]
    public GameObject chosenCarPrefab;
    [Tooltip("Opcional (útil para a IA): carros possíveis quando não há carro escolhido.")]
    public GameObject[] randomPool;

    [Header("Catálogo (IA e testes sem menu)")]
    public CarCatalog catalog;
    [Tooltip("Índice no catálogo. -1 = aleatório na IA (e ignora o catálogo no jogador).")]
    public int catalogIndex = -1;

    [Header("Ajustes")]
    [Tooltip("Escala uniforme aplicada ao carro.")]
    public float carScale = 0.5f;
    public Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);
    [Tooltip("Distância extra entre o ponto mais baixo do carro e a estrada (0 = rodas coladas).")]
    public float groundOffset = 0f;

    [Header("Escolha do menu")]
    [Tooltip("Usa o carro e os stats escolhidos no menu (GameSelection). Só se aplica se este objeto tiver um PlayerController. Desliga na IA.")]
    public bool useMenuSelection = true;

    [Header("Aceleração")]
    [Tooltip("m/s² da 1ª mudança por cada ponto do stat 'acceleration'.")]
    public float launchAccelPerStat = 1.15f;
    [Tooltip("Multiplicador extra só na 1ª mudança (arranque de carro de corrida).")]
    public float firstGearBoost = 1.2f;
    [Range(0.3f, 1.2f)]
    [Tooltip("Como a aceleração cai nas mudanças altas. 1 = proporcional ao rácio da caixa. Menor = as altas aceleram mais.")]
    public float ratioExponent = 0.85f;

    public GameObject CurrentCar { get; private set; }
    public CarStats CurrentStats { get; private set; }   // null = a usar defaults

    PlayerController player;
    CarCatalog.Entry currentEntry;

    PlayerController FindPlayer()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (player == null) player = GetComponentInChildren<PlayerController>();
        return player;
    }

    bool UseSelection => useMenuSelection && FindPlayer() != null;

    void Start()
    {
        GameObject prefab = chosenCarPrefab;
        CarCatalog.Entry entry = null;

        if (UseSelection && GameSelection.SelectedPrefab != null)
        {
            prefab = GameSelection.SelectedPrefab;
            entry = GameSelection.SelectedCar;
        }
        else
        {
            entry = PickFromCatalog();
            if (entry != null && entry.prefab != null) prefab = entry.prefab;
        }

        if (prefab == null && randomPool != null && randomPool.Length > 0)
            prefab = randomPool[Random.Range(0, randomPool.Length)];

        if (prefab != null)
            Spawn(prefab, entry);
    }

    CarCatalog.Entry PickFromCatalog()
    {
        if (catalog == null || catalog.cars == null || catalog.cars.Length == 0) return null;

        int len = catalog.cars.Length;
        if (catalogIndex >= 0) return catalog.cars[Mathf.Clamp(catalogIndex, 0, len - 1)];

        // Jogador sem índice: usa o prefab atribuído
        if (FindPlayer() != null) return null;

        // IA: aleatório, evitando o carro do jogador
        for (int i = 0; i < 8; i++)
        {
            var e = catalog.cars[Random.Range(0, len)];
            if (e.prefab != null && e != GameSelection.SelectedCar) return e;
        }
        return catalog.cars[Random.Range(0, len)];
    }

    // Chama isto a partir do menu de escolha quando o jogador escolher
    public void SpawnCar(GameObject prefab) =>
        Spawn(prefab, UseSelection ? GameSelection.SelectedCar : null);

    public void SpawnCar(CarCatalog.Entry entry)
    {
        if (entry != null && entry.prefab != null) Spawn(entry.prefab, entry);
    }

    void Spawn(GameObject prefab, CarCatalog.Entry entry)
    {
        if (CurrentCar != null)
            Destroy(CurrentCar);

        currentEntry = entry;

        Quaternion rot = transform.rotation * Quaternion.Euler(rotationOffset);
        CurrentCar = Instantiate(prefab, transform.position, rot, transform);
        CurrentCar.name = prefab.name;   // remove o "(Clone)"

        CurrentCar.transform.localScale = prefab.transform.localScale * carScale;
        CurrentCar.transform.localPosition = Vector3.zero;

        if (TryGetBounds(CurrentCar, out Bounds b))
        {
            float delta = transform.position.y + groundOffset - b.min.y;
            CurrentCar.transform.localPosition =
                transform.InverseTransformVector(Vector3.up * delta);
        }

        ApplyStats();
    }

    void ApplyStats()
    {
        CurrentStats = CurrentCar.GetComponent<CarStats>();

        if (currentEntry != null)
        {
            if (CurrentStats == null)
                CurrentStats = CurrentCar.AddComponent<CarStats>();

            var entry = currentEntry;
            CurrentStats.maxSpeed = entry.topSpeed;
            CurrentStats.acceleration = entry.acceleration;
            CurrentStats.handling = entry.handling;
            CurrentStats.shiftTolerance = entry.shiftTolerance;
            CurrentStats.powerUp = entry.powerUp;
            CurrentStats.powerUpChargeRequired = entry.powerUpChargeRequired;
            CurrentStats.engineSound = entry.engineSound;
            CurrentStats.maxRpm = entry.maxRpm;
            CurrentStats.rpmIncrease = entry.rpmIncrease;
            CurrentStats.rpmDecrease = entry.rpmDecrease;
            CurrentStats.gears = BuildGears(
                entry.gears,
                entry.topSpeed / 3.6f,
                entry.acceleration * launchAccelPerStat);

            foreach (var engine in CurrentCar.GetComponentsInChildren<EngineSound>(true))
                engine.Configure(CurrentStats);
        }

        // Só o player tem Gearbox
        PlayerController p = FindPlayer();
        if (p == null || p.gearbox == null) return;

        if (CurrentStats != null)
        {
            p.gearbox.Configure(CurrentStats.gears);
            p.gearbox.SetShiftTolerance(CurrentStats.shiftTolerance);
            if (CurrentStats.maxRpm > 0f) p.gearbox.limiterRpm = CurrentStats.maxRpm;
            if (CurrentStats.rpmDecrease > 0f) p.gearbox.fallRate = CurrentStats.rpmDecrease;
        }
        else
            p.gearbox.ResetToDefault();   // carro sem stats: usa os defaults

        RaceSplineFollower follower = p.GetComponent<RaceSplineFollower>();
        if (follower == null) follower = p.GetComponentInChildren<RaceSplineFollower>();
        if (follower == null) follower = p.GetComponentInParent<RaceSplineFollower>();
        if (follower != null)
            follower.InitializeFromCurrentPosition();
    }

    // Velocidade de cada mudança ∝ 1/rácio (a última = topo do carro).
    // Aceleração de cada mudança = arranque * (rácio / rácio da 1ª) ^ expoente.
    Gearbox.GearSetting[] BuildGears(
        Gearbox.GearSetting[] source,
        float topSpeedMs,
        float launchAccel)
    {
        if (source == null || source.Length == 0) return null;

        int n = source.Length;
        var copy = new Gearbox.GearSetting[n];

        // Os rácios têm de ser positivos e a descer (1ª = maior)
        bool ratiosValid = source[0].gearRatio > 0f;
        for (int i = 1; i < n && ratiosValid; i++)
            if (source[i].gearRatio <= 0f || source[i].gearRatio >= source[i - 1].gearRatio)
                ratiosValid = false;

        if (!ratiosValid)
            Debug.LogWarning($"{name}: gearRatio inválido no catálogo; a usar a forma maxSpeed/acceleration das mudanças.", this);

        float rFirst = ratiosValid ? source[0].gearRatio : 1f;
        float rLast = ratiosValid ? source[n - 1].gearRatio : 1f;
        float oldTop = source[n - 1].maxSpeed;
        float oldFirstAccel = source[0].acceleration;

        for (int i = 0; i < n; i++)
        {
            float maxSpeed, accel;
            if (ratiosValid)
            {
                float r = source[i].gearRatio;
                maxSpeed = topSpeedMs * rLast / r;
                accel = launchAccel * Mathf.Pow(r / rFirst, ratioExponent);
            }
            else
            {
                float speedScale = oldTop > 0f ? topSpeedMs / oldTop : 1f;
                maxSpeed = source[i].maxSpeed * speedScale;
                accel = oldFirstAccel > 0f
                    ? launchAccel * source[i].acceleration / oldFirstAccel
                    : source[i].acceleration;
            }

            if (i == 0) accel *= firstGearBoost;

            copy[i] = new Gearbox.GearSetting
            {
                name = source[i].name,
                maxSpeed = maxSpeed,
                acceleration = accel,
                gearRatio = source[i].gearRatio,
                rpmIncrease = source[i].rpmIncrease,
                rpmDecrease = source[i].rpmDecrease
            };
        }
        return copy;
    }

    static bool TryGetBounds(GameObject car, out Bounds bounds)
    {
        var renderers = car.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }
}