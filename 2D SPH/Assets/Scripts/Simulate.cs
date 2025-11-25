using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Simulate : MonoBehaviour
{
    private enum Solver { WCSPH, IISPH, PCISPH };
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
    [SerializeField] int iisphSolverIterations = 4;

    [Header("WCSPH Pressure")]
    [SerializeField, Range(0.001f, 0.1f)] float densityError = 0.01f;
    [SerializeField] float stiffness = 7f;

    [Header("PCISPH Pressure")]
    [SerializeField] float deltaScale = 0.01f;
    [SerializeField] int pcisphSolverIterations = 3;

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
    int maxStepsPerFrame = 3;

    int instanceCount;

    KernelSet kernels;

    float simulationTime = 0;

    RenderTexture densityTex;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;
    public ShaderHelper Shader => shader;

    public RenderTexture DensityTex => densityTex;

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

    public float IISPHSolverIterations
    {
        get => iisphSolverIterations;
        set
        {
            iisphSolverIterations = Mathf.FloorToInt(value);
            UpdateVariables();
        }
    }

    public float PCISPHSolverIterations
    {
        get => pcisphSolverIterations;
        set
        {
            pcisphSolverIterations = Mathf.FloorToInt(value);
            UpdateVariables();
        }
    }

    public float RestDensity
    {
        get => restDensity;
        set
        {
            restDensity = value;
            UpdateVariables();
        }
    }

    public float DeltaScale
    {
        get => deltaScale;
        set
        {
            deltaScale = value;
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

        UpdateDensityTexture();

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
        ScanAndScatter();

        if (pressureSolver == Solver.IISPH)
            RunIISPHStep();
        if (pressureSolver == Solver.WCSPH)
        {
            RunWCSPHStep();
        }
        if (pressureSolver == Solver.PCISPH)
            RunPCISPHStep();

        UpdateColours();

        simulationTime += physicsTimeStep * simulationSpeed;
    }

    void RunIISPHStep()
    {
        shader.Dispatch(kernels.IISPHPrePressureKernels);

        for (int l = 0; l < iisphSolverIterations; l++)
        {
            shader.Dispatch(kernels.IISPHPressureKernels);
        }

        shader.Dispatch(kernels.IISPHPostPressureKernels);
    }

    void RunWCSPHStep()
    {
        shader.Dispatch(kernels.WCSPHKernels);
    }

    void RunPCISPHStep()
    {
        shader.Dispatch(kernels.PCISPHPrePressureKernels);
        
        for (int i = 0; i < pcisphSolverIterations; i++)
        {
            shader.Dispatch(kernels.PCISPHPressureKernels);
        }
        shader.Dispatch(kernels.PCISPHPostPressureKernels);
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

        shader.Dispatch(kernels.Scatter, kernels.CopyBack);
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
        physicsTimeStep = 1f / SolverSteps(pressureSolver);
        float deltaTime = physicsTimeStep * simulationSpeed;
        float particleSpacing = spawner.Size + spawner.Spacing;
        float particleMass = particleSpacing * particleSpacing * particleSpacing;
        float kernelConstant = 8f / (Mathf.PI * Mathf.Pow(smoothingRadius, 3));
        float gradConstant = 6 * kernelConstant / smoothingRadius;
        float speedOfSound = maxVelocity / Mathf.Sqrt(densityError);
        float B = restDensity * speedOfSound * speedOfSound / stiffness;
        float beta = deltaTime * deltaTime * particleMass * particleMass * 2 / (restDensity * restDensity);
        float delta = ComputeDelta(particleSpacing, beta, gradConstant) * deltaScale;

        object[] keyValues =
        {
            "deltaTime", deltaTime,
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
            "nearPressureMultiplier", nearPressureMultiplier,
            "beta", beta,
            "delta", delta
        };

        shader.SetValues(keyValues);
    }

    float ComputeDelta(float particleSpacing, float beta, float gradConstant)
    {
        Vector3 gradSum = Vector3.zero;
        float dotGradSum = 0f;

        Vector3 prototypePos = Vector3.zero;

        int range = Mathf.CeilToInt(2 * smoothingRadius / particleSpacing);

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                for (int z = -range; z <= range; z++)
                {
                    if (x == 0 && y == 0 && z == 0) continue;

                    Vector3 neighborPos = new Vector3(x, y, z) * particleSpacing;
                    Vector3 offset = prototypePos - neighborPos;
                    float r = offset.magnitude;

                    if (r >= 2 * smoothingRadius) continue;

                    Vector3 grad = CubicSplineGrad(offset, r, gradConstant);

                    gradSum += grad;
                    dotGradSum += Vector3.Dot(grad, grad);
                }
            }
        }

        float denominator = beta * (-Vector3.Dot(gradSum, gradSum) - dotGradSum);

        if (Mathf.Abs(denominator) < 1e-12)
        {
            return 0f;
        }

        return -1f / denominator;
    }

    Vector3 CubicSplineGrad(Vector3 offset, float r, float gradConstant)
    {
        if (r < 1e-12) return Vector3.zero;

        float q = r / smoothingRadius;
        float gradFactor = 0f;

        if (q < 1f)
        {
            gradFactor = gradConstant * (-3f * q + 2.25f * q * q);
        }
        else if (q < 2f)
        {
            float term = 2f - q;
            gradFactor = gradConstant * (-0.75f * term * term);
        }

        return offset * gradFactor / r;
    }

    int SolverSteps(Solver solver)
    {
        if (solver == Solver.WCSPH) return Constants.stableWCSPHStep;
        if (solver == Solver.IISPH) return Constants.stableIISPHStep;
        if (solver == Solver.PCISPH) return Constants.stablePCISPHStep;

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
        iisphSolverIterations = Mathf.Max(0, iisphSolverIterations);
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

    void UpdateDensityTexture()
    {
        densityTex = CreateDensityTexture();
    }

	public static RenderTexture CreateDensityTexture(int width, int height, FilterMode filterMode, GraphicsFormat format, string name = "Unnamed", DepthMode depthMode = DepthMode.None, bool useMipMaps = false)
		{
			RenderTexture texture = new RenderTexture(width, height, (int)depthMode);
			texture.graphicsFormat = format;
			texture.enableRandomWrite = true;
			texture.autoGenerateMips = false;
			texture.useMipMap = useMipMaps;
			texture.Create();

			texture.name = name;
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = filterMode;
			return texture;
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