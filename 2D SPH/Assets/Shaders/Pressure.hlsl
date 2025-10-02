float pressureKernelConstant;
float pressureKernelGradConstant;

float gasConstant;
float restDensity;

float PressureKernel(float2 offset) {
    if (dot(offset, offset) > smoothingRadius * smoothingRadius) return 0;

    float r = length(offset);
    float inner = smoothingRadius - r;

    return pressureKernelConstant * inner * inner * inner;
}

float2 PressureKernelGrad(float2 offset) {
    float r = length(offset);
    
    if (r < 0.0001 || r > smoothingRadius) {
        return float2(0, 0);
    }
    
    float inner = smoothingRadius - r;
    return pressureKernelGradConstant * inner * inner * offset / r;
}

float CalculatePressure(uint i) {
    return gasConstant * (Densities[i] - restDensity);
}

float2 CalculatePressureForce(uint i) {
    float2 pForce = float2(0, 0);

    float pressureI = CalculatePressure(i);

    for (uint j = 0; j < instanceCount; j++) {
        float pressureJ = CalculatePressure(j);
        float2 offset = Positions[i] - Positions[j];

        pForce += ((pressureI + pressureJ) / (2 * Densities[j])) * PressureKernelGrad(offset);
    }

    return pForce / Densities[i];
}