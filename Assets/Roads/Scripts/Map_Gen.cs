using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct RoadTileAttributes
{
    [Min(0f)]
    public float maxSpeedMultiplier;
    [Min(0f)]
    [Tooltip("Torque minimo necessario nesta tile, em Nm.")]
    public float minimumTorque;
    [Min(0f)]
    public float accelerationMultiplier;
    [Range(0f, 1f)]
    public float curveDifficulty;
}

public enum RoadTileType
{
    Straight,
    Up,
    Down,
    Right,
    Left
}

public readonly struct RoadTilePath
{
    public readonly int index;
    public readonly RoadTileType type;
    public readonly Vector3 start;
    public readonly Vector3 end;
    public readonly Vector3 entryDirection;
    public readonly Vector3 exitDirection;

    public RoadTilePath(
        int index,
        RoadTileType type,
        Vector3 start,
        Vector3 end,
        Vector3 entryDirection,
        Vector3 exitDirection)
    {
        this.index = index;
        this.type = type;
        this.start = start;
        this.end = end;
        this.entryDirection = entryDirection;
        this.exitDirection = exitDirection;
    }
}

public class Map_Gen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;
    [SerializeField] private Transform tilesParent;

    [Header("Road prefabs")]
    [SerializeField] private GameObject upTile;
    [SerializeField] private GameObject downTile;
    [SerializeField] private GameObject rightTile;
    [SerializeField] private GameObject leftTile;
    [SerializeField] private GameObject straightTile;

    [Header("Tile attributes")]
    [SerializeField] private RoadTileAttributes upAttributes = new RoadTileAttributes
    {
        maxSpeedMultiplier = 0.7f,
        minimumTorque = 1000f,
        accelerationMultiplier = 0.7f
    };
    [SerializeField] private RoadTileAttributes downAttributes = new RoadTileAttributes
    {
        maxSpeedMultiplier = 1.2f,
        minimumTorque = 0f,
        accelerationMultiplier = 1.15f
    };
    [SerializeField] private RoadTileAttributes rightAttributes = new RoadTileAttributes
    {
        maxSpeedMultiplier = 1f,
        minimumTorque = 0f,
        accelerationMultiplier = 1f,
        curveDifficulty = 0.5f
    };
    [SerializeField] private RoadTileAttributes leftAttributes = new RoadTileAttributes
    {
        maxSpeedMultiplier = 1f,
        minimumTorque = 0f,
        accelerationMultiplier = 1f,
        curveDifficulty = 0.5f
    };
    [SerializeField] private RoadTileAttributes straightAttributes = new RoadTileAttributes
    {
        maxSpeedMultiplier = 1f,
        minimumTorque = 0f,
        accelerationMultiplier = 1f
    };

    [Header("Generation")]
    [Min(1)]
    [SerializeField] private int tilesAhead = 12;
    [Min(0)]
    [SerializeField] private int tilesBehind = 2;
    [Min(0)]
    [Tooltip("Numero total maximo de tiles geradas. 0 = sem limite.")]
    [SerializeField] private int maxTiles;
    [Min(0.1f)]
    [SerializeField] private float tileLength = 10f;
    [SerializeField] private float verticalOffset = 0.577f;
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private Vector3 startDirection = Vector3.forward;
    [SerializeField] private bool randomizePath = true;
    [SerializeField] private int seed;

    private readonly Queue<GeneratedTile> generatedTiles = new Queue<GeneratedTile>();
    private Vector3 nextPosition;
    private float currentHeight;
    private Vector3 direction;
    private int nextTileIndex;
    private int lastCarTileIndex = -1;
    private System.Random random;
    private GameObject lastTurnTile;
    private GameObject lastVerticalTile;

    private static readonly Vector3[] Directions =
    {
        Vector3.forward,
        Vector3.right,
        Vector3.back,
        Vector3.left
    };

    private void Awake()
    {
        if (car == null)
        {
            GameObject carObject = GameObject.FindGameObjectWithTag("Player");
            if (carObject != null)
                car = carObject.transform;
            else
            {
                CarController carController =
                    FindFirstObjectByType<CarController>();
                if (carController != null)
                    car = carController.transform;
            }
        }

        direction = CardinalDirection(startDirection);
        nextPosition = startPosition;
        currentHeight = 0f;
        nextPosition.y = 0f;
        random = seed == 0 ? new System.Random() : new System.Random(seed);

        while (nextTileIndex < tilesAhead &&
               CanGenerateMoreTiles())
            SpawnNextTile();
    }

    private void OnValidate()
    {
        tileLength = Mathf.Max(0.1f, tileLength);
        tilesAhead = Mathf.Max(1, tilesAhead);
        tilesBehind = Mathf.Max(0, tilesBehind);
        maxTiles = Mathf.Max(0, maxTiles);
    }

    private bool CanGenerateMoreTiles()
    {
        return maxTiles == 0 || nextTileIndex < maxTiles;
    }

    public void UpdateStreaming(int carTileIndex)
    {
        lastCarTileIndex = Mathf.Max(lastCarTileIndex, carTileIndex);

        RemoveTilesBehind(carTileIndex);

        while (nextTileIndex <= carTileIndex + tilesAhead &&
               CanGenerateMoreTiles())
        {
            SpawnNextTile();
        }
    }

    private void RemoveTilesBehind(int carTileIndex)
    {
        int tilesToKeepBehind = Mathf.Max(tilesBehind, 1);

        while (generatedTiles.Count > 0 &&
               generatedTiles.Peek().index <
               carTileIndex - tilesToKeepBehind)
        {
            GeneratedTile oldTile = generatedTiles.Dequeue();
            if (oldTile.instance != null)
                Destroy(oldTile.instance);
        }
    }

    private void SpawnNextTile()
    {
        Vector3 entryDirection = direction;
        GameObject prefab = ChooseTile(out Vector3 nextDirection);
        if (prefab == null)
        {
            Debug.LogError("Map_Gen precisa de pelo menos um prefab de estrada atribuído.", this);
            enabled = false;
            return;
        }

        float tileHeightOffset = GetHeightChange(prefab);
        Vector3 spawnPosition = new Vector3(
            nextPosition.x,
            currentHeight + tileHeightOffset * 0.5f,
            nextPosition.z
        );

        GameObject instance = Instantiate(
            prefab,
            spawnPosition,
            GetTileRotation(prefab, nextDirection),
            tilesParent
        );
        generatedTiles.Enqueue(new GeneratedTile(
            nextTileIndex,
            spawnPosition,
            instance,
            GetTileAttributes(prefab),
            GetTileType(prefab),
            nextDirection,
            entryDirection
        ));

        direction = nextDirection;
        nextPosition = spawnPosition + direction * tileLength;
        currentHeight += tileHeightOffset;
        nextPosition.y = 0f;

        nextTileIndex++;
    }

    private float GetHeightChange(GameObject prefab)
    {
        float scaledVerticalOffset = verticalOffset * tileLength;

        if (prefab == upTile)
            return scaledVerticalOffset;
        if (prefab == downTile)
            return -scaledVerticalOffset;

        return 0f;
    }

    private Quaternion GetTileRotation(
        GameObject prefab,
        Vector3 tileDirection)
    {
        Quaternion directionRotation =
            Quaternion.LookRotation(tileDirection, Vector3.up);
        Quaternion tileRotation = prefab.transform.rotation;

        return directionRotation * tileRotation;
    }

    private GameObject ChooseTile(out Vector3 tileDirection)
    {
        tileDirection = direction;

        if (nextTileIndex == 0)
            return straightTile;

        if (!randomizePath)
            return straightTile != null ? straightTile : FirstAvailableTile();

        List<GameObject> candidates = new List<GameObject>();
        AddCandidate(candidates, straightTile);

        if (lastVerticalTile != downTile)
            AddCandidate(candidates, upTile);
        if (lastVerticalTile != upTile)
            AddCandidate(candidates, downTile);

        if (rightTile != null && lastTurnTile != rightTile)
            candidates.Add(rightTile);
        if (leftTile != null && lastTurnTile != leftTile)
            candidates.Add(leftTile);

        if (candidates.Count == 0)
            return null;

        GameObject selectedTile = candidates[random.Next(candidates.Count)];

        if (selectedTile == rightTile)
        {
            tileDirection = TurnRight(direction);
            lastTurnTile = rightTile;
        }
        else if (selectedTile == leftTile)
        {
            tileDirection = TurnLeft(direction);
            lastTurnTile = leftTile;
        }
        else
        {
            tileDirection = direction;
            lastTurnTile = null;
        }

        if (selectedTile == upTile || selectedTile == downTile)
            lastVerticalTile = selectedTile;
        else
            lastVerticalTile = null;

        return selectedTile;
    }

    private static void AddCandidate(
        List<GameObject> candidates,
        GameObject tile)
    {
        if (tile != null)
            candidates.Add(tile);
    }

    private GameObject FirstAvailableTile()
    {
        if (upTile != null) return upTile;
        if (downTile != null) return downTile;
        if (rightTile != null) return rightTile;
        if (leftTile != null) return leftTile;
        return straightTile;
    }

    private static Vector3 TurnRight(Vector3 value)
    {
        return new Vector3(value.z, 0f, -value.x);
    }

    private static Vector3 TurnLeft(Vector3 value)
    {
        return new Vector3(-value.z, 0f, value.x);
    }

    public bool TryGetNextTileTarget(Vector3 position, out Vector3 target)
    {
        target = default;

        if (generatedTiles.Count == 0)
            return false;

        int currentIndex = GetClosestTileIndex(position);
        int targetIndex = currentIndex + 1;

        foreach (GeneratedTile tile in generatedTiles)
        {
            if (tile.index == targetIndex)
            {
                target = tile.position;
                return true;
            }
        }

        return false;
    }

    public bool TryGetCurrentTilePath(
        Vector3 position,
        out RoadTilePath path)
    {
        int currentIndex = GetClosestTileIndex(position);
        return TryGetTilePath(currentIndex, out path);
    }

    public bool TryGetTilePath(int index, out RoadTilePath path)
    {
        path = default;

        if (generatedTiles.Count == 0)
            return false;

        GeneratedTile current = default;
        GeneratedTile next = default;
        bool foundCurrent = false;
        bool foundNext = false;

        foreach (GeneratedTile tile in generatedTiles)
        {
            if (tile.index == index)
            {
                current = tile;
                foundCurrent = true;
            }
            else if (tile.index == index + 1)
            {
                next = tile;
                foundNext = true;
            }
        }

        if (!foundCurrent || !foundNext)
            return false;

        path = new RoadTilePath(
            current.index,
            current.type,
            GetTileStart(current),
            GetTileEnd(current),
            current.entryDirection,
            current.direction
        );
        return true;
    }

    public bool TryGetCurrentTileAttributes(
        Vector3 position,
        out RoadTileAttributes attributes)
    {
        attributes = default;

        if (generatedTiles.Count == 0)
            return false;

        int currentIndex = GetClosestTileIndex(position);
        foreach (GeneratedTile tile in generatedTiles)
        {
            if (tile.index == currentIndex)
            {
                attributes = tile.attributes;
                return true;
            }
        }

        return false;
    }

    public bool TryGetCurrentSpeedMultiplier(
        Vector3 position,
        float handling,
        out float speedMultiplier)
    {
        speedMultiplier = 1f;

        if (!TryGetCurrentTileAttributes(position, out RoadTileAttributes attributes))
            return false;

        speedMultiplier = attributes.maxSpeedMultiplier;

        if (attributes.curveDifficulty > 0f)
        {
            float handlingRatio = Mathf.Clamp01(
                handling / attributes.curveDifficulty
            );
            float curveMultiplier = Mathf.Lerp(0.5f, 1f, handlingRatio);
            speedMultiplier *= curveMultiplier;
        }

        return true;
    }

    private RoadTileAttributes GetTileAttributes(GameObject prefab)
    {
        if (prefab == upTile)
            return upAttributes;
        if (prefab == downTile)
            return downAttributes;
        if (prefab == rightTile)
            return rightAttributes;
        if (prefab == leftTile)
            return leftAttributes;

        return straightAttributes;
    }

    private Vector3 GetTileStart(GeneratedTile tile)
    {
        Vector3 start = tile.position -
            tile.entryDirection * (tileLength * 0.5f);

        if (tile.type == RoadTileType.Up)
            start.y -= GetScaledVerticalOffset() * 0.5f;
        else if (tile.type == RoadTileType.Down)
            start.y += GetScaledVerticalOffset() * 0.5f;

        return start;
    }

    private Vector3 GetTileEnd(GeneratedTile tile)
    {
        Vector3 end = tile.position +
            tile.direction * (tileLength * 0.5f);

        if (tile.type == RoadTileType.Up)
            end.y += GetScaledVerticalOffset() * 0.5f;
        else if (tile.type == RoadTileType.Down)
            end.y -= GetScaledVerticalOffset() * 0.5f;

        return end;
    }

    private float GetScaledVerticalOffset()
    {
        return verticalOffset * tileLength;
    }

    private RoadTileType GetTileType(GameObject prefab)
    {
        if (prefab == upTile)
            return RoadTileType.Up;
        if (prefab == downTile)
            return RoadTileType.Down;
        if (prefab == rightTile)
            return RoadTileType.Right;
        if (prefab == leftTile)
            return RoadTileType.Left;

        return RoadTileType.Straight;
    }

    private int GetClosestTileIndex(Vector3 position)
    {
        int closestIndex = generatedTiles.Peek().index;
        float closestDistance = float.PositiveInfinity;

        foreach (GeneratedTile tile in generatedTiles)
        {
            Vector3 tilePosition = tile.position;
            tilePosition.y = position.y;
            float distance = (tilePosition - position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = tile.index;
            }
        }

        return closestIndex;
    }

    private static Vector3 CardinalDirection(Vector3 value)
    {
        if (value.sqrMagnitude < 0.01f)
            return Vector3.forward;

        value.y = 0f;
        return Mathf.Abs(value.x) > Mathf.Abs(value.z)
            ? (value.x >= 0f ? Vector3.right : Vector3.left)
            : (value.z >= 0f ? Vector3.forward : Vector3.back);
    }

    private static int DirectionIndex(Vector3 value)
    {
        for (int i = 0; i < Directions.Length; i++)
        {
            if (Directions[i] == value)
                return i;
        }

        return 0;
    }

    private readonly struct GeneratedTile
    {
        public readonly int index;
        public readonly Vector3 position;
        public readonly GameObject instance;
        public readonly RoadTileAttributes attributes;
        public readonly RoadTileType type;
        public readonly Vector3 direction;
        public readonly Vector3 entryDirection;

        public GeneratedTile(
            int index,
            Vector3 position,
            GameObject instance,
            RoadTileAttributes attributes,
            RoadTileType type,
            Vector3 direction,
            Vector3 entryDirection)
        {
            this.index = index;
            this.position = position;
            this.instance = instance;
            this.attributes = attributes;
            this.type = type;
            this.direction = direction;
            this.entryDirection = entryDirection;
        }
    }
}
