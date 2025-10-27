static const uint SCAN_BLOCK_SIZE = 512;  // Each block processes 128 elements (64 threads × 2 elements)

groupshared uint temp[SCAN_BLOCK_SIZE];
RWStructuredBuffer<uint> BlockSums;

void PrefixScan(uint tid, uint blockIdx) {
    uint blockStart = blockIdx * SCAN_BLOCK_SIZE;

    uint idx1 = blockStart + 2 * tid;
    uint idx2 = idx1 + 1;
    temp[2 * tid] = (idx1 < tableSize) ? CellCounts[idx1] : 0;
    temp[2 * tid + 1] = (idx2 < tableSize) ? CellCounts[idx2] : 0;

    uint offset = 1;

    for (uint d = SCAN_BLOCK_SIZE >> 1; d > 0; d >>= 1)
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

    if (tid == 0)
    {
        BlockSums[blockIdx] = temp[SCAN_BLOCK_SIZE - 1];
        temp[SCAN_BLOCK_SIZE - 1] = 0;
    }

    for (uint e = 1; e < SCAN_BLOCK_SIZE; e *= 2)
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

    if (idx1 < tableSize) Offsets[idx1] = temp[2 * tid];
    if (idx2 < tableSize) Offsets[idx2] = temp[2 * tid + 1];
}

void AddBlockSums(uint gid) {
    if (gid >= tableSize) return;

    uint blockIdx = gid / SCAN_BLOCK_SIZE;
    if (blockIdx == 0) return;

    Offsets[gid] += BlockSums[blockIdx];
}

void ScanBlockSums(uint tid, uint numSums) {
    uint idx1 = 2 * tid;
    uint idx2 = idx1 + 1;
    temp[2 * tid] = (idx1 < numSums) ? BlockSums[idx1] : 0;
    temp[2 * tid + 1] = (idx2 < numSums) ? BlockSums[idx2] : 0;

    uint offset = 1;

    for (uint d = SCAN_BLOCK_SIZE >> 1; d > 0; d >>= 1)
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

    if (tid == 0)
    {
        temp[SCAN_BLOCK_SIZE - 1] = 0;
    }

    for (uint e = 1; e < SCAN_BLOCK_SIZE; e *= 2)
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

    if (idx1 < numSums) BlockSums[idx1] = temp[2 * tid];
    if (idx2 < numSums) BlockSums[idx2] = temp[2 * tid + 1];
}