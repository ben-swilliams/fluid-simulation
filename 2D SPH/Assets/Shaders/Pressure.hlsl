float nearPressureMultiplier;

float3 CalculatePressureContribution(float3 offset, float3 grad, uint i, uint j, float densityI, float densityJ) {
    float densityISq = max(densityI * densityI, Epsilon);
    float densityJSq = max(densityJ * densityJ, Epsilon);

    float pressureI = Pressures[i] / densityISq;
    float pressureJ = Pressures[j] / densityJSq;

    float nearPressureI = nearPressureMultiplier * Densities[3 * i + 1];
    float nearPressureJ = nearPressureMultiplier * Densities[3 * j + 1];

    float3 pressureForce = (pressureI + pressureJ) * grad;
    float3 nearPressureForce = (nearPressureI + nearPressureJ) * SpikyKernelGrad(offset, length(offset));

    return -particleMass * pressureForce - particleMass * nearPressureForce;
}