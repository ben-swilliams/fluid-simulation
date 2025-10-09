float pressureKernelGradConstant;

float gasConstant;
float restDensity;

float2 PressureKernelGrad(float2 offset) {
    float r = length(offset);
    
    if (r < 0.0001 || r > smoothingRadius)
        return float2(0, 0);
    
    float inner = smoothingRadius - r;
    return pressureKernelGradConstant * inner * inner * offset / r;
}

float CalculatePressure(uint i) {
    return gasConstant * (Densities[i] - restDensity);
}

float2 CalculatePressureForce(uint i) {
    float2 pForce = float2(0, 0);

    float pressureI = CalculatePressure(i);

    int2 gridPosI = GetGridPos(PredictedPositions[i]);

    for (int x = -1; x < 2; x++) {
        for (int y = -1; y < 2; y++) {
            int2 gridPosJ = gridPosI + int2(x, y);
            if (!IsInBounds(gridPosJ)) continue;

            uint hash = CalculateHashFromGrid(gridPosJ);

            uint startIndex = Offsets[hash];
            uint endIndex = Offsets[hash + 1];

            for (uint j = startIndex; j < endIndex; j++) {
                float pressureJ = CalculatePressure(j);
                float2 offset = PredictedPositions[j] - PredictedPositions[i];

                pForce += particleMass * ((pressureI + pressureJ) / (2 * Densities[j])) * PressureKernelGrad(offset);
            }
        }
    }

    return -pForce / Densities[i];
}