#nullable enable

using System.Collections.Generic;
using UnityEngine;

public class SpatialHashManager
{
    int ClearCounts;
    int Partition;
    int Scatter;
    int CopyBack;
    int Scan;
    int ScanBlockSums;
    int ScanSuperBlockSums;
    int AddSuperBlockSums;
    int AddBlockSums;
    int FinalizeScan;

    int binNumber;
    int threadCount;

    ComputeShader shader;
    BufferHelper bufferHelper;

    public BufferHelper Buffers => bufferHelper;

    public SpatialHashManager(ComputeShader spatialShader, Dictionary<string, ComputeBuffer> externalBuffers, int binNumber, int instanceCount)
    {
        shader = spatialShader;
        threadCount = Mathf.CeilToInt(instanceCount / (float)Common.Constants.threadGroupSize);

        this.binNumber = binNumber;

        shader.SetInt("tableSize", this.binNumber);
        shader.SetInt("instanceCount", instanceCount);

        FindKernels();

        Dictionary<int, string[]> dependencies = new Dictionary<int, string[]>
        {
            { ClearCounts, new[] { "CellCounts", "LocalOffsets" } },
            { Partition, new[] { "CellCounts", "Positions" } },
            { Scan, new[] { "Offsets", "CellCounts", "BlockSums" } },
            { ScanBlockSums, new[] { "BlockSums", "SuperBlockSums" } },
            { ScanSuperBlockSums, new[] { "SuperBlockSums" } },
            { AddSuperBlockSums, new[] { "BlockSums", "SuperBlockSums" } },
            { AddBlockSums, new[] { "Offsets", "CellCounts", "BlockSums" } },
            { FinalizeScan, new[] { "Offsets" } },
            { Scatter, new[] { "LocalOffsets", "Offsets", "Velocities", "SortedVelocities", "Positions", "SortedPositions" } },
            { CopyBack, new[] { "SortedVelocities", "SortedPositions", "Velocities", "Positions" }},
        };

        Dictionary<string, BufferInfo> bufferInfo = GenerateBufferInfo(binNumber, instanceCount);

        bufferHelper = new BufferHelper(shader, dependencies, bufferInfo, externalBuffers);
    }

    Dictionary<string, BufferInfo> GenerateBufferInfo(int binNumber, int instanceCount)
    {
        Dictionary<string, BufferInfo> bufferInfo = GenerateBinDependentBufferInfo(binNumber);

        bufferInfo.Add("SortedVelocities", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) * 3});
        bufferInfo.Add("SortedPositions", new BufferInfo { Length = instanceCount, ElementSize = sizeof(float) * 3});
        
        return bufferInfo;
    }

    Dictionary<string, BufferInfo> GenerateBinDependentBufferInfo(int binNumber)
    {
        // Calculate number of blocks needed for hierarchical scan
        int numBlocks = Mathf.CeilToInt(binNumber / (float)Common.Constants.scanBlockSize);

        // Calculate number of super blocks needed for three-level scan
        int numSuperBlocks = Mathf.CeilToInt(numBlocks / (float)Common.Constants.scanBlockSize);

        Dictionary<string, BufferInfo> bufferInfo = new Dictionary<string, BufferInfo>
        {
            { "CellCounts", new BufferInfo { Length = binNumber, ElementSize = sizeof(uint) } },
            { "LocalOffsets", new BufferInfo { Length = binNumber, ElementSize = sizeof(uint) } },
            { "Offsets", new BufferInfo { Length = binNumber + 1, ElementSize = sizeof(uint) } },
            { "BlockSums", new BufferInfo { Length = Mathf.Max(1, numBlocks), ElementSize = sizeof(uint) }},
            { "SuperBlockSums", new BufferInfo { Length = Mathf.Max(1, numSuperBlocks), ElementSize = sizeof(uint)}},
        };

        return bufferInfo;
    }

    void FindKernels()
    {
        ClearCounts = shader.FindKernel("ClearCounts");
        Partition = shader.FindKernel("Partition");
        Scatter = shader.FindKernel("Scatter");
        CopyBack = shader.FindKernel("CopyBack");
        Scan = shader.FindKernel("Scan");
        ScanBlockSums = shader.FindKernel("ScanBlockSums");
        ScanSuperBlockSums = shader.FindKernel("ScanSuperBlockSums");
        AddSuperBlockSums = shader.FindKernel("AddSuperBlockSums");
        AddBlockSums = shader.FindKernel("AddBlockSums");
        FinalizeScan = shader.FindKernel("FinalizeScan");
    }

    void HierarchicalScan(int binNumber)
    {
        int numBlocks = Mathf.CeilToInt(binNumber / (float)Common.Constants.scanBlockSize);

        // Phase 1: Local scan in each block (stores block sums in BlockSums buffer)
        shader.Dispatch(Scan, numBlocks, 1, 1);

        // Phase 2: If we have multiple blocks, scan the block sums themselves
        if (numBlocks > 1)
        {
            int numSuperBlocks = Mathf.CeilToInt(numBlocks / (float)Common.Constants.scanBlockSize);

            shader.SetInt("numBlockSums", numBlocks);
            shader.SetInt("numSuperBlocks", numSuperBlocks);

            shader.Dispatch(ScanBlockSums, numSuperBlocks, 1, 1);

            // Phase 2.5: If we have multiple super-blocks, scan them (three-level scan)
            if (numSuperBlocks > 1)
            {
                shader.Dispatch(ScanSuperBlockSums, 1, 1, 1);
            }

            // Phase 2.75: Add scanned super-block sums to BlockSums
            if (numSuperBlocks > 1)
            {
                int addSuperThreadGroups = Mathf.CeilToInt(numBlocks / (float)Common.Constants.threadGroupSize);
                shader.Dispatch(AddSuperBlockSums, addSuperThreadGroups, 1, 1);
            }
        }

        // Phase 3: Add scanned block sums to each block's elements
        if (numBlocks > 1)
        {
            int addThreadGroups = Mathf.CeilToInt(binNumber / (float)Common.Constants.threadGroupSize);
            shader.Dispatch(AddBlockSums, addThreadGroups, 1, 1);
        }

        // Phase 4: Write final element (total particle count)
        shader.Dispatch(FinalizeScan, 1, 1, 1);
    }

    public bool ScanAndScatter(int binNumber)
    {
        bool binCountChanged = false;

        if (this.binNumber != binNumber) {
            shader.SetInt("tableSize", binNumber);
            bufferHelper.UpdateBuffers(GenerateBinDependentBufferInfo(binNumber));
            binCountChanged = true;

            this.binNumber = binNumber;
        }

        int clearCountsGroupNum = Mathf.CeilToInt(binNumber / (float)Common.Constants.threadGroupSize);
        shader.Dispatch(ClearCounts, clearCountsGroupNum, 1, 1);

        shader.Dispatch(Partition, threadCount, 1, 1);

        HierarchicalScan(binNumber);

        shader.Dispatch(Scatter, threadCount, 1, 1);
        shader.Dispatch(CopyBack, threadCount, 1, 1);

        return binCountChanged;
    }

    public void Destroy()
    {
        bufferHelper.Destroy();
    }
}