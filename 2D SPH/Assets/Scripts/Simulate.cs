using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms;

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

    [Header("External forces")]
    [SerializeField] float initSpeed = 5f;
    [SerializeField] Vector2 gravity = new Vector2(0, -9.8f);
    [SerializeField] float dampingFactor = 0.9f;

    [Header("Pressure")]
    [SerializeField] float gasConstant = 1f;
    [SerializeField] float restDensity = 1f;

    [Header("Viscosity")]
    [SerializeField] float viscosityMultiplier = 1f;

    /*
    Private properties
    */
    static int threadGroupSize = 64;
    static int scanBlockSize = 128;  // Each scan block processes 128 elements (64 threads × 2)
    static int binNumber = 5000;

    int clearCountsKernel;
    int partitionKernel;
    int scanKernel;
    int scanBlockSumsKernel;
    int addBlockSumsKernel;
    int finalizeScanKernel;
    int scatterKernel;
    int densityKernel;
    int accelerationKernel;
    int velocityKernel;
    int positionKernel;
    Spawn spawner;
    bool started;
    float physicsTimeStep;
    float accumulator = 0f;
    int maxStepsPerFrame = 5;

    int instanceCount;

    ComputeBuffer countBuffer;
    ComputeBuffer offsetBuffer;
    ComputeBuffer blockSumsBuffer;
    ComputeBuffer localOffsetBuffer;
    ComputeBuffer positionBuffer;
    ComputeBuffer positionBufferA;
    ComputeBuffer positionBufferB;
    ComputeBuffer velocityBuffer;
    ComputeBuffer velocityBufferA;
    ComputeBuffer velocityBufferB;
    ComputeBuffer accelerationBuffer;
    ComputeBuffer densityBuffer;
    ComputeBuffer densityBufferA;
    ComputeBuffer densityBufferB;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;

    void Start()
    {
        spawner = GetComponent<Spawn>();

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
        computeShader.SetFloat("deltaTime", physicsTimeStep);

        ScanAndScatter();

        int threadGroups = Mathf.CeilToInt(instanceCount / (float)threadGroupSize);

        computeShader.Dispatch(densityKernel, threadGroups, 1, 1);

        computeShader.Dispatch(accelerationKernel, threadGroups, 1, 1);

        computeShader.Dispatch(positionKernel, threadGroups, 1, 1);

        ScanAndScatter();

        computeShader.Dispatch(densityKernel, threadGroups, 1, 1);

        computeShader.Dispatch(velocityKernel, threadGroups, 1, 1);
    }

    void ScanAndScatter()
    {
        int threadGroups = Mathf.CeilToInt(instanceCount / (float)threadGroupSize);

        computeShader.Dispatch(clearCountsKernel, Mathf.CeilToInt(binNumber / (float)threadGroupSize), 1, 1);
        computeShader.Dispatch(partitionKernel, threadGroups, 1, 1);

        HierarchicalScan();

        RebindBuffers();
        computeShader.Dispatch(scatterKernel, threadGroups, 1, 1);

        SwapBuffers();
        RebindBuffers();
    }

    void HierarchicalScan()
    {
        int numBlocks = Mathf.CeilToInt(binNumber / (float)scanBlockSize);

        // Phase 1: Local scan in each block (stores block sums in BlockSums buffer)
        computeShader.Dispatch(scanKernel, numBlocks, 1, 1);

        // Phase 2: If we have multiple blocks, scan the block sums themselves
        if (numBlocks > 1)
        {
            // For 1000 bins with scanBlockSize=128: numBlocks = 8
            computeShader.SetInt("numBlockSums", numBlocks);
            computeShader.Dispatch(scanBlockSumsKernel, 1, 1, 1);
        }

        // Phase 3: Add scanned block sums to each block's elements
        if (numBlocks > 1)
        {
            int addThreadGroups = Mathf.CeilToInt(binNumber / (float)threadGroupSize);
            computeShader.Dispatch(addBlockSumsKernel, addThreadGroups, 1, 1);
        }

        // Phase 4: Write final element (total particle count)
        computeShader.Dispatch(finalizeScanKernel, 1, 1, 1);
    }

    void OnValidate()
    {
        ValidateInspectorProperties();

        if (!Application.isPlaying || !started) return;

        UpdateVariables();
    }

    void OnDestroy()
    {
        if (countBuffer != null)
            countBuffer.Release();
        if (offsetBuffer != null)
            offsetBuffer.Release();
        if (blockSumsBuffer != null)
            blockSumsBuffer.Release();
        if (localOffsetBuffer != null)
            localOffsetBuffer.Release();
        if (positionBufferA != null)
            positionBufferA.Release();
        if (velocityBufferA != null)
            velocityBufferA.Release();
        if (densityBufferA != null)
            densityBufferA.Release();
        if (positionBufferB != null)
            positionBufferB.Release();
        if (velocityBufferB != null)
            velocityBufferB.Release();
        if (densityBufferB != null)
            densityBufferB.Release();
        if (accelerationBuffer != null)
            accelerationBuffer.Release();
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

    void SetupBuffers()
    {
        countBuffer = new ComputeBuffer(binNumber, sizeof(uint));

        offsetBuffer = new ComputeBuffer(binNumber + 1, sizeof(uint));

        // Calculate number of blocks needed for hierarchical scan
        int numBlocks = Mathf.CeilToInt(binNumber / (float)scanBlockSize);
        blockSumsBuffer = new ComputeBuffer(Mathf.Max(1, numBlocks), sizeof(uint));

        localOffsetBuffer = new ComputeBuffer(binNumber, sizeof(uint));

        Vector2[] positions = spawner.ExtractPositions();
        positionBufferA = new ComputeBuffer(positions.Length, sizeof(float) * 2);
        positionBufferA.SetData(positions);

        positionBufferB = new ComputeBuffer(positions.Length, sizeof(float) * 2);

        positionBuffer = positionBufferA;

        Vector2[] velocities = GenerateVelocityData();
        velocityBufferA = new ComputeBuffer(velocities.Length, sizeof(float) * 2);
        velocityBufferA.SetData(velocities);

        velocityBufferB = new ComputeBuffer(velocities.Length, sizeof(float) * 2);

        velocityBuffer = velocityBufferA;

        accelerationBuffer = new ComputeBuffer(spawner.InstanceCount, sizeof(float) * 2);

        float[] densities = new float[spawner.InstanceCount];
        densityBufferA = new ComputeBuffer(densities.Length, sizeof(float));
        densityBufferA.SetData(densities);

        densityBufferB = new ComputeBuffer(densities.Length, sizeof(float));

        densityBuffer = densityBufferA;
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
        accelerationKernel = computeShader.FindKernel("UpdateAccelerations");
        velocityKernel = computeShader.FindKernel("UpdateVelocities");
        positionKernel = computeShader.FindKernel("UpdatePositions");
    }

    void BindBuffers()
    {
        computeShader.SetBuffer(clearCountsKernel, "CellCounts", countBuffer);
        computeShader.SetBuffer(clearCountsKernel, "LocalOffsets", localOffsetBuffer);

        computeShader.SetBuffer(partitionKernel, "CellCounts", countBuffer);

        computeShader.SetBuffer(scanKernel, "Offsets", offsetBuffer);
        computeShader.SetBuffer(scanKernel, "CellCounts", countBuffer);
        computeShader.SetBuffer(scanKernel, "BlockSums", blockSumsBuffer);

        computeShader.SetBuffer(scanBlockSumsKernel, "BlockSums", blockSumsBuffer);

        computeShader.SetBuffer(addBlockSumsKernel, "Offsets", offsetBuffer);
        computeShader.SetBuffer(addBlockSumsKernel, "CellCounts", countBuffer);
        computeShader.SetBuffer(addBlockSumsKernel, "BlockSums", blockSumsBuffer);

        computeShader.SetBuffer(finalizeScanKernel, "Offsets", offsetBuffer);

        computeShader.SetBuffer(scatterKernel, "LocalOffsets", localOffsetBuffer);
        computeShader.SetBuffer(scatterKernel, "Offsets", offsetBuffer);

        computeShader.SetBuffer(accelerationKernel, "Offsets", offsetBuffer);
        computeShader.SetBuffer(accelerationKernel, "Accelerations", accelerationBuffer);

        computeShader.SetBuffer(velocityKernel, "Offsets", offsetBuffer);
        computeShader.SetBuffer(velocityKernel, "Accelerations", accelerationBuffer);

        computeShader.SetBuffer(positionKernel, "Accelerations", accelerationBuffer);

        RebindBuffers();
    }

    void RebindBuffers()
    {
        computeShader.SetBuffer(partitionKernel, "Positions", positionBuffer);

        ComputeBuffer inactivePosition = (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA;
        ComputeBuffer inactiveVelocity = (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA;
        ComputeBuffer inactiveDensity = (densityBuffer == densityBufferA) ? densityBufferB : densityBufferA;

        computeShader.SetBuffer(scatterKernel, "OldPositions", positionBuffer);
        computeShader.SetBuffer(scatterKernel, "NewPositions", inactivePosition);
        computeShader.SetBuffer(scatterKernel, "OldVelocities", velocityBuffer);
        computeShader.SetBuffer(scatterKernel, "NewVelocities", inactiveVelocity);
        computeShader.SetBuffer(scatterKernel, "OldDensities", densityBuffer);
        computeShader.SetBuffer(scatterKernel, "NewDensities", inactiveDensity);

        computeShader.SetBuffer(densityKernel, "Offsets", offsetBuffer);
        computeShader.SetBuffer(densityKernel, "Densities", densityBuffer);
        computeShader.SetBuffer(densityKernel, "Positions", positionBuffer);

        computeShader.SetBuffer(accelerationKernel, "Densities", densityBuffer);
        computeShader.SetBuffer(accelerationKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(accelerationKernel, "Positions", positionBuffer);

        computeShader.SetBuffer(velocityKernel, "Densities", densityBuffer);
        computeShader.SetBuffer(velocityKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(velocityKernel, "Positions", positionBuffer);

        computeShader.SetBuffer(positionKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(positionKernel, "Velocities", velocityBuffer);
    }

    void SwapBuffers()
    {
        // Swap which buffer is "active"
        positionBuffer = (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA;
        velocityBuffer = (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA;
        densityBuffer = (densityBuffer == densityBufferA) ? densityBufferB : densityBufferA;
    }

    void InitialiseVariables()
    {
        instanceCount = spawner.InstanceCount;
        computeShader.SetInt("threadGroupSize", threadGroupSize);
        computeShader.SetInt("tableSize", binNumber);
        computeShader.SetFloat("size", spawner.Size);
        computeShader.SetInt("instanceCount", instanceCount);
        UpdateMouseForce(Vector2.zero, 0, 0);
        UpdateVariables();

        FindKernels();
        BindBuffers();
    }

    void UpdateVariables()
    {
        Vector2 containerSize = GetComponentInChildren<Container>().Boundary;
        int gridX = Mathf.CeilToInt(containerSize.x / smoothingRadius);
        int gridY = Mathf.CeilToInt(containerSize.y / smoothingRadius);
        computeShader.SetInt("gridX", gridX);
        computeShader.SetInt("gridY", gridY);

        float kernelConstant = 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadius, 9f));
        computeShader.SetFloat("kernelConstant", kernelConstant);
        computeShader.SetFloat("smoothingRadius", smoothingRadius);
        computeShader.SetFloat("dampingFactor", dampingFactor);
        computeShader.SetVector("gravity", gravity);

        float pressureKernelGradConstant = 45 / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
        computeShader.SetFloat("pressureKernelGradConstant", pressureKernelGradConstant);
        computeShader.SetFloat("gasConstant", gasConstant);
        computeShader.SetFloat("restDensity", restDensity);

        float particleMass = spawner.Area * restDensity / instanceCount;
        computeShader.SetFloat("particleMass", particleMass);

        float viscosityKernelLapConstant = 45 / (Mathf.PI * Mathf.Pow(smoothingRadius, 6));
        computeShader.SetFloat("viscosityKernelLapConstant", viscosityKernelLapConstant);
        computeShader.SetFloat("viscosityMultiplier", viscosityMultiplier);
    }

    void ValidateInspectorProperties()
    {
        simulationSpeed = Mathf.Clamp(simulationSpeed, 0, 1);
        initSpeed = Mathf.Max(0, initSpeed);
        dampingFactor = Mathf.Max(0, dampingFactor);
        smoothingRadius = Mathf.Max(0.01f, smoothingRadius);
        gasConstant = Mathf.Max(0, gasConstant);
        restDensity = Mathf.Max(0.01f, restDensity);
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
        computeShader.SetVector("origin", origin);
        computeShader.SetFloat("mouseRadius", radius);
        computeShader.SetFloat("power", power);
    }

    public void UpdateBoundary()
    {
        computeShader.SetVector("containerSize", GetComponentInChildren<Container>().Boundary);
    }
}