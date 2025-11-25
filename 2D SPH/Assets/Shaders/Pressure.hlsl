float nearPressureMultiplier;

float3 CalculatePressureContribution(float3 offset, float3 grad, uint i, uint j) {
    float densityISq = max(Densities[3 * i] * Densities[3 * i], Epsilon);
    float densityJSq = max(Densities[3 * j] * Densities[3 * j], Epsilon);

    float pressureI = Pressures[i] / densityISq;
    float pressureJ = Pressures[j] / densityJSq;

    float nearPressureI = nearPressureMultiplier * Densities[3 * i + 1];
    float nearPressureJ = nearPressureMultiplier * Densities[3 * j + 1];

    float3 pressureForce = (pressureI + pressureJ) * grad;
    float3 nearPressureForce = (nearPressureI + nearPressureJ) * SpikyKernelGrad(offset, length(offset));

    return -particleMass * pressureForce - particleMass * nearPressureForce;
}