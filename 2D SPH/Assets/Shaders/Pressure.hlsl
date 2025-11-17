float nearPressureMultiplier;

float3 CalculatePressureContribution(float3 offset, float3 grad, uint i, uint j) {
    float densityISq = max(Densities[i] * Densities[i], Epsilon);
    float densityJSq = max(Densities[j] * Densities[j], Epsilon);

    float pressureI = Pressures[i] / densityISq;
    float pressureJ = Pressures[j] / densityJSq;

    // Near pressure proportional to near density (not inverse!)
    float nearPressureI = nearPressureMultiplier * Densities[instanceCount + i];
    float nearPressureJ = nearPressureMultiplier * Densities[instanceCount + j];

    float3 pressureForce = (pressureI + pressureJ) * grad;
    float3 nearPressureForce = (nearPressureI + nearPressureJ) * SpikyKernelGrad(offset, length(offset));

    // Don't multiply by nearPressureMultiplier again!
    return -particleMass * pressureForce - particleMass * nearPressureForce;
}