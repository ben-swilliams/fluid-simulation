using System;
using UnityEditor.PackageManager;
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
    [SerializeField] int physicsStepsPerSecond = 300;
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
    int maxStepsPerFrame = 5;

    int instanceCount;

    KernelSet kernels;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;
    public ShaderHelper Shader => shader;

    void Start()
    {
        spawner = GetComponent<Spawn>();
        drawer = GetComponent<Draw>();
        
        shader = new ShaderHelper(computeShader);

        physicsTimeStep = 1f / physicsStepsPerSecond;
    }

    void Update()
    {
        HandleKeyPresses();

        if (started)
        {
            UpdateWaveForce();
            AdvanceFrame();
        }

        drawer.DrawFrame();
    }

    void UpdateWaveForce()
    {

        float angle = wavePeriod * Time.frameCount;
        Vector3 gravityForce = new Vector3(waveStrength * Mathf.Cos(angle), gravity, waveStrength * Mathf.Sin(angle));
        shader.SetValues(new object[] { "gravity", gravityForce });
    }

    void AdvanceFrame()
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

    void RunPhysicsStep()
    {
        shader.BindDynamicBuffers(kernels);

        ScanAndScatter();

        if (pressureSolver == Solver.IISPH)
            RunIISPHStep();
        if (pressureSolver == Solver.WCSPH)
            RunWCSPHStep();
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
        // InitialiseLeapFrogVelocities();

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
        drawer.BindBuffers(shader.PositionBuffer, shader.VelocityBuffer, shader.Densities, shader.Pressures);
        drawer.UpdateVariables(spawner.Size, restDensity);
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
        physicsTimeStep = 1f / physicsStepsPerSecond;

        object[] keyValues =
        {
            "deltaTime", physicsTimeStep,
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
            "B", B
        };

        shader.SetValues(keyValues);
        drawer.UpdateVariables(restDensity: restDensity);
    }

    void ValidateInspectorProperties()
    {
        simulationSpeed = Mathf.Clamp(simulationSpeed, 0, 1);
        physicsStepsPerSecond = Mathf.Max(1, physicsStepsPerSecond);
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

        if (UnityEngine.InputSystem.Keyboard.current.downArrowKey.wasPressedThisFrame)
            simulationSpeed = Mathf.Max(0, simulationSpeed - 0.1f);

        if (UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame)
            simulationSpeed = Mathf.Min(1, simulationSpeed + 0.1f);

        if (UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            if (simulationSpeed != 0) return;
            simulationSpeed = 1;
            for (int _ = 0; _ < stepSize; _++) AdvanceFrame();
            simulationSpeed = 0;
        }

        if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
            simulationSpeed = simulationSpeed == 1 ? 0 : 1;

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

        shader.SetValues(new object[]
        {
            "containerSize", GetComponentInChildren<Container>().Boundary
        });
    }
}