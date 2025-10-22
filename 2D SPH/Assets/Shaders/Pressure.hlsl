float stiffness;

float pressureKernelGradConstant;
float nearPressureKernelGradConstant;

float pressureMultiplier;
float nearPressureMultiplier;

float restDensity;

float2 PressureKernelGrad(float2 offset) {
    float r = length(offset);
    
    if (r < 1e-7 || r > smoothingRadius)
        return float2(0, 0);
    
    float inner = smoothingRadius - r;
    return pressureKernelGradConstant * inner * inner * offset / r;
}

float2 NearPressureKernelGrad(float2 offset) {
    float r = length(offset);

    if (r < 1e-7 || r > smoothingRadius)
        return float2(0, 0);

    float inner = 1 - r/smoothingRadius;

    return nearPressureKernelGradConstant * inner * inner * offset / r;
}

float CalculatePressure(uint i) {
    if (stiffness == 0) return 0;

    float inner = pow(Densities[i] / restDensity, stiffness) - 1;
    
    return pressureMultiplier * inner * (restDensity / stiffness);
}

float CalculateNearPressure(uint i) {
    return nearPressureMultiplier * NearDensities[i]; 
}

float2 CalculatePressureContribution(uint i, uint j) {
    float pressureI = CalculatePressure(i);
    float pressureJ = CalculatePressure(j);
    float2 offset = Positions[i] - Positions[j];

    float pressureSum = pressureI / (Densities[i] * Densities[i]) + pressureJ / (Densities[j] * Densities[j]);
    float2 pressureForce = particleMass * pressureSum * PressureKernelGrad(offset);

    return pressureForce;
}

float2 CalculateNearPressureContribution(uint i, uint j) {
    float nearPressureI = CalculateNearPressure(i);
    float nearPressureJ = CalculateNearPressure(j);
    float2 offset = Positions[i] - Positions[j];

    float2 nearPressureForce = particleMass * ((nearPressureI + nearPressureJ) / (2 * NearDensities[j])) * NearPressureKernelGrad(offset);

    return nearPressureForce;
}