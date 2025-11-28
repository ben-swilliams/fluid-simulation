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

    ComputeShader shader;

    public SpatialHashManager(ComputeShader spatialShader)
    {
        shader = spatialShader;
        FindKernels();
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

    public void ScanAndScatter(int binNumber)
    {
        shader.SetInt("tableSize", binNumber);

        int clearCountsGroupNum = Mathf.CeilToInt(binNumber / (float)Constants.threadGroupSize);
        shader.Dispatch(ClearCounts, clearCountsGroupNum, 1, 1);

        shader.Dispatch(Partition, Constants.threadGroupSize, 1, 1);

        HierarchicalScan(binNumber);

        shader.Dispatch(Scatter, Constants.threadGroupSize, 1, 1);
        shader.Dispatch(CopyBack, Constants.threadGroupSize, 1, 1);
    }

    void HierarchicalScan(int binNumber)
    {
        int numBlocks = Mathf.CeilToInt(binNumber / (float)Constants.scanBlockSize);

        // Phase 1: Local scan in each block (stores block sums in BlockSums buffer)
        shader.Dispatch(Scan, numBlocks, 1, 1);

        // Phase 2: If we have multiple blocks, scan the block sums themselves
        if (numBlocks > 1)
        {
            int numSuperBlocks = Mathf.CeilToInt(numBlocks / (float)Constants.scanBlockSize);

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
                int addSuperThreadGroups = Mathf.CeilToInt(numBlocks / (float)Constants.threadGroupSize);
                shader.Dispatch(AddSuperBlockSums, addSuperThreadGroups, 1, 1);
            }
        }

        // Phase 3: Add scanned block sums to each block's elements
        if (numBlocks > 1)
        {
            int addThreadGroups = Mathf.CeilToInt(binNumber / (float)Constants.threadGroupSize);
            shader.Dispatch(AddBlockSums, addThreadGroups, 1, 1);
        }

        // Phase 4: Write final element (total particle count)
        shader.Dispatch(FinalizeScan, 1, 1, 1);
    }
}