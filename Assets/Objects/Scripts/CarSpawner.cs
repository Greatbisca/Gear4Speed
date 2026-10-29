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

    void Start()
    {
        GameObject prefab = chosenCarPrefab;

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

        // Escala e posição base primeiro, depois medir
        CurrentCar.transform.localScale = prefab.transform.localScale * carScale;
        CurrentCar.transform.localPosition = Vector3.zero;

        // Assenta o ponto mais baixo do carro na posição do player/IA
        if (TryGetBounds(CurrentCar, out Bounds b))
        {
            float delta = transform.position.y + groundOffset - b.min.y;
            CurrentCar.transform.localPosition =
                transform.InverseTransformVector(Vector3.up * delta);
        }
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