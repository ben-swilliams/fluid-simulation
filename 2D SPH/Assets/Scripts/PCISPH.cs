using System.Collections.Generic;
using UnityEngine;

public class PCISPHManager
{
    ComputeShader shader;

    int InitialisePressures;
    int CalculateNonPressureAcceleration;
    int PredictPosition;
    int CalculateNextPCISPHPressure;
    int UpdatePCISPHVelocities;

    int[] PCISPHPrePressureKernels => new int[] { InitialisePressures, CalculateNonPressureAcceleration };
    int[] PCISPHPressureKernels => new int[] { PredictPosition, CalculateNextPCISPHPressure };
    int[] PCISPHPostPressureKernels => new int[] { UpdatePCISPHVelocities };

    BufferHelper bufferHelper;

    int threadCount;
    public PCISPHManager(ComputeShader iisphShader, Dictionary<string, ComputeBuffer> externalBuffers, int instanceCount)
    {
       shader = iisphShader; 

        threadCount = Mathf.CeilToInt(instanceCount / (float)Utils.Constants.threadGroupSize);

        FindKernels();

        Dictionary<int, string[]> dependencies = new Dictionary<int, string[]> {
            { InitialisePressures, new[] { "Pressures" }},
            { CalculateNonPressureAcceleration, new[] { "Offsets", "IntermediateAccelerations", "Densities", "Pressures", "Velocities", "Positions" } },
            { PredictPosition, new[] { "IntermediateAccelerations", "PredictedPositions", "Densities", "Offsets", "Pressures", "Velocities", "Positions" } },
            { CalculateNextPCISPHPressure, new[] { "PredictedPositions", "Pressures", "Densities", "Offsets", "Positions" } },
            { UpdatePCISPHVelocities, new[] { "IntermediateAccelerations", "Densities", "Offsets", "Pressures", "Velocities", "Positions" }},
        };

        Dictionary<string, BufferInfo> bufferInfo = GenerateBufferInfo(instanceCount);

        bufferHelper = new BufferHelper(shader, dependencies, bufferInfo, externalBuffers);
    }

    void FindKernels()
    {
        InitialisePressures =
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