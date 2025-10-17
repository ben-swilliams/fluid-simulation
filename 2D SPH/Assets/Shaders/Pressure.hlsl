float pressureKernelGradConstant;

float gasConstant;
float restDensity;

float2 PressureKernelGrad(float2 offset) {
    float r = length(offset);
    
    if (r < 1e-7 || r > smoothingRadius)
        return float2(0, 0);
    
    float inner = smoothingRadius - r;
    return pressureKernelGradConstant * inner * inner * offset / r;
}

float CalculatePressure(uint i) {
    return gasConstant * (Densities[i] - restDensity);
}

float2 CalculatePressureContribution(uint i, uint j) {
    float pressureI = CalculatePressure(i);
    float pressureJ = CalculatePressure(j);
    float2 offset = Positions[i] - Positions[j];

    return particleMass * ((pressureI + pressureJ) / (2 * Densities[j])) * PressureKernelGrad(offset);
}