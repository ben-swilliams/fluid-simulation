using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
    [SerializeField] int binNumber = 200000;
    [SerializeField] bool indexHash = true;
    
    [Header("Marching cubes")]
    [SerializeField] int densityTextureRes = 100;

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
            if (drawer.UseMarchingCubes) DispatchTextureWrite();
        }

        drawer.DrawFrame(densityTex, started);
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
        if (drawer.UseMarchingCubes) return; 

        Draw.Property propChoice = drawer.ColourProperty;

        if (propChoice == Draw.Property.Velocity) shader.Dispatch(kernels.CalculateVelocityColour);
        if (propChoice == Draw.Property.Density) shader.Dispatch(kernels.CalculateDensityColour);
        if (propChoice == Draw.Property.Pressure) shader.Dispatch(kernels.CalculatePressureColour);
    }

    void ScanAndScatter()
    {
        int clearCountsGroupNum = Mathf.CeilToInt(binNumber / (float)Constants.threadGroupSize);
        shader.Dispatch(true, clearCountsGroupNum, kernels.ClearCounts);

        shader.Dispatch(kernels.Partition);

        HierarchicalScan();

        shader.Dispatch(kernels.Scatter, kernels.CopyBack);
    }

    void HierarchicalScan()
    {
        int numBlocks = Mathf.CeilToInt(binNumber / (float)Constants.scanBlockSize);

        // Phase 1: Local scan in each block (stores block sums in BlockSums buffer)
        shader.Dispatch(true, numBlocks, kernels.Scan);

        // Phase 2: If we have multiple blocks, scan the block sums themselves
        if (numBlocks > 1)
        {
            int numSuperBlocks = Mathf.CeilToInt(numBlocks / (float)Constants.scanBlockSize);

            shader.SetValues(new object[] { "numBlockSums", numBlocks, "numSuperBlocks", numSuperBlocks });
            shader.Dispatch(true, numSuperBlocks, kernels.ScanBlockSums);

            // Phase 2.5: If we have multiple super-blocks, scan them (three-level scan)
            if (numSuperBlocks > 1)
            {
                shader.Dispatch(true, 1, kernels.ScanSuperBlockSums);
            }

            // Phase 2.75: Add scanned super-block sums to BlockSums
            if (numSuperBlocks > 1)
            {
                int addSuperThreadGroups = Mathf.CeilToInt(numBlocks / (float)Constants.threadGroupSize);
                shader.Dispatch(true, addSuperThreadGroups, kernels.AddSuperBlockSums);
            }
        }

        // Phase 3: Add scanned block sums to each block's elements
        if (numBlocks > 1)
        {
            int addThreadGroups = Mathf.CeilToInt(binNumber / (float)Constants.threadGroupSize);
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

        if (indexHash)
            binNumber = CalculateCellNumber();

        shader.InitialiseCount(instanceCount);
        shader.SetupBuffers(spawner.ExtractPositions(), GenerateVelocityData(), binNumber);

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
            "size", spawner.Size,
            "instanceCount", instanceCount
        };
        shader.SetValues(keyValues);

        FindKernels();

        UpdateMouseForce(Vector3.zero, 0, 0);
        UpdateVariables();
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
            "delta", delta,
            "tableSize", binNumber,
            "useIndex", indexHash ? 1 : 0
        };

        shader.SetValues(keyValues);

        shader.SetupBinBuffers(binNumber);
        shader.BindStaticBuffers(kernels);

        UpdateBoundary();
        UpdateDensityTexture();
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

    public void ValidateInspectorProperties()
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
        binNumber = indexHash ? CalculateCellNumber() : Mathf.Max(1, binNumber);

        if (started) UpdateVariables();
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

    int CalculateCellNumber()
    {
        Vector3 containerSize = GetComponentInChildren<Container>().Boundary;
        float cellSize = 2f * smoothingRadius;

        // Calculate grid dimensions (number of cells in each axis)
        int gridX = Mathf.CeilToInt(containerSize.x / cellSize);
        int gridY = Mathf.CeilToInt(containerSize.y / cellSize);
        int gridZ = Mathf.CeilToInt(containerSize.z / cellSize);

        int cellCount = gridX * gridY * gridZ;

        return cellCount;
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
        if (shader == null) return;

        Vector3 bounds = GetComponentInChildren<Container>().Boundary;
        float maxAxis = Mathf.Max(bounds.x, bounds.y, bounds.z);
        int width = Mathf.RoundToInt(bounds.x / maxAxis * densityTextureRes);
        int height = Mathf.RoundToInt(bounds.y / maxAxis * densityTextureRes);
        int depth = Mathf.RoundToInt(bounds.z / maxAxis * densityTextureRes);

        if (densityTex == null || densityTex.width != width || densityTex.height != height || densityTex.volumeDepth != depth)
        {
            if (densityTex != null) densityTex.Release();

            densityTex = CreateDensityTexture(width, height, depth);
            shader.SetTexture(kernels.WriteDensities, "DensityTex", densityTex);
            shader.SetInts("densityTexDims", width, height, depth);
        }
    }

    void DispatchTextureWrite()
    {
        int dispatchX = Mathf.CeilToInt(densityTex.width / 8f);
        int dispatchY = Mathf.CeilToInt(densityTex.height / 8f);
        int dispatchZ = Mathf.CeilToInt(densityTex.volumeDepth / 8f);

        shader.Dispatch(kernels.WriteDensities, dispatchX, dispatchY, dispatchZ);
    }

	public static RenderTexture CreateDensityTexture(int width, int height, int depth)
		{
            RenderTexture texture = new RenderTexture(width, height, 0);
            texture.graphicsFormat = GraphicsFormat.R16_SFloat;
            texture.volumeDepth = depth;
            texture.enableRandomWrite = true;
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.Create();

			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Bilinear;
			texture.name = "DensityMap";

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

        int maxX = Mathf.CeilToInt(container.Boundary.x / cellSize) - 1;
        int maxY = Mathf.CeilToInt(container.Boundary.y / cellSize) - 1;
        int maxZ = Mathf.CeilToInt(container.Boundary.z / cellSize) - 1;

        shader.SetValues(new object[]
        {
            "containerSize", container.Boundary,
            "maxCornerX", maxX,
            "maxCornerY", maxY,
            "maxCornerZ", maxZ
        });

        drawer.UpdateContainerSize(container.Boundary);
    }
}