using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [Header("Carro")]
    [Tooltip("Carro a instanciar. Se vazio e houver pool, escolhe um aleatório.")]
    public GameObject chosenCarPrefab;
    [Tooltip("Opcional (útil para a IA): carros possíveis quando não há carro escolhido.")]
    public GameObject[] randomPool;

    [Header("Catálogo")]
    [Tooltip("No Editor, se ficar vazio, procura o CarCatalog do projeto. Numa build tem de estar atribuído.")]
    public CarCatalog catalog;
    [Tooltip("Índice no catálogo. -1 = aleatório na IA; no jogador procura o carro pelo prefab.")]
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

    [Header("Diagnóstico")]
    [Tooltip("Escreve na consola que carro e que stats foram aplicados, e de onde vieram.")]
    public bool logStats = true;

    public GameObject CurrentCar { get; private set; }
    public CarStats CurrentStats { get; private set; }   // null = a usar defaults
    public CarCatalog.Entry CurrentEntry => currentEntry;

    PlayerController player;
    CarCatalog.Entry currentEntry;
    string currentSource = "?";

    PlayerController FindPlayer()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (player == null) player = GetComponentInChildren<PlayerController>();
        return player;
    }

    bool UseSelection => useMenuSelection && FindPlayer() != null;

    CarCatalog Catalog
    {
        get
        {
#if UNITY_EDITOR
            if (catalog == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CarCatalog");
                if (guids.Length > 0)
                    catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CarCatalog>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            }
#endif
            return catalog;
        }
    }

    // O power-up de TODOS os carros é o boost (turbo do PlayerController).
    // Procura o valor do enum CarPowerUp pelo nome, para não depender de como está escrito.
    public static bool TryGetBoost(out CarPowerUp boost)
    {
        boost = default;
        string[] names = { "Boost", "Turbo", "SpeedBoost", "Nitro" };
        foreach (string n in names)
            if (System.Enum.TryParse(n, true, out boost))
                return true;
        boost = default;
        return false;
    }

    void Start()
    {
        GameObject prefab = chosenCarPrefab;
        CarCatalog.Entry entry;
        string source = "prefab do Inspector";

        bool fromMenu = UseSelection &&
            (GameSelection.SelectedPrefab != null || GameSelection.SelectedCar != null);

        if (fromMenu)
        {
            entry = GameSelection.SelectedCar;
            prefab = GameSelection.SelectedPrefab != null
                ? GameSelection.SelectedPrefab
                : entry != null ? entry.prefab : chosenCarPrefab;

            // O menu pode ter guardado só o prefab: procura a entrada pelo prefab
            if (entry == null) entry = FindEntry(prefab);
            source = "menu";
        }
        else
        {
            entry = PickFromCatalog();
            if (entry != null && entry.prefab != null)
            {
                prefab = entry.prefab;
                source = "catálogo";
            }
            else if (prefab != null)
            {
                // Cena de corrida iniciada diretamente: usa os stats do catálogo para este prefab
                entry = FindEntry(prefab);
                if (entry != null) source = "catálogo (pelo prefab)";
            }
        }

        if (prefab == null && randomPool != null && randomPool.Length > 0)
        {
            prefab = randomPool[Random.Range(0, randomPool.Length)];
            entry = FindEntry(prefab);
            source = "pool aleatório";
        }

        if (prefab != null)
            Spawn(prefab, entry, source);
        else
            Debug.LogWarning($"{name}: nenhum carro para instanciar.", this);
    }

    CarCatalog.Entry PickFromCatalog()
    {
        CarCatalog c = Catalog;
        if (c == null || c.cars == null || c.cars.Length == 0) return null;

        int len = c.cars.Length;
        if (catalogIndex >= 0) return c.cars[Mathf.Clamp(catalogIndex, 0, len - 1)];

        // Jogador sem índice: usa o prefab atribuído
        if (FindPlayer() != null) return null;

        // IA: aleatório, evitando o carro do jogador
        for (int i = 0; i < 8; i++)
        {
            var e = c.cars[Random.Range(0, len)];
            if (e.prefab != null && e != GameSelection.SelectedCar) return e;
        }
        return c.cars[Random.Range(0, len)];
    }

    CarCatalog.Entry FindEntry(GameObject prefab)
    {
        CarCatalog c = Catalog;
        if (c == null || c.cars == null || prefab == null) return null;

        foreach (var e in c.cars)
            if (e != null && e.prefab == prefab) return e;
        foreach (var e in c.cars)
            if (e != null && e.prefab != null && e.prefab.name == prefab.name) return e;
        return null;
    }

    // Chama isto a partir do menu de escolha quando o jogador escolher
    public void SpawnCar(GameObject prefab)
    {
        CarCatalog.Entry entry = UseSelection ? GameSelection.SelectedCar : null;
        if (entry == null) entry = FindEntry(prefab);
        Spawn(prefab, entry, "SpawnCar");
    }

    public void SpawnCar(CarCatalog.Entry entry)
    {
        if (entry != null && entry.prefab != null) Spawn(entry.prefab, entry, "SpawnCar(entrada)");
    }

    void Spawn(GameObject prefab, CarCatalog.Entry entry, string source)
    {
        if (CurrentCar != null)
            Destroy(CurrentCar);

        currentEntry = entry;
        currentSource = source;

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
            CurrentStats.powerUp = TryGetBoost(out CarPowerUp boost) ? boost : entry.powerUp;
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
        if (p != null && p.gearbox != null)
        {
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

        if (logStats) LogApplied(p != null ? p.gearbox : null);
    }

    void LogApplied(Gearbox gearbox)
    {
        bool isPlayer = gearbox != null;
        string who = isPlayer ? "JOGADOR" : "IA";
        if (CurrentStats == null)
        {
            Debug.Log($"[CarSpawner] {who} ({name}): {CurrentCar.name} | fonte: {currentSource} | " +
                      "SEM STATS (a usar os defaults do Gearbox). Falta CarCatalog ou entrada para este prefab.", this);
            return;
        }

        int gears = CurrentStats.gears != null ? CurrentStats.gears.Length : 0;
        Debug.Log($"[CarSpawner] {who} ({name}): {CurrentCar.name} | fonte: {currentSource} | " +
                  $"{CurrentStats.maxSpeed:F0} km/h | acel {CurrentStats.acceleration:F1} | " +
                  $"handling {CurrentStats.handling:F2} | {gears} mudanças | " +
                  $"tolerância {CurrentStats.shiftTolerance:F2} | power-up {CurrentStats.powerUp}" +
                  (isPlayer ? $" | Gearbox '{gearbox.name}' ficou com {gearbox.GearCount} mudanças" : ""), this);
    }

    // Velocidade de cada mudança ∝ 1/rácio (a última = topo do carro).
    // Aceleração de cada mudança = arranque * (rácio / rácio da 1ª) ^ expoente.
    Gearbox.GearSetting[] BuildGears(
        Gearbox.GearSetting[] source,
        float topSpeedMs,
        float launchAccel)
    {
        if (source == null || source.Length == 0) return null;

        int n = Mathf.Min(source.Length, Gearbox.MaxGears);
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