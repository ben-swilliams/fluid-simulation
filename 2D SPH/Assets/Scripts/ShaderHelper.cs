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
    ComputeBuffer superBlockSumsBuffer;
    ComputeBuffer localOffsetBuffer;
    ComputeBuffer positionBuffer;
    ComputeBuffer sortedPositionBuffer;
    ComputeBuffer predictedPositions;
    ComputeBuffer velocityBuffer;
    ComputeBuffer sortedVelocityBuffer;
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
        nameBufferMap.Add("SuperBlockSums", superBlockSumsBuffer);
        nameBufferMap.Add("Densities", densityBuffer);
        nameBufferMap.Add("IntermediateAccelerations", intermediateAccelerationBuffer);
        nameBufferMap.Add("Dii", diiBuffer);
        nameBufferMap.Add("Aii", aiiBuffer);
        nameBufferMap.Add("DPSum", dpSumBuffer);
        nameBufferMap.Add("IterPressures", iterPressureBuffer);
        nameBufferMap.Add("Pressures", pressureBuffer);
        nameBufferMap.Add("SortedVelocities", sortedVelocityBuffer);
        nameBufferMap.Add("Velocities", velocityBuffer);
        nameBufferMap.Add("PredictedPositions", predictedPositions);
        nameBufferMap.Add("SortedPositions", sortedPositionBuffer);
        nameBufferMap.Add("Positions", positionBuffer);
        nameBufferMap.Add("Colours", colourBuffer);
    }

    public void InitialiseCount(int instanceCount)
    {
        this.instanceCount = instanceCount;
        threadGroups = Mathf.CeilToInt(instanceCount / (float)Utils.Constants.threadGroupSize);
    }

    public void BindStaticBuffers(KernelSet kernels)
    {
        BindBuffers(kernels.kernelStaticBufferMap);
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

    public void SetInts(string name, params int[] ints)
    {
        computeShader.SetInts(name, ints);
    }

    public void SetupBinBuffers(int binNumber)
    {
        CleanupBinBuffers();

        // Create new buffers
        countBuffer = new ComputeBuffer(binNumber, sizeof(uint));
        allBuffers.Add(countBuffer);

        offsetBuffer = new ComputeBuffer(binNumber + 1, sizeof(uint));
        allBuffers.Add(offsetBuffer);

        // Calculate number of blocks needed for hierarchical scan
        int numBlocks = Mathf.CeilToInt(binNumber / (float)Utils.Constants.scanBlockSize);
        blockSumsBuffer = new ComputeBuffer(Mathf.Max(1, numBlocks), sizeof(uint));
        allBuffers.Add(blockSumsBuffer);

        // Calculate number of super blocks needed for three-level scan
        int numSuperBlocks = Mathf.CeilToInt(numBlocks / (float)Utils.Constants.scanBlockSize);
        superBlockSumsBuffer = new ComputeBuffer(Mathf.Max(1, numSuperBlocks), sizeof(uint));
        allBuffers.Add(superBlockSumsBuffer);

        localOffsetBuffer = new ComputeBuffer(binNumber, sizeof(uint));
        allBuffers.Add(localOffsetBuffer);

        // Update nameBufferMap only if it's already been initialized (runtime updates only)
        if (nameBufferMap.ContainsKey("CellCounts"))
        {
            nameBufferMap["CellCounts"] = countBuffer;
            nameBufferMap["LocalOffsets"] = localOffsetBuffer;
            nameBufferMap["Offsets"] = offsetBuffer;
            nameBufferMap["BlockSums"] = blockSumsBuffer;
            nameBufferMap["SuperBlockSums"] = superBlockSumsBuffer;
        }
    }

    void CleanupBinBuffers()
    {
        if (countBuffer != null)
        {
            allBuffers.Remove(countBuffer);
            countBuffer.Release();
        }
        if (offsetBuffer != null)
        {
            allBuffers.Remove(offsetBuffer);
            offsetBuffer.Release();
        }
        if (blockSumsBuffer != null)
        {
            allBuffers.Remove(blockSumsBuffer);
            blockSumsBuffer.Release();
        }
        if (superBlockSumsBuffer != null)
        {
            allBuffers.Remove(superBlockSumsBuffer);
            superBlockSumsBuffer.Release();
        }
        if (localOffsetBuffer != null)
        {
            allBuffers.Remove(localOffsetBuffer);
            localOffsetBuffer.Release();
        }

    }

    public void SetupBuffers(Vector3[] positions, Vector3[] velocities, int binNumber)
    {
        allBuffers = new List<ComputeBuffer>();

        SetupBinBuffers(binNumber);

        sortedPositionBuffer = new ComputeBuffer(positions.Length, sizeof(float) * 3);
        allBuffers.Add(sortedPositionBuffer);

        positionBuffer = new ComputeBuffer(positions.Length, sizeof(float) * 3);
        positionBuffer.SetData(positions);
        allBuffers.Add(positionBuffer);

        predictedPositions = new ComputeBuffer(positions.Length, sizeof(float) * 3);
        allBuffers.Add(predictedPositions);

        sortedVelocityBuffer = new ComputeBuffer(velocities.Length, sizeof(float) * 3);
        allBuffers.Add(sortedVelocityBuffer);

        velocityBuffer = new ComputeBuffer(velocities.Length, sizeof(float) * 3);
        velocityBuffer.SetData(velocities);
        allBuffers.Add(velocityBuffer);

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

    public void SetTexture(int kernel, string name, RenderTexture texture)
    {
        computeShader.SetTexture(kernel, name, texture);
    }

    public void Dispatch(params int[] kernels)
    {
        Dispatch(threadGroups, kernels);
    }
    public void Dispatch(bool useCustomCount, int count, params int[] kernels)
    {
        Dispatch(count, kernels);
    }

    public void Dispatch(int kernel, int countX, int countY, int countZ)
    {
        computeShader.Dispatch(kernel, countX, countY, countZ);
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