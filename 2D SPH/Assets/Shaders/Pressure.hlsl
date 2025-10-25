float3 CalculatePressureForce(uint i) {
    float3 pressureForce = float3(0, 0, 0);

    int3 gridPosI = GetGridPos(Positions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            for (int z = -1; z < 2; z++) {
                int3 gridPosJ = gridPosI + int3(x, y, z);
                if (!IsInBounds(gridPosJ)) continue;

                uint hash = CalculateHashFromGrid(gridPosJ);

                uint startIndex = Offsets[hash];
                uint endIndex = Offsets[hash + 1];

                float pressureI = Pressures[i] / (Densities[i] * Densities[i]);

                for (uint j = startIndex; j < endIndex; j++) {
                    if (i == j) continue;

                    float3 offset = Positions[i] - Positions[j];

                    float pressureJ = Pressures[j] / (Densities[j] * Densities[j]);

                    pressureForce += (pressureI + pressureJ) * CubicSplineGrad(offset);
                }
            }
        }
    }

    return -particleMass * particleMass * pressureForce;
}