float nearPressureMultiplier;

float3 CalculatePressureContribution(uint i, uint j) {
    float3 offset = Positions[i] - Positions[j];

    float pressureI = Pressures[i] / (Densities[i] * Densities[i]);
    float pressureJ = Pressures[j] / (Densities[j] * Densities[j]);

    float nearPressureI = nearPressureMultiplier / Densities[instanceCount + i];
    float nearPressureJ = nearPressureMultiplier / Densities[instanceCount + j];

    float3 pressureForce = (pressureI + pressureJ) * CubicSplineGrad(offset);
    float3 nearPressureForce = (nearPressureI + nearPressureJ) * SpikyKernelGrad(offset);

    return -particleMass * pressureForce - particleMass * nearPressureMultiplier * nearPressureForce;
}