using UnityEngine;

public class Spawn : MonoBehaviour
{
    /*
    Inspector properties
    */
    [SerializeField] int instanceCount = 10;
    [SerializeField] float size = 0.1f;
    [SerializeField] float spacing = 0f;
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
    bool prevGridMode = false;
    float sizeWithSpacing;

    void Start()
    {
        prevGridMode = asGrid;
        positions = GeneratePositions();
    }

    void OnValidate()
    {
        instanceCount = Mathf.Max(0, instanceCount);
        sizeWithSpacing = size + spacing;

        var drawer = GetComponentInChildren<Draw>();
        if (drawer == null) return;

        if (asGrid)
        {
            int maxGrid = calculateMaxInGrid();
            if (positions != null && instanceCount != positions.Length)
                instanceCount = Mathf.Min(maxGrid, instanceCount);
            else
                instanceCount = maxGrid;
        }

        if (asGrid || positions == null || positions.Length != instanceCount || prevGridMode != asGrid)
        {
            positions = GeneratePositions();
            drawer.UpdatePositions();
        }

        prevGridMode = asGrid;
    }


    int calculateMaxInGrid()
    {
        int sizeX = (int)(spawnArea.x / sizeWithSpacing);
        int sizeY = (int)(spawnArea.y / sizeWithSpacing);

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
            -spawnArea.x/2 + sizeWithSpacing / 2,
            spawnArea.y/2 - sizeWithSpacing / 2
        );

        int sizeX = (int)(spawnArea.x / sizeWithSpacing);
        int sizeY = (int)(spawnArea.y / sizeWithSpacing);
        for (int x = 0; x < sizeX; x++) {
            for (int y = 0; y < sizeY; y++)
            {
                int idx = y * sizeX + x;
                if (idx >= instanceCount) continue;

                positions[idx] = topLeft + new Vector2(
                    x * sizeWithSpacing,
                    -y * sizeWithSpacing
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
