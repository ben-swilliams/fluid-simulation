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
    [SerializeField] int physicsStepsPerSecond = 300;
    [SerializeField] float simulationSpeed = 1f;
    [SerializeField] float smoothingRadius = 1f;
    [SerializeField] float velocitySmoothing = 0f;

    [Header("External forces")]
    [SerializeField] float initSpeed = 5f;
    [SerializeField] Vector2 gravity = new Vector2(0, -9.8f);
    [SerializeField] float dampingFactor = 0.9f;

    [Header("Pressure")]
    [SerializeField] float pressureMultiplier = 1f;
    [SerializeField] float nearPressureMultiplier = 1f;
    [SerializeField] float restDensity = 1f;
    [SerializeField] float stiffness = 1f;

    [Header("Viscosity")]
    [SerializeField] float viscosityMultiplier = 1f;
    [Header("Surface tension")]
    [SerializeField] float surfaceTensionMultiplier = 1f;

    /*
    Private properties
    */
    ShaderHelper shader;

    Spawn spawner;
    bool started;
    float physicsTimeStep;
    float accumulator = 0f;
    int maxStepsPerFrame = 5;

    int instanceCount;

    // Kernel indices
    int clearCountsKernel;
    int partitionKernel;
    int scanKernel;
    int scanBlockSumsKernel;
    int addBlockSumsKernel;
    int finalizeScanKernel;
    int scatterKernel;
    int densityKernel;
    int intermediateAccelerationKernel;
    int intermediateVelocityAndDKernel;
    int intermediateDensityAndAKernel;
    int zeroPressuresKernel;
    int pressureSumIterationKernel;
    int pressureConvergeIterationKernel;
    int velocityKernel;
    int positionKernel;


    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;

    void Start()
    {
        spawner = GetComponent<Spawn>();
        shader = new ShaderHelper(computeShader);

        physicsTimeStep = 1f / physicsStepsPerSecond;
    }

    void Update()
    {
        HandleKeyPresses();

        if (started)
        {
            accumulator += Time.deltaTime * simulationSpeed;

            int stepsThisFrame = 0;
            while (accumulator >= physicsTimeStep && stepsThisFrame < maxStepsPerFrame)
            {
                RunPhysicsStep();
                accumulator -= physicsTimeStep;
                stepsThisFrame++;
            }

            // Prevent spiral of death - if we're too far behind, reset accumulator
            if (accumulator > physicsTimeStep * maxStepsPerFrame)
            {
                accumulator = 0f;
            }
        }
    }

    void RunPhysicsStep()
    {
        shader.SetValues(new object[] { "deltaTime", physicsTimeStep });

        shader.BindDynamicBuffers();

        ScanAndScatter();

        shader.Dispatch(densityKernel, intermediateAccelerationKernel, intermediateVelocityAndDKernel, intermediateDensityAndAKernel, zeroPressuresKernel);

        int minIterations = 5;

        for (int l = 0; l < minIterations; l++)
        {
            shader.Dispatch(pressureSumIterationKernel, pressureConvergeIterationKernel);
        }

        shader.Dispatch(velocityKernel, positionKernel);
    }

    void ScanAndScatter()
    {
        int clearCountsGroupNum = Mathf.CeilToInt(Constants.binNumber / (float)Constants.threadGroupSize);
        shader.Dispatch(true, clearCountsGroupNum, clearCountsKernel);

        shader.Dispatch(partitionKernel);

        HierarchicalScan();

        shader.Dispatch(scatterKernel);

        shader.SwapBuffers();
        shader.BindDynamicBuffers();
    }

    void HierarchicalScan()
    {
        int numBlocks = Mathf.CeilToInt(Constants.binNumber / (float)Constants.scanBlockSize);

        // Phase 1: Local scan in each block (stores block sums in BlockSums buffer)
        shader.Dispatch(true, numBlocks, scanKernel);

        // Phase 2: If we have multiple blocks, scan the block sums themselves
        if (numBlocks > 1)
        {
            // For 1000 bins with scanBlockSize=128: numBlocks = 8
            computeShader.SetInt("numBlockSums", numBlocks);
            shader.SetValues(new object[] { "numBlockSums", numBlocks });
            shader.Dispatch(true, 1, scanBlockSumsKernel);
        }

        // Phase 3: Add scanned block sums to each block's elements
        if (numBlocks > 1)
        {
            int addThreadGroups = Mathf.CeilToInt(Constants.binNumber / (float)Constants.threadGroupSize);
            shader.Dispatch(true, addThreadGroups, addBlockSumsKernel);
        }

        // Phase 4: Write final element (total particle count)
        shader.Dispatch(true, 1, finalizeScanKernel);
    }

    void OnValidate()
    {
        ValidateInspectorProperties();

        if (!Application.isPlaying || !started) return;

        UpdateVariables();
    }

    void OnDestroy()
    {
        shader.Destroy();
    }

    void StartSimulation()
    {
        instanceCount = spawner.InstanceCount;

        shader.InitialiseCount(instanceCount);
        shader.SetupBuffers(spawner.ExtractPositions(), GenerateVelocityData());

        InitialiseVariables();
        UpdateBoundary();
        BindExternalBuffers();
        InitializeLeapFrogVelocities();

        started = true;
    }

    void InitializeLeapFrogVelocities()
    {
        // Set half timestep for initialization
        shader.SetValues(new object[] { "deltaTime", physicsTimeStep * 0.5f });

        shader.BindDynamicBuffers();

        ScanAndScatter();

        shader.Dispatch(densityKernel, velocityKernel);

        // Restore full timestep for subsequent steps
        shader.SetValues(new object[] { "deltaTime", physicsTimeStep });
    }

    void BindExternalBuffers()
    {
        GetComponent<Draw>().BindBuffers(shader.PositionBuffer, shader.VelocityBuffer, spawner.Size);
    }

    Vector2[] GenerateVelocityData()
    {
        Vector2[] velocities = new Vector2[spawner.InstanceCount];

        for (int i = 0; i < spawner.InstanceCount; i++)
        {
            float random = UnityEngine.Random.Range(0f, 2 * Mathf.PI);
            Vector2 vel = new Vector2(Mathf.Cos(random), Mathf.Sin(random)) * initSpeed;
            velocities[i] = vel;
        }

        return velocities;
    }

    void FindKernels()
    {
        clearCountsKernel = computeShader.FindKernel("ZeroCounts");
        partitionKernel = computeShader.FindKernel("Partition");
        scanKernel = computeShader.FindKernel("Scan");
        scanBlockSumsKernel = computeShader.FindKernel("ScanBlockSums");
        addBlockSumsKernel = computeShader.FindKernel("AddBlockSums");
        finalizeScanKernel = computeShader.FindKernel("FinalizeScan");
        scatterKernel = computeShader.FindKernel("Scatter");
        densityKernel = computeShader.FindKernel("Density");
        intermediateAccelerationKernel = computeShader.FindKernel("IntermediateAcceleration");
        intermediateVelocityAndDKernel = computeShader.FindKernel("IntermediateVelocityAndD");
        intermediateDensityAndAKernel = computeShader.FindKernel("IntermediateDensityAndA");
        zeroPressuresKernel = computeShader.FindKernel("ZeroPressures");
        pressureSumIterationKernel = computeShader.FindKernel("PressureSumIteration");
        pressureConvergeIterationKernel = computeShader.FindKernel("PressureConvergeIteration");
        velocityKernel = computeShader.FindKernel("UpdateVelocities");
        positionKernel = computeShader.FindKernel("UpdatePositions");

        shader.MapKernels(clearCountsKernel,
                          partitionKernel,
                          scanKernel,
                          scanBlockSumsKernel,
                          addBlockSumsKernel,
                          finalizeScanKernel,
                          scatterKernel,
                          densityKernel,
                          intermediateAccelerationKernel,
                          intermediateVelocityAndDKernel,
                          intermediateDensityAndAKernel,
                          zeroPressuresKernel,
                          pressureSumIterationKernel,
                          pressureConvergeIterationKernel,
                          velocityKernel,
                          positionKernel);
    }

    void InitialiseVariables()
    {
        object[] keyValues =
        {
            "tableSize", Constants.binNumber,
            "size", spawner.Size,
            "instanceCount", instanceCount
        };
        shader.SetValues(keyValues);

        UpdateMouseForce(Vector2.zero, 0, 0);
        UpdateVariables();

        FindKernels();
    }

    void UpdateVariables()
    {
        Vector2 containerSize = GetComponentInChildren<Container>().Boundary;
        int gridX = Mathf.CeilToInt(containerSize.x / smoothingRadius);
        int gridY = Mathf.CeilToInt(containerSize.y / smoothingRadius);

        float poly6KernelConstant = 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadius, 9f));
        float spikyKernelGradConstant = 45 / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
        float nearPressureKernelGradConstant = 30 / (Mathf.PI * Mathf.Pow(smoothingRadius, 3));
        float particleMass = spawner.Area * 1.0f / instanceCount;
        float viscosityKernelGradConstant = 10 / (Mathf.PI * Mathf.Pow(smoothingRadius, 3));

        object[] keyValues =
        {
            "gridX", gridX,
            "gridY", gridY,
            "poly6KernelConstant", poly6KernelConstant,
            "smoothingRadius", smoothingRadius,
            "dampingFactor", dampingFactor,
            "gravity", gravity,
            "pressureMultiplier", pressureMultiplier,
            "spikyKernelGradConstant", spikyKernelGradConstant,
            "nearPressureKernelGradConstant", nearPressureKernelGradConstant,
            "nearPressureMultiplier", nearPressureMultiplier,
            "restDensity", restDensity,
            "stiffness", stiffness,
            "particleMass", particleMass,
            "viscosityKernelGradConstant", viscosityKernelGradConstant,
            "viscosityMultiplier", viscosityMultiplier,
            "surfaceTensionMultiplier", surfaceTensionMultiplier,
            "velocitySmoothing", velocitySmoothing,
        };

        shader.SetValues(keyValues);
    }

    void ValidateInspectorProperties()
    {
        simulationSpeed = Mathf.Clamp(simulationSpeed, 0, 1);
        initSpeed = Mathf.Max(0, initSpeed);
        dampingFactor = Mathf.Max(0, dampingFactor);
        smoothingRadius = Mathf.Max(0.01f, smoothingRadius);
        pressureMultiplier = Mathf.Max(0, pressureMultiplier);
        nearPressureMultiplier = Mathf.Max(0, nearPressureMultiplier);
        restDensity = Mathf.Max(0.01f, restDensity);
        stiffness = Mathf.Max(0, stiffness);
        viscosityMultiplier = Mathf.Max(0, viscosityMultiplier);
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
        if (shader == null) return;

        shader.SetValues(new object[]
        {
            "mousePos", origin,
            "mouseRadius", radius,
            "power", power
        });
    }

    public void UpdateBoundary()
    {
        if (shader == null) return;

        shader.SetValues(new object[]
        {
            "containerSize", GetComponentInChildren<Container>().Boundary
        });
    }
}