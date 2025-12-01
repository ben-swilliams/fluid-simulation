using System.Collections.Generic;
using UnityEngine;

public class IISPHManager
{
    ComputeShader shader;

    int InitialisePressures;
    int CalculateNonPressureAccelerationAndD;
    int PredictVelocity;
    int PredictDensityAndCalculateA;
    int CalculatePressureSums;
    int CalculateNextIISPHPressure;
    int FinalisePressureIteration;
    int UpdateIISPHVelocities;

    int[] IISPHPrePressureKernels => new int[] { InitialisePressures, CalculateNonPressureAccelerationAndD, PredictVelocity, PredictDensityAndCalculateA };
    int[] IISPHPressureKernels => new int[] { CalculatePressureSums, CalculateNextIISPHPressure, FinalisePressureIteration };
    int[] IISPHPostPressureKernels => new int[] { UpdateIISPHVelocities };

    BufferHelper bufferHelper;
    public BufferHelper Buffers => bufferHelper;

    int threadCount;

    public IISPHManager(ComputeShader iisphShader, Dictionary<string, ComputeBuffer> externalBuffers, int instanceCount)
    {
       shader = iisphShader; 

        threadCount = Mathf.CeilToInt(instanceCount / (float)Utils.Constants.threadGroupSize);

        FindKernels();

        Dictionary<int, string[]> dependencies = new Dictionary<int, string[]> {
            { InitialisePressures, new[] { "IterPressures" } },
            { CalculateNonPressureAccelerationAndD, new[] { "Offsets", "IntermediateAccelerations", "Densities", "Dii", "Velocities", "Positions" } },
            { PredictVelocity, new[] { "IntermediateAccelerations", "Velocities", "Positions" } },
            { PredictDensityAndCalculateA, new[] { "Densities", "Offsets", "Dii", "Aii", "Velocities", "Positions" } },
            { CalculatePressureSums, new[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures", "Velocities", "Positions" } },
            { CalculateNextIISPHPressure, new[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures", "Pressures", "Positions" } },
            { FinalisePressureIteration, new[] { "Pressures", "IterPressures" } },
            { UpdateIISPHVelocities, new[] { "Densities", "Offsets", "Pressures", "Velocities", "Positions" } },
        };

        Dictionary<string, BufferInfo> bufferInfo = GenerateBufferInfo(instanceCount);

        bufferHelper = new BufferHelper(shader, dependencies, bufferInfo, externalBuffers);
    }

    void FindKernels()
    {
        CalculateNonPressureAccelerationAndD = shader.FindKernel("CalculateNonPressureAccelerationAndD");
        PredictVelocity = shader.FindKernel("PredictVelocity");
        PredictDensityAndCalculateA = shader.FindKernel("PredictDensityAndCalculateA");
        CalculatePressureSums = shader.FindKernel("CalculatePressureSums");
        CalculateNextIISPHPressure = shader.FindKernel("CalculateNextIISPHPressure");
        FinalisePressureIteration = shader.FindKernel("FinalisePressureIteration");
        UpdateIISPHVelocities = shader.FindKernel("UpdateIISPHVelocities");
    }

    Dictionary<string, BufferInfo> GenerateBufferInfo(int instanceCount)
    {
        Dictionary<string, BufferInfo> bufferInfo = new Dictionary<string, BufferInfo>
        {
            { "Aii", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) } },
            { "IterPressures", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) } },
            { "Dii", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) * 3 } },
            { "DPSum", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) * 3 } },
        };
        
        return bufferInfo;
    }

    public void SolvePressure(int iterations)
    {
        foreach (int k in IISPHPrePressureKernels)
        {
            shader.Dispatch(k, threadCount, 1, 1);
        }

        for (int _ = 0; _ < iterations; _++)
        {
            foreach (int k in IISPHPressureKernels)
            {
                shader.Dispatch(k, threadCount, 1, 1);
            }
        }

        foreach (int k in IISPHPostPressureKernels)
        {
            shader.Dispatch(k, threadCount, 1, 1);
        }
    }

    public void Destroy()
    {
        bufferHelper.Destroy();
    }
}