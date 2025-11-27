using System.Collections.Generic;
using UnityEngine;

public class KernelSet
{
    public int ClearCounts;
    public int Partition;
    public int Scan;
    public int ScanBlockSums;
    public int ScanSuperBlockSums;
    public int AddSuperBlockSums;
    public int AddBlockSums;
    public int FinalizeScan;
    public int Scatter;
    public int CopyBack;
    public int InitialisePressures;
    public int CalculateDensity;
    public int CalculateNonPressureAcceleration;
    public int CalculateNonPressureAccelerationAndD;
    public int PredictVelocity;
    public int PredictPosition;
    public int PredictDensityAndCalculateA;
    public int CalculatePressureSums;
    public int CalculateNextIISPHPressure;
    public int FinalisePressureIteration;
    public int CalculateWCSPHPressure;
    public int CalculateNextPCISPHPressure;
    public int UpdateIISPHVelocities;
    public int UpdateWCSPHVelocities;
    public int UpdatePCISPHVelocities;
    public int UpdatePositions;

    public int WriteDensities;

    public int CalculateVelocityColour;
    public int CalculateDensityColour;
    public int CalculatePressureColour;

    public Dictionary<int, string[]> kernelStaticBufferMap;

    public int[] WCSPHKernels => new int[] { CalculateDensity, CalculateWCSPHPressure, UpdateWCSPHVelocities, UpdatePositions };
    public int[] PCISPHPrePressureKernels => new int[] { InitialisePressures, CalculateDensity, CalculateNonPressureAcceleration };
    public int[] PCISPHPressureKernels => new int[] { PredictPosition, CalculateNextPCISPHPressure };
    public int[] PCISPHPostPressureKernels => new int[] { UpdatePCISPHVelocities, UpdatePositions };
    public int[] IISPHPrePressureKernels => new int[] { CalculateDensity, CalculateNonPressureAccelerationAndD, PredictVelocity, PredictDensityAndCalculateA };
    public int[] IISPHPressureKernels => new int[] { CalculatePressureSums, CalculateNextIISPHPressure, FinalisePressureIteration };
    public int[] IISPHPostPressureKernels => new int[] { UpdateIISPHVelocities, UpdatePositions };

    public KernelSet(ComputeShader shader)
    {
        SetKernels(shader);

        kernelStaticBufferMap = new Dictionary<int, string[]>
        {
            { ClearCounts, new[] { "CellCounts", "LocalOffsets" } },
            { Partition, new[] { "CellCounts", "Positions" } },
            { Scan, new[] { "Offsets", "CellCounts", "BlockSums" } },
            { ScanBlockSums, new[] { "BlockSums", "SuperBlockSums" } },
            { ScanSuperBlockSums, new[] { "SuperBlockSums" } },
            { AddSuperBlockSums, new[] { "BlockSums", "SuperBlockSums" } },
            { AddBlockSums, new[] { "Offsets", "CellCounts", "BlockSums" } },
            { FinalizeScan, new[] { "Offsets" } },
            { Scatter, new[] { "LocalOffsets", "Offsets", "IterPressures", "Velocities", "SortedVelocities", "Positions", "SortedPositions" } },
            { CopyBack, new[] { "SortedVelocities", "SortedPositions", "Velocities", "Positions" }},
            { InitialisePressures, new[] { "Pressures" }},
            { CalculateDensity, new[] { "Densities", "Offsets", "Positions" } },
            { CalculateNonPressureAcceleration, new[] { "Offsets", "IntermediateAccelerations", "Densities", "Pressures", "Velocities", "Positions" } },
            { CalculateNonPressureAccelerationAndD, new[] { "Offsets", "IntermediateAccelerations", "Densities", "Dii", "Velocities", "Positions" } },
            { PredictVelocity, new[] { "IntermediateAccelerations", "Velocities", "Positions" } },
            { PredictPosition, new[] { "IntermediateAccelerations", "PredictedPositions", "Densities", "Offsets", "Pressures", "Velocities", "Positions" } },
            { PredictDensityAndCalculateA, new[] { "Densities", "Offsets", "Dii", "Aii", "Velocities", "Positions" } },
            { CalculatePressureSums, new[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures", "Velocities", "Positions" } },
            { CalculateNextIISPHPressure, new[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures", "Pressures", "Positions" } },
            { FinalisePressureIteration, new[] { "Pressures", "IterPressures" } },
            { CalculateWCSPHPressure, new[] { "Pressures", "Densities" } },
            { CalculateNextPCISPHPressure, new[] { "PredictedPositions", "Pressures", "Densities", "Offsets", "Positions" } },
            { UpdateIISPHVelocities, new[] { "Densities", "Offsets", "Pressures", "Velocities", "Positions" } },
            { UpdateWCSPHVelocities, new[] { "Densities", "Offsets", "Pressures", "Velocities", "Positions" }},
            { UpdatePCISPHVelocities, new[] { "IntermediateAccelerations", "Densities", "Offsets", "Pressures", "Velocities", "Positions" }},
            { UpdatePositions, new[] { "Densities", "Offsets", "Velocities", "Positions" } },
            { WriteDensities, new[] { "Offsets", "Positions" }},
            { CalculateVelocityColour, new[] {"Colours", "Velocities"}},
            { CalculateDensityColour, new[] {"Colours", "Densities"}},
            { CalculatePressureColour, new[] {"Colours", "Pressures"}},
        };
    }

    private void SetKernels(ComputeShader shader)
    {
        ClearCounts = shader.FindKernel("ClearCounts");
        Partition = shader.FindKernel("Partition");
        Scan = shader.FindKernel("Scan");
        ScanBlockSums = shader.FindKernel("ScanBlockSums");
        ScanSuperBlockSums = shader.FindKernel("ScanSuperBlockSums");
        AddSuperBlockSums = shader.FindKernel("AddSuperBlockSums");
        AddBlockSums = shader.FindKernel("AddBlockSums");
        FinalizeScan = shader.FindKernel("FinalizeScan");
        Scatter = shader.FindKernel("Scatter");
        CopyBack = shader.FindKernel("CopyBack");
        InitialisePressures = shader.FindKernel("InitialisePressures");
        CalculateDensity = shader.FindKernel("CalculateDensity");
        CalculateNonPressureAcceleration = shader.FindKernel("CalculateNonPressureAcceleration");
        CalculateNonPressureAccelerationAndD = shader.FindKernel("CalculateNonPressureAccelerationAndD");
        PredictVelocity = shader.FindKernel("PredictVelocity");
        PredictPosition = shader.FindKernel("PredictPosition");
        PredictDensityAndCalculateA = shader.FindKernel("PredictDensityAndCalculateA");
        CalculatePressureSums = shader.FindKernel("CalculatePressureSums");
        CalculateNextIISPHPressure = shader.FindKernel("CalculateNextIISPHPressure");
        FinalisePressureIteration = shader.FindKernel("FinalisePressureIteration");
        CalculateWCSPHPressure = shader.FindKernel("CalculateWCSPHPressure");
        CalculateNextPCISPHPressure = shader.FindKernel("CalculateNextPCISPHPressure");
        UpdateIISPHVelocities = shader.FindKernel("UpdateIISPHVelocities");
        UpdateWCSPHVelocities = shader.FindKernel("UpdateWCSPHVelocities");
        UpdatePCISPHVelocities = shader.FindKernel("UpdatePCISPHVelocities");
        UpdatePositions = shader.FindKernel("UpdatePositions");
        WriteDensities = shader.FindKernel("WriteDensities");
        CalculateVelocityColour = shader.FindKernel("CalculateVelocityColour");
        CalculateDensityColour = shader.FindKernel("CalculateDensityColour");
        CalculatePressureColour = shader.FindKernel("CalculatePressureColour");
    }
}