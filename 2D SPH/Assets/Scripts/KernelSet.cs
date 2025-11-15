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
    public int InitialisePressures;
    public int CalculateDensity;
    public int CalculateNonPressureAcceleration;
    public int CalculateNonPressureAccelerationAndD;
    public int PredictVelocity;
    public int PredictPosition;
    public int PredictDensityAndCalculateA;
    public int CalculatePressureSums;
    public int CalculateNextPressure;
    public int FinalisePressureIteration;
    public int CalculatePressure;
    public int UpdateIISPHVelocities;
    public int UpdateWCSPHVelocities;
    public int UpdatePCISPHVelocities;
    public int UpdatePositions;

    public int CalculateVelocityColour;
    public int CalculateDensityColour;
    public int CalculatePressureColour;

    public Dictionary<int, string[]> kernelStaticBufferMap;
    public Dictionary<int, string[]> kernelDynamicBufferMap;

    public int[] WCSPHKernels => new int[] { CalculateDensity, CalculatePressure, UpdateWCSPHVelocities, UpdatePositions };
    public int[] PCISPHPrePressureKernels => new int[] { InitialisePressures, CalculateDensity, CalculateNonPressureAcceleration };
    public int[] PCISPHPressureKernels => new int[] { PredictPosition };
    public int[] PCISPHPostPressureKernels => new int[] { UpdatePCISPHVelocities, UpdatePositions };
    public int[] IISPHPrePressureKernels => new int[] { CalculateDensity, CalculateNonPressureAccelerationAndD, PredictVelocity, PredictDensityAndCalculateA };
    public int[] IISPHPressureKernels => new int[] { CalculatePressureSums, CalculateNextPressure, FinalisePressureIteration };
    public int[] IISPHPostPressureKernels => new int[] { UpdateIISPHVelocities, UpdatePositions };

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
            { InitialisePressures, new[] { "Pressures" }},
            { CalculateDensity, new[] { "Densities", "Offsets" } },
            { CalculateNonPressureAcceleration, new[] { "Offsets", "IntermediateAccelerations", "Densities", "Pressures" } },
            { CalculateNonPressureAccelerationAndD, new[] { "Offsets", "IntermediateAccelerations", "Densities", "Dii" } },
            { PredictVelocity, new[] { "IntermediateAccelerations" } },
            { PredictPosition, new[] { "IntermediateAccelerations" } },
            { PredictDensityAndCalculateA, new[] { "Densities", "Offsets", "Dii", "Aii" } },
            { CalculatePressureSums, new[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures" } },
            { CalculateNextPressure, new[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures", "Pressures" } },
            { FinalisePressureIteration, new[] { "Pressures", "IterPressures" } },
            { CalculatePressure, new[] { "Pressures", "Densities" }},
            { UpdateIISPHVelocities, new[] { "Densities", "Offsets", "Pressures" } },
            { UpdateWCSPHVelocities, new[] { "Densities", "Offsets", "Pressures" }},
            { UpdatePCISPHVelocities, new[] { "IntermediateAccelerations" }},
            { UpdatePositions, new[] { "Densities", "Offsets" } },
            { CalculateVelocityColour, new[] {"Colours"}},
            { CalculateDensityColour, new[] {"Colours", "Densities"}},
            { CalculatePressureColour, new[] {"Colours", "Pressures"}},
        };

        kernelDynamicBufferMap = new Dictionary<int, string[]>
        {
            { Partition, new[] { "Positions" } },
            { CalculateDensity, new[] { "Positions" } },
            { Scatter, new[] { "Pressures", "IterPressures", "OldVelocities", "NewVelocities", "OldPositions", "NewPositions" } },
            { CalculateNonPressureAcceleration, new[] { "Positions", "Velocities" } },
            { CalculateNonPressureAccelerationAndD, new[] { "Positions", "Velocities" } },
            { PredictVelocity, new[] { "Velocities", "Positions" } },
            { PredictPosition, new[] { "Velocities", "Positions" } },
            { PredictDensityAndCalculateA, new[] { "Positions", "Velocities" } },
            { CalculatePressureSums, new[] { "Positions" } },
            { CalculateNextPressure, new[] { "Positions" } },
            { UpdateIISPHVelocities, new[] { "Velocities", "Positions" } },
            { UpdateWCSPHVelocities, new[] { "Velocities", "Positions" } },
            { UpdatePCISPHVelocities, new[] { "Velocities" } },
            { UpdatePositions, new[] { "Velocities", "Positions" } },
            { CalculateVelocityColour, new[] { "Velocities" } }
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
        InitialisePressures = shader.FindKernel("InitialisePressures");
        CalculateDensity = shader.FindKernel("CalculateDensity");
        CalculateNonPressureAcceleration = shader.FindKernel("CalculateNonPressureAcceleration");
        CalculateNonPressureAccelerationAndD = shader.FindKernel("CalculateNonPressureAccelerationAndD");
        PredictVelocity = shader.FindKernel("PredictVelocity");
        PredictPosition = shader.FindKernel("PredictPosition");
        PredictDensityAndCalculateA = shader.FindKernel("PredictDensityAndCalculateA");
        CalculatePressureSums = shader.FindKernel("CalculatePressureSums");
        CalculateNextPressure = shader.FindKernel("CalculateNextPressure");
        FinalisePressureIteration = shader.FindKernel("FinalisePressureIteration");
        CalculatePressure = shader.FindKernel("CalculatePressure");
        UpdateIISPHVelocities = shader.FindKernel("UpdateIISPHVelocities");
        UpdateWCSPHVelocities = shader.FindKernel("UpdateWCSPHVelocities");
        UpdatePCISPHVelocities = shader.FindKernel("UpdatePCISPHVelocities");
        UpdatePositions = shader.FindKernel("UpdatePositions");
        CalculateVelocityColour = shader.FindKernel("CalculateVelocityColour");
        CalculateDensityColour = shader.FindKernel("CalculateDensityColour");
        CalculatePressureColour = shader.FindKernel("CalculatePressureColour");
    }
}