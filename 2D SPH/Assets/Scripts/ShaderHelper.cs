using System.Collections.Generic;
using UnityEngine;

public class ShaderHelper
{
    /*
    Private properties
    */
    ComputeShader computeShader;

    int threadGroups;
    int instanceCount;

    // Buffers
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
    ComputeBuffer densityBuffer;
    ComputeBuffer intermediateAccelerationBuffer;
    ComputeBuffer diiBuffer;
    ComputeBuffer aiiBuffer;
    ComputeBuffer dpSumBuffer;
    ComputeBuffer iterPressureBuffer;
    ComputeBuffer pressureBuffer;
    ComputeBuffer colourBuffer;
    List<ComputeBuffer> allBuffers;

    // Maps
    Dictionary<string, ComputeBuffer> nameBufferMap = new Dictionary<string, ComputeBuffer>();

    /*
    Public getters
    */
    public ComputeBuffer PositionBuffer => positionBuffer;
    public ComputeBuffer VelocityBuffer => velocityBuffer;

    public ComputeBuffer Densities => densityBuffer;
    public ComputeBuffer Pressures => pressureBuffer;
    public ComputeBuffer Accelerations => intermediateAccelerationBuffer;

    public ComputeBuffer Colours => colourBuffer;

    public ShaderHelper(ComputeShader shader)
    {
        computeShader = shader;
    }

    void BindBuffers(Dictionary<int, string[]> mapping)
    {
        foreach (KeyValuePair<int, string[]> pair in mapping)
        {
            foreach (string name in pair.Value)
            {
                computeShader.SetBuffer(pair.Key, name, nameBufferMap[name]);
            }
        }
    }

    void MapBuffers()
    {
        nameBufferMap.Add("CellCounts", countBuffer);
        nameBufferMap.Add("LocalOffsets", localOffsetBuffer);
        nameBufferMap.Add("Offsets", offsetBuffer);
        nameBufferMap.Add("BlockSums", blockSumsBuffer);
        nameBufferMap.Add("Densities", densityBuffer);
        nameBufferMap.Add("IntermediateAccelerations", intermediateAccelerationBuffer);
        nameBufferMap.Add("Dii", diiBuffer);
        nameBufferMap.Add("Aii", aiiBuffer);
        nameBufferMap.Add("DPSum", dpSumBuffer);
        nameBufferMap.Add("IterPressures", iterPressureBuffer);
        nameBufferMap.Add("Pressures", pressureBuffer);
        nameBufferMap.Add("Velocities", velocityBuffer);
        nameBufferMap.Add("Positions", positionBuffer);

        nameBufferMap.Add("OldVelocities", velocityBuffer);
        nameBufferMap.Add("NewVelocities", (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA);
        nameBufferMap.Add("OldPositions", positionBuffer);
        nameBufferMap.Add("NewPositions", (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA);

        nameBufferMap.Add("Colours", colourBuffer);
    }

    public void InitialiseCount(int instanceCount)
    {
        this.instanceCount = instanceCount;
        threadGroups = Mathf.CeilToInt(instanceCount / (float)Constants.threadGroupSize);
    }

    public void BindStaticBuffers(KernelSet kernels)
    {
        BindBuffers(kernels.kernelStaticBufferMap);
    }

    public void BindDynamicBuffers(KernelSet kernels)
    {
        BindBuffers(kernels.kernelDynamicBufferMap);
    }

    public void SetValues(object[] pairs)
    {
        for (int i = 0; i < pairs.Length; i += 2)
        {
            if (pairs[i] is not string name) continue;

            if (pairs[i + 1] is int intVal)
                computeShader.SetInt(name, intVal);
            if (pairs[i + 1] is float floatVal)
                computeShader.SetFloat(name, floatVal);
            if (pairs[i + 1] is Vector3 vecVal)
                computeShader.SetVector(name, vecVal);
        }
    }

    public void SetupBuffers(Vector3[] positions, Vector3[] velocities)
    {
        allBuffers = new List<ComputeBuffer>();

        countBuffer = new ComputeBuffer(Constants.binNumber, sizeof(uint));
        allBuffers.Add(countBuffer);

        offsetBuffer = new ComputeBuffer(Constants.binNumber + 1, sizeof(uint));
        allBuffers.Add(offsetBuffer);

        // Calculate number of blocks needed for hierarchical scan
        int numBlocks = Mathf.CeilToInt(Constants.binNumber / (float)Constants.scanBlockSize);
        blockSumsBuffer = new ComputeBuffer(Mathf.Max(1, numBlocks), sizeof(uint));
        allBuffers.Add(blockSumsBuffer);

        localOffsetBuffer = new ComputeBuffer(Constants.binNumber, sizeof(uint));
        allBuffers.Add(localOffsetBuffer);

        positionBufferA = new ComputeBuffer(positions.Length, sizeof(float) * 3);
        positionBufferA.SetData(positions);
        allBuffers.Add(positionBufferA);

        positionBufferB = new ComputeBuffer(positions.Length, sizeof(float) * 3);
        allBuffers.Add(positionBufferB);

        positionBuffer = positionBufferA;

        velocityBufferA = new ComputeBuffer(velocities.Length, sizeof(float) * 3);
        velocityBufferA.SetData(velocities);
        allBuffers.Add(velocityBufferA);

        velocityBufferB = new ComputeBuffer(velocities.Length, sizeof(float) * 3);
        allBuffers.Add(velocityBufferB);

        velocityBuffer = velocityBufferA;

        // Stores densities first, then near-density, then advanced density
        densityBuffer = new ComputeBuffer(instanceCount * 3, sizeof(float));
        allBuffers.Add(densityBuffer);

        intermediateAccelerationBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3);
        allBuffers.Add(intermediateAccelerationBuffer);

        diiBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3);
        allBuffers.Add(diiBuffer);
        aiiBuffer = new ComputeBuffer(instanceCount, sizeof(float));
        allBuffers.Add(aiiBuffer);
        dpSumBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3);
        allBuffers.Add(dpSumBuffer);

        iterPressureBuffer = new ComputeBuffer(instanceCount, sizeof(float));
        allBuffers.Add(iterPressureBuffer);
        pressureBuffer = new ComputeBuffer(instanceCount, sizeof(float));
        allBuffers.Add(pressureBuffer);

        colourBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3);
        allBuffers.Add(colourBuffer);

        MapBuffers();
    }

    public void SwapBuffers()
    {
        positionBuffer = (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA;
        velocityBuffer = (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA;

        // Update mappings
        nameBufferMap["Positions"] = positionBuffer;
        nameBufferMap["Velocities"] = velocityBuffer;

        // Swap Old/New mappings
        nameBufferMap["OldPositions"] = positionBuffer;
        nameBufferMap["NewPositions"] = (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA;

        nameBufferMap["OldVelocities"] = velocityBuffer;
        nameBufferMap["NewVelocities"] = (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA;
    }


    public void Dispatch(params int[] kernels)
    {
        Dispatch(threadGroups, kernels);
    }
    public void Dispatch(bool useCustomCount, int count, params int[] kernels)
    {
        Dispatch(count, kernels);
    }
    void Dispatch(int count, params int[] kernels)
    {
        foreach (int kernel in kernels)
        {
            computeShader.Dispatch(kernel, count, 1, 1);
        }
    }

    public void Destroy()
    {
        if (allBuffers == null) return;
        foreach (ComputeBuffer buffer in allBuffers) buffer?.Release();
    }
}