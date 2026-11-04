using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [Header("Carro")]
    [Tooltip("Carro a instanciar. Se vazio e houver pool, escolhe um aleatório.")]
    public GameObject chosenCarPrefab;
    [Tooltip("Opcional (útil para a IA): carros possíveis quando não há carro escolhido.")]
    public GameObject[] randomPool;

    [Header("Ajustes")]
    [Tooltip("Escala uniforme aplicada ao carro.")]
    public float carScale = 0.5f;
    public Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);
    [Tooltip("Distância extra entre o ponto mais baixo do carro e a estrada (0 = rodas coladas).")]
    public float groundOffset = 0f;

    public GameObject CurrentCar { get; private set; }
    public CarStats CurrentStats { get; private set; }   // null = a usar defaults

    void Start()
    {
        GameObject prefab = GameSelection.SelectedPrefab != null
            ? GameSelection.SelectedPrefab
            : chosenCarPrefab;

        if (prefab == null && randomPool != null && randomPool.Length > 0)
            prefab = randomPool[Random.Range(0, randomPool.Length)];

        if (prefab != null)
            SpawnCar(prefab);
    }

    // Chama isto a partir do menu de escolha quando o jogador escolher
    public void SpawnCar(GameObject prefab)
    {
        if (CurrentCar != null)
            Destroy(CurrentCar);

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
        if (GameSelection.SelectedCar != null)
        {
            if (CurrentStats == null)
                CurrentStats = CurrentCar.AddComponent<CarStats>();

            var entry = GameSelection.SelectedCar;
            CurrentStats.maxSpeed = entry.topSpeed;
            CurrentStats.acceleration = entry.acceleration;
            CurrentStats.handling = entry.handling;
            CurrentStats.powerUp = entry.powerUp;
            CurrentStats.powerUpChargeRequired = entry.powerUpChargeRequired;
            CurrentStats.engineSound = entry.engineSound;
            CurrentStats.maxRpm = entry.maxRpm;
            CurrentStats.rpmIncrease = entry.rpmIncrease;
            CurrentStats.rpmDecrease = entry.rpmDecrease;
            CurrentStats.gears = CloneGears(entry.gears, entry.topSpeed / 3.6f);

            foreach (var engine in CurrentCar.GetComponentsInChildren<EngineSound>(true))
                engine.Configure(CurrentStats);
        }

        // Só o player tem Gearbox
        var player = GetComponent<PlayerController>()
            ?? GetComponentInParent<PlayerController>()
            ?? GetComponentInChildren<PlayerController>();
        if (player == null || player.gearbox == null) return;

        if (CurrentStats != null)
        {
            player.gearbox.Configure(CurrentStats.gears);
            player.gearbox.limiterRpm = CurrentStats.maxRpm;
            player.gearbox.fallRate = CurrentStats.rpmDecrease;
        }
        else
            player.gearbox.ResetToDefault();   // carro sem stats: usa os defaults
    }

    static Gearbox.GearSetting[] CloneGears(Gearbox.GearSetting[] source, float targetTopSpeed)
    {
        if (source == null || source.Length == 0) return null;

        var copy = new Gearbox.GearSetting[source.Length];
        float currentTopSpeed = source[source.Length - 1].maxSpeed;
        float speedScale = currentTopSpeed > 0f ? targetTopSpeed / currentTopSpeed : 1f;
        for (int i = 0; i < source.Length; i++)
        {
            copy[i] = new Gearbox.GearSetting
            {
                name = source[i].name,
                maxSpeed = source[i].maxSpeed * speedScale,
                acceleration = source[i].acceleration,
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