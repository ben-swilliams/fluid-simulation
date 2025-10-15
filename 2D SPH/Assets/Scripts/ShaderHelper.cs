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
    ComputeBuffer accelerationBuffer;
    ComputeBuffer densityBuffer;
    ComputeBuffer densityBufferA;
    ComputeBuffer densityBufferB;

    // Maps
    Dictionary<int, string[]> kernelStaticBufferMap = new Dictionary<int, string[]>();
    Dictionary<int, string[]> kernelDynamicBufferMap = new Dictionary<int, string[]>();
    Dictionary<string, ComputeBuffer> nameBufferMap = new Dictionary<string, ComputeBuffer>();

    /*
    Public getters
    */
    public ComputeBuffer PositionBuffer => positionBuffer;
    public ComputeBuffer VelocityBuffer => velocityBuffer;

    public ShaderHelper(ComputeShader shader)
    {
        computeShader = shader;
    }

    public void InitialiseCount(int instanceCount)
    {
        this.instanceCount = instanceCount;
        threadGroups = Mathf.CeilToInt(instanceCount / (float)Constants.threadGroupSize);
    }

    void BindStaticBuffers()
    {
        BindBuffers(kernelStaticBufferMap);
    }

    public void BindDynamicBuffers()
    {
        BindBuffers(kernelDynamicBufferMap);
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
        nameBufferMap.Add("Accelerations", accelerationBuffer);
        nameBufferMap.Add("Velocities", velocityBuffer);
        nameBufferMap.Add("Positions", positionBuffer);

        nameBufferMap.Add("OldDensities", densityBuffer);
        nameBufferMap.Add("NewDensities", (densityBuffer == densityBufferA) ? densityBufferB : densityBufferA);
        nameBufferMap.Add("OldVelocities", velocityBuffer);
        nameBufferMap.Add("NewVelocities", (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA);
        nameBufferMap.Add("OldPositions", positionBuffer);
        nameBufferMap.Add("NewPositions", (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA);
    }

    public void MapKernels(int clearCountsKernel,
                      int partitionKernel,
                      int scanKernel,
                      int scanBlockSumsKernel,
                      int addBlockSumsKernel,
                      int finalizeScanKernel,
                      int scatterKernel,
                      int densityKernel,
                      int accelerationKernel,
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
        kernelStaticBufferMap.Add(accelerationKernel, new string[] { "Offsets", "Accelerations" });
        kernelStaticBufferMap.Add(velocityKernel, new string[] { "Offsets", "Accelerations" });
        kernelStaticBufferMap.Add(positionKernel, new string[] { "Accelerations" });

        kernelDynamicBufferMap.Add(partitionKernel, new string[] { "Positions" });
        kernelDynamicBufferMap.Add(scatterKernel, new string[] { "OldDensities", "NewDensities",
                                                                 "OldVelocities", "NewVelocities",
                                                                 "OldPositions", "NewPositions" });
        kernelDynamicBufferMap.Add(densityKernel, new string[] { "Offsets", "Densities", "Positions" });
        kernelDynamicBufferMap.Add(accelerationKernel, new string[] { "Densities", "Velocities", "Positions" });
        kernelDynamicBufferMap.Add(velocityKernel, new string[] { "Densities", "Velocities", "Positions" });
        kernelDynamicBufferMap.Add(positionKernel, new string[] { "Velocities", "Positions" });

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

        accelerationBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);

        float[] densities = new float[instanceCount];
        densityBufferA = new ComputeBuffer(densities.Length, sizeof(float));
        densityBufferA.SetData(densities);

        densityBufferB = new ComputeBuffer(densities.Length, sizeof(float));

        densityBuffer = densityBufferA;

        MapBuffers();
    }

    public void SwapBuffers()
    {
        positionBuffer = (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA;
        velocityBuffer = (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA;
        densityBuffer = (densityBuffer == densityBufferA) ? densityBufferB : densityBufferA;

        // Update mappings
        nameBufferMap["Positions"] = positionBuffer;
        nameBufferMap["Velocities"] = velocityBuffer;
        nameBufferMap["Densities"] = densityBuffer;

        // Swap Old/New mappings
        nameBufferMap["OldPositions"] = positionBuffer;
        nameBufferMap["NewPositions"] = (positionBuffer == positionBufferA) ? positionBufferB : positionBufferA;

        nameBufferMap["OldVelocities"] = velocityBuffer;
        nameBufferMap["NewVelocities"] = (velocityBuffer == velocityBufferA) ? velocityBufferB : velocityBufferA;

        nameBufferMap["OldDensities"] = densityBuffer;
        nameBufferMap["NewDensities"] = (densityBuffer == densityBufferA) ? densityBufferB : densityBufferA;
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
        if (densityBufferA != null)
            densityBufferA.Release();
        if (positionBufferB != null)
            positionBufferB.Release();
        if (velocityBufferB != null)
            velocityBufferB.Release();
        if (densityBufferB != null)
            densityBufferB.Release();
        if (accelerationBuffer != null)
            accelerationBuffer.Release();
    }
}