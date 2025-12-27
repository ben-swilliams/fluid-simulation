using System;
using System.Collections.Generic;
using UnityEngine;

public class Compute
{
    // Common kernels
    int CalculateDensity;
    int UpdatePositions;
    int WriteDensities;
    int CalculateVelocityColour;
    int CalculateDensityColour;
    int CalculatePressureColour;

    // Shaders
    ComputeShader spatialCompute;
    ComputeShader simCompute;
    ComputeShader wcsphCompute;
    ComputeShader iisphCompute;
    ComputeShader pcisphCompute;

    // Managers
    SpatialHashManager hashManager;
    WCSPHManager wcsphManager;
    IISPHManager iisphManager;
    PCISPHManager pcisphManager;

    // Buffer helper
    BufferHelper commonBufferHelper;

    int binNumber;
    int groupCount;

    float timeStep;

    public float TimeStep => timeStep;
    public ComputeBuffer Positions => commonBufferHelper.RetrieveBuffer("Positions");
    public ComputeBuffer Colours => commonBufferHelper.RetrieveBuffer("Colours");

    public Compute(ComputeShader spatial, ComputeShader sim, ComputeShader wcsph, ComputeShader iisph, ComputeShader pcisph)
    {
        spatialCompute = spatial;
        simCompute = sim;
        wcsphCompute = wcsph;
        iisphCompute = iisph;
        pcisphCompute = pcisph;
    }

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
        groupCount = Mathf.CeilToInt(positions.Length / (float)Common.Constants.threadGroupSize);

        timeStep = Common.Utils.SolverSteps(Common.Solver.WCSPH);
        InstantiateManagers(positions.Length, binNumber, bufferInfo);
    }


    public void SetValues(object[] values, params ComputeShader[] shaders)
    {
        Common.Utils.SetValues(values, shaders);
    }

    public void UpdateColours(Draw.Property prop)
    {
        if (prop == Draw.Property.Velocity) simCompute.Dispatch(CalculateVelocityColour, groupCount, 1, 1);
        if (prop == Draw.Property.Density) simCompute.Dispatch(CalculateDensityColour, groupCount, 1, 1);
        if (prop == Draw.Property.Pressure) simCompute.Dispatch(CalculatePressureColour, groupCount, 1, 1);
    }


    public void RunPhysicsStep(int binNumber, Common.Solver pressureSolver, int iterations)
    {
        // True if binNumber has changed
        bool rebindBuffers = hashManager.ScanAndScatter(binNumber);

        if (rebindBuffers) UpdateOffsets();

        simCompute.Dispatch(CalculateDensity, groupCount, 1, 1);

        if (pressureSolver == Common.Solver.IISPH)
            iisphManager.SolvePressure(iterations);
        if (pressureSolver == Common.Solver.WCSPH)
        {
            wcsphManager.SolvePressure();
        }
        if (pressureSolver == Common.Solver.PCISPH)
            pcisphManager.SolvePressure(iterations);

        simCompute.Dispatch(UpdatePositions, groupCount, 1, 1);
    }

    public void Destroy()
    {
        hashManager?.Destroy();
        commonBufferHelper?.Destroy();
        iisphManager?.Destroy();
        pcisphManager?.Destroy();
    }
}