float NearDensityKernel(float2 offset) {
    float r = length(offset);

    if (r < 1e-7 || r > smoothingRadius) return 0;

    float inner = 1 - r / smoothingRadius;

    return inner * inner * inner;
}

float2 CalculateDensities(uint i) {
    float2 densities = float2(1e-7, 1e-7);

    int2 gridPosI = GetGridPos(Positions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                float2 offset = Positions[j] - Positions[i];
                densities.x += particleMass * Poly6Kernel(offset);
                densities.y += particleMass * NearDensityKernel(offset);
            }
        }
    }
    return densities;
}