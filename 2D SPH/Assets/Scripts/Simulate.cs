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
    [SerializeField] int physicsStepsPerSecond = 300;
    [SerializeField] float simulationSpeed = 1f;
    [SerializeField] float smoothingRadius = 1f;
    [SerializeField] float velocitySmoothing = 0f;
    [SerializeField] int stepSize = 10;

    [Header("External forces")]
    [SerializeField] float initSpeed = 5f;
    [SerializeField] Vector2 gravity = new Vector2(0, -9.8f);
    [SerializeField] float dampingFactor = 0.9f;

    [Header("Pressure")]
    [SerializeField] float restDensity = 1f;
    [SerializeField] float relaxationFactor = 0.5f;

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
    int pressureFinaliseIterationKernel;
    int velocityKernel;
    int positionKernel;


    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;
    public ShaderHelper Shader => shader;

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
            AdvanceFrame();
        }
    }

    void AdvanceFrame()
    {
        accumulator += Time.deltaTime * simulationSpeed;

        int stepsThisFrame = 0;
        while (accumulator >= physicsTimeStep && stepsThisFrame < maxStepsPerFrame)
        {
            shader.SetValues(new object[] { "deltaTime", physicsTimeStep });
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
        shader.BindDynamicBuffers();

        ScanAndScatter();

        shader.Dispatch(densityKernel, intermediateAccelerationKernel, intermediateVelocityAndDKernel, intermediateDensityAndAKernel, zeroPressuresKernel);

        int minIterations = 4;

        for (int l = 0; l < minIterations; l++)
        {
            shader.Dispatch(pressureSumIterationKernel, pressureConvergeIterationKernel, pressureFinaliseIterationKernel);
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

        // RunPhysicsStep();
    }

    void BindExternalBuffers()
    {
        Draw drawer = GetComponent<Draw>();
        drawer.BindBuffers(shader.PositionBuffer, shader.VelocityBuffer, shader.Densities, shader.Pressures);
        drawer.UpdateVariables(spawner.Size, restDensity);
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
        pressureFinaliseIterationKernel = computeShader.FindKernel("PressureFinaliseIteration");
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
                          pressureFinaliseIterationKernel,
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

        float particleMass = restDensity * smoothingRadius * smoothingRadius;
        float kernelConstant = 10f / (7 * Mathf.PI * smoothingRadius * smoothingRadius);
        float gradConstant = kernelConstant / smoothingRadius;

        object[] keyValues =
        {
            "gridX", gridX,
            "gridY", gridY,
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
            "gradConstant", gradConstant
        };

        shader.SetValues(keyValues);
        GetComponent<Draw>().UpdateVariables(-1, restDensity);
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