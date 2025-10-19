float pressureKernelGradConstant;

float pressureMultiplier;
float restDensity;

float nearPressureMultiplier;

float2 PressureKernelGrad(float2 offset) {
    float r = length(offset);
    
    if (r < 1e-7 || r > smoothingRadius)
        return float2(0, 0);
    
    float inner = smoothingRadius - r;
    return pressureKernelGradConstant * inner * inner * offset / r;
}

float CalculatePressure(uint i) {
    return pressureMultiplier * (Densities[i] - restDensity);
}

float2 CalculatePressureForce(uint i) {
    float2 force = float2(0, 0);

    float pressureI = CalculatePressure(i);
    float nearPressureI = nearPressureMultiplier * NearDensities[i];

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

                float2 offset = Positions[j] - Positions[i];
                float r = length(offset);

                if (r > smoothingRadius || r < 1e-7) continue;

                float2 dir = offset / r;  // r̂_ij
                float pressureJ = CalculatePressure(j);
                float nearPressureJ = nearPressureMultiplier * NearDensities[j];

                // Apply equation 6 from paper
                float term1 = (pressureI + pressureJ) * (1.0 - r / smoothingRadius);
                float term2 = (nearPressureI + nearPressureJ) * pow(1.0 - r / smoothingRadius, 2);

                force += (term1 + term2) * dir;
            }
        }
    }

    return -force;  // Negative because force opposes displacement
}