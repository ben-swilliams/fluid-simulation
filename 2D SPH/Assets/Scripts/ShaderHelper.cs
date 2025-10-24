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
    ComputeBuffer nearDensityBuffer;
    ComputeBuffer intermediateAccelerationBuffer;
    ComputeBuffer diiBuffer;
    ComputeBuffer aiiBuffer;
    ComputeBuffer dpSumBuffer;
    ComputeBuffer iterPressureBuffer;
    ComputeBuffer pressureBuffer;

    // Maps
    Dictionary<int, string[]> kernelStaticBufferMap = new Dictionary<int, string[]>();
    Dictionary<int, string[]> kernelDynamicBufferMap = new Dictionary<int, string[]>();
    Dictionary<string, ComputeBuffer> nameBufferMap = new Dictionary<string, ComputeBuffer>();

    /*
    Public getters
    */
    public ComputeBuffer PositionBuffer => positionBuffer;
    public ComputeBuffer VelocityBuffer => velocityBuffer;

    public ComputeBuffer Densities => densityBuffer;
    public ComputeBuffer Pressures => pressureBuffer;

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
                      int intermediateAccelerationKernel,
                      int intermediateVelocityAndDKernel,
                      int intermediateDensityAndAKernel,
                      int zeroPressuresKernel,
                      int pressureSumIterationKernel,
                      int pressureConvergeIterationKernel,
                      int pressureFinaliseIterationKernel,
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
        kernelStaticBufferMap.Add(intermediateAccelerationKernel, new string[] { "Offsets", "IntermediateAccelerations", "Densities" });
        kernelStaticBufferMap.Add(intermediateVelocityAndDKernel, new string[] { "IntermediateAccelerations", "Dii", "Offsets", "Densities" });
        kernelStaticBufferMap.Add(intermediateDensityAndAKernel, new string[] { "Densities", "Offsets", "Dii", "Aii" });
        kernelStaticBufferMap.Add(zeroPressuresKernel, new string[] { "IterPressures" });
        kernelStaticBufferMap.Add(pressureSumIterationKernel, new string[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures" });
        kernelStaticBufferMap.Add(pressureConvergeIterationKernel, new string[] { "Offsets", "Densities", "Dii", "Aii", "DPSum", "IterPressures", "Pressures"});
        kernelStaticBufferMap.Add(pressureFinaliseIterationKernel, new string[] { "Pressures", "IterPressures" });
        kernelStaticBufferMap.Add(velocityKernel, new string[] { "Densities", "Offsets", "Pressures" });
        kernelStaticBufferMap.Add(positionKernel, new string[] { "Densities", "Offsets" });

        kernelDynamicBufferMap.Add(partitionKernel, new string[] { "Positions" });
        kernelDynamicBufferMap.Add(densityKernel, new string[] {  "Positions" });
        kernelDynamicBufferMap.Add(scatterKernel, new string[] {"OldVelocities", "NewVelocities",
                                                                 "OldPositions", "NewPositions" });
        kernelDynamicBufferMap.Add(intermediateAccelerationKernel, new string[] { "Positions", "Velocities" });
        kernelDynamicBufferMap.Add(intermediateVelocityAndDKernel, new string[] { "Positions", "Velocities" });
        kernelDynamicBufferMap.Add(intermediateDensityAndAKernel, new string[] { "Positions", "Velocities" });
        kernelDynamicBufferMap.Add(pressureSumIterationKernel, new string[] { "Positions" });
        kernelDynamicBufferMap.Add(pressureConvergeIterationKernel, new string[] { "Positions" });
        kernelDynamicBufferMap.Add(velocityKernel, new string[] { "Velocities", "Positions" });
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

        // Stores both advanced density and density (density first half, advanced second half)
        densityBuffer = new ComputeBuffer(instanceCount * 2, sizeof(float));
        nearDensityBuffer = new ComputeBuffer(instanceCount, sizeof(float));

        intermediateAccelerationBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);

        diiBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);
        aiiBuffer = new ComputeBuffer(instanceCount, sizeof(float));
        dpSumBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);

        iterPressureBuffer = new ComputeBuffer(instanceCount, sizeof(float));
        pressureBuffer = new ComputeBuffer(instanceCount, sizeof(float));

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
        if (intermediateAccelerationBuffer != null)
            intermediateAccelerationBuffer.Release();
        if (diiBuffer != null)
            diiBuffer.Release();
        if (aiiBuffer != null)
            aiiBuffer.Release();
        if (dpSumBuffer != null)
            dpSumBuffer.Release();
        if (iterPressureBuffer != null)
            iterPressureBuffer.Release();
        if (pressureBuffer != null)
            pressureBuffer.Release();
    }
}