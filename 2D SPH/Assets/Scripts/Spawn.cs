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
    [SerializeField] Vector3 spawnArea = new Vector3(10f, 10f, 10f);
    [SerializeField] Vector3 spawnPosition = new Vector3(0f, 0f, 0f);
    [SerializeField] bool asGrid;

    /*
    Private properties
    */
    bool prevGridMode = false;
    float sizeWithSpacing;
    ComputeBuffer velocityBuffer;
    ComputeBuffer propertyBuffer;

    /*
    Public getters
    */
    public int InstanceCount => instanceCount;
    public float Size
    {
        get => size;
        set
        {
            size = value;    
            sizeWithSpacing = size + spacing;
            if (asGrid)
                instanceCount = calculateMaxInGrid();
            RecreatePositions();
        }
    }

    public bool AsGrid
    {
        get => asGrid;
        set
        {
            asGrid = value;
            RecreatePositions();
        }
    }

    public float Spacing => spacing;
    public ComputeBuffer positionBuffer { get; private set; }
    public float Area => spawnArea.x * spawnArea.y * spawnArea.z;


    void Start()
    {
        ValidateInspectorProperties();
        prevGridMode = asGrid;
        CreateBuffers();
        UpdateBuffers();
        BindExternalBuffers();
    }

    void OnValidate()
    {
        ValidateInspectorProperties();

        if (!Application.isPlaying) return;
        RecreatePositions();

        prevGridMode = asGrid;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(transform.position + new Vector3(spawnPosition.x, spawnPosition.y, spawnPosition.z), new Vector3(spawnArea.x, spawnArea.y, spawnArea.z));
    }

    public void RecreatePositions()
    {
        if (GetComponent<Simulate>().Started) return;

        if (positionBuffer != null && (instanceCount != positionBuffer.count || prevGridMode != asGrid))
        {
            UpdateBuffers();
        }

        if (positionBuffer != null) BindExternalBuffers();
    }

    void ValidateInspectorProperties()
    {
        instanceCount = Mathf.Max(1, instanceCount);
        spacing = Mathf.Max(0, spacing);
        size = Mathf.Max(0.01f, size);
        sizeWithSpacing = size + spacing;

        if (asGrid)
            instanceCount = calculateMaxInGrid();
    }

    void CreateBuffers()
    {
        ReleaseBuffers();
        positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3);

        Vector3[] positions = GeneratePositions();
        positionBuffer.SetData(positions);

        velocityBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3);

        propertyBuffer = new ComputeBuffer(instanceCount, sizeof(float));
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
            Vector3[] positions = GeneratePositions();
            positionBuffer.SetData(positions);
        }
    }

    void BindExternalBuffers()
    {
        Draw drawer = GetComponent<Draw>();
        drawer.BindPositions(positionBuffer);
        drawer.UpdateSize(size: size);
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

        if (propertyBuffer != null)
        {
            propertyBuffer.Release();
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
        int sizeZ = (int)(spawnArea.z / sizeWithSpacing);

        return sizeX * sizeY * sizeZ;
    }

    Vector3[] GenerateRandomPositions()
    {
        Vector3[] positions = new Vector3[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            positions[i] = new Vector3(
                Random.Range(-spawnArea.x / 2 + size / 2, spawnArea.x / 2 - size / 2),
                Random.Range(-spawnArea.y / 2 + size / 2, spawnArea.y / 2 - size / 2),
                Random.Range(-spawnArea.z / 2 + size / 2, spawnArea.z / 2 - size / 2)
            );
            positions[i] += spawnPosition;
        }

        return positions;
    }

    Vector3[] GenerateGridPositions()
    {
        Vector3[] positions = new Vector3[instanceCount];

        Vector3 startCorner = new Vector3(
            -spawnArea.x / 2 + sizeWithSpacing / 2,
            spawnArea.y / 2 - sizeWithSpacing / 2,
            -spawnArea.z / 2 + sizeWithSpacing / 2
        );
        startCorner += spawnPosition;

        int sizeX = (int)(spawnArea.x / sizeWithSpacing);
        int sizeY = (int)(spawnArea.y / sizeWithSpacing);
        int sizeZ = (int)(spawnArea.z / sizeWithSpacing);

        int idx = 0;
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (idx >= instanceCount) break;

                    positions[idx] = startCorner + new Vector3(
                        x * sizeWithSpacing,
                        -y * sizeWithSpacing,
                        z * sizeWithSpacing
                    );

                    // Small random jitter to avoid perfect stacking
                    positions[idx] += new Vector3(
                        Random.Range(-0.05f, 0.05f),
                        Random.Range(-0.05f, 0.05f),
                        Random.Range(-0.05f, 0.05f)
                    );

                    idx++;
                }
            }
        }

        return positions;
    }


    Vector3[] GeneratePositions()
    {
        return asGrid ? GenerateGridPositions() : GenerateRandomPositions();
    }

    public Vector3[] ExtractPositions()
    {
        Vector3[] positions = new Vector3[instanceCount];
        positionBuffer.GetData(positions);

        return positions;
    }


}