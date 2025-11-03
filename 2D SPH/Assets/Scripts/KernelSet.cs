using System.Collections.Generic;
using UnityEngine;

public class KernelSet
{
    public int ClearCounts;
    public int Partition;
    public int Scan;
    public int ScanBlockSums;
    public int AddBlockSums;
    public int FinalizeScan;
    public int Scatter;
    public int CalculateDensity;
    public int CalculateNonPressureAccelerationAndD;
    public int PredictVelocity;
    public int PredictDensityAndCalculateA;
    public int CalculatePressureSums;
    public int CalculateNextPressure;
    public int FinalisePressureIteration;
    public int CalculatePressure;
    public int UpdateIISPHVelocities;
    public int UpdateWCSPHVelocities;
    public int UpdatePositions;

    public Dictionary<int, string[]> kernelStaticBufferMap;
    public Dictionary<int, string[]> kernelDynamicBufferMap;

    public int[] WCSPHKernels => new int[] { CalculateDensity, CalculatePressure, UpdateWCSPHVelocities, UpdatePositions };
    public int[] PrePressureKernels => new int[] { CalculateDensity, CalculateNonPressureAccelerationAndD, PredictVelocity, PredictDensityAndCalculateA };
    public int[] PressureKernels => new int[] { CalculatePressureSums, CalculateNextPressure, FinalisePressureIteration };
    public int[] PostPressureKernels => new int[] { UpdateIISPHVelocities, UpdatePositions };

    public KernelSet(ComputeShader shader)
    {
        SetKernels(shader);

        kernelStaticBufferMap = new Dictionary<int, string[]>
        {
            { ClearCounts, new[] { "CellCounts", "LocalOffsets" } },
            { Partition, new[] { "CellCounts" } },
            { Scan, new[] { "Offsets", "CellCounts", "BlockSums" } },
            { ScanBlockSums, new[] { "BlockSums" } },
            { AddBlockSums, new[] { "Offsets", "CellCounts", "BlockSums" } },
            { FinalizeScan, new[] { "Offsets" } },
            { Scatter, new[] { "LocalOffsets", "Offsets" } },
            { CalculateDensity, new[] { "Densities", "Offsets" } },
            { CalculateNonPressureAccelerationAndD, new[] { "Offsets", "IntermediateAccelerations", "Densities", "Dii" } },
            { PredictVelocity, new[] { "IntermediateAccelerations" } },
            { PredictDensityAndCalculateA, new[] { "Densities", "Offsets", "Dii", "Aii" } },
            { CalculatePressureSums, new[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures" } },
            { CalculateNextPressure, new[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures", "Pressures" } },
            { FinalisePressureIteration, new[] { "Pressures", "IterPressures" } },
            { CalculatePressure, new[] { "Pressures", "Densities" }},
            { UpdateIISPHVelocities, new[] { "Densities", "Offsets", "Pressures" } },
            { UpdateWCSPHVelocities, new[] { "Densities", "Offsets", "Pressures" }},
            { UpdatePositions, new[] { "Densities", "Offsets" } }
        };

        kernelDynamicBufferMap = new Dictionary<int, string[]>
        {
            { Partition, new[] { "Positions" } },
            { CalculateDensity, new[] { "Positions" } },
            { Scatter, new[] { "Pressures", "IterPressures", "OldVelocities", "NewVelocities", "OldPositions", "NewPositions" } },
            { CalculateNonPressureAccelerationAndD, new[] { "Positions", "Velocities" } },
            { PredictVelocity, new[] { "Velocities" } },
            { PredictDensityAndCalculateA, new[] { "Positions", "Velocities" } },
            { CalculatePressureSums, new[] { "Positions" } },
            { CalculateNextPressure, new[] { "Positions" } },
            { UpdateIISPHVelocities, new[] { "Velocities", "Positions" } },
            { UpdateWCSPHVelocities, new[] { "Velocities", "Positions" } },
            { UpdatePositions, new[] { "Velocities", "Positions" } }
        };
    }

    private void SetKernels(ComputeShader shader)
    {
        ClearCounts = shader.FindKernel("ClearCounts");
        Partition = shader.FindKernel("Partition");
        Scan = shader.FindKernel("Scan");
        ScanBlockSums = shader.FindKernel("ScanBlockSums");
        AddBlockSums = shader.FindKernel("AddBlockSums");
        FinalizeScan = shader.FindKernel("FinalizeScan");
        Scatter = shader.FindKernel("Scatter");
        CalculateDensity = shader.FindKernel("CalculateDensity");
        CalculateNonPressureAccelerationAndD = shader.FindKernel("CalculateNonPressureAccelerationAndD");
        PredictVelocity = shader.FindKernel("PredictVelocity");
        PredictDensityAndCalculateA = shader.FindKernel("PredictDensityAndCalculateA");
        CalculatePressureSums = shader.FindKernel("CalculatePressureSums");
        CalculateNextPressure = shader.FindKernel("CalculateNextPressure");
        FinalisePressureIteration = shader.FindKernel("FinalisePressureIteration");
        CalculatePressure = shader.FindKernel("CalculatePressure");
        UpdateIISPHVelocities = shader.FindKernel("UpdateIISPHVelocities");
        UpdateWCSPHVelocities = shader.FindKernel("UpdateWCSPHVelocities");
        UpdatePositions = shader.FindKernel("UpdatePositions");
    }
}