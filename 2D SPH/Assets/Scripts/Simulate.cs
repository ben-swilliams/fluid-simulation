using System;
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
    static int binNumber = 1000;

    int clearCountsKernel;
    int partitionKernel;
    int scanKernel;
    int scanBlockSumsKernel;
    int addBlockSumsKernel;
    int finalizeScanKernel;
    int gravityKernel;
    int pressureKernel;
    int densityKernel;
    int viscosityKernel;
    int positionKernel;
    Spawn spawner;
    bool started;
    int frameRateTarget = 120;
    float dtTarget;

    int instanceCount;

    ComputeBuffer indexBuffer;
    ComputeBuffer countBuffer;
    ComputeBuffer offsetBuffer;
    ComputeBuffer blockSumsBuffer;
    ComputeBuffer positionBuffer;
    ComputeBuffer predictedPositionBuffer;
    ComputeBuffer velocityBuffer;
    ComputeBuffer densityBuffer;

    /*
    Public getters
    */
    public bool Started => started;
    public float SmoothingRadius => smoothingRadius;
    
    void Start()
    {
        spawner = GetComponent<Spawn>();
        dtTarget = 1f / frameRateTarget;
    }

    void Update()
    {
        HandleKeyPresses();

        if (started)
        {
            float dt = Mathf.Min(dtTarget, Time.deltaTime);
            computeShader.SetFloat("deltaTime", dt * simulationSpeed);

            int threadGroups = Mathf.CeilToInt(instanceCount / (float)threadGroupSize);

            computeShader.Dispatch(clearCountsKernel, Mathf.CeilToInt(binNumber / (float)threadGroupSize), 1, 1);
            computeShader.Dispatch(partitionKernel, threadGroups, 1, 1);

            HierarchicalScan(binNumber);

            computeShader.Dispatch(gravityKernel, threadGroups, 1, 1);
            computeShader.Dispatch(densityKernel, threadGroups, 1, 1);
            computeShader.Dispatch(pressureKernel, threadGroups, 1, 1);
            computeShader.Dispatch(viscosityKernel, threadGroups, 1, 1);
            computeShader.Dispatch(positionKernel, threadGroups, 1, 1);
        }
    }

    void HierarchicalScan(int size)
    {
        // Each block processes scanBlockSize elements
        int numBlocks = Mathf.CeilToInt(size / (float)scanBlockSize);

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
            int addThreadGroups = Mathf.CeilToInt(size / (float)threadGroupSize);
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
        if (indexBuffer != null)
            indexBuffer.Release();
        if (countBuffer != null)
            countBuffer.Release();
        if (offsetBuffer != null)
            offsetBuffer.Release();
        if (blockSumsBuffer != null)
            blockSumsBuffer.Release();
        if (positionBuffer != null)
                positionBuffer.Release();
        if (predictedPositionBuffer != null)
            predictedPositionBuffer.Release();
        if (velocityBuffer != null)
            velocityBuffer.Release();
        if (densityBuffer != null)
            densityBuffer.Release();
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
        uint[] indices = new uint[spawner.InstanceCount];
        indexBuffer = new ComputeBuffer(indices.Length, sizeof(uint));
        indexBuffer.SetData(indices);

        countBuffer = new ComputeBuffer(binNumber, sizeof(uint));

        offsetBuffer = new ComputeBuffer(binNumber + 1, sizeof(uint));

        // Calculate number of blocks needed for hierarchical scan
        int numBlocks = Mathf.CeilToInt(binNumber / (float)scanBlockSize);
        blockSumsBuffer = new ComputeBuffer(Mathf.Max(1, numBlocks), sizeof(uint));

        Vector2[] positions = spawner.ExtractPositions();
        positionBuffer = new ComputeBuffer(positions.Length, sizeof(float) * 2);
        positionBuffer.SetData(positions);

        Vector2[] predictedPositions = new Vector2[positions.Length];
        predictedPositionBuffer = new ComputeBuffer(positions.Length, sizeof(float) * 2);
        predictedPositionBuffer.SetData(predictedPositions);

        Vector2[] velocities = GenerateVelocityData();
        velocityBuffer = new ComputeBuffer(velocities.Length, sizeof(float) * 2);
        velocityBuffer.SetData(velocities);

        float[] densities = new float[spawner.InstanceCount];
        densityBuffer = new ComputeBuffer(densities.Length, sizeof(float));
        densityBuffer.SetData(densities);
    }

    void FindKernels()
    {
        clearCountsKernel = computeShader.FindKernel("ZeroCounts");
        partitionKernel = computeShader.FindKernel("Partition");
        scanKernel = computeShader.FindKernel("Scan");
        scanBlockSumsKernel = computeShader.FindKernel("ScanBlockSums");
        addBlockSumsKernel = computeShader.FindKernel("AddBlockSums");
        finalizeScanKernel = computeShader.FindKernel("FinalizeScan");
        gravityKernel = computeShader.FindKernel("Gravity");
        pressureKernel = computeShader.FindKernel("Pressure");
        densityKernel = computeShader.FindKernel("Density");
        viscosityKernel = computeShader.FindKernel("Viscosity");
        positionKernel = computeShader.FindKernel("UpdatePositions");
    }

    void BindBuffers()
    {
        computeShader.SetBuffer(clearCountsKernel, "CellCounts", countBuffer);
        computeShader.SetBuffer(partitionKernel, "GridIndices", indexBuffer);
        computeShader.SetBuffer(partitionKernel, "CellCounts", countBuffer);
        computeShader.SetBuffer(partitionKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(scanKernel, "Offsets", offsetBuffer);
        computeShader.SetBuffer(scanKernel, "CellCounts", countBuffer);
        computeShader.SetBuffer(scanKernel, "BlockSums", blockSumsBuffer);
        computeShader.SetBuffer(scanBlockSumsKernel, "BlockSums", blockSumsBuffer);
        computeShader.SetBuffer(addBlockSumsKernel, "Offsets", offsetBuffer);
        computeShader.SetBuffer(addBlockSumsKernel, "CellCounts", countBuffer);
        computeShader.SetBuffer(addBlockSumsKernel, "BlockSums", blockSumsBuffer);
        computeShader.SetBuffer(finalizeScanKernel, "Offsets", offsetBuffer);
        computeShader.SetBuffer(gravityKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(gravityKernel, "PredictedPositions", predictedPositionBuffer);
        computeShader.SetBuffer(gravityKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(densityKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(densityKernel, "PredictedPositions", predictedPositionBuffer);
        computeShader.SetBuffer(densityKernel, "Densities", densityBuffer);
        computeShader.SetBuffer(pressureKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(pressureKernel, "PredictedPositions", predictedPositionBuffer);
        computeShader.SetBuffer(pressureKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(pressureKernel, "Densities", densityBuffer);
        computeShader.SetBuffer(viscosityKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(viscosityKernel, "PredictedPositions", predictedPositionBuffer);
        computeShader.SetBuffer(viscosityKernel, "Velocities", velocityBuffer);
        computeShader.SetBuffer(viscosityKernel, "Densities", densityBuffer);
        computeShader.SetBuffer(positionKernel, "Positions", positionBuffer);
        computeShader.SetBuffer(positionKernel, "Velocities", velocityBuffer);
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

        if (UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame && started)
        {
            uint[] counts = new uint[binNumber];
            countBuffer.GetData(counts);

            uint[] offsets = new uint[binNumber + 1];
            offsetBuffer.GetData(offsets);

            int numBlocks = Mathf.CeilToInt(binNumber / 128f);
            uint[] blockSums = new uint[numBlocks];
            blockSumsBuffer.GetData(blockSums);

            Debug.Log("Counts: " + string.Join(", ", counts));
            Debug.Log("Offsets: " + string.Join(", ", offsets));
            Debug.Log("BlockSums: " + string.Join(", ", blockSums));
        }
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