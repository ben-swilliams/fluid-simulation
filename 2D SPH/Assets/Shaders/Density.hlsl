float2 CalculateDensities(uint i) {
    float density = Epsilon;
    float nearDensity = Epsilon;

    int3 gridPosI = GetGridPos(Positions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            for (int z = -1; z < 2; z++) {
                int3 gridPosJ = gridPosI + int3(x, y, z);
                if (!IsInBounds(gridPosJ)) continue;

                uint hash = CalculateHashFromGrid(gridPosJ);

                uint startIndex = Offsets[hash];
                uint endIndex = Offsets[hash + 1];

                for (uint j = startIndex; j < endIndex; j++) {
                    float3 offset = Positions[j] - Positions[i];
                    density += particleMass * CubicSplineKernel(offset);
                    nearDensity += particleMass * SpikyKernel(offset);
                }
            }
        }
    }

    return float2(density, nearDensity);
}