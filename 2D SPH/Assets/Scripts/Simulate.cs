using System;
using System.Collections.Generic;
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
    [SerializeField] ComputeShader spatialCompute;
    [SerializeField] ComputeShader simCompute;
    [SerializeField] ComputeShader wcsphCompute;
    [SerializeField] ComputeShader iisphCompute;
    [SerializeField] ComputeShader pcisphCompute;

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

    int CalculateDensity;
    int UpdatePositions;
    int WriteDensities;
    int CalculateVelocityColour;
    int CalculateDensityColour;
    int CalculatePressureColour;

    /*
    Private properties
    */
    Spawn spawner;
    Draw drawer;
    bool started;
    float physicsTimeStep;
    float accumulator = 0f;
    int maxStepsPerFrame = 3;

    int instanceCount;
    float size;

    float simulationTime = 0;

    RenderTexture densityTex;

    BufferHelper commonBufferHelper;
    SpatialHashManager hashManager;
    WCSPHManager wcsphManager;
    IISPHManager iisphManager;
    PCISPHManager pcisphManager;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;

    public RenderTexture DensityTex => densityTex;

    public float SimulationSpeed
    {
        set
        {
            simulationSpeed = value;
            UpdateVariables();
        }
    }

    public float Gravity
    {
        set
        {
            gravity = value;
            UpdateVariables();
        }
    }

    public float DampingFactor
    {
        set
        {
            dampingFactor = value;
            UpdateVariables();
        }
    }

    public float WaveStrength
    {
        set
        {
            waveStrength = value;
            UpdateVariables();
        }
    }

    public float WavePeriod
    {
        set
        {
            wavePeriod = value;
            UpdateVariables();
        }
    }

    public float DensityFluctuation
    {
        set
        {
            densityError = value;
            UpdateVariables();
        }
    }

    public float Stiffness
    {
        set
        {
            stiffness = value;
            UpdateVariables();
        }
    }

    public float IISPHSolverIterations
    {
        set
        {
            iisphSolverIterations = Mathf.FloorToInt(value);
            UpdateVariables();
        }
    }

    public float PCISPHSolverIterations
    {
        set
        {
            pcisphSolverIterations = Mathf.FloorToInt(value);
            UpdateVariables();
        }
    }

    public float RestDensity
    {
        set
        {
            restDensity = value;
            UpdateVariables();
        }
    }

    public float DeltaScale
    {
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

        Utils.SetValues(new object[] { "gravity", gravityForce }, wcsphCompute, iisphCompute, pcisphCompute);
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
        // True if binNumber has changed
        bool rebindBuffers = hashManager.ScanAndScatter(binNumber);

        if (rebindBuffers) UpdateOffsets();

        simCompute.Dispatch(CalculateDensity, Utils.Constants.threadGroupSize, 1, 1);

        if (pressureSolver == Solver.IISPH)
            iisphManager.SolvePressure(iisphSolverIterations);
        if (pressureSolver == Solver.WCSPH)
        {
            wcsphManager.SolvePressure();
        }
        if (pressureSolver == Solver.PCISPH)
            pcisphManager.SolvePressure(pcisphSolverIterations);

        simCompute.Dispatch(UpdatePositions, Utils.Constants.threadGroupSize, 1, 1);

        UpdateColours();

        simulationTime += physicsTimeStep * simulationSpeed;
    }

    void UpdateOffsets()
    {
        ComputeBuffer newBuffer = hashManager.Buffers.RetrieveBuffer("Offsets");
        simCompute.SetBuffer(CalculateDensity, "Offsets", newBuffer);
        simCompute.SetBuffer(WriteDensities, "Offsets", newBuffer);

        wcsphManager.Buffers.UpdateBuffer("Offsets", newBuffer);
        iisphManager.Buffers.UpdateBuffer("Offsets", newBuffer);
        pcisphManager.Buffers.UpdateBuffer("Offsets", newBuffer);
    }

    void UpdateColours()
    {
        if (drawer.UseMarchingCubes) return; 

        Draw.Property propChoice = drawer.ColourProperty;

        if (propChoice == Draw.Property.Velocity) simCompute.Dispatch(CalculateVelocityColour, Utils.Constants.threadGroupSize, 1, 1);
        if (propChoice == Draw.Property.Density) simCompute.Dispatch(CalculateDensityColour, Utils.Constants.threadGroupSize, 1, 1);
        if (propChoice == Draw.Property.Pressure) simCompute.Dispatch(CalculatePressureColour, Utils.Constants.threadGroupSize, 1, 1);
    }

    void OnValidate()
    {
        ValidateInspectorProperties();

        if (!Application.isPlaying || !started) return;

        UpdateVariables();
        UpdateBoundary();
    }

    void OnDestroy()
    {
        hashManager?.Destroy();
        commonBufferHelper?.Destroy();
        iisphManager?.Destroy();
        pcisphManager?.Destroy();
    }

    void StartSimulation()
    {
        instanceCount = spawner.InstanceCount;
        size = spawner.Size;

        if (indexHash)
            binNumber = CalculateCellNumber();

        CreateManagers();

        InitialiseVariables();
        UpdateBoundary();
        BindExternalBuffers();
        InitialiseLeapFrogVelocities();

        started = true;
    }

    void CreateManagers()
    {
        Dictionary<string, ComputeBuffer> hashDependencies = new Dictionary<string, ComputeBuffer>
        {
            { "Velocities", null },
            { "Positions", null },
        };
        hashManager = new SpatialHashManager(spatialCompute, hashDependencies, binNumber, instanceCount);

        FindKernels();
        Dictionary<int, string[]> dependencies = new Dictionary<int, string[]>
        {
            { CalculateDensity, new string[] { "Densities", "Positions", "Offsets"} },
            { UpdatePositions, new string[] { "Velocities", "Positions" } },
            { WriteDensities, new string[] { "Densities", "Positions", "Offsets" } },
            { CalculateVelocityColour, new[] {"Colours", "Velocities"}},
            { CalculateDensityColour, new[] {"Colours", "Densities"}},
            { CalculatePressureColour, new[] {"Colours", "Pressures"}}
        };

        Dictionary<string, BufferInfo> bufferInfo = GenerateBufferInfo(instanceCount);
        Dictionary<string, ComputeBuffer> commonDependencies = new Dictionary<string, ComputeBuffer>
        {
            { "Offsets", hashManager.Buffers.RetrieveBuffer("Offsets") }
        };

        commonBufferHelper = new BufferHelper(simCompute, dependencies, bufferInfo, commonDependencies);

        hashManager.Buffers.UpdateBuffer("Velocities", commonBufferHelper.RetrieveBuffer("Velocities"));
        hashManager.Buffers.UpdateBuffer("Positions", commonBufferHelper.RetrieveBuffer("Positions"));

        
        Dictionary<string, ComputeBuffer> wcsphDependencies = new Dictionary<string, ComputeBuffer>
        {
            { "Offsets", hashManager.Buffers.RetrieveBuffer("Offsets") },
            { "Densities", commonBufferHelper.RetrieveBuffer("Densities") },
            { "Pressures", commonBufferHelper.RetrieveBuffer("Pressures") },
            { "Velocities", commonBufferHelper.RetrieveBuffer("Velocities") },
            { "Positions", commonBufferHelper.RetrieveBuffer("Positions") }
        };
        wcsphManager = new WCSPHManager(wcsphCompute, wcsphDependencies, instanceCount);

        Dictionary<string, ComputeBuffer> iisphDependencies = new Dictionary<string, ComputeBuffer>
        {
            { "Offsets", hashManager.Buffers.RetrieveBuffer("Offsets") },
            { "Densities", commonBufferHelper.RetrieveBuffer("Densities") },
            { "Pressures", commonBufferHelper.RetrieveBuffer("Pressures") },
            { "IntermediateAccelerations", commonBufferHelper.RetrieveBuffer("IntermediateAccelerations") },
            { "Velocities", commonBufferHelper.RetrieveBuffer("Velocities") },
            { "Positions", commonBufferHelper.RetrieveBuffer("Positions") }
        };

        iisphManager = new IISPHManager(iisphCompute, iisphDependencies, instanceCount);

        Dictionary<string, ComputeBuffer> pcisphDependencies = new Dictionary<string, ComputeBuffer>
        {
            { "Offsets", hashManager.Buffers.RetrieveBuffer("Offsets") },
            { "Densities", commonBufferHelper.RetrieveBuffer("Densities") },
            { "Pressures", commonBufferHelper.RetrieveBuffer("Pressures") },
            { "IntermediateAccelerations", commonBufferHelper.RetrieveBuffer("IntermediateAccelerations") },
            { "Velocities", commonBufferHelper.RetrieveBuffer("Velocities") },
            { "Positions", commonBufferHelper.RetrieveBuffer("Positions") }
        };

        pcisphManager = new PCISPHManager(pcisphCompute, pcisphDependencies, instanceCount);
    }

    Dictionary<string, BufferInfo> GenerateBufferInfo(int instanceCount)
    {
        Array velocities = GenerateVelocityData();
        Array positions = spawner.ExtractPositions();

        Dictionary<string, BufferInfo> bufferInfo = new Dictionary<string, BufferInfo>
        {
            { "Densities", new BufferInfo { Length = instanceCount * 3, ElementSize = sizeof(float) } },
            { "Pressures", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) } },
            { "IntermediateAccelerations", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) * 3 }},
            { "Velocities", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) * 3, InitData = velocities } },
            { "Positions", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) * 3 , InitData = positions} },
            { "Colours", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) * 3 }}
        };

        return bufferInfo;
    }

    void InitialiseLeapFrogVelocities()
    {
        object[] halfStep = new object[] { "deltaTime", physicsTimeStep * 0.5f };
        // Set half timestep for initialization
        Utils.SetValues(halfStep, spatialCompute, simCompute, wcsphCompute, iisphCompute, pcisphCompute);

        RunPhysicsStep();

        object[] fullStep = new object[] { "deltaTime", physicsTimeStep };
        Utils.SetValues(fullStep, spatialCompute, simCompute, wcsphCompute, iisphCompute, pcisphCompute);
    }

    void BindExternalBuffers()
    {
        drawer.BindBuffers(commonBufferHelper.RetrieveBuffer("Positions"), commonBufferHelper.RetrieveBuffer("Colours"));
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
        CalculateDensity = simCompute.FindKernel("CalculateDensity");
        UpdatePositions = simCompute.FindKernel("UpdatePositions");
        WriteDensities = simCompute.FindKernel("WriteDensities");
        CalculateVelocityColour = simCompute.FindKernel("CalculateVelocityColour");
        CalculateDensityColour = simCompute.FindKernel("CalculateDensityColour");
        CalculatePressureColour = simCompute.FindKernel("CalculatePressureColour");
    }

    void InitialiseVariables()
    {
        object[] keyValues =
        {
            "size", spawner.Size,
            "instanceCount", instanceCount
        };
        Utils.SetValues(keyValues, spatialCompute, simCompute, wcsphCompute, iisphCompute, pcisphCompute);

        UpdateMouseForce(Vector3.zero, 0, 0);
        UpdateVariables();
        UpdateBoundary();
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
        float delta = Utils.ComputeDelta(particleSpacing, beta, gradConstant, smoothingRadius) * deltaScale;

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
            "useIndex", indexHash ? 1 : 0,
        };

        Utils.SetValues(keyValues, simCompute, spatialCompute, wcsphCompute, iisphCompute, pcisphCompute);

        UpdateDensityTexture();
    }

    int SolverSteps(Solver solver)
    {
        if (solver == Solver.WCSPH) return Utils.Constants.stableWCSPHStep;
        if (solver == Solver.IISPH) return Utils.Constants.stableIISPHStep;
        if (solver == Solver.PCISPH) return Utils.Constants.stablePCISPHStep;

        return Utils.Constants.stableWCSPHStep;
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
        Vector3 effectiveBoundary = containerSize - Vector3.one * size;

        int maxX = Mathf.FloorToInt(effectiveBoundary.x / cellSize);
        int maxY = Mathf.FloorToInt(effectiveBoundary.y / cellSize);
        int maxZ = Mathf.FloorToInt(effectiveBoundary.z / cellSize);

        return (maxX + 1) * (maxY + 1) * (maxZ + 1);
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
        if (simCompute == null) return;

        Vector3 bounds = GetComponentInChildren<Container>().Boundary;
        float maxAxis = Mathf.Max(bounds.x, bounds.y, bounds.z);
        int width = Mathf.RoundToInt(bounds.x / maxAxis * densityTextureRes);
        int height = Mathf.RoundToInt(bounds.y / maxAxis * densityTextureRes);
        int depth = Mathf.RoundToInt(bounds.z / maxAxis * densityTextureRes);

        if (densityTex == null || densityTex.width != width || densityTex.height != height || densityTex.volumeDepth != depth)
        {
            if (densityTex != null) densityTex.Release();

            densityTex = Utils.CreateDensityTexture(width, height, depth);
            simCompute.SetTexture(WriteDensities, "DensityTex", densityTex);
            simCompute.SetInts("densityTexDims", width, height, depth);
        }
    }

    void DispatchTextureWrite()
    {
        int dispatchX = Mathf.CeilToInt(densityTex.width / 8f);
        int dispatchY = Mathf.CeilToInt(densityTex.height / 8f);
        int dispatchZ = Mathf.CeilToInt(densityTex.volumeDepth / 8f);

        simCompute.Dispatch(WriteDensities, dispatchX, dispatchY, dispatchZ);
    }

    public void UpdateMouseForce(Vector3 origin, float radius, float power)
    {
        if (simCompute == null) return;

        Utils.SetValues(new object[]
        {
            "mousePos", origin,
            "mouseRadius", radius,
            "power", power
        }, wcsphCompute, iisphCompute, pcisphCompute);
    }

    public void UpdateBoundary()
    {
        if (simCompute == null || spawner == null) return;

        float cellSize = 2f * smoothingRadius;

        Container container = GetComponentInChildren<Container>();

        // Calculate based on the actual boundary particles can reach
        float particleSize = spawner.Size;
        Vector3 effectiveBoundary = container.Boundary - Vector3.one * particleSize;

        int maxX = Mathf.FloorToInt(effectiveBoundary.x / cellSize);
        int maxY = Mathf.FloorToInt(effectiveBoundary.y / cellSize);
        int maxZ = Mathf.FloorToInt(effectiveBoundary.z / cellSize);

        object[] values = {
            "containerSize", container.Boundary,
            "maxCornerX", maxX,
            "maxCornerY", maxY,
            "maxCornerZ", maxZ
        };
        
        Utils.SetValues(values, spatialCompute, simCompute, wcsphCompute, iisphCompute, pcisphCompute);

        drawer.UpdateContainerSize(container.Boundary);
    }
}