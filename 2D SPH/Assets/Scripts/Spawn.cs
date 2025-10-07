using UnityEngine;

public class Spawn : MonoBehaviour
{
    /*
    Inspector properties
    */
    [Header("Initialisation settings")]
    [SerializeField] int instanceCount = 10;
    [SerializeField] float size = 0.1f;
    [SerializeField] float spacing = 0f;
    [SerializeField] Vector2 spawnArea = new Vector2(10f, 10f);
    [SerializeField] Vector2 spawnPosition = new Vector2(0f, 0f);
    [SerializeField] bool asGrid;

    /*
    Private properties
    */
    bool prevGridMode = false;
    float sizeWithSpacing;
    ComputeBuffer velocityBuffer;

    /*
    Public getters
    */
    public int InstanceCount => instanceCount;
    public float Size => size;
    public ComputeBuffer positionBuffer { get; private set; }
    public float Area => spawnArea.y * spawnArea.y;


    void Start()
    {
        prevGridMode = asGrid;
        CreateBuffers();
        UpdateBuffers();
        BindExternalBuffers();
    }

    void OnValidate()
    {
        ValidateInspectorProperties();

        if (!Application.isPlaying) return;

        if (!GetComponent<Simulate>().Started)
        {
            if (positionBuffer != null && (instanceCount != positionBuffer.count || prevGridMode != asGrid))
            {
                UpdateBuffers();
            }

            if (positionBuffer != null) BindExternalBuffers();
        }

        prevGridMode = asGrid;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(transform.position + new Vector3(spawnPosition.x, spawnPosition.y, 0), new Vector3(spawnArea.x, spawnArea.y, 0f));
    }

    void ValidateInspectorProperties()
    {
        instanceCount = Mathf.Max(1, instanceCount);
        spacing = Mathf.Max(0, spacing);
        size = Mathf.Max(0, size);
        sizeWithSpacing = size + spacing;

        if (asGrid)
            instanceCount = calculateMaxInGrid();
    }

    void CreateBuffers()
    {
        ReleaseBuffers();
        positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);

        Vector2[] positions = GeneratePositions();
        positionBuffer.SetData(positions);

        velocityBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);
    }

    void UpdateBuffers()
    {
        if (positionBuffer == null || positionBuffer.count != instanceCount)
        {
            ReleaseBuffers();
            CreateBuffers();
        }
        else
        {
            Vector2[] positions = GeneratePositions();
            positionBuffer.SetData(positions);
        }
    }

    void BindExternalBuffers()
    {
        GetComponent<Draw>().BindBuffers(positionBuffer, velocityBuffer, size);
    }

    void ReleaseBuffers()
    {
        if (positionBuffer != null)
        {
            positionBuffer.Release();
            positionBuffer = null;
        }
        if (velocityBuffer != null)
        {
            velocityBuffer.Release();
            velocityBuffer = null;
        }
    }

    void OnDestroy()
    {
        ReleaseBuffers();
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
            positions[i] += spawnPosition;
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
        topLeft += spawnPosition;

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

    public Vector2[] ExtractPositions()
    {
        Vector2[] positions = new Vector2[instanceCount];
        positionBuffer.GetData(positions);

        return positions;
    }


}