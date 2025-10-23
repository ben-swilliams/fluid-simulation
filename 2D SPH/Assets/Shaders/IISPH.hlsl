float2 CalculateD(uint i) {
    float2 d = float2(0, 0);

    int2 gridPosI = GetGridPos(Positions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                if (i == j) continue;

                float2 offset = Positions[i] - Positions[j];
                
                d += (-particleMass / (Densities[i] * Densities[i])) * SpikyKernelGrad(offset);
            }
        }
    }

    return deltaTime * deltaTime * d;
}

float2 CalculateDeltaDensityAndA(uint i) {
    float deltaDensity = 0;
    float a = 0;

    int2 gridPosI = GetGridPos(Positions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                if (i == j) continue;

                float2 posOffset = Positions[i] - Positions[j];
                float2 velOffset = Velocities[i] - Velocities[j];
                float2 grad = Poly6KernelGrad(posOffset);

                deltaDensity += particleMass * dot(velOffset, grad);

                float d_ji = deltaTime * deltaTime * particleMass * grad / (Densities[i] * Densities[i]);

                a += particleMass * dot(Dii[i] - d_ji, grad);
            }
        }
    }

    return float2(deltaDensity, a);
}