using System.Collections.Generic;
using UnityEngine;

public class WCSPHManager
{
    ComputeShader shader;
    BufferHelper bufferHelper;

    int threadCount;

    int CalculateWCSPHPressure;
    int UpdateWCSPHVelocities;

    public WCSPHManager(ComputeShader wcsphShader, Dictionary<string, ComputeBuffer> externalBuffers, int instanceCount)
    {
        shader = wcsphShader;

        threadCount = Mathf.CeilToInt(instanceCount / (float)Utils.Constants.threadGroupSize);

        FindKernels();

        Dictionary<int, string[]> dependencies = new Dictionary<int, string[]> {
            { CalculateWCSPHPressure, new[] { "Pressures", "Densities" } },
            { UpdateWCSPHVelocities, new[] { "Densities", "Offsets", "Pressures", "Velocities", "Positions" }},
        };

        bufferHelper = new BufferHelper(shader, dependencies, new Dictionary<string, BufferInfo>(), externalBuffers);
    }

    void FindKernels()
    {
        CalculateWCSPHPressure = shader.FindKernel("CalculateWCSPHPressure");
        UpdateWCSPHVelocities = shader.FindKernel("UpdateWCSPHVelocities");
    }

    public void SolvePressure()
    {
        shader.Dispatch(CalculateWCSPHPressure, threadCount, 1, 1);
        shader.Dispatch(UpdateWCSPHVelocities, threadCount, 1, 1);
    }
}