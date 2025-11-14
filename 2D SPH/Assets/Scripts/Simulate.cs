using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Simulate : MonoBehaviour
{
    private enum Solver { WCSPH, IISPH };
    /*
    Inspector properties
    */
    [Header("Shaders")]
    [SerializeField] ComputeShader computeShader;

    [Header("Simulation Settings")]
    [SerializeField] float simulationSpeed = 1f;
    [SerializeField] float smoothingRadius = 1f;
    [SerializeField] float velocitySmoothing = 0f;
    [SerializeField] int stepSize = 10;
    [SerializeField] float maxVelocity = 100f;

    [Header("External forces")]
    [SerializeField] float initSpeed = 5f;
    [SerializeField] float gravity = -9.8f;
    [SerializeField] float dampingFactor = 0.9f;
    [SerializeField] float wavePeriod = 0.1f;
    [SerializeField] float waveStrength = 1f;

    [Header("Pressure")]
    [SerializeField] Solver pressureSolver = Solver.IISPH;
    [SerializeField] float restDensity = 1f;
    [SerializeField] float nearPressureMultiplier = 1f;
    
    [Header("IISPH Pressure")]
    [SerializeField] float relaxationFactor = 0.5f;
    [SerializeField] int solverIterations = 4;

    [Header("WCSPH Pressure")]
    [SerializeField, Range(0.001f, 0.1f)] float densityError = 0.01f;
    [SerializeField] float stiffness = 7f;

    [Header("Viscosity")]
    [SerializeField] float viscosityMultiplier = 1f;
    [Header("Surface tension")]
    [SerializeField] float surfaceTensionMultiplier = 1f;

    /*
    Private properties
    */
    ShaderHelper shader;

    Spawn spawner;
    Draw drawer;
    bool started;
    float physicsTimeStep;
    float accumulator = 0f;
    int maxStepsPerFrame = 1;

    int instanceCount;

    KernelSet kernels;

    float simulationTime = 0;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;
    public ShaderHelper Shader => shader;

    public float SimulationSpeed
    {
        get => simulationSpeed;
        set
        {
            simulationSpeed = value;
            UpdateVariables();
        }
    }

    public float Gravity
    {
        get => gravity;
        set
        {
            gravity = value;
            UpdateVariables();
        }
    }

    public float DampingFactor
    {
        get => dampingFactor;
        set
        {
            dampingFactor = value;
            UpdateVariables();
        }
    }

    public float WaveStrength
    {
        get => waveStrength;
        set
        {
            waveStrength = value;
            UpdateVariables();
        }
    }

    public float WavePeriod
    {
        get => wavePeriod;
        set
        {
            wavePeriod = value;
            UpdateVariables();
        }
    }

    public float DensityFluctuation
    {
        get => densityError;
        set
        {
            densityError = value;
            UpdateVariables();
        }
    }

    public float Stiffness
    {
        get => stiffness;
        set
        {
            stiffness = value;
            UpdateVariables();
        }
    }

    public float SolverIterations
    {
        get => solverIterations;
        set
        {
            solverIterations = Mathf.FloorToInt(value);
            UpdateVariables();
        }
    }

    public void SetSolver(int index)
    {
        pressureSolver = (Solver)index;
        UpdateVariables();
    }

    void Start()
    {
        spawner = GetComponent<Spawn>();
        drawer = GetComponent<Draw>();
        
        shader = new ShaderHelper(computeShader);
        drawer.SetComputeShader(shader);

        physicsTimeStep = 1f / SolverSteps(pressureSolver);
    }

    void Update()
    {
        HandleKeyPresses();

        if (started)
        {
            AdvanceFrame();
        }

        drawer.DrawFrame();
    }

    void UpdateWaveForce()
    {

        float angle = wavePeriod * simulationTime;
        Vector3 gravityForce = new Vector3(waveStrength * Mathf.Cos(angle), gravity, waveStrength * Mathf.Sin(angle));
        shader.SetValues(new object[] { "gravity", gravityForce });
    }

    void AdvanceFrame()
    {
        UpdateWaveForce();
        accumulator += Time.deltaTime;

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

        BindExternalBuffers();
    }

    void RunPhysicsStep()
    {
        shader.BindDynamicBuffers(kernels);

        ScanAndScatter();

        if (pressureSolver == Solver.IISPH)
            RunIISPHStep();
        if (pressureSolver == Solver.WCSPH)
        {
            RunWCSPHStep();
        }

        UpdateColours();

        simulationTime += physicsTimeStep * simulationSpeed;
    }

    void RunIISPHStep()
    {
        shader.Dispatch(kernels.PrePressureKernels);

        for (int l = 0; l < solverIterations; l++)
        {
            shader.Dispatch(kernels.PressureKernels);
        }

        shader.Dispatch(kernels.PostPressureKernels);
    }

    void RunWCSPHStep()
    {
        shader.Dispatch(kernels.WCSPHKernels);
    }

    void UpdateColours()
    {
        Draw.Property propChoice = drawer.ColourProperty;

        if (propChoice == Draw.Property.Velocity) shader.Dispatch(kernels.CalculateVelocityColour);
        if (propChoice == Draw.Property.Density) shader.Dispatch(kernels.CalculateDensityColour);
        if (propChoice == Draw.Property.Pressure) shader.Dispatch(kernels.CalculatePressureColour);
    }

    void ScanAndScatter()
    {
        int clearCountsGroupNum = Mathf.CeilToInt(Constants.binNumber / (float)Constants.threadGroupSize);
        shader.Dispatch(true, clearCountsGroupNum, kernels.ClearCounts);

        shader.Dispatch(kernels.Partition);

        HierarchicalScan();

        shader.Dispatch(kernels.Scatter);

        shader.SwapBuffers();
        shader.BindDynamicBuffers(kernels);
    }

    void HierarchicalScan()
    {
        int numBlocks = Mathf.CeilToInt(Constants.binNumber / (float)Constants.scanBlockSize);

        // Phase 1: Local scan in each block (stores block sums in BlockSums buffer)
        shader.Dispatch(true, numBlocks, kernels.Scan);

        // Phase 2: If we have multiple blocks, scan the block sums themselves
        if (numBlocks > 1)
        {
            // For 1000 bins with scanBlockSize=128: numBlocks = 8
            computeShader.SetInt("numBlockSums", numBlocks);
            shader.SetValues(new object[] { "numBlockSums", numBlocks });
            shader.Dispatch(true, 1, kernels.ScanBlockSums);
        }

        // Phase 3: Add scanned block sums to each block's elements
        if (numBlocks > 1)
        {
            int addThreadGroups = Mathf.CeilToInt(Constants.binNumber / (float)Constants.threadGroupSize);
            shader.Dispatch(true, addThreadGroups, kernels.AddBlockSums);
        }

        // Phase 4: Write final element (total particle count)
        shader.Dispatch(true, 1, kernels.FinalizeScan);
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
        InitialiseLeapFrogVelocities();

        started = true;
    }

    void InitialiseLeapFrogVelocities()
    {
        // Set half timestep for initialization
        shader.SetValues(new object[] { "deltaTime", physicsTimeStep * 0.5f });

        RunPhysicsStep();

        shader.SetValues(new object[] { "deltaTime", physicsTimeStep });
    }

    void BindExternalBuffers()
    {
        drawer.BindBuffers(shader.PositionBuffer, shader.Colours);
        drawer.UpdateSize(spawner.Size);
    }

    Vector3[] GenerateVelocityData()
    {
        Vector3[] velocities = new Vector3[spawner.InstanceCount];

        for (int i = 0; i < spawner.InstanceCount; i++)
        {
            float random = UnityEngine.Random.Range(0f, 2 * Mathf.PI);
            Vector3 vel = new Vector3(Mathf.Cos(random), Mathf.Sin(random)) * initSpeed;
            velocities[i] = vel;
        }

        return velocities;
    }

    void FindKernels()
    {
        kernels = new KernelSet(computeShader);

        shader.BindStaticBuffers(kernels);
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

        UpdateMouseForce(Vector3.zero, 0, 0);
        UpdateVariables();

        FindKernels();
    }

    void UpdateVariables()
    {
        float particleSpacing = spawner.Size + spawner.Spacing;
        float particleMass = particleSpacing * particleSpacing * particleSpacing;
        float kernelConstant = 8f / (Mathf.PI * Mathf.Pow(smoothingRadius, 3));
        float gradConstant = 6 * kernelConstant / smoothingRadius;
        float speedOfSound = maxVelocity / Mathf.Sqrt(densityError);
        float B = restDensity * speedOfSound * speedOfSound / stiffness;
        physicsTimeStep = 1f / SolverSteps(pressureSolver);

        object[] keyValues =
        {
            "deltaTime", physicsTimeStep * simulationSpeed,
            "smoothingRadius", smoothingRadius,
            "dampingFactor", dampingFactor,
            "gravity", gravity,
            "restDensity", restDensity,
            "relaxationFactor", relaxationFactor,
            "particleMass", particleMass,
            "viscosityMultiplier", viscosityMultiplier,
            "surfaceTensionMultiplier", surfaceTensionMultiplier,
            "velocitySmoothing", velocitySmoothing,
            "kernelConstant", kernelConstant,
            "gradConstant", gradConstant,
            "maxVelocity", maxVelocity,
            "stiffness", stiffness,
            "B", B,
            "nearPressureMultiplier", nearPressureMultiplier
        };

        shader.SetValues(keyValues);
    }

    int SolverSteps(Solver solver)
    {
        if (solver == Solver.WCSPH) return Constants.stableWCSPHStep;
        if (solver == Solver.IISPH) return Constants.stableIISPHStep;

        return Constants.stableWCSPHStep;
    }

    void ValidateInspectorProperties()
    {
        simulationSpeed = Mathf.Clamp(simulationSpeed, 0, 1);
        initSpeed = Mathf.Max(0, initSpeed);
        dampingFactor = Mathf.Max(0, dampingFactor);
        smoothingRadius = Mathf.Max(0.01f, smoothingRadius);
        restDensity = Mathf.Max(0.01f, restDensity);
        relaxationFactor = Mathf.Clamp01(relaxationFactor);
        viscosityMultiplier = Mathf.Max(0, viscosityMultiplier);
        stepSize = Mathf.Max(0, stepSize);
        maxVelocity = Mathf.Max(0.01f, maxVelocity);
        solverIterations = Mathf.Max(0, solverIterations);
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

        if (UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            if (simulationSpeed != 0) return;
            simulationSpeed = 1;
            UpdateVariables();
            for (int _ = 0; _ < stepSize; _++) AdvanceFrame();
            simulationSpeed = 0;
            UpdateVariables();
        }

        HandleSpeedControls();
    }

    void HandleSpeedControls()
    {
        if (UnityEngine.InputSystem.Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            simulationSpeed = Mathf.Max(0, simulationSpeed - 0.1f);
            UpdateVariables();
        }

        if (UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            simulationSpeed = Mathf.Min(1, simulationSpeed + 0.1f);
            UpdateVariables();
        }

        if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
        {
            simulationSpeed = simulationSpeed == 1 ? 0 : 1;
            UpdateVariables();
        }
    }

    public void UpdateMouseForce(Vector3 origin, float radius, float power)
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

        float cellSize = 2f * smoothingRadius;

        Container container = GetComponentInChildren<Container>();

        // Calculate based on the actual boundary particles can reach
        float particleSize = spawner.Size;
        Vector3 effectiveBoundary = container.Boundary - Vector3.one * particleSize;

        int maxX = Mathf.FloorToInt(effectiveBoundary.x / cellSize);
        int maxY = Mathf.FloorToInt(effectiveBoundary.y / cellSize);
        int maxZ = Mathf.FloorToInt(effectiveBoundary.z / cellSize);

        shader.SetValues(new object[]
        {
            "containerSize", container.Boundary,
            "maxCornerX", maxX,
            "maxCornerY", maxY,
            "maxCornerZ", maxZ
        });
    }
}