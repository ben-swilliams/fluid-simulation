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
    float2 pForce = float2(0, 0);
    float2 nPForce = float2(0, 0);

    float pressureI = CalculatePressure(i);

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
                float pressureJ = CalculatePressure(j);
                float2 offset = Positions[j] - Positions[i];

                pForce += particleMass * ((pressureI + pressureJ) / (2 * Densities[j])) * PressureKernelGrad(offset);
            }
        }
    }

    return -pForce / Densities[i];
}