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
    public ComputeBuffer PositionBuffer { get; private set; }

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
        CreateBuffer();
        UpdateBuffer();
        GetComponent<Draw>().BindBuffer(PositionBuffer, size);
    }

    void OnValidate()
    {
        instanceCount = Mathf.Max(0, instanceCount);
        spacing = Mathf.Max(0, spacing);
        size = Mathf.Max(0, size);
        sizeWithSpacing = size + spacing;

        if (asGrid)
            instanceCount = calculateMaxInGrid();

        if (!Application.isPlaying) return;

        Draw drawer = GetComponentInChildren<Draw>();
        Density density = GetComponentInChildren<Density>();
        Simulate sim = GetComponentInChildren<Simulate>();
        if (drawer == null || sim == null || density == null || PositionBuffer == null) return;

        if ((asGrid || prevGridMode != asGrid) && !sim.Started)
        {
            positions = GeneratePositions();

            UpdateBuffer();

            drawer.BindBuffer(PositionBuffer, size);
            density.BindBuffer(PositionBuffer);
        }

        prevGridMode = asGrid;
    }

    void CreateBuffer()
    {
        ReleaseBuffer();
        if (instanceCount > 0)
        {
            PositionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);
            PositionBuffer.SetData(positions);
        }
    }

    void UpdateBuffer()
    {
        if (PositionBuffer == null || PositionBuffer.count != instanceCount)
        {
            ReleaseBuffer();
            CreateBuffer();
        }
        else if (instanceCount > 0)
        {
            PositionBuffer.SetData(positions);
        }
    }

    void ReleaseBuffer()
    {
        if (PositionBuffer != null)
        {
            PositionBuffer.Release();
            PositionBuffer = null;
        }
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }

    void OnDisable()
    {
        ReleaseBuffer();
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
            -spawnArea.x / 2 + sizeWithSpacing / 2,
            spawnArea.y / 2 - sizeWithSpacing / 2
        );

        int sizeX = (int)(spawnArea.x / sizeWithSpacing);
        int sizeY = (int)(spawnArea.y / sizeWithSpacing);
        for (int x = 0; x < sizeX; x++)
        {
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