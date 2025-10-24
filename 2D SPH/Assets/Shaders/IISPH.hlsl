float restDensity;
float relaxationFactor;

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

                d += (particleMass / (Densities[i] * Densities[i])) * SpikyKernelGrad(offset);
            }
        }
    }

    return -deltaTime * deltaTime * d;
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
                float r = length(posOffset);

                float2 velOffset = Velocities[i] - Velocities[j];

                float2 densityGrad = Poly6KernelGrad(posOffset);
                float2 pressureGrad = SpikyKernelGrad(posOffset);

                deltaDensity += particleMass * dot(velOffset, densityGrad);

                float2 d_ji = deltaTime * deltaTime * particleMass * pressureGrad / (Densities[i] * Densities[i]);
        
                a += dot(Dii[i] - d_ji, pressureGrad);
            }
        }
    }

    a *= particleMass;

    return float2(deltaDensity, a);
}

float2 CalculatePressureSum(uint i) {
    float2 pressureSum = float2(0, 0);

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

                pressureSum += -particleMass * IterPressures[j] * SpikyKernelGrad(offset) / (Densities[j] * Densities[j]);
            }
        }
    }

    return deltaTime * deltaTime * pressureSum;
}

float CalculateNextPressure(uint i) {
    float pressureSum = 0;
    
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
                float2 pressureGrad = SpikyKernelGrad(offset);

                float2 d_ji = deltaTime * deltaTime * particleMass * pressureGrad / (Densities[i] * Densities[i]);

                float2 inner = DPSum[i] - Dii[j] * IterPressures[j] - (DPSum[j] - d_ji * IterPressures[i]);

                pressureSum +=  dot(inner, pressureGrad);
            }
        }
    }

    return (1 - relaxationFactor) * IterPressures[i] + (relaxationFactor / Aii[i]) * (restDensity - Densities[instanceCount + i] - particleMass * pressureSum); // Holy
}