using UnityEngine;

public class Simulate : MonoBehaviour
{

    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] ComputeShader computeShader;

    [Header("Simulation Settings")]
    [SerializeField] float initSpeed = 5f;

    [SerializeField] Vector2 gravity = new Vector2(0, -9.8f);
    [SerializeField] float dampingFactor = 0.9f;

    /*
    Private properties
    */
    static int threadGroupSize = 64;
    int kernel;
    Spawn spawner;
    bool started;

    int instanceCount;
    ComputeBuffer positionBuffer;
    ComputeBuffer velocityBuffer;
    /*
    Public getters
    */
    public bool Started => started;
    
    void Start()
    {
        spawner = GetComponent<Spawn>();
    }

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame && !started)
        {
            StartSimulation();
            Debug.Log("Simulation started!");
        }

        if (started)
        {
            computeShader.SetFloat("deltaTime", Time.deltaTime);

            int threadGroups = Mathf.CeilToInt(instanceCount / (float)threadGroupSize);
            computeShader.Dispatch(kernel, threadGroups, 1, 1);
        }
    }

    void StartSimulation()
    {
        SetupBuffers();
        InitialiseVariables();
        UpdateBoundary();
        GetComponent<Draw>().BindBuffer(positionBuffer, spawner.Size);
        GetComponent<Density>().BindBuffer(positionBuffer);
        started = true;
    }

    Vector2[] GenerateVelocityData()
    {
        Vector2[] velocities = new Vector2[spawner.InstanceCount];

        for (int i = 0; i < spawner.InstanceCount; i++)
        {
            float random = Random.Range(0f, 2 * Mathf.PI);
            Vector2 vel = new Vector2(Mathf.Cos(random), Mathf.Sin(random)) * initSpeed;
            velocities[i] = vel;
        }

        return velocities;
    }

    void SetupBuffers()
    {
        positionBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);
        Vector2[] positions = new Vector2[spawner.InstanceCount];
        spawner.PositionBuffer.GetData(positions);
        positionBuffer.SetData(positions);

        Vector2[] velocities = GenerateVelocityData();
        velocityBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);
        velocityBuffer.SetData(velocities);
    }

    void InitialiseVariables()
    {
        instanceCount = spawner.InstanceCount;
        computeShader.SetInt("threadGroupSize", threadGroupSize);
        computeShader.SetFloat("size", spawner.Size);
        computeShader.SetInt("instanceCount", instanceCount);
        UpdateVariables();

        kernel = computeShader.FindKernel("Gravity");

        computeShader.SetBuffer(kernel, "Positions", positionBuffer);
        computeShader.SetBuffer(kernel, "Velocities", velocityBuffer);
    }

    void UpdateVariables()
    {
        computeShader.SetFloat("dampingFactor", dampingFactor);
        computeShader.SetVector("gravity", gravity);
    }

    public void UpdateBoundary()
    {
        computeShader.SetVector("containerSize", GetComponentInChildren<Container>().Boundary);
    }

    void OnValidate()
    {
        if (!started) return;

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