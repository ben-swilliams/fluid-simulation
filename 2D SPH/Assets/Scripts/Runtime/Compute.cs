using System;
using System.Collections.Generic;
using Common;
using UnityEngine;

public class Compute : Tweakable
{
    // Shaders
    [SerializeField] ComputeShader spatialCompute;
    [SerializeField] ComputeShader simCompute;
    [SerializeField] ComputeShader wcsphCompute;
    [SerializeField] ComputeShader iisphCompute;
    [SerializeField] ComputeShader pcisphCompute;

    [Header("Kernels")]
    [SerializeField] Kernel pressureKernel;
    [SerializeField] Kernel nearPressureKernel;
    [SerializeField] Kernel densityKernel;
    [SerializeField] Kernel nearDensityKernel;
    [SerializeField] Kernel surfaceTensionKernel;
    [SerializeField] Kernel viscosityKernel;
    [SerializeField] Kernel xsphKernel;

    [Header("Time")]
    [SerializeField] float simulationSpeed = 1;
    [Header("External forces")]
    [SerializeField] float maxVelocity = 5f;
    [SerializeField] float gravity = -9.8f;
    [SerializeField] float dampingFactor = 0.95f;
    [SerializeField] float wavePeriod = 1f;
    [SerializeField] float waveStrength = 0f;
    [SerializeField] float velocitySmoothing = 0.01f;

    [Header("Pressure")]
    [SerializeField] Solver pressureSolver = Solver.IISPH;
    [SerializeField] float restDensity = 1f;
    [SerializeField] float nearPressureMultiplier = 0f;
    
    [Header("IISPH Pressure")]
    [SerializeField] float relaxationFactor = 0.5f;
    [SerializeField] int iisphSolverIterations = 4;

    [Header("WCSPH Pressure")]
    [SerializeField, Range(0.001f, 1f)] float densityError = 0.1f;
    [SerializeField] float stiffness = 7f;

    [Header("PCISPH Pressure")]
    [SerializeField] float deltaScale = 0.01f;
    [SerializeField] int pcisphSolverIterations = 3;

    [Header("Viscosity")]
    [SerializeField] float viscosityMultiplier = 0f;
    [Header("Surface tension")]
    [SerializeField] float surfaceTensionMultiplier = 0f;

    // Common kernels
    int CalculateDensity;
    int UpdatePositions;
    int WriteDensities;
    int CalculateVelocityColour;
    int CalculateDensityColour;
    int CalculatePressureColour;

    // Managers
    SpatialHashManager hashManager;
    WCSPHManager wcsphManager;
    IISPHManager iisphManager;
    PCISPHManager pcisphManager;

    // Buffer helper
    BufferHelper commonBufferHelper;

    float simulationTime = 0;
    float physicsTimeStep;

    int groupCount;

    [HideInInspector]
    public float smoothingRadius;

    public float RestDensity => restDensity;
    public float SimulationSpeed => simulationSpeed;
    public float PhysicsTimeStep => physicsTimeStep;
    public Solver PressureSolver => pressureSolver;
    public ComputeBuffer Positions => commonBufferHelper.RetrieveBuffer("Positions");
    public ComputeBuffer Colours => commonBufferHelper.RetrieveBuffer("Colours");

    void InstantiateManagers(int instanceCount, int binNumber, Dictionary<string, BufferInfo> bufferInfo)
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

            Dictionary<string, ComputeBuffer> commonDependencies = new Dictionary<string, ComputeBuffer>
            {
                { "Offsets", hashManager.Buffers.RetrieveBuffer("Offsets") }
            };

            commonBufferHelper = new BufferHelper(simCompute, dependencies, bufferInfo, commonDependencies);

            hashManager.Buffers.UpdateBuffer("Velocities", commonBufferHelper.RetrieveBuffer("Velocities"));
            hashManager.Buffers.UpdateBuffer("Positions", commonBufferHelper.RetrieveBuffer("Positions"));

            Dictionary<string, ComputeBuffer> pressureDependencies = new Dictionary<string, ComputeBuffer>
            {
                { "Offsets", hashManager.Buffers.RetrieveBuffer("Offsets") },
                { "Densities", commonBufferHelper.RetrieveBuffer("Densities") },
                { "Pressures", commonBufferHelper.RetrieveBuffer("Pressures") },
                { "Velocities", commonBufferHelper.RetrieveBuffer("Velocities") },
                { "Positions", commonBufferHelper.RetrieveBuffer("Positions") }
            };
            wcsphManager = new WCSPHManager(wcsphCompute, pressureDependencies, instanceCount);

            pressureDependencies.Add("IntermediateAccelerations", commonBufferHelper.RetrieveBuffer("IntermediateAccelerations"));

            iisphManager = new IISPHManager(iisphCompute, pressureDependencies, instanceCount);
            pcisphManager = new PCISPHManager(pcisphCompute, pressureDependencies, instanceCount);
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

    Dictionary<string, BufferInfo> GenerateBufferInfo(Array positions, Array velocities)
    {
        int instanceCount = positions.Length;

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

    void UpdateOffsets()
    {
        ComputeBuffer newBuffer = hashManager.Buffers.RetrieveBuffer("Offsets");
        simCompute.SetBuffer(CalculateDensity, "Offsets", newBuffer);
        simCompute.SetBuffer(WriteDensities, "Offsets", newBuffer);

        wcsphManager.Buffers.UpdateBuffer("Offsets", newBuffer);
        iisphManager.Buffers.UpdateBuffer("Offsets", newBuffer);
        pcisphManager.Buffers.UpdateBuffer("Offsets", newBuffer);
    }

    void OnValidate()
    {
        ValidateInspectorProperties();

        UpdateVariables();
    }

    void UpdateWaveForce()
    {
        float angle = wavePeriod * simulationTime;
        Vector3 gravityForce = new Vector3(waveStrength * Mathf.Cos(angle), gravity, waveStrength * Mathf.Sin(angle));

        SetPressureValues(new object[] { "gravity", gravityForce });
    }

    void UpdateKernelConstants()
    {
        object[] keyValues =
        {
            "cubicKernelConstant", Constants.CubicKernelConstant(smoothingRadius),
            "spikyKernelConstant", Constants.SpikyKernelConstant(smoothingRadius),
            "poly6KernelConstant", Constants.Poly6KernelConstant(smoothingRadius)
        };

        SetValues(keyValues);
    }

    public void SetSimSpeed(float newSpeed)
    {
        simulationSpeed = Mathf.Clamp01(newSpeed);
    }

    public void UpdateVariables()
    {
        physicsTimeStep = 1f / Utils.SolverSteps(pressureSolver);

        Spawn spawner = GetComponent<Spawn>();

        float deltaTime = physicsTimeStep * simulationSpeed;
        float particleSpacing = spawner.Size + spawner.Spacing;
        float particleMass = particleSpacing * particleSpacing * particleSpacing;
        float speedOfSound = maxVelocity / Mathf.Sqrt(densityError);
        float B = restDensity * speedOfSound * speedOfSound / stiffness;
        float beta = deltaTime * deltaTime * particleMass * particleMass * 2 / (restDensity * restDensity);
        
        float delta = Utils.ComputeDelta(pressureKernel, particleSpacing, beta, smoothingRadius) * deltaScale;

        object[] keyValues =
        {
            "deltaTime", deltaTime,
            "dampingFactor", dampingFactor,
            "gravity", gravity,
            "velocitySmoothing", velocitySmoothing,
            "restDensity", restDensity,
            "relaxationFactor", relaxationFactor,
            "particleMass", particleMass,
            "viscosityMultiplier", viscosityMultiplier,
            "surfaceTensionMultiplier", surfaceTensionMultiplier,
            "maxVelocity", maxVelocity,
            "stiffness", stiffness,
            "B", B,
            "nearPressureMultiplier", nearPressureMultiplier,
            "beta", beta,
            "delta", delta,
            "pressureKernel", pressureKernel,
            "nearPressureKernel", nearPressureKernel,
            "densityKernel", densityKernel,
            "nearDensityKernel", nearDensityKernel,
            "surfaceTensionKernel", surfaceTensionKernel,
            "viscosityKernel", viscosityKernel,
            "xsphKernel", xsphKernel,
        };

        SetValues(keyValues);
        UpdateKernelConstants();
    }

    public void ValidateInspectorProperties()
    {
        simulationSpeed = Mathf.Clamp01(simulationSpeed);
        dampingFactor = Mathf.Max(0, dampingFactor);
        restDensity = Mathf.Max(0.01f, restDensity);
        relaxationFactor = Mathf.Clamp01(relaxationFactor);
        viscosityMultiplier = Mathf.Max(0, viscosityMultiplier);
        iisphSolverIterations = Mathf.Max(0, iisphSolverIterations);
    }

    public void WriteToDensityTexture(int x, int y, int z)
    {
        simCompute.Dispatch(WriteDensities, x, y, z);
    }

    public void SetTexture(RenderTexture densityTex)
    {
        simCompute.SetTexture(WriteDensities, "DensityTex", densityTex);
        simCompute.SetInts("densityTexDims", densityTex.width, densityTex.height, densityTex.volumeDepth);
    }

    public void Initialise(int binNumber, Array positions, Array velocities)
    {
        Dictionary<string, BufferInfo> bufferInfo = GenerateBufferInfo(positions, velocities);
        groupCount = Mathf.CeilToInt(positions.Length / (float)Constants.threadGroupSize);

        InstantiateManagers(positions.Length, binNumber, bufferInfo);
    }


    public void SetPressureValues(object[] values)
    {
        Utils.SetValues(values, wcsphCompute, iisphCompute, pcisphCompute);
    }

    public void SetValues(object[] values)
    {
        Utils.SetValues(values, spatialCompute, simCompute);
        SetPressureValues(values);
    }

    public void UpdateColours(Draw.Property prop)
    {
        if (prop == Draw.Property.Velocity) simCompute.Dispatch(CalculateVelocityColour, groupCount, 1, 1);
        if (prop == Draw.Property.Density) simCompute.Dispatch(CalculateDensityColour, groupCount, 1, 1);
        if (prop == Draw.Property.Pressure) simCompute.Dispatch(CalculatePressureColour, groupCount, 1, 1);
    }

    public void RunPhysicsStep(int binNumber)
    {
        UpdateWaveForce();

        // True if binNumber has changed
        bool rebindBuffers = hashManager.ScanAndScatter(binNumber);

        if (rebindBuffers) UpdateOffsets();

        simCompute.Dispatch(CalculateDensity, groupCount, 1, 1);

        int iterations = pressureSolver == Solver.IISPH ? iisphSolverIterations : pcisphSolverIterations;

        if (pressureSolver == Solver.IISPH)
            iisphManager.SolvePressure(iterations);
        if (pressureSolver == Solver.WCSPH)
        {
            wcsphManager.SolvePressure();
        }
        if (pressureSolver == Solver.PCISPH)
            pcisphManager.SolvePressure(iterations);

        simCompute.Dispatch(UpdatePositions, groupCount, 1, 1);

        simulationTime += physicsTimeStep * simulationSpeed;
    }

    void OnDestroy()
    {
        hashManager?.Destroy();
        commonBufferHelper?.Destroy();
        iisphManager?.Destroy();
        pcisphManager?.Destroy();
    }
}