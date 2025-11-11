float nearPressureMultiplier;

float3 CalculatePressureContribution(float3 offset, uint i, uint j) {
    float densityISq = max(Densities[i] * Densities[i], Epsilon);
    float densityJSq = max(Densities[j] * Densities[j], Epsilon);

    float pressureI = Pressures[i] / (densityISq);
    float pressureJ = Pressures[j] / (densityJSq);

    float nearPressureI = nearPressureMultiplier / Densities[instanceCount + i];
    float nearPressureJ = nearPressureMultiplier / Densities[instanceCount + j];

    float3 pressureForce = (pressureI + pressureJ) * CubicSplineGrad(offset);
    float3 nearPressureForce = (nearPressureI + nearPressureJ) * SpikyKernelGrad(offset);

    return -particleMass * pressureForce - particleMass * nearPressureMultiplier * nearPressureForce;
}