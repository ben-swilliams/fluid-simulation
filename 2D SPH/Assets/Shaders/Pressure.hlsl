float2 CalculatePressureForce(uint i) {
    float2 pressureForce = float2(0, 0);

    int2 gridPosI = GetGridPos(Positions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            float pressureI = Pressures[i] / (Densities[i] * Densities[i]);

            for (uint j = startIndex; j < endIndex; j++) {
                if (i == j) continue;

                float2 offset = Positions[i] - Positions[j];

                float pressureJ = Pressures[j] / (Densities[j] * Densities[j]);

                pressureForce += (pressureI + pressureJ) * SpikyKernelGrad(offset);
            }
        }
    }

    return -pressureForce;
}