using UnityEngine;

public class Simulate : MonoBehaviour
{

    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] ComputeShader computeShader;

    [Header("Instancing Settings")]
    [SerializeField] int maxInstanceCount = 1000000;
    [SerializeField] int instanceCount = 1000;
    [SerializeField] Vector2 spawnArea = new Vector2(10f, 10f);
    [SerializeField] float size = 0.1f;
    [SerializeField] float initSpeed = 5f;

    [Header("Simulation Settings")]
    [SerializeField] Vector2 gravity = new Vector2(0, -9.8f);
    [SerializeField] float dampingFactor = 0.9f;

    /*
    Private properties
    */
    static int threadGroupSize = 64;
    int kernel;

    /*
    Public getters
    */
    public int InstanceCount => instanceCount;
    public float Size => size;
    public ComputeBuffer positionBuffer { get; private set; }
    public ComputeBuffer velocityBuffer { get; private set; }
    
    void Start()
    {
        SetupBuffers();
        InitialiseVariables();
    }

    Vector2[] GeneratePositionData()
    {
        Vector2[] positions = new Vector2[maxInstanceCount];

        for (int i = 0; i < maxInstanceCount; i++)
        {
            Vector2 position = new Vector2(
                Random.Range(-spawnArea.x/2 + size/2, spawnArea.x/2 - size/2),
                Random.Range(-spawnArea.y/2 + size/2, spawnArea.y/2 - size/2)
            );

            positions[i] = position;
        }

        return positions;
    }

    Vector2[] GenerateVelocityData()
    {
        Vector2[] velocities = new Vector2[maxInstanceCount];

        for (int i = 0; i < maxInstanceCount; i++)
        {
            float random = Random.Range(0f, 2 * Mathf.PI);
            Vector2 vel = new Vector2(Mathf.Cos(random), Mathf.Sin(random)) * initSpeed;
            velocities[i] = vel;
        }

        return velocities;
    }

    void SetupBuffers()
    {
        Vector2[] positions = GeneratePositionData();
        positionBuffer = new ComputeBuffer(maxInstanceCount, sizeof(float) * 2);
        positionBuffer.SetData(positions);

        Vector2[] velocities = GenerateVelocityData();
        velocityBuffer = new ComputeBuffer(maxInstanceCount, sizeof(float) * 2);
        velocityBuffer.SetData(velocities);
    }

    void InitialiseVariables()
    {
        computeShader.SetInt("threadGroupSize", threadGroupSize);
        UpdateVariables();

        kernel = computeShader.FindKernel("Gravity");

        computeShader.SetBuffer(kernel, "Positions", positionBuffer);
        computeShader.SetBuffer(kernel, "Velocities", velocityBuffer);
    }

    void UpdateVariables()
    {
        computeShader.SetFloat("size", size);
        computeShader.SetFloat("dampingFactor", dampingFactor);
        computeShader.SetVector("containerSize", GetComponentInChildren<Container>().Boundary);
        computeShader.SetVector("gravity", gravity);
        computeShader.SetInt("instanceCount", instanceCount);
    }
    
    void Update()
    {
        computeShader.SetFloat("deltaTime", Time.deltaTime);

        int threadGroups = Mathf.CeilToInt(instanceCount / (float)threadGroupSize);
        if (instanceCount == 0) return;
        computeShader.Dispatch(kernel, threadGroups, 1, 1);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(transform.position, new Vector3(spawnArea.x, spawnArea.y, 0f));
    }

    void OnValidate()
    {
        instanceCount = Mathf.Clamp(instanceCount, 0, maxInstanceCount);
        GetComponentInChildren<Draw>().UpdateVariables();
        UpdateVariables();
    }
    
    void OnDestroy()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        if (velocityBuffer != null)
            velocityBuffer.Release();
    }
}