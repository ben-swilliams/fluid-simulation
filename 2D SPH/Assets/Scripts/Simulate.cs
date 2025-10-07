using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Simulate : MonoBehaviour
{

    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] ComputeShader computeShader;

    [Header("Simulation Settings")]
    [SerializeField] float simulationSpeed = 1f;
    [SerializeField] float initSpeed = 5f;
    [SerializeField] Vector2 gravity = new Vector2(0, -9.8f);
    [SerializeField] float dampingFactor = 0.9f;
    [SerializeField] float smoothingRadius = 1f;
    [SerializeField] float gasConstant = 1f;
    [SerializeField] float restDensity = 1f;

    /*
    Private properties
    */
    static int threadGroupSize = 64;
    int gravityKernel;
    int pressureKernel;
    int densityKernel;
    int positionKernel;
    Spawn spawner;
    bool started;
    int frameRateTarget = 120;
    float dtTarget;

    int instanceCount;
    ComputeBuffer positionBuffer;
    ComputeBuffer predictedPositionBuffer;
    ComputeBuffer velocityBuffer;
    ComputeBuffer densityBuffer;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;
    
    void Start()
    {
        spawner = GetComponent<Spawn>();
        dtTarget = 1f / frameRateTarget;
    }

    void Update()
    {
        HandleKeyPresses();

        if (started)
            {
                float dt = Mathf.Min(dtTarget, Time.deltaTime);
                computeShader.SetFloat("deltaTime", dt * simulationSpeed);

                int threadGroups = Mathf.CeilToInt(instanceCount / (float)threadGroupSize);

                computeShader.Dispatch(gravityKernel, threadGroups, 1, 1);
                computeShader.Dispatch(densityKernel, threadGroups, 1, 1);
                computeShader.Dispatch(pressureKernel, threadGroups, 1, 1);
                computeShader.Dispatch(positionKernel, threadGroups, 1, 1);
            }
    }

    void OnValidate()
    {
        ValidateInspectorProperties();

        if (!Application.isPlaying) return;

        GetComponentInChildren<DensityField>().UpdateSmoothingRadius();

        if (!started) return;

        UpdateVariables();
    }

    void OnDestroy()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        if (predictedPositionBuffer != null)
            predictedPositionBuffer.Release();
        if (velocityBuffer != null)
            velocityBuffer.Release();
        if (densityBuffer != null)
            densityBuffer.Release();
    }

    void StartSimulation()
    {
        SetupBuffers();
        InitialiseVariables();
        UpdateBoundary();
        BindExternalBuffers();
        started = true;
    }

    void BindExternalBuffers()
    {
        GetComponent<Draw>().BindBuffers(positionBuffer, velocityBuffer, spawner.Size);
        GetComponentInChildren<DensityField>().BindBuffer(positionBuffer);
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
        Vector2[] positions = spawner.ExtractPositions();
        positionBuffer = new ComputeBuffer(positions.Length, sizeof(float) * 2);
        positionBuffer.SetData(positions);

        Vector2[] predictedPositions = new Vector2[positions.Length];
        predictedPositionBuffer = new ComputeBuffer(positions.Length, sizeof(float) * 2);
        predictedPositionBuffer.SetData(predictedPositions);

        Vector2[] velocities = GenerateVelocityData();
        velocityBuffer = new ComputeBuffer(velocities.Length, sizeof(float) * 2);
        velocityBuffer.SetData(velocities);

        float[] densities = new float[spawner.InstanceCount];
        densityBuffer = new ComputeBuffer(densities.Length, sizeof(float));
        densityBuffer.SetData(densities);
    }

    void FindKernels()
    {
        gravityKernel = computeShader.FindKernel("Gravity");
        pressureKernel = computeShader.FindKernel("Pressure");
        densityKernel = computeShader.FindKernel("Density");
        positionKernel = computeShader.FindKernel("UpdatePositions");
    }

    void BindBuffers()
    {
        computeShader.SetBuffer(gravityKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(gravityKernel, "PredictedPositions", predictedPositionBuffer);
        computeShader.SetBuffer(gravityKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(pressureKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(pressureKernel, "PredictedPositions", predictedPositionBuffer);
        computeShader.SetBuffer(pressureKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(pressureKernel, "Densities", densityBuffer);
        computeShader.SetBuffer(densityKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(densityKernel, "PredictedPositions", predictedPositionBuffer);
        computeShader.SetBuffer(densityKernel, "Densities", densityBuffer);
        computeShader.SetBuffer(positionKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(positionKernel, "Velocities", velocityBuffer);
    }

    void InitialiseVariables()
    {
        instanceCount = spawner.InstanceCount;
        computeShader.SetInt("threadGroupSize", threadGroupSize);
        computeShader.SetFloat("size", spawner.Size);
        computeShader.SetInt("instanceCount", instanceCount);
        UpdateMouseForce(Vector2.zero, 0, 0);
        UpdateVariables();

        FindKernels();
        BindBuffers();
    }

    void UpdateVariables()
    {
        float kernelConstant = 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadius, 9f));
        computeShader.SetFloat("kernelConstant", kernelConstant);
        computeShader.SetFloat("smoothingRadius", smoothingRadius);
        computeShader.SetFloat("dampingFactor", dampingFactor);
        computeShader.SetVector("gravity", gravity);

        float pressureKernelGradConstant = -45 / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
        computeShader.SetFloat("pressureKernelGradConstant", pressureKernelGradConstant);
        computeShader.SetFloat("gasConstant", gasConstant);
        computeShader.SetFloat("restDensity", restDensity);

        float viscosityKernelLapConstant = 45 / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
        computeShader.SetFloat("viscosityKernelLapConstant", viscosityKernelLapConstant);
    }

    void ValidateInspectorProperties()
    {
        simulationSpeed = Mathf.Clamp(simulationSpeed, 0, 1);
        initSpeed = Mathf.Max(0, initSpeed);
        dampingFactor = Mathf.Max(0, dampingFactor);
        smoothingRadius = Mathf.Max(0.01f, smoothingRadius);
        gasConstant = Mathf.Max(0, gasConstant);
        restDensity = Mathf.Max(0, restDensity);
    }

    void HandleKeyPresses()
    {
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (!started)
                StartSimulation();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
            simulationSpeed = simulationSpeed == 1 ? 0 : 1;

        if (UnityEngine.InputSystem.Keyboard.current.downArrowKey.wasPressedThisFrame)
            simulationSpeed = Mathf.Max(0, simulationSpeed - 0.1f);

        if (UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame)
            simulationSpeed = Mathf.Min(1, simulationSpeed + 0.1f);
    }



    public void UpdateMouseForce(Vector2 origin, float radius, float power)
    {
        computeShader.SetVector("origin", origin);
        computeShader.SetFloat("mouseRadius", radius);
        computeShader.SetFloat("power", power);
    }

    public void UpdateBoundary()
    {
        computeShader.SetVector("containerSize", GetComponentInChildren<Container>().Boundary);
    }

}