using UnityEngine;

public class Spawn : MonoBehaviour
{
    /*
    Inspector properties
    */
    [SerializeField] int instanceCount = 10;
    [SerializeField] float size = 0.1f;
    [SerializeField] Vector2 spawnArea = new Vector2(10f, 10f);
    [SerializeField] bool asGrid;

    /*
    Public getters
    */
    public int InstanceCount => instanceCount;
    public float Size => size;
    public Vector2[] Positions => positions;

    /*
    Private properties
    */
    Vector2[] positions;
    private bool prevGridMode = false;

    void Start()
    {
        prevGridMode = asGrid;
        positions = GeneratePositions();
    }

    void OnValidate()
    {
        instanceCount = Mathf.Max(0, instanceCount);

        Draw drawer = GetComponentInChildren<Draw>();
        if (drawer == null) return;

        if (asGrid) instanceCount = Mathf.Min(calculateMaxInGrid(), instanceCount);

        if ((positions != null && positions.Length != instanceCount) || asGrid || prevGridMode != asGrid)
        {
            positions = GeneratePositions();
            drawer.UpdatePositions();
        }

        prevGridMode = asGrid;
    }

    int calculateMaxInGrid()
    {
        int sizeX = (int)(spawnArea.x / size);
        int sizeY = (int)(spawnArea.y / size);

        return sizeX * sizeY;
    }
    
    Vector2[] GenerateRandomPositions()
    {
        Vector2[] positions = new Vector2[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            positions[i] = new Vector2(
                Random.Range(-spawnArea.x / 2 + size / 2, spawnArea.x / 2 - size / 2),
                Random.Range(-spawnArea.y / 2 + size / 2, spawnArea.y / 2 - size / 2)
            );
        }

        return positions;
    }

    Vector2[] GenerateGridPositions()
    {
        Vector2[] positions = new Vector2[instanceCount];

        Vector2 topLeft = new Vector2(
            -spawnArea.x/2 + size / 2,
            spawnArea.y/2 - size / 2
        );

        int sizeX = (int)(spawnArea.x / size);
        int sizeY = (int)(spawnArea.y / size);
        for (int x = 0; x < sizeX; x++) {
            for (int y = 0; y < sizeY; y++)
            {
                int idx = y * sizeX + x;
                if (idx >= instanceCount) continue;

                positions[idx] = topLeft + new Vector2(
                    x * size,
                    -y * size
                );
            }
        }

        return positions;
    }

    Vector2[] GeneratePositions()
    {
        return asGrid ? GenerateGridPositions() : GenerateRandomPositions();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(transform.position, new Vector3(spawnArea.x, spawnArea.y, 0f));
    }
}
