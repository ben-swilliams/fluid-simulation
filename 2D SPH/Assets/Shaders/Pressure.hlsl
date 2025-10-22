float stiffness;

float pressureMultiplier;
float nearPressureMultiplier;

float restDensity;


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
    float2 pressureForce = particleMass * pressureSum * SpikyKernelGrad(offset);

    return pressureForce;
}

float2 CalculateNearPressureContribution(uint i, uint j) {
    float nearPressureI = CalculateNearPressure(i);
    float nearPressureJ = CalculateNearPressure(j);
    float2 offset = Positions[i] - Positions[j];

    float2 nearPressureForce = particleMass * ((nearPressureI + nearPressureJ) / (2 * NearDensities[j])) * NearPressureKernelGrad(offset);

    return nearPressureForce;
}