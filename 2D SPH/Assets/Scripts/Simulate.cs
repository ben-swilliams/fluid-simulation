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

    /*
    Public getters
    */
    public ComputeBuffer positionBuffer { get; private set; }
    public ComputeBuffer velocityBuffer { get; private set; }
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

            int threadGroups = Mathf.CeilToInt(spawner.InstanceCount / (float)threadGroupSize);
            if (spawner.InstanceCount == 0) return;
            computeShader.Dispatch(kernel, threadGroups, 1, 1);
        }
    }

    void StartSimulation()
    {
        SetupBuffers();
        InitialiseVariables();
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
        positionBuffer.SetData(spawner.Positions);

        Vector2[] velocities = GenerateVelocityData();
        velocityBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);
        velocityBuffer.SetData(velocities);
    }

    void InitialiseVariables()
    {
        computeShader.SetInt("threadGroupSize", threadGroupSize);
        computeShader.SetFloat("size", spawner.Size);
        computeShader.SetInt("instanceCount", spawner.InstanceCount);
        UpdateVariables();

        kernel = computeShader.FindKernel("Gravity");

        computeShader.SetBuffer(kernel, "Positions", positionBuffer);
        computeShader.SetBuffer(kernel, "Velocities", velocityBuffer);
    }

    public void UpdateVariables()
    {
        computeShader.SetFloat("dampingFactor", dampingFactor);
        computeShader.SetVector("containerSize", GetComponentInChildren<Container>().Boundary);
        computeShader.SetVector("gravity", gravity);
    }

    // Temporary
    public Vector2[] GetPositions()
    {
        Vector2[] positions = new Vector2[positionBuffer.count];
        positionBuffer.GetData(positions);

        return positions;
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