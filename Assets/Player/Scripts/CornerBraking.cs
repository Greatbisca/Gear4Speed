using UnityEngine;

public class CornerBraking : MonoBehaviour
{
    [SerializeField] private Map_Gen map;

    [Header("Lookahead")]
    [Min(1)]
    [SerializeField] private int lookAheadTiles = 10;

    [Header("Corner speed (m/s)")]
    [SerializeField] private float cornerSpeed = 10f;
    [Range(0f, 1f)]
    [Tooltip("Link this to PlayerStats.Handling later.")]
    [SerializeField] private float handling = 0.5f;

    [Header("Braking")]
    [Tooltip("Deceleration used to reach the corner speed, in m/s2.")]
    [SerializeField] private float brakeDecel = 6f;

    // Highest speed allowed right now. Infinity = no corner nearby.
    public float SpeedLimit { get; private set; } = float.PositiveInfinity;
    public float DistanceToCorner { get; private set; } = float.PositiveInfinity;
    public float BrakeDecel => brakeDecel;

    private PlayerController player;

    // Same handling formula as Map_Gen: bad handling = slower corners
    float SafeCornerSpeed =>
        cornerSpeed * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(handling / 0.5f));

    static bool IsCorner(RoadTileType t) =>
        t == RoadTileType.Left || t == RoadTileType.Right;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        if (map == null) map = FindFirstObjectByType<Map_Gen>();
    }

    void Update()
    {
        SpeedLimit = float.PositiveInfinity;
        DistanceToCorner = float.PositiveInfinity;

        if (map == null || player == null ||
            !map.TryGetTilePath(player.TileIndex, out RoadTilePath current))
            return;

        float vc = SafeCornerSpeed;
        float distToTileStart = 0f;   // distance along the road to the start of tile i
        RoadTilePath path = current;

        for (int i = 0; i <= lookAheadTiles; i++)
        {
            if (i > 0 && !map.TryGetTilePath(current.index + i, out path))
                break;

            if (IsCorner(path.type))
            {
                float d = (i == 0) ? 0f : distToTileStart;   // already inside the corner
                // fastest speed from which we can still brake to vc over distance d
                float limit = Mathf.Sqrt(vc * vc + 2f * brakeDecel * d);
                SpeedLimit = Mathf.Min(SpeedLimit, limit);
                DistanceToCorner = Mathf.Min(DistanceToCorner, d);
            }

            // distance to the start of the next tile
            distToTileStart = (i == 0)
                ? Vector3.Distance(transform.position, path.end)
                : distToTileStart + Vector3.Distance(path.start, path.end);
        }
    }

    // Quick debug readout, remove once the HUD shows it
    void OnGUI()
    {
        string lim = float.IsInfinity(SpeedLimit) ? "none" : (SpeedLimit * 3.6f).ToString("0") + " km/h";
        GUI.Label(new Rect(10, 10, 400, 24), "Corner limit: " + lim);
    }
}