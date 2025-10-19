float NearDensityKernel(float2 offset) {
    float inner = 1 - length(offset) / smoothingRadius;

    return inner * inner * inner;
}

float2 CalculateDensities(uint i) {
    float2 densities = float2(1e-7, 0);

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

                densities.x += particleMass * GeneralKernel(offset);

                densities.y += particleMass * NearDensityKernel(offset);
            }
        }
    }
    return densities;
}