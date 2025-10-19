using System.Collections.Generic;
using System.Net.Http.Headers;
using UnityEngine;

class ShaderHelper
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
    ComputeBuffer nearDensityBuffer;

    // Maps
    Dictionary<int, string[]> kernelStaticBufferMap = new Dictionary<int, string[]>();
    Dictionary<int, string[]> kernelDynamicBufferMap = new Dictionary<int, string[]>();
    Dictionary<string, ComputeBuffer> nameBufferMap = new Dictionary<string, ComputeBuffer>();

    /*
    Public getters
    */
    public ComputeBuffer PositionBuffer => positionBuffer;
    public ComputeBuffer VelocityBuffer => velocityBuffer;

    void BindStaticBuffers()
    {
        BindBuffers(kernelStaticBufferMap);
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
        nameBufferMap.Add("NearDensities", nearDensityBuffer);
        nameBufferMap.Add("Velocities", velocityBuffer);
        nameBufferMap.Add("Positions", positionBuffer);

        nameBufferMap.Add("OldVelocities", velocityBuffer);
        nameBufferMap.Add("NewVelocities", (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA);
        nameBufferMap.Add("OldPositions", positionBuffer);
        nameBufferMap.Add("NewPositions", (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA);
    }

    public ShaderHelper(ComputeShader shader)
    {
        computeShader = shader;
    }

    public void InitialiseCount(int instanceCount)
    {
        this.instanceCount = instanceCount;
        threadGroups = Mathf.CeilToInt(instanceCount / (float)Constants.threadGroupSize);
    }

    public void BindDynamicBuffers()
    {
        BindBuffers(kernelDynamicBufferMap);
    }

    public void MapKernels(int clearCountsKernel,
                      int partitionKernel,
                      int scanKernel,
                      int scanBlockSumsKernel,
                      int addBlockSumsKernel,
                      int finalizeScanKernel,
                      int scatterKernel,
                      int densityKernel,
                      int velocityKernel,
                      int positionKernel
    )
    {
        kernelStaticBufferMap.Add(clearCountsKernel, new string[] { "CellCounts", "LocalOffsets" });
        kernelStaticBufferMap.Add(partitionKernel, new string[] { "CellCounts" });
        kernelStaticBufferMap.Add(scanKernel, new string[] { "Offsets", "CellCounts", "BlockSums" });
        kernelStaticBufferMap.Add(scanBlockSumsKernel, new string[] { "BlockSums" });
        kernelStaticBufferMap.Add(addBlockSumsKernel, new string[] { "Offsets", "CellCounts", "BlockSums" });
        kernelStaticBufferMap.Add(finalizeScanKernel, new string[] { "Offsets" });
        kernelStaticBufferMap.Add(scatterKernel, new string[] { "LocalOffsets", "Offsets" });
        kernelStaticBufferMap.Add(densityKernel, new string[] { "Densities", "NearDensities", "Offsets" });
        kernelStaticBufferMap.Add(velocityKernel, new string[] { "Densities", "NearDensities", "Offsets" });
        kernelStaticBufferMap.Add(positionKernel, new string[] { "Densities", "Offsets" });

        kernelDynamicBufferMap.Add(partitionKernel, new string[] { "Positions" });
        kernelDynamicBufferMap.Add(scatterKernel, new string[] {"OldVelocities", "NewVelocities",
                                                                 "OldPositions", "NewPositions" });
        kernelDynamicBufferMap.Add(densityKernel, new string[] { "Positions" });
        kernelDynamicBufferMap.Add(velocityKernel, new string[] {  "Velocities", "Positions" });
        kernelDynamicBufferMap.Add(positionKernel, new string[] {  "Velocities", "Positions" });

        BindStaticBuffers();
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
            if (pairs[i + 1] is Vector2 vecVal)
                computeShader.SetVector(name, vecVal);
        }
    }

    public void SetupBuffers(Vector2[] positions, Vector2[] velocities)
    {
        countBuffer = new ComputeBuffer(Constants.binNumber, sizeof(uint));

        offsetBuffer = new ComputeBuffer(Constants.binNumber + 1, sizeof(uint));

        // Calculate number of blocks needed for hierarchical scan
        int numBlocks = Mathf.CeilToInt(Constants.binNumber / (float)Constants.scanBlockSize);
        blockSumsBuffer = new ComputeBuffer(Mathf.Max(1, numBlocks), sizeof(uint));

        localOffsetBuffer = new ComputeBuffer(Constants.binNumber, sizeof(uint));

        positionBufferA = new ComputeBuffer(positions.Length, sizeof(float) * 2);
        positionBufferA.SetData(positions);

        positionBufferB = new ComputeBuffer(positions.Length, sizeof(float) * 2);

        positionBuffer = positionBufferA;

        velocityBufferA = new ComputeBuffer(velocities.Length, sizeof(float) * 2);
        velocityBufferA.SetData(velocities);

        velocityBufferB = new ComputeBuffer(velocities.Length, sizeof(float) * 2);

        velocityBuffer = velocityBufferA;

        densityBuffer = new ComputeBuffer(instanceCount, sizeof(float));
        nearDensityBuffer = new ComputeBuffer(instanceCount, sizeof(float));

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
        if (countBuffer != null)
            countBuffer.Release();
        if (offsetBuffer != null)
            offsetBuffer.Release();
        if (blockSumsBuffer != null)
            blockSumsBuffer.Release();
        if (localOffsetBuffer != null)
            localOffsetBuffer.Release();
        if (positionBufferA != null)
            positionBufferA.Release();
        if (velocityBufferA != null)
            velocityBufferA.Release();
        if (positionBufferB != null)
            positionBufferB.Release();
        if (velocityBufferB != null)
            velocityBufferB.Release();
        if (densityBuffer != null)
            densityBuffer.Release();
        if (nearDensityBuffer != null)
            nearDensityBuffer.Release();
    }
}