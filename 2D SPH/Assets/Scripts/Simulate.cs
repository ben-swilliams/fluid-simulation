using UnityEditor.ShaderGraph.Internal;
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
    int positionKernel;
    Spawn spawner;
    bool started;

    int instanceCount;
    ComputeBuffer positionBuffer;
    ComputeBuffer velocityBuffer;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;
    
    void Start()
    {
        spawner = GetComponent<Spawn>();
    }

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (!started)
                StartSimulation();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        if (started)
        {
            computeShader.SetFloat("deltaTime", Time.deltaTime);

            int threadGroups = Mathf.CeilToInt(instanceCount / (float)threadGroupSize);

            computeShader.Dispatch(gravityKernel, threadGroups, 1, 1);
            // computeShader.Dispatch(pressureKernel, threadGroups, 1, 1);
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
        if (velocityBuffer != null)
            velocityBuffer.Release();
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
        GetComponent<Draw>().BindBuffer(positionBuffer, spawner.Size);
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
        Vector2[] positions = ExtractSpawnData();
        positionBuffer = new ComputeBuffer(positions.Length, sizeof(float) * 2);
        positionBuffer.SetData(positions);

        Vector2[] velocities = GenerateVelocityData();
        velocityBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);
        velocityBuffer.SetData(velocities);
    }

    Vector2[] ExtractSpawnData()
    {
        Vector2[] positions = new Vector2[spawner.InstanceCount];
        spawner.positionBuffer.GetData(positions);

        return positions;
    }

    void InitialiseVariables()
    {
        instanceCount = spawner.InstanceCount;
        computeShader.SetInt("threadGroupSize", threadGroupSize);
        computeShader.SetFloat("size", spawner.Size);
        computeShader.SetInt("instanceCount", instanceCount);
        UpdateVariables();

        gravityKernel = computeShader.FindKernel("Gravity");
        pressureKernel = computeShader.FindKernel("Pressure");
        positionKernel = computeShader.FindKernel("UpdatePositions");

        computeShader.SetBuffer(gravityKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(gravityKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(pressureKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(pressureKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(positionKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(positionKernel, "Velocities", velocityBuffer);
    }

    void UpdateVariables()
    {
        computeShader.SetFloat("smoothingRadius", smoothingRadius);
        computeShader.SetFloat("dampingFactor", dampingFactor);
        computeShader.SetVector("gravity", gravity);

        float pressureKernelConstant = 15 / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
        computeShader.SetFloat("pressureKernelConstant", pressureKernelConstant);
        computeShader.SetFloat("gasConstant", gasConstant);
        computeShader.SetFloat("restDensity", restDensity);
    }

    void ValidateInspectorProperties()
    {
        initSpeed = Mathf.Max(0, initSpeed);
        dampingFactor = Mathf.Max(0, dampingFactor);
        smoothingRadius = Mathf.Max(0, smoothingRadius);
        gasConstant = Mathf.Max(0, gasConstant);
        restDensity = Mathf.Max(0, restDensity);
    }

    public void UpdateBoundary()
    {
        computeShader.SetVector("containerSize", GetComponentInChildren<Container>().Boundary);
    }

}