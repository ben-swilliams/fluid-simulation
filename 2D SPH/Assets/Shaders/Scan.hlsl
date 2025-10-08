groupshared uint temp[128];
RWStructuredBuffer<uint> BlockSums;

void PrefixScan(uint tid, uint blockIdx) {
    uint n = 128;
    uint blockStart = blockIdx * 128;

    // Load data from global memory (offset by block)
    uint idx1 = blockStart + 2 * tid;
    uint idx2 = idx1 + 1;
    temp[2 * tid] = (idx1 < tableSize) ? CellCounts[idx1] : 0;
    temp[2 * tid + 1] = (idx2 < tableSize) ? CellCounts[idx2] : 0;

    uint offset = 1;

    for (uint d = n >> 1; d > 0; d >>= 1)
    {
        GroupMemoryBarrierWithGroupSync();

        if (tid < d)
        {
            uint ai = offset * (2 * tid + 1) - 1;
            uint bi = offset * (2 * tid + 2) - 1;
            temp[bi] += temp[ai];
        }
        offset *= 2;
    }

    // Store block sum and clear last element
    if (tid == 0)
    {
        BlockSums[blockIdx] = temp[n - 1];
        temp[n - 1] = 0;
    }

    for (uint e = 1; e < n; e *= 2)
    {
        offset >>= 1;
        GroupMemoryBarrierWithGroupSync();

        if (tid < e)
        {
            uint ai = offset * (2 * tid + 1) - 1;
            uint bi = offset * (2 * tid + 2) - 1;

            uint t = temp[ai];
            temp[ai] = temp[bi];
            temp[bi] += t;
        }
    }

    GroupMemoryBarrierWithGroupSync();

    // Write results back to global memory
    if (idx1 < tableSize) Offsets[idx1] = temp[2 * tid];
    if (idx2 < tableSize) Offsets[idx2] = temp[2 * tid + 1];
}

void AddBlockSums(uint gid) {
    if (gid >= tableSize) return;

    // Calculate which 128-element block this element belongs to
    uint blockIdx = gid / 128;
    if (blockIdx == 0) return;

    // After scanning BlockSums, BlockSums[i] contains sum of all blocks before block i
    Offsets[gid] += BlockSums[blockIdx];
}

// Scan the BlockSums themselves (for hierarchical recursion)
void ScanBlockSums(uint tid, uint numSums) {
    uint n = 128;

    // Load from BlockSums
    uint idx1 = 2 * tid;
    uint idx2 = idx1 + 1;
    temp[2 * tid] = (idx1 < numSums) ? BlockSums[idx1] : 0;
    temp[2 * tid + 1] = (idx2 < numSums) ? BlockSums[idx2] : 0;

    uint offset = 1;

    // Up-sweep
    for (uint d = n >> 1; d > 0; d >>= 1)
    {
        GroupMemoryBarrierWithGroupSync();
        if (tid < d)
        {
            uint ai = offset * (2 * tid + 1) - 1;
            uint bi = offset * (2 * tid + 2) - 1;
            temp[bi] += temp[ai];
        }
        offset *= 2;
    }

    // Clear last element for exclusive scan
    if (tid == 0)
    {
        temp[n - 1] = 0;
    }

    // Down-sweep
    for (uint e = 1; e < n; e *= 2)
    {
        offset >>= 1;
        GroupMemoryBarrierWithGroupSync();
        if (tid < e)
        {
            uint ai = offset * (2 * tid + 1) - 1;
            uint bi = offset * (2 * tid + 2) - 1;
            uint t = temp[ai];
            temp[ai] = temp[bi];
            temp[bi] += t;
        }
    }

    GroupMemoryBarrierWithGroupSync();

    // Write results back to BlockSums
    if (idx1 < numSums) BlockSums[idx1] = temp[2 * tid];
    if (idx2 < numSums) BlockSums[idx2] = temp[2 * tid + 1];
}