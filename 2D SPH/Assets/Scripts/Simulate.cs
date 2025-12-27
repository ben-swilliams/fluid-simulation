using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Common;

public class Simulate : MonoBehaviour
{
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

    /*
    Private properties
    */
    Spawn spawner;
    Draw drawer;
    Compute compute;
    bool started;
    float physicsTimeStep;
    float accumulator = 0f;
    int maxStepsPerFrame = 3;

    int instanceCount;

    float simulationTime = 0;

    RenderTexture densityTex;

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
    }

    void Update()
    {
        HandleKeyPresses();

        if (started)
        {
            AdvanceFrame();
            if (drawer.DrawTarget != Draw.DrawMethod.Particles) DispatchTextureWrite();
        }

        drawer.DrawFrame(densityTex, started);
    }

    void UpdateWaveForce()
    {
        float angle = wavePeriod * simulationTime;
        Vector3 gravityForce = new Vector3(waveStrength * Mathf.Cos(angle), gravity, waveStrength * Mathf.Sin(angle));

        compute.SetValues(new object[] { "gravity", gravityForce }, wcsphCompute, iisphCompute, pcisphCompute);
    }

    void AdvanceFrame()
    {
        UpdateWaveForce();
        accumulator += Time.deltaTime;

        int stepsThisFrame = 0;
        while (accumulator >= physicsTimeStep && stepsThisFrame < maxStepsPerFrame)
        {
            compute.RunPhysicsStep(binNumber, pressureSolver, pressureSolver == Solver.IISPH ? iisphSolverIterations : pcisphSolverIterations);
            compute.UpdateColours(drawer.ColourProperty);

            simulationTime += physicsTimeStep * simulationSpeed;
            accumulator -= physicsTimeStep;
            stepsThisFrame++;
        }

        // Prevent spiral of death - if we're too far behind, reset accumulator
        if (accumulator > physicsTimeStep * maxStepsPerFrame)
        {
            accumulator = 0f;
        }
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
        compute?.Destroy();
    }

    void StartSimulation()
    {
        instanceCount = spawner.InstanceCount;

        if (indexHash)
            binNumber = Utils.CalculateCellNumber(GetComponentInChildren<Container>().Boundary, smoothingRadius);

        compute = new Compute(spatialCompute, simCompute, wcsphCompute, iisphCompute, pcisphCompute);
        compute.Initialise(binNumber, spawner.ExtractPositions(), Utils.GenerateVelocityData(instanceCount, initSpeed));

        InitialiseVariables();
        UpdateBoundary();
        BindExternalBuffers();
        InitialiseLeapFrogVelocities();

        started = true;
    }

    

    void InitialiseLeapFrogVelocities()
    {
        object[] halfStep = new object[] { "deltaTime", physicsTimeStep * 0.5f };
        // Set half timestep for initialization
        compute.SetValues(halfStep, spatialCompute, simCompute, wcsphCompute, iisphCompute, pcisphCompute);

        compute.RunPhysicsStep(binNumber, pressureSolver, pressureSolver == Solver.IISPH ? iisphSolverIterations : pcisphSolverIterations);

        object[] fullStep = new object[] { "deltaTime", physicsTimeStep };
        compute.SetValues(fullStep, spatialCompute, simCompute, wcsphCompute, iisphCompute, pcisphCompute);
    }

    void BindExternalBuffers()
    {
        drawer.BindBuffers(compute.Positions, compute.Colours);
        drawer.UpdateSize(spawner.Size);
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

        drawer.BindTexture(densityTex);
    }

    void UpdateVariables()
    {
        physicsTimeStep = 1f / Utils.SolverSteps(pressureSolver);
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

        UpdateBoundary();
        UpdateDensityTexture();
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
        binNumber = indexHash ? Utils.CalculateCellNumber(GetComponentInChildren<Container>().Boundary, smoothingRadius) : Mathf.Max(1, binNumber);
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
        if (compute == null) return;

        Vector3 bounds = GetComponentInChildren<Container>().Boundary;
        float maxAxis = Mathf.Max(bounds.x, bounds.y, bounds.z);
        int width = Mathf.RoundToInt(bounds.x / maxAxis * densityTextureRes);
        int height = Mathf.RoundToInt(bounds.y / maxAxis * densityTextureRes);
        int depth = Mathf.RoundToInt(bounds.z / maxAxis * densityTextureRes);

        if (densityTex == null || densityTex.width != width || densityTex.height != height || densityTex.volumeDepth != depth)
        {
            if (densityTex != null) densityTex.Release();

            densityTex = Utils.CreateDensityTexture(width, height, depth);
            compute.SetTexture(densityTex);
        }
    }

    void DispatchTextureWrite()
    {
        int dispatchX = Mathf.CeilToInt(densityTex.width / 8f);
        int dispatchY = Mathf.CeilToInt(densityTex.height / 8f);
        int dispatchZ = Mathf.CeilToInt(densityTex.volumeDepth / 8f);

        compute.WriteToDensityTexture(dispatchX, dispatchY, dispatchZ);
    }

    public void UpdateMouseForce(Vector3 origin, float radius, float power)
    {
        if (compute == null) return;

        compute.SetValues(new object[]
        {
            "mousePos", origin,
            "mouseRadius", radius,
            "power", power
        }, wcsphCompute, iisphCompute, pcisphCompute);
    }

    public void UpdateBoundary()
    {
        if (compute == null || spawner == null) return;

        float cellSize = 2f * smoothingRadius;

        Container container = GetComponentInChildren<Container>();

        int maxX = Mathf.CeilToInt(container.Boundary.x / cellSize) - 1;
        int maxY = Mathf.CeilToInt(container.Boundary.y / cellSize) - 1;
        int maxZ = Mathf.CeilToInt(container.Boundary.z / cellSize) - 1;

        object[] values = {
            "containerSize", container.Boundary,
            "maxCornerX", maxX,
            "maxCornerY", maxY,
            "maxCornerZ", maxZ
        };
        
        compute.SetValues(values, spatialCompute, simCompute, wcsphCompute, iisphCompute, pcisphCompute);

        drawer.UpdateContainerSize(container.Boundary);
    }
}